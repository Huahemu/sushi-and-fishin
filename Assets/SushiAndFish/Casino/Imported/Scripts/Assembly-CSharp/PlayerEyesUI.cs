namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;
using global::UnityEngine.Rendering;
using global::UnityEngine.Rendering.Universal;

public class PlayerEyesUI : MonoBehaviour
{
	[SerializeField]
	private Volume blindnessVolume;

	private Vignette _vignette;

	private bool _leftEyeEnabled = true;

	private bool _rightEyeEnabled = true;

	private void Awake()
	{
		if (blindnessVolume != null && blindnessVolume.profile != null)
		{
			blindnessVolume.profile.TryGet<Vignette>(out _vignette);
		}
	}

	public void ToggleEye(bool isRightEye, bool isEnabled)
	{
		if (!isRightEye)
		{
			_leftEyeEnabled = isEnabled;
		}
		else
		{
			_rightEyeEnabled = isEnabled;
		}
		UpdateVignette();
	}

	private void UpdateVignette()
	{
		if (_vignette == null)
		{
			return;
		}
		bool flag = !_leftEyeEnabled;
		bool flag2 = !_rightEyeEnabled;
		Vector2 x;
		if (flag & flag2)
		{
			blindnessVolume.weight = 1f;
			x = new Vector2(0.5f, 2f);
		}
		else if (flag)
		{
			blindnessVolume.weight = 1f;
			x = new Vector2(0.75f, 0.5f);
		}
		else
		{
			if (!flag2)
			{
				blindnessVolume.weight = 0f;
				return;
			}
			blindnessVolume.weight = 1f;
			x = new Vector2(0.25f, 0.5f);
		}
		_vignette.active = true;
		_vignette.center.Override(x);
	}
}

}
