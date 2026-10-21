using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States.MainMenu;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.UI.Notify;
using UnityEngine;

namespace PaperPixelGames.Core.Reporting
{
	public class ReportManager : Singleton<ReportManager>
	{
		private const string FeedbackCooldownPrefsKey = "Report.Feedback.LastSubmitUtcTicks";

		private const string BugCooldownPrefsKey = "Report.Bug.LastSubmitUtcTicks";

		[SerializeField]
		private BaseScreen _reportScreen;

		[SerializeField]
		private PlayFabBootstrap _playfab;

		[Header("Services")]
		[SerializeField]
		private PlayFabAuthService _authService;

		[SerializeField]
		private PlayFabReportService _reportService;

		[SerializeField]
		private ReportValidationService _validationService;

		[SerializeField]
		private ScreenshotCaptureService _screenshotService;

		[Header("Cooldown")]
		[SerializeField]
		private float _feedbackCooldownSeconds = 300f;

		[SerializeField]
		private float _bugCooldownSeconds = 300f;

		[Header("Screenshot")]
		[SerializeField]
		private int _screenshotMaxLongestSide = 1280;

		[SerializeField]
		[Range(1f, 100f)]
		private int _screenshotJpgQuality = 70;

		[SerializeField]
		private List<GameObject> _disableOnScreenshot;

		private IImageCompressionService _compression;

		private PlayFabFileService _fileService;

		private bool _screenInitialized;

		private CancellationTokenSource _submissionCancellation;

		private readonly Dictionary<GameObject, bool> _screenshotTargetStates = new Dictionary<GameObject, bool>();

		private bool _restoreScreenshotTargets;

		private CanvasGroup _captureHiddenCanvasGroup;

		public bool IsSubmitting { get; private set; }

		public PlayFabReportAuthState AuthState
		{
			get
			{
				if (!(_authService != null))
				{
					return PlayFabReportAuthState.Uninitialized;
				}
				return _authService.State;
			}
		}

		public float FeedbackCooldownRemaining => GetCooldownRemaining("Report.Feedback.LastSubmitUtcTicks", _feedbackCooldownSeconds);

		public float BugCooldownRemaining => GetCooldownRemaining("Report.Bug.LastSubmitUtcTicks", _bugCooldownSeconds);

		public bool IsFeedbackOnCooldown => FeedbackCooldownRemaining > 0f;

		public bool IsBugOnCooldown => BugCooldownRemaining > 0f;

		protected override void Awake()
		{
			base.Awake();
			_compression = new ImageCompressionService();
			_fileService = new PlayFabFileService(_authService);
		}

		private void OnDestroy()
		{
			_submissionCancellation?.Cancel();
			_submissionCancellation?.Dispose();
			_submissionCancellation = null;
		}

		public void PlayfabInit()
		{
			InitializeReportScreenOnce();
			TryInitializePlayFab(notifyOnFailure: false);
		}

		public void ShowReportScreen()
		{
			ShowReportScreen(allowScreenshot: false);
		}

		public void ShowReportScreen(bool allowScreenshot, CanvasGroup captureHiddenCanvasGroup = null)
		{
			_captureHiddenCanvasGroup = (allowScreenshot ? captureHiddenCanvasGroup : null);
			InitializeReportScreenOnce();
			TryInitializePlayFab(notifyOnFailure: true);
			if (_reportScreen == null)
			{
				Singleton<NotifyManager>.Instance?.ShowToast("ServiceMissing", "Report screen is missing.");
				Debug.LogError("[ReportManager] Report screen is missing.");
				return;
			}
			if (_reportScreen is ReportUIScreen reportUIScreen)
			{
				reportUIScreen.SetScreenshotOptionAvailable(allowScreenshot);
			}
			_reportScreen.Show();
		}

		public void HideReportScreen()
		{
			_restoreScreenshotTargets = false;
			_captureHiddenCanvasGroup = null;
			_reportScreen?.Hide();
		}

