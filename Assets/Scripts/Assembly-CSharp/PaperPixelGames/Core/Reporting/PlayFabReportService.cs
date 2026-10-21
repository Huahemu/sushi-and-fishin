using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PlayFab;
using PlayFab.EventsModels;
using UnityEngine;

namespace PaperPixelGames.Core.Reporting
{
	public class PlayFabReportService : MonoBehaviour
	{
		private const int TelemetryBlockSizeBytes = 1024;

		private const int TelemetryTimeoutSeconds = 20;

		public void SendFeedback(FeedbackReportData data, Action<ReportSubmitResult> callback)
		{
			SendReport(new ReportSubmissionData
			{
				Type = ReportSubmissionType.Feedback,
				Category = data.Category,
				Message = data.Message,
				Rating = data.Rating,
				BuildVersion = ReleaseVersion.Value,
				Platform = Application.platform.ToString(),
				ReportId = Guid.NewGuid().ToString("N")
			}, callback);
		}

		public void SendBug(BugReportData data, Action<ReportSubmitResult> callback)
		{
			SendReport(new ReportSubmissionData
			{
				Type = ReportSubmissionType.Bug,
				Category = data.Category,
				Message = data.Message,
				Rating = data.Rating,
				IncludeScreenshot = data.IncludeScreenshot,
				HasScreenshot = data.HasScreenshot,
				ScreenshotFileName = data.ScreenshotFileName,
				ScreenshotEntityId = data.ScreenshotEntityId,
				ScreenshotEntityType = data.ScreenshotEntityType,
				BuildVersion = ReleaseVersion.Value,
				Platform = Application.platform.ToString(),
				ReportId = Guid.NewGuid().ToString("N")
			}, callback);
		}

		public void SendReport(ReportSubmissionData data, Action<ReportSubmitResult> callback)
		{
			SendReportAndNotifyAsync(data, callback);
		}

		public async Task<ReportSubmitResult> SendReportAsync(ReportSubmissionData data, CancellationToken cancellationToken)
		{
			if (data == null)
			{
				return new ReportSubmitResult(ReportSubmitStatus.ValidationFailed, "Report data is null.");
			}
			Dictionary<string, object> payload = BuildPayload(data);
			int num = EstimateBilledTelemetryBlocks(payload);
			string text = ((data.Type == ReportSubmissionType.Bug) ? "bug_report_submitted" : "feedback_submitted");
			Debug.Log("[PlayFabReportService] Sending report telemetry.\n   ReportId: " + data.ReportId + $"\n   Type: {data.Type}" + "\n   EventName: " + text + $"\n   EstimatedBilledKbBlocks: {num}" + $"\n   ScreenshotBytes: {data.ScreenshotByteSize}");
			TaskCompletionSource<ReportSubmitResult> completion = new TaskCompletionSource<ReportSubmitResult>(TaskCreationOptions.RunContinuationsAsynchronously);
			PlayFabEventsAPI.WriteTelemetryEvents(new WriteEventsRequest
			{
				Events = new List<EventContents>
				{
					new EventContents
					{
						EventNamespace = "custom",
						Name = text,
						OriginalId = data.ReportId,
						OriginalTimestamp = DateTime.UtcNow,
						Payload = payload
					}
				}
			}, (WriteEventsResponse _) =>
			{
				completion.TrySetResult(new ReportSubmitResult(ReportSubmitStatus.Success, "Report submitted."));
			}, (PlayFabError error) =>
			{
				completion.TrySetResult(BuildFailureResult(error));
			});
			try
			{
				ReportSubmitResult result = await AwaitWithTimeout(completion.Task, 20, cancellationToken);
				if (result.IsSuccess)
				{
					Debug.Log("[PlayFabReportService] Report telemetry sent. ReportId: " + data.ReportId);
				}
				else
				{
					Debug.LogError("[PlayFabReportService] Report telemetry failed. ReportId: " + data.ReportId);
				}
				return result;
			}
			catch (OperationCanceledException)
			{
				return new ReportSubmitResult(ReportSubmitStatus.Failed, "Report submission was cancelled.", ReportSubmitFailureReason.Cancelled);
			}
			catch (TimeoutException)
			{
				return new ReportSubmitResult(ReportSubmitStatus.Failed, "Report submission timed out.", ReportSubmitFailureReason.Timeout);
			}
		}

		private async Task SendReportAndNotifyAsync(ReportSubmissionData data, Action<ReportSubmitResult> callback)
		{
			ReportSubmitResult obj = await SendReportAsync(data, CancellationToken.None);
			callback?.Invoke(obj);
		}

		private static ReportSubmitResult BuildFailureResult(PlayFabError error)
		{
			bool flag = error != null && (error.Error == PlayFabErrorCode.EntityTokenExpired || error.Error == PlayFabErrorCode.AuthTokenExpired || error.Error == PlayFabErrorCode.NotAuthenticated);
			string text = ((error != null) ? error.GenerateErrorReport() : "Unknown PlayFab error.");
			Debug.LogError("[PlayFabReportService] " + text);
			return new ReportSubmitResult(ReportSubmitStatus.Failed, flag ? "Player authentication expired. Retrying authentication." : "Report submit failed.", flag ? ReportSubmitFailureReason.Authentication : ReportSubmitFailureReason.Network);
		}

		private static Dictionary<string, object> BuildPayload(ReportSubmissionData data)
		{
			return new Dictionary<string, object>
			{
				{
					"reportId",
					data.ReportId ?? string.Empty
				},
				{
					"type",
					data.Type.ToString()
				},
				{
					"category",
					data.Category.ToString()
				},
				{
					"message",
					data.Message?.Trim() ?? string.Empty
				},
				{ "rating", data.Rating },
				{
					"buildVersion",
					data.BuildVersion ?? ReleaseVersion.Value
				},
				{
					"platform",
					data.Platform ?? Application.platform.ToString()
				},
				{
					"playFabId",
					data.PlayFabId ?? string.Empty
				},
				{
					"entityId",
					data.EntityId ?? string.Empty
				},
				{
					"entityType",
					data.EntityType ?? string.Empty
				},
				{ "includeScreenshot", data.IncludeScreenshot },
				{ "hasScreenshot", data.HasScreenshot },
				{
					"screenshotFileName",
					data.ScreenshotFileName ?? string.Empty
				},
				{
					"screenshotEntityId",
					data.ScreenshotEntityId ?? string.Empty
				},
				{
					"screenshotEntityType",
					data.ScreenshotEntityType ?? string.Empty
				},
				{
					"screenshotError",
					data.ScreenshotError ?? string.Empty
				},
				{ "screenshotByteSize", data.ScreenshotByteSize }
			};
		}

		private static int EstimateBilledTelemetryBlocks(Dictionary<string, object> payload)
		{
			int num = 0;
			foreach (KeyValuePair<string, object> item in payload)
			{
				num += Encoding.UTF8.GetByteCount(item.Key);
				num += Encoding.UTF8.GetByteCount(item.Value?.ToString() ?? string.Empty);
				num += 8;
			}
			return Mathf.Max(1, Mathf.CeilToInt((float)num / 1024f));
		}

		private static async Task<ReportSubmitResult> AwaitWithTimeout(Task<ReportSubmitResult> task, int timeoutSeconds, CancellationToken cancellationToken)
		{
			Task task2 = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);
			if (await Task.WhenAny(task, task2) == task)
			{
				return await task;
			}
			cancellationToken.ThrowIfCancellationRequested();
			throw new TimeoutException();
		}
	}
}
