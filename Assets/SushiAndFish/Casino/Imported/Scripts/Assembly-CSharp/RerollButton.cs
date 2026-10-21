namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Runtime.InteropServices;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::TMPro;
using global::UnityEngine;

public class RerollButton : InteractableEventTrigger
{
	[SerializeField]
	private TextMeshPro rerollCostText;

	[SyncVar(hook = "OnRerollCostChanged")]
	private int _rerollCost;

	public Action<int, int> _Mirror_SyncVarHookDelegate__rerollCost;

	public int Network_rerollCost
	{
		get
		{
			return _rerollCost;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref _rerollCost, 1uL, _Mirror_SyncVarHookDelegate__rerollCost);
		}
	}

	private void OnRerollCostChanged(int oldCost, int newCost)
	{
		rerollCostText.text = $"{newCost} Tickets";
	}

	public override void OnStartServer()
	{
		base.OnStartServer();
		Network_rerollCost = NetworkSingleton<ItemStampManager>.Instance.GetCurrentRerollCost();
	}

	public override void ServerOnInteract(PlayerInteract playerInteract)
	{
		base.ServerOnInteract(playerInteract);
		NetworkSingleton<ItemStampManager>.Instance.TryRerollAllItemStampsWithCost();
		Network_rerollCost = NetworkSingleton<ItemStampManager>.Instance.GetCurrentRerollCost();
	}

	public RerollButton()
	{
		_Mirror_SyncVarHookDelegate__rerollCost = OnRerollCostChanged;
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
			writer.WriteVarInt(_rerollCost);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteVarInt(_rerollCost);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref _rerollCost, _Mirror_SyncVarHookDelegate__rerollCost, reader.ReadVarInt());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref _rerollCost, _Mirror_SyncVarHookDelegate__rerollCost, reader.ReadVarInt());
		}
	}
}

}
