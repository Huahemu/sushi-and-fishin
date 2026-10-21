namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::System.Linq;
using global::System.Reflection;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

[CreateAssetMenu(fileName = "NewChallenge", menuName = "Challenges/Challenge")]
public class Challenge : ScriptableObject
{
	[UniqueID("challenges")]
	public int challengeID;

	[Header("Challenge Info")]
	[Tooltip("Display name of the challenge")]
	public string challengeName;

	[Header("Steam Achievement")]
	[Tooltip("Optional linked Steam Achievement. If set, this achievement will be unlocked when the challenge is completed.")]
	public SteamAchievement_SteamworksNET linkedAchievement;

	[Tooltip("Description of what the player needs to do")]
	[TextArea(3, 6)]
	public string description;

	[Header("Difficulty & Rewards")]
	public int floorIndex;

	[Tooltip("Manual ticket reward for this challenge. Set to -1 to use floor-based reward from ChallengeSettings.")]
	public int manualTicketReward = -1;

	[Header("Conditions")]
	[Tooltip("All conditions that must be met for this challenge to complete. Click the dropdown to add a condition type.")]
	[SerializeReference]
	public List<ChallengeConditionData> conditions = new List<ChallengeConditionData>();

	[Header("Settings")]
	[Tooltip("Whether all conditions must be met simultaneously or sequentially")]
	public bool requireAllSimultaneously = true;

	[Tooltip("Whether the challenge can be completed multiple times")]
	public bool repeatable;

	public HashSet<CasinoGameType> GetRequiredGameTypes()
	{
		HashSet<CasinoGameType> hashSet = new HashSet<CasinoGameType>();
		if (conditions == null)
		{
			return hashSet;
		}
		foreach (ChallengeConditionData condition in conditions)
		{
			if (condition != null)
			{
				if (condition is GameTypeConditionData gameTypeConditionData)
				{
					hashSet.Add(gameTypeConditionData.requiredGameType);
				}
				if (condition is WinCountConditionData { useSpecificGameType: not false } winCountConditionData)
				{
					hashSet.Add(winCountConditionData.specificGameType);
				}
				if (condition is LossCountConditionData { useSpecificGameType: not false } lossCountConditionData)
				{
					hashSet.Add(lossCountConditionData.specificGameType);
				}
				if (condition is BlackjackHandValueConditionData)
				{
					hashSet.Add(CasinoGameType.Blackjack);
				}
				if (condition is CrapsSequenceConditionData)
				{
					hashSet.Add(CasinoGameType.Craps);
				}
				if (condition is DuckRaceWinConditionData)
				{
					hashSet.Add(CasinoGameType.DuckRace);
				}
				if (condition is PokerHandConditionData)
				{
					hashSet.Add(CasinoGameType.Poker);
				}
			}
		}
		return hashSet;
	}

	public bool IsCompleted(ChallengeContext context)
	{
		if (conditions == null || conditions.Count == 0)
		{
			return false;
		}
		List<ChallengeConditionData> list = conditions.Where((ChallengeConditionData c) => c != null && !c.useAsResultFilter).ToList();
		if (list.Count == 0)
		{
			list = conditions.Where((ChallengeConditionData c) => c != null).ToList();
		}
		if (requireAllSimultaneously)
		{
			foreach (ChallengeConditionData item in list)
			{
				if (!item.Evaluate(context))
				{
					return false;
				}
			}
			return true;
		}
		foreach (ChallengeConditionData item2 in list)
		{
			if (item2.Evaluate(context))
			{
				return true;
			}
		}
		return false;
	}

	public float GetProgress(ChallengeContext context)
	{
		if (conditions == null || conditions.Count == 0)
		{
			return 0f;
		}
		List<ChallengeConditionData> list = conditions.Where((ChallengeConditionData c) => c != null && !c.useAsResultFilter).ToList();
		if (list.Count == 0)
		{
			return 0f;
		}
		float num = 0f;
		int num2 = 0;
		foreach (ChallengeConditionData item in list)
		{
			if (item != null)
			{
				num += item.GetProgress(context);
				num2++;
			}
		}
		if (num2 <= 0)
		{
			return 0f;
		}
		return num / (float)num2;
	}

	public string GetProgressText(ChallengeContext context)
	{
		if (conditions == null || conditions.Count == 0)
		{
			return "No conditions";
		}
		List<ChallengeConditionData> list = conditions.Where((ChallengeConditionData c) => c != null && !c.useAsResultFilter).ToList();
		if (list.Count == 0)
		{
			return string.Empty;
		}
		List<string> list2 = new List<string>();
		foreach (ChallengeConditionData item in list)
		{
			if (item != null)
			{
				list2.Add(item.GetProgressText(context));
			}
		}
		return string.Join(" | ", list2);
	}

	public void ResetChallenge()
	{
		if (conditions == null)
		{
			return;
		}
		foreach (ChallengeConditionData condition in conditions)
		{
			condition?.ResetCondition();
		}
	}

