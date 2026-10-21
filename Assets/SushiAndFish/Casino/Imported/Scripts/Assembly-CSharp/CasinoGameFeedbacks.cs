namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::DG.Tweening;
using global::FMODUnity;
using global::MoreMountains.Feedbacks;
using global::UnityEngine;

public class CasinoGameFeedbacks : MonoBehaviour
{
	[Serializable]
	public struct LightFeedbackData
	{
		public float threshold;

		public Color color;

		public float intensity;

		public float duration;
	}

	[Header("References")]
	[SerializeField]
	private Renderer[] lightRenderers;

	[SerializeField]
	private Light[] lights;

	[SerializeField]
	private List<LightFeedbackData> lightFeedbacks;

	[SerializeField]
	private MMF_Player onGameResultFeedback;

	[SerializeField]
	private MMF_Player onGameWinFeedback;

	[SerializeField]
	private MMF_Player onGameLoseFeedback;

	[SerializeField]
	private MMF_Player onGameTieFeedback;

	[SerializeField]
	private ParticleSystem[] resultParticles;

	[Header("Settings")]
	[SerializeField]
	private float lightTweenDuration;

	[SerializeField]
	private Gradient onWinColor;

	[SerializeField]
	private Gradient onLoseColor;

	[SerializeField]
	private Gradient onTieColor;

	[Header("SFX")]
	[SerializeField]
	private EventReference winSFX;

	[SerializeField]
	private EventReference loseSFX;

	[SerializeField]
	private EventReference tieSFX;

	private MaterialPropertyBlock[][] _mpbsPerRenderer;

	private Color[][] _defaultColorsPerRenderer;

	private Color[][] _startColorsPerRenderer;

	private Color[] _defaultLightColors;

	private Color[] _startLightColors;

	private Tween _lightTween;

	private void Awake()
	{
		if (lightRenderers.Length != 0)
		{
			_mpbsPerRenderer = new MaterialPropertyBlock[lightRenderers.Length][];
			_defaultColorsPerRenderer = new Color[lightRenderers.Length][];
			_startColorsPerRenderer = new Color[lightRenderers.Length][];
			for (int i = 0; i < lightRenderers.Length; i++)
			{
				Renderer renderer = lightRenderers[i];
				int num = renderer.sharedMaterials.Length;
				_mpbsPerRenderer[i] = new MaterialPropertyBlock[num];
				_defaultColorsPerRenderer[i] = new Color[num];
				_startColorsPerRenderer[i] = new Color[num];
				for (int j = 0; j < num; j++)
				{
					_mpbsPerRenderer[i][j] = new MaterialPropertyBlock();
					_defaultColorsPerRenderer[i][j] = renderer.sharedMaterials[j].GetColor("_EmissionColor");
				}
			}
		}
		if (lights.Length != 0)
		{
			_defaultLightColors = new Color[lights.Length];
			_startLightColors = new Color[lights.Length];
			for (int k = 0; k < lights.Length; k++)
			{
				_defaultLightColors[k] = lights[k].color;
			}
		}
	}

	public void PlayGameResultFeedback(double multiplier)
	{
		LightFeedbacks(multiplier);
		SfxFeedback(multiplier);
		GameFeedback(multiplier);
	}

	private void LightFeedbacks(double multiplier)
	{
		if ((lightRenderers.Length == 0 && lights.Length == 0) || lightFeedbacks.Count <= 0)
		{
			return;
		}
		LightFeedbackData data = lightFeedbacks[0];
		foreach (LightFeedbackData lightFeedback in lightFeedbacks)
		{
			if ((double)lightFeedback.threshold <= multiplier)
			{
				data = lightFeedback;
				continue;
			}
			break;
		}
		if (_lightTween != null && _lightTween.IsActive())
		{
			_lightTween.Kill();
		}
		StartLightTween(data);
	}

