using System;
using System.Collections.Generic;
using EPOOutline;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Indicator;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace PaperPixelGames.Core.Quests
{
	public sealed class QuestIndicatorTargetManager : Singleton<QuestIndicatorTargetManager>
	{
		[Serializable]
		public sealed class QuestIndicatorTargetBinding
		{
			public QuestSO Quest;

			public List<OffscreenIndicatorTarget> IndicatorTargets = new List<OffscreenIndicatorTarget>();

			public List<Outlinable> Outlinables = new List<Outlinable>();

			public bool EnableHighlight = true;

			[Min(0f)]
			public float HighlightWithinMeters = 8f;
		}

		[Header("Quest Targets")]
		[SerializeField]
		private List<QuestIndicatorTargetBinding> _bindings = new List<QuestIndicatorTargetBinding>();

		[Header("Debug")]
		[SerializeField]
		private bool _logWarnings = true;

		private readonly Dictionary<string, QuestIndicatorTargetBinding> _bindingByQuestId = new Dictionary<string, QuestIndicatorTargetBinding>();

		public override SingletonScope Scope => SingletonScope.Scene;

		protected override void Awake()
		{
			base.Awake();
			BuildLookup();
		}

		public bool TryGetBinding(string questId, out QuestIndicatorTargetBinding binding)
		{
			if (_bindingByQuestId.Count != _bindings.Count)
			{
				BuildLookup();
			}
			binding = null;
			if (string.IsNullOrWhiteSpace(questId))
			{
				return false;
			}
			if (_bindingByQuestId.TryGetValue(questId, out binding))
			{
				return binding != null;
			}
			return false;
		}

		public IReadOnlyList<OffscreenIndicatorTarget> GetIndicatorTargets(string questId)
		{
			if (!TryGetBinding(questId, out var binding))
			{
				return Array.Empty<OffscreenIndicatorTarget>();
			}
			return binding.IndicatorTargets;
		}

		public IReadOnlyList<Outlinable> GetOutlinables(string questId)
		{
			if (!TryGetBinding(questId, out var binding))
			{
				return Array.Empty<Outlinable>();
			}
			return binding.Outlinables;
		}

		public void BuildLookup()
		{
			_bindingByQuestId.Clear();
			for (int i = 0; i < _bindings.Count; i++)
			{
				QuestIndicatorTargetBinding questIndicatorTargetBinding = _bindings[i];
				if (questIndicatorTargetBinding == null || questIndicatorTargetBinding.Quest == null)
				{
					continue;
				}
				if (string.IsNullOrWhiteSpace(questIndicatorTargetBinding.Quest.Id))
				{
					LogWarning("Binding quest has empty id: " + questIndicatorTargetBinding.Quest.name);
					continue;
				}
				if (_bindingByQuestId.ContainsKey(questIndicatorTargetBinding.Quest.Id))
				{
					LogWarning("Duplicate binding for quest: " + questIndicatorTargetBinding.Quest.name);
					continue;
				}
				QuestIndicatorTargetBinding questIndicatorTargetBinding2 = questIndicatorTargetBinding;
				if (questIndicatorTargetBinding2.IndicatorTargets == null)
				{
					questIndicatorTargetBinding2.IndicatorTargets = new List<OffscreenIndicatorTarget>();
				}
				questIndicatorTargetBinding2 = questIndicatorTargetBinding;
				if (questIndicatorTargetBinding2.Outlinables == null)
				{
					questIndicatorTargetBinding2.Outlinables = new List<Outlinable>();
				}
				_bindingByQuestId.Add(questIndicatorTargetBinding.Quest.Id, questIndicatorTargetBinding);
			}
		}

		private void LogWarning(string message)
		{
			if (_logWarnings)
			{
				Debug.LogWarning("[QuestIndicatorTargetManager] " + message, this);
			}
		}
	}
}
