using PaperPixelGames.Core.Quests;

namespace PaperPixelGames.Core.SaveSystem
{
	public static class BossProgressResetMigration
	{
		private const int LastAffectedSaveVersion = 2;

		public const string BackupTag = "before-boss-progress-reset-v3";

		public const string BossGroupId = "aca623a37aaa42f0813a5a7913b64348";

		public const string TridentGroupId = "d7fb45696a385d3283c44c78e8369526";

		public const string BossQuestId = "0d6b27f7cf374bad9fe13439a8989589";

		public const string EquipTridentQuestId = "22054870e463519483f73768bba78914";

		public const string CatchWithTridentQuestId = "93523c536e9e5464998ee8f8f3b00ebf";

		public const string DashWithTridentQuestId = "bec03baa768053a7b7325da6496834e6";

		public static void Apply(SaveSlotData slot, int sourceVersion)
		{
			if (slot != null && !slot.BossGlitchResetApplied)
			{
				if (sourceVersion <= 2)
				{
					ResetLegacyProgress(slot);
				}
				slot.BossGlitchResetApplied = true;
			}
		}

		private static void ResetLegacyProgress(SaveSlotData slot)
		{
			PlayerData playerData = slot.PlayerData;
			bool flag = playerData != null && playerData.ToolData?.PoseidonTridentUnlocked == true;
			if (slot.PlayerData?.ToolData != null)
			{
				slot.PlayerData.ToolData.PoseidonTridentUnlocked = false;
			}
			QuestSaveData questSaveData = slot.GameProgress?.QuestData;
			if (questSaveData != null)
			{
				flag |= questSaveData.CurrentMainGroupId == "d7fb45696a385d3283c44c78e8369526";
				flag |= questSaveData.CompletedGroupIds?.Exists(IsBossGroup) ?? false;
				flag |= questSaveData.CompletedQuestIds?.Exists(IsBossQuest) ?? false;
				flag |= questSaveData.QuestProgresses?.Exists((QuestProgressSaveEntry entry) => entry != null && IsBossQuest(entry.QuestId) && entry.Value > 0) ?? false;
				questSaveData.CompletedGroupIds?.RemoveAll(IsBossGroup);
				questSaveData.CompletedQuestIds?.RemoveAll(IsBossQuest);
				questSaveData.QuestProgresses?.RemoveAll((QuestProgressSaveEntry entry) => entry != null && IsBossQuest(entry.QuestId));
				questSaveData.ActiveSideGroupIds?.RemoveAll(IsBossGroup);
				if (questSaveData.CurrentMainGroupId == "d7fb45696a385d3283c44c78e8369526" || (string.IsNullOrWhiteSpace(questSaveData.CurrentMainGroupId) & flag))
				{
					questSaveData.CurrentMainGroupId = "aca623a37aaa42f0813a5a7913b64348";
				}
			}
		}

		private static bool IsBossGroup(string id)
		{
			if (!(id == "aca623a37aaa42f0813a5a7913b64348"))
			{
				return id == "d7fb45696a385d3283c44c78e8369526";
			}
			return true;
		}

		private static bool IsBossQuest(string id)
		{
			switch (id)
			{
			default:
				return id == "bec03baa768053a7b7325da6496834e6";
			case "0d6b27f7cf374bad9fe13439a8989589":
			case "22054870e463519483f73768bba78914":
			case "93523c536e9e5464998ee8f8f3b00ebf":
				return true;
			}
		}
	}
}
