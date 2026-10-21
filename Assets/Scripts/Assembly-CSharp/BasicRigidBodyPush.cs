using UnityEngine;

public class BasicRigidBodyPush : MonoBehaviour
{
	public LayerMask pushLayers;

	public bool canPush;

	[Range(0.5f, 5f)]
	public float strength = 1.1f;

	private void OnControllerColliderHit(ControllerColliderHit hit)
	{
		if (canPush)
		{
			PushRigidBodies(hit);
		}
	}

	private void PushRigidBodies(ControllerColliderHit hit)
	{
		Rigidbody attachedRigidbody = hit.collider.attachedRigidbody;
		if (!(attachedRigidbody == null) && !attachedRigidbody.isKinematic && ((1 << attachedRigidbody.gameObject.layer) & pushLayers.value) != 0 && !(hit.moveDirection.y < -0.3f))
		{
			Vector3 vector = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
			attachedRigidbody.AddForce(vector * strength, ForceMode.Impulse);
		}
	}
}
