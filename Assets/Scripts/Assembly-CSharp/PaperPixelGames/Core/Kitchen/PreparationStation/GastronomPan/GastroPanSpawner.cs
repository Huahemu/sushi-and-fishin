using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Kitchen.PreparationStation.Station;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen.PreparationStation.GastronomPan
{
	public class GastroPanSpawner : NetworkBehaviour
	{
		[SerializeField]
		private PrepStation _prepStation;

		[SerializeField]
		private NetworkObject _gastroPanPrefab;

		[SerializeField]
		private Transform _gastroPanTargetContainer;

		[SerializeField]
		private Transform _gastropanParent;

		[SerializeField]
		private Transform _spawnTransform;

		[SerializeField]
		private AudioClip _gastroPanSpawnSfx;

		[SerializeField]
		private AudioClip _gastroPanMoveSfx;

		[Header("Clickable Stack Feedback")]
		[SerializeField]
		[Min(0.05f)]
		private float _clickableStackTweenDuration = 0.18f;

		[SerializeField]
		[Min(1f)]
		private float _clickableStackScalePunch = 1.035f;

		private readonly List<Transform> _gastroPanTargetPoints = new List<Transform>();

		private readonly NetworkList<byte> _targetOccupied = new NetworkList<byte>();

		private readonly NetworkVariable<bool> _isFallbackOccupied = new NetworkVariable<bool>(value: false);

		private readonly List<GastroPan> _spawnedGastroPans = new List<GastroPan>();

		private readonly Dictionary<int, GastroPan> _clientTargetPans = new Dictionary<int, GastroPan>();

		private GastroPan _activeGastroPan;

		private bool _moveToTargetInProgress;

		private Vector3 _gastropanParentBaseLocalPosition;

		private Vector3 _gastropanParentBaseScale;

		private bool _gastropanParentBaseCached;

		private Transform _attentionTarget;

		private Tween _attentionTween;

		private Vector3 _attentionBaseScale;

		private FixedString64Bytes _lastTransferProcessedId;

		private const float HoverLiftAmount = 0.015f;

		private const float HoverDuration = 0.12f;

		private readonly Dictionary<Transform, Vector3> _hoverBaseLocalPositions = new Dictionary<Transform, Vector3>();

		private readonly Dictionary<Transform, Tween> _hoverMoveTweens = new Dictionary<Transform, Tween>();

		[Header("GastroPan Action Recovery")]
		[SerializeField]
		[Min(0.01f)]
		private float _transferVisualRepairDelay = 0.05f;

		[SerializeField]
		[Min(0.25f)]
		private float _moveToTargetLockTimeout = 2f;

		[SerializeField]
		private bool _logGastroPanActionRecovery = true;

		private bool _spawnGastroPanRequestInProgress;

		private bool _recallGastroPanRequestInProgress;

		private float _moveToTargetStartedAt;

		[Header("Automatic Transfer")]
		[SerializeField]
		[Min(0.05f)]
		private float _postCutTransferDecisionDelay = 0.14f;

		[SerializeField]
		[Min(0.08f)]
		private float _directTransferDuration = 0.38f;

		[SerializeField]
		[Min(0f)]
		private float _directTransferStagger = 0.04f;

		[SerializeField]
		[Min(0.05f)]
		private float _incomingPanLiftLeadTime = 0.28f;

		[SerializeField]
		[Min(0.05f)]
		private float _panInitializationDelay = 0.5f;

		[SerializeField]
		[Min(0.05f)]
		private float _panMoveDuration = 0.3f;

		private bool _directTransferInProgress;

		public NetworkObject StaffPanPrefab => _gastroPanPrefab;

		public PrepStation PrepStation => _prepStation;

		public bool IsFallbackOccupied => _isFallbackOccupied.Value;

		public override void OnNetworkSpawn()
		{
			if (!base.IsServer)
			{
				DOVirtual.DelayedCall(0.1f, ClientRebuildLateJoinTargetPans);
				return;
			}
			_gastroPanTargetPoints.Clear();
			_targetOccupied.Clear();
			if (_gastroPanTargetContainer == null)
			{
				UtilityMethods.Log("<color=red>[GastroPanSpawner] Target container is null.</color>", LogType.Warning);
				return;
			}
			foreach (Transform item in _gastroPanTargetContainer)
			{
				_gastroPanTargetPoints.Add(item);
				_targetOccupied.Add(0);
			}
		}

		public void ClientRegisterLateJoinTargetPan(GastroPan pan)
		{
			if (pan == null || !pan.IsSpawned || !pan.ClientBelongsToPrepStationSpawner(this))
			{
				return;
			}
			int targetIndex = pan.TargetIndex;
			if (targetIndex < 0 || targetIndex >= _prepStation.GastropanRecallInteractors.Count)
			{
				return;
			}
			ClientRegisterTargetPan(pan, targetIndex);
			GastropanRecallInteractor gastropanRecallInteractor = _prepStation.GastropanRecallInteractors[targetIndex];
			if (!(gastropanRecallInteractor == null))
			{
				gastropanRecallInteractor.HasTargetObject = true;
				if (_prepStation.ItemDatabaseSO.FindById(pan.IngredientId.ToString()) is ProcessedIngredientSO processedIngredientSO)
				{
					gastropanRecallInteractor.ClientSetToolTipData(processedIngredientSO, pan.IngredientCount);
				}
			}
		}

		private void ClientRebuildLateJoinTargetPans()
		{
			if (base.IsServer || _prepStation == null)
			{
				return;
			}
			_spawnedGastroPans.Clear();
			_clientTargetPans.Clear();
			foreach (GastropanRecallInteractor gastropanRecallInteractor in _prepStation.GastropanRecallInteractors)
			{
				if (gastropanRecallInteractor != null)
				{
					gastropanRecallInteractor.HasTargetObject = false;
				}
			}
			GastroPan[] array = UnityEngine.Object.FindObjectsByType<GastroPan>(FindObjectsSortMode.None);
			foreach (GastroPan pan in array)
			{
				ClientRegisterLateJoinTargetPan(pan);
			}
		}

		private void OnDisable()
		{
			ClientStopAttentionFeedback();
		}

		[Rpc(SendTo.Server)]
		public void ServerTrySpawnGastroPanRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1814206290u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1814206290u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!(_prepStation != null) || _prepStation.StaffEmployeeId == 0)
				{
					ServerBeginSpawnGastroPanForPendingOutput();
				}
			}
		}

		private void ServerBeginSpawnGastroPanForPendingOutput()
		{
			if (_prepStation.CurrentState != PrepStationState.TransferToGastroPan || _activeGastroPan != null || _spawnGastroPanRequestInProgress)
			{
				return;
			}
			if (!_prepStation.ServerTryRepairTransferVisualBeforeGastroPan())
			{
				ShowNotifyToActiveUser("Prep Station", "notification.prep.lost_ingredient_recovered");
				_prepStation.ServerRecoverBrokenTransferToIdle("Missing pending processed output before GastroPan spawn.");
				return;
			}
			_spawnGastroPanRequestInProgress = true;
			DOVirtual.DelayedCall(_transferVisualRepairDelay, () =>
			{
				if (this == null || !base.IsServer)
				{
					_spawnGastroPanRequestInProgress = false;
				}
				else
				{
					_spawnGastroPanRequestInProgress = false;
					ServerTrySpawnGastroPanAfterTransferVisualRepair();
				}
			});
		}

		private void ServerTrySpawnGastroPanAfterTransferVisualRepair()
		{
			if (_prepStation.CurrentState != PrepStationState.TransferToGastroPan || _activeGastroPan != null)
			{
				return;
			}
			ServerRepairTargetSlots("Before GastroPan spawn availability check.");
			bool flag = GetAvailableTargetIndex() != -1;
			if (!flag && _isFallbackOccupied.Value)
			{
				ShowNotifyToActiveUser("Info", "notification.prep.no_space_to_spawn_pan");
				return;
			}
			bool flag2 = !flag;
			if (flag2)
			{
				_isFallbackOccupied.Value = true;
			}
			if (!ServerSpawnGastroPanAtPosition() & flag2)
			{
				_isFallbackOccupied.Value = false;
			}
		}

		private bool ServerSpawnGastroPanAtPosition()
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (_gastroPanPrefab == null)
			{
				UtilityMethods.Log("<color=red>[GastroPanSpawner] Spawn failed. GastroPan prefab is null.</color>", LogType.Warning);
				return false;
			}
			if (_spawnTransform == null)
			{
				UtilityMethods.Log("<color=red>[GastroPanSpawner] Spawn failed. Spawn transform is null.</color>", LogType.Warning);
				return false;
			}
			NetworkObject networkObject = UnityEngine.Object.Instantiate(_gastroPanPrefab, _spawnTransform);
			if (networkObject == null)
			{
				UtilityMethods.Log("<color=red>[GastroPanSpawner] Instantiate returned null.</color>", LogType.Warning);
				return false;
			}
			if (!networkObject.TryGetComponent<GastroPan>(out var gastroPan) || gastroPan == null)
			{
				UtilityMethods.Log("<color=red>[GastroPanSpawner] GastroPan component is missing on prefab.</color>", LogType.Warning);
				UnityEngine.Object.Destroy(networkObject.gameObject);
				return false;
			}
			_prepStation.PlayPrepStationSfxForActiveUser(PrepStation.PrepStationSfxId.GastroPanSpawn);
			networkObject.Spawn(destroyWithScene: true);
			_activeGastroPan = gastroPan;
			_activeGastroPan.TargetIndex = -1;
			_activeGastroPan.ServerInitializePrepStationIngredientData(this);
			ServerSetCenterPanPickupEnabled(_activeGastroPan, enabled: false);
			DOVirtual.DelayedCall(_panInitializationDelay, () =>
			{
				if (!(this == null) && base.IsServer && ServerIsValidPan(gastroPan))
				{
					gastroPan.ServerGastropanSpawnerInitialize(this);
					if (_activeGastroPan == gastroPan)
					{
						ServerSetCenterPanPickupEnabled(gastroPan, enabled: true);
					}
				}
			});
			_prepStation.PlayPrepStationSfxForActiveUser(PrepStation.PrepStationSfxId.GastroPanMove);
			ClientMoveGastroPanToCenterRpc(_activeGastroPan.NetworkObject, _prepStation.GastropanCenterTransform.position, -1);
			_prepStation.ServerSetStateRpc(3);
			return true;
		}

		private void ServerSetCenterPanPickupEnabled(GastroPan pan, bool enabled)
		{
			if (base.IsServer && ServerIsValidPan(pan))
			{
				pan.CanGaze.Value = enabled;
				pan.CanHold.Value = enabled;
			}
		}

		private void ServerEnableCenterPanPickupAfterDelay(GastroPan pan, float delay)
		{
			if (!base.IsServer || pan == null)
			{
				return;
			}
			DOVirtual.DelayedCall(delay, () =>
			{
				if (!(this == null) && base.IsServer && ServerIsValidPan(pan) && !(_activeGastroPan != pan))
				{
					ServerSetCenterPanPickupEnabled(pan, enabled: true);
				}
			});
		}

		private void ServerGastropanRecallInteractorSet(bool value, sbyte index)
		{
			if (base.IsServer && index >= 0 && index < _prepStation.GastropanRecallInteractors.Count)
			{
				ClientGastropanRecallInteractorSetRpc(value, index);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientGastropanRecallInteractorSetRpc(bool value, sbyte index)
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
				FastBufferWriter bufferWriter = __beginSendRpc(4066517250u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in index, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 4066517250u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (index < 0 || index >= _prepStation.GastropanRecallInteractors.Count)
			{
				return;
			}
			GastropanRecallInteractor gastropanRecallInteractor = _prepStation.GastropanRecallInteractors[index];
			if (!(gastropanRecallInteractor == null))
			{
				gastropanRecallInteractor.HasTargetObject = value;
				if (!value)
				{
					ClientUnregisterTargetPan(index);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		public void ClientSetPrepStationToolTipDataRpc(FixedString64Bytes ingredientID, byte ingredientCount, sbyte gastropanIndex)
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
				FastBufferWriter bufferWriter = __beginSendRpc(727652366u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in ingredientID, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in ingredientCount, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in gastropanIndex, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 727652366u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			string id = ingredientID.ToString();
			if (!(_prepStation.ItemDatabaseSO.FindById(id) is ProcessedIngredientSO processedIngredientSO))
			{
				return;
			}
			int num = gastropanIndex;
			if (num >= 0 && num < _prepStation.GastropanRecallInteractors.Count)
			{
				GastropanRecallInteractor gastropanRecallInteractor = _prepStation.GastropanRecallInteractors[num];
				if (!(gastropanRecallInteractor == null))
				{
					gastropanRecallInteractor.ClientSetToolTipData(processedIngredientSO, ingredientCount);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		public void ClientToolTipIngredientCountUpdateRpc(byte ingredientCount, sbyte gastropanIndex)
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
				FastBufferWriter bufferWriter = __beginSendRpc(542006931u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in ingredientCount, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in gastropanIndex, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 542006931u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			int num = gastropanIndex;
			if (num >= 0 && num < _prepStation.GastropanRecallInteractors.Count)
			{
				GastropanRecallInteractor gastropanRecallInteractor = _prepStation.GastropanRecallInteractors[num];
				if (!(gastropanRecallInteractor == null))
				{
					gastropanRecallInteractor.ClientToolTipIngredientCountUpdate(ingredientCount);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientMoveGastroPanToCenterRpc(NetworkObjectReference objRef, Vector3 targetPosition, sbyte previousTargetIndex = -1)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1473403869u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in objRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in targetPosition);
				bufferWriter.WriteValueSafe(in previousTargetIndex, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1473403869u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (objRef.TryGet(out var networkObject) && !(networkObject == null) && networkObject.TryGetComponent<GastroPan>(out var gastroPan))
			{
				if (previousTargetIndex >= 0)
				{
					ClientUnregisterTargetPan(previousTargetIndex);
				}
				_activeGastroPan = gastroPan;
				ClientStopAttentionFeedback();
				Transform target = networkObject.transform;
				target.DOKill(complete: true);
				float panMoveDuration = _panMoveDuration;
				Quaternion endValue = Quaternion.Euler(0f, 90f, 0f);
				target.DOMove(targetPosition, panMoveDuration).SetEase(Ease.InOutQuad).OnComplete(() =>
				{
					_prepStation.ClientTransferIngredientsDirectlyToGastroPanLocal(gastroPan.transform, _directTransferDuration, showCenterInteractorWhenComplete: false, _directTransferStagger);
				});
				target.DORotateQuaternion(endValue, panMoveDuration).SetEase(Ease.InOutQuad);
				AnimateStackOnTake();
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerMoveActiveGastroPanToNextTargetRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(2581415247u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2581415247u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!(_prepStation != null) || _prepStation.StaffEmployeeId == 0)
				{
					ServerMoveActiveGastroPanToNextTarget();
				}
			}
		}

		private void ServerMoveActiveGastroPanToNextTarget()
		{
			if (!ServerTryRepairCenterPanBeforeMoveToTarget("MoveActiveGastroPanToNextTarget"))
			{
				return;
			}
			ServerBeginMoveToTargetLock();
			ServerRepairTargetSlots("Before moving active GastroPan to target.");
			sbyte availableTargetIndex = GetAvailableTargetIndex();
			if (availableTargetIndex == -1)
			{
				ServerClearMoveToTargetLock();
				ShowNotifyToActiveUser("Info", "notification.prep.no_space_to_move_pan");
				return;
			}
			Transform transform = _gastroPanTargetPoints[availableTargetIndex];
			if (transform == null)
			{
				ServerClearMoveToTargetLock();
				return;
			}
			GastroPan movingPan = _activeGastroPan;
			if (!ServerIsValidPan(movingPan))
			{
				ServerClearMoveToTargetLock();
				_activeGastroPan = null;
				_isFallbackOccupied.Value = false;
				_prepStation.ServerSetStateRpc(0);
				_prepStation.ClientHideGastropanCenterInteractorRpc();
				_prepStation.ClientAutoSelectItemRpc();
				return;
			}
			ServerSetCenterPanPickupEnabled(movingPan, enabled: false);
			_targetOccupied[availableTargetIndex] = 1;
			_isFallbackOccupied.Value = false;
			movingPan.TargetIndex = availableTargetIndex;
			movingPan.CanGaze.Value = true;
			movingPan.CanHold.Value = false;
			_activeGastroPan = null;
			if (!_spawnedGastroPans.Contains(movingPan))
			{
				_spawnedGastroPans.Add(movingPan);
			}
			ClientMoveGastroPanToSlotRpc(movingPan.NetworkObject, transform.position, availableTargetIndex);
			_prepStation.ServerSetStateRpc(0);
			_prepStation.ClientAutoSelectItemRpc();
			_prepStation.ServerClearPendingProcessedOutputContext("Moved active GastroPan to target slot.");
			_prepStation.PlayPrepStationSfxForActiveUser(PrepStation.PrepStationSfxId.GastroPanMove);
			ServerGastropanRecallInteractorSet(value: true, availableTargetIndex);
			movingPan.ServerSetPrepStationToolTipDataRpc(availableTargetIndex);
			DOVirtual.DelayedCall(0.3f, () =>
			{
				if (!(this == null) && base.IsServer && ServerIsValidPan(movingPan))
				{
					movingPan.CanGaze.Value = true;
					movingPan.CanHold.Value = true;
				}
			});
			DOVirtual.DelayedCall(0.15f, () =>
			{
				if (!(this == null) && base.IsServer)
				{
					ServerClearMoveToTargetLock();
				}
			});
		}

		[Rpc(SendTo.Everyone)]
		private void ClientMoveGastroPanToSlotRpc(NetworkObjectReference objRef, Vector3 targetPosition, sbyte targetIndex)
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
				FastBufferWriter bufferWriter = __beginSendRpc(78208164u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in objRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in targetPosition);
				bufferWriter.WriteValueSafe(in targetIndex, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 78208164u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (objRef.TryGet(out var networkObject) && networkObject.TryGetComponent<GastroPan>(out var component))
			{
				if (_activeGastroPan == component)
				{
					_activeGastroPan = null;
				}
				ClientStopAttentionFeedback();
				ClientRegisterTargetPan(component, targetIndex);
				Transform target = networkObject.transform;
				target.DOKill(complete: true);
				float panMoveDuration = _panMoveDuration;
				Quaternion endValue = Quaternion.Euler(0f, 90f, 0f);
				target.DOMove(targetPosition, panMoveDuration).SetEase(Ease.OutCubic);
				target.DORotateQuaternion(endValue, panMoveDuration).SetEase(Ease.InOutSine);
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerRecallGastroPanToCenterRpc(int targetIndex)
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
				FastBufferWriter bufferWriter = __beginSendRpc(949233554u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, targetIndex);
				__endSendRpc(ref bufferWriter, 949233554u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			int targetIndex2 = targetIndex;
			if ((_prepStation != null && _prepStation.StaffEmployeeId != 0) || _prepStation.CurrentState != PrepStationState.TransferToGastroPan || _activeGastroPan != null || _isFallbackOccupied.Value || _recallGastroPanRequestInProgress)
			{
				return;
			}
			if (!_prepStation.ServerTryRepairTransferVisualBeforeGastroPan())
			{
				ShowNotifyToActiveUser("Prep Station", "notification.prep.lost_ingredient_recovered");
				_prepStation.ServerRecoverBrokenTransferToIdle("Missing pending processed output before GastroPan recall.");
				return;
			}
			_recallGastroPanRequestInProgress = true;
			DOVirtual.DelayedCall(_transferVisualRepairDelay, () =>
			{
				if (this == null || !base.IsServer)
				{
					_recallGastroPanRequestInProgress = false;
				}
				else
				{
					_recallGastroPanRequestInProgress = false;
					ServerRecallGastroPanToCenterAfterTransferVisualRepair(targetIndex2);
				}
			});
		}

		private void ServerRecallGastroPanToCenterAfterTransferVisualRepair(int targetIndex)
		{
			if (_prepStation.CurrentState != PrepStationState.TransferToGastroPan || _activeGastroPan != null || _isFallbackOccupied.Value || !ServerTryRepairTargetPanBeforeRecall(targetIndex, out var pan))
			{
				return;
			}
			string id = _prepStation.CurrentIngredientID.ToString();
			ItemSO itemSO = _prepStation.ItemDatabaseSO.FindById(id);
			ProcessedIngredientSO processedIngredientSO = null;
			if (itemSO is IngredientSO { ProcessedIngredients: var processedIngredients })
			{
				if (processedIngredients == null || processedIngredients.Count == 0)
				{
					return;
				}
				processedIngredientSO = ((_prepStation.CurrentKnifeID < processedIngredients.Count) ? processedIngredients[_prepStation.CurrentKnifeID] : processedIngredients.FirstOrDefault());
			}
			if (processedIngredientSO == null || processedIngredientSO.Id != pan.IngredientId.ToString())
			{
				ShowNotifyToActiveUser("Info", "notification.prep.ingredients_different");
				return;
			}
			if (!pan.ServerAddIngredientIfSpaceAvailable())
			{
				ShowNotifyToActiveUser("Info", "notification.prep.gastropan_full");
				return;
			}
			sbyte targetIndex2 = pan.TargetIndex;
			if (targetIndex2 >= 0 && targetIndex2 < _targetOccupied.Count)
			{
				_targetOccupied[targetIndex2] = 0;
				_spawnedGastroPans.Remove(pan);
				ServerGastropanRecallInteractorSet(value: false, targetIndex2);
			}
			_prepStation.ServerSetStateRpc(3);
			_activeGastroPan = pan;
			_activeGastroPan.TargetIndex = -1;
			ServerSetCenterPanPickupEnabled(_activeGastroPan, enabled: false);
			ClientMoveGastroPanToCenterRpc(pan.NetworkObject, _prepStation.GastropanCenterTransform.position, targetIndex2);
			ServerEnableCenterPanPickupAfterDelay(_activeGastroPan, 0.45f);
			_prepStation.ServerClearPendingProcessedOutputContext("Recalled target GastroPan to center.");
			_prepStation.PlayPrepStationSfxForActiveUser(PrepStation.PrepStationSfxId.GastroPanMove);
		}

		public void ServerMarkTargetAsFree(GastroPan gastroPan, int index)
		{
			if (base.IsServer && !(gastroPan == null))
			{
				if (index >= 0 && index < _targetOccupied.Count)
				{
					_spawnedGastroPans.Remove(gastroPan);
					_targetOccupied[index] = 0;
					ServerGastropanRecallInteractorSet(value: false, (sbyte)index);
				}
				if (_activeGastroPan == gastroPan)
				{
					_activeGastroPan = null;
					_isFallbackOccupied.Value = false;
				}
			}
		}

		public void ServerMarkCenterPanAsTaken()
		{
			if (base.IsServer)
			{
				_activeGastroPan = null;
				_prepStation.ServerSetStateRpc(0);
				_isFallbackOccupied.Value = false;
				_prepStation.ServerClearPendingProcessedOutputContext("Center GastroPan was taken.");
				_prepStation.ClientHideGastropanCenterInteractorRpc();
				_prepStation.ClientAutoSelectItemRpc();
			}
		}

		public bool ServerRequestSpawnAvailability()
		{
			if (base.IsServer)
			{
				ServerRepairTargetSlots("Spawn availability check.");
			}
			bool num = GetAvailableTargetIndex() == -1;
			bool value = _isFallbackOccupied.Value;
			return !(num & value);
		}

		private sbyte GetAvailableTargetIndex()
		{
			for (int i = 0; i < _targetOccupied.Count; i++)
			{
				if (_targetOccupied[i] == 0)
				{
					return (sbyte)i;
				}
			}
			return -1;
		}

		private void ClientRegisterTargetPan(GastroPan pan, int targetIndex)
		{
			if (!(pan == null) && targetIndex >= 0)
			{
				_clientTargetPans[targetIndex] = pan;
				if (!_spawnedGastroPans.Contains(pan))
				{
					_spawnedGastroPans.Add(pan);
				}
			}
		}

		private void ClientUnregisterTargetPan(int targetIndex)
		{
			if (targetIndex >= 0 && _clientTargetPans.TryGetValue(targetIndex, out var value))
			{
				_spawnedGastroPans.Remove(value);
				_clientTargetPans.Remove(targetIndex);
			}
		}

		private bool ClientTryGetTargetPan(int targetIndex, out GastroPan pan)
		{
			if (_clientTargetPans.TryGetValue(targetIndex, out pan) && pan != null)
			{
				return true;
			}
			pan = _spawnedGastroPans.FirstOrDefault((GastroPan x) => x != null && x.TargetIndex == targetIndex);
			return pan != null;
		}

		private void AnimateStackOnTake()
		{
			if (!(_gastropanParent == null))
			{
				Transform gastropanParent = _gastropanParent;
				CacheGastropanParentBase();
				gastropanParent.DOKill(complete: true);
				Vector3 gastropanParentBaseLocalPosition = _gastropanParentBaseLocalPosition;
				Vector3 gastropanParentBaseScale = _gastropanParentBaseScale;
				gastropanParent.localPosition = gastropanParentBaseLocalPosition;
				gastropanParent.localScale = gastropanParentBaseScale;
				Sequence s = DOTween.Sequence();
				s.Append(gastropanParent.DOLocalMoveY(gastropanParentBaseLocalPosition.y - 0.03f, 0.06f).SetEase(Ease.InQuad));
				s.Join(gastropanParent.DOScale(new Vector3(gastropanParentBaseScale.x, gastropanParentBaseScale.y * 0.96f, gastropanParentBaseScale.z), 0.06f).SetEase(Ease.InQuad));
				s.Append(gastropanParent.DOLocalMoveY(gastropanParentBaseLocalPosition.y, 0.1f).SetEase(Ease.OutQuad));
				s.Join(gastropanParent.DOScale(gastropanParentBaseScale, 0.1f).SetEase(Ease.OutBack));
				s.Join(gastropanParent.DOPunchRotation(new Vector3(0f, 0f, 2.5f), 0.18f, 3, 0.4f));
			}
		}

		public void ClientPlayClickableStackFeedback()
		{
			GastroPan gastroPan = ClientFindCompatibleTargetPan(_lastTransferProcessedId);
			ClientPlayAttentionFeedback((gastroPan != null) ? gastroPan.transform : _gastropanParent);
		}

		public void ClientPlayTransferTargetFeedback(FixedString64Bytes processedId)
		{
			_lastTransferProcessedId = processedId;
			GastroPan gastroPan = ClientFindCompatibleTargetPan(processedId);
			ClientPlayAttentionFeedback((gastroPan != null) ? gastroPan.transform : _gastropanParent);
		}

		public void ClientPlayCenterAttentionFeedback()
		{
			ClientPlayAttentionFeedback((_activeGastroPan != null) ? _activeGastroPan.transform : null);
		}

		public void ClientStopAttentionFeedback()
		{
			if (_attentionTween != null && _attentionTween.IsActive())
			{
				_attentionTween.Kill();
			}
			_attentionTween = null;
			if (_attentionTarget != null)
			{
				_attentionTarget.localScale = _attentionBaseScale;
			}
			_attentionTarget = null;
		}

		private GastroPan ClientFindCompatibleTargetPan(FixedString64Bytes processedId)
		{
			string id = processedId.ToString();
			if (string.IsNullOrWhiteSpace(id))
			{
				return null;
			}
			ItemSO itemSO = _prepStation.ItemDatabaseSO.FindById(id);
			int requiredSpace = ((!(itemSO is ProcessedIngredientSO processedIngredientSO)) ? 1 : processedIngredientSO.OutputYield);
			return _spawnedGastroPans.FirstOrDefault((GastroPan pan) => pan != null && pan.TargetIndex >= 0 && pan.IngredientId.ToString() == id && pan.MaxCapacity - pan.IngredientCount >= requiredSpace);
		}

		private void ClientPlayAttentionFeedback(Transform target)
		{
			if (target == null)
			{
				ClientStopAttentionFeedback();
			}
			else if (!(_attentionTarget == target) || _attentionTween == null || !_attentionTween.IsActive())
			{
				ClientStopAttentionFeedback();
				_attentionTarget = target;
				_attentionBaseScale = target.localScale;
				float duration = Mathf.Max(0.05f, _clickableStackTweenDuration);
				_attentionTween = target.DOScale(_attentionBaseScale * _clickableStackScalePunch, duration).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
					.SetLink(base.gameObject);
			}
		}

		private void CacheGastropanParentBase()
		{
			if (!(_gastropanParent == null) && !_gastropanParentBaseCached)
			{
				_gastropanParentBaseLocalPosition = _gastropanParent.localPosition;
				_gastropanParentBaseScale = _gastropanParent.localScale;
				_gastropanParentBaseCached = true;
			}
		}

		private void ShowNotifyToActiveUser(string header, string message)
		{
			if (!(_prepStation == null))
			{
				ulong ownerClientId = _prepStation.OwnerClientId;
				BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(ownerClientId, RpcTargetUse.Temp);
				ClientShowNotifyRpc(header, message, baseRpcTarget);
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientShowNotifyRpc(string header, string message, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(243907966u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bool value = header != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(header);
				}
				bool value2 = message != null;
				bufferWriter.WriteValueSafe(in value2, default(FastBufferWriter.ForPrimitives));
				if (value2)
				{
					bufferWriter.WriteValueSafe(message);
				}
				__endSendRpc(ref bufferWriter, 243907966u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!(Singleton<NotifyManager>.Instance == null))
				{
					Singleton<NotifyManager>.Instance.ShowToast(message);
				}
			}
		}

		public void ClientHoverEnterCenterPan()
		{
			if (!(_activeGastroPan == null))
			{
				PlayHoverTween(_activeGastroPan.transform);
			}
		}

		public void ClientHoverExitCenterPan()
		{
			if (!(_activeGastroPan == null))
			{
				ResetHoverTween(_activeGastroPan.transform);
			}
		}

		public void ClientHoverEnterTargetPan(int targetIndex)
		{
			if (ClientTryGetTargetPan(targetIndex, out var pan))
			{
				PlayHoverTween(pan.transform);
			}
		}

		public void ClientHoverExitTargetPan(int targetIndex)
		{
			if (ClientTryGetTargetPan(targetIndex, out var pan))
			{
				ResetHoverTween(pan.transform);
			}
		}

		private void PlayHoverTween(Transform target)
		{
			if (!(target == null))
			{
				KillHoverTween(target);
				_hoverBaseLocalPositions[target] = target.localPosition;
				Vector3 vector = _hoverBaseLocalPositions[target];
				_hoverMoveTweens[target] = target.DOLocalMoveY(vector.y + 0.015f, 0.12f).SetEase(Ease.OutQuad);
			}
		}

		private void ResetHoverTween(Transform target)
		{
			if (!(target == null))
			{
				KillHoverTween(target);
				Vector3 vector = (_hoverBaseLocalPositions.TryGetValue(target, out var value) ? value : target.localPosition);
				_hoverMoveTweens[target] = target.DOLocalMoveY(vector.y, 0.12f).SetEase(Ease.OutQuad);
			}
		}

		private void KillHoverTween(Transform target)
		{
			if (!(target == null))
			{
				if (_hoverMoveTweens.TryGetValue(target, out var value) && value != null && value.IsActive())
				{
					value.Kill();
				}
				_hoverMoveTweens.Remove(target);
			}
		}

		private bool ServerTryRepairMoveLockBeforeAction(string source)
		{
			if (!_moveToTargetInProgress)
			{
				return true;
			}
			if (!(Time.time - _moveToTargetStartedAt >= _moveToTargetLockTimeout))
			{
				return false;
			}
			ServerClearMoveToTargetLock();
			LogGastroPanActionRecovery("MoveToTarget lock timed out and was released. Source=" + source);
			return true;
		}

		private void ServerBeginMoveToTargetLock()
		{
			_moveToTargetInProgress = true;
			_moveToTargetStartedAt = Time.time;
		}

		private void ServerClearMoveToTargetLock()
		{
			_moveToTargetInProgress = false;
			_moveToTargetStartedAt = 0f;
		}

		private bool ServerTryRepairCenterPanBeforeMoveToTarget(string source)
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (!ServerTryRepairMoveLockBeforeAction(source))
			{
				return false;
			}
			if (_prepStation.CurrentState != PrepStationState.GastroPanMovingToTarget)
			{
				return false;
			}
			if (ServerIsValidPan(_activeGastroPan))
			{
				return true;
			}
			_activeGastroPan = null;
			_isFallbackOccupied.Value = false;
			ServerClearMoveToTargetLock();
			_prepStation.ServerSetStateRpc(0);
			_prepStation.ClientHideGastropanCenterInteractorRpc();
			_prepStation.ClientAutoSelectItemRpc();
			LogGastroPanActionRecovery("Missing or invalid center GastroPan repaired to Idle. Source=" + source);
			return false;
		}

		public void ServerRepairTargetSlots(string source)
		{
			if (!base.IsServer)
			{
				return;
			}
			ServerCleanInvalidSpawnedPans();
			for (int i = 0; i < _targetOccupied.Count; i++)
			{
				GastroPan gastroPan = ServerFindTargetPan(i);
				bool flag = ServerIsValidPan(gastroPan);
				if (_targetOccupied[i] == 1 && !flag)
				{
					_targetOccupied[i] = 0;
					ServerGastropanRecallInteractorSet(value: false, (sbyte)i);
					LogGastroPanActionRecovery($"Stale occupied target slot repaired. Source={source}, TargetIndex={i}");
				}
				else if ((_targetOccupied[i] == 0) & flag)
				{
					_targetOccupied[i] = 1;
					ServerGastropanRecallInteractorSet(value: true, (sbyte)i);
					gastroPan.ServerSetPrepStationToolTipDataRpc((sbyte)i);
					LogGastroPanActionRecovery($"Missing occupied target slot repaired. Source={source}, TargetIndex={i}");
				}
			}
		}

		public bool ServerResetRuntimeForEndDay(string source)
		{
			if (!base.IsServer)
			{
				return false;
			}
			_spawnGastroPanRequestInProgress = false;
			_recallGastroPanRequestInProgress = false;
			ServerClearMoveToTargetLock();
			ServerRepairTargetSlots(source);
			bool flag = false;
			if (ServerIsValidPan(_activeGastroPan))
			{
				flag = !ServerTryMoveActivePanToAvailableTargetForReset(source);
				if (flag)
				{
					_isFallbackOccupied.Value = true;
					_activeGastroPan.TargetIndex = -1;
					ServerSetCenterPanPickupEnabled(_activeGastroPan, enabled: true);
					LogGastroPanActionRecovery("Active center GastroPan preserved because no target slot is free. Source=" + source);
				}
			}
			else
			{
				_activeGastroPan = null;
				_isFallbackOccupied.Value = false;
			}
			ServerRepairTargetSlots(source);
			return flag;
		}

		private bool ServerTryMoveActivePanToAvailableTargetForReset(string source)
		{
			if (!ServerIsValidPan(_activeGastroPan))
			{
				return false;
			}
			sbyte availableTargetIndex = GetAvailableTargetIndex();
			if (availableTargetIndex < 0)
			{
				return false;
			}
			Transform transform = _gastroPanTargetPoints[availableTargetIndex];
			if (transform == null)
			{
				return false;
			}
			GastroPan activeGastroPan = _activeGastroPan;
			_targetOccupied[availableTargetIndex] = 1;
			_isFallbackOccupied.Value = false;
			activeGastroPan.TargetIndex = availableTargetIndex;
			activeGastroPan.CanGaze.Value = true;
			activeGastroPan.CanHold.Value = true;
			_activeGastroPan = null;
			if (!_spawnedGastroPans.Contains(activeGastroPan))
			{
				_spawnedGastroPans.Add(activeGastroPan);
			}
			ClientMoveGastroPanToSlotRpc(activeGastroPan.NetworkObject, transform.position, availableTargetIndex);
			ServerGastropanRecallInteractorSet(value: true, availableTargetIndex);
			activeGastroPan.ServerSetPrepStationToolTipDataRpc(availableTargetIndex);
			LogGastroPanActionRecovery($"Active center GastroPan moved to target slot during reset. Source={source}, TargetIndex={availableTargetIndex}");
			return true;
		}

		private bool ServerTryRepairTargetPanBeforeRecall(int targetIndex, out GastroPan pan)
		{
			pan = null;
			if (!base.IsServer)
			{
				return false;
			}
			if (targetIndex < 0 || targetIndex >= _targetOccupied.Count)
			{
				LogGastroPanActionRecovery($"Recall blocked. Invalid target index: {targetIndex}");
				return false;
			}
			ServerCleanInvalidSpawnedPans();
			pan = ServerFindTargetPan(targetIndex);
			if (!ServerIsValidPan(pan))
			{
				_targetOccupied[targetIndex] = 0;
				ServerGastropanRecallInteractorSet(value: false, (sbyte)targetIndex);
				LogGastroPanActionRecovery($"Recall target had no valid pan. Slot freed. TargetIndex={targetIndex}");
				return false;
			}
			if (_targetOccupied[targetIndex] == 0)
			{
				_targetOccupied[targetIndex] = 1;
				ServerGastropanRecallInteractorSet(value: true, (sbyte)targetIndex);
				pan.ServerSetPrepStationToolTipDataRpc((sbyte)targetIndex);
				LogGastroPanActionRecovery($"Recall target occupancy repaired. TargetIndex={targetIndex}");
			}
			pan.CanGaze.Value = true;
			pan.CanHold.Value = true;
			return true;
		}

		private void ServerCleanInvalidSpawnedPans()
		{
			for (int num = _spawnedGastroPans.Count - 1; num >= 0; num--)
			{
				GastroPan pan = _spawnedGastroPans[num];
				if (!ServerIsValidPan(pan))
				{
					_spawnedGastroPans.RemoveAt(num);
				}
			}
		}

		private GastroPan ServerFindTargetPan(int targetIndex)
		{
			for (int i = 0; i < _spawnedGastroPans.Count; i++)
			{
				GastroPan gastroPan = _spawnedGastroPans[i];
				if (ServerIsValidPan(gastroPan) && gastroPan.TargetIndex == targetIndex)
				{
					return gastroPan;
				}
			}
			return null;
		}

		private bool ServerIsValidPan(GastroPan pan)
		{
			if (pan != null && pan.NetworkObject != null)
			{
				return pan.NetworkObject.IsSpawned;
			}
			return false;
		}

		private void LogGastroPanActionRecovery(string message)
		{
			if (_logGastroPanActionRecovery)
			{
				UtilityMethods.Log("<b><color=#E6A23C>[GastroPanSpawner Action Recovery]</color></b> " + base.name + "\n" + message, LogType.Warning);
			}
		}

		public void ServerHandleCompletedProcessedOutput(FixedString64Bytes processedId, int outputYield)
		{
			if (!base.IsServer || outputYield <= 0)
			{
				return;
			}
			DOVirtual.DelayedCall(_postCutTransferDecisionDelay, () =>
			{
				if (!(this == null) && base.IsServer && _prepStation.CurrentState == PrepStationState.TransferToGastroPan)
				{
					ServerRepairTargetSlots("Post-cut automatic transfer decision.");
					GastroPan gastroPan = ServerFindCompatibleTargetPan(processedId, outputYield);
					if (gastroPan != null)
					{
						ServerTransferPendingIngredientsToTargetPan(gastroPan.TargetIndex);
					}
					else
					{
						_prepStation.ClientLiftIngredientsForGastroPanRpc();
						DOVirtual.DelayedCall(_incomingPanLiftLeadTime, () =>
						{
							if (!(this == null) && base.IsServer && _prepStation.CurrentState == PrepStationState.TransferToGastroPan)
							{
								ServerBeginSpawnGastroPanForPendingOutput();
								DOVirtual.DelayedCall(_transferVisualRepairDelay + _panMoveDuration + _directTransferDuration + (float)Mathf.Max(0, outputYield - 1) * _directTransferStagger + 0.12f, () =>
								{
									if (!(this == null) && base.IsServer && _prepStation.CurrentState == PrepStationState.GastroPanMovingToTarget)
									{
										ServerMoveActiveGastroPanToNextTarget();
									}
								});
							}
						});
					}
				}
			});
		}

		[Rpc(SendTo.Server)]
		public void ServerTransferPendingIngredientsToTargetPanRpc(int targetIndex)
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
				FastBufferWriter bufferWriter = __beginSendRpc(560179401u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, targetIndex);
				__endSendRpc(ref bufferWriter, 560179401u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ServerTransferPendingIngredientsToTargetPan(targetIndex);
			}
		}

		private void ServerTransferPendingIngredientsToTargetPan(int targetIndex)
		{
			if (!base.IsServer || _directTransferInProgress || _prepStation.CurrentState != PrepStationState.TransferToGastroPan)
			{
				return;
			}
			if (!_prepStation.ServerTryGetPendingProcessedOutput(out var processedId, out var outputYield))
			{
				_prepStation.ServerRecoverBrokenTransferToIdle("Missing pending processed output before direct GastroPan transfer.");
				return;
			}
			ServerRepairTargetSlots("Before direct GastroPan transfer.");
			if (!ServerTryRepairTargetPanBeforeRecall(targetIndex, out var pan))
			{
				return;
			}
			if (pan.IngredientId != processedId)
			{
				ShowNotifyToActiveUser("Info", "notification.prep.ingredients_different");
				return;
			}
			if (pan.MaxCapacity - pan.IngredientCount < outputYield)
			{
				ShowNotifyToActiveUser("Info", "notification.prep.gastropan_full");
				return;
			}
			ServerSetCenterPanPickupEnabled(pan, enabled: false);
			if (!pan.ServerAddIngredientIfSpaceAvailable())
			{
				ServerSetCenterPanPickupEnabled(pan, enabled: true);
				ShowNotifyToActiveUser("Info", "notification.prep.gastropan_full");
				return;
			}
			_directTransferInProgress = true;
			ClientTransferIngredientsDirectlyToTargetPanRpc(pan.NetworkObject, _directTransferDuration, _directTransferStagger);
			_prepStation.ServerClearPendingProcessedOutputContext("Transferred processed output directly to target GastroPan.");
			DOVirtual.DelayedCall(_directTransferDuration + (float)Mathf.Max(0, outputYield - 1) * _directTransferStagger + 0.08f, () =>
			{
				_directTransferInProgress = false;
				if (!(this == null) && base.IsServer)
				{
					if (ServerIsValidPan(pan) && pan.TargetIndex >= 0)
					{
						ServerSetCenterPanPickupEnabled(pan, enabled: true);
					}
					if (_prepStation.CurrentState == PrepStationState.TransferToGastroPan)
					{
						_prepStation.ServerSetStateRpc(0);
						_prepStation.ClientHideGastropanCenterInteractorRpc();
						_prepStation.ClientAutoSelectItemRpc();
					}
				}
			});
		}

		private GastroPan ServerFindCompatibleTargetPan(FixedString64Bytes processedId, int requiredSpace)
		{
			for (int i = 0; i < _spawnedGastroPans.Count; i++)
			{
				GastroPan gastroPan = _spawnedGastroPans[i];
				if (ServerIsValidPan(gastroPan) && gastroPan.TargetIndex >= 0 && !(gastroPan.IngredientId != processedId) && gastroPan.MaxCapacity - gastroPan.IngredientCount >= requiredSpace)
				{
					return gastroPan;
				}
			}
			return null;
		}

		[Rpc(SendTo.Everyone)]
		private void ClientTransferIngredientsDirectlyToTargetPanRpc(NetworkObjectReference panReference, float duration, float stagger)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3068638876u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in panReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in duration, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in stagger, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3068638876u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (panReference.TryGet(out var networkObject) && !(networkObject == null))
				{
					ClientStopAttentionFeedback();
					_prepStation.ClientTransferIngredientsDirectlyToGastroPanLocal(networkObject.transform, duration, showCenterInteractorWhenComplete: false, stagger);
				}
			}
		}

		public bool ServerStaffDispatchPan(int employeeId, GastroPan pan, Transform overflow, bool orderedSlot = false)
		{
			if (!this.CanWriteServerState() || _prepStation == null || _prepStation.StaffEmployeeId != employeeId || pan == null || !pan.IsSpawned || pan.IsStaffCarried || pan.IsPlayerInventoryOwnedForPersistence)
			{
				return false;
			}
			ServerRepairTargetSlots("Staff dispatch");
			sbyte b = (sbyte)(orderedSlot ? (-1) : GetAvailableTargetIndex());
			Transform transform = ((b >= 0) ? _gastroPanTargetPoints[b] : overflow);
			if (transform == null)
			{
				return false;
			}
			pan.ServerStaffDetachFromStation(employeeId);
			pan.CanHold.Value = true;
			if (!pan.ServerTryStaffPickup(employeeId))
			{
				return false;
			}
			pan.ServerStaffDrop(employeeId, pan.transform.position, pan.transform.rotation);
			pan.CanHold.Value = false;
			pan.CanGaze.Value = false;
			if (b >= 0)
			{
				pan.ServerStaffAssignTarget(this, b);
				_targetOccupied[b] = 1;
				if (!_spawnedGastroPans.Contains(pan))
				{
					_spawnedGastroPans.Add(pan);
				}
				ClientMoveGastroPanToSlotRpc(pan.NetworkObject, transform.position, b);
				ServerGastropanRecallInteractorSet(value: true, b);
				pan.ServerSetPrepStationToolTipDataRpc(b);
			}
			else
			{
				ClientStaffMoveOverflowRpc(pan.NetworkObject, transform.position, transform.rotation);
			}
			DOVirtual.DelayedCall(Mathf.Max(0.01f, _panMoveDuration), () =>
			{
				if (pan.CanWriteServerState())
				{
					pan.CanHold.Value = true;
					pan.CanGaze.Value = true;
				}
			}).SetUpdate(isIndependentUpdate: false);
			return true;
		}

		[Rpc(SendTo.Everyone)]
		private void ClientStaffMoveOverflowRpc(NetworkObjectReference reference, Vector3 position, Quaternion rotation)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3836676192u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in reference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in position);
				bufferWriter.WriteValueSafe(in rotation);
				__endSendRpc(ref bufferWriter, 3836676192u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (reference.TryGetComponent<GastroPan>(out var component, base.NetworkManager))
				{
					component.transform.DOKill(complete: true);
					component.transform.DOMove(position, _panMoveDuration).SetEase(Ease.OutCubic);
					component.transform.DORotateQuaternion(rotation, _panMoveDuration).SetEase(Ease.InOutSine);
				}
			}
		}

		protected override void __initializeVariables()
		{
			if (_targetOccupied == null)
			{
				throw new Exception("GastroPanSpawner._targetOccupied cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_targetOccupied.Initialize(this);
			__nameNetworkVariable(_targetOccupied, "_targetOccupied");
			NetworkVariableFields.Add(_targetOccupied);
			if (_isFallbackOccupied == null)
			{
				throw new Exception("GastroPanSpawner._isFallbackOccupied cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isFallbackOccupied.Initialize(this);
			__nameNetworkVariable(_isFallbackOccupied, "_isFallbackOccupied");
			NetworkVariableFields.Add(_isFallbackOccupied);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(1814206290u, __rpc_handler_1814206290, "ServerTrySpawnGastroPanRpc", RpcInvokePermission.Everyone);
			__registerRpc(4066517250u, __rpc_handler_4066517250, "ClientGastropanRecallInteractorSetRpc", RpcInvokePermission.Everyone);
			__registerRpc(727652366u, __rpc_handler_727652366, "ClientSetPrepStationToolTipDataRpc", RpcInvokePermission.Everyone);
			__registerRpc(542006931u, __rpc_handler_542006931, "ClientToolTipIngredientCountUpdateRpc", RpcInvokePermission.Everyone);
			__registerRpc(1473403869u, __rpc_handler_1473403869, "ClientMoveGastroPanToCenterRpc", RpcInvokePermission.Everyone);
			__registerRpc(2581415247u, __rpc_handler_2581415247, "ServerMoveActiveGastroPanToNextTargetRpc", RpcInvokePermission.Everyone);
			__registerRpc(78208164u, __rpc_handler_78208164, "ClientMoveGastroPanToSlotRpc", RpcInvokePermission.Everyone);
			__registerRpc(949233554u, __rpc_handler_949233554, "ServerRecallGastroPanToCenterRpc", RpcInvokePermission.Everyone);
			__registerRpc(243907966u, __rpc_handler_243907966, "ClientShowNotifyRpc", RpcInvokePermission.Everyone);
			__registerRpc(560179401u, __rpc_handler_560179401, "ServerTransferPendingIngredientsToTargetPanRpc", RpcInvokePermission.Everyone);
			__registerRpc(3068638876u, __rpc_handler_3068638876, "ClientTransferIngredientsDirectlyToTargetPanRpc", RpcInvokePermission.Everyone);
			__registerRpc(3836676192u, __rpc_handler_3836676192, "ClientStaffMoveOverflowRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_1814206290(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((GastroPanSpawner)target).ServerTrySpawnGastroPanRpc();
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4066517250(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out sbyte value2, default(FastBufferWriter.ForPrimitives));
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((GastroPanSpawner)target).ClientGastropanRecallInteractorSetRpc(value, value2);
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_727652366(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out sbyte value3, default(FastBufferWriter.ForPrimitives));
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((GastroPanSpawner)target).ClientSetPrepStationToolTipDataRpc(value, value2, value3);
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_542006931(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out sbyte value2, default(FastBufferWriter.ForPrimitives));
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((GastroPanSpawner)target).ClientToolTipIngredientCountUpdateRpc(value, value2);
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1473403869(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out sbyte value3, default(FastBufferWriter.ForPrimitives));
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((GastroPanSpawner)target).ClientMoveGastroPanToCenterRpc(value, value2, value3);
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2581415247(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((GastroPanSpawner)target).ServerMoveActiveGastroPanToNextTargetRpc();
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_78208164(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out sbyte value3, default(FastBufferWriter.ForPrimitives));
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((GastroPanSpawner)target).ClientMoveGastroPanToSlotRpc(value, value2, value3);
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_949233554(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((GastroPanSpawner)target).ServerRecallGastroPanToCenterRpc(value);
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_243907966(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				string s2 = null;
				if (value2)
				{
					reader.ReadValueSafe(out s2, false);
				}
				RpcParams ext = rpcParams.Ext;
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((GastroPanSpawner)target).ClientShowNotifyRpc(s, s2, ext);
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_560179401(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((GastroPanSpawner)target).ServerTransferPendingIngredientsToTargetPanRpc(value);
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3068638876(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value3, default(FastBufferWriter.ForPrimitives));
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((GastroPanSpawner)target).ClientTransferIngredientsDirectlyToTargetPanRpc(value, value2, value3);
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3836676192(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out Quaternion value3);
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((GastroPanSpawner)target).ClientStaffMoveOverflowRpc(value, value2, value3);
				((GastroPanSpawner)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "GastroPanSpawner";
		}
	}
}
