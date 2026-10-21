namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public class TransformLerper : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	private Transform target;

	[Header("Speed Settings")]
	[SerializeField]
	private float maxSpeed = 1f;

	[SerializeField]
	private float maxDistance = 1f;

	[SerializeField]
	private AnimationCurve speedByDistance;

	[Header("Stopping")]
	[SerializeField]
	private float stopDistance = 0.01f;

	private Vector3 _worldPosition;

	private void Awake()
	{
		_worldPosition = base.transform.position;
	}

	private void LateUpdate()
	{
		if ((bool)target)
		{
			Vector3 position = target.position;
			Vector3 vector = position - _worldPosition;
			float magnitude = vector.magnitude;
			if (magnitude <= stopDistance)
			{
				_worldPosition = position;
				base.transform.position = _worldPosition;
				return;
			}
			float time = Mathf.Clamp01(magnitude / maxDistance);
			float a = speedByDistance.Evaluate(time) * maxSpeed * Time.deltaTime;
			a = Mathf.Min(a, magnitude);
			_worldPosition += vector.normalized * a;
			base.transform.position = _worldPosition;
		}
	}
}

}
