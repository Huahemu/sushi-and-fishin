namespace SushiAndFish.Casino.Original
{
using global::DG.Tweening;
using global::UnityEngine;
using global::UnityEngine.Rendering;
using global::UnityEngine.Rendering.Universal;
using global::UnityEngine.SceneManagement;

public class PlayerBuffUI : MonoBehaviour
{
	public Transform melodyEffectUI;

	public Volume drunkVolume;

	private LensDistortion _lensDistortion;

	private Tween _loopTweenX;

	private Tween _loopTweenY;

	private Tween _resetTween;

	public Volume immunityVolume;

	private Bloom _bloom;

	private Tween _immunityTween;

	[Header("SFX")]
	[SerializeField]
	private SFXLocalLoopComponent drunkSfx;

	private void Start()
	{
		drunkVolume.profile.TryGet<LensDistortion>(out _lensDistortion);
		immunityVolume.profile.TryGet<Bloom>(out _bloom);
	}

	private void OnEnable()
	{
		SceneManager.sceneLoaded += OnSceneChanged;
	}

	private void OnDisable()
	{
		SceneManager.sceneLoaded -= OnSceneChanged;
		ResetAllEffects();
	}

	private void OnSceneChanged(Scene scene, LoadSceneMode mode)
	{
		ResetAllEffects();
	}

	private void ResetAllEffects()
	{
		OnTipsyFortuneChanged(1f);
		OnInspiringMelodyChanged(0f);
		OnImmunityChanged(0f);
	}

	public void OnChanged(PlayerBuffType type, float value)
	{
		switch (type)
		{
		case PlayerBuffType.TipsyFortune:
			OnTipsyFortuneChanged(value);
			break;
		case PlayerBuffType.InspiringMelody:
			OnInspiringMelodyChanged(value);
			break;
		case PlayerBuffType.Immunity:
			OnImmunityChanged(value);
			break;
		}
	}

	private void OnTipsyFortuneChanged(float value)
	{
		bool flag = value > 1f;
		drunkSfx.LoopSFX(flag);
		if (!(_lensDistortion == null))
		{
			if (flag)
			{
				StartPingPong();
			}
			else
			{
				StopAndResetToZero();
			}
		}
	}

	private void StartPingPong()
	{
		_resetTween?.Kill();
		_loopTweenX?.Kill();
		drunkVolume.weight = 1f;
		_lensDistortion.xMultiplier.value = 0f;
		_lensDistortion.yMultiplier.value = 1f;
		float t = 0f;
		_loopTweenX = DOTween.To(() => t, (float v) =>
		{
			t = v;
			_lensDistortion.xMultiplier.value = t;
		}, 0.8f, 2f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
		_loopTweenY = DOTween.To(() => t, (float v) =>
		{
			t = v;
			_lensDistortion.yMultiplier.value = 1f - t;
		}, 0.8f, 3f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
	}

	private void StopAndResetToZero()
	{
		_loopTweenX?.Kill();
		_loopTweenY?.Kill();
		_resetTween?.Kill();
		_resetTween = DOTween.To(() => new Vector2(_lensDistortion.xMultiplier.value, _lensDistortion.yMultiplier.value), (Vector2 v) =>
		{
			_lensDistortion.xMultiplier.value = v.x;
			_lensDistortion.yMultiplier.value = v.y;
			drunkVolume.weight = Mathf.Clamp01(v.x);
		}, Vector2.zero, 0.25f).SetEase(Ease.OutCubic);
	}

	private void OnInspiringMelodyChanged(float value)
	{
		melodyEffectUI.gameObject.SetActive(value > 0f);
	}

	private void OnImmunityChanged(float value)
	{
		if ((bool)_bloom)
		{
			immunityVolume.weight = ((value > 0f) ? 1f : 0f);
			_immunityTween?.Kill();
			_immunityTween = DOTween.To(() => _bloom.intensity.value, (float v) =>
			{
				immunityVolume.priority = ((v > 0f) ? 1 : 0);
				_bloom.intensity.value = v;
			}, (value > 0f) ? 5f : 0f, 0.5f).SetEase(Ease.OutCubic);
		}
	}
}

}
