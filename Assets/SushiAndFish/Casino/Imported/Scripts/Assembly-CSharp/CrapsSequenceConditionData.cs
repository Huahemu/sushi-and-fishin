namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

[Serializable]
public class CrapsSequenceConditionData : ChallengeConditionData
{
	[Header("Craps Sequence Settings")]
	[Tooltip("The sequence of dice values that must be rolled in order")]
	public List<int> requiredSequence = new List<int> { 6, 7 };

	[Tooltip("Whether the sequence must be completed in a single game")]
	public bool mustBeInSingleGame;

	public override float GetProgress(ChallengeContext context)
	{
		if (context.gameType != CasinoGameType.Craps)
		{
			return 0f;
		}
		ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState == null)
		{
			return 0f;
		}
		return Mathf.Clamp01((float)conditionState.GetCustomInt("sequenceProgress") / (float)requiredSequence.Count);
	}

	public override string GetProgressText(ChallengeContext context)
	{
		ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState == null)
		{
			return "0/0";
		}
		int customInt = conditionState.GetCustomInt("sequenceProgress");
		string arg = string.Join(" → ", requiredSequence);
		return $"{customInt}/{requiredSequence.Count} ({arg})";
	}

	public override void OnGameResult(long bet, long payout, CasinoGameType gameType, Vector3 position)
	{
	}

	public override bool Evaluate(ChallengeContext context)
	{
		ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState == null)
		{
			return false;
		}
		int num = conditionState.GetCustomInt("sequenceProgress");
		if (context.gameType != CasinoGameType.Craps)
		{
			return num >= requiredSequence.Count;
		}
		int gameData = context.GetGameData("lastDiceRoll", -1);
		if (gameData == -1)
		{
			return num >= requiredSequence.Count;
		}
		if (num < requiredSequence.Count && gameData == requiredSequence[num])
		{
			num++;
			conditionState.SetCustomInt("sequenceProgress", num);
			if (num >= requiredSequence.Count)
			{
				conditionState.currentWinCount++;
			}
		}
		else if (mustBeInSingleGame && num > 0)
		{
			conditionState.SetCustomInt("sequenceProgress", 0);
			num = 0;
		}
		return num >= requiredSequence.Count;
	}
}

}
