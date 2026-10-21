namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::UnityEngine;

public class PlayerHead : NetworkBehaviour
{
	public bool isLocked;

	private PlayerController _pc;

	private CameraSettings _cs;

	private SettingsLayout _settingsLayout;

	private Vector2 _lookInput;

	private float _yaw;

	private float _pitch;

	private Quaternion _targetRotation = Quaternion.identity;

	private bool _invertX;

	private bool _invertY;

	private bool IsFree
	{
		get
		{
			if (!_pc.hasBody)
			{
				return _pc.State == PlayerController.PlayerState.Ragdoll;
			}
			return false;
		}
	}

	private void Awake()
	{
		_cs = SushiAndFish.Casino.Original.CasinoResources.Load<CameraSettings>("CameraSettings");
		_settingsLayout = SushiAndFish.Casino.Original.CasinoResources.Load<SettingsLayout>("SettingsLayout");
		_pc = GetComponentInParent<PlayerController>();
		_targetRotation = Quaternion.identity;
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
        if (CasinoRuntimeContext.IsHostIntegration) return;
		if (!base.isLocalPlayer)
		{
			base.enabled = false;
			return;
		}
		InputEvents.OnAimEvent = (Action<Vector2>)Delegate.Combine(InputEvents.OnAimEvent, new Action<Vector2>(OnLook));
		SettingItemBase.SettingsChanged += OnSettingChanged;
		CacheInvertSettings();
	}

	public override void OnStopClient()
	{
		base.OnStopClient();
		if (base.isLocalPlayer)
		{
			InputEvents.OnAimEvent = (Action<Vector2>)Delegate.Remove(InputEvents.OnAimEvent, new Action<Vector2>(OnLook));
			SettingItemBase.SettingsChanged -= OnSettingChanged;
		}
	}

	private void OnSettingChanged(SettingItemBase setting)
	{
		if (setting is ToggleSettingItem toggleSettingItem && !string.IsNullOrWhiteSpace(toggleSettingItem.key))
		{
			string a = toggleSettingItem.key.Trim();
			if (string.Equals(a, "invertX", StringComparison.OrdinalIgnoreCase))
			{
				_invertX = toggleSettingItem.value;
			}
			else if (string.Equals(a, "invertY", StringComparison.OrdinalIgnoreCase))
			{
				_invertY = toggleSettingItem.value;
			}
		}
	}

	private void OnLook(Vector2 input)
	{
		_lookInput = input;
	}

	private void Update()
	{
        if (CasinoRuntimeContext.IsHostIntegration) { if (CasinoRuntimeContext.PlayerCamera) transform.rotation = CasinoRuntimeContext.PlayerCamera.transform.rotation; return; }
		GetInput();
		RotateHead();
	}

	private void GetInput()
	{
		if (!isLocked)
		{
			bool num = (bool)MonoSingleton<InputModeManager>.Instance && MonoSingleton<InputModeManager>.Instance.IsControllerActive();
			float num2 = Mathf.Min(Time.deltaTime, 0.033f);
			float num3 = ((!num) ? (InputEvents.IsZoomPressed ? (_cs.zoomSensitivity / 30f) : (_cs.sensitivity.value / 30f)) : ((!InputEvents.IsZoomPressed) ? (_cs.controllerSensitivity ? _cs.controllerSensitivity.value : _cs.sensitivity.value) : (_cs.controllerSensitivity ? (_cs.controllerSensitivity.value * 0.6f) : _cs.zoomSensitivity)));
			float num4 = num3;
			float num5 = _lookInput.x * (_invertX ? (-1f) : 1f);
			float num6 = _lookInput.y * (_invertY ? (-1f) : 1f);
			float num7 = num5 * num4;
			float num8 = (0f - num6) * num4;
			if (num)
			{
				num7 *= num2;
				num8 *= num2;
			}
			if (!IsFree)
			{
				SetRotation(_yaw + num7, _pitch + num8);
				return;
			}
			Quaternion quaternion = Quaternion.AngleAxis(num7, _targetRotation * Vector3.up) * Quaternion.AngleAxis(num8, _targetRotation * Vector3.right);
			SetRotationFree(quaternion * _targetRotation);
		}
	}

	private void CacheInvertSettings()
	{
		_invertX = GetInvertSetting("invertX");
		_invertY = GetInvertSetting("invertY");
	}

	private bool GetInvertSetting(string key)
	{
		if (_settingsLayout == null)
		{
			return false;
		}
		foreach (SettingsLayout.Tab tab in _settingsLayout.tabs)
		{
			if (tab == null)
			{
				continue;
			}
			foreach (SettingItemBase entry in tab.entries)
			{
				if (entry is ToggleSettingItem toggleSettingItem && !string.IsNullOrWhiteSpace(toggleSettingItem.key) && string.Equals(toggleSettingItem.key.Trim(), key, StringComparison.OrdinalIgnoreCase))
				{
					return toggleSettingItem.value;
				}
			}
		}
		return false;
	}

	private void RotateHead()
	{
		float cameraLerp = _cs.cameraLerp;
		float t = 1f - Mathf.Exp((0f - cameraLerp) * Time.deltaTime);
		base.transform.localRotation = Quaternion.Slerp(base.transform.localRotation, _targetRotation, t);
	}

	public void SetRotation(float yaw, float pitch)
	{
		_yaw = Mathf.DeltaAngle(0f, yaw);
		_pitch = Mathf.Clamp(pitch, -89f, 89f);
		_targetRotation = Quaternion.Euler(_pitch, _yaw, 0f);
	}

	public void SetRotationFree(Quaternion rotation)
	{
		_targetRotation = rotation;
	}

	public Vector2 GetRotation()
	{
		return new Vector2(_yaw, _pitch);
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
