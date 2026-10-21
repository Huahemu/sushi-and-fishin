using DG.Tweening;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using VLB;

namespace PaperPixelGames.Core.Lighthouse
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(VolumetricLightBeamSD))]
	public sealed class LighthouseBeamLightFader : MonoBehaviour
	{
		[SerializeField]
		private VolumetricLightBeamSD _beam;

		[SerializeField]
		[Range(0f, 1f)]
		private float _dayIntensityMultiplier;

		[SerializeField]
		[Range(0f, 2f)]
		private float _nightIntensityMultiplier = 1f;

		[SerializeField]
		private bool _disableBeamWhenOff = true;

		[SerializeField]
		[Min(0f)]
		private float _offThreshold = 0.01f;

		private CozyWeatherManager _cozyWeatherManager;

		private Tweener _intensityTween;

		private float _nightIntensityInside;

		private float _nightIntensityOutside;

		private bool _isBloomBound;

		private void Reset()
		{
			_beam = GetComponent<VolumetricLightBeamSD>();
		}

		private void Awake()
		{
			ResolveBeam();
			if (!(_beam == null))
			{
				_nightIntensityInside = Mathf.Max(0f, _beam.intensityInside);
				_nightIntensityOutside = Mathf.Max(0f, _beam.intensityOutside);
			}
		}

		private void OnEnable()
		{
			if (TryBindBloomUpdates())
			{
				RefreshIntensityImmediate();
			}
		}

		private void Start()
		{
			if (!_isBloomBound && TryBindBloomUpdates())
			{
				RefreshIntensityImmediate();
			}
		}

		private void OnDisable()
		{
			UnbindBloomUpdates();
			KillIntensityTween();
		}

		private void OnValidate()
		{
			ResolveBeam();
		}

		private void ResolveBeam()
		{
			if (_beam == null)
			{
				_beam = GetComponent<VolumetricLightBeamSD>();
			}
		}

		private bool TryBindBloomUpdates()
		{
			if (_isBloomBound)
			{
				return true;
			}
			_cozyWeatherManager = Singleton<GameProgressManager>.Instance?.CozyWeatherManager;
			if (_cozyWeatherManager == null)
			{
				UtilityMethods.Log("[LighthouseBeamLightFader] CozyWeatherManager reference is missing for beam light sync.", LogType.Warning);
				return false;
			}
			_cozyWeatherManager.BloomIntensityUpdated += HandleBloomIntensityUpdated;
			_isBloomBound = true;
			return true;
		}

		private void UnbindBloomUpdates()
		{
			if (_isBloomBound)
			{
				if (_cozyWeatherManager != null)
				{
					_cozyWeatherManager.BloomIntensityUpdated -= HandleBloomIntensityUpdated;
				}
				_cozyWeatherManager = null;
				_isBloomBound = false;
			}
		}

		private void RefreshIntensityImmediate()
		{
			if (!(_cozyWeatherManager == null))
			{
				ApplyIntensityByBloom(_cozyWeatherManager.GetSynchronizedBloomIntensityTarget(), 0f, Ease.Linear);
			}
		}

		private void HandleBloomIntensityUpdated(float bloomIntensity, float duration, Ease ease)
		{
			ApplyIntensityByBloom(bloomIntensity, duration, ease);
		}

		private void ApplyIntensityByBloom(float bloomIntensity, float duration, Ease ease)
		{
			if (_beam == null)
			{
				return;
			}
			float t = Mathf.Clamp01(bloomIntensity);
			float num = Mathf.Lerp(_dayIntensityMultiplier, _nightIntensityMultiplier, t);
			float targetInside = Mathf.Max(0f, _nightIntensityInside * num);
			float targetOutside = Mathf.Max(0f, _nightIntensityOutside * num);
			KillIntensityTween();
			if (duration <= 0f)
			{
				SetBeamIntensity(targetInside, targetOutside);
				SetBeamEnabledForIntensity(targetInside, targetOutside);
				return;
			}
			if (Mathf.Max(targetInside, targetOutside) > _offThreshold)
			{
				_beam.enabled = true;
			}
			float startInside = _beam.intensityInside;
			float startOutside = _beam.intensityOutside;
			_intensityTween = DOTween.To(() => 0f, (float progress) =>
			{
				SetBeamIntensity(Mathf.Lerp(startInside, targetInside, progress), Mathf.Lerp(startOutside, targetOutside, progress));
			}, 1f, duration).SetEase(ease).SetUpdate(isIndependentUpdate: true)
				.OnComplete(() =>
				{
					_intensityTween = null;
					SetBeamEnabledForIntensity(targetInside, targetOutside);
				});
		}

		private void SetBeamIntensity(float inside, float outside)
		{
			if (!(_beam == null))
			{
				_beam.intensityInside = inside;
				_beam.intensityOutside = outside;
				_beam.UpdateAfterManualPropertyChange();
			}
		}

		private void SetBeamEnabledForIntensity(float inside, float outside)
		{
			if (!(_beam == null))
			{
				if (_disableBeamWhenOff)
				{
					_beam.enabled = Mathf.Max(inside, outside) > _offThreshold;
				}
				else
				{
					_beam.enabled = true;
				}
			}
		}

		private void KillIntensityTween()
		{
			_intensityTween?.Kill();
			_intensityTween = null;
		}
	}
}
