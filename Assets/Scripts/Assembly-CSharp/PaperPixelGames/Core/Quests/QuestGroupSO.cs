using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Framework.Enums;
using UnityEngine;

namespace PaperPixelGames.Core.Quests
{
	[CreateAssetMenu(fileName = "QuestGroup", menuName = "Data/Quests/Quest Group")]
	public class QuestGroupSO : ScriptableObject
	{
		[Header("Identity")]
		[SerializeField]
		private string _id;

		[SerializeField]
		private QuestType _type;

		[Header("Text")]
		[SerializeField]
		private string _title;

		[TextArea(2, 5)]
		[SerializeField]
		private string _description;

		[Header("Quests")]
		[SerializeField]
		private List<QuestSO> _quests = new List<QuestSO>();

		[Header("Completion Unlock Rewards")]
		[SerializeField]
		private List<ProgressionUnlockKey> _completionUnlocks = new List<ProgressionUnlockKey>();

		[SerializeField]
		private List<WholesaleSO> _completionWholesaleUnlocks = new List<WholesaleSO>();

		public string Id => _id;

		public QuestType Type => _type;

		public string Title => _title;

		public string Description => _description;

		public string LocalizedTitle => LocalizationDatabase.GetString("QuestTable", "quest_group." + _id + ".title", _title);

		public string LocalizedDescription => LocalizationDatabase.GetString("QuestTable", "quest_group." + _id + ".description", _description);

		public IReadOnlyList<QuestSO> Quests => _quests;

		public IReadOnlyList<ProgressionUnlockKey> CompletionUnlocks => _completionUnlocks;

		public IReadOnlyList<WholesaleSO> CompletionWholesaleUnlocks => _completionWholesaleUnlocks;

		public bool ContainsQuest(string questId)
		{
			if (string.IsNullOrWhiteSpace(questId))
			{
				return false;
			}
			foreach (QuestSO quest in _quests)
			{
				if (quest != null && quest.Id == questId)
				{
					return true;
				}
			}
			return false;
		}
	}
}
