using System;
using System.Collections;
using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Progress;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Progression
{
	public class ProgressionManager : NetworkBehaviour
	{
		[SerializeField]
		private ProgressionLevelDataSO _levelData;

		[SerializeField]
		private ProgressionXpRewardTableSO _xpRewardTable;

		[SerializeField]
		private AudioClipSO _xpGainSfx;

		[SerializeField]
		private bool _showToasts = true;

		[SerializeField]
		[Min(0.1f)]
		private float _levelUpToastDuration = 5f;

		private readonly NetworkVariable<ProgressionNetData> _progressionData = new NetworkVariable<ProgressionNetData>(new ProgressionNetData
		{
			SyncRevision = 1,
			RestaurantLevel = 1,
			CurrentXp = 0.0,
			TotalXp = 0.0
		});

		private readonly HashSet<ProgressionUnlockKey> _unlockedKeys = new HashSet<ProgressionUnlockKey>();

		private readonly HashSet<int> _unlockedDiningTableIds = new HashSet<int>();

		private readonly HashSet<int> _unlockedConveyorTableIds = new HashSet<int>();

		private readonly HashSet<string> _unlockedWholesaleIds = new HashSet<string>(StringComparer.Ordinal);

		private readonly HashSet<ProgressionUnlockKey> _questGroupUnlockKeys = new HashSet<ProgressionUnlockKey>();

		private readonly HashSet<string> _questGroupWholesaleUnlockIds = new HashSet<string>(StringComparer.Ordinal);

		private readonly Dictionary<string, string> _questGroupWholesaleDisplayNames = new Dictionary<string, string>(StringComparer.Ordinal);

		private bool _hasLoadedData;

		private int _pendingStaffXpFeedback;

		private Coroutine _staffXpFeedbackRoutine;

		public int CurrentLevel => Mathf.Max(1, _progressionData.Value.RestaurantLevel);

		public double CurrentXp => Math.Max(0.0, _progressionData.Value.CurrentXp);

		public double TotalXp => Math.Max(0.0, _progressionData.Value.TotalXp);

		public double CurrentLevelRequiredTotalXp
		{
			get
			{
				if (!(_levelData != null))
				{
					return Math.Max(0.0, TotalXp - CurrentXp);
				}
				return _levelData.GetRequiredTotalXpForLevel(CurrentLevel);
			}
		}

		public double NextLevelRequiredTotalXp
		{
			get
			{
				if (!(_levelData != null))
				{
					return -1.0;
				}
				return _levelData.GetRequiredTotalXpForNextLevel(CurrentLevel);
			}
		}

		public double NextLevelRequiredXp
		{
			get
			{
				double nextLevelRequiredTotalXp = NextLevelRequiredTotalXp;
				if (nextLevelRequiredTotalXp < 0.0)
				{
					return -1.0;
				}
				return Math.Max(1.0, nextLevelRequiredTotalXp - CurrentLevelRequiredTotalXp);
			}
		}

		public event Action<int, double, double> ProgressionChanged;

		public event Action<int, int, double, double, int, double, double> XpFeedbackRequested;

		public event Action<ProgressionUnlockKey> UnlockGranted;

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<ProgressionNetData> progressionData = _progressionData;
			progressionData.OnValueChanged = (NetworkVariable<ProgressionNetData>.OnValueChangedDelegate)Delegate.Combine(progressionData.OnValueChanged, new NetworkVariable<ProgressionNetData>.OnValueChangedDelegate(OnProgressionDataChanged));
			if (base.IsServer && !_hasLoadedData)
			{
				ApplyProgressionData(1, 0.0, 0.0, notify: false);
			}
			RebuildUnlockSet();
			RaiseProgressionChanged();
		}

		public override void OnNetworkDespawn()
		{
			if (_staffXpFeedbackRoutine != null)
			{
				StopCoroutine(_staffXpFeedbackRoutine);
			}
			_staffXpFeedbackRoutine = null;
			_pendingStaffXpFeedback = 0;
			base.OnNetworkDespawn();
			NetworkVariable<ProgressionNetData> progressionData = _progressionData;
			progressionData.OnValueChanged = (NetworkVariable<ProgressionNetData>.OnValueChangedDelegate)Delegate.Remove(progressionData.OnValueChanged, new NetworkVariable<ProgressionNetData>.OnValueChangedDelegate(OnProgressionDataChanged));
		}

		public bool IsUnlocked(ProgressionUnlockKey key)
		{
			if (key == ProgressionUnlockKey.None)
			{
				return true;
			}
			if (_unlockedKeys.Count == 0)
			{
				RebuildUnlockSet();
			}
			return _unlockedKeys.Contains(key);
		}

		public bool IsDiningTableUnlocked(int tableId)
		{
			if (tableId < 0)
			{
				return false;
			}
			if (_unlockedDiningTableIds.Count == 0)
			{
				RebuildUnlockSet();
			}
			return _unlockedDiningTableIds.Contains(tableId);
		}

		public bool IsConveyorTableUnlocked(int tableId)
		{
			if (tableId < 0)
			{
				return false;
			}
			if (_unlockedConveyorTableIds.Count == 0)
			{
				RebuildUnlockSet();
			}
			return _unlockedConveyorTableIds.Contains(tableId);
		}

		public int GetAllowedDiningTableUnlockCount()
		{
			if (!(_levelData != null))
			{
				return int.MaxValue;
			}
			return _levelData.GetAllowedDiningTableUnlockCount(CurrentLevel);
		}

		public bool CanUnlockMoreDiningTables(int currentlyUnlockedCount)
		{
			return currentlyUnlockedCount < GetAllowedDiningTableUnlockCount();
		}

		public bool IsServedDishUnlocked(ServedDishSO servedDish)
		{
			return servedDish != null;
		}

		public bool IsWholesaleUnlocked(WholesaleSO wholesale)
		{
			if (wholesale == null || string.IsNullOrWhiteSpace(wholesale.Id))
			{
				return false;
			}
			if (_unlockedWholesaleIds.Count == 0)
			{
				RebuildUnlockSet();
			}
			return _unlockedWholesaleIds.Contains(wholesale.Id);
		}

		public int GetRequiredLevelForUnlock(ProgressionUnlockKey key)
		{
			if (!(_levelData != null))
			{
				return -1;
			}
			return _levelData.GetRequiredLevelForUnlock(key);
		}

		public int GetRequiredLevelForDiningTableUnlock(int tableId)
		{
			if (!(_levelData != null))
			{
				return -1;
			}
			return _levelData.GetRequiredLevelForDiningTableUnlock(tableId);
		}

		public int GetRequiredLevelForNextDiningTableUnlock(int currentlyUnlockedCount)
		{
			int targetUnlockedCount = Mathf.Max(0, currentlyUnlockedCount) + 1;
			if (!(_levelData != null))
			{
				return -1;
			}
			return _levelData.GetRequiredLevelForDiningTableUnlockCount(targetUnlockedCount);
		}

		public int GetRequiredLevelForConveyorTableUnlock(int tableId)
		{
			if (!(_levelData != null))
			{
				return -1;
			}
			return _levelData.GetRequiredLevelForConveyorTableUnlock(tableId);
		}

		public int GetRequiredLevelForWholesaleUnlock(WholesaleSO wholesale)
		{
			if (!(wholesale != null) || !(_levelData != null))
			{
				return -1;
			}
			return _levelData.GetRequiredLevelForWholesaleUnlock(wholesale.Id);
		}

		public void LoadFromSave(GameProgressData data)
		{
			if (base.IsServer || !base.IsSpawned)
			{
				int num = ((data == null || data.RestaurantLevel <= 0) ? 1 : data.RestaurantLevel);
				double totalXp = ((data != null) ? Math.Max(0.0, data.TotalXp) : 0.0);
				double currentXp = ((data != null) ? Math.Max(0.0, data.CurrentXp) : 0.0);
				if (_levelData != null)
				{
					num = Mathf.Max(num, _levelData.GetLevelForTotalXp(totalXp));
					currentXp = CalculateCurrentLevelXp(num, totalXp);
				}
				_hasLoadedData = true;
				ApplyProgressionData(num, currentXp, totalXp, notify: false);
			}
		}

		public void WriteToSave(GameProgressData data)
		{
			if (data != null)
			{
				data.RestaurantLevel = CurrentLevel;
				data.CurrentXp = CurrentXp;
				data.TotalXp = TotalXp;
			}
		}

		public void RefreshQuestGroupCompletionUnlockRewards(IEnumerable<QuestGroupSO> completedGroups, bool showNewUnlockToasts = false)
		{
			RebuildUnlockSet();
			HashSet<ProgressionUnlockKey> previousUnlocks = new HashSet<ProgressionUnlockKey>(_unlockedKeys);
			HashSet<string> previousWholesaleUnlocks = new HashSet<string>(_unlockedWholesaleIds, StringComparer.Ordinal);
			_questGroupUnlockKeys.Clear();
			_questGroupWholesaleUnlockIds.Clear();
			_questGroupWholesaleDisplayNames.Clear();
			if (completedGroups != null)
			{
				foreach (QuestGroupSO completedGroup in completedGroups)
				{
					if (!(completedGroup == null))
					{
						AddQuestGroupUnlockRewards(completedGroup);
					}
				}
			}
			RebuildUnlockSet();
			RaiseProgressionChanged();
			if (showNewUnlockToasts)
			{
				NotifyNewUnlocks(previousUnlocks, previousWholesaleUnlocks);
			}
		}

		public bool ServerGrantXp(ProgressionXpSource source, string contextId = null, ulong? feedbackClientId = null)
		{
			if (!base.IsServer)
			{
				UtilityMethods.Log($"[Progression] XP rejected. Only server can grant XP. Source={source}", LogType.Warning);
				return false;
			}
			int amount = ((_xpRewardTable != null) ? _xpRewardTable.GetReward(source, contextId) : 0);
			return ServerGrantXp(source, amount, contextId, feedbackClientId);
		}

		public bool ServerGrantMoneySpentXp(int spentDollars, string contextId = null, ulong? feedbackClientId = null)
		{
			if (!base.IsServer)
			{
				UtilityMethods.Log("[Progression] Money spent XP rejected. Only server can grant XP. ContextId=" + contextId, LogType.Warning);
				return false;
			}
			int amount = ((_xpRewardTable != null) ? _xpRewardTable.GetMoneySpentReward(spentDollars) : 0);
			return ServerGrantXp(ProgressionXpSource.MoneySpent, amount, contextId, feedbackClientId);
		}

		public bool ServerGrantStaffXp(ProgressionXpSource source, string contextId = null)
		{
			double totalXp = TotalXp;
			bool num = ServerGrantXp(source, contextId);
			if (num)
			{
				ServerQueueStaffXpFeedback(TotalXp - totalXp);
			}
			return num;
		}

		public bool ServerGrantStaffMoneySpentXp(int spentDollars, string contextId = null)
		{
			double totalXp = TotalXp;
			bool num = ServerGrantMoneySpentXp(spentDollars, contextId);
			if (num)
			{
				ServerQueueStaffXpFeedback(TotalXp - totalXp);
			}
			return num;
		}

		private void ServerQueueStaffXpFeedback(double gainedXp)
		{
			if (base.IsServer && base.IsSpawned && !(gainedXp <= 0.0))
			{
				_pendingStaffXpFeedback += Mathf.RoundToInt((float)gainedXp);
				if (_staffXpFeedbackRoutine == null)
				{
					_staffXpFeedbackRoutine = StartCoroutine(ServerFlushStaffXpFeedback());
				}
			}
		}

		private IEnumerator ServerFlushStaffXpFeedback()
		{
			yield return new WaitForSecondsRealtime(2f);
			int pendingStaffXpFeedback = _pendingStaffXpFeedback;
			_pendingStaffXpFeedback = 0;
			_staffXpFeedbackRoutine = null;
			if (base.IsServer && base.IsSpawned && pendingStaffXpFeedback > 0)
			{
				ClientShowStaffXpFeedbackRpc(pendingStaffXpFeedback);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientShowStaffXpFeedbackRpc(int amount)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3147589679u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, amount);
				__endSendRpc(ref bufferWriter, 3147589679u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Singleton<NotifyManager>.Instance?.ShowXpGain(amount);
			}
		}

		public bool ServerGrantXp(ProgressionXpSource source, int amount, string contextId = null, ulong? feedbackClientId = null)
		{
			if (!base.IsServer)
			{
				UtilityMethods.Log($"[Progression] XP rejected. Only server can grant XP. Source={source}", LogType.Warning);
				return false;
			}
			if (amount <= 0)
			{
				return false;
			}
			ProgressionNetData value = _progressionData.Value;
			int num = Mathf.Max(1, value.RestaurantLevel);
			double previousCurrentXp = Math.Max(0.0, value.CurrentXp);
			double previousNextLevelRequiredXp = CalculateNextLevelRequiredXp(num);
			double num2 = Math.Max(0.0, value.TotalXp + (double)amount);
			int num3 = ((_levelData != null) ? _levelData.GetLevelForTotalXp(num2) : Mathf.Max(1, value.RestaurantLevel));
			double currentXp = CalculateCurrentLevelXp(num3, num2);
			double nextLevelRequiredXp = CalculateNextLevelRequiredXp(num3);
			UtilityMethods.Log($"[Progression] XP granted.\n   Source: {source}\n   ContextId: {contextId}\n   Amount: {amount}\n   TotalXp: {value.TotalXp} -> {num2}\n   Level: {value.RestaurantLevel} -> {num3}");
			ApplyProgressionData(num3, currentXp, num2, notify: true);
			if (feedbackClientId.HasValue)
			{
				ClientRequestXpFeedbackRpc(amount, num, previousCurrentXp, previousNextLevelRequiredXp, num3, currentXp, nextLevelRequiredXp, base.RpcTarget.Single(feedbackClientId.Value, RpcTargetUse.Temp));
			}
			return true;
		}

		private void ApplyProgressionData(int level, double currentXp, double totalXp, bool notify)
		{
			ProgressionNetData value = _progressionData.Value;
			ProgressionNetData value2 = new ProgressionNetData
			{
				SyncRevision = (ushort)(value.SyncRevision + 1),
				RestaurantLevel = Mathf.Max(1, level),
				CurrentXp = Math.Max(0.0, currentXp),
				TotalXp = Math.Max(0.0, totalXp)
			};
			_progressionData.Value = value2;
			if (!base.IsSpawned || !notify)
			{
				RebuildUnlockSet();
				RaiseProgressionChanged();
			}
		}

		private double CalculateCurrentLevelXp(int level, double totalXp)
		{
			if (!(_levelData == null))
			{
				return Math.Max(0.0, totalXp - _levelData.GetRequiredTotalXpForLevel(level));
			}
			return totalXp;
		}

		private double CalculateNextLevelRequiredXp(int level)
		{
			if (_levelData == null)
			{
				return -1.0;
			}
			double requiredTotalXpForNextLevel = _levelData.GetRequiredTotalXpForNextLevel(level);
			if (requiredTotalXpForNextLevel < 0.0)
			{
				return -1.0;
			}
			return Math.Max(1.0, requiredTotalXpForNextLevel - _levelData.GetRequiredTotalXpForLevel(level));
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientRequestXpFeedbackRpc(int amount, int previousLevel, double previousCurrentXp, double previousNextLevelRequiredXp, int level, double currentXp, double nextLevelRequiredXp, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1336427634u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, amount);
				BytePacker.WriteValueBitPacked(bufferWriter, previousLevel);
				bufferWriter.WriteValueSafe(in previousCurrentXp, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in previousNextLevelRequiredXp, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, level);
				bufferWriter.WriteValueSafe(in currentXp, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in nextLevelRequiredXp, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1336427634u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				XpFeedbackRequested?.Invoke(amount, previousLevel, previousCurrentXp, previousNextLevelRequiredXp, level, currentXp, nextLevelRequiredXp);
			}
		}

		public void PlayXpGainSfxLocal()
		{
			if (!(Singleton<SoundManager>.Instance == null) && !(_xpGainSfx == null))
			{
				Singleton<SoundManager>.Instance.PlaySFX(_xpGainSfx);
			}
		}

		private void OnProgressionDataChanged(ProgressionNetData previous, ProgressionNetData current)
		{
			HashSet<ProgressionUnlockKey> hashSet = new HashSet<ProgressionUnlockKey>(_unlockedKeys);
			HashSet<int> hashSet2 = new HashSet<int>(_unlockedConveyorTableIds);
			HashSet<string> hashSet3 = new HashSet<string>(_unlockedWholesaleIds, StringComparer.Ordinal);
			int num = ((_levelData != null) ? _levelData.GetAllowedDiningTableUnlockCount(Mathf.Max(1, previous.RestaurantLevel)) : 0);
			RebuildUnlockSet();
			RaiseProgressionChanged();
			_ = current.RestaurantLevel;
			_ = previous.RestaurantLevel;
			if (_showToasts && current.RestaurantLevel > previous.RestaurantLevel && Singleton<NotifyManager>.Instance != null)
			{
				Singleton<NotifyManager>.Instance.ShowToast(ProgressionTooltipLocalization.GetToast("notification.restaurant_level", "Restaurant Level {0}", current.RestaurantLevel), NotificationState.Success, _levelUpToastDuration);
			}
			foreach (ProgressionUnlockKey unlockedKey in _unlockedKeys)
			{
				if (!hashSet.Contains(unlockedKey))
				{
					UnlockGranted?.Invoke(unlockedKey);
					if (_showToasts && Singleton<NotifyManager>.Instance != null)
					{
						Singleton<NotifyManager>.Instance.ShowToast(ProgressionTooltipLocalization.GetToast("notification.unlocked", "Unlocked: {0}", GetUnlockDisplayName(unlockedKey)), NotificationState.Success, _levelUpToastDuration);
					}
				}
			}
			if (!_showToasts || !(Singleton<NotifyManager>.Instance != null))
			{
				return;
			}
			int num2 = ((_levelData != null) ? GetAllowedDiningTableUnlockCount() : num);
			for (int i = num; i < num2; i++)
			{
				Singleton<NotifyManager>.Instance.ShowToast(ProgressionTooltipLocalization.GetToast("notification.dining_slot_unlocked", "Dining table unlock slot unlocked"), NotificationState.Success, _levelUpToastDuration);
			}
			foreach (int unlockedConveyorTableId in _unlockedConveyorTableIds)
			{
				if (!hashSet2.Contains(unlockedConveyorTableId))
				{
					Singleton<NotifyManager>.Instance.ShowToast(ProgressionTooltipLocalization.GetToast("notification.item_unlocked", "{0} unlocked", GetConveyorTableDisplayName(unlockedConveyorTableId)), NotificationState.Success, _levelUpToastDuration);
				}
			}
			foreach (string unlockedWholesaleId in _unlockedWholesaleIds)
			{
				if (!hashSet3.Contains(unlockedWholesaleId))
				{
					Singleton<NotifyManager>.Instance.ShowToast(ProgressionTooltipLocalization.GetToast("notification.market_item_unlocked", "Market item unlocked: {0}", GetWholesaleDisplayName(unlockedWholesaleId)), NotificationState.Success, _levelUpToastDuration);
				}
			}
		}

		private void RebuildUnlockSet()
		{
			_unlockedKeys.Clear();
			_unlockedDiningTableIds.Clear();
			_unlockedConveyorTableIds.Clear();
			_unlockedWholesaleIds.Clear();
			if (_levelData != null)
			{
				foreach (ProgressionUnlockKey item in _levelData.BuildUnlockSet(CurrentLevel))
				{
					_unlockedKeys.Add(item);
				}
				foreach (int item2 in _levelData.BuildDiningTableUnlockSet(CurrentLevel))
				{
					_unlockedDiningTableIds.Add(item2);
				}
				foreach (int item3 in _levelData.BuildConveyorTableUnlockSet(CurrentLevel))
				{
					_unlockedConveyorTableIds.Add(item3);
				}
				foreach (string item4 in _levelData.BuildWholesaleUnlockSet(CurrentLevel))
				{
					_unlockedWholesaleIds.Add(item4);
				}
			}
			foreach (ProgressionUnlockKey questGroupUnlockKey in _questGroupUnlockKeys)
			{
				_unlockedKeys.Add(questGroupUnlockKey);
			}
			foreach (string questGroupWholesaleUnlockId in _questGroupWholesaleUnlockIds)
			{
				_unlockedWholesaleIds.Add(questGroupWholesaleUnlockId);
			}
		}

		private void AddQuestGroupUnlockRewards(QuestGroupSO group)
		{
			if (group.CompletionUnlocks != null)
			{
				foreach (ProgressionUnlockKey completionUnlock in group.CompletionUnlocks)
				{
					if (completionUnlock != ProgressionUnlockKey.None)
					{
						_questGroupUnlockKeys.Add(completionUnlock);
					}
				}
			}
			if (group.CompletionWholesaleUnlocks == null)
			{
				return;
			}
			foreach (WholesaleSO completionWholesaleUnlock in group.CompletionWholesaleUnlocks)
			{
				if (!(completionWholesaleUnlock == null) && !string.IsNullOrWhiteSpace(completionWholesaleUnlock.Id))
				{
					_questGroupWholesaleUnlockIds.Add(completionWholesaleUnlock.Id);
					if (!_questGroupWholesaleDisplayNames.ContainsKey(completionWholesaleUnlock.Id))
					{
						_questGroupWholesaleDisplayNames.Add(completionWholesaleUnlock.Id, GetItemDisplayName(completionWholesaleUnlock, completionWholesaleUnlock.Id));
					}
				}
			}
		}

		private void NotifyNewUnlocks(HashSet<ProgressionUnlockKey> previousUnlocks, HashSet<string> previousWholesaleUnlocks)
		{
			foreach (ProgressionUnlockKey unlockedKey in _unlockedKeys)
			{
				if (!previousUnlocks.Contains(unlockedKey))
				{
					UnlockGranted?.Invoke(unlockedKey);
					if (_showToasts && Singleton<NotifyManager>.Instance != null)
					{
						Singleton<NotifyManager>.Instance.ShowToast(ProgressionTooltipLocalization.GetToast("notification.unlocked", "Unlocked: {0}", GetUnlockDisplayName(unlockedKey)), NotificationState.Success);
					}
				}
			}
			if (!_showToasts || Singleton<NotifyManager>.Instance == null)
			{
				return;
			}
			foreach (string unlockedWholesaleId in _unlockedWholesaleIds)
			{
				if (!previousWholesaleUnlocks.Contains(unlockedWholesaleId))
				{
					Singleton<NotifyManager>.Instance.ShowToast(ProgressionTooltipLocalization.GetToast("notification.market_item_unlocked", "Market item unlocked: {0}", GetWholesaleDisplayName(unlockedWholesaleId)), NotificationState.Success);
				}
			}
		}

		private string GetUnlockDisplayName(ProgressionUnlockKey key)
		{
			string fallback = ((_levelData != null) ? _levelData.GetUnlockDisplayName(key) : key.ToString());
			return ProgressionTooltipLocalization.GetUnlockDisplayName(key, fallback);
		}

		private string GetWholesaleDisplayName(string wholesaleId)
		{
			if (!string.IsNullOrWhiteSpace(wholesaleId) && _questGroupWholesaleDisplayNames.TryGetValue(wholesaleId, out var value))
			{
				return value;
			}
			if (!(_levelData != null))
			{
				return wholesaleId;
			}
			return _levelData.GetWholesaleDisplayName(wholesaleId);
		}

		private static string GetConveyorTableDisplayName(int tableId)
		{
			string unlockDisplayName = ProgressionTooltipLocalization.GetUnlockDisplayName(ProgressionUnlockKey.ConveyorTable, "Conveyor Table");
			return $"{unlockDisplayName} {tableId}";
		}

		private static string GetItemDisplayName(ItemSO item, string fallbackId)
		{
			if (item == null)
			{
				return fallbackId;
			}
			if (!string.IsNullOrWhiteSpace(item.LocalizedName))
			{
				return item.LocalizedName;
			}
			if (!string.IsNullOrWhiteSpace(item.name))
			{
				return item.name;
			}
			return fallbackId;
		}

		private void RaiseProgressionChanged()
		{
			ProgressionChanged?.Invoke(CurrentLevel, CurrentXp, NextLevelRequiredTotalXp);
		}

		protected override void __initializeVariables()
		{
			if (_progressionData == null)
			{
				throw new Exception("ProgressionManager._progressionData cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_progressionData.Initialize(this);
			__nameNetworkVariable(_progressionData, "_progressionData");
			NetworkVariableFields.Add(_progressionData);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3147589679u, __rpc_handler_3147589679, "ClientShowStaffXpFeedbackRpc", RpcInvokePermission.Everyone);
			__registerRpc(1336427634u, __rpc_handler_1336427634, "ClientRequestXpFeedbackRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3147589679(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				((ProgressionManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ProgressionManager)target).ClientShowStaffXpFeedbackRpc(value);
				((ProgressionManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1336427634(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				reader.ReadValueSafe(out double value3, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out double value4, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out int value5);
				reader.ReadValueSafe(out double value6, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out double value7, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((ProgressionManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ProgressionManager)target).ClientRequestXpFeedbackRpc(value, value2, value3, value4, value5, value6, value7, ext);
				((ProgressionManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "ProgressionManager";
		}
	}
}
