using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Michsky.MUIP;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Settings;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PaperPixelGames.Framework.UI.Notify
{
	public class NotifyManager : Singleton<NotifyManager>
	{
		private readonly struct ToastRequest
		{
			public readonly string Message;

			public readonly NotificationState State;

			public readonly float Duration;

			public readonly bool RestaurantLog;

			public ToastRequest(string message, NotificationState state, float duration, bool restaurantLog = false)
			{
				Message = message;
				State = state;
				Duration = duration;
				RestaurantLog = restaurantLog;
			}
		}

		private enum ProgressFeedbackFormat : byte
		{
			Text = 0,
			Money = 1,
			Scrap = 2,
			Xp = 3,
			OrderItem = 4
		}

		private sealed class ProgressFeedbackRequest
		{
			public readonly string MergeKey;

			public int Amount;

			public readonly string PrimaryText;

			public readonly string SecondaryText;

			public readonly Sprite Icon;

			public readonly Sprite Badge;

			public readonly Color AccentColor;

			public readonly float Duration;

			public readonly ProgressFeedbackFormat Format;

			public readonly bool IsPriority;

			public float LastUpdateTime;

			public bool CanMerge
			{
				get
				{
					if (Amount > 0)
					{
						return !string.IsNullOrWhiteSpace(MergeKey);
					}
					return false;
				}
			}

			public ProgressFeedbackRequest(string mergeKey, int amount, string primaryText, string secondaryText, Sprite icon, Sprite badge, Color accentColor, float duration, ProgressFeedbackFormat format, bool isPriority)
			{
				MergeKey = mergeKey ?? string.Empty;
				Amount = amount;
				PrimaryText = primaryText ?? string.Empty;
				SecondaryText = secondaryText ?? string.Empty;
				Icon = icon;
				Badge = badge;
				AccentColor = accentColor;
				Duration = duration;
				Format = format;
				IsPriority = isPriority;
				LastUpdateTime = Time.unscaledTime;
			}
		}

		[Header("Toast")]
		[SerializeField]
		private float _toastDuration = 2f;

		[SerializeField]
		private float _toastDestroyDelay = 0.5f;

		[SerializeField]
		private bool _toastUseUnscaledTime = true;

		[SerializeField]
		private int _maxVisibleToasts = 5;

		[SerializeField]
		private Transform _stackTransform;

		[SerializeField]
		private Notification _notifcationPrefab;

		[Header("Progress Feed")]
		[SerializeField]
		private Transform _progressFeedbackRoot;

		[SerializeField]
		private ProgressFeedbackItem _progressFeedbackPrefab;

		[SerializeField]
		[Min(1f)]
		private int _maxVisibleProgressFeedback = 3;

		[SerializeField]
		[Min(1f)]
		private int _maxPendingProgressFeedback = 8;

		[SerializeField]
		[Min(0f)]
		private float _progressFeedbackMergeWindow = 0.75f;

		[SerializeField]
		[Min(0.1f)]
		private float _resourceProgressFeedbackDuration = 2.2f;

		[SerializeField]
		[Min(0.1f)]
		private float _orderProgressFeedbackDuration = 4.2f;

		[SerializeField]
		[Min(0.1f)]
		private float _milestoneProgressFeedbackDuration = 3f;

		[Header("Progress Feed Icons")]
		[SerializeField]
		private Sprite _moneyProgressFeedbackIcon;

		[SerializeField]
		private Sprite _scrapProgressFeedbackIcon;

		[SerializeField]
		private Sprite _xpProgressFeedbackIcon;

		[SerializeField]
		private Sprite _habitatProgressFeedbackIcon;

		[SerializeField]
		private Sprite _upgradeProgressFeedbackBadge;

		[Header("Progress Feed Tool Icons")]
		[SerializeField]
		private Sprite _divingEquipmentProgressFeedbackIcon;

		[SerializeField]
		private Sprite _harpoonProgressFeedbackIcon;

		[SerializeField]
		private Sprite _fishingRodProgressFeedbackIcon;

		[SerializeField]
		private Sprite _mopProgressFeedbackIcon;

		[SerializeField]
		private Sprite _seaScooterProgressFeedbackIcon;

		[SerializeField]
		private Sprite _compassProgressFeedbackIcon;

		[SerializeField]
		private Sprite _poseidonTridentProgressFeedbackIcon;

		[Header("Progress Feed Colors")]
		[SerializeField]
		private Color _moneyProgressFeedbackColor = new Color(1f, 0.78f, 0.18f, 1f);

		[SerializeField]
		private Color _scrapProgressFeedbackColor = new Color(0.95f, 0.55f, 0.25f, 1f);

		[SerializeField]
		private Color _xpProgressFeedbackColor = new Color(0.35f, 0.85f, 1f, 1f);

		[SerializeField]
		private Color _toolProgressFeedbackColor = new Color(0.35f, 0.9f, 0.5f, 1f);

		[SerializeField]
		private Color _habitatProgressFeedbackColor = new Color(0.25f, 0.75f, 1f, 1f);

		[SerializeField]
		private Color _orderProgressFeedbackColor = new Color(1f, 0.72f, 0.3f, 1f);

		[Header("Modals")]
		[SerializeField]
		private ModalWindowManager _defaultModal;

		[SerializeField]
		private Color _popupDescriptionBackgroundColor = new Color(0f, 0f, 0f, 0.62f);

		[Header("Alerts")]
		[SerializeField]
		private RectTransform _alertRoot;

		[SerializeField]
		private CanvasGroup _alertCanvasGroup;

		[SerializeField]
		private TMP_Text _alertHeaderText;

		[SerializeField]
		private TMP_Text _alertDescriptionText;

		[Header("Alert Timing")]
		[SerializeField]
		private float _alertDefaultDuration = 2f;

		[SerializeField]
		private float _alertIntroDuration = 0.2f;

		[SerializeField]
		private float _alertOutroDuration = 0.2f;

		[SerializeField]
		private bool _alertUseUnscaledTime = true;

		[Header("Alert Animation")]
		[SerializeField]
		private float _alertMoveOffsetY = 20f;

		[SerializeField]
		private float _alertStartScale = 0.98f;

		[SerializeField]
		private Ease _alertIntroEase = Ease.OutCubic;

		[SerializeField]
		private Ease _alertOutroEase = Ease.InCubic;

		[Header("Critical Alert")]
		[SerializeField]
		private Color _criticalDescPulseColor = new Color(1f, 0.25f, 0.25f, 1f);

		[SerializeField]
		private float _criticalDescPulseDuration = 0.35f;

		[Header("Location Feedback")]
		[SerializeField]
		private RectTransform _locationFeedbackRoot;

		[SerializeField]
		private CanvasGroup _locationFeedbackCanvasGroup;

		[SerializeField]
		private TMP_Text _locationFeedbackTitleText;

		[Header("Location Feedback Timing")]
		[SerializeField]
		private float _locationFeedbackDefaultDuration = 2f;

		[SerializeField]
		private float _locationFeedbackIntroDuration = 0.35f;

		[SerializeField]
		private float _locationFeedbackOutroDuration = 0.45f;

		[SerializeField]
		private bool _locationFeedbackUseUnscaledTime = true;

		[Header("Location Feedback Animation")]
		[SerializeField]
		private float _locationFeedbackHiddenOffsetY = 160f;

		[SerializeField]
		private float _locationFeedbackStartScale = 0.96f;

		[SerializeField]
		private Ease _locationFeedbackIntroEase = Ease.OutCubic;

		[SerializeField]
		private Ease _locationFeedbackOutroEase = Ease.InCubic;

		private readonly List<Notification> _activeToasts = new List<Notification>();

		private readonly HashSet<Notification> _restaurantToasts = new HashSet<Notification>();

		private readonly Queue<ToastRequest> _pendingToasts = new Queue<ToastRequest>();

		private readonly List<ProgressFeedbackItem> _activeProgressFeedback = new List<ProgressFeedbackItem>();

		private readonly Queue<ProgressFeedbackItem> _progressFeedbackPool = new Queue<ProgressFeedbackItem>();

		private readonly List<ProgressFeedbackRequest> _pendingProgressFeedback = new List<ProgressFeedbackRequest>();

		private const int MaxConsecutiveQueuedToastsPerMessage = 3;

		private const float PopupMainContentInfoHeight = 230f;

		private const string PopupConfirmButtonKey = "ui.popup.confirm";

		private const string PopupCancelButtonKey = "ui.popup.cancel";

		private const string PopupConfirmButtonFallback = "Confirm";

		private const string PopupCancelButtonFallback = "Cancel";

		private string _previousPopupActionMap;

		private bool _previousPopupCursorVisible;

		private bool _hasPopupInputOverride;

		private bool _popupCancelRegistered;

		private bool _popupCloseOnEscape;

		private string _popupConfirmButtonDefaultText;

		private bool _hasPopupConfirmButtonDefaultText;

		private GameObject _popupConfirmButtonSpacer;

		private LayoutElement _popupConfirmButtonSpacerLayout;

		private RectTransform _popupConfirmButtonOffsetTargetRect;

		private Vector2 _popupConfirmButtonOffsetTargetDefaultPosition;

		private bool _hasPopupConfirmButtonOffsetTargetDefaultPosition;

		private RectTransform _popupMainContentRect;

		private Vector2 _popupMainContentDefaultSizeDelta;

		private bool _hasPopupMainContentDefaultSizeDelta;

		private Coroutine _alertHideCoroutine;

		private Sequence _alertSequence;

		private Tween _criticalDescTween;

		private bool _isAlertShowing;

		private bool _isAlertInitialized;

		private string _currentAlertHeader = string.Empty;

		private string _currentAlertDescription = string.Empty;

		private float _currentAlertDuration;

		private bool _currentAlertAutoClose;

		private AlertPriority _currentAlertPriority;

		private bool _currentAlertUsePriorityPulse = true;

		private Vector2 _alertShownAnchoredPosition;

		private Color _alertDescBaseColor = Color.white;

		private Coroutine _locationFeedbackHideCoroutine;

		private Sequence _locationFeedbackSequence;

		private bool _isLocationFeedbackInitialized;

		private bool _isLocationFeedbackShowing;

		private Vector2 _locationFeedbackShownAnchoredPosition;

		private string _currentLocationFeedbackTitle = string.Empty;

		public bool IsAlertShowing => _isAlertShowing;

		public bool IsLocationFeedbackShowing => _isLocationFeedbackShowing;

		protected override void Awake()
		{
			base.Awake();
			RuntimeSettings.RestaurantChatLogsChanged += OnRestaurantLogsChanged;
			PrepareProgressFeedbackPool();
			InitializeAlertVisuals();
			InitializeLocationFeedbackVisuals();
		}

		private void OnDestroy()
		{
			RuntimeSettings.RestaurantChatLogsChanged -= OnRestaurantLogsChanged;
			ClearToastRuntime();
			ClearProgressFeedbackRuntime();
			UnregisterPopupCancelInput();
			KillAlertTween();
			KillCriticalDescTween();
			if (_alertHideCoroutine != null)
			{
				StopCoroutine(_alertHideCoroutine);
				_alertHideCoroutine = null;
			}
			KillLocationFeedbackTween();
			if (_locationFeedbackHideCoroutine != null)
			{
				StopCoroutine(_locationFeedbackHideCoroutine);
				_locationFeedbackHideCoroutine = null;
			}
		}

		private void Update()
		{
			if (_popupCloseOnEscape && _hasPopupInputOverride && !(_defaultModal == null) && _defaultModal.isOn && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
			{
				HidePopup();
			}
		}

		private void InitializeAlertVisuals()
		{
			if (!_isAlertInitialized)
			{
				if (_alertRoot != null)
				{
					_alertShownAnchoredPosition = _alertRoot.anchoredPosition;
				}
				if (_alertDescriptionText != null)
				{
					_alertDescBaseColor = _alertDescriptionText.color;
				}
				SetAlertVisualImmediate(visible: false);
				_isAlertInitialized = true;
			}
		}

		[Obsolete("Use other ShowToast!")]
		public void ShowToast(string header, string message)
		{
			ShowToast(message, NotificationState.Info, _toastDuration);
		}

		public void ShowToast(string message, NotificationState state = NotificationState.Info, float duration = -1f)
		{
			EnqueueToast(message, state, duration, restaurantLog: false);
		}

		public void ShowRestaurantToast(string message, NotificationState state = NotificationState.Info, float duration = -1f)
		{
			EnqueueToast(message, state, duration, restaurantLog: true);
		}

		private void EnqueueToast(string message, NotificationState state, float duration, bool restaurantLog)
		{
			if ((restaurantLog && !RuntimeSettings.RestaurantChatLogsEnabled) || _notifcationPrefab == null || _stackTransform == null)
			{
				return;
			}
			float duration2 = ((duration > 0f) ? duration : _toastDuration);
			string text = NotificationLocalization.Localize(message);
			ToastRequest toastRequest = new ToastRequest(text ?? string.Empty, state, duration2, restaurantLog);
			if (_activeToasts.Count >= GetMaxVisibleToasts())
			{
				if (GetConsecutiveQueuedToastCount(toastRequest.Message) < 3)
				{
					_pendingToasts.Enqueue(toastRequest);
				}
			}
			else
			{
				SpawnToast(toastRequest);
			}
		}

		private void SpawnToast(ToastRequest request)
		{
			if ((!request.RestaurantLog || RuntimeSettings.RestaurantChatLogsEnabled) && !(_notifcationPrefab == null) && !(_stackTransform == null))
			{
				Notification notification = UnityEngine.Object.Instantiate(_notifcationPrefab, _stackTransform);
				notification.transform.SetSiblingIndex(0);
				notification.Hidden += OnToastHidden;
				_activeToasts.Add(notification);
				if (request.RestaurantLog)
				{
					_restaurantToasts.Add(notification);
				}
				notification.Show(request.Message, request.State, request.Duration);
			}
		}

		private void OnToastHidden(Notification notification)
		{
			if (!(notification == null))
			{
				notification.Hidden -= OnToastHidden;
				_activeToasts.Remove(notification);
				_restaurantToasts.Remove(notification);
				UnityEngine.Object.Destroy(notification.gameObject);
				StartCoroutine(ShowQueuedToastAfterDelay());
			}
		}

		private IEnumerator ShowQueuedToastAfterDelay()
		{
			if (_toastDestroyDelay > 0f)
			{
				if (_toastUseUnscaledTime)
				{
					yield return new WaitForSecondsRealtime(_toastDestroyDelay);
				}
				else
				{
					yield return new WaitForSeconds(_toastDestroyDelay);
				}
			}
			ShowQueuedToasts();
		}

		private void ShowQueuedToasts()
		{
			while (_pendingToasts.Count > 0 && _activeToasts.Count < GetMaxVisibleToasts())
			{
				SpawnToast(_pendingToasts.Dequeue());
			}
		}

		private int GetMaxVisibleToasts()
		{
			return Mathf.Max(1, _maxVisibleToasts);
		}

		private int GetConsecutiveQueuedToastCount(string message)
		{
			int num = 0;
			foreach (ToastRequest pendingToast in _pendingToasts)
			{
				num = (string.Equals(pendingToast.Message, message, StringComparison.Ordinal) ? (num + 1) : 0);
			}
			return num;
		}

		private void ClearToastRuntime()
		{
			_restaurantToasts.Clear();
			foreach (Notification activeToast in _activeToasts)
			{
				if (activeToast != null)
				{
					activeToast.Hidden -= OnToastHidden;
				}
			}
			_activeToasts.Clear();
			_pendingToasts.Clear();
		}

		private void OnRestaurantLogsChanged(bool enabled)
		{
			if (enabled)
			{
				return;
			}
			int count = _pendingToasts.Count;
			for (int i = 0; i < count; i++)
			{
				ToastRequest item = _pendingToasts.Dequeue();
				if (!item.RestaurantLog)
				{
					_pendingToasts.Enqueue(item);
				}
			}
			for (int num = _activeToasts.Count - 1; num >= 0; num--)
			{
				Notification notification = _activeToasts[num];
				if (_restaurantToasts.Remove(notification))
				{
					_activeToasts.RemoveAt(num);
					if (!(notification == null))
					{
						notification.Hidden -= OnToastHidden;
						notification.gameObject.SetActive(value: false);
						UnityEngine.Object.Destroy(notification.gameObject);
					}
				}
			}
			ShowQueuedToasts();
		}

		public void ShowMoneyGain(int storedAmount)
		{
			if (storedAmount > 0)
			{
				EnqueueProgressFeedback(new ProgressFeedbackRequest("resource.money", storedAmount, string.Empty, string.Empty, _moneyProgressFeedbackIcon, null, _moneyProgressFeedbackColor, _resourceProgressFeedbackDuration, ProgressFeedbackFormat.Money, isPriority: false));
			}
		}

		public void ShowScrapGain(int storedAmount)
		{
			if (storedAmount > 0)
			{
				EnqueueProgressFeedback(new ProgressFeedbackRequest("resource.scrap", storedAmount, string.Empty, string.Empty, _scrapProgressFeedbackIcon, null, _scrapProgressFeedbackColor, _resourceProgressFeedbackDuration, ProgressFeedbackFormat.Scrap, isPriority: false));
			}
		}

		public void ShowXpGain(int amount)
		{
			if (amount > 0)
			{
				EnqueueProgressFeedback(new ProgressFeedbackRequest("resource.xp", amount, string.Empty, string.Empty, _xpProgressFeedbackIcon, null, _xpProgressFeedbackColor, _resourceProgressFeedbackDuration, ProgressFeedbackFormat.Xp, isPriority: false));
			}
		}

		public void ShowToolUpgrade(ToolType toolType, int previousLevel, int newLevel)
		{
			if (newLevel > previousLevel)
			{
				EnqueueProgressFeedback(new ProgressFeedbackRequest(string.Empty, 0, GetToolProgressFeedbackName(toolType), $"Lv. {previousLevel} → Lv. {newLevel}", GetToolProgressFeedbackIcon(toolType), _upgradeProgressFeedbackBadge, _toolProgressFeedbackColor, _milestoneProgressFeedbackDuration, ProgressFeedbackFormat.Text, isPriority: true));
			}
		}

		public void ShowToolUnlocked(ToolType toolType)
		{
			EnqueueProgressFeedback(new ProgressFeedbackRequest(string.Empty, 0, GetToolProgressFeedbackName(toolType), "Unlocked", GetToolProgressFeedbackIcon(toolType), _upgradeProgressFeedbackBadge, _toolProgressFeedbackColor, _milestoneProgressFeedbackDuration, ProgressFeedbackFormat.Text, isPriority: true));
		}

		public void ShowToolEquipped(ToolType toolType, int level)
		{
			EnqueueProgressFeedback(new ProgressFeedbackRequest(string.Empty, 0, GetToolProgressFeedbackName(toolType), $"Lv. {level} Equipped", GetToolProgressFeedbackIcon(toolType), null, _toolProgressFeedbackColor, _resourceProgressFeedbackDuration, ProgressFeedbackFormat.Text, isPriority: true));
		}

		private Sprite GetToolProgressFeedbackIcon(ToolType toolType)
		{
			return toolType switch
			{
				ToolType.DivingEquipment => _divingEquipmentProgressFeedbackIcon, 
				ToolType.Harpoon => _harpoonProgressFeedbackIcon, 
				ToolType.FishingRod => _fishingRodProgressFeedbackIcon, 
				ToolType.Mop => _mopProgressFeedbackIcon, 
				ToolType.SeaScooter => _seaScooterProgressFeedbackIcon, 
				ToolType.Compass => _compassProgressFeedbackIcon, 
				ToolType.PoseidonTrident => _poseidonTridentProgressFeedbackIcon, 
				_ => null, 
			};
		}

		private static string GetToolProgressFeedbackName(ToolType toolType)
		{
			return toolType switch
			{
				ToolType.DivingEquipment => "Diving Suit", 
				ToolType.Harpoon => "Harpoon", 
				ToolType.FishingRod => "Fishing Rod", 
				ToolType.Mop => "Mop", 
				ToolType.SeaScooter => "Sea Scooter", 
				ToolType.Compass => "Alien Compass", 
				ToolType.PoseidonTrident => "Poseidon Trident", 
				_ => "Tool", 
			};
		}

		public void ShowHabitatDiscovered(string habitatName)
		{
			string primaryText = (string.IsNullOrWhiteSpace(habitatName) ? "New Habitat" : habitatName.Trim());
			EnqueueProgressFeedback(new ProgressFeedbackRequest(string.Empty, 0, primaryText, "Discovered", _habitatProgressFeedbackIcon, null, _habitatProgressFeedbackColor, _milestoneProgressFeedbackDuration, ProgressFeedbackFormat.Text, isPriority: true));
		}

		public void ShowOrderItem(string tableKey, string itemId, string itemName, Sprite itemIcon, int amount = 1)
		{
			if (amount > 0 && !string.IsNullOrWhiteSpace(tableKey) && !string.IsNullOrWhiteSpace(itemId))
			{
				string primaryText = (string.IsNullOrWhiteSpace(itemName) ? "Order Item" : itemName.Trim());
				EnqueueProgressFeedback(new ProgressFeedbackRequest("order." + tableKey + "." + itemId, amount, primaryText, string.Empty, itemIcon, null, _orderProgressFeedbackColor, _orderProgressFeedbackDuration, ProgressFeedbackFormat.OrderItem, isPriority: false));
			}
		}

		private void PrepareProgressFeedbackPool()
		{
			if (!(_progressFeedbackPrefab == null) && !(_progressFeedbackRoot == null))
			{
				int num = Mathf.Max(1, _maxVisibleProgressFeedback);
				for (int i = 0; i < num; i++)
				{
					_progressFeedbackPool.Enqueue(CreateProgressFeedbackItem());
				}
			}
		}

		private ProgressFeedbackItem CreateProgressFeedbackItem()
		{
			ProgressFeedbackItem progressFeedbackItem = UnityEngine.Object.Instantiate(_progressFeedbackPrefab, _progressFeedbackRoot);
			progressFeedbackItem.ReleaseImmediate();
			return progressFeedbackItem;
		}

		private void EnqueueProgressFeedback(ProgressFeedbackRequest request)
		{
			if (request == null || _progressFeedbackPrefab == null || _progressFeedbackRoot == null || TryMergeActiveProgressFeedback(request) || TryMergePendingProgressFeedback(request))
			{
				return;
			}
			if (_activeProgressFeedback.Count < Mathf.Max(1, _maxVisibleProgressFeedback))
			{
				SpawnProgressFeedback(request);
				return;
			}
			int num = Mathf.Max(1, _maxPendingProgressFeedback);
			if (_pendingProgressFeedback.Count >= num)
			{
				MakeRoomForProgressFeedback(request.IsPriority);
			}
			if (_pendingProgressFeedback.Count < num)
			{
				_pendingProgressFeedback.Add(request);
			}
		}

		private bool TryMergeActiveProgressFeedback(ProgressFeedbackRequest request)
		{
			if (!request.CanMerge)
			{
				return false;
			}
			for (int i = 0; i < _activeProgressFeedback.Count; i++)
			{
				ProgressFeedbackItem progressFeedbackItem = _activeProgressFeedback[i];
				if (!(progressFeedbackItem == null) && string.Equals(progressFeedbackItem.MergeKey, request.MergeKey, StringComparison.Ordinal) && !(Time.unscaledTime - progressFeedbackItem.LastUpdateTime > _progressFeedbackMergeWindow))
				{
					int amount = progressFeedbackItem.Amount + request.Amount;
					progressFeedbackItem.MergeAmount(amount, FormatProgressFeedbackPrimary(request, amount), request.Duration);
					return true;
				}
			}
			return false;
		}

		private bool TryMergePendingProgressFeedback(ProgressFeedbackRequest request)
		{
			if (!request.CanMerge)
			{
				return false;
			}
			for (int num = _pendingProgressFeedback.Count - 1; num >= 0; num--)
			{
				ProgressFeedbackRequest progressFeedbackRequest = _pendingProgressFeedback[num];
				if (string.Equals(progressFeedbackRequest.MergeKey, request.MergeKey, StringComparison.Ordinal) && !(Time.unscaledTime - progressFeedbackRequest.LastUpdateTime > _progressFeedbackMergeWindow))
				{
					progressFeedbackRequest.Amount += request.Amount;
					progressFeedbackRequest.LastUpdateTime = Time.unscaledTime;
					return true;
				}
			}
			return false;
		}

		private void MakeRoomForProgressFeedback(bool incomingIsPriority)
		{
			if (_pendingProgressFeedback.Count == 0 || !incomingIsPriority)
			{
				return;
			}
			int index = 0;
			if (incomingIsPriority)
			{
				int num = _pendingProgressFeedback.FindIndex((ProgressFeedbackRequest request) => !request.IsPriority);
				if (num >= 0)
				{
					index = num;
				}
			}
			_pendingProgressFeedback.RemoveAt(index);
		}

		private void SpawnProgressFeedback(ProgressFeedbackRequest request)
		{
			ProgressFeedbackItem progressFeedbackItem = ((_progressFeedbackPool.Count > 0) ? _progressFeedbackPool.Dequeue() : CreateProgressFeedbackItem());
			progressFeedbackItem.transform.SetParent(_progressFeedbackRoot, worldPositionStays: false);
			progressFeedbackItem.transform.SetSiblingIndex(0);
			progressFeedbackItem.Hidden += OnProgressFeedbackHidden;
			_activeProgressFeedback.Add(progressFeedbackItem);
			progressFeedbackItem.Show(request.MergeKey, request.Amount, FormatProgressFeedbackPrimary(request, request.Amount), request.SecondaryText ?? string.Empty, request.Icon, request.Badge, request.AccentColor, request.Duration);
		}

		private string FormatProgressFeedbackPrimary(ProgressFeedbackRequest request, int amount)
		{
			return request.Format switch
			{
				ProgressFeedbackFormat.Money => "+" + amount.ConvertMoneyText(), 
				ProgressFeedbackFormat.Scrap => "+" + amount.ConvertMoneyText(includeDollarSign: false), 
				ProgressFeedbackFormat.Xp => $"+{amount:N0} XP", 
				ProgressFeedbackFormat.OrderItem => $"{request.PrimaryText} ×{amount:N0}", 
				_ => request.PrimaryText ?? string.Empty, 
			};
		}

		private void OnProgressFeedbackHidden(ProgressFeedbackItem item)
		{
			if (!(item == null))
			{
				item.Hidden -= OnProgressFeedbackHidden;
				_activeProgressFeedback.Remove(item);
				item.ReleaseImmediate();
				_progressFeedbackPool.Enqueue(item);
				ShowPendingProgressFeedback();
			}
		}

		private void ShowPendingProgressFeedback()
		{
			int num = Mathf.Max(1, _maxVisibleProgressFeedback);
			while (_pendingProgressFeedback.Count > 0 && _activeProgressFeedback.Count < num)
			{
				ProgressFeedbackRequest request = _pendingProgressFeedback[0];
				_pendingProgressFeedback.RemoveAt(0);
				SpawnProgressFeedback(request);
			}
		}

		private void ClearProgressFeedbackRuntime()
		{
			for (int i = 0; i < _activeProgressFeedback.Count; i++)
			{
				ProgressFeedbackItem progressFeedbackItem = _activeProgressFeedback[i];
				if (progressFeedbackItem != null)
				{
					progressFeedbackItem.Hidden -= OnProgressFeedbackHidden;
				}
			}
			_activeProgressFeedback.Clear();
			_pendingProgressFeedback.Clear();
			_progressFeedbackPool.Clear();
		}

		public void ShowPopup(string header, string message, UnityAction onConfirm = null, UnityAction onCancel = null, string confirmButtonText = null, bool useDescriptionBackground = false, float confirmButtonOffsetY = 0f, bool closeOnEscape = false, string confirmButtonLocalizationKey = null)
		{
			if (!(_defaultModal == null))
			{
				_popupCloseOnEscape = closeOnEscape;
				ApplyPopupInputOverride();
				CachePopupButtonDefaults();
				string text = header ?? string.Empty;
				string text2 = message ?? string.Empty;
				_defaultModal.titleText = text;
				_defaultModal.descriptionText = text2;
				_defaultModal.showConfirmButton = onConfirm != null;
				_defaultModal.showCancelButton = onCancel != null;
				_defaultModal.UpdateUI();
				_defaultModal.onConfirm.RemoveAllListeners();
				_defaultModal.onCancel.RemoveAllListeners();
				_defaultModal.onClose.RemoveListener(RestorePopupInputOverride);
				if (onConfirm != null)
				{
					_defaultModal.onConfirm.AddListener(onConfirm);
				}
				if (onCancel != null)
				{
					_defaultModal.onCancel.AddListener(onCancel);
				}
				_defaultModal.onConfirm.AddListener(HidePopup);
				_defaultModal.onCancel.AddListener(HidePopup);
				_defaultModal.onClose.AddListener(RestorePopupInputOverride);
				_defaultModal.Open();
				ApplyPopupTexts(text, text2);
				ApplyPopupDescriptionBackground(useDescriptionBackground);
				ApplyPopupButtons(onConfirm != null, onCancel != null, confirmButtonText, confirmButtonOffsetY, confirmButtonLocalizationKey);
			}
		}

		public void HidePopup()
		{
			if (_defaultModal != null && _defaultModal.isOn)
			{
				_defaultModal.Close();
			}
			RestorePopupInputOverride();
		}

		private void ApplyPopupTexts(string header, string message)
		{
			if (!(_defaultModal == null))
			{
				_defaultModal.titleText = header;
				_defaultModal.descriptionText = message;
				if (_defaultModal.windowTitle != null)
				{
					_defaultModal.windowTitle.text = header;
				}
				if (_defaultModal.windowDescription != null)
				{
					_defaultModal.windowDescription.text = message;
				}
			}
		}

		private void ApplyPopupButtons(bool showConfirm, bool showCancel, string confirmButtonText = null, float confirmButtonOffsetY = 0f, string confirmButtonLocalizationKey = null)
		{
			if (_defaultModal == null)
			{
				return;
			}
			if (_defaultModal.confirmButton != null)
			{
				_defaultModal.confirmButton.gameObject.SetActive(showConfirm);
			}
			if (_defaultModal.cancelButton != null)
			{
				_defaultModal.cancelButton.gameObject.SetActive(showCancel);
			}
			if (_defaultModal.confirmButton != null)
			{
				string text = ((!string.IsNullOrWhiteSpace(confirmButtonText)) ? confirmButtonText : ((!string.IsNullOrWhiteSpace(_popupConfirmButtonDefaultText)) ? _popupConfirmButtonDefaultText : "Confirm"));
				string text2 = ((!string.IsNullOrWhiteSpace(confirmButtonLocalizationKey)) ? confirmButtonLocalizationKey : (string.IsNullOrWhiteSpace(confirmButtonText) ? "ui.popup.confirm" : null));
				string text3 = ((!string.IsNullOrWhiteSpace(text2)) ? LocalizationDatabase.GetString("UiTable", text2, text) : text);
				if (!string.IsNullOrWhiteSpace(text3))
				{
					_defaultModal.confirmButton.SetText(text3);
				}
			}
			if (_defaultModal.cancelButton != null)
			{
				string text4 = LocalizationDatabase.GetString("UiTable", "ui.popup.cancel", "Cancel");
				_defaultModal.cancelButton.SetText(text4);
			}
			ApplyPopupConfirmButtonOffset(confirmButtonOffsetY);
		}

		private void ApplyPopupDescriptionBackground(bool active)
		{
			if (_defaultModal == null || _defaultModal.windowDescription == null)
			{
				if (!active)
				{
					RestorePopupMainContentSize();
				}
				return;
			}
			RectTransform rectTransform = _defaultModal.windowDescription.rectTransform;
			if (rectTransform == null || rectTransform.parent == null)
			{
				return;
			}
			RectTransform popupMainContentRect = GetPopupMainContentRect(rectTransform);
			Transform transform = ((popupMainContentRect != null) ? popupMainContentRect : rectTransform.parent);
			Transform transform2 = transform.Find("PopupDescriptionBackground");
			if (!active)
			{
				if (transform2 != null)
				{
					transform2.gameObject.SetActive(value: false);
				}
				RestorePopupMainContentSize();
				return;
			}
			if (popupMainContentRect != null)
			{
				ApplyPopupMainContentInfoSize(popupMainContentRect);
			}
			Image component;
			if (transform2 != null)
			{
				transform2.gameObject.SetActive(value: true);
				component = transform2.GetComponent<Image>();
			}
			else
			{
				GameObject obj = new GameObject("PopupDescriptionBackground", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
				obj.transform.SetParent(transform, worldPositionStays: false);
				obj.transform.SetSiblingIndex(0);
				obj.GetComponent<LayoutElement>().ignoreLayout = true;
				component = obj.GetComponent<Image>();
				component.raycastTarget = false;
			}
			if (!(component == null))
			{
				RectTransform rectTransform2 = component.rectTransform;
				rectTransform2.anchorMin = Vector2.zero;
				rectTransform2.anchorMax = Vector2.one;
				rectTransform2.pivot = new Vector2(0.5f, 0.5f);
				rectTransform2.anchoredPosition = Vector2.zero;
				rectTransform2.sizeDelta = Vector2.zero;
				rectTransform2.SetSiblingIndex(0);
				component.color = _popupDescriptionBackgroundColor;
				component.enabled = true;
			}
		}

		private RectTransform GetPopupMainContentRect(RectTransform descriptionRect)
		{
			Transform transform = descriptionRect;
			while (transform != null)
			{
				if (string.Equals(transform.name, "Main Content", StringComparison.Ordinal))
				{
					return transform.GetComponent<RectTransform>();
				}
				transform = transform.parent;
			}
			return null;
		}

		private void ApplyPopupMainContentInfoSize(RectTransform mainContentRect)
		{
			if (_popupMainContentRect != null && _popupMainContentRect != mainContentRect)
			{
				RestorePopupMainContentSize();
			}
			if (_popupMainContentRect != mainContentRect)
			{
				_popupMainContentRect = mainContentRect;
				_popupMainContentDefaultSizeDelta = mainContentRect.sizeDelta;
				_hasPopupMainContentDefaultSizeDelta = true;
			}
			Vector2 popupMainContentDefaultSizeDelta = _popupMainContentDefaultSizeDelta;
			popupMainContentDefaultSizeDelta.y = 230f;
			mainContentRect.sizeDelta = popupMainContentDefaultSizeDelta;
		}

		private void RestorePopupMainContentSize()
		{
			if (_popupMainContentRect != null && _hasPopupMainContentDefaultSizeDelta)
			{
				_popupMainContentRect.sizeDelta = _popupMainContentDefaultSizeDelta;
			}
			_popupMainContentRect = null;
			_popupMainContentDefaultSizeDelta = Vector2.zero;
			_hasPopupMainContentDefaultSizeDelta = false;
		}

		private void ApplyPopupConfirmButtonOffset(float offsetY)
		{
			float offset = Mathf.Abs(offsetY);
			bool layoutSpacerApplied = ApplyPopupConfirmButtonLayoutSpacer(offset);
			ApplyPopupConfirmButtonRectOffset(offset, layoutSpacerApplied);
		}

		private bool ApplyPopupConfirmButtonLayoutSpacer(float offset)
		{
			if (!TryGetPopupConfirmButtonSpacerTarget(out var spacerParent, out var spacerTarget))
			{
				SetPopupConfirmButtonSpacerActive(active: false);
				return false;
			}
			if (offset <= 0f || spacerParent.GetComponent<LayoutGroup>() == null)
			{
				SetPopupConfirmButtonSpacerActive(active: false);
				return false;
			}
			if (_popupConfirmButtonSpacer == null || _popupConfirmButtonSpacer.transform.parent != spacerParent)
			{
				Transform transform = spacerParent.Find("PopupConfirmButtonSpacer");
				if (transform != null)
				{
					_popupConfirmButtonSpacer = transform.gameObject;
					_popupConfirmButtonSpacerLayout = transform.GetComponent<LayoutElement>();
				}
				else
				{
					_popupConfirmButtonSpacer = new GameObject("PopupConfirmButtonSpacer", typeof(RectTransform), typeof(LayoutElement));
					_popupConfirmButtonSpacer.transform.SetParent(spacerParent, worldPositionStays: false);
					_popupConfirmButtonSpacerLayout = _popupConfirmButtonSpacer.GetComponent<LayoutElement>();
				}
			}
			if (_popupConfirmButtonSpacerLayout == null)
			{
				_popupConfirmButtonSpacerLayout = _popupConfirmButtonSpacer.GetComponent<LayoutElement>();
			}
			_popupConfirmButtonSpacer.SetActive(value: true);
			if (_popupConfirmButtonSpacerLayout != null)
			{
				_popupConfirmButtonSpacerLayout.ignoreLayout = false;
				_popupConfirmButtonSpacerLayout.minHeight = offset;
				_popupConfirmButtonSpacerLayout.preferredHeight = offset;
				_popupConfirmButtonSpacerLayout.flexibleHeight = 0f;
				_popupConfirmButtonSpacerLayout.minWidth = 0f;
				_popupConfirmButtonSpacerLayout.preferredWidth = 0f;
				_popupConfirmButtonSpacerLayout.flexibleWidth = 0f;
			}
			int num = spacerTarget.GetSiblingIndex();
			if (_popupConfirmButtonSpacer.transform.GetSiblingIndex() < num)
			{
				num--;
			}
			_popupConfirmButtonSpacer.transform.SetSiblingIndex(Mathf.Max(0, num));
			return true;
		}

		private void ApplyPopupConfirmButtonRectOffset(float offset, bool layoutSpacerApplied)
		{
			if (TryGetPopupConfirmButtonOffsetTarget(out var targetRect))
			{
				if (_popupConfirmButtonOffsetTargetRect != null && _popupConfirmButtonOffsetTargetRect != targetRect && _hasPopupConfirmButtonOffsetTargetDefaultPosition)
				{
					_popupConfirmButtonOffsetTargetRect.anchoredPosition = _popupConfirmButtonOffsetTargetDefaultPosition;
				}
				if (_popupConfirmButtonOffsetTargetRect != targetRect)
				{
					_popupConfirmButtonOffsetTargetRect = targetRect;
					_popupConfirmButtonOffsetTargetDefaultPosition = targetRect.anchoredPosition;
					_hasPopupConfirmButtonOffsetTargetDefaultPosition = true;
				}
				Vector2 popupConfirmButtonOffsetTargetDefaultPosition = _popupConfirmButtonOffsetTargetDefaultPosition;
				if (offset > 0f && !layoutSpacerApplied)
				{
					popupConfirmButtonOffsetTargetDefaultPosition.y -= offset;
				}
				targetRect.anchoredPosition = popupConfirmButtonOffsetTargetDefaultPosition;
			}
		}

		private bool TryGetPopupConfirmButtonSpacerTarget(out Transform spacerParent, out Transform spacerTarget)
		{
			spacerParent = null;
			spacerTarget = null;
			if (_defaultModal == null || _defaultModal.confirmButton == null)
			{
				return false;
			}
			Transform transform = _defaultModal.confirmButton.transform;
			Transform parent = transform.parent;
			if (parent == null)
			{
				return false;
			}
			if (parent.GetComponent<HorizontalLayoutGroup>() != null && parent.parent != null)
			{
				spacerParent = parent.parent;
				spacerTarget = parent;
				return true;
			}
			spacerParent = parent;
			spacerTarget = transform;
			return true;
		}

		private bool TryGetPopupConfirmButtonOffsetTarget(out RectTransform targetRect)
		{
			targetRect = null;
			if (_defaultModal == null || _defaultModal.confirmButton == null)
			{
				return false;
			}
			Transform obj = _defaultModal.confirmButton.transform;
			Transform parent = obj.parent;
			Transform transform = obj;
			if (parent != null && parent.GetComponent<HorizontalLayoutGroup>() != null && parent.parent != null)
			{
				transform = parent.parent;
			}
			targetRect = transform.GetComponent<RectTransform>();
			return targetRect != null;
		}

		private void SetPopupConfirmButtonSpacerActive(bool active)
		{
			if (_popupConfirmButtonSpacer != null)
			{
				_popupConfirmButtonSpacer.SetActive(active);
			}
		}

		private void CachePopupButtonDefaults()
		{
			if (!(_defaultModal == null) && !(_defaultModal.confirmButton == null) && !_hasPopupConfirmButtonDefaultText)
			{
				_popupConfirmButtonDefaultText = _defaultModal.confirmButton.buttonText;
				_hasPopupConfirmButtonDefaultText = true;
			}
		}

		private void RegisterPopupCancelInput()
		{
			if (!_popupCancelRegistered)
			{
				InputHandler inputHandler = ((Singleton<InputManager>.Instance != null) ? Singleton<InputManager>.Instance.InputHandler : null);
				if (!(inputHandler == null))
				{
					inputHandler.Cancel -= OnPopupCancelInput;
					inputHandler.Cancel += OnPopupCancelInput;
					_popupCancelRegistered = true;
				}
			}
		}

		private void UnregisterPopupCancelInput()
		{
			if (_popupCancelRegistered)
			{
				InputHandler inputHandler = ((Singleton<InputManager>.Instance != null) ? Singleton<InputManager>.Instance.InputHandler : null);
				if (inputHandler != null)
				{
					inputHandler.Cancel -= OnPopupCancelInput;
				}
				_popupCancelRegistered = false;
			}
		}

		private void OnPopupCancelInput(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && !(_defaultModal == null) && _defaultModal.isOn)
			{
				HidePopup();
			}
		}

		private void ApplyPopupInputOverride()
		{
			if (!_hasPopupInputOverride)
			{
				_previousPopupCursorVisible = CursorHelper.CursorVisible;
				_hasPopupInputOverride = true;
				if (_popupCloseOnEscape)
				{
					RegisterPopupCancelInput();
				}
				if (Singleton<PlayersManager>.Instance == null || Singleton<PlayersManager>.Instance.LocalPlayer == null)
				{
					CursorHelper.CursorVisible = true;
					return;
				}
				_previousPopupActionMap = Singleton<PlayersManager>.Instance.LocalPlayer.CurrentMap;
				Singleton<PlayersManager>.Instance.LocalPlayer.SwitchTo("UI", cursorVisible: true);
			}
		}

		private void RestorePopupInputOverride()
		{
			ApplyPopupDescriptionBackground(active: false);
			ApplyPopupConfirmButtonOffset(0f);
			if (_hasPopupInputOverride)
			{
				if (Singleton<PlayersManager>.Instance != null && Singleton<PlayersManager>.Instance.LocalPlayer != null && !string.IsNullOrEmpty(_previousPopupActionMap))
				{
					Singleton<PlayersManager>.Instance.LocalPlayer.SwitchTo(_previousPopupActionMap, _previousPopupCursorVisible);
				}
				else
				{
					CursorHelper.CursorVisible = _previousPopupCursorVisible;
				}
				_previousPopupActionMap = string.Empty;
				_previousPopupCursorVisible = false;
				_hasPopupInputOverride = false;
				_popupCloseOnEscape = false;
				UnregisterPopupCancelInput();
			}
		}

		public void ShowAlert(string description, float duration = -1f, AlertPriority priority = AlertPriority.Normal, bool autoClose = true, bool usePriorityPulse = true)
		{
			float duration2 = ((duration > 0f) ? duration : _alertDefaultDuration);
			ShowAlert(new AlertRequest(description, duration2, priority, autoClose, usePriorityPulse));
		}

		public void ShowAlert(string header, string description, float duration = -1f, AlertPriority priority = AlertPriority.Normal, bool autoClose = true, bool usePriorityPulse = true)
		{
			float duration2 = ((duration > 0f) ? duration : _alertDefaultDuration);
			ShowAlert(new AlertRequest(header, description, duration2, priority, autoClose, usePriorityPulse));
		}

		public void ShowAlert(AlertRequest request)
		{
			InitializeAlertVisuals();
			if (_alertRoot == null || _alertCanvasGroup == null || _alertDescriptionText == null)
			{
				return;
			}
			string text = NotificationLocalization.Localize(request.Header) ?? string.Empty;
			string text2 = NotificationLocalization.Localize(request.Description) ?? string.Empty;
			if ((!string.IsNullOrWhiteSpace(text) || !string.IsNullOrWhiteSpace(text2)) && (!_isAlertShowing || (int)request.Priority >= (int)_currentAlertPriority))
			{
				if (_isAlertShowing && _currentAlertPriority == request.Priority && _currentAlertUsePriorityPulse == request.UsePriorityPulse && _currentAlertHeader == text && _currentAlertDescription == text2)
				{
					_currentAlertDuration = request.Duration;
					_currentAlertAutoClose = request.AutoClose;
					RestartAlertHideTimer();
					return;
				}
				_currentAlertHeader = text;
				_currentAlertDescription = text2;
				_currentAlertDuration = request.Duration;
				_currentAlertAutoClose = request.AutoClose;
				_currentAlertPriority = request.Priority;
				_currentAlertUsePriorityPulse = request.UsePriorityPulse;
				ApplyAlertTexts(_currentAlertHeader, _currentAlertDescription);
				RefreshCriticalVisualState(_currentAlertPriority, _currentAlertUsePriorityPulse);
				PlayShowAlertAnimation();
				RestartAlertHideTimer();
			}
		}

		public void ForceHideAlert()
		{
			UtilityMethods.Log("[NotifyManager] ForceHideAlert 1!!");
			if (_isAlertShowing || (!(_alertRoot == null) && _alertRoot.gameObject.activeSelf))
			{
				UtilityMethods.Log("[NotifyManager] ForceHideAlert 2!!");
				StopAlertHideTimer();
				PlayHideAlertAnimation();
			}
		}

		public bool TryHideAlert(string header, string description)
		{
			if (!_isAlertShowing)
			{
				return false;
			}
			string text = NotificationLocalization.Localize(header) ?? string.Empty;
			string text2 = NotificationLocalization.Localize(description) ?? string.Empty;
			if (_currentAlertHeader != text || _currentAlertDescription != text2)
			{
				return false;
			}
			ForceHideAlert();
			return true;
		}

		private void ApplyAlertTexts(string header, string description)
		{
			if (_alertHeaderText != null)
			{
				bool flag = !string.IsNullOrWhiteSpace(header);
				_alertHeaderText.gameObject.SetActive(flag);
				if (flag)
				{
					_alertHeaderText.text = header;
				}
			}
			if (_alertDescriptionText != null)
			{
				_alertDescriptionText.text = description;
			}
		}

		private void RestartAlertHideTimer()
		{
			StopAlertHideTimer();
			if (_currentAlertAutoClose)
			{
				_alertHideCoroutine = StartCoroutine(AlertHideCountdown(_currentAlertDuration));
			}
		}

		private void StopAlertHideTimer()
		{
			if (_alertHideCoroutine != null)
			{
				StopCoroutine(_alertHideCoroutine);
				_alertHideCoroutine = null;
			}
		}

		private IEnumerator AlertHideCountdown(float delay)
		{
			if (_alertUseUnscaledTime)
			{
				yield return new WaitForSecondsRealtime(delay);
			}
			else
			{
				yield return new WaitForSeconds(delay);
			}
			_alertHideCoroutine = null;
			PlayHideAlertAnimation();
		}

		private void PlayShowAlertAnimation()
		{
			KillAlertTween();
			_isAlertShowing = true;
			if (!(_alertRoot == null) && !(_alertCanvasGroup == null))
			{
				_alertRoot.gameObject.SetActive(value: true);
				Vector2 anchoredPosition = _alertShownAnchoredPosition + new Vector2(0f, 0f - _alertMoveOffsetY);
				_alertRoot.anchoredPosition = anchoredPosition;
				_alertRoot.localScale = Vector3.one * _alertStartScale;
				_alertCanvasGroup.alpha = 0f;
				_alertSequence = DOTween.Sequence().SetUpdate(_alertUseUnscaledTime).Append(_alertCanvasGroup.DOFade(1f, _alertIntroDuration).SetEase(_alertIntroEase))
					.Join(_alertRoot.DOAnchorPos(_alertShownAnchoredPosition, _alertIntroDuration).SetEase(_alertIntroEase))
					.Join(_alertRoot.DOScale(1f, _alertIntroDuration).SetEase(_alertIntroEase));
			}
		}

		private void PlayHideAlertAnimation()
		{
			UtilityMethods.Log("[NotifyManager] PlayHideAlertAnimation !!");
			KillAlertTween();
			KillCriticalDescTween();
			if (_alertRoot == null || _alertCanvasGroup == null)
			{
				ClearAlertRuntimeState();
				SetAlertVisualImmediate(visible: false);
				return;
			}
			Vector2 endValue = _alertShownAnchoredPosition + new Vector2(0f, _alertMoveOffsetY);
			_alertSequence = DOTween.Sequence().SetUpdate(_alertUseUnscaledTime).Append(_alertCanvasGroup.DOFade(0f, _alertOutroDuration).SetEase(_alertOutroEase))
				.Join(_alertRoot.DOAnchorPos(endValue, _alertOutroDuration).SetEase(_alertOutroEase))
				.OnComplete(() =>
				{
					ClearAlertRuntimeState();
					SetAlertVisualImmediate(visible: false);
				});
		}

		private void RefreshCriticalVisualState(AlertPriority priority, bool usePriorityPulse)
		{
			KillCriticalDescTween();
			if (!(_alertDescriptionText == null))
			{
				_alertDescriptionText.color = _alertDescBaseColor;
				if (usePriorityPulse && (priority == AlertPriority.Critical || priority == AlertPriority.High))
				{
					_criticalDescTween = _alertDescriptionText.DOColor(_criticalDescPulseColor, _criticalDescPulseDuration).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
						.SetUpdate(_alertUseUnscaledTime);
				}
			}
		}

		private void KillCriticalDescTween()
		{
			if (_criticalDescTween != null && _criticalDescTween.IsActive())
			{
				_criticalDescTween.Kill();
			}
			_criticalDescTween = null;
			if (_alertDescriptionText != null)
			{
				_alertDescriptionText.color = _alertDescBaseColor;
			}
		}

		private void KillAlertTween()
		{
			if (_alertSequence != null)
			{
				if (_alertSequence.IsActive())
				{
					_alertSequence.Kill();
				}
				_alertSequence = null;
			}
		}

		private void ClearAlertRuntimeState()
		{
			_isAlertShowing = false;
			_currentAlertHeader = string.Empty;
			_currentAlertDescription = string.Empty;
			_currentAlertDuration = 0f;
			_currentAlertAutoClose = false;
			_currentAlertPriority = AlertPriority.Low;
			_currentAlertUsePriorityPulse = true;
		}

		private void SetAlertVisualImmediate(bool visible)
		{
			if (!(_alertRoot == null) && !(_alertCanvasGroup == null))
			{
				KillCriticalDescTween();
				if (visible)
				{
					_alertRoot.gameObject.SetActive(value: true);
					_alertRoot.anchoredPosition = _alertShownAnchoredPosition;
					_alertRoot.localScale = Vector3.one;
					_alertCanvasGroup.alpha = 1f;
				}
				else
				{
					_alertCanvasGroup.alpha = 0f;
					_alertRoot.anchoredPosition = _alertShownAnchoredPosition;
					_alertRoot.localScale = Vector3.one;
					_alertRoot.gameObject.SetActive(value: false);
				}
			}
		}

		public void ShowLocationFeedback(string title, float duration = -1f)
		{
			InitializeLocationFeedbackVisuals();
			if (!(_locationFeedbackRoot == null) && !(_locationFeedbackCanvasGroup == null) && !(_locationFeedbackTitleText == null) && !string.IsNullOrWhiteSpace(title))
			{
				float delay = ((duration > 0f) ? duration : _locationFeedbackDefaultDuration);
				_currentLocationFeedbackTitle = title.Trim();
				_locationFeedbackTitleText.text = _currentLocationFeedbackTitle;
				PlayShowLocationFeedbackAnimation();
				RestartLocationFeedbackHideTimer(delay);
			}
		}

		public void ForceHideLocationFeedback()
		{
			if (_isLocationFeedbackShowing || (!(_locationFeedbackRoot == null) && _locationFeedbackRoot.gameObject.activeSelf))
			{
				StopLocationFeedbackHideTimer();
				PlayHideLocationFeedbackAnimation();
			}
		}

		private void InitializeLocationFeedbackVisuals()
		{
			if (!_isLocationFeedbackInitialized)
			{
				if (_locationFeedbackRoot != null)
				{
					_locationFeedbackShownAnchoredPosition = _locationFeedbackRoot.anchoredPosition;
				}
				SetLocationFeedbackVisualImmediate(visible: false);
				_isLocationFeedbackInitialized = true;
			}
		}

		private void RestartLocationFeedbackHideTimer(float delay)
		{
			StopLocationFeedbackHideTimer();
			_locationFeedbackHideCoroutine = StartCoroutine(LocationFeedbackHideCountdown(delay));
		}

		private void StopLocationFeedbackHideTimer()
		{
			if (_locationFeedbackHideCoroutine != null)
			{
				StopCoroutine(_locationFeedbackHideCoroutine);
				_locationFeedbackHideCoroutine = null;
			}
		}

		private IEnumerator LocationFeedbackHideCountdown(float delay)
		{
			if (_locationFeedbackUseUnscaledTime)
			{
				yield return new WaitForSecondsRealtime(delay);
			}
			else
			{
				yield return new WaitForSeconds(delay);
			}
			_locationFeedbackHideCoroutine = null;
			PlayHideLocationFeedbackAnimation();
		}

		private void PlayShowLocationFeedbackAnimation()
		{
			KillLocationFeedbackTween();
			_isLocationFeedbackShowing = true;
			if (!(_locationFeedbackRoot == null) && !(_locationFeedbackCanvasGroup == null))
			{
				_locationFeedbackRoot.gameObject.SetActive(value: true);
				Vector2 anchoredPosition = _locationFeedbackShownAnchoredPosition + new Vector2(0f, _locationFeedbackHiddenOffsetY);
				_locationFeedbackRoot.anchoredPosition = anchoredPosition;
				_locationFeedbackRoot.localScale = Vector3.one * _locationFeedbackStartScale;
				_locationFeedbackCanvasGroup.alpha = 0f;
				_locationFeedbackSequence = DOTween.Sequence().SetUpdate(_locationFeedbackUseUnscaledTime).Append(_locationFeedbackCanvasGroup.DOFade(1f, _locationFeedbackIntroDuration).SetEase(_locationFeedbackIntroEase))
					.Join(_locationFeedbackRoot.DOAnchorPos(_locationFeedbackShownAnchoredPosition, _locationFeedbackIntroDuration).SetEase(_locationFeedbackIntroEase))
					.Join(_locationFeedbackRoot.DOScale(1f, _locationFeedbackIntroDuration).SetEase(_locationFeedbackIntroEase));
			}
		}

		private void PlayHideLocationFeedbackAnimation()
		{
			KillLocationFeedbackTween();
			if (_locationFeedbackRoot == null || _locationFeedbackCanvasGroup == null)
			{
				ClearLocationFeedbackRuntimeState();
				SetLocationFeedbackVisualImmediate(visible: false);
				return;
			}
			Vector2 endValue = _locationFeedbackShownAnchoredPosition + new Vector2(0f, _locationFeedbackHiddenOffsetY);
			_locationFeedbackSequence = DOTween.Sequence().SetUpdate(_locationFeedbackUseUnscaledTime).Append(_locationFeedbackCanvasGroup.DOFade(0f, _locationFeedbackOutroDuration).SetEase(_locationFeedbackOutroEase))
				.Join(_locationFeedbackRoot.DOAnchorPos(endValue, _locationFeedbackOutroDuration).SetEase(_locationFeedbackOutroEase))
				.OnComplete(() =>
				{
					ClearLocationFeedbackRuntimeState();
					SetLocationFeedbackVisualImmediate(visible: false);
				});
		}

		private void KillLocationFeedbackTween()
		{
			if (_locationFeedbackSequence != null)
			{
				if (_locationFeedbackSequence.IsActive())
				{
					_locationFeedbackSequence.Kill();
				}
				_locationFeedbackSequence = null;
			}
		}

		private void ClearLocationFeedbackRuntimeState()
		{
			_isLocationFeedbackShowing = false;
			_currentLocationFeedbackTitle = string.Empty;
		}

		private void SetLocationFeedbackVisualImmediate(bool visible)
		{
			if (!(_locationFeedbackRoot == null) && !(_locationFeedbackCanvasGroup == null))
			{
				if (visible)
				{
					_locationFeedbackRoot.gameObject.SetActive(value: true);
					_locationFeedbackRoot.anchoredPosition = _locationFeedbackShownAnchoredPosition;
					_locationFeedbackRoot.localScale = Vector3.one;
					_locationFeedbackCanvasGroup.alpha = 1f;
				}
				else
				{
					_locationFeedbackCanvasGroup.alpha = 0f;
					_locationFeedbackRoot.anchoredPosition = _locationFeedbackShownAnchoredPosition;
					_locationFeedbackRoot.localScale = Vector3.one;
					_locationFeedbackRoot.gameObject.SetActive(value: false);
				}
			}
		}

		private void TestToastInfo()
		{
			ShowToast("This is an informational message");
		}

		private void TestToastSuccess()
		{
			ShowToast("Saved successfully", NotificationState.Success);
		}

		private void TestToastError()
		{
			ShowToast("Something went wrong", NotificationState.Error);
		}

		private void TestProgressMoney()
		{
			ShowMoneyGain(40000);
		}

		private void TestProgressScrap()
		{
			ShowScrapGain(20000);
		}

		private void TestProgressXp()
		{
			ShowXpGain(200);
		}

		private void TestProgressToolUpgrade()
		{
			ShowToolUpgrade(ToolType.Harpoon, 1, 2);
		}

		private void TestProgressHabitat()
		{
			ShowHabitatDiscovered("Sunlight Reef");
		}

		private void TestPopupInfo()
		{
			ShowPopup("Info", "This is an informational message");
		}

		private void TestPopupConfirm()
		{
			ShowPopup("Proceed?", "Do you want to continue?", () =>
			{
				Debug.Log("Confirmed");
			});
		}

		private void TestPopupBoth()
		{
			ShowPopup("Delete?", "Are you sure you want to delete this?", () =>
			{
				Debug.Log("Confirmed");
			}, () =>
			{
				Debug.Log("Cancelled");
			});
		}

		private void TestPopupHide()
		{
			HidePopup();
		}

		private void TestAlertSimple()
		{
			ShowAlert("You have been spotted");
		}

		private void TestAlertHeaderDesc()
		{
			ShowAlert("Warning", "Low oxygen", 2f, AlertPriority.High);
		}

		private void TestAlertZone()
		{
			ShowAlert("Sunlight Zone", 3f, AlertPriority.Low);
		}

		private void TestAlertCritical()
		{
			ShowAlert("Critical", "Pressure unstable", 2.5f, AlertPriority.Critical);
		}

		private void TestAlertSticky()
		{
			ShowAlert("Critical", "Hull pressure unstable", 2f, AlertPriority.Critical, autoClose: false);
		}

		private void TestAlertForceHide()
		{
			ForceHideAlert();
		}

		private void TestLocationFeedbackMarket()
		{
			ShowLocationFeedback("Market");
		}

		private void TestLocationFeedbackFishmonger()
		{
			ShowLocationFeedback("Fishmonger");
		}

		private void TestLocationFeedbackRestaurant()
		{
			ShowLocationFeedback("Restaurant");
		}

		private void TestLocationFeedbackLighthouse()
		{
			ShowLocationFeedback("Lighthouse");
		}

		private void TestLocationFeedbackForceHide()
		{
			ForceHideLocationFeedback();
		}
	}
}
