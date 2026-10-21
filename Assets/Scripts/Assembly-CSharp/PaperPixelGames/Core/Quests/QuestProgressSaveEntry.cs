using System;

namespace PaperPixelGames.Core.Quests
{
	[Serializable]
	public class QuestProgressSaveEntry
	{
		public string QuestId;

		public int Value;

		public QuestProgressSaveEntry(string questId, int value)
		{
			QuestId = questId;
			Value = value;
		}
	}
}
