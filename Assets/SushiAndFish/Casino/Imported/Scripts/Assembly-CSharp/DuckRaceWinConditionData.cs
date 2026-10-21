namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

[Serializable]
[global::UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "DuckRaceWinConditionData")]
public class DuckRaceWinConditionData : ChallengeConditionData
{
	[Header("Duck Race Settings")]
	[Tooltip("The duck index (0-based) that must win. 0 = first duck, 1 = second duck, etc.")]
	public int requiredDuckIndex;

	[Tooltip("Whether the player must have bet on this duck")]
	public bool requireBetOnDuck = true;

	public override bool Evaluate(ChallengeContext context)
	{
		if (context.gameType != CasinoGameType.DuckRace)
		{
			ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState == null)
			{
				return false;
			}
			return conditionState.currentWinCount >= 1;
		}
		if (context.payout <= context.bet)
		{
			ConditionState conditionState2 = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState2 == null)
			{
				return false;
			}
			return conditionState2.currentWinCount >= 1;
		}
		int gameData = context.GetGameData("winningDuckIndex", -1);
		int gameData2 = context.GetGameData("betDuckIndex", -1);
		if (gameData == -1)
		{
			ConditionState conditionState3 = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState3 == null)
			{
				return false;
			}
			return conditionState3.currentWinCount >= 1;
		}
		bool flag = gameData == requiredDuckIndex;
		if (flag && requireBetOnDuck)
		{
			flag = gameData2 == requiredDuckIndex;
		}
		if (flag)
		{
			ConditionState conditionState4 = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState4 != null && conditionState4.currentWinCount == 0)
			{
				conditionState4.currentWinCount = 1;
			}
		}
		ConditionState conditionState5 = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState5 == null)
		{
			return false;
		}
		return conditionState5.currentWinCount >= 1;
	}

	public override float GetProgress(ChallengeContext context)
	{
		if (context.gameType != CasinoGameType.DuckRace)
		{
			return 0f;
		}
		ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState == null)
		{
			return 0f;
		}
		return Mathf.Clamp01((float)conditionState.currentWinCount / 1f);
	}

	public override string GetProgressText(ChallengeContext context)
	{
		ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState == null)
		{
			return "0/1 wins";
		}
		if (conditionState.currentWinCount < 1)
		{
			return $"Win with duck #{requiredDuckIndex + 1}";
		}
		return "Complete";
	}

	public override void OnGameResult(long bet, long payout, CasinoGameType gameType, Vector3 position)
	{
		if (gameType == CasinoGameType.DuckRace && payout > bet)
		{
			NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		}
	}
}

}
