using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen.ServedDishes;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Core.Settings;
using PaperPixelGames.Core.SteamAchievements;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States.Game;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking;
using PaperPixelGames.Networking.Chat;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityServiceLocator;

[DefaultExecutionOrder(-10000)]
public class RpcRelay : NetworkBehaviour
{
	[SerializeField]
	private ItemDatabaseSO _itemDatabaseSO;

	[SerializeField]
	private GameStartGate _gameStartGate;

	[SerializeField]
	[Min(1f)]
	private float _bootstrapApplyTimeoutSeconds = 60f;

	private byte[] _pendingBootstrapBlob;

	private byte[] _lastAppliedBootstrapBlob;

	private Coroutine _bootstrapApplyCoroutine;

	private BootstrapApplyOperation _bootstrapOperation;

	private BootstrapLiveUpdateBuffer _bootstrapLiveUpdates;

	private uint _bootstrapSequence;

	private readonly BootstrapSnapshotLedger _serverSnapshots = new BootstrapSnapshotLedger();

	private readonly BootstrapRevisionState _clientRevision = new BootstrapRevisionState();

	private readonly Dictionary<ulong, BootstrapTransferSender> _serverTransfers = new Dictionary<ulong, BootstrapTransferSender>();

	private readonly List<ulong> _transferClients = new List<ulong>(4);

	private readonly BootstrapTransferReceiver _clientTransfer = new BootstrapTransferReceiver();

	private double _nextTransferProgressReport;

	private double _clientBootstrapDeadline;

	public NetworkVariable<ToolDataNet> ToolDataNV = new NetworkVariable<ToolDataNet>();

	public NetworkVariable<bool> CompleteBlueMarket = new NetworkVariable<bool>(value: false);

	public NetworkVariable<bool> BlueMarketToolsAvailable = new NetworkVariable<bool>(value: false);

	public NetworkVariable<bool> CompleteOpenSignTuto = new NetworkVariable<bool>(value: false);

	public NetworkVariable<bool> CompleteTimeTuto = new NetworkVariable<bool>(value: false);

	public NetworkVariable<bool> PlayerCollisionEnabled = new NetworkVariable<bool>(value: false);

	public static RpcRelay ActiveInstance { get; private set; }

	private static bool SessionWorkBlocked
	{
		get
		{
			if (!Singleton<GameSessionLifecycle>.TryGetInstance(out var instance) || !instance.ReturnToMenuInProgress)
			{
				if (Singleton<MultiplayerCoordinator>.TryGetInstance(out var instance2))
				{
					return instance2.IsClientSessionWorkBlocked;
				}
				return false;
			}
			return true;
		}
	}

	public bool ClientReplayReady
	{
		get
		{
			if (_bootstrapLiveUpdates != null)
			{
				if (!_bootstrapLiveUpdates.HasFailure)
				{
					return _bootstrapLiveUpdates.PendingCount == 0;
				}
				return false;
			}
			return true;
		}
	}

	public GameStartGate GameStartGate => _gameStartGate;

	public void CancelClientSessionWork()
	{
		if (!base.IsServer)
		{
			StopBootstrapWork();
			_lastAppliedBootstrapBlob = null;
			ResetSnapshotSession();
			BootstrapLiveUpdateBuffer.Reset();
		}
	}

	public void ReleaseBootstrapHistory()
	{
		_bootstrapLiveUpdates?.Dispose();
		_bootstrapLiveUpdates = null;
		_clientBootstrapDeadline = 0.0;
	}

	private void Update()
	{
		if (!base.IsSpawned || SessionWorkBlocked)
		{
			return;
		}
		if (base.IsServer)
		{
			PumpBootstrapTransfers();
		}
		else
		{
			if (_pendingBootstrapBlob != null || _clientBootstrapDeadline == 0.0)
			{
				return;
			}
			if (_clientTransfer.Receiving)
			{
				if (Time.realtimeSinceStartupAsDouble >= _clientBootstrapDeadline)
				{
					FailClientTransfer($"Snapshot transfer exceeded its admission deadline: bytes={_clientTransfer.ReceivedBytes}/{_clientTransfer.Header.TotalBytes}, chunks={_clientTransfer.NextChunk}/{_clientTransfer.Header.ChunkCount}.");
				}
				else if (_bootstrapLiveUpdates != null && _bootstrapLiveUpdates.HasFailure)
				{
					FailClientTransfer(_bootstrapLiveUpdates.Failure);
				}
				else if (Time.realtimeSinceStartupAsDouble >= _nextTransferProgressReport)
				{
					_nextTransferProgressReport = Time.realtimeSinceStartupAsDouble + 5.0;
					MultiplayerDiagnostics.Record("snapshot_transfer_progress", $"snapshotId={_clientTransfer.Header.SnapshotId} bytes={_clientTransfer.ReceivedBytes}/{_clientTransfer.Header.TotalBytes} chunks={_clientTransfer.NextChunk}/{_clientTransfer.Header.ChunkCount}", ulong.MaxValue);
				}
			}
			else if (Time.realtimeSinceStartupAsDouble >= _clientBootstrapDeadline)
			{
				FailClientBootstrap("Timed out waiting for bootstrap gameplay permission.");
			}
			else
			{
				_bootstrapLiveUpdates?.DrainFrame(() => this != null && base.IsSpawned && !SessionWorkBlocked);
				if (_bootstrapLiveUpdates != null && _bootstrapLiveUpdates.HasFailure)
				{
					FailClientBootstrap(_bootstrapLiveUpdates.Failure);
				}
			}
		}
	}

	private void ResetSnapshotSession()
	{
		_serverSnapshots.Reset();
		_serverTransfers.Clear();
		_transferClients.Clear();
		_clientTransfer.Reset();
		_clientRevision.Reset();
		_clientBootstrapDeadline = 0.0;
	}

	public void ServerDeferBootstrap(ulong clientId)
	{
		_serverTransfers.Remove(clientId);
		_serverSnapshots.Defer(clientId);
	}

	public void ServerForgetBootstrap(ulong clientId)
	{
		_serverTransfers.Remove(clientId);
		_serverSnapshots.Remove(clientId);
	}

