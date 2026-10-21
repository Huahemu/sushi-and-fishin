namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::System.IO;
using global::System.Linq;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

public class CosmeticsUnlockManager : MonoSingleton<CosmeticsUnlockManager>
{
	[Serializable]
	private class EquippedCosmeticData
	{
		public CosmeticType cosmeticType;

		public int cosmeticId;
	}

	[Serializable]
	private class CosmeticsSaveData
	{
		public int[] unlockedCosmetics;

		public EquippedCosmeticData[] equippedCosmetics;

		public string playerColorHex = "";

		public long timestamp;

		public string version = "1.0";
	}

	[Header("Settings")]
	[SerializeField]
	private string saveFileName = "cosmetics_unlocks.json";

	[Header("Defaults")]
	[SerializeField]
	private CosmeticData defaultClothingCosmetic;

	[Header("Debug")]
	[SerializeField]
	private bool debugMode = true;

	private HashSet<int> unlockedCosmetics = new HashSet<int>();

	private Dictionary<CosmeticType, int> equippedCosmetics = new Dictionary<CosmeticType, int>();

	private Color? savedPlayerColor;

	public static event Action<int> OnCosmeticUnlocked;

	public static event Action OnUnlocksLoaded;

	protected override void OnAwake()
	{
		LoadFromFile();
		EnsureDefaultClothingUnlocked(skipSave: false);
		if (debugMode)
		{
			Debug.Log($"[CosmeticsUnlockManager] Initialized with {unlockedCosmetics.Count} unlocked cosmetics");
		}
	}

	public bool UnlockCosmetic(int cosmeticId)
	{
		if (unlockedCosmetics.Contains(cosmeticId))
		{
			if (debugMode)
			{
				Debug.Log($"[CosmeticsUnlockManager] Cosmetic {cosmeticId} already unlocked");
			}
			return false;
		}
		unlockedCosmetics.Add(cosmeticId);
		SaveToFile();
		OnCosmeticUnlocked?.Invoke(cosmeticId);
		if (debugMode)
		{
			Debug.Log($"[CosmeticsUnlockManager] Unlocked cosmetic {cosmeticId}");
		}
		return true;
	}

	public int UnlockRandomCosmetic()
	{
		EnsureDefaultClothingUnlocked(skipSave: true);
		CosmeticData[] array = (from c in SushiAndFish.Casino.Original.CasinoResources.LoadAll<CosmeticData>("Cosmetics")
			where !unlockedCosmetics.Contains(c.cosmeticId)
			select c).ToArray();
		if (array.Length == 0)
		{
			if (debugMode)
			{
				Debug.Log("[CosmeticsUnlockManager] All cosmetics unlocked");
			}
			return -1;
		}
		CosmeticData cosmeticData = array[UnityEngine.Random.Range(0, array.Length)];
		UnlockCosmetic(cosmeticData.cosmeticId);
		return cosmeticData.cosmeticId;
	}

	public bool IsCosmeticUnlocked(int cosmeticId)
	{
		return unlockedCosmetics.Contains(cosmeticId);
	}

	public int[] GetUnlockedCosmetics()
	{
		return unlockedCosmetics.ToArray();
	}

	public int GetUnlockedCount()
	{
		return unlockedCosmetics.Count;
	}

	public int GetTotalCosmeticsCount()
	{
		return CosmeticDataManager.GetValidCosmeticCount();
	}

	public int GetDefaultClothingCosmeticId()
	{
		if (!(defaultClothingCosmetic != null))
		{
			return -1;
		}
		return defaultClothingCosmetic.cosmeticId;
	}

	public void ResetAllUnlocks()
	{
		unlockedCosmetics.Clear();
		EnsureDefaultClothingUnlocked(skipSave: true);
		SaveToFile();
		if (debugMode)
		{
			Debug.Log("[CosmeticsUnlockManager] All unlocks reset");
		}
	}

	public Dictionary<CosmeticType, int> GetEquippedCosmetics()
	{
		return new Dictionary<CosmeticType, int>(equippedCosmetics);
	}

	public void SetEquippedCosmetics(Dictionary<CosmeticType, int> equipped, bool skipSave = false)
	{
		equippedCosmetics = new Dictionary<CosmeticType, int>(equipped);
		if (!skipSave)
		{
			SaveToFile();
		}
	}

