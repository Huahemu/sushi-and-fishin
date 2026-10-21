using System.Collections.Generic;
using PaperPixelGames.Core.Player.QuickSlot;

namespace PaperPixelGames.Core.SaveSystem
{
	public static class ClientDataMigrations
	{
		public static void Cleanup(ClientData data)
		{
			if (data != null)
			{
				ClientData clientData = data;
				if (clientData.WallpaperData == null)
				{
					clientData.WallpaperData = new List<DefaultWallpaperData>();
				}
				clientData = data;
				if (clientData.QuickSlotData == null)
				{
					clientData.QuickSlotData = new QuickSlotSaveData();
				}
				NormalizeQuickSlotData(data.QuickSlotData);
			}
		}

		private static void NormalizeQuickSlotData(QuickSlotSaveData data)
		{
			if (data == null)
			{
				return;
			}
			QuickSlotSaveData quickSlotSaveData = data;
			if (quickSlotSaveData.Held == null)
			{
				quickSlotSaveData.Held = HeldSlotSaveData.Empty;
			}
			HeldSlotSaveData held = data.Held;
			if (held.Item == null)
			{
				held.Item = new SlotItemData();
			}
			quickSlotSaveData = data;
			if (quickSlotSaveData.Slots == null)
			{
				quickSlotSaveData.Slots = new List<QuickSlotStackSaveData>();
			}
			for (int i = 0; i < data.Slots.Count; i++)
			{
				QuickSlotStackSaveData quickSlotStackSaveData = data.Slots[i];
				if (quickSlotStackSaveData == null)
				{
					quickSlotStackSaveData = QuickSlotStackSaveData.Empty;
					quickSlotStackSaveData.Index = i;
					data.Slots[i] = quickSlotStackSaveData;
				}
				QuickSlotStackSaveData quickSlotStackSaveData2 = quickSlotStackSaveData;
				if (quickSlotStackSaveData2.Items == null)
				{
					quickSlotStackSaveData2.Items = new List<SlotItemData>();
				}
				for (int j = 0; j < quickSlotStackSaveData.Items.Count; j++)
				{
					List<SlotItemData> items = quickSlotStackSaveData.Items;
					int index = j;
					if (items[index] == null)
					{
						SlotItemData slotItemData = (items[index] = new SlotItemData());
					}
				}
			}
		}
	}
}
