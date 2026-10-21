namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::UnityEngine;

public static class CosmeticDataManager
{
	private static Dictionary<int, CosmeticData> _cosmeticCache;

	private static int[] _sortedValidCosmeticIds = Array.Empty<int>();

	private static bool _isInitialized = false;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void AutoInitialize()
	{
		Initialize();
	}

	public static void Initialize()
	{
		if (_isInitialized)
		{
			return;
		}
		_cosmeticCache = new Dictionary<int, CosmeticData>();
		CosmeticData[] array = SushiAndFish.Casino.Original.CasinoResources.LoadAll<CosmeticData>("Cosmetics");
		int num = 0;
		CosmeticData[] array2 = array;
		foreach (CosmeticData cosmeticData in array2)
		{
			if (cosmeticData == null)
			{
				num++;
			}
			else if (cosmeticData.cosmeticId > 0)
			{
				if (_cosmeticCache.ContainsKey(cosmeticData.cosmeticId))
				{
					Debug.LogWarning($"[CosmeticDataManager] Duplicate cosmeticId found: {cosmeticData.cosmeticId} ({cosmeticData.cosmeticName}). Skipping duplicate.");
				}
				else
				{
					_cosmeticCache[cosmeticData.cosmeticId] = cosmeticData;
				}
			}
		}
		if (num > 0)
		{
			Debug.LogWarning($"[CosmeticDataManager] Skipped {num} null/broken cosmetic assets during initialization.");
		}
		RebuildSortedCosmeticIds();
		_isInitialized = true;
	}

	public static CosmeticData GetCosmeticById(int cosmeticId)
	{
		if (!_isInitialized)
		{
			Debug.LogWarning("[CosmeticDataManager] Not initialized. Calling Initialize() now...");
			Initialize();
		}
		if (_cosmeticCache == null || !_cosmeticCache.TryGetValue(cosmeticId, out var value))
		{
			Debug.LogWarning($"[CosmeticDataManager] CosmeticData with ID {cosmeticId} not found!");
			return null;
		}
		return value;
	}

	public static bool HasCosmetic(int cosmeticId)
	{
		if (!_isInitialized)
		{
			Initialize();
		}
		if (_cosmeticCache != null)
		{
			return _cosmeticCache.ContainsKey(cosmeticId);
		}
		return false;
	}

	public static IEnumerable<CosmeticData> GetAllCosmetics()
	{
		if (!_isInitialized)
		{
			Initialize();
		}
		if (_cosmeticCache == null)
		{
			return new List<CosmeticData>();
		}
		return _cosmeticCache.Values;
	}

	private static void RebuildSortedCosmeticIds()
	{
		if (_cosmeticCache == null || _cosmeticCache.Count == 0)
		{
			_sortedValidCosmeticIds = Array.Empty<int>();
			return;
		}
		List<int> list = new List<int>(_cosmeticCache.Keys);
		list.Sort();
		_sortedValidCosmeticIds = list.ToArray();
	}

	public static int GetValidCosmeticCount()
	{
		if (!_isInitialized)
		{
			Initialize();
		}
		if (_cosmeticCache == null)
		{
			return 0;
		}
		return _cosmeticCache.Count;
	}

	public static int[] GetValidCosmeticIdsSorted()
	{
		if (!_isInitialized)
		{
			Initialize();
		}
		return _sortedValidCosmeticIds ?? Array.Empty<int>();
	}
}

}
