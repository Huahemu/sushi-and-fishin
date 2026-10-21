namespace SushiAndFish.Casino.Original
{
using global::Mirror;
using SushiAndFish.Casino.Original.Smooth;
using global::UnityEngine;

public class SmoothSyncMirrorExamplePlayerController : NetworkBehaviour
{
	private Rigidbody rb;

	private Rigidbody2D rb2D;

	private SmoothSyncMirror smoothSync;

	public float transformMovementSpeed = 30f;

	public float rigidbodyMovementForce = 500f;

	public GameObject childObjectToControl;

	private void Start()
	{
		rb = GetComponent<Rigidbody>();
		rb2D = GetComponent<Rigidbody2D>();
		smoothSync = GetComponent<SmoothSyncMirror>();
		if ((bool)smoothSync)
		{
			smoothSync.validateStateMethod = validateStateOfPlayer;
		}
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.T))
		{
			if (base.isOwned)
			{
				base.transform.position = base.transform.position + Vector3.right * 18f;
				smoothSync.teleportOwnedObjectFromOwner();
			}
			else if (NetworkServer.active)
			{
				smoothSync.teleportAnyObjectFromServer(base.transform.position + Vector3.right * 18f, base.transform.rotation, base.transform.localScale);
			}
		}
		if (!base.isOwned && (!NetworkServer.active || base.netIdentity.connectionToClient != null))
		{
			return;
		}
		if (Input.GetKeyDown(KeyCode.F))
		{
			smoothSync.forceStateSendNextFixedUpdate();
		}
		Input.GetKeyDown(KeyCode.C);
		float num = transformMovementSpeed * Time.deltaTime;
		if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.Equals))
		{
			base.transform.localScale = base.transform.localScale + new Vector3(1f, 1f, 1f) * num * 0.2f;
		}
		if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.Minus))
		{
			base.transform.localScale = base.transform.localScale - new Vector3(1f, 1f, 1f) * num * 0.2f;
		}
		if ((bool)childObjectToControl)
		{
			if (Input.GetKey(KeyCode.RightShift) && Input.GetKey(KeyCode.Equals))
			{
				childObjectToControl.transform.localScale = childObjectToControl.transform.localScale + new Vector3(1f, 1f, 1f) * num * 0.2f;
			}
			if (Input.GetKey(KeyCode.RightShift) && Input.GetKey(KeyCode.Minus))
			{
				childObjectToControl.transform.localScale = childObjectToControl.transform.localScale - new Vector3(1f, 1f, 1f) * num * 0.2f;
			}
		}
		if ((bool)childObjectToControl)
		{
			if (Input.GetKey(KeyCode.S))
			{
				childObjectToControl.transform.position = childObjectToControl.transform.position + new Vector3(0f, -1.5f, -1f) * num;
			}
			if (Input.GetKey(KeyCode.W))
			{
				childObjectToControl.transform.position = childObjectToControl.transform.position + new Vector3(0f, 1.5f, 1f) * num;
			}
			if (Input.GetKey(KeyCode.A))
			{
				childObjectToControl.transform.position = childObjectToControl.transform.position + new Vector3(-1f, 0f, 0f) * num;
			}
			if (Input.GetKey(KeyCode.D))
			{
				childObjectToControl.transform.position = childObjectToControl.transform.position + new Vector3(1f, 0f, 0f) * num;
			}
		}
		if ((bool)rb)
		{
			if (Input.GetKeyDown(KeyCode.Alpha0))
			{
				rb.linearVelocity = Vector3.zero;
				rb.angularVelocity = Vector3.zero;
			}
			if (Input.GetKeyDown(KeyCode.DownArrow))
			{
				rb.AddForce(new Vector3(0f, -1.5f, -1f) * rigidbodyMovementForce);
			}
			if (Input.GetKeyDown(KeyCode.UpArrow))
			{
				rb.AddForce(new Vector3(0f, 1.5f, 1f) * rigidbodyMovementForce);
			}
			if (Input.GetKeyDown(KeyCode.LeftArrow))
			{
				rb.AddForce(new Vector3(-1f, 0f, 0f) * rigidbodyMovementForce);
			}
			if (Input.GetKeyDown(KeyCode.RightArrow))
			{
				rb.AddForce(new Vector3(1f, 0f, 0f) * rigidbodyMovementForce);
			}
		}
		else if ((bool)rb2D)
		{
			if (Input.GetKeyDown(KeyCode.Alpha0))
			{
				rb2D.linearVelocity = Vector3.zero;
				rb2D.angularVelocity = 0f;
			}
			if (Input.GetKeyDown(KeyCode.DownArrow))
			{
				rb2D.AddForce(new Vector3(0f, -1.5f, -1f) * rigidbodyMovementForce);
			}
			if (Input.GetKeyDown(KeyCode.UpArrow))
			{
				rb2D.AddForce(new Vector3(0f, 1.5f, 1f) * rigidbodyMovementForce);
			}
			if (Input.GetKeyDown(KeyCode.LeftArrow))
			{
				rb2D.AddForce(new Vector3(-1f, 0f, 0f) * rigidbodyMovementForce);
			}
			if (Input.GetKeyDown(KeyCode.RightArrow))
			{
				rb2D.AddForce(new Vector3(1f, 0f, 0f) * rigidbodyMovementForce);
			}
		}
		else
		{
			if (Input.GetKey(KeyCode.DownArrow))
			{
				base.transform.position = base.transform.position + new Vector3(0f, 0f, -1f) * num;
			}
			if (Input.GetKey(KeyCode.UpArrow))
			{
				base.transform.position = base.transform.position + new Vector3(0f, 0f, 1f) * num;
			}
			if (Input.GetKey(KeyCode.LeftArrow))
			{
				base.transform.position = base.transform.position + new Vector3(-1f, 0f, 0f) * num;
			}
			if (Input.GetKey(KeyCode.RightArrow))
			{
				base.transform.position = base.transform.position + new Vector3(1f, 0f, 0f) * num;
			}
		}
	}

	public static bool validateStateOfPlayer(StateMirror latestReceivedState, StateMirror latestValidatedState)
	{
		if (Vector3.Distance(latestReceivedState.position, latestValidatedState.position) > 9000f && latestReceivedState.ownerTimestamp - latestValidatedState.receivedOnServerTimestamp < 0.5f)
		{
			return false;
		}
		return true;
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
