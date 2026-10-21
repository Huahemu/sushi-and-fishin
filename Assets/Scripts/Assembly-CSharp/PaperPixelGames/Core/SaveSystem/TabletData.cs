using System;
using System.Collections.Generic;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public class TabletData
	{
		public string WallpaperName;

		public List<DefaultWallpaperData> WallpaperData = new List<DefaultWallpaperData>();

		public List<string> MenuItems = new List<string>();

		public bool DailySushiSpecialFeatureActivated;

		public int DailySushiSpecialDay;

		public List<DailySushiSpecialEntryData> DailySushiSpecials = new List<DailySushiSpecialEntryData>();

		public List<FishBookEntryData> FishBookEntries = new List<FishBookEntryData>();

		public List<FishPersonalBestEntryData> FishPersonalBestEntries = new List<FishPersonalBestEntryData>();

		public List<byte> DiscoveredSeaLayerIndices = new List<byte>();

		public StaffData StaffData = new StaffData();
	}
}
