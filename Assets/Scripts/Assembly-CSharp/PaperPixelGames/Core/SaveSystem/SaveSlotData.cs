using System;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public class SaveSlotData
	{
		public int SlotID;

		public string HostSteamID = "";

		public int Version;

		public bool BossGlitchResetApplied;

		public int TemporaryStaffStackRevision;

		public PlayerData PlayerData = new PlayerData();

		public RestaurantData RestaurantData = new RestaurantData();

		public GameProgressData GameProgress = new GameProgressData();

		public DailyRecordData DailyRecord = new DailyRecordData();

		public TabletData TabletData = new TabletData();

		public static SaveSlotData CreateNew()
		{
			return new SaveSlotData
			{
				Version = 4,
				BossGlitchResetApplied = true
			};
		}
	}
}
