using System.Collections.Generic;
using EPOOutline;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Indicator;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.Quests
{
	[DefaultExecutionOrder(100)]
	public sealed class QuestIndicatorBridge : MonoBehaviour
	{
		[Header("Rules")]
		[SerializeField]
		private bool _showOnlyFirstIndicatorQuestPerGroup = true;

		[SerializeField]
		[Min(0.02f)]
		private float _highlightRefreshInterval = 0.1f;

		[Header("Debug")]
		[SerializeField]
		private bool _logWarnings = true;

		private QuestManager _questManager;

		private readonly Dictionary<string, List<OffscreenIndicatorTarget>> _activeTargetsByQuestId = new Dictionary<string, List<OffscreenIndicatorTarget>>();

		private readonly HashSet<string> _activeIndicatorQuestIds = new HashSet<string>();

		private Outlinable _activeQuestHighlight;

		private float _nextHighlightRefreshTime;

		private int _lastHighlightCandidateCount;

		public int LastHighlightCandidateCount => _lastHighlightCandidateCount;

		private void OnEnable()
		{
			TryBindQuestManager();
		}

		private void Start()
		{
			TryBindQuestManager();
			SyncFromQuestManager();
		}

		private void LateUpdate()
		{
			if (!(Time.unscaledTime < _nextHighlightRefreshTime))
			{
				_nextHighlightRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, _highlightRefreshInterval);
				RefreshQuestHighlight();
			}
		}

		private void OnDisable()
		{
			UnbindQuestManager();
			HideAllIndicators();
			ClearQuestHighlight();
		}

		private void OnDestroy()
		{
			UnbindQuestManager();
			HideAllIndicators();
			ClearQuestHighlight();
			_activeTargetsByQuestId.Clear();
			_activeIndicatorQuestIds.Clear();
		}

		public void SyncFromQuestManager()
		{
			if (TryBindQuestManager())
			{
				HideAllIndicators();
				ClearQuestHighlight();
				ShowVisibleMainQuestIndicators();
				ShowVisibleSideQuestIndicators();
				RefreshQuestHighlight();
				_nextHighlightRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, _highlightRefreshInterval);
			}
		}

		private bool TryBindQuestManager()
		{
			if (_questManager != null)
			{
				return true;
			}
			_questManager = QuestManager.Instance;
			if (_questManager == null)
			{
				return false;
			}
			_questManager.MainGroupChanged += HandleMainGroupChanged;
			_questManager.SideGroupStarted += HandleSideGroupStarted;
			_questManager.SideGroupCompleted += HandleSideGroupCompleted;
			_questManager.QuestCompleted += HandleQuestCompleted;
			_questManager.QuestStateChanged += HandleQuestStateChanged;
			return true;
		}

		private void UnbindQuestManager()
		{
			if (!(_questManager == null))
			{
				_questManager.MainGroupChanged -= HandleMainGroupChanged;
				_questManager.SideGroupStarted -= HandleSideGroupStarted;
				_questManager.SideGroupCompleted -= HandleSideGroupCompleted;
				_questManager.QuestCompleted -= HandleQuestCompleted;
				_questManager.QuestStateChanged -= HandleQuestStateChanged;
				_questManager = null;
			}
		}

		private void HandleMainGroupChanged(QuestGroupSO group)
		{
			SyncFromQuestManager();
		}

		private void HandleSideGroupStarted(QuestGroupSO group)
		{
			SyncFromQuestManager();
		}

		private void HandleSideGroupCompleted(QuestGroupSO group)
		{
			SyncFromQuestManager();
		}

		private void HandleQuestCompleted(QuestSO quest)
		{
			SyncFromQuestManager();
		}

		private void HandleQuestStateChanged()
		{
			SyncFromQuestManager();
		}

		private void ShowVisibleMainQuestIndicators()
		{
			if (!(_questManager == null))
			{
				QuestGroupSO currentMainGroup = _questManager.CurrentMainGroup;
				if (!(currentMainGroup == null))
				{
					ShowGroupQuestIndicators(currentMainGroup);
				}
			}
		}

		private void ShowVisibleSideQuestIndicators()
		{
			if (!(_questManager == null))
			{
				List<QuestGroupSO> activeSideGroups = _questManager.GetActiveSideGroups();
				for (int i = 0; i < activeSideGroups.Count; i++)
				{
					ShowGroupQuestIndicators(activeSideGroups[i]);
				}
			}
		}

		private void ShowGroupQuestIndicators(QuestGroupSO group)
		{
			if (group == null || group.Quests == null)
			{
				return;
			}
			if (_showOnlyFirstIndicatorQuestPerGroup)
			{
				ShowFirstAvailableIndicatorInGroup(group);
				return;
			}
			for (int i = 0; i < group.Quests.Count; i++)
			{
				ShowQuestIndicatorIfActive(group.Quests[i]);
			}
		}

		private void ShowFirstAvailableIndicatorInGroup(QuestGroupSO group)
		{
			if (group == null || group.Quests == null)
			{
				return;
			}
			QuestSO questSO = null;
			for (int i = 0; i < group.Quests.Count; i++)
			{
				QuestSO questSO2 = group.Quests[i];
				if (!(questSO2 == null) && (!(_questManager != null) || !_questManager.IsQuestCompleted(questSO2)))
				{
					questSO = questSO2;
					break;
				}
			}
			if (!(questSO == null) && (!(_questManager != null) || _questManager.IsQuestActive(questSO)) && questSO.IndicateWhenActive)
			{
				ShowQuestIndicatorIfActive(questSO);
			}
		}

		private void ShowQuestIndicatorIfActive(QuestSO quest)
		{
			if (quest == null)
			{
				return;
			}
			HideQuestIndicator(quest);
			if (!quest.IndicateWhenActive || (_questManager != null && !_questManager.IsQuestActive(quest)) || (_questManager != null && _questManager.IsQuestCompleted(quest)))
			{
				return;
			}
			if (string.IsNullOrWhiteSpace(quest.Id))
			{
				LogWarning("Quest id is empty: " + quest.name);
				return;
			}
			QuestIndicatorTargetManager instance = Singleton<QuestIndicatorTargetManager>.Instance;
			if (instance == null)
			{
				LogWarning("QuestIndicatorTargetManager instance is null.");
				return;
			}
			if (!instance.TryGetBinding(quest.Id, out var binding))
			{
				LogWarning("No target binding found for quest: " + quest.name);
				return;
			}
			_activeIndicatorQuestIds.Add(quest.Id);
			List<OffscreenIndicatorTarget> orCreateActiveTargetList = GetOrCreateActiveTargetList(quest.Id);
			orCreateActiveTargetList.Clear();
			if (binding.IndicatorTargets != null && Singleton<OffscreenIndicatorHandler>.Instance != null)
			{
				for (int i = 0; i < binding.IndicatorTargets.Count; i++)
				{
					OffscreenIndicatorTarget offscreenIndicatorTarget = binding.IndicatorTargets[i];
					if (!(offscreenIndicatorTarget == null))
					{
						orCreateActiveTargetList.Add(offscreenIndicatorTarget);
						if (quest.DeactivateIndicatorWhenReached)
						{
							Singleton<OffscreenIndicatorHandler>.Instance.ShowUntilReached(offscreenIndicatorTarget, quest.IndicatorReachDistance);
						}
						else
						{
							Singleton<OffscreenIndicatorHandler>.Instance.ShowIndefinite(offscreenIndicatorTarget);
						}
					}
				}
			}
			else if (binding.IndicatorTargets != null && binding.IndicatorTargets.Count > 0)
			{
				LogWarning("OffscreenIndicatorHandler instance is null.");
			}
			bool flag = binding.Outlinables != null && binding.Outlinables.Count > 0;
			if (orCreateActiveTargetList.Count == 0 && !flag)
			{
				LogWarning("Binding has no indicator targets or outlinables for quest: " + quest.name);
			}
		}

		private void HideQuestIndicator(QuestSO quest)
		{
			if (!(quest == null))
			{
				HideQuestIndicator(quest.Id);
			}
		}

		private void HideQuestIndicator(string questId)
		{
			if (!string.IsNullOrWhiteSpace(questId))
			{
				if (_activeTargetsByQuestId.TryGetValue(questId, out var value))
				{
					HideTargets(value);
					_activeTargetsByQuestId.Remove(questId);
				}
				_activeIndicatorQuestIds.Remove(questId);
			}
		}

		private void HideAllIndicators()
		{
			foreach (KeyValuePair<string, List<OffscreenIndicatorTarget>> item in _activeTargetsByQuestId)
			{
				HideTargets(item.Value);
			}
			_activeTargetsByQuestId.Clear();
			_activeIndicatorQuestIds.Clear();
		}

		private void HideTargets(List<OffscreenIndicatorTarget> targets)
		{
			if (Singleton<OffscreenIndicatorHandler>.Instance == null || targets == null)
			{
				return;
			}
			for (int i = 0; i < targets.Count; i++)
			{
				OffscreenIndicatorTarget offscreenIndicatorTarget = targets[i];
				if (offscreenIndicatorTarget != null)
				{
					Singleton<OffscreenIndicatorHandler>.Instance.HideIndefinite(offscreenIndicatorTarget);
				}
			}
		}

		private List<OffscreenIndicatorTarget> GetOrCreateActiveTargetList(string questId)
		{
			if (_activeTargetsByQuestId.TryGetValue(questId, out var value))
			{
				return value;
			}
			value = new List<OffscreenIndicatorTarget>();
			_activeTargetsByQuestId.Add(questId, value);
			return value;
		}

		private void RefreshQuestHighlight()
		{
			_lastHighlightCandidateCount = 0;
			if (_activeIndicatorQuestIds.Count == 0)
			{
				ClearQuestHighlight();
				return;
			}
			QuestIndicatorTargetManager instance = Singleton<QuestIndicatorTargetManager>.Instance;
			HighlightManager instance2 = Singleton<HighlightManager>.Instance;
			PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
			if (instance == null || instance2 == null || pPG_Player == null)
			{
				ClearQuestHighlight();
				return;
			}
			Transform transform = pPG_Player.transform;
			Outlinable outlinable = null;
			float num = float.PositiveInfinity;
			foreach (string activeIndicatorQuestId in _activeIndicatorQuestIds)
			{
				if (!instance.TryGetBinding(activeIndicatorQuestId, out var binding) || binding == null || !binding.EnableHighlight || binding.Outlinables == null)
				{
					continue;
				}
				float num2 = Mathf.Max(0f, binding.HighlightWithinMeters);
				float num3 = num2 * num2;
				_lastHighlightCandidateCount += binding.Outlinables.Count;
				for (int i = 0; i < binding.Outlinables.Count; i++)
				{
					Outlinable outlinable2 = binding.Outlinables[i];
					if (!(outlinable2 == null))
					{
						float sqrMagnitude = (outlinable2.transform.position - transform.position).sqrMagnitude;
						if (!(sqrMagnitude > num3) && !(sqrMagnitude >= num))
						{
							num = sqrMagnitude;
							outlinable = outlinable2;
						}
					}
				}
			}
			if (!(outlinable == _activeQuestHighlight))
			{
				ClearQuestHighlight();
				if (!(outlinable == null))
				{
					instance2.SetPersistent(outlinable);
					_activeQuestHighlight = outlinable;
				}
			}
		}

		private void ClearQuestHighlight()
		{
			if (!(_activeQuestHighlight == null))
			{
				if (Singleton<HighlightManager>.Instance != null)
				{
					Singleton<HighlightManager>.Instance.ClearPersistentHighlight(_activeQuestHighlight);
				}
				_activeQuestHighlight = null;
			}
		}

		private void LogWarning(string message)
		{
			if (_logWarnings)
			{
				UtilityMethods.Log("[QuestIndicatorBridge] " + message, LogType.Warning);
			}
		}
	}
}
