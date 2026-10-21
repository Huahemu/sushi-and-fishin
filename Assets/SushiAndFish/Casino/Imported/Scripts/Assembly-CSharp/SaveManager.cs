namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.IO;
using global::System.Linq;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::UnityEngine;

public class SaveManager : NetworkSingleton<SaveManager>
{
	private const string SAVE_DIRECTORY = "Saves";

	private SaveData currentSaveData;

	private bool hasLoadedGame;

	public string CurrentSaveName { get; private set; }

	private string SaveDirectoryPath => Path.Combine(SushiAndFish.Casino.Original.CasinoPaths.PersistentDataPath, "Saves");

	public IEnumerator LoadGameSaveCoroutine()
	{
		string text = PlayerPrefs.GetString("SelectedSaveData", "");
		string text2 = PlayerPrefs.GetString("SelectedSaveName", "");
		if (!string.IsNullOrEmpty(text))
		{
			try
			{
				currentSaveData = JsonUtility.FromJson<SaveData>(text);
				if (currentSaveData != null)
				{
					CurrentSaveName = currentSaveData.saveName;
					Debug.Log($"[SaveManager] Loaded save data from PlayerPrefs: {CurrentSaveName} (money: {currentSaveData.money}, tickets: {currentSaveData.tickets})");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError("[SaveManager] Failed to parse save data from PlayerPrefs: " + ex.Message);
			}
		}
		if (currentSaveData == null && !string.IsNullOrEmpty(text2))
		{
			currentSaveData = LoadSaveDataFromFile(text2);
			if (currentSaveData != null)
			{
				CurrentSaveName = currentSaveData.saveName;
				Debug.Log("[SaveManager] Loaded save data from file: " + CurrentSaveName);
			}
		}
		if (currentSaveData != null)
		{
			if (currentSaveData.seed == 0)
			{
				currentSaveData.seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
				Debug.Log($"[SaveManager] Generated new seed for old save: {currentSaveData.seed}");
			}
			if (NetworkSingleton<SeededRandomManager>.Instance != null)
			{
				NetworkSingleton<SeededRandomManager>.Instance.InitializeSeed(currentSaveData.seed);
			}
		}
		LoadGame();
		yield return null;
	}

	[Server]
	public void SaveGame()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void SaveManager::SaveGame()' called when server was not active");
			return;
		}
		if (currentSaveData == null || string.IsNullOrEmpty(CurrentSaveName))
		{
			Debug.LogWarning("[SaveManager] No save data to save");
			return;
		}
		GameManager instance = NetworkSingleton<GameManager>.Instance;
		MoneyManager instance2 = NetworkSingleton<MoneyManager>.Instance;
		ItemManager instance3 = NetworkSingleton<ItemManager>.Instance;
		ChallengeManager instance4 = NetworkSingleton<ChallengeManager>.Instance;
		if (instance == null || instance2 == null || instance3 == null)
		{
			Debug.LogError("[SaveManager] Required managers not found");
			return;
		}
		currentSaveData.saveTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		currentSaveData.successfulQuota = instance.successfulQuota;
		currentSaveData.daysLeft = instance.daysLeft;
		currentSaveData.daysPassed = instance.daysPassed;
		currentSaveData.currentQuota = instance.currentQuota;
		currentSaveData.currentFloor = instance.currentFloor;
		currentSaveData.requiredQuotaToNextFloor = instance.requiredQuotaToNextFloor;
		currentSaveData.money = instance2.balance;
		currentSaveData.tickets = instance2.ticketBalance;
		if (NetworkSingleton<PayoutTracker>.Instance != null)
		{
			currentSaveData.payoutTotalWins = NetworkSingleton<PayoutTracker>.Instance.GetLifetimeTotalWins();
			currentSaveData.payoutTotalLosses = NetworkSingleton<PayoutTracker>.Instance.GetLifetimeTotalLosses();
		}
		if (currentSaveData.seed == 0)
		{
			currentSaveData.seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
		}
		currentSaveData.itemIds = instance3.GetCurrentItemIds();
		currentSaveData.challengeIds.Clear();
		currentSaveData.challengeProgress.Clear();
		if (instance4 != null)
		{
			foreach (ChallengeProgress activeChallenge in instance4.GetActiveChallenges())
			{
				if (activeChallenge?.challenge != null)
				{
					currentSaveData.challengeIds.Add(activeChallenge.challenge.challengeID);
					ConditionStateSyncData[] conditionStateSnapshot = instance4.GetConditionStateSnapshot(activeChallenge.challenge);
					ChallengeProgressSaveData challengeProgressSaveData = new ChallengeProgressSaveData
					{
						challengeID = activeChallenge.challenge.challengeID,
						progress = activeChallenge.progress,
						isCompleted = activeChallenge.isCompleted,
						isClaimed = activeChallenge.isClaimed,
						completionCount = activeChallenge.completionCount,
						lastBet = activeChallenge.lastBet,
						lastPayout = activeChallenge.lastPayout,
						lastGameType = activeChallenge.lastGameType,
						quotaAtActivation = activeChallenge.quotaAtActivation
					};
					if (conditionStateSnapshot != null && conditionStateSnapshot.Length != 0)
					{
						challengeProgressSaveData.conditionStates.AddRange(conditionStateSnapshot);
					}
					currentSaveData.challengeProgress.Add(challengeProgressSaveData);
				}
			}
		}
		currentSaveData.playerOrganStates.Clear();
		OrganManager instance5 = NetworkSingleton<OrganManager>.Instance;
		if (instance5 != null)
		{
			foreach (KeyValuePair<ulong, PlayerOrganData> item in instance5.GetAllOrganDataBySteamId())
			{
				PlayerOrganData value = item.Value;
				currentSaveData.playerOrganStates.Add(new PlayerOrganSaveData
				{
					steamId = item.Key.ToString(),
					leftEye = value.leftEye,
					rightEye = value.rightEye,
					body = value.body,
					mouth = value.mouth
				});
			}
			Debug.Log($"[SaveManager] Saved organ states for {currentSaveData.playerOrganStates.Count} players");
		}
		currentSaveData.profitHistory.Clear();
		MoneyDisplayAndFeedbacks moneyDisplayAndFeedbacks = UnityEngine.Object.FindFirstObjectByType<MoneyDisplayAndFeedbacks>();
		if (moneyDisplayAndFeedbacks != null)
		{
			foreach (KeyValuePair<string, long> item2 in moneyDisplayAndFeedbacks.GetProfitHistorySnapshot())
			{
				currentSaveData.profitHistory.Add(new ProfitHistorySaveData
				{
					playerName = item2.Key,
					profitAmount = item2.Value
				});
			}
		}
		currentSaveData.playerUpgradeStates.Clear();
		UpgradeManager instance6 = NetworkSingleton<UpgradeManager>.Instance;
		if (instance6 != null)
		{
			foreach (KeyValuePair<ulong, PlayerUpgradeData> item3 in instance6.GetAllUpgradeDataBySteamId())
			{
				currentSaveData.playerUpgradeStates.Add(new PlayerUpgradeSaveData
				{
					steamId = item3.Key.ToString(),
					upgrades = item3.Value.Upgrades.Select((KeyValuePair<PlayerUpgradeType, float> kvp) => new PlayerUpgradeValue
					{
						type = kvp.Key,
						value = kvp.Value
					}).ToList()
				});
			}
		}
		SaveGameDataToFile(currentSaveData);
	}

	[Server]
	public void LoadGame()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void SaveManager::LoadGame()' called when server was not active");
			return;
		}
		if (currentSaveData == null)
		{
			Debug.LogWarning("[SaveManager] No save data to load");
			return;
		}
		if (hasLoadedGame)
		{
			Debug.Log("[SaveManager] Save data already applied, skipping duplicate load");
			return;
		}
		GameManager instance = NetworkSingleton<GameManager>.Instance;
		MoneyManager instance2 = NetworkSingleton<MoneyManager>.Instance;
		ItemManager instance3 = NetworkSingleton<ItemManager>.Instance;
		ChallengeManager instance4 = NetworkSingleton<ChallengeManager>.Instance;
		if (instance == null || instance2 == null || instance3 == null)
		{
			Debug.LogError("[SaveManager] Required managers not found - will retry");
			return;
		}
		instance.NetworksuccessfulQuota = currentSaveData.successfulQuota;
		instance.NetworkdaysLeft = currentSaveData.daysLeft;
		instance.NetworkdaysPassed = currentSaveData.daysPassed;
		instance.NetworkcurrentQuota = currentSaveData.currentQuota;
		instance.NetworkcurrentFloor = currentSaveData.currentFloor;
		instance.NetworkrequiredQuotaToNextFloor = currentSaveData.requiredQuotaToNextFloor;
		instance2.SetBalance(currentSaveData.money, null, ChangeType.Save);
		instance2.TrySetTicketBalance(currentSaveData.tickets);
		if (NetworkSingleton<PayoutTracker>.Instance != null)
		{
			NetworkSingleton<PayoutTracker>.Instance.SetLifetimeTotals(currentSaveData.payoutTotalWins, currentSaveData.payoutTotalLosses);
		}
		instance3.SetCurrentItems(currentSaveData.itemIds);
		if (instance4 != null && currentSaveData != null)
		{
			ChallengeSettings challengeSettings = SushiAndFish.Casino.Original.CasinoResources.Load<ChallengeSettings>("ChallengeSettings");
			int num = 0;
			if (currentSaveData.challengeProgress != null && currentSaveData.challengeProgress.Count > 0)
			{
				foreach (ChallengeProgressSaveData savedProgress in currentSaveData.challengeProgress)
				{
					Challenge challenge = challengeSettings.challenges.FirstOrDefault((Challenge c) => c.challengeID == savedProgress.challengeID);
					if (!(challenge == null))
					{
						instance4.RestoreChallenge(challenge, savedProgress.isCompleted, savedProgress.isClaimed, savedProgress.progress, savedProgress.completionCount, savedProgress.lastBet, savedProgress.lastPayout, savedProgress.lastGameType, savedProgress.quotaAtActivation, savedProgress.conditionStates?.ToArray());
						num++;
					}
				}
				Debug.Log($"[SaveManager] Restored {num}/{currentSaveData.challengeProgress.Count} challenges with progress");
			}
			else
			{
				List<int> challengeIds = currentSaveData.challengeIds;
				if (challengeIds != null && challengeIds.Count > 0)
				{
					foreach (int challengeId in currentSaveData.challengeIds)
					{
						if (instance4.ActivateChallenge(challengeSettings.challenges.FirstOrDefault((Challenge c) => c.challengeID == challengeId)))
						{
							num++;
						}
					}
					Debug.Log($"[SaveManager] Activated {num}/{currentSaveData.challengeIds.Count} challenges");
				}
			}
		}
		OrganManager instance5 = NetworkSingleton<OrganManager>.Instance;
		if (instance5 != null && currentSaveData.playerOrganStates != null && currentSaveData.playerOrganStates.Count > 0)
		{
			int num2 = 0;
			foreach (PlayerOrganSaveData playerOrganState in currentSaveData.playerOrganStates)
			{
				if (!string.IsNullOrEmpty(playerOrganState.steamId) && ulong.TryParse(playerOrganState.steamId, out var result))
				{
					bool mouth = playerOrganState.mouth;
					instance5.SetOrganDataBySteamId(result, playerOrganState.leftEye, playerOrganState.rightEye, playerOrganState.body, mouth);
					num2++;
				}
			}
			Debug.Log($"[SaveManager] Loaded organ states for {num2} players");
			instance5.ServerApplyAllOrganSettings();
		}
		UpgradeManager instance6 = NetworkSingleton<UpgradeManager>.Instance;
		if (instance6 != null && currentSaveData.playerUpgradeStates != null && currentSaveData.playerUpgradeStates.Count > 0)
		{
			int num3 = 0;
			foreach (PlayerUpgradeSaveData playerUpgradeState in currentSaveData.playerUpgradeStates)
			{
				if (string.IsNullOrEmpty(playerUpgradeState.steamId) || !ulong.TryParse(playerUpgradeState.steamId, out var result2))
				{
					continue;
				}
				foreach (PlayerUpgradeValue upgrade in playerUpgradeState.upgrades)
				{
					instance6.SetUpgradeData(result2, upgrade.type, upgrade.value);
				}
			}
			Debug.Log($"[SaveManager] Loaded upgrade states for {num3} players");
		}
		MoneyDisplayAndFeedbacks moneyDisplayAndFeedbacks = UnityEngine.Object.FindFirstObjectByType<MoneyDisplayAndFeedbacks>();
		if (moneyDisplayAndFeedbacks != null && currentSaveData.profitHistory != null)
		{
			Dictionary<string, long> dictionary = new Dictionary<string, long>();
			foreach (ProfitHistorySaveData item in currentSaveData.profitHistory)
			{
				if (!string.IsNullOrWhiteSpace(item.playerName))
				{
					dictionary[item.playerName] = item.profitAmount;
				}
			}
			moneyDisplayAndFeedbacks.SetProfitHistory(dictionary);
		}
		hasLoadedGame = true;
		Debug.Log($"[SaveManager] Applied save data to game state: {CurrentSaveName} (money: {instance2.balance}, tickets: {instance2.ticketBalance})");
	}

	[Server]
	public void ResetCurrentSaveToDefaults()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void SaveManager::ResetCurrentSaveToDefaults()' called when server was not active");
			return;
		}
		if (string.IsNullOrEmpty(CurrentSaveName))
		{
			Debug.LogWarning("[SaveManager] No current save name to reset");
			return;
		}
		GameSettings gameSettings = SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
		if (gameSettings == null)
		{
			Debug.LogError("[SaveManager] GameSettings not found - cannot reset save");
			return;
		}
		long requiredQuotaToNextFloor = 0L;
		if (gameSettings.floorData != null && gameSettings.floorData.Count > 0)
		{
			requiredQuotaToNextFloor = ((gameSettings.floorData.Count > 1) ? gameSettings.floorData[1].requiredQuotaToAccess : gameSettings.floorData[0].requiredQuotaToAccess);
		}
		currentSaveData = new SaveData
		{
			saveName = CurrentSaveName,
			saveTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			successfulQuota = 0,
			daysLeft = gameSettings.daysBeforeQuota,
			daysPassed = 0,
			currentQuota = gameSettings.startingQuota,
			currentFloor = 0,
			requiredQuotaToNextFloor = requiredQuotaToNextFloor,
			money = gameSettings.startingMoney,
			tickets = gameSettings.startingTicket,
			itemIds = new List<int>(),
			challengeIds = new List<int>(),
			challengeProgress = new List<ChallengeProgressSaveData>(),
			playerOrganStates = new List<PlayerOrganSaveData>(),
			profitHistory = new List<ProfitHistorySaveData>(),
			payoutTotalWins = 0L,
			payoutTotalLosses = 0L,
			seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue)
		};
		if (NetworkSingleton<SeededRandomManager>.Instance != null)
		{
			NetworkSingleton<SeededRandomManager>.Instance.InitializeSeed(currentSaveData.seed);
		}
		SaveGameDataToFile(currentSaveData);
		OrganManager instance = NetworkSingleton<OrganManager>.Instance;
		if (instance != null)
		{
			instance.ServerResetAllOrgansToDefaults();
		}
		UpgradeManager instance2 = NetworkSingleton<UpgradeManager>.Instance;
		if (instance2 != null)
		{
			instance2.ServerResetAllUpgradesToDefaults();
		}
		ChallengeManager instance3 = NetworkSingleton<ChallengeManager>.Instance;
		if (instance3 != null)
		{
			instance3.ServerResetAllChallenges();
		}
		MoneyDisplayAndFeedbacks instance4 = NetworkSingleton<MoneyDisplayAndFeedbacks>.Instance;
		if (instance4 != null)
		{
			instance4.ServerResetProfitHistory();
		}
		hasLoadedGame = false;
		Debug.Log("[SaveManager] Reset save to defaults: " + CurrentSaveName);
	}

	private SaveData LoadSaveDataFromFile(string saveName)
	{
		if (string.IsNullOrEmpty(saveName))
		{
			Debug.LogError("[SaveManager] Cannot load save with empty name");
			return null;
		}
		if (!Directory.Exists(SaveDirectoryPath))
		{
			Directory.CreateDirectory(SaveDirectoryPath);
		}
		string text = Path.Combine(SaveDirectoryPath, saveName + ".json");
		if (!File.Exists(text))
		{
			Debug.LogError("[SaveManager] Save file not found: " + text);
			return null;
		}
		try
		{
			SaveData result = JsonUtility.FromJson<SaveData>(File.ReadAllText(text));
			Debug.Log("[SaveManager] Loaded save data from file: " + saveName);
			return result;
		}
		catch (Exception ex)
		{
			Debug.LogError("[SaveManager] Failed to load save: " + ex.Message);
			return null;
		}
	}

	private void SaveGameDataToFile(SaveData saveData)
	{
		if (saveData == null || string.IsNullOrEmpty(saveData.saveName))
		{
			Debug.LogWarning("[SaveManager] No save data to save");
			return;
		}
		if (!Directory.Exists(SaveDirectoryPath))
		{
			Directory.CreateDirectory(SaveDirectoryPath);
		}
		string path = Path.Combine(SaveDirectoryPath, saveData.saveName + ".json");
		try
		{
			saveData.saveTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			string contents = JsonUtility.ToJson(saveData, prettyPrint: true);
			File.WriteAllText(path, contents);
			Debug.Log("[SaveManager] Saved game to file: " + saveData.saveName);
		}
		catch (Exception ex)
		{
			Debug.LogError("[SaveManager] Failed to save game: " + ex.Message);
		}
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
