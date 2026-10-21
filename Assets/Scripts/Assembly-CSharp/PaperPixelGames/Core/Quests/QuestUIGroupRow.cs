using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Quests
{
	public class QuestUIGroupRow : MonoBehaviour
	{
		[Header("Text")]
		[SerializeField]
		private TextMeshProUGUI _titleText;

		[SerializeField]
		private TextMeshProUGUI _descriptionText;

		[Header("Rows")]
		[SerializeField]
		private RectTransform _questRowsParent;

		[SerializeField]
		private QuestUIQuestRow _questRowPrefab;

		[Header("Options")]
		[SerializeField]
		private bool _hideDescriptionIfEmpty = true;

		[SerializeField]
		private bool _hideCompletedQuestRows;

		[Header("Layout")]
		[SerializeField]
		private LayoutGroup _layoutGroup;

		private readonly Dictionary<string, QuestUIQuestRow> _questRowsById = new Dictionary<string, QuestUIQuestRow>();

		private QuestGroupSO _group;

		private QuestManager _manager;

		public QuestGroupSO Group => _group;

		public void Setup(QuestGroupSO group, QuestManager manager)
		{
			_group = group;
			_manager = manager;
			ClearRows();
			SetupTexts();
			SpawnQuestRows();
			RebuildLayout();
		}

		public bool TryCompleteQuest(QuestSO quest)
		{
			if (quest == null || string.IsNullOrWhiteSpace(quest.Id))
			{
				return false;
			}
			if (!_questRowsById.TryGetValue(quest.Id, out var value))
			{
				return false;
			}
			value.SetCompleted(isCompleted: true, playFeedback: true);
			return true;
		}

		public bool TrySetProgress(QuestSO quest, int current, int target)
		{
			if (quest == null || string.IsNullOrWhiteSpace(quest.Id))
			{
				return false;
			}
			if (!_questRowsById.TryGetValue(quest.Id, out var value))
			{
				return false;
			}
			value.SetProgress(current, target);
			return true;
		}

		private void SetupTexts()
		{
			if (_group == null)
			{
				return;
			}
			if (_titleText != null)
			{
				_titleText.text = (string.IsNullOrWhiteSpace(_group.LocalizedTitle) ? "Quest Group" : _group.LocalizedTitle);
			}
			if (!(_descriptionText == null))
			{
				bool flag = !string.IsNullOrWhiteSpace(_group.LocalizedDescription);
				_descriptionText.text = (flag ? _group.LocalizedDescription : string.Empty);
				if (_hideDescriptionIfEmpty)
				{
					_descriptionText.gameObject.SetActive(flag);
				}
			}
		}

		private void SpawnQuestRows()
		{
			if (_group == null || _questRowPrefab == null || _questRowsParent == null)
			{
				return;
			}
			foreach (QuestSO quest in _group.Quests)
			{
				if (quest == null)
				{
					continue;
				}
				bool flag = _manager != null && _manager.IsQuestCompleted(quest);
				if (!(_hideCompletedQuestRows & flag))
				{
					int currentProgress = ((_manager != null) ? _manager.GetQuestProgress(quest) : 0);
					int targetProgress = ((_manager != null) ? _manager.GetQuestTarget(quest) : quest.TargetAmount);
					QuestUIQuestRow questUIQuestRow = Object.Instantiate(_questRowPrefab, _questRowsParent);
					questUIQuestRow.Setup(quest, flag, currentProgress, targetProgress);
					if (!string.IsNullOrWhiteSpace(quest.Id))
					{
						_questRowsById[quest.Id] = questUIQuestRow;
					}
				}
			}
		}

		private void ClearRows()
		{
			foreach (QuestUIQuestRow value in _questRowsById.Values)
			{
				if (value != null)
				{
					Object.Destroy(value.gameObject);
				}
			}
			_questRowsById.Clear();
			if (!(_questRowsParent == null))
			{
				for (int num = _questRowsParent.childCount - 1; num >= 0; num--)
				{
					Object.Destroy(_questRowsParent.GetChild(num).gameObject);
				}
			}
		}

		private void RebuildLayout()
		{
			if (!(_layoutGroup == null))
			{
				RectTransform rectTransform = _layoutGroup.transform as RectTransform;
				if (!(rectTransform == null))
				{
					LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
				}
			}
		}
	}
}
