using System.Collections.Generic;
using DG.Tweening;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Quests
{
	public class QuestUI : MonoBehaviour
	{
		[Header("Root")]
		[SerializeField]
		private GameObject _rootObject;

		[Header("Main Quest UI")]
		[SerializeField]
		private CanvasGroup _mainCanvasGroup;

		[SerializeField]
		private TextMeshProUGUI _mainTitleText;

		[SerializeField]
		private RectTransform _mainQuestsParent;

		[SerializeField]
		private QuestUIQuestRow _mainQuestRowPrefab;

		[Header("Side Quest UI")]
		[SerializeField]
		private CanvasGroup _sideCanvasGroup;

		[SerializeField]
		private TextMeshProUGUI _sideTitleText;

		[SerializeField]
		private RectTransform _sideQuestsParent;

		[SerializeField]
		private QuestUIQuestRow _sideQuestRowPrefab;

		[SerializeField]
		private string _fallbackSideTitle = "Side Quests";

		[Header("Feedback")]
		[SerializeField]
		private float _rebuildDelayAfterComplete = 0.35f;

		private readonly Dictionary<string, QuestUIQuestRow> _rowsByQuestId = new Dictionary<string, QuestUIQuestRow>();

		private Tween _rebuildTween;

		private bool _isRegistered;

		private bool _layoutDirty;

		private void OnEnable()
		{
			LocalizationSettings.SelectedLocaleChanged -= HandleSelectedLocaleChanged;
			LocalizationSettings.SelectedLocaleChanged += HandleSelectedLocaleChanged;
			RegisterEvents();
			Rebuild();
		}

		private void Start()
		{
			RegisterEvents();
			Rebuild();
		}

		private void OnDisable()
		{
			LocalizationSettings.SelectedLocaleChanged -= HandleSelectedLocaleChanged;
			UnregisterEvents();
			KillRebuildTween();
		}

		private void HandleSelectedLocaleChanged(Locale locale)
		{
			Rebuild();
		}

		private void LateUpdate()
		{
			if (_layoutDirty)
			{
				ForceLayoutRebuild();
				_layoutDirty = false;
			}
		}

		public void SetVisibility(bool isVisible)
		{
			if (_rootObject != null)
			{
				_rootObject.SetActive(isVisible);
			}
		}

		public void SetQuestUIStatus(bool isVisible)
		{
			SetCanvasGroup(_mainCanvasGroup, isVisible);
			bool flag = QuestManager.Instance != null && QuestManager.Instance.GetActiveSideGroups().Count > 0;
			SetCanvasGroup(_sideCanvasGroup, isVisible & flag);
		}

		public void Rebuild()
		{
			ClearRows();
			QuestManager instance = QuestManager.Instance;
			if (!(instance == null))
			{
				RebuildMainQuest(instance);
				RebuildSideQuests(instance);
				_layoutDirty = true;
			}
		}

		public void HardResetUI()
		{
			KillRebuildTween();
			ClearRows();
			SetText(_mainTitleText, string.Empty);
			SetText(_sideTitleText, string.Empty);
			SetCanvasGroup(_mainCanvasGroup, isVisible: false);
			SetCanvasGroup(_sideCanvasGroup, isVisible: false);
		}

		private void RebuildMainQuest(QuestManager manager)
		{
			QuestGroupSO currentMainGroup = manager.CurrentMainGroup;
			if (currentMainGroup == null)
			{
				SetText(_mainTitleText, string.Empty);
				SetCanvasGroup(_mainCanvasGroup, isVisible: false);
				return;
			}
			SetCanvasGroup(_mainCanvasGroup, isVisible: true);
			SetText(_mainTitleText, currentMainGroup.LocalizedTitle);
			foreach (QuestSO quest in currentMainGroup.Quests)
			{
				if (!(quest == null))
				{
					SpawnQuestRow(quest, manager.IsQuestCompleted(quest), _mainQuestsParent, _mainQuestRowPrefab);
				}
			}
		}

		private void RebuildSideQuests(QuestManager manager)
		{
			List<QuestGroupSO> activeSideGroups = manager.GetActiveSideGroups();
			if (activeSideGroups == null || activeSideGroups.Count == 0)
			{
				SetText(_sideTitleText, string.Empty);
				SetCanvasGroup(_sideCanvasGroup, isVisible: false);
				return;
			}
			SetCanvasGroup(_sideCanvasGroup, isVisible: true);
			if (activeSideGroups.Count == 1)
			{
				SetText(_sideTitleText, activeSideGroups[0].LocalizedTitle);
			}
			else
			{
				SetText(_sideTitleText, _fallbackSideTitle);
			}
			foreach (QuestGroupSO item in activeSideGroups)
			{
				if (item == null)
				{
					continue;
				}
				foreach (QuestSO quest in item.Quests)
				{
					if (!(quest == null))
					{
						SpawnQuestRow(quest, manager.IsQuestCompleted(quest), _sideQuestsParent, (_sideQuestRowPrefab != null) ? _sideQuestRowPrefab : _mainQuestRowPrefab);
					}
				}
			}
		}

		private void SpawnQuestRow(QuestSO quest, bool isCompleted, RectTransform parent, QuestUIQuestRow prefab)
		{
			if (quest == null || parent == null || prefab == null)
			{
				UtilityMethods.Log("[QuestUI] SpawnQuestRow failed. Missing quest, parent or prefab.");
				return;
			}
			QuestManager instance = QuestManager.Instance;
			int currentProgress = ((instance != null) ? instance.GetQuestProgress(quest) : 0);
			int targetProgress = ((!(instance != null)) ? 1 : instance.GetQuestTarget(quest));
			QuestUIQuestRow questUIQuestRow = Object.Instantiate(prefab, parent);
			questUIQuestRow.Setup(quest, isCompleted, currentProgress, targetProgress);
			if (!string.IsNullOrWhiteSpace(quest.Id))
			{
				_rowsByQuestId[quest.Id] = questUIQuestRow;
			}
		}

		private void HandleQuestCompleted(QuestSO quest)
		{
			if (!(quest == null))
			{
				if (_rowsByQuestId.TryGetValue(quest.Id, out var value))
				{
					value.SetCompleted(isCompleted: true, playFeedback: true);
					ScheduleRebuild(_rebuildDelayAfterComplete);
				}
				else
				{
					ScheduleRebuild(0f);
				}
			}
		}

		private void HandleQuestProgressChanged(QuestSO quest, int current, int target)
		{
			if (!(quest == null) && _rowsByQuestId.TryGetValue(quest.Id, out var value))
			{
				value.SetProgress(current, target);
			}
		}

		private void HandleQuestStateChanged()
		{
			ScheduleRebuild(0f);
		}

		private void HandleMainGroupChanged(QuestGroupSO group)
		{
			ScheduleRebuild(0f);
		}

		private void HandleSideGroupStarted(QuestGroupSO group)
		{
			ScheduleRebuild(0f);
		}

		private void HandleSideGroupCompleted(QuestGroupSO group)
		{
			ScheduleRebuild(0f);
		}

		private void ScheduleRebuild(float delay)
		{
			KillRebuildTween();
			if (delay <= 0f)
			{
				Rebuild();
			}
			else
			{
				_rebuildTween = DOVirtual.DelayedCall(delay, Rebuild);
			}
		}

		private void ClearRows()
		{
			foreach (QuestUIQuestRow value in _rowsByQuestId.Values)
			{
				if (value != null)
				{
					Object.Destroy(value.gameObject);
				}
			}
			_rowsByQuestId.Clear();
			ClearChildren(_mainQuestsParent);
			ClearChildren(_sideQuestsParent);
		}

		private void RegisterEvents()
		{
			if (!_isRegistered)
			{
				QuestManager instance = QuestManager.Instance;
				if (!(instance == null))
				{
					instance.QuestCompleted += HandleQuestCompleted;
					instance.QuestProgressChanged += HandleQuestProgressChanged;
					instance.QuestStateChanged += HandleQuestStateChanged;
					instance.MainGroupChanged += HandleMainGroupChanged;
					instance.SideGroupStarted += HandleSideGroupStarted;
					instance.SideGroupCompleted += HandleSideGroupCompleted;
					_isRegistered = true;
				}
			}
		}

		private void UnregisterEvents()
		{
			if (_isRegistered)
			{
				QuestManager instance = QuestManager.Instance;
				if (!(instance == null))
				{
					instance.QuestCompleted -= HandleQuestCompleted;
					instance.QuestProgressChanged -= HandleQuestProgressChanged;
					instance.QuestStateChanged -= HandleQuestStateChanged;
					instance.MainGroupChanged -= HandleMainGroupChanged;
					instance.SideGroupStarted -= HandleSideGroupStarted;
					instance.SideGroupCompleted -= HandleSideGroupCompleted;
					_isRegistered = false;
				}
			}
		}

		private void KillRebuildTween()
		{
			if (_rebuildTween != null)
			{
				_rebuildTween.Kill();
				_rebuildTween = null;
			}
		}

		private void ForceLayoutRebuild()
		{
			Canvas.ForceUpdateCanvases();
			if (_mainQuestsParent != null)
			{
				LayoutRebuilder.ForceRebuildLayoutImmediate(_mainQuestsParent);
			}
			if (_sideQuestsParent != null)
			{
				LayoutRebuilder.ForceRebuildLayoutImmediate(_sideQuestsParent);
			}
		}

		private static void ClearChildren(RectTransform parent)
		{
			if (!(parent == null))
			{
				for (int num = parent.childCount - 1; num >= 0; num--)
				{
					Object.Destroy(parent.GetChild(num).gameObject);
				}
			}
		}

		private static void SetText(TextMeshProUGUI text, string value)
		{
			if (!(text == null))
			{
				text.text = value;
			}
		}

		private static void SetCanvasGroup(CanvasGroup canvasGroup, bool isVisible)
		{
			if (!(canvasGroup == null))
			{
				canvasGroup.alpha = (isVisible ? 1f : 0f);
				canvasGroup.interactable = isVisible;
				canvasGroup.blocksRaycasts = isVisible;
			}
		}
	}
}
