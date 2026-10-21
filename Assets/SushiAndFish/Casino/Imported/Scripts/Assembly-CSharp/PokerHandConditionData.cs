namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

[Serializable]
[global::UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "PokerHandConditionData")]
public class PokerHandConditionData : ChallengeConditionData
{
	[Header("Poker Hand Settings")]
	[Tooltip("Minimum hand rank required (1=Pair, 2=TwoPair, 3=ThreeOfAKind, 4=Straight, 5=Flush, 6=FullHouse, 7=FourOfAKind, 8=StraightFlush)")]
	public int minHandRank = 3;

	[Tooltip("Whether cards must not be locked (cardsToKeep must be empty)")]
	public bool requireNoLockedCards = true;

	[Tooltip("Whether the player must win (payout > bet)")]
	public bool requireWin;

	public override bool Evaluate(ChallengeContext context)
	{
		if (context.gameType != CasinoGameType.Poker)
		{
			ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState == null)
			{
				return false;
			}
			return conditionState.currentWinCount >= 1;
		}
		if (requireWin && context.payout <= context.bet)
		{
			ConditionState conditionState2 = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState2 == null)
			{
				return false;
			}
			return conditionState2.currentWinCount >= 1;
		}
		int gameData = context.GetGameData("handRank", -1);
		if (gameData == -1)
		{
			ConditionState conditionState3 = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState3 == null)
			{
				return false;
			}
			return conditionState3.currentWinCount >= 1;
		}
		if (gameData < minHandRank)
		{
			ConditionState conditionState4 = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState4 == null)
			{
				return false;
			}
			return conditionState4.currentWinCount >= 1;
		}
		if (requireNoLockedCards)
		{
			int gameData2 = context.GetGameData("lockedCardsCount", -1);
			if (gameData2 == -1 || gameData2 > 0)
			{
				ConditionState conditionState5 = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
				if (conditionState5 == null)
				{
					return false;
				}
				return conditionState5.currentWinCount >= 1;
			}
		}
		ConditionState conditionState6 = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState6 != null && conditionState6.currentWinCount == 0)
		{
			conditionState6.currentWinCount = 1;
		}
		if (conditionState6 == null)
		{
			return false;
		}
		return conditionState6.currentWinCount >= 1;
	}

	public override float GetProgress(ChallengeContext context)
	{
		if (context.gameType != CasinoGameType.Poker)
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
			return "0/1";
		}
		string handRankName = GetHandRankName(minHandRank);
		string text = (requireNoLockedCards ? " without locking cards" : "");
		string text2 = (requireWin ? " and win" : "");
		if (conditionState.currentWinCount < 1)
		{
			return "Get " + handRankName + text + text2;
		}
		return "Complete";
	}

	public override void OnGameResult(long bet, long payout, CasinoGameType gameType, Vector3 position)
	{
		if (gameType == CasinoGameType.Poker)
		{
			NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		}
	}

	private string GetHandRankName(int rank)
	{
		return rank switch
		{
			1 => "Pair", 
			2 => "Two Pair", 
			3 => "Three of a Kind", 
			4 => "Straight", 
			5 => "Flush", 
			6 => "Full House", 
			7 => "Four of a Kind", 
			8 => "Straight Flush", 
			_ => $"Rank {rank}", 
		};
	}
}

}
