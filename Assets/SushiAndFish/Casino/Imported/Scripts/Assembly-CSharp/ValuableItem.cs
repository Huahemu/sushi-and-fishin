namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Runtime.InteropServices;
using global::Mirror;
using global::UnityEngine;

public class ValuableItem : NetworkBehaviour
{
	[SerializeField]
	private Renderer renderer;

	[SyncVar(hook = "OnValueChanged")]
	public int value = 10;

	private Item _item;

	public Action<int, int> _Mirror_SyncVarHookDelegate_value;

	public int Networkvalue
	{
		get
		{
			return value;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref this.value, 1uL, _Mirror_SyncVarHookDelegate_value);
		}
	}

	private void Awake()
	{
		_item = GetComponent<Item>();
	}

	private void Start()
	{
		_ = base.isServer;
	}

	private void DestroySelf()
	{
		_item.ServerDrop();
		NetworkServer.Destroy(base.gameObject);
	}

	[Server]
	public void ServerChangeValue(int targetValue)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ValuableItem::ServerChangeValue(System.Int32)' called when server was not active");
		}
		else
		{
			Networkvalue = targetValue;
		}
	}

	private void OnValueChanged(int oldValue, int newValue)
	{
		if (newValue > oldValue)
		{
			ChangeMatColor(Color.green);
		}
	}

	private void ChangeMatColor(Color targetColor)
	{
		renderer.material.color = targetColor;
	}

	public ValuableItem()
	{
		_Mirror_SyncVarHookDelegate_value = OnValueChanged;
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
			writer.WriteVarInt(value);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteVarInt(value);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref value, _Mirror_SyncVarHookDelegate_value, reader.ReadVarInt());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref value, _Mirror_SyncVarHookDelegate_value, reader.ReadVarInt());
		}
	}
}

}
