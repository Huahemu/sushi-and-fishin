namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

[Serializable]
[global::UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "ProfitConditionData")]
public class ProfitConditionData : ChallengeConditionData
{
	[Header("Profit Settings")]
	[Tooltip("The minimum profit multiplier (quota * this value). Enter 0.2 for 20% of quota.")]
	public float minProfitMultiplier = 0.1f;

	[Tooltip("Whether to check total profit or single game profit")]
	public bool checkTotalProfit = true;

	public long GetMinProfit(long quota = 0L)
	{
		if (quota == 0L)
		{
			if (NetworkSingleton<GameManager>.Instance == null)
			{
				return 0L;
			}
			quota = NetworkSingleton<GameManager>.Instance.currentQuota;
		}
		return FathF.RoundByFirstNDigits((long)((float)quota * minProfitMultiplier), 2);
	}

	public override bool Evaluate(ChallengeContext context)
	{
		long quota = ((context.quotaAtActivation > 0) ? context.quotaAtActivation : ((NetworkSingleton<GameManager>.Instance != null) ? NetworkSingleton<GameManager>.Instance.currentQuota : 0));
		long minProfit = GetMinProfit(quota);
		if (checkTotalProfit)
		{
			ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState == null)
			{
				return false;
			}
			return conditionState.totalProfit >= minProfit;
		}
		return context.profit >= minProfit;
	}

	public override float GetProgress(ChallengeContext context)
	{
		long quota = ((context.quotaAtActivation > 0) ? context.quotaAtActivation : ((NetworkSingleton<GameManager>.Instance != null) ? NetworkSingleton<GameManager>.Instance.currentQuota : 0));
		long minProfit = GetMinProfit(quota);
		if (minProfit <= 0)
		{
			return 0f;
		}
		if (checkTotalProfit)
		{
			ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState == null)
			{
				return 0f;
			}
			return Mathf.Clamp01((float)conditionState.totalProfit / (float)minProfit);
		}
		return Mathf.Clamp01((float)context.profit / (float)minProfit);
	}

	public override string GetProgressText(ChallengeContext context)
	{
		long quota = ((context.quotaAtActivation > 0) ? context.quotaAtActivation : ((NetworkSingleton<GameManager>.Instance != null) ? NetworkSingleton<GameManager>.Instance.currentQuota : 0));
		long minProfit = GetMinProfit(quota);
		if (checkTotalProfit)
		{
			ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState == null)
			{
				return $"Profit 0/{minProfit}";
			}
			return $"Profit {conditionState.totalProfit}/{minProfit}";
		}
		if (context.profit >= minProfit)
		{
			return $"Profit {context.profit} (✓)";
		}
		return $"Profit {context.profit}/{minProfit}";
	}

	public override void OnGameResult(long bet, long payout, CasinoGameType gameType, Vector3 position)
	{
		if (checkTotalProfit)
		{
			ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState != null)
			{
				conditionState.totalProfit += payout - bet;
			}
		}
	}

	public override void ResetCondition()
	{
		ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState != null)
		{
			conditionState.totalProfit = 0L;
		}
	}
}

}
