using System;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using Deform;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Interaction.ToolTip;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.Indicator;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Ocean
{
	public class DeadFish : BaseHoldable, ISaveable<DeadFishData>
	{
		private const float MIN_COLLIDER_DIMENSION = 0.01f;

		private static readonly HashSet<string> _reportedColliderBoundsFallbackFishIds = new HashSet<string>();

		private float _slideDistance = 0.6f;

		private float _slideDuration = 0.3f;

		private GameObject _spawnedGraphic;

		private bool _serverRegisteredInventoryStock;

		private ulong _lastHolderClientId = ulong.MaxValue;

		public NetworkVariable<FixedString64Bytes> FishId = new NetworkVariable<FixedString64Bytes>();

		public NetworkVariable<float> Weight = new NetworkVariable<float>(1f);

		public NetworkVariable<bool> IsFishBookCatchRegistered = new NetworkVariable<bool>(value: false);

		private Deformable _fpsDeformable;

		private Deformable _tpsDeformable;

		private BendDeformer _fpsBendDeformer;

		private BendDeformer _tpsBendDeformer;

		public FishSO FishSO => _itemSO as FishSO;

		public GameObject DeadGraphic => FishSO.DeadGraphic;

		public override bool TryGetScanIndicatorVisualSettings(out OffscreenIndicatorVisualSettings settings)
		{
			if (FishSO == null || FishSO.Sprite == null)
			{
				return base.TryGetScanIndicatorVisualSettings(out settings);
			}
			settings = new OffscreenIndicatorVisualSettings
			{
				IconSprite = FishSO.Sprite,
				ApplyDistanceVisible = true,
				DistanceVisible = false
			};
			return true;
		}

		public override TooltipData GetTooltip()
		{
			TooltipData data = FishSO.Tooltip.Data;
			data.Title = FishSO.LocalizedName;
			data.Body = LocalizationDatabase.GetFormattedString("UiTable", "tooltip.weight", "Weight: {0} kg", Weight.Value.ToString("F2", CultureInfo.CurrentCulture));
			return data;
		}

		public override void OnNetworkSpawn()
		{
			if (_itemSO != null)
			{
				SpawnGraphic();
			}
			if (FishId.Value != default(FixedString32Bytes))
			{
				LazyInit(FishId.Value);
			}
			if (base.IsServer)
			{
				RestaurantManager restaurantManager = Singleton<GameProgressManager>.Instance.RestaurantManager;
				if (restaurantManager != null)
				{
					restaurantManager.RegisterDeadFish(this);
				}
			}
			base.OnNetworkSpawn();
		}

		public override void OnNetworkDespawn()
		{
			if (base.IsServer)
			{
				ServerDeregisterInventoryStock();
				RestaurantManager restaurantManager = Singleton<GameProgressManager>.Instance.RestaurantManager;
				if (restaurantManager != null)
				{
					restaurantManager.DeRegisterDeadFish(this);
				}
			}
			base.OnNetworkDespawn();
		}

		private void SetupColliderByType()
		{
			if (FishSO == null)
			{
				UtilityMethods.Log("[DeadFish] Collider setup failed. FishSO is null!", LogType.Warning);
				return;
			}
			_deactiveColliders.Clear();
			bool useDeadCollider = FishSO.UseDeadCollider;
			if ((useDeadCollider ? FishSO.DeadColliderShape : FishSO.ColliderShape) == ColliderType.Box)
			{
				SetupBoxCollider(useDeadCollider);
			}
			else
			{
				SetupCapsuleCollider(useDeadCollider);
			}
		}

		private void SetupBoxCollider(bool useDeadColliderData)
		{
			Vector3 center = (useDeadColliderData ? FishSO.DeadBoxCenter : FishSO.ColliderCenter);
			Vector3 size = (useDeadColliderData ? FishSO.DeadBoxSize : FishSO.BoxSize);
			if (!IsValidBoxColliderData(center, size))
			{
				if (!TryGetGraphicLocalBounds(out var bounds))
				{
					UtilityMethods.Log("[DeadFish] Collider setup failed. Box data is invalid and graphic bounds could not be resolved. Fish:" + FishSO.Id, LogType.Error);
					return;
				}
				center = bounds.center;
				size = bounds.size;
				LogColliderBoundsFallback();
			}
			if (TryGetComponent<CapsuleCollider>(out var component))
			{
				component.enabled = false;
			}
			BoxCollider orAddComponent = base.gameObject.GetOrAddComponent<BoxCollider>();
			orAddComponent.isTrigger = false;
			orAddComponent.center = center;
			orAddComponent.size = ClampColliderSize(size);
			orAddComponent.enabled = true;
			_deactiveColliders.Add(orAddComponent);
		}

		private void SetupCapsuleCollider(bool useDeadColliderData)
		{
			Vector3 center = (useDeadColliderData ? FishSO.DeadColliderCenter : FishSO.ColliderCenter);
			float radius = (useDeadColliderData ? FishSO.DeadColliderRadius : FishSO.ColliderRadius);
			float height = (useDeadColliderData ? FishSO.DeadColliderHeight : FishSO.ColliderHeight);
			int direction = Mathf.Clamp(useDeadColliderData ? FishSO.DeadCapsuleDirection : FishSO.CapsuleDirection, 0, 2);
			if (!IsValidCapsuleColliderData(center, radius, height))
			{
				if (!TryGetGraphicLocalBounds(out var bounds))
				{
					UtilityMethods.Log("[DeadFish] Collider setup failed. Capsule data is invalid and graphic bounds could not be resolved. Fish:" + FishSO.Id, LogType.Error);
					return;
				}
				ResolveCapsuleFromBounds(bounds, out center, out radius, out height, out direction);
				LogColliderBoundsFallback();
			}
			if (TryGetComponent<BoxCollider>(out var component))
			{
				component.enabled = false;
			}
			CapsuleCollider orAddComponent = base.gameObject.GetOrAddComponent<CapsuleCollider>();
			orAddComponent.isTrigger = false;
			orAddComponent.center = center;
			orAddComponent.radius = Mathf.Max(0.01f, radius);
			orAddComponent.height = Mathf.Max(orAddComponent.radius * 2f, height);
			orAddComponent.direction = direction;
			orAddComponent.enabled = true;
			_deactiveColliders.Add(orAddComponent);
		}

		private bool TryGetGraphicLocalBounds(out Bounds bounds)
		{
			bounds = default;
			if (_spawnedGraphic == null)
			{
				return false;
			}
			Renderer[] componentsInChildren = _spawnedGraphic.GetComponentsInChildren<Renderer>(includeInactive: true);
			bool flag = false;
			foreach (Renderer renderer in componentsInChildren)
			{
				if (renderer == null)
				{
					continue;
				}
				Bounds localBounds = renderer.localBounds;
				Vector3 center = localBounds.center;
				Vector3 extents = localBounds.extents;
				for (int j = 0; j < 8; j++)
				{
					Vector3 position = center + Vector3.Scale(extents, new Vector3(((j & 1) == 0) ? (-1f) : 1f, ((j & 2) == 0) ? (-1f) : 1f, ((j & 4) == 0) ? (-1f) : 1f));
					Vector3 vector = base.transform.InverseTransformPoint(renderer.transform.TransformPoint(position));
					if (!flag)
					{
						bounds = new Bounds(vector, Vector3.zero);
						flag = true;
					}
					else
					{
						bounds.Encapsulate(vector);
					}
				}
			}
			if (flag)
			{
				return IsValidBoxColliderData(bounds.center, bounds.size);
			}
			return false;
		}

		private static void ResolveCapsuleFromBounds(Bounds bounds, out Vector3 center, out float radius, out float height, out int direction)
		{
			center = bounds.center;
			Vector3 vector = ClampColliderSize(bounds.size);
			direction = 0;
			float num = vector.x;
			float a = vector.y;
			float b = vector.z;
			if (vector.y > num)
			{
				direction = 1;
				num = vector.y;
				a = vector.x;
				b = vector.z;
			}
			if (vector.z > num)
			{
				direction = 2;
				num = vector.z;
				a = vector.x;
				b = vector.y;
			}
			radius = Mathf.Max(0.01f, Mathf.Max(a, b) * 0.5f);
			height = Mathf.Max(num, radius * 2f);
		}

		private static Vector3 ClampColliderSize(Vector3 size)
		{
			return new Vector3(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y), Mathf.Max(0.01f, size.z));
		}

		private static bool IsValidBoxColliderData(Vector3 center, Vector3 size)
		{
			if (IsFinite(center) && IsFinite(size) && size.x > 0.01f && size.y > 0.01f)
			{
				return size.z > 0.01f;
			}
			return false;
		}

		private static bool IsValidCapsuleColliderData(Vector3 center, float radius, float height)
		{
			if (IsFinite(center) && float.IsFinite(radius) && float.IsFinite(height) && radius > 0.01f)
			{
				return height > 0.01f;
			}
			return false;
		}

		private static bool IsFinite(Vector3 value)
		{
			if (float.IsFinite(value.x) && float.IsFinite(value.y))
			{
				return float.IsFinite(value.z);
			}
			return false;
		}

		private void LogColliderBoundsFallback()
		{
			if (_reportedColliderBoundsFallbackFishIds.Add(FishSO.Id))
			{
				UtilityMethods.Log("[DeadFish] Fish collider data is invalid. Using dead graphic bounds. Fish:" + FishSO.Id, LogType.Warning);
			}
		}

		public void ServerLazyHold(FixedString64Bytes itemId, ulong clientId, NetworkObjectReference interactorRef, bool select, bool hold, bool useFallbackDropPosition = false, Vector3 fallbackDropPosition = default(Vector3), bool requireQuickSlot = false)
		{
			ServerSpawnGraphic(itemId);
			ServerLazyHold(clientId, interactorRef, select, hold, useFallbackDropPosition, fallbackDropPosition, requireQuickSlot);
			if ((base.IsServer & hold) && base.OwnerClientId == clientId && !CanHold.Value)
			{
				_lastHolderClientId = clientId;
			}
			if (hold)
			{
				ServerRegisterInventoryStock();
			}
		}

		public override void ServerHold(Interactor interactor)
		{
			base.ServerHold(interactor);
			if (base.IsServer && interactor != null && base.OwnerClientId == interactor.OwnerClientId && !CanHold.Value)
			{
				_lastHolderClientId = interactor.OwnerClientId;
			}
			ServerRegisterInventoryStock();
			ServerTryRegisterFishBookPickup(interactor);
		}

		public void ServerMarkFishBookCatchRegistered()
		{
			if (base.IsServer)
			{
				IsFishBookCatchRegistered.Value = true;
			}
		}

		private void ServerTryRegisterFishBookPickup(Interactor interactor)
		{
			if (base.IsServer && !IsFishBookCatchRegistered.Value && !(FishSO == null) && !(interactor?.Player == null))
			{
				if (!ServiceLocator.ForSceneOf(this).TryGet<TabletNetworkHandler>(out var service) || service == null)
				{
					UtilityMethods.Log("[DeadFish] FishBook registration skipped. TabletNetworkHandler not found.", LogType.Warning);
					return;
				}
				service.ServerAddToFishBook(FishSO, Weight.Value, interactor.Player.SteamID.Value, interactor.OwnerClientId);
				IsFishBookCatchRegistered.Value = true;
			}
		}

		public bool ServerTryGetLastHolderClientId(out ulong clientId)
		{
			clientId = _lastHolderClientId;
			if (base.IsServer)
			{
				return clientId != ulong.MaxValue;
			}
			return false;
		}

		public void ServerSpawnGraphic(FixedString64Bytes itemId)
		{
			ClientSpawnGraphicRpc(itemId);
		}

		[Rpc(SendTo.Everyone)]
		private void ClientSpawnGraphicRpc(FixedString64Bytes itemId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2833488567u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in itemId, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 2833488567u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_itemSO = ResolveFishById(itemId.ToString());
				SpawnGraphic();
			}
		}

		private FishSO ResolveFishById(string fishId)
		{
			if (string.IsNullOrWhiteSpace(fishId))
			{
				return null;
			}
			if (Singleton<SeaCreatureManager>.Instance == null)
			{
				if (StaffWorkplaceRegistry.Instance?.Database?.FindById(fishId) is FishSO result)
				{
					return result;
				}
				UtilityMethods.Log("[DeadFish] SeaCreatureManager.Instance is null!", LogType.Warning);
				return null;
			}
			return Singleton<SeaCreatureManager>.Instance.GetFishById(fishId);
		}

		private void ApplyDeadEyeShader(GameObject targetGraphic)
		{
			if (!(targetGraphic == null))
			{
				if (Singleton<SeaCreatureManager>.Instance == null)
				{
					UtilityMethods.Log("[DeadFish] SeaCreatureManager.Instance is null. Dead eye shader skipped.", LogType.Warning);
				}
				else if (!FishSO.Name.Contains("Shrimp"))
				{
					Singleton<SeaCreatureManager>.Instance.ApplyDeadEyeShaderState(targetGraphic);
				}
			}
		}

		private void SpawnGraphic()
		{
			if (_spawnedGraphic != null)
			{
				return;
			}
			_spawnedGraphic = UnityEngine.Object.Instantiate(FishSO.DeadGraphic, base.transform);
			_deactiveObjects.Add(_spawnedGraphic);
			SetupColliderByType();
			ApplyDeadEyeShader(_spawnedGraphic);
			if (!(_outlinable != null))
			{
				return;
			}
			Renderer[] componentsInChildren = _spawnedGraphic.GetComponentsInChildren<Renderer>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				if (componentsInChildren[i] != null)
				{
					_outlinable.AddRenderer(componentsInChildren[i]);
				}
			}
		}

		public void ServerDeregisterInventoryStock()
		{
			if (base.IsServer && _serverRegisteredInventoryStock && !(FishSO == null))
			{
				RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
				if (!(restaurantManager == null))
				{
					restaurantManager.ServerRemoveStockRpc(FishSO.Id, 1);
					_serverRegisteredInventoryStock = false;
				}
			}
		}

		private void ServerRegisterInventoryStock()
		{
			if (base.IsServer && !_serverRegisteredInventoryStock && !(FishSO == null))
			{
				RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
				if (!(restaurantManager == null))
				{
					restaurantManager.ServerAddStockRpc(FishSO.Id, 1);
					_serverRegisteredInventoryStock = true;
				}
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerSlideForwardRpc(Vector3 direction)
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
				FastBufferWriter bufferWriter = __beginSendRpc(377523264u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in direction);
				__endSendRpc(ref bufferWriter, 377523264u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ServerSlideForward(direction);
			}
		}

		public void ServerSlideForward(Vector3 direction)
		{
			if (base.IsServer && !(direction.sqrMagnitude < 0.0001f))
			{
				ClientSlideForwardRpc(direction);
			}
		}

		[Rpc(SendTo.Everyone)]
		public void ClientSlideForwardRpc(Vector3 direction)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3619850985u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in direction);
				__endSendRpc(ref bufferWriter, 3619850985u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				base.transform.DOMove(base.transform.position + direction.normalized * _slideDistance, _slideDuration).SetEase(Ease.OutQuad).SetDelay(0.25f);
			}
		}

		[Rpc(SendTo.Everyone)]
		public void ClientSpawnGraphicsRpc(FixedString64Bytes id)
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
				FastBufferWriter bufferWriter = __beginSendRpc(358822219u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in id, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 358822219u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				LazyInit(id);
			}
		}

		private void LazyInit(FixedString64Bytes id)
		{
			_itemSO = ResolveFishById(id.ToString());
			SpawnGraphic();
		}

		protected override void SpawnFpsShadow(Transform parent)
		{
			DestroyFpsShadow();
			_fpsShadowObject = UnityEngine.Object.Instantiate(FishSO.DeadGraphic, parent);
			if (base.ItemSO.FPSHoldOffset.ScaleOffset != Vector3.zero)
			{
				_fpsShadowObject.transform.localScale = base.ItemSO.FPSHoldOffset.ScaleOffset;
			}
			_fpsShadowObject.transform.SetLocalPositionAndRotation(base.ItemSO.FPSHoldOffset.PositionOffset, Quaternion.Euler(base.ItemSO.FPSHoldOffset.RotationOffset));
			UtilityMethods.SetLayerRecursive(_fpsShadowObject, "ViewModel", includeChildren: true);
			foreach (NetworkObjectReference parentedChild in _parentedChilds)
			{
				if (parentedChild.TryGetComponent<BaseHoldable>(out var component))
				{
					component.SpawnFpsParentedShadow(_fpsShadowObject.transform);
				}
			}
			if (!_fpsShadowObject.TryGetComponent<Deformable>(out _fpsDeformable) || _fpsDeformable.transform.childCount <= 1 || !_fpsDeformable.transform.GetChild(1).TryGetComponent<BendDeformer>(out _fpsBendDeformer))
			{
				UtilityMethods.Log("[DeadFish] SpawnFpsShadow _fpsDeformable not found!", LogType.Warning);
			}
			else
			{
				_fpsBendDeformer.update = true;
			}
		}

		protected override void SpawnTpsShadow(Transform parent, bool isOwner)
		{
			DestroyTpsShadow();
			_tpsShadowObject = UnityEngine.Object.Instantiate(FishSO.DeadGraphic, parent);
			if (base.ItemSO.TPSHoldOffset.ScaleOffset != Vector3.zero)
			{
				_tpsShadowObject.transform.localScale = base.ItemSO.TPSHoldOffset.ScaleOffset;
			}
			_tpsShadowObject.transform.SetLocalPositionAndRotation(base.ItemSO.TPSHoldOffset.PositionOffset, Quaternion.Euler(base.ItemSO.TPSHoldOffset.RotationOffset));
			UtilityMethods.SetLayerRecursive(_tpsShadowObject, isOwner ? "Player" : "ClientPlayer", includeChildren: true);
			foreach (NetworkObjectReference parentedChild in _parentedChilds)
			{
				if (parentedChild.TryGetComponent<BaseHoldable>(out var component))
				{
					component.SpawnTpsParentedShadow(_tpsShadowObject.transform, isOwner);
				}
			}
			if (!_tpsShadowObject.TryGetComponent<Deformable>(out _tpsDeformable) || _tpsDeformable.transform.childCount <= 1 || !_tpsDeformable.transform.GetChild(1).TryGetComponent<BendDeformer>(out _tpsBendDeformer))
			{
				UtilityMethods.Log("[DeadFish] SpawnTpsShadow _tpsDeformable not found!", LogType.Warning);
			}
			else
			{
				_tpsBendDeformer.update = true;
			}
		}

		protected override void DestroyFpsShadow()
		{
			if (_fpsBendDeformer != null)
			{
				_fpsBendDeformer.update = false;
			}
			base.DestroyFpsShadow();
		}

		protected override void DestroyTpsShadow()
		{
			if (_tpsBendDeformer != null)
			{
				_tpsBendDeformer.update = false;
			}
			base.DestroyTpsShadow();
		}

		protected override void ClientHoldEveryone()
		{
			base.ClientHoldEveryone();
			if (_fpsShadowObject != null)
			{
				ApplyDeadEyeShader(_fpsShadowObject);
			}
			if (_tpsShadowObject != null)
			{
				ApplyDeadEyeShader(_tpsShadowObject);
			}
		}

		public DeadFishData Save()
		{
			return new DeadFishData
			{
				FishId = FishSO.Id,
				TransformData = GetTransformData(),
				FishWeight = Weight.Value,
				IsFishBookCatchRegistered = IsFishBookCatchRegistered.Value
			};
		}

		public override string SaveCustomData()
		{
			return Weight.Value.ToString(CultureInfo.InvariantCulture);
		}

		public override void LoadCustomData(FixedString128Bytes data)
		{
			string text = data.ToString();
			if (!string.IsNullOrWhiteSpace(text) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				Weight.Value = result;
			}
		}

		public void Load(DeadFishData data)
		{
			Weight.Value = data.FishWeight;
			FishId.Value = data.FishId;
			IsFishBookCatchRegistered.Value = data.IsFishBookCatchRegistered;
			ClientSpawnGraphicsRpc(data.FishId);
		}

		public TransformData GetTransformData()
		{
			return new TransformData
			{
				Position = base.transform.position,
				Rotation = base.transform.rotation.eulerAngles
			};
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

		public override void EnableHeldHints()
		{
			Singleton<HintsManager>.Instance.ClearAllHints();
			Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_place");
			AddHeldThrowHint();
		}

		public override void DisableHeldHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("RightMouseMechanic");
			RemoveHeldThrowHint();
		}

		protected override void __initializeVariables()
		{
			if (FishId == null)
			{
				throw new Exception("DeadFish.FishId cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			FishId.Initialize(this);
			__nameNetworkVariable(FishId, "FishId");
			NetworkVariableFields.Add(FishId);
			if (Weight == null)
			{
				throw new Exception("DeadFish.Weight cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			Weight.Initialize(this);
			__nameNetworkVariable(Weight, "Weight");
			NetworkVariableFields.Add(Weight);
			if (IsFishBookCatchRegistered == null)
			{
				throw new Exception("DeadFish.IsFishBookCatchRegistered cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			IsFishBookCatchRegistered.Initialize(this);
			__nameNetworkVariable(IsFishBookCatchRegistered, "IsFishBookCatchRegistered");
			NetworkVariableFields.Add(IsFishBookCatchRegistered);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(2833488567u, __rpc_handler_2833488567, "ClientSpawnGraphicRpc", RpcInvokePermission.Everyone);
			__registerRpc(377523264u, __rpc_handler_377523264, "ServerSlideForwardRpc", RpcInvokePermission.Everyone);
			__registerRpc(3619850985u, __rpc_handler_3619850985, "ClientSlideForwardRpc", RpcInvokePermission.Everyone);
			__registerRpc(358822219u, __rpc_handler_358822219, "ClientSpawnGraphicsRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_2833488567(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				((DeadFish)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DeadFish)target).ClientSpawnGraphicRpc(value);
				((DeadFish)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_377523264(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				((DeadFish)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DeadFish)target).ServerSlideForwardRpc(value);
				((DeadFish)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3619850985(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				((DeadFish)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DeadFish)target).ClientSlideForwardRpc(value);
				((DeadFish)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_358822219(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				((DeadFish)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((DeadFish)target).ClientSpawnGraphicsRpc(value);
				((DeadFish)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "DeadFish";
		}
	}
}
