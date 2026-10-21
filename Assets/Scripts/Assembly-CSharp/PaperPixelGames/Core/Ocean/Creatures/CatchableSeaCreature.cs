using System;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.PlayerTools.Harpoon;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Networking.Chat;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	public class CatchableSeaCreature : WanderingSeaCreature
	{
		[Header("Catchable Refs")]
		[SerializeField]
		protected Transform _hookedPoint;

		[Header("Catchable Visual")]
		[SerializeField]
		private bool _forceVisibleWhileHooked = true;

		protected readonly NetworkVariable<bool> _isHooked = new NetworkVariable<bool>(value: false);

		protected Transform _hook;

		protected PPG_Player _hookOwner;

		protected ulong _hookOwnerClientId;

		protected Sequence _runawaySequence;

		protected AudioSource _runawaySfx;

		private CatchableCreatureState _catchState;

		private bool _pendingCatchFinalize;

		private bool _fishBookCatchRegistered;

		private float _pendingCatchWeight;

		private ulong _pendingCatchOwnerClientId;

		private bool _hasPendingCatchOwner;

		private bool _assistCatchActive;

		private bool _assistCatchFinished;

		private ulong _assistPrimaryClientId;

		private ulong _assistFinisherClientId;

		private bool _hasAssistFinisher;

		private int _assistZoneCount;

		private HarpoonSuccessZoneNetData _assistSuccessZoneNetData;

		private bool _hasAssistSuccessZoneNetData;

		private readonly HashSet<ulong> _assistParticipantClientIds = new HashSet<ulong>();

		private readonly HashSet<int> _assistCompletedZoneIndexes = new HashSet<int>();

		public bool AssistCatchActive => _assistCatchActive;

		public bool AssistCatchFinished => _assistCatchFinished;

		public ulong AssistPrimaryClientId => _assistPrimaryClientId;

		public ulong AssistFinisherClientId => _assistFinisherClientId;

		public int AssistZoneCount => _assistZoneCount;

		public int AssistCompletedZoneCount => _assistCompletedZoneIndexes.Count;

		public int AssistParticipantCount => _assistParticipantClientIds.Count;

		public NetworkVariable<bool> IsHooked => _isHooked;

		public CatchableCreatureState CatchState => _catchState;

		public PPG_Player HookOwner => _hookOwner;

		public ulong HookOwnerClientId => _hookOwnerClientId;

		public float PendingCatchWeight => _pendingCatchWeight;

		public Transform HookedPoint
		{
			get
			{
				if (!(_hookedPoint != null))
				{
					return base.transform;
				}
				return _hookedPoint;
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<bool> isHooked = _isHooked;
			isHooked.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isHooked.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnHookedValueChanged));
			if (base.IsServer && base.NetworkManager != null)
			{
				base.NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
			}
		}

		public override void OnNetworkDespawn()
		{
			if (base.IsServer && base.NetworkManager != null)
			{
				base.NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
			}
			NetworkVariable<bool> isHooked = _isHooked;
			isHooked.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isHooked.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnHookedValueChanged));
			DetachHook();
			StopRunawayVisual();
			base.OnNetworkDespawn();
		}

		public override void ServerPrepareForDespawn()
		{
			if (base.IsServer)
			{
				ServerClearCatchRuntimeForDespawn();
				base.ServerPrepareForDespawn();
			}
		}

		private void ServerClearCatchRuntimeForDespawn()
		{
			_catchState = CatchableCreatureState.Free;
			_pendingCatchFinalize = false;
			_pendingCatchWeight = 0f;
			ClearPendingCatchOwner();
			if (_assistCatchActive && !_assistCatchFinished)
			{
				ServerBroadcastAssistCatchCancelled(AssistCatchCancelReason.CreatureReleased);
			}
			_assistCatchActive = false;
			_assistCatchFinished = false;
			_assistPrimaryClientId = 0uL;
			_assistFinisherClientId = 0uL;
			_hasAssistFinisher = false;
			_assistZoneCount = 0;
			_assistParticipantClientIds.Clear();
			_assistCompletedZoneIndexes.Clear();
			ServerClearAssistSuccessZoneNetData();
			if (_isHooked.Value)
			{
				_isHooked.Value = false;
			}
			ClearHookOwner();
			DetachHook();
			StopRunawayVisual();
		}

		public override bool CanReceiveHit(PPG_Player player)
		{
			if (!base.CanReceiveHit(player))
			{
				return false;
			}
			if (_catchState == CatchableCreatureState.Caught)
			{
				return false;
			}
			return !_isHooked.Value;
		}

		public virtual bool CanReceiveAssistHit(PPG_Player player)
		{
			if (player == null)
			{
				return false;
			}
			if (_catchState == CatchableCreatureState.Caught)
			{
				return false;
			}
			return _isHooked.Value;
		}

		protected override void OnServerHit(PPG_Player player)
		{
			if (base.IsServer)
			{
				if (player == null)
				{
					Log("OnServerHit ignored. Player is null.", LogType.Warning);
				}
				else if (CanReceiveHit(player))
				{
					RaiseCreatureHitEvent(player);
					OnServerCatchableHit(player);
				}
			}
		}

		protected virtual void OnServerCatchableHit(PPG_Player player)
		{
			if (base.IsServer && !(player == null))
			{
				ServerHooked(player);
			}
		}

		public virtual bool ServerHooked(PPG_Player owner)
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (owner == null)
			{
				Log("ServerHooked failed. Owner is null.", LogType.Warning);
				return false;
			}
			if (_isHooked.Value)
			{
				Log("ServerHooked ignored. Already hooked.");
				return false;
			}
			_hookOwner = owner;
			_hookOwnerClientId = owner.OwnerClientId;
			_catchState = CatchableCreatureState.Hooked;
			_isHooked.Value = true;
			ChangeAnim(CreatureAnim.Struggle);
			Log($"Hooked by {owner.name} ClientId={_hookOwnerClientId}");
			return true;
		}

		public bool ServerTryInstantCatch(PPG_Player owner, Vector3 hitPosition)
		{
			if (!base.IsServer || owner == null || !CanReceiveHit(owner))
			{
				return false;
			}
			if (!ServerCanInstantCatch(owner))
			{
				return false;
			}
			CreatureCatchResult result = new CreatureCatchResult(this, owner, GetCatchWeight());
			if (!SpawnCaughtDeadFish(result, hitPosition, useFallbackDropPosition: true))
			{
				return false;
			}
			RaiseCreatureHitEvent(owner);
			_catchState = CatchableCreatureState.Caught;
			_pendingCatchFinalize = false;
			_pendingCatchWeight = 0f;
			ClearPendingCatchOwner();
			ServerClearAssistCatchSession();
			ServerRegisterCatch(result);
			OnServerCaught(result);
			Singleton<GameProgressManager>.Instance?.RpcRelay?.ServerUnlockSteamAchievementForClient("ACH_FIRST_FISH_CAUGHT", owner.OwnerClientId);
			Singleton<GameProgressManager>.Instance?.ServerGrantProgressionXp(ProgressionXpSource.FishCaught, (base.FishSO != null) ? base.FishSO.Id : null, owner.OwnerClientId);
			ServerApplyCatchQuestProgress();
			ServerDespawnCaughtCreature();
			Log($"Instant catch finalized. Owner={owner.PlayerName.Value} Weight={result.Weight:0.00}");
			return true;
		}

		protected virtual bool ServerCanInstantCatch(PPG_Player owner)
		{
			return true;
		}

		private void OnClientDisconnected(ulong clientId)
		{
			if (base.IsServer && _isHooked.Value && clientId == _hookOwnerClientId)
			{
				ServerBroadcastAssistCatchCancelled(AssistCatchCancelReason.PrimaryCancelled);
				ReleaseFromHook();
				ChangeArea();
				Log($"Hook owner disconnected. Creature released. ClientId={clientId}", LogType.Warning);
			}
		}

		public bool ServerBeginAssistCatchSession(PPG_Player primaryOwner, int zoneCount)
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (primaryOwner == null)
			{
				Log("Assist catch session begin failed. Primary owner is null.", LogType.Warning);
				return false;
			}
			if (!_isHooked.Value)
			{
				Log("Assist catch session begin failed. Creature is not hooked.", LogType.Warning);
				return false;
			}
			if (_pendingCatchFinalize || _catchState != CatchableCreatureState.Hooked)
			{
				Log($"Assist catch session begin failed. Creature is not joinable. State={_catchState} Pending={_pendingCatchFinalize}", LogType.Warning);
				return false;
			}
			ServerClearAssistSuccessZoneNetData();
			if (primaryOwner == null)
			{
				Log("Assist catch session begin failed. Primary owner is null.", LogType.Warning);
				return false;
			}
			if (!_isHooked.Value)
			{
				Log("Assist catch session begin failed. Creature is not hooked.", LogType.Warning);
				return false;
			}
			if (_pendingCatchFinalize || _catchState != CatchableCreatureState.Hooked)
			{
				Log($"Assist catch session begin failed. Creature is not joinable. State={_catchState} Pending={_pendingCatchFinalize}", LogType.Warning);
				return false;
			}
			_assistCatchActive = true;
			_assistCatchFinished = false;
			_assistPrimaryClientId = primaryOwner.OwnerClientId;
			_assistFinisherClientId = 0uL;
			_hasAssistFinisher = false;
			_assistZoneCount = Mathf.Max(1, zoneCount);
			_assistParticipantClientIds.Clear();
			_assistCompletedZoneIndexes.Clear();
			_assistParticipantClientIds.Add(primaryOwner.OwnerClientId);
			Log("Assist catch session started. " + $"PrimaryClientId={_assistPrimaryClientId} ZoneCount={_assistZoneCount}");
			ClientAssistParticipantCountChangedRpc(_assistParticipantClientIds.Count);
			return true;
		}

		public bool ServerCanJoinAssistCatchSession(PPG_Player player)
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (player == null)
			{
				return false;
			}
			if (!ServerHasActiveAssistCatchTarget())
			{
				return false;
			}
			if (player.OwnerClientId == _assistPrimaryClientId)
			{
				return false;
			}
			return true;
		}

		public bool ServerTryJoinAssistCatchSession(PPG_Player player)
		{
			if (!ServerCanJoinAssistCatchSession(player))
			{
				return false;
			}
			if (_assistParticipantClientIds.Add(player.OwnerClientId))
			{
				Log("Assist participant joined. " + $"ClientId={player.OwnerClientId} Participants={_assistParticipantClientIds.Count}");
				ClientAssistParticipantCountChangedRpc(_assistParticipantClientIds.Count);
			}
			return true;
		}

		public bool ServerLeaveAssistCatchSession(ulong clientId)
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (!_assistCatchActive)
			{
				return false;
			}
			bool num = _assistParticipantClientIds.Remove(clientId);
			if (num)
			{
				Log("Assist participant left. " + $"ClientId={clientId} Participants={_assistParticipantClientIds.Count}");
				ClientAssistParticipantCountChangedRpc(_assistParticipantClientIds.Count);
			}
			return num;
		}

		public bool ServerIsAssistParticipant(ulong clientId)
		{
			if (!base.IsServer)
			{
				return false;
			}
			return _assistParticipantClientIds.Contains(clientId);
		}

		public bool ServerTryCompleteAssistZone(ulong clientId, int zoneIndex, out bool allZonesCompleted)
		{
			allZonesCompleted = false;
			if (!base.IsServer)
			{
				return false;
			}
			if (!ServerHasActiveAssistCatchTarget())
			{
				return false;
			}
			if (!_assistParticipantClientIds.Contains(clientId))
			{
				Log($"Assist zone complete ignored. Client is not participant. ClientId={clientId}", LogType.Warning);
				return false;
			}
			if (zoneIndex < 0 || zoneIndex >= _assistZoneCount)
			{
				Log($"Assist zone complete ignored. Invalid zone index={zoneIndex}", LogType.Warning);
				return false;
			}
			if (!_assistCompletedZoneIndexes.Add(zoneIndex))
			{
				return false;
			}
			Log("Assist zone completed. " + $"Zone={zoneIndex + 1}/{_assistZoneCount} " + $"Completed={_assistCompletedZoneIndexes.Count}/{_assistZoneCount}");
			allZonesCompleted = _assistCompletedZoneIndexes.Count >= _assistZoneCount;
			if (allZonesCompleted)
			{
				_assistCatchFinished = true;
				_assistFinisherClientId = clientId;
				_hasAssistFinisher = true;
				Log($"Assist catch session completed all zones. FinisherClientId={clientId}");
			}
			return true;
		}

		public byte ServerGetCompletedAssistZoneMask()
		{
			if (!base.IsServer)
			{
				return 0;
			}
			byte b = 0;
			foreach (int assistCompletedZoneIndex in _assistCompletedZoneIndexes)
			{
				if (assistCompletedZoneIndex >= 0 && assistCompletedZoneIndex <= 7)
				{
					b |= (byte)(1 << assistCompletedZoneIndex);
				}
			}
			return b;
		}

		public float ServerGetAssistGlobalProgress01()
		{
			if (!base.IsServer)
			{
				return 0f;
			}
			if (_assistZoneCount <= 0)
			{
				return 0f;
			}
			return Mathf.Clamp01((float)_assistCompletedZoneIndexes.Count / (float)_assistZoneCount);
		}

		public void ServerClearAssistCatchSession()
		{
			if (base.IsServer)
			{
				_assistCatchActive = false;
				_assistCatchFinished = false;
				_assistPrimaryClientId = 0uL;
				_assistFinisherClientId = 0uL;
				_hasAssistFinisher = false;
				_assistZoneCount = 0;
				_assistParticipantClientIds.Clear();
				_assistCompletedZoneIndexes.Clear();
				ServerClearAssistSuccessZoneNetData();
				Log("Assist catch session cleared.");
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerReportAssistZoneCompletedRpc(int zoneIndex, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2143236802u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, zoneIndex);
				__endSendRpc(ref bufferWriter, 2143236802u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsServer)
			{
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (_assistCatchActive && !_assistCatchFinished && ServerIsAssistParticipant(senderClientId) && zoneIndex >= 0 && zoneIndex < _assistZoneCount && ServerTryCompleteAssistZone(senderClientId, zoneIndex, out var allZonesCompleted))
			{
				float globalProgress = ServerGetAssistGlobalProgress01();
				ClientAssistZoneCompletedRpc(zoneIndex, globalProgress);
				if (allZonesCompleted)
				{
					ServerStopAssistParticipantCatchVisuals();
					ClientAssistCatchFinishedRpc(_assistPrimaryClientId, _assistFinisherClientId);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientAssistZoneCompletedRpc(int zoneIndex, float globalProgress01)
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
				FastBufferWriter bufferWriter = __beginSendRpc(570202595u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, zoneIndex);
				bufferWriter.WriteValueSafe(in globalProgress01, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 570202595u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				EventBus<AssistCatchZoneCompletedEvent>.Raise(new AssistCatchZoneCompletedEvent(this, zoneIndex, globalProgress01));
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientAssistCatchFinishedRpc(ulong primaryClientId, ulong finisherClientId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(790409828u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, primaryClientId);
				BytePacker.WriteValueBitPacked(bufferWriter, finisherClientId);
				__endSendRpc(ref bufferWriter, 790409828u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Log("[Assist DEBUG] ClientAssistCatchFinishedRpc received. " + $"LocalIsServer={base.IsServer} LocalIsOwner={base.IsOwner} " + $"PrimaryClientId={primaryClientId} FinisherClientId={finisherClientId}");
				EventBus<AssistCatchFinishedEvent>.Raise(new AssistCatchFinishedEvent(this, primaryClientId, finisherClientId));
			}
		}

		private void ServerStopAssistParticipantCatchVisuals()
		{
			if (!base.IsServer || Singleton<PlayersManager>.Instance == null)
			{
				return;
			}
			foreach (ulong assistParticipantClientId in _assistParticipantClientIds)
			{
				if (Singleton<PlayersManager>.Instance.TryGetPlayer(assistParticipantClientId, out var player) && !(player == null) && !(player.Harpoon == null))
				{
					player.Harpoon.ServerStopSuccessfulCatchVisuals();
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientAssistCatchCancelledRpc(ulong primaryClientId, AssistCatchCancelReason reason)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2565647405u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, primaryClientId);
				bufferWriter.WriteValueSafe(in reason, default(FastBufferWriter.ForEnums));
				__endSendRpc(ref bufferWriter, 2565647405u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Log("[Assist DEBUG] ClientAssistCatchCancelledRpc received. " + $"LocalIsServer={base.IsServer} LocalIsOwner={base.IsOwner} " + $"PrimaryClientId={primaryClientId} Reason={reason}");
				EventBus<AssistCatchCancelledEvent>.Raise(new AssistCatchCancelledEvent(this, primaryClientId, reason));
			}
		}

		private void ServerBroadcastAssistCatchCancelled(AssistCatchCancelReason reason)
		{
			if (base.IsServer && _assistCatchActive && !_assistCatchFinished)
			{
				ClientAssistCatchCancelledRpc(_assistPrimaryClientId, reason);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientAssistParticipantCountChangedRpc(int participantCount)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3016527311u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, participantCount);
				__endSendRpc(ref bufferWriter, 3016527311u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				EventBus<AssistCatchParticipantCountChangedEvent>.Raise(new AssistCatchParticipantCountChangedEvent(this, participantCount));
			}
		}

		public void ServerSetAssistSuccessZoneNetData(HarpoonSuccessZoneNetData zoneNetData)
		{
			if (!base.IsServer || !ServerHasActiveAssistCatchTarget())
			{
				return;
			}
			int num = Mathf.Clamp(zoneNetData.Count, 0, 8);
			if (num <= 0)
			{
				Log("Assist success zone net data rejected. Zone count is zero.", LogType.Warning);
				return;
			}
			if (_assistZoneCount != num)
			{
				Log("Assist zone count reconciled. " + $"OldZoneCount={_assistZoneCount} NewZoneCount={num}", LogType.Warning);
				_assistZoneCount = num;
			}
			zoneNetData.Count = (byte)num;
			_assistSuccessZoneNetData = zoneNetData;
			_hasAssistSuccessZoneNetData = true;
		}

		public bool ServerTryGetAssistSuccessZoneNetData(out HarpoonSuccessZoneNetData zoneNetData)
		{
			zoneNetData = _assistSuccessZoneNetData;
			if (!base.IsServer)
			{
				return false;
			}
			if (!ServerHasActiveAssistCatchTarget())
			{
				return false;
			}
			if (!_hasAssistSuccessZoneNetData)
			{
				return false;
			}
			return zoneNetData.Count > 0;
		}

		private bool ServerHasActiveAssistCatchTarget()
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (!_assistCatchActive)
			{
				return false;
			}
			if (_assistCatchFinished)
			{
				return false;
			}
			if (!_isHooked.Value)
			{
				return false;
			}
			if (_pendingCatchFinalize)
			{
				return false;
			}
			return _catchState == CatchableCreatureState.Hooked;
		}

		private void ServerClearAssistSuccessZoneNetData()
		{
			if (base.IsServer)
			{
				_assistSuccessZoneNetData.Clear();
				_hasAssistSuccessZoneNetData = false;
			}
		}

		public void ClientHooked(PPG_Player whoHooked, Transform hook)
		{
			if (!base.IsServer || (whoHooked != null && whoHooked.OwnerClientId == _hookOwnerClientId))
			{
				_hookOwner = whoHooked;
				_hook = hook;
			}
			if (hook != null)
			{
				hook.SetParent(HookedPoint, worldPositionStays: false);
				hook.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			}
			if (whoHooked != null)
			{
				StartRunawayVisual(whoHooked.transform);
			}
			ChangeAnimLocal(CreatureAnim.Struggle);
		}

		[Rpc(SendTo.Server)]
		public virtual void ServerChallengeSuccessRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(836896462u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 836896462u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			Log("[Assist DEBUG] ServerChallengeSuccessRpc received. " + $"SenderClientId={senderClientId} HookOwnerClientId={_hookOwnerClientId} " + $"AssistFinisherClientId={_assistFinisherClientId} Pending={_pendingCatchFinalize}");
			if (base.IsServer)
			{
				if (!_isHooked.Value)
				{
					Log("Challenge success ignored. Creature is not hooked.", LogType.Warning);
					return;
				}
				if (_pendingCatchFinalize)
				{
					Log("Challenge success ignored. Finalize is already pending.");
					return;
				}
				if (!TryResolveChallengeSuccessOwner(senderClientId, out var catchOwner))
				{
					Log("Challenge success rejected. Sender is not the authoritative finisher. " + $"SenderClientId={senderClientId}", LogType.Warning);
					return;
				}
				_pendingCatchWeight = GetCatchWeight();
				_pendingCatchOwnerClientId = catchOwner.OwnerClientId;
				_hasPendingCatchOwner = true;
				_pendingCatchFinalize = true;
				_catchState = CatchableCreatureState.CatchPending;
				OnServerChallengeSuccess(new CreatureCatchResult(this, catchOwner, _pendingCatchWeight));
				Log($"Challenge success. CatchOwnerClientId={catchOwner.OwnerClientId} " + $"Weight={_pendingCatchWeight}");
			}
		}

		[Rpc(SendTo.Server)]
		public virtual void ServerChallengeFailRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3943972863u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3943972863u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && _isHooked.Value)
				{
					ServerBroadcastAssistCatchCancelled(AssistCatchCancelReason.PrimaryFailed);
					ReleaseFromHook();
					ChangeArea();
					Log("Challenge failed. Creature released.");
				}
			}
		}

		[Rpc(SendTo.Server)]
		public virtual void ServerChallengeCancelRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3400792541u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3400792541u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && _isHooked.Value)
				{
					ServerBroadcastAssistCatchCancelled(AssistCatchCancelReason.PrimaryCancelled);
					ReleaseFromHook();
					ChangeArea();
					Log("Challenge cancelled. Creature released.");
				}
			}
		}

		[Rpc(SendTo.Server)]
		public virtual void ServerFinalizeChallengeSuccessRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(610916905u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 610916905u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ServerFinalizeChallengeSuccess();
			}
		}

		public virtual void ServerFinalizeChallengeSuccess()
		{
			if (!base.IsServer)
			{
				return;
			}
			if (!_pendingCatchFinalize)
			{
				Log("Finalize ignored. No pending catch finalize.", LogType.Warning);
				return;
			}
			if (!TryResolvePendingCatchOwner(out var catchOwner))
			{
				AbortCatchAndRelease("Pending catch owner could not be resolved on finalize.");
				return;
			}
			CreatureCatchResult result = new CreatureCatchResult(this, catchOwner, _pendingCatchWeight);
			if (!OnServerFinalizeCatch(result))
			{
				AbortCatchAndRelease("OnServerFinalizeCatch returned false.");
				return;
			}
			_catchState = CatchableCreatureState.Caught;
			_pendingCatchFinalize = false;
			_pendingCatchWeight = 0f;
			ClearPendingCatchOwner();
			ServerClearAssistCatchSession();
			ServerRegisterCatch(result);
			OnServerCaught(result);
			Singleton<GameProgressManager>.Instance?.RpcRelay?.ServerUnlockSteamAchievementForClient("ACH_FIRST_FISH_CAUGHT", result.Owner.OwnerClientId);
			Singleton<GameProgressManager>.Instance?.ServerGrantProgressionXp(ProgressionXpSource.FishCaught, (base.FishSO != null) ? base.FishSO.Id : null, result.Owner.OwnerClientId);
			ServerApplyCatchQuestProgress();
			ServerDespawnCaughtCreature();
			Log("Catch finalized.");
		}

		public bool ServerCanFinalizeChallengeSuccess(ulong clientId)
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (_pendingCatchFinalize && _hasPendingCatchOwner)
			{
				return _pendingCatchOwnerClientId == clientId;
			}
			return false;
		}

		private void ServerApplyCatchQuestProgress()
		{
			if (!base.IsServer)
			{
				return;
			}
			if (Singleton<GameProgressManager>.Instance == null || Singleton<GameProgressManager>.Instance.ItemDatabaseSO == null || QuestReferenceHub.Instance == null)
			{
				Log("Catch quest progress skipped. Required manager is missing.", LogType.Warning);
				return;
			}
			FishSO fishSO = base.FishSO;
			if (!(fishSO == null))
			{
				if (fishSO == Singleton<GameProgressManager>.Instance.ItemDatabaseSO.MackerelSO)
				{
					QuestReferenceHub.Instance.AddProgress(QuestId.CatchAMackerel);
				}
				else if (fishSO == Singleton<GameProgressManager>.Instance.ItemDatabaseSO.BlackSeaBreamSO)
				{
					QuestReferenceHub.Instance.AddProgress(QuestId.CatchBlackSeaBream);
				}
				else if (fishSO == Singleton<GameProgressManager>.Instance.ItemDatabaseSO.ChinookSalmonSO)
				{
					QuestReferenceHub.Instance.AddProgress(QuestId.CatchChinookSalmonAtGoldenfinDrift);
				}
				else if (fishSO == Singleton<GameProgressManager>.Instance.ItemDatabaseSO.SquidSO)
				{
					QuestReferenceHub.Instance.AddProgress(QuestId.CatchASquid);
				}
			}
		}

		private void ServerDespawnCaughtCreature()
		{
			if (base.IsServer && base.NetworkObject != null && base.NetworkObject.IsSpawned)
			{
				ServerPrepareForDespawn();
				base.NetworkObject.Despawn();
			}
		}

		protected virtual bool OnServerFinalizeCatch(CreatureCatchResult result)
		{
			return SpawnCaughtDeadFish(result, base.transform.position, useFallbackDropPosition: false);
		}

		private bool SpawnCaughtDeadFish(CreatureCatchResult result, Vector3 fallbackDropPosition, bool useFallbackDropPosition)
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (result.Owner == null)
			{
				Log("Finalize catch failed. Owner is null.", LogType.Warning);
				return false;
			}
			FishSO fishSO = base.FishSO;
			if (fishSO == null)
			{
				Log("Finalize catch failed. CreatureSO is not FishSO. Creature=" + _creatureSO?.name, LogType.Warning);
				return false;
			}
			if (fishSO.DeadPrefab == null)
			{
				Log("Finalize catch failed. DeadPrefab is null for " + fishSO.name + ".", LogType.Warning);
				return false;
			}
			if (result.Owner.Interactor == null || result.Owner.Interactor.NetworkObject == null)
			{
				Log("Finalize catch failed. Owner interactor is missing.", LogType.Warning);
				return false;
			}
			if (Singleton<SeaCreatureManager>.Instance == null)
			{
				Log("Finalize catch failed. SeaCreatureManager.Instance is null.", LogType.Warning);
				return false;
			}
			Log("[Harpoon Catch DEBUG] Finalize spawn dead fish to inventory. " + $"Fish={fishSO.name} Owner={result.Owner.name} Weight={result.Weight}");
			DeadFish deadFish = Singleton<SeaCreatureManager>.Instance.SpawnDeadFish(fishSO, base.transform.position, result.Owner.OwnerClientId, result.Owner.Interactor.NetworkObject.ToReference(), select: false, canHold: true, useFallbackDropPosition, fallbackDropPosition, useFallbackDropPosition);
			if (deadFish == null)
			{
				Log("Finalize catch failed. SpawnDeadFish returned null.", LogType.Warning);
				return false;
			}
			deadFish.Weight.Value = result.Weight;
			deadFish.ServerMarkFishBookCatchRegistered();
			return true;
		}

		protected virtual void OnServerChallengeSuccess(CreatureCatchResult result)
		{
			if (result.Owner == null || result.Owner.Harpoon == null)
			{
				Log("OnServerChallengeSuccess failed. Owner or Harpoon is null.", LogType.Warning);
				return;
			}
			Log("[Assist DEBUG] Sending ClientChallangeSuccessRpc. " + $"ResultOwnerClientId={result.Owner.OwnerClientId} " + $"HookOwnerClientId={_hookOwnerClientId}");
			ServerRegisterCatch(result);
			result.Owner.Harpoon.ClientShowCatchInfoRpc((base.FishSO != null) ? new FixedString64Bytes(base.FishSO.Id) : default(FixedString64Bytes), result.Weight, base.RpcTarget.Single(result.Owner.OwnerClientId, RpcTargetUse.Temp));
			result.Owner.Harpoon.ClientChallangeSuccessRpc(base.NetworkObject.ToReference(), result.Owner.OwnerClientId, result.Weight, base.RpcTarget.Single(result.Owner.OwnerClientId, RpcTargetUse.Temp));
		}

		private void ServerRegisterCatch(CreatureCatchResult result)
		{
			if (!base.IsServer || result.Owner == null || base.FishSO == null)
			{
				Log("ServerRegisterCatch skipped. Owner or FishSO is null.", LogType.Warning);
			}
			else
			{
				if (_fishBookCatchRegistered)
				{
					return;
				}
				try
				{
					ServiceLocator.ForSceneOf(this).Get<TabletNetworkHandler>(out var service);
					if (service == null)
					{
						Log("TabletNetworkHandler not found while registering catch.", LogType.Warning);
						return;
					}
					service.ServerAddToFishBook(base.FishSO, result.Weight, result.Owner.SteamID.Value, result.Owner.OwnerClientId);
					_fishBookCatchRegistered = true;
					Log("Catch registered in FishBook and daily report.");
				}
				catch (Exception ex)
				{
					Log("ServerRegisterCatch failed. " + ex.Message, LogType.Warning);
				}
			}
		}

		protected virtual void OnServerCaught(CreatureCatchResult result)
		{
			BroadcastCaughtInfo(result);
			RaiseCreatureCaughtEvent(result);
		}

		private void BroadcastCaughtInfo(CreatureCatchResult result)
		{
			if (base.IsServer)
			{
				string text = ((result.Owner != null) ? result.Owner.PlayerName.Value.ToString() : "A player");
				string text2 = ((base.FishSO != null && !string.IsNullOrWhiteSpace(base.FishSO.Name)) ? base.FishSO.Name : "a fish");
				string text3 = ((base.FishSO != null && !string.IsNullOrWhiteSpace(base.FishSO.Id)) ? ("item." + base.FishSO.Id + ".name") : string.Empty);
				string text4 = (string.IsNullOrWhiteSpace(text3) ? text2 : ("@" + text3 + "|" + text2));
				GlobalChatManager.BroadcastLocalizedInfoFromServer(this, "chat.info.creature_caught", "{0} caught {1}, {2} kg.", text, text4, result.Weight.ToString("0.0", CultureInfo.InvariantCulture));
			}
		}

		protected virtual void ReleaseFromHook()
		{
			if (base.IsServer)
			{
				_catchState = CatchableCreatureState.Released;
				_pendingCatchFinalize = false;
				_pendingCatchWeight = 0f;
				ClearPendingCatchOwner();
				ServerClearAssistCatchSession();
				RaiseCreatureReleasedEvent(_hookOwner);
				_isHooked.Value = false;
			}
		}

		protected virtual void AbortCatchAndRelease(string reason)
		{
			Log("Catch aborted. Reason=" + reason, LogType.Warning);
			ReleaseFromHook();
			ChangeArea();
		}

		protected virtual float GetCatchWeight()
		{
			if (!(base.FishSO == null))
			{
				return base.FishSO.GetRandomWeight();
			}
			return 1f;
		}

		protected virtual bool TryResolveHookOwner()
		{
			if (_hookOwner != null && _hookOwner.IsSpawned && _hookOwner.OwnerClientId == _hookOwnerClientId)
			{
				return true;
			}
			_hookOwner = null;
			if (base.NetworkManager == null)
			{
				return false;
			}
			if (!base.NetworkManager.ConnectedClients.TryGetValue(_hookOwnerClientId, out var value))
			{
				return false;
			}
			if (value == null || value.PlayerObject == null)
			{
				return false;
			}
			_hookOwner = value.PlayerObject.GetComponent<PPG_Player>();
			return _hookOwner != null;
		}

		private bool TryResolveChallengeSuccessOwner(ulong senderClientId, out PPG_Player catchOwner)
		{
			catchOwner = null;
			ulong num = _hookOwnerClientId;
			if (_assistCatchActive && _assistCatchFinished)
			{
				if (!_hasAssistFinisher)
				{
					return false;
				}
				if (!_assistParticipantClientIds.Contains(_assistFinisherClientId))
				{
					return false;
				}
				num = _assistFinisherClientId;
			}
			if (senderClientId != num)
			{
				return false;
			}
			return TryResolvePlayer(num, out catchOwner);
		}

		private bool TryResolvePendingCatchOwner(out PPG_Player catchOwner)
		{
			catchOwner = null;
			if (!_hasPendingCatchOwner)
			{
				return false;
			}
			return TryResolvePlayer(_pendingCatchOwnerClientId, out catchOwner);
		}

		private bool TryResolvePlayer(ulong clientId, out PPG_Player player)
		{
			player = null;
			if (Singleton<PlayersManager>.Instance != null && Singleton<PlayersManager>.Instance.TryGetPlayer(clientId, out player) && player != null && player.IsSpawned)
			{
				return true;
			}
			if (base.NetworkManager == null || !base.NetworkManager.ConnectedClients.TryGetValue(clientId, out var value) || value == null || value.PlayerObject == null)
			{
				player = null;
				return false;
			}
			player = value.PlayerObject.GetComponent<PPG_Player>();
			if (player != null)
			{
				return player.IsSpawned;
			}
			return false;
		}

		private void ClearPendingCatchOwner()
		{
			_pendingCatchOwnerClientId = 0uL;
			_hasPendingCatchOwner = false;
		}

		private void OnHookedValueChanged(bool oldValue, bool newValue)
		{
			if (base.IsServer)
			{
				if (newValue)
				{
					SetServerSimulationActive(active: false);
				}
				else
				{
					ClearHookOwner();
					bool serverSimulationActive = _currentSeaLayer != null && _currentSeaLayer.HasServerActiveOccupants;
					SetServerSimulationActive(serverSimulationActive);
					ChangeAnim(CreatureAnim.Move);
				}
			}
			if (!newValue)
			{
				DetachHook();
				StopRunawayVisual();
			}
			RefreshLayerActivity();
			OnHookedStateChanged(newValue);
		}

		protected virtual void OnHookedStateChanged(bool hooked)
		{
		}

		protected virtual void ClearHookOwner()
		{
			_hookOwner = null;
			_hookOwnerClientId = 0uL;
		}

		protected virtual void DetachHook()
		{
			if (!(_hook == null))
			{
				_hook.SetParent(null);
				_hook = null;
			}
		}

		protected virtual void StartRunawayVisual(Transform owner)
		{
			if (!(owner == null) && !(_activeGraphic == null))
			{
				_runawaySequence?.Kill();
				base.transform.DOKill();
				Vector3 normalized = (base.transform.position - owner.position).normalized;
				if (normalized.sqrMagnitude > 0.001f)
				{
					Quaternion endValue = Quaternion.LookRotation(normalized, Vector3.up);
					base.transform.DORotateQuaternion(endValue, 0.25f).SetEase(Ease.OutExpo);
				}
				ChangeAnim(CreatureAnim.Struggle);
			}
		}

		public virtual void StopRunawayVisual()
		{
			_runawaySequence?.Kill();
			_runawaySequence = null;
			base.transform.DOKill();
			if (_runawaySfx != null && _runawaySfx.isPlaying)
			{
				_runawaySfx.Stop();
			}
			ChangeAnim(CreatureAnim.Move);
		}

		protected override bool ShouldForceLocalVisible()
		{
			return base.ShouldForceLocalVisible();
		}

		protected virtual void RaiseCreatureCaughtEvent(CreatureCatchResult result)
		{
			EventBus<CreatureCaughtEvent>.Raise(new CreatureCaughtEvent(this, result.Owner, result.Weight, canHold: true));
		}

		protected virtual void RaiseCreatureReleasedEvent(PPG_Player owner)
		{
			EventBus<CreatureReleasedEvent>.Raise(new CreatureReleasedEvent(this, owner));
		}

		public override void ServerCreatureSens(PPG_Player player, byte sensType)
		{
			if (base.IsServer && !_isHooked.Value && _catchState != CatchableCreatureState.Caught)
			{
				base.ServerCreatureSens(player, sensType);
			}
		}

		public int[] ServerGetCompletedAssistZoneIndexes()
		{
			if (!base.IsServer)
			{
				return Array.Empty<int>();
			}
			int[] array = new int[_assistCompletedZoneIndexes.Count];
			_assistCompletedZoneIndexes.CopyTo(array);
			return array;
		}

		[Rpc(SendTo.Server)]
		public void ServerLeaveAssistCatchSessionRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(628523755u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 628523755u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsServer)
			{
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				if (_assistCatchActive && !_assistCatchFinished)
				{
					ServerLeaveAssistCatchSession(senderClientId);
				}
			}
		}

		protected override void __initializeVariables()
		{
			if (_isHooked == null)
			{
				throw new Exception("CatchableSeaCreature._isHooked cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isHooked.Initialize(this);
			__nameNetworkVariable(_isHooked, "_isHooked");
			NetworkVariableFields.Add(_isHooked);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(2143236802u, __rpc_handler_2143236802, "ServerReportAssistZoneCompletedRpc", RpcInvokePermission.Everyone);
			__registerRpc(570202595u, __rpc_handler_570202595, "ClientAssistZoneCompletedRpc", RpcInvokePermission.Everyone);
			__registerRpc(790409828u, __rpc_handler_790409828, "ClientAssistCatchFinishedRpc", RpcInvokePermission.Everyone);
			__registerRpc(2565647405u, __rpc_handler_2565647405, "ClientAssistCatchCancelledRpc", RpcInvokePermission.Everyone);
			__registerRpc(3016527311u, __rpc_handler_3016527311, "ClientAssistParticipantCountChangedRpc", RpcInvokePermission.Everyone);
			__registerRpc(836896462u, __rpc_handler_836896462, "ServerChallengeSuccessRpc", RpcInvokePermission.Everyone);
			__registerRpc(3943972863u, __rpc_handler_3943972863, "ServerChallengeFailRpc", RpcInvokePermission.Everyone);
			__registerRpc(3400792541u, __rpc_handler_3400792541, "ServerChallengeCancelRpc", RpcInvokePermission.Everyone);
			__registerRpc(610916905u, __rpc_handler_610916905, "ServerFinalizeChallengeSuccessRpc", RpcInvokePermission.Everyone);
			__registerRpc(628523755u, __rpc_handler_628523755, "ServerLeaveAssistCatchSessionRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_2143236802(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				RpcParams ext = rpcParams.Ext;
				((CatchableSeaCreature)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CatchableSeaCreature)target).ServerReportAssistZoneCompletedRpc(value, ext);
				((CatchableSeaCreature)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_570202595(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				((CatchableSeaCreature)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CatchableSeaCreature)target).ClientAssistZoneCompletedRpc(value, value2);
				((CatchableSeaCreature)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_790409828(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
				((CatchableSeaCreature)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CatchableSeaCreature)target).ClientAssistCatchFinishedRpc(value, value2);
				((CatchableSeaCreature)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2565647405(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				reader.ReadValueSafe(out AssistCatchCancelReason value2, default(FastBufferWriter.ForEnums));
				((CatchableSeaCreature)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CatchableSeaCreature)target).ClientAssistCatchCancelledRpc(value, value2);
				((CatchableSeaCreature)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3016527311(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				((CatchableSeaCreature)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CatchableSeaCreature)target).ClientAssistParticipantCountChangedRpc(value);
				((CatchableSeaCreature)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_836896462(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((CatchableSeaCreature)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CatchableSeaCreature)target).ServerChallengeSuccessRpc(ext);
				((CatchableSeaCreature)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3943972863(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((CatchableSeaCreature)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CatchableSeaCreature)target).ServerChallengeFailRpc();
				((CatchableSeaCreature)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3400792541(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((CatchableSeaCreature)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CatchableSeaCreature)target).ServerChallengeCancelRpc();
				((CatchableSeaCreature)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_610916905(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((CatchableSeaCreature)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CatchableSeaCreature)target).ServerFinalizeChallengeSuccessRpc();
				((CatchableSeaCreature)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_628523755(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((CatchableSeaCreature)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CatchableSeaCreature)target).ServerLeaveAssistCatchSessionRpc(ext);
				((CatchableSeaCreature)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "CatchableSeaCreature";
		}
	}
}
