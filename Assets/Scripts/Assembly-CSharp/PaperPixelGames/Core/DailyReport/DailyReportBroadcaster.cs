using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityServiceLocator;

namespace PaperPixelGames.Core.DailyReport
{
	public sealed class DailyReportBroadcaster : NetworkBehaviour, ISaveable<DailyRecordData>
	{
		private sealed class PlayerDailyStats
		{
			public int Earnings;

			public int CustomersServed;

			public int SushiPrepared;

			public int FishCaught;

			public float DeepestDepthMeters;

			public int DrownCount;

			public BiggestCatchRuntime BiggestCatch;

			public bool FromSeaToTableCompleted;
		}

		private struct BiggestCatchRuntime
		{
			public bool HasValue;

			public string FishId;

			public float Kg;

			public string PlayerName;
		}

		[Header("MVP Tuning (Normalized Weights)")]
		[SerializeField]
		[Min(0f)]
		private float _mvpWFish = 0.25f;

		[SerializeField]
		[Min(0f)]
		private float _mvpWSushi = 0.2f;

		[SerializeField]
		[Min(0f)]
		private float _mvpWCustomers = 0.15f;

		[SerializeField]
		[Min(0f)]
		private float _mvpWAwards = 0.2f;

		[Header("Award Tuning")]
		[SerializeField]
		[Min(0f)]
		private float _minimumDiveAwardDepth = 1f;

		private const int WakeUpHour = 8;

		private readonly Dictionary<ulong, PlayerDailyStats> _statsBySteamId = new Dictionary<ulong, PlayerDailyStats>();

		private readonly List<AwardPayload> _awards = new List<AwardPayload>();

		private readonly Dictionary<ulong, int> _mvpScoreBySteamId = new Dictionary<ulong, int>();

		private int _totalCustomerCount;

		private int _totalFishCaught;

		private int _totalEarned;

		private float _deepestDepthMeters;

		private BiggestCatchRuntime _biggestCatch;

		private ulong _firstDrownSteamId = ulong.MaxValue;

		private MvpPayload _mvp;

		private DailyRecordData _dailyRecordData = new DailyRecordData();

		private void Awake()
		{
			ServiceLocator.ForSceneOf(this).Register(typeof(DailyReportBroadcaster), this);
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			if (base.IsServer)
			{
				if (Singleton<SaveManager>.Instance != null && Singleton<SaveManager>.Instance.TryGetCurrentSaveSlot(out var data))
				{
					Load(data.DailyRecord);
					return;
				}
				UtilityMethods.Log("[DailyReportBroadcaster] Save slot is unavailable. Best records were initialized with empty data.", LogType.Warning);
				Load(new DailyRecordData());
			}
		}

		public void ServerResetDailyReport()
		{
			if (base.IsServer)
			{
				StaffManager.Instance?.Ledger.Reset();
			}
			if (base.IsServer)
			{
				_totalCustomerCount = 0;
				_totalFishCaught = 0;
				_totalEarned = 0;
				_deepestDepthMeters = 0f;
				_biggestCatch = default;
				_firstDrownSteamId = ulong.MaxValue;
				_mvp = null;
				_statsBySteamId.Clear();
				_awards.Clear();
				_mvpScoreBySteamId.Clear();
				ClientResetDailyDiveTrackersRpc();
			}
		}

		public void ServerBuildPayloadAndPublish()
		{
			if (base.IsServer)
			{
				ServerCaptureFinalPlayerDepths();
				ServerEvaluateAwards();
				ServerEvaluateMvpScores();
				CommonReportPayload common = ServerBuildCommonPayload();
				ServerPublishReports(common);
			}
		}