		public void SubmitFeedback(FeedbackReportData data, Action<ReportSubmitResult> callback)
		{
			if (data == null)
			{
				NotifySubmitResult(new ReportSubmitResult(ReportSubmitStatus.ValidationFailed, "Feedback data is null."), callback);
				return;
			}
			SubmitReport(new ReportSubmissionData
			{
				Type = ReportSubmissionType.Feedback,
				Category = data.Category,
				Message = data.Message,
				Rating = data.Rating
			}, callback);
		}

		public void SubmitBug(BugReportData data, Action<ReportSubmitResult> callback)
		{
			if (data == null)
			{
				NotifySubmitResult(new ReportSubmitResult(ReportSubmitStatus.ValidationFailed, "Bug data is null."), callback);
				return;
			}
			SubmitReport(new ReportSubmissionData
			{
				Type = ReportSubmissionType.Bug,
				Category = data.Category,
				Message = data.Message,
				Rating = data.Rating,
				IncludeScreenshot = data.IncludeScreenshot
			}, callback);
		}

		private void SubmitReport(ReportSubmissionData data, Action<ReportSubmitResult> callback)
		{
			SubmitReportAsync(data, callback);
		}

		private async Task SubmitReportAsync(ReportSubmissionData data, Action<ReportSubmitResult> callback)
		{
			if (IsSubmitting)
			{
				NotifySubmitResult(new ReportSubmitResult(ReportSubmitStatus.AlreadySubmitting, "A report is already being submitted."), callback);
				return;
			}
			if (!HasRequiredServices())
			{
				NotifySubmitResult(new ReportSubmitResult(ReportSubmitStatus.ServiceMissing, "Report services are missing."), callback);
				return;
			}
			if (!TryInitializePlayFab(notifyOnFailure: false))
			{
				NotifySubmitResult(new ReportSubmitResult(ReportSubmitStatus.ServiceMissing, "PlayFab report services could not be initialized."), callback);
				return;
			}
			if (IsOnCooldown(data.Type, out var message))
			{
				NotifySubmitResult(new ReportSubmitResult(ReportSubmitStatus.CooldownActive, message), callback);
				return;
			}
			data.Message = data.Message?.Trim();
			if (!_validationService.ValidateSubmission(data, out var message2))
			{
				NotifySubmitResult(new ReportSubmitResult(ReportSubmitStatus.ValidationFailed, message2), callback);
				return;
			}
			IsSubmitting = true;
			_submissionCancellation?.Cancel();
			_submissionCancellation?.Dispose();
			_submissionCancellation = new CancellationTokenSource();
			CancellationToken cancellationToken = _submissionCancellation.Token;
			ReportSubmitResult submitResult = new ReportSubmitResult(ReportSubmitStatus.Failed, "Report submit failed.", ReportSubmitFailureReason.Unknown);
			try
			{
				if (!(await _authService.EnsureAuthenticatedAsync(isRetry: true, forceRefresh: false, cancellationToken)))
				{
					submitResult = new ReportSubmitResult(ReportSubmitStatus.NotAuthenticated, BuildAuthMessage(), ReportSubmitFailureReason.Authentication);
					return;
				}
				PopulateRuntimeFields(data);
				if (ShouldTryScreenshot(data))
				{
					await TryAttachScreenshotAsync(data, cancellationToken);
				}
				submitResult = await _reportService.SendReportAsync(data, cancellationToken);
				if (submitResult.IsAuthenticationFailure)
				{
					_authService.InvalidateAuthentication("PlayFab rejected the entity authentication context.");
					if (await _authService.EnsureAuthenticatedAsync(isRetry: true, forceRefresh: true, cancellationToken))
					{
						PopulateIdentityFields(data);
						submitResult = await _reportService.SendReportAsync(data, cancellationToken);
					}
				}
				if (!submitResult.IsSuccess)
				{
					await TryCleanupFailedScreenshotAsync(data, submitResult);
				}
				else
				{
					RecordCooldown(data.Type);
				}
			}
			catch (OperationCanceledException)
			{
				submitResult = new ReportSubmitResult(ReportSubmitStatus.Failed, "Report submission was cancelled.", ReportSubmitFailureReason.Cancelled);
				await TryCleanupFailedScreenshotAsync(data, submitResult);
			}
			catch (Exception arg)
			{
				Debug.LogError($"[ReportManager] SubmitReport exception: {arg}");
				submitResult = new ReportSubmitResult(ReportSubmitStatus.Failed, "Report submit failed.", ReportSubmitFailureReason.Unknown);
				await TryCleanupFailedScreenshotAsync(data, submitResult);
			}
			finally
			{
				IsSubmitting = false;
				_submissionCancellation?.Dispose();
				_submissionCancellation = null;
				NotifySubmitResult(submitResult, callback);
			}
		}

