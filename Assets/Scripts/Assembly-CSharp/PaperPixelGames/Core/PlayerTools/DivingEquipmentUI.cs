using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DG.Tweening;
using Michsky.MUIP;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.PlayerTools
{
	public class DivingEquipmentUI : MonoBehaviour
	{
		[Header("References")]
		[SerializeField]
		private GameObject _visualObject;

		[SerializeField]
		private RectTransform _oxygenRect;

		[SerializeField]
		private Slider _oxygenMeter;

		[SerializeField]
		private Slider _oxygenPositiveMeter;

		[SerializeField]
		private Slider _oxygenNegativeMeter;

		[SerializeField]
		private Transform _feedbackTransform;

		[SerializeField]
		private TextMeshProUGUI _debugText;

		[Header("Depth UI")]
		[SerializeField]
		private GameObject _depthUIRoot;

		[SerializeField]
		private RectTransform _depthMeter;

		[SerializeField]
		private TextMeshProUGUI _depthText;

		[SerializeField]
		private float _depthMeterLerpSpeed = 6f;

		[SerializeField]
		private float _maxVisualDepth = 250f;

		[SerializeField]
		private float _depthMeterContentHeight = 2500f;

		[SerializeField]
		private bool _invertDepthMeterY;

		[SerializeField]
		private string _depthTextFormat = "{0:0.0} m";

		[Space]
		[SerializeField]
		private GameObject _knockUI;

		[SerializeField]
		private ProgressBar _knockRadialImage;

		[SerializeField]
		private TextMeshProUGUI _friendsMeterText;

		[Space]
		[SerializeField]
		private float _meterLerpSpeed = 6f;

		[Header("Locked")]
		[SerializeField]
		private Slider _lockedMeter;

		[SerializeField]
		private List<GameObject> _lockedIcons;

		private float _lastOxygen01 = 1f;

		private float _currentDepthSliderValue;

		private Tweener _pulseTween;

		private bool _isLowOxygenPulseActive;

		private Vector3 _iconDefaultScale;

		private const float OxygenChangeEpsilon = 0.0001f;

		private bool _oxygenInitialized;

		private bool _requestedVisibility;

		private bool _visibilitySuppressed;

		private bool _depthVisibilitySuppressed;

		private void Awake()
		{
			if (_feedbackTransform != null)
			{
				_iconDefaultScale = _feedbackTransform.localScale;
			}
			if (_depthMeter != null)
			{
				SetDepthMeter(0f);
				_currentDepthSliderValue = 0f;
			}
			_visualObject.SetActive(value: false);
		}

		private void OnDisable()
		{
			_pulseTween?.Kill();
			_pulseTween = null;
			_isLowOxygenPulseActive = false;
			if (_feedbackTransform != null)
			{
				_feedbackTransform.localScale = _iconDefaultScale;
			}
		}

		private void OnDestroy()
		{
			_pulseTween?.Kill();
			_pulseTween = null;
		}

		public void SetVisibility(bool status)
		{
			_requestedVisibility = status;
			ApplyVisibility();
		}

		public void SetVisibilitySuppressed(bool suppressed)
		{
			_visibilitySuppressed = suppressed;
			ApplyVisibility();
		}

		public void SetDepthVisibilitySuppressed(bool suppressed)
		{
			_depthVisibilitySuppressed = suppressed;
			ApplyDepthVisibility();
		}

		private void ApplyVisibility()
		{
			if (_visualObject != null)
			{
				_visualObject.SetActive(_requestedVisibility && !_visibilitySuppressed);
			}
			ApplyDepthVisibility();
		}

		private void ApplyDepthVisibility()
		{
			if (!(_depthUIRoot == null))
			{
				_depthUIRoot.SetActive(_requestedVisibility && !_visibilitySuppressed && !_depthVisibilitySuppressed);
			}
		}

		private void SetDepthMeter(float depthMeters)
		{
			if (!(_depthMeter == null))
			{
				depthMeters = Mathf.Clamp(depthMeters, 0f, _maxVisualDepth);
				float num = ((_maxVisualDepth <= 0f) ? 0f : (depthMeters / _maxVisualDepth)) * _depthMeterContentHeight;
				if (_invertDepthMeterY)
				{
					num = 0f - num;
				}
				Vector2 anchoredPosition = _depthMeter.anchoredPosition;
				anchoredPosition.y = num;
				_depthMeter.anchoredPosition = anchoredPosition;
			}
		}

		public void SetDepthUI(float currentDepth)
		{
			float target = Mathf.Clamp(currentDepth, 0f, _maxVisualDepth);
			if (_depthMeter != null)
			{
				_currentDepthSliderValue = ExpSmooth(_currentDepthSliderValue, target, _depthMeterLerpSpeed, Time.deltaTime);
				SetDepthMeter(_currentDepthSliderValue);
			}
			if (_depthText != null)
			{
				_depthText.text = string.Format(_depthTextFormat, currentDepth);
			}
		}

		public void SetDepthUIImmediate(float currentDepth)
		{
			float currentDepthSliderValue = Mathf.Clamp(currentDepth, 0f, _maxVisualDepth);
			if (_depthMeter != null)
			{
				_currentDepthSliderValue = currentDepthSliderValue;
				SetDepthMeter(_currentDepthSliderValue);
			}
			if (_depthText != null)
			{
				_depthText.text = string.Format(_depthTextFormat, currentDepth);
			}
		}

		public void SetOxygenLevel(float normalizedValue)
		{
			if (_oxygenMeter == null || _oxygenPositiveMeter == null || _oxygenNegativeMeter == null)
			{
				return;
			}
			normalizedValue = Mathf.Clamp01(normalizedValue);
			if (!_oxygenInitialized)
			{
				SetOxygenLevelImmediate(normalizedValue);
				HandleLowOxygenPulse(normalizedValue);
				return;
			}
			float deltaTime = Time.deltaTime;
			float lastOxygen = _lastOxygen01;
			float num = normalizedValue - lastOxygen;
			bool flag = num > 0.0001f;
			bool flag2 = num < -0.0001f;
			_lastOxygen01 = normalizedValue;
			if (flag)
			{
				_oxygenPositiveMeter.value = normalizedValue;
				_oxygenMeter.value = ExpSmooth(_oxygenMeter.value, normalizedValue, _meterLerpSpeed, deltaTime);
				_oxygenNegativeMeter.value = _oxygenMeter.value;
			}
			else if (flag2)
			{
				_oxygenMeter.value = normalizedValue;
				_oxygenPositiveMeter.value = normalizedValue;
				if (_oxygenNegativeMeter.value < lastOxygen)
				{
					_oxygenNegativeMeter.value = lastOxygen;
				}
				_oxygenNegativeMeter.value = ExpSmooth(_oxygenNegativeMeter.value, normalizedValue, _meterLerpSpeed, deltaTime);
			}
			else
			{
				_oxygenMeter.value = ExpSmooth(_oxygenMeter.value, normalizedValue, _meterLerpSpeed, deltaTime);
				_oxygenPositiveMeter.value = ExpSmooth(_oxygenPositiveMeter.value, normalizedValue, _meterLerpSpeed, deltaTime);
				_oxygenNegativeMeter.value = ExpSmooth(_oxygenNegativeMeter.value, normalizedValue, _meterLerpSpeed, deltaTime);
			}
			HandleLowOxygenPulse(normalizedValue);
		}

		private void SetOxygenLevelImmediate(float normalizedValue)
		{
			normalizedValue = Mathf.Clamp01(normalizedValue);
			_oxygenMeter.value = normalizedValue;
			_oxygenPositiveMeter.value = normalizedValue;
			_oxygenNegativeMeter.value = normalizedValue;
			_lastOxygen01 = normalizedValue;
			_oxygenInitialized = true;
		}

		private void HandleLowOxygenPulse(float normalizedValue)
		{
			if (!(_feedbackTransform == null))
			{
				if (normalizedValue <= 0.1f && !_isLowOxygenPulseActive)
				{
					_isLowOxygenPulseActive = true;
					_pulseTween?.Kill();
					_pulseTween = _feedbackTransform.DOScale(_iconDefaultScale * 1.35f, 0.25f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutQuad)
						.SetUpdate(isIndependentUpdate: true);
				}
				else if (normalizedValue > 0.1f && _isLowOxygenPulseActive)
				{
					StopPulseTween();
				}
			}
		}

		private void StopPulseTween()
		{
			_isLowOxygenPulseActive = false;
			_pulseTween?.Kill();
			_pulseTween = null;
			if (!(_feedbackTransform == null))
			{
				_feedbackTransform.DOScale(_iconDefaultScale, 0.25f).SetEase(Ease.OutQuad).SetUpdate(isIndependentUpdate: true);
			}
		}

		public void ShowKnock()
		{
			if (!(_knockUI == null))
			{
				StopPulseTween();
				if (_knockRadialImage != null)
				{
					DOTween.Kill(_knockRadialImage.transform);
					SetKnockRadial(1f);
					_knockRadialImage.transform.localScale = Vector3.one;
				}
				_knockUI.SetActive(value: true);
				if (_friendsMeterText != null)
				{
					_friendsMeterText.text = string.Empty;
				}
			}
		}

		public void UpdateKnock(float progress01, bool accelerating, string friendMetersOrNull)
		{
			if (!(_knockUI == null))
			{
				SetKnockRadial(progress01);
				if (!string.IsNullOrEmpty(friendMetersOrNull) && _friendsMeterText != null)
				{
					_friendsMeterText.text = friendMetersOrNull;
				}
				if (accelerating && _knockRadialImage != null && !DOTween.IsTweening(_knockRadialImage.transform, alsoCheckIfIsPlaying: true))
				{
					_knockRadialImage.transform.DOPunchScale(Vector3.one * 0.05f, 0.2f, 1, 0.5f).SetUpdate(isIndependentUpdate: true);
				}
			}
		}

		public void HideKnock()
		{
			if (!(_knockUI == null))
			{
				_knockUI.SetActive(value: false);
				SetKnockRadial(1f);
				if (_knockRadialImage != null)
				{
					_knockRadialImage.transform.localScale = Vector3.one;
				}
			}
		}

		private void SetKnockRadial(float progress01)
		{
			if (!(_knockRadialImage == null))
			{
				_knockRadialImage.SetValue(progress01 * 100f);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float ExpSmooth(float current, float target, float speed, float dt)
		{
			float num = 1f - Mathf.Exp((0f - speed) * dt);
			return current + (target - current) * num;
		}

		public void UpdateLockBar(int count)
		{
			count = Mathf.Clamp(count, 0, 5);
			if (_lockedMeter != null)
			{
				_lockedMeter.maxValue = Mathf.Max(_lockedMeter.maxValue, 5f);
				_lockedMeter.value = count;
			}
			int num = ((_lockedIcons != null) ? _lockedIcons.Count : 0);
			for (int i = 0; i < num; i++)
			{
				if (_lockedIcons[i] != null)
				{
					_lockedIcons[i].SetActive(i < count);
				}
			}
			if (!(_oxygenRect == null))
			{
				switch (count)
				{
				case 0:
					_oxygenRect.SetRight(10f);
					break;
				case 1:
					_oxygenRect.SetRight(55f);
					break;
				case 2:
					_oxygenRect.SetRight(100f);
					break;
				case 3:
					_oxygenRect.SetRight(145f);
					break;
				case 4:
					_oxygenRect.SetRight(192.5f);
					break;
				case 5:
					_oxygenRect.SetRight(237.5f);
					break;
				}
			}
		}
	}
}
