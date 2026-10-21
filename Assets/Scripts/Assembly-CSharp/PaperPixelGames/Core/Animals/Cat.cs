using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.ProcessedIngredient;
using PaperPixelGames.Core.Streaming;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Animals
{
	public class Cat : NetworkBehaviour, IBasicInteractable, IHeldItemMouse1Interactable
	{
		private enum FoodState
		{
			None = 0,
			GoingToFish = 1,
			CarryingToSleepSpot = 2,
			Eating = 3,
			FinishingEating = 4,
			SleepingAfterMeal = 5
		}

		private enum CatSfxType : byte
		{
			Sleep = 0,
			Angry = 1,
			Eat = 2,
			Pet = 3
		}

		private enum CannotPetReason : byte
		{
			Unavailable = 0,
			Sleeping = 1,
			Eating = 2,
			BusyWithFish = 3,
			Paused = 4
		}

		[Header("Ref")]
		[SerializeField]
		private Canvas _canvas;

		[SerializeField]
		private Animator _animator;

		[SerializeField]
		private NavMeshAgent _agent;

		[Header("Identity")]
		[SerializeField]
		private string _catName = "Tarcin";

		[Header("Feeding")]
		[SerializeField]
		private Transform _mouthSocket;

		[SerializeField]
		private Transform _fishEatingSpot;

		[SerializeField]
		private Transform _deadFishDetectionCenter;

		[SerializeField]
		private float _deadFishDetectionRadius = 3f;

		[SerializeField]
		private string _deadFishTag = "DeadFish";

		[SerializeField]
		private float _deadFishScanInterval = 0.5f;

		[SerializeField]
		private float _fishPickupDistance = 0.6f;

		[SerializeField]
		private float _eatDuration = 60f;

		[SerializeField]
		private float _eatExitDuration = 1f;

		[SerializeField]
		private Transform _mouthFishPose;

		[SerializeField]
		private Transform _eatingFishPose;

		[SerializeField]
		private string _eatBoolParameter = "Eat";

		[SerializeField]
		private string _scareTriggerParameter = "Scare";

		[SerializeField]
		private float _scareDeadFishCooldown = 3f;

		[SerializeField]
		private SurfaceTags _deadFishAllowedSurfaceTags = SurfaceTags.Ground | SurfaceTags.RestaurantFloor;

		[SerializeField]
		private float _deadFishSurfaceRayStartOffset = 0.25f;

		[SerializeField]
		private float _deadFishSurfaceRayDistance = 1.25f;

		[SerializeField]
		private float _deadFishMaxNavMeshHeightDelta = 0.45f;

		[Header("Patrol")]
		[SerializeField]
		private bool _patrolEnabled = true;

		[SerializeField]
		private List<Transform> _waypoints = new List<Transform>();

		[SerializeField]
		private List<Transform> _sleepWaypoints = new List<Transform>();

		[SerializeField]
		private Vector2 _idleWaitTime = new Vector2(2f, 6f);

		[SerializeField]
		private Vector2 _sleepTime = new Vector2(60f, 240f);

		[SerializeField]
		private Vector2 _sleepCooldownTime = new Vector2(120f, 300f);

		[SerializeField]
		[Range(0f, 1f)]
		private float _sleepDestinationChance = 0.25f;

		[SerializeField]
		private int _sleepAnimationCount = 3;

		[SerializeField]
		private float _destinationSampleRadius = 1.5f;

		[SerializeField]
		private int _navMeshAreaMask = -1;

		[SerializeField]
		private bool _avoidSameWaypoint = true;

		[Header("Root Motion")]
		[SerializeField]
		private bool _driveTransformFromRootMotion = true;

		[SerializeField]
		private float _arrivalDistance = 0.35f;

		[SerializeField]
		private float _petPauseTime = 2f;

		[SerializeField]
		private float _turnInputAtAngle = 70f;

		[SerializeField]
		private float _forwardWhenTurning = 0.65f;

		[SerializeField]
		private float _animatorDampTime = 0.12f;

		[SerializeField]
		private float _rootMotionNavMeshClampRadius = 0.45f;

		[Header("Animator Parameters")]
		[SerializeField]
		private string _forwardParameter = "Forward";

		[SerializeField]
		private string _turnParameter = "Turn";

		[SerializeField]
		private string _sleepParameter = "Sleep";

		[SerializeField]
		private string _sleepTypeParameter = "SleepType";

		[SerializeField]
		private string _runParameter = "Run";

		[SerializeField]
		private float _scareRunDuration = 10f;

		[Header("Testing")]
		[SerializeField]
		private bool _alwaysRunForTesting;

		[Header("Collision")]
		[SerializeField]
		private bool _ignoreResidentCollisions = true;

		[Header("Audio")]
		[SerializeField]
		private AudioClipSO _sleepSfx;

		[SerializeField]
		private AudioClipSO _angrySfx;

		[SerializeField]
		private AudioClipSO _eatSfx;

		[SerializeField]
		private AudioClipSO _petSfx;

		[SerializeField]
		private List<AudioClipSO> _meows = new List<AudioClipSO>();

		[SerializeField]
		private Vector2 _randomMeowInterval = new Vector2(15f, 40f);

		private readonly NetworkVariable<bool> _canAcceptPetNetwork = new NetworkVariable<bool>(value: true);

		private readonly NetworkVariable<bool> _canAcceptDeadFishOfferNetwork = new NetworkVariable<bool>(value: true);

		private int _forwardHash;

		private int _turnHash;

		private int _sleepHash;

		private int _sleepTypeHash;

		private int _runHash;

		private int _eatHash;

		private int _scareHash;

		private int _currentWaypointIndex = -1;

		private int _currentSleepWaypointIndex = -1;

		private float _nextDestinationTime;

		private float _aiPausedUntil;

		private float _nextDeadFishScanTime;

		private float _eatUntil;

		private float _eatExitUntil;

		private float _sleepUntil;

		private float _sleepCooldownUntil;

		private float _nextRandomMeowTime;

		private bool _randomMeowTimerStarted;

		private float _scareRunUntil;

		private bool _isRunningToFish;

		private bool _isGoingToSleep;

		private bool _isSleeping;

		private DeadFish _targetDeadFish;

		private FixedString64Bytes _carriedFishId;

		private GameObject _carriedFishGraphicPrefab;

		private bool _carriedFoodIsFillet;

		private bool _hasCarriedFish;

		private GameObject _mouthFishVisual;

		private GameObject _eatingFishVisual;

		private OwnedLoopHandle _sleepSfxLoop;

		private OwnedLoopHandle _eatSfxLoop;

		private FoodState _foodState;

		private bool _isGazedAt;

		private readonly Collider[] _deadFishHits = new Collider[16];

		private Collider[] _catColliders;

		private ResidentManager _residentManager;

		public bool CanGaze => true;

		public bool CanInteractable => true;

		private void Awake()
		{
			_forwardHash = Animator.StringToHash(_forwardParameter);
			_turnHash = Animator.StringToHash(_turnParameter);
			_sleepHash = Animator.StringToHash(_sleepParameter);
			_sleepTypeHash = Animator.StringToHash(_sleepTypeParameter);
			_runHash = ((!string.IsNullOrWhiteSpace(_runParameter) && HasAnimatorParameter(_runParameter, AnimatorControllerParameterType.Bool)) ? Animator.StringToHash(_runParameter) : 0);
			_eatHash = ((!string.IsNullOrWhiteSpace(_eatBoolParameter) && HasAnimatorParameter(_eatBoolParameter, AnimatorControllerParameterType.Bool)) ? Animator.StringToHash(_eatBoolParameter) : 0);
			_scareHash = ((!string.IsNullOrWhiteSpace(_scareTriggerParameter) && HasAnimatorParameter(_scareTriggerParameter, AnimatorControllerParameterType.Trigger)) ? Animator.StringToHash(_scareTriggerParameter) : 0);
			if (_canvas != null)
			{
				_canvas.gameObject.SetActive(value: false);
			}
			if (_animator != null)
			{
				_animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
			}
			ConfigureAgentForRootMotion();
			BindResidentCollisionRegistry();
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<bool> canAcceptPetNetwork = _canAcceptPetNetwork;
			canAcceptPetNetwork.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(canAcceptPetNetwork.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnInteractionAvailabilityChanged));
			NetworkVariable<bool> canAcceptDeadFishOfferNetwork = _canAcceptDeadFishOfferNetwork;
			canAcceptDeadFishOfferNetwork.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(canAcceptDeadFishOfferNetwork.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnInteractionAvailabilityChanged));
			ConfigureAgentForRootMotion();
			UpdateInteractionAvailability();
			QueueNextDestination();
			_randomMeowTimerStarted = false;
			BindResidentCollisionRegistry();
		}

		public override void OnNetworkDespawn()
		{
			NetworkVariable<bool> canAcceptPetNetwork = _canAcceptPetNetwork;
			canAcceptPetNetwork.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(canAcceptPetNetwork.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnInteractionAvailabilityChanged));
			NetworkVariable<bool> canAcceptDeadFishOfferNetwork = _canAcceptDeadFishOfferNetwork;
			canAcceptDeadFishOfferNetwork.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(canAcceptDeadFishOfferNetwork.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnInteractionAvailabilityChanged));
			UnsubscribeFromQuickSlotSelectionChanged();
			_randomMeowTimerStarted = false;
			StopAllLocalLoopSfx();
			UnbindResidentCollisionRegistry();
			base.OnNetworkDespawn();
		}

		private void OnEnable()
		{
			ConfigureAgentForRootMotion();
			BindResidentCollisionRegistry();
		}

		private void OnDisable()
		{
			UnsubscribeFromQuickSlotSelectionChanged();
			StopAllLocalLoopSfx();
			UnbindResidentCollisionRegistry();
		}

		private void Update()
		{
			if (ShouldRunAi())
			{
				UpdateRandomMeowScheduleState();
				UpdateInteractionAvailability();
				TickDeadFishSearch();
				if (_foodState != FoodState.None)
				{
					TickFoodRoutine();
				}
				else
				{
					TickPatrol();
				}
			}
		}

		private void OnAnimatorMove()
		{
			if (_driveTransformFromRootMotion && !(_animator == null) && ShouldRunAi())
			{
				if (Time.time < _aiPausedUntil)
				{
					SyncAgentToTransform();
					return;
				}
				Vector3 position = base.transform.position + _animator.deltaPosition;
				base.transform.position = ClampPositionToNavMesh(position);
				base.transform.rotation *= _animator.deltaRotation;
				SyncAgentToTransform();
			}
		}

		private void BindResidentCollisionRegistry()
		{
			if (!_ignoreResidentCollisions)
			{
				return;
			}
			ResidentManager instance = Singleton<ResidentManager>.Instance;
			if (!(instance == null) && !(_residentManager == instance))
			{
				UnbindResidentCollisionRegistry();
				_residentManager = instance;
				_residentManager.RuntimeResidentRegistered += IgnoreResidentCollisions;
				if (_catColliders == null)
				{
					_catColliders = GetComponentsInChildren<Collider>(includeInactive: true);
				}
				List<ResidentController> residents = _residentManager.Residents;
				for (int i = 0; i < residents.Count; i++)
				{
					IgnoreResidentCollisions(residents[i]);
				}
			}
		}

		private void UnbindResidentCollisionRegistry()
		{
			if (!(_residentManager == null))
			{
				_residentManager.RuntimeResidentRegistered -= IgnoreResidentCollisions;
				_residentManager = null;
			}
		}

		private void IgnoreResidentCollisions(ResidentController resident)
		{
			if (resident == null || _catColliders == null)
			{
				return;
			}
			Collider[] componentsInChildren = resident.GetComponentsInChildren<Collider>(includeInactive: true);
			for (int i = 0; i < _catColliders.Length; i++)
			{
				Collider collider = _catColliders[i];
				if (collider == null || collider.isTrigger)
				{
					continue;
				}
				foreach (Collider collider2 in componentsInChildren)
				{
					if (collider2 != null && !collider2.isTrigger)
					{
						Physics.IgnoreCollision(collider, collider2, ignore: true);
					}
				}
			}
		}

		public void GazeEnter()
		{
			_isGazedAt = true;
			SubscribeToQuickSlotSelectionChanged();
			ShowInteractionHint();
		}

		public void GazeExit()
		{
			_isGazedAt = false;
			UnsubscribeFromQuickSlotSelectionChanged();
			HideInteractionHint();
		}

		public void TriggerInput(Interactor interactor, InputKind inputKind, InputActionPhase phase)
		{
			if (inputKind != InputKind.Mouse1 || phase != InputActionPhase.Started)
			{
				return;
			}
			if (!CanAcceptPet())
			{
				if (!TryRequestServerPetToast())
				{
					ShowCannotPetToastLocal();
				}
			}
			else
			{
				RequestPet();
			}
		}

		public void TriggerCrossInput(Interactor interactor, BaseHoldable held, InputKind inputKind, InputActionPhase phase)
		{
			if (inputKind != InputKind.Mouse1 || phase != InputActionPhase.Started)
			{
				return;
			}
			if (held is DeadFish deadFish && interactor != null && interactor.NetworkObject != null && deadFish.NetworkObject != null)
			{
				if (!CanAcceptDeadFishOffer())
				{
					if (ShouldUseNetworkInteractionState())
					{
						ServerOfferDeadFishRpc(interactor.NetworkObject.ToReference(), deadFish.NetworkObject.ToReference());
					}
					else
					{
						ShowCannotPetToastLocal();
					}
				}
				else
				{
					ServerOfferDeadFishRpc(interactor.NetworkObject.ToReference(), deadFish.NetworkObject.ToReference());
				}
			}
			else if (held is IngredientEntity ingredientEntity && ResolveFilletFish(ingredientEntity) != null && interactor != null && interactor.NetworkObject != null && ingredientEntity.NetworkObject != null)
			{
				if (!CanAcceptDeadFishOffer())
				{
					if (ShouldUseNetworkInteractionState())
					{
						ServerOfferFilletRpc(interactor.NetworkObject.ToReference(), ingredientEntity.NetworkObject.ToReference());
					}
					else
					{
						ShowCannotPetToastLocal();
					}
				}
				else
				{
					ServerOfferFilletRpc(interactor.NetworkObject.ToReference(), ingredientEntity.NetworkObject.ToReference());
				}
			}
			else if (!CanAcceptPet())
			{
				if (!TryRequestServerPetToast())
				{
					ShowCannotPetToastLocal();
				}
			}
			else
			{
				RequestPet();
			}
		}

		private void RequestPet()
		{
			if (!CanAcceptPet())
			{
				if (!TryRequestServerPetToast())
				{
					ShowCannotPetToastLocal();
				}
			}
			else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && base.IsSpawned && !base.IsServer)
			{
				ServerPetRpc();
			}
			else
			{
				TriggerPet();
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerPetRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1042669685u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1042669685u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!CanAcceptPetLocal())
				{
					ShowCannotPetToastClientRpc(GetCannotPetReason(), base.RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
				}
				else
				{
					TriggerPet();
				}
			}
		}

		private void TriggerPet()
		{
			if (CanAcceptPetLocal())
			{
				PauseAi(_petPauseTime);
				PlayCatSfx(CatSfxType.Pet);
				if (_animator != null)
				{
					_animator.SetTrigger("Pet");
				}
			}
		}

		private void TickPatrol()
		{
			if (!_patrolEnabled || _agent == null || !_agent.enabled || _waypoints.Count == 0)
			{
				SetRun(run: false);
				SetLocomotionInput(0f, 0f);
				return;
			}
			if (!_agent.isOnNavMesh && !TrySnapAgentToNavMesh())
			{
				SetRun(run: false);
				SetLocomotionInput(0f, 0f);
				return;
			}
			SyncAgentToTransform();
			if (Time.time < _aiPausedUntil)
			{
				StopAgent();
				StopSleeping();
				SetRun(run: false);
				SetLocomotionInput(0f, 0f);
			}
			else if (_isSleeping)
			{
				StopAgent();
				SetRun(run: false);
				SetLocomotionInput(0f, 0f);
				if (Time.time >= _sleepUntil)
				{
					StopSleeping();
					QueueNextDestination();
				}
			}
			else if (HasArrived())
			{
				if (IsScareRunActive())
				{
					QueueNextDestination();
					SetRun(run: true);
					return;
				}
				StopAgent();
				SetRun(run: false);
				SetLocomotionInput(0f, 0f);
				if (_isGoingToSleep)
				{
					StartSleeping();
				}
				else if (Time.time >= _nextDestinationTime)
				{
					QueueNextDestination();
				}
			}
			else
			{
				TickRandomMeow();
				UpdateLocomotionInputFromPath();
			}
		}

		private void TickDeadFishSearch()
		{
			if (_foodState != FoodState.None || Time.time < _nextDeadFishScanTime)
			{
				return;
			}
			_nextDeadFishScanTime = Time.time + Mathf.Max(0.1f, _deadFishScanInterval);
			int num = Physics.OverlapSphereNonAlloc(((_deadFishDetectionCenter != null) ? _deadFishDetectionCenter : base.transform).position, Mathf.Max(0f, _deadFishDetectionRadius), _deadFishHits, -1, QueryTriggerInteraction.Collide);
			for (int i = 0; i < num; i++)
			{
				Collider collider = _deadFishHits[i];
				if (!IsDeadFishTagged(collider))
				{
					continue;
				}
				DeadFish deadFish = ((collider != null) ? collider.GetComponentInParent<DeadFish>() : null);
				if (!(deadFish == null) && CanClaimWorldDeadFish(deadFish))
				{
					if (IsDeadFishTooLarge(deadFish))
					{
						TriggerScare();
						_nextDeadFishScanTime = Time.time + Mathf.Max(0.1f, _scareDeadFishCooldown);
					}
					else
					{
						BeginFoodRoutine(deadFish, alreadyInMouth: false);
					}
					break;
				}
			}
		}

		private void TickFoodRoutine()
		{
			if (_agent == null || !_agent.enabled)
			{
				SetRun(run: false);
				return;
			}
			if (!_agent.isOnNavMesh && !TrySnapAgentToNavMesh())
			{
				SetRun(run: false);
				SetLocomotionInput(0f, 0f);
				return;
			}
			SyncAgentToTransform();
			switch (_foodState)
			{
			case FoodState.GoingToFish:
				TickGoingToFish();
				break;
			case FoodState.CarryingToSleepSpot:
				TickCarryingToFishSpot();
				break;
			case FoodState.Eating:
				TickEating();
				break;
			case FoodState.FinishingEating:
				TickFinishingEating();
				break;
			case FoodState.SleepingAfterMeal:
				TickSleepingAfterMeal();
				break;
			}
		}

		private void TickGoingToFish()
		{
			if (!CanClaimWorldDeadFish(_targetDeadFish))
			{
				EndFoodRoutine();
				return;
			}
			Vector3 position = _targetDeadFish.transform.position;
			if (Vector3.ProjectOnPlane(position - base.transform.position, Vector3.up).sqrMagnitude <= _fishPickupDistance * _fishPickupDistance || HasArrived())
			{
				PickUpDeadFish(_targetDeadFish, allowOfferedHeldFish: false);
				return;
			}
			if (NavMesh.SamplePosition(position, out var hit, _destinationSampleRadius, _navMeshAreaMask))
			{
				_agent.SetDestination(hit.position);
			}
			UpdateLocomotionInputFromPath();
		}

		private void TickCarryingToFishSpot()
		{
			if (!_hasCarriedFish)
			{
				EndFoodRoutine();
			}
			else if (!HasArrived())
			{
				UpdateLocomotionInputFromPath();
			}
			else
			{
				StartEating();
			}
		}

		private void TickEating()
		{
			StopAgent();
			SetRun(run: false);
			SetLocomotionInput(0f, 0f);
			if (!(Time.time < _eatUntil))
			{
				SetEating(eating: false);
				_eatExitUntil = Time.time + Mathf.Max(0f, _eatExitDuration);
				_foodState = FoodState.FinishingEating;
			}
		}

		private void TickFinishingEating()
		{
			StopAgent();
			SetRun(run: false);
			SetLocomotionInput(0f, 0f);
			if (!(Time.time < _eatExitUntil))
			{
				ConsumeCarriedFish();
				_foodState = FoodState.SleepingAfterMeal;
				StartSleeping();
			}
		}

		private void TickSleepingAfterMeal()
		{
			StopAgent();
			SetRun(run: false);
			SetLocomotionInput(0f, 0f);
			if (_isSleeping && !(Time.time < _sleepUntil))
			{
				StopSleeping();
				EndFoodRoutine();
				QueueNextDestination();
			}
		}

		private void BeginFoodRoutine(DeadFish deadFish, bool alreadyInMouth)
		{
			if (!CanTargetDeadFish(deadFish))
			{
				return;
			}
			StopSleeping();
			HideInteractionHint();
			StopAgent();
			SetLocomotionInput(0f, 0f);
			_isGoingToSleep = false;
			_targetDeadFish = deadFish;
			_hasCarriedFish = false;
			_isRunningToFish = true;
			SetRun(_isRunningToFish);
			_carriedFishId = default;
			_carriedFishGraphicPrefab = null;
			_carriedFoodIsFillet = false;
			UpdateInteractionAvailability();
			if (alreadyInMouth)
			{
				PickUpDeadFish(deadFish, allowOfferedHeldFish: true);
				return;
			}
			_foodState = FoodState.GoingToFish;
			if (NavMesh.SamplePosition(deadFish.transform.position, out var hit, _destinationSampleRadius, _navMeshAreaMask))
			{
				_agent.isStopped = false;
				_agent.SetDestination(hit.position);
			}
		}

		private void PickUpDeadFish(DeadFish deadFish, bool allowOfferedHeldFish)
		{
			if (!CanTakeDeadFish(deadFish, allowOfferedHeldFish))
			{
				EndFoodRoutine();
				return;
			}
			_targetDeadFish = null;
			TakeDeadFishToMouth(deadFish);
			GoToFishEatingSpot();
		}

		private void GoToFishEatingSpot()
		{
			_foodState = FoodState.CarryingToSleepSpot;
			if (TryGetFishEatingDestination(out var destination))
			{
				_agent.isStopped = false;
				_agent.SetDestination(destination);
			}
			else
			{
				StartEating();
			}
		}

		private void StartEating()
		{
			_foodState = FoodState.Eating;
			_isRunningToFish = false;
			_eatUntil = Time.time + Mathf.Max(0f, _eatDuration);
			StopAgent();
			SetRun(run: false);
			SetLocomotionInput(0f, 0f);
			DestroyMouthFishVisualClientRpc();
			SpawnEatingFishVisualClientRpc(_carriedFishId, _carriedFoodIsFillet);
			SetEating(eating: true);
			PlayCatSfx(CatSfxType.Eat);
		}

		private void EndFoodRoutine()
		{
			SetEating(eating: false);
			_isRunningToFish = false;
			SetRun(run: false);
			_foodState = FoodState.None;
			_targetDeadFish = null;
			_hasCarriedFish = false;
			_carriedFishId = default;
			_carriedFishGraphicPrefab = null;
			_carriedFoodIsFillet = false;
			DestroyMouthFishVisualClientRpc();
			DestroyEatingFishVisualClientRpc();
			UpdateInteractionAvailability();
		}

		private bool CanTargetDeadFish(DeadFish deadFish)
		{
			if (deadFish != null && deadFish.NetworkObject != null)
			{
				return deadFish.NetworkObject.IsSpawned;
			}
			return false;
		}

		private bool CanClaimWorldDeadFish(DeadFish deadFish)
		{
			if (!CanTargetDeadFish(deadFish) || !deadFish.CanHold.Value)
			{
				return false;
			}
			if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
			{
				return IsDeadFishOnAllowedSurface(deadFish);
			}
			if (deadFish.OwnerClientId == 0L)
			{
				return IsDeadFishOnAllowedSurface(deadFish);
			}
			return false;
		}

		private bool CanTakeDeadFish(DeadFish deadFish, bool allowOfferedHeldFish)
		{
			if (!CanTargetDeadFish(deadFish))
			{
				return false;
			}
			if (!allowOfferedHeldFish)
			{
				return CanClaimWorldDeadFish(deadFish);
			}
			return true;
		}

		private bool IsDeadFishTooLarge(DeadFish deadFish)
		{
			return IsFishTooLarge(ResolveDeadFishSO(deadFish));
		}

		private static bool IsFishTooLarge(FishSO fishSO)
		{
			if (fishSO != null)
			{
				return (int)fishSO.FurnitureWeight > 3;
			}
			return false;
		}

		private bool IsDeadFishOnAllowedSurface(DeadFish deadFish)
		{
			if (deadFish == null)
			{
				return false;
			}
			RaycastHit[] array = Physics.RaycastAll(GetDeadFishSurfaceCheckOrigin(deadFish) + Vector3.up * Mathf.Max(0f, _deadFishSurfaceRayStartOffset), Vector3.down, Mathf.Max(0.01f, _deadFishSurfaceRayDistance), -1, QueryTriggerInteraction.Ignore);
			Array.Sort(array, (RaycastHit left, RaycastHit right) => left.distance.CompareTo(right.distance));
			for (int num = 0; num < array.Length; num++)
			{
				Collider collider = array[num].collider;
				if (!(collider == null) && !collider.transform.IsChildOf(deadFish.transform))
				{
					PlacementSurface componentInParent = collider.GetComponentInParent<PlacementSurface>();
					if (!(componentInParent != null))
					{
						break;
					}
					return (componentInParent.Tags & _deadFishAllowedSurfaceTags) != 0;
				}
			}
			return IsDeadFishCloseToNavMeshGround(deadFish.transform.position);
		}

		private Vector3 GetDeadFishSurfaceCheckOrigin(DeadFish deadFish)
		{
			Collider[] componentsInChildren = deadFish.GetComponentsInChildren<Collider>(includeInactive: true);
			Bounds bounds = default;
			bool flag = false;
			foreach (Collider collider in componentsInChildren)
			{
				if (!(collider == null) && collider.enabled && !collider.isTrigger)
				{
					if (!flag)
					{
						bounds = collider.bounds;
						flag = true;
					}
					else
					{
						bounds.Encapsulate(collider.bounds);
					}
				}
			}
			if (!flag)
			{
				return deadFish.transform.position;
			}
			return new Vector3(bounds.center.x, bounds.min.y + 0.02f, bounds.center.z);
		}

		private bool IsDeadFishCloseToNavMeshGround(Vector3 fishPosition)
		{
			float num = Mathf.Max(0f, _deadFishMaxNavMeshHeightDelta);
			if (!NavMesh.SamplePosition(fishPosition, out var hit, num, _navMeshAreaMask))
			{
				return false;
			}
			return Mathf.Abs(fishPosition.y - hit.position.y) <= num;
		}

		private FishSO ResolveDeadFishSO(DeadFish deadFish)
		{
			if (deadFish == null)
			{
				return null;
			}
			if (deadFish.FishSO != null)
			{
				return deadFish.FishSO;
			}
			FixedString64Bytes deadFishId = GetDeadFishId(deadFish);
			if (deadFishId.Length <= 0)
			{
				return null;
			}
			return ResolveFish(deadFishId);
		}

		private void TriggerScare()
		{
			_scareRunUntil = Time.time + Mathf.Max(0f, _scareRunDuration);
			_isGoingToSleep = false;
			StopSleeping();
			QueueRunDestination();
			SetRun(run: true);
			if (_animator != null && _scareHash != 0)
			{
				_animator.SetTrigger(_scareHash);
			}
			PlayCatSfx(CatSfxType.Angry);
		}

		private void TakeDeadFishToMouth(DeadFish deadFish)
		{
			if (deadFish == null || deadFish.NetworkObject == null)
			{
				return;
			}
			FixedString64Bytes deadFishId = GetDeadFishId(deadFish);
			if (deadFishId.Length == 0)
			{
				EndFoodRoutine();
				return;
			}
			_carriedFishId = deadFishId;
			_carriedFishGraphicPrefab = deadFish.DeadGraphic;
			_carriedFoodIsFillet = false;
			_hasCarriedFish = true;
			deadFish.CanHold.Value = false;
			deadFish.CanGaze.Value = false;
			deadFish.CollidersStatus(status: false);
			deadFish.ObjectsStatus(status: true);
			SpawnMouthFishVisualClientRpc(deadFishId, isFillet: false);
			if (deadFish.NetworkObject.IsSpawned)
			{
				deadFish.NetworkObject.Despawn();
			}
			else if (deadFish.gameObject != null)
			{
				UnityEngine.Object.Destroy(deadFish.gameObject);
			}
		}

		private void ConsumeCarriedFish()
		{
			_hasCarriedFish = false;
			_carriedFishId = default;
			_carriedFishGraphicPrefab = null;
			_carriedFoodIsFillet = false;
			DestroyEatingFishVisualClientRpc();
		}

		[Rpc(SendTo.Server)]
		private void ServerOfferDeadFishRpc(NetworkObjectReference interactorRef, NetworkObjectReference deadFishRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(817462004u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in deadFishRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 817462004u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!CanAcceptDeadFishOfferLocal())
			{
				ShowCannotPetToastClientRpc(GetCannotPetReason(), base.RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
			}
			else
			{
				if (!interactorRef.TryGet(out var networkObject) || !networkObject.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != rpcParams.Receive.SenderClientId || !deadFishRef.TryGet(out var networkObject2) || !networkObject2.TryGetComponent<DeadFish>(out var component2) || component2.OwnerClientId != rpcParams.Receive.SenderClientId || component2.CanHold.Value)
				{
					return;
				}
				if (IsDeadFishTooLarge(component2))
				{
					TriggerScare();
					return;
				}
				RemoveOfferedFoodFromQuickSlotClientRpc(deadFishRef, base.RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
				BeginFoodRoutine(component2, alreadyInMouth: true);
				if (_hasCarriedFish)
				{
					Singleton<GameProgressManager>.Instance?.RpcRelay?.ServerUnlockSteamAchievementForClient("ACH_CAT_TAX", rpcParams.Receive.SenderClientId);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerOfferFilletRpc(NetworkObjectReference interactorRef, NetworkObjectReference filletRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(100572627u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in filletRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 100572627u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!CanAcceptDeadFishOfferLocal())
			{
				ShowCannotPetToastClientRpc(GetCannotPetReason(), base.RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
			}
			else
			{
				if (!interactorRef.TryGet(out var networkObject) || !networkObject.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != rpcParams.Receive.SenderClientId || !filletRef.TryGet(out var networkObject2) || !networkObject2.TryGetComponent<IngredientEntity>(out var component2) || component2.OwnerClientId != rpcParams.Receive.SenderClientId || component2.CanHold.Value)
				{
					return;
				}
				FishSO fishSO = ResolveFilletFish(component2);
				if (!(fishSO == null))
				{
					RemoveOfferedFoodFromQuickSlotClientRpc(filletRef, base.RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
					BeginFilletFoodRoutine(component2, fishSO);
					if (_hasCarriedFish)
					{
						Singleton<GameProgressManager>.Instance?.RpcRelay?.ServerUnlockSteamAchievementForClient("ACH_CAT_TAX", rpcParams.Receive.SenderClientId);
					}
				}
			}
		}

		private void BeginFilletFoodRoutine(IngredientEntity fillet, FishSO fishSO)
		{
			if (!(fillet == null) && !(fillet.NetworkObject == null) && !(fishSO == null))
			{
				StopSleeping();
				HideInteractionHint();
				StopAgent();
				SetLocomotionInput(0f, 0f);
				_isGoingToSleep = false;
				_targetDeadFish = null;
				_isRunningToFish = true;
				SetRun(run: true);
				_carriedFishId = new FixedString64Bytes(fishSO.Id);
				_carriedFishGraphicPrefab = ((fillet.ItemSO != null) ? fillet.ItemSO.GraphicObjectPrefab : null);
				_carriedFoodIsFillet = true;
				_hasCarriedFish = true;
				UpdateInteractionAvailability();
				SpawnMouthFishVisualClientRpc(_carriedFishId, isFillet: true);
				if (fillet.NetworkObject.IsSpawned)
				{
					fillet.NetworkObject.Despawn();
				}
				else if (fillet.gameObject != null)
				{
					UnityEngine.Object.Destroy(fillet.gameObject);
				}
				GoToFishEatingSpot();
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void RemoveOfferedFoodFromQuickSlotClientRpc(NetworkObjectReference foodRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1045636082u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in foodRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 1045636082u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (foodRef.TryGetComponent<BaseHoldable>(out var component))
				{
					Singleton<QuickSlotHandler>.Instance?.RemoveSpecificItem(component);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void SpawnMouthFishVisualClientRpc(FixedString64Bytes fishId, bool isFillet)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2263780065u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in fishId, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in isFillet, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2263780065u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Transform parent = ((_mouthSocket != null) ? _mouthSocket : base.transform);
				_mouthFishVisual = SpawnFishVisual(fishId, isFillet, parent, _mouthFishPose, _mouthFishVisual);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void DestroyMouthFishVisualClientRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3825406955u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3825406955u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				DestroyFishVisual(ref _mouthFishVisual);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void SpawnEatingFishVisualClientRpc(FixedString64Bytes fishId, bool isFillet)
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
				FastBufferWriter bufferWriter = __beginSendRpc(423355213u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in fishId, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in isFillet, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 423355213u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Transform parent = ((_fishEatingSpot != null) ? _fishEatingSpot : base.transform);
				_eatingFishVisual = SpawnFishVisual(fishId, isFillet, parent, null, _eatingFishVisual);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void DestroyEatingFishVisualClientRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1171035512u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1171035512u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				DestroyFishVisual(ref _eatingFishVisual);
			}
		}

		private GameObject SpawnFishVisual(FixedString64Bytes fishId, bool isFillet, Transform parent, Transform localPose, GameObject currentVisual)
		{
			if (currentVisual != null)
			{
				UnityEngine.Object.Destroy(currentVisual);
			}
			FishSO fishSO = ResolveFish(fishId);
			GameObject gameObject = ((!isFillet) ? fishSO?.DeadGraphic : fishSO?.IngredientSO?.GraphicObjectPrefab);
			if ((object)gameObject == null)
			{
				gameObject = _carriedFishGraphicPrefab;
			}
			if (gameObject == null)
			{
				return null;
			}
			GameObject gameObject2 = ((parent != null) ? UnityEngine.Object.Instantiate(gameObject, parent) : UnityEngine.Object.Instantiate(gameObject));
			ApplyLocalPose(gameObject2.transform, localPose);
			Collider[] componentsInChildren = gameObject2.GetComponentsInChildren<Collider>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].enabled = false;
			}
			return gameObject2;
		}

		private void ApplyLocalPose(Transform target, Transform localPose)
		{
			if (!(target == null))
			{
				if (localPose == null)
				{
					target.localPosition = Vector3.zero;
					target.localRotation = Quaternion.identity;
				}
				else
				{
					target.localPosition = localPose.localPosition;
					target.localRotation = localPose.localRotation;
				}
			}
		}

		private void DestroyFishVisual(ref GameObject visual)
		{
			if (visual != null)
			{
				UnityEngine.Object.Destroy(visual);
			}
			visual = null;
		}

		private FishSO ResolveFish(FixedString64Bytes fishId)
		{
			if (fishId.Length == 0 || Singleton<SeaCreatureManager>.Instance == null)
			{
				return null;
			}
			return Singleton<SeaCreatureManager>.Instance.GetFishById(fishId.ToString());
		}

		private FishSO ResolveFilletFish(IngredientEntity fillet)
		{
			IngredientSO ingredientSO = ((fillet != null) ? fillet.IngredientSO : null);
			ItemDatabaseSO itemDatabaseSO = ((Singleton<SeaCreatureManager>.Instance != null) ? Singleton<SeaCreatureManager>.Instance.ItemDatabase : null);
			if (ingredientSO == null || itemDatabaseSO?.CreatureItems == null)
			{
				return null;
			}
			for (int i = 0; i < itemDatabaseSO.CreatureItems.Count; i++)
			{
				if (itemDatabaseSO.CreatureItems[i] is FishSO fishSO && fishSO.IngredientSO == ingredientSO)
				{
					return fishSO;
				}
			}
			return null;
		}

		private FixedString64Bytes GetDeadFishId(DeadFish deadFish)
		{
			if (deadFish == null)
			{
				return default;
			}
			if (deadFish.FishId.Value.Length > 0)
			{
				return deadFish.FishId.Value;
			}
			if (!(deadFish.FishSO != null))
			{
				return default;
			}
			return new FixedString64Bytes(deadFish.FishSO.Id);
		}

		private bool TryGetFishEatingDestination(out Vector3 destination)
		{
			destination = base.transform.position;
			if (_fishEatingSpot == null)
			{
				if (_sleepWaypoints.Count > 0)
				{
					return TryGetRandomWaypointDestination(_sleepWaypoints, ref _currentSleepWaypointIndex, out destination);
				}
				return false;
			}
			if (!NavMesh.SamplePosition(_fishEatingSpot.position, out var hit, _destinationSampleRadius, _navMeshAreaMask))
			{
				return false;
			}
			destination = hit.position;
			return true;
		}

		private void UpdateLocomotionInputFromPath()
		{
			Vector3 vector = _agent.steeringTarget;
			if ((vector - base.transform.position).sqrMagnitude < 0.01f)
			{
				vector = _agent.destination;
			}
			Vector3 vector2 = Vector3.ProjectOnPlane(vector - base.transform.position, Vector3.up);
			if (vector2.sqrMagnitude < 0.0001f)
			{
				SetRun(ShouldUseRunLocomotion());
				SetLocomotionInput(0f, 0f);
				return;
			}
			float num = Mathf.Clamp(base.transform.InverseTransformDirection(vector2.normalized).x / Mathf.Max(1f, _turnInputAtAngle / 90f), -1f, 1f);
			float forward = Mathf.Lerp(1f, Mathf.Clamp01(_forwardWhenTurning), Mathf.Abs(num));
			SetRun(ShouldUseRunLocomotion());
			SetLocomotionInput(forward, num);
		}

		private void QueueNextDestination()
		{
			_isGoingToSleep = false;
			_nextDestinationTime = Time.time + UnityEngine.Random.Range(_idleWaitTime.x, _idleWaitTime.y);
			if (!(_agent == null) && _agent.enabled && (_waypoints.Count != 0 || _sleepWaypoints.Count != 0) && (_agent.isOnNavMesh || TrySnapAgentToNavMesh()))
			{
				Vector3 destination2;
				if (_sleepWaypoints.Count > 0 && Time.time >= _sleepCooldownUntil && UnityEngine.Random.value <= _sleepDestinationChance && TryGetRandomWaypointDestination(_sleepWaypoints, ref _currentSleepWaypointIndex, out var destination))
				{
					_isGoingToSleep = true;
					_agent.isStopped = false;
					_agent.SetDestination(destination);
				}
				else if (TryGetRandomWaypointDestination(_waypoints, ref _currentWaypointIndex, out destination2))
				{
					_agent.isStopped = false;
					_agent.SetDestination(destination2);
				}
			}
		}

		private void QueueRunDestination()
		{
			if (!(_agent == null) && _agent.enabled && _waypoints.Count != 0 && (_agent.isOnNavMesh || TrySnapAgentToNavMesh()) && TryGetRandomWaypointDestination(_waypoints, ref _currentWaypointIndex, out var destination))
			{
				_isGoingToSleep = false;
				_agent.isStopped = false;
				_agent.SetDestination(destination);
			}
		}

		private bool TryGetRandomWaypointDestination(List<Transform> waypoints, ref int currentIndex, out Vector3 destination)
		{
			destination = base.transform.position;
			if (waypoints.Count == 0)
			{
				return false;
			}
			int num = UnityEngine.Random.Range(0, waypoints.Count);
			for (int i = 0; i < waypoints.Count; i++)
			{
				int num2 = (num + i) % waypoints.Count;
				Transform transform = waypoints[num2];
				if (!(transform == null) && (!_avoidSameWaypoint || waypoints.Count <= 1 || num2 != currentIndex) && NavMesh.SamplePosition(transform.position, out var hit, _destinationSampleRadius, _navMeshAreaMask))
				{
					currentIndex = num2;
					destination = hit.position;
					return true;
				}
			}
			return false;
		}

		private bool IsDeadFishTagged(Collider hit)
		{
			if (hit == null || string.IsNullOrWhiteSpace(_deadFishTag))
			{
				return false;
			}
			if (hit.CompareTag(_deadFishTag))
			{
				return true;
			}
			Transform parent = hit.transform.parent;
			while (parent != null)
			{
				if (parent.CompareTag(_deadFishTag))
				{
					return true;
				}
				parent = parent.parent;
			}
			return false;
		}

		private void SetLocomotionInput(float forward, float turn)
		{
			if (!(_animator == null))
			{
				_animator.SetFloat(_forwardHash, forward, _animatorDampTime, Time.deltaTime);
				_animator.SetFloat(_turnHash, turn, _animatorDampTime, Time.deltaTime);
			}
		}

		private void SetRun(bool run)
		{
			if (!(_animator == null) && _runHash != 0)
			{
				_animator.SetBool(_runHash, run || _alwaysRunForTesting);
			}
		}

		private bool ShouldUseRunLocomotion()
		{
			if (!_isRunningToFish)
			{
				return IsScareRunActive();
			}
			return true;
		}

		private bool IsScareRunActive()
		{
			return Time.time < _scareRunUntil;
		}

		private void TickRandomMeow()
		{
			if (_randomMeowTimerStarted && _meows.Count != 0 && !(Time.time < _nextRandomMeowTime))
			{
				PlayMeow(UnityEngine.Random.Range(0, _meows.Count));
				ScheduleNextRandomMeow();
			}
		}

		private void UpdateRandomMeowScheduleState()
		{
			if (_meows.Count != 0)
			{
				if (Singleton<DressingSceneManager>.TryGetInstance(out var instance) && instance.IsStartupLoading)
				{
					_randomMeowTimerStarted = false;
				}
				else if (!_randomMeowTimerStarted)
				{
					ScheduleNextRandomMeow();
					_randomMeowTimerStarted = true;
				}
			}
		}

		private void ScheduleNextRandomMeow()
		{
			float num = Mathf.Max(0.1f, Mathf.Min(_randomMeowInterval.x, _randomMeowInterval.y));
			float maxInclusive = Mathf.Max(num, Mathf.Max(_randomMeowInterval.x, _randomMeowInterval.y));
			_nextRandomMeowTime = Time.time + UnityEngine.Random.Range(num, maxInclusive);
		}

		private void PlayCatSfx(CatSfxType sfxType)
		{
			if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && base.IsSpawned)
			{
				PlayCatSfxClientRpc((byte)sfxType);
			}
			else
			{
				PlayLocalCatSfx(sfxType);
			}
		}

		private void StopCatSfx(CatSfxType sfxType)
		{
			if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && base.IsSpawned)
			{
				StopCatSfxClientRpc((byte)sfxType);
			}
			else
			{
				StopLocalCatSfx(sfxType);
			}
		}

		private void PlayMeow(int clipIndex)
		{
			if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && base.IsSpawned)
			{
				PlayMeowClientRpc(clipIndex);
			}
			else if (clipIndex >= 0 && clipIndex < _meows.Count)
			{
				PlayLocalCatSfx(_meows[clipIndex]);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void PlayCatSfxClientRpc(byte sfxType)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2653075207u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in sfxType, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2653075207u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				PlayLocalCatSfx((CatSfxType)sfxType);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void StopCatSfxClientRpc(byte sfxType)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2482842086u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in sfxType, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2482842086u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				StopLocalCatSfx((CatSfxType)sfxType);
			}
		}

		private AudioClipSO GetCatSfx(CatSfxType sfxType)
		{
			return sfxType switch
			{
				CatSfxType.Sleep => _sleepSfx, 
				CatSfxType.Angry => _angrySfx, 
				CatSfxType.Eat => _eatSfx, 
				CatSfxType.Pet => _petSfx, 
				_ => null, 
			};
		}

		[Rpc(SendTo.Everyone)]
		private void PlayMeowClientRpc(int clipIndex)
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
				FastBufferWriter bufferWriter = __beginSendRpc(374293698u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, clipIndex);
				__endSendRpc(ref bufferWriter, 374293698u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (clipIndex >= 0 && clipIndex < _meows.Count)
				{
					PlayLocalCatSfx(_meows[clipIndex]);
				}
			}
		}

		private void PlayLocalCatSfx(AudioClipSO clip)
		{
			if (!(clip == null) && !(Singleton<SoundManager>.Instance == null))
			{
				Singleton<SoundManager>.Instance.PlaySFX(clip, base.transform.position, base.transform);
			}
		}

		private void PlayLocalCatSfx(CatSfxType sfxType)
		{
			if (sfxType != CatSfxType.Sleep && sfxType != CatSfxType.Eat)
			{
				PlayLocalCatSfx(GetCatSfx(sfxType));
				return;
			}
			OwnedLoopHandle ownedLoopHandle = PlayLocalCatOwnedLoop(GetCatSfx(sfxType), sfxType);
			if (ownedLoopHandle != null)
			{
				switch (sfxType)
				{
				case CatSfxType.Sleep:
					StopLocalCatSfx(CatSfxType.Sleep);
					_sleepSfxLoop = ownedLoopHandle;
					break;
				case CatSfxType.Eat:
					StopLocalCatSfx(CatSfxType.Eat);
					_eatSfxLoop = ownedLoopHandle;
					break;
				}
			}
		}

		private OwnedLoopHandle PlayLocalCatOwnedLoop(AudioClipSO clip, CatSfxType sfxType)
		{
			if (clip == null || Singleton<SoundManager>.Instance == null)
			{
				return null;
			}
			return Singleton<SoundManager>.Instance.PlayOwnedLoop(clip, base.transform.position, base.transform, $"Cat-{base.NetworkObjectId}-{sfxType}");
		}

		private void StopLocalCatSfx(CatSfxType sfxType)
		{
			switch (sfxType)
			{
			case CatSfxType.Sleep:
				StopLocalLoop(ref _sleepSfxLoop);
				break;
			case CatSfxType.Eat:
				StopLocalLoop(ref _eatSfxLoop);
				break;
			}
		}

		private void StopLocalLoop(ref OwnedLoopHandle loop)
		{
			if (loop != null)
			{
				Singleton<SoundManager>.Instance?.ReleaseOwnedLoop(loop);
				loop = null;
			}
		}

		private void StopAllLocalLoopSfx()
		{
			StopLocalLoop(ref _sleepSfxLoop);
			StopLocalLoop(ref _eatSfxLoop);
		}

		private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
		{
			if (_animator == null)
			{
				return false;
			}
			AnimatorControllerParameter[] parameters = _animator.parameters;
			foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
			{
				if (animatorControllerParameter.type == parameterType && animatorControllerParameter.name == parameterName)
				{
					return true;
				}
			}
			return false;
		}

		private bool CanAcceptPet()
		{
			if (ShouldUseNetworkInteractionState())
			{
				return _canAcceptPetNetwork.Value;
			}
			return CanAcceptPetLocal();
		}

		private bool CanAcceptDeadFishOffer()
		{
			if (ShouldUseNetworkInteractionState())
			{
				return _canAcceptDeadFishOfferNetwork.Value;
			}
			return CanAcceptDeadFishOfferLocal();
		}

		private bool CanAcceptPetLocal()
		{
			if (_foodState == FoodState.None && !_isSleeping)
			{
				return Time.time >= _aiPausedUntil;
			}
			return false;
		}

		private bool CanAcceptDeadFishOfferLocal()
		{
			if (_foodState == FoodState.None)
			{
				return !_isSleeping;
			}
			return false;
		}

		private bool ShouldUseNetworkInteractionState()
		{
			if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && base.IsSpawned)
			{
				return !base.IsServer;
			}
			return false;
		}

		private bool TryRequestServerPetToast()
		{
			if (!ShouldUseNetworkInteractionState())
			{
				return false;
			}
			ServerPetRpc();
			return true;
		}

		private void UpdateInteractionAvailability()
		{
			if (!(NetworkManager.Singleton != null) || !NetworkManager.Singleton.IsListening || base.IsServer)
			{
				bool flag = CanAcceptPetLocal();
				if (_canAcceptPetNetwork.Value != flag)
				{
					_canAcceptPetNetwork.Value = flag;
				}
				bool flag2 = CanAcceptDeadFishOfferLocal();
				if (_canAcceptDeadFishOfferNetwork.Value != flag2)
				{
					_canAcceptDeadFishOfferNetwork.Value = flag2;
				}
			}
		}

		private void OnInteractionAvailabilityChanged(bool previousValue, bool newValue)
		{
			if (_isGazedAt)
			{
				ShowInteractionHint();
			}
		}

		private void ShowInteractionHint()
		{
			if (_canvas != null)
			{
				_canvas.gameObject.SetActive(value: true);
			}
			BaseHoldable baseHoldable = Singleton<QuickSlotHandler>.Instance?.SelectedItem;
			string locKey = ((baseHoldable is DeadFish || (baseHoldable is IngredientEntity fillet && ResolveFilletFish(fillet) != null)) ? "key_give_fish" : "key_pet");
			Singleton<HintsManager>.Instance?.AddHint("LeftMouseMechanic", locKey);
		}

		private void HideInteractionHint()
		{
			if (_canvas != null)
			{
				_canvas.gameObject.SetActive(value: false);
			}
			Singleton<HintsManager>.Instance?.RemoveHint("LeftMouseMechanic");
		}

		private void SubscribeToQuickSlotSelectionChanged()
		{
			if (!(Singleton<QuickSlotHandler>.Instance == null))
			{
				Singleton<QuickSlotHandler>.Instance.SelectedItemChanged -= OnQuickSlotSelectedItemChanged;
				Singleton<QuickSlotHandler>.Instance.SelectedItemChanged += OnQuickSlotSelectedItemChanged;
			}
		}

		private void UnsubscribeFromQuickSlotSelectionChanged()
		{
			if (!(Singleton<QuickSlotHandler>.Instance == null))
			{
				Singleton<QuickSlotHandler>.Instance.SelectedItemChanged -= OnQuickSlotSelectedItemChanged;
			}
		}

		private void OnQuickSlotSelectedItemChanged(BaseHoldable selectedItem)
		{
			if (_isGazedAt)
			{
				ShowInteractionHint();
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ShowCannotPetToastClientRpc(byte reason, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2177440017u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in reason, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2177440017u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ShowCannotPetToastLocal((CannotPetReason)reason);
			}
		}

		private void ShowCannotPetToastLocal()
		{
			ShowCannotPetToastLocal((CannotPetReason)GetCannotPetReason());
		}

		private void ShowCannotPetToastLocal(CannotPetReason reason)
		{
			string text = (string.IsNullOrWhiteSpace(_catName) ? base.name : _catName);
			string message = reason switch
			{
				CannotPetReason.Sleeping => text + " is sleeping.", 
				CannotPetReason.Eating => text + " is eating.", 
				CannotPetReason.BusyWithFish => text + " is focused on the fish.", 
				CannotPetReason.Paused => text + " is already being petted.", 
				_ => text + " cannot be petted right now.", 
			};
			Singleton<NotifyManager>.Instance?.ShowToast(message, NotificationState.Info, 1.5f);
		}

		private byte GetCannotPetReason()
		{
			if (_isSleeping || _foodState == FoodState.SleepingAfterMeal)
			{
				return 1;
			}
			if (_foodState == FoodState.Eating || _foodState == FoodState.FinishingEating)
			{
				return 2;
			}
			if (_foodState == FoodState.GoingToFish || _foodState == FoodState.CarryingToSleepSpot)
			{
				return 3;
			}
			if (Time.time < _aiPausedUntil)
			{
				return 4;
			}
			return 0;
		}

		private bool HasArrived()
		{
			if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh)
			{
				return true;
			}
			if (_agent.pathPending)
			{
				return false;
			}
			return _agent.remainingDistance <= Mathf.Max(_agent.stoppingDistance, _arrivalDistance);
		}

		private void PauseAi(float duration)
		{
			_aiPausedUntil = Mathf.Max(_aiPausedUntil, Time.time + Mathf.Max(0f, duration));
			StopSleeping();
			StopAgent();
			SetRun(run: false);
			SetLocomotionInput(0f, 0f);
			UpdateInteractionAvailability();
		}

		private void StartSleeping()
		{
			_isGoingToSleep = false;
			_isSleeping = true;
			HideInteractionHint();
			SetRun(run: false);
			_sleepUntil = Time.time + UnityEngine.Random.Range(_sleepTime.x, _sleepTime.y);
			_sleepCooldownUntil = _sleepUntil + UnityEngine.Random.Range(_sleepCooldownTime.x, _sleepCooldownTime.y);
			UpdateInteractionAvailability();
			if (!(_animator == null))
			{
				_animator.SetInteger(_sleepTypeHash, UnityEngine.Random.Range(0, Mathf.Max(1, _sleepAnimationCount)));
				_animator.SetBool(_sleepHash, value: true);
				PlayCatSfx(CatSfxType.Sleep);
			}
		}

		private void StopSleeping()
		{
			bool isSleeping = _isSleeping;
			_isGoingToSleep = false;
			_isSleeping = false;
			if (_animator != null)
			{
				_animator.SetBool(_sleepHash, value: false);
			}
			if (isSleeping)
			{
				StopCatSfx(CatSfxType.Sleep);
			}
			UpdateInteractionAvailability();
		}

		private void SetEating(bool eating)
		{
			if (!eating)
			{
				StopCatSfx(CatSfxType.Eat);
			}
			if (!(_animator == null) && _eatHash != 0)
			{
				_animator.SetBool(_eatHash, eating);
				UpdateInteractionAvailability();
			}
		}

		private void StopAgent()
		{
			if (!(_agent == null) && _agent.enabled && _agent.isOnNavMesh)
			{
				_agent.isStopped = true;
				_agent.ResetPath();
			}
		}

		private void ConfigureAgentForRootMotion()
		{
			if (!(_agent == null))
			{
				_agent.updatePosition = !_driveTransformFromRootMotion;
				_agent.updateRotation = !_driveTransformFromRootMotion;
				if (_driveTransformFromRootMotion)
				{
					SyncAgentToTransform();
				}
			}
		}

		private bool TrySnapAgentToNavMesh()
		{
			if (_agent == null)
			{
				return false;
			}
			if (!NavMesh.SamplePosition(base.transform.position, out var hit, _destinationSampleRadius, _navMeshAreaMask))
			{
				return false;
			}
			_agent.Warp(hit.position);
			base.transform.position = hit.position;
			return true;
		}

		private Vector3 ClampPositionToNavMesh(Vector3 position)
		{
			if (_agent == null || !_agent.enabled)
			{
				return position;
			}
			float maxDistance = Mathf.Max(_rootMotionNavMeshClampRadius, _agent.radius, 0.05f);
			if (NavMesh.SamplePosition(position, out var hit, maxDistance, _navMeshAreaMask))
			{
				return hit.position;
			}
			if (_agent.isOnNavMesh)
			{
				return _agent.nextPosition;
			}
			return base.transform.position;
		}

		private void SyncAgentToTransform()
		{
			if (!(_agent == null) && _agent.enabled && _agent.isOnNavMesh)
			{
				_agent.nextPosition = base.transform.position;
			}
		}

		private bool ShouldRunAi()
		{
			if (!Application.isPlaying)
			{
				return false;
			}
			if (!(NetworkManager.Singleton == null) && NetworkManager.Singleton.IsListening)
			{
				return base.IsServer;
			}
			return true;
		}

		private void OnDrawGizmosSelected()
		{
			Transform obj = ((_deadFishDetectionCenter != null) ? _deadFishDetectionCenter : base.transform);
			Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.2f);
			Gizmos.DrawSphere(obj.position, Mathf.Max(0f, _deadFishDetectionRadius));
			Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.9f);
			Gizmos.DrawWireSphere(obj.position, Mathf.Max(0f, _deadFishDetectionRadius));
			if (!(_fishEatingSpot == null))
			{
				Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.9f);
				Gizmos.DrawSphere(_fishEatingSpot.position, 0.12f);
				Gizmos.DrawLine(base.transform.position, _fishEatingSpot.position);
			}
		}

		protected override void __initializeVariables()
		{
			if (_canAcceptPetNetwork == null)
			{
				throw new Exception("Cat._canAcceptPetNetwork cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_canAcceptPetNetwork.Initialize(this);
			__nameNetworkVariable(_canAcceptPetNetwork, "_canAcceptPetNetwork");
			NetworkVariableFields.Add(_canAcceptPetNetwork);
			if (_canAcceptDeadFishOfferNetwork == null)
			{
				throw new Exception("Cat._canAcceptDeadFishOfferNetwork cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_canAcceptDeadFishOfferNetwork.Initialize(this);
			__nameNetworkVariable(_canAcceptDeadFishOfferNetwork, "_canAcceptDeadFishOfferNetwork");
			NetworkVariableFields.Add(_canAcceptDeadFishOfferNetwork);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(1042669685u, __rpc_handler_1042669685, "ServerPetRpc", RpcInvokePermission.Everyone);
			__registerRpc(817462004u, __rpc_handler_817462004, "ServerOfferDeadFishRpc", RpcInvokePermission.Everyone);
			__registerRpc(100572627u, __rpc_handler_100572627, "ServerOfferFilletRpc", RpcInvokePermission.Everyone);
			__registerRpc(1045636082u, __rpc_handler_1045636082, "RemoveOfferedFoodFromQuickSlotClientRpc", RpcInvokePermission.Everyone);
			__registerRpc(2263780065u, __rpc_handler_2263780065, "SpawnMouthFishVisualClientRpc", RpcInvokePermission.Everyone);
			__registerRpc(3825406955u, __rpc_handler_3825406955, "DestroyMouthFishVisualClientRpc", RpcInvokePermission.Everyone);
			__registerRpc(423355213u, __rpc_handler_423355213, "SpawnEatingFishVisualClientRpc", RpcInvokePermission.Everyone);
			__registerRpc(1171035512u, __rpc_handler_1171035512, "DestroyEatingFishVisualClientRpc", RpcInvokePermission.Everyone);
			__registerRpc(2653075207u, __rpc_handler_2653075207, "PlayCatSfxClientRpc", RpcInvokePermission.Everyone);
			__registerRpc(2482842086u, __rpc_handler_2482842086, "StopCatSfxClientRpc", RpcInvokePermission.Everyone);
			__registerRpc(374293698u, __rpc_handler_374293698, "PlayMeowClientRpc", RpcInvokePermission.Everyone);
			__registerRpc(2177440017u, __rpc_handler_2177440017, "ShowCannotPetToastClientRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_1042669685(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Cat)target).ServerPetRpc(ext);
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_817462004(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Cat)target).ServerOfferDeadFishRpc(value, value2, ext);
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_100572627(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Cat)target).ServerOfferFilletRpc(value, value2, ext);
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1045636082(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Cat)target).RemoveOfferedFoodFromQuickSlotClientRpc(value, ext);
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2263780065(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Cat)target).SpawnMouthFishVisualClientRpc(value, value2);
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3825406955(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Cat)target).DestroyMouthFishVisualClientRpc();
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_423355213(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Cat)target).SpawnEatingFishVisualClientRpc(value, value2);
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1171035512(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Cat)target).DestroyEatingFishVisualClientRpc();
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2653075207(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Cat)target).PlayCatSfxClientRpc(value);
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2482842086(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Cat)target).StopCatSfxClientRpc(value);
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_374293698(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Cat)target).PlayMeowClientRpc(value);
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2177440017(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Cat)target).ShowCannotPetToastClientRpc(value, ext);
				((Cat)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "Cat";
		}
	}
}
