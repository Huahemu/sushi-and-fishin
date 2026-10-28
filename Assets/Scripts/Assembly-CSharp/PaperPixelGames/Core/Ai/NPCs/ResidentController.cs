using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bozo.ModularCharacters;
using PaperPixelGames.Core.Data.Ai.NPCs;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Interaction.ToolTip;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public class ResidentController : BaseInteractable
	{
		[Header("Resident")]
		[SerializeField]
		private NavMeshAgent _agent;

		[SerializeField]
		private Animator _animator;

		[SerializeField]
		private ResidentDataSO _residentData;

		[SerializeField]
		private GameObject _model;

		[SerializeField]
		private ActorType _actorType;

		[Header("Interaction")]
		[SerializeField]
		private ResidentBillboardText _residentBillboardText;

		[Header("Restaurant Props")]
		[SerializeField]
		private Transform _restaurantPropAnchor;

		[SerializeField]
		private GameObject _menuPropPrefab;

		[SerializeField]
		private List<GameObject> _chopstickPropPrefabs = new List<GameObject>();

		[Header("Menu Prop Transform")]
		[FormerlySerializedAs("_restaurantPropLocalPosition")]
		[SerializeField]
		private Vector3 _menuPropLocalPosition;

		[FormerlySerializedAs("_restaurantPropLocalRotation")]
		[SerializeField]
		private Vector3 _menuPropLocalRotation;

		[FormerlySerializedAs("_restaurantPropLocalScale")]
		[SerializeField]
		private Vector3 _menuPropLocalScale = Vector3.one;

		[Header("Chopstick Prop Transform")]
		[SerializeField]
		private Vector3 _chopstickPropLocalPosition;

		[SerializeField]
		private Vector3 _chopstickPropLocalRotation;

		[SerializeField]
		private Vector3 _chopstickPropLocalScale = Vector3.one;

		[Header("Runtime Debug GUI")]
		[SerializeField]
		private bool _drawRuntimeDebugGui;

		[SerializeField]
		private Vector2 _runtimeDebugGuiPosition = new Vector2(12f, 80f);

		[SerializeField]
		private Vector2 _runtimeDebugGuiSize = new Vector2(560f, 420f);

		[Header("Visual Performance")]
		[SerializeField]
		private bool _enableSafeVisualUpdateThrottling = true;

		[SerializeField]
		[Min(5f)]
		private float _fullAnimatorUpdateDistance = 45f;

		[SerializeField]
		[Min(0.02f)]
		private float _farAnimatorUpdateInterval = 0.15f;

		private EventBinding<RestaurantTableStateChangeEvent> _restaurantTableStateChangeEventBinding;

		private EventBinding<RestaurantCustomerMenuWaitingEvent> _restaurantCustomerMenuWaitingEventBinding;

		private EventBinding<ConveyorBeltTableStateChangeEvent> _conveyorBeltTableStateChangeEventBinding;

		private ResidentBrain _brain;

		private ResidentRestaurantController _restaurant;

		private ResidentInteractionController _interaction;

		private ResidentQuestLinks _questLinks;

		private ResidentAudio _residentAudio;

		private ResidentCustomization _customization;

		private OutfitSystem _outfitSystem;

		private ResidentDeadFishBuyer _deadFishBuyer;

		private GameObject _appliedBakedVisualPrefab;

		private bool _sellDeadFishHintShown;

		private Vector3 _homePosition;

		private Quaternion _homeRotation = Quaternion.identity;

		private bool _hasConfiguredHome;

		private bool _isCustomizationApplying;

		private int _scheduleHour = 8;

		private int _scheduleMinute;

		private bool _pendingRestaurantQueueReturn;

		private int _customizationApplyVersion;

		private int _customizationVersion;

		private bool _outfitStartPhaseCompleted;

		private bool _pendingCustomizationApply;

		private Vector2 _runtimeDebugScroll;

		private Vector3 _networkVisualVelocity;

		private Vector3 _networkTargetPosition;

		private Quaternion _networkTargetRotation = Quaternion.identity;

		private Vector3 _networkSmoothVelocity;

		private float _lastNetworkSnapshotApplyTime;

		private bool _hasNetworkMovementTarget;

		private GameObject _activeMenuProp;

		private GameObject _activeChopstickProp;

		private Renderer[] _modelRenderers = Array.Empty<Renderer>();

		private Camera _visualCullingCamera;

		private float _nextFarAnimatorUpdateTime;

		private float _temporaryExpression = -1f;

		private float _temporaryExpressionUntil;

		private bool _hasLastRestaurantExpressionServiceState;

		private float _eatingExpressionStartTime;

		private bool _restaurantEatingAnimationDelayed;

		private float _restaurantEatingAnimationStartTime;

		private bool _isClassicTableOrderBackgroundGuest;

		private bool _hasPlayedClassicTableOrderBackgroundTalk;

		private float _classicTableOrderBackgroundTalkAt;

		private RestaurantActivityType _lastRestaurantExpressionServiceState;

		private readonly HashSet<int> _animatorParameterHashes = new HashSet<int>();

		private readonly NetworkVariable<byte> _networkResidentId = new NetworkVariable<byte>(0);

		private const float NetworkMovementSmoothTime = 0.14f;

		private const float NetworkRotationLerpSpeed = 12f;

		private const float NetworkTeleportDistance = 3f;

		private const float NetworkMovementSilenceTimeout = 0.5f;

		private const float NetworkPositionDeadzone = 0.02f;

		private const float NetworkYawDeadzone = 0.5f;

		private const float PurchaseExpressionDuration = 4f;

		private const float EatingExpressionSmileDuration = 0.75f;

		private static readonly ResidentExpressionType[] DefaultExpressions = new ResidentExpressionType[4]
		{
			ResidentExpressionType.Neutral,
			ResidentExpressionType.Neutral,
			ResidentExpressionType.Smile,
			ResidentExpressionType.Sad
		};

		private static readonly int OnGroundHash = Animator.StringToHash("OnGround");

		private static readonly int JumpHash = Animator.StringToHash("Jump");

		private static readonly int JumpLegHash = Animator.StringToHash("JumpLeg");

		private static readonly int SmallHoldHash = Animator.StringToHash("SmallHold");

		private static readonly int LargeHoldHash = Animator.StringToHash("LargeHold");

		private static readonly int SwimHash = Animator.StringToHash("Swim");

		private static readonly int SwimUpHash = Animator.StringToHash("SwimUp");

		private static readonly int SwimDownHash = Animator.StringToHash("SwimDown");

		private static readonly int RodHash = Animator.StringToHash("Rod");

		private static readonly int SitHash = Animator.StringToHash("Sit");

		private static readonly int SpinHash = Animator.StringToHash("Spin");

		private static readonly int OrderHash = Animator.StringToHash("Order");

		private static readonly int EatHash = Animator.StringToHash("Eat");

		private static readonly int LookMenuHash = Animator.StringToHash("LookMenu");

		private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");

		private static readonly int TurnHash = Animator.StringToHash("Turn");

		private static readonly int ForwardHash = Animator.StringToHash("Forward");

		private static readonly int WalkHash = Animator.StringToHash("Walk");

		private static readonly int HeelHeightHash = Animator.StringToHash("HeelHeight");

		private static readonly int LayDownHash = Animator.StringToHash("LayDown");

		private static readonly int ExpressionHash = Animator.StringToHash("Expression");

		private static readonly int WaveHash = Animator.StringToHash("Wave");

		private static readonly int RestaurantGestureHash = Animator.StringToHash("RestaurantGesture");

		private static readonly int RestaurantGestureVariantHash = Animator.StringToHash("RestaurantGestureVariant");

		private static readonly int TopLayerEmptyStateHash = Animator.StringToHash("Top Layer.Empty");

		private const string RestaurantHappyBaseClipName = "A_MOD_EMOT_Happy_Nodding_Confident_Masc";

		private static readonly string[] RestaurantHappyAnimationNames = new string[3] { "Cheering While Sitting", "Sitting Clap", "Sitting Talking" };

		private static AnimationClip[] _restaurantHappyAnimations;

		private AnimatorOverrideController _restaurantHappyOverrideController;

		private AnimationClip _restaurantHappyBaseClip;

		private bool _hadActiveRestaurantAnimation;

		public string ResidentName
		{
			get
			{
				if (!(_residentData != null))
				{
					return base.name;
				}
				return _residentData.Name;
			}
		}

		public byte ResidentId => (byte)((_residentData != null) ? _residentData.NpcID : _networkResidentId.Value);

		public byte NetworkResidentId => _networkResidentId.Value;

		public int CurrentTableId_ServerReadable
		{
			get
			{
				if (Runtime == null)
				{
					return -1;
				}
				return Runtime.RestaurantTableId;
			}
		}

		public int CurrentSeatIndex_ServerReadable
		{
			get
			{
				if (Runtime == null)
				{
					return -1;
				}
				return Runtime.RestaurantSeatIndex;
			}
		}

		public ResidentRuntimeState Runtime
		{
			get
			{
				if (_brain == null)
				{
					return null;
				}
				return _brain.Runtime;
			}
		}

		public ResidentBrain Brain => _brain;

        public void PlaySeatedIdleAnimation()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            SetAnimatorBool(SitHash, true);
            SetAnimatorBool(IsWalkingHash, false);
            // Some recovered idle transitions wait for a locomotion exit.
            // Enter the existing Sit state when a seat has actually arrived.
            _animator.Play(SitHash, 0, 0);
        }

        public Animator VisualAnimator => _animator;

		public ResidentDataSO ResidentData => _residentData;

		public ResidentVoiceGender VoiceGender
		{
			get
			{
				if (!(_residentData != null))
				{
					return ResidentVoiceGender.Male;
				}
				return _residentData.VoiceGender;
			}
		}

		public bool IsUsingBakedVisual
		{
			get
			{
				if (_appliedBakedVisualPrefab != null)
				{
					return _model != null;
				}
				return false;
			}
		}

		public bool IsUsingRuntimeCustomizationFallback
		{
			get
			{
				if (_residentData != null)
				{
					return !IsUsingBakedVisual;
				}
				return false;
			}
		}

		public bool HasActiveResidentAnimator
		{
			get
			{
				if (_animator != null && _animator.enabled)
				{
					return _animator.gameObject.activeInHierarchy;
				}
				return false;
			}
		}

		public override bool IsScanable => false;

		public bool IsCustomizationApplying => _isCustomizationApplying;

		protected override void Awake()
		{
			base.Awake();
			if (_agent == null)
			{
				TryGetComponent<NavMeshAgent>(out _agent);
			}
			if (_animator == null)
			{
				_animator = GetComponentInChildren<Animator>();
			}
			CacheAnimatorParameters();
			if (_model == null && base.transform.childCount > 0)
			{
				_model = base.transform.GetChild(0).gameObject;
			}
			CacheModelRenderers();
			CacheOptionalComponents();
		}

		private void Update()
		{
			if (ShouldRunLocalSimulation())
			{
				_brain?.Tick(Time.deltaTime);
				TryNotifyRestaurantArrival();
				TryReturnToRestaurantQueueAfterLeave();
				_interaction?.Tick(Time.time);
				RefreshInteractionAvailability();
			}
			else if (IsNetworkClientProxy())
			{
				SmoothNetworkProxyMovement();
				DecayNetworkVisualVelocity();
			}
			if (ShouldUpdateAnimatorParametersThisFrame())
			{
				UpdateAnimatorParameters();
			}
		}

		private IEnumerator Start()
		{
			yield return null;
			_outfitStartPhaseCompleted = true;
			if (_pendingCustomizationApply && _brain != null)
			{
				_pendingCustomizationApply = false;
				ApplyCustomizationWithModelHoldAsync();
			}
		}

		private void OnGUI()
		{
			if (_drawRuntimeDebugGui)
			{
				_runtimeDebugGuiPosition = GUILayout.Window(screenRect: new Rect(_runtimeDebugGuiPosition, _runtimeDebugGuiSize), id: GetInstanceID(), func: DrawRuntimeDebugWindow, text: "Resident Debug - " + ResidentName, options: Array.Empty<GUILayoutOption>()).position;
			}
		}

		private void DrawRuntimeDebugWindow(int windowId)
		{
			_runtimeDebugScroll = GUILayout.BeginScrollView(_runtimeDebugScroll);
			GUILayout.Label(GetRuntimeDebugText());
			GUILayout.EndScrollView();
			GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
		}

		private void ToggleRuntimeDebugGui()
		{
			_drawRuntimeDebugGui = !_drawRuntimeDebugGui;
		}

		private void CacheAnimatorParameters()
		{
			_animatorParameterHashes.Clear();
			if (_animator == null)
			{
				return;
			}
			_animator.enabled = true;
			if (!(_animator.runtimeAnimatorController == null))
			{
				AnimatorControllerParameter[] parameters = _animator.parameters;
				foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
				{
					_animatorParameterHashes.Add(animatorControllerParameter.nameHash);
				}
			}
		}

		private void UpdateAnimatorParameters()
		{
			if (!(_animator == null) && _animator.gameObject.activeInHierarchy)
			{
				if (_animatorParameterHashes.Count == 0)
				{
					CacheAnimatorParameters();
				}
				ResidentRuntimeState runtime = Runtime;
				ResidentActivityType residentActivityType = runtime?.CurrentActivity ?? ResidentActivityType.Home;
				bool flag = runtime?.IsSwimming ?? false;
				bool flag2 = runtime?.HasArrived ?? true;
				bool flag3 = runtime != null && runtime.IsInRestaurant && runtime.HasArrived;
				bool flag4 = flag3 && runtime != null && runtime.HasArrived && runtime.RestaurantSubState != ResidentRestaurantSubState.Leaving;
				TrackRestaurantAnimatorLifecycle(flag4);
				Vector3 vector = Vector3.zero;
				float num = 0f;
				float num2 = 0f;
				bool flag5 = false;
				if (IsNetworkClientProxy())
				{
					vector = _networkVisualVelocity;
					num = vector.magnitude;
					num2 = num;
					flag5 = num > 0.05f && !flag2;
				}
				else if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
				{
					vector = _agent.desiredVelocity;
					num = _agent.velocity.magnitude;
					num2 = vector.magnitude;
					flag5 = !_agent.isStopped && (_agent.hasPath || _agent.pathPending || num2 > 0.05f);
				}
				bool flag6 = (!flag & flag5) && Mathf.Max(num, num2) > 0.05f;
				float value = 0f;
				if (flag6 && _agent != null && _agent.speed > 0.01f)
				{
					value = Mathf.Clamp01(Mathf.Max(num, num2) / _agent.speed);
				}
				float value2 = 0f;
				if ((flag6 | flag) && vector.sqrMagnitude > 0.001f)
				{
					value2 = Mathf.Clamp(base.transform.InverseTransformDirection(vector.normalized).x, -1f, 1f);
				}
				bool flag7 = flag2 && (residentActivityType == ResidentActivityType.SeasideBench || residentActivityType == ResidentActivityType.ParkBench);
				bool flag8 = flag2 && residentActivityType == ResidentActivityType.BeachSunbathe;
				bool flag9 = flag3 && runtime != null && (runtime.RestaurantServiceState == RestaurantActivityType.Order || runtime.RestaurantServiceState == RestaurantActivityType.RequestPayment);
				bool flag10 = IsClassicTableOrderSpeaker(runtime);
				if (runtime != null && runtime.RestaurantTargetType == ResidentRestaurantTargetType.DiningTable)
				{
					flag9 &= flag10;
				}
				UpdateClassicTableOrderBackgroundTalk(flag3, runtime, flag10);
				bool value3 = flag3 && runtime != null && runtime.RestaurantServiceState == RestaurantActivityType.Eat && IsRestaurantEatingAnimationReady();
				bool value4 = flag3 && runtime != null && runtime.RestaurantServiceState == RestaurantActivityType.LookMenu;
				bool value5 = flag && vector.y > 0.05f;
				bool value6 = flag && vector.y < -0.05f;
				SetAnimatorBool(OnGroundHash, !flag);
				SetAnimatorFloat(JumpHash, 0f);
				SetAnimatorFloat(JumpLegHash, 0f);
				SetAnimatorBool(SmallHoldHash, value: false);
				SetAnimatorBool(LargeHoldHash, value: false);
				SetAnimatorBool(SwimHash, flag);
				SetAnimatorBool(SwimUpHash, value5);
				SetAnimatorBool(SwimDownHash, value6);
				SetAnimatorBool(RodHash, value: false);
				SetAnimatorBool(SitHash, !flag6 && !flag && (flag7 | flag4));
				SetAnimatorBool(OrderHash, flag9);
				SetAnimatorBool(EatHash, value3);
				SetAnimatorBool(LookMenuHash, value4);
				SetAnimatorBool(IsWalkingHash, flag6);
				SetAnimatorFloat(TurnHash, value2);
				SetAnimatorFloat(ForwardHash, value);
				SetAnimatorFloat(WalkHash, value);
				SetAnimatorFloat(HeelHeightHash, 0f);
				SetAnimatorBool(LayDownHash, (!flag6 && !flag) & flag8);
				TrackRestaurantExpressionState(runtime);
				SetAnimatorFloatImmediate(ExpressionHash, ResolveExpression(runtime));
			}
		}

		private bool IsClassicTableOrderSpeaker(ResidentRuntimeState runtime)
		{
			if (runtime == null || runtime.RestaurantTargetType != ResidentRestaurantTargetType.DiningTable || runtime.RestaurantTableId < 0)
			{
				return false;
			}
			if (!(Singleton<ResidentManager>.Instance != null))
			{
				return runtime.RestaurantSeatIndex == 0;
			}
			return runtime.RestaurantSeatIndex == Singleton<ResidentManager>.Instance.GetClassicTableOrderSpeakerSeatIndex(runtime.RestaurantTableId);
		}

		internal static uint GetClassicTableOrderSpeakerScore(int tableId, byte residentId)
		{
			int num = tableId * -1640531527 + residentId + -2048144789;
			uint num2 = ((uint)num ^ ((uint)num >> 16)) * 2146121005;
			int num3 = (int)(num2 ^ (num2 >> 15)) * -2073254261;
			return (uint)num3 ^ ((uint)num3 >> 16);
		}

		private void UpdateClassicTableOrderBackgroundTalk(bool inRestaurant, ResidentRuntimeState runtime, bool isOrderSpeaker)
		{
			if (!inRestaurant || runtime == null || runtime.RestaurantTargetType != ResidentRestaurantTargetType.DiningTable || runtime.RestaurantServiceState != RestaurantActivityType.Order || isOrderSpeaker)
			{
				_isClassicTableOrderBackgroundGuest = false;
				_hasPlayedClassicTableOrderBackgroundTalk = false;
				return;
			}
			if (!_isClassicTableOrderBackgroundGuest)
			{
				_isClassicTableOrderBackgroundGuest = true;
				_hasPlayedClassicTableOrderBackgroundTalk = false;
				_classicTableOrderBackgroundTalkAt = Time.time + (float)Mathf.Clamp(runtime.RestaurantSeatIndex, 1, 3) * 0.2f;
			}
			if (!_hasPlayedClassicTableOrderBackgroundTalk && !(Time.time < _classicTableOrderBackgroundTalkAt))
			{
				PlayRestaurantGesture(RestaurantResidentGesture.TalkPoint);
				_hasPlayedClassicTableOrderBackgroundTalk = true;
			}
		}

		private void TrackRestaurantExpressionState(ResidentRuntimeState runtime)
		{
			if (runtime == null || !runtime.IsInRestaurant || !runtime.HasArrived || !runtime.HasRestaurantAssignment)
			{
				_hasLastRestaurantExpressionServiceState = false;
				_lastRestaurantExpressionServiceState = RestaurantActivityType.Idle;
				_eatingExpressionStartTime = 0f;
				return;
			}
			RestaurantActivityType restaurantServiceState = runtime.RestaurantServiceState;
			if (!_hasLastRestaurantExpressionServiceState || _lastRestaurantExpressionServiceState != restaurantServiceState)
			{
				if (restaurantServiceState == RestaurantActivityType.Eat)
				{
					_eatingExpressionStartTime = (_restaurantEatingAnimationDelayed ? _restaurantEatingAnimationStartTime : Time.time);
				}
				_lastRestaurantExpressionServiceState = restaurantServiceState;
				_hasLastRestaurantExpressionServiceState = true;
			}
		}

		private float ResolveExpression(ResidentRuntimeState runtime)
		{
			if (_temporaryExpression >= 0f)
			{
				if (Time.time < _temporaryExpressionUntil)
				{
					return _temporaryExpression;
				}
				_temporaryExpression = -1f;
				_temporaryExpressionUntil = 0f;
			}
			if (runtime != null && runtime.IsInRestaurant && runtime.HasArrived)
			{
				switch (runtime.RestaurantServiceState)
				{
				case RestaurantActivityType.Order:
				case RestaurantActivityType.LookMenu:
					return 0f;
				case RestaurantActivityType.Idle:
					if (runtime.RestaurantSubState == ResidentRestaurantSubState.WaitingOrder)
					{
						return 3f;
					}
					break;
				case RestaurantActivityType.Eat:
					if (Time.time - _eatingExpressionStartTime < 0.75f)
					{
						return 1f;
					}
					return 8f;
				case RestaurantActivityType.RequestPayment:
				case RestaurantActivityType.Pay:
					return 1f;
				}
			}
			if (runtime != null && runtime.IsWorking)
			{
				return (float)GetWorkerDefaultExpression();
			}
			return (float)GetDefaultExpression();
		}

		private ResidentExpressionType GetDefaultExpression()
		{
			int num = ResidentId;
			if (num <= 0)
			{
				num = Mathf.Abs(GetInstanceID());
			}
			return DefaultExpressions[num % DefaultExpressions.Length];
		}

		private ResidentExpressionType GetWorkerDefaultExpression()
		{
			return ResidentExpressionType.Smile;
		}

		private void TriggerSpinAnimation()
		{
			if (!(_animator == null) && _animator.gameObject.activeInHierarchy)
			{
				if (_animatorParameterHashes.Count == 0)
				{
					CacheAnimatorParameters();
				}
				if (_animatorParameterHashes.Contains(SpinHash))
				{
					_animator.SetTrigger(SpinHash);
				}
			}
		}

		private void SetAnimatorBool(int hash, bool value)
		{
			if (_animator != null && _animatorParameterHashes.Contains(hash))
			{
				_animator.SetBool(hash, value);
			}
		}

		private void SetAnimatorFloat(int hash, float value)
		{
			if (_animator != null && _animatorParameterHashes.Contains(hash))
			{
				_animator.SetFloat(hash, value, 0.1f, Time.deltaTime);
			}
		}

		private void SetAnimatorFloatImmediate(int hash, float value)
		{
			if (_animator != null && _animatorParameterHashes.Contains(hash))
			{
				_animator.SetFloat(hash, value);
			}
		}

		public void ServerPlayPurchaseExpression()
		{
			if (base.IsServer)
			{
				ServerSendPlayTemporaryExpressionClient(ResidentExpressionType.Happy, 4f);
			}
		}

		public void ServerPlayPurchaseWave()
		{
			if (base.IsServer)
			{
				ServerSendPlayPurchaseWaveClient();
			}
		}

		public void RequestPurchaseReaction()
		{
			if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && base.IsSpawned)
			{
				ServerRequestPurchaseReactionRpc();
			}
			else
			{
				ApplyPurchaseReactionLocal();
			}
		}

		private void ServerSendPlayTemporaryExpressionClient(ResidentExpressionType expression, float duration)
		{
			if (base.IsServer)
			{
				PlayTemporaryExpressionClientRevisionRpc(expression, duration, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void PlayTemporaryExpressionClientRevisionRpc(ResidentExpressionType expression, float duration, ulong worldRevision)
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
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3341648715u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in expression, default(FastBufferWriter.ForEnums));
				bufferWriter.WriteValueSafe(in duration, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 3341648715u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, (expression, duration), (ResidentController target, (ResidentExpressionType expression, float duration) data) =>
				{
					target.ApplyPlayTemporaryExpressionClient(data.expression, data.duration);
				}, 4096, worldRevision))
				{
					ApplyPlayTemporaryExpressionClient(expression, duration);
				}
			}
		}

		private void ApplyPlayTemporaryExpressionClient(ResidentExpressionType expression, float duration)
		{
			_temporaryExpression = (float)expression;
			_temporaryExpressionUntil = Time.time + Mathf.Max(0.1f, duration);
		}

		private void ServerSendPlayPurchaseWaveClient()
		{
			if (base.IsServer)
			{
				PlayPurchaseWaveClientRevisionRpc(BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void PlayPurchaseWaveClientRevisionRpc(ulong worldRevision)
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
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(4066351243u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 4066351243u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, 0, (ResidentController target, int data) =>
				{
					target.ApplyPlayPurchaseWaveClient();
				}, 4096, worldRevision))
				{
					ApplyPlayPurchaseWaveClient();
				}
			}
		}

		private void ApplyPlayPurchaseWaveClient()
		{
			if (!(_animator == null) && _animator.gameObject.activeInHierarchy)
			{
				if (_animatorParameterHashes.Count == 0)
				{
					CacheAnimatorParameters();
				}
				if (_animatorParameterHashes.Contains(WaveHash))
				{
					_animator.SetTrigger(WaveHash);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestPurchaseReactionRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(891743732u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 891743732u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				EnsureInitialized();
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				if (_interaction.IsHeldBy(senderClientId))
				{
					ServerSendPlayTemporaryExpressionClient(ResidentExpressionType.Happy, 4f);
					ServerSendPlayPurchaseWaveClient();
				}
			}
		}

		private void ApplyPurchaseReactionLocal()
		{
			_temporaryExpression = 6f;
			_temporaryExpressionUntil = Time.time + 4f;
			if (!(_animator == null) && _animator.gameObject.activeInHierarchy)
			{
				if (_animatorParameterHashes.Count == 0)
				{
					CacheAnimatorParameters();
				}
				if (_animatorParameterHashes.Contains(WaveHash))
				{
					_animator.SetTrigger(WaveHash);
				}
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<byte> networkResidentId = _networkResidentId;
			networkResidentId.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(networkResidentId.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnNetworkResidentIdChanged));
			Singleton<ResidentManager>.Instance?.RegisterRuntimeResident(this);
			RegisterRestaurantEvents();
		}

		public override void OnNetworkDespawn()
		{
			ClearRestaurantEatingAnimationDelay();
			ClearRestaurantDiningProps();
			NetworkVariable<byte> networkResidentId = _networkResidentId;
			networkResidentId.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(networkResidentId.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnNetworkResidentIdChanged));
			Singleton<ResidentManager>.Instance?.UnregisterRuntimeResident(this);
			DeregisterRestaurantEvents();
			base.OnNetworkDespawn();
		}

		public override void OnDestroy()
		{
			ClearRestaurantEatingAnimationDelay();
			ClearRestaurantDiningProps();
			if (Singleton<ResidentManager>.Instance != null)
			{
				Singleton<ResidentManager>.Instance.UnregisterRuntimeResident(this);
			}
			base.OnDestroy();
		}

		public void ConfigureResident(ResidentDataSO residentData, Vector3 homePosition, Quaternion homeRotation, ResidentManager manager)
		{
			if (residentData != null)
			{
				_residentData = residentData;
				TryApplyBakedVisual();
				if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
				{
					_networkResidentId.Value = (byte)residentData.NpcID;
				}
			}
			_homePosition = homePosition;
			_homeRotation = homeRotation;
			_hasConfiguredHome = true;
			if (_brain != null)
			{
				RequestCustomizationApply();
			}
		}

		public void Initialize()
		{
			if (_brain == null)
			{
				CacheOptionalComponents();
				if (!_hasConfiguredHome)
				{
					_homePosition = base.transform.position;
					_homeRotation = base.transform.rotation;
					_hasConfiguredHome = true;
				}
				ResidentMovementController movement = new ResidentMovementController(base.transform, _agent);
				ResidentWaypointResolver waypoints = new ResidentWaypointResolver(_homePosition, _homeRotation);
				_brain = new ResidentBrain(this, _residentData, _model, movement, waypoints);
				_brain.BeginModelVisibilityHold();
				_brain.Initialize(ResidentId, _scheduleHour, _scheduleMinute);
				_restaurant = new ResidentRestaurantController(this, _brain.Runtime);
				_interaction = new ResidentInteractionController(_brain.Runtime);
				RequestCustomizationApply();
				SetRestaurantServiceInteraction(enabled: false);
			}
		}

		public void SetScheduleTime(int hour, int minute)
		{
			_scheduleHour = Mathf.Clamp(hour, 0, 24);
			_scheduleMinute = Mathf.Clamp(minute, 0, 59);
			_brain?.SetScheduleTime(_scheduleHour, _scheduleMinute);
		}

		public void TeleportResident()
		{
			EnsureInitialized();
			_brain.ForceResetToStart();
		}

		public void HideModel()
		{
			if (_model != null)
			{
				_model.SetActive(value: false);
			}
		}

		public void ShowModel()
		{
			if (_model != null)
			{
				_model.SetActive(value: true);
			}
		}

		public void ChangeOutfit(bool isBeachOutfit)
		{
			TriggerSpinAnimation();
		}

		public bool CanBeCalledForRestaurant()
		{
			if (_brain == null || Runtime == null)
			{
				return false;
			}
			if (Runtime.SystemState == ResidentSystemState.Normal && Runtime.IsAvailable && !Runtime.IsWorking && !Runtime.IsSwimming && !Runtime.IsInteracting && !Runtime.IsInRestaurant)
			{
				return !Runtime.HasRestaurantAssignment;
			}
			return false;
		}

		public bool IsResidentWorking()
		{
			if (Runtime != null)
			{
				return Runtime.IsWorking;
			}
			return false;
		}

		public bool TryAcceptRestaurantAssignment(RestaurantAssignmentRequest request)
		{
			EnsureInitialized();
			bool result = _restaurant != null && _restaurant.BeginAssignment(request);
			SetRestaurantServiceInteraction(enabled: false);
			return result;
		}

		public void CallForRestaurant(Vector3 destination, Quaternion rot, int tableId, int seatIndex)
		{
			Transform transform = new GameObject("ResidentSyntheticSeat").transform;
			transform.position = destination;
			transform.rotation = rot;
			RestaurantAssignmentRequest request = new RestaurantAssignmentRequest(tableId, seatIndex, ResidentRestaurantTargetType.DiningTable, transform);
			TryAcceptRestaurantAssignment(request);
			UnityEngine.Object.Destroy(transform.gameObject);
		}

		public void CallForConveyorRestaurant(Vector3 destination, Quaternion rot, int tableId)
		{
			Transform transform = new GameObject("ResidentSyntheticConveyorSeat").transform;
			transform.position = destination;
			transform.rotation = rot;
			RestaurantAssignmentRequest request = new RestaurantAssignmentRequest(tableId, 0, ResidentRestaurantTargetType.ConveyorTable, transform);
			TryAcceptRestaurantAssignment(request);
			UnityEngine.Object.Destroy(transform.gameObject);
		}

		public bool ServerArrivedToTable(string residentName)
		{
			if (!base.IsServer)
			{
				return true;
			}
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (restaurantManager == null || Runtime == null)
			{
				return false;
			}
			return Runtime.RestaurantTargetType switch
			{
				ResidentRestaurantTargetType.DiningTable => restaurantManager.ResidentArrivedToTable(Runtime.RestaurantTableId, Runtime.RestaurantSeatIndex, residentName), 
				ResidentRestaurantTargetType.ConveyorTable => restaurantManager.ResidentArrivedToConveyorTable(Runtime.RestaurantTableId), 
				_ => false, 
			};
		}

		public void TakingOrder()
		{
			if (Runtime != null)
			{
				Runtime.RestaurantServiceState = RestaurantActivityType.Order;
			}
		}

		public void DeliveringOrder()
		{
			if (Runtime != null)
			{
				Runtime.RestaurantServiceState = RestaurantActivityType.Idle;
			}
		}

		public void Eating()
		{
			if (Runtime != null)
			{
				Runtime.RestaurantServiceState = RestaurantActivityType.Eat;
			}
		}

		public void BeginRestaurantEatingAnimationDelay(float delaySeconds)
		{
			float num = Mathf.Max(0f, delaySeconds);
			_restaurantEatingAnimationDelayed = num > 0f;
			_restaurantEatingAnimationStartTime = Time.time + num;
		}

		public void ClearRestaurantEatingAnimationDelay()
		{
			_restaurantEatingAnimationDelayed = false;
			_restaurantEatingAnimationStartTime = 0f;
		}

		private bool IsRestaurantEatingAnimationReady()
		{
			if (!_restaurantEatingAnimationDelayed)
			{
				return true;
			}
			if (Time.time < _restaurantEatingAnimationStartTime)
			{
				return false;
			}
			_restaurantEatingAnimationDelayed = false;
			return true;
		}

		public void AwaitingPayment()
		{
			if (Runtime != null)
			{
				Runtime.RestaurantServiceState = RestaurantActivityType.RequestPayment;
			}
		}

		public void Leave()
		{
			if (Runtime != null && _restaurant != null && Runtime.HasRestaurantAssignment)
			{
				ClearRestaurantEatingAnimationDelay();
				ClearRestaurantDiningProps();
				ResetRestaurantAnimatorState();
				_restaurant.BeginLeave();
				_pendingRestaurantQueueReturn = true;
				SetRestaurantServiceInteraction(enabled: false);
			}
		}

		public void ForceLeaveRestaurantAssignment(int tableId, ResidentRestaurantTargetType targetType)
		{
			if (Runtime != null && Runtime.HasRestaurantAssignment && Runtime.RestaurantTargetType == targetType && Runtime.RestaurantTableId == tableId)
			{
				Leave();
			}
		}

		public void OnRestaurantTableStateChanged(RestaurantTableStateChangeEvent @event)
		{
			if (Runtime != null && Runtime.RestaurantTargetType == ResidentRestaurantTargetType.DiningTable && Runtime.RestaurantTableId == @event.TableId)
			{
				_restaurant.OnDiningTableStateChanged(@event.NewState);
				HandleDiningTableVisualState(@event.NewState);
			}
		}

		public void OnRestaurantCustomerMenuWaitingChanged(RestaurantCustomerMenuWaitingEvent @event)
		{
			if (Runtime == null || Runtime.RestaurantTargetType != ResidentRestaurantTargetType.DiningTable || Runtime.RestaurantTableId != @event.TableId || Runtime.RestaurantSeatIndex != @event.SeatIndex)
			{
				return;
			}
			if (@event.IsWaiting)
			{
				Runtime.RestaurantServiceState = RestaurantActivityType.LookMenu;
				Runtime.HasArrived = true;
				Runtime.IsInRestaurant = true;
				Runtime.RestaurantSubState = ResidentRestaurantSubState.LookingMenu;
				ClearChopstickProp();
				ShowMenuProp();
				return;
			}
			ClearMenuProp();
			if (Runtime.RestaurantServiceState == RestaurantActivityType.LookMenu)
			{
				Runtime.RestaurantServiceState = RestaurantActivityType.Idle;
				if (Runtime.HasArrived)
				{
					Runtime.RestaurantSubState = ResidentRestaurantSubState.ArrivedAtSeat;
				}
			}
		}

		public void OnConveyorBeltTableStateChanged(ConveyorBeltTableStateChangeEvent @event)
		{
			if (Runtime != null && Runtime.RestaurantTargetType == ResidentRestaurantTargetType.ConveyorTable && Runtime.RestaurantTableId == @event.TableId)
			{
				_restaurant.OnConveyorTableStateChanged(@event.NewState);
				HandleConveyorTableVisualState(@event.NewState);
			}
		}

		private void HandleDiningTableVisualState(RestaurantTableState state)
		{
			switch (state)
			{
			case RestaurantTableState.Eating:
				ClearMenuProp();
				ShowChopstickProp();
				break;
			case RestaurantTableState.Empty:
			case RestaurantTableState.Cleaning:
				ClearRestaurantDiningProps();
				break;
			default:
				ClearMenuProp();
				ClearChopstickProp();
				break;
			}
		}

		private void HandleConveyorTableVisualState(ConveyorBeltTableState state)
		{
			switch (state)
			{
			case ConveyorBeltTableState.Eating:
				ClearMenuProp();
				ShowChopstickProp();
				break;
			case ConveyorBeltTableState.Empty:
				ClearRestaurantDiningProps();
				break;
			default:
				ClearMenuProp();
				ClearChopstickProp();
				break;
			}
		}

		private void SyncRestaurantDiningPropsWithRuntime()
		{
			if (Runtime == null || Runtime.CurrentActivity != ResidentActivityType.Restaurant || !Runtime.IsInRestaurant || !Runtime.HasRestaurantAssignment)
			{
				ClearRestaurantDiningProps();
				return;
			}
			switch (Runtime.RestaurantServiceState)
			{
			case RestaurantActivityType.LookMenu:
				ClearChopstickProp();
				ShowMenuProp();
				break;
			case RestaurantActivityType.Eat:
				ClearMenuProp();
				ShowChopstickProp();
				break;
			default:
				ClearRestaurantDiningProps();
				break;
			}
		}

		private void ShowMenuProp()
		{
			if (!(_activeMenuProp != null) && !(_menuPropPrefab == null))
			{
				_activeMenuProp = InstantiateRestaurantProp(_menuPropPrefab, _menuPropLocalPosition, _menuPropLocalRotation, _menuPropLocalScale);
			}
		}

		private void ShowChopstickProp()
		{
			if (!(_activeChopstickProp != null) && Runtime != null)
			{
				GameObject deterministicChopstickPrefab = GetDeterministicChopstickPrefab(Runtime.RestaurantTableId, Runtime.RestaurantSeatIndex);
				if (!(deterministicChopstickPrefab == null))
				{
					_activeChopstickProp = InstantiateRestaurantProp(deterministicChopstickPrefab, _chopstickPropLocalPosition, _chopstickPropLocalRotation, _chopstickPropLocalScale);
				}
			}
		}

		private GameObject GetDeterministicChopstickPrefab(int tableId, int seatIndex)
		{
			if (_chopstickPropPrefabs == null || _chopstickPropPrefabs.Count == 0)
			{
				return null;
			}
			int num = (int)((uint)((tableId * 397) ^ seatIndex) % (uint)_chopstickPropPrefabs.Count);
			for (int i = 0; i < _chopstickPropPrefabs.Count; i++)
			{
				int index = (num + i) % _chopstickPropPrefabs.Count;
				if (_chopstickPropPrefabs[index] != null)
				{
					return _chopstickPropPrefabs[index];
				}
			}
			return null;
		}

		private GameObject InstantiateRestaurantProp(GameObject prefab, Vector3 localPosition, Vector3 localRotation, Vector3 localScale)
		{
			Transform restaurantPropAnchor = GetRestaurantPropAnchor();
			GameObject obj = UnityEngine.Object.Instantiate(prefab, restaurantPropAnchor);
			obj.transform.localPosition = localPosition;
			obj.transform.localRotation = Quaternion.Euler(localRotation);
			obj.transform.localScale = localScale;
			return obj;
		}

		private Transform GetRestaurantPropAnchor()
		{
			if (_restaurantPropAnchor != null)
			{
				return _restaurantPropAnchor;
			}
			if (_animator != null && _animator.isHuman)
			{
				Transform boneTransform = _animator.GetBoneTransform(HumanBodyBones.RightHand);
				if (boneTransform != null)
				{
					return boneTransform;
				}
			}
			if (_model != null)
			{
				return _model.transform;
			}
			return base.transform;
		}

		private void ClearRestaurantDiningProps()
		{
			ClearMenuProp();
			ClearChopstickProp();
		}

		private void ClearMenuProp()
		{
			if (!(_activeMenuProp == null))
			{
				UnityEngine.Object.Destroy(_activeMenuProp);
				_activeMenuProp = null;
			}
		}

		private void ClearChopstickProp()
		{
			if (!(_activeChopstickProp == null))
			{
				UnityEngine.Object.Destroy(_activeChopstickProp);
				_activeChopstickProp = null;
			}
		}

		public override void GazeEnter()
		{
			if (!(Singleton<GameUIManager>.Instance != null) || !Singleton<GameUIManager>.Instance.TryFastForwardResidentDialogue(this))
			{
				base.GazeEnter();
			}
		}

		protected override bool CanTrigger(Interactor interactor)
		{
			if (CanInteract.Value)
			{
				return CanInteractWithResident();
			}
			return false;
		}

		public override void EnableGazeHints()
		{
			if (Singleton<HintsManager>.Instance != null)
			{
				Singleton<HintsManager>.Instance.AddHint("Interact", "key_interact");
				if (ShouldShowSellDeadFishHint())
				{
					Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
					Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_sell_fish");
					_sellDeadFishHintShown = true;
				}
				else
				{
					_sellDeadFishHintShown = false;
				}
			}
			if (_interactableTooltip != null && _interactableTooltip.IsEnabled && Singleton<ToolTipHandler>.Instance != null)
			{
				Singleton<ToolTipHandler>.Instance.OnGazeEnter(_interactableTooltip, _interactableTooltip.GetWorldPosition());
			}
		}

		public override void DisableGazeHints()
		{
			if (Singleton<HintsManager>.Instance != null)
			{
				Singleton<HintsManager>.Instance.RemoveHint("Interact");
				if (_sellDeadFishHintShown)
				{
					Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
					_sellDeadFishHintShown = false;
				}
			}
			if (_interactableTooltip != null && Singleton<ToolTipHandler>.Instance != null)
			{
				Singleton<ToolTipHandler>.Instance.OnGazeExit(_interactableTooltip);
			}
		}

		private bool ShouldShowSellDeadFishHint()
		{
			if (_deadFishBuyer == null || !_deadFishBuyer.CanBuyDeadFish)
			{
				return false;
			}
			if (Singleton<QuickSlotHandler>.Instance != null)
			{
				return Singleton<QuickSlotHandler>.Instance.SelectedItem is DeadFish;
			}
			return false;
		}

		protected override void ClientInteract(Interactor interactor, InputActionPhase phase)
		{
			base.ClientInteract(interactor, phase);
			if (phase == InputActionPhase.Started)
			{
				ServerTryStartInteractRpc();
			}
		}

		protected override void ClientCrossMouse1(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			if (phase != InputActionPhase.Started)
			{
				return;
			}
			if (_deadFishBuyer == null || !_deadFishBuyer.CanBuyDeadFish)
			{
				base.ClientCrossMouse1(interactor, held, phase);
			}
			else if (!(interactor == null) && !(interactor.NetworkObject == null))
			{
				if (!(held is DeadFish deadFish) || deadFish.NetworkObject == null)
				{
					base.ClientCrossMouse1(interactor, held, phase);
				}
				else
				{
					ServerTrySellHeldDeadFishRpc(interactor.NetworkObject.ToReference(), deadFish.NetworkObject.ToReference());
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerTryStartInteractRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3315275557u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3315275557u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			EnsureInitialized();
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!_interaction.TryBegin(senderClientId, ShouldRequireWorkingForInteraction()))
			{
				if (_interaction.IsHeldBy(senderClientId))
				{
					ServerSendClientHandleOwnLockedInteract(senderClientId);
				}
				else
				{
					ServerSendClientShowBusyToast(senderClientId);
				}
			}
			else
			{
				CanInteract.Value = false;
				CanGaze.Value = false;
				ServerSendClientOpenResidentUi(senderClientId);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerTrySellHeldDeadFishRpc(NetworkObjectReference interactorRef, NetworkObjectReference fishRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3365836575u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in fishRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3365836575u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			EnsureInitialized();
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!(_deadFishBuyer == null) && _deadFishBuyer.CanBuyDeadFish)
			{
				Interactor component;
				DeadFish component2;
				if (!_interaction.CanInteractWith(ShouldRequireWorkingForInteraction()))
				{
					ServerSendClientShowBusyToast(senderClientId);
				}
				else if (!interactorRef.TryGetComponent<Interactor>(out component))
				{
					UtilityMethods.Log("[ResidentController] Dead fish sale failed. Interactor reference is invalid.", LogType.Warning);
				}
				else if (!fishRef.TryGetComponent<DeadFish>(out component2))
				{
					UtilityMethods.Log("[ResidentController] Dead fish sale failed. Fish reference is invalid.", LogType.Warning);
				}
				else
				{
					_deadFishBuyer.ServerTrySellDeadFish(this, component, component2, senderClientId);
				}
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerEnableInteractRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1271522367u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1271522367u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				EnsureInitialized();
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				if (_interaction.End(senderClientId))
				{
					RefreshInteractionAvailability(force: true);
					RefreshGazeFeedback();
					_brain.ResumeScheduleAfterInteraction();
				}
			}
		}

		public void RequestDialogueBillboardText(string message)
		{
			if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && base.IsSpawned)
			{
				ServerShowDialogueBillboardTextRpc(message);
			}
			else
			{
				PlayDialogueBillboardTextLocal(message);
			}
		}

		public void RequestLocalizedDialogueBillboardText(string tableName, string localizationKey, string fallback)
		{
			if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && base.IsSpawned)
			{
				ServerShowLocalizedDialogueBillboardTextRpc(tableName, localizationKey, fallback);
			}
			else
			{
				PlayLocalizedDialogueBillboardTextLocal(tableName, localizationKey, fallback);
			}
		}

		public void RequestHideDialogueBillboard()
		{
			if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && base.IsSpawned)
			{
				ServerHideDialogueBillboardRpc();
			}
			else
			{
				_residentBillboardText?.Hide();
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerShowDialogueBillboardTextRpc(string message, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3718605444u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bool value = message != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(message);
				}
				__endSendRpc(ref bufferWriter, 3718605444u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				EnsureInitialized();
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				if (!_interaction.IsHeldBy(senderClientId))
				{
					UtilityMethods.Log($"[ResidentController] Dialogue billboard text ignored. Sender={senderClientId} is not active interactor.", LogType.Warning);
				}
				else
				{
					ServerSendClientShowDialogueBillboardText(message ?? string.Empty);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerShowLocalizedDialogueBillboardTextRpc(string tableName, string localizationKey, string fallback, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(4243498832u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bool value = tableName != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(tableName);
				}
				bool value2 = localizationKey != null;
				bufferWriter.WriteValueSafe(in value2, default(FastBufferWriter.ForPrimitives));
				if (value2)
				{
					bufferWriter.WriteValueSafe(localizationKey);
				}
				bool value3 = fallback != null;
				bufferWriter.WriteValueSafe(in value3, default(FastBufferWriter.ForPrimitives));
				if (value3)
				{
					bufferWriter.WriteValueSafe(fallback);
				}
				__endSendRpc(ref bufferWriter, 4243498832u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				EnsureInitialized();
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				if (!_interaction.IsHeldBy(senderClientId))
				{
					UtilityMethods.Log($"[ResidentController] Localized dialogue billboard text ignored. Sender={senderClientId} is not active interactor.", LogType.Warning);
				}
				else
				{
					ServerSendClientShowLocalizedDialogueBillboardText(tableName ?? string.Empty, localizationKey ?? string.Empty, fallback ?? string.Empty);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerHideDialogueBillboardRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(604047052u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 604047052u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				EnsureInitialized();
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				if (!_interaction.IsHeldBy(senderClientId))
				{
					UtilityMethods.Log($"[ResidentController] Dialogue billboard hide ignored. Sender={senderClientId} is not active interactor.", LogType.Warning);
				}
				else
				{
					ServerSendClientHideDialogueBillboard();
				}
			}
		}

		private void ServerSendClientShowDialogueBillboardText(string message)
		{
			if (base.IsServer)
			{
				ClientShowDialogueBillboardTextRevisionRpc(message, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientShowDialogueBillboardTextRevisionRpc(string message, ulong worldRevision)
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
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3956984337u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bool value = message != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(message);
				}
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 3956984337u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, message, (ResidentController target, string data) =>
				{
					target.ApplyClientShowDialogueBillboardText(data);
				}, 4096 + (message?.Length ?? 0) * 2, worldRevision))
				{
					ApplyClientShowDialogueBillboardText(message);
				}
			}
		}

		private void ApplyClientShowDialogueBillboardText(string message)
		{
			PlayDialogueBillboardTextLocal(message);
		}

		private void ServerSendClientShowLocalizedDialogueBillboardText(string tableName, string localizationKey, string fallback)
		{
			if (base.IsServer)
			{
				ClientShowLocalizedDialogueBillboardTextRevisionRpc(tableName, localizationKey, fallback, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientShowLocalizedDialogueBillboardTextRevisionRpc(string tableName, string localizationKey, string fallback, ulong worldRevision)
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
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2595555987u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bool value = tableName != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(tableName);
				}
				bool value2 = localizationKey != null;
				bufferWriter.WriteValueSafe(in value2, default(FastBufferWriter.ForPrimitives));
				if (value2)
				{
					bufferWriter.WriteValueSafe(localizationKey);
				}
				bool value3 = fallback != null;
				bufferWriter.WriteValueSafe(in value3, default(FastBufferWriter.ForPrimitives));
				if (value3)
				{
					bufferWriter.WriteValueSafe(fallback);
				}
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 2595555987u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, (tableName, localizationKey, fallback), (ResidentController target, (string tableName, string localizationKey, string fallback) data) =>
				{
					target.ApplyClientShowLocalizedDialogueBillboardText(data.tableName, data.localizationKey, data.fallback);
				}, 4096 + (tableName?.Length ?? 0) * 2 + (localizationKey?.Length ?? 0) * 2 + (fallback?.Length ?? 0) * 2, worldRevision))
				{
					ApplyClientShowLocalizedDialogueBillboardText(tableName, localizationKey, fallback);
				}
			}
		}

		private void ApplyClientShowLocalizedDialogueBillboardText(string tableName, string localizationKey, string fallback)
		{
			PlayLocalizedDialogueBillboardTextLocal(tableName, localizationKey, fallback);
		}

		private void ServerSendClientHideDialogueBillboard()
		{
			if (base.IsServer)
			{
				ClientHideDialogueBillboardRevisionRpc(BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientHideDialogueBillboardRevisionRpc(ulong worldRevision)
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
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3180465951u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 3180465951u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, 0, (ResidentController target, int data) =>
				{
					target.ApplyClientHideDialogueBillboard();
				}, 4096, worldRevision))
				{
					ApplyClientHideDialogueBillboard();
				}
			}
		}

		private void ApplyClientHideDialogueBillboard()
		{
			_residentBillboardText?.Hide();
		}

		private void PlayDialogueBillboardTextLocal(string message)
		{
			if (!(_residentBillboardText == null))
			{
				_residentBillboardText.PlayTypewriter(message, () =>
				{
					Singleton<GameUIManager>.Instance?.NotifyResidentBillboardTypingFinished(this);
				});
			}
		}

		private void PlayLocalizedDialogueBillboardTextLocal(string tableName, string localizationKey, string fallback)
		{
			string message = LocalizationDatabase.GetString(tableName, localizationKey, fallback);
			PlayDialogueBillboardTextLocal(message);
		}

		private void ServerSendClientOpenResidentUi(ulong targetClientId)
		{
			if (base.IsServer)
			{
				ClientOpenResidentUiRevisionRpc(targetClientId, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientOpenResidentUiRevisionRpc(ulong targetClientId, ulong worldRevision)
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
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(892847299u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, targetClientId);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 892847299u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, targetClientId, (ResidentController target, ulong data) =>
				{
					target.ApplyClientOpenResidentUi(data);
				}, 4096, worldRevision))
				{
					ApplyClientOpenResidentUi(targetClientId);
				}
			}
		}

		private void ApplyClientOpenResidentUi(ulong targetClientId)
		{
			if (!(NetworkManager.Singleton == null) && NetworkManager.Singleton.LocalClientId == targetClientId)
			{
				OpenUI();
			}
		}

		private void ServerSendClientShowBusyToast(ulong targetClientId)
		{
			if (base.IsServer)
			{
				ClientShowBusyToastRevisionRpc(targetClientId, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientShowBusyToastRevisionRpc(ulong targetClientId, ulong worldRevision)
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
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(276426175u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, targetClientId);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 276426175u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, targetClientId, (ResidentController target, ulong data) =>
				{
					target.ApplyClientShowBusyToast(data);
				}, 4096, worldRevision))
				{
					ApplyClientShowBusyToast(targetClientId);
				}
			}
		}

		private void ApplyClientShowBusyToast(ulong targetClientId)
		{
			if (!(NetworkManager.Singleton == null) && NetworkManager.Singleton.LocalClientId == targetClientId)
			{
				Singleton<NotifyManager>.Instance?.ShowToast("I'm busy right now!", NotificationState.Info, 1.5f);
			}
		}

		private void ServerSendClientHandleOwnLockedInteract(ulong targetClientId)
		{
			if (base.IsServer)
			{
				ClientHandleOwnLockedInteractRevisionRpc(targetClientId, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientHandleOwnLockedInteractRevisionRpc(ulong targetClientId, ulong worldRevision)
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
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1057243664u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, targetClientId);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 1057243664u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, targetClientId, (ResidentController target, ulong data) =>
				{
					target.ApplyClientHandleOwnLockedInteract(data);
				}, 4096, worldRevision))
				{
					ApplyClientHandleOwnLockedInteract(targetClientId);
				}
			}
		}

		private void ApplyClientHandleOwnLockedInteract(ulong targetClientId)
		{
			if (!(NetworkManager.Singleton == null) && NetworkManager.Singleton.LocalClientId == targetClientId && (!(Singleton<GameUIManager>.Instance != null) || !Singleton<GameUIManager>.Instance.TryFastForwardResidentDialogue(this)))
			{
				ServerRestartOwnInteractionRpc();
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerRestartOwnInteractionRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(941392823u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 941392823u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				EnsureInitialized();
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				if (!_interaction.IsHeldBy(senderClientId))
				{
					ServerSendClientShowBusyToast(senderClientId);
				}
				else if (!CanInteractWithResident())
				{
					_interaction.Clear();
					RefreshInteractionAvailability(force: true);
					ServerSendClientShowBusyToast(senderClientId);
				}
				else
				{
					CanInteract.Value = false;
					CanGaze.Value = false;
					ServerSendClientOpenResidentUi(senderClientId);
				}
			}
		}

		private void OpenUI()
		{
			DisableGazeHints();
			PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
			if (pPG_Player != null)
			{
				pPG_Player.SwitchTo("UI", cursorVisible: true);
				pPG_Player.HideCursor();
			}
			PlaySFX(ResidentSounds.Greeting);
			if (Singleton<GameUIManager>.Instance != null)
			{
				Singleton<GameUIManager>.Instance.ShowDialoguePanel(_actorType, _residentBillboardText, GetRelatedQuestIds(), this);
			}
		}

		public void PlaySFX(ResidentSounds sounds)
		{
			if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && base.IsSpawned)
			{
				ServerPlayDialogueSfxRpc((byte)sounds);
			}
			else
			{
				PlaySfxLocal(sounds);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerPlayDialogueSfxRpc(byte soundValue, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3847933120u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in soundValue, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3847933120u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			EnsureInitialized();
			ResidentSounds residentSounds = (ResidentSounds)soundValue;
			if (Enum.IsDefined(typeof(ResidentSounds), residentSounds))
			{
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				if (!_interaction.IsHeldBy(senderClientId))
				{
					UtilityMethods.Log($"[ResidentController] Dialogue SFX ignored. Sender={senderClientId} is not active interactor.", LogType.Warning);
				}
				else
				{
					ServerSendClientPlayDialogueSfx(soundValue);
				}
			}
		}

		private void ServerSendClientPlayDialogueSfx(byte soundValue)
		{
			if (base.IsServer)
			{
				ClientPlayDialogueSfxRevisionRpc(soundValue, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientPlayDialogueSfxRevisionRpc(byte soundValue, ulong worldRevision)
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
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2054934730u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in soundValue, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 2054934730u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, soundValue, (ResidentController target, byte data) =>
				{
					target.ApplyClientPlayDialogueSfx(data);
				}, 4096, worldRevision))
				{
					ApplyClientPlayDialogueSfx(soundValue);
				}
			}
		}

		private void ApplyClientPlayDialogueSfx(byte soundValue)
		{
			PlaySfxLocal((ResidentSounds)soundValue);
		}

		private void PlaySfxLocal(ResidentSounds sound)
		{
			_residentAudio?.Play(sound);
		}

		public bool PlayRestaurantFeedbackCue(int tableId, int speakerSeatIndex, RestaurantFeedbackCue cue)
		{
			return PlayRestaurantMoment(tableId, speakerSeatIndex, cue, RestaurantResidentGesture.None);
		}

		public bool PlayRestaurantMoment(int tableId, int speakerSeatIndex, RestaurantFeedbackCue cue, RestaurantResidentGesture gesture)
		{
			if (Runtime == null || Runtime.RestaurantTargetType != ResidentRestaurantTargetType.DiningTable || Runtime.RestaurantTableId != tableId || Runtime.RestaurantSeatIndex != speakerSeatIndex || !Runtime.HasArrived || !Runtime.IsInRestaurant)
			{
				return false;
			}
			PlayRestaurantGesture(gesture);
			ApplyRestaurantMomentExpression(cue, gesture);
			if (cue == RestaurantFeedbackCue.RequestOrder && gesture == RestaurantResidentGesture.Wave)
			{
				_residentAudio?.Play(ResidentSounds.Greeting);
			}
			return true;
		}

		private void PlayRestaurantGesture(RestaurantResidentGesture gesture)
		{
			if (gesture == RestaurantResidentGesture.None || _animator == null || !_animator.gameObject.activeInHierarchy)
			{
				return;
			}
			if (_animatorParameterHashes.Count == 0)
			{
				CacheAnimatorParameters();
			}
			if (gesture == RestaurantResidentGesture.Wave)
			{
				if (_animatorParameterHashes.Contains(WaveHash))
				{
					_animator.SetTrigger(WaveHash);
				}
			}
			else if (_animatorParameterHashes.Contains(RestaurantGestureHash) && _animatorParameterHashes.Contains(RestaurantGestureVariantHash))
			{
				if (IsRestaurantHappyGesture(gesture))
				{
					ApplyRestaurantHappyAnimation(gesture);
				}
				int value = (IsRestaurantHappyGesture(gesture) ? 3 : ((gesture != RestaurantResidentGesture.TalkPoint) ? 1 : 2));
				_animator.SetInteger(RestaurantGestureVariantHash, value);
				_animator.SetTrigger(RestaurantGestureHash);
			}
		}

		private void TrackRestaurantAnimatorLifecycle(bool restaurantAnimationActive)
		{
			if (restaurantAnimationActive)
			{
				_hadActiveRestaurantAnimation = true;
			}
			else if (_hadActiveRestaurantAnimation)
			{
				ResetRestaurantAnimatorState();
			}
		}

		private void ResetRestaurantAnimatorState()
		{
			_hadActiveRestaurantAnimation = false;
			_isClassicTableOrderBackgroundGuest = false;
			_hasPlayedClassicTableOrderBackgroundTalk = false;
			_classicTableOrderBackgroundTalkAt = 0f;
			_temporaryExpressionUntil = 0f;
			ClearRestaurantEatingAnimationDelay();
			if (!(_animator == null) && _animator.gameObject.activeInHierarchy)
			{
				if (_animatorParameterHashes.Count == 0)
				{
					CacheAnimatorParameters();
				}
				if (_animatorParameterHashes.Contains(RestaurantGestureHash))
				{
					_animator.ResetTrigger(RestaurantGestureHash);
				}
				if (_animatorParameterHashes.Contains(WaveHash))
				{
					_animator.ResetTrigger(WaveHash);
				}
				if (_animatorParameterHashes.Contains(RestaurantGestureVariantHash))
				{
					_animator.SetInteger(RestaurantGestureVariantHash, 0);
				}
				SetAnimatorBool(OrderHash, value: false);
				SetAnimatorBool(EatHash, value: false);
				SetAnimatorBool(LookMenuHash, value: false);
				int layerIndex = _animator.GetLayerIndex("Top Layer");
				if (layerIndex >= 0 && _animator.HasState(layerIndex, TopLayerEmptyStateHash))
				{
					_animator.CrossFade(TopLayerEmptyStateHash, 0.1f, layerIndex, 0f);
				}
			}
		}

		private void ApplyRestaurantHappyAnimation(RestaurantResidentGesture gesture)
		{
			AnimationClip restaurantHappyAnimation = GetRestaurantHappyAnimation((int)(gesture - 4));
			if (!(restaurantHappyAnimation == null) && EnsureRestaurantHappyOverrideController())
			{
				_restaurantHappyOverrideController[_restaurantHappyBaseClip] = restaurantHappyAnimation;
			}
		}

		private bool EnsureRestaurantHappyOverrideController()
		{
			if (_restaurantHappyOverrideController != null && _restaurantHappyBaseClip != null)
			{
				if (_animator.runtimeAnimatorController != _restaurantHappyOverrideController)
				{
					_animator.runtimeAnimatorController = _restaurantHappyOverrideController;
				}
				return true;
			}
			RuntimeAnimatorController runtimeAnimatorController = _animator.runtimeAnimatorController;
			if (runtimeAnimatorController == null)
			{
				return false;
			}
			AnimatorOverrideController animatorOverrideController = new AnimatorOverrideController(runtimeAnimatorController);
			List<KeyValuePair<AnimationClip, AnimationClip>> list = new List<KeyValuePair<AnimationClip, AnimationClip>>();
			animatorOverrideController.GetOverrides(list);
			for (int i = 0; i < list.Count; i++)
			{
				AnimationClip key = list[i].Key;
				if (!(key == null) && !(key.name != "A_MOD_EMOT_Happy_Nodding_Confident_Masc"))
				{
					_restaurantHappyBaseClip = key;
					break;
				}
			}
			if (_restaurantHappyBaseClip == null)
			{
				return false;
			}
			_restaurantHappyOverrideController = animatorOverrideController;
			_animator.runtimeAnimatorController = _restaurantHappyOverrideController;
			CacheAnimatorParameters();
			return true;
		}

		private static AnimationClip GetRestaurantHappyAnimation(int requestedIndex)
		{
			bool flag = _restaurantHappyAnimations == null;
			if (!flag)
			{
				flag = true;
				for (int i = 0; i < _restaurantHappyAnimations.Length; i++)
				{
					if (!(_restaurantHappyAnimations[i] == null))
					{
						flag = false;
						break;
					}
				}
			}
			if (flag)
			{
				_restaurantHappyAnimations = new AnimationClip[RestaurantHappyAnimationNames.Length];
				for (int j = 0; j < RestaurantHappyAnimationNames.Length; j++)
				{
					string text = RestaurantHappyAnimationNames[j];
					AnimationClip[] array = Resources.LoadAll<AnimationClip>("RestaurantHappy/" + text);
					foreach (AnimationClip animationClip in array)
					{
						if (!(animationClip == null) && !(animationClip.length <= 0.05f) && !animationClip.name.StartsWith("__preview__"))
						{
							_restaurantHappyAnimations[j] = animationClip;
							break;
						}
					}
				}
			}
			if (requestedIndex >= 0 && requestedIndex < _restaurantHappyAnimations.Length && _restaurantHappyAnimations[requestedIndex] != null)
			{
				return _restaurantHappyAnimations[requestedIndex];
			}
			for (int l = 0; l < _restaurantHappyAnimations.Length; l++)
			{
				if (_restaurantHappyAnimations[l] != null)
				{
					return _restaurantHappyAnimations[l];
				}
			}
			return null;
		}

		private static bool IsRestaurantHappyGesture(RestaurantResidentGesture gesture)
		{
			if ((int)gesture >= 4)
			{
				return (int)gesture <= 6;
			}
			return false;
		}

		private void ApplyRestaurantMomentExpression(RestaurantFeedbackCue cue, RestaurantResidentGesture gesture)
		{
			if (!IsRestaurantHappyGesture(gesture))
			{
				switch (cue)
				{
				case RestaurantFeedbackCue.OrderServed:
					break;
				case RestaurantFeedbackCue.EatingChatter:
					_temporaryExpression = 1f;
					_temporaryExpressionUntil = Time.time + 1.5f;
					return;
				default:
					return;
				}
			}
			_temporaryExpression = 6f;
			_temporaryExpressionUntil = Time.time + 2.5f;
		}

		public void BroadcastResidentStateBatch(ResidentStateSnapshotPacked[] snapshots)
		{
			if (snapshots != null && snapshots.Length != 0 && base.IsServer && base.IsSpawned)
			{
				ServerSendClientApplyResidentStateBatch(snapshots);
			}
		}

		public void BroadcastResidentMovementBatch(ResidentMovementSnapshotPacked[] snapshots)
		{
			if (snapshots != null && snapshots.Length != 0 && base.IsServer && base.IsSpawned)
			{
				ServerSendClientApplyResidentMovementBatch(snapshots);
			}
		}

		private void ServerSendClientApplyResidentStateBatch(ResidentStateSnapshotPacked[] snapshots)
		{
			if (base.IsServer)
			{
				ClientApplyResidentStateBatchRevisionRpc(snapshots, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientApplyResidentStateBatchRevisionRpc(ResidentStateSnapshotPacked[] snapshots, ulong worldRevision)
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
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3258338804u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bool value = snapshots != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(snapshots, default(FastBufferWriter.ForNetworkSerializable));
				}
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 3258338804u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDeferArray(this, snapshots, (ResidentController target, ResidentStateSnapshotPacked[] data) =>
				{
					target.ApplyClientApplyResidentStateBatch(data);
				}, 128, worldRevision))
				{
					ApplyClientApplyResidentStateBatch(snapshots);
				}
			}
		}

		private void ApplyClientApplyResidentStateBatch(ResidentStateSnapshotPacked[] snapshots)
		{
			if (IsNetworkClientProxy())
			{
				Singleton<ResidentManager>.Instance?.ApplyStateSnapshots(snapshots);
			}
		}

		private void ServerSendClientApplyResidentMovementBatch(ResidentMovementSnapshotPacked[] snapshots)
		{
			if (base.IsServer)
			{
				ClientApplyResidentMovementBatchRevisionRpc(snapshots, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, Delivery = RpcDelivery.Unreliable, InvokePermission = RpcInvokePermission.Server)]
		private void ClientApplyResidentMovementBatchRevisionRpc(ResidentMovementSnapshotPacked[] snapshots, ulong worldRevision)
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
					Delivery = RpcDelivery.Unreliable,
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(905712343u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Unreliable);
				bool value = snapshots != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(snapshots, default(FastBufferWriter.ForNetworkSerializable));
				}
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 905712343u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Unreliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDeferArray(this, snapshots, (ResidentController target, ResidentMovementSnapshotPacked[] data) =>
				{
					target.ApplyClientApplyResidentMovementBatch(data);
				}, 128, worldRevision))
				{
					ApplyClientApplyResidentMovementBatch(snapshots);
				}
			}
		}

		private void ApplyClientApplyResidentMovementBatch(ResidentMovementSnapshotPacked[] snapshots)
		{
			if (IsNetworkClientProxy())
			{
				Singleton<ResidentManager>.Instance?.ApplyMovementSnapshots(snapshots);
			}
		}

		public void SetSnapshotApplyMode(bool enabled)
		{
			EnsureInitialized();
			if (enabled)
			{
				_brain.Pause();
			}
			else
			{
				_brain.Resume();
			}
		}

		public void ApplySnapshotPacked(ResidentSnapshotPacked snapshot)
		{
			ApplySnapshotPacked(snapshot, authoritativeProxy: false);
		}

		public void ApplySnapshotPacked(ResidentSnapshotPacked snapshot, bool authoritativeProxy)
		{
			EnsureInitialized();
			_pendingRestaurantQueueReturn = false;
			_interaction?.Clear();
			if (!authoritativeProxy)
			{
				_brain.Pause();
			}
			ApplySnapshotState(snapshot, authoritativeProxy);
			if (!authoritativeProxy)
			{
				_brain.Resume();
				RefreshInteractionAvailability(force: true);
				_brain.RequestScheduleChange();
			}
		}

		public void ApplyStateSnapshotPacked(ResidentStateSnapshotPacked snapshot, bool authoritativeProxy)
		{
			EnsureInitialized();
			_pendingRestaurantQueueReturn = false;
			_interaction?.Clear();
			ApplyStateSnapshotState(snapshot, authoritativeProxy);
			if (authoritativeProxy)
			{
				SetLocalSimulationEnabled(enabled: false);
			}
			else
			{
				RefreshInteractionAvailability(force: true);
			}
		}

		public void ApplyMovementSnapshotPacked(ResidentMovementSnapshotPacked snapshot)
		{
			if (IsNetworkClientProxy())
			{
				EnsureInitialized();
				if (ShouldAcceptMovementSnapshot())
				{
					Quaternion rotation = Quaternion.Euler(0f, ResidentYawQuantize.DecodeYaw(snapshot.YawQ), 0f);
					SetNetworkMovementTarget(snapshot.Position, rotation);
				}
			}
		}

		private void ApplySnapshotState(ResidentSnapshotPacked snapshot, bool authoritativeProxy)
		{
			ResidentRuntimeState runtime = Runtime;
			Vector3 position = snapshot.Position;
			Quaternion rotation = Quaternion.Euler(0f, ResidentYawQuantize.DecodeYaw(snapshot.YawQ), 0f);
			if (authoritativeProxy)
			{
				UpdateNetworkVisualVelocity(position);
			}
			_brain.Movement.ClearPath();
			if (authoritativeProxy)
			{
				_brain.Movement.StopAtPose(position, rotation);
			}
			else
			{
				_brain.Movement.Warp(position, rotation);
			}
			ResidentActivityType currentState = (ResidentActivityType)snapshot.CurrentState;
			runtime.SetActivity(currentState);
			runtime.PreviousActivity = (ResidentActivityType)snapshot.PreviousState;
			runtime.SystemState = ResidentSystemState.Normal;
			runtime.IsSnapshotApplying = false;
			runtime.CurrentScheduleEntryIndex = ((snapshot.Index0 == byte.MaxValue) ? (-1) : snapshot.Index0);
			runtime.IsWorking = (snapshot.Flags & 1) != 0;
			runtime.IsAvailable = (snapshot.Flags & 2) != 0;
			runtime.IsSwimming = (snapshot.Flags & 4) != 0;
			runtime.HasNewEntry = (snapshot.Flags & 8) != 0;
			runtime.HasArrived = (snapshot.Flags & 0x20) != 0;
			runtime.HasValidTarget = (snapshot.Flags & 0x40) != 0;
			runtime.IsInteracting = false;
			runtime.ActiveInteractorClientId = ulong.MaxValue;
			bool flag = false;
			if (currentState == ResidentActivityType.Restaurant)
			{
				flag = TryRestoreRestaurantSnapshot(snapshot, runtime);
			}
			if (!flag)
			{
				runtime.ClearRestaurantAssignment();
				if (currentState == ResidentActivityType.Restaurant)
				{
					runtime.SetActivity(ResidentActivityType.Home);
				}
			}
			ApplyModelVisibilityFromRuntime();
			SyncRestaurantDiningPropsWithRuntime();
		}

		private void ApplyStateSnapshotState(ResidentStateSnapshotPacked snapshot, bool authoritativeProxy)
		{
			ResidentRuntimeState runtime = Runtime;
			bool wasModelActive = _model != null && _model.activeSelf;
			ResidentActivityType currentState = (ResidentActivityType)snapshot.CurrentState;
			runtime.SetActivity(currentState);
			runtime.PreviousActivity = (ResidentActivityType)snapshot.PreviousState;
			runtime.SystemState = ResidentSystemState.Normal;
			runtime.IsSnapshotApplying = false;
			runtime.CurrentScheduleEntryIndex = snapshot.CurrentScheduleEntryIndex;
			runtime.CurrentWaypointIndex = (WayPointIndex)snapshot.WaypointIndex;
			runtime.CurrentPatrolEntryPointIndex = snapshot.CurrentPatrolEntryPointIndex;
			runtime.TargetDestination = snapshot.TargetDestination;
			runtime.TargetRotation = new Vector3(0f, ResidentYawQuantize.DecodeYaw(snapshot.TargetYawQ), 0f);
			runtime.IsWorking = (snapshot.Flags & 1) != 0;
			runtime.IsAvailable = (snapshot.Flags & 2) != 0;
			runtime.IsSwimming = (snapshot.Flags & 4) != 0;
			runtime.HasNewEntry = (snapshot.Flags & 8) != 0;
			runtime.HasArrived = (snapshot.Flags & 0x20) != 0;
			runtime.HasValidTarget = (snapshot.Flags & 0x40) != 0;
			runtime.IsInteracting = false;
			runtime.ActiveInteractorClientId = ulong.MaxValue;
			runtime.RestaurantTargetType = (ResidentRestaurantTargetType)snapshot.RestaurantTargetType;
			runtime.RestaurantTableId = snapshot.RestaurantTableId;
			runtime.RestaurantSeatIndex = ((snapshot.RestaurantTableId < 0) ? (-1) : snapshot.RestaurantSeatIndex);
			runtime.RestaurantSubState = (ResidentRestaurantSubState)snapshot.RestaurantSubState;
			runtime.RestaurantServiceState = (RestaurantActivityType)snapshot.RestaurantServiceState;
			runtime.IsInRestaurant = currentState == ResidentActivityType.Restaurant && runtime.HasRestaurantAssignment && runtime.HasArrived;
			if (runtime.RestaurantTargetType == ResidentRestaurantTargetType.None && runtime.RestaurantTableId < 0)
			{
				runtime.ClearRestaurantAssignment();
			}
			ApplyModelVisibilityFromRuntime();
			SyncRestaurantDiningPropsWithRuntime();
			if (authoritativeProxy)
			{
				ApplyReliableStatePose(snapshot, wasModelActive);
			}
		}

		private void ApplyModelVisibilityFromRuntime()
		{
			if (_brain != null && Runtime != null)
			{
				bool modelVisible = Runtime.CurrentActivity != ResidentActivityType.Home || Runtime.HasRestaurantAssignment;
				_brain.SetModelVisible(modelVisible);
			}
		}

		private bool TryRestoreRestaurantSnapshot(ResidentSnapshotPacked snapshot, ResidentRuntimeState runtime)
		{
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (restaurantManager == null)
			{
				ResidentDebugLogger.Log("Restaurant snapshot restore cancelled. RestaurantManager missing.", LogType.Warning);
				return false;
			}
			ResidentRestaurantTargetType residentRestaurantTargetType = (ResidentRestaurantTargetType)snapshot.Index1;
			if (residentRestaurantTargetType == ResidentRestaurantTargetType.None)
			{
				residentRestaurantTargetType = ResidentRestaurantTargetType.DiningTable;
			}
			Transform transform;
			try
			{
				transform = ((residentRestaurantTargetType == ResidentRestaurantTargetType.ConveyorTable) ? restaurantManager.GetConveyorSeatTransform(snapshot.TableId) : restaurantManager.GetSeatTransform(snapshot.TableId, snapshot.SeatIndex));
			}
			catch (Exception ex)
			{
				ResidentDebugLogger.Log("Restaurant snapshot restore cancelled. Seat lookup failed. " + ex.Message, LogType.Warning);
				return false;
			}
			if (transform == null)
			{
				ResidentDebugLogger.Log("Restaurant snapshot restore cancelled. Invalid table/seat. Table=" + snapshot.TableId + ", Seat=" + snapshot.SeatIndex, LogType.Warning);
				return false;
			}
			runtime.RestaurantTableId = snapshot.TableId;
			runtime.RestaurantSeatIndex = snapshot.SeatIndex;
			runtime.RestaurantTargetType = residentRestaurantTargetType;
			runtime.RestaurantServiceState = (RestaurantActivityType)snapshot.RestaurantState;
			runtime.RestaurantSubState = (runtime.HasArrived ? ResidentRestaurantSubState.ArrivedAtSeat : ResidentRestaurantSubState.MovingToSeat);
			runtime.TargetDestination = transform.position;
			runtime.TargetRotation = transform.rotation.eulerAngles;
			runtime.HasValidTarget = true;
			runtime.IsAvailable = false;
			runtime.IsInRestaurant = runtime.HasArrived;
			return true;
		}

		public bool TryBuildSnapshotPacked(out ResidentSnapshotPacked snapshot)
		{
			EnsureInitialized();
			snapshot = ResidentSnapshotController.Build(this);
			return Runtime != null;
		}

		private void RegisterRestaurantEvents()
		{
			_restaurantTableStateChangeEventBinding = new EventBinding<RestaurantTableStateChangeEvent>(OnRestaurantTableStateChanged);
			EventBus<RestaurantTableStateChangeEvent>.Register(_restaurantTableStateChangeEventBinding);
			_restaurantCustomerMenuWaitingEventBinding = new EventBinding<RestaurantCustomerMenuWaitingEvent>(OnRestaurantCustomerMenuWaitingChanged);
			EventBus<RestaurantCustomerMenuWaitingEvent>.Register(_restaurantCustomerMenuWaitingEventBinding);
			_conveyorBeltTableStateChangeEventBinding = new EventBinding<ConveyorBeltTableStateChangeEvent>(OnConveyorBeltTableStateChanged);
			EventBus<ConveyorBeltTableStateChangeEvent>.Register(_conveyorBeltTableStateChangeEventBinding);
		}

		private void DeregisterRestaurantEvents()
		{
			if (_restaurantTableStateChangeEventBinding != null)
			{
				EventBus<RestaurantTableStateChangeEvent>.Deregister(_restaurantTableStateChangeEventBinding);
			}
			if (_restaurantCustomerMenuWaitingEventBinding != null)
			{
				EventBus<RestaurantCustomerMenuWaitingEvent>.Deregister(_restaurantCustomerMenuWaitingEventBinding);
			}
			if (_conveyorBeltTableStateChangeEventBinding != null)
			{
				EventBus<ConveyorBeltTableStateChangeEvent>.Deregister(_conveyorBeltTableStateChangeEventBinding);
			}
		}

		private void TryNotifyRestaurantArrival()
		{
			if (Runtime != null && Runtime.CurrentActivity == ResidentActivityType.Restaurant && Runtime.HasRestaurantAssignment && !Runtime.HasArrived && Runtime.HasValidTarget && _brain.Movement.HasArrived())
			{
				if (!ServerArrivedToTable(ResidentName))
				{
					ResidentDebugLogger.Log($"Restaurant arrival rejected. Resident={ResidentName}, Table={Runtime.RestaurantTableId}, Seat={Runtime.RestaurantSeatIndex}", LogType.Warning);
					Leave();
					return;
				}
				Runtime.HasArrived = true;
				Runtime.IsInRestaurant = true;
				Runtime.RestaurantSubState = GetArrivedRestaurantSubState(Runtime.RestaurantServiceState);
				_brain.Movement.StopAtPose(Runtime.TargetDestination, Quaternion.Euler(Runtime.TargetRotation));
			}
		}

		private static ResidentRestaurantSubState GetArrivedRestaurantSubState(RestaurantActivityType serviceState)
		{
			return serviceState switch
			{
				RestaurantActivityType.Order => ResidentRestaurantSubState.Ordering, 
				RestaurantActivityType.Eat => ResidentRestaurantSubState.Eating, 
				RestaurantActivityType.RequestPayment => ResidentRestaurantSubState.RequestingPayment, 
				RestaurantActivityType.Pay => ResidentRestaurantSubState.Paying, 
				RestaurantActivityType.Leave => ResidentRestaurantSubState.Leaving, 
				RestaurantActivityType.LookMenu => ResidentRestaurantSubState.LookingMenu, 
				_ => ResidentRestaurantSubState.ArrivedAtSeat, 
			};
		}

		private void RefreshInteractionAvailability(bool force = false)
		{
			if (base.IsServer && _interaction != null && Runtime != null)
			{
				bool flag = CanInteractWithResident() && !Runtime.IsInteracting;
				bool flag2 = flag;
				if (force || CanInteract.Value != flag)
				{
					CanInteract.Value = flag;
				}
				if (force || CanGaze.Value != flag2)
				{
					CanGaze.Value = flag2;
				}
			}
		}

		private void TryReturnToRestaurantQueueAfterLeave()
		{
			if (_pendingRestaurantQueueReturn && Runtime != null && _brain != null && !Runtime.HasRestaurantAssignment && Runtime.CurrentActivity != ResidentActivityType.Restaurant && !Runtime.IsInRestaurant && (Runtime.HasArrived || !_brain.Movement.HasAgent || !_brain.Movement.AgentEnabled))
			{
				_pendingRestaurantQueueReturn = false;
				Singleton<ResidentManager>.Instance?.AddQueue(this);
			}
		}

		private void SetRestaurantServiceInteraction(bool enabled)
		{
			if (base.IsServer)
			{
				CanGaze.Value = enabled;
				CanInteract.Value = enabled;
			}
		}

		private void EnsureInitialized()
		{
			if (_brain == null)
			{
				Initialize();
			}
		}

		public void SetLocalSimulationEnabled(bool enabled)
		{
			EnsureInitialized();
			_brain.SetLocalSimulationEnabled(enabled);
			if (!enabled && IsNetworkClientProxy())
			{
				DisableAgentTransformSyncForNetworkProxy();
			}
		}

		private bool ShouldRunLocalSimulation()
		{
			NetworkManager singleton = NetworkManager.Singleton;
			if (!(singleton == null) && singleton.IsListening)
			{
				return base.IsServer;
			}
			return true;
		}

		private bool IsNetworkClientProxy()
		{
			NetworkManager singleton = NetworkManager.Singleton;
			if (singleton != null && singleton.IsListening)
			{
				return !base.IsServer;
			}
			return false;
		}

		private void OnNetworkResidentIdChanged(byte previousValue, byte newValue)
		{
			if (!(_residentData != null) && newValue != 0)
			{
				Singleton<ResidentManager>.Instance?.RegisterRuntimeResident(this);
			}
		}

		private void SetNetworkMovementTarget(Vector3 position, Quaternion rotation)
		{
			UpdateNetworkVisualVelocity(position);
			_networkTargetPosition = position;
			_networkTargetRotation = rotation;
			_hasNetworkMovementTarget = true;
		}

		private bool ShouldAcceptMovementSnapshot()
		{
			ResidentRuntimeState runtime = Runtime;
			if (runtime != null && runtime.HasValidTarget)
			{
				return !runtime.HasArrived;
			}
			return false;
		}

		private void ApplyReliableStatePose(ResidentStateSnapshotPacked snapshot, bool wasModelActive)
		{
			ResidentRuntimeState runtime = Runtime;
			if (runtime == null)
			{
				return;
			}
			Vector3 currentPosition = snapshot.CurrentPosition;
			Quaternion quaternion = Quaternion.Euler(0f, ResidentYawQuantize.DecodeYaw(snapshot.CurrentYawQ), 0f);
			int num;
			Vector3 vector;
			if (runtime.HasArrived)
			{
				num = (runtime.HasValidTarget ? 1 : 0);
				if (num != 0)
				{
					vector = runtime.TargetDestination;
					goto IL_004a;
				}
			}
			else
			{
				num = 0;
			}
			vector = currentPosition;
			goto IL_004a;
			IL_004a:
			Vector3 position = vector;
			Quaternion rotation = ((num != 0) ? Quaternion.Euler(runtime.TargetRotation) : quaternion);
			if (runtime.HasArrived)
			{
				SettleNetworkFinalPose(position, rotation);
				return;
			}
			if (!runtime.HasValidTarget)
			{
				_hasNetworkMovementTarget = false;
				return;
			}
			bool num2 = _model != null && _model.activeSelf && !wasModelActive;
			float num3 = Vector3.Distance(base.transform.position, currentPosition);
			if (num2 || num3 > 3f)
			{
				SettleNetworkFinalPose(currentPosition, quaternion);
				SetNetworkMovementTarget(currentPosition, quaternion);
			}
			else
			{
				SetNetworkMovementTarget(currentPosition, quaternion);
			}
		}

		private void SmoothNetworkProxyMovement()
		{
			if (_hasNetworkMovementTarget)
			{
				float num = Vector3.Distance(base.transform.position, _networkTargetPosition);
				float num2 = Mathf.Abs(Mathf.DeltaAngle(base.transform.eulerAngles.y, _networkTargetRotation.eulerAngles.y));
				if (num < 0.02f && num2 < 0.5f)
				{
					SettleNetworkFinalPose(_networkTargetPosition, _networkTargetRotation);
				}
				else if (num > 3f)
				{
					ApplyNetworkProxyPose(_networkTargetPosition, _networkTargetRotation);
					_networkSmoothVelocity = Vector3.zero;
				}
				else
				{
					base.transform.position = Vector3.SmoothDamp(base.transform.position, _networkTargetPosition, ref _networkSmoothVelocity, 0.14f, float.PositiveInfinity, Time.deltaTime);
					base.transform.rotation = Quaternion.Slerp(base.transform.rotation, _networkTargetRotation, Mathf.Clamp01(12f * Time.deltaTime));
				}
			}
		}

		private void SettleNetworkFinalPose(Vector3 position, Quaternion rotation)
		{
			ApplyNetworkProxyPose(position, rotation);
			_networkVisualVelocity = Vector3.zero;
			_networkSmoothVelocity = Vector3.zero;
			_hasNetworkMovementTarget = false;
			_networkTargetPosition = position;
			_networkTargetRotation = rotation;
			_lastNetworkSnapshotApplyTime = Time.time;
		}

		private void ApplyNetworkProxyPose(Vector3 position, Quaternion rotation)
		{
			DisableAgentTransformSyncForNetworkProxy();
			base.transform.SetPositionAndRotation(position, rotation);
		}

		private void DisableAgentTransformSyncForNetworkProxy()
		{
			if (!(_agent == null))
			{
				_agent.updatePosition = false;
				_agent.updateRotation = false;
			}
		}

		private void UpdateNetworkVisualVelocity(Vector3 newPosition)
		{
			float time = Time.time;
			if (_lastNetworkSnapshotApplyTime > 0f)
			{
				float num = Mathf.Max(0.001f, time - _lastNetworkSnapshotApplyTime);
				_networkVisualVelocity = (newPosition - base.transform.position) / num;
			}
			_lastNetworkSnapshotApplyTime = time;
		}

		private void DecayNetworkVisualVelocity()
		{
			if (!(_lastNetworkSnapshotApplyTime <= 0f) && Time.time - _lastNetworkSnapshotApplyTime > 0.5f)
			{
				_networkVisualVelocity = Vector3.zero;
			}
		}

		private bool CanInteractWithResident()
		{
			if (_interaction == null || Runtime == null)
			{
				return false;
			}
			return _interaction.CanInteractWith(ShouldRequireWorkingForInteraction());
		}

		private bool ShouldRequireWorkingForInteraction()
		{
			return true;
		}

		private void CacheOptionalComponents()
		{
			if (_questLinks == null)
			{
				TryGetComponent<ResidentQuestLinks>(out _questLinks);
			}
			if (_residentAudio == null)
			{
				TryGetComponent<ResidentAudio>(out _residentAudio);
			}
			if (_deadFishBuyer == null)
			{
				TryGetComponent<ResidentDeadFishBuyer>(out _deadFishBuyer);
			}
			if (_customization == null)
			{
				TryGetComponent<ResidentCustomization>(out _customization);
			}
			if (_outfitSystem == null)
			{
				_outfitSystem = GetComponentInChildren<OutfitSystem>(includeInactive: true);
			}
			if (_outfitSystem != null)
			{
				_outfitSystem.loadMode = OutfitSystem.LoadMode.Manual;
			}
		}

		private bool TryApplyBakedVisual()
		{
			if (_residentData == null || !_residentData.HasUsableBakedVisual)
			{
				return false;
			}
			GameObject bakedVisualPrefab = _residentData.BakedVisualPrefab;
			if (_appliedBakedVisualPrefab == bakedVisualPrefab && _model != null)
			{
				return true;
			}
			if (bakedVisualPrefab == null || _model == null)
			{
				return false;
			}
			GameObject model = _model;
			Transform transform = model.transform;
			Transform parent = transform.parent;
			bool activeSelf = model.activeSelf;
			Animator animator = _animator;
			GameObject gameObject = UnityEngine.Object.Instantiate(bakedVisualPrefab, parent);
			gameObject.name = model.name + "_Baked";
			gameObject.transform.SetLocalPositionAndRotation(transform.localPosition, transform.localRotation);
			gameObject.transform.localScale = transform.localScale;
			SetLayerRecursive(gameObject, model.layer);
			ResidentBakedVisualBindings residentBakedVisualBindings = gameObject.GetComponent<ResidentBakedVisualBindings>();
			if (residentBakedVisualBindings == null)
			{
				residentBakedVisualBindings = gameObject.AddComponent<ResidentBakedVisualBindings>();
			}
			residentBakedVisualBindings.InitializeFromHierarchy();
			if (residentBakedVisualBindings.Animator == null)
			{
				UtilityMethods.Log("[Resident] Baked visual has no Animator. Falling back to runtime customization. Resident=" + ResidentName, LogType.Warning);
				UnityEngine.Object.Destroy(gameObject);
				return false;
			}
			model.SetActive(value: false);
			_model = gameObject;
			_animator = residentBakedVisualBindings.Animator;
			CopyAnimatorRuntimeSettings(animator, _animator);
			_outfitSystem = residentBakedVisualBindings.OutfitSystem;
			_restaurantPropAnchor = residentBakedVisualBindings.RestaurantPropAnchor;
			_appliedBakedVisualPrefab = bakedVisualPrefab;
			CacheModelRenderers();
			if (_outfitSystem != null)
			{
				_outfitSystem.loadMode = OutfitSystem.LoadMode.Manual;
			}
			if (_customization == null)
			{
				TryGetComponent<ResidentCustomization>(out _customization);
			}
			_customization?.BindOutfitSystem(_outfitSystem);
			CacheAnimatorParameters();
			RefreshBakedVisualOutlineTargets();
			gameObject.SetActive(activeSelf);
			_brain?.RebindModel(gameObject);
			UnityEngine.Object.Destroy(model);
			return true;
		}

		private static void CopyAnimatorRuntimeSettings(Animator source, Animator target)
		{
			if (!(source == null) && !(target == null))
			{
				if (source.runtimeAnimatorController != null)
				{
					target.runtimeAnimatorController = source.runtimeAnimatorController;
				}
				if (target.avatar == null && source.avatar != null)
				{
					target.avatar = source.avatar;
				}
				target.applyRootMotion = source.applyRootMotion;
				target.cullingMode = source.cullingMode;
				target.updateMode = source.updateMode;
				target.stabilizeFeet = source.stabilizeFeet;
				target.enabled = source.enabled;
				target.Rebind();
			}
		}

		private void RefreshBakedVisualOutlineTargets()
		{
			if (_outlinable == null || _model == null)
			{
				return;
			}
			while (_outlinable.OutlineTargetsCount > 0)
			{
				_outlinable.RemoveTarget(_outlinable[0]);
			}
			Renderer[] componentsInChildren = _model.GetComponentsInChildren<Renderer>(includeInactive: true);
			foreach (Renderer renderer in componentsInChildren)
			{
				if (HasUsableOutlineMesh(renderer))
				{
					_outlinable.AddRenderer(renderer);
				}
			}
			_outlinable.enabled = false;
		}

		private bool HasUsableOutlineMesh(Renderer renderer)
		{
			Mesh mesh = null;
			if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
			{
				mesh = skinnedMeshRenderer.sharedMesh;
			}
			else
			{
				if (!(renderer is MeshRenderer))
				{
					return renderer != null;
				}
				MeshFilter component = renderer.GetComponent<MeshFilter>();
				mesh = ((component != null) ? component.sharedMesh : null);
			}
			if (mesh != null)
			{
				return true;
			}
			UtilityMethods.Log("[Resident] Baked visual outline renderer skipped because its mesh is missing. Resident=" + ResidentName + ", Renderer=" + renderer.name + ", Type=" + renderer.GetType().Name, LogType.Warning);
			return false;
		}

		private static void SetLayerRecursive(GameObject root, int layer)
		{
			if (!(root == null))
			{
				root.layer = layer;
				Transform transform = root.transform;
				for (int i = 0; i < transform.childCount; i++)
				{
					SetLayerRecursive(transform.GetChild(i).gameObject, layer);
				}
			}
		}

		private bool ShouldUpdateAnimatorParametersThisFrame()
		{
			if (!_enableSafeVisualUpdateThrottling)
			{
				return true;
			}
			if (_animator == null || !_animator.gameObject.activeInHierarchy)
			{
				return false;
			}
			if (IsHighPriorityAnimatorUpdate())
			{
				return true;
			}
			if (IsModelVisibleToAnyCamera())
			{
				return true;
			}
			float unscaledTime = Time.unscaledTime;
			if (unscaledTime < _nextFarAnimatorUpdateTime)
			{
				return false;
			}
			_nextFarAnimatorUpdateTime = unscaledTime + Mathf.Max(0.02f, _farAnimatorUpdateInterval);
			return true;
		}

		private bool IsHighPriorityAnimatorUpdate()
		{
			ResidentRuntimeState runtime = Runtime;
			if (runtime != null && (runtime.IsInRestaurant || runtime.HasRestaurantAssignment || runtime.IsInteracting || runtime.IsSwimming || runtime.IsWorking))
			{
				return true;
			}
			Camera visualCullingCamera = GetVisualCullingCamera();
			if (visualCullingCamera == null)
			{
				return true;
			}
			float num = Mathf.Max(5f, _fullAnimatorUpdateDistance);
			return (base.transform.position - visualCullingCamera.transform.position).sqrMagnitude <= num * num;
		}

		private bool IsModelVisibleToAnyCamera()
		{
			if (_modelRenderers == null || _modelRenderers.Length == 0)
			{
				CacheModelRenderers();
			}
			if (_modelRenderers == null)
			{
				return false;
			}
			for (int i = 0; i < _modelRenderers.Length; i++)
			{
				Renderer renderer = _modelRenderers[i];
				if (renderer != null && renderer.enabled && renderer.isVisible)
				{
					return true;
				}
			}
			return false;
		}

		private Camera GetVisualCullingCamera()
		{
			if (_visualCullingCamera == null)
			{
				_visualCullingCamera = Camera.main;
			}
			return _visualCullingCamera;
		}

		private void CacheModelRenderers()
		{
			_modelRenderers = ((_model != null) ? _model.GetComponentsInChildren<Renderer>(includeInactive: true) : Array.Empty<Renderer>());
		}

		private async Task ApplyCustomizationWithModelHoldAsync()
		{
			int version = ++_customizationVersion;
			_brain?.BeginModelVisibilityHold();
			try
			{
				await ApplyCustomizationAsync();
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[Resident] Customization apply failed. " + ex.Message, LogType.Warning);
			}
			finally
			{
				if (this != null && version == _customizationVersion)
				{
					_brain?.ReleaseModelVisibilityHold();
				}
			}
		}

		private Task ApplyCustomizationAsync()
		{
			CacheOptionalComponents();
			if (_customization != null)
			{
				return _customization.Apply(_residentData);
			}
			return ResidentCustomization.ApplyToOutfitSystem(_outfitSystem, _residentData, this);
		}

		private void RequestCustomizationApply()
		{
			if (_brain == null)
			{
				return;
			}
			if (_residentData != null && _residentData.HasUsableBakedVisual)
			{
				_pendingCustomizationApply = false;
				_brain.ReleaseModelVisibilityHold();
				return;
			}
			_brain.BeginModelVisibilityHold();
			if (!_outfitStartPhaseCompleted)
			{
				_pendingCustomizationApply = true;
				return;
			}
			_pendingCustomizationApply = false;
			ApplyCustomizationWithModelHoldAsync();
		}

		private async Task ApplyCustomizationAsync(int version)
		{
			_isCustomizationApplying = true;
			ShowModel();
			Task task = ((!(_customization != null)) ? ResidentCustomization.ApplyToOutfitSystem(_outfitSystem, _residentData, this) : _customization.Apply(_residentData));
			await task;
			if (version == _customizationApplyVersion)
			{
				_isCustomizationApplying = false;
				if (Runtime != null && Runtime.CurrentActivity == ResidentActivityType.Home && !Runtime.HasRestaurantAssignment)
				{
					HideModel();
				}
			}
		}

		public string GetRuntimeDebugText()
		{
			if (_brain == null || Runtime == null)
			{
				return "Resident: " + ResidentName + "\nStatus: Not initialized";
			}
			float num = ((_brain.Movement != null) ? _brain.Movement.RemainingDistance : (-1f));
			Vector3 vector = ((_brain.Movement != null) ? _brain.Movement.Destination : base.transform.position);
			bool flag = IsNetworkClientProxy();
			bool flag2 = ShouldRunLocalSimulation();
			float num2 = (_hasNetworkMovementTarget ? Vector3.Distance(base.transform.position, _networkTargetPosition) : 0f);
			return $"Resident: {ResidentName} [{ResidentId}]\n" + "ResidentData: " + ((_residentData != null) ? _residentData.name : "null") + "\n" + $"ActorType: {_actorType}\n\n" + "StateMachine: " + _brain.StateMachine.CurrentName + (flag ? " (client proxy/stale)" : string.Empty) + "\n" + $"NetworkClientProxy: {flag}\n" + $"LocalSimulation: {flag2}\n" + $"Activity: {Runtime.CurrentActivity}\n" + $"PreviousActivity: {Runtime.PreviousActivity}\n" + $"SystemState: {Runtime.SystemState}\n" + $"ScheduleIndex: {Runtime.CurrentScheduleEntryIndex}\n" + $"WaypointIndex: {Runtime.CurrentWaypointIndex}\n" + $"PatrolIndex: {Runtime.CurrentPatrolEntryPointIndex}\n\n" + $"HasValidTarget: {Runtime.HasValidTarget}\n" + $"TargetDestination: {Runtime.TargetDestination:F2}\n" + $"AgentDestination: {vector:F2}\n" + $"TargetRotation: {Runtime.TargetRotation:F1}\n" + "RemainingDistance: " + ((num >= 0f) ? num.ToString("0.00") : "n/a") + "\n" + $"HasArrived: {Runtime.HasArrived}\n\n" + $"HasNetworkMovementTarget: {_hasNetworkMovementTarget}\n" + $"NetworkTargetPosition: {_networkTargetPosition:F2}\n" + $"NetworkTargetDistance: {num2:0.00}\n\n" + $"Available: {Runtime.IsAvailable}\n" + $"Working: {Runtime.IsWorking}\n" + $"Swimming: {Runtime.IsSwimming}\n" + $"Interacting: {Runtime.IsInteracting}\n" + $"InRestaurant: {Runtime.IsInRestaurant}\n" + $"CustomizationApplying: {_isCustomizationApplying}\n" + $"ModelActive: {_model != null && _model.activeSelf}\n\n" + $"HasRestaurantAssignment: {Runtime.HasRestaurantAssignment}\n" + $"RestaurantTargetType: {Runtime.RestaurantTargetType}\n" + $"RestaurantTableId: {Runtime.RestaurantTableId}\n" + $"RestaurantSeatIndex: {Runtime.RestaurantSeatIndex}\n" + $"RestaurantSubState: {Runtime.RestaurantSubState}\n" + $"RestaurantServiceState: {Runtime.RestaurantServiceState}\n" + $"PendingQueueReturn: {_pendingRestaurantQueueReturn}";
		}

		private List<string> GetRelatedQuestIds()
		{
			if (!(_questLinks != null))
			{
				return null;
			}
			return _questLinks.RelatedQuestIds;
		}

		protected override void __initializeVariables()
		{
			if (_networkResidentId == null)
			{
				throw new Exception("ResidentController._networkResidentId cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_networkResidentId.Initialize(this);
			__nameNetworkVariable(_networkResidentId, "_networkResidentId");
			NetworkVariableFields.Add(_networkResidentId);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3341648715u, __rpc_handler_3341648715, "PlayTemporaryExpressionClientRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(4066351243u, __rpc_handler_4066351243, "PlayPurchaseWaveClientRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(891743732u, __rpc_handler_891743732, "ServerRequestPurchaseReactionRpc", RpcInvokePermission.Everyone);
			__registerRpc(3315275557u, __rpc_handler_3315275557, "ServerTryStartInteractRpc", RpcInvokePermission.Everyone);
			__registerRpc(3365836575u, __rpc_handler_3365836575, "ServerTrySellHeldDeadFishRpc", RpcInvokePermission.Everyone);
			__registerRpc(1271522367u, __rpc_handler_1271522367, "ServerEnableInteractRpc", RpcInvokePermission.Everyone);
			__registerRpc(3718605444u, __rpc_handler_3718605444, "ServerShowDialogueBillboardTextRpc", RpcInvokePermission.Everyone);
			__registerRpc(4243498832u, __rpc_handler_4243498832, "ServerShowLocalizedDialogueBillboardTextRpc", RpcInvokePermission.Everyone);
			__registerRpc(604047052u, __rpc_handler_604047052, "ServerHideDialogueBillboardRpc", RpcInvokePermission.Everyone);
			__registerRpc(3956984337u, __rpc_handler_3956984337, "ClientShowDialogueBillboardTextRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(2595555987u, __rpc_handler_2595555987, "ClientShowLocalizedDialogueBillboardTextRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(3180465951u, __rpc_handler_3180465951, "ClientHideDialogueBillboardRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(892847299u, __rpc_handler_892847299, "ClientOpenResidentUiRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(276426175u, __rpc_handler_276426175, "ClientShowBusyToastRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(1057243664u, __rpc_handler_1057243664, "ClientHandleOwnLockedInteractRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(941392823u, __rpc_handler_941392823, "ServerRestartOwnInteractionRpc", RpcInvokePermission.Everyone);
			__registerRpc(3847933120u, __rpc_handler_3847933120, "ServerPlayDialogueSfxRpc", RpcInvokePermission.Everyone);
			__registerRpc(2054934730u, __rpc_handler_2054934730, "ClientPlayDialogueSfxRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(3258338804u, __rpc_handler_3258338804, "ClientApplyResidentStateBatchRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(905712343u, __rpc_handler_905712343, "ClientApplyResidentMovementBatchRevisionRpc", RpcInvokePermission.Server);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3341648715(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out ResidentExpressionType value, default(FastBufferWriter.ForEnums));
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentController)target).PlayTemporaryExpressionClientRevisionRpc(value, value2, value3);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4066351243(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentController)target).PlayPurchaseWaveClientRevisionRpc(value);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_891743732(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentController)target).ServerRequestPurchaseReactionRpc(ext);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3315275557(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentController)target).ServerTryStartInteractRpc(ext);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3365836575(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentController)target).ServerTrySellHeldDeadFishRpc(value, value2, ext);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1271522367(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentController)target).ServerEnableInteractRpc(ext);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3718605444(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
				RpcParams ext = rpcParams.Ext;
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentController)target).ServerShowDialogueBillboardTextRpc(s, ext);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4243498832(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
				reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
				string s3 = null;
				if (value3)
				{
					reader.ReadValueSafe(out s3, false);
				}
				RpcParams ext = rpcParams.Ext;
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentController)target).ServerShowLocalizedDialogueBillboardTextRpc(s, s2, s3, ext);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_604047052(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentController)target).ServerHideDialogueBillboardRpc(ext);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3956984337(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentController)target).ClientShowDialogueBillboardTextRevisionRpc(s, value2);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2595555987(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
				reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
				string s3 = null;
				if (value3)
				{
					reader.ReadValueSafe(out s3, false);
				}
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value4);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentController)target).ClientShowLocalizedDialogueBillboardTextRevisionRpc(s, s2, s3, value4);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3180465951(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentController)target).ClientHideDialogueBillboardRevisionRpc(value);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_892847299(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentController)target).ClientOpenResidentUiRevisionRpc(value, value2);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_276426175(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentController)target).ClientShowBusyToastRevisionRpc(value, value2);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1057243664(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentController)target).ClientHandleOwnLockedInteractRevisionRpc(value, value2);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_941392823(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentController)target).ServerRestartOwnInteractionRpc(ext);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3847933120(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentController)target).ServerPlayDialogueSfxRpc(value, ext);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2054934730(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentController)target).ClientPlayDialogueSfxRevisionRpc(value, value2);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3258338804(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				ResidentStateSnapshotPacked[] value2 = null;
				if (value)
				{
					reader.ReadValueSafe(out value2, default(FastBufferWriter.ForNetworkSerializable));
				}
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentController)target).ClientApplyResidentStateBatchRevisionRpc(value2, value3);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_905712343(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				ResidentMovementSnapshotPacked[] value2 = null;
				if (value)
				{
					reader.ReadValueSafe(out value2, default(FastBufferWriter.ForNetworkSerializable));
				}
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentController)target).ClientApplyResidentMovementBatchRevisionRpc(value2, value3);
				((ResidentController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "ResidentController";
		}
	}
}
