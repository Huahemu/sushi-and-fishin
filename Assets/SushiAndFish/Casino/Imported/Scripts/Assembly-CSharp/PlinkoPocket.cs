namespace SushiAndFish.Casino.Original
{
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::MoreMountains.Feedbacks;
using global::UnityEngine;

public class PlinkoPocket : NetworkBehaviour
{
	[Header("Pocket Settings")]
	[SerializeField]
	private int slotIndex;

	[SerializeField]
	private Plinko plinkoGame;

	[SerializeField]
	private MMF_Player onEnterFb;

	private void OnTriggerEnter(Collider other)
	{
		if (base.isServer && (bool)other.attachedRigidbody && other.attachedRigidbody.TryGetComponent<PlinkoPuck>(out var component) && !component.hasTouched)
		{
			component.hasTouched = true;
			plinkoGame.OnPuckEnteredPocket(slotIndex, component);
			NetworkServer.Destroy(component.gameObject);
			RpcPlayFeedbacks();
		}
	}

	[ClientRpc]
	private void RpcPlayFeedbacks()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void PlinkoPocket::RpcPlayFeedbacks()", -2139126725, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcPlayFeedbacks()
	{
		onEnterFb.PlayFeedbacks();
	}

	protected static void InvokeUserCode_RpcPlayFeedbacks(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlayFeedbacks called on server.");
		}
		else
		{
			((PlinkoPocket)obj).UserCode_RpcPlayFeedbacks();
		}
	}

	static PlinkoPocket()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(PlinkoPocket), "System.Void PlinkoPocket::RpcPlayFeedbacks()", InvokeUserCode_RpcPlayFeedbacks);
	}
}

}