		private void InitializeReportScreenOnce()
		{
			if (!_screenInitialized)
			{
				if (_reportScreen == null)
				{
					Debug.LogError("[ReportManager] Report screen is missing.");
					return;
				}
				_reportScreen.Initialize();
				_screenInitialized = true;
			}
		}

		private bool TryInitializePlayFab(bool notifyOnFailure)
		{
			if (_authService != null && _authService.IsInitialized)
			{
				return true;
			}
			if (_playfab == null)
			{
				if (notifyOnFailure)
				{
					Singleton<NotifyManager>.Instance?.ShowToast("ServiceMissing", "PlayFab bootstrap is missing.");
				}
				Debug.LogError("[ReportManager] PlayFabBootstrap is missing.");
				return false;
			}
			bool num = _playfab.Initialize();
			if (!num & notifyOnFailure)
			{
				NotifyManager instance = Singleton<NotifyManager>.Instance;
				if ((object)instance == null)
				{
					return num;
				}
				instance.ShowToast("ServiceMissing", "PlayFab report services could not be initialized.");
			}
			return num;
		}

		private bool HasRequiredServices()
		{
			if (_authService != null && _reportService != null)
			{
				return _validationService != null;
			}
			return false;
		}

		private string BuildAuthMessage()
		{
			if (_authService == null)
			{
				return "Player authentication service is missing.";
			}
			return _authService.State switch
			{
				PlayFabReportAuthState.Authenticating => "Player authentication is still in progress.", 
				PlayFabReportAuthState.Retrying => "Player authentication is being retried.", 
				PlayFabReportAuthState.Failed => "Player authentication failed. Please check Steam connection.", 
				_ => "Player is not authenticated.", 
			};
		}

		private bool IsOnCooldown(ReportSubmissionType type, out string message)
		{
			float num = ((type == ReportSubmissionType.Bug) ? BugCooldownRemaining : FeedbackCooldownRemaining);
			if (num <= 0f)
			{
				message = string.Empty;
				return false;
			}
			message = ((type == ReportSubmissionType.Bug) ? $"Bug report is on cooldown. Try again in {Mathf.CeilToInt(num)} seconds." : $"Feedback is on cooldown. Try again in {Mathf.CeilToInt(num)} seconds.");
			return true;
		}

		private void RecordCooldown(ReportSubmissionType type)
		{
			PlayerPrefs.SetString((type == ReportSubmissionType.Bug) ? "Report.Bug.LastSubmitUtcTicks" : "Report.Feedback.LastSubmitUtcTicks", DateTime.UtcNow.Ticks.ToString());
			PlayerPrefs.Save();
		}

		private float GetCooldownRemaining(string key, float durationSeconds)
		{
			if (durationSeconds <= 0f)
			{
				return 0f;
			}
			string text = PlayerPrefs.GetString(key, string.Empty);
			if (string.IsNullOrWhiteSpace(text) || !long.TryParse(text, out var result))
			{
				return 0f;
			}
			DateTime dateTime = new DateTime(result, DateTimeKind.Utc);
			double totalSeconds = (DateTime.UtcNow - dateTime).TotalSeconds;
			return Mathf.Max(0f, durationSeconds - (float)totalSeconds);
		}

