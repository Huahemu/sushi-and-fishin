namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::UnityEngine;

public class SleddingFrog : Plush
{
	[SerializeField]
	private float strength = 5f;

	[SerializeField]
	private float angularDamping = 2f;

	public override void OnStartServer()
	{
		base.OnStartServer();
		Rb.isKinematic = true;
	}

	private void OnTriggerEnter(Collider other)
	{
		if (base.isServer && Rb.isKinematic && !base.NetworkHolder)
		{
			Rb.isKinematic = false;
		}
	}

	private void OnCollisionEnter(Collision other)
	{
		if (base.isServer && Rb.isKinematic && !base.NetworkHolder)
		{
			Rb.isKinematic = false;
		}
	}

	protected override void OnDropped(PlayerInventory playerInventory)
	{
		base.OnDropped(playerInventory);
		StartCoroutine(FixedUpdateDelay());
	}

	private IEnumerator FixedUpdateDelay()
	{
		yield return new WaitForFixedUpdate();
		Vector3 normalized = (Vector3.down + UnityEngine.Random.insideUnitSphere * Mathf.Tan(MathF.PI / 12f)).normalized;
		Rb.Rotate(Quaternion.LookRotation(normalized));
		Rb.angularVelocity = Vector3.Project(Rb.angularVelocity, base.transform.forward);
	}

	private void FixedUpdate()
	{
		if (base.isServer && !base.NetworkHolder && !Rb.isKinematic)
		{
			Vector3 vector = -base.transform.forward;
			Vector3 up = Vector3.up;
			Vector3 vector2 = Vector3.Cross(vector, up);
			if (!(vector2.sqrMagnitude < 0.001f))
			{
				vector2.Normalize();
				float num = Vector3.Angle(vector, up) * (MathF.PI / 180f);
				Rb.AddTorque(vector2 * (num * strength));
				float num2 = Vector3.Dot(Rb.angularVelocity, vector2);
				Rb.angularVelocity -= vector2 * (num2 * angularDamping * Time.fixedDeltaTime);
			}
		}
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
