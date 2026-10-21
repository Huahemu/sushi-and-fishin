using System;
using System.Linq;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Interaction.ToolTip;
using PaperPixelGames.Core.Kitchen.GastronormPanIngredients;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.ProcessedIngredient;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Indicator;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Kitchen.PreparationStation.GastronomPan
{
	public class GastroPan : BaseHoldable, ISaveable<GastropanData>
	{
		[SerializeField]
		private ItemDatabaseSO _itemDatabaseSO;

		[SerializeField]
		private GameObject _modelOBJ;

		[Header("Ingredient Fill Feel")]
		[SerializeField]
		[Min(0.01f)]
		private float _ingredientRevealInterval = 0.045f;

		private NetworkVariable<byte> _ingredientType = new NetworkVariable<byte>(0);

		private NetworkVariable<byte> _ingredientCount = new NetworkVariable<byte>(0);

		private NetworkVariable<FixedString64Bytes> _ingredientID = new NetworkVariable<FixedString64Bytes>();

		private NetworkVariable<byte> _maxCapacity = new NetworkVariable<byte>(0);

		private NetworkVariable<sbyte> _targetIndex = new NetworkVariable<sbyte>(-1);

		private NetworkVariable<NetworkObjectReference> _prepStationSpawnerReference = new NetworkVariable<NetworkObjectReference>();

		private bool _serverPrepStationSlotReleased;

		private GastroPanSpawner _gastroPanSpawner;

		private PanIngredientGraphics _originalOBJPanIngredientGraphics;

		private PanIngredientGraphics _fpsOBJPanIngredientGraphics;

		private PanIngredientGraphics _tpsOBJPanIngredientGraphics;

		private ProcessedIngredientSO _processedIngredientSO;

		private bool _ingredientReady;

		private bool _fpsShadowReady;

		private bool _tpsShadowReady;

		private bool _fpsGraphicsBuilt;

		private bool _tpsGraphicsBuilt;

		private bool _lastIsOwnerFlag;

		private bool _originalRootReady;

		private bool _originalGraphicsBuilt;

		private NetworkVariable<bool> _originalRootReadyNetworkVariable = new NetworkVariable<bool>(value: false);

		private bool _originalBuildPending;

		private FixedString64Bytes _pendingOriginalId;

		private bool _wasOriginalModelActive;

		public byte IngredientType => _ingredientType.Value;

		public byte IngredientCount
		{
			get
			{
				return _ingredientCount.Value;
			}
			set
			{
				_ingredientCount.Value = value;
			}
		}

		public FixedString64Bytes IngredientId => _ingredientID.Value;

		public byte MaxCapacity => _maxCapacity.Value;

		public override string QuickSlotDisplayName
		{
			get
			{
				if (_processedIngredientSO == null)
				{
					return base.QuickSlotDisplayName;
				}
				return $"{_processedIngredientSO.LocalizedName} ({_ingredientCount.Value}/{_maxCapacity.Value})";
			}
		}

		public override Sprite QuickSlotDisplaySprite
		{
			get
			{
				if (!(_processedIngredientSO != null) || !(_processedIngredientSO.Sprite != null))
				{
					return base.QuickSlotDisplaySprite;
				}
				return _processedIngredientSO.Sprite;
			}
		}

		public sbyte TargetIndex
		{
			get
			{
				return _targetIndex.Value;
			}
			set
			{
				if (base.IsServer)
				{
					_targetIndex.Value = value;
				}
			}
		}

		public override bool TryGetScanIndicatorVisualSettings(out OffscreenIndicatorVisualSettings settings)
		{
			settings = new OffscreenIndicatorVisualSettings
			{
				IconSprite = QuickSlotDisplaySprite,
				ApplyDistanceVisible = true,
				DistanceVisible = false
			};
			return true;
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<FixedString64Bytes> ingredientID = _ingredientID;
			ingredientID.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Combine(ingredientID.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(OnIngredientIdChanged));
			NetworkVariable<byte> ingredientCount = _ingredientCount;
			ingredientCount.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(ingredientCount.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnIngredientCountChanged));
			NetworkVariable<NetworkObjectReference> prepStationSpawnerReference = _prepStationSpawnerReference;
			prepStationSpawnerReference.OnValueChanged = (NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate)Delegate.Combine(prepStationSpawnerReference.OnValueChanged, new NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate(OnPrepStationSpawnerReferenceChanged));
			string text = _ingredientID.Value.ToString();
			_ingredientReady = IsValidIngredientId(text);
			_originalRootReady = _originalRootReadyNetworkVariable.Value;
			if (IsValidIngredientId(text))
			{
				ClientSetProcessedIngredientSO_Local(text);
			}
			ClientSyncAllGraphicsToCount(_ingredientCount.Value, animateIncrease: false, 0);
			ClientRefreshQuickSlotDisplay();
			ClientTryBuildOriginalGraphics();
			ClientTryBuildFpsGraphics();
			ClientTryBuildTpsGraphics();
			if (!base.IsServer)
			{
				ClientNotifyOwningPrepStationSpawner();
			}
			if (base.IsServer)
			{
				Singleton<GameProgressManager>.Instance.RestaurantManager.RegisterGastropanData(this);
			}
		}

		public override void OnNetworkDespawn()
		{
			if (base.IsServer)
			{
				ServerReleasePrepStationSlotWithoutReward("OnNetworkDespawn");
				Singleton<GameProgressManager>.Instance.RestaurantManager.DeRegisterGastropanData(this);
			}
			NetworkVariable<FixedString64Bytes> ingredientID = _ingredientID;
			ingredientID.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Remove(ingredientID.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(OnIngredientIdChanged));
			NetworkVariable<byte> ingredientCount = _ingredientCount;
			ingredientCount.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(ingredientCount.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnIngredientCountChanged));
			NetworkVariable<NetworkObjectReference> prepStationSpawnerReference = _prepStationSpawnerReference;
			prepStationSpawnerReference.OnValueChanged = (NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate)Delegate.Remove(prepStationSpawnerReference.OnValueChanged, new NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate(OnPrepStationSpawnerReferenceChanged));
			base.OnNetworkDespawn();
		}

		public override void OnDestroy()
		{
			if (Application.isPlaying && base.IsServer)
			{
				ServerReleasePrepStationSlotWithoutReward("OnDestroy");
				base.OnDestroy();
			}
		}

		protected override bool CanCrossTrigger(Interactor interactor, BaseHoldable holdable)
		{
			if (!base.CanCrossTrigger(interactor, holdable))
			{
				return false;
			}
			return true;
		}

		protected override void ClientCrossMouse1(Interactor interactor, BaseHoldable holdable, InputActionPhase phase)
		{
			if (phase != InputActionPhase.Started)
			{
				return;
			}
			base.RpcTarget.Single(interactor.OwnerClientId, RpcTargetUse.Temp);
			bool num = holdable is GastroPan;
			bool flag = holdable is IngredientEntity;
			if (!num && !flag)
			{
				TryPickup();
				return;
			}
			string text;
			int ingredientCount;
			if (holdable is GastroPan { IngredientId: var ingredientId } gastroPan)
			{
				text = ingredientId.ToString();
				ingredientCount = gastroPan.IngredientCount;
			}
			else
			{
				IngredientEntity obj = (IngredientEntity)holdable;
				text = obj.ItemSO.Id;
				ingredientCount = obj.IngredientCount;
			}
			if (!((FixedString64Bytes)text == _ingredientID.Value))
			{
				TryPickup();
				return;
			}
			int num2 = MaxCapacity - _ingredientCount.Value;
			if (num2 <= 0)
			{
				TryPickup();
				return;
			}
			int num3 = Mathf.Min(ingredientCount, num2);
			if (num3 <= 0)
			{
				TryPickup();
				return;
			}
			byte num4 = (byte)ingredientCount;
			ServerIngredientCountUpdateRpc((byte)num3);
			if (holdable is GastroPan gastroPan2)
			{
				gastroPan2.ServerConsumeIngredientsRpc((byte)num3);
			}
			else
			{
				((IngredientEntity)holdable).ServerConsumeIngredientsRpc((byte)num3);
			}
			if (num4 - num3 <= 0)
			{
				Singleton<QuickSlotHandler>.Instance.DestroyCurrentItem();
			}
			bool TryPickup()
			{
				base.ClientCrossMouse1(interactor, holdable, phase);
				return true;
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerIngredientCountUpdateRpc(byte count)
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
				FastBufferWriter bufferWriter = __beginSendRpc(264515628u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in count, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 264515628u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (count != 0)
			{
				byte value = _ingredientCount.Value;
				int num = Mathf.Clamp(max: _maxCapacity.Value, value: value + count, min: 0);
				_ingredientCount.Value = (byte)num;
				if (_gastroPanSpawner != null)
				{
					_gastroPanSpawner.ClientToolTipIngredientCountUpdateRpc(_ingredientCount.Value, TargetIndex);
				}
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerConsumeIngredientsRpc(byte amount)
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
				FastBufferWriter bufferWriter = __beginSendRpc(438246081u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in amount, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 438246081u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && amount != 0)
				{
					int ingredientCount = IngredientCount;
					int num = Mathf.Max(0, ingredientCount - amount);
					_ingredientCount.Value = (byte)num;
				}
			}
		}

		public void ServerAssignPrepStationSpawner(GastroPanSpawner spawner)
		{
			if (base.IsServer && !(spawner == null))
			{
				_gastroPanSpawner = spawner;
				_prepStationSpawnerReference.Value = spawner.NetworkObject;
			}
		}

		public bool ServerInitializePrepStationIngredientData(GastroPanSpawner spawner)
		{
			ServerAssignPrepStationSpawner(spawner);
			if (_gastroPanSpawner == null || _gastroPanSpawner.PrepStation == null)
			{
				UtilityMethods.Log("[GastroPan - ServerInitializePrepStationIngredientData] Spawner veya PrepStation referansı null!");
				return false;
			}
			if (_itemDatabaseSO == null)
			{
				UtilityMethods.Log("[GastroPan - ServerInitializePrepStationIngredientData] ItemDatabaseSO referansı null!");
				return false;
			}
			string text = _gastroPanSpawner.PrepStation.CurrentIngredientID.ToString();
			int currentKnifeID = _gastroPanSpawner.PrepStation.CurrentKnifeID;
			return ServerTryResolveProcessedIngredient(text, currentKnifeID) != null;
		}

		public void ServerGastropanSpawnerInitialize(GastroPanSpawner spawner)
		{
			if (ServerInitializePrepStationIngredientData(spawner))
			{
				ClientInitializeOriginalPanGraphicsRpc(_ingredientID.Value);
				ClientSetProcessedIngredientSORpc(_ingredientID.Value);
				_originalRootReadyNetworkVariable.Value = true;
			}
		}

		public void ServerSetPrepStationToolTipDataRpc(sbyte gastropanIndex)
		{
			if (!(_gastroPanSpawner == null))
			{
				_gastroPanSpawner.ClientSetPrepStationToolTipDataRpc(_ingredientID.Value, _ingredientCount.Value, gastropanIndex);
			}
		}

		public void ServerNonChefDeskInitialize(FixedString64Bytes processedIngredientID, byte count)
		{
			if (!(ServerTryResolveProcessedIngredient(processedIngredientID, -1, count) == null))
			{
				ClientInitializeOriginalPanGraphicsRpc(_ingredientID.Value);
				ClientSetProcessedIngredientSORpc(_ingredientID.Value);
				_originalRootReadyNetworkVariable.Value = true;
			}
		}

		private ProcessedIngredientSO ServerTryResolveProcessedIngredient(FixedString64Bytes ingredientId, int knifeID, byte? overrideCount = null)
		{
			ItemSO itemSO = _itemDatabaseSO.FindById(ingredientId.ToString());
			if (itemSO == null)
			{
				UtilityMethods.Log($"[Initialize] ID: {ingredientId} için ItemSO bulunamadı.");
				return null;
			}
			ProcessedIngredientSO processedIngredientSO = null;
			if (itemSO is IngredientSO { ProcessedIngredients: var processedIngredients })
			{
				if (processedIngredients == null || processedIngredients.Count == 0)
				{
					UtilityMethods.Log("[Initialize] IngredientSO için ProcessedIngredients listesi boş!");
					return null;
				}
				processedIngredientSO = ((knifeID >= 0 && knifeID < processedIngredients.Count) ? processedIngredients[knifeID] : processedIngredients.FirstOrDefault());
			}
			else if (itemSO is ProcessedIngredientSO processedIngredientSO2)
			{
				processedIngredientSO = processedIngredientSO2;
			}
			if (processedIngredientSO == null)
			{
				UtilityMethods.Log($"[Initialize] Seçilen ProcessedIngredient null! ID: {ingredientId}");
				return null;
			}
			_ingredientType.Value = (byte)processedIngredientSO.IngredientType;
			_ingredientID.Value = processedIngredientSO.Id;
			_ingredientCount.Value = overrideCount ?? processedIngredientSO.OutputYield;
			_maxCapacity.Value = processedIngredientSO.MaxPanCapacity;
			return processedIngredientSO;
		}

		[Rpc(SendTo.Everyone)]
		private void ClientInitializeOriginalPanGraphicsRpc(FixedString64Bytes ingredientId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(415023493u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in ingredientId, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 415023493u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_originalRootReady = true;
				ClientTryBuildOriginalGraphics(ingredientId);
			}
		}

		public bool ServerAddIngredientIfSpaceAvailable()
		{
			int num = MaxCapacity - _ingredientCount.Value;
			if (num <= 0)
			{
				return false;
			}
			string id = _ingredientID.Value.ToString();
			if (_itemDatabaseSO.FindById(id) is ProcessedIngredientSO processedIngredientSO)
			{
				int num2 = Mathf.Min(num, processedIngredientSO.OutputYield);
				_ingredientCount.Value += (byte)num2;
				if (_gastroPanSpawner != null && TargetIndex >= 0)
				{
					_gastroPanSpawner.ClientToolTipIngredientCountUpdateRpc(_ingredientCount.Value, TargetIndex);
				}
				UtilityMethods.Log($"{num2} malzeme eklendi. Yeni toplam: {_ingredientCount.Value}");
				return true;
			}
			return false;
		}

		public void ServerStaffDetachFromStation(int employeeId)
		{
			if (base.IsServer && _gastroPanSpawner != null && _gastroPanSpawner.PrepStation.StaffEmployeeId == employeeId)
			{
				ServerReleasePrepStationSlotWithoutReward("Staff dispatch");
				if (base.transform.parent != null)
				{
					base.NetworkObject.TryRemoveParent();
				}
			}
		}

		public void ServerStaffAssignTarget(GastroPanSpawner spawner, sbyte index)
		{
			if (base.IsServer && base.IsSpawned && !(spawner == null))
			{
				ServerAssignPrepStationSpawner(spawner);
				_serverPrepStationSlotReleased = false;
				TargetIndex = index;
			}
		}

		private void ServerReleasePrepStationSlotWithoutReward(string source)
		{
			if (base.IsServer && !(base.NetworkManager == null) && base.NetworkManager.IsListening && !base.NetworkManager.ShutdownInProgress && !_serverPrepStationSlotReleased && !(_gastroPanSpawner == null))
			{
				sbyte targetIndex = TargetIndex;
				GastroPanSpawner gastroPanSpawner = _gastroPanSpawner;
				_serverPrepStationSlotReleased = true;
				if (targetIndex >= 0)
				{
					gastroPanSpawner.ServerMarkTargetAsFree(this, targetIndex);
					UtilityMethods.Log("<b><color=#E6A23C>[GastroPan Recovery]</color></b> Target slot released without pickup.\nSource: " + source + "\n" + $"TargetIndex: {targetIndex}", LogType.Warning);
				}
				else
				{
					gastroPanSpawner.ServerMarkCenterPanAsTaken();
					UtilityMethods.Log("<b><color=#E6A23C>[GastroPan Recovery]</color></b> Center pan released without pickup.\nSource: " + source, LogType.Warning);
				}
				if (base.NetworkObject != null && base.NetworkObject.IsSpawned)
				{
					TargetIndex = -1;
				}
				_gastroPanSpawner = null;
			}
		}

		private void ServerReleasePrepStationSlotFromPickup()
		{
			if (base.IsServer && !_serverPrepStationSlotReleased && !(_gastroPanSpawner == null))
			{
				sbyte targetIndex = TargetIndex;
				GastroPanSpawner gastroPanSpawner = _gastroPanSpawner;
				_serverPrepStationSlotReleased = true;
				if (targetIndex >= 0)
				{
					gastroPanSpawner.ServerMarkTargetAsFree(this, targetIndex);
					UtilityMethods.Log($"Target pozisyon boşaltıldı: {targetIndex}");
				}
				else
				{
					gastroPanSpawner.ServerMarkCenterPanAsTaken();
					UtilityMethods.Log("Fallback pozisyon boşaltıldı.");
				}
				if (Singleton<GameProgressManager>.Instance.ItemDatabaseSO.CucumberLongStripSO != null && IngredientId.ToString() == Singleton<GameProgressManager>.Instance.ItemDatabaseSO.CucumberLongStripSO.Id)
				{
					QuestReferenceHub.Instance.QuestComplete(QuestId.TakeSlicedCucumber);
				}
				if (Singleton<GameProgressManager>.Instance.ItemDatabaseSO.MackeralNigiriSO != null && IngredientId.ToString() == Singleton<GameProgressManager>.Instance.ItemDatabaseSO.MackeralNigiriSO.Id)
				{
					QuestReferenceHub.Instance.QuestComplete(QuestId.TakeSlicedMackerel);
				}
				TargetIndex = -1;
				_gastroPanSpawner = null;
			}
		}

		public override void ServerHold(Interactor interactor)
		{
			base.ServerHold(interactor);
			if (_gastroPanSpawner != null)
			{
				ServerReleaseTargetRpc();
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerReleaseTargetRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1338783904u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1338783904u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ServerReleasePrepStationSlotFromPickup();
			}
		}

		protected override void SpawnFpsShadow(Transform parent)
		{
			base.SpawnFpsShadow(parent);
			if (_fpsShadowObject == null)
			{
				UtilityMethods.Log("[GastroPan - SpawnFpsShadow] FPS Shadow Object null!");
				_fpsShadowReady = false;
			}
			else
			{
				_fpsShadowReady = true;
				ClientTryBuildFpsGraphics();
			}
		}

		protected override void SpawnTpsShadow(Transform parent, bool isOwner)
		{
			base.SpawnTpsShadow(parent, isOwner);
			if (_tpsShadowObject == null)
			{
				UtilityMethods.Log("[GastroPan - SpawnTpsShadow] TPS Shadow Object null!");
				_tpsShadowReady = false;
			}
			else
			{
				_lastIsOwnerFlag = isOwner;
				_tpsShadowReady = true;
				ClientTryBuildTpsGraphics();
			}
		}

		protected override void DestroyFpsShadow()
		{
			base.DestroyFpsShadow();
			_fpsOBJPanIngredientGraphics = null;
			_fpsShadowReady = false;
			_fpsGraphicsBuilt = false;
		}

		protected override void DestroyTpsShadow()
		{
			base.DestroyTpsShadow();
			_tpsOBJPanIngredientGraphics = null;
			_tpsShadowReady = false;
			_tpsGraphicsBuilt = false;
		}

		private void OnIngredientIdChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
		{
			string text = newValue.ToString();
			_ingredientReady = IsValidIngredientId(text);
			if (_ingredientReady)
			{
				ClientSetProcessedIngredientSO_Local(text);
			}
			ClientTryBuildOriginalGraphics();
			ClientTryBuildFpsGraphics();
			ClientTryBuildTpsGraphics();
			ClientRefreshQuickSlotDisplay();
		}

		private void OnIngredientCountChanged(byte oldValue, byte newValue)
		{
			ClientSyncAllGraphicsToCount(newValue, newValue > oldValue, oldValue);
			ClientRefreshQuickSlotDisplay();
		}

		private void OnPrepStationSpawnerReferenceChanged(NetworkObjectReference previousValue, NetworkObjectReference currentValue)
		{
			ClientNotifyOwningPrepStationSpawner();
		}

		public bool ClientBelongsToPrepStationSpawner(GastroPanSpawner spawner)
		{
			if (spawner != null && _prepStationSpawnerReference.Value.TryGet(out var networkObject))
			{
				return networkObject == spawner.NetworkObject;
			}
			return false;
		}

		private void ClientNotifyOwningPrepStationSpawner()
		{
			GastroPanSpawner[] array = UnityEngine.Object.FindObjectsByType<GastroPanSpawner>(FindObjectsSortMode.None);
			foreach (GastroPanSpawner gastroPanSpawner in array)
			{
				if (ClientBelongsToPrepStationSpawner(gastroPanSpawner))
				{
					gastroPanSpawner.ClientRegisterLateJoinTargetPan(this);
					break;
				}
			}
		}

		public GastropanData Save()
		{
			return new GastropanData
			{
				Amount = IngredientCount,
				ItemId = IngredientId.ToString(),
				IsOnDesk = false,
				TransformData = GetTransformData()
			};
		}

		public void Load(GastropanData data)
		{
			_ingredientCount.Value = data.Amount;
			_ingredientID.Value = data.ItemId;
			ItemSO itemSO = _itemDatabaseSO.FindById(data.ItemId);
			if (itemSO is ProcessedIngredientSO processedIngredientSO)
			{
				if (processedIngredientSO == null)
				{
					return;
				}
				_maxCapacity.Value = processedIngredientSO.MaxPanCapacity;
			}
			else if (itemSO is IngredientSO ingredientSO && ingredientSO != null)
			{
				if (ingredientSO.ProcessedIngredients.Count == 0)
				{
					UtilityMethods.Log("[ChefDeskGastroPan - ResolveProcessedIngredientSO] IngredientSO " + ingredientSO.name + " için ProcessedIngredients listesi boş!");
					return;
				}
				_maxCapacity.Value = ingredientSO.ProcessedIngredients[0].MaxPanCapacity;
			}
			ServerSetOriginalRootRootReadForLoadRpc();
			ClientGraphicsHandlerRpc();
		}

		[Rpc(SendTo.Everyone)]
		private void ClientGraphicsHandlerRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3925876363u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3925876363u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientInitializeOriginalPanGraphicsRpc(_ingredientID.Value);
				ClientSyncAllGraphicsToCount(_ingredientCount.Value, animateIncrease: false, 0);
				ClientTryBuildFpsGraphics();
				ClientTryBuildTpsGraphics();
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerSetOriginalRootRootReadForLoadRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1816614896u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1816614896u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_originalRootReadyNetworkVariable.Value = true;
			}
		}

		public TransformData GetTransformData()
		{
			return new TransformData
			{
				Position = base.transform.position,
				Rotation = base.transform.rotation.eulerAngles
			};
		}

		[Rpc(SendTo.Everyone)]
		private void ClientSetProcessedIngredientSORpc(FixedString64Bytes ingredientId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2095457295u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in ingredientId, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 2095457295u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientSetProcessedIngredientSO_Local(ingredientId.ToString());
				ClientRefreshQuickSlotDisplay();
			}
		}

		private void ClientSetProcessedIngredientSO_Local(string ingredientId)
		{
			if (!string.IsNullOrWhiteSpace(ingredientId) && (!(_processedIngredientSO != null) || !(_processedIngredientSO.Id == ingredientId)) && _itemDatabaseSO.FindById(ingredientId) is ProcessedIngredientSO processedIngredientSO)
			{
				_processedIngredientSO = processedIngredientSO;
			}
		}

		private void ClientRefreshQuickSlotDisplay()
		{
			if (!(Singleton<QuickSlotHandler>.Instance == null))
			{
				Singleton<QuickSlotHandler>.Instance.RefreshDisplayForItem(this);
			}
		}

		public override TooltipData GetTooltip()
		{
			if (_processedIngredientSO == null)
			{
				return default;
			}
			TooltipConfig tooltip = _itemSO.Tooltip;
			tooltip.Data.Title = _processedIngredientSO.LocalizedName;
			tooltip.Data.Body = $"{_ingredientCount.Value}/{_maxCapacity.Value}";
			return tooltip.Data;
		}

		public override void EnableGazeHints()
		{
			Singleton<HintsManager>.Instance.ClearAllHints();
			Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_hold");
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
		}

		private void LateUpdate()
		{
			if (_originalBuildPending && base.IsSpawned)
			{
				ClientTryBuildOriginalGraphics(_pendingOriginalId);
			}
			bool flag = _modelOBJ != null && _modelOBJ.activeInHierarchy;
			if (flag && !_wasOriginalModelActive && _originalOBJPanIngredientGraphics != null)
			{
				ClientSyncGraphics(_originalOBJPanIngredientGraphics, 0, _ingredientCount.Value, animateIncrease: false);
			}
			_wasOriginalModelActive = flag;
		}

		private bool IsValidIngredientId(string s)
		{
			if (!string.IsNullOrEmpty(s))
			{
				return s != "0";
			}
			return false;
		}

		private void ClientTryBuildOriginalGraphics()
		{
			ClientTryBuildOriginalGraphics(_ingredientID.Value);
		}

		private void ClientTryBuildOriginalGraphics(FixedString64Bytes idFromRpc)
		{
			if (_originalGraphicsBuilt)
			{
				return;
			}
			string text = ((idFromRpc.Length > 0) ? idFromRpc.ToString() : _ingredientID.Value.ToString());
			_ingredientReady = IsValidIngredientId(text);
			if (!_originalRootReady || !_ingredientReady)
			{
				return;
			}
			if (_modelOBJ == null)
			{
				UtilityMethods.Log("[GastroPan] _modelOBJ is null – cannot build original graphics.");
				return;
			}
			if (Application.isPlaying && (!_modelOBJ.activeInHierarchy || !PanIngredientGraphics.TryBeginDeferredBuild()))
			{
				_originalBuildPending = true;
				_pendingOriginalId = idFromRpc;
				return;
			}
			_originalBuildPending = false;
			PanIngredientGraphics panIngredientGraphics = ClientSpawnPanIngredientGraphicsById(_modelOBJ.transform, text);
			if (!(panIngredientGraphics == null))
			{
				_originalOBJPanIngredientGraphics = panIngredientGraphics;
				_originalOBJPanIngredientGraphics.SyncToCount(_ingredientCount.Value);
				_originalGraphicsBuilt = true;
			}
		}

		private void ClientTryBuildFpsGraphics()
		{
			if (!_fpsGraphicsBuilt && _fpsShadowReady && _ingredientReady)
			{
				PanIngredientGraphics panIngredientGraphics = ClientSpawnPanIngredientGraphics(_fpsShadowObject.transform);
				if (!(panIngredientGraphics == null))
				{
					_fpsOBJPanIngredientGraphics = panIngredientGraphics;
					_fpsOBJPanIngredientGraphics.SyncToCount(_ingredientCount.Value);
					UtilityMethods.SetLayerRecursive(_fpsOBJPanIngredientGraphics.gameObject, "ViewModel", includeChildren: true);
					_fpsGraphicsBuilt = true;
				}
			}
		}

		private void ClientTryBuildTpsGraphics()
		{
			if (!_tpsGraphicsBuilt && _tpsShadowReady && _ingredientReady)
			{
				PanIngredientGraphics panIngredientGraphics = ClientSpawnPanIngredientGraphics(_tpsShadowObject.transform);
				if (!(panIngredientGraphics == null))
				{
					_tpsOBJPanIngredientGraphics = panIngredientGraphics;
					_tpsOBJPanIngredientGraphics.SyncToCount(_ingredientCount.Value);
					UtilityMethods.SetLayerRecursive(_tpsOBJPanIngredientGraphics.gameObject, _lastIsOwnerFlag ? "Player" : "ClientPlayer", includeChildren: true);
					_tpsGraphicsBuilt = true;
				}
			}
		}

		private void ClientSyncAllGraphicsToCount(byte target, bool animateIncrease = false, byte previous = 0)
		{
			ClientSyncGraphics(_originalOBJPanIngredientGraphics, previous, target, animateIncrease);
			ClientSyncGraphics(_fpsOBJPanIngredientGraphics, previous, target, animateIncrease);
			ClientSyncGraphics(_tpsOBJPanIngredientGraphics, previous, target, animateIncrease);
		}

		private void ClientSyncGraphics(PanIngredientGraphics graphics, byte previous, byte target, bool animateIncrease)
		{
			if (!(graphics == null))
			{
				if (animateIncrease && (graphics != _originalOBJPanIngredientGraphics || (_modelOBJ != null && _modelOBJ.activeInHierarchy)))
				{
					graphics.SyncToCountStaggered(previous, target, _ingredientRevealInterval);
					return;
				}
				graphics.CancelStaggeredSync();
				graphics.SyncToCount(target);
			}
		}

		private PanIngredientGraphics ClientSpawnPanIngredientGraphics(Transform parent)
		{
			return ClientSpawnPanIngredientGraphicsById(parent, _ingredientID.Value.ToString());
		}

		private PanIngredientGraphics ClientSpawnPanIngredientGraphicsById(Transform parent, string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				UtilityMethods.Log("[GastroPan - SpawnPanIngredientGraphicsById] Invalid id (empty).");
				return null;
			}
			if (!(_itemDatabaseSO.FindById(id) is ProcessedIngredientSO processedIngredientSO))
			{
				UtilityMethods.Log("[GastroPan - SpawnPanIngredientGraphicsById] ProcessedIngredientSO not found for id: " + id);
				return null;
			}
			GameObject obj = UnityEngine.Object.Instantiate(processedIngredientSO.PanIngredientPrefab, parent);
			obj.transform.localPosition = Vector3.zero;
			PanIngredientGraphics component = obj.GetComponent<PanIngredientGraphics>();
			if (component == null)
			{
				UtilityMethods.Log("[GastroPan - SpawnPanIngredientGraphicsById] Missing PanIngredientGraphics on prefab for id: " + id);
				return null;
			}
			return component;
		}

		public int ServerStaffMergeInto(GastroPan target)
		{
			if (!this.CanWriteServerState() || !target.CanWriteServerState() || target == this || target.NetworkManager != base.NetworkManager || base.IsStaffCarried || base.IsPlayerInventoryOwnedForPersistence || target.IsStaffCarried || target.IsPlayerInventoryOwnedForPersistence || !target.CanHold.Value || target.transform.parent != null || target.IngredientId != IngredientId)
			{
				return 0;
			}
			int num = Mathf.Min(IngredientCount, target.MaxCapacity - target.IngredientCount);
			if (num <= 0)
			{
				return 0;
			}
			target.ServerIngredientCountUpdateRpc((byte)num);
			IngredientCount -= (byte)num;
			if (IngredientCount == 0)
			{
				base.NetworkObject.Despawn();
			}
			return num;
		}

		protected override void __initializeVariables()
		{
			if (_ingredientType == null)
			{
				throw new Exception("GastroPan._ingredientType cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_ingredientType.Initialize(this);
			__nameNetworkVariable(_ingredientType, "_ingredientType");
			NetworkVariableFields.Add(_ingredientType);
			if (_ingredientCount == null)
			{
				throw new Exception("GastroPan._ingredientCount cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_ingredientCount.Initialize(this);
			__nameNetworkVariable(_ingredientCount, "_ingredientCount");
			NetworkVariableFields.Add(_ingredientCount);
			if (_ingredientID == null)
			{
				throw new Exception("GastroPan._ingredientID cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_ingredientID.Initialize(this);
			__nameNetworkVariable(_ingredientID, "_ingredientID");
			NetworkVariableFields.Add(_ingredientID);
			if (_maxCapacity == null)
			{
				throw new Exception("GastroPan._maxCapacity cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_maxCapacity.Initialize(this);
			__nameNetworkVariable(_maxCapacity, "_maxCapacity");
			NetworkVariableFields.Add(_maxCapacity);
			if (_targetIndex == null)
			{
				throw new Exception("GastroPan._targetIndex cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_targetIndex.Initialize(this);
			__nameNetworkVariable(_targetIndex, "_targetIndex");
			NetworkVariableFields.Add(_targetIndex);
			if (_prepStationSpawnerReference == null)
			{
				throw new Exception("GastroPan._prepStationSpawnerReference cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_prepStationSpawnerReference.Initialize(this);
			__nameNetworkVariable(_prepStationSpawnerReference, "_prepStationSpawnerReference");
			NetworkVariableFields.Add(_prepStationSpawnerReference);
			if (_originalRootReadyNetworkVariable == null)
			{
				throw new Exception("GastroPan._originalRootReadyNetworkVariable cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_originalRootReadyNetworkVariable.Initialize(this);
			__nameNetworkVariable(_originalRootReadyNetworkVariable, "_originalRootReadyNetworkVariable");
			NetworkVariableFields.Add(_originalRootReadyNetworkVariable);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(264515628u, __rpc_handler_264515628, "ServerIngredientCountUpdateRpc", RpcInvokePermission.Everyone);
			__registerRpc(438246081u, __rpc_handler_438246081, "ServerConsumeIngredientsRpc", RpcInvokePermission.Everyone);
			__registerRpc(415023493u, __rpc_handler_415023493, "ClientInitializeOriginalPanGraphicsRpc", RpcInvokePermission.Everyone);
			__registerRpc(1338783904u, __rpc_handler_1338783904, "ServerReleaseTargetRpc", RpcInvokePermission.Everyone);
			__registerRpc(3925876363u, __rpc_handler_3925876363, "ClientGraphicsHandlerRpc", RpcInvokePermission.Everyone);
			__registerRpc(1816614896u, __rpc_handler_1816614896, "ServerSetOriginalRootRootReadForLoadRpc", RpcInvokePermission.Everyone);
			__registerRpc(2095457295u, __rpc_handler_2095457295, "ClientSetProcessedIngredientSORpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_264515628(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((GastroPan)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((GastroPan)target).ServerIngredientCountUpdateRpc(value);
				((GastroPan)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_438246081(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((GastroPan)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((GastroPan)target).ServerConsumeIngredientsRpc(value);
				((GastroPan)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_415023493(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				((GastroPan)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((GastroPan)target).ClientInitializeOriginalPanGraphicsRpc(value);
				((GastroPan)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1338783904(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((GastroPan)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((GastroPan)target).ServerReleaseTargetRpc();
				((GastroPan)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3925876363(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((GastroPan)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((GastroPan)target).ClientGraphicsHandlerRpc();
				((GastroPan)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1816614896(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((GastroPan)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((GastroPan)target).ServerSetOriginalRootRootReadForLoadRpc();
				((GastroPan)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2095457295(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				((GastroPan)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((GastroPan)target).ClientSetProcessedIngredientSORpc(value);
				((GastroPan)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "GastroPan";
		}
	}
}
