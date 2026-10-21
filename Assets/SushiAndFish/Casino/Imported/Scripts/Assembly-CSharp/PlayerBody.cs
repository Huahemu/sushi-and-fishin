namespace SushiAndFish.Casino.Original
{
using global::System.Runtime.InteropServices;
using global::Mirror;
using global::UnityEngine;

public class PlayerBody : NetworkBehaviour
{
	private Rigidbody _rb;

	[SyncVar]
	private bool _hasBody = true;

	public bool Network_hasBody
	{
		get
		{
			return _hasBody;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _hasBody, 1uL, null);
		}
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
			writer.WriteBool(_hasBody);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteBool(_hasBody);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref _hasBody, null, reader.ReadBool());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _hasBody, null, reader.ReadBool());
		}
	}
}

}
