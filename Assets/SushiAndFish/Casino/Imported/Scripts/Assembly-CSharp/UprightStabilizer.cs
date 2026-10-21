namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class UprightStabilizer : MonoBehaviour
{
	[SerializeField]
	private float strength = 5f;

	[SerializeField]
	private float angularDamping = 2f;

	private Rigidbody _rb;

	private void Awake()
	{
		_rb = GetComponent<Rigidbody>();
	}

	private void FixedUpdate()
	{
		if (!_rb.isKinematic)
		{
			Vector3 up = base.transform.up;
			Vector3 up2 = Vector3.up;
			Vector3 vector = Vector3.Cross(up, up2);
			if (!(vector.sqrMagnitude < 0.001f))
			{
				vector.Normalize();
				float num = Vector3.Angle(up, up2) * (MathF.PI / 180f);
				_rb.AddTorque(vector * (num * strength));
				float num2 = Vector3.Dot(_rb.angularVelocity, vector);
				_rb.angularVelocity -= vector * (num2 * angularDamping * Time.fixedDeltaTime);
			}
		}
	}
}

}
