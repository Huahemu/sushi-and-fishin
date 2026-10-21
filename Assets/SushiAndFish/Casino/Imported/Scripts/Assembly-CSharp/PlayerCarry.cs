namespace SushiAndFish.Casino.Original
{
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class PlayerCarry : Item
{
	private PlayerController _pc;

	[SerializeField]
	private SpringPositionFollower handSpring;

	[SerializeField]
	private PlayerHandButtonAnimation phba;

	protected override void OnAwake()
	{
		base.OnAwake();
		_pc = GetComponent<PlayerController>();
	}

	private void Start()
	{
		IsInteractable = !_pc.hasBody && !base.NetworkHolder;
	}

	protected override void OnPickedUp(PlayerInventory playerInventory)
	{
		base.OnPickedUp(playerInventory);
		RpcOnPickedUp(playerInventory.GetComponent<PlayerCarry>());
	}

	[ClientRpc]
	private void RpcOnPickedUp(PlayerCarry holderCarry)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(holderCarry);
		SendRPCInternal("System.Void PlayerCarry::RpcOnPickedUp(PlayerCarry)", -1743165390, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	protected override void OnDropped(PlayerInventory playerInventory)
	{
		base.OnDropped(playerInventory);
		TargetOnDropped(base.connectionToClient, playerInventory.GetComponent<PlayerCarry>());
	}

	[TargetRpc]
	private void TargetOnDropped(NetworkConnection conn, PlayerCarry holderCarry)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteNetworkBehaviour(holderCarry);
		SendTargetRPCInternal(conn, "System.Void PlayerCarry::TargetOnDropped(Mirror.NetworkConnection,PlayerCarry)", -2013112336, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	public override void ServerThrow(Vector3 position, Quaternion rotation, Vector3 force, Vector3 torque)
	{
		if ((bool)base.NetworkHolder)
		{
			ServerDrop();
		}
		TargetOnThrow(base.connectionToClient, force, torque);
	}

	[TargetRpc]
	private void TargetOnThrow(NetworkConnection conn, Vector3 force, Vector3 torque)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVector3(force);
		writer.WriteVector3(torque);
		SendTargetRPCInternal(conn, "System.Void PlayerCarry::TargetOnThrow(Mirror.NetworkConnection,UnityEngine.Vector3,UnityEngine.Vector3)", 1675301902, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	public void LocalSetInteractable(bool isInteractable)
	{
		CmdSetInteractable(isInteractable);
	}

	public bool TryGetHolderInventory(out PlayerInventory holderInventory)
	{
		holderInventory = base.NetworkHolder;
		return holderInventory;
	}

	[Command]
	private void CmdSetInteractable(bool isInteractable)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isInteractable);
		SendCommandInternal("System.Void PlayerCarry::CmdSetInteractable(System.Boolean)", -737844972, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	[ClientRpc]
	private void RpcSetInteractable(bool isInteractable)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteBool(isInteractable);
		SendRPCInternal("System.Void PlayerCarry::RpcSetInteractable(System.Boolean)", -570291399, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcOnPickedUp__PlayerCarry(PlayerCarry holderCarry)
	{
		phba.LocalResetHands();
		if (base.isLocalPlayer)
		{
			_pc.State = PlayerController.PlayerState.Locked;
			holderCarry.MeetRequirements = false;
			holderCarry.GetComponentInChildren<SpringPositionFollower>().enabled = false;
			handSpring.enabled = false;
		}
	}

	protected static void InvokeUserCode_RpcOnPickedUp__PlayerCarry(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnPickedUp called on server.");
		}
		else
		{
			((PlayerCarry)obj).UserCode_RpcOnPickedUp__PlayerCarry(reader.ReadNetworkBehaviour<PlayerCarry>());
		}
	}

	protected void UserCode_TargetOnDropped__NetworkConnection__PlayerCarry(NetworkConnection conn, PlayerCarry holderCarry)
	{
		_pc.State = PlayerController.PlayerState.Ragdoll;
		holderCarry.MeetRequirements = true;
		holderCarry.GetComponentInChildren<SpringPositionFollower>().enabled = true;
		handSpring.enabled = true;
	}

	protected static void InvokeUserCode_TargetOnDropped__NetworkConnection__PlayerCarry(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("TargetRPC TargetOnDropped called on server.");
		}
		else
		{
			((PlayerCarry)obj).UserCode_TargetOnDropped__NetworkConnection__PlayerCarry(null, reader.ReadNetworkBehaviour<PlayerCarry>());
		}
	}

	protected void UserCode_TargetOnThrow__NetworkConnection__Vector3__Vector3(NetworkConnection conn, Vector3 force, Vector3 torque)
	{
		Rb.AddForce(force, ForceMode.VelocityChange);
		Rb.AddTorque(torque, ForceMode.VelocityChange);
	}

	protected static void InvokeUserCode_TargetOnThrow__NetworkConnection__Vector3__Vector3(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("TargetRPC TargetOnThrow called on server.");
		}
		else
		{
			((PlayerCarry)obj).UserCode_TargetOnThrow__NetworkConnection__Vector3__Vector3(null, reader.ReadVector3(), reader.ReadVector3());
		}
	}

	protected void UserCode_CmdSetInteractable__Boolean(bool isInteractable)
	{
		RpcSetInteractable(isInteractable);
	}

	protected static void InvokeUserCode_CmdSetInteractable__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdSetInteractable called on client.");
		}
		else
		{
			((PlayerCarry)obj).UserCode_CmdSetInteractable__Boolean(reader.ReadBool());
		}
	}

	protected void UserCode_RpcSetInteractable__Boolean(bool isInteractable)
	{
		InteractableName = GetComponent<PlayerProfile>().playerName;
		IsInteractable = isInteractable;
		CursorType = ((isInteractable && MeetRequirements) ? CursorManager.CursorType.Interact : CursorManager.CursorType.Default);
	}

	protected static void InvokeUserCode_RpcSetInteractable__Boolean(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetInteractable called on server.");
		}
		else
		{
			((PlayerCarry)obj).UserCode_RpcSetInteractable__Boolean(reader.ReadBool());
		}
	}

	static PlayerCarry()
	{
		RemoteProcedureCalls.RegisterCommand(typeof(PlayerCarry), "System.Void PlayerCarry::CmdSetInteractable(System.Boolean)", InvokeUserCode_CmdSetInteractable__Boolean, requiresAuthority: true);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerCarry), "System.Void PlayerCarry::RpcOnPickedUp(PlayerCarry)", InvokeUserCode_RpcOnPickedUp__PlayerCarry);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerCarry), "System.Void PlayerCarry::RpcSetInteractable(System.Boolean)", InvokeUserCode_RpcSetInteractable__Boolean);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerCarry), "System.Void PlayerCarry::TargetOnDropped(Mirror.NetworkConnection,PlayerCarry)", InvokeUserCode_TargetOnDropped__NetworkConnection__PlayerCarry);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerCarry), "System.Void PlayerCarry::TargetOnThrow(Mirror.NetworkConnection,UnityEngine.Vector3,UnityEngine.Vector3)", InvokeUserCode_TargetOnThrow__NetworkConnection__Vector3__Vector3);
	}
}

}
