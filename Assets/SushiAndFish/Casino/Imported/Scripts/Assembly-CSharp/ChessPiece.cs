namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::UnityEngine;

public class ChessPiece : Item
{
	[Header("Settings")]
	[SerializeField]
	private float strength = 15f;

	[SerializeField]
	private float angularDamping = 5f;

	private Coroutine _colRoutine;

	private LayerMask _excludeLayers;

	private void Start()
	{
		_excludeLayers = SushiAndFish.Casino.CasinoLayers.GetMask("Player", "SelfMeshPlayer");
	}

	private void FixedUpdate()
	{
		if (!Rb.isKinematic)
		{
			Vector3 up = base.transform.up;
			Vector3 up2 = Vector3.up;
			Vector3 vector = Vector3.Cross(up, up2);
			if (!(vector.sqrMagnitude < 0.001f))
			{
				vector.Normalize();
				float num = Vector3.Angle(up, up2) * (MathF.PI / 180f);
				Rb.AddTorque(vector * (num * strength));
				float num2 = Vector3.Dot(Rb.angularVelocity, vector);
				Rb.angularVelocity -= vector * (num2 * angularDamping * Time.fixedDeltaTime);
			}
		}
	}

	protected override void OnPickedUp(PlayerInventory playerInventory)
	{
		base.OnPickedUp(playerInventory);
		if (_colRoutine != null)
		{
			StopCoroutine(_colRoutine);
		}
		Rb.excludeLayers = _excludeLayers;
	}

	protected override void OnDropped(PlayerInventory playerInventory)
	{
		base.OnDropped(playerInventory);
		_colRoutine = StartCoroutine(DelayedEnableColliders());
	}

	private IEnumerator DelayedEnableColliders()
	{
		yield return new WaitForSeconds(0.5f);
		Rb.excludeLayers = 0;
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
