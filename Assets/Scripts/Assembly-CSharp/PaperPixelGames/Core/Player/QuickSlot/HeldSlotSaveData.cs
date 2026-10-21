using System;

namespace PaperPixelGames.Core.Player.QuickSlot
{
	[Serializable]
	public class HeldSlotSaveData
	{
		public bool HasItem;

		public SlotItemData Item;

		public static HeldSlotSaveData Empty => new HeldSlotSaveData
		{
			HasItem = false,
			Item = new SlotItemData()
		};
	}
}
