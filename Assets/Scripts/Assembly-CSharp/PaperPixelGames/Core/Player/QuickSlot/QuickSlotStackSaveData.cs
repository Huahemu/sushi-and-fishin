using System;
using System.Collections.Generic;

namespace PaperPixelGames.Core.Player.QuickSlot
{
	[Serializable]
	public class QuickSlotStackSaveData
	{
		public int Index;

		public List<SlotItemData> Items;

		public static QuickSlotStackSaveData Empty => new QuickSlotStackSaveData
		{
			Index = 0,
			Items = new List<SlotItemData>()
		};
	}
}
