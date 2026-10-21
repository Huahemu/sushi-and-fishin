namespace SushiAndFish.Casino.Original
{
using global::System.Runtime.InteropServices;
using global::Mirror;
using global::UnityEngine;

public class ShopMusicRandomizer : NetworkBehaviour
{
	[SerializeField]
	private SFXLoopComponent musicLoop;

	[SyncVar]
	private int songidx;

	public int Networksongidx
	{
		get
		{
			return songidx;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref songidx, 1uL, null);
		}
	}

	public override void OnStartClient()
	{
		if (base.isServer)
		{
			Networksongidx = Random.Range(0, 2);
		}
		musicLoop.LoopSFX(play: true);
		musicLoop.loopInstance.setParameterByName("ShopSong", songidx);
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
			writer.WriteVarInt(songidx);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteVarInt(songidx);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref songidx, null, reader.ReadVarInt());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref songidx, null, reader.ReadVarInt());
		}
	}
}

}
