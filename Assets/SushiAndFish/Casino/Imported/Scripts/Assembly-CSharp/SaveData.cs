namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;

[Serializable]
public class SaveData
{
	public string saveName;

	public long saveTime;

	public int successfulQuota;

	public int daysLeft;

	public int daysPassed;

	public long currentQuota;

	public int currentFloor;

	public long requiredQuotaToNextFloor;

	public long money;

	public long tickets;

	public List<int> itemIds = new List<int>();

	public List<int> challengeIds = new List<int>();

	public List<ChallengeProgressSaveData> challengeProgress = new List<ChallengeProgressSaveData>();

	public List<PlayerOrganSaveData> playerOrganStates = new List<PlayerOrganSaveData>();

	public List<PlayerUpgradeSaveData> playerUpgradeStates = new List<PlayerUpgradeSaveData>();

	public List<ProfitHistorySaveData> profitHistory = new List<ProfitHistorySaveData>();

	public long payoutTotalWins;

	public long payoutTotalLosses;

	public int seed;
}

}
