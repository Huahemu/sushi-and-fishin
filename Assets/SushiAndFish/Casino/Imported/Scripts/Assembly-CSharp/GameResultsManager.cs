namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::UnityEngine;

public class GameResultsManager : NetworkSingleton<GameResultsManager>
{
	private readonly Dictionary<PlayerProfile, PlayerResultData> results = new Dictionary<PlayerProfile, PlayerResultData>();

	public readonly Dictionary<PlayerProfile, PlayerResultData> lastResults = new Dictionary<PlayerProfile, PlayerResultData>();

	[SerializeField]
	private List<PlayerResultDebugEntry> debugEntries = new List<PlayerResultDebugEntry>();

	public event Action<long, long, PlayerProfile, CasinoGameType, Vector3, bool, bool, bool, Dictionary<string, object>> OnResultRegistered;

	[Server]
	public void RegisterResult(long bet, long payout, PlayerProfile playerProfile, CasinoGameType gameType, Vector3 position, bool hadTipsyFortune = false, bool hadInspiringMelody = false, bool hadImmunity = false, Dictionary<string, object> gameSpecificData = null)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GameResultsManager::RegisterResult(System.Int64,System.Int64,PlayerProfile,CasinoGameType,UnityEngine.Vector3,System.Boolean,System.Boolean,System.Boolean,System.Collections.Generic.Dictionary`2<System.String,System.Object>)' called when server was not active");
			return;
		}
		if (!results.TryGetValue(playerProfile, out var value))
		{
			value = new PlayerResultData();
			results[playerProfile] = value;
		}
		value.totalBets += bet;
		value.totalPayouts += payout;
		if (!value.ByGameType.TryGetValue(gameType, out var value2))
		{
			value2 = new GameResultBreakdown();
			value.ByGameType[gameType] = value2;
		}
		value2.totalBet += bet;
		value2.totalPayout += payout;
		SetAsLastResult(bet, payout, playerProfile);
		OnResultRegistered?.Invoke(bet, payout, playerProfile, gameType, position, hadTipsyFortune, hadInspiringMelody, hadImmunity, gameSpecificData);
		UpdateDebugList(playerProfile, value);
	}

	[Server]
	private void SetAsLastResult(long bet, long payout, PlayerProfile playerProfile)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GameResultsManager::SetAsLastResult(System.Int64,System.Int64,PlayerProfile)' called when server was not active");
			return;
		}
		PlayerResultData playerResultData = new PlayerResultData();
		playerResultData.totalBets = bet;
		playerResultData.totalPayouts = payout;
		lastResults[playerProfile] = playerResultData;
	}

	[Server]
	public PlayerResultData GetPlayerResult(PlayerProfile profile)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'PlayerResultData GameResultsManager::GetPlayerResult(PlayerProfile)' called when server was not active");
			return null;
		}
		results.TryGetValue(profile, out var value);
		return value;
	}

	[Server]
	private void UpdateDebugList(PlayerProfile profile, PlayerResultData data)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GameResultsManager::UpdateDebugList(PlayerProfile,PlayerResultData)' called when server was not active");
			return;
		}
		PlayerResultDebugEntry playerResultDebugEntry = debugEntries.Find((PlayerResultDebugEntry e) => e.player == profile);
		if (playerResultDebugEntry == null)
		{
			playerResultDebugEntry = new PlayerResultDebugEntry();
			debugEntries.Add(playerResultDebugEntry);
		}
		playerResultDebugEntry.player = profile;
		playerResultDebugEntry.totalBets = data.totalBets;
		playerResultDebugEntry.totalPayouts = data.totalPayouts;
		playerResultDebugEntry.netProfit = data.NetProfit;
	}

	[Server]
	public void ClearResults()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GameResultsManager::ClearResults()' called when server was not active");
			return;
		}
		results.Clear();
		lastResults.Clear();
		debugEntries.Clear();
		Debug.Log("[GameResultsManager] Results cleared");
	}

	[Server]
	public void RollbackResults(IEnumerable<PayoutRecord> records)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GameResultsManager::RollbackResults(System.Collections.Generic.IEnumerable`1<PayoutRecord>)' called when server was not active");
		}
		else
		{
			if (records == null)
			{
				return;
			}
			foreach (PayoutRecord record in records)
			{
				if (record == null || record.playerProfile == null)
				{
					continue;
				}
				if (results.TryGetValue(record.playerProfile, out var value))
				{
					value.totalBets -= record.bet;
					value.totalPayouts -= record.payout;
					if (value.ByGameType.TryGetValue(record.gameType, out var value2))
					{
						value2.totalBet -= record.bet;
						value2.totalPayout -= record.payout;
					}
					UpdateDebugList(record.playerProfile, value);
				}
				lastResults.Remove(record.playerProfile);
			}
		}
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
