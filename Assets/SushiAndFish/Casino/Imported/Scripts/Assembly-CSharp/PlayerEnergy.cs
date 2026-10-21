namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Runtime.InteropServices;
using global::Mirror;
using global::MoreMountains.Tools;
using global::UnityEngine;

public class PlayerEnergy : NetworkBehaviour
{
	[SerializeField]
	[SyncVar(hook = "OnEnergyChanged")]
	private float energy;

	public CanvasGroup energyUI;

	public Action<float, float> _Mirror_SyncVarHookDelegate_energy;

	public float Networkenergy
	{
		get
		{
			return energy;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref energy, 1uL, _Mirror_SyncVarHookDelegate_energy);
		}
	}

	public void DecreaseEnergy(float amount)
	{
		Networkenergy = energy - amount;
		Networkenergy = Mathf.Clamp(energy, 0f, 100f);
	}

	public void AddEnergy(float amount)
	{
		Networkenergy = energy + amount;
		Networkenergy = Mathf.Clamp(energy, 0f, 100f);
	}

	private void OnEnergyChanged(float oldValue, float newValue)
	{
		if (base.isLocalPlayer)
		{
			float alpha = MMMaths.Remap(newValue, 100f, 0f, 0f, 1f);
			energyUI.alpha = alpha;
		}
	}

	public PlayerEnergy()
	{
		_Mirror_SyncVarHookDelegate_energy = OnEnergyChanged;
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
			writer.WriteFloat(energy);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteFloat(energy);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref energy, _Mirror_SyncVarHookDelegate_energy, reader.ReadFloat());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref energy, _Mirror_SyncVarHookDelegate_energy, reader.ReadFloat());
		}
	}
}

}
