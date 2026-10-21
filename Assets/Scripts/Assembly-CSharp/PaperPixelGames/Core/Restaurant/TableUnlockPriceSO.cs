using System.Collections.Generic;
using UnityEngine;

namespace PaperPixelGames.Core.Restaurant
{
	[CreateAssetMenu(fileName = "TableUnlockPriceSO", menuName = "Data/Restaurant/Table Unlock Price")]
	public class TableUnlockPriceSO : ScriptableObject
	{
		[Header("Dining Table Prices")]
		public List<int> DiningTablePrices = new List<int>();

		[Header("Conveyor Table Prices")]
		public List<int> ConveyorTablePrices = new List<int>();

		public int ConveyorBeltPrice = 100;

		[Header("Fallback")]
		[SerializeField]
		[Min(0f)]
		private int _fallbackPrice = 9999;

		public int GetDiningTableUnlockPrice(int unlockedDiningTableCount)
		{
			return GetPriceByUnlockedCount(DiningTablePrices, unlockedDiningTableCount);
		}

		public int GetConveyorTableUnlockPrice(int unlockedConveyorTableCount)
		{
			return GetPriceByUnlockedCount(ConveyorTablePrices, unlockedConveyorTableCount);
		}

		public int GetConveyorBeltUnlockPrice()
		{
			return Mathf.Max(0, ConveyorBeltPrice);
		}

		private int GetPriceByUnlockedCount(List<int> prices, int unlockedCount)
		{
			if (prices == null || prices.Count == 0)
			{
				return _fallbackPrice;
			}
			int num = Mathf.Max(0, unlockedCount);
			if (num < prices.Count)
			{
				return Mathf.Max(0, prices[num]);
			}
			return Mathf.Max(0, prices[prices.Count - 1]);
		}
	}
}
