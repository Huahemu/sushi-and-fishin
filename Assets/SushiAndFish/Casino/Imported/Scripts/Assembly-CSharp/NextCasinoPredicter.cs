namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::System.Linq;
using SushiAndFish.Casino.Original.Extensions;
using global::MoreMountains.Tools;
using global::UnityEngine;

public class NextCasinoPredicter
{
	private static readonly Dictionary<int, HashSet<CasinoGameType>> _cachedAvailableGameTypes = new Dictionary<int, HashSet<CasinoGameType>>();

	public static void PredictFloorGames(MMLootTableGameObjectSO lootTable, int floorIndex, int predictionCount)
	{
		if (NetworkSingleton<GameManager>.Instance == null || NetworkSingleton<SeededRandomManager>.Instance == null)
		{
			Debug.LogWarning("[NextCasinoPredicter] Required managers not available");
		}
		else
		{
			if (predictionCount <= 0)
			{
				return;
			}
			if (lootTable == null)
			{
				Debug.LogWarning($"[NextCasinoPredicter] Loot table is null for floor {floorIndex}");
				return;
			}
			if (lootTable.LootTable == null)
			{
				Debug.LogWarning($"[NextCasinoPredicter] Floor {floorIndex} loot table has no LootTable data");
				return;
			}
			int successfulQuota = NetworkSingleton<GameManager>.Instance.successfulQuota;
			int currentSeed = NetworkSingleton<SeededRandomManager>.Instance.CurrentSeed;
			List<GameObject> list = (from x in lootTable.LootTable.ObjectsToLoot
				where x != null && x.Loot != null
				select x.Loot).ToList();
			if (list.Count == 0)
			{
				Debug.LogWarning($"[NextCasinoPredicter] Floor {floorIndex} has no valid prefabs");
				return;
			}
			global::System.Random random = new global::System.Random(GetDeterministicHash(Vector3.zero, currentSeed, successfulQuota) * 31 + floorIndex);
			List<GameObject> list2 = list.ToList();
			for (int num = list2.Count - 1; num > 0; num--)
			{
				int index = random.Next(0, num + 1);
				GameObject value = list2[num];
				list2[num] = list2[index];
				list2[index] = value;
			}
			List<(int, string)> list3 = new List<(int, string)>();
			HashSet<CasinoGameType> hashSet = new HashSet<CasinoGameType>();
			_cachedAvailableGameTypes[floorIndex] = hashSet;
			for (int num2 = 0; num2 < predictionCount; num2++)
			{
				int index2 = num2 % list2.Count;
				GameObject gameObject = list2[index2];
				list3.Add((num2, gameObject.name));
				CasinoGameType? casinoGameType = ExtractGameTypeFromPrefabName(gameObject.name);
				if (casinoGameType.HasValue)
				{
					hashSet.Add(casinoGameType.Value);
				}
			}
			for (int num3 = 0; num3 < list3.Count; num3++)
			{
				_ = list3[num3];
			}
		}
	}

	public static void PredictFloorGames(int floorIndex, int predictionCount)
	{
		string text = $"FloorLootTables/Floor {floorIndex}";
		MMLootTableGameObjectSO mMLootTableGameObjectSO = SushiAndFish.Casino.Original.CasinoResources.Load<MMLootTableGameObjectSO>(text);
		if (mMLootTableGameObjectSO == null)
		{
			Debug.LogWarning($"[NextCasinoPredicter] Could not load loot table for floor {floorIndex} (tried path: {text})");
		}
		else
		{
			PredictFloorGames(mMLootTableGameObjectSO, floorIndex, predictionCount);
		}
	}

