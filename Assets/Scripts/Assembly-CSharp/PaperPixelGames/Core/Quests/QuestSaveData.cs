using System;
using System.Collections.Generic;

namespace PaperPixelGames.Core.Quests
{
	[Serializable]
	public class QuestSaveData
	{
		public string CurrentMainGroupId;

		public List<string> ActiveSideGroupIds = new List<string>();

		public List<string> CompletedGroupIds = new List<string>();

		public List<string> CompletedQuestIds = new List<string>();

		public List<QuestProgressSaveEntry> QuestProgresses = new List<QuestProgressSaveEntry>();
	}
}
