namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

[Serializable]
[global::UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "WinCountConditionData")]
public class WinCountConditionData : ChallengeConditionData
{
	[Header("Win Count Settings")]
	[Tooltip("The number of wins required")]
	public int requiredWins = 1;

	[Tooltip("Whether wins must be consecutive")]
	public bool consecutive;

	[Tooltip("Optional: Only count wins from a specific game type (None = all games)")]
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
			return conditionState.consecutiveWinCount >= requiredWins;
		}
		return conditionState.currentWinCount >= requiredWins;
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
			return Mathf.Clamp01((float)conditionState.consecutiveWinCount / (float)requiredWins);
		}
		return Mathf.Clamp01((float)conditionState.currentWinCount / (float)requiredWins);
	}

	public override string GetProgressText(ChallengeContext context)
	{
		ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState == null)
		{
			return "0/0 wins";
		}
		if (consecutive)
		{
			return $"{conditionState.consecutiveWinCount}/{requiredWins} consecutive wins";
		}
		return $"{conditionState.currentWinCount}/{requiredWins} wins";
	}

	public override void OnGameResult(long bet, long payout, CasinoGameType gameType, Vector3 position)
	{
		ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState != null && (!useSpecificGameType || gameType == specificGameType))
		{
			if (payout > bet)
			{
				conditionState.currentWinCount++;
				conditionState.consecutiveWinCount++;
			}
			else if (consecutive && payout < bet)
			{
				conditionState.consecutiveWinCount = 0;
			}
		}
	}

	public override void ResetCondition()
	{
		ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState != null)
		{
			conditionState.currentWinCount = 0;
			conditionState.consecutiveWinCount = 0;
		}
	}
}

}
