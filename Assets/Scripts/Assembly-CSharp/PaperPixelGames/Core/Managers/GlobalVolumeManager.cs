using DG.Tweening;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PaperPixelGames.Core.Managers
{
	public sealed class GlobalVolumeManager : Singleton<GlobalVolumeManager>
	{
		[SerializeField]
		private bool _log;

		private Volume _activeVolume;

		private Vignette _vignette;

		private Color _defaultColor;

		private float _defaultIntensity;

		private float _defaultSmoothness;

		private bool _defaultColorOverride;

		private bool _defaultIntensityOverride;

		private bool _defaultSmoothnessOverride;

		private bool _divingKnockActive;

		private Sequence _vignetteTween;

		private float _knockStartIntensity;

		private float _knockStartSmoothness;

		[SerializeField]
		[Range(0f, 1f)]
		private float _o2StartThreshold = 0.35f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _o2CriticalThreshold = 0.1f;

		[SerializeField]
		private Color _o2Color = new Color(0.25f, 0.45f, 0.75f, 1f);

		[SerializeField]
		[Range(0f, 1f)]
		private float _o2MaxIntensity = 0.35f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _o2MaxSmoothness = 0.7f;

		[SerializeField]
		private Color _knockColor = new Color(0.05f, 0.05f, 0.07f, 1f);

		[SerializeField]
		[Range(0f, 1f)]
		private float _knockMaxIntensity = 0.491f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _knockMaxSmoothness = 0.774f;

		[SerializeField]
		private float _enterKnockTween = 0.12f;

		[SerializeField]
		private float _exitKnockTween = 0.2f;

		[SerializeField]
		private Ease _enterEase = Ease.OutQuad;

		[SerializeField]
		private Ease _exitEase = Ease.OutQuad;

		public override SingletonScope Scope => SingletonScope.Scene;

		protected override void Awake()
		{
			base.Awake();
		}

		private bool SetActiveVolume(Volume volume)
		{
			if (_activeVolume == volume)
			{
				return _vignette != null;
			}
			KillAllTweens();
			RestoreActiveVignetteImmediate();
			_activeVolume = null;
			_vignette = null;
			if (volume == null)
			{
				if (_log)
				{
					Debug.LogWarning("[GlobalVolumeManager] Active underwater Volume reference is missing.");
				}
				return false;
			}
			if (volume.profile == null)
			{
				if (_log)
				{
					Debug.LogWarning("[GlobalVolumeManager] Volume profile is missing. Volume=" + volume.name);
				}
				return false;
			}
			if (!volume.profile.TryGet<Vignette>(out _vignette))
			{
				if (_log)
				{
					Debug.LogWarning("[GlobalVolumeManager] Vignette override was not found. Volume=" + volume.name);
				}
				return false;
			}
			_activeVolume = volume;
			_defaultColor = _vignette.color.value;
			_defaultIntensity = _vignette.intensity.value;
			_defaultSmoothness = _vignette.smoothness.value;
			_defaultColorOverride = _vignette.color.overrideState;
			_defaultIntensityOverride = _vignette.intensity.overrideState;
			_defaultSmoothnessOverride = _vignette.smoothness.overrideState;
			_vignette.color.overrideState = true;
			_vignette.intensity.overrideState = true;
			_vignette.smoothness.overrideState = true;
			return true;
		}

		private void OnDisable()
		{
			KillAllTweens();
			RestoreActiveVignetteImmediate();
			_activeVolume = null;
			_vignette = null;
			_divingKnockActive = false;
		}

		private void OnDestroy()
		{
			KillAllTweens();
			ResetVignetteToDefaultsImmediate();
		}

		public void KillAllTweens()
		{
			_vignetteTween?.Kill();
			_vignetteTween = null;
		}

		private void ResetVignetteToDefaultsImmediate()
		{
			RestoreActiveVignetteImmediate();
			_divingKnockActive = false;
		}

		private void RestoreActiveVignetteImmediate()
		{
			if (!(_vignette == null))
			{
				_vignette.color.value = _defaultColor;
				_vignette.intensity.value = _defaultIntensity;
				_vignette.smoothness.value = _defaultSmoothness;
				_vignette.color.overrideState = _defaultColorOverride;
				_vignette.intensity.overrideState = _defaultIntensityOverride;
				_vignette.smoothness.overrideState = _defaultSmoothnessOverride;
			}
		}

		public void TickDivingO2(Volume volume, float o2Normalized)
		{
			if (SetActiveVolume(volume) && !_divingKnockActive)
			{
				float num = RiskFromThresholds(o2Normalized, _o2StartThreshold, _o2CriticalThreshold);
				if (num <= 0f)
				{
					float unscaledDeltaTime = Time.unscaledDeltaTime;
					float num2 = 8f;
					_vignette.color.value = Color.Lerp(_vignette.color.value, _defaultColor, 1f - Mathf.Exp((0f - num2) * unscaledDeltaTime));
					_vignette.intensity.value = Mathf.Lerp(_vignette.intensity.value, _defaultIntensity, 1f - Mathf.Exp((0f - num2) * unscaledDeltaTime));
					_vignette.smoothness.value = Mathf.Lerp(_vignette.smoothness.value, _defaultSmoothness, 1f - Mathf.Exp((0f - num2) * unscaledDeltaTime));
				}
				else
				{
					float b = Mathf.Min(_o2MaxIntensity, _knockMaxIntensity);
					float b2 = Mathf.Min(_o2MaxSmoothness, _knockMaxSmoothness);
					float value = Mathf.Lerp(_defaultIntensity, b, num);
					float value2 = Mathf.Lerp(_defaultSmoothness, b2, num);
					_vignette.color.value = _o2Color;
					_vignette.intensity.value = Mathf.Clamp01(value);
					_vignette.smoothness.value = Mathf.Clamp01(value2);
				}
			}
		}

		public void EnterDivingKnock(Volume volume)
		{
			if (SetActiveVolume(volume))
			{
				_divingKnockActive = true;
				KillAllTweens();
				CaptureKnockStartValues();
				_vignetteTween = DOTween.Sequence().Append(DOTween.To(() => _vignette.color.value, (Color v) =>
				{
					_vignette.color.value = v;
				}, _knockColor, _enterKnockTween)).SetUpdate(isIndependentUpdate: true)
					.SetEase(_enterEase);
			}
		}

		public void TickDivingKnockGiveUp(Volume volume, float progress01)
		{
			bool flag = _activeVolume != volume;
			if (SetActiveVolume(volume) && _divingKnockActive)
			{
				if (flag)
				{
					CaptureKnockStartValues();
				}
				float t = 1f - Mathf.Clamp01(progress01);
				_vignette.intensity.value = Mathf.Clamp01(Mathf.Lerp(_knockStartIntensity, _knockMaxIntensity, t));
				_vignette.smoothness.value = Mathf.Clamp01(Mathf.Lerp(_knockStartSmoothness, _knockMaxSmoothness, t));
			}
		}

		private void CaptureKnockStartValues()
		{
			_knockStartIntensity = Mathf.Max(_vignette.intensity.value, _defaultIntensity);
			_knockStartSmoothness = Mathf.Max(_vignette.smoothness.value, _defaultSmoothness);
		}

		public void ExitDivingKnock()
		{
			if (!(_vignette == null))
			{
				_divingKnockActive = false;
				KillAllTweens();
				_vignetteTween = DOTween.Sequence().Append(DOTween.To(() => _vignette.color.value, (Color v) =>
				{
					_vignette.color.value = v;
				}, _defaultColor, _exitKnockTween)).Join(DOTween.To(() => _vignette.intensity.value, (float v) =>
				{
					_vignette.intensity.value = v;
				}, _defaultIntensity, _exitKnockTween))
					.Join(DOTween.To(() => _vignette.smoothness.value, (float v) =>
					{
						_vignette.smoothness.value = v;
					}, _defaultSmoothness, _exitKnockTween))
					.SetUpdate(isIndependentUpdate: true)
					.SetEase(_exitEase);
			}
		}

		private static float RiskFromThresholds(float o2Normalized, float start, float critical)
		{
			if (o2Normalized >= start)
			{
				return 0f;
			}
			if (o2Normalized <= critical)
			{
				return 1f;
			}
			return 1f - Mathf.InverseLerp(critical, start, o2Normalized);
		}
	}
}
