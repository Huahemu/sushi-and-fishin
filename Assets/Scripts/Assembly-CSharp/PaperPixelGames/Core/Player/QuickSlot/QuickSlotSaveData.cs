using System;
using System.Collections.Generic;

namespace PaperPixelGames.Core.Player.QuickSlot
{
	[Serializable]
	public class QuickSlotSaveData
	{
		public HeldSlotSaveData Held;

		public List<QuickSlotStackSaveData> Slots;

		public static QuickSlotSaveData CreateDefault(int slotSize)
		{
			return new QuickSlotSaveData
			{
				Held = HeldSlotSaveData.Empty,
				Slots = new List<QuickSlotStackSaveData>(slotSize)
			};
		}
	}
}
