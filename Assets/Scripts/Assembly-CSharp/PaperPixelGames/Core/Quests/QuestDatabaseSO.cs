using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PaperPixelGames.Core.Quests
{
	[CreateAssetMenu(fileName = "QuestDatabase", menuName = "Data/Quests/Quest Database")]
	public class QuestDatabaseSO : ScriptableObject
	{
		[Header("Main Quest Groups")]
		[SerializeField]
		private List<QuestGroupSO> _mainGroups = new List<QuestGroupSO>();

		[Header("Side Quest Groups")]
		[SerializeField]
		private List<QuestGroupSO> _sideGroups = new List<QuestGroupSO>();

		private Dictionary<string, QuestSO> _questLookup;

		private Dictionary<string, QuestGroupSO> _groupLookup;

		public IReadOnlyList<QuestGroupSO> MainGroups => _mainGroups;

		public IReadOnlyList<QuestGroupSO> SideGroups => _sideGroups;

		public QuestSO GetQuestById(string id)
		{
			RebuildLookups();
			if (string.IsNullOrWhiteSpace(id))
			{
				return null;
			}
			if (!_questLookup.TryGetValue(id, out var value))
			{
				return null;
			}
			return value;
		}

		public QuestGroupSO GetGroupById(string id)
		{
			RebuildLookups();
			if (string.IsNullOrWhiteSpace(id))
			{
				return null;
			}
			if (!_groupLookup.TryGetValue(id, out var value))
			{
				return null;
			}
			return value;
		}

		public QuestGroupSO GetGroupOfQuest(string questId)
		{
			if (string.IsNullOrWhiteSpace(questId))
			{
				return null;
			}
			RebuildLookups();
			foreach (QuestGroupSO allGroup in GetAllGroups())
			{
				if (allGroup != null && allGroup.ContainsQuest(questId))
				{
					return allGroup;
				}
			}
			return null;
		}

		public IReadOnlyList<QuestGroupSO> GetAllGroups()
		{
			return (from @group in _mainGroups.Concat(_sideGroups)
				where @group != null
				select @group).ToList();
		}

		public IReadOnlyList<QuestSO> GetAllQuests()
		{
			RebuildLookups();
			return _questLookup.Values.ToList();
		}

		private void RebuildLookups()
		{
			_questLookup = new Dictionary<string, QuestSO>();
			_groupLookup = new Dictionary<string, QuestGroupSO>();
			foreach (QuestGroupSO allGroup in GetAllGroups())
			{
				if (!(allGroup == null))
				{
					RegisterGroup(allGroup);
					RegisterGroupQuests(allGroup);
				}
			}
		}

		private void RegisterGroup(QuestGroupSO group)
		{
			if (string.IsNullOrWhiteSpace(group.Id))
			{
				Debug.LogWarning("[QuestDatabaseSO] Group has empty id: " + group.name, group);
			}
			else if (_groupLookup.ContainsKey(group.Id))
			{
				Debug.LogError("[QuestDatabaseSO] Duplicate group id found: " + group.Id, group);
			}
			else
			{
				_groupLookup.Add(group.Id, group);
			}
		}

		private void RegisterGroupQuests(QuestGroupSO group)
		{
			foreach (QuestSO quest in group.Quests)
			{
				if (!(quest == null))
				{
					if (string.IsNullOrWhiteSpace(quest.Id))
					{
						Debug.LogWarning("[QuestDatabaseSO] Quest has empty id in group: " + group.name, quest);
					}
					else if (_questLookup.ContainsKey(quest.Id))
					{
						Debug.LogError("[QuestDatabaseSO] Duplicate quest id found: " + quest.Id + " | Quest: " + quest.name, quest);
					}
					else
					{
						_questLookup.Add(quest.Id, quest);
					}
				}
			}
		}
	}
}
