using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Progression;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "ProgressionXpRewardTableSO", menuName = "Data/Progression/XP Reward Table")]
	public class ProgressionXpRewardTableSO : ScriptableObject
	{
		[Serializable]
		public class RewardEntry
		{
			public ProgressionXpSource Source;

			[Min(0f)]
			public int Xp;
		}

		[Serializable]
		public class ContextRewardEntry
		{
			public ProgressionXpSource Source;

			public string ContextId;

			[Min(0f)]
			public int Xp;
		}

		[SerializeField]
		private List<RewardEntry> _baseRewards = new List<RewardEntry>();

		[SerializeField]
		private List<ContextRewardEntry> _contextRewards = new List<ContextRewardEntry>();

		[Header("Money Spend")]
		[SerializeField]
		[Min(1f)]
		private int _moneySpentDollarsPerXp = 25;

		[SerializeField]
		[Min(0f)]
		private int _moneySpentMinimumXp = 1;

		[SerializeField]
		[Min(0f)]
		private int _moneySpentMaximumXp;

		public int GetReward(ProgressionXpSource source, string contextId)
		{
			if (source == ProgressionXpSource.None)
			{
				return 0;
			}
			if (!string.IsNullOrWhiteSpace(contextId) && _contextRewards != null)
			{
				foreach (ContextRewardEntry contextReward in _contextRewards)
				{
					if (contextReward != null && contextReward.Source == source && string.Equals(contextReward.ContextId, contextId, StringComparison.Ordinal))
					{
						return Mathf.Max(0, contextReward.Xp);
					}
				}
			}
			if (_baseRewards != null)
			{
				foreach (RewardEntry baseReward in _baseRewards)
				{
					if (baseReward != null && baseReward.Source == source)
					{
						return Mathf.Max(0, baseReward.Xp);
					}
				}
			}
			return 0;
		}

		public int GetMoneySpentReward(int spentDollars)
		{
			spentDollars = Mathf.Max(0, spentDollars);
			if (spentDollars <= 0)
			{
				return 0;
			}
			int num = Mathf.Max(1, _moneySpentDollarsPerXp);
			int b = spentDollars / num;
			if (_moneySpentMinimumXp > 0)
			{
				b = Mathf.Max(_moneySpentMinimumXp, b);
			}
			if (_moneySpentMaximumXp > 0)
			{
				b = Mathf.Min(_moneySpentMaximumXp, b);
			}
			return Mathf.Max(0, b);
		}
	}
}