		private void PopulateRuntimeFields(ReportSubmissionData data)
		{
			data.BuildVersion = ReleaseVersion.Value;
			data.Platform = Application.platform.ToString();
			data.ReportId = (string.IsNullOrWhiteSpace(data.ReportId) ? Guid.NewGuid().ToString("N") : data.ReportId);
			PopulateIdentityFields(data);
			data.HasScreenshot = false;
			data.ScreenshotFileName = null;
			data.ScreenshotEntityId = null;
			data.ScreenshotEntityType = null;
			data.ScreenshotError = string.Empty;
			data.ScreenshotByteSize = 0;
		}

		private void PopulateIdentityFields(ReportSubmissionData data)
		{
			data.PlayFabId = _authService.PlayFabId;
			data.EntityId = _authService.EntityId;
			data.EntityType = _authService.EntityType;
		}

		private void NotifySubmitResult(ReportSubmitResult result, Action<ReportSubmitResult> callback)
		{
			Singleton<NotifyManager>.Instance?.ShowToast(result.Status.ToString(), result.Message);
			callback?.Invoke(result);
		}

		private bool ShouldTryScreenshot(ReportSubmissionData data)
		{
			if (data == null || data.Type != ReportSubmissionType.Bug || !data.IncludeScreenshot)
			{
				return false;
			}
			if (_screenshotService == null)
			{
				data.ScreenshotError = "missing_capture_service";
				Debug.LogWarning("[ReportManager] Screenshot requested but ScreenshotCaptureService is missing.");
				return false;
			}
			if (_compression == null)
			{
				data.ScreenshotError = "missing_compression_service";
				Debug.LogWarning("[ReportManager] Screenshot requested but ImageCompressionService is missing.");
				return false;
			}
			if (_fileService == null)
			{
				data.ScreenshotError = "missing_file_service";
				Debug.LogWarning("[ReportManager] Screenshot requested but PlayFabFileService is missing.");
				return false;
			}
			return true;
		}

		private async Task TryAttachScreenshotAsync(ReportSubmissionData data, CancellationToken cancellationToken)
		{
			Texture2D screenshotTexture = null;
			try
			{
				screenshotTexture = await CaptureScreenshotWithoutUiAsync();
				cancellationToken.ThrowIfCancellationRequested();
				if (screenshotTexture == null)
				{
					data.ScreenshotError = "capture_null";
					Debug.LogWarning("[ReportManager] Screenshot capture returned null.");
					return;
				}
				byte[] array = _compression.CompressToJpg(screenshotTexture, _screenshotMaxLongestSide, _screenshotJpgQuality);
				cancellationToken.ThrowIfCancellationRequested();
				if (array == null || array.Length == 0)
				{
					data.ScreenshotError = "compression_empty";
					Debug.LogWarning("[ReportManager] Screenshot compression returned empty bytes.");
					return;
				}
				data.ScreenshotByteSize = array.Length;
				Debug.Log("[ReportManager] Screenshot prepared." + $"\n   SizeKb: {(float)array.Length / 1024f:0.0}" + $"\n   MaxLongestSide: {_screenshotMaxLongestSide}" + $"\n   JpgQuality: {_screenshotJpgQuality}");
				string fileName = BuildScreenshotFileName(data.ReportId);
				ScreenshotUploadResult screenshotUploadResult = await _fileService.UploadScreenshotAsync(array, fileName, cancellationToken);
				if (!screenshotUploadResult.Success)
				{
					data.ScreenshotError = "upload_failed";
					Debug.LogWarning("[ReportManager] Screenshot upload failed: " + screenshotUploadResult.ErrorMessage);
					return;
				}
				data.HasScreenshot = true;
				data.ScreenshotFileName = screenshotUploadResult.FileName;
				data.ScreenshotEntityId = screenshotUploadResult.EntityId;
				data.ScreenshotEntityType = screenshotUploadResult.EntityType;
				data.ScreenshotError = string.Empty;
				Debug.Log("[ReportManager] Screenshot upload success.\n   FileName: " + data.ScreenshotFileName + "\n   EntityId: " + data.ScreenshotEntityId + "\n   EntityType: " + data.ScreenshotEntityType);
			}
			catch (OperationCanceledException)
			{
				data.ScreenshotError = "pipeline_cancelled";
				throw;
			}
			catch (Exception ex2)
			{
				data.ScreenshotError = "pipeline_exception";
				Debug.LogWarning("[ReportManager] Screenshot pipeline failed: " + ex2.Message);
			}
			finally
			{
				if (screenshotTexture != null)
				{
					UnityEngine.Object.Destroy(screenshotTexture);
				}
			}
		}

