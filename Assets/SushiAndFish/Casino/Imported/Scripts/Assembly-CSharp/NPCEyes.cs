namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

public class NPCEyes : MonoBehaviour
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

	[SerializeField]
	private float interestRadius = 25f;

	[Header("References")]
	[SerializeField]
	private Transform eyeLeft;

	[SerializeField]
	private Transform eyeRight;

	[SerializeField]
	private Transform headTransform;

	private Transform _targetLookAt;

	private Vector3 _targetPosition;

	private bool _hasPositionTarget;

	private float _lastTargetSetTime;

	private Quaternion _eyeRotationState;

	private Vector3 _eyeAngularVelocity;

	private float _interestRadiusSqr;

	private void Awake()
	{
		if (eyeLeft != null)
		{
			_eyeRotationState = eyeLeft.rotation;
		}
		if (headTransform == null)
		{
			headTransform = base.transform;
		}
		_interestRadiusSqr = interestRadius * interestRadius;
	}

	private void LateUpdate()
	{
		SelectTargetLookAt();
		if ((bool)_targetLookAt && Vector3.Angle(_targetLookAt.position - headTransform.position, headTransform.forward) > eyeRotationClampInDegrees)
		{
			_targetLookAt = null;
		}
		SmoothRotateEyes();
	}

	private void SelectTargetLookAt()
	{
		if (Time.time - _lastTargetSetTime < changeTargetCd)
		{
			return;
		}
		Transform transform = null;
		Vector3 zero = Vector3.zero;
		bool flag = false;
		float num = float.MinValue;
		if (MonoSingleton<LocalManager>.Instance != null)
		{
			foreach (PlayerReferences player in MonoSingleton<LocalManager>.Instance.players)
			{
				if (player?.headTransform == null)
				{
					continue;
				}
				Vector3 vector = player.headTransform.position - headTransform.position;
				float sqrMagnitude = vector.sqrMagnitude;
				if (sqrMagnitude > _interestRadiusSqr)
				{
					continue;
				}
				float num2 = Vector3.Angle(vector, headTransform.forward);
				if (!(num2 > eyeRotationClampInDegrees))
				{
					float num3 = 1f / (sqrMagnitude + 1f) * (1f - num2 / eyeRotationClampInDegrees);
					if (num3 > num)
					{
						num = num3;
						transform = player.headTransform;
						flag = false;
					}
				}
			}
		}
		if (flag)
		{
			_targetLookAt = null;
			_targetPosition = zero;
			_hasPositionTarget = true;
			_lastTargetSetTime = Time.time;
		}
		else if (_targetLookAt != transform)
		{
			_targetLookAt = transform;
			_hasPositionTarget = false;
			_lastTargetSetTime = Time.time;
		}
	}

	private void SmoothRotateEyes()
	{
		Vector3 forward = (_hasPositionTarget ? (_targetPosition - headTransform.position).normalized : ((!_targetLookAt) ? headTransform.forward : (_targetLookAt.position - headTransform.position).normalized));
		if (!(forward.sqrMagnitude < 0.0001f))
		{
			(Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Inverse(_eyeRotationState)).ToAngleAxis(out var angle, out var axis);
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
			if (eyeLeft != null)
			{
				eyeLeft.rotation = _eyeRotationState;
			}
			if (eyeRight != null)
			{
				eyeRight.rotation = _eyeRotationState;
			}
		}
	}
}

}
