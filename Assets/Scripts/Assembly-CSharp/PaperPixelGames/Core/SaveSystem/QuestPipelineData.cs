using System;
using System.Collections.Generic;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public sealed class QuestPipelineData
	{
		public ushort NextQuestListIndex;

		public List<ushort> CompletedQuests = new List<ushort>();

		public List<QuestStateData> ActiveQuests = new List<QuestStateData>();
	}
}
