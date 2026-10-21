namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::System.Linq;
using global::UnityEngine;

[CreateAssetMenu(menuName = "Game Settings/Spawnable Settings", fileName = "SpawnableSettings")]
public class SpawnableSettings : ScriptableObject
{
	[Header("Settings")]
	public bool isEnabled = true;

	[Header("Spawnable Prefabs")]
	public List<SpawnableSO> spawnables = new List<SpawnableSO>();

	public static event Action<SpawnableSettings> SettingsChanged;

	public static SpawnableSO GetSpawnableSoById(int id)
	{
		return SushiAndFish.Casino.Original.CasinoResources.Load<SpawnableSettings>("SpawnableSettings").spawnables.FirstOrDefault((SpawnableSO s) => s.spawnableID == id);
	}

	public void NotifyChanged()
	{
		SettingsChanged?.Invoke(this);
	}
}

}
