namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

[CreateAssetMenu(menuName = "Game Settings/Camera Settings", fileName = "CameraSettings")]
public class CameraSettings : ScriptableObject
{
	[Header("Camera Settings")]
	public SliderSettingItem cameraLerpSlider;

	public ToggleSettingItem cameraLerpToggle;

	public SliderSettingItem sensitivity;

	public SliderSettingItem controllerSensitivity;

	[Header("FOV Settings")]
	public SliderSettingItem baseFOV;

	public float zoomLerpSpeed;

	public float runFOVMultiplier;

	public float runFOVThreshold;

	public float runLerpSpeed;

	[Header("Head Sway Settings")]
	public float swayAmountZAxis;

	public float swayAmountXAxis;

	public float swayDamping;

	[Header("HeadBob Settings")]
	public AnimationCurve xCurve;

	public AnimationCurve yCurve;

	public ToggleSettingItem CameraHeadBobToggle;

	public float xAmplitude = 4f;

	public float yAmplitude = 8f;

	public float xFrequency;

	public float yFrequency;

	public float headBobLerpSpeed;

	public float headBobResetLerpSpeed;

	public float cameraLerp
	{
		get
		{
			if (!cameraLerpToggle.value)
			{
				return 100f;
			}
			return cameraLerpSlider.value;
		}
	}

	public float zoomSensitivity => sensitivity.value * 0.6f;

	public float zoomFOV => baseFOV.value * 0.7f;

	public bool bobbingEnabled => CameraHeadBobToggle.value;

	public static event Action<CameraSettings> SettingsChanged;

	private void NotifyChanged()
	{
		SettingsChanged?.Invoke(this);
	}
}

}