		private CommonReportPayload ServerBuildCommonPayload()
		{
			int day = ServerGetCompletedReportDay();
			string restaurantName = ServerGetRestaurantName();
			ServerUpdateBestRecords(_deepestDepthMeters, _totalFishCaught, ServerCalculateTotalDrownCount(), _totalCustomerCount, _totalEarned);
			return new CommonReportPayload
			{
				Day = day,
				RestaurantName = restaurantName,
				TotalEarned = _totalEarned,
				TotalCustomerCount = _totalCustomerCount,
				TotalFishCaught = _totalFishCaught,
				DeepestDepthMeters = _deepestDepthMeters,
				BiggestCatch = ToPayload(_biggestCatch),
				MVP = _mvp
			};
		}

		private PersonalReportPayload ServerBuildPersonalPayload(PPG_Player player)
		{
			ulong value = player.SteamID.Value;
			PlayerDailyStats orCreateStats = GetOrCreateStats(value);
			List<ushort> list = new List<ushort>();
			for (int i = 0; i < _awards.Count; i++)
			{
				AwardPayload awardPayload = _awards[i];
				if (awardPayload.WinnerSteamId == value)
				{
					list.Add(awardPayload.AwardId);
				}
			}
			int value2;
			return new PersonalReportPayload
			{
				SteamId = value,
				PlayerName = player.PlayerName.Value.ToString(),
				Earnings = orCreateStats.Earnings,
				CustomersServed = orCreateStats.CustomersServed,
				SushiPrepared = orCreateStats.SushiPrepared,
				FishCaught = orCreateStats.FishCaught,
				BiggestCatch = ToPayload(orCreateStats.BiggestCatch),
				DeepestDepthMeters = orCreateStats.DeepestDepthMeters,
				DrownCount = orCreateStats.DrownCount,
				AwardIds = list,
				MvpScore = (_mvpScoreBySteamId.TryGetValue(value, out value2) ? value2 : 0)
			};
		}

