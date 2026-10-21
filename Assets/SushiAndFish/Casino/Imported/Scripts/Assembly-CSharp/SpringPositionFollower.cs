namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public class SpringPositionFollower : MonoBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private float springStrength = 300f;

	[SerializeField]
	private float damping = 12f;

	[SerializeField]
	private float maxSpeed = 20f;

	[SerializeField]
	private float positionOffsetMultiplier = 0.2f;

	[Header("References")]
	[SerializeField]
	private Transform target;

	private Vector3 _positionState;

	private Vector3 _velocity;

	private void Awake()
	{
		_positionState = base.transform.position;
	}

	private void OnDisable()
	{
		base.transform.localPosition = Vector3.zero;
		_velocity = Vector3.zero;
	}

	private void OnEnable()
	{
		_positionState = target.position;
	}

	private void LateUpdate()
	{
		SmoothMove();
	}

	private void SmoothMove()
	{
		if ((bool)target)
		{
			Vector3 vector = (target.position - _positionState) * springStrength;
			_velocity += vector * Time.deltaTime;
			_velocity *= Mathf.Exp((0f - damping) * Time.deltaTime);
			_velocity = Vector3.ClampMagnitude(_velocity, maxSpeed);
			_positionState += _velocity * Time.deltaTime;
			base.transform.position = Vector3.Lerp(target.position, _positionState, positionOffsetMultiplier);
		}
	}
}

}
