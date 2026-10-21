namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

[Serializable]
[global::UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "BlackjackHandValueConditionData")]
public class BlackjackHandValueConditionData : ChallengeConditionData
{
	[Header("Blackjack Hand Value Settings")]
	[Tooltip("Minimum hand value required (inclusive). Set to 0 to ignore.")]
	public int minHandValue;

	[Tooltip("Maximum hand value allowed (exclusive). Set to 0 to ignore.")]
	public int maxHandValue = 10;

	[Tooltip("Whether to check player hand or dealer hand")]
	public bool checkPlayerHand = true;

	public override float GetProgress(ChallengeContext context)
	{
		if (context.gameType != CasinoGameType.Blackjack)
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
		if (conditionState.currentWinCount >= 1)
		{
			return "Complete";
		}
		string result = "Win with hand value";
		if (minHandValue > 0 && maxHandValue > 0)
		{
			result = $"Win with hand value between {minHandValue} and {maxHandValue}";
		}
		else if (minHandValue > 0)
		{
			result = $"Win with hand value >= {minHandValue}";
		}
		else if (maxHandValue > 0)
		{
			result = $"Win with hand value < {maxHandValue}";
		}
		return result;
	}

	public override void OnGameResult(long bet, long payout, CasinoGameType gameType, Vector3 position)
	{
	}

	public override bool Evaluate(ChallengeContext context)
	{
		if (context.gameType != CasinoGameType.Blackjack)
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
		int gameData = context.GetGameData(checkPlayerHand ? "playerHandValue" : "dealerHandValue", -1);
		if (gameData == -1)
		{
			ConditionState conditionState3 = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState3 == null)
			{
				return false;
			}
			return conditionState3.currentWinCount >= 1;
		}
		bool num = minHandValue == 0 || gameData >= minHandValue;
		bool flag = maxHandValue == 0 || gameData < maxHandValue;
		if (num & flag)
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
}

}
