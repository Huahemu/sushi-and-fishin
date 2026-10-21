using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Quests
{
	public class QuestManager : NetworkBehaviour
	{
		private const string LogPrefix = "[QuestManager]";

		[Header("Database")]
		[SerializeField]
		private QuestDatabaseSO _database;

		[Header("Startup")]
		[SerializeField]
		private bool _autoStartFirstMainGroup;

		private readonly HashSet<string> _activeSideGroupIds = new HashSet<string>();

		private readonly HashSet<string> _completedQuestIds = new HashSet<string>();

		private readonly Dictionary<string, int> _questProgressById = new Dictionary<string, int>();

		private readonly HashSet<string> _analyticsStartedQuestIds = new HashSet<string>();

		private QuestGroupSO _currentMainGroup;

		private bool _isInitialized;

		private bool _isRefreshingExternalQuestRequirements;

		public static QuestManager Instance { get; private set; }

		public QuestGroupSO CurrentMainGroup => _currentMainGroup;

		public IReadOnlyCollection<string> ActiveSideGroupIds => _activeSideGroupIds;

		public IReadOnlyCollection<string> CompletedQuestIds => _completedQuestIds;

		public bool IsStateReady => _isInitialized;

		private bool IsNetworkRunning
		{
			get
			{
				if (NetworkManager.Singleton != null)
				{
					return NetworkManager.Singleton.IsListening;
				}
				return false;
			}
		}

		private bool IsServerOrOffline
		{
			get
			{
				if (IsNetworkRunning && !base.IsServer)
				{
					if (NetworkManager.Singleton != null)
					{
						return NetworkManager.Singleton.IsServer;
					}
					return false;
				}
				return true;
			}
		}

		private bool CanWriteState => IsServerOrOffline;

		public event Action<QuestGroupSO> MainGroupChanged;

		public event Action<QuestGroupSO> SideGroupStarted;

		public event Action<QuestGroupSO> SideGroupCompleted;

		public event Action<QuestSO> QuestCompleted;

		public event Action<QuestSO, int, int> QuestProgressChanged;

		public event Action QuestStateChanged;

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				UnityEngine.Object.Destroy(base.gameObject);
			}
			else
			{
				Instance = this;
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			if (base.IsServer)
			{
				if (_autoStartFirstMainGroup && !_isInitialized)
				{
					InitializeNewGame();
				}
				BroadcastSnapshotToClients();
			}
			else
			{
				RequestSnapshotRpc();
			}
		}

		private void Start()
		{
			if (!IsNetworkRunning && _autoStartFirstMainGroup && !_isInitialized)
			{
				InitializeNewGame();
			}
		}

		private bool IsClientOnlyNetworkInstance()
		{
			if (!IsNetworkRunning)
			{
				return false;
			}
			bool flag = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
			if (!base.IsServer)
			{
				return !flag;
			}
			return false;
		}

		public bool RequestCompleteQuest(QuestSO quest)
		{
			if (quest == null)
			{
				return false;
			}
			return RequestCompleteQuest(quest.Id);
		}

		public bool RequestCompleteQuest(string questId)
		{
			if (string.IsNullOrWhiteSpace(questId))
			{
				return false;
			}
			if (CanWriteState)
			{
				bool num = ServerCompleteQuestInternal(questId);
				if (num)
				{
					BroadcastSnapshotToClients();
				}
				return num;
			}
			CompleteQuestRpc(questId);
			return true;
		}

		public void ServerReconcileOwnedCompassQuests(bool compassUnlocked, byte compassLevel)
		{
			if (CanWriteState && _isInitialized && TryCompleteOwnedCompassQuests(compassUnlocked, compassLevel))
			{
				BroadcastSnapshotToClients();
			}
		}

		public void ServerReconcileOwnedToolQuests(ToolDataNet toolData)
		{
			if (CanWriteState && _isInitialized && TryCompleteOwnedToolQuests(toolData))
			{
				BroadcastSnapshotToClients();
			}
		}

		public void ServerReconcileRepairedConveyorQuest(bool conveyorRepaired)
		{
			if (CanWriteState && _isInitialized && TryCompleteRepairedConveyorQuest(conveyorRepaired))
			{
				BroadcastSnapshotToClients();
			}
		}

		public void ServerReconcileRepairedConveyorChairQuest(bool chairRepaired)
		{
			if (CanWriteState && _isInitialized && TryCompleteRepairedConveyorChairQuest(chairRepaired))
			{
				BroadcastSnapshotToClients();
			}
		}

		public bool RequestAddQuestProgress(QuestSO quest, int amount = 1)
		{
			if (quest == null)
			{
				return false;
			}
			return RequestAddQuestProgress(quest.Id, amount);
		}

		public bool RequestAddQuestProgress(string questId, int amount = 1)
		{
			if (string.IsNullOrWhiteSpace(questId) || amount <= 0)
			{
				return false;
			}
			if (CanWriteState)
			{
				bool num = ServerAddQuestProgressInternal(questId, amount);
				if (num)
				{
					BroadcastSnapshotToClients();
				}
				return num;
			}
			AddQuestProgressRpc(questId, amount);
			return true;
		}

		public bool RequestSetQuestProgressAtLeast(QuestSO quest, int targetProgress)
		{
			if (quest == null)
			{
				return false;
			}
			return RequestSetQuestProgressAtLeast(quest.Id, targetProgress);
		}

		public bool RequestSetQuestProgressAtLeast(string questId, int targetProgress)
		{
			if (string.IsNullOrWhiteSpace(questId) || targetProgress <= 0)
			{
				return false;
			}
			if (CanWriteState)
			{
				bool num = ServerSetQuestProgressAtLeastInternal(questId, targetProgress);
				if (num)
				{
					BroadcastSnapshotToClients();
				}
				return num;
			}
			SetQuestProgressAtLeastRpc(questId, targetProgress);
			return true;
		}

		public bool RequestCompleteCurrentMainGroupIfContainsQuest(QuestSO quest)
		{
			if (quest == null)
			{
				return false;
			}
			return RequestCompleteCurrentMainGroupIfContainsQuest(quest.Id);
		}

		public bool RequestCompleteCurrentMainGroupIfContainsQuest(string anchorQuestId)
		{
			if (string.IsNullOrWhiteSpace(anchorQuestId))
			{
				return false;
			}
			if (CanWriteState)
			{
				bool num = ServerCompleteCurrentMainGroupIfContainsQuestInternal(anchorQuestId);
				if (num)
				{
					BroadcastSnapshotToClients();
				}
				return num;
			}
			CompleteCurrentMainGroupIfContainsQuestRpc(anchorQuestId);
			return true;
		}

		[Rpc(SendTo.Server)]
		private void CompleteCurrentMainGroupIfContainsQuestRpc(string anchorQuestId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3835413954u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bool value = anchorQuestId != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(anchorQuestId);
				}
				__endSendRpc(ref bufferWriter, 3835413954u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (ServerCompleteCurrentMainGroupIfContainsQuestInternal(anchorQuestId))
				{
					BroadcastSnapshotToClients();
				}
			}
		}

		private bool ServerCompleteCurrentMainGroupIfContainsQuestInternal(string anchorQuestId)
		{
			if (_database == null)
			{
				LogQuest("[CompleteCurrentMainGroup] FAILED - Database is null.", LogType.Warning);
				return false;
			}
			if (_currentMainGroup == null)
			{
				LogQuest("[CompleteCurrentMainGroup] FAILED - Current main group is null.", LogType.Warning);
				return false;
			}
			if (!_currentMainGroup.ContainsQuest(anchorQuestId))
			{
				return false;
			}
			QuestGroupSO currentMainGroup = _currentMainGroup;
			bool flag = false;
			foreach (QuestSO quest in currentMainGroup.Quests)
			{
				if (!(quest == null) && !_completedQuestIds.Contains(quest.Id))
				{
					AddCompletedQuestInternal(quest);
					flag = true;
				}
			}
			if (!flag)
			{
				return false;
			}
			if (IsGroupCompleted(currentMainGroup))
			{
				CompleteQuestGroupRewardsInternal(currentMainGroup);
				AdvanceMainGroupInternal();
			}
			RaiseQuestStateChanged();
			return true;
		}

		public bool RequestStartSideGroup(QuestGroupSO group)
		{
			if (group == null)
			{
				return false;
			}
			return RequestStartSideGroup(group.Id);
		}

		public bool RequestStartSideGroup(string groupId)
		{
			if (string.IsNullOrWhiteSpace(groupId))
			{
				return false;
			}
			if (CanWriteState)
			{
				bool num = ServerStartSideGroupInternal(groupId);
				if (num)
				{
					BroadcastSnapshotToClients();
				}
				return num;
			}
			StartSideGroupRpc(groupId);
			return true;
		}

		[Rpc(SendTo.Server)]
		private void CompleteQuestRpc(string questId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2693880456u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bool value = questId != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(questId);
				}
				__endSendRpc(ref bufferWriter, 2693880456u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (ServerCompleteQuestInternal(questId))
				{
					BroadcastSnapshotToClients();
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void AddQuestProgressRpc(string questId, int amount)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2207900302u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bool value = questId != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(questId);
				}
				BytePacker.WriteValueBitPacked(bufferWriter, amount);
				__endSendRpc(ref bufferWriter, 2207900302u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (ServerAddQuestProgressInternal(questId, amount))
				{
					BroadcastSnapshotToClients();
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void SetQuestProgressAtLeastRpc(string questId, int targetProgress)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1631450048u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bool value = questId != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(questId);
				}
				BytePacker.WriteValueBitPacked(bufferWriter, targetProgress);
				__endSendRpc(ref bufferWriter, 1631450048u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (ServerSetQuestProgressAtLeastInternal(questId, targetProgress))
				{
					BroadcastSnapshotToClients();
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void StartSideGroupRpc(string groupId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1065164540u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bool value = groupId != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(groupId);
				}
				__endSendRpc(ref bufferWriter, 1065164540u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (ServerStartSideGroupInternal(groupId))
				{
					BroadcastSnapshotToClients();
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void RequestSnapshotRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3315873935u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3315873935u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				BroadcastSnapshotToClients();
			}
		}

		[Rpc(SendTo.NotServer)]
		private void SyncQuestSnapshotRpc(string json)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1835669337u, rpcParams, attributeParams, SendTo.NotServer, RpcDelivery.Reliable);
				bool value = json != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(json);
				}
				__endSendRpc(ref bufferWriter, 1835669337u, rpcParams, attributeParams, SendTo.NotServer, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (string.IsNullOrWhiteSpace(json))
				{
					LogQuest("[Snapshot] EMPTY - Client received empty quest snapshot.", LogType.Warning);
					return;
				}
				QuestSaveData data = JsonUtility.FromJson<QuestSaveData>(json);
				ApplySnapshot(data, raiseEvents: true);
				LogQuest(BuildQuestStateSummary("[Snapshot] CLIENT APPLIED"));
			}
		}

		public void InitializeNewGame()
		{
			if (IsClientOnlyNetworkInstance())
			{
				LogQuest("[NewGame] BLOCKED - Client cannot initialize quest state. Requesting server snapshot.", LogType.Warning);
				if (base.IsSpawned)
				{
					RequestSnapshotRpc();
				}
				return;
			}
			QuestGroupSO firstIncompleteMainGroup = GetFirstIncompleteMainGroup();
			QuestSaveData questSaveData = new QuestSaveData
			{
				CurrentMainGroupId = ((firstIncompleteMainGroup != null) ? firstIncompleteMainGroup.Id : string.Empty),
				ActiveSideGroupIds = new List<string>(),
				CompletedGroupIds = new List<string>(),
				CompletedQuestIds = new List<string>(),
				QuestProgresses = new List<QuestProgressSaveEntry>()
			};
			LogQuest("[NewGame] START - First Main Group: " + GetGroupName(questSaveData.CurrentMainGroupId));
			ApplySnapshot(questSaveData, raiseEvents: true);
			LogQuest(BuildQuestStateSummary("[NewGame] DONE"));
			BroadcastSnapshotToClients();
		}

		public void Load(QuestSaveData data)
		{
			if (!CanWriteState)
			{
				LogQuest("[Load] BLOCKED - Only server/offline instance can load quest state.", LogType.Warning);
				return;
			}
			if (data == null)
			{
				LogQuest("[Load] NO SAVE DATA - Initializing new quest state.", LogType.Warning);
				InitializeNewGame();
				return;
			}
			QuestSaveData questSaveData = data;
			if (questSaveData.ActiveSideGroupIds == null)
			{
				questSaveData.ActiveSideGroupIds = new List<string>();
			}
			questSaveData = data;
			if (questSaveData.CompletedGroupIds == null)
			{
				questSaveData.CompletedGroupIds = new List<string>();
			}
			questSaveData = data;
			if (questSaveData.CompletedQuestIds == null)
			{
				questSaveData.CompletedQuestIds = new List<string>();
			}
			questSaveData = data;
			if (questSaveData.QuestProgresses == null)
			{
				questSaveData.QuestProgresses = new List<QuestProgressSaveEntry>();
			}
			LogQuest(BuildQuestLoadSummary(data));
			ApplySnapshot(data, raiseEvents: true);
			LogQuest(BuildQuestStateSummary("[Load] DONE"));
			BroadcastSnapshotToClients();
		}

		public QuestSaveData GetSaveData()
		{
			QuestSaveData questSaveData = CreateSaveData();
			LogQuest(BuildQuestSaveSummary(questSaveData));
			return questSaveData;
		}

		private QuestSaveData CreateSaveData()
		{
			return new QuestSaveData
			{
				CurrentMainGroupId = ((_currentMainGroup != null) ? _currentMainGroup.Id : string.Empty),
				ActiveSideGroupIds = _activeSideGroupIds.ToList(),
				CompletedGroupIds = GetCompletedGroupIds(),
				CompletedQuestIds = _completedQuestIds.ToList(),
				QuestProgresses = GetProgressSaveEntries()
			};
		}

		private bool ServerStartSideGroupInternal(string groupId)
		{
			if (_database == null)
			{
				LogQuest("[StartSideGroup] FAILED - Database is null.", LogType.Warning);
				return false;
			}
			QuestGroupSO groupById = _database.GetGroupById(groupId);
			if (groupById == null)
			{
				LogQuest("[StartSideGroup] FAILED - Group not found: " + groupId, LogType.Warning);
				return false;
			}
			if (groupById.Type != QuestType.Side)
			{
				LogQuest("[StartSideGroup] FAILED - Group is not Side: " + GetGroupLabel(groupById), LogType.Warning);
				return false;
			}
			if (IsGroupCompleted(groupById))
			{
				return false;
			}
			if (!_activeSideGroupIds.Add(groupById.Id))
			{
				return false;
			}
			SideGroupStarted?.Invoke(groupById);
			RaiseQuestStateChanged();
			return true;
		}

		private bool ServerCompleteQuestInternal(string questId)
		{
			if (_database == null)
			{
				LogQuest("[CompleteQuest] FAILED - Database is null.", LogType.Warning);
				return false;
			}
			QuestSO questById = _database.GetQuestById(questId);
			if (questById == null)
			{
				LogQuest("[CompleteQuest] FAILED - Quest not found: " + questId, LogType.Warning);
				return false;
			}
			if (_completedQuestIds.Contains(questById.Id))
			{
				return false;
			}
			QuestGroupSO groupOfQuest = _database.GetGroupOfQuest(questById.Id);
			if (groupOfQuest == null)
			{
				LogQuest("[CompleteQuest] FAILED - Group not found for quest: " + GetQuestLabel(questById), LogType.Warning);
				return false;
			}
			bool flag = IsQuestActive(questById);
			if (!flag && !questById.CanCompleteBeforeActive)
			{
				LogQuest("[CompleteQuest] BLOCKED - Quest is not active: " + GetQuestLabel(questById), LogType.Warning);
				return false;
			}
			if (!flag && questById.CanCompleteBeforeActive)
			{
				AddCompletedQuestInternal(questById);
				RaiseQuestStateChanged();
				return true;
			}
			if (groupOfQuest.Type == QuestType.Main)
			{
				return CompleteMainQuestInternal(groupOfQuest, questById);
			}
			return CompleteSideQuestInternal(groupOfQuest, questById);
		}

		private bool ServerAddQuestProgressInternal(string questId, int amount)
		{
			if (_database == null)
			{
				LogQuest("[QuestProgress] FAILED - Database is null.", LogType.Warning);
				return false;
			}
			if (amount <= 0)
			{
				return false;
			}
			QuestSO questById = _database.GetQuestById(questId);
			if (questById == null)
			{
				LogQuest("[QuestProgress] FAILED - Quest not found: " + questId, LogType.Warning);
				return false;
			}
			if (_completedQuestIds.Contains(questById.Id))
			{
				return false;
			}
			if (_database.GetGroupOfQuest(questById.Id) == null)
			{
				LogQuest("[QuestProgress] FAILED - Group not found for quest: " + GetQuestLabel(questById), LogType.Warning);
				return false;
			}
			if (!IsQuestActive(questById) && !questById.CanCompleteBeforeActive)
			{
				LogQuest("[QuestProgress] BLOCKED - Quest is not active: " + GetQuestLabel(questById), LogType.Warning);
				return false;
			}
			if (!questById.IsCountable)
			{
				return ServerCompleteQuestInternal(questById.Id);
			}
			int questProgress = GetQuestProgress(questById);
			int targetAmount = questById.TargetAmount;
			questProgress = Mathf.Clamp(questProgress + amount, 0, targetAmount);
			_questProgressById[questById.Id] = questProgress;
			QuestProgressChanged?.Invoke(questById, questProgress, targetAmount);
			if (questProgress >= targetAmount)
			{
				return ServerCompleteQuestInternal(questById.Id);
			}
			RaiseQuestStateChanged();
			return true;
		}

		private bool ServerSetQuestProgressAtLeastInternal(string questId, int targetProgress)
		{
			if (_database == null)
			{
				LogQuest("[QuestProgressAtLeast] FAILED - Database is null.", LogType.Warning);
				return false;
			}
			if (targetProgress <= 0)
			{
				return false;
			}
			QuestSO questById = _database.GetQuestById(questId);
			if (questById == null)
			{
				LogQuest("[QuestProgressAtLeast] FAILED - Quest not found: " + questId, LogType.Warning);
				return false;
			}
			if (_completedQuestIds.Contains(questById.Id))
			{
				return false;
			}
			if (_database.GetGroupOfQuest(questById.Id) == null)
			{
				LogQuest("[QuestProgressAtLeast] FAILED - Group not found for quest: " + GetQuestLabel(questById), LogType.Warning);
				return false;
			}
			if (!IsQuestActive(questById) && !questById.CanCompleteBeforeActive)
			{
				LogQuest("[QuestProgressAtLeast] BLOCKED - Quest is not active: " + GetQuestLabel(questById), LogType.Warning);
				return false;
			}
			if (!questById.IsCountable)
			{
				return false;
			}
			int questProgress = GetQuestProgress(questById);
			int targetAmount = questById.TargetAmount;
			int num = Mathf.Clamp(targetProgress, 0, targetAmount);
			if (num <= questProgress)
			{
				return false;
			}
			_questProgressById[questById.Id] = num;
			QuestProgressChanged?.Invoke(questById, num, targetAmount);
			if (num >= targetAmount)
			{
				return ServerCompleteQuestInternal(questById.Id);
			}
			RaiseQuestStateChanged();
			return true;
		}

		private bool CompleteMainQuestInternal(QuestGroupSO group, QuestSO quest)
		{
			if (_currentMainGroup == null)
			{
				LogQuest("[CompleteMainQuest] FAILED - No active main group. Quest: " + GetQuestLabel(quest), LogType.Warning);
				return false;
			}
			if (_currentMainGroup.Id != group.Id)
			{
				LogQuest("[CompleteMainQuest] FAILED - Quest is not in active main group: " + GetQuestLabel(quest), LogType.Warning);
				return false;
			}
			AddCompletedQuestInternal(quest);
			if (IsGroupCompleted(group))
			{
				CompleteQuestGroupRewardsInternal(group);
				AdvanceMainGroupInternal();
			}
			RaiseQuestStateChanged();
			return true;
		}

		private bool CompleteSideQuestInternal(QuestGroupSO group, QuestSO quest)
		{
			if (!_activeSideGroupIds.Contains(group.Id))
			{
				LogQuest("[CompleteSideQuest] FAILED - Side group is not active: " + GetGroupLabel(group), LogType.Warning);
				return false;
			}
			AddCompletedQuestInternal(quest);
			if (IsGroupCompleted(group))
			{
				_activeSideGroupIds.Remove(group.Id);
				CompleteQuestGroupRewardsInternal(group);
				SideGroupCompleted?.Invoke(group);
			}
			RaiseQuestStateChanged();
			return true;
		}

		private void AddCompletedQuestInternal(QuestSO quest)
		{
			if (!(quest == null) && _completedQuestIds.Add(quest.Id))
			{
				if (quest.IsCountable)
				{
					_questProgressById[quest.Id] = quest.TargetAmount;
				}
				else
				{
					_questProgressById.Remove(quest.Id);
				}
				QuestCompleted?.Invoke(quest);
			}
		}

		private void CompleteQuestGroupRewardsInternal(QuestGroupSO group)
		{
			SyncQuestGroupCompletionUnlockRewards(showNewUnlockToasts: true);
			ServerGrantQuestGroupCompletedXp(group);
		}

		private void ServerGrantQuestGroupCompletedXp(QuestGroupSO group)
		{
			if (base.IsServer && !(group == null))
			{
				ProgressionManager progressionManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.ProgressionManager : null);
				if (!(progressionManager == null))
				{
					progressionManager.ServerGrantXp(ProgressionXpSource.QuestGroupCompleted, group.Id);
				}
			}
		}

		private void AdvanceMainGroupInternal()
		{
			_currentMainGroup = GetFirstIncompleteMainGroup();
			MainGroupChanged?.Invoke(_currentMainGroup);
		}

		private void BroadcastSnapshotToClients()
		{
			if (IsNetworkRunning && base.IsServer && base.IsSpawned)
			{
				string json = JsonUtility.ToJson(CreateSaveData());
				SyncQuestSnapshotRpc(json);
			}
		}

		private void ApplySnapshot(QuestSaveData data, bool raiseEvents)
		{
			if (data == null)
			{
				data = new QuestSaveData();
			}
			QuestSaveData questSaveData = data;
			if (questSaveData.ActiveSideGroupIds == null)
			{
				questSaveData.ActiveSideGroupIds = new List<string>();
			}
			questSaveData = data;
			if (questSaveData.CompletedGroupIds == null)
			{
				questSaveData.CompletedGroupIds = new List<string>();
			}
			questSaveData = data;
			if (questSaveData.CompletedQuestIds == null)
			{
				questSaveData.CompletedQuestIds = new List<string>();
			}
			questSaveData = data;
			if (questSaveData.QuestProgresses == null)
			{
				questSaveData.QuestProgresses = new List<QuestProgressSaveEntry>();
			}
			HashSet<string> groupIds = ResolveCompletedGroupIdsForLoad(data);
			string oldMainGroupId = ((_currentMainGroup != null) ? _currentMainGroup.Id : string.Empty);
			HashSet<string> oldSideGroups = new HashSet<string>(_activeSideGroupIds);
			HashSet<string> oldCompletedQuests = new HashSet<string>(_completedQuestIds);
			Dictionary<string, int> oldProgress = new Dictionary<string, int>(_questProgressById);
			_activeSideGroupIds.Clear();
			_completedQuestIds.Clear();
			_questProgressById.Clear();
			foreach (string completedQuestId in data.CompletedQuestIds)
			{
				if (!string.IsNullOrWhiteSpace(completedQuestId))
				{
					_completedQuestIds.Add(completedQuestId);
				}
			}
			AddCompletedQuestIdsFromGroups(groupIds);
			foreach (QuestProgressSaveEntry questProgress in data.QuestProgresses)
			{
				if (questProgress != null && !string.IsNullOrWhiteSpace(questProgress.QuestId) && !_completedQuestIds.Contains(questProgress.QuestId))
				{
					_questProgressById[questProgress.QuestId] = Mathf.Max(0, questProgress.Value);
				}
			}
			foreach (string activeSideGroupId in data.ActiveSideGroupIds)
			{
				if (!string.IsNullOrWhiteSpace(activeSideGroupId))
				{
					QuestGroupSO questGroupSO = ((_database != null) ? _database.GetGroupById(activeSideGroupId) : null);
					if (questGroupSO == null)
					{
						LogQuest("[Load] Skipped side group. Group not found: " + activeSideGroupId, LogType.Warning);
					}
					else if (questGroupSO.Type != QuestType.Side)
					{
						LogQuest("[Load] Skipped side group. Group is not Side: " + GetGroupLabel(questGroupSO), LogType.Warning);
					}
					else if (!IsGroupCompleted(questGroupSO))
					{
						_activeSideGroupIds.Add(questGroupSO.Id);
					}
				}
			}
			_currentMainGroup = ((_database != null) ? _database.GetGroupById(data.CurrentMainGroupId) : null);
			if (_currentMainGroup == null || IsGroupCompleted(_currentMainGroup))
			{
				_currentMainGroup = GetFirstIncompleteMainGroup();
			}
			_isInitialized = true;
			SyncQuestGroupCompletionUnlockRewards(showNewUnlockToasts: false);
			if (Singleton<JoinBootstrapCoordinator>.TryGetInstance(out var instance))
			{
				instance.NotifyQuestStateReady();
			}
			if (raiseEvents)
			{
				RaiseSnapshotEvents(oldMainGroupId, oldSideGroups, oldCompletedQuests, oldProgress);
			}
		}

		private void RaiseSnapshotEvents(string oldMainGroupId, HashSet<string> oldSideGroups, HashSet<string> oldCompletedQuests, Dictionary<string, int> oldProgress)
		{
			string text = ((_currentMainGroup != null) ? _currentMainGroup.Id : string.Empty);
			if (oldMainGroupId != text)
			{
				MainGroupChanged?.Invoke(_currentMainGroup);
			}
			foreach (string activeSideGroupId in _activeSideGroupIds)
			{
				if (!oldSideGroups.Contains(activeSideGroupId))
				{
					QuestGroupSO questGroupSO = ((_database != null) ? _database.GetGroupById(activeSideGroupId) : null);
					if (questGroupSO != null)
					{
						SideGroupStarted?.Invoke(questGroupSO);
					}
				}
			}
			foreach (string oldSideGroup in oldSideGroups)
			{
				if (!_activeSideGroupIds.Contains(oldSideGroup))
				{
					QuestGroupSO questGroupSO2 = ((_database != null) ? _database.GetGroupById(oldSideGroup) : null);
					if (questGroupSO2 != null)
					{
						SideGroupCompleted?.Invoke(questGroupSO2);
					}
				}
			}
			foreach (KeyValuePair<string, int> item in _questProgressById)
			{
				if ((oldProgress.TryGetValue(item.Key, out var value) ? value : 0) != item.Value)
				{
					QuestSO questSO = ((_database != null) ? _database.GetQuestById(item.Key) : null);
					if (questSO != null)
					{
						QuestProgressChanged?.Invoke(questSO, item.Value, questSO.TargetAmount);
					}
				}
			}
			foreach (string completedQuestId in _completedQuestIds)
			{
				if (!oldCompletedQuests.Contains(completedQuestId))
				{
					QuestSO obj = ((_database != null) ? _database.GetQuestById(completedQuestId) : null);
					QuestCompleted?.Invoke(obj);
				}
			}
			RaiseQuestStateChanged();
		}

		public bool IsQuestActive(QuestSO quest)
		{
			if (quest == null)
			{
				return false;
			}
			return IsQuestActive(quest.Id);
		}

		public bool IsQuestActive(string questId)
		{
			if (string.IsNullOrWhiteSpace(questId))
			{
				return false;
			}
			if (_completedQuestIds.Contains(questId))
			{
				return false;
			}
			if (_currentMainGroup != null && _currentMainGroup.ContainsQuest(questId))
			{
				return true;
			}
			if (_database == null)
			{
				return false;
			}
			foreach (string activeSideGroupId in _activeSideGroupIds)
			{
				QuestGroupSO groupById = _database.GetGroupById(activeSideGroupId);
				if (groupById != null && groupById.ContainsQuest(questId))
				{
					return true;
				}
			}
			return false;
		}

		public bool IsQuestCompleted(QuestSO quest)
		{
			if (quest == null)
			{
				return false;
			}
			return IsQuestCompleted(quest.Id);
		}

		public bool IsQuestCompleted(string questId)
		{
			if (string.IsNullOrWhiteSpace(questId))
			{
				return false;
			}
			return _completedQuestIds.Contains(questId);
		}

		public bool IsGroupCompleted(QuestGroupSO group)
		{
			if (group == null)
			{
				return false;
			}
			if (group.Quests == null || group.Quests.Count == 0)
			{
				return false;
			}
			foreach (QuestSO quest in group.Quests)
			{
				if (!(quest == null) && !_completedQuestIds.Contains(quest.Id))
				{
					return false;
				}
			}
			return true;
		}

		public int GetQuestProgress(QuestSO quest)
		{
			if (quest == null)
			{
				return 0;
			}
			return GetQuestProgress(quest.Id);
		}

		public int GetQuestProgress(string questId)
		{
			if (string.IsNullOrWhiteSpace(questId))
			{
				return 0;
			}
			QuestSO questSO = ((_database != null) ? _database.GetQuestById(questId) : null);
			if (_completedQuestIds.Contains(questId))
			{
				if (!(questSO != null))
				{
					return 1;
				}
				return questSO.TargetAmount;
			}
			if (_questProgressById.TryGetValue(questId, out var value))
			{
				return Mathf.Max(0, value);
			}
			return 0;
		}

		public int GetQuestTarget(QuestSO quest)
		{
			if (quest == null)
			{
				return 1;
			}
			return quest.TargetAmount;
		}

		public List<QuestGroupSO> GetActiveSideGroups()
		{
			List<QuestGroupSO> list = new List<QuestGroupSO>();
			if (_database == null)
			{
				return list;
			}
			foreach (string activeSideGroupId in _activeSideGroupIds)
			{
				QuestGroupSO groupById = _database.GetGroupById(activeSideGroupId);
				if (groupById != null)
				{
					list.Add(groupById);
				}
			}
			return list;
		}

		public List<QuestSO> GetVisibleMainQuests()
		{
			List<QuestSO> list = new List<QuestSO>();
			if (_currentMainGroup == null)
			{
				return list;
			}
			foreach (QuestSO quest in _currentMainGroup.Quests)
			{
				if (quest != null)
				{
					list.Add(quest);
				}
			}
			return list;
		}

		public List<QuestSO> GetVisibleSideQuests()
		{
			List<QuestSO> list = new List<QuestSO>();
			foreach (QuestGroupSO activeSideGroup in GetActiveSideGroups())
			{
				foreach (QuestSO quest in activeSideGroup.Quests)
				{
					if (quest != null)
					{
						list.Add(quest);
					}
				}
			}
			return list;
		}

		private QuestGroupSO GetFirstIncompleteMainGroup()
		{
			if (_database == null || _database.MainGroups == null)
			{
				return null;
			}
			foreach (QuestGroupSO mainGroup in _database.MainGroups)
			{
				if (!(mainGroup == null) && !IsGroupCompleted(mainGroup))
				{
					return mainGroup;
				}
			}
			return null;
		}

		private List<string> GetCompletedGroupIds()
		{
			List<string> list = new List<string>();
			foreach (QuestGroupSO completedQuestGroup in GetCompletedQuestGroups())
			{
				if (!(completedQuestGroup == null) && !string.IsNullOrWhiteSpace(completedQuestGroup.Id))
				{
					list.Add(completedQuestGroup.Id);
				}
			}
			return list;
		}

		private HashSet<string> ResolveCompletedGroupIdsForLoad(QuestSaveData data)
		{
			HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
			if (data == null || _database == null)
			{
				return hashSet;
			}
			if (data.CompletedGroupIds != null && data.CompletedGroupIds.Count > 0)
			{
				foreach (string completedGroupId in data.CompletedGroupIds)
				{
					if (!string.IsNullOrWhiteSpace(completedGroupId))
					{
						hashSet.Add(completedGroupId);
					}
				}
			}
			else
			{
				AddLegacyCompletedMainGroupsBeforeCurrent(data.CurrentMainGroupId, data.CompletedQuestIds, hashSet);
			}
			AddGroupsAlreadyCompletedBySavedQuestIds(data.CompletedQuestIds, hashSet);
			return hashSet;
		}

		private void AddLegacyCompletedMainGroupsBeforeCurrent(string currentMainGroupId, List<string> completedQuestIds, HashSet<string> groupIds)
		{
			if (_database == null || _database.MainGroups == null || groupIds == null)
			{
				return;
			}
			bool flag = !string.IsNullOrWhiteSpace(currentMainGroupId);
			bool flag2 = completedQuestIds?.Any((string id) => !string.IsNullOrWhiteSpace(id)) ?? false;
			if (!flag && !flag2)
			{
				return;
			}
			foreach (QuestGroupSO mainGroup in _database.MainGroups)
			{
				if (!(mainGroup == null) && !string.IsNullOrWhiteSpace(mainGroup.Id))
				{
					if (flag && mainGroup.Id == currentMainGroupId)
					{
						break;
					}
					groupIds.Add(mainGroup.Id);
				}
			}
		}

		private void AddGroupsAlreadyCompletedBySavedQuestIds(List<string> completedQuestIds, HashSet<string> groupIds)
		{
			if (_database == null || completedQuestIds == null || groupIds == null)
			{
				return;
			}
			HashSet<string> completedQuestIds2 = new HashSet<string>(completedQuestIds.Where((string id) => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
			foreach (QuestGroupSO allGroup in _database.GetAllGroups())
			{
				if (!(allGroup == null) && !string.IsNullOrWhiteSpace(allGroup.Id) && IsGroupCompletedByQuestIds(allGroup, completedQuestIds2))
				{
					groupIds.Add(allGroup.Id);
				}
			}
		}

		private static bool IsGroupCompletedByQuestIds(QuestGroupSO group, HashSet<string> completedQuestIds)
		{
			if (group == null || group.Quests == null || group.Quests.Count == 0 || completedQuestIds == null)
			{
				return false;
			}
			foreach (QuestSO quest in group.Quests)
			{
				if (!(quest == null) && !string.IsNullOrWhiteSpace(quest.Id) && !completedQuestIds.Contains(quest.Id))
				{
					return false;
				}
			}
			return true;
		}

		private void AddCompletedQuestIdsFromGroups(HashSet<string> groupIds)
		{
			if (_database == null || groupIds == null || groupIds.Count == 0)
			{
				return;
			}
			foreach (string groupId in groupIds)
			{
				if (string.IsNullOrWhiteSpace(groupId))
				{
					continue;
				}
				QuestGroupSO groupById = _database.GetGroupById(groupId);
				if (groupById == null || groupById.Quests == null)
				{
					continue;
				}
				foreach (QuestSO quest in groupById.Quests)
				{
					if (!(quest == null) && !string.IsNullOrWhiteSpace(quest.Id))
					{
						_completedQuestIds.Add(quest.Id);
					}
				}
			}
		}

		private List<QuestProgressSaveEntry> GetProgressSaveEntries()
		{
			List<QuestProgressSaveEntry> list = new List<QuestProgressSaveEntry>();
			foreach (KeyValuePair<string, int> item in _questProgressById)
			{
				if (!string.IsNullOrWhiteSpace(item.Key) && !_completedQuestIds.Contains(item.Key) && item.Value > 0)
				{
					list.Add(new QuestProgressSaveEntry(item.Key, item.Value));
				}
			}
			return list;
		}

		private void RaiseQuestStateChanged(bool emitStartedAnalytics = true)
		{
			if (emitStartedAnalytics)
			{
				EmitQuestStartedAnalyticsForActiveQuests();
			}
			QuestStateChanged?.Invoke();
			RefreshExternalQuestRequirementsAfterStateChanged();
		}

		private void EmitQuestStartedAnalyticsForActiveQuests()
		{
			EmitQuestStartedAnalyticsForGroup(_currentMainGroup);
			foreach (QuestGroupSO activeSideGroup in GetActiveSideGroups())
			{
				EmitQuestStartedAnalyticsForGroup(activeSideGroup);
			}
		}

		private void EmitQuestStartedAnalyticsForGroup(QuestGroupSO group)
		{
			if (group == null || group.Quests == null)
			{
				return;
			}
			foreach (QuestSO quest in group.Quests)
			{
				if (!(quest == null) && !string.IsNullOrWhiteSpace(quest.Id) && IsQuestActive(quest) && !_completedQuestIds.Contains(quest.Id))
				{
					_analyticsStartedQuestIds.Add(quest.Id);
				}
			}
		}

		private void RefreshExternalQuestRequirementsAfterStateChanged()
		{
			if (!_isRefreshingExternalQuestRequirements && CanWriteState)
			{
				_isRefreshingExternalQuestRequirements = true;
				TryCompleteMarketVisitAfterSideDishPurchase();
				TryCompleteDivingSuitResourceQuestsAfterRequiredUpgrades();
				TryCompleteOwnedToolQuestsFromCurrentSave();
				TryCompleteRepairedConveyorQuestFromCurrentState();
				TryCompleteRepairedConveyorChairQuestFromCurrentState();
				if (Singleton<GameProgressManager>.Instance != null && Singleton<GameProgressManager>.Instance.EconomyHandler != null)
				{
					Singleton<GameProgressManager>.Instance.EconomyHandler.RefreshEconomyQuestRequirements();
				}
				_isRefreshingExternalQuestRequirements = false;
			}
		}

		private void TryCompleteMarketVisitAfterSideDishPurchase()
		{
			if (!(_currentMainGroup == null) && !(QuestReferenceHub.Instance == null))
			{
				QuestSO quest = QuestReferenceHub.Instance.GetQuest(QuestId.GoToGreenMarketForSideDish);
				QuestSO quest2 = QuestReferenceHub.Instance.GetQuest(QuestId.BuyASideDish);
				if (!(quest == null) && !(quest2 == null) && _currentMainGroup.ContainsQuest(quest.Id) && !_completedQuestIds.Contains(quest.Id) && _completedQuestIds.Contains(quest2.Id))
				{
					ServerCompleteQuestInternal(quest.Id);
				}
			}
		}

		private void TryCompleteDivingSuitResourceQuestsAfterRequiredUpgrades()
		{
			if (_currentMainGroup == null || QuestReferenceHub.Instance == null)
			{
				return;
			}
			QuestSO quest = QuestReferenceHub.Instance.GetQuest(QuestId.EarnOneThousandDollars);
			QuestSO quest2 = QuestReferenceHub.Instance.GetQuest(QuestId.CollectScrapForDivingSuit);
			QuestSO quest3 = QuestReferenceHub.Instance.GetQuest(QuestId.UpgradeDivingSuitToLevelThree);
			QuestSO quest4 = QuestReferenceHub.Instance.GetQuest(QuestId.UpgradeHarpoonToLevelTwo);
			if (!(quest == null) && !(quest2 == null) && !(quest3 == null) && !(quest4 == null) && _currentMainGroup.ContainsQuest(quest.Id) && _currentMainGroup.ContainsQuest(quest2.Id) && _completedQuestIds.Contains(quest3.Id) && _completedQuestIds.Contains(quest4.Id))
			{
				if (!_completedQuestIds.Contains(quest.Id))
				{
					ServerCompleteQuestInternal(quest.Id);
				}
				if (!_completedQuestIds.Contains(quest2.Id))
				{
					ServerCompleteQuestInternal(quest2.Id);
				}
			}
		}

		private void TryCompleteOwnedToolQuestsFromCurrentSave()
		{
			RpcRelay rpcRelay = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RpcRelay : null);
			if (rpcRelay == null || (IsNetworkRunning && (!rpcRelay.IsSpawned || !rpcRelay.IsServer)))
			{
				rpcRelay = RpcRelay.ActiveInstance;
			}
			if (!(rpcRelay == null) && (!IsNetworkRunning || (rpcRelay.IsSpawned && rpcRelay.IsServer)))
			{
				TryCompleteOwnedToolQuests(rpcRelay.ToolDataNV.Value);
			}
		}

		private bool TryCompleteOwnedToolQuests(ToolDataNet toolData)
		{
			bool flag = TryCompleteOwnedCompassQuests(toolData.CompassUnlocked, toolData.CompassLevel);
			if (QuestReferenceHub.Instance == null)
			{
				return flag;
			}
			if (toolData.FishingRodUnlocked)
			{
				flag |= TryCompleteOwnedToolQuest(QuestId.UnlockTheFishingRod);
			}
			if (toolData.ScooterUnlocked)
			{
				flag |= TryCompleteOwnedToolQuest(QuestId.UnlockTheSeaScooter);
			}
			return flag;
		}

		private bool TryCompleteOwnedCompassQuests(bool compassUnlocked, byte compassLevel)
		{
			if ((!compassUnlocked && compassLevel == 0) || QuestReferenceHub.Instance == null)
			{
				return false;
			}
			bool flag = TryCompleteOwnedToolQuest(QuestId.PickUpCompass);
			if (compassLevel >= 2)
			{
				flag |= TryCompleteOwnedToolQuest(QuestId.FallowTheCompassSignal);
			}
			if (compassLevel >= 3)
			{
				flag |= TryCompleteOwnedToolQuest(QuestId.FindTheNewCompassPiece);
			}
			if (compassLevel >= 4)
			{
				flag |= TryCompleteOwnedToolQuest(QuestId.FollowTheCompassSignalAndFindTheFinalPiece);
			}
			return flag;
		}

		private bool TryCompleteOwnedToolQuest(QuestId questId)
		{
			QuestSO quest = QuestReferenceHub.Instance.GetQuest(questId);
			if (quest == null || _completedQuestIds.Contains(quest.Id))
			{
				return false;
			}
			return ServerCompleteQuestInternal(quest.Id);
		}

		private void TryCompleteRepairedConveyorQuestFromCurrentState()
		{
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (!(restaurantManager == null))
			{
				TryCompleteRepairedConveyorQuest(restaurantManager.IsConveyorBeltRepaired);
			}
		}

		private bool TryCompleteRepairedConveyorQuest(bool conveyorRepaired)
		{
			if (!conveyorRepaired || QuestReferenceHub.Instance == null)
			{
				return false;
			}
			QuestSO quest = QuestReferenceHub.Instance.GetQuest(QuestId.RepairTheConveyor);
			if (quest == null || _completedQuestIds.Contains(quest.Id))
			{
				return false;
			}
			return ServerCompleteQuestInternal(quest.Id);
		}

		private void TryCompleteRepairedConveyorChairQuestFromCurrentState()
		{
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (restaurantManager != null)
			{
				TryCompleteRepairedConveyorChairQuest(restaurantManager.HasRepairedConveyorChairForQuest);
			}
		}

		private bool TryCompleteRepairedConveyorChairQuest(bool chairRepaired)
		{
			if (!chairRepaired || QuestReferenceHub.Instance == null)
			{
				return false;
			}
			QuestSO quest = QuestReferenceHub.Instance.GetQuest(QuestId.RepairConveyorChair);
			if (quest != null && !_completedQuestIds.Contains(quest.Id))
			{
				return ServerCompleteQuestInternal(quest.Id);
			}
			return false;
		}

		private void SyncQuestGroupCompletionUnlockRewards(bool showNewUnlockToasts)
		{
			ProgressionManager progressionManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.ProgressionManager : null);
			if (!(progressionManager == null))
			{
				progressionManager.RefreshQuestGroupCompletionUnlockRewards(GetCompletedQuestGroups(), showNewUnlockToasts);
			}
		}

		private List<QuestGroupSO> GetCompletedQuestGroups()
		{
			List<QuestGroupSO> list = new List<QuestGroupSO>();
			if (_database == null)
			{
				return list;
			}
			foreach (QuestGroupSO allGroup in _database.GetAllGroups())
			{
				if (!(allGroup == null) && IsGroupCompleted(allGroup))
				{
					list.Add(allGroup);
				}
			}
			return list;
		}

		public bool IsGroupActive(QuestGroupSO group)
		{
			if (group == null)
			{
				return false;
			}
			return IsGroupActive(group.Id);
		}

		public bool IsGroupActive(string groupId)
		{
			if (string.IsNullOrWhiteSpace(groupId))
			{
				return false;
			}
			if (_currentMainGroup != null && _currentMainGroup.Id == groupId)
			{
				return true;
			}
			return _activeSideGroupIds.Contains(groupId);
		}

		public QuestGroupSO GetActiveGroupById(string groupId)
		{
			if (string.IsNullOrWhiteSpace(groupId) || _database == null)
			{
				return null;
			}
			if (_currentMainGroup != null && _currentMainGroup.Id == groupId)
			{
				return _currentMainGroup;
			}
			if (_activeSideGroupIds.Contains(groupId))
			{
				return _database.GetGroupById(groupId);
			}
			return null;
		}

		public bool IsQuestOrGroupActive(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				return false;
			}
			if (IsQuestActive(id))
			{
				return true;
			}
			if (IsGroupActive(id))
			{
				return true;
			}
			return false;
		}

		private void LogQuest(string message, LogType logType = LogType.Log)
		{
			UtilityMethods.Log("[QuestManager] " + message, logType);
		}

		private string BuildQuestLoadSummary(QuestSaveData data)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("[Load] START");
			stringBuilder.AppendLine("Main Group From Save: " + GetGroupName(data.CurrentMainGroupId));
			stringBuilder.AppendLine($"Completed Quests From Save: {data.CompletedQuestIds.Count}");
			AppendQuestIdList(stringBuilder, data.CompletedQuestIds, "  ? ");
			stringBuilder.AppendLine($"Completed Groups From Save: {data.CompletedGroupIds.Count}");
			AppendGroupIdList(stringBuilder, data.CompletedGroupIds, "  > ");
			stringBuilder.AppendLine($"Progress Quests From Save: {data.QuestProgresses.Count}");
			AppendProgressList(stringBuilder, data.QuestProgresses, "  › ");
			stringBuilder.AppendLine($"Active Side Groups From Save: {data.ActiveSideGroupIds.Count}");
			AppendGroupIdList(stringBuilder, data.ActiveSideGroupIds, "  • ");
			return stringBuilder.ToString();
		}

		private string BuildQuestSaveSummary(QuestSaveData data)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("[Save] CREATED");
			stringBuilder.AppendLine("Current Main Group: " + GetGroupName(data.CurrentMainGroupId));
			stringBuilder.AppendLine($"Completed Quests: {data.CompletedQuestIds.Count}");
			AppendQuestIdList(stringBuilder, data.CompletedQuestIds, "  ? ");
			stringBuilder.AppendLine($"Completed Groups: {data.CompletedGroupIds.Count}");
			AppendGroupIdList(stringBuilder, data.CompletedGroupIds, "  > ");
			stringBuilder.AppendLine($"Progress Quests: {data.QuestProgresses.Count}");
			AppendProgressList(stringBuilder, data.QuestProgresses, "  › ");
			stringBuilder.AppendLine($"Active Side Groups: {data.ActiveSideGroupIds.Count}");
			AppendGroupIdList(stringBuilder, data.ActiveSideGroupIds, "  • ");
			return stringBuilder.ToString();
		}

		private string BuildQuestStateSummary(string header)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine(header);
			stringBuilder.AppendLine("Current Main Group: " + GetGroupLabel(_currentMainGroup));
			stringBuilder.AppendLine($"Completed Quests: {_completedQuestIds.Count}");
			stringBuilder.AppendLine($"Progress Quests: {_questProgressById.Count}");
			stringBuilder.AppendLine($"Active Side Groups: {_activeSideGroupIds.Count}");
			return stringBuilder.ToString();
		}

		private void AppendQuestIdList(StringBuilder sb, List<string> questIds, string bullet)
		{
			if (questIds == null || questIds.Count == 0)
			{
				sb.AppendLine(bullet + "None");
				return;
			}
			foreach (string questId in questIds)
			{
				sb.AppendLine(bullet + GetQuestName(questId));
			}
		}

		private void AppendGroupIdList(StringBuilder sb, List<string> groupIds, string bullet)
		{
			if (groupIds == null || groupIds.Count == 0)
			{
				sb.AppendLine(bullet + "None");
				return;
			}
			foreach (string groupId in groupIds)
			{
				sb.AppendLine(bullet + GetGroupName(groupId));
			}
		}

		private void AppendProgressList(StringBuilder sb, List<QuestProgressSaveEntry> progresses, string bullet)
		{
			if (progresses == null || progresses.Count == 0)
			{
				sb.AppendLine(bullet + "None");
				return;
			}
			foreach (QuestProgressSaveEntry progress in progresses)
			{
				if (progress != null)
				{
					QuestSO questSO = ((_database != null) ? _database.GetQuestById(progress.QuestId) : null);
					int num = ((!(questSO != null)) ? 1 : questSO.TargetAmount);
					sb.AppendLine($"{bullet}{GetQuestName(progress.QuestId)}: {progress.Value}/{num}");
				}
			}
		}

		private string GetGroupName(string groupId)
		{
			if (string.IsNullOrWhiteSpace(groupId))
			{
				return "None";
			}
			QuestGroupSO questGroupSO = ((_database != null) ? _database.GetGroupById(groupId) : null);
			if (!(questGroupSO != null))
			{
				return "MissingGroup (" + groupId + ")";
			}
			return GetGroupLabel(questGroupSO);
		}

		private string GetQuestName(string questId)
		{
			if (string.IsNullOrWhiteSpace(questId))
			{
				return "None";
			}
			QuestSO questSO = ((_database != null) ? _database.GetQuestById(questId) : null);
			if (!(questSO != null))
			{
				return "MissingQuest (" + questId + ")";
			}
			return GetQuestLabel(questSO);
		}

		private static string GetQuestLabel(QuestSO quest)
		{
			if (quest == null)
			{
				return "None";
			}
			return quest.Title + " (" + quest.Id + ")";
		}

		private static string GetGroupLabel(QuestGroupSO group)
		{
			if (group == null)
			{
				return "None";
			}
			return group.Title + " (" + group.Id + ")";
		}

		public bool DebugTryPrepareMainQuestForTesting(int mainGroupNumber, int mainQuestNumber, out string message)
		{
			if (!CanWriteState)
			{
				message = "[qtest] Only the server or offline instance can change quest test state.";
				return false;
			}
			if (_database == null)
			{
				message = "[qtest] Quest database is not assigned.";
				return false;
			}
			if (mainGroupNumber < 1 || mainGroupNumber > _database.MainGroups.Count)
			{
				message = $"[qtest] Invalid MG number. Use 1..{_database.MainGroups.Count}.";
				return false;
			}
			QuestGroupSO questGroupSO = _database.MainGroups[mainGroupNumber - 1];
			if (questGroupSO == null || questGroupSO.Quests == null)
			{
				message = $"[qtest] MG {mainGroupNumber:000} is missing or has no quest list.";
				return false;
			}
			if (mainQuestNumber < 1 || mainQuestNumber > questGroupSO.Quests.Count)
			{
				message = $"[qtest] Invalid MQ number for MG {mainGroupNumber:000}. Use 1..{questGroupSO.Quests.Count}.";
				return false;
			}
			QuestSO questSO = questGroupSO.Quests[mainQuestNumber - 1];
			if (questSO == null)
			{
				message = $"[qtest] MG {mainGroupNumber:000} MQ {mainQuestNumber:00} is null.";
				return false;
			}
			return DebugApplyIsolatedMainQuestState(mainGroupNumber - 1, mainQuestNumber - 1, questGroupSO, questSO, out message);
		}

		public bool DebugTryPrepareMainQuestForTesting(QuestId questId, out string message)
		{
			if (!CanWriteState)
			{
				message = "[qtest] Only the server or offline instance can change quest test state.";
				return false;
			}
			if (_database == null)
			{
				message = "[qtest] Quest database is not assigned.";
				return false;
			}
			QuestSO questSO = QuestReferenceHub.Instance?.GetQuest(questId);
			QuestGroupSO questGroupSO = ((questSO != null) ? _database.GetGroupOfQuest(questSO.Id) : null);
			if (questSO == null || questGroupSO == null)
			{
				message = $"[qtest] Main quest could not be resolved: {questId}.";
				return false;
			}
			int num = -1;
			for (int i = 0; i < _database.MainGroups.Count; i++)
			{
				if (_database.MainGroups[i] == questGroupSO)
				{
					num = i;
					break;
				}
			}
			int num2 = -1;
			for (int j = 0; j < questGroupSO.Quests.Count; j++)
			{
				if (questGroupSO.Quests[j] == questSO)
				{
					num2 = j;
					break;
				}
			}
			if (num < 0 || num2 < 0)
			{
				message = $"[qtest] Quest is not registered in a main MG: {questId}.";
				return false;
			}
			return DebugApplyIsolatedMainQuestState(num, num2, questGroupSO, questSO, out message);
		}

		public string DebugBuildMainQuestTestList()
		{
			if (_database == null)
			{
				return "[qtest] Quest database is not assigned.";
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("[qtest] Main Quest Groups");
			for (int i = 0; i < _database.MainGroups.Count; i++)
			{
				QuestGroupSO questGroupSO = _database.MainGroups[i];
				string arg = ((questGroupSO != null) ? questGroupSO.Title : "<null>");
				int valueOrDefault = (questGroupSO?.Quests?.Count).GetValueOrDefault();
				stringBuilder.AppendLine($"  MG {i + 1:000} | MQ Count: {valueOrDefault:00} | {arg}");
			}
			return stringBuilder.ToString();
		}

		private bool DebugApplyIsolatedMainQuestState(int targetGroupIndex, int targetQuestIndex, QuestGroupSO targetGroup, QuestSO targetQuest, out string message)
		{
			QuestSaveData questSaveData = new QuestSaveData
			{
				CurrentMainGroupId = targetGroup.Id,
				ActiveSideGroupIds = new List<string>(),
				CompletedGroupIds = new List<string>(),
				CompletedQuestIds = new List<string>(),
				QuestProgresses = new List<QuestProgressSaveEntry>()
			};
			for (int i = 0; i < targetGroupIndex; i++)
			{
				QuestGroupSO questGroupSO = _database.MainGroups[i];
				if (questGroupSO == null)
				{
					continue;
				}
				questSaveData.CompletedGroupIds.Add(questGroupSO.Id);
				if (questGroupSO.Quests == null)
				{
					continue;
				}
				foreach (QuestSO quest in questGroupSO.Quests)
				{
					if (quest != null && !string.IsNullOrWhiteSpace(quest.Id))
					{
						questSaveData.CompletedQuestIds.Add(quest.Id);
					}
				}
			}
			for (int j = 0; j < targetGroup.Quests.Count; j++)
			{
				if (j != targetQuestIndex)
				{
					QuestSO questSO = targetGroup.Quests[j];
					if (questSO != null && !string.IsNullOrWhiteSpace(questSO.Id))
					{
						questSaveData.CompletedQuestIds.Add(questSO.Id);
					}
				}
			}
			ApplySnapshot(questSaveData, raiseEvents: false);
			MainGroupChanged?.Invoke(_currentMainGroup);
			RaiseQuestStateChanged(emitStartedAnalytics: false);
			BroadcastSnapshotToClients();
			message = $"[qtest] Active: MG {targetGroupIndex + 1:000} MQ {targetQuestIndex + 1:00} | " + targetQuest.Title + " (" + targetQuest.Id + ")";
			return true;
		}

		public bool DebugTryCompleteCurrentMainQuest(out string message)
		{
			QuestSO questSO = DebugGetFirstIncompleteQuestInCurrentMainGroup();
			if (questSO == null)
			{
				message = "[tuto] No incomplete quest found in current main group.";
				return false;
			}
			if (!RequestCompleteQuest(questSO.Id))
			{
				message = "[tuto] Failed to complete current quest: " + DebugGetQuestLabel(questSO);
				return false;
			}
			message = "[tuto] Completed current quest: " + DebugGetQuestLabel(questSO);
			return true;
		}

		public bool DebugTryCompleteCurrentMainGroup(out string message)
		{
			if (_currentMainGroup == null)
			{
				message = "[tuto] No current main group found.";
				return false;
			}
			if (_currentMainGroup.Quests == null || _currentMainGroup.Quests.Count == 0)
			{
				message = "[tuto] Current main group has no quests: " + DebugGetGroupLabel(_currentMainGroup);
				return false;
			}
			string text = DebugGetGroupLabel(_currentMainGroup);
			int num = 0;
			int num2 = 0;
			List<QuestSO> list = new List<QuestSO>();
			foreach (QuestSO quest in _currentMainGroup.Quests)
			{
				if (quest == null)
				{
					num2++;
				}
				else if (_completedQuestIds.Contains(quest.Id))
				{
					num2++;
				}
				else
				{
					list.Add(quest);
				}
			}
			if (list.Count == 0)
			{
				message = "[tuto] Current main group is already completed: " + text;
				return false;
			}
			foreach (QuestSO item in list)
			{
				if (RequestCompleteQuest(item.Id))
				{
					num++;
				}
				else
				{
					num2++;
				}
			}
			message = $"[tuto] Completed current main group: {text}. Completed={num}, Skipped={num2}";
			return num > 0;
		}

		public string DebugBuildCurrentQuestStatus()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("[tuto] Quest Status");
			stringBuilder.AppendLine("Current Main Group: " + DebugGetGroupLabel(_currentMainGroup));
			if (_currentMainGroup == null || _currentMainGroup.Quests == null || _currentMainGroup.Quests.Count == 0)
			{
				stringBuilder.AppendLine("Main Quests: None");
				return stringBuilder.ToString();
			}
			stringBuilder.AppendLine("Main Quests:");
			foreach (QuestSO quest in _currentMainGroup.Quests)
			{
				if (!(quest == null))
				{
					bool num = _completedQuestIds.Contains(quest.Id);
					int questProgress = GetQuestProgress(quest);
					int questTarget = GetQuestTarget(quest);
					string text = (num ? "Completed" : "Active");
					stringBuilder.AppendLine($"  - {text}: {quest.Title} ({quest.Id}) [{questProgress}/{questTarget}]");
				}
			}
			return stringBuilder.ToString();
		}

		private QuestSO DebugGetFirstIncompleteQuestInCurrentMainGroup()
		{
			if (_currentMainGroup == null || _currentMainGroup.Quests == null)
			{
				return null;
			}
			foreach (QuestSO quest in _currentMainGroup.Quests)
			{
				if (!(quest == null) && !_completedQuestIds.Contains(quest.Id))
				{
					return quest;
				}
			}
			return null;
		}

		private static string DebugGetQuestLabel(QuestSO quest)
		{
			if (quest == null)
			{
				return "None";
			}
			return quest.Title + " (" + quest.Id + ")";
		}

		private static string DebugGetGroupLabel(QuestGroupSO group)
		{
			if (group == null)
			{
				return "None";
			}
			return group.Title + " (" + group.Id + ")";
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3835413954u, __rpc_handler_3835413954, "CompleteCurrentMainGroupIfContainsQuestRpc", RpcInvokePermission.Everyone);
			__registerRpc(2693880456u, __rpc_handler_2693880456, "CompleteQuestRpc", RpcInvokePermission.Everyone);
			__registerRpc(2207900302u, __rpc_handler_2207900302, "AddQuestProgressRpc", RpcInvokePermission.Everyone);
			__registerRpc(1631450048u, __rpc_handler_1631450048, "SetQuestProgressAtLeastRpc", RpcInvokePermission.Everyone);
			__registerRpc(1065164540u, __rpc_handler_1065164540, "StartSideGroupRpc", RpcInvokePermission.Everyone);
			__registerRpc(3315873935u, __rpc_handler_3315873935, "RequestSnapshotRpc", RpcInvokePermission.Everyone);
			__registerRpc(1835669337u, __rpc_handler_1835669337, "SyncQuestSnapshotRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3835413954(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				string s = null;
				if (value)
				{
					reader.ReadValueSafe(out s, false);
				}
				((QuestManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((QuestManager)target).CompleteCurrentMainGroupIfContainsQuestRpc(s);
				((QuestManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2693880456(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				string s = null;
				if (value)
				{
					reader.ReadValueSafe(out s, false);
				}
				((QuestManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((QuestManager)target).CompleteQuestRpc(s);
				((QuestManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2207900302(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				string s = null;
				if (value)
				{
					reader.ReadValueSafe(out s, false);
				}
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				((QuestManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((QuestManager)target).AddQuestProgressRpc(s, value2);
				((QuestManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1631450048(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				string s = null;
				if (value)
				{
					reader.ReadValueSafe(out s, false);
				}
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				((QuestManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((QuestManager)target).SetQuestProgressAtLeastRpc(s, value2);
				((QuestManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1065164540(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				string s = null;
				if (value)
				{
					reader.ReadValueSafe(out s, false);
				}
				((QuestManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((QuestManager)target).StartSideGroupRpc(s);
				((QuestManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3315873935(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((QuestManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((QuestManager)target).RequestSnapshotRpc();
				((QuestManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1835669337(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				string s = null;
				if (value)
				{
					reader.ReadValueSafe(out s, false);
				}
				((QuestManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((QuestManager)target).SyncQuestSnapshotRpc(s);
				((QuestManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "QuestManager";
		}
	}
}