	public void ServerSendBeginGameplay(ulong clientId)
	{
		if (_serverSnapshots.TryGet(clientId, out var context, out var _))
		{
			BeginGameplayForSingleClientRpc(context.SessionEpoch, context.SnapshotId, context.WorldRevision, base.RpcTarget.Single(clientId, RpcTargetUse.Temp));
		}
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();
		StopBootstrapWork();
		_lastAppliedBootstrapBlob = null;
		ResetSnapshotSession();
		ActiveInstance = this;
		BootstrapLiveUpdateBuffer.Reset();
		if (!base.IsServer)
		{
			_bootstrapLiveUpdates = new BootstrapLiveUpdateBuffer(1024, 1048576, retainHistory: true);
			_bootstrapLiveUpdates.Activate();
		}
		ServiceLocator.ForSceneOf(this).Register(this);
		NetworkVariable<ToolDataNet> toolDataNV = ToolDataNV;
		toolDataNV.OnValueChanged = (NetworkVariable<ToolDataNet>.OnValueChangedDelegate)Delegate.Combine(toolDataNV.OnValueChanged, new NetworkVariable<ToolDataNet>.OnValueChangedDelegate(OnToolDataNVChanged));
		NetworkVariable<bool> playerCollisionEnabled = PlayerCollisionEnabled;
		playerCollisionEnabled.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(playerCollisionEnabled.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnPlayerCollisionEnabledChanged));
		if (base.IsServer)
		{
			PlayerCollisionEnabled.Value = RuntimeSettings.PlayerCollisionEnabled;
			QuestManager.Instance?.ServerReconcileOwnedToolQuests(ToolDataNV.Value);
			ServerReconcileSharedCollectionAchievements();
		}
		RuntimeSettings.SetPlayerCollision(PlayerCollisionEnabled.Value, forceApply: true);
		if (base.IsServer && base.NetworkManager != null)
		{
			base.NetworkManager.OnClientDisconnectCallback += OnDebugSharkClientDisconnected;
		}
	}

	public override void OnNetworkDespawn()
	{
		StopBootstrapWork();
		_lastAppliedBootstrapBlob = null;
		ResetSnapshotSession();
		if (base.IsServer && base.NetworkManager != null)
		{
			base.NetworkManager.OnClientDisconnectCallback -= OnDebugSharkClientDisconnected;
		}
		base.OnNetworkDespawn();
		NetworkVariable<ToolDataNet> toolDataNV = ToolDataNV;
		toolDataNV.OnValueChanged = (NetworkVariable<ToolDataNet>.OnValueChangedDelegate)Delegate.Remove(toolDataNV.OnValueChanged, new NetworkVariable<ToolDataNet>.OnValueChangedDelegate(OnToolDataNVChanged));
		NetworkVariable<bool> playerCollisionEnabled = PlayerCollisionEnabled;
		playerCollisionEnabled.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(playerCollisionEnabled.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnPlayerCollisionEnabledChanged));
		if (ActiveInstance == this)
		{
			BootstrapLiveUpdateBuffer.Reset();
			ActiveInstance = null;
		}
	}

	public void HostSetPlayerCollision(bool enabled)
	{
		if (base.IsServer && !(base.NetworkManager == null) && base.NetworkManager.IsHost)
		{
			PlayerCollisionEnabled.Value = enabled;
		}
	}

	public override void OnDestroy()
	{
		StopBootstrapWork();
		if (ActiveInstance == this)
		{
			ResetSnapshotSession();
			BootstrapLiveUpdateBuffer.Reset();
			ActiveInstance = null;
		}
		base.OnDestroy();
	}

	private void OnPlayerCollisionEnabledChanged(bool previousValue, bool currentValue)
	{
		RuntimeSettings.SetPlayerCollision(currentValue, forceApply: true);
	}

	[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
	public void ServerSetDebugSharkRpc(bool enabled, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcParams rpcParams2 = rpcParams;
			RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
			{
				InvokePermission = RpcInvokePermission.Everyone
			};
			FastBufferWriter bufferWriter = __beginSendRpc(1018407709u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in enabled, default(FastBufferWriter.ForPrimitives));
			__endSendRpc(ref bufferWriter, 1018407709u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		if (base.IsServer)
		{
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!TryResolveDebugSharkSender(senderClientId, out var player))
			{
				UtilityMethods.Log($"[RpcRelay][SharkDebug] Request rejected. Sender player not found. ClientId={senderClientId}", LogType.Warning);
				return;
			}
			if (!SeaCreatureManager.TryGetActiveInstance(out var manager))
			{
				UtilityMethods.Log("[RpcRelay][SharkDebug] Request failed. SeaCreatureManager not found.", LogType.Warning);
				return;
			}
			bool flag = (enabled ? manager.ServerSpawnDebugSharkForPlayer(player) : manager.ServerKillDebugSharkForClient(senderClientId, "SharkOffCommand"));
			UtilityMethods.Log($"[RpcRelay][SharkDebug] Request completed. ClientId={senderClientId} Enabled={enabled} Success={flag}");
		}
	}

	private bool TryResolveDebugSharkSender(ulong senderClientId, out PPG_Player player)
	{
		player = null;
		if (base.NetworkManager == null || !base.NetworkManager.ConnectedClients.TryGetValue(senderClientId, out var value) || value.PlayerObject == null || !value.PlayerObject.TryGetComponent<PPG_Player>(out player))
		{
			return false;
		}
		if (player.IsSpawned)
		{
			return player.OwnerClientId == senderClientId;
		}
		return false;
	}

	private void OnDebugSharkClientDisconnected(ulong clientId)
	{
		ServerForgetBootstrap(clientId);
		if (base.IsServer && SeaCreatureManager.TryGetActiveInstance(out var manager))
		{
			manager.ServerKillDebugSharkForClient(clientId, "OwnerDisconnected");
		}
	}

	private void OnToolDataNVChanged(ToolDataNet oldData, ToolDataNet newData)
	{
		UtilityMethods.Log("[RpcRelay] OnToolDataNVChanged");
		if (base.IsServer)
		{
			QuestManager.Instance?.ServerReconcileOwnedToolQuests(newData);
			if (newData.CompassUnlocked && newData.CompassLevel >= 4)
			{
				ServerUnlockSteamAchievementForAllClients("ACH_PIECES_OF_A_MYSTERY");
			}
		}
		EventBus<ToolDataEvent>.Raise(new ToolDataEvent
		{
			ToolDataNet = newData
		});
	}

	public void ServerApplyToolsFromSave()
	{
		if (!base.IsServer)
		{
			return;
		}
		ToolDataNet value = ToolDataNV.Value;
		value.SyncRevision = (ushort)(ToolDataNV.Value.SyncRevision + 1);
		ToolDataNV.Value = value;
		if (value.CompassUnlocked)
		{
			RestaurantManager restaurantManager = Singleton<GameProgressManager>.Instance.RestaurantManager;
			byte stepIndex = (byte)((restaurantManager.CurrentCompassTargetLevel == value.CompassLevel) ? restaurantManager.CurrentCompassTargetStepIndex : 0);
			if (!restaurantManager.ServerSetCompassTargetRouteState(value.CompassLevel, stepIndex))
			{
				restaurantManager.ServerSetCompassTargetFromLevel(value.CompassLevel);
			}
		}
	}

	public void ServerReconcileHarpoonUnlockFromQuest()
	{
		if (!base.IsServer)
		{
			return;
		}
		QuestReferenceHub instance = QuestReferenceHub.Instance;
		if (instance == null)
		{
			return;
		}
		if (!instance.IsCompleted(QuestId.UnlockTheHarpoon))
		{
			bool value = !CompleteBlueMarket.Value && !ToolDataNV.Value.HarpoonUnlocked && (instance.IsActive(QuestId.UnlockTheHarpoon) || instance.IsCompleted(QuestId.TalkToTheFisherman));
			BlueMarketToolsAvailable.Value = value;
			return;
		}
		CompleteBlueMarket.Value = true;
		BlueMarketToolsAvailable.Value = false;
		ToolDataNet value2 = ToolDataNV.Value;
		if (!value2.HarpoonUnlocked || !value2.DivingEquipmentUnlocked)
		{
			UtilityMethods.Log("[RpcRelay] Restoring missing harpoon or diving equipment unlock from completed quest save.", LogType.Warning);
			value2.HarpoonUnlocked = true;
			value2.DivingEquipmentUnlocked = true;
			if (value2.DivingEquipmentLevel < 1)
			{
				value2.DivingEquipmentLevel = 1;
			}
			ToolDataNV.Value = value2;
		}
	}

	[Rpc(SendTo.Server)]
	public void ServerSpawnRpc(FixedString64Bytes id, ulong clientId, NetworkObjectReference interactorRef, bool select)
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
			FastBufferWriter bufferWriter = __beginSendRpc(2153083603u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in id, default(FastBufferWriter.ForFixedStrings));
			BytePacker.WriteValueBitPacked(bufferWriter, clientId);
			bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
			bufferWriter.WriteValueSafe(in select, default(FastBufferWriter.ForPrimitives));
			__endSendRpc(ref bufferWriter, 2153083603u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		ItemSO itemSO = _itemDatabaseSO.FindById(id.ToString());
		if (itemSO == null)
		{
			UtilityMethods.Log("[RpcRelay] ItemSO not found!");
			return;
		}
		NetworkObject networkObject = NetworkManager.Singleton.SpawnManager.InstantiateAndSpawn(itemSO.Prefab, clientId, destroyWithScene: true);
		if (!networkObject.TryGetComponent<BaseHoldable>(out var component))
		{
			UtilityMethods.Log("[RpcRelay] Spawned object is not contains BaseHoldable.cs!", LogType.Warning);
			networkObject.Despawn();
		}
		else
		{
			component.ServerLazyHold(clientId, interactorRef, select, hold: true);
		}
	}

	[Rpc(SendTo.Server)]
	public void ServerSpawnShopInventoryItemRpc(FixedString64Bytes id, NetworkObjectReference interactorRef, Vector3 fallbackDropPosition, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcParams rpcParams2 = rpcParams;
			RpcAttribute.RpcAttributeParams attributeParams = default;
			FastBufferWriter bufferWriter = __beginSendRpc(303762351u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in id, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
			bufferWriter.WriteValueSafe(in fallbackDropPosition);
			__endSendRpc(ref bufferWriter, 303762351u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		ulong senderClientId = rpcParams.Receive.SenderClientId;
		if (!interactorRef.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != senderClientId)
		{
			UtilityMethods.Log($"[RpcRelay][ShopDelivery] Spawn rejected. Interactor ownership mismatch. Sender={senderClientId}", LogType.Warning);
			return;
		}
		if (!IsFinite(fallbackDropPosition))
		{
			UtilityMethods.Log($"[RpcRelay][ShopDelivery] Spawn rejected. Fallback position is invalid. Sender={senderClientId}", LogType.Warning);
			return;
		}
		if (!(_itemDatabaseSO.FindById(id.ToString()) is HoldableSO holdableSO) || holdableSO.Prefab == null)
		{
			UtilityMethods.Log($"[RpcRelay][ShopDelivery] HoldableSO not found. ItemId={id}", LogType.Warning);
			return;
		}
		if (holdableSO is WholesaleSO wholesale && Singleton<GameProgressManager>.Instance != null && !Singleton<GameProgressManager>.Instance.IsWholesaleProgressionUnlocked(wholesale))
		{
			UtilityMethods.Log($"[RpcRelay][ShopDelivery] Wholesale spawn rejected by progression. ItemId={id}", LogType.Warning);
			return;
		}
		NetworkObject networkObject = NetworkManager.Singleton.SpawnManager.InstantiateAndSpawn(holdableSO.Prefab, senderClientId, destroyWithScene: true);
		if (networkObject == null || !networkObject.TryGetComponent<BaseHoldable>(out var component2))
		{
			UtilityMethods.Log($"[RpcRelay][ShopDelivery] Spawned object has no BaseHoldable. ItemId={id}", LogType.Warning);
			if (networkObject != null && networkObject.IsSpawned)
			{
				networkObject.Despawn();
			}
		}
		else
		{
			component2.ServerLazyHold(senderClientId, interactorRef, select: false, hold: true, useFallbackDropPosition: true, fallbackDropPosition, requireQuickSlot: true);
		}
	}

	private static bool IsFinite(Vector3 value)
	{
		if (!float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z))
		{
			return !float.IsInfinity(value.z);
		}
		return false;
	}

	[Rpc(SendTo.Everyone)]
	public void ClientSaveRpc()
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
			FastBufferWriter bufferWriter = __beginSendRpc(3776483117u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 3776483117u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			SaveResult saveResult;
			if (NetworkManager.Singleton.IsHost)
			{
				UtilityMethods.Log("[RpcRelay] ClientSaveRpc called on Host - Saving World + Local Profile");
				saveResult = Singleton<SaveManager>.Instance.SaveHostSession();
			}
			else
			{
				UtilityMethods.Log("[RpcRelay] ClientSaveRpc called on Client - Saving Client Profile");
				saveResult = Singleton<SaveManager>.Instance.SaveCurrentClientProfile();
			}
			ShowSaveToast(saveResult);
			if (NetworkManager.Singleton.IsHost && saveResult == SaveResult.Success)
			{
				GlobalChatManager.BroadcastLocalizedInfoFromServer(this, "chat.info.game_saved", "Game saved.");
			}
		}
	}

	private void ShowSaveToast(SaveResult result)
	{
		if (!(Singleton<NotifyManager>.Instance == null))
		{
			switch (result)
			{
			case SaveResult.Success:
				Singleton<NotifyManager>.Instance.ShowToast("Game saved successfully.", NotificationState.Success);
				break;
			case SaveResult.Skipped:
				Singleton<NotifyManager>.Instance.ShowToast("Save skipped.");
				break;
			case SaveResult.Failed:
				Singleton<NotifyManager>.Instance.ShowToast("Save failed.", NotificationState.Error);
				break;
			}
		}
	}

	[Rpc(SendTo.Everyone)]
	public void ClientGlobalChatInfoRpc(FixedString512Bytes message)
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
			FastBufferWriter bufferWriter = __beginSendRpc(2603772542u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in message, default(FastBufferWriter.ForFixedStrings));
			__endSendRpc(ref bufferWriter, 2603772542u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			Singleton<GlobalChatManager>.Instance?.AddInfoMessage(message.ToString());
		}
	}

	[Rpc(SendTo.Everyone)]
	public void ClientGlobalChatLocalizedRpc(FixedString128Bytes key, FixedString512Bytes fallback, FixedString128Bytes argument0, FixedString128Bytes argument1, FixedString128Bytes argument2, FixedString128Bytes argument3, byte argumentCount, byte messageType, byte tone)
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
			FastBufferWriter bufferWriter = __beginSendRpc(732087377u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in key, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in fallback, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in argument0, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in argument1, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in argument2, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in argument3, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in argumentCount, default(FastBufferWriter.ForPrimitives));
			bufferWriter.WriteValueSafe(in messageType, default(FastBufferWriter.ForPrimitives));
			bufferWriter.WriteValueSafe(in tone, default(FastBufferWriter.ForPrimitives));
			__endSendRpc(ref bufferWriter, 732087377u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			int num = Mathf.Clamp(argumentCount, 0, 4);
			string[] array = new string[num];
			if (num > 0)
			{
				array[0] = argument0.ToString();
			}
			if (num > 1)
			{
				array[1] = argument1.ToString();
			}
			if (num > 2)
			{
				array[2] = argument2.ToString();
			}
			if (num > 3)
			{
				array[3] = argument3.ToString();
			}
			ChatMessageType type = (ChatMessageType)((messageType <= 3) ? messageType : 2);
			Singleton<GlobalChatManager>.Instance?.AddLocalizedMessage(type, key.ToString(), fallback.ToString(), ResolveRestaurantChatTone(tone), GlobalChatManager.ResolveLocalizedArguments(array));
		}
	}

	[Rpc(SendTo.Everyone)]
	public void ClientGlobalChatRestaurantRpc(FixedString512Bytes message, byte tone)
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
			FastBufferWriter bufferWriter = __beginSendRpc(2903982800u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in message, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in tone, default(FastBufferWriter.ForPrimitives));
			__endSendRpc(ref bufferWriter, 2903982800u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			Singleton<GlobalChatManager>.Instance?.AddRestaurantMessage(message.ToString(), ResolveRestaurantChatTone(tone));
		}
	}

	[Rpc(SendTo.SpecifiedInParams)]
	public void ClientGlobalChatRestaurantTargetRpc(FixedString512Bytes message, byte tone, RpcParams rpcParams = default(RpcParams))
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
			FastBufferWriter bufferWriter = __beginSendRpc(1170027295u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in message, default(FastBufferWriter.ForFixedStrings));
			bufferWriter.WriteValueSafe(in tone, default(FastBufferWriter.ForPrimitives));
			__endSendRpc(ref bufferWriter, 1170027295u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			Singleton<GlobalChatManager>.Instance?.AddRestaurantMessage(message.ToString(), ResolveRestaurantChatTone(tone));
		}
	}

	private static RestaurantChatTone ResolveRestaurantChatTone(byte tone)
	{
		if (tone > 3)
		{
			return RestaurantChatTone.Info;
		}
		return (RestaurantChatTone)tone;
	}

	public void ServerUnlockSteamAchievementForAllClients(string achievementApiName)
	{
		if (!base.IsServer || !base.IsSpawned || base.NetworkManager == null)
		{
			return;
		}
		foreach (ulong connectedClientsId in base.NetworkManager.ConnectedClientsIds)
		{
			ServerUnlockSteamAchievementForClient(achievementApiName, connectedClientsId);
		}
	}

	public void ServerReconcileSharedCollectionAchievements()
	{
		if (!base.IsServer || !base.IsSpawned || base.NetworkManager == null)
		{
			return;
		}
		foreach (ulong connectedClientsId in base.NetworkManager.ConnectedClientsIds)
		{
			ServerReconcileSharedCollectionAchievementsForClient(connectedClientsId);
		}
	}

	private void ServerReconcileSharedCollectionAchievementsForClient(ulong clientId)
	{
		if (base.IsServer && base.IsSpawned && !(base.NetworkManager == null) && base.NetworkManager.ConnectedClients.ContainsKey(clientId))
		{
			ToolDataNet value = ToolDataNV.Value;
			if (value.CompassUnlocked && value.CompassLevel >= 4)
			{
				ServerUnlockSteamAchievementForClient("ACH_PIECES_OF_A_MYSTERY", clientId);
			}
			if (TabletNetworkHandler.Instance != null && TabletNetworkHandler.Instance.ServerIsFishBookComplete())
			{
				ServerUnlockSteamAchievementForClient("ACH_NO_MORE_QUESTION_MARKS", clientId);
			}
		}
	}

	public void ServerUnlockSteamAchievementForClient(string achievementApiName, ulong clientId)
	{
		if (base.IsServer && !string.IsNullOrWhiteSpace(achievementApiName))
		{
			if (clientId == ulong.MaxValue)
			{
				SteamAchievementService.Unlock(achievementApiName);
			}
			else
			{
				ClientUnlockSteamAchievementRpc(new FixedString64Bytes(achievementApiName), base.RpcTarget.Single(clientId, RpcTargetUse.Temp));
			}
		}
	}

	[Rpc(SendTo.SpecifiedInParams)]
	public void ClientUnlockSteamAchievementRpc(FixedString64Bytes achievementApiName, RpcParams rpcParams = default(RpcParams))
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
			FastBufferWriter bufferWriter = __beginSendRpc(1530242252u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in achievementApiName, default(FastBufferWriter.ForFixedStrings));
			__endSendRpc(ref bufferWriter, 1530242252u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			SteamAchievementService.Unlock(achievementApiName.ToString());
		}
	}

	[Rpc(SendTo.Server)]
	public void ServerLoadQuickSlotRpc(QuickSlotLoadPayload payload, NetworkObjectReference interactorRef, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcParams rpcParams2 = rpcParams;
			RpcAttribute.RpcAttributeParams attributeParams = default;
			FastBufferWriter bufferWriter = __beginSendRpc(4260445826u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in payload, default(FastBufferWriter.ForNetworkSerializable));
			bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
			__endSendRpc(ref bufferWriter, 4260445826u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		ulong senderClientId = rpcParams.Receive.SenderClientId;
		for (int i = 0; i < payload.Count; i++)
		{
			QuickSlotItemNet item = payload.Items[i];
			if (item.ItemId.IsEmpty)
			{
				continue;
			}
			ItemSO itemSO = _itemDatabaseSO.FindById(item.ItemId.ToString());
			if (itemSO == null)
			{
				UtilityMethods.Log("[RpcRelay] ServerLoadQuickSlotRpc - ItemSO not found!");
			}
			else
			{
				if (ShouldSkipQuickSlotLoadItem(itemSO))
				{
					continue;
				}
				if (item.HasCustomSushi)
				{
					TryLoadCustomSushiQuickSlotItem(item, senderClientId, interactorRef);
				}
				else if (!TryLoadDeadFishQuickSlotItem(itemSO, item, senderClientId, interactorRef))
				{
					if (!NetworkManager.Singleton.SpawnManager.InstantiateAndSpawn(itemSO.Prefab, senderClientId, destroyWithScene: true).TryGetComponent<BaseHoldable>(out var component))
					{
						UtilityMethods.Log("[RpcRelay] ServerLoadQuickSlotRpc - holdable not found!");
						continue;
					}
					component.LoadCustomData(item.CustomData);
					component.ServerLazyHold(senderClientId, interactorRef, select: false, hold: true);
					ServerRegisterLoadedQuickSlotStock(itemSO);
				}
			}
		}
	}

	private static bool ShouldSkipQuickSlotLoadItem(ItemSO itemSO)
	{
		Tray component;
		if (itemSO != null && itemSO.Prefab != null)
		{
			return itemSO.Prefab.TryGetComponent<Tray>(out component);
		}
		return false;
	}

	private static void ServerRegisterLoadedQuickSlotStock(ItemSO itemSO)
	{
		if (!(itemSO == null) && !string.IsNullOrEmpty(itemSO.Id) && (itemSO.ItemType == ItemType.RawIngredient || itemSO.ItemType == ItemType.ProcessedIngredient))
		{
			((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null)?.ServerAddStockRpc(itemSO.Id, 1);
		}
	}

	private static bool TryLoadCustomSushiQuickSlotItem(QuickSlotItemNet item, ulong senderId, NetworkObjectReference interactorRef)
	{
		CustomSushiPhysicalData customSushiPhysicalData = item.CustomSushi.ToData();
		NetworkObject networkObject = (Singleton<GameProgressManager>.Instance?.RestaurantManager)?.ResolveCustomSushiPhysicalPrefab(customSushiPhysicalData.Composition.SushiType);
		if (networkObject == null)
		{
			return false;
		}
		NetworkObject networkObject2 = UnityEngine.Object.Instantiate(networkObject);
		if (!networkObject2.TryGetComponent<ServedDishSushi>(out var component) || !component.ServerInitializeCustomSushi(customSushiPhysicalData.Composition, customSushiPhysicalData.IdentityKind, customSushiPhysicalData.RecipeId, customSushiPhysicalData.DisplayName, customSushiPhysicalData.Price))
		{
			UnityEngine.Object.Destroy(networkObject2.gameObject);
			return false;
		}
		networkObject2.SpawnWithOwnership(senderId, destroyWithScene: true);
		component.ServerLazyHold(senderId, interactorRef, select: false, hold: true);
		return true;
	}

	private bool TryLoadDeadFishQuickSlotItem(ItemSO itemSO, QuickSlotItemNet item, ulong senderId, NetworkObjectReference interactorRef)
	{
		if (!(itemSO is FishSO fishSO))
		{
			return false;
		}
		if (fishSO.DeadPrefab == null)
		{
			UtilityMethods.Log("[RpcRelay] ServerLoadQuickSlotRpc - DeadPrefab not found for fish " + fishSO.name + "!", LogType.Warning);
			return true;
		}
		NetworkObject networkObject = NetworkManager.Singleton.SpawnManager.InstantiateAndSpawn(fishSO.DeadPrefab, senderId, destroyWithScene: true);
		if (!networkObject.TryGetComponent<DeadFish>(out var component))
		{
			UtilityMethods.Log("[RpcRelay] ServerLoadQuickSlotRpc - DeadFish component not found!", LogType.Warning);
			networkObject.Despawn();
			return true;
		}
		FixedString64Bytes fixedString64Bytes = new FixedString64Bytes(fishSO.Id);
		component.FishId.Value = fixedString64Bytes;
		component.ServerSpawnGraphic(fixedString64Bytes);
		component.LoadCustomData(item.CustomData);
		component.ServerLazyHold(senderId, interactorRef, select: false, hold: true);
		return true;
	}

	[Rpc(SendTo.Server)]
	public void ServerCompleteTimeTutoRpc()
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
			FastBufferWriter bufferWriter = __beginSendRpc(1917338793u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 1917338793u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			CompleteTimeTuto.Value = true;
			Singleton<GameProgressManager>.Instance.CozyWeatherManager.ServerResumeTimeRpc();
		}
	}

	[Rpc(SendTo.Server)]
	public void ServerCompleteOpenSignTutoRpc()
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
			FastBufferWriter bufferWriter = __beginSendRpc(2171746553u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 2171746553u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			CompleteOpenSignTuto.Value = true;
		}
	}

	[Rpc(SendTo.Server)]
	public void ServerActivateBlueMarketToolsRpc()
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
			FastBufferWriter bufferWriter = __beginSendRpc(77049073u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 77049073u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!CompleteBlueMarket.Value)
			{
				BlueMarketToolsAvailable.Value = true;
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void ServerCompleteBlueMarketRpc()
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
			FastBufferWriter bufferWriter = __beginSendRpc(1696123403u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 1696123403u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!CompleteBlueMarket.Value)
			{
				CompleteBlueMarket.Value = true;
				BlueMarketToolsAvailable.Value = false;
				ToolDataNet value = ToolDataNV.Value;
				value.HarpoonLevel = 0;
				value.HarpoonUnlocked = true;
				value.DivingEquipmentUnlocked = true;
				value.DivingEquipmentLevel = 1;
				ToolDataNV.Value = value;
				QuestReferenceHub.Instance.QuestComplete(QuestId.UnlockTheHarpoon);
			}
		}
	}

	[Rpc(SendTo.Server)]
	public void ServerCompleteBlueMarket2Rpc()
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
			FastBufferWriter bufferWriter = __beginSendRpc(1667644177u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 1667644177u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			ToolDataNet value = ToolDataNV.Value;
			value.CompassLevel = 1;
			value.CompassUnlocked = true;
			ToolDataNV.Value = value;
			Singleton<GameProgressManager>.Instance.RestaurantManager.ServerSetCompassTargetFromLevel(value.CompassLevel);
		}
	}

	[Rpc(SendTo.Server)]
	public void ServerUpgradeCompassToNextLevelRpc()
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
			FastBufferWriter bufferWriter = __beginSendRpc(3392232515u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 3392232515u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			ServerTryUpgradeCompassToNextLevel();
		}
	}

	public bool ServerTryUpgradeCompassToNextLevel()
	{
		if (!base.IsServer)
		{
			return false;
		}
		ToolDataNet value = ToolDataNV.Value;
		if (value.CompassLevel == byte.MaxValue)
		{
			UtilityMethods.Log("[RpcRelay][Compass] Upgrade rejected. Compass level is already at byte max.", LogType.Warning);
			return false;
		}
		byte targetLevel = (byte)(value.CompassLevel + 1);
		return ServerTryUpgradeCompassToLevel(targetLevel);
	}

	public bool ServerTryUpgradeCompassToLevel(byte targetLevel)
	{
		if (!base.IsServer)
		{
			return false;
		}
		if (targetLevel == 0)
		{
			UtilityMethods.Log("[RpcRelay][Compass] Upgrade rejected. Target level 0 is invalid.", LogType.Warning);
			return false;
		}
		ToolDataNet value = ToolDataNV.Value;
		int num = value.CompassLevel + 1;
		if (targetLevel != num)
		{
			UtilityMethods.Log("[RpcRelay][Compass] Upgrade rejected. Compass part is out of order." + $"\n   CurrentLevel: {value.CompassLevel}" + $"\n   ExpectedLevel: {num}" + $"\n   TargetLevel: {targetLevel}", LogType.Warning);
			return false;
		}
		if (!Singleton<GameProgressManager>.Instance.RestaurantManager.TryGetCompassTargetIndexForLevel(targetLevel, out var _))
		{
			UtilityMethods.Log($"[RpcRelay][Compass] Upgrade rejected. No compass route exists for targetLevel={targetLevel}", LogType.Warning);
			return false;
		}
		value.CompassLevel = targetLevel;
		value.CompassUnlocked = true;
		ToolDataNV.Value = value;
		Singleton<GameProgressManager>.Instance.RestaurantManager.ServerSetCompassTargetFromLevel(value.CompassLevel);
		return true;
	}

	[Rpc(SendTo.Server)]
	public void ServerUpgradeRpc(ToolType toolType, byte targetLevelIndex)
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
			FastBufferWriter bufferWriter = __beginSendRpc(99542811u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in toolType, default(FastBufferWriter.ForEnums));
			bufferWriter.WriteValueSafe(in targetLevelIndex, default(FastBufferWriter.ForPrimitives));
			__endSendRpc(ref bufferWriter, 99542811u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		ToolDataNet value = ToolDataNV.Value;
		switch (toolType)
		{
		case ToolType.DivingEquipment:
			value.DivingEquipmentLevel = targetLevelIndex;
			value.DivingEquipmentUnlocked = true;
			break;
		case ToolType.Harpoon:
			value.HarpoonLevel = targetLevelIndex;
			value.HarpoonUnlocked = true;
			break;
		case ToolType.SeaScooter:
			value.ScooterLevel = targetLevelIndex;
			value.ScooterUnlocked = true;
			break;
		case ToolType.FishingRod:
			value.FishingRodLevel = targetLevelIndex;
			value.FishingRodUnlocked = true;
			break;
		case ToolType.Compass:
		{
			if (!Singleton<GameProgressManager>.Instance.RestaurantManager.TryGetCompassTargetIndexForLevel(targetLevelIndex, out var _))
			{
				return;
			}
			value.CompassLevel = targetLevelIndex;
			value.CompassUnlocked = true;
			break;
		}
		}
		ToolDataNV.Value = value;
		if (toolType == ToolType.Compass)
		{
			Singleton<GameProgressManager>.Instance.RestaurantManager.ServerSetCompassTargetFromLevel(value.CompassLevel);
		}
	}

	[Rpc(SendTo.Server)]
	public void ServerRequestBootstrapRpc(ulong sessionEpoch, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcParams rpcParams2 = rpcParams;
			RpcAttribute.RpcAttributeParams attributeParams = default;
			FastBufferWriter bufferWriter = __beginSendRpc(1152135776u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			BytePacker.WriteValueBitPacked(bufferWriter, sessionEpoch);
			__endSendRpc(ref bufferWriter, 1152135776u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsServer && base.IsSpawned && Singleton<MultiplayerCoordinator>.TryGetInstance(out var instance) && sessionEpoch != 0L && sessionEpoch == instance.SessionEpoch)
			{
				ServerSendBootstrap(rpcParams.Receive.SenderClientId);
			}
		}
	}

	[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
	private void SendBootstrapClientRpc(byte[] blob, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
			{
				InvokePermission = RpcInvokePermission.Server
			};
			FastBufferWriter bufferWriter = __beginSendRpc(2143993722u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			bool value = blob != null;
			bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
			if (value)
			{
				bufferWriter.WriteValueSafe(blob, default(FastBufferWriter.ForPrimitives));
			}
			__endSendRpc(ref bufferWriter, 2143993722u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		if (base.IsSpawned && !SessionWorkBlocked)
		{
			if (blob == null || blob.Length > 16384)
			{
				FailClientTransfer("Inline snapshot exceeds its transfer limit.");
			}
			else
			{
				ReceiveBootstrapBlob(blob);
			}
		}
	}

	public void ServerSendBootstrap(ulong senderClientId, bool refreshOnly = false)
	{
		if (refreshOnly && !_serverSnapshots.NeedsRefresh(senderClientId))
		{
			return;
		}
		if (!base.IsServer || !base.IsSpawned || _gameStartGate == null || !_gameStartGate.TryResolveBootstrapMode(senderClientId, out var mode))
		{
			MultiplayerDiagnostics.Record("bootstrap_request_not_admitted", "No ready, spawned admission with a capturable world.", senderClientId);
			return;
		}
		if (_serverSnapshots.TryGet(senderClientId, out var context, out var blob))
		{
			if (blob != null)
			{
				ServerDeliverBootstrap(senderClientId, context, blob);
			}
			return;
		}
		SaveManager instance = Singleton<SaveManager>.Instance;
		MultiplayerDiagnostics.Record("bootstrap_request_received", "", senderClientId);
		if (instance == null || !instance.TryGetCurrentSaveSlot(out var data))
		{
			ServerFailBootstrapRequest(senderClientId, "Current save slot is invalid.");
			return;
		}
		string text = instance.CurrentSaveSlotHostSteamID;
		if (string.IsNullOrWhiteSpace(text))
		{
			text = data.HostSteamID;
		}
		if (string.IsNullOrWhiteSpace(text) && Singleton<PlayersManager>.Instance != null && Singleton<PlayersManager>.Instance.TryGetPlayer(NetworkManager.Singleton.LocalClientId, out var player) && player != null)
		{
			text = player.SteamID.Value.ToString();
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			ServerFailBootstrapRequest(senderClientId, $"HostSteamID is empty. SlotId={data.SlotID}, Mode={mode}.");
			return;
		}
		instance.SaveHostSteamId(text);
		JoinSessionContext context2 = new JoinSessionContext
		{
			HostSteamId = text,
			SlotId = data.SlotID,
			Mode = mode,
			SessionEpoch = ((Singleton<MultiplayerCoordinator>.Instance != null) ? Singleton<MultiplayerCoordinator>.Instance.SessionEpoch : 0)
		};
		GameProgressManager instance2 = Singleton<GameProgressManager>.Instance;
		if (instance2 == null)
		{
			ServerFailBootstrapRequest(senderClientId, "GameProgressManager is null.");
			return;
		}
		UtilityMethods.Log("<b><color=#8FD14F>[Bootstrap]</color></b> ServerRequestBootstrap" + $"\n   SenderClientId: {senderClientId}" + $"\n   Mode: {mode}" + "\n   HostSteamID: " + context2.HostSteamId + $"\n   SlotId: {context2.SlotId}");
		double now = MultiplayerDiagnostics.Now;
		MultiplayerDiagnostics.Record("snapshot_build_begin", $"mode={mode}", senderClientId);
		byte[] array;
		try
		{
			if (!instance2.ServerCaptureBootstrap(senderClientId, mode, out var failReason))
			{
				ServerFailBootstrapRequest(senderClientId, failReason);
				return;
			}
			context2.SnapshotId = _serverSnapshots.AllocateId();
			context2.WorldRevision = BootstrapLiveUpdateBuffer.NextServerRevision();
			array = instance2.ServerBuildBootstrapBlob(context2);
			_serverSnapshots.Store(senderClientId, context2, array);
		}
		catch (Exception ex)
		{
			ServerFailBootstrapRequest(senderClientId, "Server snapshot build exception: " + ex.Message);
			return;
		}
		MultiplayerDiagnostics.Record("snapshot_built", FormattableString.Invariant($"bytes={((array != null) ? array.Length : 0)} buildMs={(MultiplayerDiagnostics.Now - now) * 1000.0:F1} mode={mode}"), senderClientId);
		MultiplayerDiagnostics.Record("snapshot_issued", $"mode={context2.Mode} snapshotId={context2.SnapshotId} worldRevision={context2.WorldRevision}", senderClientId);
		ServerDeliverBootstrap(senderClientId, context2, array);
	}

	private void ServerDeliverBootstrap(ulong clientId, JoinSessionContext context, byte[] blob)
	{
		if (!base.IsServer || !base.IsSpawned)
		{
			return;
		}
		if (base.NetworkManager != null && clientId == base.NetworkManager.LocalClientId)
		{
			ReceiveBootstrapBlob(blob);
			return;
		}
		if (blob.Length <= 16384)
		{
			SendBootstrapClientRpc(blob, base.RpcTarget.Single(clientId, RpcTargetUse.Temp));
			return;
		}
		if (_serverTransfers.TryGetValue(clientId, out var value))
		{
			if (value.Header.Context.SameSnapshot(context))
			{
				return;
			}
			_serverTransfers.Remove(clientId);
		}
		if (_serverTransfers.Count >= 4)
		{
			ServerFailBootstrapRequest(clientId, "Snapshot transfer capacity reached.");
			return;
		}
		try
		{
			BootstrapTransferSender bootstrapTransferSender = new BootstrapTransferSender(context, blob, Time.realtimeSinceStartupAsDouble + (double)Math.Max(_bootstrapApplyTimeoutSeconds, (context.Mode == JoinBootstrapMode.Initial) ? 240 : 120));
			_serverTransfers.Add(clientId, bootstrapTransferSender);
			MultiplayerDiagnostics.Record("snapshot_transfer_begin", $"snapshotId={context.SnapshotId} bytes={blob.Length} chunks={bootstrapTransferSender.Header.ChunkCount} chunkBytes={16384}", clientId);
			BeginBootstrapTransferClientRpc(bootstrapTransferSender.Header, base.RpcTarget.Single(clientId, RpcTargetUse.Temp));
		}
		catch (Exception ex)
		{
			ServerFailBootstrapRequest(clientId, "Snapshot transfer start failed: " + ex.Message);
		}
	}

	private void PumpBootstrapTransfers()
	{
		if (base.NetworkManager == null || !base.NetworkManager.IsListening || _serverTransfers.Count == 0)
		{
			return;
		}
		_transferClients.Clear();
		_transferClients.AddRange(_serverTransfers.Keys);
		int num = 0;
		foreach (ulong transferClient in _transferClients)
		{
			if (!_serverTransfers.TryGetValue(transferClient, out var value))
			{
				continue;
			}
			BootstrapTransferHeader header = value.Header;
			int index;
			byte[] chunk;
			uint checksum;
			if (!_serverSnapshots.Matches(transferClient, header.Epoch, header.SnapshotId, header.Revision))
			{
				_serverTransfers.Remove(transferClient);
			}
			else if (value.Expired(Time.realtimeSinceStartupAsDouble))
			{
				ServerFailBootstrapRequest(transferClient, $"Snapshot transfer acknowledgement timed out: snapshotId={header.SnapshotId}, sent={value.SentChunks}, acknowledged={value.AcknowledgedChunks}/{header.ChunkCount}.");
			}
			else if (num < 4 && value.TryTakeNext(out index, out chunk, out checksum))
			{
				num++;
				try
				{
					BootstrapChunkClientRpc(header.Epoch, header.SnapshotId, header.Revision, index, chunk, checksum, base.RpcTarget.Single(transferClient, RpcTargetUse.Temp));
				}
				catch (Exception ex)
				{
					ServerFailBootstrapRequest(transferClient, "Snapshot chunk send failed: " + ex.Message);
				}
			}
		}
	}

	[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
	private void AcknowledgeBootstrapTransferRpc(ulong epoch, ulong snapshotId, ulong revision, int nextChunk, byte windowChunks, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcParams rpcParams2 = rpcParams;
			RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
			{
				InvokePermission = RpcInvokePermission.Everyone
			};
			FastBufferWriter bufferWriter = __beginSendRpc(1099357261u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			BytePacker.WriteValueBitPacked(bufferWriter, epoch);
			BytePacker.WriteValueBitPacked(bufferWriter, snapshotId);
			BytePacker.WriteValueBitPacked(bufferWriter, revision);
			BytePacker.WriteValueBitPacked(bufferWriter, nextChunk);
			bufferWriter.WriteValueSafe(in windowChunks, default(FastBufferWriter.ForPrimitives));
			__endSendRpc(ref bufferWriter, 1099357261u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			HandleBootstrapTransferAck(rpcParams.Receive.SenderClientId, epoch, snapshotId, revision, nextChunk, windowChunks);
		}
	}

	private void HandleBootstrapTransferAck(ulong clientId, ulong epoch, ulong snapshotId, ulong revision, int nextChunk, byte windowChunks)
	{
		if (base.IsServer && base.IsSpawned && _serverSnapshots.Matches(clientId, epoch, snapshotId, revision) && _serverTransfers.TryGetValue(clientId, out var value) && value.Header.Matches(epoch, snapshotId, revision))
		{
			bool complete = value.Complete;
			if (!value.Acknowledge(nextChunk, windowChunks))
			{
				ServerFailBootstrapRequest(clientId, "Invalid snapshot transfer acknowledgement.");
			}
			else if (!complete && value.Complete)
			{
				MultiplayerDiagnostics.Record("snapshot_transfer_delivered", $"snapshotId={snapshotId} bytes={value.Header.TotalBytes} chunks={nextChunk}", clientId);
			}
		}
	}

	[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
	private void BeginBootstrapTransferClientRpc(BootstrapTransferHeader header, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
			{
				InvokePermission = RpcInvokePermission.Server
			};
			FastBufferWriter bufferWriter = __beginSendRpc(1967624731u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in header, default(FastBufferWriter.ForNetworkSerializable));
			__endSendRpc(ref bufferWriter, 1967624731u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		if (!base.IsSpawned || SessionWorkBlocked)
		{
			return;
		}
		ulong num = (Singleton<MultiplayerCoordinator>.TryGetInstance(out var instance) ? instance.SessionEpoch : 0);
		if (num == 0L || header.Epoch != num || (_clientRevision.Current.HasSnapshotIdentity && header.SnapshotId < _clientRevision.Current.SnapshotId))
		{
			return;
		}
		if (!header.IsValid)
		{
			FailClientTransfer("Invalid snapshot transfer header.");
			return;
		}
		JoinBootstrapCoordinator instance2 = Singleton<JoinBootstrapCoordinator>.Instance;
		switch (_clientRevision.Inspect(header.Context, num, instance2 != null && instance2.BootstrapCompleted))
		{
		case BootstrapRevisionDecision.Stale:
			break;
		case BootstrapRevisionDecision.Duplicate:
			if (!_clientTransfer.Header.SameHeader(header))
			{
				FailClientTransfer("Snapshot transfer identity was reused with different metadata.");
				break;
			}
			SendBootstrapTransferAck();
			if (_clientTransfer.Complete)
			{
				instance2?.ResendBootstrapCompletion();
			}
			break;
		default:
			FailClientTransfer("Snapshot transfer replacement is not valid for this admission.");
			break;
		case BootstrapRevisionDecision.Accept:
			if (PrepareClientSnapshot(header.Context))
			{
				if (!_clientTransfer.Begin(header))
				{
					FailClientTransfer("Snapshot receiver rejected the transfer.");
					break;
				}
				_nextTransferProgressReport = Time.realtimeSinceStartupAsDouble + 5.0;
				MultiplayerDiagnostics.Record("snapshot_transfer_receiving", $"snapshotId={header.SnapshotId} bytes={header.TotalBytes} chunks={header.ChunkCount} window={2}", ulong.MaxValue);
				SendBootstrapTransferAck();
			}
			break;
		}
	}

	private void SendBootstrapTransferAck()
	{
		BootstrapTransferHeader header = _clientTransfer.Header;
		AcknowledgeBootstrapTransferRpc(header.Epoch, header.SnapshotId, header.Revision, _clientTransfer.NextChunk, 2);
	}

	[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
	private void BootstrapChunkClientRpc(ulong epoch, ulong snapshotId, ulong revision, int index, byte[] chunk, uint checksum, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
			{
				InvokePermission = RpcInvokePermission.Server
			};
			FastBufferWriter bufferWriter = __beginSendRpc(2729660114u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			BytePacker.WriteValueBitPacked(bufferWriter, epoch);
			BytePacker.WriteValueBitPacked(bufferWriter, snapshotId);
			BytePacker.WriteValueBitPacked(bufferWriter, revision);
			BytePacker.WriteValueBitPacked(bufferWriter, index);
			bool value = chunk != null;
			bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
			if (value)
			{
				bufferWriter.WriteValueSafe(chunk, default(FastBufferWriter.ForPrimitives));
			}
			BytePacker.WriteValueBitPacked(bufferWriter, checksum);
			__endSendRpc(ref bufferWriter, 2729660114u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		if (!base.IsSpawned || SessionWorkBlocked || !Singleton<MultiplayerCoordinator>.TryGetInstance(out var instance) || epoch != instance.SessionEpoch)
		{
			return;
		}
		double now = MultiplayerDiagnostics.Now;
		switch (_clientTransfer.Accept(epoch, snapshotId, revision, index, chunk, checksum))
		{
		case BootstrapChunkResult.Ignored:
			break;
		case BootstrapChunkResult.Rejected:
			FailClientTransfer("Snapshot chunk validation failed.");
			break;
		default:
			SendBootstrapTransferAck();
			break;
		case BootstrapChunkResult.Complete:
		{
			byte[] array = _clientTransfer.TakeCompletedBlob();
			if (!JoinBootstrapHub.TryReadContext(array, out var context) || !context.SameSnapshot(_clientRevision.Current))
			{
				FailClientTransfer("Assembled snapshot header does not match the admitted transfer.");
				break;
			}
			SendBootstrapTransferAck();
			MultiplayerDiagnostics.Record("snapshot_transfer_complete", FormattableString.Invariant($"snapshotId={snapshotId} bytes={array.Length} chunks={_clientTransfer.NextChunk} finalChunkValidationMs={(MultiplayerDiagnostics.Now - now) * 1000.0:F2}"), ulong.MaxValue);
			MultiplayerDiagnostics.Record("snapshot_received", $"bytes={array.Length} chunked=True", ulong.MaxValue);
			StartClientSnapshotApply(array);
			break;
		}
		}
	}

	private void FailClientTransfer(string reason)
	{
		FailClientBootstrap(reason);
		if (!base.IsServer && Singleton<GameSessionLifecycle>.TryGetInstance(out var instance) && !instance.ReturnToMenuInProgress)
		{
			instance.ReturnToMainMenu(ReturnToMainMenuRequest.ConnectionFailed(MultiplayerFailure.Create(MultiplayerFailureCode.BootstrapFailed, MultiplayerFailureStage.Bootstrap, reason)));
		}
	}

	private IEnumerator CoApplyBootstrapWhenReady(uint sequence, long attempt, ulong epoch)
	{
		double deadline = _clientBootstrapDeadline;
		WaitForSecondsRealtime retryDelay = new WaitForSecondsRealtime(0.25f);
		double applyStarted = MultiplayerDiagnostics.Now;
		double nextWaitReport = applyStarted;
		double injectedDelay = ((!base.IsServer && _clientRevision.Current.Mode == JoinBootstrapMode.Initial) ? BootstrapDevelopmentDelay.InitialSeconds : 0.0);
		Func<bool> isCurrent = () => IsBootstrapCurrent(sequence, attempt, epoch);
		MultiplayerDiagnostics.Record("snapshot_apply_begin", FormattableString.Invariant($"remainingSeconds={Math.Max(0.0, deadline - Time.realtimeSinceStartupAsDouble):F1} sliced={!base.IsServer} sequence={sequence} snapshotId={_clientRevision.Current.SnapshotId} worldRevision={_clientRevision.Current.WorldRevision}"), ulong.MaxValue);
		while (Time.realtimeSinceStartupAsDouble < deadline)
		{
			if (!isCurrent())
			{
				StopBootstrapWork(stopCoroutine: false);
				yield break;
			}
			if (_bootstrapLiveUpdates != null && _bootstrapLiveUpdates.HasFailure)
			{
				FailClientBootstrap(_bootstrapLiveUpdates.Failure, stopCoroutine: false);
				yield break;
			}
			GameProgressManager instance = Singleton<GameProgressManager>.Instance;
			JoinBootstrapCoordinator coordinator = Singleton<JoinBootstrapCoordinator>.Instance;
			if (instance == null || coordinator == null)
			{
				ReportBootstrapWait("GameProgressManager or JoinBootstrapCoordinator missing", ref nextWaitReport);
				yield return retryDelay;
				continue;
			}
			try
			{
				if (_bootstrapOperation == null)
				{
					double num = BootstrapApplyOperation.MonotonicSeconds();
					_bootstrapOperation = instance.CreateBootstrapApply(_pendingBootstrapBlob, epoch, isCurrent, !base.IsServer);
					MultiplayerDiagnostics.Record("snapshot_prepare_complete", FormattableString.Invariant($"prepareMs={(BootstrapApplyOperation.MonotonicSeconds() - num) * 1000.0:F2} sequence={sequence}"), ulong.MaxValue);
				}
			}
			catch (Exception ex)
			{
				FailClientBootstrap(ex.Message, stopCoroutine: false);
				yield break;
			}
			bool wasComplete = _bootstrapOperation.Completed;
			if (!wasComplete)
			{
				_bootstrapOperation.AdvanceFrame();
			}
			if (injectedDelay > 0.0)
			{
				MultiplayerDiagnostics.Record("snapshot_initial_delay_injected", $"seconds={injectedDelay}", ulong.MaxValue);
				double resumeAt = Time.realtimeSinceStartupAsDouble + injectedDelay;
				injectedDelay = 0.0;
				while (isCurrent() && Time.realtimeSinceStartupAsDouble < resumeAt)
				{
					yield return null;
				}
			}
			if (!isCurrent())
			{
				StopBootstrapWork(stopCoroutine: false);
				yield break;
			}
			BootstrapApplyOperation bootstrapOperation = _bootstrapOperation;
			BootstrapApplyResult result = bootstrapOperation.Result;
			if (bootstrapOperation.Completed && !result.Success)
			{
				FailClientBootstrap(result.FailReason, stopCoroutine: false);
				yield break;
			}
			if (!bootstrapOperation.Completed)
			{
				if (result.Retryable)
				{
					ReportBootstrapWait(result.FailReason, ref nextWaitReport);
					yield return retryDelay;
					continue;
				}
				if (MultiplayerDiagnostics.Now >= nextWaitReport)
				{
					nextWaitReport = MultiplayerDiagnostics.Now + 5.0;
					MultiplayerDiagnostics.Record("snapshot_apply_progress", $"sequence={sequence} frames={bootstrapOperation.Frames} steps={bootstrapOperation.Steps}", ulong.MaxValue);
				}
				yield return null;
				continue;
			}
			if (_bootstrapLiveUpdates != null && _bootstrapLiveUpdates.PendingCount > 0)
			{
				if (!wasComplete)
				{
					yield return null;
					continue;
				}
				_bootstrapLiveUpdates.DrainFrame(isCurrent);
				if (!isCurrent())
				{
					StopBootstrapWork(stopCoroutine: false);
					yield break;
				}
				if (_bootstrapLiveUpdates.HasFailure)
				{
					FailClientBootstrap(_bootstrapLiveUpdates.Failure, stopCoroutine: false);
					yield break;
				}
				if (_bootstrapLiveUpdates.PendingCount > 0)
				{
					yield return null;
					continue;
				}
			}
			int num2 = _bootstrapLiveUpdates?.ReplayedCount ?? 0;
			MultiplayerDiagnostics.Record("snapshot_apply_complete", FormattableString.Invariant($"totalMs={(MultiplayerDiagnostics.Now - applyStarted) * 1000.0:F1} frames={bootstrapOperation.Frames} steps={bootstrapOperation.Steps} maxFrameWorkMs={bootstrapOperation.MaxFrameWorkMs:F2} maxStepMs={bootstrapOperation.MaxStepMs:F2} replayed={num2} replayFrames={_bootstrapLiveUpdates?.ReplayFrames ?? 0} maxReplayFrameMs={_bootstrapLiveUpdates?.MaxReplayFrameMs ?? 0.0:F2} deferredPeak={_bootstrapLiveUpdates?.PeakCount ?? 0} deferredPeakBytes={_bootstrapLiveUpdates?.PeakBytes ?? 0} sequence={sequence}"), ulong.MaxValue);
			_lastAppliedBootstrapBlob = _pendingBootstrapBlob;
			StopBootstrapWork(stopCoroutine: false, preserveLiveUpdates: true);
			coordinator.NotifyBootstrapApplied(result.Context);
			yield break;
		}
		FailClientBootstrap("Bootstrap apply or live update replay exceeded its admission deadline.", stopCoroutine: false);
	}

	private void ReceiveBootstrapBlob(byte[] blob)
	{
		if (!base.IsSpawned || SessionWorkBlocked)
		{
			return;
		}
		MultiplayerDiagnostics.Record("snapshot_received", $"bytes={((blob != null) ? blob.Length : 0)}", ulong.MaxValue);
		ulong epoch = (Singleton<MultiplayerCoordinator>.TryGetInstance(out var instance) ? instance.SessionEpoch : 0);
		if (!JoinBootstrapHub.TryReadContext(blob, out var context))
		{
			FailClientBootstrap("Bootstrap header is invalid.");
			return;
		}
		JoinBootstrapCoordinator instance2 = Singleton<JoinBootstrapCoordinator>.Instance;
		switch (_clientRevision.Inspect(context, epoch, instance2 != null && instance2.BootstrapCompleted))
		{
		case BootstrapRevisionDecision.Stale:
			MultiplayerDiagnostics.Record("snapshot_stale_ignored", $"snapshotId={context.SnapshotId}", ulong.MaxValue);
			break;
		case BootstrapRevisionDecision.Duplicate:
			if (_lastAppliedBootstrapBlob != null && _lastAppliedBootstrapBlob.SequenceEqual(blob))
			{
				instance2?.ResendBootstrapCompletion();
			}
			else if (_pendingBootstrapBlob == null || !_pendingBootstrapBlob.SequenceEqual(blob))
			{
				FailClientBootstrap("Snapshot identity was reused with different data.");
			}
			break;
		default:
			if (!(instance2 == null))
			{
				if (PrepareClientSnapshot(context))
				{
					StartClientSnapshotApply((byte[])blob.Clone());
				}
				break;
			}
			goto case BootstrapRevisionDecision.Reject;
		case BootstrapRevisionDecision.Reject:
			FailClientBootstrap("Snapshot replacement is not valid for this admission.");
			break;
		}
	}

	private bool PrepareClientSnapshot(JoinSessionContext context)
	{
		bool hasSnapshotIdentity = _clientRevision.Current.HasSnapshotIdentity;
		StopBootstrapWork(stopCoroutine: true, preserveLiveUpdates: true);
		_clientTransfer.Reset();
		_lastAppliedBootstrapBlob = null;
		if (Singleton<JoinBootstrapCoordinator>.Instance == null || !Singleton<JoinBootstrapCoordinator>.Instance.PrepareSnapshot(context))
		{
			FailClientTransfer("Local snapshot admission failed.");
			return false;
		}
		_clientRevision.Commit(context);
		_clientBootstrapDeadline = Time.realtimeSinceStartupAsDouble + (double)Math.Max(_bootstrapApplyTimeoutSeconds, (context.Mode == JoinBootstrapMode.Initial) ? 240 : 120);
		if (!base.IsServer)
		{
			if (_bootstrapLiveUpdates == null)
			{
				_bootstrapLiveUpdates = new BootstrapLiveUpdateBuffer(1024, 1048576, retainHistory: true);
			}
			try
			{
				_bootstrapLiveUpdates.Activate();
				_bootstrapLiveUpdates.BeginSnapshot(context.WorldRevision);
			}
			catch (Exception ex)
			{
				FailClientTransfer(ex.Message);
				return false;
			}
		}
		MultiplayerDiagnostics.Record(hasSnapshotIdentity ? "snapshot_refresh_received" : "snapshot_identity_accepted", $"mode={context.Mode} snapshotId={context.SnapshotId} worldRevision={context.WorldRevision}", ulong.MaxValue);
		return true;
	}

	private void StartClientSnapshotApply(byte[] blob)
	{
		_pendingBootstrapBlob = blob;
		uint num = ++_bootstrapSequence;
		Coroutine bootstrapApplyCoroutine = StartCoroutine(CoApplyBootstrapWhenReady(num, MultiplayerDiagnostics.CurrentAttempt, _clientRevision.Current.SessionEpoch));
		if (num == _bootstrapSequence && _pendingBootstrapBlob != null)
		{
			_bootstrapApplyCoroutine = bootstrapApplyCoroutine;
		}
	}

	private bool IsBootstrapCurrent(uint sequence, long attempt, ulong epoch)
	{
		if (this != null && base.IsSpawned && sequence == _bootstrapSequence && epoch != 0L && attempt == MultiplayerDiagnostics.CurrentAttempt && base.NetworkManager != null && base.NetworkManager.IsListening && Singleton<MultiplayerCoordinator>.TryGetInstance(out var instance) && instance.SessionEpoch == epoch)
		{
			return !SessionWorkBlocked;
		}
		return false;
	}

	private void FailClientBootstrap(string reason, bool stopCoroutine = true)
	{
		MultiplayerDiagnostics.Record("snapshot_apply_failed", reason, ulong.MaxValue, critical: true);
		UtilityMethods.Log("[Bootstrap] Apply failed: " + reason, LogType.Warning);
		StopBootstrapWork(stopCoroutine);
		if (base.IsSpawned && base.NetworkManager != null && base.NetworkManager.IsListening)
		{
			JoinSessionContext current = _clientRevision.Current;
			ServerReportBootstrapFailureRpc(reason, current.SessionEpoch, current.SnapshotId, current.WorldRevision);
		}
	}

	private void StopBootstrapWork(bool stopCoroutine = true, bool preserveLiveUpdates = false)
	{
		_bootstrapSequence++;
		Coroutine bootstrapApplyCoroutine = _bootstrapApplyCoroutine;
		_bootstrapApplyCoroutine = null;
		if (stopCoroutine && bootstrapApplyCoroutine != null)
		{
			StopCoroutine(bootstrapApplyCoroutine);
		}
		BootstrapApplyOperation bootstrapOperation = _bootstrapOperation;
		_bootstrapOperation = null;
		bootstrapOperation?.Dispose();
		if (!preserveLiveUpdates)
		{
			_clientTransfer.Reset();
			ReleaseBootstrapHistory();
		}
		_pendingBootstrapBlob = null;
	}

	private static void ReportBootstrapWait(string reason, ref double nextReport)
	{
		if (!(MultiplayerDiagnostics.Now < nextReport))
		{
			nextReport = MultiplayerDiagnostics.Now + 5.0;
			MultiplayerDiagnostics.Record("snapshot_apply_wait", reason, ulong.MaxValue);
		}
	}

	[Rpc(SendTo.Server)]
	private void ServerReportBootstrapFailureRpc(string reason, ulong epoch, ulong snapshotId, ulong revision, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcParams rpcParams2 = rpcParams;
			RpcAttribute.RpcAttributeParams attributeParams = default;
			FastBufferWriter bufferWriter = __beginSendRpc(786865357u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bool value = reason != null;
			bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
			if (value)
			{
				bufferWriter.WriteValueSafe(reason);
			}
			BytePacker.WriteValueBitPacked(bufferWriter, epoch);
			BytePacker.WriteValueBitPacked(bufferWriter, snapshotId);
			BytePacker.WriteValueBitPacked(bufferWriter, revision);
			__endSendRpc(ref bufferWriter, 786865357u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		if (base.IsServer)
		{
			if (!_serverSnapshots.Matches(rpcParams.Receive.SenderClientId, epoch, snapshotId, revision))
			{
				MultiplayerDiagnostics.Record("bootstrap_stale_failure_ignored", "", rpcParams.Receive.SenderClientId);
				return;
			}
			string reason2 = (string.IsNullOrWhiteSpace(reason) ? "Unknown client bootstrap failure." : ((reason.Length <= 512) ? reason : reason.Substring(0, 512)));
			ServerFailBootstrapRequest(rpcParams.Receive.SenderClientId, reason2);
		}
	}

	private void ServerFailBootstrapRequest(ulong clientId, string reason)
	{
		ServerForgetBootstrap(clientId);
		MultiplayerDiagnostics.Record("bootstrap_failed", reason, clientId, critical: true);
		UtilityMethods.Log("<b><color=#FF4040>[Bootstrap]</color></b> Bootstrap request failed." + $"\n   ClientId: {clientId}" + "\n   Reason: " + reason, LogType.Error);
		if (_gameStartGate != null)
		{
			_gameStartGate.ServerHandleBootstrapFailure(clientId, reason);
		}
	}

	[Rpc(SendTo.Server)]
	public void ServerReportBootstrapCompleteRpc(JoinBootstrapMode mode, ulong sessionEpoch, ulong snapshotId, ulong revision, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcParams rpcParams2 = rpcParams;
			RpcAttribute.RpcAttributeParams attributeParams = default;
			FastBufferWriter bufferWriter = __beginSendRpc(2360490522u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in mode, default(FastBufferWriter.ForEnums));
			BytePacker.WriteValueBitPacked(bufferWriter, sessionEpoch);
			BytePacker.WriteValueBitPacked(bufferWriter, snapshotId);
			BytePacker.WriteValueBitPacked(bufferWriter, revision);
			__endSendRpc(ref bufferWriter, 2360490522u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			HandleBootstrapCompletion(rpcParams.Receive.SenderClientId, mode, sessionEpoch, snapshotId, revision);
		}
	}

	private void HandleBootstrapCompletion(ulong senderClientId, JoinBootstrapMode mode, ulong sessionEpoch, ulong snapshotId, ulong revision)
	{
		if (base.IsServer && !(_gameStartGate == null))
		{
			if (!_serverSnapshots.Matches(senderClientId, sessionEpoch, snapshotId, revision) || !_serverSnapshots.TryGet(senderClientId, out var context, out var _) || context.Mode != mode)
			{
				MultiplayerDiagnostics.Record("bootstrap_stale_completion_ignored", $"snapshotId={snapshotId} worldRevision={revision}", senderClientId);
				return;
			}
			if (_serverTransfers.TryGetValue(senderClientId, out var value) && !value.Complete)
			{
				MultiplayerDiagnostics.Record("bootstrap_completion_before_transfer_ignored", $"snapshotId={snapshotId}", senderClientId);
				return;
			}
			_serverSnapshots.Acknowledge(senderClientId);
			_serverTransfers.Remove(senderClientId);
			_gameStartGate.ServerMarkBootstrapComplete(senderClientId, mode, sessionEpoch);
			ServerReconcileSharedCollectionAchievementsForClient(senderClientId);
		}
	}

	[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
	public void BeginGameplayForSingleClientRpc(ulong epoch, ulong snapshotId, ulong revision, RpcParams rpcParams = default(RpcParams))
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
			{
				InvokePermission = RpcInvokePermission.Server
			};
			FastBufferWriter bufferWriter = __beginSendRpc(2768543271u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			BytePacker.WriteValueBitPacked(bufferWriter, epoch);
			BytePacker.WriteValueBitPacked(bufferWriter, snapshotId);
			BytePacker.WriteValueBitPacked(bufferWriter, revision);
			__endSendRpc(ref bufferWriter, 2768543271u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			MultiplayerDiagnostics.Record("gameplay_rpc_received", "", ulong.MaxValue);
			UtilityMethods.Log("<b><color=#8FD14F>[Bootstrap]</color></b> BeginGameplayForSingleClientRpc received.");
			Singleton<JoinBootstrapCoordinator>.Instance?.OnBeginGameplayReceived(epoch, snapshotId, revision);
		}
	}

	protected override void __initializeVariables()
	{
		if (ToolDataNV == null)
		{
			throw new Exception("RpcRelay.ToolDataNV cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		ToolDataNV.Initialize(this);
		__nameNetworkVariable(ToolDataNV, "ToolDataNV");
		NetworkVariableFields.Add(ToolDataNV);
		if (CompleteBlueMarket == null)
		{
			throw new Exception("RpcRelay.CompleteBlueMarket cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		CompleteBlueMarket.Initialize(this);
		__nameNetworkVariable(CompleteBlueMarket, "CompleteBlueMarket");
		NetworkVariableFields.Add(CompleteBlueMarket);
		if (BlueMarketToolsAvailable == null)
		{
			throw new Exception("RpcRelay.BlueMarketToolsAvailable cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		BlueMarketToolsAvailable.Initialize(this);
		__nameNetworkVariable(BlueMarketToolsAvailable, "BlueMarketToolsAvailable");
		NetworkVariableFields.Add(BlueMarketToolsAvailable);
		if (CompleteOpenSignTuto == null)
		{
			throw new Exception("RpcRelay.CompleteOpenSignTuto cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		CompleteOpenSignTuto.Initialize(this);
		__nameNetworkVariable(CompleteOpenSignTuto, "CompleteOpenSignTuto");
		NetworkVariableFields.Add(CompleteOpenSignTuto);
		if (CompleteTimeTuto == null)
		{
			throw new Exception("RpcRelay.CompleteTimeTuto cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		CompleteTimeTuto.Initialize(this);
		__nameNetworkVariable(CompleteTimeTuto, "CompleteTimeTuto");
		NetworkVariableFields.Add(CompleteTimeTuto);
		if (PlayerCollisionEnabled == null)
		{
			throw new Exception("RpcRelay.PlayerCollisionEnabled cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		PlayerCollisionEnabled.Initialize(this);
		__nameNetworkVariable(PlayerCollisionEnabled, "PlayerCollisionEnabled");
		NetworkVariableFields.Add(PlayerCollisionEnabled);
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		__registerRpc(1018407709u, __rpc_handler_1018407709, "ServerSetDebugSharkRpc", RpcInvokePermission.Everyone);
		__registerRpc(2153083603u, __rpc_handler_2153083603, "ServerSpawnRpc", RpcInvokePermission.Everyone);
		__registerRpc(303762351u, __rpc_handler_303762351, "ServerSpawnShopInventoryItemRpc", RpcInvokePermission.Everyone);
		__registerRpc(3776483117u, __rpc_handler_3776483117, "ClientSaveRpc", RpcInvokePermission.Everyone);
		__registerRpc(2603772542u, __rpc_handler_2603772542, "ClientGlobalChatInfoRpc", RpcInvokePermission.Everyone);
		__registerRpc(732087377u, __rpc_handler_732087377, "ClientGlobalChatLocalizedRpc", RpcInvokePermission.Everyone);
		__registerRpc(2903982800u, __rpc_handler_2903982800, "ClientGlobalChatRestaurantRpc", RpcInvokePermission.Everyone);
		__registerRpc(1170027295u, __rpc_handler_1170027295, "ClientGlobalChatRestaurantTargetRpc", RpcInvokePermission.Everyone);
		__registerRpc(1530242252u, __rpc_handler_1530242252, "ClientUnlockSteamAchievementRpc", RpcInvokePermission.Everyone);
		__registerRpc(4260445826u, __rpc_handler_4260445826, "ServerLoadQuickSlotRpc", RpcInvokePermission.Everyone);
		__registerRpc(1917338793u, __rpc_handler_1917338793, "ServerCompleteTimeTutoRpc", RpcInvokePermission.Everyone);
		__registerRpc(2171746553u, __rpc_handler_2171746553, "ServerCompleteOpenSignTutoRpc", RpcInvokePermission.Everyone);
		__registerRpc(77049073u, __rpc_handler_77049073, "ServerActivateBlueMarketToolsRpc", RpcInvokePermission.Everyone);
		__registerRpc(1696123403u, __rpc_handler_1696123403, "ServerCompleteBlueMarketRpc", RpcInvokePermission.Everyone);
		__registerRpc(1667644177u, __rpc_handler_1667644177, "ServerCompleteBlueMarket2Rpc", RpcInvokePermission.Everyone);
		__registerRpc(3392232515u, __rpc_handler_3392232515, "ServerUpgradeCompassToNextLevelRpc", RpcInvokePermission.Everyone);
		__registerRpc(99542811u, __rpc_handler_99542811, "ServerUpgradeRpc", RpcInvokePermission.Everyone);
		__registerRpc(1152135776u, __rpc_handler_1152135776, "ServerRequestBootstrapRpc", RpcInvokePermission.Everyone);
		__registerRpc(2143993722u, __rpc_handler_2143993722, "SendBootstrapClientRpc", RpcInvokePermission.Server);
		__registerRpc(1099357261u, __rpc_handler_1099357261, "AcknowledgeBootstrapTransferRpc", RpcInvokePermission.Everyone);
		__registerRpc(1967624731u, __rpc_handler_1967624731, "BeginBootstrapTransferClientRpc", RpcInvokePermission.Server);
		__registerRpc(2729660114u, __rpc_handler_2729660114, "BootstrapChunkClientRpc", RpcInvokePermission.Server);
		__registerRpc(786865357u, __rpc_handler_786865357, "ServerReportBootstrapFailureRpc", RpcInvokePermission.Everyone);
		__registerRpc(2360490522u, __rpc_handler_2360490522, "ServerReportBootstrapCompleteRpc", RpcInvokePermission.Everyone);
		__registerRpc(2768543271u, __rpc_handler_2768543271, "BeginGameplayForSingleClientRpc", RpcInvokePermission.Server);
		base.__initializeRpcs();
	}

	private static void __rpc_handler_1018407709(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			RpcParams ext = rpcParams.Ext;
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).ServerSetDebugSharkRpc(value, ext);
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2153083603(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
			reader.ReadValueSafe(out NetworkObjectReference value3, default(FastBufferWriter.ForNetworkSerializable));
			reader.ReadValueSafe(out bool value4, default(FastBufferWriter.ForPrimitives));
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).ServerSpawnRpc(value, value2, value3, value4);
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_303762351(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
			reader.ReadValueSafe(out Vector3 value3);
			RpcParams ext = rpcParams.Ext;
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).ServerSpawnShopInventoryItemRpc(value, value2, value3, ext);
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3776483117(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).ClientSaveRpc();
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2603772542(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out FixedString512Bytes value, default(FastBufferWriter.ForFixedStrings));
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).ClientGlobalChatInfoRpc(value);
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_732087377(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out FixedString128Bytes value, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString512Bytes value2, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString128Bytes value3, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString128Bytes value4, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString128Bytes value5, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out FixedString128Bytes value6, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out byte value7, default(FastBufferWriter.ForPrimitives));
			reader.ReadValueSafe(out byte value8, default(FastBufferWriter.ForPrimitives));
			reader.ReadValueSafe(out byte value9, default(FastBufferWriter.ForPrimitives));
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).ClientGlobalChatLocalizedRpc(value, value2, value3, value4, value5, value6, value7, value8, value9);
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2903982800(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out FixedString512Bytes value, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).ClientGlobalChatRestaurantRpc(value, value2);
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1170027295(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out FixedString512Bytes value, default(FastBufferWriter.ForFixedStrings));
			reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
			RpcParams ext = rpcParams.Ext;
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).ClientGlobalChatRestaurantTargetRpc(value, value2, ext);
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1530242252(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
			RpcParams ext = rpcParams.Ext;
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).ClientUnlockSteamAchievementRpc(value, ext);
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_4260445826(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out QuickSlotLoadPayload value, default(FastBufferWriter.ForNetworkSerializable));
			reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
			RpcParams ext = rpcParams.Ext;
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).ServerLoadQuickSlotRpc(value, value2, ext);
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1917338793(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).ServerCompleteTimeTutoRpc();
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2171746553(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).ServerCompleteOpenSignTutoRpc();
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_77049073(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).ServerActivateBlueMarketToolsRpc();
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1696123403(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).ServerCompleteBlueMarketRpc();
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1667644177(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).ServerCompleteBlueMarket2Rpc();
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3392232515(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).ServerUpgradeCompassToNextLevelRpc();
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_99542811(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out ToolType value, default(FastBufferWriter.ForEnums));
			reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).ServerUpgradeRpc(value, value2);
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1152135776(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
			RpcParams ext = rpcParams.Ext;
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).ServerRequestBootstrapRpc(value, ext);
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2143993722(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).SendBootstrapClientRpc(value2, ext);
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1099357261(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
			ByteUnpacker.ReadValueBitPacked(reader, out int value4);
			reader.ReadValueSafe(out byte value5, default(FastBufferWriter.ForPrimitives));
			RpcParams ext = rpcParams.Ext;
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).AcknowledgeBootstrapTransferRpc(value, value2, value3, value4, value5, ext);
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1967624731(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out BootstrapTransferHeader value, default(FastBufferWriter.ForNetworkSerializable));
			RpcParams ext = rpcParams.Ext;
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).BeginBootstrapTransferClientRpc(value, ext);
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2729660114(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
			ByteUnpacker.ReadValueBitPacked(reader, out int value4);
			reader.ReadValueSafe(out bool value5, default(FastBufferWriter.ForPrimitives));
			byte[] value6 = null;
			if (value5)
			{
				reader.ReadValueSafe(out value6, default(FastBufferWriter.ForPrimitives));
			}
			ByteUnpacker.ReadValueBitPacked(reader, out uint value7);
			RpcParams ext = rpcParams.Ext;
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).BootstrapChunkClientRpc(value, value2, value3, value4, value6, value7, ext);
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_786865357(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value4);
			RpcParams ext = rpcParams.Ext;
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).ServerReportBootstrapFailureRpc(s, value2, value3, value4, ext);
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2360490522(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out JoinBootstrapMode value, default(FastBufferWriter.ForEnums));
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value4);
			RpcParams ext = rpcParams.Ext;
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).ServerReportBootstrapCompleteRpc(value, value2, value3, value4, ext);
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_2768543271(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
			RpcParams ext = rpcParams.Ext;
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((RpcRelay)target).BeginGameplayForSingleClientRpc(value, value2, value3, ext);
			((RpcRelay)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	protected override string __getTypeName()
	{
		return "RpcRelay";
	}
}
