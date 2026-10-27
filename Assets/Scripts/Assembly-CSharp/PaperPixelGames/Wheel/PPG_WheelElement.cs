using System;
using DG.Tweening;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace PaperPixelGames.Wheel
{
	public class PPG_WheelElement : MonoBehaviour
	{
		[SerializeField]
		private PPG_Wheel _wheel;

		[SerializeField]
		private ScriptableObject _data;

		[SerializeField]
		private TextMeshProUGUI _label;

		[SerializeField]
		private Image _icon;

		[SerializeField]
		private BaseButton _button;

		[SerializeField]
		private bool _isUnlocked;

		private Tweener _scaleTween;

		private bool _delayDone;

		private Coroutine _delayCor;

		public IWheelSlice Data => _data as IWheelSlice;

		public bool Unlocked => _isUnlocked;

		public bool HoverSelect { get; private set; }

		public event Action EnterSlice;

		public event Action ExitSlice;

		public void ConfigureRuntime(PPG_Wheel wheel, ScriptableObject data, bool unlocked)
		{
			_wheel = wheel;
			_data = data;
			_isUnlocked = unlocked;
			RefreshContent();
			base.gameObject.name = ((data != null) ? ("WheelElement_" + data.name) : "WheelElement_Runtime");
			base.gameObject.SetActive(value: false);
		}

		private void Awake()
		{
			if (_data == null)
			{
				UtilityMethods.Log("[PPG_WheelElement] Data is null!", LogType.Error);
				return;
			}
			RefreshContent();
            // An inactive element can receive Awake on its first ShowElement.
            // Keep it visible when the wheel has already begun opening.
            if(_wheel==null||!_wheel.WheelStatus)base.gameObject.SetActive(value: false);
		}

		private void OnEnable()
		{
			RefreshContent();
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
			_delayDone = false;
			if (_delayCor != null)
			{
				Singleton<DelayHelper>.Instance.StopCoroutine(_delayCor);
			}
			_delayCor = Singleton<DelayHelper>.Instance.DelayAction(0.1f, () =>
			{
				_delayDone = true;
			});
			_button.Clicked += OnClick;
			_button.HoverChanged += OnHoverChanged;
		}

		private void OnDisable()
		{
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			_button.Clicked -= OnClick;
			_button.HoverChanged -= OnHoverChanged;
		}

		private void OnSelectedLocaleChanged(Locale locale)
		{
			RefreshContent();
		}

		private void RefreshContent()
		{
			if (_data is IWheelSlice wheelSlice)
			{
				if (_label != null)
				{
					_label.text = wheelSlice.WheelName;
				}
				if (_icon != null)
				{
					_icon.sprite = wheelSlice.WheelSprite;
				}
			}
		}

		public void SetLock(bool status)
		{
			_isUnlocked = status;
		}

		private void OnClick()
		{
			HoverSelect = false;
			_wheel.SelectSlice?.Invoke(Data);
		}

		private void OnHoverChanged(bool hover)
		{
			HoverSelect = (_delayDone && _isUnlocked) & hover;
		}

		public void ShowElement(float duration, float delay, Ease easeType)
		{
			if (_isUnlocked)
			{
				base.gameObject.SetActive(value: true);
				_scaleTween?.Kill();
				_scaleTween = base.transform.DOScale(1f, duration).SetEase(easeType).SetDelay(delay)
					.OnComplete(() =>
					{
						_button.SetTweenEnabled(enabled: true);
					});
			}
		}

		public void HideElement(float duration, float delay, Ease easeType)
		{
			if (_isUnlocked)
			{
				_button.SetTweenEnabled(enabled: false);
				_scaleTween?.Kill();
				_scaleTween = base.transform.DOScale(0f, duration).SetEase(easeType).SetDelay(delay)
					.OnComplete(() =>
					{
						base.gameObject.SetActive(value: false);
					});
			}
		}
	}
}