	private void StartLightTween(LightFeedbackData data)
	{
		_lightTween?.Kill();
		for (int i = 0; i < lightRenderers.Length; i++)
		{
			Renderer renderer = lightRenderers[i];
			int num = renderer.sharedMaterials.Length;
			for (int j = 0; j < num; j++)
			{
				MaterialPropertyBlock materialPropertyBlock = _mpbsPerRenderer[i][j];
				renderer.GetPropertyBlock(materialPropertyBlock);
				_startColorsPerRenderer[i][j] = materialPropertyBlock.GetColor("_EmissionColor");
			}
		}
		for (int k = 0; k < lights.Length; k++)
		{
			_startLightColors[k] = lights[k].color;
		}
		float t = 0f;
		Color target = data.color * data.intensity;
		_lightTween = DOTween.Sequence().Append(DOTween.To(() => t, (float x) =>
		{
			t = x;
			for (int l = 0; l < lightRenderers.Length; l++)
			{
				Renderer renderer2 = lightRenderers[l];
				int num2 = renderer2.sharedMaterials.Length;
				for (int m = 0; m < num2; m++)
				{
					MaterialPropertyBlock materialPropertyBlock2 = _mpbsPerRenderer[l][m];
					Color value = Color.Lerp(_startColorsPerRenderer[l][m], target, t);
					materialPropertyBlock2.SetColor("_EmissionColor", value);
					renderer2.SetPropertyBlock(materialPropertyBlock2, m);
				}
			}
			for (int n = 0; n < lights.Length; n++)
			{
				lights[n].color = Color.Lerp(_startLightColors[n], target, t);
			}
		}, 1f, lightTweenDuration).SetEase(Ease.OutQuad)).AppendInterval(data.duration)
			.Append(DOTween.To(() => t, (float x) =>
			{
				t = x;
				for (int l = 0; l < lightRenderers.Length; l++)
				{
					Renderer renderer2 = lightRenderers[l];
					int num2 = renderer2.sharedMaterials.Length;
					for (int m = 0; m < num2; m++)
					{
						MaterialPropertyBlock materialPropertyBlock2 = _mpbsPerRenderer[l][m];
						Color value = Color.Lerp(_defaultColorsPerRenderer[l][m], target, t);
						materialPropertyBlock2.SetColor("_EmissionColor", value);
						renderer2.SetPropertyBlock(materialPropertyBlock2, m);
					}
				}
				for (int n = 0; n < lights.Length; n++)
				{
					lights[n].color = Color.Lerp(_defaultLightColors[n], target, t);
				}
			}, 0f, lightTweenDuration).SetEase(Ease.InQuad));
	}

	private void GameFeedback(double multiplier)
	{
		ParticleSystem[] array = resultParticles;
		if (array != null && array.Length > 0 && lightFeedbacks.Count > 0)
		{
			LightFeedbackData lightFeedbackData = lightFeedbacks[0];
			foreach (LightFeedbackData lightFeedback in lightFeedbacks)
			{
				if ((double)lightFeedback.threshold <= multiplier)
				{
					lightFeedbackData = lightFeedback;
					continue;
				}
				break;
			}
			array = resultParticles;
			for (int i = 0; i < array.Length; i++)
			{
				ParticleSystem.MainModule main = array[i].main;
				main.startColor = lightFeedbackData.color;
			}
		}
		onGameResultFeedback.PlayFeedbacks();
		if (multiplier > 1.0)
		{
			onGameWinFeedback.PlayFeedbacks();
		}
		else if (multiplier < 1.0)
		{
			onGameLoseFeedback.PlayFeedbacks();
		}
		else
		{
			onGameTieFeedback.PlayFeedbacks();
		}
	}

	private void SfxFeedback(double multiplier)
	{
		if (multiplier > 1.0)
		{
			SFXParams[] sFXParams = new SFXParams[1]
			{
				new SFXParams("Multiplier", Mathf.Clamp((float)multiplier, 0f, 1000f))
			};
			SFXManager.SFXOneShotWithParameters(winSFX, sFXParams, base.transform.position);
		}
		else if (multiplier < 1.0)
		{
			SFXManager.SFXOneShot(loseSFX, base.transform.position);
		}
		else
		{
			SFXManager.SFXOneShot(tieSFX, base.transform.position);
		}
	}
}

}
