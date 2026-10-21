namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public class CameraBreathing : MonoBehaviour
{
	[Header("Settings")]
	public TransformTarget transformTarget;

	public float frequency = 1f;

	public float amplitude = 0.02f;

	public float rotationMultiplier = 1f;

	[Header("Axis")]
	public bool x = true;

	public bool y = true;

	public bool z = true;

	private Vector3 _baseLocalPos;

	private Quaternion _baseLocalRot;

	private float _time;

	private void Awake()
	{
		_baseLocalPos = base.transform.localPosition;
		_baseLocalRot = base.transform.localRotation;
	}

	private void LateUpdate()
	{
		_time += Time.deltaTime * frequency;
		Vector3 vector = new Vector3(x ? Mathf.Sin(_time * 1.1f) : 0f, y ? Mathf.Sin(_time * 1.3f + 1f) : 0f, z ? Mathf.Sin(_time * 0.9f + 2f) : 0f) * amplitude;
		Vector3 euler = vector * rotationMultiplier;
		switch (transformTarget)
		{
		case TransformTarget.Position:
			base.transform.localPosition = _baseLocalPos + vector;
			break;
		case TransformTarget.Rotation:
			base.transform.localRotation = _baseLocalRot * Quaternion.Euler(euler);
			break;
		case TransformTarget.Both:
			base.transform.localPosition = _baseLocalPos + vector;
			base.transform.localRotation = _baseLocalRot * Quaternion.Euler(euler);
			break;
		}
	}
}

}
