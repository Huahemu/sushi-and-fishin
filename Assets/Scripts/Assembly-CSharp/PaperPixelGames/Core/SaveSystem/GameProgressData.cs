using System;
using PaperPixelGames.Core.Quests;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public class GameProgressData
	{
		public int ShopBalance = 10000;

        public string ExactGoldStorage = "";
        public string TownDiningLedger = "";
        // Co-saved with the wallet; absent in legacy saves until first capture.
        public bool FishingWeaponsAnchored;
        public int FishingWeaponsMask;

		public int ScrapBalance;

		public int CurrentDay = 1;

		public string LastSaveTime = "";

		public QuestSaveData QuestData;

		public bool HasSave;

		public bool CompleteTimeTuto;

		public bool CompleteOpenSignTuto;

		public byte CompassIndex;

		public byte CompassLevel;

		public byte CompassStepIndex;

		public int RestaurantLevel = 1;

		public double CurrentXp;

		public double TotalXp;

		public AnalyticsMilestoneData AnalyticsMilestones = new AnalyticsMilestoneData();
	}
}
