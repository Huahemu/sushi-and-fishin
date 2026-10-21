namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::System.Linq;
using global::System.Runtime.InteropServices;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::UnityEngine;
using global::UnityEngine.SceneManagement;

public class PayoutTracker : NetworkSingleton<PayoutTracker>
{
	[Header("Settings")]
	[SerializeField]
	private bool enableTracking = true;

	[SerializeField]
	private int maxHistoryRecords = 10000;

	[SerializeField]
	private readonly SyncList<PayoutRecord> payoutHistory = new SyncList<PayoutRecord>();

	[SyncVar]
	[SerializeField]
	private long lifetimeTotalWins;

	[SyncVar]
	[SerializeField]
	private long lifetimeTotalLosses;

	public long NetworklifetimeTotalWins
	{
		get
		{
			return lifetimeTotalWins;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref lifetimeTotalWins, 1uL, null);
		}
	}

	public long NetworklifetimeTotalLosses
	{
		get
		{
			return lifetimeTotalLosses;
		}
		[param: In]
		set
		{
			GeneratedSyncVarSetter(value, ref lifetimeTotalLosses, 2uL, null);
		}
	}

	public event Action<PayoutRecord> OnPayoutRecorded;

	protected override void OnAwake()
	{
		base.OnAwake();
		SyncList<PayoutRecord> syncList = payoutHistory;
		syncList.OnAdd = (Action<int>)Delegate.Combine(syncList.OnAdd, new Action<int>(OnPayoutAdded));
	}

	private void OnEnable()
	{
		NetworkSingleton<GameResultsManager>.Instance.OnResultRegistered += OnGameResultRegistered;
		if (NetworkSingleton<MoneyManager>.Instance != null)
		{
			MoneyManager instance = NetworkSingleton<MoneyManager>.Instance;
			instance.OnBalanceChanged = (Action<BalanceChangeData>)Delegate.Combine(instance.OnBalanceChanged, new Action<BalanceChangeData>(OnBalanceChanged));
		}
	}

	private void OnDisable()
	{
		if (NetworkSingleton<GameResultsManager>.Instance != null)
		{
			NetworkSingleton<GameResultsManager>.Instance.OnResultRegistered -= OnGameResultRegistered;
		}
		if (NetworkSingleton<MoneyManager>.Instance != null)
		{
			MoneyManager instance = NetworkSingleton<MoneyManager>.Instance;
			instance.OnBalanceChanged = (Action<BalanceChangeData>)Delegate.Remove(instance.OnBalanceChanged, new Action<BalanceChangeData>(OnBalanceChanged));
		}
	}

	[Server]
	private void OnBalanceChanged(BalanceChangeData data)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PayoutTracker::OnBalanceChanged(BalanceChangeData)' called when server was not active");
		}
		else if (!(SceneManager.GetActiveScene().name != "CasinoScene") && enableTracking && data != null && data.changeType != ChangeType.Save && data.changeAmount != 0L)
		{
			if (data.changeAmount > 0)
			{
				NetworklifetimeTotalWins = lifetimeTotalWins + data.changeAmount;
			}
			else
			{
				NetworklifetimeTotalLosses = lifetimeTotalLosses + Math.Abs(data.changeAmount);
			}
		}
	}

	[Server]
	public void InitializeStartingPoints()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PayoutTracker::InitializeStartingPoints()' called when server was not active");
		}
		else
		{
			if (!enableTracking)
			{
				return;
			}
			List<PlayerProfile> list = (from p in UnityEngine.Object.FindObjectsByType<PlayerProfile>(FindObjectsSortMode.None)
				where p != null && p.hasSynced && !string.IsNullOrEmpty(p.playerName)
				select p).ToList();
			float time = Time.time;
			foreach (PlayerProfile player in list)
			{
				if (!payoutHistory.Any((PayoutRecord r) => r.playerProfile == player))
				{
					PayoutRecord item = new PayoutRecord
					{
						timestamp = time,
						playerName = player.playerName,
						playerProfile = player,
						bet = 0L,
						payout = 0L,
						profit = 0L,
						isWin = false,
						isLoss = false,
						gameType = CasinoGameType.Blackjack,
						gamePosition = Vector3.zero
					};
					payoutHistory.Add(item);
				}
			}
		}
	}

	[Server]
	private void OnGameResultRegistered(long bet, long payout, PlayerProfile playerProfile, CasinoGameType gameType, Vector3 gamePosition, bool hadTipsyFortune, bool hadInspiringMelody, bool hadImmunity, Dictionary<string, object> gameSpecificData)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PayoutTracker::OnGameResultRegistered(System.Int64,System.Int64,PlayerProfile,CasinoGameType,UnityEngine.Vector3,System.Boolean,System.Boolean,System.Boolean,System.Collections.Generic.Dictionary`2<System.String,System.Object>)' called when server was not active");
		}
		else
		{
			if (!enableTracking || playerProfile == null)
			{
				return;
			}
			if (!payoutHistory.Any((PayoutRecord r) => r.playerProfile == playerProfile))
			{
				PayoutRecord item = new PayoutRecord
				{
					timestamp = Time.time,
					playerName = playerProfile.playerName,
					playerProfile = playerProfile,
					bet = 0L,
					payout = 0L,
					profit = 0L,
					isWin = false,
					isLoss = false,
					gameType = CasinoGameType.Blackjack,
					gamePosition = Vector3.zero
				};
				payoutHistory.Add(item);
			}
			long num = payout - bet;
			bool isWin = num > 0;
			bool isLoss = num < 0;
			PayoutRecord item2 = new PayoutRecord
			{
				timestamp = Time.time,
				playerName = playerProfile.playerName,
				playerProfile = playerProfile,
				bet = bet,
				payout = payout,
				profit = num,
				isWin = isWin,
				isLoss = isLoss,
				gameType = gameType,
				gamePosition = gamePosition
			};
			payoutHistory.Add(item2);
			if (payoutHistory.Count > maxHistoryRecords)
			{
				int num2 = payoutHistory.Count - maxHistoryRecords;
				for (int num3 = 0; num3 < num2; num3++)
				{
					payoutHistory.RemoveAt(0);
				}
			}
			Debug.Log($"[PayoutTracker] Recorded: {playerProfile.playerName} - Bet: {bet}, Payout: {payout}, Profit: {num} ({gameType})");
		}
	}

	private void OnPayoutAdded(int index)
	{
		if (index >= 0 && index < payoutHistory.Count)
		{
			OnPayoutRecorded?.Invoke(payoutHistory[index]);
		}
	}

	public List<PayoutRecord> GetPlayerRecords(PlayerProfile playerProfile)
	{
		if (playerProfile == null)
		{
			return new List<PayoutRecord>();
		}
		return payoutHistory.Where((PayoutRecord r) => r.playerProfile == playerProfile).ToList();
	}

	public List<PayoutRecord> GetPlayerRecords(string playerName)
	{
		if (string.IsNullOrEmpty(playerName))
		{
			return new List<PayoutRecord>();
		}
		return payoutHistory.Where((PayoutRecord r) => r.playerName == playerName).ToList();
	}

	public List<PayoutRecord> GetAllRecords()
	{
		return new List<PayoutRecord>(payoutHistory);
	}

	public List<PayoutRecord> GetRecordsInTimeRange(float startTime, float endTime)
	{
		return payoutHistory.Where((PayoutRecord r) => r.timestamp >= startTime && r.timestamp <= endTime).ToList();
	}

	public List<ProfitDataPoint> GetPlayerProfitOverTime(PlayerProfile playerProfile)
	{
		List<PayoutRecord> list = (from r in GetPlayerRecords(playerProfile)
			orderby r.timestamp
			select r).ToList();
		List<ProfitDataPoint> list2 = new List<ProfitDataPoint>();
		long num = 0L;
		foreach (PayoutRecord item in list)
		{
			num += item.profit;
			list2.Add(new ProfitDataPoint
			{
				time = item.timestamp,
				cumulativeProfit = num
			});
		}
		return list2;
	}

	public List<ProfitDataPoint> GetPlayerProfitByMinute(PlayerProfile playerProfile)
	{
		List<PayoutRecord> list = (from r in GetPlayerRecords(playerProfile)
			orderby r.timestamp
			select r).ToList();
		if (list.Count == 0)
		{
			return new List<ProfitDataPoint>();
		}
		Dictionary<int, long> dictionary = new Dictionary<int, long>();
		foreach (PayoutRecord item in list)
		{
			int key = Mathf.FloorToInt(item.timestamp / 60f);
			if (!dictionary.ContainsKey(key))
			{
				dictionary[key] = 0L;
			}
			dictionary[key] += item.profit;
		}
		List<ProfitDataPoint> list2 = new List<ProfitDataPoint>();
		long num = 0L;
		foreach (int item2 in dictionary.Keys.OrderBy((int m) => m).ToList())
		{
			num += dictionary[item2];
			list2.Add(new ProfitDataPoint
			{
				time = (float)item2 * 60f,
				cumulativeProfit = num
			});
		}
		return list2;
	}

	[Server]
	public void ClearHistory()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PayoutTracker::ClearHistory()' called when server was not active");
			return;
		}
		payoutHistory.Clear();
		Debug.Log("[PayoutTracker] History cleared");
	}

	[Server]
	public List<PayoutRecord> RollbackLastSeconds(float seconds)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Collections.Generic.List`1<PayoutRecord> PayoutTracker::RollbackLastSeconds(System.Single)' called when server was not active");
			return null;
		}
		if (!enableTracking || seconds <= 0f || payoutHistory.Count == 0)
		{
			return new List<PayoutRecord>();
		}
		float cutoff = Time.time - seconds;
		List<PayoutRecord> list = (from r in payoutHistory
			where r.timestamp >= cutoff
			orderby r.timestamp
			select r).ToList();
		if (list.Count == 0)
		{
			return list;
		}
		foreach (PayoutRecord item in list)
		{
			payoutHistory.Remove(item);
		}
		Debug.Log($"[PayoutTracker] Rolled back {list.Count} records from last {seconds} seconds");
		return list;
	}

	public int GetRecordCount()
	{
		return payoutHistory.Count;
	}

	public long GetLifetimeTotalWins()
	{
		return lifetimeTotalWins;
	}

	public long GetLifetimeTotalLosses()
	{
		return lifetimeTotalLosses;
	}

	public long GetLifetimeNetTotal()
	{
		return lifetimeTotalWins - lifetimeTotalLosses;
	}

	[Server]
	public void SetLifetimeTotals(long totalWins, long totalLosses)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void PayoutTracker::SetLifetimeTotals(System.Int64,System.Int64)' called when server was not active");
			return;
		}
		NetworklifetimeTotalWins = Math.Max(0L, totalWins);
		NetworklifetimeTotalLosses = Math.Max(0L, totalLosses);
	}

	public PayoutTracker()
	{
		InitSyncObject(payoutHistory);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected override void SerializeSyncVars(NetworkWriter writer, bool forceAll)
	{
		base.SerializeSyncVars(writer, forceAll);
		if (forceAll)
		{
			writer.WriteVarLong(lifetimeTotalWins);
			writer.WriteVarLong(lifetimeTotalLosses);
			return;
		}
		writer.WriteVarULong(syncVarDirtyBits);
		if ((syncVarDirtyBits & 1L) != 0L)
		{
			writer.WriteVarLong(lifetimeTotalWins);
		}
		if ((syncVarDirtyBits & 2L) != 0L)
		{
			writer.WriteVarLong(lifetimeTotalLosses);
		}
	}

	protected override void DeserializeSyncVars(NetworkReader reader, bool initialState)
	{
		base.DeserializeSyncVars(reader, initialState);
		if (initialState)
		{
			GeneratedSyncVarDeserialize(ref lifetimeTotalWins, null, reader.ReadVarLong());
			GeneratedSyncVarDeserialize(ref lifetimeTotalLosses, null, reader.ReadVarLong());
			return;
		}
		long num = (long)reader.ReadVarULong();
		if ((num & 1L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref lifetimeTotalWins, null, reader.ReadVarLong());
		}
		if ((num & 2L) != 0L)
		{
			GeneratedSyncVarDeserialize(ref lifetimeTotalLosses, null, reader.ReadVarLong());
		}
	}
}

}
