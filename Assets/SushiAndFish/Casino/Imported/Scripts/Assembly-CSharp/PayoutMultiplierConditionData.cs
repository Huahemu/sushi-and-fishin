namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

[Serializable]
[global::UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "PayoutMultiplierConditionData")]
public class PayoutMultiplierConditionData : ChallengeConditionData
{
	[Header("Payout Multiplier Settings")]
	[Tooltip("The minimum payout multiplier required")]
	public float minPayoutMultiplier = 1f;

	[Tooltip("The maximum payout multiplier (0 = no maximum)")]
	public float maxPayoutMultiplier;

	public override bool Evaluate(ChallengeContext context)
	{
		if (context.bet <= 0)
		{
			return false;
		}
		float num = (float)context.payout / (float)context.bet;
		bool num2 = num >= minPayoutMultiplier;
		bool flag = maxPayoutMultiplier == 0f || num <= maxPayoutMultiplier;
		return num2 & flag;
	}

	public override float GetProgress(ChallengeContext context)
	{
		if (context.bet <= 0)
		{
			return 0f;
		}
		return Mathf.Clamp01((float)context.payout / (float)context.bet / minPayoutMultiplier);
	}

	public override string GetProgressText(ChallengeContext context)
	{
		if (context.bet <= 0)
		{
			return $"{minPayoutMultiplier:F1}x multiplier";
		}
		float num = (float)context.payout / (float)context.bet;
		if (num >= minPayoutMultiplier && (maxPayoutMultiplier == 0f || num <= maxPayoutMultiplier))
		{
			return $"{num:F1}x (✓)";
		}
		return $"{num:F1}x / {minPayoutMultiplier:F1}x";
	}

	public override void OnGameResult(long bet, long payout, CasinoGameType gameType, Vector3 position)
	{
	}

	public override void ResetCondition()
	{
	}
}

}
