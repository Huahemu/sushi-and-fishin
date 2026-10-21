namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::JetBrains.Annotations;
using global::UnityEngine;

public class PlayerEyes : MonoBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private float springStrength = 300f;

	[SerializeField]
	private float damping = 12f;

	[SerializeField]
	private float maxSpeed = 720f;

	[SerializeField]
	private float changeTargetCd = 2f;

	[SerializeField]
	private float eyeRotationClampInDegrees = 60f;

	[Header("References")]
	[SerializeField]
	private Transform eyeLeft;

	[SerializeField]
	private Transform eyeRight;

	[SerializeField]
	private List<PlayerMouth> _mouths = new List<PlayerMouth>();

	[SerializeField]
	[CanBeNull]
	private Transform _targetLookAt;

	private float _lastTargetSetTime;

	private Quaternion _eyeRotationState;

	private Vector3 _eyeAngularVelocity;

	public Transform EyeLeft => eyeLeft;

	public Transform EyeRight => eyeRight;

	private void Awake()
	{
		_eyeRotationState = eyeLeft.rotation;
	}

	private void OnEnable()
	{
		LocalManager instance = MonoSingleton<LocalManager>.Instance;
		instance.OnNewPlayerRegistered = (Action<PlayerReferences>)Delegate.Combine(instance.OnNewPlayerRegistered, new Action<PlayerReferences>(OnPlayerRegistered));
	}

	private void OnDisable()
	{
		if (MonoSingleton<LocalManager>.Instance != null)
		{
			LocalManager instance = MonoSingleton<LocalManager>.Instance;
			instance.OnNewPlayerRegistered = (Action<PlayerReferences>)Delegate.Remove(instance.OnNewPlayerRegistered, new Action<PlayerReferences>(OnPlayerRegistered));
		}
	}

	private void OnPlayerRegistered(PlayerReferences references)
	{
		if (!_mouths.Contains(references.mouth) && !(references.transform.GetComponentInChildren<PlayerEyes>() == this))
		{
			_mouths.Add(references.mouth);
		}
	}

	private void Start()
	{
		foreach (PlayerReferences player in MonoSingleton<LocalManager>.Instance.players)
		{
			if (!_mouths.Contains(player.mouth) && !(player.transform.GetComponentInChildren<PlayerEyes>() == this))
			{
				_mouths.Add(player.mouth);
			}
		}
	}

	private void LateUpdate()
	{
		SelectTargetLookAt();
		if ((bool)_targetLookAt && Vector3.Angle(_targetLookAt.position - base.transform.position, base.transform.forward) > eyeRotationClampInDegrees)
		{
			_targetLookAt = null;
		}
		SmoothRotateEyes();
	}

	private void SelectTargetLookAt()
	{
		if (Time.time - _lastTargetSetTime < changeTargetCd || _mouths == null || _mouths.Count == 0)
		{
			return;
		}
		Transform transform = null;
		float num = float.MinValue;
		foreach (PlayerMouth mouth in _mouths)
		{
			if (!mouth)
			{
				continue;
			}
			Vector3 vector = mouth.headTransform.position - base.transform.position;
			float num2 = Vector3.Angle(vector, base.transform.forward);
			if (!(num2 > eyeRotationClampInDegrees))
			{
				float num3 = Mathf.Clamp(vector.sqrMagnitude, 25f, 2500f);
				float currentAmplitude = mouth.currentAmplitude;
				float num4 = 0f;
				float num5 = num2 / eyeRotationClampInDegrees;
				if (currentAmplitude > 0f)
				{
					num4 = currentAmplitude / num3 * (1f - num5);
				}
				if (num4 > num)
				{
					num = num4;
					transform = mouth.headTransform;
				}
			}
		}
		if (_targetLookAt != transform)
		{
			_targetLookAt = transform;
			_lastTargetSetTime = Time.time;
		}
	}

	private void SmoothRotateEyes()
	{
		Vector3 forward = ((!_targetLookAt) ? base.transform.forward : (_targetLookAt.position - base.transform.position).normalized);
		if (!(forward.sqrMagnitude < 0.0001f))
		{
			(Quaternion.LookRotation(forward, base.transform.up) * Quaternion.Inverse(_eyeRotationState)).ToAngleAxis(out var angle, out var axis);
			if (angle > 180f)
			{
				angle -= 360f;
			}
			Vector3 vector = axis * (angle * springStrength);
			_eyeAngularVelocity += vector * Time.deltaTime;
			_eyeAngularVelocity *= Mathf.Exp((0f - damping) * Time.deltaTime);
			_eyeAngularVelocity = Vector3.ClampMagnitude(_eyeAngularVelocity, maxSpeed);
			Quaternion quaternion = Quaternion.Euler(_eyeAngularVelocity * Time.deltaTime);
			_eyeRotationState = quaternion * _eyeRotationState;
			eyeLeft.rotation = _eyeRotationState;
			eyeRight.rotation = _eyeRotationState;
		}
	}
}

}
