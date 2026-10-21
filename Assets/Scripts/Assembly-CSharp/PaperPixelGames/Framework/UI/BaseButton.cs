using System;
using DG.Tweening;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace PaperPixelGames.Framework.UI
{
	public class BaseButton : Selectable, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, ISubmitHandler, ICancelHandler
	{
		private enum InputSource
		{
			Pointer = 0,
			Navigation = 1
		}

		[Flags]
		public enum VisualChannels
		{
			None = 0,
			Scale = 1,
			Rotation = 2,
			All = Scale | Rotation
		}

		public enum ClickTrigger
		{
			PointerClick = 0,
			PointerUpInside = 1
		}

		[Serializable]
		public class VisualPreset
		{
			public VisualChannels Channels = VisualChannels.Scale;

			public float Duration = 0.12f;

			public Ease Ease = Ease.OutQuad;

			public Vector3 Scale = Vector3.one;

			public bool UsePunchScale;

			public float PunchScale = 0.12f;

			public int PunchVibrato = 8;

			[Range(0f, 1f)]
			public float PunchElasticity = 0.6f;

			public Vector3 LocalEulerAngles = Vector3.zero;
		}

		[Serializable]
		public class SfxSettings
		{
			public bool Enabled = true;

			public SoundType Hover = SoundType.Click_Down;

			public SoundType Click = SoundType.Click_Down;

			public SoundType DisabledClick = SoundType.Click_Down;

			public float Cooldown = 0.03f;
		}

		[Tooltip("Optional: target for scale/rotation. If null, uses this.transform.")]
		[SerializeField]
		private Transform _transformTarget;

		[Tooltip("Optional: activated while this button is selected, deactivated otherwise.")]
		[SerializeField]
		private GameObject _selectedGameObject;

		[SerializeField]
		private ClickTrigger _clickTrigger;

		[SerializeField]
		private float _clickCooldown;

		[SerializeField]
		private bool _useUnscaledTime = true;

		[SerializeField]
		private bool _leftClickOnly = true;

		[SerializeField]
		private bool _pointerUpMustBeInside = true;

		[SerializeField]
		private bool _cancelPressOnPointerExit = true;

		[SerializeField]
		private bool _forceClearStatesOnDisable = true;

		[SerializeField]
		private bool _selectOnPointerDown;

		[SerializeField]
		private VisualPreset _normal = new VisualPreset
		{
			Channels = VisualChannels.Scale,
			Scale = Vector3.one,
			LocalEulerAngles = Vector3.zero,
			Duration = 0.12f,
			Ease = Ease.OutQuad
		};

		[SerializeField]
		private VisualPreset _hovered = new VisualPreset
		{
			Channels = VisualChannels.Scale,
			Scale = Vector3.one * 1.06f,
			LocalEulerAngles = Vector3.zero,
			Duration = 0.12f,
			Ease = Ease.OutQuad
		};

		[SerializeField]
		private VisualPreset _pressed = new VisualPreset
		{
			Channels = VisualChannels.Scale,
			Scale = Vector3.one * 0.92f,
			LocalEulerAngles = Vector3.zero,
			Duration = 0.06f,
			Ease = Ease.OutQuad
		};

		[SerializeField]
		private VisualPreset _selected = new VisualPreset
		{
			Channels = VisualChannels.Scale,
			Scale = Vector3.one * 1.04f,
			LocalEulerAngles = Vector3.zero,
			Duration = 0.1f,
			Ease = Ease.OutQuad
		};

		[SerializeField]
		private VisualPreset _disabled = new VisualPreset
		{
			Channels = VisualChannels.Scale,
			Scale = Vector3.one,
			LocalEulerAngles = Vector3.zero,
			Duration = 0.1f,
			Ease = Ease.OutQuad
		};

		[SerializeField]
		private SfxSettings _sfx = new SfxSettings();

		[SerializeField]
		private UnityEvent _onClick;

		[SerializeField]
		private UnityEvent _onHoverEnter;

		[SerializeField]
		private UnityEvent _onHoverExit;

		[SerializeField]
		private UnityEvent _onPressDown;

		[SerializeField]
		private UnityEvent _onPressUp;

		[SerializeField]
		private UnityEvent _onSelected;

		[SerializeField]
		private UnityEvent _onDeselected;

		private Transform _tTarget;

		private Vector3 _defaultScale;

		private Vector3 _defaultLocalEuler;

		private bool _defaultsCached;

		private InputSource _lastInputSource;

		private bool _pointerInside;

		private bool _isPressed;

		private bool _isSelected;

		private bool _hoverEffective;

		private bool _pendingPointerUpClick;

		private float _lastClickTime = -999f;

		private object _tweenKeyScale;

		private object _tweenKeyRotation;

		private float _lastSfxTime = -999f;

		private bool _pointerHoverSfxArmed = true;

		private bool _navSelectSfxArmed = true;

		private bool _firedClickThisPress;

		private bool _tweensEnabled = true;

		[SerializeField]
		private TMP_Text _label;

		private bool _labelCached;

		private LocalizeStringEvent _labelLocalizer;

		public event Action Clicked;

		public event Action<bool> HoverChanged;

		public event Action<bool> PressChanged;

		protected override void Awake()
		{
			base.Awake();
			_tweenKeyScale = new object();
			_tweenKeyRotation = new object();
			ResolveTargets();
			CacheDefaults(force: true);
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			ResolveTargets();
			CacheDefaultsIfNeeded();
			KillAllTweens();
			RefreshHoverEffective();
			RefreshSelectedGameObject();
			RefreshVisualState(immediate: true);
		}

		protected override void OnDisable()
		{
			KillAllTweens();
			_firedClickThisPress = false;
			if (_forceClearStatesOnDisable)
			{
				_pointerInside = false;
				_pendingPointerUpClick = false;
				if (_isPressed)
				{
					SetPressed(pressed: false);
				}
				if (_hoverEffective)
				{
					SetHover(hovered: false);
				}
				_isSelected = false;
				_lastInputSource = InputSource.Pointer;
				_pointerHoverSfxArmed = true;
				_navSelectSfxArmed = true;
			}
			SetSelectedGameObjectActive(active: false);
			if (_isSelected)
			{
				_onDeselected?.Invoke();
			}
			base.OnDisable();
		}

		protected override void OnDestroy()
		{
			KillAllTweens();
			Clicked = null;
			HoverChanged = null;
			PressChanged = null;
			base.OnDestroy();
		}

		public override void OnSelect(BaseEventData eventData)
		{
			bool flag = eventData is PointerEventData;
			_lastInputSource = ((!flag) ? InputSource.Navigation : InputSource.Pointer);
			_isSelected = true;
			_firedClickThisPress = false;
			if (!flag)
			{
				if (_navSelectSfxArmed)
				{
					TryPlaySfx(_sfx.Hover);
				}
				_navSelectSfxArmed = false;
			}
			RefreshHoverEffective();
			RefreshSelectedGameObject();
			RefreshVisualState(immediate: false);
			_onSelected?.Invoke();
			base.OnSelect(eventData);
		}

		public override void OnDeselect(BaseEventData eventData)
		{
			_lastInputSource = InputSource.Navigation;
			bool isSelected = _isSelected;
			_isSelected = false;
			_firedClickThisPress = false;
			if (_isPressed)
			{
				SetPressed(pressed: false);
			}
			_navSelectSfxArmed = true;
			RefreshHoverEffective();
			RefreshSelectedGameObject();
			RefreshVisualState(immediate: false);
			if (isSelected)
			{
				_onDeselected?.Invoke();
			}
			base.OnDeselect(eventData);
		}

		public override void OnPointerEnter(PointerEventData eventData)
		{
			_lastInputSource = InputSource.Pointer;
			_pointerInside = true;
			RefreshHoverEffective();
			RefreshVisualState(immediate: false);
		}

		public override void OnPointerExit(PointerEventData eventData)
		{
			_lastInputSource = InputSource.Pointer;
			_pointerInside = false;
			if (_cancelPressOnPointerExit && _isPressed)
			{
				SetPressed(pressed: false);
			}
			RefreshHoverEffective();
			RefreshVisualState(immediate: false);
		}

		public override void OnPointerDown(PointerEventData eventData)
		{
			_lastInputSource = InputSource.Pointer;
			if (!_leftClickOnly || eventData.button == PointerEventData.InputButton.Left)
			{
				if (!IsInteractable())
				{
					TryPlaySfx(_sfx.DisabledClick);
					return;
				}
				TrySelectSelf(eventData);
				_pendingPointerUpClick = _clickTrigger == ClickTrigger.PointerUpInside;
				SetPressed(pressed: true);
				_firedClickThisPress = false;
				RefreshVisualState(immediate: false);
			}
		}

		public override void OnPointerUp(PointerEventData eventData)
		{
			_lastInputSource = InputSource.Pointer;
			if (!_leftClickOnly || eventData.button == PointerEventData.InputButton.Left)
			{
				bool isPressed = _isPressed;
				SetPressed(pressed: false);
				RefreshVisualState(immediate: false);
				if (isPressed && _clickTrigger == ClickTrigger.PointerUpInside && IsInteractable() && (!_pointerUpMustBeInside || _pointerInside) && _pendingPointerUpClick)
				{
					_pendingPointerUpClick = false;
					TryFireClick(fromPointer: true);
				}
			}
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			_lastInputSource = InputSource.Pointer;
			if (!IsInteractable())
			{
				TryPlaySfx(_sfx.DisabledClick);
			}
			else if ((!_leftClickOnly || eventData.button == PointerEventData.InputButton.Left) && _clickTrigger == ClickTrigger.PointerClick)
			{
				TryFireClick(fromPointer: true);
			}
		}

		public void OnSubmit(BaseEventData eventData)
		{
			_lastInputSource = InputSource.Navigation;
			_firedClickThisPress = false;
			if (!IsInteractable())
			{
				TryPlaySfx(_sfx.DisabledClick);
			}
			else
			{
				TryFireClick(fromPointer: false);
			}
		}

		public void OnCancel(BaseEventData eventData)
		{
			_lastInputSource = InputSource.Navigation;
			if (_isPressed)
			{
				SetPressed(pressed: false);
				RefreshVisualState(immediate: false);
			}
		}

		private void TryFireClick(bool fromPointer)
		{
			if (IsInteractable())
			{
				float num = (_useUnscaledTime ? Time.unscaledTime : Time.time);
				if ((!(_clickCooldown > 0f) || !(num - _lastClickTime < _clickCooldown)) && !_firedClickThisPress)
				{
					_firedClickThisPress = true;
					_lastClickTime = num;
					FireClick(fromPointer);
				}
			}
		}

		protected virtual void FireClick(bool fromPointer)
		{
			TryPlaySfx(_sfx.Click);
			Clicked?.Invoke();
			_onClick?.Invoke();
		}

		private void RefreshHoverEffective()
		{
			if (!IsInteractable())
			{
				SetHover(hovered: false);
				return;
			}
			bool hover = _lastInputSource == InputSource.Pointer && _pointerInside && !_isSelected;
			SetHover(hover);
		}

		private void SetHover(bool hovered)
		{
			if (_hoverEffective == hovered)
			{
				return;
			}
			_hoverEffective = hovered;
			if (_hoverEffective)
			{
				if (_pointerHoverSfxArmed)
				{
					TryPlaySfx(_sfx.Hover);
				}
				_pointerHoverSfxArmed = false;
				_onHoverEnter?.Invoke();
			}
			else
			{
				_pointerHoverSfxArmed = true;
				_onHoverExit?.Invoke();
			}
			HoverChanged?.Invoke(_hoverEffective);
		}

		private void SetPressed(bool pressed)
		{
			if (_isPressed != pressed)
			{
				_isPressed = pressed;
				if (_isPressed)
				{
					_onPressDown?.Invoke();
				}
				else
				{
					_onPressUp?.Invoke();
				}
				PressChanged?.Invoke(_isPressed);
			}
		}

		private VisualPreset ResolvePreset()
		{
			if (!IsInteractable())
			{
				return _disabled;
			}
			if (_isPressed)
			{
				return _pressed;
			}
			if (_hoverEffective)
			{
				return _hovered;
			}
			if (_isSelected && _lastInputSource == InputSource.Navigation)
			{
				return _selected;
			}
			return _normal;
		}

		private void RefreshVisualState(bool immediate)
		{
			ResolveTargets();
			CacheDefaultsIfNeeded();
			VisualPreset preset = ResolvePreset();
			ApplyPreset(preset, immediate);
		}

		public void SetTweenEnabled(bool enabled)
		{
			_tweensEnabled = enabled;
		}

		private void ApplyPreset(VisualPreset preset, bool immediate)
		{
			if (preset != null && _tweensEnabled)
			{
				float duration = (immediate ? 0f : Mathf.Max(0f, preset.Duration));
				if ((preset.Channels & VisualChannels.Scale) != VisualChannels.None)
				{
					ApplyScale(preset, duration);
				}
				if ((preset.Channels & VisualChannels.Rotation) != VisualChannels.None)
				{
					ApplyRotation(preset, duration);
				}
			}
		}

		private void ApplyScale(VisualPreset preset, float duration)
		{
			DOTween.Kill(_tweenKeyScale);
			if (preset.UsePunchScale && duration > 0f)
			{
				_tTarget.localScale = _defaultScale;
				_tTarget.DOPunchScale(Vector3.one * preset.PunchScale, duration, preset.PunchVibrato, preset.PunchElasticity).SetEase(preset.Ease).SetUpdate(_useUnscaledTime)
					.SetLink(base.gameObject)
					.SetId(_tweenKeyScale);
				return;
			}
			Vector3 vector = new Vector3(_defaultScale.x * preset.Scale.x, _defaultScale.y * preset.Scale.y, _defaultScale.z * preset.Scale.z);
			if (duration <= 0f)
			{
				_tTarget.localScale = vector;
			}
			else
			{
				_tTarget.DOScale(vector, duration).SetEase(preset.Ease).SetUpdate(_useUnscaledTime)
					.SetLink(base.gameObject)
					.SetId(_tweenKeyScale);
			}
		}

		private void ApplyRotation(VisualPreset preset, float duration)
		{
			DOTween.Kill(_tweenKeyRotation);
			Vector3 vector = _defaultLocalEuler + preset.LocalEulerAngles;
			if (duration <= 0f)
			{
				_tTarget.localRotation = Quaternion.Euler(vector);
			}
			else
			{
				_tTarget.DOLocalRotate(vector, duration, RotateMode.FastBeyond360).SetEase(preset.Ease).SetUpdate(_useUnscaledTime)
					.SetLink(base.gameObject)
					.SetId(_tweenKeyRotation);
			}
		}

		private void KillAllTweens()
		{
			DOTween.Kill(_tweenKeyScale);
			DOTween.Kill(_tweenKeyRotation);
		}

		private void ResolveTargets()
		{
			_tTarget = ((_transformTarget != null) ? _transformTarget : base.transform);
		}

		private void CacheDefaultsIfNeeded()
		{
			if (!_defaultsCached)
			{
				CacheDefaults(force: false);
			}
		}

		private void CacheDefaults(bool force)
		{
			ResolveTargets();
			_defaultScale = _tTarget.localScale;
			_defaultLocalEuler = _tTarget.localEulerAngles;
			_defaultsCached = true;
		}

		private void TryPlaySfx(SoundType type)
		{
			if (!_sfx.Enabled || type == SoundType.Empty)
			{
				return;
			}
			float num = (_useUnscaledTime ? Time.unscaledTime : Time.time);
			if (!(_sfx.Cooldown > 0f) || !(num - _lastSfxTime < _sfx.Cooldown))
			{
				_lastSfxTime = num;
				if (!(Singleton<SoundManager>.Instance == null))
				{
					Singleton<SoundManager>.Instance.PlaySFX(type);
				}
			}
		}

		private void CacheLabelIfNeeded()
		{
			if (!_labelCached)
			{
				if (_label == null)
				{
					_label = GetComponentInChildren<TMP_Text>(includeInactive: true);
				}
				if (_label != null)
				{
					_labelLocalizer = _label.GetComponent<LocalizeStringEvent>();
				}
				_labelCached = true;
			}
		}

		public void SetText(string value)
		{
			CacheLabelIfNeeded();
			if (_label == null)
			{
				Debug.LogWarning("[BaseButton] No TMP_Text found on " + base.name);
			}
			else
			{
				_label.text = value ?? string.Empty;
			}
		}

		public void SetLocalizedText(string tableName, string key, string fallback)
		{
			CacheLabelIfNeeded();
			if (_label == null)
			{
				Debug.LogWarning("[BaseButton] No TMP_Text found on " + base.name);
				return;
			}
			_label.text = fallback ?? string.Empty;
			if (_labelLocalizer == null)
			{
				Debug.LogWarning("[BaseButton] No LocalizeStringEvent found on label for " + base.name);
				return;
			}
			_labelLocalizer.StringReference = new LocalizedString(tableName, key);
			_labelLocalizer.RefreshString();
		}

		public void SetSelected(bool selected)
		{
			if (_isSelected != selected)
			{
				_lastInputSource = InputSource.Navigation;
				_isSelected = selected;
				_firedClickThisPress = false;
				RefreshHoverEffective();
				RefreshSelectedGameObject();
				RefreshVisualState(immediate: false);
				if (_isSelected)
				{
					_onSelected?.Invoke();
				}
				else
				{
					_onDeselected?.Invoke();
				}
			}
		}

		private void TrySelectSelf(BaseEventData eventData)
		{
			if (_selectOnPointerDown && IsActive() && IsInteractable() && !(EventSystem.current == null) && !(EventSystem.current.currentSelectedGameObject == base.gameObject))
			{
				EventSystem.current.SetSelectedGameObject(base.gameObject, eventData);
			}
		}

		private void RefreshSelectedGameObject()
		{
			SetSelectedGameObjectActive(_isSelected);
		}

		private void SetSelectedGameObjectActive(bool active)
		{
			if (!(_selectedGameObject == null) && _selectedGameObject.activeSelf != active)
			{
				_selectedGameObject.SetActive(active);
			}
		}
	}
}
