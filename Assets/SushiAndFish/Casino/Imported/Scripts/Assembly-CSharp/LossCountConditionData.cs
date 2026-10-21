namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

[Serializable]
[global::UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "LossCountConditionData")]
public class LossCountConditionData : ChallengeConditionData
{
	[Header("Loss Count Settings")]
	[Tooltip("The number of losses required")]
	public int requiredLosses = 1;

	[Tooltip("Whether losses must be consecutive")]
	public bool consecutive;

	[Tooltip("Optional: Only count losses from a specific game type (None = all games)")]
	public CasinoGameType specificGameType;

	[Tooltip("Whether to filter by specific game type")]
	public bool useSpecificGameType;

	public override bool Evaluate(ChallengeContext context)
	{
		ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState == null)
		{
			return false;
		}
		if (consecutive)
		{
			return conditionState.consecutiveLossCount >= requiredLosses;
		}
		return conditionState.currentLossCount >= requiredLosses;
	}

	public override float GetProgress(ChallengeContext context)
	{
		ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState == null)
		{
			return 0f;
		}
		if (consecutive)
		{
			return Mathf.Clamp01((float)conditionState.consecutiveLossCount / (float)requiredLosses);
		}
		return Mathf.Clamp01((float)conditionState.currentLossCount / (float)requiredLosses);
	}

	public override string GetProgressText(ChallengeContext context)
	{
		ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState == null)
		{
			return "0/0 losses";
		}
		if (consecutive)
		{
			return $"{conditionState.consecutiveLossCount}/{requiredLosses} consecutive losses";
		}
		return $"{conditionState.currentLossCount}/{requiredLosses} losses";
	}

	public override void OnGameResult(long bet, long payout, CasinoGameType gameType, Vector3 position)
	{
		ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState != null && (!useSpecificGameType || gameType == specificGameType))
		{
			if (payout < bet)
			{
				conditionState.currentLossCount++;
				conditionState.consecutiveLossCount++;
			}
			else if (consecutive && payout > bet)
			{
				conditionState.consecutiveLossCount = 0;
			}
		}
	}

	public override void ResetCondition()
	{
		ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState != null)
		{
			conditionState.currentLossCount = 0;
			conditionState.consecutiveLossCount = 0;
		}
	}
}

}
