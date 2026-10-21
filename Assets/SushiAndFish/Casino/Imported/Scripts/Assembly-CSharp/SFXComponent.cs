namespace SushiAndFish.Casino.Original
{
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class SFXComponent : NetworkBehaviour
{
	[SerializeField]
	private EventReference eventReference;

	public SFXParams[] fmodParams;

	public void PlayOneShotWith3DPos()
	{
		if (!eventReference.IsNull)
		{
			SFXManager.SFXOneShot(eventReference, base.gameObject.transform.position);
		}
	}

	public void PlayOneShotWithCustom3DPos(Vector3 pos)
	{
		if (!eventReference.IsNull)
		{
			SFXManager.SFXOneShot(eventReference, pos);
		}
	}

	public void PlayOneShotOverrideParams(float _value = 0f)
	{
		if (!eventReference.IsNull)
		{
			SFXManager.SFXOneShotWithParameters(eventReference, fmodParams, base.gameObject.transform.position);
		}
	}

	public void PlayOneShotOverrideParamsWithCustomPos(Vector3 pos)
	{
		if (!eventReference.IsNull)
		{
			SFXManager.SFXOneShotWithParameters(eventReference, fmodParams, pos);
		}
	}

	public void PlayOneShotAttached()
	{
		if (!eventReference.IsNull)
		{
			SFXManager.SFXOneShot3DAttached(eventReference, base.gameObject);
		}
	}

	public void PlayOneShotAttachedOverrideParameters()
	{
		if (!eventReference.IsNull)
		{
			SFXManager.SFXOneShot3DAttachedWithParameters(eventReference, fmodParams, base.gameObject);
		}
	}

	[ClientRpc]
	public void RpcPlayOneShotAttached()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void SFXComponent::RpcPlayOneShotAttached()", -1900197242, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	public void RpcPlayOneShotWith3DPos()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void SFXComponent::RpcPlayOneShotWith3DPos()", -1364297797, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	public void RpcPlayOneShotWithCustom3DPos(Vector3 pos)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVector3(pos);
		SendRPCInternal("System.Void SFXComponent::RpcPlayOneShotWithCustom3DPos(UnityEngine.Vector3)", -510311345, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	public void RpcPlayerInteractOneShotOnlyOnClient(PlayerInteract playerInteract)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(playerInteract);
		SendRPCInternal("System.Void SFXComponent::RpcPlayerInteractOneShotOnlyOnClient(PlayerInteract)", 653552140, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcPlayOneShotAttached()
	{
		PlayOneShotAttached();
	}

	protected static void InvokeUserCode_RpcPlayOneShotAttached(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlayOneShotAttached called on server.");
		}
		else
		{
			((SFXComponent)obj).UserCode_RpcPlayOneShotAttached();
		}
	}

	protected void UserCode_RpcPlayOneShotWith3DPos()
	{
		PlayOneShotWith3DPos();
	}

	protected static void InvokeUserCode_RpcPlayOneShotWith3DPos(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlayOneShotWith3DPos called on server.");
		}
		else
		{
			((SFXComponent)obj).UserCode_RpcPlayOneShotWith3DPos();
		}
	}

	protected void UserCode_RpcPlayOneShotWithCustom3DPos__Vector3(Vector3 pos)
	{
		PlayOneShotWithCustom3DPos(pos);
	}

	protected static void InvokeUserCode_RpcPlayOneShotWithCustom3DPos__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlayOneShotWithCustom3DPos called on server.");
		}
		else
		{
			((SFXComponent)obj).UserCode_RpcPlayOneShotWithCustom3DPos__Vector3(reader.ReadVector3());
		}
	}

	protected void UserCode_RpcPlayerInteractOneShotOnlyOnClient__PlayerInteract(PlayerInteract playerInteract)
	{
		if (!(playerInteract == null) && playerInteract.isLocalPlayer)
		{
			PlayOneShotWith3DPos();
		}
	}

	protected static void InvokeUserCode_RpcPlayerInteractOneShotOnlyOnClient__PlayerInteract(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcPlayerInteractOneShotOnlyOnClient called on server.");
		}
		else
		{
			((SFXComponent)obj).UserCode_RpcPlayerInteractOneShotOnlyOnClient__PlayerInteract(reader.ReadNetworkBehaviour<PlayerInteract>());
		}
	}

	static SFXComponent()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(SFXComponent), "System.Void SFXComponent::RpcPlayOneShotAttached()", InvokeUserCode_RpcPlayOneShotAttached);
		RemoteProcedureCalls.RegisterRpc(typeof(SFXComponent), "System.Void SFXComponent::RpcPlayOneShotWith3DPos()", InvokeUserCode_RpcPlayOneShotWith3DPos);
		RemoteProcedureCalls.RegisterRpc(typeof(SFXComponent), "System.Void SFXComponent::RpcPlayOneShotWithCustom3DPos(UnityEngine.Vector3)", InvokeUserCode_RpcPlayOneShotWithCustom3DPos__Vector3);
		RemoteProcedureCalls.RegisterRpc(typeof(SFXComponent), "System.Void SFXComponent::RpcPlayerInteractOneShotOnlyOnClient(PlayerInteract)", InvokeUserCode_RpcPlayerInteractOneShotOnlyOnClient__PlayerInteract);
	}
}

}
