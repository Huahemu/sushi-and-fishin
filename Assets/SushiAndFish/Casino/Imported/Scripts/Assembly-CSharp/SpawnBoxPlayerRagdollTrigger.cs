namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Runtime.InteropServices;
using global::FMODUnity;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class SpawnBoxPlayerRagdollTrigger : InteractableBase
{
	[SerializeField]
	private float setInteractableDelay = 1f;

	[SerializeField]
	private Animator animator;

	[SerializeField]
	private EventReference wakeUpSfx;

	[SerializeField]
	private EventReference musicStinger;

	[SerializeField]
	private Collider[] lidColliders;

	[SerializeField]
	private PlayerController assignedPlayer;

	[SyncVar(hook = "OnBoxInteractableChanged")]
	private bool _isInteractable;

	[SyncVar(hook = "OnLidsToggle")]
	private bool _areLidsOpen;

	public Action<bool, bool> _Mirror_SyncVarHookDelegate__isInteractable;

	public Action<bool, bool> _Mirror_SyncVarHookDelegate__areLidsOpen;

	public bool IsBoxInteractable => _isInteractable;

	public bool AreLidsOpen => _areLidsOpen;

	public bool Network_isInteractable
	{
		get
		{
			return _isInteractable;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _isInteractable, 1uL, _Mirror_SyncVarHookDelegate__isInteractable);
		}
	}

	public bool Network_areLidsOpen
	{
		get
		{
			return _areLidsOpen;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _areLidsOpen, 2uL, _Mirror_SyncVarHookDelegate__areLidsOpen);
		}
	}

	private void OnBoxInteractableChanged(bool oldValue, bool newValue)
	{
		IsInteractable = newValue;
	}

	private void OnLidsToggle(bool oldValue, bool newValue)
	{
		animator.SetBool("OpenLids", newValue);
	}

	[Server]
	public void AssignPlayer(PlayerController player)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void SpawnBoxPlayerRagdollTrigger::AssignPlayer(PlayerController)' called when server was not active");
			return;
		}
		Network_areLidsOpen = false;
		StartCoroutine(AssignPlayerRoutine(player));
	}

	private IEnumerator AssignPlayerRoutine(PlayerController player)
	{
		yield return new WaitForSeconds(setInteractableDelay);
		TargetDisableLids(player.connectionToClient);
		yield return new WaitForSeconds(0.5f);
		assignedPlayer = player;
		Network_isInteractable = true;
	}

	public override void ServerOnInteract(PlayerInteract playerInteract)
	{
		base.ServerOnInteract(playerInteract);
		WakePlayerUp();
	}

	[Server]
	private void WakePlayerUp()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void SpawnBoxPlayerRagdollTrigger::WakePlayerUp()' called when server was not active");
		}
		else if ((bool)assignedPlayer)
		{
			PlayerController playerController = assignedPlayer;
			assignedPlayer = null;
			Network_isInteractable = false;
			Network_areLidsOpen = true;
			RpcOnWakeUp();
			playerController.ServerWakeUp();
			PlayMusicStinger(playerController.connectionToClient);
		}
	}

	[TargetRpc]
	private void TargetDisableLids(NetworkConnection conn)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendTargetRPCInternal(conn, "System.Void SpawnBoxPlayerRagdollTrigger::TargetDisableLids(Mirror.NetworkConnection)", -1416889657, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	private void DisableLidColliders()
	{
		Collider[] array = lidColliders;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].isTrigger = true;
		}
	}

	private void EnableLidColliders()
	{
		Collider[] array = lidColliders;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].isTrigger = false;
		}
	}

	[ClientRpc]
	private void RpcOnWakeUp()
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendRPCInternal("System.Void SpawnBoxPlayerRagdollTrigger::RpcOnWakeUp()", -587603264, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	[TargetRpc]
	private void PlayMusicStinger(NetworkConnection conn)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendTargetRPCInternal(conn, "System.Void SpawnBoxPlayerRagdollTrigger::PlayMusicStinger(Mirror.NetworkConnection)", 1929011759, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	public SpawnBoxPlayerRagdollTrigger()
	{
		_Mirror_SyncVarHookDelegate__isInteractable = OnBoxInteractableChanged;
		_Mirror_SyncVarHookDelegate__areLidsOpen = OnLidsToggle;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_TargetDisableLids__NetworkConnection(NetworkConnection conn)
	{
		DisableLidColliders();
	}

	protected static void InvokeUserCode_TargetDisableLids__NetworkConnection(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("TargetRPC TargetDisableLids called on server.");
		}
		else
		{
			((SpawnBoxPlayerRagdollTrigger)obj).UserCode_TargetDisableLids__NetworkConnection(null);
		}
	}

	protected void UserCode_RpcOnWakeUp()
	{
		SFXManager.SFXOneShot(wakeUpSfx, base.transform.position);
	}

	protected static void InvokeUserCode_RpcOnWakeUp(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcOnWakeUp called on server.");
		}
		else
		{
			((SpawnBoxPlayerRagdollTrigger)obj).UserCode_RpcOnWakeUp();
		}
	}

	protected void UserCode_PlayMusicStinger__NetworkConnection(NetworkConnection conn)
	{
		SFXManager.SFXOneShot(musicStinger);
	}

	protected static void InvokeUserCode_PlayMusicStinger__NetworkConnection(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("TargetRPC PlayMusicStinger called on server.");
		}
		else
		{
			((SpawnBoxPlayerRagdollTrigger)obj).UserCode_PlayMusicStinger__NetworkConnection(null);
		}
	}

	static SpawnBoxPlayerRagdollTrigger()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(SpawnBoxPlayerRagdollTrigger), "System.Void SpawnBoxPlayerRagdollTrigger::RpcOnWakeUp()", InvokeUserCode_RpcOnWakeUp);
		RemoteProcedureCalls.RegisterRpc(typeof(SpawnBoxPlayerRagdollTrigger), "System.Void SpawnBoxPlayerRagdollTrigger::TargetDisableLids(Mirror.NetworkConnection)", InvokeUserCode_TargetDisableLids__NetworkConnection);
		RemoteProcedureCalls.RegisterRpc(typeof(SpawnBoxPlayerRagdollTrigger), "System.Void SpawnBoxPlayerRagdollTrigger::PlayMusicStinger(Mirror.NetworkConnection)", InvokeUserCode_PlayMusicStinger__NetworkConnection);
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteBool(_isInteractable);
			writer.WriteBool(_areLidsOpen);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteBool(_isInteractable);
		}
		if ((syncVarDirtyBits & 2L) != 0L)
		{
			writer.WriteBool(_areLidsOpen);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref _isInteractable, _Mirror_SyncVarHookDelegate__isInteractable, reader.ReadBool());
			GeneratedSyncVarDeserialize(ref _areLidsOpen, _Mirror_SyncVarHookDelegate__areLidsOpen, reader.ReadBool());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _isInteractable, _Mirror_SyncVarHookDelegate__isInteractable, reader.ReadBool());
		}
		if ((num & 2L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _areLidsOpen, _Mirror_SyncVarHookDelegate__areLidsOpen, reader.ReadBool());
		}
	}
}

}
