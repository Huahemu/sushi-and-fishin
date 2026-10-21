namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CustomGravity : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	private Rigidbody rb;

	[Header("Optional References")]
	[SerializeField]
	private Transform gravityCenter;

	[Header("Settings")]
	[SerializeField]
	private Vector3 gravityDirection = Vector3.down;

	[SerializeField]
	private float gravityStrength = 9.81f;

	private void Awake()
	{
		if (!rb)
		{
			rb = GetComponent<Rigidbody>();
		}
		rb.useGravity = false;
	}

	private void FixedUpdate()
	{
		if ((bool)gravityCenter)
		{
			Vector3 normalized = (gravityCenter.position - base.transform.position).normalized;
			rb.AddForce(normalized * gravityStrength, ForceMode.Acceleration);
		}
		else
		{
			rb.AddForce(gravityDirection.normalized * gravityStrength, ForceMode.Acceleration);
		}
	}
}

}