		private async Task<Texture2D> CaptureScreenshotWithoutUiAsync()
		{
			CanvasGroup captureHiddenCanvasGroup = _captureHiddenCanvasGroup;
			float previousAlpha = ((captureHiddenCanvasGroup != null) ? captureHiddenCanvasGroup.alpha : 1f);
			SetScreenshotHidden(hidden: true);
			if (captureHiddenCanvasGroup != null)
			{
				captureHiddenCanvasGroup.alpha = 0f;
			}
			try
			{
				return await _screenshotService.CaptureAsync();
			}
			finally
			{
				if (captureHiddenCanvasGroup != null)
				{
					captureHiddenCanvasGroup.alpha = previousAlpha;
				}
				SetScreenshotHidden(hidden: false);
			}
		}

		private void SetScreenshotHidden(bool hidden)
		{
			if (_disableOnScreenshot == null)
			{
				return;
			}
			if (hidden)
			{
				_screenshotTargetStates.Clear();
				_restoreScreenshotTargets = true;
			}
			for (int i = 0; i < _disableOnScreenshot.Count; i++)
			{
				GameObject gameObject = _disableOnScreenshot[i];
				if (!(gameObject == null))
				{
					bool value;
					if (hidden)
					{
						_screenshotTargetStates[gameObject] = gameObject.activeSelf;
						gameObject.SetActive(value: false);
					}
					else if (_screenshotTargetStates.TryGetValue(gameObject, out value))
					{
						gameObject.SetActive(_restoreScreenshotTargets & value);
					}
				}
			}
			if (!hidden)
			{
				_screenshotTargetStates.Clear();
			}
		}

		private async Task TryCleanupFailedScreenshotAsync(ReportSubmissionData data, ReportSubmitResult submitResult)
		{
			if (data != null && data.HasScreenshot && !string.IsNullOrWhiteSpace(data.ScreenshotFileName))
			{
				if (submitResult.FailureReason == ReportSubmitFailureReason.Timeout)
				{
					Debug.LogWarning("[ReportManager] Screenshot cleanup deferred because telemetry completion is uncertain.\n   ReportId: " + data.ReportId + "\n   FileName: " + data.ScreenshotFileName);
				}
				else if (await _fileService.DeleteScreenshotAsync(data.ScreenshotFileName, CancellationToken.None))
				{
					Debug.Log("[ReportManager] Screenshot rolled back after report failure.\n   ReportId: " + data.ReportId + "\n   FileName: " + data.ScreenshotFileName);
					data.HasScreenshot = false;
					data.ScreenshotFileName = null;
					data.ScreenshotEntityId = null;
					data.ScreenshotEntityType = null;
				}
			}
		}

		private string BuildScreenshotFileName(string reportId)
		{
			string text = (string.IsNullOrWhiteSpace(reportId) ? Guid.NewGuid().ToString("N") : reportId);
			return "bug_" + text + ".jpg";
		}
	}
}
