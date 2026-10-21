namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

[Serializable]
public class PayoutAmountConditionData : ChallengeConditionData
{
	[Header("Payout Amount Settings")]
	[Tooltip("The minimum payout amount required")]
	public int minPayoutAmount = 1000;

	[Tooltip("The maximum payout amount (0 = no maximum)")]
	public int maxPayoutAmount;

	[Tooltip("Whether to check total payout amount or single payout")]
	public bool checkTotalPayout;

	public override bool Evaluate(ChallengeContext context)
	{
		if (checkTotalPayout)
		{
			ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState == null)
			{
				return false;
			}
			if (conditionState.totalPayoutAmount >= minPayoutAmount)
			{
				if (maxPayoutAmount != 0)
				{
					return conditionState.totalPayoutAmount <= maxPayoutAmount;
				}
				return true;
			}
			return false;
		}
		if (context.payout >= minPayoutAmount)
		{
			if (maxPayoutAmount != 0)
			{
				return context.payout <= maxPayoutAmount;
			}
			return true;
		}
		return false;
	}

	public override float GetProgress(ChallengeContext context)
	{
		if (checkTotalPayout)
		{
			ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState == null)
			{
				return 0f;
			}
			return Mathf.Clamp01((float)conditionState.totalPayoutAmount / (float)minPayoutAmount);
		}
		return Mathf.Clamp01((float)context.payout / (float)minPayoutAmount);
	}

	public override string GetProgressText(ChallengeContext context)
	{
		if (checkTotalPayout)
		{
			ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState == null)
			{
				return $"Payout 0/{minPayoutAmount} total";
			}
			return $"Payout {conditionState.totalPayoutAmount}/{minPayoutAmount} total";
		}
		if (context.payout >= minPayoutAmount)
		{
			return $"Payout {context.payout} (✓)";
		}
		return $"Payout {context.payout}/{minPayoutAmount}";
	}

	public override void OnGameResult(long bet, long payout, CasinoGameType gameType, Vector3 position)
	{
		if (checkTotalPayout)
		{
			ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState != null)
			{
				conditionState.totalPayoutAmount += payout;
			}
		}
	}

	public override void ResetCondition()
	{
		ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState != null)
		{
			conditionState.totalPayoutAmount = 0L;
		}
	}
}

}
