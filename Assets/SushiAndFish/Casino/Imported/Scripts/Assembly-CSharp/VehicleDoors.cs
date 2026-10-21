namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Runtime.InteropServices;
using global::FMODUnity;
using global::Mirror;
using global::UnityEngine;

public class VehicleDoors : NetworkBehaviour
{
	[SerializeField]
	private Animator animator;

	[SerializeField]
	private AllPlayersTriggerZone triggerZone;

	[SyncVar(hook = "OnDoorsOpenChanged")]
	private bool _doorsOpen;

	public Action<bool, bool> _Mirror_SyncVarHookDelegate__doorsOpen;

	public bool Network_doorsOpen
	{
		get
		{
			return _doorsOpen;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _doorsOpen, 1uL, _Mirror_SyncVarHookDelegate__doorsOpen);
		}
	}

	[Server]
	public void ServerOpenDoors()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void VehicleDoors::ServerOpenDoors()' called when server was not active");
		}
		else if (!_doorsOpen)
		{
			Network_doorsOpen = true;
			triggerZone.IsActive = true;
		}
	}

	private void OnDoorsOpenChanged(bool oldValue, bool newValue)
	{
		if (newValue)
		{
			animator.SetTrigger("openDoors");
			StudioParameterTrigger component = GetComponent<StudioParameterTrigger>();
			if (component != null)
			{
				component.TriggerParameters();
			}
		}
	}

	public VehicleDoors()
	{
		_Mirror_SyncVarHookDelegate__doorsOpen = OnDoorsOpenChanged;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteBool(_doorsOpen);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteBool(_doorsOpen);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref _doorsOpen, _Mirror_SyncVarHookDelegate__doorsOpen, reader.ReadBool());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _doorsOpen, _Mirror_SyncVarHookDelegate__doorsOpen, reader.ReadBool());
		}
	}
}

}
