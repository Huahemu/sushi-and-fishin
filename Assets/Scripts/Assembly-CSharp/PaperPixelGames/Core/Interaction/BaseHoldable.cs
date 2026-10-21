using System;
using System.Collections;
using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction.ToolTip;
using PaperPixelGames.Core.Lighthouse;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Indicator;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Wheel;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Interaction
{
	public abstract class BaseHoldable : BaseInteractable, ITooltipProvider
	{
		protected struct PhysicsThrowData
		{
			public Vector3 StartPosition;

			public Vector3 Direction;

			public float Force;

			public float UpwardForce;

			public Vector3 TorqueImpulse;

			public int ForceModeId;

			public float Mass;

			public bool UseGravity;

			public float LinearDamping;

			public float AngularDamping;

			public int CollisionDetectionModeId;

			public int InterpolationId;

			public int ExcludeLayerMaskValue;

			public float SettleVelocity;

			public float SettleStableDuration;

			public float MaxFlightDuration;

			public bool StartedUnderwater;
		}

		[Header("BaseHoldable")]
		[SerializeField]
		protected HoldableSO _itemSO;

		[SerializeField]
		protected List<GameObject> _deactiveObjects;

		[SerializeField]
		protected List<Collider> _deactiveColliders;

		[Header("Throw Impact SFX")]
		[SerializeField]
		private AudioClipSO _throwImpactSfxClip;

		[SerializeField]
		[Min(0f)]
		private float _throwImpactMinRelativeSpeed = 1.5f;

		[SerializeField]
		[Min(0f)]
		private float _throwImpactCooldown = 0.12f;

		[SerializeField]
		private LayerMask _throwImpactExcludeLayers;

		[Header("Throw Water Physics")]
		[SerializeField]
		[Min(0f)]
		private float _throwWaterLinearDamping = 4f;

		[SerializeField]
		[Min(0f)]
		private float _throwWaterAngularDamping = 3f;

		[SerializeField]
		[Min(0.01f)]
		private float _throwWaterVelocityCap = 2.5f;

		[SerializeField]
		[Min(0.01f)]
		private float _throwWaterSettleVelocity = 0.18f;

		[SerializeField]
		[Min(0.01f)]
		private float _throwWaterSettleStableDuration = 0.18f;

		[SerializeField]
		[Min(0f)]
		private float _throwWaterBigSplashSpeed = 4f;

		[SerializeField]
		[Min(0.01f)]
		private float _throwWaterProbeRadius = 0.15f;

		[SerializeField]
		private LayerMask _throwWaterProbeLayers = -1;

		[NonSerialized]
		public NetworkVariable<bool> CanHold = new NetworkVariable<bool>(value: true);

		protected NetworkList<NetworkObjectReference> _parentedChilds = new NetworkList<NetworkObjectReference>();

		private NetworkVariable<Vector3> _parentedLocalPos = new NetworkVariable<Vector3>(Vector3.zero);

		private NetworkVariable<bool> _tooltipSuppressedByPlacement = new NetworkVariable<bool>(value: false);

		protected Interactor _ownerInteractor;

		protected GameObject _tpsParentedShadowObject;

		protected GameObject _fpsParentedShadowObject;

		protected GameObject _tpsShadowObject;

		protected GameObject _fpsShadowObject;

		protected Tray _tray;

		protected SnapPoint _snapPoint;

		private Rigidbody _throwRigidbody;

		private Coroutine _throwMonitorCoroutine;

		private bool _throwRigidbodyCreated;

		private bool _localThrowInFlight;

		private bool _localThrowSettledReported;

		private bool _throwRigidbodyStateStored;

		private bool _previousUseGravity;

		private bool _previousIsKinematic;

		private float _previousMass;

		private float _previousLinearDamping;

		private float _previousAngularDamping;

		private CollisionDetectionMode _previousCollisionDetectionMode;

		private RigidbodyInterpolation _previousInterpolation;

		private LayerMask _previousExcludeLayers;

		private bool _serverThrowInFlight;

		private bool _serverPlayerInventoryOwned;

		private int _throwFlightVersion;

		private float _lastThrowImpactSfxTime = float.NegativeInfinity;

		private bool _throwImpactSfxPlayedThisFlight;

		private bool _throwEnteredWater;

		private readonly Collider[] _throwWaterProbeHits = new Collider[8];

		private bool _parented;

		private bool _hasPendingPlacementScanSuppression;

		private bool _pendingPlacementScanSuppression;

		private const string UNDERWATER_PHYSIC_VOLUME_NAME = "Underwater Physic Volume";

		private const string LEGACY_UNDERWATER_VOLUME_NAME = "UnderWater Volume";

		private static readonly HashSet<BaseHoldable> staffWorldItems = new HashSet<BaseHoldable>();

		private readonly NetworkVariable<int> _staffCarrier = new NetworkVariable<int>(0);

		internal static readonly HashSet<BaseHoldable> StackWorldItems = new HashSet<BaseHoldable>();

		private readonly NetworkVariable<int> _staffStackPoint = new NetworkVariable<int>(-1);

		private readonly NetworkVariable<bool> _staffStackReserved = new NetworkVariable<bool>(value: false);

		private bool stackHidden;

		public static IReadOnlyCollection<BaseHoldable> StaffWorldItems => staffWorldItems;

		public bool IsStaffCarried => _staffCarrier.Value != 0;

		public HoldableSO ItemSO => _itemSO;

		public virtual string QuickSlotDisplayName
		{
			get
			{
				if (!(_itemSO != null))
				{
					return string.Empty;
				}
				return _itemSO.LocalizedName;
			}
		}

		public virtual Sprite QuickSlotDisplaySprite
		{
			get
			{
				if (!(_itemSO != null))
				{
					return null;
				}
				return _itemSO.Sprite;
			}
		}

		public virtual string QuickSlotStackId
		{
			get
			{
				if (!(_itemSO != null))
				{
					return string.Empty;
				}
				return _itemSO.Id;
			}
		}

		public int HoldType => _itemSO.HoldType;

		public bool IsPlaceable => _itemSO.IsPlaceable;

		public bool IsThrowable => _itemSO.IsThrowable;

		public virtual bool RequiresHeldSlot => false;

		public virtual bool CanPersistInQuickSlotSave => true;

		public bool IsPlayerInventoryOwnedForPersistence => _serverPlayerInventoryOwned;

		public SurfaceTags PlaceableTags => _itemSO.PlaceableTags;

		public Vector3 PlaceOffset => _itemSO.PlaceOffset.PositionOffset;

		public bool IsPlacementTooltipSuppressed => _tooltipSuppressedByPlacement.Value;

		public bool HasRuntimeShadowVisual
		{
			get
			{
				if (!(_tpsShadowObject != null) && !(_fpsShadowObject != null) && !(_tpsParentedShadowObject != null))
				{
					return _fpsParentedShadowObject != null;
				}
				return true;
			}
		}

		protected virtual bool UsesServerAuthoritativePersistentThrow => false;

		public int StaffStackPoint => _staffStackPoint.Value;

		public bool IsStaffStackReserved => _staffStackReserved.Value;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStaffItems()
		{
			staffWorldItems.Clear();
		}

		public virtual bool ServerTryStaffPickup(int employeeId)
		{
			if (!base.IsServer || !base.IsSpawned || employeeId <= 0 || IsStaffCarried || !CanHold.Value || _serverPlayerInventoryOwned || base.transform.parent != null)
			{
				return false;
			}
			StaffWorkplaceRegistry.Instance?.FillStackPackage(this, employeeId);
			ServerSetStaffStack(-1);
			if (_serverThrowInFlight)
			{
				ServerCancelThrowFlightForHold();
			}
			ServerClearHolderPresentation();
			if (base.OwnerClientId != 0L)
			{
				base.NetworkObject.RemoveOwnership();
			}
			ServerStaffSetCarrier(employeeId);
			CanHold.Value = false;
			CanGaze.Value = false;
			return true;
		}

		public bool ServerStaffCarriedBy(int employeeId)
		{
			if (base.IsServer && base.IsSpawned && employeeId > 0)
			{
				return _staffCarrier.Value == employeeId;
			}
			return false;
		}

		public void ServerStaffSetCarrier(int employeeId)
		{
			if (!base.IsServer || !base.IsSpawned)
			{
				return;
			}
			_staffCarrier.Value = employeeId;
			BaseHoldable[] componentsInChildren = GetComponentsInChildren<BaseHoldable>(includeInactive: true);
			foreach (BaseHoldable baseHoldable in componentsInChildren)
			{
				if (!(baseHoldable == this))
				{
					baseHoldable._staffCarrier.Value = employeeId;
					baseHoldable.CanHold.Value = employeeId == 0;
					baseHoldable.CanGaze.Value = employeeId == 0;
				}
			}
		}

		public void ServerStaffDrop(int employeeId, Vector3 position, Quaternion rotation)
		{
			if (base.IsServer && _staffCarrier.Value == employeeId)
			{
				ServerStaffSetCarrier(0);
				CanHold.Value = true;
				CanGaze.Value = true;
				_serverPlayerInventoryOwned = false;
				ServerSetPlacementScanSuppressed(suppressed: false);
				base.transform.SetPositionAndRotation(position, rotation);
				ClientStaffPlaceRpc(position, rotation);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientStaffPlaceRpc(Vector3 position, Quaternion rotation)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1983165441u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in position);
				bufferWriter.WriteValueSafe(in rotation);
				__endSendRpc(ref bufferWriter, 1983165441u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientCancelThrowFlight();
				ClientClearHeldVisuals();
				base.transform.SetPositionAndRotation(position, rotation);
				if (ItemSO.PlaceOffset.ScaleOffset != Vector3.zero)
				{
					base.transform.localScale = ItemSO.PlaceOffset.ScaleOffset;
				}
				CollidersStatus(status: true);
				ObjectsStatus(status: true);
				ClientRefreshRuntimeRepresentationState();
				ClientRefreshParentedChildrenRuntimeRepresentationStates();
				if (IsStaffCarried)
				{
					ApplyStaffVisibility();
				}
				Singleton<SoundManager>.Instance?.PlaySFX(SoundType.Item_Put, _itemSO.HoldableMaterialType, _itemSO.FurnitureWeight, position);
			}
		}

		private void OnStaffCarrierChanged(int previous, int current)
		{
			ApplyStaffVisibility();
		}

		private void ApplyStaffVisibility()
		{
			if (IsStaffCarried)
			{
				ObjectsStatus(status: false);
				CollidersStatus(status: false);
				Renderer[] componentsInChildren = GetComponentsInChildren<Renderer>(includeInactive: true);
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					componentsInChildren[i].forceRenderingOff = true;
				}
			}
			else
			{
				ObjectsStatus(status: true);
				CollidersStatus(status: true);
				Renderer[] componentsInChildren = GetComponentsInChildren<Renderer>(includeInactive: true);
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					componentsInChildren[i].forceRenderingOff = false;
				}
			}
		}

		public virtual GameObject CreatePlacementPreview(Transform parent)
		{
			return UnityEngine.Object.Instantiate(_itemSO.PlacementObjectPrefab, parent);
		}

		public override bool TryGetScanIndicatorVisualSettings(out OffscreenIndicatorVisualSettings settings)
		{
			if (!(_itemSO is ServedDishSO))
			{
				settings = null;
				return false;
			}
			settings = new OffscreenIndicatorVisualSettings
			{
				IconSprite = _itemSO.Sprite,
				ApplyDistanceVisible = true,
				DistanceVisible = false
			};
			return true;
		}

		public virtual void ClientClearHeldVisuals()
		{
			DestroyFpsShadow();
			DestroyTpsShadow();
			DestroyFpsParentedShadow();
			DestroyTpsParentedShadow();
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			if (base.IsServer)
			{
				_serverPlayerInventoryOwned = false;
			}
			ObjectsStatus(status: true);
			CollidersStatus(status: true);
			if (base.IsServer)
			{
				staffWorldItems.Add(this);
			}
			StackWorldItems.Add(this);
			NetworkVariable<int> staffStackPoint = _staffStackPoint;
			staffStackPoint.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Combine(staffStackPoint.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnStaffStackChanged));
			NetworkVariable<int> staffCarrier = _staffCarrier;
			staffCarrier.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Combine(staffCarrier.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnStaffCarrierChanged));
			if (IsStaffCarried)
			{
				ApplyStaffVisibility();
			}
			NetworkVariable<Vector3> parentedLocalPos = _parentedLocalPos;
			parentedLocalPos.OnValueChanged = (NetworkVariable<Vector3>.OnValueChangedDelegate)Delegate.Combine(parentedLocalPos.OnValueChanged, new NetworkVariable<Vector3>.OnValueChangedDelegate(OnParentedLocalPositionChanged));
			NetworkVariable<bool> tooltipSuppressedByPlacement = _tooltipSuppressedByPlacement;
			tooltipSuppressedByPlacement.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(tooltipSuppressedByPlacement.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnPlacementTooltipSuppressionChanged));
			_parented = base.NetworkObject.IsSpawned && base.NetworkObject.CurrentParent != null;
			if (!CanHold.Value && Singleton<PlayersManager>.Instance != null && Singleton<PlayersManager>.Instance.TryGetPlayer(base.OwnerClientId, out var player))
			{
				player.Interactor?.ClientReconcileHeldPresentation();
			}
		}

		public override void OnNetworkDespawn()
		{
			NetworkVariable<int> staffStackPoint = _staffStackPoint;
			staffStackPoint.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Remove(staffStackPoint.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnStaffStackChanged));
			StackWorldItems.Remove(this);
			ClientSetStackHidden(hidden: false);
			staffWorldItems.Remove(this);
			NetworkVariable<int> staffCarrier = _staffCarrier;
			staffCarrier.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Remove(staffCarrier.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnStaffCarrierChanged));
			ServerClearHolderPresentation();
			Singleton<ToolTipHandler>.Instance?.OnGazeExit(this);
			if (Singleton<QuickSlotHandler>.Instance != null)
			{
				Singleton<QuickSlotHandler>.Instance.RemoveSpecificItem(this);
			}
			NetworkVariable<Vector3> parentedLocalPos = _parentedLocalPos;
			parentedLocalPos.OnValueChanged = (NetworkVariable<Vector3>.OnValueChangedDelegate)Delegate.Remove(parentedLocalPos.OnValueChanged, new NetworkVariable<Vector3>.OnValueChangedDelegate(OnParentedLocalPositionChanged));
			NetworkVariable<bool> tooltipSuppressedByPlacement = _tooltipSuppressedByPlacement;
			tooltipSuppressedByPlacement.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(tooltipSuppressedByPlacement.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnPlacementTooltipSuppressionChanged));
			ClientCancelThrowFlight();
			ClientDestroy();
			base.OnNetworkDespawn();
		}

		private void OnParentedLocalPositionChanged(Vector3 previousPosition, Vector3 currentPosition)
		{
			UpdateParentedShadowPosition(_fpsParentedShadowObject, currentPosition);
			UpdateParentedShadowPosition(_tpsParentedShadowObject, currentPosition);
		}

		private static void UpdateParentedShadowPosition(GameObject shadowObject, Vector3 localPosition)
		{
			if (shadowObject != null)
			{
				shadowObject.transform.localPosition = localPosition;
			}
		}

		public virtual string SaveCustomData()
		{
			return string.Empty;
		}

		public virtual void LoadCustomData(FixedString128Bytes data)
		{
		}

		public override void GazeEnter()
		{
			base.GazeEnter();
			if (!_tooltipSuppressedByPlacement.Value)
			{
				Singleton<ToolTipHandler>.Instance.OnGazeEnter(this);
			}
		}

		public override void GazeExit()
		{
			base.GazeExit();
			Singleton<ToolTipHandler>.Instance.OnGazeExit(this);
		}

		protected override void OnPlacementScanSuppressionChanged(bool previousValue, bool currentValue)
		{
			if (_inGaze && !_tooltipSuppressedByPlacement.Value)
			{
				Singleton<ToolTipHandler>.Instance?.OnGazeEnter(this);
			}
		}

		private void OnPlacementTooltipSuppressionChanged(bool previousValue, bool currentValue)
		{
			if (currentValue)
			{
				Singleton<ToolTipHandler>.Instance?.OnGazeExit(this);
			}
			else if (_inGaze)
			{
				Singleton<ToolTipHandler>.Instance?.OnGazeEnter(this);
			}
		}

		public void ServerSetPlacementTooltipSuppressed(bool suppressed)
		{
			if (base.IsServer)
			{
				_tooltipSuppressedByPlacement.Value = suppressed;
			}
		}

		protected void GazeRefresh()
		{
			if (_inGaze)
			{
				GazeExit();
				Singleton<DelayHelper>.Instance.DelayActionUntilEndOfFrame(GazeEnter);
			}
		}

		public virtual TooltipData GetTooltip()
		{
			TooltipConfig tooltip = _itemSO.Tooltip;
			if (!tooltip.Enabled)
			{
				return default;
			}
			TooltipData data = tooltip.Data;
			data.Title = ((_itemSO != null) ? _itemSO.LocalizedName : data.Title);
			return data;
		}

		public override void EnableGazeHints()
		{
			Singleton<HintsManager>.Instance.AddHint("Interact", "key_interact");
			Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_hold");
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("Interact");
			Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
		}

		public virtual void EnableHeldHints()
		{
			Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_use");
			Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_place");
			AddHeldThrowHint();
		}

		public virtual void DisableHeldHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
			Singleton<HintsManager>.Instance.RemoveHint("RightMouseMechanic");
			RemoveHeldThrowHint();
		}

		protected void AddHeldThrowHint()
		{
			if (IsThrowable)
			{
				Singleton<HintsManager>.Instance.AddHint("Throw", "key_throw");
			}
		}

		protected void RemoveHeldThrowHint()
		{
			Singleton<HintsManager>.Instance.RemoveHint("Throw");
		}

		private void ServerCacheParentedLocalPosition(NetworkObject parentNetworkObject, Vector3 worldPos)
		{
			if (parentNetworkObject == null)
			{
				_parentedLocalPos.Value = Vector3.zero;
			}
			else
			{
				_parentedLocalPos.Value = parentNetworkObject.transform.InverseTransformPoint(worldPos);
			}
		}

		private void ServerClearParentedState()
		{
			_parented = false;
			_parentedLocalPos.Value = Vector3.zero;
		}

		public bool ServerPlaceOnParentDirect(NetworkObject parentNetworkObject, Vector3 pos, Vector3 rot, Action onClientPlacementQueued = null, bool staffPlacement = false)
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (parentNetworkObject == null)
			{
				return false;
			}
			ulong holderClientId = base.OwnerClientId;
			ServerClearHolderPresentation();
			CanGaze.Value = true;
			CanHold.Value = true;
			_serverPlayerInventoryOwned = false;
			if (!base.NetworkObject.TrySetParent(parentNetworkObject, worldPositionStays: false))
			{
				UtilityMethods.Log("[BaseHoldable] ServerPlaceOnParentDirect TrySetParent failed!", LogType.Warning);
				return false;
			}
			_parented = true;
			_parentedLocalPos.Value = parentNetworkObject.transform.InverseTransformPoint(pos);
			if (parentNetworkObject.TryGetComponent<BaseHoldable>(out var component))
			{
				component.ServerParentToMe(this);
			}
			ServerSetPlacementScanSuppressed(suppressed: false);
			ServerChangeChildsStatus(status: true, forget: false);
			Singleton<DelayHelper>.Instance.DelayActionUntilEndOfFrame(() =>
			{
				if (staffPlacement)
				{
					ClientStaffPlaceRpc(pos, Quaternion.Euler(rot));
				}
				else
				{
					ClientsPlaceRpc(pos, rot, holderClientId);
				}
				onClientPlacementQueued?.Invoke();
			});
			return true;
		}

		public virtual void ServerHold(Interactor interactor)
		{
			ServerSetStaffStack(-1);
			ServerClearHolderPresentation();
			if (_serverThrowInFlight)
			{
				ServerCancelThrowFlightForHold();
			}
			if (base.OwnerClientId != interactor.OwnerClientId)
			{
				base.NetworkObject.ChangeOwnership(interactor.OwnerClientId);
			}
			if (_parented)
			{
				if (base.transform.parent != null && base.transform.parent.TryGetComponent<BaseHoldable>(out var component))
				{
					component.ServerUnParentFromMe(this);
				}
				if (!base.NetworkObject.TryRemoveParent())
				{
					UtilityMethods.Log("[BaseHoldable] ServerHold TryRemoveParent failed!", LogType.Warning);
					return;
				}
				ServerClearParentedState();
			}
			CanGaze.Value = false;
			CanHold.Value = false;
			_serverPlayerInventoryOwned = true;
			ServerSetPlacementScanSuppressed(suppressed: false);
			ServerSetPlacementTooltipSuppressed(suppressed: false);
			ServerChangeChildsStatus(status: false, forget: false);
		}

		public virtual void ClientHold(Interactor interactor)
		{
			GazeExit();
			EnableHeldHints();
			SpawnFpsShadow(interactor.Player.RigBones.FpsRig.RightHand);
			_ownerInteractor = interactor;
			Singleton<QuickSlotHandler>.Instance.AddItem(this);
			ClientPlayHoldSfx();
		}

		protected virtual void ClientPlayHoldSfx()
		{
			if (!(Singleton<SoundManager>.Instance == null) && !(_itemSO == null))
			{
				Singleton<SoundManager>.Instance.PlaySFX(SoundType.Item_Pick, _itemSO.HoldableMaterialType, _itemSO.FurnitureWeight, base.transform.position);
			}
		}

		public virtual void ClientHoldFailed()
		{
			UtilityMethods.Log("[BaseHoldable] ClientHoldFailed");
		}

		public virtual void ServerLazyHold(ulong clientId, NetworkObjectReference interactorRef, bool select, bool hold)
		{
			ServerLazyHold(clientId, interactorRef, select, hold, useFallbackDropPosition: false, default, requireQuickSlot: false);
		}

		public void ServerLazyHold(ulong clientId, NetworkObjectReference interactorRef, bool select, bool hold, bool useFallbackDropPosition, Vector3 fallbackDropPosition, bool requireQuickSlot)
		{
			if (hold)
			{
				ServerClearHolderPresentation();
				CanGaze.Value = false;
				CanHold.Value = false;
				_serverPlayerInventoryOwned = true;
				ServerSetPlacementScanSuppressed(suppressed: false);
				ServerSetPlacementTooltipSuppressed(suppressed: false);
				if (base.OwnerClientId != clientId)
				{
					base.NetworkObject.ChangeOwnership(clientId);
				}
				ClientLazyHoldRpc(interactorRef, select, useFallbackDropPosition, fallbackDropPosition, requireQuickSlot);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientLazyHoldRpc(NetworkObjectReference interactorRef, bool select, bool useFallbackDropPosition, Vector3 fallbackDropPosition, bool requireQuickSlot)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2881547370u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in select, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in useFallbackDropPosition, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in fallbackDropPosition);
				bufferWriter.WriteValueSafe(in requireQuickSlot, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2881547370u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!interactorRef.TryGetComponent<Interactor>(out var component))
			{
				UtilityMethods.Log("[BaseHoldable] ClientLazyHoldRpc - Interactor not found!", LogType.Warning);
				return;
			}
			CollidersStatus(status: false);
			ObjectsStatus(status: false);
			if (select)
			{
				SpawnTpsShadow(component.Player.RigBones.TpsRig.RightHand, base.IsOwner);
			}
			if (!base.IsOwner)
			{
				return;
			}
			_ownerInteractor = component;
			bool flag = false;
			if (select)
			{
				SpawnFpsShadow(component.Player.RigBones.FpsRig.RightHand);
				flag = component.Player != null && component.Player.ActiveTool != null;
				if (flag)
				{
					component.Player.ActiveTool.RefreshCloseToolHint();
				}
				else
				{
					EnableHeldHints();
				}
			}
			if (!(requireQuickSlot ? Singleton<QuickSlotHandler>.Instance.CanHoldSlot(this) : Singleton<QuickSlotHandler>.Instance.CanHold(this)))
			{
				Vector3 vector = (useFallbackDropPosition ? fallbackDropPosition : (component.Player.CameraParent.position + component.Player.CameraParent.forward));
				if (component.Player.Thrower != null && component.Player.Thrower.TryBuildFreeFallRequestPayload(this, vector, out var data))
				{
					RequestFreeFall(data);
				}
				else
				{
					UtilityMethods.Log("[BaseHoldable] Lazy hold free-fall payload failed. Dropping item instead. Item:" + base.name, LogType.Warning);
					Drop(vector, Quaternion.identity);
				}
			}
			else
			{
				Singleton<QuickSlotHandler>.Instance.AddItem(this, select);
			}
			if (select & flag)
			{
				component.Player.ActiveTool?.RefreshCloseToolHint();
			}
			Singleton<DelayHelper>.Instance.DelayAction(2f, () =>
			{
				Singleton<PPG_WheelManager>.Instance.DisableToolWheel = false;
				Singleton<PPG_WheelManager>.Instance.DisableEmoteWheel = false;
			});
		}

		protected virtual void ClientHoldEveryone()
		{
		}

		public virtual void ClientEnsureHeldVisuals(Interactor interactor)
		{
			if (interactor == null || interactor.Player == null)
			{
				return;
			}
			CollidersStatus(status: false);
			ObjectsStatus(status: false);
			if (_tpsShadowObject == null)
			{
				SpawnTpsShadow(interactor.Player.RigBones.TpsRig.RightHand, base.IsOwner);
			}
			else
			{
				TpsShadowStatus(status: true);
			}
			if (base.IsOwner)
			{
				if ((object)_ownerInteractor != interactor)
				{
					ClientHold(interactor);
				}
				else if (_fpsShadowObject == null)
				{
					SpawnFpsShadow(interactor.Player.RigBones.FpsRig.RightHand);
				}
				else
				{
					FpsShadowStatus(status: true);
				}
			}
			ClientHoldEveryone();
		}

		protected override void ClientMouse1(Interactor interactor, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				interactor.TryHold(this);
			}
		}

		protected virtual bool CanCrossHold(Interactor interactor, BaseHoldable held)
		{
			return true;
		}

		protected override void ClientCrossMouse1(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				if (CanCrossHold(interactor, held))
				{
					interactor.TryHold(this);
				}
				else
				{
					base.ClientCrossMouse1(interactor, held, phase);
				}
			}
		}

		private Transform GetFpsParentShadowRoot()
		{
			if (_fpsShadowObject != null)
			{
				return _fpsShadowObject.transform;
			}
			if (_fpsParentedShadowObject != null)
			{
				return _fpsParentedShadowObject.transform;
			}
			return null;
		}

		private Transform GetTpsParentShadowRoot()
		{
			if (_tpsShadowObject != null)
			{
				return _tpsShadowObject.transform;
			}
			if (_tpsParentedShadowObject != null)
			{
				return _tpsParentedShadowObject.transform;
			}
			return null;
		}

		protected virtual GameObject InstantiateShadowGraphic(Transform parent)
		{
			return UnityEngine.Object.Instantiate(_itemSO.GraphicObjectPrefab, parent);
		}

		protected virtual void SpawnFpsShadow(Transform parent)
		{
			DestroyFpsShadow();
			_fpsShadowObject = InstantiateShadowGraphic(parent);
			if (ItemSO.FPSHoldOffset.ScaleOffset != Vector3.zero)
			{
				_fpsShadowObject.transform.localScale = ItemSO.FPSHoldOffset.ScaleOffset;
			}
			_fpsShadowObject.transform.SetLocalPositionAndRotation(ItemSO.FPSHoldOffset.PositionOffset, Quaternion.Euler(ItemSO.FPSHoldOffset.RotationOffset));
			UtilityMethods.SetLayerRecursive(_fpsShadowObject, "ViewModel", includeChildren: true);
			SpawnFpsParenteds();
		}

		public virtual void SpawnFpsParenteds()
		{
			Transform fpsParentShadowRoot = GetFpsParentShadowRoot();
			if (fpsParentShadowRoot == null)
			{
				return;
			}
			foreach (NetworkObjectReference parentedChild in _parentedChilds)
			{
				if (parentedChild.TryGetComponent<BaseHoldable>(out var component, base.NetworkManager))
				{
					component.SpawnFpsParentedShadow(fpsParentShadowRoot);
				}
			}
		}

		protected virtual void SpawnTpsShadow(Transform parent, bool isOwner)
		{
			DestroyTpsShadow();
			_tpsShadowObject = InstantiateShadowGraphic(parent);
			ItemSO.ApplyTpsHoldOffset(_tpsShadowObject.transform);
			UtilityMethods.SetLayerRecursive(_tpsShadowObject, isOwner ? "Player" : "ClientPlayer", includeChildren: true);
			SpawnTpsParenteds(isOwner);
		}

		public virtual void SpawnTpsParenteds(bool isOwner)
		{
			Transform tpsParentShadowRoot = GetTpsParentShadowRoot();
			if (tpsParentShadowRoot == null)
			{
				return;
			}
			foreach (NetworkObjectReference parentedChild in _parentedChilds)
			{
				if (parentedChild.TryGetComponent<BaseHoldable>(out var component, base.NetworkManager))
				{
					component.SpawnTpsParentedShadow(tpsParentShadowRoot, isOwner);
				}
			}
		}

		public virtual void SpawnFpsParentedShadow(Transform parent)
		{
			if (_fpsParentedShadowObject != null)
			{
				UnityEngine.Object.Destroy(_fpsParentedShadowObject);
			}
			_fpsParentedShadowObject = InstantiateShadowGraphic(parent);
			_fpsParentedShadowObject.transform.SetLocalPositionAndRotation(_parentedLocalPos.Value, Quaternion.identity);
			UtilityMethods.SetLayerRecursive(_fpsParentedShadowObject, "ViewModel", includeChildren: true);
			SpawnFpsParenteds();
			ClientRefreshRuntimeRepresentationState();
		}

		public virtual void SpawnTpsParentedShadow(Transform parent, bool isOwner)
		{
			if (_tpsParentedShadowObject != null)
			{
				UnityEngine.Object.Destroy(_tpsParentedShadowObject);
			}
			_tpsParentedShadowObject = InstantiateShadowGraphic(parent);
			_tpsParentedShadowObject.transform.SetLocalPositionAndRotation(_parentedLocalPos.Value, Quaternion.identity);
			UtilityMethods.SetLayerRecursive(_tpsParentedShadowObject, isOwner ? "Player" : "ClientPlayer", includeChildren: true);
			SpawnTpsParenteds(isOwner);
			ClientRefreshRuntimeRepresentationState();
		}

		protected virtual void DestroyFpsShadow()
		{
			foreach (NetworkObjectReference parentedChild in _parentedChilds)
			{
				if (parentedChild.TryGetComponent<BaseHoldable>(out var component, base.NetworkManager))
				{
					component.DestroyFpsParentedShadow();
				}
			}
			if (!(_fpsShadowObject == null))
			{
				UnityEngine.Object.Destroy(_fpsShadowObject);
				_fpsShadowObject = null;
			}
		}

		protected virtual void DestroyTpsShadow()
		{
			foreach (NetworkObjectReference parentedChild in _parentedChilds)
			{
				if (parentedChild.TryGetComponent<BaseHoldable>(out var component, base.NetworkManager))
				{
					component.DestroyTpsParentedShadow();
				}
			}
			if (!(_tpsShadowObject == null))
			{
				UnityEngine.Object.Destroy(_tpsShadowObject);
				_tpsShadowObject = null;
			}
		}

		public virtual void DestroyFpsParentedShadow()
		{
			foreach (NetworkObjectReference parentedChild in _parentedChilds)
			{
				if (parentedChild.TryGetComponent<BaseHoldable>(out var component, base.NetworkManager))
				{
					component.DestroyFpsParentedShadow();
				}
			}
			if (!(_fpsParentedShadowObject == null))
			{
				UnityEngine.Object.Destroy(_fpsParentedShadowObject);
				_fpsParentedShadowObject = null;
			}
		}

		public virtual void DestroyTpsParentedShadow()
		{
			foreach (NetworkObjectReference parentedChild in _parentedChilds)
			{
				if (parentedChild.TryGetComponent<BaseHoldable>(out var component, base.NetworkManager))
				{
					component.DestroyTpsParentedShadow();
				}
			}
			if (!(_tpsParentedShadowObject == null))
			{
				UnityEngine.Object.Destroy(_tpsParentedShadowObject);
				_tpsParentedShadowObject = null;
			}
		}

		public void ObjectsStatus(bool status)
		{
			if (_deactiveObjects == null)
			{
				return;
			}
			for (int i = 0; i < _deactiveObjects.Count; i++)
			{
				GameObject gameObject = _deactiveObjects[i];
				if (!(gameObject == null))
				{
					gameObject.SetActive(status);
				}
			}
		}

		public void CollidersStatus(bool status)
		{
			if (_deactiveColliders == null)
			{
				return;
			}
			int count = _deactiveColliders.Count;
			for (int i = 0; i < count; i++)
			{
				Collider collider = _deactiveColliders[i];
				if (!(collider == null))
				{
					collider.enabled = status;
				}
			}
		}

		protected virtual void FpsShadowStatus(bool status)
		{
			if (!(_fpsShadowObject == null))
			{
				_fpsShadowObject.SetActive(status);
			}
		}

		protected virtual void TpsShadowStatus(bool status)
		{
			if (!(_tpsShadowObject == null))
			{
				_tpsShadowObject.SetActive(status);
			}
		}

		public virtual void ServerChangeChildsStatus(bool status, bool forget)
		{
			foreach (NetworkObjectReference parentedChild in _parentedChilds)
			{
				if (parentedChild.TryGetComponent<BaseHoldable>(out var component, base.NetworkManager))
				{
					component.ClientParentedChildStatusRpc(status);
					component.CanGaze.Value = !forget;
					component.CanHold.Value = !forget;
					component.ServerChangeChildsStatus(status, forget);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientParentedChildStatusRpc(bool status)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1091750692u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in status, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1091750692u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ObjectsStatus(status);
				CollidersStatus(status);
			}
		}

		public virtual void RequestThrow(Thrower.ThrowPhysicsRequestData throwData)
		{
			ServerThrowHeldRpc(throwData.StartPosition, throwData.Direction, throwData.Force, throwData.UpwardForce, throwData.TorqueImpulse, throwData.ForceModeId, throwData.Mass, throwData.UseGravity, throwData.LinearDamping, throwData.AngularDamping, throwData.CollisionDetectionModeId, throwData.InterpolationId, throwData.ExcludeLayerMaskValue, throwData.SettleVelocity, throwData.SettleStableDuration, throwData.MaxFlightDuration, throwData.StartedUnderwater, throwData.IsValid);
		}

		public void RequestFreeFall(Thrower.ThrowPhysicsRequestData throwData)
		{
			ServerFreeFallRpc(throwData.StartPosition, throwData.Direction, throwData.Force, throwData.UpwardForce, throwData.TorqueImpulse, throwData.ForceModeId, throwData.Mass, throwData.LinearDamping, throwData.AngularDamping, throwData.CollisionDetectionModeId, throwData.InterpolationId, throwData.ExcludeLayerMaskValue, throwData.SettleVelocity, throwData.SettleStableDuration, throwData.MaxFlightDuration, throwData.StartedUnderwater, throwData.IsValid);
		}

		public bool ServerStartWorldFreeFall(Vector3 startPosition, Vector3 direction, ulong preferredSimulationClientId, bool startedUnderwater, int excludeLayerMaskValue = 0, Vector3 torqueImpulse = default(Vector3), float maxFlightDuration = 5f)
		{
			if (!base.IsServer || base.NetworkObject == null || !base.NetworkObject.IsSpawned || !IsThrowable)
			{
				return false;
			}
			if (base.NetworkManager == null || base.NetworkManager.ConnectedClients.Count == 0)
			{
				return false;
			}
			ulong num = preferredSimulationClientId;
			if (!base.NetworkManager.ConnectedClients.ContainsKey(num))
			{
				using IEnumerator<ulong> enumerator = base.NetworkManager.ConnectedClientsIds.GetEnumerator();
				if (enumerator.MoveNext())
				{
					num = enumerator.Current;
				}
			}
			if (!base.NetworkManager.ConnectedClients.ContainsKey(num))
			{
				return false;
			}
			if (!IsFinite(direction) || direction.sqrMagnitude < 0.0001f)
			{
				direction = Vector3.down;
			}
			if (!ServerTryAcceptPhysicsThrow(startPosition, direction, 0.01f, 0f, torqueImpulse, 1, 1f, 0.1f, 0.05f, 2, 1, excludeLayerMaskValue, 0.08f, 0.3f, maxFlightDuration, startedUnderwater, isValid: true, out var data))
			{
				return false;
			}
			if (base.OwnerClientId != num)
			{
				base.NetworkObject.ChangeOwnership(num);
			}
			data.UseGravity = true;
			ServerApplyPhysicsThrow(data, num, clearHeldAction: false);
			return true;
		}

		[Rpc(SendTo.Server)]
		private void ServerFreeFallRpc(Vector3 startPosition, Vector3 direction, float force, float upwardForce, Vector3 torqueImpulse, int forceModeId, float mass, float linearDamping, float angularDamping, int collisionDetectionModeId, int interpolationId, int excludeLayerMaskValue, float settleVelocity, float settleStableDuration, float maxFlightDuration, bool startedUnderwater, bool isValid, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(4058289452u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in startPosition);
				bufferWriter.WriteValueSafe(in direction);
				bufferWriter.WriteValueSafe(in force, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in upwardForce, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in torqueImpulse);
				BytePacker.WriteValueBitPacked(bufferWriter, forceModeId);
				bufferWriter.WriteValueSafe(in mass, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in linearDamping, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in angularDamping, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, collisionDetectionModeId);
				BytePacker.WriteValueBitPacked(bufferWriter, interpolationId);
				BytePacker.WriteValueBitPacked(bufferWriter, excludeLayerMaskValue);
				bufferWriter.WriteValueSafe(in settleVelocity, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in settleStableDuration, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in maxFlightDuration, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in startedUnderwater, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in isValid, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 4058289452u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (IsThrowable && base.OwnerClientId == senderClientId)
			{
				if (!ServerTryAcceptPhysicsThrow(startPosition, direction, force, upwardForce, torqueImpulse, forceModeId, mass, linearDamping, angularDamping, collisionDetectionModeId, interpolationId, excludeLayerMaskValue, settleVelocity, settleStableDuration, maxFlightDuration, startedUnderwater, isValid, out var data))
				{
					UtilityMethods.Log($"[BaseHoldable] Free-fall rejected. Payload is invalid. Object:{base.name}, Requester:{senderClientId}", LogType.Warning);
					return;
				}
				data.UseGravity = true;
				ServerApplyPhysicsThrow(data, senderClientId, clearHeldAction: false);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerThrowHeldRpc(Vector3 startPosition, Vector3 direction, float force, float upwardForce, Vector3 torqueImpulse, int forceModeId, float mass, bool useGravity, float linearDamping, float angularDamping, int collisionDetectionModeId, int interpolationId, int excludeLayerMaskValue, float settleVelocity, float settleStableDuration, float maxFlightDuration, bool startedUnderwater, bool isValid, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3858765115u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in startPosition);
				bufferWriter.WriteValueSafe(in direction);
				bufferWriter.WriteValueSafe(in force, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in upwardForce, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in torqueImpulse);
				BytePacker.WriteValueBitPacked(bufferWriter, forceModeId);
				bufferWriter.WriteValueSafe(in mass, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in useGravity, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in linearDamping, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in angularDamping, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, collisionDetectionModeId);
				BytePacker.WriteValueBitPacked(bufferWriter, interpolationId);
				BytePacker.WriteValueBitPacked(bufferWriter, excludeLayerMaskValue);
				bufferWriter.WriteValueSafe(in settleVelocity, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in settleStableDuration, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in maxFlightDuration, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in startedUnderwater, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in isValid, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3858765115u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!ServerCanExecuteHeldAction(senderClientId, "Throw"))
			{
				ClientForceClearQuickSlotReferenceRpc(senderClientId);
				return;
			}
			if (!IsThrowable)
			{
				UtilityMethods.Log($"[BaseHoldable] Throw rejected. Object is not throwable. Object:{base.name}, Requester:{senderClientId}", LogType.Warning);
				ClientThrowRejectedRpc(senderClientId);
				return;
			}
			if (!ServerTryAcceptPhysicsThrow(startPosition, direction, force, upwardForce, torqueImpulse, forceModeId, mass, linearDamping, angularDamping, collisionDetectionModeId, interpolationId, excludeLayerMaskValue, settleVelocity, settleStableDuration, maxFlightDuration, startedUnderwater, isValid, out var data))
			{
				UtilityMethods.Log($"[BaseHoldable] Throw rejected. Payload is invalid. Object:{base.name}, Requester:{senderClientId}", LogType.Warning);
				ClientThrowRejectedRpc(senderClientId);
				return;
			}
			data.UseGravity = useGravity;
			if (UsesServerAuthoritativePersistentThrow)
			{
				ServerApplyPersistentPhysicsThrow(data, senderClientId);
			}
			else
			{
				ServerApplyPhysicsThrow(data, senderClientId);
			}
		}

		protected virtual void ServerOnPersistentPhysicsThrowStarted(PhysicsThrowData data)
		{
		}

		protected virtual void ClientOnPersistentPhysicsThrowStarted(bool isLocalHolder)
		{
		}

		protected virtual void ClientOnThrowRejected()
		{
		}

		protected virtual void ClientOnHeldActionForceCleared()
		{
		}

		protected virtual void ServerSetPersistentPhysicsThrowPose(Vector3 startPosition, Quaternion startRotation)
		{
			base.transform.SetPositionAndRotation(startPosition, startRotation);
		}

		private void ServerApplyPersistentPhysicsThrow(PhysicsThrowData data, ulong holderClientId)
		{
			_serverThrowInFlight = false;
			_throwFlightVersion++;
			ServerClearHolderPresentation();
			CanGaze.Value = true;
			CanHold.Value = true;
			_serverPlayerInventoryOwned = false;
			ServerChangeChildsStatus(status: true, forget: false);
			ServerSetPersistentPhysicsThrowPose(data.StartPosition, BuildThrowRotation(data.Direction));
			if (base.OwnerClientId != 0L)
			{
				base.NetworkObject.RemoveOwnership();
			}
			ClientsStartPersistentPhysicsThrowRpc(data.StartPosition, data.Direction, holderClientId);
			ServerOnPersistentPhysicsThrowStarted(data);
		}

		[Rpc(SendTo.Everyone)]
		private void ClientsStartPersistentPhysicsThrowRpc(Vector3 startPosition, Vector3 direction, ulong holderClientId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2779884290u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in startPosition);
				bufferWriter.WriteValueSafe(in direction);
				BytePacker.WriteValueBitPacked(bufferWriter, holderClientId);
				__endSendRpc(ref bufferWriter, 2779884290u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ClientCancelThrowFlight();
			bool flag = IsLocalClient(holderClientId);
			if (flag)
			{
				DisableHeldHints();
				DestroyFpsShadow();
				if (_ownerInteractor != null)
				{
					_ownerInteractor.EndThrow();
					_ownerInteractor = null;
				}
				if (Singleton<QuickSlotHandler>.Instance != null)
				{
					Singleton<QuickSlotHandler>.Instance.RemoveSpecificItem(this, selectNextIfPossible: false);
				}
			}
			DestroyTpsShadow();
			base.transform.SetPositionAndRotation(startPosition, BuildThrowRotation(direction));
			ObjectsStatus(status: true);
			CollidersStatus(status: true);
			ClientOnPersistentPhysicsThrowStarted(flag);
			ClientRefreshRuntimeRepresentationState();
			ClientRefreshParentedChildrenRuntimeRepresentationStates();
		}

		private void ServerApplyPhysicsThrow(PhysicsThrowData data, ulong holderClientId, bool clearHeldAction = true)
		{
			_serverThrowInFlight = true;
			int flightVersion = ++_throwFlightVersion;
			ServerClearHolderPresentation();
			CanGaze.Value = true;
			CanHold.Value = true;
			_serverPlayerInventoryOwned = false;
			ServerChangeChildsStatus(status: true, forget: false);
			base.transform.SetPositionAndRotation(data.StartPosition, BuildThrowRotation(data.Direction));
			ClientsStartPhysicsThrowRpc(data.StartPosition, data.Direction, data.Force, data.UpwardForce, data.TorqueImpulse, data.ForceModeId, data.Mass, data.UseGravity, data.LinearDamping, data.AngularDamping, data.CollisionDetectionModeId, data.InterpolationId, data.ExcludeLayerMaskValue, data.SettleVelocity, data.SettleStableDuration, data.MaxFlightDuration, data.StartedUnderwater, holderClientId, clearHeldAction, flightVersion);
			Singleton<DelayHelper>.Instance.DelayAction(data.MaxFlightDuration + data.SettleStableDuration + 0.25f, () =>
			{
				if (!(base.NetworkObject == null) && base.NetworkObject.IsSpawned && _serverThrowInFlight && _throwFlightVersion == flightVersion)
				{
					_serverThrowInFlight = false;
					CanGaze.Value = true;
					CanHold.Value = true;
					ServerChangeChildsStatus(status: true, forget: false);
					ServerRecoverInaccessibleThrownItem();
					if (base.OwnerClientId != 0L)
					{
						base.NetworkObject.RemoveOwnership();
					}
					ClientsCancelThrowFlightRpc(flightVersion);
					UtilityMethods.Log($"[BaseHoldable] Throw timed out. Object:{base.name}, FlightVersion:{flightVersion}", LogType.Warning);
				}
			});
		}

		[Rpc(SendTo.Everyone)]
		private void ClientsStartPhysicsThrowRpc(Vector3 startPosition, Vector3 direction, float force, float upwardForce, Vector3 torqueImpulse, int forceModeId, float mass, bool useGravity, float linearDamping, float angularDamping, int collisionDetectionModeId, int interpolationId, int excludeLayerMaskValue, float settleVelocity, float settleStableDuration, float maxFlightDuration, bool startedUnderwater, ulong holderClientId, bool clearHeldAction, int flightVersion)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1214121656u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in startPosition);
				bufferWriter.WriteValueSafe(in direction);
				bufferWriter.WriteValueSafe(in force, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in upwardForce, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in torqueImpulse);
				BytePacker.WriteValueBitPacked(bufferWriter, forceModeId);
				bufferWriter.WriteValueSafe(in mass, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in useGravity, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in linearDamping, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in angularDamping, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, collisionDetectionModeId);
				BytePacker.WriteValueBitPacked(bufferWriter, interpolationId);
				BytePacker.WriteValueBitPacked(bufferWriter, excludeLayerMaskValue);
				bufferWriter.WriteValueSafe(in settleVelocity, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in settleStableDuration, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in maxFlightDuration, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in startedUnderwater, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, holderClientId);
				bufferWriter.WriteValueSafe(in clearHeldAction, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, flightVersion);
				__endSendRpc(ref bufferWriter, 1214121656u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (flightVersion < _throwFlightVersion)
			{
				return;
			}
			ClientCancelThrowFlight();
			_throwFlightVersion = flightVersion;
			bool flag = IsLocalClient(holderClientId);
			if (flag)
			{
				DisableHeldHints();
				DestroyFpsShadow();
				if (clearHeldAction && _ownerInteractor != null)
				{
					_ownerInteractor.EndThrow();
				}
				_ownerInteractor = null;
				if (Singleton<QuickSlotHandler>.Instance != null)
				{
					Singleton<QuickSlotHandler>.Instance.RemoveSpecificItem(this, clearHeldAction);
				}
			}
			DestroyTpsShadow();
			ClientOnPhysicsThrowStarted(flag);
			ObjectsStatus(status: true);
			CollidersStatus(status: true);
			ClientRefreshRuntimeRepresentationState();
			base.transform.SetPositionAndRotation(startPosition, BuildThrowRotation(direction));
			ClientStartLocalPhysicsThrow(direction, force, upwardForce, torqueImpulse, forceModeId, mass, useGravity, linearDamping, angularDamping, collisionDetectionModeId, interpolationId, excludeLayerMaskValue, settleVelocity, settleStableDuration, maxFlightDuration, startedUnderwater, flag, flightVersion);
		}

		[Rpc(SendTo.Everyone)]
		private void ClientThrowRejectedRpc(ulong targetClientId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1462937754u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, targetClientId);
				__endSendRpc(ref bufferWriter, 1462937754u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (IsLocalClient(targetClientId))
			{
				ClientOnThrowRejected();
				ClientCancelThrowFlight();
				if (_ownerInteractor != null)
				{
					_ownerInteractor.EndThrow(throwDone: false);
				}
				EnableHeldHints();
				UtilityMethods.Log("[BaseHoldable] Throw rejected by server. Object:" + base.name, LogType.Warning);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientsCancelThrowFlightRpc(int flightVersion)
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
				FastBufferWriter bufferWriter = __beginSendRpc(836720658u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, flightVersion);
				__endSendRpc(ref bufferWriter, 836720658u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (flightVersion >= _throwFlightVersion)
				{
					_throwFlightVersion = flightVersion;
					ClientCancelThrowFlight();
					CollidersStatus(status: true);
					ObjectsStatus(status: true);
					ClientRefreshRuntimeRepresentationState();
					ClientRefreshParentedChildrenRuntimeRepresentationStates();
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerThrowSettledRpc(Vector3 finalPosition, Quaternion finalRotation, int flightVersion, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1854315509u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in finalPosition);
				bufferWriter.WriteValueSafe(in finalRotation);
				BytePacker.WriteValueBitPacked(bufferWriter, flightVersion);
				__endSendRpc(ref bufferWriter, 1854315509u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!_serverThrowInFlight || _throwFlightVersion != flightVersion)
			{
				return;
			}
			if (base.OwnerClientId != senderClientId)
			{
				UtilityMethods.Log($"[BaseHoldable] Throw settle ignored. Requester is not owner. Object:{base.name}, Requester:{senderClientId}, Owner:{base.OwnerClientId}", LogType.Warning);
			}
			else if (IsFinite(finalPosition) && IsFinite(finalRotation))
			{
				_serverThrowInFlight = false;
				base.transform.SetPositionAndRotation(finalPosition, finalRotation);
				ServerRecoverInaccessibleThrownItem();
				CanGaze.Value = true;
				CanHold.Value = true;
				ServerChangeChildsStatus(status: true, forget: false);
				if (base.OwnerClientId != 0L)
				{
					base.NetworkObject.RemoveOwnership();
				}
			}
		}

		private void ServerRecoverInaccessibleThrownItem()
		{
			if (base.IsServer)
			{
				Physics.SyncTransforms();
				if (ThrowRecoveryStation.TryGetRecoveryPosition(this, out var recoveryPosition))
				{
					base.transform.position = recoveryPosition;
					UtilityMethods.Log($"[BaseHoldable] Recovered inaccessible thrown item. Object:{base.name}, Position:{recoveryPosition}", LogType.Warning);
				}
			}
		}

		private void ServerCancelThrowFlightForHold()
		{
			if (base.IsServer && _serverThrowInFlight)
			{
				_serverThrowInFlight = false;
				int flightVersion = ++_throwFlightVersion;
				CanGaze.Value = true;
				CanHold.Value = true;
				ServerChangeChildsStatus(status: true, forget: false);
				ClientsCancelThrowFlightRpc(flightVersion);
			}
		}

		private void ClientStartLocalPhysicsThrow(Vector3 direction, float force, float upwardForce, Vector3 torqueImpulse, int forceModeId, float mass, bool useGravity, float linearDamping, float angularDamping, int collisionDetectionModeId, int interpolationId, int excludeLayerMaskValue, float settleVelocity, float settleStableDuration, float maxFlightDuration, bool startedUnderwater, bool reportSettleToServer, int flightVersion)
		{
			if (!TryCreateThrowPhysics(mass, useGravity, linearDamping, angularDamping, collisionDetectionModeId, interpolationId, excludeLayerMaskValue))
			{
				FinishLocalPhysicsThrow(reportSettleToServer, flightVersion);
				return;
			}
			_localThrowInFlight = true;
			_localThrowSettledReported = false;
			_throwEnteredWater = false;
			_throwImpactSfxPlayedThisFlight = false;
			_lastThrowImpactSfxTime = float.NegativeInfinity;
			if (startedUnderwater)
			{
				ApplyThrowWaterPhysics(base.transform.position, 0f, playImpactFx: false);
			}
			ApplyThrowForces(_throwRigidbody, direction, force, upwardForce, torqueImpulse, forceModeId);
			if (_throwMonitorCoroutine != null)
			{
				StopCoroutine(_throwMonitorCoroutine);
			}
			_throwMonitorCoroutine = StartCoroutine(MonitorLocalPhysicsThrow(Mathf.Max(0.01f, settleVelocity), Mathf.Max(0.01f, settleStableDuration), Mathf.Max(0.1f, maxFlightDuration), startedUnderwater, reportSettleToServer, flightVersion));
		}

		private IEnumerator MonitorLocalPhysicsThrow(float settleVelocity, float settleStableDuration, float maxFlightDuration, bool startedUnderwater, bool reportSettleToServer, int flightVersion)
		{
			float elapsed = 0f;
			float stableElapsed = 0f;
			Vector3 previousPhysicsPosition = _throwRigidbody.position;
			float nextPortalTeleportTime = 0f;
			while (elapsed < maxFlightDuration)
			{
				if (_throwRigidbody == null || !_localThrowInFlight || _throwFlightVersion != flightVersion)
				{
					yield break;
				}
				elapsed += Time.deltaTime;
				if (Time.time >= nextPortalTeleportTime && Portal.TryTeleportThrownItem(_throwRigidbody, previousPhysicsPosition))
				{
					nextPortalTeleportTime = Time.time + 0.25f;
					stableElapsed = 0f;
				}
				previousPhysicsPosition = _throwRigidbody.position;
				ProbeThrowWaterVolume();
				bool num = startedUnderwater || _throwEnteredWater;
				float num2 = (num ? Mathf.Max(settleVelocity, _throwWaterSettleVelocity) : settleVelocity);
				float num3 = (num ? Mathf.Min(settleStableDuration, _throwWaterSettleStableDuration) : settleStableDuration);
				float num4 = num2 * num2;
				stableElapsed = ((!(_throwRigidbody.linearVelocity.sqrMagnitude <= num4)) ? 0f : (stableElapsed + Time.deltaTime));
				if (stableElapsed >= num3)
				{
					break;
				}
				yield return null;
			}
			FinishLocalPhysicsThrow(reportSettleToServer, flightVersion);
		}

		private void FinishLocalPhysicsThrow(bool reportSettleToServer, int flightVersion)
		{
			if (_throwFlightVersion == flightVersion)
			{
				if (_throwMonitorCoroutine != null)
				{
					StopCoroutine(_throwMonitorCoroutine);
					_throwMonitorCoroutine = null;
				}
				_localThrowInFlight = false;
				Vector3 position = base.transform.position;
				Quaternion rotation = base.transform.rotation;
				RemoveThrowPhysics();
				base.transform.SetPositionAndRotation(position, rotation);
				CollidersStatus(status: true);
				ObjectsStatus(status: true);
				ClientOnPhysicsThrowEnded();
				if (ShouldPlayThrowSettleSfx())
				{
					PlayThrowImpactSfx(position);
				}
				ClientRefreshRuntimeRepresentationState();
				ClientRefreshParentedChildrenRuntimeRepresentationStates();
				if (reportSettleToServer && !_localThrowSettledReported)
				{
					_localThrowSettledReported = true;
					ServerThrowSettledRpc(position, rotation, flightVersion);
				}
			}
		}

		private void ClientCancelThrowFlight()
		{
			if (_throwMonitorCoroutine != null)
			{
				StopCoroutine(_throwMonitorCoroutine);
				_throwMonitorCoroutine = null;
			}
			_localThrowInFlight = false;
			_localThrowSettledReported = false;
			_throwEnteredWater = false;
			_throwImpactSfxPlayedThisFlight = false;
			RemoveThrowPhysics();
			ClientOnPhysicsThrowEnded();
		}

		protected virtual void ClientOnPhysicsThrowStarted(bool isLocalHolder)
		{
		}

		protected virtual void ClientOnPhysicsThrowEnded()
		{
		}

		protected virtual void OnCollisionEnter(Collision collision)
		{
			if (_localThrowInFlight && !TryHandleThrowWaterCollision(collision))
			{
				TryPlayThrowImpactSfx(collision);
			}
		}

		protected virtual void OnTriggerEnter(Collider other)
		{
			if (_localThrowInFlight)
			{
				TryHandleThrowWaterTrigger(other);
			}
		}

		private void ProbeThrowWaterVolume()
		{
			if (_throwEnteredWater)
			{
				return;
			}
			int num = Physics.OverlapSphereNonAlloc(base.transform.position, Mathf.Max(0.01f, _throwWaterProbeRadius), _throwWaterProbeHits, _throwWaterProbeLayers, QueryTriggerInteraction.Collide);
			for (int i = 0; i < num; i++)
			{
				Collider collider = _throwWaterProbeHits[i];
				if (!(collider == null) && IsThrowWaterCollider(collider))
				{
					TryHandleThrowWaterTrigger(collider);
					break;
				}
			}
		}

		private bool TryHandleThrowWaterCollision(Collision collision)
		{
			if (collision == null || collision.collider == null || !IsThrowWaterCollider(collision.collider))
			{
				return false;
			}
			Vector3 impactPosition = ((collision.contactCount > 0) ? collision.GetContact(0).point : base.transform.position);
			float magnitude = collision.relativeVelocity.magnitude;
			ApplyThrowWaterPhysics(impactPosition, magnitude, playImpactFx: true);
			return true;
		}

		private void TryHandleThrowWaterTrigger(Collider other)
		{
			if (other == null || !IsThrowWaterCollider(other))
			{
				return;
			}
			float impactSpeed = ((_throwRigidbody != null) ? _throwRigidbody.linearVelocity.magnitude : 0f);
			Vector3 impactPosition = base.transform.position;
			if (other.enabled)
			{
				Vector3 vector = other.ClosestPoint(base.transform.position);
				if (IsFinite(vector))
				{
					impactPosition = vector;
				}
			}
			ApplyThrowWaterPhysics(impactPosition, impactSpeed, playImpactFx: true);
		}

		private void ApplyThrowWaterPhysics(Vector3 impactPosition, float impactSpeed, bool playImpactFx)
		{
			if (_throwEnteredWater)
			{
				return;
			}
			_throwEnteredWater = true;
			_lastThrowImpactSfxTime = Time.time;
			if (_throwRigidbody != null)
			{
				_throwRigidbody.useGravity = true;
				_throwRigidbody.linearDamping = Mathf.Max(_throwRigidbody.linearDamping, _throwWaterLinearDamping);
				_throwRigidbody.angularDamping = Mathf.Max(_throwRigidbody.angularDamping, _throwWaterAngularDamping);
				if (_throwWaterVelocityCap > 0f && _throwRigidbody.linearVelocity.magnitude > _throwWaterVelocityCap)
				{
					_throwRigidbody.linearVelocity = _throwRigidbody.linearVelocity.normalized * _throwWaterVelocityCap;
				}
				if (_throwWaterVelocityCap > 0f && _throwRigidbody.angularVelocity.magnitude > _throwWaterVelocityCap)
				{
					_throwRigidbody.angularVelocity = _throwRigidbody.angularVelocity.normalized * _throwWaterVelocityCap;
				}
			}
			if (playImpactFx && !(Singleton<OceanFXManager>.Instance == null))
			{
				bool isBig = impactSpeed >= _throwWaterBigSplashSpeed;
				Singleton<OceanFXManager>.Instance.PlayObjectWaterImpact(impactPosition, impactSpeed, isBig);
			}
		}

		protected static bool IsThrowWaterCollider(Collider collider)
		{
			if (collider == null)
			{
				return false;
			}
			if (IsThrowWaterObject(collider.gameObject))
			{
				return true;
			}
			Transform parent = collider.transform;
			while (parent != null)
			{
				if (IsThrowWaterObject(parent.gameObject))
				{
					return true;
				}
				parent = parent.parent;
			}
			return false;
		}

		private static bool IsThrowWaterObject(GameObject target)
		{
			if (target == null)
			{
				return false;
			}
			if (!(target.name == "Underwater Physic Volume") && !(target.name == "UnderWater Volume"))
			{
				return target.CompareTag("Water");
			}
			return true;
		}

		private void TryPlayThrowImpactSfx(Collision collision)
		{
			if (!(Singleton<SoundManager>.Instance == null) && ((1 << collision.gameObject.layer) & _throwImpactExcludeLayers.value) == 0 && !(Time.time - _lastThrowImpactSfxTime < _throwImpactCooldown) && !(collision.relativeVelocity.magnitude < _throwImpactMinRelativeSpeed))
			{
				Vector3 position = ((collision.contactCount > 0) ? collision.GetContact(0).point : base.transform.position);
				_lastThrowImpactSfxTime = Time.time;
				PlayThrowImpactSfx(position);
			}
		}

		private bool ShouldPlayThrowSettleSfx()
		{
			if (_throwEnteredWater)
			{
				return false;
			}
			if (_throwImpactSfxPlayedThisFlight)
			{
				return false;
			}
			return Time.time - _lastThrowImpactSfxTime >= Mathf.Max(0.1f, _throwImpactCooldown);
		}

		private void PlayThrowImpactSfx(Vector3 position)
		{
			if (!(Singleton<SoundManager>.Instance == null))
			{
				if (_throwImpactSfxClip != null)
				{
					_throwImpactSfxPlayedThisFlight = true;
					Singleton<SoundManager>.Instance.PlayItemImpactSFX(_throwImpactSfxClip, position);
				}
				else if (!(_itemSO == null))
				{
					_throwImpactSfxPlayedThisFlight = true;
					Singleton<SoundManager>.Instance.PlaySFX(SoundType.Item_Put, _itemSO.HoldableMaterialType, _itemSO.FurnitureWeight, position);
				}
			}
		}

		private bool TryCreateThrowPhysics(float mass, bool useGravity, float linearDamping, float angularDamping, int collisionDetectionModeId, int interpolationId, int excludeLayerMaskValue)
		{
			RemoveThrowPhysics();
			_throwRigidbody = GetComponent<Rigidbody>();
			_throwRigidbodyCreated = _throwRigidbody == null;
			if (_throwRigidbody == null)
			{
				_throwRigidbody = base.gameObject.AddComponent<Rigidbody>();
			}
			StoreThrowRigidbodyState(_throwRigidbody);
			_throwRigidbody.mass = Mathf.Max(0.01f, mass);
			_throwRigidbody.useGravity = useGravity;
			_throwRigidbody.linearDamping = Mathf.Max(0f, linearDamping);
			_throwRigidbody.angularDamping = Mathf.Max(0f, angularDamping);
			_throwRigidbody.collisionDetectionMode = ResolveCollisionDetectionMode(collisionDetectionModeId);
			_throwRigidbody.interpolation = ResolveRigidbodyInterpolation(interpolationId);
			_throwRigidbody.excludeLayers = new LayerMask
			{
				value = excludeLayerMaskValue
			};
			_throwRigidbody.isKinematic = false;
			_throwRigidbody.linearVelocity = Vector3.zero;
			_throwRigidbody.angularVelocity = Vector3.zero;
			return _throwRigidbody != null;
		}

		private void RemoveThrowPhysics()
		{
			if (_throwRigidbody != null)
			{
				_throwRigidbody.linearVelocity = Vector3.zero;
				_throwRigidbody.angularVelocity = Vector3.zero;
				if (_throwRigidbodyCreated)
				{
					UnityEngine.Object.Destroy(_throwRigidbody);
				}
				else
				{
					RestoreThrowRigidbodyState(_throwRigidbody);
				}
			}
			_throwRigidbody = null;
			_throwRigidbodyCreated = false;
			_throwRigidbodyStateStored = false;
		}

		private void StoreThrowRigidbodyState(Rigidbody target)
		{
			if (!(target == null) && !_throwRigidbodyStateStored)
			{
				_previousMass = target.mass;
				_previousUseGravity = target.useGravity;
				_previousIsKinematic = target.isKinematic;
				_previousLinearDamping = target.linearDamping;
				_previousAngularDamping = target.angularDamping;
				_previousCollisionDetectionMode = target.collisionDetectionMode;
				_previousInterpolation = target.interpolation;
				_previousExcludeLayers = target.excludeLayers;
				_throwRigidbodyStateStored = true;
			}
		}

		private void RestoreThrowRigidbodyState(Rigidbody target)
		{
			if (!(target == null) && _throwRigidbodyStateStored)
			{
				target.mass = _previousMass;
				target.useGravity = _previousUseGravity;
				target.isKinematic = _previousIsKinematic;
				target.linearDamping = _previousLinearDamping;
				target.angularDamping = _previousAngularDamping;
				target.collisionDetectionMode = _previousCollisionDetectionMode;
				target.interpolation = _previousInterpolation;
				target.excludeLayers = _previousExcludeLayers;
			}
		}

		private bool ServerTryAcceptPhysicsThrow(Vector3 startPosition, Vector3 direction, float force, float upwardForce, Vector3 torqueImpulse, int forceModeId, float mass, float linearDamping, float angularDamping, int collisionDetectionModeId, int interpolationId, int excludeLayerMaskValue, float settleVelocity, float settleStableDuration, float maxFlightDuration, bool startedUnderwater, bool isValid, out PhysicsThrowData data)
		{
			data = default;
			if (!isValid)
			{
				return false;
			}
			if (!IsFinite(startPosition) || !IsFinite(direction) || !IsFinite(torqueImpulse) || direction.sqrMagnitude < 0.0001f)
			{
				return false;
			}
			if (!float.IsFinite(force) || force <= 0f)
			{
				return false;
			}
			if (!float.IsFinite(upwardForce) || upwardForce < 0f)
			{
				return false;
			}
			if (!float.IsFinite(mass) || mass <= 0f)
			{
				return false;
			}
			if (!float.IsFinite(linearDamping) || !float.IsFinite(angularDamping))
			{
				return false;
			}
			if (!float.IsFinite(settleVelocity) || !float.IsFinite(settleStableDuration) || !float.IsFinite(maxFlightDuration))
			{
				return false;
			}
			data = new PhysicsThrowData
			{
				StartPosition = startPosition,
				Direction = direction.normalized,
				Force = Mathf.Clamp(force, 0.01f, 100f),
				UpwardForce = Mathf.Clamp(upwardForce, 0f, 100f),
				TorqueImpulse = Vector3.ClampMagnitude(torqueImpulse, 100f),
				ForceModeId = forceModeId,
				Mass = Mathf.Clamp(mass, 0.01f, 100f),
				LinearDamping = Mathf.Clamp(linearDamping, 0f, 25f),
				AngularDamping = Mathf.Clamp(angularDamping, 0f, 25f),
				CollisionDetectionModeId = collisionDetectionModeId,
				InterpolationId = interpolationId,
				ExcludeLayerMaskValue = excludeLayerMaskValue,
				SettleVelocity = Mathf.Clamp(settleVelocity, 0.01f, 10f),
				SettleStableDuration = Mathf.Clamp(settleStableDuration, 0.01f, 5f),
				MaxFlightDuration = Mathf.Clamp(maxFlightDuration, 0.1f, 30f),
				StartedUnderwater = startedUnderwater
			};
			return true;
		}

		private static Quaternion BuildThrowRotation(Vector3 direction)
		{
			if (direction.sqrMagnitude < 0.0001f)
			{
				return Quaternion.identity;
			}
			return Quaternion.LookRotation(direction.normalized, Vector3.up);
		}

		protected static void ApplyThrowForces(Rigidbody target, Vector3 direction, float force, float upwardForce, Vector3 torqueImpulse, int forceModeId)
		{
			if (!(target == null))
			{
				Vector3 force2 = direction.normalized * force + Vector3.up * upwardForce;
				target.AddForce(force2, ResolveForceMode(forceModeId));
				if (torqueImpulse.sqrMagnitude > 0.0001f)
				{
					target.AddTorque(torqueImpulse, ForceMode.Impulse);
				}
			}
		}

		private static ForceMode ResolveForceMode(int forceModeId)
		{
			if (!Enum.IsDefined(typeof(ForceMode), forceModeId))
			{
				return ForceMode.Impulse;
			}
			return (ForceMode)forceModeId;
		}

		private static CollisionDetectionMode ResolveCollisionDetectionMode(int collisionDetectionModeId)
		{
			if (!Enum.IsDefined(typeof(CollisionDetectionMode), collisionDetectionModeId))
			{
				return CollisionDetectionMode.ContinuousDynamic;
			}
			return (CollisionDetectionMode)collisionDetectionModeId;
		}

		private static RigidbodyInterpolation ResolveRigidbodyInterpolation(int interpolationId)
		{
			if (!Enum.IsDefined(typeof(RigidbodyInterpolation), interpolationId))
			{
				return RigidbodyInterpolation.Interpolate;
			}
			return (RigidbodyInterpolation)interpolationId;
		}

		private static bool IsFinite(Vector3 value)
		{
			if (float.IsFinite(value.x) && float.IsFinite(value.y))
			{
				return float.IsFinite(value.z);
			}
			return false;
		}

		private static bool IsFinite(Quaternion value)
		{
			if (float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z))
			{
				return float.IsFinite(value.w);
			}
			return false;
		}

		public virtual void ServerParentToMe(BaseHoldable child)
		{
			NetworkObjectReference item = child.NetworkObject.ToReference();
			if (!_parentedChilds.Contains(item))
			{
				_parentedChilds.Add(item);
			}
		}

		public virtual void ServerUnParentFromMe(BaseHoldable child)
		{
			NetworkObjectReference item = child.NetworkObject.ToReference();
			if (_parentedChilds.Contains(item))
			{
				_parentedChilds.Remove(item);
			}
		}

		public virtual void OnPlacementStart()
		{
		}

		public virtual void OnPlacementEnd(bool placed)
		{
		}

		public void Place(Vector3 pos, Vector3 rot)
		{
			ServerPlaceHeldRpc(pos, rot, setParent: false, default);
		}

		public void Place(Vector3 pos, Vector3 rot, bool suppressTooltipWhilePlaced)
		{
			ServerPlaceHeldRpc(pos, rot, setParent: false, default, suppressScanWhilePlaced: false, suppressTooltipWhilePlaced);
		}

		public void Place(Vector3 pos, Vector3 rot, bool suppressScanWhilePlaced, bool suppressTooltipWhilePlaced)
		{
			ServerPlaceHeldRpc(pos, rot, setParent: false, default, suppressScanWhilePlaced, suppressTooltipWhilePlaced);
		}

		public void Place(Vector3 pos, Vector3 rot, NetworkObject parentRef)
		{
			ServerPlaceHeldRpc(pos, rot, setParent: true, parentRef.ToReference());
		}

		public void Place(Vector3 pos, Vector3 rot, NetworkObject parentRef, bool suppressScanWhilePlaced)
		{
			ServerPlaceHeldRpc(pos, rot, setParent: true, parentRef.ToReference(), suppressScanWhilePlaced);
		}

		public void Place(Vector3 pos, Vector3 rot, NetworkObject parentRef, bool suppressScanWhilePlaced, bool suppressTooltipWhilePlaced)
		{
			ServerPlaceHeldRpc(pos, rot, setParent: true, parentRef.ToReference(), suppressScanWhilePlaced, suppressTooltipWhilePlaced);
		}

		public void PlaceForget(Vector3 pos, Vector3 rot)
		{
			ServerPlaceHeldForgetRpc(pos, rot);
		}

		[Rpc(SendTo.Server)]
		private void ServerPlaceHeldRpc(Vector3 pos, Vector3 rot, bool setParent, NetworkObjectReference parentRef, bool suppressScanWhilePlaced = false, bool suppressTooltipWhilePlaced = false, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(54460094u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in pos);
				bufferWriter.WriteValueSafe(in rot);
				bufferWriter.WriteValueSafe(in setParent, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in parentRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in suppressScanWhilePlaced, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in suppressTooltipWhilePlaced, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 54460094u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				if (!ServerCanExecuteHeldAction(senderClientId, "Place"))
				{
					ClientForceClearQuickSlotReferenceRpc(senderClientId);
				}
				else if (!setParent)
				{
					ServerSetPlacementTooltipSuppressed(suppressTooltipWhilePlaced);
					ServerPlaceWithScanSuppression(pos, rot, forget: false, suppressScanWhilePlaced);
				}
				else
				{
					ServerSetPlacementTooltipSuppressed(suppressTooltipWhilePlaced);
					ServerSnapPlace(pos, rot, parentRef, suppressScanWhilePlaced);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerPlaceHeldForgetRpc(Vector3 pos, Vector3 rot, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1621421580u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in pos);
				bufferWriter.WriteValueSafe(in rot);
				__endSendRpc(ref bufferWriter, 1621421580u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				if (!ServerCanExecuteHeldAction(senderClientId, "PlaceForget"))
				{
					ClientForceClearQuickSlotReferenceRpc(senderClientId);
					return;
				}
				ServerSetPlacementTooltipSuppressed(suppressed: false);
				ServerPlace(pos, rot, forget: true);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientsPlaceRpc(Vector3 pos, Vector3 rot, ulong holderClientId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2200582493u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in pos);
				bufferWriter.WriteValueSafe(in rot);
				BytePacker.WriteValueBitPacked(bufferWriter, holderClientId);
				__endSendRpc(ref bufferWriter, 2200582493u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientPlace(pos, rot, holderClientId);
			}
		}

		private void ServerPlaceWithScanSuppression(Vector3 pos, Vector3 rot, bool forget, bool suppressScanWhilePlaced)
		{
			_hasPendingPlacementScanSuppression = true;
			_pendingPlacementScanSuppression = suppressScanWhilePlaced;
			try
			{
				ServerPlace(pos, rot, forget);
			}
			finally
			{
				_hasPendingPlacementScanSuppression = false;
				_pendingPlacementScanSuppression = false;
			}
		}

		protected virtual void ServerPlace(Vector3 pos, Vector3 rot, bool forget)
		{
			ServerClearHolderPresentation();
			CanGaze.Value = !forget;
			CanHold.Value = !forget;
			_serverPlayerInventoryOwned = false;
			ServerSetPlacementScanSuppressed(_hasPendingPlacementScanSuppression && _pendingPlacementScanSuppression);
			ServerChangeChildsStatus(status: true, forget);
			ClientsPlaceRpc(pos, rot, base.OwnerClientId);
		}

		protected virtual void ServerSnapPlace(Vector3 pos, Vector3 rot, NetworkObjectReference parentRef, bool suppressScanWhilePlaced = false)
		{
			ServerClearHolderPresentation();
			CanGaze.Value = true;
			CanHold.Value = true;
			_serverPlayerInventoryOwned = false;
			if (!parentRef.TryGet(out var networkObject))
			{
				UtilityMethods.Log("[BaseHoldable-ServerSnapPlace] Parent NetworkObject not found!", LogType.Warning);
				return;
			}
			if (!base.NetworkObject.TrySetParent(networkObject, worldPositionStays: false))
			{
				UtilityMethods.Log("[BaseHoldable-ServerSnapPlace] TrySetParent failed!", LogType.Warning);
				return;
			}
			_parented = true;
			ServerCacheParentedLocalPosition(networkObject, pos);
			if (networkObject.TryGetComponent<BaseHoldable>(out var component))
			{
				component.ServerParentToMe(this);
			}
			ServerSetPlacementScanSuppressed(suppressScanWhilePlaced);
			Singleton<DelayHelper>.Instance.DelayActionUntilEndOfFrame(() =>
			{
				ClientsPlaceRpc(pos, rot, base.OwnerClientId);
			});
		}

		protected virtual void ClientPlace(Vector3 pos, Vector3 rot, ulong holderClientId)
		{
			if (IsLocalClient(holderClientId))
			{
				DisableHeldHints();
				DestroyFpsShadow();
				if (_ownerInteractor != null)
				{
					_ownerInteractor.EndPlacement();
					_ownerInteractor = null;
				}
				if (Singleton<QuickSlotHandler>.Instance != null)
				{
					Singleton<QuickSlotHandler>.Instance.RemoveSpecificItem(this);
				}
			}
			if (base.IsServer && base.OwnerClientId != 0L)
			{
				base.NetworkObject.RemoveOwnership();
			}
			Singleton<SoundManager>.Instance?.PlaySFX(SoundType.Item_Put, _itemSO.HoldableMaterialType, _itemSO.FurnitureWeight, pos);
			base.transform.SetPositionAndRotation(pos, Quaternion.Euler(rot));
			if (ItemSO.PlaceOffset.ScaleOffset != Vector3.zero)
			{
				base.transform.localScale = ItemSO.PlaceOffset.ScaleOffset;
			}
			DestroyTpsShadow();
			CollidersStatus(status: true);
			ObjectsStatus(status: true);
			ClientRefreshRuntimeRepresentationState();
			ClientRefreshParentedChildrenRuntimeRepresentationStates();
		}

		public void SetVisibility(bool visible)
		{
			ClientSetVisibility(visible);
			ServerSetVisibilityRpc(visible);
		}

		[Rpc(SendTo.Server)]
		private void ServerSetVisibilityRpc(bool visible)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2672742937u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in visible, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2672742937u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ServerSetVisibility(visible);
				ClientSetVisibilityRpc(visible);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientSetVisibilityRpc(bool visible)
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
				FastBufferWriter bufferWriter = __beginSendRpc(524362753u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in visible, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 524362753u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientSetVisibility(visible);
			}
		}

		public virtual void ClientSetVisibility(bool visible)
		{
			if (visible && base.IsOwner && Singleton<QuickSlotHandler>.Instance != null)
			{
				BaseHoldable selectedItem = Singleton<QuickSlotHandler>.Instance.SelectedItem;
				if (selectedItem != null && (object)selectedItem != this)
				{
					TpsShadowStatus(status: false);
					if (_fpsShadowObject != null)
					{
						FpsShadowStatus(status: false);
					}
					return;
				}
			}
			TpsShadowStatus(visible);
			if (_fpsShadowObject != null)
			{
				FpsShadowStatus(visible);
			}
		}

		public virtual void ServerSetVisibility(bool visible)
		{
			ServerSetHolderPresentationVisibility(visible);
		}

		public void Destroy()
		{
			ServerDestroyRpc();
		}

		protected virtual void ServerDestroy()
		{
			base.NetworkObject.Despawn(destroy: false);
		}

		protected virtual void ClientDestroy()
		{
			DestroyFpsShadow();
			DestroyTpsShadow();
			DestroyFpsParentedShadow();
			DestroyTpsParentedShadow();
			UnityEngine.Object.Destroy(base.gameObject, 0.5f);
		}

		[Rpc(SendTo.Server)]
		private void ServerDestroyRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(799856908u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 799856908u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ServerDestroy();
			}
		}

		public void Drop(Vector3 pos, Quaternion rot)
		{
			ServerDropRpc(pos, rot);
		}

		protected virtual void ServerDrop(Vector3 pos, Quaternion rot)
		{
			ServerClearHolderPresentation();
			CanGaze.Value = true;
			CanHold.Value = true;
			_serverPlayerInventoryOwned = false;
			ClientDropRpc(pos, rot);
			Singleton<DelayHelper>.Instance.DelayActionUntilEndOfFrame(() =>
			{
				base.NetworkObject.RemoveOwnership();
			});
		}

		[Rpc(SendTo.Server)]
		private void ServerDropRpc(Vector3 pos, Quaternion rot)
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
				FastBufferWriter bufferWriter = __beginSendRpc(700880917u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in pos);
				bufferWriter.WriteValueSafe(in rot);
				__endSendRpc(ref bufferWriter, 700880917u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ServerDrop(pos, rot);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientDropRpc(Vector3 pos, Quaternion rot)
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
				FastBufferWriter bufferWriter = __beginSendRpc(4261386902u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in pos);
				bufferWriter.WriteValueSafe(in rot);
				__endSendRpc(ref bufferWriter, 4261386902u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientDrop(pos, rot);
			}
		}

		protected virtual void ClientDrop(Vector3 pos, Quaternion rot)
		{
			if (base.IsOwner)
			{
				DisableHeldHints();
				_ownerInteractor?.ForceClearHeldActionMode(refreshHints: false);
				Singleton<QuickSlotHandler>.Instance.RemoveSpecificItem(this);
				DestroyFpsShadow();
				_ownerInteractor = null;
			}
			DestroyTpsShadow();
			CollidersStatus(status: true);
			ObjectsStatus(status: true);
			base.transform.SetPositionAndRotation(pos, rot);
		}

		public void Disable(bool clearQuickSlotOnOwner = true)
		{
			ServerDisableRpc(clearQuickSlotOnOwner);
		}

		[Rpc(SendTo.Server)]
		private void ServerDisableRpc(bool clearQuickSlotOnOwner)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2406811177u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in clearQuickSlotOnOwner, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2406811177u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ServerDisable(clearQuickSlotOnOwner);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientDisableRpc(bool clearQuickSlotOnOwner)
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
				FastBufferWriter bufferWriter = __beginSendRpc(818971400u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in clearQuickSlotOnOwner, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 818971400u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientDisable(clearQuickSlotOnOwner);
			}
		}

		protected virtual void ServerDisable(bool clearQuickSlotOnOwner)
		{
			ServerClearHolderPresentation();
			CanGaze.Value = true;
			CanHold.Value = true;
			_serverPlayerInventoryOwned = false;
			ClientDisableRpc(clearQuickSlotOnOwner);
			Singleton<DelayHelper>.Instance.DelayActionUntilEndOfFrame(() =>
			{
				base.NetworkObject.RemoveOwnership();
			});
		}

		protected virtual void ClientDisable(bool clearQuickSlotOnOwner)
		{
			if (base.IsOwner)
			{
				DisableHeldHints();
				_ownerInteractor?.ForceClearHeldActionMode(refreshHints: false);
				if (clearQuickSlotOnOwner)
				{
					Singleton<QuickSlotHandler>.Instance.ClearSlot();
				}
				DestroyFpsShadow();
				_ownerInteractor = null;
			}
			DestroyTpsShadow();
			CollidersStatus(status: false);
			ObjectsStatus(status: false);
		}

		[Rpc(SendTo.Everyone)]
		private void ClientForceClearQuickSlotReferenceRpc(ulong targetClientId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3425054831u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, targetClientId);
				__endSendRpc(ref bufferWriter, 3425054831u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (IsLocalClient(targetClientId))
			{
				ClientOnHeldActionForceCleared();
				DisableHeldHints();
				DestroyFpsShadow();
				if (_ownerInteractor != null)
				{
					_ownerInteractor.ForceClearHeldActionMode(refreshHints: false);
					_ownerInteractor = null;
				}
				if (Singleton<QuickSlotHandler>.Instance != null)
				{
					Singleton<QuickSlotHandler>.Instance.RemoveSpecificItem(this);
				}
				UtilityMethods.Log("[BaseHoldable] Force-cleared stale quick slot reference: " + base.name, LogType.Warning);
			}
		}

		private bool ServerCanExecuteHeldAction(ulong requesterClientId, string actionName)
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (base.NetworkObject == null || !base.NetworkObject.IsSpawned)
			{
				return false;
			}
			if (base.OwnerClientId != requesterClientId)
			{
				UtilityMethods.Log($"[BaseHoldable] {actionName} rejected. Requester is not owner. Object:{base.name}, Requester:{requesterClientId}, Owner:{base.OwnerClientId}", LogType.Warning);
				return false;
			}
			if (CanHold.Value)
			{
				UtilityMethods.Log($"[BaseHoldable] {actionName} rejected. Object is not in held state anymore. Object:{base.name}, Requester:{requesterClientId}", LogType.Warning);
				return false;
			}
			return true;
		}

		private bool IsLocalClient(ulong clientId)
		{
			if (NetworkManager.Singleton != null)
			{
				return NetworkManager.Singleton.LocalClientId == clientId;
			}
			return false;
		}

		private void ServerClearHolderPresentation()
		{
			if (base.IsServer && !(Singleton<PlayersManager>.Instance == null) && Singleton<PlayersManager>.Instance.TryGetPlayer(base.OwnerClientId, out var player) && !(player == null) && !(player.Interactor == null))
			{
				player.Interactor.ServerClearHeldPresentationIfMatches(this);
			}
		}

		private void ServerSetHolderPresentationVisibility(bool visible)
		{
			if (base.IsServer && !(Singleton<PlayersManager>.Instance == null) && Singleton<PlayersManager>.Instance.TryGetPlayer(base.OwnerClientId, out var player) && !(player == null) && !(player.Interactor == null))
			{
				player.Interactor.ServerSetHeldPresentationVisibilityIfMatches(this, visible);
			}
		}

		public virtual void ClientRefreshRuntimeRepresentationState()
		{
		}

		protected void ClientRefreshParentedChildRuntimeRepresentationState(BaseHoldable holdableChild)
		{
			if (holdableChild == null)
			{
				return;
			}
			Transform tpsParentShadowRoot = GetTpsParentShadowRoot();
			if (tpsParentShadowRoot != null)
			{
				holdableChild.SpawnTpsParentedShadow(tpsParentShadowRoot, base.IsOwner);
			}
			if (base.IsOwner)
			{
				Transform fpsParentShadowRoot = GetFpsParentShadowRoot();
				if (fpsParentShadowRoot != null)
				{
					holdableChild.SpawnFpsParentedShadow(fpsParentShadowRoot);
				}
			}
			holdableChild.ClientRefreshRuntimeRepresentationState();
			holdableChild.ClientRefreshParentedChildrenRuntimeRepresentationStates();
		}

		protected void ClientRefreshParentedChildrenRuntimeRepresentationStates()
		{
			foreach (NetworkObjectReference parentedChild in _parentedChilds)
			{
				if (parentedChild.TryGetComponent<BaseHoldable>(out var component, base.NetworkManager))
				{
					component.ClientRefreshRuntimeRepresentationState();
					component.ClientRefreshParentedChildrenRuntimeRepresentationStates();
				}
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerAddToStaffStackRpc(NetworkObjectReference interactorRef, NetworkObjectReference anchorRef, int expectedIndex, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1852444015u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in anchorRef, default(FastBufferWriter.ForNetworkSerializable));
				BytePacker.WriteValueBitPacked(bufferWriter, expectedIndex);
				__endSendRpc(ref bufferWriter, 1852444015u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!base.IsServer || !base.IsSpawned || CanHold.Value || IsStaffCarried || base.OwnerClientId != senderClientId || !interactorRef.TryGet(out var networkObject) || !networkObject.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != senderClientId || component.CurrentHeld != this || !anchorRef.TryGet(out var networkObject2) || !networkObject2.TryGetComponent<BaseHoldable>(out var component2))
			{
				return;
			}
			StaffWorkplaceRegistry instance = StaffWorkplaceRegistry.Instance;
			if (!(instance == null) && instance.ServerTryGetPackageStackPoint(component2, this, expectedIndex, out var point))
			{
				float num = component.InteractRange + 2f;
				if (!((component.transform.position - point.position).sqrMagnitude > num * num) && (!(base.NetworkObject.CurrentParent != null) || base.NetworkObject.TryRemoveParent()))
				{
					ServerSetPlacementTooltipSuppressed(suppressed: false);
					ServerPlaceWithScanSuppression(point.position, point.eulerAngles, forget: false, suppressScanWhilePlaced: false);
					component.ServerClearHeldPresentationIfMatches(this);
					instance.RefreshStorageStacks();
				}
			}
		}

		public void ServerSetStaffStack(int index)
		{
			if (base.IsServer && base.IsSpawned)
			{
				_staffStackPoint.Value = index;
				_staffStackReserved.Value = index >= 0 && (StaffManager.Instance?.IsStackItemReserved(base.NetworkObjectId) ?? false);
			}
		}

		private void OnStaffStackChanged(int previous, int current)
		{
			if (current < 0)
			{
				ClientSetStackHidden(hidden: false);
			}
		}

		public void ClientSetStackHidden(bool hidden)
		{
			if (hidden || stackHidden)
			{
				stackHidden = hidden;
				Renderer[] componentsInChildren = GetComponentsInChildren<Renderer>(includeInactive: true);
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					componentsInChildren[i].forceRenderingOff = hidden || IsStaffCarried;
				}
				CollidersStatus(!hidden && CanHold.Value && !IsStaffCarried);
			}
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStackItems()
		{
			StackWorldItems.Clear();
		}

		protected override void __initializeVariables()
		{
			if (CanHold == null)
			{
				throw new Exception("BaseHoldable.CanHold cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			CanHold.Initialize(this);
			__nameNetworkVariable(CanHold, "CanHold");
			NetworkVariableFields.Add(CanHold);
			if (_parentedChilds == null)
			{
				throw new Exception("BaseHoldable._parentedChilds cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_parentedChilds.Initialize(this);
			__nameNetworkVariable(_parentedChilds, "_parentedChilds");
			NetworkVariableFields.Add(_parentedChilds);
			if (_parentedLocalPos == null)
			{
				throw new Exception("BaseHoldable._parentedLocalPos cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_parentedLocalPos.Initialize(this);
			__nameNetworkVariable(_parentedLocalPos, "_parentedLocalPos");
			NetworkVariableFields.Add(_parentedLocalPos);
			if (_tooltipSuppressedByPlacement == null)
			{
				throw new Exception("BaseHoldable._tooltipSuppressedByPlacement cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_tooltipSuppressedByPlacement.Initialize(this);
			__nameNetworkVariable(_tooltipSuppressedByPlacement, "_tooltipSuppressedByPlacement");
			NetworkVariableFields.Add(_tooltipSuppressedByPlacement);
			if (_staffCarrier == null)
			{
				throw new Exception("BaseHoldable._staffCarrier cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_staffCarrier.Initialize(this);
			__nameNetworkVariable(_staffCarrier, "_staffCarrier");
			NetworkVariableFields.Add(_staffCarrier);
			if (_staffStackPoint == null)
			{
				throw new Exception("BaseHoldable._staffStackPoint cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_staffStackPoint.Initialize(this);
			__nameNetworkVariable(_staffStackPoint, "_staffStackPoint");
			NetworkVariableFields.Add(_staffStackPoint);
			if (_staffStackReserved == null)
			{
				throw new Exception("BaseHoldable._staffStackReserved cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_staffStackReserved.Initialize(this);
			__nameNetworkVariable(_staffStackReserved, "_staffStackReserved");
			NetworkVariableFields.Add(_staffStackReserved);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(1983165441u, __rpc_handler_1983165441, "ClientStaffPlaceRpc", RpcInvokePermission.Everyone);
			__registerRpc(2881547370u, __rpc_handler_2881547370, "ClientLazyHoldRpc", RpcInvokePermission.Everyone);
			__registerRpc(1091750692u, __rpc_handler_1091750692, "ClientParentedChildStatusRpc", RpcInvokePermission.Everyone);
			__registerRpc(4058289452u, __rpc_handler_4058289452, "ServerFreeFallRpc", RpcInvokePermission.Everyone);
			__registerRpc(3858765115u, __rpc_handler_3858765115, "ServerThrowHeldRpc", RpcInvokePermission.Everyone);
			__registerRpc(2779884290u, __rpc_handler_2779884290, "ClientsStartPersistentPhysicsThrowRpc", RpcInvokePermission.Everyone);
			__registerRpc(1214121656u, __rpc_handler_1214121656, "ClientsStartPhysicsThrowRpc", RpcInvokePermission.Everyone);
			__registerRpc(1462937754u, __rpc_handler_1462937754, "ClientThrowRejectedRpc", RpcInvokePermission.Everyone);
			__registerRpc(836720658u, __rpc_handler_836720658, "ClientsCancelThrowFlightRpc", RpcInvokePermission.Everyone);
			__registerRpc(1854315509u, __rpc_handler_1854315509, "ServerThrowSettledRpc", RpcInvokePermission.Everyone);
			__registerRpc(54460094u, __rpc_handler_54460094, "ServerPlaceHeldRpc", RpcInvokePermission.Everyone);
			__registerRpc(1621421580u, __rpc_handler_1621421580, "ServerPlaceHeldForgetRpc", RpcInvokePermission.Everyone);
			__registerRpc(2200582493u, __rpc_handler_2200582493, "ClientsPlaceRpc", RpcInvokePermission.Everyone);
			__registerRpc(2672742937u, __rpc_handler_2672742937, "ServerSetVisibilityRpc", RpcInvokePermission.Everyone);
			__registerRpc(524362753u, __rpc_handler_524362753, "ClientSetVisibilityRpc", RpcInvokePermission.Everyone);
			__registerRpc(799856908u, __rpc_handler_799856908, "ServerDestroyRpc", RpcInvokePermission.Everyone);
			__registerRpc(700880917u, __rpc_handler_700880917, "ServerDropRpc", RpcInvokePermission.Everyone);
			__registerRpc(4261386902u, __rpc_handler_4261386902, "ClientDropRpc", RpcInvokePermission.Everyone);
			__registerRpc(2406811177u, __rpc_handler_2406811177, "ServerDisableRpc", RpcInvokePermission.Everyone);
			__registerRpc(818971400u, __rpc_handler_818971400, "ClientDisableRpc", RpcInvokePermission.Everyone);
			__registerRpc(3425054831u, __rpc_handler_3425054831, "ClientForceClearQuickSlotReferenceRpc", RpcInvokePermission.Everyone);
			__registerRpc(1852444015u, __rpc_handler_1852444015, "ServerAddToStaffStackRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_1983165441(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Quaternion value2);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ClientStaffPlaceRpc(value, value2);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2881547370(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out Vector3 value4);
				reader.ReadValueSafe(out bool value5, default(FastBufferWriter.ForPrimitives));
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ClientLazyHoldRpc(value, value2, value3, value4, value5);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1091750692(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ClientParentedChildStatusRpc(value);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4058289452(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out float value3, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value4, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out Vector3 value5);
				ByteUnpacker.ReadValueBitPacked(reader, out int value6);
				reader.ReadValueSafe(out float value7, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value8, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value9, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out int value10);
				ByteUnpacker.ReadValueBitPacked(reader, out int value11);
				ByteUnpacker.ReadValueBitPacked(reader, out int value12);
				reader.ReadValueSafe(out float value13, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value14, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value15, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value16, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value17, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ServerFreeFallRpc(value, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15, value16, value17, ext);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3858765115(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out float value3, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value4, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out Vector3 value5);
				ByteUnpacker.ReadValueBitPacked(reader, out int value6);
				reader.ReadValueSafe(out float value7, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value8, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value9, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value10, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out int value11);
				ByteUnpacker.ReadValueBitPacked(reader, out int value12);
				ByteUnpacker.ReadValueBitPacked(reader, out int value13);
				reader.ReadValueSafe(out float value14, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value15, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value16, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value17, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value18, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ServerThrowHeldRpc(value, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15, value16, value17, value18, ext);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2779884290(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Vector3 value2);
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ClientsStartPersistentPhysicsThrowRpc(value, value2, value3);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1214121656(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out float value3, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value4, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out Vector3 value5);
				ByteUnpacker.ReadValueBitPacked(reader, out int value6);
				reader.ReadValueSafe(out float value7, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value8, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value9, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value10, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out int value11);
				ByteUnpacker.ReadValueBitPacked(reader, out int value12);
				ByteUnpacker.ReadValueBitPacked(reader, out int value13);
				reader.ReadValueSafe(out float value14, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value15, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value16, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value17, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value18);
				reader.ReadValueSafe(out bool value19, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out int value20);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ClientsStartPhysicsThrowRpc(value, value2, value3, value4, value5, value6, value7, value8, value9, value10, value11, value12, value13, value14, value15, value16, value17, value18, value19, value20);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1462937754(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ClientThrowRejectedRpc(value);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_836720658(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ClientsCancelThrowFlightRpc(value);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1854315509(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Quaternion value2);
				ByteUnpacker.ReadValueBitPacked(reader, out int value3);
				RpcParams ext = rpcParams.Ext;
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ServerThrowSettledRpc(value, value2, value3, ext);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_54460094(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out NetworkObjectReference value4, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out bool value5, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value6, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ServerPlaceHeldRpc(value, value2, value3, value4, value5, value6, ext);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1621421580(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Vector3 value2);
				RpcParams ext = rpcParams.Ext;
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ServerPlaceHeldForgetRpc(value, value2, ext);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2200582493(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Vector3 value2);
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ClientsPlaceRpc(value, value2, value3);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2672742937(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ServerSetVisibilityRpc(value);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_524362753(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ClientSetVisibilityRpc(value);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_799856908(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ServerDestroyRpc();
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_700880917(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Quaternion value2);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ServerDropRpc(value, value2);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4261386902(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Quaternion value2);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ClientDropRpc(value, value2);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2406811177(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ServerDisableRpc(value);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_818971400(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ClientDisableRpc(value);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3425054831(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ClientForceClearQuickSlotReferenceRpc(value);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1852444015(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				ByteUnpacker.ReadValueBitPacked(reader, out int value3);
				RpcParams ext = rpcParams.Ext;
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BaseHoldable)target).ServerAddToStaffStackRpc(value, value2, value3, ext);
				((BaseHoldable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "BaseHoldable";
		}
	}
}