	public void SetPlayerColor(Color color)
	{
		savedPlayerColor = color;
		SaveToFile();
		if (debugMode)
		{
			Debug.Log($"[CosmeticsUnlockManager] Saved player color: {color}");
		}
	}

	public Color? GetPlayerColor()
	{
		return savedPlayerColor;
	}

	public void LoadFromFile()
	{
		try
		{
			string saveFilePath = GetSaveFilePath();
			if (!File.Exists(saveFilePath))
			{
				if (debugMode)
				{
					Debug.Log("[CosmeticsUnlockManager] No save file found");
				}
				return;
			}
			CosmeticsSaveData cosmeticsSaveData = JsonUtility.FromJson<CosmeticsSaveData>(File.ReadAllText(saveFilePath));
			if (cosmeticsSaveData == null)
			{
				if (debugMode)
				{
					Debug.LogWarning("[CosmeticsUnlockManager] Failed to parse save file");
				}
				return;
			}
			if (cosmeticsSaveData.unlockedCosmetics != null)
			{
				unlockedCosmetics = new HashSet<int>(cosmeticsSaveData.unlockedCosmetics);
			}
			equippedCosmetics.Clear();
			if (cosmeticsSaveData.equippedCosmetics != null)
			{
				EquippedCosmeticData[] array = cosmeticsSaveData.equippedCosmetics;
				foreach (EquippedCosmeticData equippedCosmeticData in array)
				{
					equippedCosmetics[equippedCosmeticData.cosmeticType] = equippedCosmeticData.cosmeticId;
				}
			}
			savedPlayerColor = ((!string.IsNullOrEmpty(cosmeticsSaveData.playerColorHex)) ? new Color?(ColorHexUtility.HexToColor(cosmeticsSaveData.playerColorHex)) : ((Color?)null));
			if (debugMode)
			{
				Debug.Log($"[CosmeticsUnlockManager] Loaded {unlockedCosmetics.Count} unlocks, " + $"{equippedCosmetics.Count} equipped cosmetics");
			}
			OnUnlocksLoaded?.Invoke();
			EnsureDefaultClothingUnlocked(skipSave: false);
		}
		catch (Exception ex)
		{
			Debug.LogError("[CosmeticsUnlockManager] Failed to load save file: " + ex.Message);
			ResetToDefaults();
			EnsureDefaultClothingUnlocked(skipSave: false);
		}
	}

	private void SaveToFile()
	{
		try
		{
			string contents = JsonUtility.ToJson(new CosmeticsSaveData
			{
				unlockedCosmetics = unlockedCosmetics.ToArray(),
				equippedCosmetics = equippedCosmetics.Select((KeyValuePair<CosmeticType, int> kvp) => new EquippedCosmeticData
				{
					cosmeticType = kvp.Key,
					cosmeticId = kvp.Value
				}).ToArray(),
				playerColorHex = (savedPlayerColor.HasValue ? ColorHexUtility.ColorToHex(savedPlayerColor.Value) : ""),
				timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
				version = "1.0"
			}, prettyPrint: true);
			File.WriteAllText(GetSaveFilePath(), contents);
			if (debugMode)
			{
				Debug.Log("[CosmeticsUnlockManager] Saved to " + GetSaveFilePath());
			}
		}
		catch (Exception ex)
		{
			Debug.LogError("[CosmeticsUnlockManager] Failed to save: " + ex.Message);
		}
	}

	private void ResetToDefaults()
	{
		unlockedCosmetics = new HashSet<int>();
		equippedCosmetics.Clear();
		savedPlayerColor = null;
	}

	private void EnsureDefaultClothingUnlocked(bool skipSave)
	{
		int defaultClothingCosmeticId = GetDefaultClothingCosmeticId();
		if (defaultClothingCosmeticId > 0 && !unlockedCosmetics.Contains(defaultClothingCosmeticId))
		{
			unlockedCosmetics.Add(defaultClothingCosmeticId);
			if (!skipSave)
			{
				SaveToFile();
			}
		}
	}

	private string GetSaveFilePath()
	{
		return Path.Combine(SushiAndFish.Casino.Original.CasinoPaths.PersistentDataPath, saveFileName);
	}
}

}
