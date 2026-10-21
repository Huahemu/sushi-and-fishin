using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Player.QuickSlot;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public class ClientData
	{
		public string HostSteamID = "";

		public int SlotID = -1;

		public List<DefaultWallpaperData> WallpaperData = new List<DefaultWallpaperData>();

		public QuickSlotSaveData QuickSlotData = new QuickSlotSaveData();
	}
}
