using System.Collections.Generic;
using DG.Tweening;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace PaperPixelGames.Framework.UI
{
	public sealed class LoadingScreenBehaviour : MonoBehaviour
	{
		private enum LoadingScreenState
		{
			Hidden = 0,
			Showing = 1,
			Visible = 2,
			Hiding = 3
		}

		[Header("UI References")]
		[SerializeField]
		private Canvas _canvas;

		[SerializeField]
		private Animator _transitionAnimator;

		[SerializeField]
		private GameObject _root;

		[SerializeField]
		private CanvasGroup _group;

		[SerializeField]
		private RectTransform _contentRoot;

		[Header("Texts")]
		[SerializeField]
		private TextMeshProUGUI _titleText;

		[SerializeField]
		private TextMeshProUGUI _statusText;

		[SerializeField]
		private TextMeshProUGUI _hintText;

		[Header("Image")]
		[SerializeField]
		private Image _randomImage;

		[Header("Random Content")]
		[SerializeField]
		private List<string> _hints;

		[SerializeField]
		private List<string> _hintLocalizationKeys;

		[SerializeField]
		private List<Sprite> _images;

		[Header("Random Content Tuning")]
		[SerializeField]
		private bool _useRandomHints = true;

		[SerializeField]
		private bool _useRandomImages = true;

		[SerializeField]
		[Min(0.25f)]
		private float _hintChangeInterval = 3f;

		[SerializeField]
		[Min(0.25f)]
		private float _imageChangeInterval = 4f;

		[SerializeField]
		private bool _changeHintImmediatelyOnShow = true;

		[SerializeField]
		private bool _changeImageImmediatelyOnShow = true;

		[SerializeField]
		private bool _avoidImmediateHintRepeat = true;

		[SerializeField]
		private bool _avoidImmediateImageRepeat = true;

		[Header("Hide Delay")]
		[SerializeField]
		[Min(0f)]
		private float _hideDelaySeconds = 1f;

		[Header("Tween Config")]
		[SerializeField]
		[Min(0f)]
		private float _fadeDuration = 0.15f;

		[SerializeField]
		[Min(0f)]
		private float _scaleDuration = 0.15f;

		[SerializeField]
		private Ease _fadeEase = Ease.OutQuad;

		[SerializeField]
		private Ease _scaleEase = Ease.OutBack;

		[SerializeField]
		private Ease _hideScaleEase = Ease.InQuad;

		[SerializeField]
		[Range(0.85f, 1f)]
		private float _hiddenScale = 0.95f;

		[Header("Debug")]
		[SerializeField]
		private bool _logDebug;

		private int _showCount;

		private int _lastHintIndex = -1;

		private int _lastImageIndex = -1;

		private string _titleLocalizationKey;

		private string _titleFallback;

		private string _statusLocalizationKey;

		private string _statusFallback;

		private Tween _fadeTween;

		private Tween _scaleTween;

		private Tween _hideDelayTween;

		private Tween _hintLoopTween;

		private Tween _imageLoopTween;

		private LoadingScreenState _state;

		private Button _cancelButton;

		private TextMeshProUGUI _cancelLabel;

		private MultiplayerCoordinator _cancelCoordinator;

		private long _cancelVersion;

		private bool _cancelCursorOwned;

		private bool _developmentCancelEnabled;

		private bool _returnCoverActive;

		private long _returnCoverVersion;

		private static readonly int OpenTransitionState = Animator.StringToHash("Base Layer.LiquidTransitionEnter");

		[SerializeField]
		private string _debugTitle = "Loading...";

		[SerializeField]
		private string _debugDescription = "Preparing game world...";

		[SerializeField]
		private bool _debugShowImmediate;

		public bool IsVisible
		{
			get
			{
				if (_state != LoadingScreenState.Visible)
				{
					return _state == LoadingScreenState.Showing;
				}
				return true;
			}
		}

		private void Awake()
		{
			if (LoadingScreen.HasInstance)
			{
				Debug.LogWarning("[LoadingScreenBehaviour] Duplicate instance detected. Destroying the new one.", this);
				Object.Destroy(base.gameObject);
				return;
			}
			Object.DontDestroyOnLoad(base.gameObject);
			LoadingScreen.Register(this);
			LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
			ValidateReferences();
			if (_transitionAnimator != null)
			{
				_transitionAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
			}
			EnsureCancelControl();
			SetVisibleImmediate(visible: false);
		}

		private void OnDestroy()
		{
			if (_cancelButton != null)
			{
				_cancelButton.onClick.RemoveListener(OnCancelClicked);
			}
			KillAllTweens();
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			LoadingScreen.Unregister(this);
		}

		private void EnsureCancelControl()
		{
			EnsureCancelControlForBuild(Application.isEditor || Debug.isDebugBuild);
		}

		private void EnsureCancelControlForBuild(bool developmentBuild)
		{
			_developmentCancelEnabled = developmentBuild;
			if (!_developmentCancelEnabled)
			{
				RefreshCancelControl();
			}
			else if (!(_cancelButton != null) && !(_canvas == null) && !(_root == null))
			{
				if (_canvas.GetComponent<GraphicRaycaster>() == null)
				{
					_canvas.gameObject.AddComponent<GraphicRaycaster>();
				}
				GameObject gameObject = new GameObject("CancelClientJoin", typeof(RectTransform), typeof(Image), typeof(Button));
				gameObject.layer = _root.layer;
				RectTransform rectTransform = (RectTransform)gameObject.transform;
				rectTransform.SetParent(_root.transform, worldPositionStays: false);
				Vector2 anchorMin = (rectTransform.anchorMax = new Vector2(1f, 0f));
				rectTransform.anchorMin = anchorMin;
				rectTransform.pivot = new Vector2(1f, 0f);
				rectTransform.anchoredPosition = new Vector2(-40f, 40f);
				rectTransform.sizeDelta = new Vector2(260f, 64f);
				Image component = gameObject.GetComponent<Image>();
				component.color = new Color(0.16f, 0.18f, 0.22f, 0.98f);
				_cancelButton = gameObject.GetComponent<Button>();
				_cancelButton.targetGraphic = component;
				_cancelButton.onClick.AddListener(OnCancelClicked);
				GameObject gameObject2 = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
				gameObject2.layer = _root.layer;
				RectTransform obj = (RectTransform)gameObject2.transform;
				obj.SetParent(rectTransform, worldPositionStays: false);
				obj.anchorMin = Vector2.zero;
				obj.anchorMax = Vector2.one;
				obj.offsetMin = new Vector2(12f, 6f);
				obj.offsetMax = new Vector2(-12f, -6f);
				_cancelLabel = gameObject2.GetComponent<TextMeshProUGUI>();
				if (_titleText != null && _titleText.font != null)
				{
					_cancelLabel.font = _titleText.font;
				}
				_cancelLabel.alignment = TextAlignmentOptions.Center;
				_cancelLabel.enableAutoSizing = true;
				_cancelLabel.fontSizeMin = 16f;
				_cancelLabel.fontSizeMax = 28f;
				_cancelLabel.color = Color.white;
				_cancelLabel.raycastTarget = false;
				RefreshCancelLabel();
				gameObject.SetActive(value: false);
			}
		}

		private void RefreshCancelLabel()
		{
			if (_cancelLabel != null)
			{
				_cancelLabel.text = GetLocalizedText("ui.popup.cancel", "Cancel");
			}
		}

		private void Update()
		{
			if (!_developmentCancelEnabled)
			{
				return;
			}
			RefreshCancelControl();
			if (!(_cancelButton == null) && _cancelButton.gameObject.activeSelf)
			{
				if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
				{
					OnCancelClicked();
				}
				EventSystem current = EventSystem.current;
				Mouse current2 = Mouse.current;
				if ((current == null || !current.isActiveAndEnabled || current.currentInputModule == null) && current2 != null && current2.leftButton.wasReleasedThisFrame && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)_cancelButton.transform, current2.position.ReadValue(), (_canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : _canvas.worldCamera))
				{
					OnCancelClicked();
				}
			}
		}

		private void RefreshCancelControl()
		{
			if (_cancelButton == null)
			{
				return;
			}
			bool flag = _developmentCancelEnabled && IsVisible && Singleton<MultiplayerCoordinator>.TryGetInstance(out _cancelCoordinator) && _cancelCoordinator.CanCancelClientJoin;
			_cancelButton.gameObject.SetActive(flag);
			_cancelButton.interactable = flag;
			if (flag)
			{
				_cancelVersion = _cancelCoordinator.ClientCancellationVersion;
				_cancelCursorOwned = true;
				if (!CursorHelper.CursorVisible)
				{
					CursorHelper.CursorVisible = true;
				}
			}
			else if (_cancelCursorOwned)
			{
				if (_cancelCoordinator != null && _cancelCoordinator.SessionPhase == MultiplayerSessionPhase.Running)
				{
					CursorHelper.CursorVisible = false;
				}
				_cancelCursorOwned = false;
			}
		}

		private void OnCancelClicked()
		{
			if (_developmentCancelEnabled && IsVisible && !(_cancelCoordinator == null) && _cancelCoordinator.CancelClientJoin(_cancelVersion))
			{
				_cancelButton.interactable = false;
				_cancelButton.gameObject.SetActive(value: false);
			}
		}

		public long BeginReturnCover()
		{
			if (_returnCoverActive)
			{
				return _returnCoverVersion;
			}
			KillAllTweens();
			_showCount = 1;
			UpdateLocalizedText("loading.title.leaving_session", "Leaving Session", "loading.status.returning_main_menu", "Returning to main menu...");
			_returnCoverActive = true;
			_returnCoverVersion++;
			SetVisibleImmediate(visible: true);
			if (_transitionAnimator != null && _transitionAnimator.runtimeAnimatorController != null)
			{
				_transitionAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
				_transitionAnimator.ResetTrigger("Open");
				_transitionAnimator.ResetTrigger("Close");
				if (_transitionAnimator.HasState(0, OpenTransitionState))
				{
					_transitionAnimator.Play(OpenTransitionState, 0, 1f);
					_transitionAnimator.Update(0f);
				}
			}
			StartRandomContentLoops();
			Canvas.ForceUpdateCanvases();
			MultiplayerDiagnostics.Record("return_cover_acquired", $"version={_returnCoverVersion}", ulong.MaxValue);
			return _returnCoverVersion;
		}

		public void EndReturnCover(long version)
		{
			if (_returnCoverActive && version == _returnCoverVersion)
			{
				_returnCoverActive = false;
				_showCount = 0;
				StopRandomContentLoops();
				if (_group != null)
				{
					_group.blocksRaycasts = false;
					_group.interactable = false;
				}
				ExecuteHideVisuals();
				MultiplayerDiagnostics.Record("return_cover_released", $"version={version}", ulong.MaxValue);
			}
		}

		public void Push(string title, string description)
		{
			if (!_returnCoverActive)
			{
				_showCount++;
				UpdateText(title, description);
				HandlePush();
			}
		}

		public void PushLocalized(string titleKey, string titleFallback, string descriptionKey, string descriptionFallback)
		{
			if (!_returnCoverActive)
			{
				_showCount++;
				UpdateLocalizedText(titleKey, titleFallback, descriptionKey, descriptionFallback);
				HandlePush();
			}
		}

		private void HandlePush()
		{
			if (_logDebug)
			{
				Debug.Log($"[LoadingScreenBehaviour] Push | ShowCount={_showCount} | State={_state}", this);
			}
			if (_showCount == 1)
			{
				ShowInternal();
			}
		}

		public void Pop()
		{
			if (_returnCoverActive)
			{
				return;
			}
			if (_showCount <= 0)
			{
				_showCount = 0;
				if (_logDebug)
				{
					Debug.LogWarning("[LoadingScreenBehaviour] Pop called while ShowCount was already 0.", this);
				}
				return;
			}
			_showCount--;
			if (_logDebug)
			{
				Debug.Log($"[LoadingScreenBehaviour] Pop | ShowCount={_showCount} | State={_state}", this);
			}
			if (_showCount == 0)
			{
				HideInternal();
			}
		}

		public void ForceHide()
		{
			if (!_returnCoverActive)
			{
				if (_logDebug)
				{
					Debug.Log($"[LoadingScreenBehaviour] ForceHide | PrevShowCount={_showCount} | State={_state}", this);
				}
				_showCount = 0;
				HideInternal();
			}
		}

		public void UpdateText(string title, string description)
		{
			if (!_returnCoverActive && IsAlive())
			{
				ClearLocalizedTextState();
				if (_titleText != null)
				{
					_titleText.text = title ?? string.Empty;
				}
				if (_statusText != null)
				{
					_statusText.text = description ?? string.Empty;
				}
			}
		}

		public void UpdateLocalizedText(string titleKey, string titleFallback, string descriptionKey, string descriptionFallback)
		{
			if (!_returnCoverActive && IsAlive())
			{
				_titleLocalizationKey = titleKey;
				_titleFallback = titleFallback;
				_statusLocalizationKey = descriptionKey;
				_statusFallback = descriptionFallback;
				RefreshLocalizedText();
			}
		}

		private void RefreshLocalizedText()
		{
			if (_titleText != null)
			{
				_titleText.text = GetLocalizedText(_titleLocalizationKey, _titleFallback);
			}
			if (_statusText != null)
			{
				_statusText.text = GetLocalizedText(_statusLocalizationKey, _statusFallback);
			}
		}

		private static string GetLocalizedText(string key, string fallback)
		{
			// The recovered localization bundles retain the original wording.
			if (key == "loading.status.waiting_for_players" && LocalizationSettings.SelectedLocale != null &&
				LocalizationSettings.SelectedLocale.Identifier.Code == "zh-CN")
			{
				return "加载中";
			}
			if (fallback == null)
			{
				fallback = string.Empty;
			}
			if (!string.IsNullOrWhiteSpace(key))
			{
				return LocalizationDatabase.GetStringOrFallback("UiTable", key, fallback);
			}
			return fallback;
		}

		private void ClearLocalizedTextState()
		{
			_titleLocalizationKey = null;
			_titleFallback = null;
			_statusLocalizationKey = null;
			_statusFallback = null;
		}

		private void ShowInternal()
		{
			if (!IsAlive())
			{
				return;
			}
			KillHideDelayTween();
			if (_state == LoadingScreenState.Visible || _state == LoadingScreenState.Showing)
			{
				return;
			}
			_state = LoadingScreenState.Showing;
			if (_canvas != null)
			{
				_canvas.enabled = true;
			}
			if (_root != null)
			{
				_root.SetActive(value: true);
			}
			if (_group != null)
			{
				_group.blocksRaycasts = true;
				_group.interactable = true;
			}
			KillVisualTweens();
			if (_group != null)
			{
				_group.alpha = 0f;
				_fadeTween = _group.DOFade(1f, _fadeDuration).SetEase(_fadeEase).SetUpdate(isIndependentUpdate: true)
					.OnComplete(() =>
					{
						if (IsAlive())
						{
							_state = LoadingScreenState.Visible;
						}
					});
			}
			else
			{
				_state = LoadingScreenState.Visible;
			}
			if (_contentRoot != null)
			{
				_contentRoot.localScale = Vector3.one * _hiddenScale;
				_scaleTween = _contentRoot.DOScale(1f, _scaleDuration).SetEase(_scaleEase).SetUpdate(isIndependentUpdate: true);
			}
			if (_transitionAnimator != null)
			{
				_transitionAnimator.SetTrigger("Open");
			}
			StartRandomContentLoops();
		}

		private void HideInternal(bool forceImmediate = false)
		{
			if (!_returnCoverActive && IsAlive())
			{
				StopRandomContentLoops();
				if (_group != null)
				{
					_group.blocksRaycasts = false;
					_group.interactable = false;
				}
				if (_transitionAnimator != null)
				{
					_transitionAnimator.SetTrigger("Close");
				}
				if (forceImmediate || _hideDelaySeconds <= 0f)
				{
					ExecuteHideVisuals();
					return;
				}
				_state = LoadingScreenState.Hiding;
				KillHideDelayTween();
				_hideDelayTween = DOVirtual.DelayedCall(_hideDelaySeconds, ExecuteHideVisuals);
			}
		}

		private void ExecuteHideVisuals()
		{
			if (_returnCoverActive || !IsAlive())
			{
				return;
			}
			_state = LoadingScreenState.Hiding;
			KillHideDelayTween();
			KillVisualTweens();
			if (_group != null)
			{
				_fadeTween = _group.DOFade(0f, _fadeDuration).SetEase(_fadeEase).SetUpdate(isIndependentUpdate: true)
					.OnComplete(() =>
					{
						if (!_returnCoverActive && IsAlive())
						{
							if (_root != null)
							{
								_root.SetActive(value: false);
							}
							if (_canvas != null)
							{
								_canvas.enabled = false;
							}
							_state = LoadingScreenState.Hidden;
						}
					});
			}
			else
			{
				if (_root != null)
				{
					_root.SetActive(value: false);
				}
				if (_canvas != null)
				{
					_canvas.enabled = false;
				}
				_state = LoadingScreenState.Hidden;
			}
			if (_contentRoot != null)
			{
				_scaleTween = _contentRoot.DOScale(_hiddenScale, _scaleDuration).SetEase(_hideScaleEase).SetUpdate(isIndependentUpdate: true);
			}
		}

		private void StartRandomContentLoops()
		{
			StopRandomContentLoops();
			if (_useRandomHints && _hintText != null && HasValidHints())
			{
				if (_changeHintImmediatelyOnShow)
				{
					ApplyRandomHint();
				}
				_hintLoopTween = DOVirtual.DelayedCall(_hintChangeInterval, OnHintLoopTick).SetLoops(-1, LoopType.Restart);
			}
			if (_useRandomImages && _randomImage != null && HasValidImages())
			{
				if (_changeImageImmediatelyOnShow)
				{
					ApplyRandomImage();
				}
				_imageLoopTween = DOVirtual.DelayedCall(_imageChangeInterval, OnImageLoopTick).SetLoops(-1, LoopType.Restart);
			}
		}

		private void StopRandomContentLoops()
		{
			_hintLoopTween?.Kill();
			_hintLoopTween = null;
			_imageLoopTween?.Kill();
			_imageLoopTween = null;
		}

		private void OnHintLoopTick()
		{
			if (IsAlive() && (_state == LoadingScreenState.Visible || _state == LoadingScreenState.Showing))
			{
				ApplyRandomHint();
			}
		}

		private void OnImageLoopTick()
		{
			if (IsAlive() && (_state == LoadingScreenState.Visible || _state == LoadingScreenState.Showing))
			{
				ApplyRandomImage();
			}
		}

		private void ApplyRandomHint()
		{
			if (!(_hintText == null) && HasValidHints())
			{
				int randomIndex = GetRandomIndex(_hints.Count, _lastHintIndex, _avoidImmediateHintRepeat);
				if (randomIndex >= 0)
				{
					_lastHintIndex = randomIndex;
					ApplyHint(randomIndex);
				}
			}
		}

		private void ApplyHint(int index)
		{
			if (!(_hintText == null) && index >= 0 && index < _hints.Count)
			{
				string text = _hints[index] ?? string.Empty;
				string text2 = ((_hintLocalizationKeys != null && index < _hintLocalizationKeys.Count) ? _hintLocalizationKeys[index] : string.Empty);
				_hintText.text = (string.IsNullOrWhiteSpace(text2) ? text : LocalizationDatabase.GetString("UiTable", text2, text));
			}
		}

		private void OnSelectedLocaleChanged(Locale _)
		{
			RefreshCancelLabel();
			if (_state != LoadingScreenState.Hidden)
			{
				if (!string.IsNullOrWhiteSpace(_titleLocalizationKey) || !string.IsNullOrWhiteSpace(_statusLocalizationKey))
				{
					RefreshLocalizedText();
				}
				if (_lastHintIndex >= 0)
				{
					ApplyHint(_lastHintIndex);
				}
			}
		}

		private void ApplyRandomImage()
		{
			if (!(_randomImage == null) && HasValidImages())
			{
				int randomIndex = GetRandomIndex(_images.Count, _lastImageIndex, _avoidImmediateImageRepeat);
				if (randomIndex >= 0)
				{
					_lastImageIndex = randomIndex;
					Sprite sprite = _images[randomIndex];
					_randomImage.sprite = sprite;
					_randomImage.enabled = sprite != null;
				}
			}
		}

		private bool HasValidHints()
		{
			if (_hints != null)
			{
				return _hints.Count > 0;
			}
			return false;
		}

		private bool HasValidImages()
		{
			if (_images != null)
			{
				return _images.Count > 0;
			}
			return false;
		}

		private int GetRandomIndex(int count, int lastIndex, bool avoidImmediateRepeat)
		{
			if (count <= 0)
			{
				return -1;
			}
			if (count == 1)
			{
				return 0;
			}
			if (!avoidImmediateRepeat)
			{
				return Random.Range(0, count);
			}
			int num = Random.Range(0, count);
			if (num == lastIndex)
			{
				num = (num + 1) % count;
			}
			return num;
		}

		private void KillAllTweens()
		{
			KillHideDelayTween();
			KillVisualTweens();
			StopRandomContentLoops();
		}

		private void KillHideDelayTween()
		{
			_hideDelayTween?.Kill();
			_hideDelayTween = null;
		}

		private void KillVisualTweens()
		{
			_fadeTween?.Kill();
			_fadeTween = null;
			_scaleTween?.Kill();
			_scaleTween = null;
		}

		private void SetVisibleImmediate(bool visible)
		{
			if (_canvas != null)
			{
				_canvas.enabled = visible;
			}
			if (_root != null)
			{
				_root.SetActive(visible);
			}
			if (_group != null)
			{
				_group.alpha = (visible ? 1f : 0f);
				_group.blocksRaycasts = visible;
				_group.interactable = visible;
			}
			if (_contentRoot != null)
			{
				_contentRoot.localScale = (visible ? Vector3.one : (Vector3.one * _hiddenScale));
			}
			_state = (visible ? LoadingScreenState.Visible : LoadingScreenState.Hidden);
		}

		private bool IsAlive()
		{
			if (this != null)
			{
				return base.gameObject != null;
			}
			return false;
		}

		private void ValidateReferences()
		{
			if (_group == null)
			{
				Debug.LogError("[LoadingScreenBehaviour] CanvasGroup reference is missing.", this);
			}
			if (_root == null)
			{
				Debug.LogWarning("[LoadingScreenBehaviour] Root reference is missing.", this);
			}
			if (_titleText == null)
			{
				Debug.LogWarning("[LoadingScreenBehaviour] Title text reference is missing.", this);
			}
			if (_statusText == null)
			{
				Debug.LogWarning("[LoadingScreenBehaviour] Status text reference is missing.", this);
			}
			if (_hintText == null && _useRandomHints)
			{
				Debug.LogWarning("[LoadingScreenBehaviour] Hint text reference is missing but random hints are enabled.", this);
			}
			if (_randomImage == null && _useRandomImages)
			{
				Debug.LogWarning("[LoadingScreenBehaviour] Random image reference is missing but random images are enabled.", this);
			}
		}
	}
}
