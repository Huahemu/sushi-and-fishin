namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

[Serializable]
[global::UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "BetAmountConditionData")]
public class BetAmountConditionData : ChallengeConditionData
{
	[Header("Bet Amount Settings")]
	[Tooltip("The minimum bet amount multiplier (quota * this value). Enter 0.2 for 20% of quota.")]
	public float minBetMultiplier = 0.1f;

	[Tooltip("The maximum bet amount multiplier (0 = no maximum). Enter 0.5 for 50% of quota.")]
	public float maxBetMultiplier;

	[Tooltip("Whether to check total bet amount or single bet")]
	public bool checkTotalBet;

	public long GetMinBetAmount(long quota = 0L)
	{
		if (quota == 0L)
		{
			if (NetworkSingleton<GameManager>.Instance == null)
			{
				return 0L;
			}
			quota = NetworkSingleton<GameManager>.Instance.currentQuota;
		}
		return FathF.RoundByFirstNDigits((long)((float)quota * minBetMultiplier), 2);
	}

	public long GetMaxBetAmount(long quota = 0L)
	{
		if (maxBetMultiplier <= 0f)
		{
			return 0L;
		}
		if (quota == 0L)
		{
			if (NetworkSingleton<GameManager>.Instance == null)
			{
				return 0L;
			}
			quota = NetworkSingleton<GameManager>.Instance.currentQuota;
		}
		return FathF.RoundByFirstNDigits((long)((float)quota * maxBetMultiplier), 2);
	}

	public override bool Evaluate(ChallengeContext context)
	{
		long quota = ((context.quotaAtActivation > 0) ? context.quotaAtActivation : ((NetworkSingleton<GameManager>.Instance != null) ? NetworkSingleton<GameManager>.Instance.currentQuota : 0));
		long minBetAmount = GetMinBetAmount(quota);
		long maxBetAmount = GetMaxBetAmount(quota);
		if (checkTotalBet)
		{
			ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState == null)
			{
				return false;
			}
			if (conditionState.totalBetAmount >= minBetAmount)
			{
				if (maxBetAmount != 0L)
				{
					return conditionState.totalBetAmount <= maxBetAmount;
				}
				return true;
			}
			return false;
		}
		if (context.bet >= minBetAmount)
		{
			if (maxBetAmount != 0L)
			{
				return context.bet <= maxBetAmount;
			}
			return true;
		}
		return false;
	}

	public override float GetProgress(ChallengeContext context)
	{
		long quota = ((context.quotaAtActivation > 0) ? context.quotaAtActivation : ((NetworkSingleton<GameManager>.Instance != null) ? NetworkSingleton<GameManager>.Instance.currentQuota : 0));
		long minBetAmount = GetMinBetAmount(quota);
		if (minBetAmount <= 0)
		{
			return 0f;
		}
		if (checkTotalBet)
		{
			ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState == null)
			{
				return 0f;
			}
			return Mathf.Clamp01((float)conditionState.totalBetAmount / (float)minBetAmount);
		}
		return Mathf.Clamp01((float)context.bet / (float)minBetAmount);
	}

	public override string GetProgressText(ChallengeContext context)
	{
		long quota = ((context.quotaAtActivation > 0) ? context.quotaAtActivation : ((NetworkSingleton<GameManager>.Instance != null) ? NetworkSingleton<GameManager>.Instance.currentQuota : 0));
		long minBetAmount = GetMinBetAmount(quota);
		if (checkTotalBet)
		{
			ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState == null)
			{
				return $"Bet 0/{minBetAmount} total";
			}
			return $"Bet {conditionState.totalBetAmount}/{minBetAmount} total";
		}
		if (context.bet >= minBetAmount)
		{
			return $"Bet {context.bet} (✓)";
		}
		return $"Bet {context.bet}/{minBetAmount}";
	}

	public override void OnGameResult(long bet, long payout, CasinoGameType gameType, Vector3 position)
	{
		if (checkTotalBet)
		{
			ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState != null)
			{
				conditionState.totalBetAmount += bet;
			}
		}
	}

	public override void ResetCondition()
	{
		ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState != null)
		{
			conditionState.totalBetAmount = 0L;
		}
	}
}

}
