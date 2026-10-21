namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public class SpringRotationFollower : MonoBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private float springStrength = 300f;

	[SerializeField]
	private float damping = 12f;

	[SerializeField]
	private float maxSpeed = 720f;

	[Header("References")]
	[SerializeField]
	private Transform target;

	private Quaternion _rotationState;

	private Vector3 _angularVelocity;

	private void Awake()
	{
		_rotationState = target.rotation;
	}

	private void LateUpdate()
	{
		SmoothRotate();
	}

	private void SmoothRotate()
	{
		Vector3 forward = target.forward;
		if (!(forward.sqrMagnitude < 0.0001f))
		{
			(Quaternion.LookRotation(forward, target.up) * Quaternion.Inverse(_rotationState)).ToAngleAxis(out var angle, out var axis);
			if (angle > 180f)
			{
				angle -= 360f;
			}
			Vector3 vector = axis * (angle * springStrength);
			_angularVelocity += vector * Time.deltaTime;
			_angularVelocity *= Mathf.Exp((0f - damping) * Time.deltaTime);
			_angularVelocity = Vector3.ClampMagnitude(_angularVelocity, maxSpeed);
			Quaternion quaternion = Quaternion.Euler(_angularVelocity * Time.deltaTime);
			_rotationState = quaternion * _rotationState;
			base.transform.rotation = _rotationState;
		}
	}
}

}
