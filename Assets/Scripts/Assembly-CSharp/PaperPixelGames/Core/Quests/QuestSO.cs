using PaperPixelGames.Core.Localization;
using PaperPixelGames.Framework.Enums;
using UnityEngine;
using UnityEngine.Serialization;

namespace PaperPixelGames.Core.Quests
{
	[CreateAssetMenu(fileName = "Quest", menuName = "Data/Quests/Quest")]
	public class QuestSO : ScriptableObject
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

		[Header("Localized Title Term")]
		[Tooltip("Optional UiTable key for a named term embedded in the localized quest title.")]
		[SerializeField]
		private string _localizedTitleTermKey;

		[SerializeField]
		private string _localizedTitleTermFallback;

		[Header("Completion")]
		[SerializeField]
		private QuestCompletionMode _completionMode;

		[SerializeField]
		[Min(1f)]
		private int _targetAmount = 1;

		[Header("Rules")]
		[SerializeField]
		private bool _canCompleteBeforeActive;

		[Header("Indicator")]
		[SerializeField]
		private bool _indicateWhenActive;

		[FormerlySerializedAs("_unregisterIndicatorWhenReached")]
		[SerializeField]
		private bool _deactivateIndicatorWhenReached = true;

		[SerializeField]
		private bool _deactivateIndicatorWhenCompleted = true;

		[SerializeField]
		[Min(0f)]
		private float _indicatorReachDistance = 4f;

		[Header("UI")]
		[SerializeField]
		private Sprite _icon;

		[Header("Reward")]
		[SerializeField]
		private int _moneyReward;

		[SerializeField]
		private int _scrapReward;

		public string Id => _id;

		public QuestType Type => _type;

		public string Title => _title;

		public string Description => _description;

		public string LocalizedTitle
		{
			get
			{
				string text = LocalizationDatabase.GetString("QuestTable", "quest." + _id + ".title", _title);
				if (string.IsNullOrWhiteSpace(_localizedTitleTermKey) || string.IsNullOrWhiteSpace(_localizedTitleTermFallback))
				{
					return text;
				}
				string newValue = LocalizationDatabase.GetString("UiTable", _localizedTitleTermKey, _localizedTitleTermFallback);
				return text.Replace(_localizedTitleTermFallback, newValue);
			}
		}

		public string LocalizedDescription => LocalizationDatabase.GetString("QuestTable", "quest." + _id + ".description", _description);

		public QuestCompletionMode CompletionMode => _completionMode;

		public bool IsCountable => _completionMode == QuestCompletionMode.Countable;

		public int TargetAmount
		{
			get
			{
				if (!IsCountable)
				{
					return 1;
				}
				return Mathf.Max(1, _targetAmount);
			}
		}

		public bool CanCompleteBeforeActive => _canCompleteBeforeActive;

		public bool IndicateWhenActive => _indicateWhenActive;

		public bool DeactivateIndicatorWhenReached => _deactivateIndicatorWhenReached;

		public bool DeactivateIndicatorWhenCompleted => _deactivateIndicatorWhenCompleted;

		public float IndicatorReachDistance => _indicatorReachDistance;

		public Sprite Icon => _icon;

		public int MoneyReward => _moneyReward;

		public int ScrapReward => _scrapReward;
	}
}
