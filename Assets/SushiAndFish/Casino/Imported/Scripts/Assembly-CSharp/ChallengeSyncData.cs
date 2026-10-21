namespace SushiAndFish.Casino.Original
{
using global::System;

[Serializable]
public struct ChallengeSyncData
{
	public int challengeID;

	public float progress;

	public bool isCompleted;

	public bool isClaimed;

	public int completionCount;

	public long lastBet;

	public long lastPayout;

	public CasinoGameType lastGameType;

	public ConditionStateSyncData[] conditionStates;
}

}
