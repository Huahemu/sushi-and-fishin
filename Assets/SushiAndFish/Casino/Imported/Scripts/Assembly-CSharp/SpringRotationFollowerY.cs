namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public class SpringRotationFollowerY : MonoBehaviour
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

	private float _currentYaw;

	private float _angularVelocity;

	private void Awake()
	{
		_currentYaw = base.transform.localEulerAngles.y;
	}

	private void LateUpdate()
	{
		SmoothRotateY();
	}

	private void SmoothRotateY()
	{
		Vector3 vector = (base.transform.parent ? base.transform.parent.InverseTransformDirection(target.forward) : target.forward);
		vector.y = 0f;
		if (!(vector.sqrMagnitude < 0.0001f))
		{
			vector.Normalize();
			float num = Mathf.Atan2(vector.x, vector.z) * 57.29578f;
			float num2 = Mathf.DeltaAngle(_currentYaw, num) * springStrength;
			_angularVelocity += num2 * Time.deltaTime;
			_angularVelocity *= Mathf.Exp((0f - damping) * Time.deltaTime);
			_angularVelocity = Mathf.Clamp(_angularVelocity, 0f - maxSpeed, maxSpeed);
			_currentYaw += _angularVelocity * Time.deltaTime;
			base.transform.localRotation = Quaternion.Euler(0f, _currentYaw, 0f);
		}
	}
}

}
