namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::UnityEngine;

[CreateAssetMenu(menuName = "Game Settings/Item Price Settings", fileName = "ItemPriceSettings")]
public class ItemPriceSettings : ScriptableObject
{
	[Serializable]
	public class ItemPriceData
	{
		[Tooltip("The SpawnableSO this price applies to.")]
		public SpawnableSO spawnableSO;

		[Tooltip("Base price in tickets for this item.")]
		[Min(0f)]
		public int basePrice = 2;

		[Tooltip("Price increase per floor. Final price = basePrice + (floorIndex * priceIncreasePerFloor)")]
		[Min(0f)]
		public int priceIncreasePerFloor = 1;
	}

	[Header("Item Prices")]
	[Tooltip("List of items and their base prices.")]
	[SerializeField]
	private List<ItemPriceData> itemPrices = new List<ItemPriceData>();

	[Header("Global Settings")]
	[Tooltip("Default base price for items not in the list.")]
	[Min(0f)]
	[SerializeField]
	private int defaultBasePrice = 2;

	[Tooltip("Default price increase per floor for items not in the list.")]
	[Min(0f)]
	[SerializeField]
	private int defaultPriceIncreasePerFloor = 1;

	[Header("Cosmetics (rarity)")]
	[Tooltip("Extra tickets on top of the default floor-scaled base. Order: Common, Uncommon, Rare, Epic, Legendary.")]
	[SerializeField]
	private int[] cosmeticRarityTicketAdd = new int[5] { 0, 1, 3, 6, 10 };

	private static readonly int[] DefaultCosmeticRarityTicketAdd = new int[5] { 0, 1, 3, 6, 10 };

	private Dictionary<int, ItemPriceData> _priceCache;

	public int GetBasePrice(SpawnableSO spawnableSO)
	{
		if (spawnableSO == null)
		{
			return defaultBasePrice;
		}
		BuildCacheIfNeeded();
		if (_priceCache.TryGetValue(spawnableSO.spawnableID, out var value))
		{
			return value.basePrice;
		}
		return defaultBasePrice;
	}

	public int GetPriceIncreasePerFloor(SpawnableSO spawnableSO)
	{
		if (spawnableSO == null)
		{
			return defaultPriceIncreasePerFloor;
		}
		BuildCacheIfNeeded();
		if (_priceCache.TryGetValue(spawnableSO.spawnableID, out var value))
		{
			return value.priceIncreasePerFloor;
		}
		return defaultPriceIncreasePerFloor;
	}

	public int CalculatePrice(SpawnableSO spawnableSO, int floorIndex)
	{
		if (spawnableSO == null)
		{
			return defaultBasePrice;
		}
		int basePrice = GetBasePrice(spawnableSO);
		int priceIncreasePerFloor = GetPriceIncreasePerFloor(spawnableSO);
		return basePrice + floorIndex * priceIncreasePerFloor;
	}

	public int CalculateCosmeticPrice(CosmeticRarity rarity, int floorIndex)
	{
		int num = defaultBasePrice + floorIndex * defaultPriceIncreasePerFloor;
		int[] array = ((cosmeticRarityTicketAdd != null && cosmeticRarityTicketAdd.Length != 0) ? cosmeticRarityTicketAdd : DefaultCosmeticRarityTicketAdd);
		if (rarity >= CosmeticRarity.Common && (int)rarity < array.Length)
		{
			return num + array[(int)rarity];
		}
		return num;
	}

	private void BuildCacheIfNeeded()
	{
		if (_priceCache != null)
		{
			return;
		}
		_priceCache = new Dictionary<int, ItemPriceData>();
		foreach (ItemPriceData itemPrice in itemPrices)
		{
			if (itemPrice.spawnableSO != null)
			{
				_priceCache[itemPrice.spawnableSO.spawnableID] = itemPrice;
			}
		}
	}

	private void OnValidate()
	{
		_priceCache = null;
	}
}

}
