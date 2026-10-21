using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using PaperPixelGames.Core.Boss.Attacks;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking.Chat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PaperPixelGames.Core.Boss
{
	[RequireComponent(typeof(NetworkObject))]
	public sealed class CrabBossController : NetworkBehaviour
	{
		[Serializable]
		private sealed class PullDropDamageTarget
		{
			[SerializeField]
			private HarpoonPullDropTarget _target;

			[SerializeField]
			[Min(0f)]
			private float _damageMultiplier = 1f;

			public HarpoonPullDropTarget Target => _target;

			public float DamageMultiplier => _damageMultiplier;
		}

		private const int PhaseCount = 3;

		[Header("Phase 1")]
		[SerializeField]
		private float _phase1Health;

		[SerializeField]
		private float _phase1ShieldHealth;

		[SerializeField]
		private float _phase1AttackInterval;

		[Header("Phase 2")]
		[SerializeField]
		private float _phase2Health;

		[SerializeField]
		private float _phase2ShieldHealth;

		[SerializeField]
		private float _phase2AttackInterval;

		[Header("Phase 3")]
		[SerializeField]
		private float _phase3Health;

		[SerializeField]
		private float _phase3ShieldHealth;

		[SerializeField]
		private float _phase3AttackInterval;

		[Header("Damage Per Hit")]
		[SerializeField]
		private float _harpoonShieldDamage;

		[SerializeField]
		private float _fallingRockShieldDamage;

		[SerializeField]
		private float _harpoonBodyDamage;

		[SerializeField]
		private float _fallingRockBodyDamage;

		[Header("Harpoon Hitbox")]
		[SerializeField]
		private bool _autoFitHarpoonHitbox = true;

		[SerializeField]
		private BoxCollider _harpoonHitbox;

		[SerializeField]
		private Vector3 _harpoonHitboxPadding = new Vector3(0.5f, 0.5f, 0.5f);

		[Header("Pull Drop Damage Targets")]
		[SerializeField]
		private PullDropDamageTarget[] _pullDropDamageTargets;

		[SerializeField]
		private bool _autoDiscoverPullDropDamageTargets = true;

		[SerializeField]
		private Transform _pullDropImpactAnchor;

		[SerializeField]
		private float _pullDropImpactHeightOffset;

		[SerializeField]
		[Min(0.01f)]
		private float _pullDropFallSpeed = 30f;

		[Header("Defeated Reward")]
		[SerializeField]
		private GameObject _poseidonTridentReward;

		[SerializeField]
		[Min(0f)]
		private float _defeatedDespawnDelay = 15f;

		[Header("Phase Transition")]
		[SerializeField]
		private float _phaseTransitionDuration;

		[Header("Attacks")]
		[SerializeField]
		private CrabBossAttackSettings[] _attacks;

		[SerializeField]
		private bool _announceAttacksInChat;

		[Header("Facing")]
		[SerializeField]
		private bool _faceNearestPlayerDuringCombat = true;

		[SerializeField]
		[Min(0.01f)]
		private float _facingTargetRefreshInterval = 0.1f;

		[SerializeField]
		[Min(0f)]
		private float _facingTurnSpeedDegrees = 180f;

		[SerializeField]
		[Min(0.01f)]
		private float _facingSmoothTime = 0.15f;

		[SerializeField]
		private float _facingYawOffset;

		[Header("Debug")]
		[SerializeField]
		private bool _log;

		private readonly NetworkVariable<CrabBossEncounterState> _encounterState = new NetworkVariable<CrabBossEncounterState>(CrabBossEncounterState.Inactive);

		private readonly NetworkVariable<int> _currentPhase = new NetworkVariable<int>(0);

		private readonly NetworkVariable<int> _lockedPlayerCount = new NetworkVariable<int>(0);

		private readonly NetworkVariable<float> _currentHealth = new NetworkVariable<float>(0f);

		private readonly NetworkVariable<float> _maximumHealth = new NetworkVariable<float>(0f);

		private readonly NetworkVariable<float> _currentShieldHealth = new NetworkVariable<float>(0f);

		private readonly NetworkVariable<float> _maximumShieldHealth = new NetworkVariable<float>(0f);

		private readonly NetworkVariable<int> _playersInRoomCount = new NetworkVariable<int>(0);

		private readonly NetworkVariable<bool> _isIdle = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<bool> _isAttackRunning = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<CrabBossAttackId> _currentAttackId = new NetworkVariable<CrabBossAttackId>(CrabBossAttackId.None);

		private readonly NetworkVariable<uint> _currentAttackToken = new NetworkVariable<uint>(0u);

		private readonly NetworkVariable<bool> _isPoseidonTridentRewardAvailable = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<bool> _isQuestRuntimeAvailable = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<bool> _isBossRuntimeActive = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<float> _targetFacingYaw = new NetworkVariable<float>(0f);

		private readonly HashSet<ulong> _playersInRoom = new HashSet<ulong>();

		private readonly HashSet<ulong> _encounterParticipantClientIds = new HashSet<ulong>();

		private readonly HashSet<HarpoonPullDropTarget> _subscribedPullDropDamageTargets = new HashSet<HarpoonPullDropTarget>();

		private readonly Dictionary<HarpoonPullDropTarget, float> _resolvedPullDropDamageTargets = new Dictionary<HarpoonPullDropTarget, float>();

		private readonly List<HarpoonPullDropTarget> _orderedPullDropDamageTargets = new List<HarpoonPullDropTarget>();

		private readonly HashSet<HarpoonPullDropTarget> _completedPullDropDamageTargets = new HashSet<HarpoonPullDropTarget>();

		private readonly List<float> _pendingPullDropDamageMultipliers = new List<float>();

		private readonly List<CrabBossAttackSettings> _eligibleAttacks = new List<CrabBossAttackSettings>();

		private readonly HashSet<CrabBossAttackId> _completedPhaseAttackIds = new HashSet<CrabBossAttackId>();

		private readonly List<PPG_Player> _facingPlayers = new List<PPG_Player>();

		private CrabBossAttackBase[] _attackHandlers;

		private CrabBossPresentationController _presentation;

		private GameObject[] _runtimeChildRoots;

		private bool[] _runtimeChildRootInitialStates;

		private bool[] _attackHandlerInitialStates;

		private QuestManager _subscribedQuestManager;

		private Coroutine _attackLoopCoroutine;

		private Coroutine _attackTimeoutCoroutine;

		private Coroutine _attackRepeatCoroutine;

		private Coroutine _phaseTransitionCoroutine;

		private Coroutine _poseidonRewardGuaranteeCoroutine;

		private Coroutine _attackAnimationStartCoroutine;

		private CrabBossAttackId _lastAttackId;

		private uint _nextAttackToken;

		private CrabBossEncounterState _resumeState;

		private int _pendingTransitionPhase;

		private bool _playersManagerSubscribed;

		private bool _attackSequenceActive;

		private bool _automaticFightStartSuppressed;

		private CrabBossAttackSettings _activeAttackSequence;

		private CrabBossAttackBase _activeAttackHandler;

		private int _attackSequenceIteration;

		private uint _localAttackAnimationToken;

		private CrabBossAttackAnimation _localAttackAnimation;

		private double _nextFacingTargetRefreshTime;

		private float _facingAngularVelocity;

		private ulong? _facingOverridePlayerClientId;

		private bool _runtimeAvailabilityApplied;

		private bool _appliedRuntimeAvailability;

		private bool _questStateResolved;

		private bool _bossDespawnScheduled;

		[Header("Trident Reward Presentation")]
		[SerializeField]
		[Min(0f)]
		private float _poseidonTridentRiseHeight = 5f;

		[SerializeField]
		[Min(0.01f)]
		private float _poseidonTridentRiseDuration = 8f;

		[SerializeField]
		[Min(0f)]
		private float _poseidonTridentAutoEquipDelay = 100f;

		[SerializeField]
		private GameObject _poseidonTridentShinePrefab;

		[SerializeField]
		private Vector3 _poseidonTridentShineLocalOffset = new Vector3(0f, 0f, 0.4f);

		[SerializeField]
		[Min(0.01f)]
		private float _poseidonTridentShineScale = 1f;

		[Header("Defeated Return Portal")]
		[SerializeField]
		private CrabBossReturnPortal _defeatedReturnPortalPrefab;

		[SerializeField]
		[Min(0.1f)]
		private float _defeatedReturnPortalRadius = 1f;

		private readonly NetworkVariable<Vector3> _defeatedWorldPosition = new NetworkVariable<Vector3>();

		private readonly NetworkVariable<double> _defeatedAtServerTime = new NetworkVariable<double>(0.0);

		private readonly NetworkVariable<bool> _isDefeatedReturnPortalAvailable = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<bool> _isPoseidonTridentRewardGranted = new NetworkVariable<bool>(value: false);

		private Tween _poseidonTridentRiseTween;

		private GameObject _poseidonTridentShineInstance;

		private CrabBossReturnPortal _defeatedReturnPortalInstance;

		private Coroutine _localPoseidonTridentEquipCoroutine;

		private bool _localPoseidonTridentEquipped;

		private bool _refreshDefeatedPresentation;

		public CrabBossEncounterState EncounterState => _encounterState.Value;

		public int CurrentPhase => _currentPhase.Value;

		public int LockedPlayerCount => _lockedPlayerCount.Value;

		public float CurrentHealth => _currentHealth.Value;

		public float MaximumHealth => _maximumHealth.Value;

		public float CurrentEncounterHealth => ResolveCurrentEncounterHealth();

		public float MaximumEncounterHealth => ResolveMaximumEncounterHealth();

		public float CurrentShieldHealth => _currentShieldHealth.Value;

		public float MaximumShieldHealth => _maximumShieldHealth.Value;

		public int PlayersInRoomCount => _playersInRoomCount.Value;

		public bool IsIdle => _isIdle.Value;

		public bool IsAttackRunning => _isAttackRunning.Value;

		public CrabBossAttackId CurrentAttackId => _currentAttackId.Value;

		public uint CurrentAttackToken => _currentAttackToken.Value;

		public uint LocalAttackAnimationToken => _localAttackAnimationToken;

		public bool IsPoseidonTridentRewardAvailable => _isPoseidonTridentRewardAvailable.Value;

		public event Action<int> PhaseStarted;

		public event Action<int> PhaseCompleted;

		public event Action ShieldBroken;

		public event Action IdleStarted;

		public event Action IdleEnded;

		public event Action<CrabBossAttackCommand> AttackStarted;

		public event Action<CrabBossAttackCommand, CrabBossAttackEndReason> AttackEnded;

		public event Action SleepStarted;

		public event Action WokeUp;

		public event Action Defeated;

		public void PlayLocalCrabAnimatorTrigger(CrabBossAnimatorTrigger trigger)
		{
			if ((base.IsClient || base.IsServer) && trigger != CrabBossAnimatorTrigger.None && !(_presentation == null))
			{
				_presentation.ResetAnimatorTrigger(trigger);
				_presentation.PlayAnimatorTrigger(trigger);
			}
		}

		public void ResetLocalCrabAnimatorTrigger(CrabBossAnimatorTrigger trigger)
		{
			if ((base.IsClient || base.IsServer) && trigger != CrabBossAnimatorTrigger.None)
			{
				_presentation?.ResetAnimatorTrigger(trigger);
			}
		}

		public float GetEncounterHealth01()
		{
			float maximumEncounterHealth = MaximumEncounterHealth;
			if (!(maximumEncounterHealth > 0f))
			{
				return 0f;
			}
			return Mathf.Clamp01(CurrentEncounterHealth / maximumEncounterHealth);
		}

		public float GetEncounterHealthBoundary01(int completedPhase)
		{
			float maximumEncounterHealth = MaximumEncounterHealth;
			if (maximumEncounterHealth <= 0f)
			{
				return 0f;
			}
			float num = 0f;
			for (int i = completedPhase + 1; i <= 3; i++)
			{
				num += GetMaximumHealthForPhase(i);
			}
			return Mathf.Clamp01(num / maximumEncounterHealth);
		}

		private void Awake()
		{
			CacheAttackHandlers();
			_presentation = GetComponent<CrabBossPresentationController>();
			ConfigureHarpoonHitbox();
			CacheRuntimeObjects();
			ApplyRuntimeAvailability(available: false);
		}

		private void CacheRuntimeObjects()
		{
			int childCount = base.transform.childCount;
			_runtimeChildRoots = new GameObject[childCount];
			_runtimeChildRootInitialStates = new bool[childCount];
			for (int i = 0; i < childCount; i++)
			{
				GameObject gameObject = base.transform.GetChild(i).gameObject;
				_runtimeChildRoots[i] = gameObject;
				_runtimeChildRootInitialStates[i] = gameObject.activeSelf;
			}
			_attackHandlerInitialStates = new bool[_attackHandlers.Length];
			for (int j = 0; j < _attackHandlers.Length; j++)
			{
				_attackHandlerInitialStates[j] = _attackHandlers[j] != null && _attackHandlers[j].enabled;
			}
		}

		private void ApplyRuntimeAvailability(bool available)
		{
			if (_runtimeAvailabilityApplied && _appliedRuntimeAvailability == available)
			{
				return;
			}
			_runtimeAvailabilityApplied = true;
			_appliedRuntimeAvailability = available;
			if (!available && _presentation != null)
			{
				_presentation.enabled = false;
			}
			if (_runtimeChildRoots != null)
			{
				for (int i = 0; i < _runtimeChildRoots.Length; i++)
				{
					GameObject gameObject = _runtimeChildRoots[i];
					if (gameObject != null && gameObject != _poseidonTridentReward)
					{
						gameObject.SetActive(available && _runtimeChildRootInitialStates[i]);
					}
				}
			}
			if (available && _presentation != null)
			{
				_presentation.enabled = true;
			}
			if (_attackHandlers != null)
			{
				for (int j = 0; j < _attackHandlers.Length; j++)
				{
					CrabBossAttackBase crabBossAttackBase = _attackHandlers[j];
					if (crabBossAttackBase != null)
					{
						bool flag = _attackHandlerInitialStates == null || j >= _attackHandlerInitialStates.Length || _attackHandlerInitialStates[j];
						crabBossAttackBase.enabled = available & flag;
					}
				}
			}
			if (_harpoonHitbox != null)
			{
				_harpoonHitbox.enabled = available;
			}
		}

		private void TrySubscribeAndRefreshQuestState()
		{
			if (!base.IsServer)
			{
				return;
			}
			QuestManager instance = QuestManager.Instance;
			if (_subscribedQuestManager != instance)
			{
				UnsubscribeQuestManager();
				_subscribedQuestManager = instance;
				if (_subscribedQuestManager != null)
				{
					_subscribedQuestManager.QuestStateChanged += OnQuestStateChanged;
				}
			}
			if (_subscribedQuestManager == null || !_subscribedQuestManager.IsStateReady)
			{
				_questStateResolved = false;
				SetQuestRuntimeAvailability(available: false);
			}
			else
			{
				_questStateResolved = true;
				RefreshQuestRuntimeAvailability();
			}
		}

		private void UnsubscribeQuestManager()
		{
			if (_subscribedQuestManager != null)
			{
				_subscribedQuestManager.QuestStateChanged -= OnQuestStateChanged;
			}
			_subscribedQuestManager = null;
			_questStateResolved = false;
		}

		private void OnQuestStateChanged()
		{
			if (base.IsServer)
			{
				RefreshQuestRuntimeAvailability();
			}
		}

		private void RefreshQuestRuntimeAvailability()
		{
			QuestReferenceHub instance = QuestReferenceHub.Instance;
			if (instance == null)
			{
				SetQuestRuntimeAvailability(available: false);
				return;
			}
			bool num = instance.IsCompleted(QuestId.DefeatTheRedKingCrab);
			bool flag = !num && instance.IsActiveOrCompleted(QuestId.DiscoverTheLighthouse);
			SetQuestRuntimeAvailability(flag);
			if (num && _encounterState.Value != CrabBossEncounterState.Defeated)
			{
				if (_encounterState.Value == CrabBossEncounterState.Inactive && !TryRestorePendingPoseidonTridentReward())
				{
					_questStateResolved = false;
				}
				else
				{
					ServerScheduleBossDespawn();
				}
			}
			else if (flag && _playersInRoom.Count > 0 && !_automaticFightStartSuppressed && _encounterState.Value == CrabBossEncounterState.Inactive)
			{
				ServerStartFight();
			}
		}

		private void SetQuestRuntimeAvailability(bool available)
		{
			if (_isQuestRuntimeAvailable.Value != available)
			{
				_isQuestRuntimeAvailable.Value = available;
			}
			SetRoomTriggerAvailability(available);
			if (!available)
			{
				ServerSetPullDropTargetOutlineVisibility(visible: false);
				ServerDisablePulledDropDamageTargets();
			}
		}

		private void OnQuestRuntimeAvailabilityChanged(bool previousValue, bool currentValue)
		{
			SetRoomTriggerAvailability(currentValue);
			if (!currentValue && _encounterState.Value == CrabBossEncounterState.Inactive)
			{
				ApplyRuntimeAvailability(available: false);
			}
		}

		private void SetRoomTriggerAvailability(bool available)
		{
			CrabBossRoomTrigger[] array = UnityEngine.Object.FindObjectsByType<CrabBossRoomTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			foreach (CrabBossRoomTrigger crabBossRoomTrigger in array)
			{
				if (crabBossRoomTrigger != null && crabBossRoomTrigger.IsForBoss(this))
				{
					crabBossRoomTrigger.SetEncounterAvailable(available);
				}
			}
		}

		private void SetBossRuntimeActive(bool active)
		{
			if (_isBossRuntimeActive.Value != active)
			{
				_isBossRuntimeActive.Value = active;
			}
			ApplyRuntimeAvailability(active);
		}

		private void OnBossRuntimeActiveChanged(bool previousValue, bool currentValue)
		{
			ApplyRuntimeAvailability(currentValue);
		}

		private void ConfigureHarpoonHitbox()
		{
			if (!_autoFitHarpoonHitbox)
			{
				return;
			}
			SkinnedMeshRenderer[] componentsInChildren = GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
			if (componentsInChildren.Length == 0)
			{
				UtilityMethods.Log("[CrabBoss][Controller] Harpoon hitbox could not be fitted. No SkinnedMeshRenderer was found under the boss.", LogType.Warning);
				return;
			}
			if (_harpoonHitbox == null)
			{
				GameObject gameObject = new GameObject("HarpoonHitbox");
				gameObject.layer = base.gameObject.layer;
				gameObject.transform.SetParent(base.transform, worldPositionStays: false);
				_harpoonHitbox = gameObject.AddComponent<BoxCollider>();
			}
			bool hasBounds = false;
			Bounds bounds = default;
			foreach (SkinnedMeshRenderer skinnedMeshRenderer in componentsInChildren)
			{
				if (!(skinnedMeshRenderer == null))
				{
					Bounds bounds2 = skinnedMeshRenderer.bounds;
					Vector3 min = bounds2.min;
					Vector3 max = bounds2.max;
					EncapsulateHarpoonHitboxPoint(_harpoonHitbox.transform.InverseTransformPoint(new Vector3(min.x, min.y, min.z)), ref bounds, ref hasBounds);
					EncapsulateHarpoonHitboxPoint(_harpoonHitbox.transform.InverseTransformPoint(new Vector3(min.x, min.y, max.z)), ref bounds, ref hasBounds);
					EncapsulateHarpoonHitboxPoint(_harpoonHitbox.transform.InverseTransformPoint(new Vector3(min.x, max.y, min.z)), ref bounds, ref hasBounds);
					EncapsulateHarpoonHitboxPoint(_harpoonHitbox.transform.InverseTransformPoint(new Vector3(min.x, max.y, max.z)), ref bounds, ref hasBounds);
					EncapsulateHarpoonHitboxPoint(_harpoonHitbox.transform.InverseTransformPoint(new Vector3(max.x, min.y, min.z)), ref bounds, ref hasBounds);
					EncapsulateHarpoonHitboxPoint(_harpoonHitbox.transform.InverseTransformPoint(new Vector3(max.x, min.y, max.z)), ref bounds, ref hasBounds);
					EncapsulateHarpoonHitboxPoint(_harpoonHitbox.transform.InverseTransformPoint(new Vector3(max.x, max.y, min.z)), ref bounds, ref hasBounds);
					EncapsulateHarpoonHitboxPoint(_harpoonHitbox.transform.InverseTransformPoint(new Vector3(max.x, max.y, max.z)), ref bounds, ref hasBounds);
				}
			}
			if (!hasBounds)
			{
				_harpoonHitbox.enabled = false;
				return;
			}
			Vector3 vector = new Vector3(Mathf.Max(0f, _harpoonHitboxPadding.x), Mathf.Max(0f, _harpoonHitboxPadding.y), Mathf.Max(0f, _harpoonHitboxPadding.z));
			_harpoonHitbox.center = bounds.center;
			_harpoonHitbox.size = bounds.size + vector * 2f;
			_harpoonHitbox.isTrigger = true;
			_harpoonHitbox.enabled = true;
		}

		private static void EncapsulateHarpoonHitboxPoint(Vector3 point, ref Bounds bounds, ref bool hasBounds)
		{
			if (!hasBounds)
			{
				bounds = new Bounds(point, Vector3.zero);
				hasBounds = true;
			}
			else
			{
				bounds.Encapsulate(point);
			}
		}

		private void Update()
		{
			if (!base.IsSpawned)
			{
				_facingAngularVelocity = 0f;
				return;
			}
			if (base.IsServer && !_questStateResolved)
			{
				TrySubscribeAndRefreshQuestState();
			}
			if (!_isBossRuntimeActive.Value)
			{
				_facingAngularVelocity = 0f;
				return;
			}
			if (base.IsServer && _faceNearestPlayerDuringCombat && IsCombatState(_encounterState.Value) && base.NetworkManager != null && base.NetworkManager.ServerTime.Time >= _nextFacingTargetRefreshTime)
			{
				_nextFacingTargetRefreshTime = base.NetworkManager.ServerTime.Time + (double)_facingTargetRefreshInterval;
				ServerRefreshFacingTarget();
			}
			if (base.IsClient || base.IsServer)
			{
				UpdateFacing(Time.deltaTime);
			}
		}

		private void UpdateFacing(float deltaTime)
		{
			if (_facingTurnSpeedDegrees <= 0f)
			{
				_facingAngularVelocity = 0f;
			}
			else if (!(deltaTime <= 0f))
			{
				float y = Mathf.SmoothDampAngle(base.transform.eulerAngles.y, _targetFacingYaw.Value, ref _facingAngularVelocity, Mathf.Max(0.01f, _facingSmoothTime), _facingTurnSpeedDegrees, deltaTime);
				base.transform.rotation = Quaternion.Euler(0f, y, 0f);
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			_facingAngularVelocity = 0f;
			NetworkVariable<bool> isPoseidonTridentRewardAvailable = _isPoseidonTridentRewardAvailable;
			isPoseidonTridentRewardAvailable.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isPoseidonTridentRewardAvailable.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnPoseidonTridentRewardAvailabilityChanged));
			NetworkVariable<bool> isDefeatedReturnPortalAvailable = _isDefeatedReturnPortalAvailable;
			isDefeatedReturnPortalAvailable.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isDefeatedReturnPortalAvailable.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnDefeatedReturnPortalAvailabilityChanged));
			NetworkVariable<bool> isPoseidonTridentRewardGranted = _isPoseidonTridentRewardGranted;
			isPoseidonTridentRewardGranted.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isPoseidonTridentRewardGranted.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnPoseidonTridentRewardGranted));
			NetworkVariable<bool> isQuestRuntimeAvailable = _isQuestRuntimeAvailable;
			isQuestRuntimeAvailable.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isQuestRuntimeAvailable.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnQuestRuntimeAvailabilityChanged));
			NetworkVariable<bool> isBossRuntimeActive = _isBossRuntimeActive;
			isBossRuntimeActive.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isBossRuntimeActive.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnBossRuntimeActiveChanged));
			ApplyPoseidonTridentRewardVisual(_isPoseidonTridentRewardAvailable.Value);
			ApplyRuntimeAvailability(_isBossRuntimeActive.Value);
			ApplyDefeatedReturnPortalVisual(_isDefeatedReturnPortalAvailable.Value);
			if (_isPoseidonTridentRewardGranted.Value)
			{
				StartLocalPoseidonTridentEquip();
			}
			SetRoomTriggerAvailability(_isQuestRuntimeAvailable.Value);
			if (base.IsServer)
			{
				_playersInRoom.Clear();
				_automaticFightStartSuppressed = false;
				ResetRuntimeState();
				_targetFacingYaw.Value = base.transform.eulerAngles.y;
				SubscribePlayersManager();
				SubscribePullDropDamageTargets();
				ServerSetPullDropTargetOutlineVisibility(visible: false);
				TrySubscribeAndRefreshQuestState();
			}
			else if (base.IsClient)
			{
				ServerRequestLateJoinStateRpc();
			}
		}

		public override void OnNetworkDespawn()
		{
			_facingAngularVelocity = 0f;
			NetworkVariable<bool> isPoseidonTridentRewardAvailable = _isPoseidonTridentRewardAvailable;
			isPoseidonTridentRewardAvailable.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isPoseidonTridentRewardAvailable.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnPoseidonTridentRewardAvailabilityChanged));
			NetworkVariable<bool> isDefeatedReturnPortalAvailable = _isDefeatedReturnPortalAvailable;
			isDefeatedReturnPortalAvailable.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isDefeatedReturnPortalAvailable.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnDefeatedReturnPortalAvailabilityChanged));
			NetworkVariable<bool> isPoseidonTridentRewardGranted = _isPoseidonTridentRewardGranted;
			isPoseidonTridentRewardGranted.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isPoseidonTridentRewardGranted.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnPoseidonTridentRewardGranted));
			CleanupDefeatedRewardPresentation();
			NetworkVariable<bool> isQuestRuntimeAvailable = _isQuestRuntimeAvailable;
			isQuestRuntimeAvailable.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isQuestRuntimeAvailable.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnQuestRuntimeAvailabilityChanged));
			NetworkVariable<bool> isBossRuntimeActive = _isBossRuntimeActive;
			isBossRuntimeActive.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isBossRuntimeActive.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnBossRuntimeActiveChanged));
			UnsubscribeQuestManager();
			UnsubscribePullDropDamageTargets();
			UnsubscribePlayersManager();
			StopAllBossCoroutines();
			ClearAttackSequenceRuntime();
			_playersInRoom.Clear();
			_encounterParticipantClientIds.Clear();
			_eligibleAttacks.Clear();
			_completedPullDropDamageTargets.Clear();
			_pendingPullDropDamageMultipliers.Clear();
			_presentation?.StopAttackAnimation(_localAttackAnimation);
			_localAttackAnimationToken = 0u;
			_localAttackAnimation = CrabBossAttackAnimation.None;
			base.OnNetworkDespawn();
		}

		public bool ServerStartFight()
		{
			if (!base.IsServer)
			{
				Log("ServerStartFight rejected. Server only.", LogType.Warning);
				return false;
			}
			if (!_isQuestRuntimeAvailable.Value)
			{
				Log("ServerStartFight rejected. Lighthouse quest is not active yet.", LogType.Warning);
				return false;
			}
			if (_encounterState.Value != CrabBossEncounterState.Inactive)
			{
				Log($"ServerStartFight rejected. Current state={_encounterState.Value}.", LogType.Warning);
				return false;
			}
			SetBossRuntimeActive(active: true);
			RefreshPullDropDamageTargetSubscriptions();
			if (!ValidateConfiguration(out var error))
			{
				UtilityMethods.Log("[CrabBoss][Controller] Fight could not start. " + error, LogType.Error);
				SetBossRuntimeActive(active: false);
				return false;
			}
			LogPullDropDamageTargetWarnings();
			if (!TryResolvePlayerCount(out var playerCount))
			{
				SetBossRuntimeActive(active: false);
				return false;
			}
			_encounterParticipantClientIds.Clear();
			foreach (ulong item in _playersInRoom)
			{
				_encounterParticipantClientIds.Add(item);
			}
			StopAllBossCoroutines();
			ClearCurrentIdle();
			ClearCurrentAttackRuntime();
			ClearAttackSequenceRuntime();
			_lastAttackId = CrabBossAttackId.None;
			_lockedPlayerCount.Value = playerCount;
			_automaticFightStartSuppressed = false;
			BeginPhase(1);
			ServerSetPullDropTargetOutlineVisibility(visible: true);
			Log($"Fight started. LockedPlayerCount={playerCount}.");
			return true;
		}

		public bool ServerStopFight()
		{
			if (!base.IsServer)
			{
				Log("ServerStopFight rejected. Server only.", LogType.Warning);
				return false;
			}
			StopAttackLoop();
			StopPhaseTransition();
			ClearCurrentIdle();
			CancelCurrentAttack(CrabBossAttackEndReason.FightStopped);
			ServerForceCleanupAttackHandlers();
			ServerSetPullDropTargetOutlineVisibility(visible: false);
			ServerDisablePulledDropDamageTargets();
			ResetRuntimeState();
			SetBossRuntimeActive(active: false);
			_automaticFightStartSuppressed = _playersInRoom.Count > 0;
			Log("Fight stopped.");
			return true;
		}

		public bool ServerPlayerEnteredRoom(PPG_Player player)
		{
			if (!base.IsServer)
			{
				Log("ServerPlayerEnteredRoom rejected. Server only.", LogType.Warning);
				return false;
			}
			if (player == null || !player.IsSpawned)
			{
				Log("ServerPlayerEnteredRoom rejected. Player is missing or not spawned.", LogType.Warning);
				return false;
			}
			bool flag = _playersInRoom.Add(player.OwnerClientId);
			if (flag)
			{
				_playersInRoomCount.Value = _playersInRoom.Count;
			}
			SubscribePlayersManager();
			if (_encounterState.Value == CrabBossEncounterState.Inactive)
			{
				if (!_automaticFightStartSuppressed)
				{
					return ServerStartFight();
				}
				return false;
			}
			if (_encounterState.Value != CrabBossEncounterState.Defeated)
			{
				_encounterParticipantClientIds.Add(player.OwnerClientId);
			}
			if (_encounterState.Value == CrabBossEncounterState.Sleeping)
			{
				ServerSetPullDropTargetOutlineVisibility(visible: true);
				ResumeFromSleep();
			}
			return flag;
		}

		public void RequestPlayerEnteredRoomFromLocalTrigger()
		{
			if (base.IsSpawned && base.IsClient)
			{
				ServerPlayerEnteredRoomFromTriggerRpc();
			}
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
		private void ServerPlayerEnteredRoomFromTriggerRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(649394712u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 649394712u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsServer || Singleton<PlayersManager>.Instance == null)
			{
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!Singleton<PlayersManager>.Instance.TryGetPlayer(senderClientId, out var player) || player == null || !player.IsSpawned)
			{
				Log($"Room trigger notification rejected. Player unavailable. ClientId={senderClientId}.", LogType.Warning);
				return;
			}
			Log($"Room trigger notification received. ClientId={senderClientId}.");
			if (!CrabBossRoomTrigger.ServerIsPlayerInsideBossRoom(this, player))
			{
				Log($"Room trigger notification rejected. Player is outside the boss room. ClientId={senderClientId}.", LogType.Warning);
			}
			else
			{
				ServerPlayerEnteredRoom(player);
			}
		}

		public void RequestPlayerExitedRoomFromLocalTrigger()
		{
			if (base.IsSpawned && base.IsClient)
			{
				ServerPlayerExitedRoomFromTriggerRpc();
			}
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
		private void ServerPlayerExitedRoomFromTriggerRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(4256381682u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 4256381682u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsServer)
			{
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				if (!(Singleton<PlayersManager>.Instance != null) || !Singleton<PlayersManager>.Instance.TryGetPlayer(senderClientId, out var player) || !(player != null) || !player.IsSpawned || !CrabBossRoomTrigger.ServerIsPlayerInsideBossRoom(this, player))
				{
					Log($"Room exit trigger notification received. ClientId={senderClientId}.");
					ServerPlayerExitedRoom(senderClientId);
				}
			}
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
		private void ServerRequestLateJoinStateRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3769119323u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3769119323u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsServer || !_isAttackRunning.Value || _currentAttackToken.Value == 0)
			{
				return;
			}
			CrabBossAttackBase handler = _activeAttackHandler;
			if (handler == null)
			{
				TryGetUniqueAttackHandler(_currentAttackId.Value, out handler, out var _);
			}
			CrabBossAttackAnimation animation = CrabBossAttackAnimation.None;
			if (_attacks != null)
			{
				for (int i = 0; i < _attacks.Length; i++)
				{
					if (_attacks[i] != null && _attacks[i].Id == _currentAttackId.Value)
					{
						animation = _attacks[i].Animation;
						break;
					}
				}
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			ClientStartAttackAnimationForLateJoinRpc(_currentAttackToken.Value, animation, base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp));
			handler?.ServerSyncLateJoinClient(senderClientId);
		}

		public bool ServerPlayerExitedRoom(ulong clientId)
		{
			if (!base.IsServer)
			{
				Log("ServerPlayerExitedRoom rejected. Server only.", LogType.Warning);
				return false;
			}
			if (!_playersInRoom.Remove(clientId))
			{
				return false;
			}
			_playersInRoomCount.Value = _playersInRoom.Count;
			if (_playersInRoom.Count == 0)
			{
				_automaticFightStartSuppressed = false;
				ServerSetPullDropTargetOutlineVisibility(visible: false);
				EnterSleepIfPossible();
			}
			return true;
		}

		public int ServerCollectPlayersInRoom(List<PPG_Player> results)
		{
			if (results == null)
			{
				return 0;
			}
			results.Clear();
			if (!base.IsServer || Singleton<PlayersManager>.Instance == null)
			{
				return 0;
			}
			foreach (ulong item in _playersInRoom)
			{
				if (Singleton<PlayersManager>.Instance.TryGetPlayer(item, out var player) && !(player == null) && player.IsSpawned)
				{
					results.Add(player);
				}
			}
			return results.Count;
		}

		public bool ServerIsPlayerInRoom(ulong clientId)
		{
			if (base.IsServer)
			{
				return _playersInRoom.Contains(clientId);
			}
			return false;
		}

		public void ServerSetFacingOverride(ulong playerClientId)
		{
			if (base.IsServer && ServerIsPlayerInRoom(playerClientId))
			{
				_facingOverridePlayerClientId = playerClientId;
				ServerRefreshFacingTarget();
			}
		}

		public void ServerClearFacingOverride()
		{
			if (base.IsServer)
			{
				_facingOverridePlayerClientId = null;
			}
		}

		private void ServerRefreshFacingTarget()
		{
			if (!base.IsServer)
			{
				return;
			}
			PPG_Player pPG_Player = null;
			if (_facingOverridePlayerClientId.HasValue)
			{
				ServerCollectPlayersInRoom(_facingPlayers);
				for (int i = 0; i < _facingPlayers.Count; i++)
				{
					PPG_Player pPG_Player2 = _facingPlayers[i];
					if (pPG_Player2 != null && pPG_Player2.IsSpawned && pPG_Player2.OwnerClientId == _facingOverridePlayerClientId.Value)
					{
						pPG_Player = pPG_Player2;
						break;
					}
				}
				if (pPG_Player == null)
				{
					_facingOverridePlayerClientId = null;
				}
			}
			if (pPG_Player == null)
			{
				ServerCollectPlayersInRoom(_facingPlayers);
				float num = float.PositiveInfinity;
				for (int j = 0; j < _facingPlayers.Count; j++)
				{
					PPG_Player pPG_Player3 = _facingPlayers[j];
					if (!(pPG_Player3 == null) && pPG_Player3.IsSpawned)
					{
						Vector3 vector = pPG_Player3.transform.position - base.transform.position;
						vector.y = 0f;
						float sqrMagnitude = vector.sqrMagnitude;
						if (sqrMagnitude > 0.0001f && sqrMagnitude < num)
						{
							num = sqrMagnitude;
							pPG_Player = pPG_Player3;
						}
					}
				}
			}
			if (pPG_Player == null)
			{
				return;
			}
			Vector3 forward = pPG_Player.transform.position - base.transform.position;
			forward.y = 0f;
			if (!(forward.sqrMagnitude <= 0.0001f))
			{
				float num2 = Quaternion.LookRotation(forward).eulerAngles.y + _facingYawOffset;
				if (Mathf.Abs(Mathf.DeltaAngle(_targetFacingYaw.Value, num2)) > 0.01f)
				{
					_targetFacingYaw.Value = num2;
				}
			}
		}

		public bool TryGetAttackTimeout(CrabBossAttackId attackId, out float timeout)
		{
			if (_attacks == null)
			{
				timeout = 0f;
				return false;
			}
			for (int i = 0; i < _attacks.Length; i++)
			{
				CrabBossAttackSettings crabBossAttackSettings = _attacks[i];
				if (crabBossAttackSettings != null && crabBossAttackSettings.Id == attackId)
				{
					timeout = crabBossAttackSettings.Timeout;
					return true;
				}
			}
			timeout = 0f;
			return false;
		}

		public bool ServerApplyDamage(CrabBossDamageType damageType, float hitMultiplier, PPG_Player sourcePlayer = null)
		{
			if (!base.IsServer)
			{
				Log("ServerApplyDamage rejected. Server only.", LogType.Warning);
				return false;
			}
			if (!IsPositiveFinite(hitMultiplier))
			{
				Log($"ServerApplyDamage rejected. Invalid hit multiplier={hitMultiplier}.", LogType.Warning);
				return false;
			}
			if (sourcePlayer != null && (!sourcePlayer.IsSpawned || !ServerIsPlayerInRoom(sourcePlayer.OwnerClientId)))
			{
				return false;
			}
			if (_encounterState.Value != CrabBossEncounterState.Shielded && _encounterState.Value != CrabBossEncounterState.Vulnerable)
			{
				return false;
			}
			if (_encounterState.Value == CrabBossEncounterState.Shielded)
			{
				if (!TryGetConfiguredDamage(damageType, shielded: true, out var damage))
				{
					Log($"ServerApplyDamage rejected. Unknown damage type={damageType}.", LogType.Warning);
					return false;
				}
				float num = damage * hitMultiplier;
				if (!IsPositiveFinite(num))
				{
					return false;
				}
				ApplyShieldDamage(num);
				return true;
			}
			if (!TryGetConfiguredDamage(damageType, shielded: false, out var damage2))
			{
				Log($"ServerApplyDamage rejected. Unknown damage type={damageType}.", LogType.Warning);
				return false;
			}
			float num2 = damage2 * hitMultiplier;
			if (!IsPositiveFinite(num2))
			{
				return false;
			}
			ApplyBodyDamage(num2);
			return true;
		}

		public bool ServerTryApplyContactHit(PPG_Player player, Vector3 pushOrigin, byte oxygenDamage, float knockbackImpulse, float upwardForce, bool showExpandingPulseFeedback = false)
		{
			if (!base.IsServer)
			{
				Log("ServerTryApplyContactHit rejected. Server only.", LogType.Warning);
				return false;
			}
			if (player == null || !player.IsSpawned)
			{
				return false;
			}
			if (!CanDealContactHit(_encounterState.Value))
			{
				return false;
			}
			if (oxygenDamage <= 0 && knockbackImpulse <= 0f)
			{
				return false;
			}
			ClientApplyContactHitRpc(player.NetworkObject, pushOrigin, oxygenDamage, Mathf.Max(0f, knockbackImpulse), Mathf.Max(0f, upwardForce), showExpandingPulseFeedback, base.RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));
			Log($"Contact hit applied. ClientId={player.OwnerClientId} " + $"OxygenDamage={oxygenDamage} Knockback={knockbackImpulse}.");
			return true;
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientApplyContactHitRpc(NetworkObjectReference playerReference, Vector3 pushOrigin, byte oxygenDamage, float knockbackImpulse, float upwardForce, bool showExpandingPulseFeedback, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2745621442u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in playerReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in pushOrigin);
				bufferWriter.WriteValueSafe(in oxygenDamage, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in knockbackImpulse, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in upwardForce, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in showExpandingPulseFeedback, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2745621442u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (playerReference.TryGetComponent<PPG_Player>(out var component))
			{
				if (oxygenDamage > 0 && component.DivingEquipment != null)
				{
					component.DivingEquipment.ConsumeOxygen(oxygenDamage, withFeedback: true, showExpandingPulseFeedback);
				}
				if (knockbackImpulse > 0f && component.FirstPersonCharacter != null)
				{
					component.FirstPersonCharacter.PushFromPoint(pushOrigin, knockbackImpulse, upwardForce);
				}
			}
		}

		public bool ServerCompleteAttack(uint token)
		{
			if (!base.IsServer)
			{
				Log("ServerCompleteAttack rejected. Server only.", LogType.Warning);
				return false;
			}
			if (!_isAttackRunning.Value || token == 0 || token != _currentAttackToken.Value)
			{
				Log($"Attack completion rejected. Token={token} ActiveToken={_currentAttackToken.Value}.", LogType.Warning);
				return false;
			}
			FinishCurrentAttack(CrabBossAttackEndReason.Completed);
			return true;
		}

		public void RequestClaimPoseidonTridentReward()
		{
			if (base.IsSpawned && base.IsClient)
			{
				ServerClaimPoseidonTridentRewardRpc();
			}
		}

		public bool ServerDebugDefeat(PPG_Player debugParticipant)
		{
			if (!base.IsServer || _encounterState.Value == CrabBossEncounterState.Defeated)
			{
				return false;
			}
			if (debugParticipant == null || !debugParticipant.IsSpawned)
			{
				Log("Debug defeat rejected. Local participant is missing or not spawned.", LogType.Warning);
				return false;
			}
			ulong ownerClientId = debugParticipant.OwnerClientId;
			_playersInRoom.Add(ownerClientId);
			_playersInRoomCount.Value = _playersInRoom.Count;
			_encounterParticipantClientIds.Add(ownerClientId);
			_currentHealth.Value = 0f;
			_currentShieldHealth.Value = 0f;
			EnterDefeatedState();
			return true;
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
		private void ServerClaimPoseidonTridentRewardRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1577987474u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1577987474u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsServer && _isPoseidonTridentRewardAvailable.Value && _encounterState.Value == CrabBossEncounterState.Defeated)
			{
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				if (!(Singleton<PlayersManager>.Instance == null) && Singleton<PlayersManager>.Instance.TryGetPlayer(senderClientId, out var player) && CanPlayerClaimPoseidonTridentReward(player) && ServerEnsurePoseidonTridentUnlocked())
				{
					StopPoseidonRewardGuarantee();
					Log($"Poseidon Trident reward claimed. ClientId={senderClientId}.");
				}
			}
		}

		public bool ValidateConfiguration(out string error)
		{
			if (!ValidatePhaseSettings(1, _phase1Health, _phase1ShieldHealth, _phase1AttackInterval, out error) || !ValidatePhaseSettings(2, _phase2Health, _phase2ShieldHealth, _phase2AttackInterval, out error) || !ValidatePhaseSettings(3, _phase3Health, _phase3ShieldHealth, _phase3AttackInterval, out error))
			{
				return false;
			}
			if (!IsPositiveFinite(_harpoonShieldDamage))
			{
				error = "Harpoon shield damage must be greater than zero and finite.";
				return false;
			}
			if (!IsPositiveFinite(_fallingRockShieldDamage))
			{
				error = "Falling-rock shield damage must be greater than zero and finite.";
				return false;
			}
			if (!IsPositiveFinite(_harpoonBodyDamage))
			{
				error = "Harpoon body damage must be greater than zero and finite.";
				return false;
			}
			if (!IsNonNegativeFinite(_fallingRockBodyDamage))
			{
				error = "Falling-rock body damage must be zero or greater and finite.";
				return false;
			}
			if (!ValidatePullDropShieldCapacity(out error))
			{
				return false;
			}
			if (!IsPositiveFinite(_phaseTransitionDuration))
			{
				error = "Phase transition duration must be greater than zero and finite.";
				return false;
			}
			if (!ValidateAttackSettings(out error))
			{
				return false;
			}
			error = null;
			return true;
		}

		private void BeginPhase(int phase)
		{
			if (base.IsServer)
			{
				StopPhaseTransition();
				StopAttackLoop();
				ClearCurrentIdle();
				CancelCurrentAttack(CrabBossAttackEndReason.PhaseChanged);
				float healthPerPlayer = GetHealthPerPlayer(phase);
				float shieldHealthPerPlayer = GetShieldHealthPerPlayer(phase);
				_currentPhase.Value = phase;
				_completedPhaseAttackIds.Clear();
				_completedPullDropDamageTargets.Clear();
				_pendingPullDropDamageMultipliers.Clear();
				_maximumHealth.Value = healthPerPlayer * (float)_lockedPlayerCount.Value;
				_currentHealth.Value = _maximumHealth.Value;
				_maximumShieldHealth.Value = shieldHealthPerPlayer;
				_currentShieldHealth.Value = _maximumShieldHealth.Value;
				_encounterState.Value = CrabBossEncounterState.Shielded;
				ServerConfigurePullDropDamageTargetsForPhase(phase);
				PhaseStarted?.Invoke(phase);
				StartAttackLoop();
				Log($"Phase {phase} started. Health={_currentHealth.Value} " + $"Shield={_currentShieldHealth.Value} Players={_lockedPlayerCount.Value}.");
			}
		}

		private void ApplyShieldDamage(float damage)
		{
			if (damage < _currentShieldHealth.Value)
			{
				_currentShieldHealth.Value -= damage;
				return;
			}
			_currentShieldHealth.Value = 0f;
			_encounterState.Value = CrabBossEncounterState.Vulnerable;
			ServerDisablePulledDropDamageTargets();
			ShieldBroken?.Invoke();
			Log($"Phase {_currentPhase.Value} shield broken.");
		}

		private void ApplyBodyDamage(float damage)
		{
			if (damage < _currentHealth.Value)
			{
				_currentHealth.Value -= damage;
				return;
			}
			_currentHealth.Value = 0f;
			int value = _currentPhase.Value;
			PhaseCompleted?.Invoke(value);
			if (value >= 3)
			{
				EnterDefeatedState();
			}
			else
			{
				BeginPhaseTransition(value + 1);
			}
		}

		private void BeginPhaseTransition(int nextPhase)
		{
			StopAttackLoop();
			ClearCurrentIdle();
			CancelCurrentAttack(CrabBossAttackEndReason.PhaseChanged);
			ServerDisablePulledDropDamageTargets();
			_encounterState.Value = CrabBossEncounterState.PhaseTransition;
			_pendingTransitionPhase = nextPhase;
			_phaseTransitionCoroutine = StartCoroutine(PhaseTransitionCoroutine(nextPhase));
			Log($"Transitioning to phase {nextPhase}.");
		}

		private IEnumerator PhaseTransitionCoroutine(int nextPhase)
		{
			yield return new WaitForSeconds(_phaseTransitionDuration);
			_phaseTransitionCoroutine = null;
			if (base.IsServer && _encounterState.Value == CrabBossEncounterState.PhaseTransition)
			{
				_pendingTransitionPhase = 0;
				BeginPhase(nextPhase);
			}
		}

		private void EnterDefeatedState()
		{
			StopAttackLoop();
			StopPhaseTransition();
			ClearCurrentIdle();
			CancelCurrentAttack(CrabBossAttackEndReason.PhaseChanged);
			ServerForceCleanupAttackHandlers();
			_currentShieldHealth.Value = 0f;
			_encounterState.Value = CrabBossEncounterState.Defeated;
			ServerSetPullDropTargetOutlineVisibility(visible: false);
			ServerDisablePulledDropDamageTargets();
			_defeatedWorldPosition.Value = base.transform.position;
			_defeatedAtServerTime.Value = base.NetworkManager.ServerTime.Time;
			_isDefeatedReturnPortalAvailable.Value = true;
			_isPoseidonTridentRewardAvailable.Value = true;
			StartPoseidonRewardGuarantee();
			ServerUnlockShellShockedForParticipants();
			if (!(QuestReferenceHub.Instance != null) || !QuestReferenceHub.Instance.QuestComplete(QuestId.DefeatTheRedKingCrab))
			{
				UtilityMethods.Log("[CrabBoss][Controller] Defeat quest completion request was not accepted. The quest may already be complete or the quest pipeline is unavailable.", LogType.Warning);
			}
			Defeated?.Invoke();
			ServerScheduleBossDespawn();
			Log("Boss defeated.");
		}

		private void ServerUnlockShellShockedForParticipants()
		{
			if (!base.IsServer || Singleton<PlayersManager>.Instance == null)
			{
				return;
			}
			foreach (ulong encounterParticipantClientId in _encounterParticipantClientIds)
			{
				if (Singleton<PlayersManager>.Instance.TryGetPlayer(encounterParticipantClientId, out var player) && !(player == null) && player.IsSpawned)
				{
					Singleton<GameProgressManager>.Instance?.RpcRelay?.ServerUnlockSteamAchievementForClient("ACH_SHELL_SHOCKED", encounterParticipantClientId);
				}
			}
		}

		private void StartAttackLoop()
		{
			StopAttackLoop();
			_attackLoopCoroutine = StartCoroutine(AttackLoopCoroutine(_currentPhase.Value));
		}

		private IEnumerator AttackLoopCoroutine(int phaseAtStart)
		{
			while (base.IsServer && _currentPhase.Value == phaseAtStart && IsCombatState(_encounterState.Value))
			{
				StartIdleForCurrentPhase();
				yield return new WaitForSeconds(GetAttackInterval(phaseAtStart));
				if (!base.IsServer || _currentPhase.Value != phaseAtStart || !IsCombatState(_encounterState.Value))
				{
					ClearCurrentIdle();
					break;
				}
				ClearCurrentIdle();
				if (!_attackSequenceActive)
				{
					TryStartNextAttack();
				}
				while (base.IsServer && _currentPhase.Value == phaseAtStart && IsCombatState(_encounterState.Value) && _attackSequenceActive)
				{
					bool num = _activeAttackSequence == null || !_activeAttackSequence.Enabled;
					bool flag = _isAttackRunning.Value && (_activeAttackHandler == null || !_activeAttackHandler.isActiveAndEnabled);
					if (num | flag)
					{
						Log("Active attack was disabled. Cancelling its remaining runtime and repeats.");
						CancelCurrentAttack(CrabBossAttackEndReason.FailedToStart);
						break;
					}
					yield return null;
				}
			}
			_attackLoopCoroutine = null;
		}

		private void StartIdleForCurrentPhase()
		{
			if (!_isIdle.Value)
			{
				_isIdle.Value = true;
				IdleStarted?.Invoke();
				Log("Idle started.");
			}
		}

		private void ClearCurrentIdle()
		{
			if (_isIdle.Value)
			{
				_isIdle.Value = false;
				IdleEnded?.Invoke();
				Log("Idle ended.");
			}
		}

		private bool TryStartNextAttack()
		{
			CrabBossAttackSettings crabBossAttackSettings = SelectAttackForCurrentPhase();
			if (crabBossAttackSettings == null)
			{
				return false;
			}
			StopAttackRepeat();
			_activeAttackSequence = crabBossAttackSettings;
			_attackSequenceIteration = 0;
			_attackSequenceActive = true;
			_lastAttackId = crabBossAttackSettings.Id;
			return TryStartAttackSequenceIteration(announceInChat: true);
		}

		private bool TryStartAttackSequenceIteration(bool announceInChat)
		{
			CrabBossAttackSettings activeAttackSequence = _activeAttackSequence;
			if (!_attackSequenceActive || activeAttackSequence == null)
			{
				return false;
			}
			if (!activeAttackSequence.Enabled)
			{
				Log($"Attack '{activeAttackSequence.Id}' was disabled before its iteration could start.");
				ClearAttackSequenceRuntime();
				return false;
			}
			if (!TryGetUniqueAttackHandler(activeAttackSequence.Id, out var handler, out var matchCount))
			{
				UtilityMethods.Log($"[CrabBoss][Controller] Attack '{activeAttackSequence.Id}' could not start. " + $"Expected one handler, found {matchCount}. {BuildAttackHandlerDiagnostics()}", LogType.Error);
				ClearAttackSequenceRuntime();
				return false;
			}
			if (!handler.isActiveAndEnabled)
			{
				UtilityMethods.Log($"[CrabBoss][Controller] Attack '{activeAttackSequence.Id}' could not start because " + "handler '" + handler.GetType().Name + "' is disabled or inactive.", LogType.Error);
				ClearAttackSequenceRuntime();
				return false;
			}
			if (!handler.ServerCanSelectAttack())
			{
				Log($"Attack '{activeAttackSequence.Id}' is no longer selectable. " + "Cancelling this sequence before it starts.");
				ClearAttackSequenceRuntime();
				return false;
			}
			_activeAttackHandler = handler;
			_attackSequenceIteration++;
			_nextAttackToken++;
			if (_nextAttackToken == 0)
			{
				_nextAttackToken++;
			}
			_isAttackRunning.Value = true;
			_currentAttackId.Value = activeAttackSequence.Id;
			_currentAttackToken.Value = _nextAttackToken;
			CrabBossAttackCommand crabBossAttackCommand = new CrabBossAttackCommand(activeAttackSequence.Id, _nextAttackToken);
			AttackStarted?.Invoke(crabBossAttackCommand);
			if (!_isAttackRunning.Value || _currentAttackToken.Value != crabBossAttackCommand.Token)
			{
				return true;
			}
			if (!handler.ServerStartAttack(crabBossAttackCommand))
			{
				FinishCurrentAttack(CrabBossAttackEndReason.FailedToStart);
				return false;
			}
			if (announceInChat)
			{
				AnnounceAttackInChat(crabBossAttackCommand.AttackId);
			}
			if (!_isAttackRunning.Value || _currentAttackToken.Value != crabBossAttackCommand.Token)
			{
				return true;
			}
			StartAttackAnimationPresentation(crabBossAttackCommand, activeAttackSequence.Animation, handler.ClientAnimationStartDelay);
			StopAttackTimeout();
			_attackTimeoutCoroutine = StartCoroutine(AttackTimeoutCoroutine(crabBossAttackCommand, activeAttackSequence.Timeout));
			Log($"Attack started. Id={crabBossAttackCommand.AttackId} Token={crabBossAttackCommand.Token} " + $"Iteration={_attackSequenceIteration}/{activeAttackSequence.TotalExecutionCount}.");
			return true;
		}

		private IEnumerator AttackRepeatCoroutine(CrabBossAttackSettings settings)
		{
			if (settings.RepeatInterval > 0f)
			{
				yield return new WaitForSeconds(settings.RepeatInterval);
			}
			else
			{
				yield return null;
			}
			_attackRepeatCoroutine = null;
			if (!base.IsServer || !_attackSequenceActive || _activeAttackSequence != settings || !settings.Enabled || !IsCombatState(_encounterState.Value))
			{
				ClearAttackSequenceRuntime();
				yield break;
			}
			ClearCurrentIdle();
			if (!TryStartAttackSequenceIteration(announceInChat: false))
			{
				ClearAttackSequenceRuntime();
			}
		}

		private void AnnounceAttackInChat(CrabBossAttackId attackId)
		{
			if (_announceAttacksInChat && base.IsServer)
			{
				string text = attackId switch
				{
					CrabBossAttackId.ExpandingPulse => "chat.info.crab_boss.expanding_pulse", 
					CrabBossAttackId.CreatureSpawn => "chat.info.crab_boss.creature_spawn", 
					CrabBossAttackId.RockRain => "chat.info.crab_boss.rock_rain", 
					CrabBossAttackId.StalactiteDrop => "chat.info.crab_boss.stalactite_drop", 
					CrabBossAttackId.GrabThrow => "chat.info.crab_boss.grab_throw", 
					CrabBossAttackId.ProjectTile => "chat.info.crab_boss.projectile", 
					_ => "chat.info.crab_boss.generic", 
				};
				string fallback = attackId switch
				{
					CrabBossAttackId.ExpandingPulse => "Crab Boss used Expanding Pulse!", 
					CrabBossAttackId.CreatureSpawn => "Crab Boss spawned a random creature!", 
					CrabBossAttackId.RockRain => "Crab Boss used Rock Rain!", 
					CrabBossAttackId.StalactiteDrop => "Crab Boss used Stalactite Drop!", 
					CrabBossAttackId.GrabThrow => "Crab Boss used Grab Throw!", 
					CrabBossAttackId.ProjectTile => "Crab Boss fired a projectile!", 
					_ => "Crab Boss used {0}!", 
				};
				if (string.Equals(text, "chat.info.crab_boss.generic", StringComparison.Ordinal))
				{
					GlobalChatManager.BroadcastLocalizedInfoFromServer(this, text, fallback, attackId.ToString());
				}
				else
				{
					GlobalChatManager.BroadcastLocalizedInfoFromServer(this, text, fallback);
				}
			}
		}

		private CrabBossAttackSettings SelectAttackForCurrentPhase()
		{
			_eligibleAttacks.Clear();
			if (_attacks == null || _attacks.Length == 0)
			{
				return null;
			}
			int value = _currentPhase.Value;
			for (int i = 0; i < _attacks.Length; i++)
			{
				CrabBossAttackSettings crabBossAttackSettings = _attacks[i];
				if (crabBossAttackSettings != null && crabBossAttackSettings.Enabled && crabBossAttackSettings.UnlockPhase <= value && IsAttackCurrentlySelectable(crabBossAttackSettings.Id))
				{
					_eligibleAttacks.Add(crabBossAttackSettings);
				}
			}
			if (_eligibleAttacks.Count == 0)
			{
				return null;
			}
			if (value == 3 && !_completedPhaseAttackIds.Contains(CrabBossAttackId.CreatureSpawn))
			{
				for (int j = 0; j < _eligibleAttacks.Count; j++)
				{
					CrabBossAttackSettings crabBossAttackSettings2 = _eligibleAttacks[j];
					if (crabBossAttackSettings2.Id == CrabBossAttackId.CreatureSpawn)
					{
						return crabBossAttackSettings2;
					}
				}
			}
			int num = 0;
			for (int k = 0; k < _eligibleAttacks.Count; k++)
			{
				if (!_completedPhaseAttackIds.Contains(_eligibleAttacks[k].Id))
				{
					num++;
				}
			}
			bool flag = num > 0;
			bool flag2 = (flag ? num : _eligibleAttacks.Count) > 1;
			float num2 = 0f;
			for (int l = 0; l < _eligibleAttacks.Count; l++)
			{
				CrabBossAttackSettings crabBossAttackSettings3 = _eligibleAttacks[l];
				if ((!flag || !_completedPhaseAttackIds.Contains(crabBossAttackSettings3.Id)) && (!flag2 || crabBossAttackSettings3.Id != _lastAttackId))
				{
					num2 += crabBossAttackSettings3.SelectionWeight;
				}
			}
			float num3 = UnityEngine.Random.value * num2;
			for (int m = 0; m < _eligibleAttacks.Count; m++)
			{
				CrabBossAttackSettings crabBossAttackSettings4 = _eligibleAttacks[m];
				if ((!flag || !_completedPhaseAttackIds.Contains(crabBossAttackSettings4.Id)) && (!flag2 || crabBossAttackSettings4.Id != _lastAttackId))
				{
					num3 -= crabBossAttackSettings4.SelectionWeight;
					if (num3 <= 0f)
					{
						return crabBossAttackSettings4;
					}
				}
			}
			for (int num4 = _eligibleAttacks.Count - 1; num4 >= 0; num4--)
			{
				CrabBossAttackSettings crabBossAttackSettings5 = _eligibleAttacks[num4];
				if ((!flag || !_completedPhaseAttackIds.Contains(crabBossAttackSettings5.Id)) && (!flag2 || crabBossAttackSettings5.Id != _lastAttackId))
				{
					return crabBossAttackSettings5;
				}
			}
			return null;
		}

		private bool IsAttackCurrentlySelectable(CrabBossAttackId attackId)
		{
			if (TryGetUniqueAttackHandler(attackId, out var handler, out var _))
			{
				return handler.ServerCanSelectAttack();
			}
			return false;
		}

		private IEnumerator AttackTimeoutCoroutine(CrabBossAttackCommand command, float timeout)
		{
			yield return new WaitForSeconds(timeout);
			_attackTimeoutCoroutine = null;
			if (base.IsServer && _isAttackRunning.Value && command.Token == _currentAttackToken.Value)
			{
				FinishCurrentAttack(CrabBossAttackEndReason.TimedOut);
			}
		}

		private void FinishCurrentAttack(CrabBossAttackEndReason reason)
		{
			if (_isAttackRunning.Value)
			{
				CrabBossAttackCommand crabBossAttackCommand = new CrabBossAttackCommand(_currentAttackId.Value, _currentAttackToken.Value);
				CrabBossAttackBase handler = _activeAttackHandler;
				if (handler == null)
				{
					TryGetUniqueAttackHandler(crabBossAttackCommand.AttackId, out handler, out var _);
				}
				StopAttackTimeout();
				StopAttackAnimationStart();
				ClearCurrentAttackRuntime();
				ClientStopAttackAnimationRpc(crabBossAttackCommand.Token);
				handler?.ServerEndAttack(crabBossAttackCommand, reason);
				AttackEnded?.Invoke(crabBossAttackCommand, reason);
				if (reason == CrabBossAttackEndReason.Completed)
				{
					_completedPhaseAttackIds.Add(crabBossAttackCommand.AttackId);
				}
				Log($"Attack ended. Id={crabBossAttackCommand.AttackId} Token={crabBossAttackCommand.Token} Reason={reason}.");
				if (reason != CrabBossAttackEndReason.Completed || !_attackSequenceActive || _activeAttackSequence == null || !_activeAttackSequence.Enabled || _activeAttackSequence.Id != crabBossAttackCommand.AttackId || _attackSequenceIteration >= _activeAttackSequence.TotalExecutionCount)
				{
					ClearAttackSequenceRuntime();
					return;
				}
				StartIdleForCurrentPhase();
				StopAttackRepeat();
				_attackRepeatCoroutine = StartCoroutine(AttackRepeatCoroutine(_activeAttackSequence));
			}
		}

		private void CancelCurrentAttack(CrabBossAttackEndReason reason)
		{
			if (!_isAttackRunning.Value)
			{
				StopAttackTimeout();
				StopAttackRepeat();
				ClearCurrentAttackRuntime();
				ClearAttackSequenceRuntime();
			}
			else
			{
				FinishCurrentAttack(reason);
			}
		}

		private void ServerForceCleanupAttackHandlers()
		{
			if (!base.IsServer)
			{
				return;
			}
			CacheAttackHandlers();
			if (_attackHandlers != null)
			{
				for (int i = 0; i < _attackHandlers.Length; i++)
				{
					_attackHandlers[i]?.ServerForceCleanup();
				}
			}
		}

		private void ClearCurrentAttackRuntime()
		{
			_isAttackRunning.Value = false;
			_currentAttackId.Value = CrabBossAttackId.None;
			_currentAttackToken.Value = 0u;
			_activeAttackHandler = null;
		}

		private bool TryResolvePlayerCount(out int playerCount)
		{
			playerCount = 0;
			if (Singleton<PlayersManager>.Instance == null)
			{
				UtilityMethods.Log("[CrabBoss][Controller] Fight could not start. PlayersManager is missing.", LogType.Error);
				return false;
			}
			foreach (ulong item in _playersInRoom)
			{
				if (Singleton<PlayersManager>.Instance.TryGetPlayer(item, out var player) && player != null && player.IsSpawned)
				{
					playerCount++;
				}
			}
			if (playerCount > 0)
			{
				return true;
			}
			UtilityMethods.Log("[CrabBoss][Controller] Fight could not start. No spawned registered players were found.", LogType.Error);
			return false;
		}

		private bool ValidateAttackSettings(out string error)
		{
			CacheAttackHandlers(forceRefresh: true);
			if (_attacks == null || _attacks.Length == 0)
			{
				if (_attackHandlers != null && _attackHandlers.Length != 0)
				{
					error = "Attack components exist, but the Attacks list is empty.";
					return false;
				}
				error = null;
				return true;
			}
			HashSet<CrabBossAttackId> hashSet = new HashSet<CrabBossAttackId>();
			int num = 0;
			for (int i = 0; i < _attacks.Length; i++)
			{
				CrabBossAttackSettings crabBossAttackSettings = _attacks[i];
				int num2 = i + 1;
				if (crabBossAttackSettings == null)
				{
					error = $"Attack entry {num2} is missing.";
					return false;
				}
				if (crabBossAttackSettings.Id == CrabBossAttackId.None)
				{
					error = $"Attack entry {num2} has an empty ID.";
					return false;
				}
				if (!hashSet.Add(crabBossAttackSettings.Id))
				{
					error = $"Attack ID '{crabBossAttackSettings.Id}' is duplicated.";
					return false;
				}
				if (crabBossAttackSettings.Enabled)
				{
					num++;
					if (crabBossAttackSettings.UnlockPhase < 1 || crabBossAttackSettings.UnlockPhase > 3)
					{
						error = $"Attack '{crabBossAttackSettings.Id}' unlock phase must be between 1 and {3}.";
						return false;
					}
					if (!IsPositiveFinite(crabBossAttackSettings.SelectionWeight))
					{
						error = $"Attack '{crabBossAttackSettings.Id}' selection weight must be greater than zero and finite.";
						return false;
					}
					if (!IsPositiveFinite(crabBossAttackSettings.Timeout))
					{
						error = $"Attack '{crabBossAttackSettings.Id}' timeout must be greater than zero and finite.";
						return false;
					}
					if (crabBossAttackSettings.Loop && crabBossAttackSettings.TotalExecutionCount < 2)
					{
						error = $"Attack '{crabBossAttackSettings.Id}' repeat count must be at least 2 when Loop is enabled.";
						return false;
					}
					if (crabBossAttackSettings.Loop && !IsNonNegativeFinite(crabBossAttackSettings.RepeatInterval))
					{
						error = $"Attack '{crabBossAttackSettings.Id}' repeat interval must be zero or greater and finite.";
						return false;
					}
					if (_presentation == null)
					{
						error = "CrabBossPresentationController is missing.";
						return false;
					}
					if (!_presentation.ValidateAttackAnimation(crabBossAttackSettings.Animation, out var error2))
					{
						error = $"Attack '{crabBossAttackSettings.Id}' animation is invalid. {error2}";
						return false;
					}
					if (!TryGetUniqueAttackHandler(crabBossAttackSettings.Id, out var handler, out var matchCount))
					{
						error = $"Attack '{crabBossAttackSettings.Id}' must have exactly one component handler. " + $"Found {matchCount}. {BuildAttackHandlerDiagnostics()}";
						return false;
					}
					if (!handler.isActiveAndEnabled)
					{
						error = $"Attack '{crabBossAttackSettings.Id}' handler '{handler.GetType().Name}' " + "is disabled or its GameObject is inactive.";
						return false;
					}
					if (!handler.ValidateAttack(out var error3))
					{
						error = $"Attack '{crabBossAttackSettings.Id}' handler is invalid. {error3}";
						return false;
					}
				}
			}
			if (num == 0)
			{
				error = "At least one attack must be enabled.";
				return false;
			}
			for (int j = 0; j < _attackHandlers.Length; j++)
			{
				CrabBossAttackBase crabBossAttackBase = _attackHandlers[j];
				if (!(crabBossAttackBase == null) && !hashSet.Contains(crabBossAttackBase.AttackId))
				{
					error = "Attack handler '" + crabBossAttackBase.GetType().Name + "' uses ID " + $"'{crabBossAttackBase.AttackId}', but that ID is not in the Attacks list.";
					return false;
				}
			}
			error = null;
			return true;
		}

		private void CacheAttackHandlers(bool forceRefresh = false)
		{
			if (forceRefresh || _attackHandlers == null)
			{
				_attackHandlers = GetComponents<CrabBossAttackBase>();
			}
		}

		private bool TryGetUniqueAttackHandler(CrabBossAttackId attackId, out CrabBossAttackBase handler, out int matchCount)
		{
			CacheAttackHandlers();
			handler = null;
			matchCount = 0;
			if (_attackHandlers == null || attackId == CrabBossAttackId.None)
			{
				return false;
			}
			for (int i = 0; i < _attackHandlers.Length; i++)
			{
				CrabBossAttackBase crabBossAttackBase = _attackHandlers[i];
				if (!(crabBossAttackBase == null) && crabBossAttackBase.AttackId == attackId)
				{
					matchCount++;
					handler = crabBossAttackBase;
				}
			}
			return matchCount == 1;
		}

		private string BuildAttackHandlerDiagnostics()
		{
			if (_attackHandlers == null || _attackHandlers.Length == 0)
			{
				return "No CrabBossAttackBase components were found on the boss GameObject.";
			}
			StringBuilder stringBuilder = new StringBuilder("Found handlers:");
			for (int i = 0; i < _attackHandlers.Length; i++)
			{
				CrabBossAttackBase crabBossAttackBase = _attackHandlers[i];
				if (crabBossAttackBase == null)
				{
					stringBuilder.Append(" <null>;");
				}
				else
				{
					stringBuilder.Append(' ').Append(crabBossAttackBase.GetType().Name).Append("(Id='")
						.Append(crabBossAttackBase.AttackId)
						.Append("', GameObject='")
						.Append(crabBossAttackBase.gameObject.name)
						.Append("');");
				}
			}
			return stringBuilder.ToString();
		}

		private static bool ValidatePhaseSettings(int phase, float health, float shieldHealth, float attackInterval, out string error)
		{
			if (!IsPositiveFinite(health))
			{
				error = $"Phase {phase} health must be greater than zero and finite.";
				return false;
			}
			if (!IsPositiveFinite(shieldHealth))
			{
				error = $"Phase {phase} shield health must be greater than zero and finite.";
				return false;
			}
			if (!IsPositiveFinite(attackInterval))
			{
				error = $"Phase {phase} attack interval must be greater than zero and finite.";
				return false;
			}
			error = null;
			return true;
		}

		private float GetHealthPerPlayer(int phase)
		{
			return phase switch
			{
				1 => _phase1Health, 
				2 => _phase2Health, 
				3 => _phase3Health, 
				_ => throw new ArgumentOutOfRangeException("phase", phase, null), 
			};
		}

		private float ResolveCurrentEncounterHealth()
		{
			int value = _currentPhase.Value;
			if (value < 1 || value > 3)
			{
				return 0f;
			}
			float num = _currentHealth.Value;
			for (int i = value + 1; i <= 3; i++)
			{
				num += GetMaximumHealthForPhase(i);
			}
			return num;
		}

		private float ResolveMaximumEncounterHealth()
		{
			float num = 0f;
			for (int i = 1; i <= 3; i++)
			{
				num += GetMaximumHealthForPhase(i);
			}
			return num;
		}

		private float GetMaximumHealthForPhase(int phase)
		{
			return GetHealthPerPlayer(phase) * (float)_lockedPlayerCount.Value;
		}

		private float GetShieldHealthPerPlayer(int phase)
		{
			return phase switch
			{
				1 => _phase1ShieldHealth, 
				2 => _phase2ShieldHealth, 
				3 => _phase3ShieldHealth, 
				_ => throw new ArgumentOutOfRangeException("phase", phase, null), 
			};
		}

		private float GetAttackInterval(int phase)
		{
			return phase switch
			{
				1 => _phase1AttackInterval, 
				2 => _phase2AttackInterval, 
				3 => _phase3AttackInterval, 
				_ => throw new ArgumentOutOfRangeException("phase", phase, null), 
			};
		}

		private bool TryGetConfiguredDamage(CrabBossDamageType damageType, bool shielded, out float damage)
		{
			switch (damageType)
			{
			case CrabBossDamageType.Harpoon:
				damage = (shielded ? _harpoonShieldDamage : _harpoonBodyDamage);
				return true;
			case CrabBossDamageType.FallingRock:
				damage = (shielded ? _fallingRockShieldDamage : _fallingRockBodyDamage);
				return true;
			default:
				damage = 0f;
				return false;
			}
		}

		private void SubscribePullDropDamageTargets()
		{
			if (!base.IsServer)
			{
				return;
			}
			ResolvePullDropDamageTargets();
			foreach (HarpoonPullDropTarget key in _resolvedPullDropDamageTargets.Keys)
			{
				if (!(key == null) && _subscribedPullDropDamageTargets.Add(key))
				{
					key.ServerFallCompleted += HandlePullDropTargetFallCompleted;
				}
			}
		}

		private void UnsubscribePullDropDamageTargets()
		{
			foreach (HarpoonPullDropTarget subscribedPullDropDamageTarget in _subscribedPullDropDamageTargets)
			{
				if (subscribedPullDropDamageTarget != null)
				{
					subscribedPullDropDamageTarget.ServerFallCompleted -= HandlePullDropTargetFallCompleted;
				}
			}
			_subscribedPullDropDamageTargets.Clear();
		}

		private void RefreshPullDropDamageTargetSubscriptions()
		{
			if (base.IsServer)
			{
				UnsubscribePullDropDamageTargets();
				SubscribePullDropDamageTargets();
			}
		}

		private void ResolvePullDropDamageTargets()
		{
			_resolvedPullDropDamageTargets.Clear();
			_orderedPullDropDamageTargets.Clear();
			if (_pullDropDamageTargets != null)
			{
				for (int i = 0; i < _pullDropDamageTargets.Length; i++)
				{
					PullDropDamageTarget pullDropDamageTarget = _pullDropDamageTargets[i];
					if (pullDropDamageTarget != null && !(pullDropDamageTarget.Target == null) && !_resolvedPullDropDamageTargets.ContainsKey(pullDropDamageTarget.Target))
					{
						AddResolvedPullDropDamageTarget(pullDropDamageTarget.Target, pullDropDamageTarget.DamageMultiplier);
					}
				}
			}
			if (!_autoDiscoverPullDropDamageTargets)
			{
				return;
			}
			HarpoonPullDropTarget[] componentsInChildren = ((base.transform.parent != null) ? base.transform.parent : base.transform.root).GetComponentsInChildren<HarpoonPullDropTarget>(includeInactive: true);
			foreach (HarpoonPullDropTarget harpoonPullDropTarget in componentsInChildren)
			{
				if (!(harpoonPullDropTarget == null) && !_resolvedPullDropDamageTargets.ContainsKey(harpoonPullDropTarget))
				{
					AddResolvedPullDropDamageTarget(harpoonPullDropTarget, 1f);
				}
			}
		}

		private void AddResolvedPullDropDamageTarget(HarpoonPullDropTarget target, float damageMultiplier)
		{
			_resolvedPullDropDamageTargets.Add(target, damageMultiplier);
			_orderedPullDropDamageTargets.Add(target);
		}

		private void HandlePullDropTargetFallCompleted(HarpoonPullDropTarget target)
		{
			if (!base.IsServer || target == null)
			{
				return;
			}
			bool flag = _encounterState.Value == CrabBossEncounterState.Sleeping && IsCombatState(_resumeState);
			if ((!IsCombatState(_encounterState.Value) && !flag) || !TryGetPullDropDamageMultiplier(target, out var damageMultiplier))
			{
				return;
			}
			if (!IsPositiveFinite(damageMultiplier))
			{
				UtilityMethods.Log("[CrabBoss][Controller] Pull drop target damage ignored. " + $"Target='{target.name}' DamageMultiplier={damageMultiplier}.", LogType.Warning);
			}
			else if (_completedPullDropDamageTargets.Add(target))
			{
				if (flag)
				{
					_pendingPullDropDamageMultipliers.Add(damageMultiplier);
				}
				else
				{
					ServerApplyDamage(CrabBossDamageType.FallingRock, damageMultiplier);
				}
			}
		}

		private void ApplyPendingPullDropDamage()
		{
			float[] array = _pendingPullDropDamageMultipliers.ToArray();
			_pendingPullDropDamageMultipliers.Clear();
			for (int i = 0; i < array.Length; i++)
			{
				if (!IsCombatState(_encounterState.Value))
				{
					break;
				}
				ServerApplyDamage(CrabBossDamageType.FallingRock, array[i]);
			}
		}

		private bool TryGetPullDropDamageMultiplier(HarpoonPullDropTarget target, out float damageMultiplier)
		{
			if (target != null && _resolvedPullDropDamageTargets.TryGetValue(target, out damageMultiplier))
			{
				return true;
			}
			damageMultiplier = 0f;
			return false;
		}

		private bool ValidatePullDropShieldCapacity(out string error)
		{
			ResolvePullDropDamageTargets();
			float num = Mathf.Max(_phase1ShieldHealth, _phase2ShieldHealth, _phase3ShieldHealth);
			float num2 = 0f;
			int num3 = 0;
			foreach (KeyValuePair<HarpoonPullDropTarget, float> resolvedPullDropDamageTarget in _resolvedPullDropDamageTargets)
			{
				HarpoonPullDropTarget key = resolvedPullDropDamageTarget.Key;
				float value = resolvedPullDropDamageTarget.Value;
				if (!(key == null) && key.isActiveAndEnabled && key.gameObject.activeInHierarchy && key.IsSpawned && IsPositiveFinite(value))
				{
					num3++;
					num2 += value;
				}
			}
			float num4 = _fallingRockShieldDamage * num2;
			if (num4 + 0.0001f >= num)
			{
				error = null;
				return true;
			}
			int num5 = Mathf.CeilToInt((num - num4) / _fallingRockShieldDamage);
			error = "Pull-drop stalactites cannot break the strongest boss shield. " + $"Valid unique targets={num3}, " + $"total shield damage={num4:0.###}, " + $"required={num:0.###}. " + $"Add at least {num5} more active " + "HarpoonPullDropTarget object(s) under BossArea. Auto-discovered targets use damage multiplier 1.";
			return false;
		}

		private void LogPullDropDamageTargetWarnings()
		{
			if (_pullDropDamageTargets == null || _pullDropDamageTargets.Length == 0)
			{
				return;
			}
			HashSet<HarpoonPullDropTarget> hashSet = new HashSet<HarpoonPullDropTarget>();
			for (int i = 0; i < _pullDropDamageTargets.Length; i++)
			{
				PullDropDamageTarget pullDropDamageTarget = _pullDropDamageTargets[i];
				int num = i + 1;
				if (pullDropDamageTarget == null || pullDropDamageTarget.Target == null)
				{
					UtilityMethods.Log($"[CrabBoss][Controller] Pull drop damage target entry {num} is missing.", LogType.Warning);
					continue;
				}
				if (!hashSet.Add(pullDropDamageTarget.Target))
				{
					UtilityMethods.Log("[CrabBoss][Controller] Pull drop damage target '" + pullDropDamageTarget.Target.name + "' is duplicated. It will only deal damage once per fall.", LogType.Warning);
				}
				if (!IsPositiveFinite(pullDropDamageTarget.DamageMultiplier))
				{
					UtilityMethods.Log("[CrabBoss][Controller] Pull drop damage target '" + pullDropDamageTarget.Target.name + "' " + $"has invalid damage multiplier={pullDropDamageTarget.DamageMultiplier}.", LogType.Warning);
				}
			}
		}

		private void ServerConfigurePullDropDamageTargetsForPhase(int phase)
		{
			if (!base.IsServer)
			{
				return;
			}
			ResolvePullDropDamageTargets();
			float shieldHealthPerPlayer = GetShieldHealthPerPlayer(phase);
			float num = 0f;
			float num2 = ResolvePullDropImpactHeight();
			for (int i = 0; i < _orderedPullDropDamageTargets.Count; i++)
			{
				HarpoonPullDropTarget harpoonPullDropTarget = _orderedPullDropDamageTargets[i];
				if (!(harpoonPullDropTarget == null) && harpoonPullDropTarget.IsSpawned)
				{
					float num3 = _resolvedPullDropDamageTargets[harpoonPullDropTarget];
					bool flag = num + 0.0001f < shieldHealthPerPlayer && IsPositiveFinite(num3);
					harpoonPullDropTarget.ServerConfigureBossFall(num2, _pullDropFallSpeed);
					harpoonPullDropTarget.ServerSetBossEncounterAvailability(flag);
					if (flag && !harpoonPullDropTarget.ServerRepairBossInteractionColliders())
					{
						UtilityMethods.Log("[CrabBoss][Controller] Pull-drop target '" + harpoonPullDropTarget.name + "' could not restore an interaction collider and was disabled for this phase.", LogType.Error);
						harpoonPullDropTarget.ServerSetBossEncounterAvailability(available: false);
					}
					else if (flag)
					{
						num += _fallingRockShieldDamage * num3;
					}
				}
			}
			if (num + 0.0001f < shieldHealthPerPlayer)
			{
				UtilityMethods.Log($"[CrabBoss][Controller] Phase {phase} has insufficient usable pull-drop capacity " + $"({num:0.###}/{shieldHealthPerPlayer:0.###}). Shield was capped to prevent a soft-lock.", LogType.Error);
				_maximumShieldHealth.Value = Mathf.Max(0f, num);
				_currentShieldHealth.Value = _maximumShieldHealth.Value;
				if (_currentShieldHealth.Value <= 0f && _encounterState.Value == CrabBossEncounterState.Shielded)
				{
					_encounterState.Value = CrabBossEncounterState.Vulnerable;
					ShieldBroken?.Invoke();
				}
			}
			Log($"Phase {phase} pull-drop targets configured. " + $"Available shield damage={num:0.###}, " + $"required={shieldHealthPerPlayer:0.###}, " + $"impact height={num2:0.###}, fall speed={_pullDropFallSpeed:0.###}.");
		}

		private void ServerDisablePulledDropDamageTargets()
		{
			if (!base.IsServer)
			{
				return;
			}
			ResolvePullDropDamageTargets();
			for (int i = 0; i < _orderedPullDropDamageTargets.Count; i++)
			{
				HarpoonPullDropTarget harpoonPullDropTarget = _orderedPullDropDamageTargets[i];
				if (harpoonPullDropTarget != null && harpoonPullDropTarget.IsSpawned)
				{
					harpoonPullDropTarget.ServerSetBossEncounterAvailability(available: false);
				}
			}
		}

		private void ServerSetPullDropTargetOutlineVisibility(bool visible)
		{
			if (!base.IsServer)
			{
				return;
			}
			ResolvePullDropDamageTargets();
			for (int i = 0; i < _orderedPullDropDamageTargets.Count; i++)
			{
				HarpoonPullDropTarget harpoonPullDropTarget = _orderedPullDropDamageTargets[i];
				if (harpoonPullDropTarget != null && harpoonPullDropTarget.IsSpawned)
				{
					harpoonPullDropTarget.ServerSetBossOutlineVisibility(visible);
				}
			}
		}

		private float ResolvePullDropImpactHeight()
		{
			if (_pullDropImpactAnchor != null)
			{
				return _pullDropImpactAnchor.position.y + _pullDropImpactHeightOffset;
			}
			SkinnedMeshRenderer[] componentsInChildren = GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
			bool flag = false;
			float num = base.transform.position.y;
			foreach (SkinnedMeshRenderer skinnedMeshRenderer in componentsInChildren)
			{
				if (!(skinnedMeshRenderer == null))
				{
					float y = skinnedMeshRenderer.bounds.max.y;
					if (!flag || y > num)
					{
						flag = true;
						num = y;
					}
				}
			}
			return (flag ? num : base.transform.position.y) + _pullDropImpactHeightOffset;
		}

		private void EnterSleepIfPossible()
		{
			CrabBossEncounterState value = _encounterState.Value;
			if (IsCombatState(value) || value == CrabBossEncounterState.PhaseTransition)
			{
				_resumeState = value;
				if (value == CrabBossEncounterState.PhaseTransition && _pendingTransitionPhase == 0)
				{
					_pendingTransitionPhase = _currentPhase.Value + 1;
				}
				StopAttackLoop();
				StopPhaseTransition();
				ClearCurrentIdle();
				CancelCurrentAttack(CrabBossAttackEndReason.BossSleeping);
				ServerForceCleanupAttackHandlers();
				_encounterState.Value = CrabBossEncounterState.Sleeping;
				SleepStarted?.Invoke();
				Log($"Boss entered sleep. ResumeState={_resumeState} " + $"Phase={_currentPhase.Value} Health={_currentHealth.Value} " + $"Shield={_currentShieldHealth.Value}.");
			}
		}

		private void ResumeFromSleep()
		{
			if (!base.IsServer || _encounterState.Value != CrabBossEncounterState.Sleeping || _playersInRoom.Count == 0)
			{
				return;
			}
			if (IsCombatState(_resumeState))
			{
				_encounterState.Value = _resumeState;
				WokeUp?.Invoke();
				LogWakeUp();
				ApplyPendingPullDropDamage();
				if (IsCombatState(_encounterState.Value))
				{
					StartAttackLoop();
				}
			}
			else if (_resumeState == CrabBossEncounterState.PhaseTransition)
			{
				if (_pendingTransitionPhase < 1 || _pendingTransitionPhase > 3)
				{
					UtilityMethods.Log("[CrabBoss][Controller] Boss could not wake. " + $"Invalid pending phase={_pendingTransitionPhase}.", LogType.Error);
					return;
				}
				_encounterState.Value = CrabBossEncounterState.PhaseTransition;
				WokeUp?.Invoke();
				LogWakeUp();
				_phaseTransitionCoroutine = StartCoroutine(PhaseTransitionCoroutine(_pendingTransitionPhase));
			}
			else
			{
				UtilityMethods.Log($"[CrabBoss][Controller] Boss could not wake. Invalid resume state={_resumeState}.", LogType.Error);
			}
		}

		private void LogWakeUp()
		{
			Log($"Boss woke up. State={_encounterState.Value} Phase={_currentPhase.Value} " + $"Health={_currentHealth.Value} Shield={_currentShieldHealth.Value} " + $"LockedPlayers={_lockedPlayerCount.Value}.");
		}

		private void SubscribePlayersManager()
		{
			if (!_playersManagerSubscribed && !(Singleton<PlayersManager>.Instance == null))
			{
				Singleton<PlayersManager>.Instance.PlayerUnregistered += OnPlayerUnregistered;
				Singleton<PlayersManager>.Instance.PlayerRegistered += OnPlayerRegistered;
				_playersManagerSubscribed = true;
			}
		}

		private void UnsubscribePlayersManager()
		{
			if (_playersManagerSubscribed)
			{
				if (Singleton<PlayersManager>.Instance != null)
				{
					Singleton<PlayersManager>.Instance.PlayerUnregistered -= OnPlayerUnregistered;
					Singleton<PlayersManager>.Instance.PlayerRegistered -= OnPlayerRegistered;
				}
				_playersManagerSubscribed = false;
			}
		}

		private void OnPlayerUnregistered(PPG_Player player)
		{
			if (base.IsServer && !(player == null))
			{
				ServerPlayerExitedRoom(player.OwnerClientId);
			}
		}

		private void OnPlayerRegistered(PPG_Player player)
		{
			if (base.IsServer && !(player == null) && player.IsSpawned && CrabBossRoomTrigger.ServerIsPlayerInsideBossRoom(this, player))
			{
				ServerPlayerEnteredRoom(player);
			}
		}

		private void StartPoseidonRewardGuarantee()
		{
			StopPoseidonRewardGuarantee();
			_poseidonRewardGuaranteeCoroutine = StartCoroutine(ServerPoseidonRewardGuaranteeRoutine());
		}

		private IEnumerator ServerPoseidonRewardGuaranteeRoutine()
		{
			WaitForSecondsRealtime retryWait = new WaitForSecondsRealtime(0.25f);
			while (base.IsServer && _encounterState.Value == CrabBossEncounterState.Defeated && _isPoseidonTridentRewardAvailable.Value)
			{
				if (HasPoseidonRewardAutoEquipDeadlinePassed(base.NetworkManager.ServerTime.Time) && ServerEnsurePoseidonTridentUnlocked())
				{
					_poseidonRewardGuaranteeCoroutine = null;
					yield break;
				}
				yield return retryWait;
			}
			_poseidonRewardGuaranteeCoroutine = null;
		}

		private void ServerScheduleBossDespawn()
		{
			if (base.IsServer && !_bossDespawnScheduled)
			{
				_bossDespawnScheduled = true;
				SetQuestRuntimeAvailability(available: false);
				StartCoroutine(ServerDespawnBossRoutine());
			}
		}

		private IEnumerator ServerDespawnBossRoutine()
		{
			if (_defeatedDespawnDelay > 0f)
			{
				yield return new WaitForSeconds(_defeatedDespawnDelay);
			}
			SetBossRuntimeActive(active: false);
			RefreshPullDropDamageTargetSubscriptions();
			_orderedPullDropDamageTargets.Clear();
			foreach (HarpoonPullDropTarget key in _resolvedPullDropDamageTargets.Keys)
			{
				if (key != null)
				{
					_orderedPullDropDamageTargets.Add(key);
				}
			}
			for (int i = 0; i < _orderedPullDropDamageTargets.Count; i++)
			{
				HarpoonPullDropTarget harpoonPullDropTarget = _orderedPullDropDamageTargets[i];
				if (harpoonPullDropTarget != null && harpoonPullDropTarget.IsSpawned && harpoonPullDropTarget.NetworkObject != null)
				{
					harpoonPullDropTarget.NetworkObject.Despawn();
				}
			}
			if (!_isDefeatedReturnPortalAvailable.Value && base.IsSpawned && base.NetworkObject != null)
			{
				base.NetworkObject.Despawn();
			}
		}

		private bool ServerEnsurePoseidonTridentUnlocked()
		{
			if (!base.IsServer || !_isPoseidonTridentRewardAvailable.Value)
			{
				return false;
			}
			RpcRelay activeInstance = RpcRelay.ActiveInstance;
			if (activeInstance == null || !activeInstance.IsSpawned || !activeInstance.IsServer)
			{
				return false;
			}
			ToolDataNet value = activeInstance.ToolDataNV.Value;
			value.PoseidonTridentUnlocked = true;
			activeInstance.ToolDataNV.Value = value;
			_isPoseidonTridentRewardGranted.Value = true;
			_isPoseidonTridentRewardAvailable.Value = false;
			return true;
		}

		private void StopPoseidonRewardGuarantee()
		{
			if (_poseidonRewardGuaranteeCoroutine != null)
			{
				StopCoroutine(_poseidonRewardGuaranteeCoroutine);
				_poseidonRewardGuaranteeCoroutine = null;
			}
		}

		private void ResetRuntimeState()
		{
			StopAllBossCoroutines();
			_encounterState.Value = CrabBossEncounterState.Inactive;
			_currentPhase.Value = 0;
			_lockedPlayerCount.Value = 0;
			_currentHealth.Value = 0f;
			_maximumHealth.Value = 0f;
			_currentShieldHealth.Value = 0f;
			_maximumShieldHealth.Value = 0f;
			_playersInRoomCount.Value = _playersInRoom.Count;
			_resumeState = CrabBossEncounterState.Inactive;
			_pendingTransitionPhase = 0;
			_lastAttackId = CrabBossAttackId.None;
			_completedPhaseAttackIds.Clear();
			_encounterParticipantClientIds.Clear();
			_completedPullDropDamageTargets.Clear();
			_pendingPullDropDamageMultipliers.Clear();
			_isPoseidonTridentRewardAvailable.Value = false;
			_isDefeatedReturnPortalAvailable.Value = false;
			_isPoseidonTridentRewardGranted.Value = false;
			ClearCurrentIdle();
			ClearCurrentAttackRuntime();
			ClearAttackSequenceRuntime();
		}

		private void OnPoseidonTridentRewardAvailabilityChanged(bool previousValue, bool currentValue)
		{
			_refreshDefeatedPresentation = true;
		}

		private void ApplyPoseidonTridentRewardVisual(bool visible)
		{
			StopPoseidonTridentRise();
			if (_poseidonTridentReward != null && _poseidonTridentReward.activeSelf != visible)
			{
				_poseidonTridentReward.SetActive(visible);
			}
			if (visible)
			{
				ApplyPoseidonTridentRewardPresentation((base.NetworkManager != null) ? base.NetworkManager.ServerTime.Time : 0.0);
			}
		}

		private void StopAllBossCoroutines()
		{
			StopAttackLoop();
			StopAttackTimeout();
			StopAttackRepeat();
			StopAttackAnimationStart();
			StopPhaseTransition();
			StopPoseidonRewardGuarantee();
		}

		private void StartAttackAnimationPresentation(CrabBossAttackCommand command, CrabBossAttackAnimation animation, float delay)
		{
			StopAttackAnimationStart();
			if (delay <= 0f)
			{
				ClientStartAttackAnimationRpc(command.Token, animation);
			}
			else
			{
				_attackAnimationStartCoroutine = StartCoroutine(AttackAnimationStartCoroutine(command, animation, delay));
			}
		}

		private IEnumerator AttackAnimationStartCoroutine(CrabBossAttackCommand command, CrabBossAttackAnimation animation, float delay)
		{
			yield return new WaitForSeconds(delay);
			_attackAnimationStartCoroutine = null;
			if (base.IsServer && _isAttackRunning.Value && _currentAttackToken.Value == command.Token && _currentAttackId.Value == command.AttackId)
			{
				ClientStartAttackAnimationRpc(command.Token, animation);
			}
		}

		private void StopAttackAnimationStart()
		{
			if (_attackAnimationStartCoroutine != null)
			{
				StopCoroutine(_attackAnimationStartCoroutine);
				_attackAnimationStartCoroutine = null;
			}
		}

		private void StopAttackLoop()
		{
			if (_attackLoopCoroutine != null)
			{
				StopCoroutine(_attackLoopCoroutine);
				_attackLoopCoroutine = null;
			}
		}

		private void StopAttackTimeout()
		{
			if (_attackTimeoutCoroutine != null)
			{
				StopCoroutine(_attackTimeoutCoroutine);
				_attackTimeoutCoroutine = null;
			}
		}

		private void StopAttackRepeat()
		{
			if (_attackRepeatCoroutine != null)
			{
				StopCoroutine(_attackRepeatCoroutine);
				_attackRepeatCoroutine = null;
			}
		}

		private void ClearAttackSequenceRuntime()
		{
			StopAttackRepeat();
			_attackSequenceActive = false;
			_activeAttackSequence = null;
			_activeAttackHandler = null;
			_attackSequenceIteration = 0;
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientStartAttackAnimationRpc(uint token, CrabBossAttackAnimation animation)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1850893273u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				bufferWriter.WriteValueSafe(in animation, default(FastBufferWriter.ForEnums));
				__endSendRpc(ref bufferWriter, 1850893273u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if ((base.IsClient || base.IsServer) && token != 0)
			{
				if (_localAttackAnimationToken != 0 && _presentation != null)
				{
					_presentation.StopAttackAnimation(_localAttackAnimation);
				}
				_localAttackAnimationToken = token;
				_localAttackAnimation = animation;
				_presentation?.PlayAttackAnimation(animation);
			}
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientStartAttackAnimationForLateJoinRpc(uint token, CrabBossAttackAnimation animation, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3418519578u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				bufferWriter.WriteValueSafe(in animation, default(FastBufferWriter.ForEnums));
				__endSendRpc(ref bufferWriter, 3418519578u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsClient && token != 0 && _localAttackAnimationToken != token)
			{
				if (_localAttackAnimationToken != 0)
				{
					_presentation?.StopAttackAnimation(_localAttackAnimation);
				}
				_localAttackAnimationToken = token;
				_localAttackAnimation = animation;
				_presentation?.PlayAttackAnimation(animation);
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientStopAttackAnimationRpc(uint token)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2416593563u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				__endSendRpc(ref bufferWriter, 2416593563u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if ((base.IsClient || base.IsServer) && token != 0 && token == _localAttackAnimationToken)
				{
					_presentation?.StopAttackAnimation(_localAttackAnimation);
					_localAttackAnimationToken = 0u;
					_localAttackAnimation = CrabBossAttackAnimation.None;
				}
			}
		}

		private void StopPhaseTransition()
		{
			if (_phaseTransitionCoroutine != null)
			{
				StopCoroutine(_phaseTransitionCoroutine);
				_phaseTransitionCoroutine = null;
			}
		}

		private static bool IsCombatState(CrabBossEncounterState state)
		{
			if (state != CrabBossEncounterState.Shielded)
			{
				return state == CrabBossEncounterState.Vulnerable;
			}
			return true;
		}

		private static bool CanDealContactHit(CrabBossEncounterState state)
		{
			if (state != CrabBossEncounterState.Shielded && state != CrabBossEncounterState.Vulnerable)
			{
				return state == CrabBossEncounterState.PhaseTransition;
			}
			return true;
		}

		private static bool IsPositiveFinite(float value)
		{
			if (value > 0f && !float.IsNaN(value))
			{
				return !float.IsInfinity(value);
			}
			return false;
		}

		private static bool IsNonNegativeFinite(float value)
		{
			if (value >= 0f && !float.IsNaN(value))
			{
				return !float.IsInfinity(value);
			}
			return false;
		}

		private void Log(string message, LogType type = LogType.Log)
		{
			if (_log)
			{
				UtilityMethods.Log("[CrabBoss][Controller] " + message, type);
			}
		}

		private void LateUpdate()
		{
			if (_refreshDefeatedPresentation)
			{
				_refreshDefeatedPresentation = false;
				ApplyPoseidonTridentRewardVisual(_isPoseidonTridentRewardAvailable.Value);
				ApplyDefeatedReturnPortalVisual(_isDefeatedReturnPortalAvailable.Value);
			}
		}

		private bool HasPoseidonRewardAutoEquipDeadlinePassed(double serverTime)
		{
			if (_isPoseidonTridentRewardAvailable.Value && _encounterState.Value == CrabBossEncounterState.Defeated)
			{
				return serverTime >= _defeatedAtServerTime.Value + (double)_poseidonTridentAutoEquipDelay;
			}
			return false;
		}

		private bool CanPlayerClaimPoseidonTridentReward(PPG_Player player)
		{
			if (player == null || !player.IsSpawned || player.Interactor == null || (player.DivingEquipment != null && player.DivingEquipment.IsKnocked) || _poseidonTridentReward == null)
			{
				return false;
			}
			Collider componentInChildren = _poseidonTridentReward.GetComponentInChildren<Collider>();
			Vector3 position = player.transform.position;
			Vector3 obj = ((componentInChildren != null) ? componentInChildren.ClosestPoint(position) : _poseidonTridentReward.transform.position);
			float num = player.Interactor.InteractRange + 2f;
			return (obj - position).sqrMagnitude <= num * num;
		}

		private void ApplyPoseidonTridentRewardPresentation(double serverTime)
		{
			if (_poseidonTridentReward == null)
			{
				return;
			}
			StopPoseidonTridentRise();
			Transform transform = _poseidonTridentReward.transform;
			transform.position = _defeatedWorldPosition.Value;
			if (_poseidonTridentShineInstance == null && _poseidonTridentShinePrefab != null)
			{
				_poseidonTridentShineInstance = UnityEngine.Object.Instantiate(_poseidonTridentShinePrefab, transform);
				Transform obj = _poseidonTridentShineInstance.transform;
				obj.SetLocalPositionAndRotation(_poseidonTridentShineLocalOffset, Quaternion.identity);
				obj.localScale = Vector3.one * _poseidonTridentShineScale;
				_poseidonTridentShineInstance.SetActive(value: true);
			}
			float num = Mathf.Max(0.01f, _poseidonTridentRiseDuration);
			float num2 = Mathf.Clamp((float)(serverTime - _defeatedAtServerTime.Value), 0f, num);
			if (num2 >= num)
			{
				transform.position += Vector3.up * _poseidonTridentRiseHeight;
				return;
			}
			_poseidonTridentRiseTween = transform.DOBlendableMoveBy(Vector3.up * _poseidonTridentRiseHeight, num).SetEase(Ease.InOutSine).SetUpdate(isIndependentUpdate: true);
			if (num2 > 0f)
			{
				_poseidonTridentRiseTween.Goto(num2, andPlay: true);
			}
		}

		private void StopPoseidonTridentRise()
		{
			_poseidonTridentRiseTween?.Kill();
			_poseidonTridentRiseTween = null;
		}

		private void OnDefeatedReturnPortalAvailabilityChanged(bool previousValue, bool currentValue)
		{
			_refreshDefeatedPresentation = true;
		}

		private void ApplyDefeatedReturnPortalVisual(bool visible)
		{
			if (!visible)
			{
				if (_defeatedReturnPortalInstance != null)
				{
					_defeatedReturnPortalInstance.SetAvailable(available: false);
				}
				return;
			}
			if (_defeatedReturnPortalInstance == null && _defeatedReturnPortalPrefab != null)
			{
				_defeatedReturnPortalInstance = UnityEngine.Object.Instantiate(_defeatedReturnPortalPrefab, _defeatedWorldPosition.Value, Quaternion.identity);
				SceneManager.MoveGameObjectToScene(_defeatedReturnPortalInstance.gameObject, base.gameObject.scene);
			}
			if (_defeatedReturnPortalInstance != null)
			{
				_defeatedReturnPortalInstance.Configure(_defeatedWorldPosition.Value, _defeatedReturnPortalRadius);
				_defeatedReturnPortalInstance.SetAvailable(available: true);
			}
		}

		private void OnPoseidonTridentRewardGranted(bool previousValue, bool currentValue)
		{
			if (currentValue)
			{
				StartLocalPoseidonTridentEquip();
			}
		}

		private void StartLocalPoseidonTridentEquip()
		{
			if (base.IsClient && !_localPoseidonTridentEquipped && _localPoseidonTridentEquipCoroutine == null)
			{
				_localPoseidonTridentEquipCoroutine = StartCoroutine(LocalPoseidonTridentEquipRoutine());
			}
		}

		private IEnumerator LocalPoseidonTridentEquipRoutine()
		{
			yield return null;
			WaitForSecondsRealtime retryWait = new WaitForSecondsRealtime(0.25f);
			while (base.IsSpawned && _isPoseidonTridentRewardGranted.Value)
			{
				PPG_Player pPG_Player = Singleton<PlayersManager>.Instance?.LocalPlayer;
				if (pPG_Player != null && pPG_Player.TryAutoEquipPoseidonTridentReward())
				{
					_localPoseidonTridentEquipped = true;
					break;
				}
				yield return retryWait;
			}
			_localPoseidonTridentEquipCoroutine = null;
		}

		private void CleanupDefeatedRewardPresentation()
		{
			_refreshDefeatedPresentation = false;
			StopPoseidonTridentRise();
			if (_localPoseidonTridentEquipCoroutine != null)
			{
				StopCoroutine(_localPoseidonTridentEquipCoroutine);
			}
			_localPoseidonTridentEquipCoroutine = null;
			_localPoseidonTridentEquipped = false;
			if (_poseidonTridentReward != null)
			{
				_poseidonTridentReward.SetActive(value: false);
			}
			if (_poseidonTridentShineInstance != null)
			{
				UnityEngine.Object.Destroy(_poseidonTridentShineInstance);
			}
			if (_defeatedReturnPortalInstance != null)
			{
				UnityEngine.Object.Destroy(_defeatedReturnPortalInstance.gameObject);
			}
			_poseidonTridentShineInstance = null;
			_defeatedReturnPortalInstance = null;
		}

		private bool TryRestorePendingPoseidonTridentReward()
		{
			RpcRelay activeInstance = RpcRelay.ActiveInstance;
			if (activeInstance == null || !activeInstance.IsSpawned || !activeInstance.IsServer)
			{
				return false;
			}
			if (activeInstance.ToolDataNV.Value.PoseidonTridentUnlocked)
			{
				return true;
			}
			_encounterState.Value = CrabBossEncounterState.Defeated;
			_defeatedWorldPosition.Value = base.transform.position;
			_defeatedAtServerTime.Value = base.NetworkManager.ServerTime.Time;
			_isDefeatedReturnPortalAvailable.Value = true;
			_isPoseidonTridentRewardAvailable.Value = true;
			StartPoseidonRewardGuarantee();
			return true;
		}

		protected override void __initializeVariables()
		{
			if (_encounterState == null)
			{
				throw new Exception("CrabBossController._encounterState cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_encounterState.Initialize(this);
			__nameNetworkVariable(_encounterState, "_encounterState");
			NetworkVariableFields.Add(_encounterState);
			if (_currentPhase == null)
			{
				throw new Exception("CrabBossController._currentPhase cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_currentPhase.Initialize(this);
			__nameNetworkVariable(_currentPhase, "_currentPhase");
			NetworkVariableFields.Add(_currentPhase);
			if (_lockedPlayerCount == null)
			{
				throw new Exception("CrabBossController._lockedPlayerCount cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_lockedPlayerCount.Initialize(this);
			__nameNetworkVariable(_lockedPlayerCount, "_lockedPlayerCount");
			NetworkVariableFields.Add(_lockedPlayerCount);
			if (_currentHealth == null)
			{
				throw new Exception("CrabBossController._currentHealth cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_currentHealth.Initialize(this);
			__nameNetworkVariable(_currentHealth, "_currentHealth");
			NetworkVariableFields.Add(_currentHealth);
			if (_maximumHealth == null)
			{
				throw new Exception("CrabBossController._maximumHealth cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_maximumHealth.Initialize(this);
			__nameNetworkVariable(_maximumHealth, "_maximumHealth");
			NetworkVariableFields.Add(_maximumHealth);
			if (_currentShieldHealth == null)
			{
				throw new Exception("CrabBossController._currentShieldHealth cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_currentShieldHealth.Initialize(this);
			__nameNetworkVariable(_currentShieldHealth, "_currentShieldHealth");
			NetworkVariableFields.Add(_currentShieldHealth);
			if (_maximumShieldHealth == null)
			{
				throw new Exception("CrabBossController._maximumShieldHealth cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_maximumShieldHealth.Initialize(this);
			__nameNetworkVariable(_maximumShieldHealth, "_maximumShieldHealth");
			NetworkVariableFields.Add(_maximumShieldHealth);
			if (_playersInRoomCount == null)
			{
				throw new Exception("CrabBossController._playersInRoomCount cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_playersInRoomCount.Initialize(this);
			__nameNetworkVariable(_playersInRoomCount, "_playersInRoomCount");
			NetworkVariableFields.Add(_playersInRoomCount);
			if (_isIdle == null)
			{
				throw new Exception("CrabBossController._isIdle cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isIdle.Initialize(this);
			__nameNetworkVariable(_isIdle, "_isIdle");
			NetworkVariableFields.Add(_isIdle);
			if (_isAttackRunning == null)
			{
				throw new Exception("CrabBossController._isAttackRunning cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isAttackRunning.Initialize(this);
			__nameNetworkVariable(_isAttackRunning, "_isAttackRunning");
			NetworkVariableFields.Add(_isAttackRunning);
			if (_currentAttackId == null)
			{
				throw new Exception("CrabBossController._currentAttackId cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_currentAttackId.Initialize(this);
			__nameNetworkVariable(_currentAttackId, "_currentAttackId");
			NetworkVariableFields.Add(_currentAttackId);
			if (_currentAttackToken == null)
			{
				throw new Exception("CrabBossController._currentAttackToken cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_currentAttackToken.Initialize(this);
			__nameNetworkVariable(_currentAttackToken, "_currentAttackToken");
			NetworkVariableFields.Add(_currentAttackToken);
			if (_isPoseidonTridentRewardAvailable == null)
			{
				throw new Exception("CrabBossController._isPoseidonTridentRewardAvailable cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isPoseidonTridentRewardAvailable.Initialize(this);
			__nameNetworkVariable(_isPoseidonTridentRewardAvailable, "_isPoseidonTridentRewardAvailable");
			NetworkVariableFields.Add(_isPoseidonTridentRewardAvailable);
			if (_isQuestRuntimeAvailable == null)
			{
				throw new Exception("CrabBossController._isQuestRuntimeAvailable cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isQuestRuntimeAvailable.Initialize(this);
			__nameNetworkVariable(_isQuestRuntimeAvailable, "_isQuestRuntimeAvailable");
			NetworkVariableFields.Add(_isQuestRuntimeAvailable);
			if (_isBossRuntimeActive == null)
			{
				throw new Exception("CrabBossController._isBossRuntimeActive cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isBossRuntimeActive.Initialize(this);
			__nameNetworkVariable(_isBossRuntimeActive, "_isBossRuntimeActive");
			NetworkVariableFields.Add(_isBossRuntimeActive);
			if (_targetFacingYaw == null)
			{
				throw new Exception("CrabBossController._targetFacingYaw cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_targetFacingYaw.Initialize(this);
			__nameNetworkVariable(_targetFacingYaw, "_targetFacingYaw");
			NetworkVariableFields.Add(_targetFacingYaw);
			if (_defeatedWorldPosition == null)
			{
				throw new Exception("CrabBossController._defeatedWorldPosition cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_defeatedWorldPosition.Initialize(this);
			__nameNetworkVariable(_defeatedWorldPosition, "_defeatedWorldPosition");
			NetworkVariableFields.Add(_defeatedWorldPosition);
			if (_defeatedAtServerTime == null)
			{
				throw new Exception("CrabBossController._defeatedAtServerTime cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_defeatedAtServerTime.Initialize(this);
			__nameNetworkVariable(_defeatedAtServerTime, "_defeatedAtServerTime");
			NetworkVariableFields.Add(_defeatedAtServerTime);
			if (_isDefeatedReturnPortalAvailable == null)
			{
				throw new Exception("CrabBossController._isDefeatedReturnPortalAvailable cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isDefeatedReturnPortalAvailable.Initialize(this);
			__nameNetworkVariable(_isDefeatedReturnPortalAvailable, "_isDefeatedReturnPortalAvailable");
			NetworkVariableFields.Add(_isDefeatedReturnPortalAvailable);
			if (_isPoseidonTridentRewardGranted == null)
			{
				throw new Exception("CrabBossController._isPoseidonTridentRewardGranted cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isPoseidonTridentRewardGranted.Initialize(this);
			__nameNetworkVariable(_isPoseidonTridentRewardGranted, "_isPoseidonTridentRewardGranted");
			NetworkVariableFields.Add(_isPoseidonTridentRewardGranted);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(649394712u, __rpc_handler_649394712, "ServerPlayerEnteredRoomFromTriggerRpc", RpcInvokePermission.Everyone);
			__registerRpc(4256381682u, __rpc_handler_4256381682, "ServerPlayerExitedRoomFromTriggerRpc", RpcInvokePermission.Everyone);
			__registerRpc(3769119323u, __rpc_handler_3769119323, "ServerRequestLateJoinStateRpc", RpcInvokePermission.Everyone);
			__registerRpc(2745621442u, __rpc_handler_2745621442, "ClientApplyContactHitRpc", RpcInvokePermission.Server);
			__registerRpc(1577987474u, __rpc_handler_1577987474, "ServerClaimPoseidonTridentRewardRpc", RpcInvokePermission.Everyone);
			__registerRpc(1850893273u, __rpc_handler_1850893273, "ClientStartAttackAnimationRpc", RpcInvokePermission.Server);
			__registerRpc(3418519578u, __rpc_handler_3418519578, "ClientStartAttackAnimationForLateJoinRpc", RpcInvokePermission.Server);
			__registerRpc(2416593563u, __rpc_handler_2416593563, "ClientStopAttackAnimationRpc", RpcInvokePermission.Server);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_649394712(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((CrabBossController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossController)target).ServerPlayerEnteredRoomFromTriggerRpc(ext);
				((CrabBossController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4256381682(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((CrabBossController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossController)target).ServerPlayerExitedRoomFromTriggerRpc(ext);
				((CrabBossController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3769119323(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((CrabBossController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossController)target).ServerRequestLateJoinStateRpc(ext);
				((CrabBossController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2745621442(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out byte value3, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value4, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value5, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value6, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((CrabBossController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossController)target).ClientApplyContactHitRpc(value, value2, value3, value4, value5, value6, ext);
				((CrabBossController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1577987474(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((CrabBossController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossController)target).ServerClaimPoseidonTridentRewardRpc(ext);
				((CrabBossController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1850893273(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				reader.ReadValueSafe(out CrabBossAttackAnimation value2, default(FastBufferWriter.ForEnums));
				((CrabBossController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossController)target).ClientStartAttackAnimationRpc(value, value2);
				((CrabBossController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3418519578(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				reader.ReadValueSafe(out CrabBossAttackAnimation value2, default(FastBufferWriter.ForEnums));
				RpcParams ext = rpcParams.Ext;
				((CrabBossController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossController)target).ClientStartAttackAnimationForLateJoinRpc(value, value2, ext);
				((CrabBossController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2416593563(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				((CrabBossController)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossController)target).ClientStopAttackAnimationRpc(value);
				((CrabBossController)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "CrabBossController";
		}
	}
}
