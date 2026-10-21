namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

[Serializable]
public class ChallengeProgress
{
	public Challenge challenge;

	public bool isCompleted;

	public bool isClaimed;

	public float progress;

	public string progressText;

	public float startTime;

	public int completionCount;

	public long lastBet;

	public long lastPayout;

	public CasinoGameType lastGameType;

	public long quotaAtActivation;

	public ChallengeProgress(Challenge challenge)
	{
		this.challenge = challenge;
		isCompleted = false;
		isClaimed = false;
		progress = 0f;
		progressText = "";
		startTime = Time.time;
		completionCount = 0;
		lastBet = 0L;
		lastPayout = 0L;
		lastGameType = CasinoGameType.Blackjack;
		quotaAtActivation = ((NetworkSingleton<GameManager>.Instance != null) ? NetworkSingleton<GameManager>.Instance.currentQuota : 0);
	}

	public void UpdateProgress(ChallengeContext context)
	{
		if (!(challenge == null) && (!isCompleted || challenge.repeatable))
		{
			lastBet = context.bet;
			lastPayout = context.payout;
			lastGameType = context.gameType;
			context.quotaAtActivation = quotaAtActivation;
			progress = challenge.GetProgress(context);
			progressText = challenge.GetProgressText(context);
			if (challenge.IsCompleted(context) && !isCompleted)
			{
				isCompleted = true;
				completionCount++;
			}
		}
	}

	public void Reset()
	{
		isCompleted = false;
		isClaimed = false;
		progress = 0f;
		progressText = "";
		startTime = Time.time;
	}
}

}
