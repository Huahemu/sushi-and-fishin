namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CustomDrag : MonoBehaviour
{
	public Vector3 linearDrag;

	public Vector3 angularDrag;

	private Rigidbody rb;

	private void Awake()
	{
		rb = GetComponent<Rigidbody>();
		rb.linearDamping = 0f;
		rb.angularDamping = 0f;
	}

	private void FixedUpdate()
	{
		if (!rb.isKinematic)
		{
			ApplyLocalLinearDrag();
			ApplyLocalAngularDrag();
		}
	}

	private void ApplyLocalLinearDrag()
	{
		Vector3 direction = base.transform.InverseTransformDirection(rb.linearVelocity);
		direction.x *= 1f - linearDrag.x * Time.fixedDeltaTime;
		direction.y *= 1f - linearDrag.y * Time.fixedDeltaTime;
		direction.z *= 1f - linearDrag.z * Time.fixedDeltaTime;
		rb.linearVelocity = base.transform.TransformDirection(direction);
	}

	private void ApplyLocalAngularDrag()
	{
		Vector3 direction = base.transform.InverseTransformDirection(rb.angularVelocity);
		direction.x *= 1f - angularDrag.x * Time.fixedDeltaTime;
		direction.y *= 1f - angularDrag.y * Time.fixedDeltaTime;
		direction.z *= 1f - angularDrag.z * Time.fixedDeltaTime;
		rb.angularVelocity = base.transform.TransformDirection(direction);
	}
}

}
