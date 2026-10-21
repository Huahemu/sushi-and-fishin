using System;
using PaperPixelGames.Core.Player.QuickSlot;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public class PlayerData
	{
		[Obsolete]
		public TabletData TabletData = new TabletData();

		[Obsolete("QuickSlotData is now stored in ClientData. Kept for backward compatibility.")]
		public QuickSlotSaveData QuickSlotData = new QuickSlotSaveData();

		public ToolData ToolData = new ToolData();
	}
}