		private void ServerPublishReports(CommonReportPayload common)
		{
			StaffManager instance = StaffManager.Instance;
			StaffReportLedger staffReportLedger = instance?.Ledger;
			common.StaffTeamCount = 0;
			if (instance != null)
			{
				for (int i = 0; i < instance.TeamCount; i++)
				{
					StaffEmployeeEntry teamMember = instance.GetTeamMember(i);
					staffReportLedger.Register(teamMember);
					if (teamMember.Departure == StaffDeparture.None)
					{
						common.StaffTeamCount++;
					}
				}
			}
			common.Staff = staffReportLedger?.Snapshot() ?? new List<StaffReportEntry>();
			common.StaffWagesStored = staffReportLedger?.TotalWages ?? 0;
			common.StaffUnpaidWagesStored = staffReportLedger?.TotalUnpaidWages ?? 0;
			common.StaffPurchasesStored = staffReportLedger?.TotalPurchases ?? 0;
			if (Singleton<PlayersManager>.Instance == null)
			{
				UtilityMethods.Log("[DailyReportBroadcaster] Report publish failed. PlayersManager is unavailable.", LogType.Error);
				return;
			}
			int num = 0;
			foreach (KeyValuePair<ulong, PPG_Player> player in Singleton<PlayersManager>.Instance.Players)
			{
				ulong key = player.Key;
				PPG_Player value = player.Value;
				if (value == null || !IsValidSteamId(value.SteamID.Value))
				{
					UtilityMethods.Log($"[DailyReportBroadcaster] Personal report skipped. ClientId={key}, Player or SteamId is invalid.", LogType.Warning);
					continue;
				}
				byte[] bytes = ReportCodec.ToBytes(new DailyLogReportPayload
				{
					Common = common,
					Personal = ServerBuildPersonalPayload(value)
				}, gzip: true);
				ClientReceiveDailyReportRpc(bytes, base.RpcTarget.Single(key, RpcTargetUse.Temp));
				num++;
			}
			UtilityMethods.Log($"[DailyReportBroadcaster] Daily reports published. Clients={num}, Day={common.Day}");
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientReceiveDailyReportRpc(byte[] bytes, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(658181545u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bool value = bytes != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(bytes, default(FastBufferWriter.ForPrimitives));
				}
				__endSendRpc(ref bufferWriter, 658181545u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				DailyLogReportPayload report = ReportCodec.FromBytes<DailyLogReportPayload>(bytes, gzip: true);
				if (Singleton<GameUIManager>.Instance == null || Singleton<GameUIManager>.Instance.DailyLogScreen == null)
				{
					UtilityMethods.Log("[DailyReportBroadcaster] Daily report received, but DailyLogScreen is unavailable.", LogType.Error);
				}
				else
				{
					Singleton<GameUIManager>.Instance.DailyLogScreen.BindReport(report);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientResetDailyDiveTrackersRpc(RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1305104990u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1305104990u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null)?.DivingEquipment?.ResetDailyMaxDepth();
			}
		}

		public void ServerHideLog()
		{
			if (base.IsServer)
			{
				ClientHideLogRpc();
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientHideLogRpc()
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(4047019126u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 4047019126u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (Singleton<GameUIManager>.Instance != null && Singleton<GameUIManager>.Instance.DailyLogScreen != null)
				{
					Singleton<GameUIManager>.Instance.DailyLogScreen.HideLog();
				}
				ServiceLocator.ForSceneOf(this).Get<Bed>(out var service);
				service?.HandleWakeUp();
			}
		}

		public void ServerRecordDiveDepth(ulong steamId, float depthMeters)
		{
			if (base.IsServer && !(depthMeters <= 0f))
			{
				if (!IsValidSteamId(steamId))
				{
					UtilityMethods.Log($"[DailyReportBroadcaster] Dive depth rejected. SteamId={steamId}, Depth={depthMeters:F2}", LogType.Warning);
					return;
				}
				_deepestDepthMeters = Mathf.Max(_deepestDepthMeters, depthMeters);
				PlayerDailyStats orCreateStats = GetOrCreateStats(steamId);
				orCreateStats.DeepestDepthMeters = Mathf.Max(orCreateStats.DeepestDepthMeters, depthMeters);
			}
		}

		public void ServerRecordCaught(string fishId, float weight, string playerName, ulong steamId)
		{
			if (!base.IsServer || string.IsNullOrEmpty(fishId))
			{
				return;
			}
			if (!IsValidSteamId(steamId))
			{
				UtilityMethods.Log($"[DailyReportBroadcaster] Fish catch rejected. SteamId={steamId}, FishId={fishId}", LogType.Warning);
				return;
			}
			float num = Mathf.Max(0f, weight);
			_totalFishCaught++;
			PlayerDailyStats orCreateStats = GetOrCreateStats(steamId);
			orCreateStats.FishCaught++;
			if (!orCreateStats.BiggestCatch.HasValue || num > orCreateStats.BiggestCatch.Kg)
			{
				orCreateStats.BiggestCatch = CreateBiggestCatch(fishId, num, playerName);
			}
			if (!_biggestCatch.HasValue || num > _biggestCatch.Kg)
			{
				_biggestCatch = CreateBiggestCatch(fishId, num, playerName);
			}
		}

		public void ServerRecordDrown(FixedString64Bytes playerName, ulong steamId)
		{
			if (!base.IsServer)
			{
				return;
			}
			if (!IsValidSteamId(steamId))
			{
				UtilityMethods.Log($"[DailyReportBroadcaster] Drown record rejected. SteamId={steamId}, PlayerName={playerName}", LogType.Warning);
				return;
			}
			GetOrCreateStats(steamId).DrownCount++;
			if (_firstDrownSteamId == ulong.MaxValue)
			{
				_firstDrownSteamId = steamId;
			}
		}

		public void ServerRecordStaffMoneyEarned(int amount)
		{
			if (base.IsServer && amount > 0)
			{
				_totalEarned += amount;
			}
		}

		public void ServerRecordMoneyEarned(ulong steamId, int amount)
		{
			if (base.IsServer && amount > 0)
			{
				_totalEarned += amount;
				if (!IsValidSteamId(steamId))
				{
					UtilityMethods.Log($"[DailyReportBroadcaster] Money was added to the common report without personal attribution. SteamId={steamId}, Amount={amount}", LogType.Warning);
				}
				else
				{
					GetOrCreateStats(steamId).Earnings += amount;
				}
			}
		}

		public void ServerRecordCustomersServed(int totalCustomerCount, IReadOnlyDictionary<ulong, int> customerCreditsBySteamId, int staffCustomerCount = 0)
		{
			if (!base.IsServer || totalCustomerCount <= 0)
			{
				return;
			}
			_totalCustomerCount += totalCustomerCount;
			if (customerCreditsBySteamId == null)
			{
				UtilityMethods.Log($"[DailyReportBroadcaster] Customers were added without personal attribution. Count={totalCustomerCount}", LogType.Warning);
				return;
			}
			int num = Mathf.Clamp(staffCustomerCount, 0, totalCustomerCount);
			foreach (KeyValuePair<ulong, int> item in customerCreditsBySteamId)
			{
				if (item.Value > 0)
				{
					if (!IsValidSteamId(item.Key))
					{
						UtilityMethods.Log($"[DailyReportBroadcaster] Customer attribution skipped. SteamId={item.Key}, Count={item.Value}", LogType.Warning);
						continue;
					}
					PlayerDailyStats orCreateStats = GetOrCreateStats(item.Key);
					orCreateStats.CustomersServed += item.Value;
					ServerTryUnlockFromSeaToTable(item.Key, orCreateStats);
					num += item.Value;
				}
			}
			if (num != totalCustomerCount)
			{
				UtilityMethods.Log($"[DailyReportBroadcaster] Customer attribution mismatch. Total={totalCustomerCount}, Attributed={num}", LogType.Warning);
			}
		}

		public void ServerRecordSushiMade(ulong steamId, int count = 1)
		{
			if (base.IsServer && count > 0)
			{
				if (!IsValidSteamId(steamId))
				{
					UtilityMethods.Log($"[DailyReportBroadcaster] Sushi record rejected. SteamId={steamId}, Count={count}", LogType.Warning);
				}
				else
				{
					GetOrCreateStats(steamId).SushiPrepared += count;
				}
			}
		}

		private void ServerCaptureFinalPlayerDepths()
		{
			if (Singleton<PlayersManager>.Instance == null)
			{
				return;
			}
			foreach (PPG_Player value2 in Singleton<PlayersManager>.Instance.Players.Values)
			{
				if (!(value2 == null) && !(value2.DivingEquipment == null))
				{
					ulong value = value2.SteamID.Value;
					float dailyMaxDepth = value2.DivingEquipment.DailyMaxDepth;
					if (IsValidSteamId(value) && dailyMaxDepth > 0f)
					{
						ServerRecordDiveDepth(value, dailyMaxDepth);
					}
					value2.DivingEquipment.ResetDailyMaxDepth();
				}
			}
		}

		private void ServerEvaluateAwards()
		{
			_awards.Clear();
			ulong winnerSteamId = PickMax((PlayerDailyStats stats) => stats.DeepestDepthMeters, (float value) => value > _minimumDiveAwardDepth);
			ulong winnerSteamId2 = PickMax((PlayerDailyStats stats) => stats.SushiPrepared, (int value) => value > 0);
			ulong winnerSteamId3 = PickMax((PlayerDailyStats stats) => stats.CustomersServed, (int value) => value > 0);
			ulong winnerSteamId4 = PickMax((PlayerDailyStats stats) => stats.FishCaught, (int value) => value > 0);
			AddAward(DailyAwardId.DeepestDiver, winnerSteamId);
			AddAward(DailyAwardId.FirstDrown, _firstDrownSteamId);
			AddAward(DailyAwardId.MasterChef, winnerSteamId2);
			AddAward(DailyAwardId.SpeedyWaitress, winnerSteamId3);
			AddAward(DailyAwardId.FishHunter, winnerSteamId4);
		}

		private void ServerEvaluateMvpScores()
		{
			_mvpScoreBySteamId.Clear();
			_mvp = null;
			HashSet<ulong> hashSet = new HashSet<ulong>();
			foreach (ulong key in _statsBySteamId.Keys)
			{
				if (IsValidSteamId(key))
				{
					hashSet.Add(key);
				}
			}
			if (Singleton<PlayersManager>.Instance != null)
			{
				foreach (PPG_Player value4 in Singleton<PlayersManager>.Instance.Players.Values)
				{
					if (value4 != null && IsValidSteamId(value4.SteamID.Value))
					{
						hashSet.Add(value4.SteamID.Value);
					}
				}
			}
			Dictionary<ulong, int> dictionary = new Dictionary<ulong, int>();
			for (int i = 0; i < _awards.Count; i++)
			{
				ulong winnerSteamId = _awards[i].WinnerSteamId;
				dictionary[winnerSteamId] = ((!dictionary.TryGetValue(winnerSteamId, out var value)) ? 1 : (value + 1));
			}
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			foreach (ulong item in hashSet)
			{
				PlayerDailyStats orCreateStats = GetOrCreateStats(item);
				num = Mathf.Max(num, orCreateStats.FishCaught);
				num2 = Mathf.Max(num2, orCreateStats.SushiPrepared);
				num3 = Mathf.Max(num3, orCreateStats.CustomersServed);
				num4 = Mathf.Max(num4, dictionary.TryGetValue(item, out var value2) ? value2 : 0);
			}
			if (num <= 0 && num2 <= 0 && num3 <= 0 && num4 <= 0)
			{
				foreach (ulong item2 in hashSet)
				{
					_mvpScoreBySteamId[item2] = 0;
				}
				return;
			}
			float num5 = _mvpWFish + _mvpWSushi + _mvpWCustomers + _mvpWAwards;
			if (num5 <= Mathf.Epsilon)
			{
				UtilityMethods.Log("[DailyReportBroadcaster] MVP score weights sum to zero. All scores were set to 0.", LogType.Warning);
				{
					foreach (ulong item3 in hashSet)
					{
						_mvpScoreBySteamId[item3] = 0;
					}
					return;
				}
			}
			int num6 = int.MinValue;
			int num7 = -1;
			float num8 = -1f;
			ulong num9 = ulong.MaxValue;
			foreach (ulong item4 in hashSet)
			{
				PlayerDailyStats orCreateStats2 = GetOrCreateStats(item4);
				int num10 = (dictionary.TryGetValue(item4, out var value3) ? value3 : 0);
				int num11 = Mathf.Clamp(Mathf.RoundToInt((_mvpWFish * Normalize(orCreateStats2.FishCaught, num) + _mvpWSushi * Normalize(orCreateStats2.SushiPrepared, num2) + _mvpWCustomers * Normalize(orCreateStats2.CustomersServed, num3) + _mvpWAwards * Normalize(num10, num4)) / num5 * 100f), 0, 100);
				_mvpScoreBySteamId[item4] = num11;
				if (num11 > num6 || (num11 == num6 && num10 > num7) || (num11 == num6 && num10 == num7 && orCreateStats2.DeepestDepthMeters > num8) || (num11 == num6 && num10 == num7 && Mathf.Approximately(orCreateStats2.DeepestDepthMeters, num8) && item4 < num9))
				{
					num6 = num11;
					num7 = num10;
					num8 = orCreateStats2.DeepestDepthMeters;
					num9 = item4;
				}
			}
			if (IsValidSteamId(num9))
			{
				string playerName = "-";
				Singleton<PlayersManager>.Instance?.TryGetPlayerNameBySteamId(num9, out playerName);
				_mvp = new MvpPayload
				{
					SteamId = num9,
					PlayerName = (string.IsNullOrWhiteSpace(playerName) ? "-" : playerName),
					Score = num6
				};
				ServerUnlockAchievementForSteamId(num9, "ACH_EMPLOYEE_OF_THE_DAY", "EmployeeOfTheDay");
			}
		}

		private void ServerTryUnlockFromSeaToTable(ulong steamId, PlayerDailyStats stats)
		{
			if (base.IsServer && stats != null && !stats.FromSeaToTableCompleted && stats.FishCaught > 0 && stats.SushiPrepared > 0 && stats.CustomersServed > 0)
			{
				stats.FromSeaToTableCompleted = ServerUnlockAchievementForSteamId(steamId, "ACH_FROM_SEA_TO_TABLE", "FromSeaToTable");
			}
		}

		private bool ServerUnlockAchievementForSteamId(ulong steamId, string achievementApiName, string context)
		{
			if (!base.IsServer || !IsValidSteamId(steamId))
			{
				return false;
			}
			if (Singleton<PlayersManager>.Instance == null || !Singleton<PlayersManager>.Instance.TryGetPlayerBySteamId(steamId, out var player) || player == null || !player.IsSpawned)
			{
				UtilityMethods.Log($"[DailyReportBroadcaster] Achievement target is unavailable. Context={context}, SteamId={steamId}", LogType.Warning);
				return false;
			}
			RpcRelay rpcRelay = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RpcRelay : null);
			if (rpcRelay == null)
			{
				UtilityMethods.Log($"[DailyReportBroadcaster] Achievement relay is unavailable. Context={context}, SteamId={steamId}", LogType.Warning);
				return false;
			}
			rpcRelay.ServerUnlockSteamAchievementForClient(achievementApiName, player.OwnerClientId);
			return true;
		}

		private ulong PickMax<TValue>(Func<PlayerDailyStats, TValue> selector, Func<TValue, bool> isEligible) where TValue : IComparable<TValue>
		{
			bool flag = false;
			TValue other = default;
			ulong num = ulong.MaxValue;
			foreach (KeyValuePair<ulong, PlayerDailyStats> item in _statsBySteamId)
			{
				TValue val = selector(item.Value);
				if (isEligible(val) && (!flag || val.CompareTo(other) > 0 || (val.CompareTo(other) == 0 && item.Key < num)))
				{
					flag = true;
					other = val;
					num = item.Key;
				}
			}
			if (!flag)
			{
				return ulong.MaxValue;
			}
			return num;
		}

		private void AddAward(DailyAwardId id, ulong winnerSteamId)
		{
			if (IsValidSteamId(winnerSteamId))
			{
				_awards.Add(new AwardPayload
				{
					AwardId = (ushort)id,
					WinnerSteamId = winnerSteamId
				});
			}
		}

		private PlayerDailyStats GetOrCreateStats(ulong steamId)
		{
			if (_statsBySteamId.TryGetValue(steamId, out var value))
			{
				return value;
			}
			value = new PlayerDailyStats();
			_statsBySteamId.Add(steamId, value);
			return value;
		}

		private static BiggestCatchRuntime CreateBiggestCatch(string fishId, float weight, string playerName)
		{
			return new BiggestCatchRuntime
			{
				HasValue = true,
				FishId = fishId,
				Kg = weight,
				PlayerName = (string.IsNullOrWhiteSpace(playerName) ? "-" : playerName)
			};
		}

		private static BiggestCatchPayload ToPayload(BiggestCatchRuntime biggestCatch)
		{
			if (!biggestCatch.HasValue)
			{
				return null;
			}
			return new BiggestCatchPayload
			{
				FishId = biggestCatch.FishId,
				Kg = biggestCatch.Kg,
				PlayerName = biggestCatch.PlayerName
			};
		}

		private static float Normalize(int value, int maximum)
		{
			if (maximum <= 0)
			{
				return 0f;
			}
			return (float)value / (float)maximum;
		}

		private static bool IsValidSteamId(ulong steamId)
		{
			if (steamId != 0L)
			{
				return steamId != ulong.MaxValue;
			}
			return false;
		}

		private static int ServerGetCompletedReportDay()
		{
			int num = 1;
			int num2 = 8;
			SaveSlotData data;
			if (Singleton<GameProgressManager>.Instance != null && Singleton<GameProgressManager>.Instance.CozyWeatherManager != null)
			{
				CozyWeatherManager cozyWeatherManager = Singleton<GameProgressManager>.Instance.CozyWeatherManager;
				num = cozyWeatherManager.CurrentDay;
				num2 = cozyWeatherManager.CurrentTime.hours;
			}
			else if (Singleton<SaveManager>.Instance != null && Singleton<SaveManager>.Instance.TryGetCurrentSaveSlot(out data) && data.GameProgress != null)
			{
				num = data.GameProgress.CurrentDay;
			}
			if (num2 < 8)
			{
				num--;
			}
			return Mathf.Max(1, num);
		}

		private static string ServerGetRestaurantName()
		{
			if (Singleton<SaveManager>.Instance != null && Singleton<SaveManager>.Instance.TryGetCurrentSaveSlot(out var data) && data.RestaurantData?.RestaurantSignCustomizationData != null)
			{
				return data.RestaurantData.RestaurantSignCustomizationData.RestaurantName;
			}
			UtilityMethods.Log("[DailyReportBroadcaster] Restaurant name is unavailable while building the report.", LogType.Warning);
			return string.Empty;
		}

		private int ServerCalculateTotalDrownCount()
		{
			int num = 0;
			foreach (PlayerDailyStats value in _statsBySteamId.Values)
			{
				num += Mathf.Max(0, value.DrownCount);
			}
			return num;
		}

		public DailyRecordData Save()
		{
			return new DailyRecordData
			{
				MostDiveDepth = (_dailyRecordData?.MostDiveDepth ?? 0f),
				MostFishCaught = (_dailyRecordData?.MostFishCaught ?? 0),
				MostDrown = (_dailyRecordData?.MostDrown ?? 0),
				MostCustomerCount = (_dailyRecordData?.MostCustomerCount ?? 0),
				MostServedDishCount = (_dailyRecordData?.MostServedDishCount ?? 0),
				MostTotalEarned = (_dailyRecordData?.MostTotalEarned ?? 0)
			};
		}

		public void Load(DailyRecordData data)
		{
			_dailyRecordData = data ?? new DailyRecordData();
		}

		private void ServerUpdateBestRecords(float deepestDepthMeters, int fishCaught, int drownCount, int customerCount, int totalEarned)
		{
			if (_dailyRecordData == null)
			{
				_dailyRecordData = new DailyRecordData();
			}
			_dailyRecordData.MostDiveDepth = Mathf.Max(_dailyRecordData.MostDiveDepth, deepestDepthMeters);
			_dailyRecordData.MostFishCaught = Mathf.Max(_dailyRecordData.MostFishCaught, fishCaught);
			_dailyRecordData.MostDrown = Mathf.Max(_dailyRecordData.MostDrown, drownCount);
			_dailyRecordData.MostCustomerCount = Mathf.Max(_dailyRecordData.MostCustomerCount, customerCount);
			_dailyRecordData.MostTotalEarned = Mathf.Max(_dailyRecordData.MostTotalEarned, totalEarned);
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(658181545u, __rpc_handler_658181545, "ClientReceiveDailyReportRpc", RpcInvokePermission.Everyone);
			__registerRpc(1305104990u, __rpc_handler_1305104990, "ClientResetDailyDiveTrackersRpc", RpcInvokePermission.Everyone);
			__registerRpc(4047019126u, __rpc_handler_4047019126, "ClientHideLogRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_658181545(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				byte[] value2 = null;
				if (value)
				{
					reader.ReadValueSafe(out value2, default(FastBufferWriter.ForPrimitives));
				}
				RpcParams ext = rpcParams.Ext;
				((DailyReportBroadcaster)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DailyReportBroadcaster)target).ClientReceiveDailyReportRpc(value2, ext);
				((DailyReportBroadcaster)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1305104990(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((DailyReportBroadcaster)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DailyReportBroadcaster)target).ClientResetDailyDiveTrackersRpc(ext);
				((DailyReportBroadcaster)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4047019126(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((DailyReportBroadcaster)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DailyReportBroadcaster)target).ClientHideLogRpc();
				((DailyReportBroadcaster)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "DailyReportBroadcaster";
		}
	}
}
