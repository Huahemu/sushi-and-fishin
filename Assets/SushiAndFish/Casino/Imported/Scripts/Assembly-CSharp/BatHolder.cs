namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Runtime.InteropServices;
using global::Mirror;
using global::UnityEngine;

public class BatHolder : NetworkBehaviour
{
	[SerializeField]
	private Bat batPrefab;

	[SerializeField]
	private Transform batSpawnPoint;

	[SyncVar(hook = "OnBatSet")]
	private Bat _bat;

	protected NetworkBehaviourSyncVar ____batNetId;

	public Action<Bat, Bat> _Mirror_SyncVarHookDelegate__bat;

	public Bat Network_bat
	{
		get
		{
			return GetSyncVarNetworkBehaviour(____batNetId, ref _bat);
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter_NetworkBehaviour(value, ref _bat, 1uL, _Mirror_SyncVarHookDelegate__bat, ref ____batNetId);
		}
	}

	private void OnBatSet(Bat oldBat, Bat newBat)
	{
		newBat.GetComponent<Rigidbody>().isKinematic = true;
		newBat.transform.SetParent(batSpawnPoint);
		newBat.LocalSetBatSpawnPoint(batSpawnPoint);
	}

	public override void OnStartServer()
	{
		base.OnStartServer();
		Bat bat = UnityEngine.Object.Instantiate(batPrefab, batSpawnPoint.position, batSpawnPoint.rotation);
		NetworkServer.Spawn(bat.gameObject);
		Network_bat = bat;
	}

	public BatHolder()
	{
		_Mirror_SyncVarHookDelegate__bat = OnBatSet;
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
			writer.WriteNetworkBehaviour(Network_bat);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteNetworkBehaviour(Network_bat);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize_NetworkBehaviour(ref _bat, _Mirror_SyncVarHookDelegate__bat, reader, ref ____batNetId);
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize_NetworkBehaviour(ref _bat, _Mirror_SyncVarHookDelegate__bat, reader, ref ____batNetId);
		}
	}
}

}
