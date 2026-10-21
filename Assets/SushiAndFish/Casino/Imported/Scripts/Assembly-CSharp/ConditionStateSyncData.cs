namespace SushiAndFish.Casino.Original
{
using global::System;

[Serializable]
public struct ConditionStateSyncData
{
	public int currentWinCount;

	public int consecutiveWinCount;

	public int currentLossCount;

	public int consecutiveLossCount;

	public long totalBetAmount;

	public long totalPayoutAmount;

	public long totalProfit;

	public float elapsedSinceStart;

	public float elapsedSinceLastGame;
}

}
