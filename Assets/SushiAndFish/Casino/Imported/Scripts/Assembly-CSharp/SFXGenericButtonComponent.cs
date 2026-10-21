namespace SushiAndFish.Casino.Original
{
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class SFXGenericButtonComponent : NetworkBehaviour
{
	[SerializeField]
	private EventReference buttonSFX;

	private InteractableEventTrigger interactableEventTrigger;

	private void Awake()
	{
		interactableEventTrigger = GetComponent<InteractableEventTrigger>();
	}

	private void OnEnable()
	{
		interactableEventTrigger.serverOnInteractEvent.AddListener(RpcPlayButtonSFX);
	}

	private void OnDisable()
	{
		interactableEventTrigger.serverOnInteractEvent.RemoveListener(RpcPlayButtonSFX);
	}

	[ClientRpc]
	private void RpcPlayButtonSFX(PlayerInteract _playerInteract)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(_playerInteract);
		SendRPCInternal("System.Void SFXGenericButtonComponent::RpcPlayButtonSFX(PlayerInteract)", 1731324265, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcPlayButtonSFX__PlayerInteract(PlayerInteract _playerInteract)
	{
		SFXManager.SFXOneShot(buttonSFX, base.transform.position);
	}

	protected static void InvokeUserCode_RpcPlayButtonSFX__PlayerInteract(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlayButtonSFX called on server.");
		}
		else
		{
			((SFXGenericButtonComponent)obj).UserCode_RpcPlayButtonSFX__PlayerInteract(reader.ReadNetworkBehaviour<PlayerInteract>());
		}
	}

	static SFXGenericButtonComponent()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(SFXGenericButtonComponent), "System.Void SFXGenericButtonComponent::RpcPlayButtonSFX(PlayerInteract)", InvokeUserCode_RpcPlayButtonSFX__PlayerInteract);
	}
}

}
