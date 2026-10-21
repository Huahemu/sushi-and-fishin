using System;
using System.Collections.Generic;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public class RestaurantUpgradeData
	{
		public List<byte> UnlockedTableIds = new List<byte>();

		public bool HasExplicitDiningTableOpenState;

		public List<byte> ClosedDiningTableIds = new List<byte>();

		public bool IsConveyorBeltUnlocked;

		public List<byte> UnlockedConveyorBeltTableIds = new List<byte>();

		public bool IsFishFridgeUnlocked;

		public bool IsBeverageFridgeUnlocked;

		public List<byte> UnlockedTrayTrolleyIndexes = new List<byte>();

		public bool HasExplicitStationUnlockData;

		public List<byte> UnlockedChefDeskIndexes = new List<byte>();

		public List<byte> UnlockedPrepStationIndexes = new List<byte>();

		public bool ShouldDiningTableLoadOpen(byte tableId)
		{
			List<byte> unlockedTableIds = UnlockedTableIds;
			if (unlockedTableIds == null || !unlockedTableIds.Contains(tableId))
			{
				return false;
			}
			if (HasExplicitDiningTableOpenState)
			{
				List<byte> closedDiningTableIds = ClosedDiningTableIds;
				if (closedDiningTableIds == null)
				{
					return true;
				}
				return !closedDiningTableIds.Contains(tableId);
			}
			return true;
		}
	}
}
