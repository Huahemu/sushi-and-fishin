namespace SushiAndFish.Casino.Original
{
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::MoreMountains.Feedbacks;
using global::UnityEngine;

public class PlinkoPuck : NetworkBehaviour
{
	[Header("References")]
	[SerializeField]
	private MMF_Player onHitFb;

	[SerializeField]
	private Rigidbody rb;

	[Header("Puck Settings")]
	[SerializeField]
	private float lifetime = 20f;

	[SerializeField]
	private float minHorizontalSpeedOnCollide = 0.5f;

	[SerializeField]
	private float minVelocityForSound = 0.5f;

	[Header("Debug")]
	public long betAmount;

	public bool hasTouched;

	[Server]
	public void Initialize(long bet)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PlinkoPuck::Initialize(System.Int64)' called when server was not active");
		}
		else
		{
			betAmount = bet;
		}
	}

	public override void OnStartServer()
	{
		base.OnStartServer();
		Invoke("ServerDestroyPuck", lifetime);
	}

	private void OnCollisionEnter(Collision other)
	{
		ClampHorizontalVelocity();
		float num = Mathf.Max(1f, minVelocityForSound);
		if (other.GetContact(0).impulse.sqrMagnitude > num * num)
		{
			RpcPlayFeedbacks();
		}
	}

	[ClientRpc]
	private void RpcPlayFeedbacks()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void PlinkoPuck::RpcPlayFeedbacks()", -410348092, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void ClampHorizontalVelocity()
	{
		Vector3 linearVelocity = rb.linearVelocity;
		Vector3 vector = new Vector3(linearVelocity.x, 0f, linearVelocity.z);
		float magnitude = vector.magnitude;
		if (magnitude < minHorizontalSpeedOnCollide && magnitude > 0.001f)
		{
			Vector3 vector2 = vector.normalized * minHorizontalSpeedOnCollide;
			rb.linearVelocity = new Vector3(vector2.x, linearVelocity.y, vector2.z);
		}
	}

	[Server]
	private void ServerDestroyPuck()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PlinkoPuck::ServerDestroyPuck()' called when server was not active");
		}
		else
		{
			NetworkServer.Destroy(base.gameObject);
		}
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcPlayFeedbacks()
	{
		onHitFb.PlayFeedbacks();
	}

	protected static void InvokeUserCode_RpcPlayFeedbacks(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlayFeedbacks called on server.");
		}
		else
		{
			((PlinkoPuck)obj).UserCode_RpcPlayFeedbacks();
		}
	}

	static PlinkoPuck()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(PlinkoPuck), "System.Void PlinkoPuck::RpcPlayFeedbacks()", InvokeUserCode_RpcPlayFeedbacks);
	}
}

}