	public void OnGameResult(long bet, long payout, CasinoGameType gameType, Vector3 position, ChallengeContext context = null)
	{
		if (conditions == null)
		{
			return;
		}
		if (context == null)
		{
			long quotaAtActivation = 0L;
			if (NetworkSingleton<ChallengeManager>.Instance != null)
			{
				ChallengeProgress challengeProgress = NetworkSingleton<ChallengeManager>.Instance.GetChallengeProgress(this);
				if (challengeProgress != null)
				{
					quotaAtActivation = challengeProgress.quotaAtActivation;
				}
			}
			context = new ChallengeContext
			{
				bet = bet,
				payout = payout,
				gameType = gameType,
				gamePosition = position,
				quotaAtActivation = quotaAtActivation,
				hadTipsyFortuneBuff = false,
				hadInspiringMelodyBuff = false,
				hadImmunityBuff = false
			};
		}
		else
		{
			context.bet = bet;
			context.payout = payout;
			context.gameType = gameType;
			context.gamePosition = position;
			if (context.quotaAtActivation == 0L && NetworkSingleton<ChallengeManager>.Instance != null)
			{
				ChallengeProgress challengeProgress2 = NetworkSingleton<ChallengeManager>.Instance.GetChallengeProgress(this);
				if (challengeProgress2 != null)
				{
					context.quotaAtActivation = challengeProgress2.quotaAtActivation;
				}
			}
		}
		foreach (ChallengeConditionData condition in conditions)
		{
			if (condition != null)
			{
				condition.OnGameResult(bet, payout, gameType, position);
				if (!condition.Evaluate(context))
				{
					break;
				}
			}
		}
	}

	public int GetTicketReward()
	{
		if (manualTicketReward >= 0)
		{
			return manualTicketReward;
		}
		ChallengeSettings challengeSettings = GetChallengeSettings();
		if (challengeSettings != null)
		{
			return challengeSettings.GetTicketReward(floorIndex);
		}
		return 0;
	}

	public bool ShouldShowProgress()
	{
		if (conditions == null || conditions.Count == 0)
		{
			return false;
		}
		foreach (ChallengeConditionData condition in conditions)
		{
			if (condition != null && !condition.useAsResultFilter)
			{
				if (condition is WinCountConditionData)
				{
					return true;
				}
				if (condition is LossCountConditionData)
				{
					return true;
				}
				if (condition is PayoutAmountConditionData { checkTotalPayout: not false })
				{
					return true;
				}
				if (condition is ProfitConditionData { checkTotalProfit: not false })
				{
					return true;
				}
				if (condition is BetAmountConditionData { checkTotalBet: not false })
				{
					return true;
				}
				if (condition is CrapsSequenceConditionData)
				{
					return true;
				}
				if (condition is TimeConditionData)
				{
					return true;
				}
			}
		}
		return false;
	}

	public string GetProcessedDescription()
	{
		if (string.IsNullOrEmpty(description))
		{
			return description;
		}
		if (conditions == null)
		{
			return description;
		}
		string text = description;
		foreach (ChallengeConditionData condition in conditions)
		{
			if (condition == null)
			{
				continue;
			}
			long num = 0L;
			if (NetworkSingleton<ChallengeManager>.Instance != null)
			{
				ChallengeProgress challengeProgress = NetworkSingleton<ChallengeManager>.Instance.GetChallengeProgress(this);
				if (challengeProgress != null && challengeProgress.quotaAtActivation > 0)
				{
					num = challengeProgress.quotaAtActivation;
				}
			}
			if (num == 0L && NetworkSingleton<GameManager>.Instance != null)
			{
				num = NetworkSingleton<GameManager>.Instance.currentQuota;
			}
			if (condition is BetAmountConditionData betAmountConditionData)
			{
				long minBetAmount = betAmountConditionData.GetMinBetAmount(num);
				long maxBetAmount = betAmountConditionData.GetMaxBetAmount(num);
				text = text.Replace("[minAmount]", MoneyFormatter.FormatWithDollar(minBetAmount));
				text = text.Replace("[maxAmount]", (maxBetAmount > 0) ? MoneyFormatter.FormatWithDollar(maxBetAmount) : "");
			}
			else if (condition is ProfitConditionData profitConditionData)
			{
				long minProfit = profitConditionData.GetMinProfit(num);
				text = text.Replace("[minAmount]", MoneyFormatter.FormatWithDollar(minProfit));
			}
		}
		return text;
	}

	private ChallengeSettings GetChallengeSettings()
	{
		if (NetworkSingleton<ChallengeManager>.Instance != null)
		{
			FieldInfo field = typeof(ChallengeManager).GetField("challengeSettings", BindingFlags.Instance | BindingFlags.NonPublic);
			if (field != null)
			{
				ChallengeSettings challengeSettings = field.GetValue(NetworkSingleton<ChallengeManager>.Instance) as ChallengeSettings;
				if (challengeSettings != null)
				{
					return challengeSettings;
				}
			}
		}
		return SushiAndFish.Casino.Original.CasinoResources.FindObjectsOfTypeAll<ChallengeSettings>().FirstOrDefault();
	}
}

}
