using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Core.ProcessedIngredient;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using PaperPixelGames.Core.Kitchen;
using PaperPixelGames.Core.Kitchen.ChefDesk;
using PaperPixelGames.Core.Kitchen.RiceCookerScript;
using PaperPixelGames.Core.Kitchen.Wholasale;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.PlayerTools;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Core.ScrapSystem;
using PaperPixelGames.Core.ScrapeSystem;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Indicator;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Interaction
{
	public class Interactor : NetworkBehaviour
	{
		[SerializeField]
		private PPG_Player _player;

		[SerializeField]
		private Thrower _thrower;

		[SerializeField]
		private Placer _placer;

		[SerializeField]
		private Camera _camera;

		[SerializeField]
		private float _interactRange = 3f;

		[SerializeField]
		private LayerMask _interactableMask;

		[SerializeField]
		private LayerMask _quickSnapPlacementMask;

		private IBasicInteractable _currentBasicInteractable;

		private BaseInteractable _currentGazed;

		private BaseHoldable _currentHeld;

		private BaseUsable _currentInUse;

		private BaseHoldable _presentedHeldItem;

		private bool _holdRequestPending;

		private NetworkObjectReference _pendingHoldReference;

		private bool _useRequestPending;

		private uint _useRequestSequence;

		private readonly NetworkVariable<bool> _isUsingNetwork = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<NetworkObjectReference> _heldItemReference = new NetworkVariable<NetworkObjectReference>();

		private readonly NetworkVariable<bool> _heldItemVisible = new NetworkVariable<bool>(value: false);

		private const int GazeHitBufferSize = 16;

		private readonly RaycastHit[] _gazeHits = new RaycastHit[16];

		private const float EmptyAnimationInputDelay = 0.1f;

		private Coroutine _emptyLeftCor;

		private Coroutine _emptyRightCor;

		private bool _isEmptyLeftPressActive;

		private bool _isEmptyRightPressActive;

		private bool _suppressMouse1UntilCanceled;

		private bool _isEmptyLeftActive;

		private bool _isEmptyRightActive;

		[Header("Ping")]
		[SerializeField]
		private GameObject _pingObject;

		[SerializeField]
		private AudioSource _sfx;

		[SerializeField]
		private float _pingLifetime = 4f;

		[SerializeField]
		private float _pingMaxDistance = 25f;

		[SerializeField]
		private LayerMask _pingSurfaceMask = -1;

		[SerializeField]
		private float _pingSurfaceOffset = 0.01f;

		[SerializeField]
		private bool _destroyDetachedPingOnDestroy = true;

		[Header("Ping - Server Validation")]
		[SerializeField]
		private bool _serverValidatePingDistance = true;

		[SerializeField]
		private float _serverMaxPingDistanceFromPlayer = 35f;

		[Header("Ping - Knife Placement")]
		[SerializeField]
		private Vector3 _pingLocalTipOffset = Vector3.zero;

		[Header("Ping - Distance Scale")]
		[SerializeField]
		private bool _scalePingByDistance = true;

		[SerializeField]
		private float _pingScaleMinDistance = 2f;

		[SerializeField]
		private float _pingScaleMaxDistance = 25f;

		[SerializeField]
		private float _pingScaleAtMinDistance = 0.75f;

		[SerializeField]
		private float _pingScaleAtMaxDistance = 1.35f;

		[SerializeField]
		private AnimationCurve _pingDistanceScaleCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

		[Header("Ping - Knife Animation (DOTween)")]
		[SerializeField]
		private float _pingStabBackDistance = 0.25f;

		[SerializeField]
		private float _pingStabDuration = 0.12f;

		[SerializeField]
		private float _pingRotRandomDegrees = 6f;

		[SerializeField]
		private float _pingPunchDuration = 0.18f;

		[SerializeField]
		private float _pingPunchDegrees = 10f;

		[SerializeField]
		private Vector3 _pingPunchLocalAxis = Vector3.right;

		[SerializeField]
		private float _pingSwayDegrees = 3.5f;

		[SerializeField]
		private float _pingSwayPeriod = 0.9f;

		[SerializeField]
		private Vector3 _pingSwayLocalAxis = Vector3.forward;

		[SerializeField]
		private float _pingCooldown = 0.35f;

		private float _lastPingTime;

		private float _serverNextAllowedPingTime;

		private Coroutine _pingHideRoutine;

		private Sequence _pingSeq;

		private Tweener _pingSwayTween;

		private Renderer[] _pingRenderers;

		private Vector3 _pingBaseScale = Vector3.one;

		[Header("Scan (Hold)")]
		[SerializeField]
		[Min(0.05f)]
		private float _scanHoldDuration = 0.75f;

		[SerializeField]
		[Min(0f)]
		private float _scanCooldown = 1.25f;

		[SerializeField]
		[Min(1f)]
		private int _scanMaxTargets = 10;

		[Header("Scan (Cast)")]
		[SerializeField]
		private Transform _scanOrigin;

		[SerializeField]
		[Min(0.05f)]
		private float _scanSphereRadius = 0.45f;

		[SerializeField]
		[Min(0.1f)]
		private float _scanDistance = 6f;

		[SerializeField]
		private LayerMask _scanMask = -1;

		[Header("Scan (Indicators)")]
		[SerializeField]
		[Min(0.25f)]
		private float _scanIndicatorLifetime = 6f;

		[SerializeField]
		private Vector3 _scanPlayerIndicatorWorldOffset = new Vector3(0f, 1.75f, 0f);

		[Header("Scan (UI)")]
		[SerializeField]
		private CanvasGroup _scanHoldCanvasGroup;

		[SerializeField]
		private Slider _scanHoldSlider;

		[SerializeField]
		[Min(0f)]
		private float _scanHoldShowFade = 0.08f;

		[SerializeField]
		[Min(0f)]
		private float _scanHoldHideFade = 0.08f;

		[SerializeField]
		[Min(8f)]
		private int _scanHitBufferSize = 32;

		private RaycastHit[] _scanHits;

		private Coroutine _scanHoldRoutine;

		private Coroutine _scanUiFadeRoutine;

		private float _nextScanTime;

		private readonly List<BaseInteractable> _scanTargetsBuffer = new List<BaseInteractable>(10);

		private readonly HashSet<BaseInteractable> _scanTargetsUnique = new HashSet<BaseInteractable>();

		private readonly List<OffscreenIndicatorTarget> _scanIndicatorTargetsBuffer = new List<OffscreenIndicatorTarget>(10);

		private readonly HashSet<OffscreenIndicatorTarget> _scanIndicatorTargetsUnique = new HashSet<OffscreenIndicatorTarget>();

		private readonly Dictionary<BaseInteractable, float> _scanInteractableMinDistSqr = new Dictionary<BaseInteractable, float>();

		private readonly Dictionary<OffscreenIndicatorTarget, float> _scanIndicatorMinDistSqr = new Dictionary<OffscreenIndicatorTarget, float>();

		private readonly List<BaseInteractable> _scanInteractableSorted = new List<BaseInteractable>(16);

		private readonly List<OffscreenIndicatorTarget> _scanIndicatorSorted = new List<OffscreenIndicatorTarget>(16);

		private readonly HashSet<OffscreenIndicatorTarget> _selectedIndicatorsFromInteractables = new HashSet<OffscreenIndicatorTarget>();

		public InteractorState InteractorState { get; set; }

		public bool InteractorStatus { get; private set; }

		public bool OnThrow { get; private set; }

		public bool OnPlacement { get; private set; }

		public bool IsHolding => _currentHeld != null;

		public bool IsUsing
		{
			get
			{
				if (!(_currentInUse != null))
				{
					return _isUsingNetwork.Value;
				}
				return true;
			}
		}

		public bool IsUseRequestPending => _useRequestPending;

		public PPG_Player Player => _player;

		public Placer Placer => _placer;

		public BaseInteractable CurrentGazed => _currentGazed;

		public BaseHoldable CurrentHeld => _currentHeld;

		public BaseUsable CurrentInUse => _currentInUse;

		public float InteractRange => _interactRange;

		public bool InHeldActionMode => false;

		private OffscreenIndicatorHandler _indicatorHandler => Singleton<OffscreenIndicatorHandler>.Instance;

		private void Awake()
		{
			SetInteractorStatus(status: true);
			PingSetup();
			ScanInit();
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<NetworkObjectReference> heldItemReference = _heldItemReference;
			heldItemReference.OnValueChanged = (NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate)Delegate.Combine(heldItemReference.OnValueChanged, new NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate(OnHeldItemReferenceChanged));
			NetworkVariable<bool> heldItemVisible = _heldItemVisible;
			heldItemVisible.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(heldItemVisible.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnHeldItemVisibilityChanged));
			ApplyHeldItemReference(_heldItemReference.Value);
		}

		public override void OnNetworkDespawn()
		{
			ServerReleaseCurrentUseOnDespawn();
			NetworkVariable<NetworkObjectReference> heldItemReference = _heldItemReference;
			heldItemReference.OnValueChanged = (NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate)Delegate.Remove(heldItemReference.OnValueChanged, new NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate(OnHeldItemReferenceChanged));
			NetworkVariable<bool> heldItemVisible = _heldItemVisible;
			heldItemVisible.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(heldItemVisible.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnHeldItemVisibilityChanged));
			ClearPresentedHeldItem();
			_currentHeld = null;
			ClearPendingHoldRequest();
			_useRequestPending = false;
			base.OnNetworkDespawn();
		}

		public bool TryBeginUseRequest(out uint requestSequence)
		{
			requestSequence = 0u;
			if (!base.IsOwner || IsUsing || _useRequestPending)
			{
				return false;
			}
			_useRequestSequence++;
			if (_useRequestSequence == 0)
			{
				_useRequestSequence = 1u;
			}
			_useRequestPending = true;
			requestSequence = _useRequestSequence;
			return true;
		}

		public void CancelPendingUseRequest()
		{
			if (_useRequestPending)
			{
				_useRequestPending = false;
				_useRequestSequence++;
			}
		}

		private void Update()
		{
			if (base.IsOwner && InteractorStatus)
			{
				TryGaze();
				RefreshQuickSnapPlacementPreview();
				CheckHit();
			}
		}

		public void SetInteractorStatus(bool status, bool exitCurrent = false)
		{
			InteractorStatus = status;
			if (!status & exitCurrent)
			{
				GazeExitCurrent();
			}
		}

		public void GazeExitCurrent()
		{
			if (!(_currentGazed == null))
			{
				_currentGazed.GazeExit();
				_currentGazed = null;
			}
		}

		public void SetInteractRange(int range)
		{
			_interactRange = range;
		}

		public void RefreshCurrentHints()
		{
			if (!base.IsOwner || (_placer != null && _placer.InPlaceMode) || Singleton<HintsManager>.Instance == null)
			{
				return;
			}
			Singleton<HintsManager>.Instance.ClearAllHints();
			_player.ActiveTool?.RefreshCloseToolHint();
			if (_currentInUse != null)
			{
				_currentInUse.EnableUseHints();
				return;
			}
			BaseHoldable baseHoldable = ((Singleton<QuickSlotHandler>.Instance != null) ? Singleton<QuickSlotHandler>.Instance.SelectedItem : null);
			if (baseHoldable != null && (_player == null || _player.ActiveTool == null))
			{
				baseHoldable.EnableHeldHints();
			}
			if (_currentGazed != null && _currentGazed.CanGaze.Value)
			{
				_currentGazed.EnableGazeHints();
			}
			else
			{
				_currentBasicInteractable?.GazeEnter();
			}
		}

		public void RefreshCurrentGazeFor(Transform targetRoot)
		{
			if (base.IsOwner && !(targetRoot == null) && IsCurrentGazeInHierarchy(targetRoot))
			{
				RefreshCurrentGaze();
			}
		}

		private void RefreshCurrentGaze()
		{
			if (base.IsOwner)
			{
				_currentGazed?.GazeExit();
				_currentGazed = null;
				_currentBasicInteractable?.GazeExit();
				_currentBasicInteractable = null;
				TryGaze();
			}
		}

		private bool IsCurrentGazeInHierarchy(Transform targetRoot)
		{
			if (_currentGazed != null)
			{
				return SharesHierarchy(_currentGazed.transform, targetRoot);
			}
			Component component = _currentBasicInteractable as Component;
			if (component != null)
			{
				return SharesHierarchy(component.transform, targetRoot);
			}
			return false;
		}

		private static bool SharesHierarchy(Transform first, Transform second)
		{
			if (!(first == second) && !first.IsChildOf(second))
			{
				return second.IsChildOf(first);
			}
			return true;
		}

		public void TryGaze()
		{
			if (!_player.DivingEquipment.IsKnocked && !OnPlacement && !OnThrow)
			{
				FindGazeTargets(out var closestInteractable, out var closestBasicInteractable);
				ApplyGazeTargets(closestInteractable, closestBasicInteractable);
			}
		}

		private void FindGazeTargets(out BaseInteractable closestInteractable, out IBasicInteractable closestBasicInteractable)
		{
			closestInteractable = null;
			closestBasicInteractable = null;
			if (_camera == null)
			{
				return;
			}
			int num = Physics.RaycastNonAlloc(_camera.transform.position, _camera.transform.forward, _gazeHits, _interactRange, _interactableMask, QueryTriggerInteraction.Collide);
			float num2 = float.MaxValue;
			for (int i = 0; i < num; i++)
			{
				RaycastHit raycastHit = _gazeHits[i];
				BaseInteractable baseInteractable = raycastHit.collider.GetComponent<BaseInteractable>();
				if (baseInteractable == null)
				{
					baseInteractable = raycastHit.collider.GetComponentInParent<BaseInteractable>();
					if (IsPlacementOnlyHit(raycastHit.collider, baseInteractable))
					{
						continue;
					}
				}
				if (baseInteractable is LatchPiranha)
				{
					PPG_Player player = _player;
					if ((object)player == null || !player.IsUnderWater)
					{
						continue;
					}
				}
				if (baseInteractable != null && baseInteractable.CanGaze.Value)
				{
					if (raycastHit.distance < num2)
					{
						closestInteractable = baseInteractable;
						closestBasicInteractable = null;
						num2 = raycastHit.distance;
					}
					continue;
				}
				IBasicInteractable basicInteractable = raycastHit.collider.GetComponent(typeof(IBasicInteractable)) as IBasicInteractable;
				if (basicInteractable == null)
				{
					basicInteractable = raycastHit.collider.GetComponentInParent(typeof(IBasicInteractable)) as IBasicInteractable;
				}
				if (basicInteractable != null && basicInteractable.CanGaze && raycastHit.distance < num2)
				{
					closestInteractable = null;
					closestBasicInteractable = basicInteractable;
					num2 = raycastHit.distance;
				}
			}
		}

		public bool TryGetStorageStackTarget(out StaffStorageStack stack)
		{
			stack = null;
			if (!StaffStackPackage.IsPackage(_currentHeld))
			{
				return false;
			}
			if (OnPlacement)
			{
				FindGazeTargets(out var _, out var closestBasicInteractable);
				stack = closestBasicInteractable as StaffStorageStack;
			}
			else
			{
				stack = _currentBasicInteractable as StaffStorageStack;
			}
			if (stack != null)
			{
				return stack.CanGaze;
			}
			return false;
		}

		private void ApplyGazeTargets(BaseInteractable closestInteractable, IBasicInteractable closestBasicInteractable)
		{
			if (closestInteractable != null)
			{
				if (_currentBasicInteractable != null)
				{
					_currentBasicInteractable.GazeExit();
					_currentBasicInteractable = null;
				}
				if (closestInteractable != _currentGazed)
				{
					if (_currentGazed != null)
					{
						_currentGazed.GazeExit();
					}
					_currentGazed = closestInteractable;
					_currentGazed.GazeEnter();
				}
				return;
			}
			if (closestBasicInteractable != null)
			{
				if (_currentGazed != null)
				{
					_currentGazed.GazeExit();
					_currentGazed = null;
				}
				if (_currentBasicInteractable != closestBasicInteractable)
				{
					_currentBasicInteractable?.GazeExit();
					_currentBasicInteractable = closestBasicInteractable;
					_currentBasicInteractable.GazeEnter();
				}
				return;
			}
			bool flag = false;
			if (_currentGazed != null)
			{
				_currentGazed.GazeExit();
				_currentGazed = null;
				flag = true;
			}
			if (_currentBasicInteractable != null)
			{
				_currentBasicInteractable.GazeExit();
				_currentBasicInteractable = null;
				flag = true;
			}
			if (flag)
			{
				Singleton<QuickSlotHandler>.Instance?.RefreshHeldHints();
			}
		}

		private static bool IsPlacementOnlyHit(Collider collider, BaseInteractable parentTarget)
		{
			QuickSnapPlacementZone componentInParent = collider.GetComponentInParent<QuickSnapPlacementZone>();
			if (componentInParent != null)
			{
				if (!(parentTarget == null))
				{
					return componentInParent.transform.IsChildOf(parentTarget.transform);
				}
				return true;
			}
			FixedSnapPlacementZone componentInParent2 = collider.GetComponentInParent<FixedSnapPlacementZone>();
			if (componentInParent2 != null)
			{
				if (!(parentTarget == null))
				{
					return componentInParent2.transform.IsChildOf(parentTarget.transform);
				}
				return true;
			}
			return false;
		}

		public void CheckHit()
		{
			_player.Harpoon.HitRaycast(_camera);
		}

		public bool TryHold(BaseHoldable holdable)
		{
			if (Player != null && Player.HasSurfboard)
			{
				return false;
			}
			if (holdable == null)
			{
				return false;
			}
			if (holdable.NetworkObject == null || !holdable.NetworkObject.IsSpawned)
			{
				UtilityMethods.Log("[Interactor_Hold] Can't hold an unspawned NetworkObject. Item:" + holdable.name, LogType.Warning);
				return false;
			}
			if (_holdRequestPending && ResolveHeldItem(_pendingHoldReference) == null)
			{
				ClearPendingHoldRequest();
			}
			if (_holdRequestPending)
			{
				UtilityMethods.Log("[Interactor_Hold] Hold request ignored while another pickup is pending. Item:" + holdable.name, LogType.Warning);
				return false;
			}
			if (Player.IsToolActive && (!holdable.RequiresHeldSlot || !Player.DisableActiveToolIf()))
			{
				UtilityMethods.Log("[Interactor_Hold] Tool is Active!");
				return false;
			}
			if (!holdable.CanHold.Value)
			{
				UtilityMethods.Log("[Interactor_Hold] Can't hold this holdable!");
				return false;
			}
			if (!Singleton<QuickSlotHandler>.Instance.CanHold(holdable))
			{
				UtilityMethods.Log("[Interactor_Hold] Slots are not available!");
				Singleton<NotifyManager>.Instance.ShowToast("Slots are full!", NotificationState.Error);
				return false;
			}
			_suppressMouse1UntilCanceled = true;
			_pendingHoldReference = holdable.NetworkObject.ToReference();
			_holdRequestPending = true;
			ServerHoldRpc(_pendingHoldReference);
			UtilityMethods.Log("[Interactor_Hold] TryHold success!");
			return true;
		}

		[Rpc(SendTo.Server)]
		public void ServerHoldRpc(NetworkObjectReference reference)
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
				FastBufferWriter bufferWriter = __beginSendRpc(864342254u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in reference, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 864342254u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!ServerTryHoldItem(reference))
				{
					ClientHoldFailRpc(reference);
				}
			}
		}

		public bool ServerTryHoldItem(NetworkObjectReference reference)
		{
			if (Player != null && Player.HasSurfboard)
			{
				return false;
			}
			if (!base.IsServer || !reference.TryGetComponent<BaseHoldable>(out var component) || !component.CanHold.Value)
			{
				return false;
			}
			StaffWorkplaceRegistry instance = StaffWorkplaceRegistry.Instance;
			int staffStackPoint = component.StaffStackPoint;
			if (staffStackPoint >= 0)
			{
				if (!((base.transform.position - component.transform.position).sqrMagnitude > (_interactRange + 2f) * (_interactRange + 2f)))
				{
					StaffManager instance2 = StaffManager.Instance;
					if ((object)instance2 == null || !instance2.IsStackItemReserved(component.NetworkObjectId))
					{
						goto IL_00aa;
					}
				}
				return false;
			}
			goto IL_00aa;
			IL_00aa:
			component.ServerHold(this);
			if (component.OwnerClientId != base.OwnerClientId || component.CanHold.Value)
			{
				return false;
			}
			if (staffStackPoint >= 0)
			{
				instance?.FillStackPackage(component, 0, staffStackPoint);
			}
			ServerSetHeldItem(component);
			return true;
		}

		public bool ServerPrepareToHoldAnotherItem(ulong requesterClientId, string context)
		{
			if (!base.IsServer || base.OwnerClientId != requesterClientId)
			{
				return false;
			}
			if (_currentHeld == null)
			{
				return true;
			}
			if (_currentHeld.OwnerClientId != requesterClientId || _currentHeld.CanHold.Value)
			{
				UtilityMethods.Log("[Interactor_Hold] " + context + " rejected. Current held item cannot be stored. Item:" + _currentHeld.name, LogType.Warning);
				return false;
			}
			ServerSetHeldItem(null);
			return true;
		}

		[Rpc(SendTo.Owner)]
		private void ClientHoldFailRpc(NetworkObjectReference holdableRef)
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
				FastBufferWriter bufferWriter = __beginSendRpc(140169587u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in holdableRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 140169587u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClearPendingHoldRequest();
				if (!holdableRef.TryGetComponent<BaseHoldable>(out var component))
				{
					UtilityMethods.Log("Hold fail on ClientSuccessRpc!");
					return;
				}
				component.ClientHoldFailed();
				UtilityMethods.Log("[Interactor_Hold] fail on Server!");
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerSyncHoldableRpc(NetworkObjectReference holdableRef)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2939824725u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in holdableRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 2939824725u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!holdableRef.TryGetComponent<BaseHoldable>(out var component))
				{
					ServerSetHeldItem(null);
				}
				else if (component.OwnerClientId != base.OwnerClientId || component.CanHold.Value)
				{
					UtilityMethods.Log($"[Interactor_Hold] Held item sync rejected. InteractorOwner:{base.OwnerClientId}, Item:{component.name}, ItemOwner:{component.OwnerClientId}, CanHold:{component.CanHold.Value}", LogType.Warning);
				}
				else
				{
					ServerSetHeldItem(component);
				}
			}
		}

		public void ServerClearHeldPresentationIfMatches(BaseHoldable holdable)
		{
			if (base.IsServer && !(holdable == null) && (object)_currentHeld == holdable)
			{
				ServerSetHeldItem(null);
			}
		}

		public void ClientReconcileHeldPresentation()
		{
			BaseHoldable baseHoldable = (_heldItemVisible.Value ? ResolveHeldItem(_heldItemReference.Value) : null);
			if (_presentedHeldItem != null && (object)_presentedHeldItem != baseHoldable)
			{
				_presentedHeldItem.ClientClearHeldVisuals();
			}
			_presentedHeldItem = baseHoldable;
			if (_presentedHeldItem != null)
			{
				_presentedHeldItem.ClientEnsureHeldVisuals(this);
				if (base.IsOwner && _holdRequestPending && _pendingHoldReference.Equals(_heldItemReference.Value))
				{
					ClearPendingHoldRequest();
				}
			}
		}

		private void ServerSetHeldItem(BaseHoldable holdable)
		{
			if (base.IsServer)
			{
				_currentHeld = holdable;
				NetworkObjectReference networkObjectReference = ((holdable != null) ? holdable.NetworkObject.ToReference() : default(NetworkObjectReference));
				if (!_heldItemReference.Value.Equals(networkObjectReference))
				{
					_heldItemReference.Value = networkObjectReference;
				}
				bool flag = holdable != null;
				if (_heldItemVisible.Value != flag)
				{
					_heldItemVisible.Value = flag;
				}
				else
				{
					ClientReconcileHeldPresentation();
				}
			}
		}

		public void ServerSetHeldPresentationVisibilityIfMatches(BaseHoldable holdable, bool visible)
		{
			if (base.IsServer && !(holdable == null) && (object)_currentHeld == holdable && _heldItemVisible.Value != visible)
			{
				_heldItemVisible.Value = visible;
			}
		}

		private void OnHeldItemReferenceChanged(NetworkObjectReference previousReference, NetworkObjectReference currentReference)
		{
			ApplyHeldItemReference(currentReference);
		}

		private void OnHeldItemVisibilityChanged(bool previousVisibility, bool currentVisibility)
		{
			ClientReconcileHeldPresentation();
		}

		private void ApplyHeldItemReference(NetworkObjectReference heldItemReference)
		{
			_currentHeld = ResolveHeldItem(heldItemReference);
			ClientReconcileHeldPresentation();
		}

		private void ClearPendingHoldRequest()
		{
			_holdRequestPending = false;
			_pendingHoldReference = default;
		}

		private static BaseHoldable ResolveHeldItem(NetworkObjectReference heldItemReference)
		{
			if (!heldItemReference.TryGetComponent<BaseHoldable>(out var component))
			{
				return null;
			}
			return component;
		}

		private void ClearPresentedHeldItem()
		{
			if (_presentedHeldItem != null)
			{
				_presentedHeldItem.ClientClearHeldVisuals();
			}
			_presentedHeldItem = null;
		}

		[Rpc(SendTo.Owner)]
		public void ClientDestroyHeldItemRpc(NetworkObjectReference holdableRef)
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
				FastBufferWriter bufferWriter = __beginSendRpc(666019032u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in holdableRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 666019032u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!holdableRef.TryGetComponent<BaseHoldable>(out var component) || Singleton<QuickSlotHandler>.Instance == null || !Singleton<QuickSlotHandler>.Instance.RemoveSpecificItem(component))
				{
					UtilityMethods.Log("[Interactor_Hold] ClientDestroyHeldItemRpc could not resolve the expected inventory item.", LogType.Warning);
				}
				else
				{
					component.Destroy();
				}
			}
		}

		public void RegisterInput()
		{
			if (base.IsOwner)
			{
				Singleton<InputManager>.Instance.InputHandler.Interact += OnInteract;
				Singleton<InputManager>.Instance.InputHandler.Mouse1 += OnMouse1;
				Singleton<InputManager>.Instance.InputHandler.RapidPress += OnRapidPress;
				Singleton<InputManager>.Instance.InputHandler.Mouse2 += OnMouse2;
				Singleton<InputManager>.Instance.InputHandler.Ping += OnPing;
				Singleton<InputManager>.Instance.InputHandler.Scan += OnScan;
				Singleton<InputManager>.Instance.InputHandler.Throw += OnThrowInput;
			}
		}

		public void UnRegisterInput()
		{
			if (base.IsOwner)
			{
				Singleton<InputManager>.Instance.InputHandler.Interact -= OnInteract;
				Singleton<InputManager>.Instance.InputHandler.Mouse1 -= OnMouse1;
				Singleton<InputManager>.Instance.InputHandler.RapidPress -= OnRapidPress;
				Singleton<InputManager>.Instance.InputHandler.Mouse2 -= OnMouse2;
				Singleton<InputManager>.Instance.InputHandler.Ping -= OnPing;
				Singleton<InputManager>.Instance.InputHandler.Scan -= OnScan;
				Singleton<InputManager>.Instance.InputHandler.Throw -= OnThrowInput;
			}
		}

		private void OnInteract(InputActionPhase phase)
		{
            if (SushiAndFish.Casino.ParkCasinoIntegration.TryConsumeInteraction(phase)) return;
			if (Player != null && Player.HasSurfboard)
			{
				return;
			}
			if (Player.DivingEquipment.IsKnocked)
			{
				Player.DivingEquipment.Interact(phase);
			}
			else if (Player.IsToolActive)
			{
				if (Player.ActiveTool != null && Player.ActiveTool.CapturesInteractInput)
				{
					Player.ActiveTool.Interact(phase);
				}
				else if (_currentGazed is IToolActiveInteractable)
				{
					_currentGazed.TriggerInput(this, InputKind.Interact, phase);
				}
				else if (_currentBasicInteractable != null)
				{
					bool flag = Player.ActiveTool is AlienCompass && _currentBasicInteractable is CompassPartPickup;
					IBasicInteractable currentBasicInteractable = _currentBasicInteractable;
					if ((currentBasicInteractable is WorldPickup || currentBasicInteractable is IToolActiveInteractable) | flag)
					{
						_currentBasicInteractable.TriggerInput(this, InputKind.Interact, phase);
					}
				}
				else
				{
					Player.ActiveTool.Interact(phase);
				}
			}
			else if (!OnPlacement && !OnThrow)
			{
				Trigger(InputKind.Interact, phase);
			}
		}

		private void OnMouse1(InputActionPhase phase)
		{
            if (SushiAndFish.Casino.ParkCasinoIntegration.TryConsumeHeldItemInput(phase, false)) return;
			if (Player != null && Player.HasSurfboard)
			{
				return;
			}
			if (phase == InputActionPhase.Canceled)
			{
				StopEmptyLeftAnimation();
			}
			if (Player.IsSharkDragActive)
			{
				ResetEmptyAnims();
				return;
			}
			if (Player.DivingEquipment.IsKnocked)
			{
				Player.DivingEquipment.Mouse1(phase);
				return;
			}
			if (Player.IsToolActive)
			{
				if (_currentBasicInteractable is IToolActiveMouse1Interactable)
				{
					_currentBasicInteractable.TriggerInput(this, InputKind.Mouse1, phase);
				}
				else
				{
					Player.ActiveTool.Mouse1(phase);
				}
				return;
			}
			if (_suppressMouse1UntilCanceled)
			{
				switch (phase)
				{
				case InputActionPhase.Canceled:
					_suppressMouse1UntilCanceled = false;
					return;
				case InputActionPhase.Started:
					break;
				default:
					return;
				}
				_suppressMouse1UntilCanceled = false;
			}
			if (OnThrow)
			{
				return;
			}
			if (OnPlacement)
			{
				if (phase == InputActionPhase.Started && TryGetStorageStackTarget(out var stack))
				{
					BaseHoldable currentHeld = _currentHeld;
					EndPlacement(placeDone: false);
					stack.TriggerCrossInput(this, currentHeld, InputKind.Mouse1, phase);
				}
				else
				{
					if (phase != InputActionPhase.Started)
					{
						return;
					}
					_placer?.RefreshPlacementPreview();
					BaseHoldable currentHeld2 = _currentHeld;
					if (_placer != null && _placer.IsValid && TryPlace())
					{
						if (currentHeld2 is Tray)
						{
							_suppressMouse1UntilCanceled = true;
						}
						EndPlacement();
					}
				}
			}
			else
			{
				if (phase == InputActionPhase.Started && TryGetGazedTrayForInventory(out var targetTray) && TryHold(targetTray))
				{
					return;
				}
				if (phase == InputActionPhase.Started && _currentGazed is RestaurantTable)
				{
					Trigger(InputKind.Mouse1, phase);
				}
				else if (phase == InputActionPhase.Started && _currentGazed is BaseHoldable)
				{
					if (_currentHeld == null)
					{
						ResetEmptyAnims();
					}
					Trigger(InputKind.Mouse1, phase);
				}
				else if (phase == InputActionPhase.Started && _currentHeld != null && (_currentGazed is IHeldItemMouse1Interactable || _currentBasicInteractable is IHeldItemMouse1Interactable))
				{
					Trigger(InputKind.Mouse1, phase);
				}
				else if (phase != InputActionPhase.Started || !TryPlacePassiveSnapPreview())
				{
					Trigger(InputKind.Mouse1, phase);
				}
			}
		}

		private void OnRapidPress(InputActionPhase phase)
		{
			if (Player.TryHandleSharkDragEscapeInput(phase))
			{
				ResetEmptyAnims();
			}
			else if (Player.IsToolActive && Player.ActiveTool != null)
			{
				Player.ActiveTool.RapidPress(phase);
			}
		}

		private bool TryGetGazedTrayForInventory(out Tray targetTray)
		{
			targetTray = _currentGazed as Tray;
			if ((object)targetTray == null)
			{
				targetTray = ((_currentGazed != null) ? _currentGazed.GetComponentInParent<Tray>() : null);
			}
			if (_currentHeld is Tray && targetTray != null && targetTray != _currentHeld && targetTray.CanHold.Value && Singleton<QuickSlotHandler>.Instance != null)
			{
				return Singleton<QuickSlotHandler>.Instance.CanHoldSlot(targetTray);
			}
			return false;
		}

		private void RefreshQuickSnapPlacementPreview()
		{
			if (_placer == null)
			{
				return;
			}
			if (OnPlacement || OnThrow || Player == null || Player.IsToolActive)
			{
				_placer.ClearPassiveSnapPreview();
				return;
			}
			if (Player.DivingEquipment != null && Player.DivingEquipment.IsKnocked)
			{
				_placer.ClearPassiveSnapPreview();
				return;
			}
			if (_currentHeld == null || !_currentHeld.IsPlaceable)
			{
				_placer.ClearPassiveSnapPreview();
				return;
			}
			if (_currentGazed is ChefDeskGastroPan || TryGetStorageStackTarget(out var _) || TryGetGazedTrayForInventory(out var _))
			{
				_placer.ClearPassiveSnapPreview();
				return;
			}
			Transform transform = ((_camera != null) ? _camera.transform : Player.CameraTransform);
			if (transform == null)
			{
				_placer.ClearPassiveSnapPreview();
				return;
			}
			if (!Physics.Raycast(transform.position, transform.forward, out var hitInfo, _interactRange, _quickSnapPlacementMask, QueryTriggerInteraction.Collide))
			{
				_placer.ClearPassiveSnapPreview();
				return;
			}
			IQuickSnapPlacementZone componentInParent = hitInfo.collider.GetComponentInParent<QuickSnapPlacementZone>();
			if (componentInParent == null)
			{
				componentInParent = hitInfo.collider.GetComponentInParent<FixedSnapPlacementZone>();
			}
			if (componentInParent == null)
			{
				_placer.ClearPassiveSnapPreview();
			}
			else
			{
				_placer.TryRefreshPassiveSnapPreview(_currentHeld, componentInParent, hitInfo.point);
			}
		}

		private bool TryPlacePassiveSnapPreview()
		{
			RefreshQuickSnapPlacementPreview();
			if (_placer == null || !_placer.IsPassiveSnapPreviewValid)
			{
				return false;
			}
			BaseHoldable currentHeld = _currentHeld;
			bool num = TryPlace(suppressScanWhileParented: true);
			if (num)
			{
				if (currentHeld is Tray)
				{
					_suppressMouse1UntilCanceled = true;
				}
				QuestReferenceHub.Instance?.QuestComplete(QuestId.PlaceIngredientStorage);
				_placer.ClearPassiveSnapPreview();
			}
			return num;
		}

		private void OnMouse2(InputActionPhase phase)
		{
			if (Player != null && Player.TryDropSurfboard(phase))
			{
				return;
			}
			if (phase == InputActionPhase.Canceled)
			{
				StopEmptyRightAnimation();
			}
			if (Player.SeatedTotti != null)
			{
				switch (phase)
				{
				case InputActionPhase.Performed:
					Player.SeatedTotti.StartUnSeatHold(Player.OwnerClientId);
					break;
				case InputActionPhase.Canceled:
					Player.SeatedTotti.StopUnSeatHold();
					break;
				}
			}
			else if (Player.DivingEquipment.IsKnocked)
			{
				Player.DivingEquipment.Mouse2(phase);
			}
			else if (Player.IsToolActive)
			{
				Player.ActiveTool.Mouse2(phase);
			}
			else
			{
				if (OnThrow)
				{
					return;
				}
				if (OnPlacement)
				{
					if (phase == InputActionPhase.Canceled)
					{
						EndPlacement(placeDone: false);
					}
				}
				else if (_currentHeld != null && _currentHeld.IsPlaceable)
				{
					if (phase == InputActionPhase.Started && HasPlaceableMouse2CrossTarget())
					{
						Trigger(InputKind.Mouse2, phase);
					}
					else if (phase == InputActionPhase.Started)
					{
						StartPlacementMode();
					}
				}
				else if (!(_currentHeld != null) || !_currentHeld.IsThrowable || _currentGazed != null || _currentBasicInteractable != null)
				{
					Trigger(InputKind.Mouse2, phase);
				}
			}
		}

		private bool HasPlaceableMouse2CrossTarget()
		{
			if (!IsPlaceableMouse2CrossTarget(_currentGazed))
			{
				return IsPlaceableMouse2CrossTarget(_currentBasicInteractable);
			}
			return true;
		}

		private static bool IsPlaceableMouse2CrossTarget(object interactable)
		{
			if (!(interactable is WholesaleEntity) && !(interactable is SideDishIngredientEntity) && !(interactable is RiceCooker) && !(interactable is BeverageShelf) && !(interactable is StaffStorageStack))
			{
				if (interactable is RestaurantTable restaurantTable)
				{
					return restaurantTable.HasTakeBackableDeliveredTray;
				}
				return false;
			}
			return true;
		}

		private void OnThrowInput(InputActionPhase phase)
		{
            if (SushiAndFish.Casino.ParkCasinoIntegration.TryConsumeHeldItemInput(phase, true)) return;
			if ((!(Player != null) || !Player.TryDropSurfboard(phase)) && phase == InputActionPhase.Started && CanProcessThrowInput() && TryThrow())
			{
				EndThrow();
			}
		}

		private bool CanProcessThrowInput()
		{
			if (!base.IsOwner)
			{
				return false;
			}
			if (!InteractorStatus)
			{
				return false;
			}
			if (Player == null)
			{
				return false;
			}
			if (Player.DivingEquipment != null && Player.DivingEquipment.IsKnocked)
			{
				return false;
			}
			if (Player.IsToolActive)
			{
				return false;
			}
			if (OnPlacement || OnThrow)
			{
				return false;
			}
			if (_currentHeld == null)
			{
				return false;
			}
			return _currentHeld.IsThrowable;
		}

		private void StartPlacementMode()
		{
			if (!OnPlacement && !(_currentHeld == null) && _currentHeld.IsPlaceable)
			{
				if (_placer == null)
				{
					UtilityMethods.Log("<b>[Interactor_Input]</b> Placer reference is null.", LogType.Warning);
					return;
				}
				GazeExitCurrent();
				OnPlacement = true;
				_placer.StartPlacement(_currentHeld);
			}
		}

		private void Trigger(InputKind inputKind, InputActionPhase phase)
		{
			if (_currentHeld != null)
			{
				if (_currentGazed != null)
				{
					_currentGazed.TriggerCrossInput(this, _currentHeld, inputKind, phase);
				}
				else if (_currentBasicInteractable != null)
				{
					_currentBasicInteractable.TriggerCrossInput(this, _currentHeld, inputKind, phase);
				}
				else
				{
					CurrentHeldTrigger(inputKind, phase);
				}
			}
			else if (_currentGazed != null)
			{
				if (!(_currentGazed is PlayerInteraction) || (inputKind != InputKind.Mouse1 && inputKind != InputKind.Mouse2))
				{
					ResetEmptyAnims();
				}
				_currentGazed.TriggerInput(this, inputKind, phase);
			}
			else if (_currentBasicInteractable != null)
			{
				ResetEmptyAnims();
				_currentBasicInteractable.TriggerInput(this, inputKind, phase);
			}
			else
			{
				EmptyInputActions(inputKind, phase);
			}
		}

		public void CurrentHeldTrigger(InputKind inputKind, InputActionPhase phase)
		{
			if (_currentHeld == null)
			{
				return;
			}
			if (_currentHeld.IsPlaceable)
			{
				if (inputKind == InputKind.Mouse2 && phase == InputActionPhase.Started)
				{
					StartPlacementMode();
				}
			}
			else
			{
				_currentHeld.TriggerInput(this, inputKind, phase);
			}
		}

		public void EmptyInputActions(InputKind inputKind, InputActionPhase phase)
		{
			if (Player != null && Player.IsTrapped)
			{
				ResetEmptyAnims();
				return;
			}
			switch (inputKind)
			{
			case InputKind.Mouse1:
				switch (phase)
				{
				case InputActionPhase.Started:
					StartEmptyLeftAnimation();
					break;
				case InputActionPhase.Canceled:
					StopEmptyLeftAnimation();
					break;
				}
				break;
			case InputKind.Mouse2:
				switch (phase)
				{
				case InputActionPhase.Started:
					StartEmptyRightAnimation();
					break;
				case InputActionPhase.Canceled:
					StopEmptyRightAnimation();
					break;
				}
				break;
			case InputKind.Interact:
				break;
			}
		}

		private void StartEmptyLeftAnimation()
		{
			StopEmptyLeftAnimation();
			_isEmptyLeftPressActive = true;
			float seconds = (_isEmptyRightActive ? 0f : 0.1f);
			_emptyLeftCor = Singleton<DelayHelper>.Instance.DelayAction(seconds, () =>
			{
				_emptyLeftCor = null;
				if (!_isEmptyLeftPressActive || !CanPlayEmptyAnimation())
				{
					_isEmptyLeftPressActive = false;
				}
				else
				{
					_player.AnimSetBool(PPG_Player.EMPTY_LEFT, value: true);
					_isEmptyLeftActive = true;
				}
			});
		}

		private void StartEmptyRightAnimation()
		{
			StopEmptyRightAnimation();
			_isEmptyRightPressActive = true;
			float seconds = (_isEmptyLeftActive ? 0f : 0.1f);
			_emptyRightCor = Singleton<DelayHelper>.Instance.DelayAction(seconds, () =>
			{
				_emptyRightCor = null;
				if (!_isEmptyRightPressActive || !CanPlayEmptyAnimation())
				{
					_isEmptyRightPressActive = false;
				}
				else
				{
					_player.AnimSetBool(PPG_Player.EMPTY_RIGHT, value: true);
					_isEmptyRightActive = true;
				}
			});
		}

		private bool CanPlayEmptyAnimation()
		{
			if (Player != null && !Player.IsTrapped && !Player.IsToolActive && (Player.DivingEquipment == null || !Player.DivingEquipment.IsKnocked) && !OnThrow && !OnPlacement)
			{
				return _currentHeld == null;
			}
			return false;
		}

		private void StopEmptyLeftAnimation()
		{
			_isEmptyLeftPressActive = false;
			if (_emptyLeftCor != null)
			{
				Singleton<DelayHelper>.Instance.StopCoroutine(_emptyLeftCor);
				_emptyLeftCor = null;
			}
			if (_player != null)
			{
				_player.AnimSetBool(PPG_Player.EMPTY_LEFT, value: false);
			}
			_isEmptyLeftActive = false;
		}

		private void StopEmptyRightAnimation()
		{
			_isEmptyRightPressActive = false;
			if (_emptyRightCor != null)
			{
				Singleton<DelayHelper>.Instance.StopCoroutine(_emptyRightCor);
				_emptyRightCor = null;
			}
			if (_player != null)
			{
				_player.AnimSetBool(PPG_Player.EMPTY_RIGHT, value: false);
			}
			_isEmptyRightActive = false;
		}

		public void ResetEmptyAnims()
		{
			StopEmptyLeftAnimation();
			StopEmptyRightAnimation();
		}

		public void ForceClearHeldActionMode(bool refreshHints = true)
		{
			OnThrow = false;
			_suppressMouse1UntilCanceled = false;
			if (_thrower != null)
			{
				_thrower.EndThrow();
			}
			if (OnPlacement)
			{
				OnPlacement = false;
				if (_placer != null && _placer.InPlaceMode)
				{
					_placer.EndPlacement(placeDone: false);
				}
			}
			else if (_placer != null && _placer.InPlaceMode)
			{
				_placer.EndPlacement(placeDone: false);
			}
			ResetEmptyAnims();
			if (refreshHints)
			{
				TryGaze();
			}
		}

		private void PingSetup()
		{
			if (!(_pingObject == null))
			{
				_pingObject.transform.SetParent(null, worldPositionStays: true);
				_pingBaseScale = _pingObject.transform.localScale;
				CachePingComponents();
				_pingObject.SetActive(value: false);
			}
		}

		private void CachePingComponents()
		{
			if (_pingObject == null)
			{
				_pingRenderers = null;
			}
			else
			{
				_pingRenderers = _pingObject.GetComponentsInChildren<Renderer>(includeInactive: true);
			}
		}

		private void OnPing(InputActionPhase phase)
		{
			if ((!(Player != null) || !Player.HasSurfboard) && phase == InputActionPhase.Started && base.IsOwner && InteractorStatus && !(_pingObject == null) && !(Time.time < _lastPingTime + _pingCooldown) && (!(_player != null) || !(_player.DivingEquipment != null) || !_player.DivingEquipment.IsKnocked) && !OnPlacement && !OnThrow)
			{
				Transform transform = ((_camera != null) ? _camera.transform : null);
				if (!(transform == null) && Physics.Raycast(transform.position, transform.forward, out var hitInfo, _pingMaxDistance, _pingSurfaceMask, QueryTriggerInteraction.Ignore))
				{
					Vector3 point = hitInfo.point;
					Vector3 normal = SanitizeNormal(hitInfo.normal);
					Quaternion targetRotation = BuildKnifeRotation(normal);
					_lastPingTime = Time.time;
					float scaleMultiplier = CalculatePingScaleMultiplier(point);
					byte localPlayerColorIndex = GetLocalPlayerColorIndex();
					SpawnOrRefreshLocalPing(point, normal, targetRotation, localPlayerColorIndex, scaleMultiplier);
					ServerRequestPingRpc(point, normal, targetRotation, scaleMultiplier);
				}
			}
		}

		private byte GetLocalPlayerColorIndex()
		{
			if (Singleton<PlayersManager>.Instance == null)
			{
				return byte.MaxValue;
			}
			if (Singleton<PlayersManager>.Instance.LocalPlayer == null)
			{
				return byte.MaxValue;
			}
			return Singleton<PlayersManager>.Instance.LocalPlayer.ColorIndex.Value;
		}

		private void SpawnOrRefreshLocalPing(Vector3 hitPoint, Vector3 normal, Quaternion targetRotation, byte colorIndex, float scaleMultiplier)
		{
			if (_pingObject == null || !IsFinite(hitPoint))
			{
				return;
			}
			normal = SanitizeNormal(normal);
			targetRotation = SanitizeRotation(targetRotation, normal);
			ApplyPingMaterial(colorIndex);
			if (!_pingObject.activeSelf)
			{
				_pingObject.SetActive(value: true);
			}
			Transform tf = _pingObject.transform;
			KillPingTweens();
			scaleMultiplier = Mathf.Max(0.01f, scaleMultiplier);
			tf.localScale = _pingBaseScale * scaleMultiplier;
			Vector3 scaledTipOffset = Vector3.Scale(_pingLocalTipOffset, tf.localScale);
			Vector3 vector = ComputeKnifeRootPosition(hitPoint, targetRotation, normal, scaledTipOffset);
			Vector3 position = vector + normal * _pingStabBackDistance;
			Quaternion rotation = targetRotation;
			if (_pingRotRandomDegrees > 0.01f)
			{
				rotation *= Quaternion.Euler(UnityEngine.Random.Range(0f - _pingRotRandomDegrees, _pingRotRandomDegrees), UnityEngine.Random.Range(0f - _pingRotRandomDegrees, _pingRotRandomDegrees), UnityEngine.Random.Range(0f - _pingRotRandomDegrees, _pingRotRandomDegrees));
			}
			tf.SetPositionAndRotation(position, rotation);
			if (_sfx != null)
			{
				_sfx.transform.position = vector;
				_sfx.Stop();
				_sfx.Play();
			}
			_pingSeq = DOTween.Sequence();
			_pingSeq.Join(tf.DOMove(vector, _pingStabDuration).SetEase(Ease.OutCubic));
			_pingSeq.Join(tf.DORotateQuaternion(targetRotation, _pingStabDuration).SetEase(Ease.OutCubic));
			if (_pingPunchDegrees > 0.01f && _pingPunchDuration > 0.01f)
			{
				Vector3 punch = ((_pingPunchLocalAxis.sqrMagnitude > 0.0001f) ? _pingPunchLocalAxis.normalized : Vector3.right) * _pingPunchDegrees;
				_pingSeq.Append(tf.DOPunchRotation(punch, _pingPunchDuration, 8, 0.7f));
			}
			_pingSeq.OnComplete(() =>
			{
				if (!(_pingObject == null) && _pingObject.activeSelf && !(_pingSwayDegrees <= 0.01f) && !(_pingSwayPeriod <= 0.01f))
				{
					Vector3 endValue = ((_pingSwayLocalAxis.sqrMagnitude > 0.0001f) ? _pingSwayLocalAxis.normalized : Vector3.forward) * _pingSwayDegrees;
					_pingSwayTween = tf.DOLocalRotate(endValue, _pingSwayPeriod).SetRelative().SetEase(Ease.InOutSine)
						.SetLoops(-1, LoopType.Yoyo);
				}
			});
			RestartPingHideRoutine();
		}

		private float CalculatePingScaleMultiplier(Vector3 hitPoint)
		{
			if (!_scalePingByDistance)
			{
				return 1f;
			}
			float num = Mathf.Max(0f, _pingScaleMinDistance);
			float b = Mathf.Max(num + 0.01f, _pingScaleMaxDistance);
			float value = Vector3.Distance(base.transform.position, hitPoint);
			float num2 = Mathf.InverseLerp(num, b, value);
			if (_pingDistanceScaleCurve != null)
			{
				num2 = Mathf.Clamp01(_pingDistanceScaleCurve.Evaluate(num2));
			}
			return Mathf.Lerp(_pingScaleAtMinDistance, _pingScaleAtMaxDistance, num2);
		}

		private void ApplyPingMaterial(byte colorIndex)
		{
			if (_pingObject == null || Singleton<PlayersManager>.Instance == null || !Singleton<PlayersManager>.Instance.TryGetPingMaterial(colorIndex, out var material))
			{
				return;
			}
			if (_pingRenderers == null || _pingRenderers.Length == 0)
			{
				CachePingComponents();
			}
			if (_pingRenderers == null)
			{
				return;
			}
			for (int i = 0; i < _pingRenderers.Length; i++)
			{
				Renderer renderer = _pingRenderers[i];
				if (!(renderer == null))
				{
					Material[] sharedMaterials = renderer.sharedMaterials;
					if (sharedMaterials != null && sharedMaterials.Length != 0)
					{
						sharedMaterials[0] = material;
						renderer.sharedMaterials = sharedMaterials;
					}
				}
			}
		}

		private void RestartPingHideRoutine()
		{
			if (_pingHideRoutine != null)
			{
				StopCoroutine(_pingHideRoutine);
			}
			_pingHideRoutine = StartCoroutine(HidePingAfter(_pingLifetime));
		}

		private IEnumerator HidePingAfter(float delay)
		{
			if (delay > 0f)
			{
				yield return new WaitForSeconds(delay);
			}
			HidePingObject();
			_pingHideRoutine = null;
		}

		private void HidePingObject()
		{
			KillPingTweens();
			if (_pingObject != null)
			{
				_pingObject.SetActive(value: false);
			}
			if (_sfx != null && _sfx.isPlaying)
			{
				_sfx.Stop();
			}
		}

		private void KillPingTweens()
		{
			_pingSeq?.Kill();
			_pingSeq = null;
			_pingSwayTween?.Kill();
			_pingSwayTween = null;
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestPingRpc(Vector3 hitPoint, Vector3 normal, Quaternion targetRotation, float scaleMultiplier, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(4171937056u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in hitPoint);
				bufferWriter.WriteValueSafe(in normal);
				bufferWriter.WriteValueSafe(in targetRotation);
				bufferWriter.WriteValueSafe(in scaleMultiplier, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 4171937056u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsServer && !(Time.time < _serverNextAllowedPingTime) && IsFinite(hitPoint) && IsFinite(targetRotation))
			{
				normal = SanitizeNormal(normal);
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				if (!_serverValidatePingDistance || IsPingDistanceValidForSender(senderClientId, hitPoint))
				{
					_serverNextAllowedPingTime = Time.time + _pingCooldown;
					byte colorIndex = ResolveServerColorIndex(senderClientId);
					scaleMultiplier = Mathf.Clamp(scaleMultiplier, 0.01f, 10f);
					ClientSpawnPingRpc(hitPoint, normal, targetRotation, colorIndex, scaleMultiplier);
				}
			}
		}

		private bool IsPingDistanceValidForSender(ulong senderClientId, Vector3 hitPoint)
		{
			if (base.NetworkManager == null)
			{
				return true;
			}
			if (!base.NetworkManager.ConnectedClients.TryGetValue(senderClientId, out var value))
			{
				return false;
			}
			if (value.PlayerObject == null)
			{
				return false;
			}
			float num = Mathf.Max(_pingMaxDistance, _serverMaxPingDistanceFromPlayer);
			float num2 = num * num;
			Vector3 position = value.PlayerObject.transform.position;
			return (hitPoint - position).sqrMagnitude <= num2;
		}

		private byte ResolveServerColorIndex(ulong senderClientId)
		{
			if (Singleton<PlayersManager>.Instance == null)
			{
				return byte.MaxValue;
			}
			if (!Singleton<PlayersManager>.Instance.TryGetPlayer(senderClientId, out var player) || player == null)
			{
				return byte.MaxValue;
			}
			return player.ColorIndex.Value;
		}

		[Rpc(SendTo.Everyone)]
		private void ClientSpawnPingRpc(Vector3 hitPoint, Vector3 normal, Quaternion targetRotation, byte colorIndex, float scaleMultiplier)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1931740822u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in hitPoint);
				bufferWriter.WriteValueSafe(in normal);
				bufferWriter.WriteValueSafe(in targetRotation);
				bufferWriter.WriteValueSafe(in colorIndex, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in scaleMultiplier, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1931740822u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!base.IsOwner)
				{
					SpawnOrRefreshLocalPing(hitPoint, normal, targetRotation, colorIndex, scaleMultiplier);
				}
			}
		}

		private void OnDisable()
		{
			if (_pingHideRoutine != null)
			{
				StopCoroutine(_pingHideRoutine);
				_pingHideRoutine = null;
			}
			HidePingObject();
			ScanOnDisable();
		}

		public override void OnDestroy()
		{
			if (_pingHideRoutine != null)
			{
				StopCoroutine(_pingHideRoutine);
				_pingHideRoutine = null;
			}
			KillPingTweens();
			if (_sfx != null && _sfx.isPlaying)
			{
				_sfx.Stop();
			}
			if (_destroyDetachedPingOnDestroy && _pingObject != null)
			{
				UnityEngine.Object.Destroy(_pingObject);
				_pingObject = null;
			}
			base.OnDestroy();
		}

		private Quaternion BuildKnifeRotation(Vector3 normal)
		{
			normal = SanitizeNormal(normal);
			Vector3 vector = -normal;
			Vector3 upwards = ((_camera != null) ? _camera.transform.up : Vector3.up);
			if (Mathf.Abs(Vector3.Dot(vector.normalized, upwards.normalized)) > 0.98f)
			{
				upwards = ((Vector3.Cross(vector, Vector3.right).sqrMagnitude > 0.0001f) ? Vector3.right : Vector3.forward);
			}
			return Quaternion.LookRotation(vector, upwards);
		}

		private Vector3 ComputeKnifeRootPosition(Vector3 hitPoint, Quaternion rotation, Vector3 normal, Vector3 scaledTipOffset)
		{
			Vector3 vector = rotation * scaledTipOffset;
			return hitPoint - vector + normal * _pingSurfaceOffset;
		}

		private static Vector3 SanitizeNormal(Vector3 normal)
		{
			if (!IsFinite(normal))
			{
				return Vector3.up;
			}
			if (normal.sqrMagnitude <= 0.0001f)
			{
				return Vector3.up;
			}
			return normal.normalized;
		}

		private static Quaternion SanitizeRotation(Quaternion rotation, Vector3 fallbackNormal)
		{
			if (!IsFinite(rotation))
			{
				return Quaternion.LookRotation(-SanitizeNormal(fallbackNormal), Vector3.up);
			}
			float num = Mathf.Sqrt(rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w);
			if (num <= 0.0001f)
			{
				return Quaternion.LookRotation(-SanitizeNormal(fallbackNormal), Vector3.up);
			}
			return new Quaternion(rotation.x / num, rotation.y / num, rotation.z / num, rotation.w / num);
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

		private bool TryPlace(bool suppressScanWhileParented = false)
		{
			if (!_placer.TryGetPlacementTransform(out var pos, out var rot, out var snapPoint, out var interactable))
			{
				UtilityMethods.Log("Ghost transform not valid!");
				return false;
			}
			Tray tray = _currentHeld as Tray;
			IQuickSnapPlacementZone activeQuickSnapZone = _placer.ActiveQuickSnapZone;
			FixedSnapPlacementZone fixedSnapPlacementZone = activeQuickSnapZone as FixedSnapPlacementZone;
			TrayTrolley trayTrolley = ((fixedSnapPlacementZone != null) ? fixedSnapPlacementZone.GetComponentInParent<TrayTrolley>() : null);
			bool suppressScanWhilePlaced = suppressScanWhileParented || activeQuickSnapZone is QuickSnapPlacementZone;
			if (tray != null && trayTrolley != null)
			{
				return trayTrolley.TryRequestTrayPlacement(this, tray, fixedSnapPlacementZone);
			}
			if (snapPoint != null && interactable != null && interactable.NetworkObject != null)
			{
				_currentHeld.Place(pos, rot.eulerAngles, interactable.NetworkObject, suppressScanWhilePlaced, suppressTooltipWhilePlaced: false);
				return true;
			}
			_currentHeld.Place(pos, rot.eulerAngles, suppressScanWhilePlaced, suppressTooltipWhilePlaced: false);
			return true;
		}

		public void EndPlacement(bool placeDone = true)
		{
			OnPlacement = false;
			if (!(_placer == null))
			{
				if (_placer.InPlaceMode)
				{
					_placer.EndPlacement(placeDone);
				}
				else
				{
					_placer.ClearPassiveSnapPreview();
				}
			}
		}

		public void SelectItem(BaseHoldable item, bool startHold)
		{
			UtilityMethods.Log("[Interactor_QuickSlot] HoldItem:" + item);
			if (!(item == null))
			{
				ResetPlacementForHeldItemTransition();
				_currentHeld = item;
				_currentHeld.SetVisibility(visible: true);
				_currentHeld.ClientEnsureHeldVisuals(this);
				ServerSyncHoldableRpc(_currentHeld.NetworkObject.ToReference());
				SetStateHolding(startHold);
			}
		}

		public void DeselectHeldItem(bool setVisibility = true)
		{
			UtilityMethods.Log("[Interactor_QuickSlot] UnequipHeldItem:" + _currentHeld);
			ResetPlacementForHeldItemTransition();
			SetStateEmpty();
			if (_currentHeld == null)
			{
				SyncHeldItemCleared();
				return;
			}
			if (setVisibility)
			{
				_currentHeld.SetVisibility(visible: false);
			}
			_currentHeld = null;
			SyncHeldItemCleared();
		}

		private void ResetPlacementForHeldItemTransition()
		{
			OnPlacement = false;
			_placer?.ResetForHeldItemTransition();
		}

		private void SyncHeldItemCleared()
		{
			if (base.IsOwner && !(base.NetworkObject == null) && base.NetworkObject.IsSpawned)
			{
				ServerSyncHoldableRpc(default);
			}
		}

		private void ScanInit()
		{
			int num = Mathf.Max(8, _scanHitBufferSize);
			_scanHits = new RaycastHit[num];
		}

		private void OnScan(InputActionPhase phase)
		{
			if (Player != null && Player.HasSurfboard)
			{
				CancelScanHold();
				return;
			}
			switch (phase)
			{
			case InputActionPhase.Started:
				TryBeginScanHold();
				break;
			case InputActionPhase.Canceled:
				CancelScanHold();
				break;
			}
		}

		private void TryBeginScanHold()
		{
			if (!(Time.time < _nextScanTime))
			{
				if (_scanHoldRoutine != null)
				{
					StopCoroutine(_scanHoldRoutine);
				}
				ScanHoldUI_Show();
				_scanHoldRoutine = StartCoroutine(ScanHoldRoutine());
				QuestReferenceHub.Instance.QuestComplete(QuestId.InspectTheRestaurant);
			}
		}

		private void CancelScanHold()
		{
			if (_scanHoldRoutine != null)
			{
				StopCoroutine(_scanHoldRoutine);
				_scanHoldRoutine = null;
			}
			ScanHoldUI_Hide();
		}

		private IEnumerator ScanHoldRoutine()
		{
			float t = 0f;
			while (t < _scanHoldDuration)
			{
				if (Player != null && Player.HasSurfboard)
				{
					_scanHoldRoutine = null;
					ScanHoldUI_Hide(instant: true);
					yield break;
				}
				t += Time.deltaTime;
				float normalized = ((_scanHoldDuration <= 0.0001f) ? 1f : (t / _scanHoldDuration));
				ScanHoldUI_SetProgress(normalized);
				yield return null;
			}
			_scanHoldRoutine = null;
			_nextScanTime = Time.time + _scanCooldown;
			ScanHoldUI_Hide();
			PerformScan();
			RegisterPlayerScanIndicators();
		}

		private void PerformScan()
		{
			if (_indicatorHandler == null)
			{
				UtilityMethods.Log("<b>[Scan]</b> <color=#FF6666>OffscreenIndicatorHandler.Instance is null.</color>");
				return;
			}
			if (_scanHits == null || _scanHits.Length != Mathf.Max(8, _scanHitBufferSize))
			{
				_scanHits = new RaycastHit[Mathf.Max(8, _scanHitBufferSize)];
			}
			Transform obj = ((_scanOrigin != null) ? _scanOrigin : base.transform);
			Vector3 position = obj.position;
			Vector3 forward = obj.forward;
			_scanTargetsBuffer.Clear();
			_scanTargetsUnique.Clear();
			_scanIndicatorTargetsBuffer.Clear();
			_scanIndicatorTargetsUnique.Clear();
			_scanInteractableMinDistSqr.Clear();
			_scanIndicatorMinDistSqr.Clear();
			_scanInteractableSorted.Clear();
			_scanIndicatorSorted.Clear();
			_selectedIndicatorsFromInteractables.Clear();
			int num = Physics.SphereCastNonAlloc(new Ray(position, forward), _scanSphereRadius, _scanHits, _scanDistance, _scanMask);
			for (int i = 0; i < num; i++)
			{
				Collider collider = _scanHits[i].collider;
				if (collider == null)
				{
					continue;
				}
				float sqrMagnitude = (collider.transform.position - position).sqrMagnitude;
				BaseInteractable componentInParent = collider.GetComponentInParent<BaseInteractable>();
				if (componentInParent != null)
				{
					if (!(componentInParent is PlayerInteraction) && componentInParent.IsScanable)
					{
						if (!_scanInteractableMinDistSqr.TryGetValue(componentInParent, out var value) || sqrMagnitude < value)
						{
							_scanInteractableMinDistSqr[componentInParent] = sqrMagnitude;
						}
						OffscreenIndicatorTarget indicatorTarget = componentInParent.IndicatorTarget;
						if (indicatorTarget != null && (!_scanIndicatorMinDistSqr.TryGetValue(indicatorTarget, out var value2) || sqrMagnitude < value2))
						{
							_scanIndicatorMinDistSqr[indicatorTarget] = sqrMagnitude;
						}
					}
					continue;
				}
				SeaCreature componentInParent2 = collider.GetComponentInParent<SeaCreature>();
				if (componentInParent2 != null)
				{
					OffscreenIndicatorTarget scanIndicatorTarget = componentInParent2.ScanIndicatorTarget;
					if (scanIndicatorTarget != null && (!_scanIndicatorMinDistSqr.TryGetValue(scanIndicatorTarget, out var value3) || sqrMagnitude < value3))
					{
						_scanIndicatorMinDistSqr[scanIndicatorTarget] = sqrMagnitude;
					}
				}
				else
				{
					OffscreenIndicatorTarget componentInParent3 = collider.GetComponentInParent<OffscreenIndicatorTarget>();
					if (componentInParent3 != null && (!_scanIndicatorMinDistSqr.TryGetValue(componentInParent3, out var value4) || sqrMagnitude < value4))
					{
						_scanIndicatorMinDistSqr[componentInParent3] = sqrMagnitude;
					}
				}
			}
			CollectSeaCreatureScanFallbacks(position, forward);
			foreach (KeyValuePair<BaseInteractable, float> item in _scanInteractableMinDistSqr)
			{
				_scanInteractableSorted.Add(item.Key);
			}
			_scanInteractableSorted.Sort((BaseInteractable a, BaseInteractable b) =>
			{
				float num7 = _scanInteractableMinDistSqr[a];
				float num8 = _scanInteractableMinDistSqr[b];
				return (!(num7 < num8)) ? ((num7 > num8) ? 1 : 0) : (-1);
			});
			foreach (KeyValuePair<OffscreenIndicatorTarget, float> item2 in _scanIndicatorMinDistSqr)
			{
				_scanIndicatorSorted.Add(item2.Key);
			}
			_scanIndicatorSorted.Sort((OffscreenIndicatorTarget a, OffscreenIndicatorTarget b) =>
			{
				float num7 = _scanIndicatorMinDistSqr[a];
				float num8 = _scanIndicatorMinDistSqr[b];
				return (!(num7 < num8)) ? ((num7 > num8) ? 1 : 0) : (-1);
			});
			int num2 = Mathf.Max(1, _scanMaxTargets);
			for (int num3 = 0; num3 < _scanInteractableSorted.Count; num3++)
			{
				if (_scanTargetsBuffer.Count >= num2)
				{
					break;
				}
				BaseInteractable baseInteractable = _scanInteractableSorted[num3];
				if (!(baseInteractable == null) && _scanTargetsUnique.Add(baseInteractable))
				{
					_scanTargetsBuffer.Add(baseInteractable);
					TryCompleteScanFishQuest(baseInteractable);
					if (baseInteractable.IndicatorTarget != null)
					{
						_selectedIndicatorsFromInteractables.Add(baseInteractable.IndicatorTarget);
					}
				}
			}
			if (_scanTargetsBuffer.Count > 0)
			{
				_indicatorHandler.RegisterScan(_scanTargetsBuffer, _scanIndicatorLifetime);
			}
			int num4 = num2 - _scanTargetsBuffer.Count;
			int num5 = 0;
			if (num4 > 0 && _scanIndicatorSorted.Count > 0)
			{
				for (int num6 = 0; num6 < _scanIndicatorSorted.Count; num6++)
				{
					if (num5 >= num4)
					{
						break;
					}
					OffscreenIndicatorTarget offscreenIndicatorTarget = _scanIndicatorSorted[num6];
					if (!(offscreenIndicatorTarget == null) && !_selectedIndicatorsFromInteractables.Contains(offscreenIndicatorTarget))
					{
						BaseInteractable componentInParent4 = offscreenIndicatorTarget.GetComponentInParent<BaseInteractable>();
						if (!(componentInParent4 != null) || !_scanTargetsUnique.Contains(componentInParent4))
						{
							_indicatorHandler.Register(offscreenIndicatorTarget, _scanIndicatorLifetime);
							TryCompleteScanFishQuest(offscreenIndicatorTarget);
							num5++;
						}
					}
				}
			}
			if (_scanTargetsBuffer.Count == 0 && num5 == 0)
			{
				UtilityMethods.Log("<b>[Scan]</b> <color=#AAAAAA>No valid scan targets found.</color>");
				return;
			}
			UtilityMethods.Log($"<b>[Scan]</b> Registered <color=#00FFAA>{_scanTargetsBuffer.Count + num5}</color> / " + $"<color=#00FFAA>{num2}</color> icons " + $"(<color=#00FFAA>{_scanTargetsBuffer.Count}</color> interactables, " + $"<color=#00FFAA>{num5}</color> indicator-only) " + $"for <color=#00FFAA>{_scanIndicatorLifetime:0.##}s</color>.");
		}

		private void CollectSeaCreatureScanFallbacks(Vector3 origin, Vector3 direction)
		{
			if (direction.sqrMagnitude <= 0.0001f)
			{
				return;
			}
			SeaCreature[] array = UnityEngine.Object.FindObjectsByType<SeaCreature>(FindObjectsSortMode.None);
			if (array == null || array.Length == 0)
			{
				return;
			}
			Vector3 normalized = direction.normalized;
			float num = Mathf.Max(0.01f, _scanSphereRadius);
			float scanRadiusSqr = num * num;
			float scanDistance = Mathf.Max(0.01f, _scanDistance);
			foreach (SeaCreature seaCreature in array)
			{
				if (!(seaCreature == null) && seaCreature.isActiveAndEnabled && (!(seaCreature.NetworkObject != null) || seaCreature.NetworkObject.IsSpawned) && TryGetSeaCreatureScanDistanceSqr(seaCreature, origin, normalized, scanDistance, scanRadiusSqr, out var distSqr))
				{
					OffscreenIndicatorTarget scanIndicatorTarget = seaCreature.ScanIndicatorTarget;
					if (!(scanIndicatorTarget == null) && (!_scanIndicatorMinDistSqr.TryGetValue(scanIndicatorTarget, out var value) || distSqr < value))
					{
						_scanIndicatorMinDistSqr[scanIndicatorTarget] = distSqr;
					}
				}
			}
		}

		private static bool TryGetSeaCreatureScanDistanceSqr(SeaCreature creature, Vector3 origin, Vector3 direction, float scanDistance, float scanRadiusSqr, out float distSqr)
		{
			distSqr = float.MaxValue;
			Collider[] componentsInChildren = creature.GetComponentsInChildren<Collider>(includeInactive: true);
			bool flag = false;
			if (componentsInChildren != null)
			{
				foreach (Collider collider in componentsInChildren)
				{
					if (!(collider == null) && collider.enabled && !collider.isTrigger)
					{
						flag = true;
						float value = Vector3.Dot(collider.bounds.center - origin, direction);
						value = Mathf.Clamp(value, 0f, scanDistance);
						Vector3 vector = origin + direction * value;
						if (!((collider.ClosestPoint(vector) - vector).sqrMagnitude > scanRadiusSqr))
						{
							distSqr = Mathf.Min(distSqr, value * value);
						}
					}
				}
			}
			if (distSqr < float.MaxValue)
			{
				return true;
			}
			if (flag)
			{
				return false;
			}
			float num = Vector3.Dot(creature.transform.position - origin, direction);
			if (num < 0f || num > scanDistance)
			{
				return false;
			}
			Vector3 vector2 = origin + direction * num;
			if ((creature.transform.position - vector2).sqrMagnitude > scanRadiusSqr)
			{
				return false;
			}
			distSqr = num * num;
			return true;
		}

		private void TryCompleteScanFishQuest(Component scanSource)
		{
			if (_player == null || !_player.IsUnderWater || scanSource == null)
			{
				return;
			}
			SeaCreature componentInParent = scanSource.GetComponentInParent<SeaCreature>();
			if (componentInParent == null || componentInParent.FishSO == null)
			{
				return;
			}
			GameProgressManager instance = Singleton<GameProgressManager>.Instance;
			if (!(instance == null) && !(instance.ItemDatabaseSO == null))
			{
				string id = componentInParent.FishSO.Id;
				if (!string.IsNullOrWhiteSpace(id) && id == instance.ItemDatabaseSO.MackerelSO?.Id)
				{
					QuestReferenceHub.Instance.QuestComplete(QuestId.ScanMackerel);
				}
				if (!string.IsNullOrWhiteSpace(id) && id == instance.ItemDatabaseSO.BlackSeaBreamSO?.Id)
				{
					QuestReferenceHub.Instance.QuestComplete(QuestId.ScanBlackSeaBream);
				}
				if (!string.IsNullOrWhiteSpace(id) && id == instance.ItemDatabaseSO.SquidSO?.Id)
				{
					QuestReferenceHub.Instance.QuestComplete(QuestId.ScanSquid);
				}
			}
		}

		private void RegisterPlayerScanIndicators()
		{
			if (_indicatorHandler == null || Singleton<PlayersManager>.Instance == null || Singleton<PlayersManager>.Instance.LocalPlayer == null)
			{
				return;
			}
			PPG_Player localPlayer = Singleton<PlayersManager>.Instance.LocalPlayer;
			Transform distanceOrigin = ((_scanOrigin != null) ? _scanOrigin : localPlayer.transform);
			float expireAt = Time.time + _scanIndicatorLifetime;
			foreach (PPG_Player value in Singleton<PlayersManager>.Instance.Players.Values)
			{
				if (!(value == null) && !(value == localPlayer) && value.OwnerClientId != localPlayer.OwnerClientId)
				{
					_indicatorHandler.RegisterTransform(value.transform, _scanIndicatorLifetime, refreshIfExists: true, null, showDistance: true, _scanPlayerIndicatorWorldOffset, distanceOrigin);
					RegisterPlayerScanAvatarWhenReady(value, expireAt).Forget();
				}
			}
		}

		private async UniTaskVoid RegisterPlayerScanAvatarWhenReady(PPG_Player player, float expireAt)
		{
			if (player == null)
			{
				return;
			}
			ulong value = player.SteamID.Value;
			if (value == 0L || value == ulong.MaxValue)
			{
				return;
			}
			Sprite avatarSprite = await SteamAvatarCache.GetAvatarSpriteAsync(value);
			await UniTask.SwitchToMainThread();
			if (!(this == null) && !(player == null) && !(avatarSprite == null) && !(_indicatorHandler == null))
			{
				float num = expireAt - Time.time;
				if (!(num <= 0f))
				{
					PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
					Transform distanceOrigin = ((_scanOrigin != null) ? _scanOrigin : ((pPG_Player != null) ? pPG_Player.transform : base.transform));
					_indicatorHandler.RegisterTransform(player.transform, num, refreshIfExists: true, avatarSprite, showDistance: true, _scanPlayerIndicatorWorldOffset, distanceOrigin);
				}
			}
		}

		private void ScanOnDisable()
		{
			ScanHoldUI_Hide(instant: true);
			if (_scanHoldRoutine != null)
			{
				StopCoroutine(_scanHoldRoutine);
				_scanHoldRoutine = null;
			}
		}

		private void ScanHoldUI_Show()
		{
			if (!(_scanHoldCanvasGroup == null) && !(_scanHoldSlider == null))
			{
				_scanHoldSlider.value = 0f;
				SetScanUIVisible(visible: true, _scanHoldShowFade <= 0f);
				if (_scanHoldShowFade > 0f)
				{
					FadeScanUI(1f, _scanHoldShowFade);
				}
			}
		}

		private void ScanHoldUI_Hide(bool instant = false)
		{
			if (!(_scanHoldCanvasGroup == null) && !(_scanHoldSlider == null))
			{
				if (instant || _scanHoldHideFade <= 0f)
				{
					SetScanUIVisible(visible: false, instant: true);
				}
				else
				{
					FadeScanUI(0f, _scanHoldHideFade, onCompleteDisable: true);
				}
			}
		}

		private void ScanHoldUI_SetProgress(float normalized01)
		{
			if (!(_scanHoldSlider == null))
			{
				_scanHoldSlider.value = Mathf.Clamp01(normalized01);
			}
		}

		private void SetScanUIVisible(bool visible, bool instant)
		{
			if (!(_scanHoldCanvasGroup == null))
			{
				if (_scanUiFadeRoutine != null)
				{
					StopCoroutine(_scanUiFadeRoutine);
					_scanUiFadeRoutine = null;
				}
				_scanHoldCanvasGroup.gameObject.SetActive(visible);
				_scanHoldCanvasGroup.blocksRaycasts = false;
				_scanHoldCanvasGroup.interactable = false;
				if (instant)
				{
					_scanHoldCanvasGroup.alpha = (visible ? 1f : 0f);
				}
			}
		}

		private void FadeScanUI(float to, float duration, bool onCompleteDisable = false)
		{
			if (!(_scanHoldCanvasGroup == null))
			{
				if (_scanUiFadeRoutine != null)
				{
					StopCoroutine(_scanUiFadeRoutine);
				}
				_scanUiFadeRoutine = StartCoroutine(FadeScanUICoroutine(to, duration, onCompleteDisable));
			}
		}

		private IEnumerator FadeScanUICoroutine(float to, float duration, bool onCompleteDisable)
		{
			float from = _scanHoldCanvasGroup.alpha;
			float t = 0f;
			if (!_scanHoldCanvasGroup.gameObject.activeSelf)
			{
				_scanHoldCanvasGroup.gameObject.SetActive(value: true);
			}
			while (t < duration)
			{
				t += Time.deltaTime;
				float t2 = Mathf.Clamp01(t / duration);
				_scanHoldCanvasGroup.alpha = Mathf.Lerp(from, to, t2);
				yield return null;
			}
			_scanHoldCanvasGroup.alpha = to;
			if (onCompleteDisable && to <= 0.001f)
			{
				_scanHoldCanvasGroup.gameObject.SetActive(value: false);
			}
			_scanUiFadeRoutine = null;
		}

		public void SetStateHolding(bool startHold)
		{
			if (!Player.IsToolActive && !(_currentHeld == null))
			{
				UtilityMethods.Log("[Interactor_State] SetStateHolding");
				InteractorState = (Player.FirstPersonCharacter.IsSwimming() ? InteractorState.SwimHolding : InteractorState.Holding);
				Player.EquipHoldProfile(_currentHeld.HoldType);
				Player.SetHolding(isHolding: true, startHold);
			}
		}

		public void SetStateEmpty()
		{
			if (!Player.IsToolActive)
			{
				UtilityMethods.Log("[Interactor_State] SetStateEmpty");
				InteractorState = (Player.FirstPersonCharacter.IsSwimming() ? InteractorState.SwimEmpty : InteractorState.Empty);
				Player.SetHolding(isHolding: false);
			}
		}

		public void SetStateUsing()
		{
			InteractorState = InteractorState.Using;
		}

		public bool IsStateHolding()
		{
			if (InteractorState != InteractorState.Holding)
			{
				return InteractorState == InteractorState.SwimHolding;
			}
			return true;
		}

		public bool IsStateEmpty()
		{
			if (InteractorState != InteractorState.Empty)
			{
				return InteractorState == InteractorState.SwimEmpty;
			}
			return true;
		}

		public bool IsStateUsing()
		{
			return InteractorState == InteractorState.Using;
		}

		private bool TryThrow()
		{
			if (_currentHeld == null)
			{
				return false;
			}
			if (!_currentHeld.IsThrowable)
			{
				return false;
			}
			if (_thrower == null)
			{
				UtilityMethods.Log("<b>[Interactor_Throw]</b> Thrower reference is null.", LogType.Warning);
				return false;
			}
			_thrower.StartThrow(_currentHeld);
			if (!_thrower.TryGetThrowRequestPayload(out var data))
			{
				UtilityMethods.Log("<b>[Interactor_Throw]</b> Throw payload could not be built.", LogType.Warning);
				return false;
			}
			_currentHeld.RequestThrow(data);
			UtilityMethods.Log($"<b>[Interactor_Throw]</b> Physics throw request sent. Force:{data.Force:F2} Direction:{data.Direction}");
			return true;
		}

		public void EndThrow(bool throwDone = true)
		{
			OnThrow = false;
			if (_thrower != null)
			{
				_thrower.EndThrow();
			}
			if (!throwDone && _currentHeld != null)
			{
				_currentHeld.EnableHeldHints();
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerUseEnterRpc(NetworkObjectReference reference, uint requestSequence)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2545263783u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in reference, default(FastBufferWriter.ForNetworkSerializable));
				BytePacker.WriteValueBitPacked(bufferWriter, requestSequence);
				__endSendRpc(ref bufferWriter, 2545263783u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (IsUsing || Player == null || Player.IsTrapped)
				{
					ClientUseRequestResolvedRpc(requestSequence);
					return;
				}
				if (!reference.TryGetComponent<BaseUsable>(out var component) || !component.IsAvailable())
				{
					ClientUseRequestResolvedRpc(requestSequence);
					return;
				}
				_currentInUse = component;
				SetNetworkUseState(isUsing: true);
				_currentInUse.ServerUseEnter(this);
				ClientUseEnterRpc(new NetworkObjectReference(_currentInUse.NetworkObject), requestSequence);
			}
		}

		[Rpc(SendTo.Owner)]
		private void ClientUseEnterRpc(NetworkObjectReference reference, uint requestSequence)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1278383090u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in reference, default(FastBufferWriter.ForNetworkSerializable));
				BytePacker.WriteValueBitPacked(bufferWriter, requestSequence);
				__endSendRpc(ref bufferWriter, 1278383090u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!_useRequestPending || requestSequence != _useRequestSequence)
			{
				ServerUseExitRpc(reference);
				return;
			}
			_useRequestPending = false;
			if (reference.TryGetComponent<BaseUsable>(out var component))
			{
				_currentInUse = component;
				_currentInUse.GazeExit();
				_currentInUse.ClientUseEnter(this);
			}
		}

		[Rpc(SendTo.Owner)]
		private void ClientUseRequestResolvedRpc(uint requestSequence)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2125333280u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, requestSequence);
				__endSendRpc(ref bufferWriter, 2125333280u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (_useRequestPending && requestSequence == _useRequestSequence)
				{
					_useRequestPending = false;
				}
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerUseExitRpc(NetworkObjectReference reference = default(NetworkObjectReference))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1278739012u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in reference, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 1278739012u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				BaseUsable baseUsable = _currentInUse;
				if (baseUsable == null && reference.TryGetComponent<BaseUsable>(out var component))
				{
					baseUsable = component;
				}
				if (baseUsable != null)
				{
					baseUsable.ServerUseExit(this);
				}
				NetworkObjectReference reference2 = ((baseUsable != null) ? baseUsable.NetworkObject.ToReference() : default(NetworkObjectReference));
				ClearCurrentUseLocal();
				SetNetworkUseState(isUsing: false);
				ClientUseExitRpc(reference2);
			}
		}

		[Rpc(SendTo.Owner)]
		private void ClientUseExitRpc(NetworkObjectReference reference)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3590544220u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in reference, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3590544220u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_useRequestPending = false;
				BaseUsable baseUsable = _currentInUse;
				if (baseUsable == null && reference.TryGetComponent<BaseUsable>(out var component))
				{
					baseUsable = component;
				}
				if (baseUsable != null)
				{
					baseUsable.ClientUseExit(this);
				}
				ClearCurrentUseLocal();
				SetStateEmpty();
				TryGaze();
			}
		}

		public void ForceClientUseExit(BaseUsable usable)
		{
			if (base.IsOwner)
			{
				BaseUsable baseUsable = ((usable != null) ? usable : _currentInUse);
				if (baseUsable != null)
				{
					baseUsable.ClientUseExit(this);
				}
				ClearCurrentUseLocal();
				SetStateEmpty();
				TryGaze();
			}
		}

		public void ForceServerUseExit(BaseUsable usable)
		{
			if (base.IsServer)
			{
				BaseUsable baseUsable = ((usable != null) ? usable : _currentInUse);
				if (baseUsable != null)
				{
					baseUsable.ServerUseExit(this);
				}
				NetworkObjectReference reference = ((baseUsable != null) ? baseUsable.NetworkObject.ToReference() : default(NetworkObjectReference));
				ClearCurrentUseLocal();
				SetNetworkUseState(isUsing: false);
				ClientUseExitRpc(reference);
			}
		}

		private void ServerReleaseCurrentUseOnDespawn()
		{
			if (base.IsServer && !(_currentInUse == null))
			{
				_currentInUse.ServerUseInterrupted(this);
				ClearCurrentUseLocal();
			}
		}

		private void ClearCurrentUseLocal()
		{
			_currentInUse = null;
		}

		private void SetNetworkUseState(bool isUsing)
		{
			if (base.IsServer)
			{
				_isUsingNetwork.Value = isUsing;
			}
		}

		protected override void __initializeVariables()
		{
			if (_isUsingNetwork == null)
			{
				throw new Exception("Interactor._isUsingNetwork cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isUsingNetwork.Initialize(this);
			__nameNetworkVariable(_isUsingNetwork, "_isUsingNetwork");
			NetworkVariableFields.Add(_isUsingNetwork);
			if (_heldItemReference == null)
			{
				throw new Exception("Interactor._heldItemReference cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_heldItemReference.Initialize(this);
			__nameNetworkVariable(_heldItemReference, "_heldItemReference");
			NetworkVariableFields.Add(_heldItemReference);
			if (_heldItemVisible == null)
			{
				throw new Exception("Interactor._heldItemVisible cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_heldItemVisible.Initialize(this);
			__nameNetworkVariable(_heldItemVisible, "_heldItemVisible");
			NetworkVariableFields.Add(_heldItemVisible);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(864342254u, __rpc_handler_864342254, "ServerHoldRpc", RpcInvokePermission.Everyone);
			__registerRpc(140169587u, __rpc_handler_140169587, "ClientHoldFailRpc", RpcInvokePermission.Everyone);
			__registerRpc(2939824725u, __rpc_handler_2939824725, "ServerSyncHoldableRpc", RpcInvokePermission.Everyone);
			__registerRpc(666019032u, __rpc_handler_666019032, "ClientDestroyHeldItemRpc", RpcInvokePermission.Everyone);
			__registerRpc(4171937056u, __rpc_handler_4171937056, "ServerRequestPingRpc", RpcInvokePermission.Everyone);
			__registerRpc(1931740822u, __rpc_handler_1931740822, "ClientSpawnPingRpc", RpcInvokePermission.Everyone);
			__registerRpc(2545263783u, __rpc_handler_2545263783, "ServerUseEnterRpc", RpcInvokePermission.Everyone);
			__registerRpc(1278383090u, __rpc_handler_1278383090, "ClientUseEnterRpc", RpcInvokePermission.Everyone);
			__registerRpc(2125333280u, __rpc_handler_2125333280, "ClientUseRequestResolvedRpc", RpcInvokePermission.Everyone);
			__registerRpc(1278739012u, __rpc_handler_1278739012, "ServerUseExitRpc", RpcInvokePermission.Everyone);
			__registerRpc(3590544220u, __rpc_handler_3590544220, "ClientUseExitRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_864342254(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Interactor)target).ServerHoldRpc(value);
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_140169587(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Interactor)target).ClientHoldFailRpc(value);
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2939824725(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Interactor)target).ServerSyncHoldableRpc(value);
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_666019032(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Interactor)target).ClientDestroyHeldItemRpc(value);
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4171937056(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out Quaternion value3);
				reader.ReadValueSafe(out float value4, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Interactor)target).ServerRequestPingRpc(value, value2, value3, value4, ext);
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1931740822(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out Quaternion value3);
				reader.ReadValueSafe(out byte value4, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value5, default(FastBufferWriter.ForPrimitives));
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Interactor)target).ClientSpawnPingRpc(value, value2, value3, value4, value5);
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2545263783(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				ByteUnpacker.ReadValueBitPacked(reader, out uint value2);
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Interactor)target).ServerUseEnterRpc(value, value2);
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1278383090(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				ByteUnpacker.ReadValueBitPacked(reader, out uint value2);
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Interactor)target).ClientUseEnterRpc(value, value2);
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2125333280(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Interactor)target).ClientUseRequestResolvedRpc(value);
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1278739012(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Interactor)target).ServerUseExitRpc(value);
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3590544220(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Interactor)target).ClientUseExitRpc(value);
				((Interactor)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "Interactor";
		}
	}
}
