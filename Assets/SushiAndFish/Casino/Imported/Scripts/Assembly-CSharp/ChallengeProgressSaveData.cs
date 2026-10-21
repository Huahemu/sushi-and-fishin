namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;

[Serializable]
public class ChallengeProgressSaveData
{
	public int challengeID;

	public float progress;

	public bool isCompleted;

	public bool isClaimed;

	public int completionCount;

	public long lastBet;

	public long lastPayout;

	public CasinoGameType lastGameType;

	public long quotaAtActivation;

	public List<ConditionStateSyncData> conditionStates = new List<ConditionStateSyncData>();
}

}