	public static HashSet<CasinoGameType> GetAvailableGameTypesForFloor(int floorIndex)
	{
		if (_cachedAvailableGameTypes.TryGetValue(floorIndex, out var value) && value != null && value.Count > 0)
		{
			return new HashSet<CasinoGameType>(value);
		}
		HashSet<CasinoGameType> hashSet = new HashSet<CasinoGameType>();
		string text = $"FloorLootTables/Floor {floorIndex}";
		MMLootTableGameObjectSO mMLootTableGameObjectSO = SushiAndFish.Casino.Original.CasinoResources.Load<MMLootTableGameObjectSO>(text);
		if (mMLootTableGameObjectSO == null || mMLootTableGameObjectSO.LootTable == null || mMLootTableGameObjectSO.LootTable.ObjectsToLoot == null)
		{
			Debug.LogWarning($"[NextCasinoPredicter] Could not load loot table for floor {floorIndex} to compute available game types (path: {text})");
			return hashSet;
		}
		foreach (GameObject item in (from x in mMLootTableGameObjectSO.LootTable.ObjectsToLoot
			where x != null && x.Loot != null
			select x.Loot).ToList())
		{
			if (!(item == null))
			{
				GameBase componentInChildren = item.GetComponentInChildren<GameBase>(includeInactive: true);
				if (componentInChildren != null)
				{
					hashSet.Add(componentInChildren.GameType);
				}
			}
		}
		return hashSet;
	}

	private static CasinoGameType? ExtractGameTypeFromPrefabName(string prefabName)
	{
		if (string.IsNullOrEmpty(prefabName))
		{
			return null;
		}
		string text = prefabName;
		if (text.EndsWith("_Cluster"))
		{
			text = text.Substring(0, text.Length - "_Cluster".Length);
		}
		if (Enum.TryParse<CasinoGameType>(text, ignoreCase: true, out var result))
		{
			return result;
		}
		if (text.Equals("SlotMachine", StringComparison.OrdinalIgnoreCase) || text.Equals("Slots", StringComparison.OrdinalIgnoreCase))
		{
			return CasinoGameType.SlotMachine;
		}
		if (text.Equals("WheelOfFortune", StringComparison.OrdinalIgnoreCase) || text.Equals("Wheel of Fortune", StringComparison.OrdinalIgnoreCase))
		{
			return CasinoGameType.WheelOfFortune;
		}
		if (text.Equals("DuckRace", StringComparison.OrdinalIgnoreCase) || text.Equals("Duck Race", StringComparison.OrdinalIgnoreCase))
		{
			return CasinoGameType.DuckRace;
		}
		if (text.Equals("CrossyRoad", StringComparison.OrdinalIgnoreCase) || text.Equals("Crossy Road", StringComparison.OrdinalIgnoreCase))
		{
			return CasinoGameType.CrossyRoad;
		}
		if (text.Equals("DragonTower", StringComparison.OrdinalIgnoreCase) || text.Equals("Dragon Tower", StringComparison.OrdinalIgnoreCase))
		{
			return CasinoGameType.DragonTower;
		}
		if (text.Equals("Minesweeper", StringComparison.OrdinalIgnoreCase) || text.Equals("MineSweeper", StringComparison.OrdinalIgnoreCase) || text.Equals("Mine Sweeper", StringComparison.OrdinalIgnoreCase))
		{
			return CasinoGameType.Minesweeper;
		}
		if (text.Equals("MoneyWheel", StringComparison.OrdinalIgnoreCase) || text.Equals("Money Wheel", StringComparison.OrdinalIgnoreCase))
		{
			return CasinoGameType.MoneyWheel;
		}
		Debug.LogWarning("[NextCasinoPredicter] Could not extract game type from prefab name: '" + prefabName + "' (cleaned: '" + text + "')");
		return null;
	}

	private static int GetDeterministicHash(Vector3 position, int seed, int quotaIndex)
	{
		int num = seed * 31 + quotaIndex;
		int num2 = Mathf.RoundToInt(position.x * 100f);
		int num3 = Mathf.RoundToInt(position.y * 100f);
		int num4 = Mathf.RoundToInt(position.z * 100f);
		return ((num * 31 + num2) * 31 + num3) * 31 + num4;
	}
}

}
