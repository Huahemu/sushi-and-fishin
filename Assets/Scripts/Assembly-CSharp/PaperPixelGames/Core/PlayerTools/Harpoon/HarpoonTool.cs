using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using ECM2.Examples.FirstPerson;
using IndieKit;
using PaperPixelGames.Core.Animals;
using PaperPixelGames.Core.Boss;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Data.Tools;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Core.Tutorials;
using PaperPixelGames.Core.UI;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Wheel;
using Steamworks;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityServiceLocator;

namespace PaperPixelGames.Core.PlayerTools.Harpoon
{
	public class HarpoonTool : BaseTool
	{
		private enum HarpoonState
		{
			Idle = 0,
			Aiming = 1,
			Fired = 2,
			HookedObstacle = 3,
			HookedPlayer = 4,
			Challenge = 5,
			Reloading = 6,
			Pulling = 7
		}

		private sealed class HarpoonSuccessZoneRuntime
		{
			public float Min;

			public float Max;

			public float HoldTimer;

			public bool Completed;

			public HarpoonSuccessZoneRuntime(float min, float max)
			{
				Min = min;
				Max = max;
				HoldTimer = 0f;
				Completed = false;
			}

			public bool Contains(float value)
			{
				if (value >= Min)
				{
					return value <= Max;
				}
				return false;
			}

			public float GetProgress01(float requiredHoldTime)
			{
				if (Completed)
				{
					return 1f;
				}
				if (requiredHoldTime <= 0f)
				{
					return 0f;
				}
				return Mathf.Clamp01(HoldTimer / requiredHoldTime);
			}
		}

		[Header("Data & Visual")]
		[SerializeField]
		private HarpoonData _data;

		[SerializeField]
		private StationTutorialData _tutorialData;

		[SerializeField]
		private GrappleEffect _grapple;

		[SerializeField]
		private ItemOffset _tpsTipGraphicOffset;

		[SerializeField]
		private CatchInfoUI _catchInfoUI;

		[Header("Hook/Graphics")]
		[SerializeField]
		private Transform _hookedPoint;

		[SerializeField]
		private HarpoonTipSensor _tipSensor;

		[SerializeField]
		private LayerMask _obstacleLayer;

		[SerializeField]
		private float _stuckTipNormalOffset = 0.003f;

		[Header("Start Settings")]
		[SerializeField]
		private float _maxValue = 100f;

		[SerializeField]
		private ItemOffset _fpsTipGraphicOffset;

		[SerializeField]
		private float _pullTimer = 20f;

		[SerializeField]
		private int _failLimit = 3;

		[Header("Challenge UI")]
		[SerializeField]
		private GameObject _uiPanel;

		[SerializeField]
		private Image _uiBarFill;

		[SerializeField]
		private RectTransform _uiHoldRoot;

		[SerializeField]
		private Image _uiHoldFill;

		[SerializeField]
		private RectTransform _stalactiteHoldRoot;

		[SerializeField]
		private Image _stalactiteHoldFill;

		[SerializeField]
		private RectTransform _uiSuccessZone;

		[SerializeField]
		private TextMeshProUGUI _uiDebug;

		[SerializeField]
		private GameObject _barBlock;

		[SerializeField]
		[Min(1f)]
		private int _maxSuccessZoneCount = 5;

		[SerializeField]
		private GameObject _assistFeedbackRoot;

		[SerializeField]
		private HarpoonChallengeSpamPromptFeedback _spamPromptFeedback;

		[Header("Assist Bar Pins")]
		[SerializeField]
		private RectTransform _assistPinContainer;

		[SerializeField]
		private HarpoonAssistBarPinUI _assistPinPrefab;

		[SerializeField]
		private Sprite _assistAvatarFallback;

		[SerializeField]
		[Min(0.02f)]
		private float _assistBarSyncInterval = 0.1f;

		[SerializeField]
		[Range(0f, 0.1f)]
		private float _assistBarMinDelta = 0.02f;

		[SerializeField]
		[Min(0.1f)]
		private float _assistPinTimeout = 0.35f;

		[Header("Challenge Cancel")]
		[SerializeField]
		[Min(0.05f)]
		private float _challengeCancelHoldDuration = 0.5f;

		[Header("Challange")]
		[SerializeField]
		private float _tipRotationStep = 2.5f;

		[SerializeField]
		private float _tipBottomZAngle = -15f;

		[SerializeField]
		private float _tipTopZAngle = 15f;

		[SerializeField]
		private float _tipReturnSpeed = 8f;

		[Header("Challenge Zone Feedback")]
		[SerializeField]
		private Color _successZoneDefaultColor = Color.white;

		[SerializeField]
		private Color _successZoneActiveColor = Color.yellow;

		[Space]
		[SerializeField]
		private bool _useBubbleLoopFeedback = true;

		[SerializeField]
		[Min(0f)]
		private float _bubbleMinVolume = 0.08f;

		[SerializeField]
		[Min(0f)]
		private float _bubbleMaxVolume = 0.35f;

		[SerializeField]
		[Min(0.1f)]
		private float _bubbleMinPitch = 0.75f;

		[SerializeField]
		[Min(0.1f)]
		private float _bubbleMaxPitch = 1.45f;

		[SerializeField]
		[Min(0f)]
		private float _bubblePitchSmoothSpeed = 10f;

		[SerializeField]
		[Min(0f)]
		private float _bubbleVolumeSmoothSpeed = 10f;

		[Header("Challenge Fish Pull")]
		[SerializeField]
		private AudioClipSO _bubbleLoopSFX;

		[SerializeField]
		private AudioClipSO _zoneSuccesSFX;

		[SerializeField]
		private float _fishPullProgressStep = 0.18f;

		[SerializeField]
		private float _fishPullReturnSpeed = 1.25f;

		[SerializeField]
		private float _fishPullMoveSpeedMin = 8f;

		[SerializeField]
		private float _fishPullMoveSpeedMax = 22f;

		[SerializeField]
		private float _fishPullFastDistance = 8f;

		[SerializeField]
		private Vector3 _fishPullPlayerOffset = new Vector3(0f, 0.2f, 1.25f);

		[SerializeField]
		[Min(0.02f)]
		private float _fishPullServerSyncInterval = 0.08f;

		[Header("Player Pull")]
		[SerializeField]
		private float _playerPullForce = 65f;

		[SerializeField]
		private float _playerPullArriveDistance = 1.35f;

		[SerializeField]
		private float _playerPullSafetyMaxSeconds = 6f;

		[SerializeField]
		private bool _playerPullAllowLeaveGround = true;

		private OwnedLoopHandle _challengeBubbleLoopHandle;

		private AudioSource _challengeBubbleLoopSFX;

		private int _bubbleActiveZoneIndex = -1;

		private float _fishPullServerSyncTimer;

		private byte _spawnGraphicLevel;

		private Vector3 _fishChallengeStartWorldPosition;

		private bool _hasCachedFishChallengeStartWorldPosition;

		private float _currentFishPullProgress;

		private float _activeChallengeDuration;

		private Quaternion _fpsGraphicDefaultLocalRotation;

		private bool _hasCachedFpsGraphicDefaultRotation;

		private float _currentTipZOffset;

		private bool _challengeCancelHoldActive;

		private float _challengeCancelHoldTimer;

		private bool _challengeMouseCancelHoldFromDevice;

		private int _lastChallengeMouse1PressFrame = -1;

		private bool _isMouse1Held;

		private object _lastHarpoonContextTarget;

		private bool _hasHarpoonContextInteractHint;

		private bool _allowReachTargetReload;

		private HarpoonState _state;

		private Transform _fpsShootOrigin;

		private Transform _fpsSpearTransform;

		private Transform _tpsShootOrigin;

		private Transform _tpsSpearTransform;

		private GameObject _fpsTipGraphic;

		private GameObject _tpsTipGraphic;

		private GameObject _fpsStuckTipInstance;

		private GameObject _tpsStuckTipInstance;

		private HarpoonLevelData _currentLevelData;

		private Coroutine _startReloadDelayCor;

		private bool _preventInitSfx = true;

		private bool _instantReloadInProgress;

		[Header("Assist Bar Debug")]
		[SerializeField]
		private bool _debugMockAssistPinEnabled;

		[SerializeField]
		private ulong _debugMockAssistClientId = 900001uL;

		[SerializeField]
		private Sprite _debugMockAssistAvatar;

		[SerializeField]
		[Range(0f, 1f)]
		private float _debugMockAssistMinValue01 = 0.05f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _debugMockAssistMaxValue01 = 0.95f;

		[SerializeField]
		[Min(0.05f)]
		private float _debugMockAssistMoveSpeed = 1.25f;

		[SerializeField]
		private bool _debugMockAssistOpenPanel = true;

		private static HarpoonTool s_localOwnedAssistBarReceiver;

		private readonly Dictionary<ulong, HarpoonAssistBarPinUI> _assistBarPins = new Dictionary<ulong, HarpoonAssistBarPinUI>();

		private float _assistBarSyncTimer;

		private float _lastSentAssistBarValue01 = -1f;

		private const string HarpoonTutorialSeenKey = "StationTutorial.Harpoon.Seen";

		private CatchableSeaCreature _currentFish;

		private HarpoonPullDropTarget _currentPullDropTarget;

		private float _currentDecreaseRate;

		private float _currentIncreaseInput;

		private float _currentHoldTime;

		private float _currentValue;

		private float _currentPullTimer;

		private float _holdTimer;

		private float _zoneHoldTimeRequired;

		private int _activeSuccessZoneIndex = -1;

		private RectTransform _activeHoldRoot;

		private Image _activeHoldFill;

		private bool _sharedCatchActive;

		private bool _isPrimaryCatchPlayer;

		private bool _isAssistCatchFinisher;

		private ulong _sharedCatchPrimaryClientId;

		private bool _challengeStart;

		private OwnedLoopHandle _harpoonTensionLoop;

		private readonly List<HarpoonSuccessZoneRuntime> _successZones = new List<HarpoonSuccessZoneRuntime>();

		private readonly List<RectTransform> _uiSuccessZoneInstances = new List<RectTransform>();

		private EventBinding<AssistCatchZoneCompletedEvent> _assistZoneCompletedBinding;

		private EventBinding<AssistCatchFinishedEvent> _assistFinishedBinding;

		private EventBinding<AssistCatchParticipantCountChangedEvent> _assistParticipantCountBinding;

		private EventBinding<AssistCatchCancelledEvent> _assistCancelledBinding;

		private EventBinding<PullDropTargetZoneCompletedEvent> _pullDropTargetZoneCompletedBinding;

		private EventBinding<PullDropTargetFinishedEvent> _pullDropTargetFinishedBinding;

		private EventBinding<PullDropTargetParticipantCountChangedEvent> _pullDropTargetParticipantCountBinding;

		private EventBinding<PullDropTargetCancelledEvent> _pullDropTargetCancelledBinding;

		private bool _assistCatchEventsBound;

		private CatchableSeaCreature _recentlyInvalidatedAssistCreature;

		private float _recentAssistInvalidatedTime;

		private const float AssistStartRaceRejectWindow = 2f;

		private string _lastCatchInfoFishId;

		private float _lastCatchInfoWeight;

		private float _lastCatchInfoTime;

		public HarpoonTipSensor TipSensor => _tipSensor;

		public override byte Level => _player.ToolsData.HarpoonLevel;

		public override bool IsUnlocked => _player.ToolsData.HarpoonUnlocked;

		public override string Name => _data.Name;

		public override Sprite Icon => _data.Sprite;

		public bool IsIdle => _state == HarpoonState.Idle;

		public bool OnAim => _state == HarpoonState.Aiming;

		public bool IsFired => _state == HarpoonState.Fired;

		public bool IsHookedObstacle => _state == HarpoonState.HookedObstacle;

		public bool IsHookedPlayer => _state == HarpoonState.HookedPlayer;

		public bool IsChallenge => _state == HarpoonState.Challenge;

		public bool IsReloading => _state == HarpoonState.Reloading;

		public bool IsPulling => _state == HarpoonState.Pulling;

		private void Start()
		{
			_hookedPoint.SetParent(null);
			UtilityMethods.SetLayerRecursive(_tpsGraphicParent.gameObject, base.IsOwner ? "Player" : "ClientPlayer", includeChildren: true);
			UtilityMethods.SetLayerRecursive(_grapple.gameObject, base.IsOwner ? "ViewModel" : "ClientPlayer", includeChildren: true);
			_fpsGraphicParent.gameObject.SetActive(value: false);
			_tpsGraphicParent.gameObject.SetActive(value: false);
			_tpsGraphicParent.transform.SetParent(_player.RigBones.TpsRig.LeftHand);
			_tpsGraphicParent.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			if (base.IsOwner)
			{
				HideChallengeHoldUI();
				EnsureCatchInfoUiOutsideChallengePanel();
				BindAssistCatchEvents();
				Challenge_RegisterLocalAssistBarReceiver();
				if ((bool)_grapple)
				{
					_grapple.OnReachTarget.RemoveAllListeners();
					_grapple.OnReachTarget.AddListener(ReachTarget);
					_grapple.OnRetractComplete.RemoveAllListeners();
					_grapple.OnRetractComplete.AddListener(RetractDone);
				}
				if ((bool)_tipSensor)
				{
					_tipSensor.OnTipHit.RemoveAllListeners();
					_tipSensor.OnTipHit.AddListener(TipHit);
					_tipSensor.EndSense();
				}
				_fpsGraphicParent.transform.SetParent(_player.RigBones.FpsRig.RightHand);
				_fpsGraphicParent.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
				UtilityMethods.SetLayerRecursive(_fpsGraphicParent.gameObject, "ViewModel", includeChildren: true);
			}
		}

		private void EnsureCatchInfoUiOutsideChallengePanel()
		{
			if (!(_catchInfoUI == null) && !(_uiPanel == null))
			{
				Transform transform = _catchInfoUI.transform;
				Transform transform2 = _uiPanel.transform;
				Transform parent = transform2.parent;
				if (!(parent == null) && transform.IsChildOf(transform2))
				{
					transform.SetParent(parent, worldPositionStays: false);
					transform.SetAsLastSibling();
				}
			}
		}

		protected override void NetworkSpawn()
		{
			base.NetworkSpawn();
			if (!(_grapple == null))
			{
				_grapple.HardStop();
			}
		}

		protected override void NetworkDespawn()
		{
			Challenge_UnregisterLocalAssistBarReceiver();
			Challenge_ClearAssistPins();
			UnbindAssistCatchEvents();
			base.NetworkDespawn();
			ResetHarpoon();
		}

		public override bool DisableIf()
		{
			return IsIdle;
		}

		public override void LoadLevel(ToolDataNet data)
		{
			UtilityMethods.Log($"[HarpoonTool-{base.OwnerClientId}] LoadLevel:{data.HarpoonLevel} Unlocked:{data.HarpoonUnlocked}");
			ApplyLevel(data.HarpoonLevel);
			if (base.IsOwner)
			{
				SetUnlock(data.HarpoonUnlocked);
			}
		}

		protected override void SetUnlock(bool isUnlocked)
		{
			UtilityMethods.Log($"[HarpoonTool] SetUnlock: {isUnlocked}");
			Singleton<PPG_WheelManager>.Instance.UnlockHarpoon(isUnlocked);
		}

		protected override void ApplyToolStatus(bool status)
		{
			base.ApplyToolStatus(status);
			_player.AnimSetBool(PPG_Player.HARPOON, status);
			if (!IsIdle && !status)
			{
				ResetHarpoon();
			}
			if (_preventInitSfx)
			{
				_preventInitSfx = false;
				return;
			}
			SoundType type = (status ? SoundType.Harpoon_Equip : SoundType.Harpoon_Unequip);
			Vector3 position = _player.transform.position + Vector3.up + _player.transform.forward;
			if (base.IsLocalPlayer)
			{
				Singleton<SoundManager>.Instance.PlaySFX(type, position);
			}
		}

		public override void ApplyLevel(byte level)
		{
			_spawnGraphicLevel = level;
			_currentLevelData = _data.Levels.Find((HarpoonLevelData l) => l.Level == level);
			if (_currentLevelData == null)
			{
				UtilityMethods.Log($"[HarpoonTool] No harpoon data for incoming level {level}", LogType.Warning);
			}
			else
			{
				base.ApplyLevel(level);
			}
		}

		protected override void SpawnGraphics()
		{
			base.SpawnGraphics();
			HarpoonLevelData currentLevelData = _currentLevelData;
			if (currentLevelData == null)
			{
				UtilityMethods.Log($"[HarpoonTool] SpawnGraphics failed. Current level data is null. Level: {_spawnGraphicLevel}", LogType.Warning);
				return;
			}
			if (_tpsGraphic == null)
			{
				UtilityMethods.Log($"[HarpoonTool] SpawnGraphics failed. TPS graphic is null. Level: {_spawnGraphicLevel}", LogType.Warning);
				return;
			}
			_tpsShootOrigin = _tpsGraphic.transform.GetChild(0);
			_tpsSpearTransform = _tpsGraphic.transform.GetChild(1);
			_tpsGraphic.transform.SetLocalPositionAndRotation(_data.OffsetTps.PositionOffset, Quaternion.Euler(_data.OffsetTps.RotationOffset));
			if (!base.IsOwner)
			{
				if (_tpsTipGraphic != null)
				{
					UnityEngine.Object.Destroy(_tpsTipGraphic);
				}
				_tpsTipGraphic = UnityEngine.Object.Instantiate(_tpsSpearTransform, _player.RigBones.TpsRig.LeftHand).gameObject;
				_tpsTipGraphic.transform.SetLocalPositionAndRotation(currentLevelData.TipGraphicOffset.PositionOffset, Quaternion.Euler(currentLevelData.TipGraphicOffset.RotationOffset));
				_tpsTipGraphic.transform.localScale = currentLevelData.TipGraphicOffset.ScaleOffset;
				_tpsTipGraphic.SetActive(value: false);
				UtilityMethods.SetLayerRecursive(_tpsTipGraphic, "ClientPlayer", includeChildren: true);
			}
			else
			{
				if (_fpsGraphic == null)
				{
					UtilityMethods.Log($"[HarpoonTool] SpawnGraphics failed. FPS graphic is null. Level: {_spawnGraphicLevel}", LogType.Warning);
					return;
				}
				_fpsShootOrigin = _fpsGraphic.transform.GetChild(0);
				_fpsSpearTransform = _fpsGraphic.transform.GetChild(1);
				_fpsGraphic.transform.SetLocalPositionAndRotation(_data.OffsetFps.PositionOffset, Quaternion.Euler(_data.OffsetFps.RotationOffset));
				_fpsGraphicDefaultLocalRotation = _fpsGraphic.transform.localRotation;
				_hasCachedFpsGraphicDefaultRotation = true;
				_currentTipZOffset = 0f;
				if (_fpsTipGraphic != null)
				{
					UnityEngine.Object.Destroy(_fpsTipGraphic);
				}
				_fpsTipGraphic = UnityEngine.Object.Instantiate(_fpsSpearTransform, _player.RigBones.FpsRig.LeftHand).gameObject;
				_fpsTipGraphic.transform.SetLocalPositionAndRotation(currentLevelData.TipGraphicOffset.PositionOffset, Quaternion.Euler(currentLevelData.TipGraphicOffset.RotationOffset));
				_fpsTipGraphic.transform.localScale = currentLevelData.TipGraphicOffset.ScaleOffset;
				_fpsTipGraphic.SetActive(value: false);
				UtilityMethods.SetLayerRecursive(_fpsTipGraphic, "ViewModel", includeChildren: true);
				_tipSensor.SetupTipGraphic(_fpsTipGraphic);
			}
			_grapple.transform.SetParent((!base.IsOwner) ? _tpsShootOrigin : _fpsShootOrigin);
			_grapple.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
		}

		protected override void DestroyGraphics()
		{
			base.DestroyGraphics();
			if (_fpsTipGraphic != null)
			{
				UnityEngine.Object.Destroy(_fpsTipGraphic);
				_fpsTipGraphic = null;
			}
			if (_tpsTipGraphic != null)
			{
				UnityEngine.Object.Destroy(_tpsTipGraphic);
				_tpsTipGraphic = null;
			}
			ClearStuck(ref _fpsStuckTipInstance);
			ClearStuck(ref _tpsStuckTipInstance);
			_fpsShootOrigin = null;
			_fpsSpearTransform = null;
			_tpsShootOrigin = null;
			_tpsSpearTransform = null;
			_hasCachedFpsGraphicDefaultRotation = false;
			_currentTipZOffset = 0f;
		}

		protected override GameObject GetGraphicByLevel()
		{
			HarpoonLevelData harpoonLevelData = _data.Levels.Find((HarpoonLevelData l) => l.Level == _spawnGraphicLevel);
			if (harpoonLevelData == null)
			{
				UtilityMethods.Log($"[HarpoonTool] Graphic data not found for level {_spawnGraphicLevel}", LogType.Warning);
				return null;
			}
			return harpoonLevelData.GraphicPrefab;
		}

		protected override int GetLevelCount()
		{
			return _data.Levels.Count;
		}

		public override void Interact(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				if (_player.Interactor.CurrentGazed is PlayerInteraction playerInteraction)
				{
					playerInteraction.TriggerInput(_player.Interactor, InputKind.Interact, phase);
				}
				else if (_player.Interactor.CurrentGazed is LatchPiranha latchPiranha)
				{
					latchPiranha.TriggerInput(_player.Interactor, InputKind.Interact, phase);
				}
			}
		}

		private void SetState(HarpoonState s)
		{
			if (_state != s)
			{
				_state = s;
				RefreshHintsForState();
			}
		}

		protected override void EnableHints()
		{
			if (base.IsOwner)
			{
				RefreshHintsForState();
			}
		}

		protected override void DisableHints()
		{
			if (base.IsOwner)
			{
				ClearHarpoonHints();
			}
		}

		private void RefreshHintsForState()
		{
			if (base.IsOwner && base.ToolStatus && !(Singleton<HintsManager>.Instance == null))
			{
				ClearHarpoonHints();
				switch (_state)
				{
				case HarpoonState.Idle:
					Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_aim");
					TickHarpoonContextInteractHint(force: true);
					break;
				case HarpoonState.Aiming:
					Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_shoot");
					TickHarpoonContextInteractHint(force: true);
					break;
				case HarpoonState.Challenge:
					Singleton<HintsManager>.Instance.AddHint("RapidPress", "key_spam");
					Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_cancel");
					break;
				case HarpoonState.Fired:
				case HarpoonState.HookedObstacle:
				case HarpoonState.HookedPlayer:
				case HarpoonState.Reloading:
				case HarpoonState.Pulling:
					break;
				}
			}
		}

		private void ClearHarpoonHints()
		{
			if (!(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
				Singleton<HintsManager>.Instance.RemoveHint("RapidPress");
				Singleton<HintsManager>.Instance.RemoveHint("RightMouseMechanic");
				if (_hasHarpoonContextInteractHint)
				{
					Singleton<HintsManager>.Instance.RemoveHint("Interact");
				}
				_hasHarpoonContextInteractHint = false;
				_lastHarpoonContextTarget = null;
			}
		}

		private void TickHarpoonContextInteractHint(bool force = false)
		{
			if (!base.IsOwner || !base.ToolStatus || (!IsIdle && !OnAim) || Singleton<HintsManager>.Instance == null)
			{
				return;
			}
			object harpoonContextTarget = GetHarpoonContextTarget();
			if (force || _lastHarpoonContextTarget != harpoonContextTarget)
			{
				_lastHarpoonContextTarget = harpoonContextTarget;
				if (harpoonContextTarget != null)
				{
					Singleton<HintsManager>.Instance.AddHint("Interact", "key_interact");
					_hasHarpoonContextInteractHint = true;
				}
				else if (_hasHarpoonContextInteractHint)
				{
					Singleton<HintsManager>.Instance.RemoveHint("Interact");
					_hasHarpoonContextInteractHint = false;
				}
			}
		}

		private object GetHarpoonContextTarget()
		{
			if (_player == null || _player.Interactor == null)
			{
				return null;
			}
			object currentGazed = _player.Interactor.CurrentGazed;
			if (currentGazed is PlayerInteraction)
			{
				return currentGazed;
			}
			if (currentGazed is LatchPiranha)
			{
				return currentGazed;
			}
			return null;
		}

		private void Update()
		{
			if (base.IsOwner && base.ToolStatus)
			{
				TickHarpoonContextInteractHint();
				if (IsChallenge)
				{
					ChallengeInput_TickMouseCancelDevice();
					Challenge_Tick();
					ChallengeCancelHold_Tick();
					ReturnFpsGraphicRotationToDefault();
				}
			}
		}

		public override void Mouse1(InputActionPhase phase)
		{
			switch (phase)
			{
			case InputActionPhase.Started:
				_isMouse1Held = true;
				break;
			case InputActionPhase.Canceled:
				_isMouse1Held = false;
				break;
			}
			if (IsChallenge)
			{
				return;
			}
			if (phase == InputActionPhase.Started)
			{
				if (IsIdle)
				{
					SetAim(status: true);
					return;
				}
				Shoot();
			}
			if ((IsHookedObstacle || IsHookedPlayer) && phase == InputActionPhase.Canceled)
			{
				StartReload();
			}
		}

		private void UpdateFpsTipGraphicRotation(float stepDelta)
		{
			if (!(_fpsGraphic == null) && _hasCachedFpsGraphicDefaultRotation)
			{
				_currentTipZOffset = Mathf.Clamp(_currentTipZOffset + stepDelta, _tipBottomZAngle, _tipTopZAngle);
				Vector3 eulerAngles = _fpsGraphicDefaultLocalRotation.eulerAngles;
				_fpsGraphic.transform.localRotation = Quaternion.Euler(eulerAngles.x, eulerAngles.y, eulerAngles.z + _currentTipZOffset);
			}
		}

		private void ReturnFpsGraphicRotationToDefault()
		{
			if (!(_fpsGraphic == null) && _hasCachedFpsGraphicDefaultRotation)
			{
				if (Mathf.Abs(_currentTipZOffset) < 0.01f)
				{
					_currentTipZOffset = 0f;
					_fpsGraphic.transform.localRotation = _fpsGraphicDefaultLocalRotation;
				}
				else
				{
					_currentTipZOffset = Mathf.MoveTowards(_currentTipZOffset, 0f, _tipReturnSpeed * Time.deltaTime);
					Vector3 eulerAngles = _fpsGraphicDefaultLocalRotation.eulerAngles;
					_fpsGraphic.transform.localRotation = Quaternion.Euler(eulerAngles.x, eulerAngles.y, eulerAngles.z + _currentTipZOffset);
				}
			}
		}

		private void ResetFpsTipGraphicRotation()
		{
			if (!(_fpsGraphic == null) && _hasCachedFpsGraphicDefaultRotation)
			{
				_currentTipZOffset = 0f;
				_fpsGraphic.transform.localRotation = _fpsGraphicDefaultLocalRotation;
			}
		}

		private Vector3 GetChallengeFishTargetPosition()
		{
			return _player.transform.position + _player.transform.forward * _fishPullPlayerOffset.z + _player.transform.right * _fishPullPlayerOffset.x + _player.transform.up * _fishPullPlayerOffset.y;
		}

		private void UpdateChallengeFishPullFromSuccessProgress()
		{
			if (!(_currentFish == null) && _hasCachedFishChallengeStartWorldPosition)
			{
				_currentFishPullProgress = Challenge_GetGlobalProgress01();
				ApplyChallengeFishPull();
			}
		}

		private void ApplyChallengeFishPull()
		{
			if (_currentFish == null || !_hasCachedFishChallengeStartWorldPosition)
			{
				return;
			}
			if (base.IsServer)
			{
				ApplyChallengeFishPullToCreature(_currentFish, _fishChallengeStartWorldPosition, _currentFishPullProgress);
				return;
			}
			_fishPullServerSyncTimer += Time.deltaTime;
			if (_fishPullServerSyncTimer >= _fishPullServerSyncInterval || _currentFishPullProgress >= 0.999f)
			{
				_fishPullServerSyncTimer = 0f;
				ServerUpdateChallengeFishPullRpc(_currentFish.NetworkObject.ToReference(), _currentFishPullProgress);
			}
		}

		private void ApplyChallengeFishPullToCreature(CatchableSeaCreature creature, Vector3 startPosition, float progress)
		{
			if (!(creature == null))
			{
				Vector3 challengeFishTargetPosition = GetChallengeFishTargetPosition();
				float num = Vector3.Distance(startPosition, challengeFishTargetPosition);
				float num2 = Mathf.Lerp(_fishPullMoveSpeedMin, _fishPullMoveSpeedMax, Mathf.Clamp01(num / _fishPullFastDistance));
				Vector3 b = Vector3.Lerp(startPosition, challengeFishTargetPosition, Mathf.Clamp01(progress));
				creature.transform.position = Vector3.Lerp(creature.transform.position, b, num2 * Time.deltaTime);
			}
		}

		private void ResetChallengeFishPull()
		{
			_currentFishPullProgress = 0f;
			_hasCachedFishChallengeStartWorldPosition = false;
			_fishPullServerSyncTimer = 0f;
		}

		public override void Mouse2(InputActionPhase phase)
		{
			if (IsChallenge)
			{
				switch (phase)
				{
				case InputActionPhase.Started:
				case InputActionPhase.Performed:
					StartChallengeCancelHold();
					break;
				case InputActionPhase.Canceled:
					ResetChallengeCancelHold();
					break;
				}
			}
			else if (IsIdle || OnAim)
			{
				switch (phase)
				{
				case InputActionPhase.Started:
					SetAim(status: true);
					break;
				case InputActionPhase.Canceled:
					SetAim(status: false);
					break;
				}
			}
		}

		private void SetAim(bool status)
		{
			_player.UIAimFeedback(status);
			if (status)
			{
				SetState(HarpoonState.Aiming);
				_player.SetTransientCameraFovOffset(-5f);
				_player.AnimSetBool(PPG_Player.HARPOON_AIM, value: true);
				_player.Sway.SetIdleWeight(0f);
				Singleton<SoundManager>.Instance.PlaySFX(SoundType.Harpoon_InAim);
			}
			else
			{
				SetState(HarpoonState.Idle);
				_player.ResetCameraFov();
				_player.AnimSetBool(PPG_Player.HARPOON_AIM, value: false);
				_player.Sway.SetIdleWeight(1f);
				Singleton<SoundManager>.Instance.PlaySFX(SoundType.Harpoon_OutAim);
			}
		}

		private void Shoot()
		{
			if (!base.IsOwner || _player.FirstPersonCharacterLookInput.PauseLook || !OnAim)
			{
				return;
			}
			if (_currentLevelData == null)
			{
				_player.UICenterFeedback(willHit: false);
				return;
			}
			ForceResetVisualState(hardStopGrapple: true, restoreHeldSpearGraphic: false);
			UtilityMethods.Log("[HarpoonTool] Shoot");
			Vector3 position = _fpsShootOrigin.position;
			Vector3 forward = _player.CameraTransform.forward;
			float shootRange = _currentLevelData.ShootRange;
			if (Physics.Raycast(position, forward, out var hitInfo, shootRange, _obstacleLayer, QueryTriggerInteraction.Ignore))
			{
				_hookedPoint.position = hitInfo.point - forward * 0.005f;
			}
			else
			{
				_hookedPoint.position = position + forward * shootRange;
			}
			ServerShootVisualsToRpc(_hookedPoint.position, _currentLevelData.ShootSpeed);
			_allowReachTargetReload = true;
			_grapple.StartForward(_currentLevelData.ShootSpeed);
			_tipSensor.BeginSense();
			if ((bool)_fpsSpearTransform)
			{
				_fpsSpearTransform.gameObject.SetActive(value: false);
			}
			_player.AnimSetBool(PPG_Player.HARPOON_SHOOT, value: true);
			_player.ResetCameraFov(0.5f);
			SetState(HarpoonState.Fired);
			Vector3 position2 = _player.transform.position + Vector3.up + _player.transform.forward;
			Singleton<SoundManager>.Instance.PlaySFX(SoundType.Harpoon_Shoot, position2, _player.transform);
		}

		public void StartReload(bool instant = false)
		{
			if (!IsReloading && !IsIdle)
			{
				UtilityMethods.Log($"[HarpoonTool] StartReload Instant={instant} State={_state}");
				_allowReachTargetReload = false;
				ServerStartReloadVisualsToRpc(instant);
				StopOrbit(!instant);
				ForceResetVisualState(hardStopGrapple: false, restoreHeldSpearGraphic: false);
				SetFpsSpearGraphic(s: false);
				SetTpsSpearGraphic(s: false);
				_player.AnimSetBool(PPG_Player.HARPOON_AIM, value: false);
				_player.AnimSetBool(PPG_Player.HARPOON_SHOOT, value: false);
				_player.ResetCameraFov(0.5f);
				_player.Sway.SetIdleWeight(1f);
				_player.UIAimFeedback(status: false);
				ResetStartReloadCor();
				SetState(HarpoonState.Reloading);
				_instantReloadInProgress = instant;
				bool flag = _grapple.StartRetract(instant);
				_instantReloadInProgress = false;
				if (instant)
				{
					ReloadDone();
				}
				else if (!flag)
				{
					UtilityMethods.Log("[HarpoonTool] Reload retract skipped because grapple was inactive.");
					RetractDone();
				}
			}
		}

		public override void RapidPress(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Performed)
			{
				ChallengeInput_ApplyRapidPress();
			}
		}

		private void ResetStartReloadCor()
		{
			if (_startReloadDelayCor != null)
			{
				Singleton<DelayHelper>.Instance.StopCoroutine(_startReloadDelayCor);
			}
		}

		public void ReloadAnim()
		{
			if (!IsReloading)
			{
				UtilityMethods.Log($"[HarpoonTool] ReloadAnim ignored. State={_state}");
				return;
			}
			UtilityMethods.Log("[HarpoonTool] ReloadAnim");
			_player.AnimSetBool(PPG_Player.HARPOON_AIM, value: false);
			_player.AnimSetBool(PPG_Player.HARPOON_SHOOT, value: false);
		}

		public void ResetHarpoon()
		{
			UtilityMethods.Log("[HarpoonTool] ResetHarpoon");
			if (_player == null)
			{
				return;
			}
			_isMouse1Held = false;
			StopOrbit();
			ForceResetVisualState(hardStopGrapple: true, restoreHeldSpearGraphic: true);
			ResetStartReloadCor();
			_startReloadDelayCor = null;
			if (_tipSensor != null)
			{
				_tipSensor.EndSense();
			}
			Challenge_StopTensionSfx();
			_player.AnimSetBool(PPG_Player.HARPOON_AIM, value: false);
			_player.AnimSetBool(PPG_Player.HARPOON_SHOOT, value: false);
			if (OnAim)
			{
				SetAim(status: false);
			}
			_player.UIAimFeedback(status: false);
			if (_challengeStart && (_currentFish != null || _currentPullDropTarget != null))
			{
				if (_currentPullDropTarget != null)
				{
					_currentPullDropTarget.CancelHook(this);
				}
				else if (_isPrimaryCatchPlayer)
				{
					if (_currentFish != null)
					{
						_currentFish.ServerChallengeFailRpc();
					}
				}
				else
				{
					Challenge_LeaveAssistSessionIfNeeded();
				}
				Challenge_End();
			}
			SetState(HarpoonState.Idle);
			_currentFish = null;
			_currentPullDropTarget = null;
			_currentValue = 0f;
			_holdTimer = 0f;
			Challenge_InitializeDefaultTimer();
			Challenge_ClearSuccessZones();
			SetAssistFeedbackStatus(status: false);
			Challenge_ClearAssistPins();
			Challenge_StopBubbleLoopFeedback();
			Challenge_StopSpamPromptFeedback(instant: true);
			ResetFpsTipGraphicRotation();
			ResetChallengeFishPull();
			if (_player.FirstPersonCharacter != null)
			{
				_player.FirstPersonCharacter.StopDrag();
				_player.FirstPersonCharacter.EnableGroundConstraint(enable: true);
			}
			_player.AnimSetBool(PPG_Player.HARPOON, value: false);
			_player.ExitHarpoonReel();
			if (!_player.IsTrapped)
			{
				_player.ResumeCharacter();
			}
			SetTpsTipGraphic(s: false);
			SetFpsTipGraphic(s: false);
			ServerResetHarpoonToRpc();
		}

		private void ChallengeInput_TickMouseCancelDevice()
		{
			if (_challengeStart && Mouse.current != null)
			{
				if (Mouse.current.rightButton.wasPressedThisFrame)
				{
					_challengeMouseCancelHoldFromDevice = true;
					StartChallengeCancelHold();
				}
				if (_challengeMouseCancelHoldFromDevice && !Mouse.current.rightButton.isPressed)
				{
					_challengeMouseCancelHoldFromDevice = false;
					ResetChallengeCancelHold();
				}
			}
		}

		private void ChallengeInput_ApplyRapidPress()
		{
			if (base.IsOwner && IsChallenge && _challengeStart && _lastChallengeMouse1PressFrame != Time.frameCount)
			{
				_lastChallengeMouse1PressFrame = Time.frameCount;
				float currentValue = Mathf.Clamp(_currentValue + _currentIncreaseInput, 0f, _maxValue);
				_currentValue = currentValue;
				UpdateFpsTipGraphicRotation(_tipRotationStep);
				Challenge_UpdateUI();
			}
		}

		private void TipHit(RaycastHit hit)
		{
			if (!base.IsOwner)
			{
				return;
			}
			if (hit.collider != null)
			{
				UtilityMethods.Log("[HarpoonTool] TipHit:" + hit.collider.name);
			}
			_tipSensor.EndSense();
			_allowReachTargetReload = false;
			_grapple.EndForward(invokeReachTargetEvent: false);
			if (((hit.collider != null) ? hit.collider.GetComponentInParent<CrabSmallController>() : null) != null)
			{
				StartReload();
				return;
			}
			CrabBossController crabBossController = ((hit.collider != null) ? hit.collider.GetComponentInParent<CrabBossController>() : null);
			if (crabBossController != null)
			{
				if (crabBossController.NetworkObject != null && crabBossController.NetworkObject.IsSpawned)
				{
					ServerRequestHitCrabBossRpc(crabBossController.NetworkObject.ToReference(), _player.NetworkObject.ToReference());
				}
				StartReload();
			}
			else
			{
				if (TryHandleRadioHit(hit))
				{
					return;
				}
				JellyfishTrap component2;
				PlayerInteraction component3;
				HarpoonGrapplePoint component4;
				DestructibleObject component5;
				HarpoonPullDropTarget component6;
				PullableRock component7;
				if (hit.collider.TryGetComponent<SeaCreature>(out var component))
				{
					if (component is CatchableSeaCreature catchableSeaCreature)
					{
						bool num = catchableSeaCreature.CanReceiveHit(_player);
						bool flag = catchableSeaCreature.CanReceiveAssistHit(_player);
						if (num | flag)
						{
							SetState(HarpoonState.Challenge);
							ServerRequestHookCreatureRpc(catchableSeaCreature.NetworkObject.ToReference(), _player.NetworkObject.ToReference());
							return;
						}
					}
					if (component is OctopusCreature octopusCreature)
					{
						if (octopusCreature.CanStartCatchChallenge(_player))
						{
							SetState(HarpoonState.Challenge);
							ServerRequestHookCreatureRpc(octopusCreature.NetworkObject.ToReference(), _player.NetworkObject.ToReference());
						}
						else
						{
							ServerRequestHitOctopusCreatureRpc(octopusCreature.NetworkObject.ToReference(), _player.NetworkObject.ToReference());
							StartReload();
						}
					}
					else if (component is HostileSeaCreature hostileSeaCreature)
					{
						ServerRequestHitHostileCreatureRpc(hostileSeaCreature.NetworkObject.ToReference(), _player.NetworkObject.ToReference());
						StartReload();
					}
					else
					{
						StartReload();
					}
				}
				else if (hit.collider.TryGetComponent<JellyfishTrap>(out component2) && component2.CanBeKilledByHarpoon)
				{
					ServerRequestHitJellyfishTrapRpc(component2.NetworkObject.ToReference(), _player.NetworkObject.ToReference());
					StartReload();
				}
				else if (hit.collider.TryGetComponent<PlayerInteraction>(out component3))
				{
					if (!CanHookPlayer(component3))
					{
						StartReload();
						return;
					}
					SetState(HarpoonState.HookedPlayer);
					component3.OwnerPlayer.Harpooned(base.NetworkObject, _playerPullForce, _playerPullArriveDistance, _playerPullSafetyMaxSeconds, _playerPullAllowLeaveGround);
					if (component3.NetworkObject != null)
					{
						ServerTipHitPlayerToRpc(component3.NetworkObject.ToReference(), hit.point, hit.normal);
					}
					_hookedPoint.SetParent(component3.transform, worldPositionStays: true);
					_hookedPoint.SetPositionAndRotation(hit.point, Quaternion.LookRotation(hit.normal));
					ResetStartReloadCor();
					_startReloadDelayCor = Singleton<DelayHelper>.Instance.StartCoroutine(MonitorPlayerPullReloadRoutine(component3.OwnerPlayer));
				}
				else if (hit.collider.TryGetComponent<HarpoonGrapplePoint>(out component4))
				{
					SetState(HarpoonState.HookedObstacle);
					_hookedPoint.SetParent(component4.transform, worldPositionStays: true);
					_hookedPoint.SetPositionAndRotation(hit.point, Quaternion.LookRotation(hit.normal));
					ServerTipHitEnvironmentToRpc(hit.point, hit.normal);
					Transform transform = PlaceStuck(_fpsTipGraphic, ref _fpsStuckTipInstance, hit.point, hit.normal, "ViewModel");
					if (_grapple != null && transform != null)
					{
						_grapple.SetVisualTarget(transform);
					}
					_player.CameraShake(ShakeIntensity.Light, ShakeDuration.Instant);
					StartOrbitSoftLock(component4, hit.point);
					if (!_isMouse1Held)
					{
						StartReload();
					}
				}
				else if (hit.collider.TryGetComponent<DestructibleObject>(out component5))
				{
					ServerTipHitDestructibleToRpc((byte)component5.Id, hit.point);
					StartReload();
				}
				else if (hit.collider.TryGetComponent<HarpoonPullDropTarget>(out component6))
				{
					SetState(HarpoonState.Challenge);
					ServerRequestPullDropTargetChallengeRpc(component6.NetworkObject.ToReference(), _player.NetworkObject.ToReference(), hit.point, hit.normal);
				}
				else if (hit.collider.TryGetComponent<PullableRock>(out component7))
				{
					SetState(HarpoonState.Pulling);
					component7.RequestPull(this);
					_hookedPoint.SetParent(hit.transform, worldPositionStays: true);
					_hookedPoint.SetPositionAndRotation(hit.point, Quaternion.LookRotation(hit.normal));
					ServerTipHitEnvironmentToRpc(hit.point, hit.normal);
					Transform visualTarget = PlaceStuck(_fpsTipGraphic, ref _fpsStuckTipInstance, hit.point, hit.normal, "ViewModel");
					if (_grapple != null)
					{
						_grapple.SetVisualTarget(visualTarget);
					}
					ResetStartReloadCor();
					_startReloadDelayCor = Singleton<DelayHelper>.Instance.DelayAction(0.5f, () =>
					{
						StartReload();
					});
				}
				else
				{
					SetState(HarpoonState.HookedObstacle);
					_hookedPoint.SetParent(hit.transform, worldPositionStays: true);
					_hookedPoint.SetPositionAndRotation(hit.point, Quaternion.LookRotation(hit.normal));
					ServerTipHitEnvironmentToRpc(hit.point, hit.normal);
					_player.FirstPersonCharacter.PullTowardsPoint(_hookedPoint.position, _currentLevelData.HarpoonForce);
					_player.FirstPersonCharacter.DragToward(_hookedPoint, _currentLevelData.HarpoonForce * 2f, 4f, 1f, allowLeaveGround: true, () =>
					{
						StartReload();
					});
					Transform visualTarget2 = PlaceStuck(_fpsTipGraphic, ref _fpsStuckTipInstance, hit.point, hit.normal, "ViewModel");
					if (_grapple != null)
					{
						_grapple.SetVisualTarget(visualTarget2);
					}
				}
			}
		}

		private bool TryHandleRadioHit(RaycastHit hit)
		{
			if (hit.collider == null)
			{
				return false;
			}
			if (!hit.collider.TryGetComponent<Radio>(out var component))
			{
				return false;
			}
			component.BreakFromHarpoon();
			StartReload();
			return true;
		}

		private void StartOrbitSoftLock(HarpoonGrapplePoint gp, Vector3 hitPoint)
		{
			if (!(_player.FirstPersonCharacter == null) && !(gp == null))
			{
				Vector3 initialForward = ((_player.CameraTransform != null) ? _player.CameraTransform.forward : _player.transform.forward);
				_player.FirstPersonCharacter.StartOrbitSwing(gp.transform, hitPoint, initialForward, gp.UpBoostVelocity, gp.InitialForwardVelocity, gp.ForwardTargetSpeed, gp.ForwardAcceleration, gp.OrbitStartDistance, gp.OrbitRadius, gp.OrbitTargetSpeed, gp.OrbitAcceleration, gp.RadialSpring, gp.RadialDamp, gp.PlaneDamp, gp.MaxAcceleration, gp.BreakDistanceTolerance, gp.BreakAppliesReleaseVelocity, GetGrappleReleaseSpeed(), () =>
				{
					StartReload(instant: true);
				});
			}
		}

		private void StopOrbit(bool doRelease = false)
		{
			if (!(_player.FirstPersonCharacter == null) && _player.FirstPersonCharacter.StopOrbitSwing(doRelease, GetGrappleReleaseSpeed()))
			{
				_player.CameraShake(ShakeIntensity.Medium, ShakeDuration.Instant);
			}
		}

		private float GetGrappleReleaseSpeed()
		{
			if (!(_currentLevelData != null))
			{
				return 0f;
			}
			return Mathf.Max(0f, _currentLevelData.ReleaseSpeed);
		}

		private void ReachTarget()
		{
			if (base.IsOwner)
			{
				if (!_allowReachTargetReload)
				{
					UtilityMethods.Log($"[HarpoonTool] ReachTarget ignored. AllowReload=false State={_state}");
				}
				else if (!IsFired)
				{
					UtilityMethods.Log($"[HarpoonTool] ReachTarget ignored. State={_state}");
					_allowReachTargetReload = false;
				}
				else
				{
					_allowReachTargetReload = false;
					UtilityMethods.Log("[HarpoonTool] ReachTarget");
					StartReload();
				}
			}
		}

		private bool CanHookPlayer(PlayerInteraction client)
		{
			if (client == null || client.OwnerPlayer == null)
			{
				return false;
			}
			Interactor interactor = client.OwnerPlayer.Interactor;
			if (!(interactor == null))
			{
				return !interactor.IsUsing;
			}
			return true;
		}

		private void RetractDone()
		{
			if (!_instantReloadInProgress)
			{
				if (!IsReloading)
				{
					UtilityMethods.Log($"[HarpoonTool] RetractDone ignored. State={_state}");
					return;
				}
				UtilityMethods.Log("[HarpoonTool] RetractDone");
				ResetStartReloadCor();
				_startReloadDelayCor = Singleton<DelayHelper>.Instance.DelayAction(0.3f, ReloadAnim);
			}
		}

		public void HitRaycast(Camera camera)
		{
			if (!OnAim)
			{
				_player.UICenterFeedback(willHit: false);
				return;
			}
			if (_currentLevelData == null)
			{
				_player.UICenterFeedback(willHit: false);
				return;
			}
			Physics.Raycast(camera.transform.position, camera.transform.forward, out var hitInfo, _currentLevelData.ShootRange + _tipSensor.sphereRadius, _tipSensor.hitMask);
			_player.UICenterFeedback(hitInfo.collider != null);
		}

		public void SetTpsTipGraphic(bool s)
		{
			if (!(_tpsTipGraphic == null))
			{
				_tpsTipGraphic.SetActive(s);
			}
		}

		public void SetFpsTipGraphic(bool s)
		{
			if (!(_fpsTipGraphic == null))
			{
				_fpsTipGraphic.SetActive(s);
			}
		}

		public void SetTpsSpearGraphic(bool s)
		{
			if (!(_tpsSpearTransform == null))
			{
				_tpsSpearTransform.gameObject.SetActive(s);
			}
		}

		public void SetFpsSpearGraphic(bool s)
		{
			if (!(_fpsSpearTransform == null))
			{
				_fpsSpearTransform.gameObject.SetActive(s);
			}
		}

		public void ReloadDone()
		{
			if (!IsReloading)
			{
				UtilityMethods.Log($"[HarpoonTool] ReloadDone ignored. State={_state} " + $"fpsStuck={_fpsStuckTipInstance != null} " + $"tpsStuck={_tpsStuckTipInstance != null}");
				return;
			}
			UtilityMethods.Log("[HarpoonTool] ReloadDone " + $"fpsStuck={_fpsStuckTipInstance != null} " + $"tpsStuck={_tpsStuckTipInstance != null}");
			ForceResetVisualState(hardStopGrapple: true, restoreHeldSpearGraphic: true);
			SetState(HarpoonState.Idle);
			ServerHardStopGrappleToRpc();
			_player.UIAimFeedback(status: false);
		}

		public void ReloadSFX()
		{
			Vector3 position = _player.transform.position + Vector3.up + _player.transform.forward;
			Singleton<SoundManager>.Instance.PlaySFX(SoundType.Harpoon_Reload, position, _player.transform);
		}

		private IEnumerator MonitorPlayerPullReloadRoutine(PPG_Player targetPlayer)
		{
			float elapsed = 0f;
			WaitForFixedUpdate wait = new WaitForFixedUpdate();
			while (elapsed < _playerPullSafetyMaxSeconds && !(targetPlayer == null))
			{
				if (!IsHookedPlayer)
				{
					yield break;
				}
				if (Vector3.Distance(_player.transform.position, targetPlayer.transform.position) <= _playerPullArriveDistance)
				{
					targetPlayer.StopHarpoonedPull();
					StartReload();
					yield break;
				}
				elapsed += Time.fixedDeltaTime;
				yield return wait;
			}
			if (targetPlayer != null)
			{
				targetPlayer.StopHarpoonedPull();
			}
			StartReload();
		}

		private Quaternion BuildStuckRotation(Vector3 hitNormal)
		{
			return Quaternion.LookRotation(-((hitNormal.sqrMagnitude > 1E-06f) ? hitNormal.normalized : Vector3.up), Vector3.up);
		}

		private GameObject EnsureStuckInstance(ref GameObject instance, GameObject source)
		{
			if (instance != null)
			{
				return instance;
			}
			if (source == null)
			{
				return null;
			}
			instance = UnityEngine.Object.Instantiate(source);
			instance.name = source.name + "_StuckClone";
			instance.SetActive(value: true);
			return instance;
		}

		private Transform PlaceStuck(GameObject source, ref GameObject instance, Vector3 hitPoint, Vector3 hitNormal, string layerName)
		{
			GameObject gameObject = EnsureStuckInstance(ref instance, source);
			if (gameObject == null)
			{
				return null;
			}
			Vector3 vector = ((hitNormal.sqrMagnitude > 1E-06f) ? hitNormal.normalized : Vector3.up);
			Vector3 position = hitPoint + vector * _stuckTipNormalOffset;
			Quaternion rotation = BuildStuckRotation(hitNormal);
			gameObject.transform.SetParent(null, worldPositionStays: true);
			gameObject.transform.SetPositionAndRotation(position, rotation);
			gameObject.SetActive(value: true);
			if (!string.IsNullOrEmpty(layerName))
			{
				UtilityMethods.SetLayerRecursive(gameObject, layerName, includeChildren: true);
			}
			Transform child = gameObject.transform.GetChild(0).GetChild(0);
			if (!(child != null))
			{
				return gameObject.transform;
			}
			return child;
		}

		private void ClearStuck(ref GameObject instance)
		{
			if (!(instance == null))
			{
				instance.SetActive(value: false);
				instance.transform.SetParent(null, worldPositionStays: true);
				UnityEngine.Object.Destroy(instance);
				instance = null;
			}
		}

		[Rpc(SendTo.Owner)]
		public void ClientBarBlockStatusRpc(bool status)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2301285499u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in status, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2301285499u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				SetBarBlockStatusLocal(status);
			}
		}

		public void SetBarBlockStatusLocal(bool status)
		{
			if (_barBlock != null)
			{
				_barBlock.SetActive(status);
			}
		}

		[Rpc(SendTo.Owner)]
		public void OwnerFailPullableRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1428109325u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1428109325u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				string description = LocalizationDatabase.GetString("UiTable", "gameplay.pullable_rock.level_too_low", "Your harpoon level isn't high enough. Try it together with your friends!");
				Singleton<NotifyManager>.Instance.ShowAlert(description, 2f, AlertPriority.High);
			}
		}

		private void ForceResetVisualState(bool hardStopGrapple, bool restoreHeldSpearGraphic)
		{
			UtilityMethods.Log("[HarpoonTool] ForceResetVisualState " + $"hardStop={hardStopGrapple} " + $"restoreHeldSpear={restoreHeldSpearGraphic} " + $"fpsStuck={_fpsStuckTipInstance != null} " + $"tpsStuck={_tpsStuckTipInstance != null}");
			_allowReachTargetReload = false;
			ClearStuck(ref _fpsStuckTipInstance);
			ClearStuck(ref _tpsStuckTipInstance);
			if (_hookedPoint == null)
			{
				_hookedPoint = new GameObject("HookPoint").transform;
			}
			_hookedPoint.SetParent(null);
			if (_tipSensor != null)
			{
				_tipSensor.EndSense();
			}
			if (_player != null && _player.FirstPersonCharacter != null)
			{
				_player.FirstPersonCharacter.StopActiveMotion();
			}
			if (_grapple != null)
			{
				_grapple.ClearVisualTarget();
				if (hardStopGrapple)
				{
					_grapple.HardStop();
				}
			}
			if (restoreHeldSpearGraphic && _fpsSpearTransform != null)
			{
				_fpsSpearTransform.gameObject.SetActive(value: true);
			}
			if (restoreHeldSpearGraphic && _tpsSpearTransform != null)
			{
				_tpsSpearTransform.gameObject.SetActive(value: true);
			}
		}

		private void StartChallengeCancelHold()
		{
			if (base.IsOwner && IsChallenge && _challengeStart && !_challengeCancelHoldActive)
			{
				_challengeCancelHoldActive = true;
				_challengeCancelHoldTimer = 0f;
				UtilityMethods.Log("[HarpoonTool_Challange] Challenge cancel hold started.");
			}
		}

		private void ResetChallengeCancelHold()
		{
			if (_challengeCancelHoldActive || !(_challengeCancelHoldTimer <= 0f))
			{
				_challengeCancelHoldActive = false;
				_challengeCancelHoldTimer = 0f;
				_challengeMouseCancelHoldFromDevice = false;
				UtilityMethods.Log("[HarpoonTool_Challange] Challenge cancel hold reset.");
			}
		}

		private void ChallengeCancelHold_Tick()
		{
			if (!_challengeCancelHoldActive)
			{
				return;
			}
			if (!IsChallenge || !_challengeStart)
			{
				ResetChallengeCancelHold();
				return;
			}
			_challengeCancelHoldTimer += Time.deltaTime;
			if (!(_challengeCancelHoldTimer < _challengeCancelHoldDuration))
			{
				ResetChallengeCancelHold();
				CancelChallengeByInput();
			}
		}

		private void SetAssistFeedbackStatus(bool status)
		{
			if (!(_assistFeedbackRoot == null))
			{
				_assistFeedbackRoot.SetActive(status);
			}
		}

		private void Challenge_RegisterLocalAssistBarReceiver()
		{
			if (base.IsOwner)
			{
				s_localOwnedAssistBarReceiver = this;
			}
		}

		private void Challenge_UnregisterLocalAssistBarReceiver()
		{
			if (!(s_localOwnedAssistBarReceiver != this))
			{
				s_localOwnedAssistBarReceiver = null;
			}
		}

		private void Challenge_TryPublishAssistBarValue()
		{
			if (base.IsOwner && _challengeStart && _sharedCatchActive && !(_currentFish == null) && !(_maxValue <= 0f))
			{
				_assistBarSyncTimer += Time.deltaTime;
				float num = Mathf.Clamp01(_currentValue / _maxValue);
				float num2 = Mathf.Abs(num - _lastSentAssistBarValue01);
				bool flag = _assistBarSyncTimer >= _assistBarSyncInterval;
				bool flag2 = num2 >= _assistBarMinDelta;
				if (((num <= 0.001f || num >= 0.999f) && num2 > 0.001f) || (flag && flag2))
				{
					_assistBarSyncTimer = 0f;
					_lastSentAssistBarValue01 = num;
					ulong senderSteamIdRaw = (SteamClient.IsValid ? SteamClient.SteamId.Value : 0);
					ServerReportAssistBarValueRpc(_currentFish.NetworkObject.ToReference(), num, senderSteamIdRaw);
				}
			}
		}

		private void Challenge_TickAssistPins()
		{
			if (!base.IsOwner || _assistBarPins.Count <= 0)
			{
				return;
			}
			List<ulong> list = null;
			foreach (KeyValuePair<ulong, HarpoonAssistBarPinUI> assistBarPin in _assistBarPins)
			{
				HarpoonAssistBarPinUI value = assistBarPin.Value;
				if (value == null)
				{
					if (list == null)
					{
						list = new List<ulong>();
					}
					list.Add(assistBarPin.Key);
					continue;
				}
				value.Tick(_assistPinContainer, Time.unscaledDeltaTime);
				if (value.IsExpired(_assistPinTimeout))
				{
					if (list == null)
					{
						list = new List<ulong>();
					}
					list.Add(assistBarPin.Key);
				}
			}
			if (list == null)
			{
				return;
			}
			for (int i = 0; i < list.Count; i++)
			{
				ulong key = list[i];
				if (_assistBarPins.TryGetValue(key, out var value2))
				{
					if (value2 != null)
					{
						UnityEngine.Object.Destroy(value2.gameObject);
					}
					_assistBarPins.Remove(key);
				}
			}
		}

		private void Challenge_ClearAssistPins()
		{
			foreach (KeyValuePair<ulong, HarpoonAssistBarPinUI> assistBarPin in _assistBarPins)
			{
				if (assistBarPin.Value != null)
				{
					UnityEngine.Object.Destroy(assistBarPin.Value.gameObject);
				}
			}
			_assistBarPins.Clear();
			_assistBarSyncTimer = 0f;
			_lastSentAssistBarValue01 = -1f;
		}

		private HarpoonAssistBarPinUI Challenge_GetOrCreateAssistPin(ulong senderClientId)
		{
			if (_assistPinContainer == null || _assistPinPrefab == null)
			{
				return null;
			}
			if (_assistBarPins.TryGetValue(senderClientId, out var value) && value != null)
			{
				return value;
			}
			HarpoonAssistBarPinUI harpoonAssistBarPinUI = UnityEngine.Object.Instantiate(_assistPinPrefab, _assistPinContainer);
			harpoonAssistBarPinUI.name = $"AssistBarPin_{senderClientId}";
			harpoonAssistBarPinUI.Initialize(senderClientId, _assistAvatarFallback);
			harpoonAssistBarPinUI.transform.SetAsLastSibling();
			_assistBarPins[senderClientId] = harpoonAssistBarPinUI;
			return harpoonAssistBarPinUI;
		}

		private void Challenge_ReceiveAssistBarValueLocal(NetworkObjectReference creatureReference, ulong senderClientId, ulong senderSteamIdRaw, float value01)
		{
			if (!base.IsOwner || senderClientId == base.OwnerClientId || !_challengeStart || !_sharedCatchActive || _currentFish == null || !creatureReference.TryGet(out var networkObject) || !networkObject.TryGetComponent<CatchableSeaCreature>(out var component) || component != _currentFish)
			{
				return;
			}
			HarpoonAssistBarPinUI harpoonAssistBarPinUI = Challenge_GetOrCreateAssistPin(senderClientId);
			if (!(harpoonAssistBarPinUI == null))
			{
				harpoonAssistBarPinUI.SetTargetValue(value01);
				if (harpoonAssistBarPinUI.ShouldLoadAvatar(senderSteamIdRaw))
				{
					Challenge_LoadAssistAvatarAsync(harpoonAssistBarPinUI, senderSteamIdRaw).Forget();
				}
			}
		}

		private async UniTaskVoid Challenge_LoadAssistAvatarAsync(HarpoonAssistBarPinUI pin, ulong steamIdRaw)
		{
			if (!(pin == null) && steamIdRaw != 0L)
			{
				pin.MarkAvatarLoadRequested(steamIdRaw);
				Sprite avatar = await SteamAvatarCache.GetAvatarSpriteAsync(steamIdRaw, _assistAvatarFallback);
				if (!(pin == null) && pin.CanApplyAvatar(steamIdRaw))
				{
					pin.SetAvatar(steamIdRaw, avatar);
				}
			}
		}

		[Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
		private void ServerReportAssistBarValueRpc(NetworkObjectReference creatureReference, float value01, ulong senderSteamIdRaw)
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
					Delivery = RpcDelivery.Unreliable
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2855019682u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Unreliable);
				bufferWriter.WriteValueSafe(in creatureReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in value01, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, senderSteamIdRaw);
				__endSendRpc(ref bufferWriter, 2855019682u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Unreliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (creatureReference.TryGet(out var networkObject) && networkObject.TryGetComponent<CatchableSeaCreature>(out var component) && component.AssistCatchActive && !(_currentFish != component))
				{
					float value2 = Mathf.Clamp01(value01);
					ClientReceiveAssistBarValueRpc(creatureReference, base.OwnerClientId, senderSteamIdRaw, value2);
				}
			}
		}

		[Rpc(SendTo.NotOwner, Delivery = RpcDelivery.Unreliable)]
		private void ClientReceiveAssistBarValueRpc(NetworkObjectReference creatureReference, ulong senderClientId, ulong senderSteamIdRaw, float value01)
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
					Delivery = RpcDelivery.Unreliable
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3863465075u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Unreliable);
				bufferWriter.WriteValueSafe(in creatureReference, default(FastBufferWriter.ForNetworkSerializable));
				BytePacker.WriteValueBitPacked(bufferWriter, senderClientId);
				BytePacker.WriteValueBitPacked(bufferWriter, senderSteamIdRaw);
				bufferWriter.WriteValueSafe(in value01, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3863465075u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Unreliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!(s_localOwnedAssistBarReceiver == null))
				{
					s_localOwnedAssistBarReceiver.Challenge_ReceiveAssistBarValueLocal(creatureReference, senderClientId, senderSteamIdRaw, value01);
				}
			}
		}

		private void BindAssistCatchEvents()
		{
			if (!_assistCatchEventsBound)
			{
				_assistZoneCompletedBinding = new EventBinding<AssistCatchZoneCompletedEvent>(OnAssistCatchZoneCompleted);
				_assistFinishedBinding = new EventBinding<AssistCatchFinishedEvent>(OnAssistCatchFinished);
				_assistParticipantCountBinding = new EventBinding<AssistCatchParticipantCountChangedEvent>(OnAssistCatchParticipantCountChanged);
				_assistCancelledBinding = new EventBinding<AssistCatchCancelledEvent>(OnAssistCatchCancelled);
				_pullDropTargetZoneCompletedBinding = new EventBinding<PullDropTargetZoneCompletedEvent>(OnPullDropTargetZoneCompleted);
				_pullDropTargetFinishedBinding = new EventBinding<PullDropTargetFinishedEvent>(OnPullDropTargetFinished);
				_pullDropTargetParticipantCountBinding = new EventBinding<PullDropTargetParticipantCountChangedEvent>(OnPullDropTargetParticipantCountChanged);
				_pullDropTargetCancelledBinding = new EventBinding<PullDropTargetCancelledEvent>(OnPullDropTargetCancelled);
				EventBus<AssistCatchCancelledEvent>.Register(_assistCancelledBinding);
				EventBus<AssistCatchZoneCompletedEvent>.Register(_assistZoneCompletedBinding);
				EventBus<AssistCatchFinishedEvent>.Register(_assistFinishedBinding);
				EventBus<AssistCatchParticipantCountChangedEvent>.Register(_assistParticipantCountBinding);
				EventBus<PullDropTargetCancelledEvent>.Register(_pullDropTargetCancelledBinding);
				EventBus<PullDropTargetZoneCompletedEvent>.Register(_pullDropTargetZoneCompletedBinding);
				EventBus<PullDropTargetFinishedEvent>.Register(_pullDropTargetFinishedBinding);
				EventBus<PullDropTargetParticipantCountChangedEvent>.Register(_pullDropTargetParticipantCountBinding);
				_assistCatchEventsBound = true;
			}
		}

		private void UnbindAssistCatchEvents()
		{
			if (_assistCatchEventsBound)
			{
				EventBus<AssistCatchCancelledEvent>.Deregister(_assistCancelledBinding);
				EventBus<AssistCatchZoneCompletedEvent>.Deregister(_assistZoneCompletedBinding);
				EventBus<AssistCatchFinishedEvent>.Deregister(_assistFinishedBinding);
				EventBus<AssistCatchParticipantCountChangedEvent>.Deregister(_assistParticipantCountBinding);
				EventBus<PullDropTargetCancelledEvent>.Deregister(_pullDropTargetCancelledBinding);
				EventBus<PullDropTargetZoneCompletedEvent>.Deregister(_pullDropTargetZoneCompletedBinding);
				EventBus<PullDropTargetFinishedEvent>.Deregister(_pullDropTargetFinishedBinding);
				EventBus<PullDropTargetParticipantCountChangedEvent>.Deregister(_pullDropTargetParticipantCountBinding);
				_assistZoneCompletedBinding = null;
				_assistFinishedBinding = null;
				_assistParticipantCountBinding = null;
				_pullDropTargetZoneCompletedBinding = null;
				_pullDropTargetFinishedBinding = null;
				_pullDropTargetParticipantCountBinding = null;
				_pullDropTargetCancelledBinding = null;
				_assistCatchEventsBound = false;
			}
		}

		private void OnAssistCatchCancelled(AssistCatchCancelledEvent evt)
		{
			CacheInvalidatedAssistCreature(evt.Creature);
			UtilityMethods.Log("[Assist DEBUG] OnAssistCatchCancelled ENTER. " + $"IsOwner={base.IsOwner} ChallengeStart={_challengeStart} " + $"CurrentFishNull={_currentFish == null} " + $"CreatureMatch={evt.Creature == _currentFish} " + $"OwnerClientId={base.OwnerClientId} EventPrimary={evt.PrimaryClientId} " + $"LocalPrimary={_sharedCatchPrimaryClientId} IsPrimary={_isPrimaryCatchPlayer} " + $"Reason={evt.Reason}");
			if (base.IsOwner && _challengeStart && !(_currentFish == null) && !(evt.Creature != _currentFish) && !_isPrimaryCatchPlayer)
			{
				Challenge_EndAsAssist();
			}
		}

		private void OnAssistCatchZoneCompleted(AssistCatchZoneCompletedEvent evt)
		{
			if (base.IsOwner && _challengeStart && !(_currentFish == null) && !(evt.Creature != _currentFish))
			{
				Challenge_ApplySharedCompletedZone(evt.ZoneIndex);
				_currentFishPullProgress = Mathf.Clamp01(evt.GlobalProgress01);
				UtilityMethods.Log("[HarpoonTool_Assist] Zone completed event received. " + $"Zone={evt.ZoneIndex + 1} Global={evt.GlobalProgress01:0.00}");
			}
		}

		private void OnAssistCatchFinished(AssistCatchFinishedEvent evt)
		{
			CacheInvalidatedAssistCreature(evt.Creature);
			UtilityMethods.Log("[Assist DEBUG] OnAssistCatchFinished ENTER. " + $"IsOwner={base.IsOwner} ChallengeStart={_challengeStart} " + $"CurrentFishNull={_currentFish == null} " + $"CreatureMatch={evt.Creature == _currentFish} " + $"OwnerClientId={base.OwnerClientId} EventPrimary={evt.PrimaryClientId} " + $"LocalPrimary={_sharedCatchPrimaryClientId} IsPrimary={_isPrimaryCatchPlayer}");
			if (base.IsOwner && _challengeStart && !(_currentFish == null) && !(evt.Creature != _currentFish))
			{
				SetAssistFeedbackStatus(status: false);
				_isAssistCatchFinisher = base.OwnerClientId == evt.FinisherClientId;
				if (_isAssistCatchFinisher)
				{
					Challenge_Success();
				}
				else
				{
					Challenge_EndAsAssist();
				}
			}
		}

		private void OnAssistCatchParticipantCountChanged(AssistCatchParticipantCountChangedEvent evt)
		{
			if (base.IsOwner && _challengeStart && !(_currentFish == null) && !(evt.Creature != _currentFish))
			{
				bool flag = evt.ParticipantCount > 1;
				SetAssistFeedbackStatus(flag);
				if (!flag)
				{
					Challenge_ClearAssistPins();
				}
				UtilityMethods.Log($"[HarpoonTool_Assist] Participant count changed. Count={evt.ParticipantCount}");
			}
		}

		private void OnPullDropTargetZoneCompleted(PullDropTargetZoneCompletedEvent evt)
		{
			if (base.IsOwner && _challengeStart && !(_currentPullDropTarget == null) && !(evt.Target != _currentPullDropTarget))
			{
				Challenge_ApplySharedCompletedZone(evt.ZoneIndex);
			}
		}

		private void OnPullDropTargetFinished(PullDropTargetFinishedEvent evt)
		{
			if (base.IsOwner && _challengeStart && !(_currentPullDropTarget == null) && !(evt.Target != _currentPullDropTarget))
			{
				SetAssistFeedbackStatus(status: false);
				_currentPullDropTarget = null;
				_challengeStart = false;
				Challenge_End();
				StartReload();
			}
		}

		private void OnPullDropTargetParticipantCountChanged(PullDropTargetParticipantCountChangedEvent evt)
		{
			if (base.IsOwner && _challengeStart && !(_currentPullDropTarget == null) && !(evt.Target != _currentPullDropTarget))
			{
				bool flag = evt.ParticipantCount > 1;
				SetAssistFeedbackStatus(flag);
				if (!flag)
				{
					Challenge_ClearAssistPins();
				}
			}
		}

		private void OnPullDropTargetCancelled(PullDropTargetCancelledEvent evt)
		{
			if (base.IsOwner && _challengeStart && !(_currentPullDropTarget == null) && !(evt.Target != _currentPullDropTarget))
			{
				SetAssistFeedbackStatus(status: false);
				_currentPullDropTarget = null;
				_challengeStart = false;
				Challenge_End();
				StartReload();
			}
		}

		private void CacheInvalidatedAssistCreature(CatchableSeaCreature creature)
		{
			if (base.IsOwner && !(creature == null))
			{
				_recentlyInvalidatedAssistCreature = creature;
				_recentAssistInvalidatedTime = Time.realtimeSinceStartup;
			}
		}

		private bool HasRecentlyInvalidatedAssistCreature(CatchableSeaCreature creature)
		{
			if (creature == null || _recentlyInvalidatedAssistCreature != creature)
			{
				return false;
			}
			return Time.realtimeSinceStartup - _recentAssistInvalidatedTime <= 2f;
		}

		private void StartFishChallenge(int zoneCount)
		{
			_sharedCatchActive = true;
			_isPrimaryCatchPlayer = true;
			_isAssistCatchFinisher = false;
			_sharedCatchPrimaryClientId = base.OwnerClientId;
			if (_currentFish.FishSO.Size == CreatureSize.Tiny)
			{
				Challenge_Success();
				return;
			}
			PrepareOwnerForFishChallenge();
			if (Singleton<PPG_WheelManager>.Instance != null)
			{
				Singleton<PPG_WheelManager>.Instance.DisableToolWheel = true;
				Singleton<PPG_WheelManager>.Instance.DisableEmoteWheel = true;
			}
			_activeSuccessZoneIndex = -1;
			HarpoonChallengePreset preset = _data.GetPreset(_currentFish.FishSO.FishingMode, _currentFish.FishSO.Rarity);
			Challenge_InitializeFishTimer(in preset);
			if (!Challenge_IsPresetValid(in preset))
			{
				UtilityMethods.Log("[HarpoonTool_Challange] Invalid harpoon challenge preset.", LogType.Error);
				_challengeStart = true;
				Challenge_Fail();
			}
			else
			{
				Challenge_Setup(in preset, zoneCount);
				ServerRegisterAssistSuccessZoneNetDataRpc(_currentFish.NetworkObject.ToReference(), Challenge_BuildSuccessZoneNetData());
				CacheChallengeFishPull();
				_challengeStart = true;
			}
		}

		private void StartPullDropTargetChallenge(int zoneCount)
		{
			if (_currentPullDropTarget == null)
			{
				StartReload();
				return;
			}
			_sharedCatchActive = true;
			_isPrimaryCatchPlayer = true;
			_isAssistCatchFinisher = false;
			_sharedCatchPrimaryClientId = base.OwnerClientId;
			if (Singleton<PPG_WheelManager>.Instance != null)
			{
				Singleton<PPG_WheelManager>.Instance.DisableToolWheel = true;
				Singleton<PPG_WheelManager>.Instance.DisableEmoteWheel = true;
			}
			Challenge_InitializeDefaultTimer();
			_activeSuccessZoneIndex = -1;
			HarpoonChallengePreset preset = _data.GetPreset(_currentPullDropTarget.ChallengeFishingMode, _currentPullDropTarget.ChallengeRarity);
			if (!Challenge_IsPresetValid(in preset))
			{
				UtilityMethods.Log("[HarpoonTool_Challenge] Invalid pull drop target challenge preset.", LogType.Error);
				_challengeStart = true;
				Challenge_Fail();
			}
			else
			{
				zoneCount = Mathf.Clamp(zoneCount, 1, Mathf.Max(1, _maxSuccessZoneCount));
				Challenge_Setup(in preset, zoneCount, useStalactiteHoldUI: true);
				_challengeStart = true;
			}
		}

		private bool Challenge_IsPresetValid(in HarpoonChallengePreset preset)
		{
			if (preset.SuccessRange <= 0f)
			{
				return false;
			}
			if (preset.DecreaseFactor <= 0f)
			{
				return false;
			}
			if (preset.IncreaseFactor <= 0f)
			{
				return false;
			}
			if (preset.HoldTimeDivider <= 0f)
			{
				return false;
			}
			return true;
		}

		private void Challenge_InitializeFishTimer(in HarpoonChallengePreset preset)
		{
			float activeChallengeDuration = Mathf.Max(0.1f, _pullTimer);
			if (_currentLevelData == null)
			{
				UtilityMethods.Log("[HarpoonTool_Challenge] Fish challenge duration fallback used because current level data is null.", LogType.Warning);
				_activeChallengeDuration = activeChallengeDuration;
				_currentPullTimer = _activeChallengeDuration;
			}
			else if (_currentLevelData.BaseChallengeDuration <= 0f || _currentLevelData.ChallengeDifficultyResistance <= 0f)
			{
				UtilityMethods.Log("[HarpoonTool_Challenge] Fish challenge duration fallback used because level tuning is invalid. " + $"Level={Level} BaseDuration={_currentLevelData.BaseChallengeDuration:0.00} " + $"DifficultyResistance={_currentLevelData.ChallengeDifficultyResistance:0.00}", LogType.Error);
				_activeChallengeDuration = activeChallengeDuration;
				_currentPullTimer = _activeChallengeDuration;
			}
			else
			{
				float num = Mathf.Max(0f, preset.ChallengeDurationPenalty) / _currentLevelData.ChallengeDifficultyResistance;
				_activeChallengeDuration = Mathf.Max(0.1f, _currentLevelData.BaseChallengeDuration - num);
				_currentPullTimer = _activeChallengeDuration;
			}
		}

		private void Challenge_InitializeDefaultTimer()
		{
			_activeChallengeDuration = Mathf.Max(0.1f, _pullTimer);
			_currentPullTimer = _activeChallengeDuration;
		}

		private void CacheChallengeFishPull()
		{
			if (!(_currentFish == null))
			{
				_fishChallengeStartWorldPosition = _currentFish.transform.position;
				_hasCachedFishChallengeStartWorldPosition = true;
				_currentFishPullProgress = 0f;
			}
		}

		private void Challenge_Setup(in HarpoonChallengePreset preset, int zoneCount, bool useStalactiteHoldUI = false)
		{
			Challenge_SetupBase(in preset);
			Challenge_GenerateSuccessZones(in preset, zoneCount);
			Challenge_FinalizeSuccessZoneSetup();
			UseChallengeHoldUI(useStalactiteHoldUI);
			Challenge_UpdateUI();
			Challenge_Show();
			AlignHoldRootToSuccessZoneIndex(0);
		}

		private bool Challenge_SetupFromSyncedZones(in HarpoonChallengePreset preset, HarpoonSuccessZoneNetData zoneNetData, bool useStalactiteHoldUI = false)
		{
			Challenge_SetupBase(in preset);
			if (!Challenge_ApplySyncedSuccessZones(zoneNetData))
			{
				return false;
			}
			Challenge_FinalizeSuccessZoneSetup();
			UseChallengeHoldUI(useStalactiteHoldUI);
			Challenge_UpdateUI();
			Challenge_Show();
			AlignHoldRootToSuccessZoneIndex(0);
			return true;
		}

		private void Challenge_SetupBase(in HarpoonChallengePreset preset)
		{
			Challenge_StopTensionSfx();
			if (Singleton<SoundManager>.Instance != null)
			{
				_harpoonTensionLoop = Singleton<SoundManager>.Instance.PlayOwnedLoop(SoundType.Harpoon_Rope_Tension, (_player != null) ? _player.transform.position : base.transform.position, (_player != null) ? _player.transform : base.transform, $"HarpoonTool-{base.OwnerClientId}");
			}
			Challenge_ResetBubbleLoopFeedback();
			_player.EnterHarpoonReel();
			_currentValue = 0f;
			_holdTimer = 0f;
			_currentDecreaseRate = _currentLevelData.DecreasePerSecond * preset.DecreaseFactor;
			_currentIncreaseInput = _currentLevelData.IncreasePerInput * preset.IncreaseFactor;
			_currentHoldTime = _currentLevelData.HoldTime / preset.HoldTimeDivider;
		}

		private void Challenge_FinalizeSuccessZoneSetup()
		{
			_zoneHoldTimeRequired = ((_successZones.Count > 0) ? Mathf.Max(0.05f, _currentHoldTime / (float)_successZones.Count) : Mathf.Max(0.05f, _currentHoldTime));
			Challenge_RefreshSuccessZoneUI();
		}

		private void Challenge_ResetBubbleLoopFeedback()
		{
			Challenge_StopBubbleLoopFeedback();
			_bubbleActiveZoneIndex = -1;
		}

		private void UseChallengeHoldUI(bool useStalactiteHoldUI)
		{
			bool flag = useStalactiteHoldUI && _stalactiteHoldRoot != null && _stalactiteHoldFill != null;
			_activeHoldRoot = (flag ? _stalactiteHoldRoot : _uiHoldRoot);
			_activeHoldFill = (flag ? _stalactiteHoldFill : _uiHoldFill);
			SetChallengeHoldRootActive(_uiHoldRoot, _activeHoldRoot == _uiHoldRoot);
			SetChallengeHoldRootActive(_stalactiteHoldRoot, _activeHoldRoot == _stalactiteHoldRoot);
			ResetChallengeHoldFill(_uiHoldFill);
			ResetChallengeHoldFill(_stalactiteHoldFill);
		}

		private void HideChallengeHoldUI()
		{
			ResetChallengeHoldFill(_uiHoldFill);
			ResetChallengeHoldFill(_stalactiteHoldFill);
			SetChallengeHoldRootActive(_uiHoldRoot, active: false);
			SetChallengeHoldRootActive(_stalactiteHoldRoot, active: false);
			_activeHoldRoot = null;
			_activeHoldFill = null;
		}

		private static void SetChallengeHoldRootActive(RectTransform root, bool active)
		{
			if (root != null)
			{
				root.gameObject.SetActive(active);
			}
		}

		private static void ResetChallengeHoldFill(Image fill)
		{
			if (fill != null)
			{
				fill.fillAmount = 0f;
			}
		}

		private void Challenge_StartBubbleLoopFeedback(int zoneIndex)
		{
			if (!_useBubbleLoopFeedback || Singleton<SoundManager>.Instance == null || _bubbleLoopSFX == null)
			{
				return;
			}
			if (!Challenge_IsBubbleLoopPlaybackActive())
			{
				_challengeBubbleLoopHandle = null;
				_challengeBubbleLoopSFX = null;
			}
			if (!(_challengeBubbleLoopSFX != null) || !_challengeBubbleLoopSFX.isPlaying || _bubbleActiveZoneIndex != zoneIndex)
			{
				Challenge_StopBubbleLoopFeedback();
				SoundManager instance = Singleton<SoundManager>.Instance;
				AudioClipSO bubbleLoopSFX = _bubbleLoopSFX;
				string ownerName = $"HarpoonBubble-{base.OwnerClientId}";
				_challengeBubbleLoopHandle = instance.PlayOwnedLoop(bubbleLoopSFX, default, null, ownerName);
				_challengeBubbleLoopSFX = _challengeBubbleLoopHandle?.Source;
				if (_challengeBubbleLoopSFX == null)
				{
					_challengeBubbleLoopHandle = null;
					return;
				}
				_challengeBubbleLoopSFX.pitch = _bubbleMinPitch;
				_challengeBubbleLoopSFX.volume = _bubbleMinVolume;
				_bubbleActiveZoneIndex = zoneIndex;
			}
		}

		private void AlignHoldRootToSuccessZoneIndex(int zoneIndex)
		{
			if (!(_activeHoldRoot == null) && zoneIndex >= 0 && zoneIndex < _uiSuccessZoneInstances.Count)
			{
				RectTransform rectTransform = _uiSuccessZoneInstances[zoneIndex];
				if (!(rectTransform == null))
				{
					AlignHoldRootToSuccessZoneCenter(rectTransform);
				}
			}
		}

		private void Challenge_GenerateSuccessZones(in HarpoonChallengePreset preset, int requestedZoneCount)
		{
			_successZones.Clear();
			int num = Mathf.Clamp(requestedZoneCount, 1, Mathf.Max(1, _maxSuccessZoneCount));
			float num2 = Mathf.Max(1f, preset.SuccessRange * _currentLevelData.SuccessRangeFactor) / (float)num;
			if (num <= 1)
			{
				GenerateSingleSuccessZone(num2);
			}
			else
			{
				GenerateMultipleSuccessZones(num, num2);
			}
		}

		private void GenerateSingleSuccessZone(float range)
		{
			float num = Mathf.Clamp(range, 1f, _maxValue);
			float num2 = _maxValue * 0.4f;
			float maxInclusive = Mathf.Max(num2, _maxValue - num);
			float start = UnityEngine.Random.Range(num2, maxInclusive);
			AddSuccessZone(start, num);
		}

		private void GenerateMultipleSuccessZones(int zoneCount, float rangePerZone)
		{
			float num = _maxValue * 0.12f;
			float num2 = _maxValue * 0.95f;
			float num3 = Mathf.Max(1f, num2 - num) / (float)zoneCount;
			float num4 = Mathf.Clamp(rangePerZone, 1f, Mathf.Max(1f, num3 * 0.65f));
			for (int i = 0; i < zoneCount; i++)
			{
				float num5 = num + num3 * (float)i;
				float num6 = num + num3 * (float)(i + 1) - num4;
				float start = ((num6 <= num5) ? num5 : UnityEngine.Random.Range(num5, num6));
				AddSuccessZone(start, num4);
			}
		}

		private HarpoonSuccessZoneNetData Challenge_BuildSuccessZoneNetData()
		{
			HarpoonSuccessZoneNetData result = default;
			int num = Mathf.Min(_successZones.Count, 8);
			for (int i = 0; i < num; i++)
			{
				HarpoonSuccessZoneRuntime harpoonSuccessZoneRuntime = _successZones[i];
				byte b = Challenge_EncodeZoneValueToByte(harpoonSuccessZoneRuntime.Min);
				byte b2 = Challenge_EncodeZoneValueToByte(harpoonSuccessZoneRuntime.Max);
				if (b2 <= b)
				{
					b2 = (byte)Mathf.Min(255, b + 1);
				}
				result.TrySetZone(i, b, b2);
			}
			return result;
		}

		private bool Challenge_ApplySyncedSuccessZones(HarpoonSuccessZoneNetData zoneNetData)
		{
			_successZones.Clear();
			int num = Mathf.Clamp(zoneNetData.Count, 0, Mathf.Min(8, Mathf.Max(1, _maxSuccessZoneCount)));
			if (num <= 0)
			{
				return false;
			}
			for (int i = 0; i < num; i++)
			{
				if (zoneNetData.TryGetZone(i, out var min, out var max))
				{
					float value = Challenge_DecodeZoneValueFromByte(min);
					float value2 = Challenge_DecodeZoneValueFromByte(max);
					value = Mathf.Clamp(value, 0f, _maxValue);
					value2 = Mathf.Clamp(value2, 0f, _maxValue);
					if (value2 <= value)
					{
						value2 = Mathf.Min(_maxValue, value + 1f);
					}
					_successZones.Add(new HarpoonSuccessZoneRuntime(value, value2));
				}
			}
			return _successZones.Count > 0;
		}

		private byte Challenge_EncodeZoneValueToByte(float value)
		{
			if (_maxValue <= 0f)
			{
				return 0;
			}
			return (byte)Mathf.RoundToInt(Mathf.Clamp01(value / _maxValue) * 255f);
		}

		private float Challenge_DecodeZoneValueFromByte(byte value)
		{
			return (float)(int)value / 255f * _maxValue;
		}

		private void AddSuccessZone(float start, float range)
		{
			float value = Mathf.RoundToInt(start);
			float value2 = Mathf.RoundToInt(start + range);
			value = Mathf.Clamp(value, 0f, _maxValue);
			value2 = Mathf.Clamp(value2, 0f, _maxValue);
			if (value2 <= value)
			{
				value2 = Mathf.Min(_maxValue, value + 1f);
			}
			_successZones.Add(new HarpoonSuccessZoneRuntime(value, value2));
		}

		private bool TryGetCurrentSuccessZoneRect(out RectTransform successZoneRect)
		{
			successZoneRect = null;
			int num = Challenge_GetActiveIncompleteZoneIndex(_currentValue);
			if (num < 0)
			{
				return false;
			}
			if (num >= _uiSuccessZoneInstances.Count)
			{
				return false;
			}
			RectTransform rectTransform = _uiSuccessZoneInstances[num];
			if (rectTransform == null)
			{
				return false;
			}
			if (!rectTransform.gameObject.activeInHierarchy)
			{
				return false;
			}
			successZoneRect = rectTransform;
			return true;
		}

		private void AlignHoldRootToSuccessZoneCenter(RectTransform successZoneRect)
		{
			if (!(_activeHoldRoot == null) && !(successZoneRect == null))
			{
				RectTransform rectTransform = _activeHoldRoot.parent as RectTransform;
				if (!(rectTransform == null))
				{
					Vector3 position = successZoneRect.TransformPoint(successZoneRect.rect.center);
					Vector3 vector = rectTransform.InverseTransformPoint(position);
					Vector2 anchoredPosition = _activeHoldRoot.anchoredPosition;
					anchoredPosition.y = vector.y;
					_activeHoldRoot.anchoredPosition = anchoredPosition;
				}
			}
		}

		private int Challenge_GetActiveIncompleteZoneIndex(float value)
		{
			for (int i = 0; i < _successZones.Count; i++)
			{
				HarpoonSuccessZoneRuntime harpoonSuccessZoneRuntime = _successZones[i];
				if (!harpoonSuccessZoneRuntime.Completed && harpoonSuccessZoneRuntime.Contains(value))
				{
					return i;
				}
			}
			return -1;
		}

		private float Challenge_GetGlobalProgress01()
		{
			if (_successZones.Count <= 0)
			{
				return 0f;
			}
			float num = 0f;
			for (int i = 0; i < _successZones.Count; i++)
			{
				num += _successZones[i].GetProgress01(_zoneHoldTimeRequired);
			}
			return Mathf.Clamp01(num / (float)_successZones.Count);
		}

		private bool Challenge_AreAllSuccessZonesCompleted()
		{
			if (_successZones.Count <= 0)
			{
				return false;
			}
			for (int i = 0; i < _successZones.Count; i++)
			{
				if (!_successZones[i].Completed)
				{
					return false;
				}
			}
			return true;
		}

		private void Challenge_CompleteSuccessZone(int index)
		{
			if (index >= 0 && index < _successZones.Count)
			{
				HarpoonSuccessZoneRuntime harpoonSuccessZoneRuntime = _successZones[index];
				if (!harpoonSuccessZoneRuntime.Completed)
				{
					harpoonSuccessZoneRuntime.Completed = true;
					harpoonSuccessZoneRuntime.HoldTimer = _zoneHoldTimeRequired;
					Challenge_StopBubbleLoopFeedback();
					Challenge_SetSuccessZoneVisualStatus(index, status: false);
					Challenge_PlayZoneSuccessSFX();
					UtilityMethods.Log($"[HarpoonTool_Challange] Success zone completed: {index + 1}/{_successZones.Count}");
					_activeSuccessZoneIndex = -1;
				}
			}
		}

		private void Challenge_PlayZoneSuccessSFX()
		{
			if (!(Singleton<SoundManager>.Instance == null) && !(_zoneSuccesSFX == null))
			{
				Singleton<SoundManager>.Instance.PlaySFX(_zoneSuccesSFX);
			}
		}

		private void Challenge_SetSuccessZoneVisualStatus(int index, bool status)
		{
			if (index >= 0 && index < _uiSuccessZoneInstances.Count)
			{
				RectTransform rectTransform = _uiSuccessZoneInstances[index];
				if (!(rectTransform == null))
				{
					rectTransform.gameObject.SetActive(status);
				}
			}
		}

		private void Challenge_DecayActiveSuccessZone()
		{
			if (_activeSuccessZoneIndex < 0 || _activeSuccessZoneIndex >= _successZones.Count)
			{
				return;
			}
			HarpoonSuccessZoneRuntime harpoonSuccessZoneRuntime = _successZones[_activeSuccessZoneIndex];
			if (harpoonSuccessZoneRuntime.Completed)
			{
				_activeSuccessZoneIndex = -1;
				return;
			}
			float holdTimer = harpoonSuccessZoneRuntime.HoldTimer;
			harpoonSuccessZoneRuntime.HoldTimer = Mathf.Clamp(harpoonSuccessZoneRuntime.HoldTimer - Time.deltaTime * 7.5f, 0f, _zoneHoldTimeRequired);
			if (holdTimer > 0f && harpoonSuccessZoneRuntime.HoldTimer <= 0f)
			{
				_activeSuccessZoneIndex = -1;
			}
		}

		private void Challenge_RefreshSuccessZoneUI()
		{
			if (_uiSuccessZone == null)
			{
				return;
			}
			int count = _successZones.Count;
			Challenge_EnsureSuccessZoneUIInstances(count);
			for (int i = 0; i < _uiSuccessZoneInstances.Count; i++)
			{
				RectTransform rectTransform = _uiSuccessZoneInstances[i];
				if (rectTransform == null)
				{
					continue;
				}
				bool flag = i < count && !_successZones[i].Completed;
				rectTransform.gameObject.SetActive(flag);
				if (flag)
				{
					HarpoonSuccessZoneRuntime harpoonSuccessZoneRuntime = _successZones[i];
					Challenge_UpdateSuccessZoneRect(rectTransform, harpoonSuccessZoneRuntime.Min / _maxValue, harpoonSuccessZoneRuntime.Max / _maxValue);
					if (rectTransform.TryGetComponent<Image>(out var component))
					{
						component.color = _successZoneDefaultColor;
					}
				}
			}
		}

		private void Challenge_EnsureSuccessZoneUIInstances(int targetCount)
		{
			if (!(_uiSuccessZone == null))
			{
				if (_uiSuccessZoneInstances.Count == 0)
				{
					_uiSuccessZoneInstances.Add(_uiSuccessZone);
				}
				while (_uiSuccessZoneInstances.Count < targetCount)
				{
					RectTransform rectTransform = UnityEngine.Object.Instantiate(_uiSuccessZone, _uiSuccessZone.parent);
					rectTransform.name = $"{_uiSuccessZone.name}_{_uiSuccessZoneInstances.Count}";
					rectTransform.gameObject.SetActive(value: false);
					_uiSuccessZoneInstances.Add(rectTransform);
				}
			}
		}

		private void Challenge_UpdateSuccessZoneRect(RectTransform target, float min01, float max01)
		{
			if (!(target == null))
			{
				float y = Mathf.Clamp01(min01);
				float y2 = Mathf.Clamp01(max01);
				Vector2 anchorMin = target.anchorMin;
				Vector2 anchorMax = target.anchorMax;
				target.anchorMin = new Vector2(anchorMin.x, y);
				target.anchorMax = new Vector2(anchorMax.x, y2);
				target.anchoredPosition = Vector2.zero;
				target.sizeDelta = new Vector2(target.sizeDelta.x, 0f);
			}
		}

		private void Challenge_ClearSuccessZones()
		{
			_successZones.Clear();
			for (int i = 0; i < _uiSuccessZoneInstances.Count; i++)
			{
				RectTransform rectTransform = _uiSuccessZoneInstances[i];
				if (!(rectTransform == null))
				{
					if (rectTransform.TryGetComponent<Image>(out var component))
					{
						component.color = _successZoneDefaultColor;
					}
					rectTransform.gameObject.SetActive(value: false);
				}
			}
			if (_uiSuccessZoneInstances.Count == 0 && _uiSuccessZone != null)
			{
				_uiSuccessZone.gameObject.SetActive(value: false);
			}
			_activeSuccessZoneIndex = -1;
			_zoneHoldTimeRequired = 0f;
			_holdTimer = 0f;
			_sharedCatchActive = false;
			_isPrimaryCatchPlayer = false;
			_isAssistCatchFinisher = false;
			_sharedCatchPrimaryClientId = 0uL;
		}

		private void Challenge_UpdateUI()
		{
			float fillAmount = Mathf.Clamp01(_currentValue / _maxValue);
			if (_uiBarFill != null)
			{
				_uiBarFill.fillAmount = fillAmount;
			}
			if (TryGetCurrentSuccessZoneRect(out var successZoneRect))
			{
				AlignHoldRootToSuccessZoneCenter(successZoneRect);
			}
			float num = Challenge_GetGlobalProgress01();
			_holdTimer = num * _currentHoldTime;
			if ((bool)_activeHoldFill)
			{
				_activeHoldFill.fillAmount = num;
			}
			Challenge_UpdateSuccessZoneFeedback();
			if ((bool)_uiDebug && _debugActive)
			{
				_uiDebug.text = "<b><color=#FFD700>[Harpoon DEBUG]</color></b>\n" + $"Level: {Level}\n" + $"Tool: {base.ToolStatus} | Hooked: {_currentFish != null || _currentPullDropTarget != null}\n" + $"Value: {_currentValue:0.0}/{_maxValue}  Global: {num:P0}\n" + "Zones: " + BuildSuccessZoneDebugText() + "\n" + $"Timer: {_currentPullTimer:0.00}/{_activeChallengeDuration:0.00}";
			}
		}

		private void Challenge_UpdateSuccessZoneFeedback()
		{
			for (int i = 0; i < _uiSuccessZoneInstances.Count; i++)
			{
				RectTransform rectTransform = _uiSuccessZoneInstances[i];
				if (!(rectTransform == null) && i < _successZones.Count && !_successZones[i].Completed && rectTransform.gameObject.activeSelf && rectTransform.TryGetComponent<Image>(out var component))
				{
					bool flag = _successZones[i].Contains(_currentValue);
					component.color = (flag ? _successZoneActiveColor : _successZoneDefaultColor);
				}
			}
		}

		private void Challenge_Show()
		{
			if ((bool)_uiPanel)
			{
				_uiPanel.SetActive(value: true);
			}
			Challenge_StartSpamPromptFeedback();
			_player.Sway.StartFishingSway(0.5f);
			_player.StartFishingCameraShake();
		}

		private void Challenge_Tick()
		{
			if (!_challengeStart || (_currentFish == null && _currentPullDropTarget == null))
			{
				return;
			}
			_currentValue -= _currentDecreaseRate * Time.deltaTime;
			_currentValue = Mathf.Clamp(_currentValue, 0f, _maxValue);
			_currentPullTimer -= Time.deltaTime;
			_currentPullTimer = Mathf.Clamp(_currentPullTimer, 0f, _activeChallengeDuration);
			if (_currentPullTimer == 0f)
			{
				Challenge_Fail(isKnocked: false, showTutorial: true);
				return;
			}
			int num = Challenge_GetActiveIncompleteZoneIndex(_currentValue);
			if (num >= 0)
			{
				_activeSuccessZoneIndex = num;
				HarpoonSuccessZoneRuntime harpoonSuccessZoneRuntime = _successZones[num];
				harpoonSuccessZoneRuntime.HoldTimer += Time.deltaTime;
				harpoonSuccessZoneRuntime.HoldTimer = Mathf.Clamp(harpoonSuccessZoneRuntime.HoldTimer, 0f, _zoneHoldTimeRequired);
				Challenge_UpdateBubbleLoopFeedback(num);
				if (harpoonSuccessZoneRuntime.HoldTimer >= _zoneHoldTimeRequired)
				{
					UtilityMethods.Log("[HarpoonTool_Assist DEBUG] Zone threshold reached. " + $"Zone={num + 1} Hold={harpoonSuccessZoneRuntime.HoldTimer:0.00}/{_zoneHoldTimeRequired:0.00}");
					Challenge_ReportZoneCompleted(num);
				}
			}
			else
			{
				Challenge_StopBubbleLoopFeedback();
				Challenge_DecayActiveSuccessZone();
				if (!_challengeStart)
				{
					return;
				}
			}
			UpdateChallengeFishPullFromSuccessProgress();
			Challenge_UpdateUI();
			Challenge_TryPublishAssistBarValue();
			Challenge_TickAssistPins();
		}

		private void Challenge_UpdateBubbleLoopFeedback(int zoneIndex)
		{
			if (!_useBubbleLoopFeedback)
			{
				return;
			}
			if (zoneIndex < 0 || zoneIndex >= _successZones.Count)
			{
				Challenge_StopBubbleLoopFeedback();
				return;
			}
			HarpoonSuccessZoneRuntime harpoonSuccessZoneRuntime = _successZones[zoneIndex];
			if (harpoonSuccessZoneRuntime.Completed)
			{
				Challenge_StopBubbleLoopFeedback();
				return;
			}
			if (!harpoonSuccessZoneRuntime.Contains(_currentValue))
			{
				Challenge_StopBubbleLoopFeedback();
				return;
			}
			Challenge_StartBubbleLoopFeedback(zoneIndex);
			if (!Challenge_IsBubbleLoopPlaybackActive())
			{
				_challengeBubbleLoopHandle = null;
				_challengeBubbleLoopSFX = null;
				return;
			}
			float progress = harpoonSuccessZoneRuntime.GetProgress01(_zoneHoldTimeRequired);
			float b = Mathf.Lerp(_bubbleMinPitch, _bubbleMaxPitch, progress);
			float b2 = Mathf.Lerp(_bubbleMinVolume, _bubbleMaxVolume, progress);
			_challengeBubbleLoopSFX.pitch = Mathf.Lerp(_challengeBubbleLoopSFX.pitch, b, Time.deltaTime * _bubblePitchSmoothSpeed);
			_challengeBubbleLoopSFX.volume = Mathf.Lerp(_challengeBubbleLoopSFX.volume, b2, Time.deltaTime * _bubbleVolumeSmoothSpeed);
		}

		private void Challenge_ReportZoneCompleted(int zoneIndex)
		{
			if (zoneIndex >= 0 && zoneIndex < _successZones.Count && !_successZones[zoneIndex].Completed)
			{
				Challenge_CompleteSuccessZone(zoneIndex);
				UpdateChallengeFishPullFromSuccessProgress();
				Challenge_UpdateUI();
				if (_currentPullDropTarget != null)
				{
					_currentPullDropTarget.ServerReportPullZoneCompletedRpc(zoneIndex);
					return;
				}
				if (_currentFish == null)
				{
					UtilityMethods.Log("[HarpoonTool_Assist] Zone report failed. Current fish is null.", LogType.Warning);
					return;
				}
				UtilityMethods.Log($"[HarpoonTool_Assist] Reporting completed zone to creature. Zone={zoneIndex + 1}");
				_currentFish.ServerReportAssistZoneCompletedRpc(zoneIndex);
			}
		}

		private void Challenge_ApplySharedCompletedZone(int zoneIndex)
		{
			if (zoneIndex < 0 || zoneIndex >= _successZones.Count)
			{
				return;
			}
			HarpoonSuccessZoneRuntime harpoonSuccessZoneRuntime = _successZones[zoneIndex];
			if (!harpoonSuccessZoneRuntime.Completed)
			{
				harpoonSuccessZoneRuntime.Completed = true;
				harpoonSuccessZoneRuntime.HoldTimer = _zoneHoldTimeRequired;
				if (_bubbleActiveZoneIndex == zoneIndex)
				{
					Challenge_StopBubbleLoopFeedback();
				}
				Challenge_SetSuccessZoneVisualStatus(zoneIndex, status: false);
				_activeSuccessZoneIndex = -1;
				UpdateChallengeFishPullFromSuccessProgress();
				Challenge_UpdateUI();
				UtilityMethods.Log($"[HarpoonTool_Assist] Shared completed zone applied. Zone={zoneIndex + 1}/{_successZones.Count}");
			}
		}

		private void Challenge_EndAsAssist()
		{
			UtilityMethods.Log("[HarpoonTool_Assist] Assist challenge ended.");
			_challengeStart = false;
			NotifyCurrentFishChallengeEnded();
			Challenge_LeaveAssistSessionIfNeeded();
			Challenge_End();
			_currentFish = null;
			StartReload();
		}

		private void Challenge_Success()
		{
			UtilityMethods.Log("[Assist DEBUG] Challenge_Success ENTER. " + $"OwnerClientId={base.OwnerClientId} IsPrimary={_isPrimaryCatchPlayer} " + $"SharedActive={_sharedCatchActive} CurrentFishNull={_currentFish == null} " + $"CurrentPullDropTargetNull={_currentPullDropTarget == null}");
			if (_currentPullDropTarget != null)
			{
				UtilityMethods.Log("[HarpoonTool_Challenge] Pull drop target challenge success.");
				HarpoonPullDropTarget currentPullDropTarget = _currentPullDropTarget;
				_currentPullDropTarget = null;
				_challengeStart = false;
				Challenge_End();
				currentPullDropTarget.CompletePull(this);
				StartReload();
				return;
			}
			if (_sharedCatchActive && !_isPrimaryCatchPlayer && !_isAssistCatchFinisher)
			{
				Challenge_EndAsAssist();
				return;
			}
			UtilityMethods.Log("[HarpoonTool_Challange] Challenge_Success");
			NotifyCurrentFishChallengeEnded();
			_challengeStart = false;
			if (_currentFish == null)
			{
				Challenge_End();
				StartReload();
				return;
			}
			UtilityMethods.Log("[Harpoon Catch DEBUG] Challenge_Success Fish=" + _currentFish.FishSO.name + " " + $"State={_state} " + $"ChallengeStart={_challengeStart} " + $"SharedActive={_sharedCatchActive} " + $"IsPrimary={_isPrimaryCatchPlayer}");
			_currentFish.ServerChallengeSuccessRpc();
		}

		public void Challenge_Fail(bool isKnocked = false, bool showTutorial = false)
		{
			if (!_challengeStart)
			{
				return;
			}
			UtilityMethods.Log("[HarpoonTool_Challange] Challenge_Fail");
			NotifyCurrentFishChallengeEnded();
			_challengeStart = false;
			if (_currentPullDropTarget != null)
			{
				_currentPullDropTarget.CancelHook(this);
				_currentPullDropTarget = null;
			}
			else if (_isPrimaryCatchPlayer)
			{
				if (_currentFish != null)
				{
					_currentFish.ServerChallengeFailRpc();
				}
			}
			else
			{
				Challenge_LeaveAssistSessionIfNeeded();
			}
			Challenge_End();
			StartReload();
			if (showTutorial)
			{
				TryOpenTutorialForFirstFailure();
			}
			Vector3 position = ((_player != null) ? _player.transform.position : base.transform.position);
			Transform parent = ((_player != null) ? _player.transform : base.transform);
			Singleton<SoundManager>.Instance.PlaySFX(SoundType.Harpoon_Rope_Fail, position, parent);
		}

		private void TryOpenTutorialForFirstFailure()
		{
			if (base.IsOwner && PlayerPrefs.GetInt("StationTutorial.Harpoon.Seen", 0) != 1 && !(_player == null) && !(Singleton<GameUIManager>.Instance == null) && !(Singleton<GameUIManager>.Instance.StationTutorialPopupUI == null))
			{
				PlayerPrefs.SetInt("StationTutorial.Harpoon.Seen", 1);
				PlayerPrefs.Save();
				Singleton<GameUIManager>.Instance.StationTutorialPopupUI.OpenAfterRepair(_tutorialData, _player);
			}
		}

		private void NotifyCurrentFishChallengeEnded()
		{
			if (!(_currentFish == null) && !(_player == null) && !(_player.NetworkObject == null) && _currentFish is SquidCreature squidCreature)
			{
				squidCreature.ServerEndSquidInkResponseRpc(_player.NetworkObject.ToReference());
			}
		}

		private void Challenge_End()
		{
			UtilityMethods.Log("[HarpoonTool_Challange] Challenge_End");
			ResetChallengeCancelHold();
			Challenge_StopTensionSfx();
			Challenge_StopBubbleLoopFeedback();
			if ((bool)_uiPanel)
			{
				_uiPanel.SetActive(value: false);
			}
			if ((bool)_uiBarFill)
			{
				_uiBarFill.fillAmount = 0f;
			}
			HideChallengeHoldUI();
			Challenge_ClearSuccessZones();
			SetAssistFeedbackStatus(status: false);
			Challenge_ClearAssistPins();
			Challenge_StopSpamPromptFeedback(instant: true);
			if (Singleton<PPG_WheelManager>.Instance != null)
			{
				Singleton<PPG_WheelManager>.Instance.DisableToolWheel = false;
				Singleton<PPG_WheelManager>.Instance.DisableEmoteWheel = false;
			}
			_player.StopFishingCameraShake();
			if (_player.Sway != null)
			{
				_player.Sway.StopScriptedSway();
			}
			_player.ExitHarpoonReel();
			if (!_player.IsTrapped)
			{
				_player.ResumeCharacter();
			}
			_challengeStart = false;
			ResetFpsTipGraphicRotation();
			ResetChallengeFishPull();
		}

		private void Challenge_StartSpamPromptFeedback()
		{
			if (base.IsOwner)
			{
				if (_spamPromptFeedback == null)
				{
					UtilityMethods.Log("[HarpoonTool_Challange] Spam prompt feedback is not assigned.", LogType.Warning);
					return;
				}
				RectTransform preferredRoot = ((_uiPanel != null) ? (_uiPanel.transform as RectTransform) : null);
				_spamPromptFeedback.Play(preferredRoot);
			}
		}

		private void Challenge_StopSpamPromptFeedback(bool instant)
		{
			if (!(_spamPromptFeedback == null))
			{
				_spamPromptFeedback.Stop(instant);
			}
		}

		public void StartSharkDragPunchPrompt()
		{
			if (base.IsOwner && !(_spamPromptFeedback == null))
			{
				RectTransform preferredRoot = ((_uiPanel != null) ? (_uiPanel.transform as RectTransform) : null);
				_spamPromptFeedback.Play(preferredRoot, "gameplay.shark.drag.punch", "PUNCH", new Color(1f, 0.12f, 0.08f, 0.9f), new Color(0.16f, 0.01f, 0.01f, 0.85f));
			}
		}

		public void StopSharkDragPunchPrompt(bool instant = true)
		{
			if (base.IsOwner && !(_spamPromptFeedback == null))
			{
				_spamPromptFeedback.Stop(instant);
			}
		}

		private void Challenge_StopBubbleLoopFeedback()
		{
			Singleton<SoundManager>.Instance?.ReleaseOwnedLoop(_challengeBubbleLoopHandle);
			_challengeBubbleLoopHandle = null;
			_challengeBubbleLoopSFX = null;
			_bubbleActiveZoneIndex = -1;
		}

		private bool Challenge_IsBubbleLoopPlaybackActive()
		{
			if (_challengeBubbleLoopHandle != null && _challengeBubbleLoopHandle.IsActive)
			{
				return _challengeBubbleLoopSFX != null;
			}
			return false;
		}

		private void Challenge_StopTensionSfx()
		{
			if (_harpoonTensionLoop != null)
			{
				Singleton<SoundManager>.Instance?.ReleaseOwnedLoop(_harpoonTensionLoop);
				_harpoonTensionLoop = null;
			}
		}

		private void CancelChallengeByInput()
		{
			if (!base.IsOwner || !IsChallenge || !_challengeStart)
			{
				return;
			}
			UtilityMethods.Log("[HarpoonTool_Challange] Challenge cancelled by Mouse2 hold.");
			NotifyCurrentFishChallengeEnded();
			_challengeStart = false;
			if (_currentPullDropTarget != null)
			{
				_currentPullDropTarget.CancelHook(this);
				_currentPullDropTarget = null;
			}
			else if (_currentFish != null)
			{
				if (_isPrimaryCatchPlayer)
				{
					_currentFish.ServerChallengeCancelRpc();
				}
				else
				{
					Challenge_LeaveAssistSessionIfNeeded();
				}
			}
			StartReload();
			Challenge_End();
			_currentFish = null;
			_currentPullDropTarget = null;
		}

		private string BuildSuccessZoneDebugText()
		{
			if (_successZones.Count <= 0)
			{
				return "-";
			}
			string text = string.Empty;
			for (int i = 0; i < _successZones.Count; i++)
			{
				if (i > 0)
				{
					text += " | ";
				}
				HarpoonSuccessZoneRuntime harpoonSuccessZoneRuntime = _successZones[i];
				string text2 = (harpoonSuccessZoneRuntime.Completed ? "Done" : $"{harpoonSuccessZoneRuntime.GetProgress01(_zoneHoldTimeRequired):P0}");
				text += $"{i + 1}:{harpoonSuccessZoneRuntime.Min:0}-{harpoonSuccessZoneRuntime.Max:0}({text2})";
			}
			return text;
		}

		private void StartAssistFishChallenge(HarpoonSuccessZoneNetData zoneNetData, byte completedZoneMask, ulong primaryClientId)
		{
			if (_currentFish == null || _currentFish.FishSO == null)
			{
				StartReload();
				return;
			}
			PrepareOwnerForFishChallenge();
			if (Singleton<PPG_WheelManager>.Instance != null)
			{
				Singleton<PPG_WheelManager>.Instance.DisableToolWheel = true;
				Singleton<PPG_WheelManager>.Instance.DisableEmoteWheel = true;
			}
			_sharedCatchActive = true;
			_isPrimaryCatchPlayer = false;
			_isAssistCatchFinisher = false;
			_sharedCatchPrimaryClientId = primaryClientId;
			_activeSuccessZoneIndex = -1;
			HarpoonChallengePreset preset = _data.GetPreset(_currentFish.FishSO.FishingMode, _currentFish.FishSO.Rarity);
			Challenge_InitializeFishTimer(in preset);
			if (!Challenge_IsPresetValid(in preset))
			{
				UtilityMethods.Log("[HarpoonTool_Assist] Invalid assist challenge preset.", LogType.Error);
				Challenge_EndAsAssist();
				return;
			}
			if (!Challenge_SetupFromSyncedZones(in preset, zoneNetData))
			{
				UtilityMethods.Log("[HarpoonTool_Assist] Synced success zone data is invalid.", LogType.Warning);
				Challenge_EndAsAssist();
				return;
			}
			Challenge_ApplyCompletedZoneMask(completedZoneMask);
			CacheChallengeFishPull();
			_challengeStart = true;
			SetAssistFeedbackStatus(status: true);
		}

		private void PrepareOwnerForFishChallenge()
		{
			if (base.IsOwner && !(_player?.FirstPersonCharacter == null) && !(_currentFish == null))
			{
				FirstPersonCharacter firstPersonCharacter = _player.FirstPersonCharacter;
				if (firstPersonCharacter.characterMovement != null)
				{
					firstPersonCharacter.characterMovement.velocity = Vector3.zero;
				}
				firstPersonCharacter.LookAtInstant(_currentFish.transform.position);
			}
		}

		private void StartAssistPullDropTargetChallenge(HarpoonSuccessZoneNetData zoneNetData, byte completedZoneMask, ulong primaryClientId)
		{
			if (_currentPullDropTarget == null)
			{
				StartReload();
				return;
			}
			if (Singleton<PPG_WheelManager>.Instance != null)
			{
				Singleton<PPG_WheelManager>.Instance.DisableToolWheel = true;
				Singleton<PPG_WheelManager>.Instance.DisableEmoteWheel = true;
			}
			_sharedCatchActive = true;
			_isPrimaryCatchPlayer = false;
			_isAssistCatchFinisher = false;
			_sharedCatchPrimaryClientId = primaryClientId;
			Challenge_InitializeDefaultTimer();
			_activeSuccessZoneIndex = -1;
			HarpoonChallengePreset preset = _data.GetPreset(_currentPullDropTarget.ChallengeFishingMode, _currentPullDropTarget.ChallengeRarity);
			if (!Challenge_IsPresetValid(in preset))
			{
				UtilityMethods.Log("[HarpoonTool_TargetAssist] Invalid assist challenge preset.", LogType.Error);
				Challenge_End();
				StartReload();
			}
			else if (!Challenge_SetupFromSyncedZones(in preset, zoneNetData, useStalactiteHoldUI: true))
			{
				UtilityMethods.Log("[HarpoonTool_TargetAssist] Synced success zone data is invalid.", LogType.Warning);
				Challenge_End();
				StartReload();
			}
			else
			{
				Challenge_ApplyCompletedZoneMask(completedZoneMask);
				_challengeStart = true;
				SetAssistFeedbackStatus(status: true);
			}
		}

		private void Challenge_ApplyCompletedZoneMask(byte completedZoneMask)
		{
			if (_successZones.Count <= 0)
			{
				return;
			}
			for (int i = 0; i < _successZones.Count; i++)
			{
				if ((completedZoneMask & (1 << i)) != 0)
				{
					HarpoonSuccessZoneRuntime harpoonSuccessZoneRuntime = _successZones[i];
					harpoonSuccessZoneRuntime.Completed = true;
					harpoonSuccessZoneRuntime.HoldTimer = _zoneHoldTimeRequired;
					Challenge_SetSuccessZoneVisualStatus(i, status: false);
				}
			}
			Challenge_UpdateUI();
		}

		[Rpc(SendTo.Server)]
		private void ServerShootVisualsToRpc(Vector3 hookedPoint, float shootSpeed)
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
				FastBufferWriter bufferWriter = __beginSendRpc(395073671u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in hookedPoint);
				bufferWriter.WriteValueSafe(in shootSpeed, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 395073671u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsSpawned)
				{
					ClientShootVisualsRpc(hookedPoint, shootSpeed);
				}
			}
		}

		[Rpc(SendTo.NotOwner)]
		private void ClientShootVisualsRpc(Vector3 hookedPoint, float shootSpeed)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3877412408u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in hookedPoint);
				bufferWriter.WriteValueSafe(in shootSpeed, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3877412408u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsOwner && base.ToolStatus && !(_grapple == null) && !(_hookedPoint == null))
			{
				_hookedPoint.position = hookedPoint;
				_grapple.StartForward(shootSpeed);
				if ((bool)_tpsSpearTransform)
				{
					_tpsSpearTransform.gameObject.SetActive(value: false);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerStartReloadVisualsToRpc(bool instant)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3410618994u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in instant, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3410618994u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsSpawned)
				{
					ClientStartReloadVisualsRpc(instant);
				}
			}
		}

		[Rpc(SendTo.NotOwner)]
		private void ClientStartReloadVisualsRpc(bool instant)
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
				FastBufferWriter bufferWriter = __beginSendRpc(862990163u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in instant, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 862990163u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!base.IsOwner && !(_grapple == null))
				{
					_grapple.StartRetract(instant);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerHardStopGrappleToRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3280889285u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3280889285u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsSpawned)
				{
					ClientHardStopGrappleRpc();
				}
			}
		}

		[Rpc(SendTo.NotOwner)]
		private void ClientHardStopGrappleRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(241023933u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 241023933u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!(_grapple == null))
			{
				if (_hookedPoint == null)
				{
					_hookedPoint = new GameObject("HookPoint").transform;
				}
				_hookedPoint.SetParent(null);
				if ((bool)_tpsSpearTransform)
				{
					_tpsSpearTransform.gameObject.SetActive(value: true);
				}
				_grapple.HardStop();
				ClearStuck(ref _tpsStuckTipInstance);
			}
		}

		public void ServerStopSuccessfulCatchVisuals()
		{
			if (base.IsServer && base.IsSpawned)
			{
				ClientStopSuccessfulCatchVisualsRpc();
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientStopSuccessfulCatchVisualsRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(552906656u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 552906656u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClearStuck(ref _fpsStuckTipInstance);
				ClearStuck(ref _tpsStuckTipInstance);
				if (_hookedPoint == null)
				{
					_hookedPoint = new GameObject("HookPoint").transform;
				}
				_hookedPoint.SetParent(null);
				if (!(_grapple == null))
				{
					_grapple.ClearVisualTarget();
					_grapple.HardStop();
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerResetHarpoonToRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(500683969u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 500683969u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsSpawned)
				{
					ServerResetHarpoonRpc();
				}
			}
		}

		[Rpc(SendTo.NotOwner)]
		private void ServerResetHarpoonRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3703591482u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3703591482u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_currentFish = null;
				_currentPullDropTarget = null;
				ClearStuck(ref _tpsStuckTipInstance);
				if (_hookedPoint == null)
				{
					_hookedPoint = new GameObject("HookPoint").transform;
				}
				_hookedPoint.SetParent(null);
				_grapple.HardStop();
				if ((bool)_tpsSpearTransform)
				{
					_tpsSpearTransform.gameObject.SetActive(value: true);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerTipHitEnvironmentToRpc(Vector3 hitPoint, Vector3 hitNormal)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2611128831u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in hitPoint);
				bufferWriter.WriteValueSafe(in hitNormal);
				__endSendRpc(ref bufferWriter, 2611128831u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsSpawned)
				{
					ClientTipHitEnvironmentRpc(hitPoint, hitNormal);
				}
			}
		}

		[Rpc(SendTo.NotOwner)]
		private void ClientTipHitEnvironmentRpc(Vector3 hitPoint, Vector3 hitNormal)
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
				FastBufferWriter bufferWriter = __beginSendRpc(4294135301u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in hitPoint);
				bufferWriter.WriteValueSafe(in hitNormal);
				__endSendRpc(ref bufferWriter, 4294135301u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!base.IsOwner && !(_hookedPoint == null))
				{
					_hookedPoint.SetParent(null);
					_hookedPoint.SetPositionAndRotation(hitPoint, Quaternion.LookRotation(hitNormal));
					PlaceStuck(_tpsTipGraphic, ref _tpsStuckTipInstance, hitPoint, hitNormal, "ClientPlayer");
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerTipHitPlayerToRpc(NetworkObjectReference playerRef, Vector3 hitPoint, Vector3 hitNormal)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3491376826u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in playerRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in hitPoint);
				bufferWriter.WriteValueSafe(in hitNormal);
				__endSendRpc(ref bufferWriter, 3491376826u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsSpawned)
				{
					Singleton<GameProgressManager>.Instance?.RpcRelay?.ServerUnlockSteamAchievementForClient("ACH_HARPOONED_FRIEND", base.OwnerClientId);
					ClientTipHitPlayerRpc(playerRef, hitPoint, hitNormal);
				}
			}
		}

		[Rpc(SendTo.NotOwner)]
		private void ClientTipHitPlayerRpc(NetworkObjectReference playerRef, Vector3 hitPoint, Vector3 hitNormal)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2620572696u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in playerRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in hitPoint);
				bufferWriter.WriteValueSafe(in hitNormal);
				__endSendRpc(ref bufferWriter, 2620572696u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!base.IsOwner && playerRef.TryGet(out var networkObject) && !(_hookedPoint == null))
				{
					_hookedPoint.SetParent(networkObject.transform, worldPositionStays: true);
					_hookedPoint.SetPositionAndRotation(hitPoint, Quaternion.LookRotation(hitNormal));
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestHookCreatureRpc(NetworkObjectReference creatureReference, NetworkObjectReference playerReference)
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
				FastBufferWriter bufferWriter = __beginSendRpc(4266322301u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in creatureReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in playerReference, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 4266322301u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			CatchableSeaCreature component;
			PPG_Player component2;
			if (!creatureReference.TryGet(out var networkObject))
			{
				UtilityMethods.Log("[HarpoonTool] ServerRequestHookCreatureRpc failed. Creature reference could not resolve.", LogType.Warning);
				RejectCreatureHookForOwner();
			}
			else if (!networkObject.TryGetComponent<CatchableSeaCreature>(out component))
			{
				UtilityMethods.Log("[HarpoonTool] ServerRequestHookCreatureRpc failed. Target is not CatchableSeaCreature.", LogType.Warning);
				RejectCreatureHookForOwner();
			}
			else if (!playerReference.TryGetComponent<PPG_Player>(out component2))
			{
				UtilityMethods.Log("[HarpoonTool] ServerRequestHookCreatureRpc failed. Player reference could not resolve.", LogType.Warning);
				RejectCreatureHookForOwner();
			}
			else if (component2 != _player)
			{
				UtilityMethods.Log("[HarpoonTool] ServerRequestHookCreatureRpc failed. Player mismatch.", LogType.Warning);
				RejectCreatureHookForOwner();
			}
			else if (component.AssistCatchActive)
			{
				if (!component.ServerTryGetAssistSuccessZoneNetData(out var zoneNetData))
				{
					UtilityMethods.Log("[HarpoonTool_Assist] Assist join rejected. Success zone net data is not ready.", LogType.Warning);
					ClientRejectCreatureHookRpc(base.RpcTarget.Single(component2.OwnerClientId, RpcTargetUse.Temp));
					return;
				}
				if (!component.ServerTryJoinAssistCatchSession(component2))
				{
					ClientRejectCreatureHookRpc(base.RpcTarget.Single(component2.OwnerClientId, RpcTargetUse.Temp));
					return;
				}
				_currentFish = component;
				_fishChallengeStartWorldPosition = component.transform.position;
				_hasCachedFishChallengeStartWorldPosition = true;
				_currentFishPullProgress = component.ServerGetAssistGlobalProgress01();
				_fishPullServerSyncTimer = 0f;
				ClientStartAssistCreatureChallengeRpc(component.NetworkObject.ToReference(), zoneNetData, component.ServerGetCompletedAssistZoneMask(), component.AssistPrimaryClientId);
				ClientStartAssistCreatureVisualRpc(component.NetworkObject.ToReference());
				UtilityMethods.Log("[HarpoonTool_Assist] Assist challenge start sent. " + $"ClientId={component2.OwnerClientId} PrimaryClientId={component.AssistPrimaryClientId} " + $"ZoneCount={zoneNetData.Count}");
			}
			else if (!component.ServerTryReceiveHit(component2))
			{
				ClientRejectCreatureHookRpc(base.RpcTarget.Single(component2.OwnerClientId, RpcTargetUse.Temp));
			}
			else if (!component.IsHooked.Value)
			{
				UtilityMethods.Log("[HarpoonTool] ServerRequestHookCreatureRpc failed. Creature accepted hit but was not hooked.", LogType.Warning);
				ClientRejectCreatureHookRpc(base.RpcTarget.Single(component2.OwnerClientId, RpcTargetUse.Temp));
			}
			else
			{
				int challengeZoneCountForCreature = GetChallengeZoneCountForCreature(component);
				component.ServerBeginAssistCatchSession(component2, challengeZoneCountForCreature);
				_currentFish = component;
				_fishChallengeStartWorldPosition = component.transform.position;
				_hasCachedFishChallengeStartWorldPosition = true;
				_currentFishPullProgress = 0f;
				_fishPullServerSyncTimer = 0f;
				UtilityMethods.Log("[HarpoonTool_Assist] Primary catch session started. " + $"ClientId={component2.OwnerClientId} PrimaryClientId={component.AssistPrimaryClientId} ZoneCount={challengeZoneCountForCreature}");
				ClientStartCreatureChallengeRpc(component.NetworkObject.ToReference(), challengeZoneCountForCreature);
			}
		}

		private void RejectCreatureHookForOwner()
		{
			ClientRejectCreatureHookRpc(base.RpcTarget.Single(base.OwnerClientId, RpcTargetUse.Temp));
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestHitHostileCreatureRpc(NetworkObjectReference creatureReference, NetworkObjectReference playerReference, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3902902839u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in creatureReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in playerReference, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3902902839u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (creatureReference.TryGet(out var networkObject) && playerReference.TryGet(out var networkObject2) && networkObject.TryGetComponent<HostileSeaCreature>(out var component) && networkObject2.TryGetComponent<PPG_Player>(out var component2))
			{
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				if (senderClientId != base.OwnerClientId || component2.OwnerClientId != senderClientId || component2 != _player)
				{
					UtilityMethods.Log($"[HarpoonTool] Hostile hit rejected. Sender={senderClientId} ToolOwner={base.OwnerClientId} PlayerOwner={component2.OwnerClientId}", LogType.Warning);
				}
				else
				{
					component.ServerReceiveHarpoonHit(component2, GetHostileHitDamage());
				}
			}
		}

		internal byte GetHostileHitDamage()
		{
			if (_player == null)
			{
				return 1;
			}
			byte equippedHarpoonLevel = _player.EquippedHarpoonLevel;
			byte harpoonLevel = _player.ToolsData.HarpoonLevel;
			int effectiveLevel = Mathf.Min(equippedHarpoonLevel, harpoonLevel);
			HarpoonLevelData harpoonLevelData = ((_data != null && _data.Levels != null) ? _data.Levels.Find((HarpoonLevelData entry) => entry != null && entry.Level == effectiveLevel) : null);
			if (!(harpoonLevelData != null))
			{
				return 1;
			}
			return (byte)Mathf.Max(1, harpoonLevelData.HostileDamage);
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestHitJellyfishTrapRpc(NetworkObjectReference jellyfishReference, NetworkObjectReference playerReference)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2284852911u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in jellyfishReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in playerReference, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 2284852911u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (jellyfishReference.TryGet(out var networkObject) && playerReference.TryGet(out var networkObject2) && networkObject.TryGetComponent<JellyfishTrap>(out var component) && networkObject2.TryGetComponent<PPG_Player>(out var component2))
				{
					component.ServerReceiveHarpoonHit(component2);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestHitOctopusCreatureRpc(NetworkObjectReference creatureReference, NetworkObjectReference playerReference)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3278989640u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in creatureReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in playerReference, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3278989640u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (creatureReference.TryGet(out var networkObject) && playerReference.TryGet(out var networkObject2) && networkObject.TryGetComponent<OctopusCreature>(out var component) && networkObject2.TryGetComponent<PPG_Player>(out var component2))
				{
					component.ServerReceiveOctopusHarpoonHit(component2);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestHitCrabBossRpc(NetworkObjectReference bossReference, NetworkObjectReference playerReference, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3400116543u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in bossReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in playerReference, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3400116543u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!bossReference.TryGetComponent<CrabBossController>(out var component))
			{
				UtilityMethods.Log("[HarpoonTool] Boss hit rejected. CrabBossController could not be resolved.", LogType.Warning);
				return;
			}
			if (!playerReference.TryGetComponent<PPG_Player>(out var component2))
			{
				UtilityMethods.Log("[HarpoonTool] Boss hit rejected. Player could not be resolved.", LogType.Warning);
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (base.OwnerClientId != senderClientId || component2.OwnerClientId != senderClientId || component2 != _player)
			{
				UtilityMethods.Log("[HarpoonTool] Boss hit rejected. " + $"Sender={senderClientId} ToolOwner={base.OwnerClientId} PlayerOwner={component2.OwnerClientId}.", LogType.Warning);
				return;
			}
			float currentHealth = component.CurrentHealth;
			float currentShieldHealth = component.CurrentShieldHealth;
			if (!component.ServerApplyDamage(CrabBossDamageType.Harpoon, 1f, component2))
			{
				UtilityMethods.Log("[HarpoonTool] Boss hit reached the server but damage was rejected. " + $"State={component.EncounterState} Phase={component.CurrentPhase} " + $"Health={component.CurrentHealth:0.###}/{component.MaximumHealth:0.###} " + $"Shield={component.CurrentShieldHealth:0.###}/{component.MaximumShieldHealth:0.###}.", LogType.Warning);
				return;
			}
			UtilityMethods.Log($"[HarpoonTool] Boss hit applied. State={component.EncounterState} " + $"Phase={component.CurrentPhase} " + $"BodyDelta={currentHealth - component.CurrentHealth:0.###} " + $"ShieldDelta={currentShieldHealth - component.CurrentShieldHealth:0.###} " + $"Health={component.CurrentHealth:0.###}/{component.MaximumHealth:0.###} " + $"Shield={component.CurrentShieldHealth:0.###}/{component.MaximumShieldHealth:0.###}.");
		}

		[Rpc(SendTo.Everyone)]
		private void ClientStartCreatureChallengeRpc(NetworkObjectReference creatureReference, int zoneCount)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2030621906u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in creatureReference, default(FastBufferWriter.ForNetworkSerializable));
				BytePacker.WriteValueBitPacked(bufferWriter, zoneCount);
				__endSendRpc(ref bufferWriter, 2030621906u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			CatchableSeaCreature component;
			if (!creatureReference.TryGet(out var networkObject))
			{
				UtilityMethods.Log("[HarpoonTool] ClientStartCreatureChallengeRpc failed. Creature reference could not resolve.", LogType.Warning);
				if (base.IsOwner)
				{
					StartReload();
				}
			}
			else if (!networkObject.TryGetComponent<CatchableSeaCreature>(out component))
			{
				UtilityMethods.Log("[HarpoonTool] ClientStartCreatureChallengeRpc failed. Target is not CatchableSeaCreature.", LogType.Warning);
				if (base.IsOwner)
				{
					StartReload();
				}
			}
			else if (!base.IsOwner)
			{
				AttachCreatureHookVisual(component);
			}
			else
			{
				BeginCreatureChallenge(component, zoneCount);
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientRejectCreatureHookRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3319068633u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3319068633u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsOwner)
				{
					StartReload();
				}
			}
		}

		private void BeginCreatureChallenge(CatchableSeaCreature creature, int zoneCount)
		{
			if (creature == null)
			{
				StartReload();
				return;
			}
			_currentFish = creature;
			_fishChallengeStartWorldPosition = creature.transform.position;
			_hasCachedFishChallengeStartWorldPosition = true;
			_currentFishPullProgress = 0f;
			if (_hookedPoint == null)
			{
				_hookedPoint = new GameObject("HookPoint").transform;
			}
			creature.ClientHooked(_player, _hookedPoint);
			SetState(HarpoonState.Challenge);
			StartFishChallenge(zoneCount);
		}

		[Rpc(SendTo.SpecifiedInParams)]
		public void ClientChallangeSuccessRpc(NetworkObjectReference creatureReference, ulong catchOwnerClientId, float weight, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3059836996u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in creatureReference, default(FastBufferWriter.ForNetworkSerializable));
				BytePacker.WriteValueBitPacked(bufferWriter, catchOwnerClientId);
				bufferWriter.WriteValueSafe(in weight, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3059836996u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsOwner)
			{
				return;
			}
			if (base.OwnerClientId != catchOwnerClientId)
			{
				UtilityMethods.Log("[HarpoonTool_Network] ClientChallangeSuccessRpc ignored. " + $"ThisOwner={base.OwnerClientId} CatchOwner={catchOwnerClientId}");
				return;
			}
			CatchableSeaCreature catchableSeaCreature = null;
			FishSO fishSO = null;
			if (creatureReference.TryGet(out var networkObject) && networkObject.TryGetComponent<CatchableSeaCreature>(out var component))
			{
				catchableSeaCreature = (_currentFish = component);
				if (fishSO == null)
				{
					fishSO = catchableSeaCreature.FishSO;
				}
			}
			try
			{
				if (fishSO == null)
				{
					UtilityMethods.Log("[HarpoonTool_Network] ClientChallangeSuccessRpc failed. FishSO could not resolve.", LogType.Warning);
					EndSuccessfulCatchWithReload();
					ServerFinalizeCreatureChallengeSuccessRpc(creatureReference);
					return;
				}
				try
				{
					ServiceLocator.ForSceneOf(_player).Get<TabletNetworkHandler>(out var service);
					if (service != null)
					{
						service.RegisterCatch(fishSO.Id, weight);
					}
				}
				catch (Exception ex)
				{
					UtilityMethods.Log("[HarpoonTool_Network] RegisterCatch skipped: " + ex.Message, LogType.Warning);
				}
				ShowCatchInfoLocal(fishSO, weight);
				EndSuccessfulCatchWithReload();
				if (Singleton<SoundManager>.Instance != null)
				{
					Singleton<SoundManager>.Instance.PlaySFX(SoundType.Harpoon_Rope_Success);
				}
				ServerFinalizeCreatureChallengeSuccessRpc(creatureReference);
			}
			catch (Exception ex2)
			{
				UtilityMethods.Log("[HarpoonTool_Network] ClientChallangeSuccessRpc failed: " + ex2.Message, LogType.Warning);
				EndSuccessfulCatchWithReload();
				ServerFinalizeCreatureChallengeSuccessRpc(creatureReference);
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		public void ClientShowCatchInfoRpc(FixedString64Bytes fishId, float weight, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1501152810u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in fishId, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in weight, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1501152810u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsOwner)
			{
				FishSO fishSO = ResolveCaughtFishSO(fishId);
				if (fishSO == null)
				{
					UtilityMethods.Log($"[HarpoonTool_Network] ClientShowCatchInfoRpc failed. FishSO could not resolve. FishId={fishId}", LogType.Warning);
				}
				else
				{
					ShowCatchInfoLocal(fishSO, weight);
				}
			}
		}

		private FishSO ResolveCaughtFishSO(FixedString64Bytes fishId)
		{
			string text = fishId.ToString();
			if (string.IsNullOrWhiteSpace(text))
			{
				return null;
			}
			ItemDatabaseSO itemDatabaseSO = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.ItemDatabaseSO : null);
			if (!(itemDatabaseSO != null))
			{
				return null;
			}
			return itemDatabaseSO.FindById(text) as FishSO;
		}

		private void ShowCatchInfoLocal(FishSO fishSO, float weight)
		{
			if (fishSO == null)
			{
				return;
			}
			if (_catchInfoUI == null)
			{
				UtilityMethods.Log("[HarpoonTool_Network] CatchInfoUI is not assigned.", LogType.Warning);
				return;
			}
			string id = fishSO.Id;
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (!string.Equals(_lastCatchInfoFishId, id, StringComparison.Ordinal) || !Mathf.Approximately(_lastCatchInfoWeight, weight) || !(realtimeSinceStartup - _lastCatchInfoTime < 0.5f))
			{
				_lastCatchInfoFishId = id;
				_lastCatchInfoWeight = weight;
				_lastCatchInfoTime = realtimeSinceStartup;
				_catchInfoUI.UpdateCatchInfo(fishSO, weight);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerFinalizeCreatureChallengeSuccessRpc(NetworkObjectReference creatureReference)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2099277671u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in creatureReference, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 2099277671u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				CatchableSeaCreature component;
				if (!creatureReference.TryGet(out var networkObject))
				{
					UtilityMethods.Log("[HarpoonTool_Network] Finalize failed. Creature reference could not resolve.", LogType.Warning);
				}
				else if (!networkObject.TryGetComponent<CatchableSeaCreature>(out component))
				{
					UtilityMethods.Log("[HarpoonTool_Network] Finalize failed. Target is not CatchableSeaCreature.", LogType.Warning);
				}
				else if (!component.ServerCanFinalizeChallengeSuccess(base.OwnerClientId))
				{
					UtilityMethods.Log("[HarpoonTool_Network] Finalize rejected. " + $"HarpoonOwner={base.OwnerClientId} is not the pending catch owner.", LogType.Warning);
				}
				else
				{
					component.ServerFinalizeChallengeSuccess();
				}
			}
		}

		private void EndSuccessfulCatchWithReload()
		{
			Challenge_End();
			StartReload();
		}

		[Rpc(SendTo.Server)]
		private void ServerTipHitDestructibleToRpc(byte id, Vector3 hitPoint, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(4085371659u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in id, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in hitPoint);
				__endSendRpc(ref bufferWriter, 4085371659u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsSpawned)
			{
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				if (senderClientId != base.OwnerClientId)
				{
					UtilityMethods.Log($"[HarpoonTool] Destructible hit rejected. Sender={senderClientId} ToolOwner={base.OwnerClientId}.", LogType.Warning);
				}
				else if (!DestructibleManager.HasInstance || !Singleton<DestructibleManager>.Instance.TryApplyDamageById(id, hitPoint))
				{
					UtilityMethods.Log($"[HarpoonTool] Destructible hit rejected. Target Id={id} is not registered.", LogType.Warning);
				}
				else
				{
					ClientTipHitDestructibleRpc(id, hitPoint);
				}
			}
		}

		[Rpc(SendTo.NotServer)]
		private void ClientTipHitDestructibleRpc(byte id, Vector3 hitPoint)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2283807490u, rpcParams, attributeParams, SendTo.NotServer, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in id, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in hitPoint);
				__endSendRpc(ref bufferWriter, 2283807490u, rpcParams, attributeParams, SendTo.NotServer, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (DestructibleManager.HasInstance)
				{
					Singleton<DestructibleManager>.Instance.ApplyDamageById(id, hitPoint);
				}
			}
		}

		[Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
		private void ServerUpdateChallengeFishPullRpc(NetworkObjectReference creatureReference, float progress)
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
					Delivery = RpcDelivery.Unreliable
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1077796824u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Unreliable);
				bufferWriter.WriteValueSafe(in creatureReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in progress, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1077796824u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Unreliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (creatureReference.TryGet(out var networkObject) && networkObject.TryGetComponent<CatchableSeaCreature>(out var component) && component.IsHooked.Value && component.HookOwnerClientId == base.OwnerClientId)
			{
				if (_currentFish != component)
				{
					_currentFish = component;
				}
				if (!_hasCachedFishChallengeStartWorldPosition)
				{
					_fishChallengeStartWorldPosition = component.transform.position;
					_hasCachedFishChallengeStartWorldPosition = true;
				}
				_currentFishPullProgress = Mathf.Clamp01(progress);
				ApplyChallengeFishPullToCreature(component, _fishChallengeStartWorldPosition, _currentFishPullProgress);
			}
		}

		private int GetChallengeZoneCountForCreature(CatchableSeaCreature creature)
		{
			if (creature == null || creature.FishSO == null || _data == null)
			{
				return 1;
			}
			return Mathf.Clamp(_data.GetPreset(creature.FishSO.FishingMode, creature.FishSO.Rarity).GetRandomSuccessZoneCount(), 1, Mathf.Max(1, _maxSuccessZoneCount));
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestPullDropTargetChallengeRpc(NetworkObjectReference targetReference, NetworkObjectReference playerReference, Vector3 hitPoint, Vector3 hitNormal)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1950513083u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in targetReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in playerReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in hitPoint);
				bufferWriter.WriteValueSafe(in hitNormal);
				__endSendRpc(ref bufferWriter, 1950513083u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			HarpoonPullDropTarget component;
			PPG_Player component2;
			if (!targetReference.TryGet(out var networkObject))
			{
				ClientRejectPullDropTargetHookRpc(base.RpcTarget.Single(base.OwnerClientId, RpcTargetUse.Temp));
			}
			else if (!networkObject.TryGetComponent<HarpoonPullDropTarget>(out component))
			{
				ClientRejectPullDropTargetHookRpc(base.RpcTarget.Single(base.OwnerClientId, RpcTargetUse.Temp));
			}
			else if (!playerReference.TryGetComponent<PPG_Player>(out component2))
			{
				ClientRejectPullDropTargetHookRpc(base.RpcTarget.Single(base.OwnerClientId, RpcTargetUse.Temp));
			}
			else if (component2 != _player)
			{
				ClientRejectPullDropTargetHookRpc(base.RpcTarget.Single(base.OwnerClientId, RpcTargetUse.Temp));
			}
			else if (component.PullSessionActive)
			{
				if (!component.ServerTryGetPullSuccessZoneNetData(out var zoneNetData))
				{
					ClientRejectPullDropTargetHookRpc(base.RpcTarget.Single(component2.OwnerClientId, RpcTargetUse.Temp));
					return;
				}
				if (!component.ServerTryJoinPullSession(this))
				{
					ClientRejectPullDropTargetHookRpc(base.RpcTarget.Single(component2.OwnerClientId, RpcTargetUse.Temp));
					return;
				}
				_currentPullDropTarget = component;
				ClientStartAssistPullDropTargetChallengeRpc(component.NetworkObject.ToReference(), zoneNetData, component.ServerGetCompletedPullZoneMask(), component.PullPrimaryClientId, base.RpcTarget.Single(component2.OwnerClientId, RpcTargetUse.Temp));
				ClientStartPullDropTargetVisualRpc(component.NetworkObject.ToReference());
			}
			else
			{
				int challengeZoneCountForPullDropTarget = GetChallengeZoneCountForPullDropTarget(component);
				if (!component.ServerBeginPullSession(this, challengeZoneCountForPullDropTarget))
				{
					ClientRejectPullDropTargetHookRpc(base.RpcTarget.Single(component2.OwnerClientId, RpcTargetUse.Temp));
					return;
				}
				_currentPullDropTarget = component;
				ClientStartPullDropTargetChallengeRpc(component.NetworkObject.ToReference(), challengeZoneCountForPullDropTarget, base.RpcTarget.Single(component2.OwnerClientId, RpcTargetUse.Temp));
				ClientStartPullDropTargetVisualRpc(component.NetworkObject.ToReference());
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientStartPullDropTargetChallengeRpc(NetworkObjectReference targetReference, int zoneCount, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2326852655u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in targetReference, default(FastBufferWriter.ForNetworkSerializable));
				BytePacker.WriteValueBitPacked(bufferWriter, zoneCount);
				__endSendRpc(ref bufferWriter, 2326852655u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsOwner)
			{
				if (!targetReference.TryGet(out var networkObject) || !networkObject.TryGetComponent<HarpoonPullDropTarget>(out var component))
				{
					ServerCancelUnstartedPullDropTargetChallengeRpc(targetReference);
					StartReload();
				}
				else
				{
					BeginPullDropTargetChallenge(component, zoneCount);
					ServerRegisterPullDropTargetSuccessZoneNetDataRpc(component.NetworkObject.ToReference(), Challenge_BuildSuccessZoneNetData());
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerCancelUnstartedPullDropTargetChallengeRpc(NetworkObjectReference targetReference, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(396542152u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in targetReference, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 396542152u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (rpcParams.Receive.SenderClientId == base.OwnerClientId && targetReference.TryGetComponent<HarpoonPullDropTarget>(out var component) && component.PullSessionActive && component.PullPrimaryClientId == base.OwnerClientId)
				{
					component.CancelHook(this);
				}
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientStartAssistPullDropTargetChallengeRpc(NetworkObjectReference targetReference, HarpoonSuccessZoneNetData zoneNetData, byte completedZoneMask, ulong primaryClientId, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2539352796u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in targetReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in zoneNetData, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in completedZoneMask, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, primaryClientId);
				__endSendRpc(ref bufferWriter, 2539352796u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsOwner)
			{
				string rejectReason;
				if (!targetReference.TryGet(out var networkObject) || !networkObject.TryGetComponent<HarpoonPullDropTarget>(out var component))
				{
					RejectPullDropTargetChallengeStart(null, "Target reference could not resolve.");
				}
				else if (!CanStartAssistPullDropTargetChallengeFromRpc(component, zoneNetData, completedZoneMask, primaryClientId, out rejectReason))
				{
					RejectPullDropTargetChallengeStart(component, rejectReason);
				}
				else
				{
					BeginAssistPullDropTargetChallenge(component, zoneNetData, completedZoneMask, primaryClientId);
				}
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientRejectPullDropTargetHookRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1910288464u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1910288464u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsOwner)
				{
					StartReload();
				}
			}
		}

		private void BeginPullDropTargetChallenge(HarpoonPullDropTarget target, int zoneCount)
		{
			if (target == null)
			{
				StartReload();
				return;
			}
			AttachPullDropTargetHookLocal(target, useFpsVisual: true);
			SetState(HarpoonState.Challenge);
			StartPullDropTargetChallenge(zoneCount);
		}

		private void BeginAssistPullDropTargetChallenge(HarpoonPullDropTarget target, HarpoonSuccessZoneNetData zoneNetData, byte completedZoneMask, ulong primaryClientId)
		{
			if (target == null)
			{
				StartReload();
				return;
			}
			AttachPullDropTargetHookLocal(target, useFpsVisual: true);
			SetState(HarpoonState.Challenge);
			StartAssistPullDropTargetChallenge(zoneNetData, completedZoneMask, primaryClientId);
		}

		private void AttachPullDropTargetHookLocal(HarpoonPullDropTarget target, bool useFpsVisual)
		{
			if (!(target == null))
			{
				_currentPullDropTarget = target;
				if (_hookedPoint == null)
				{
					_hookedPoint = new GameObject("HookPoint").transform;
				}
				Transform hookPoint = target.HookPoint;
				_hookedPoint.SetParent(hookPoint, worldPositionStays: false);
				_hookedPoint.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
				string layerName = (useFpsVisual ? "ViewModel" : "ClientPlayer");
				Transform transform = (useFpsVisual ? PlaceStuck(_fpsTipGraphic, ref _fpsStuckTipInstance, hookPoint.position, hookPoint.forward, layerName) : PlaceStuck(_tpsTipGraphic, ref _tpsStuckTipInstance, hookPoint.position, hookPoint.forward, layerName));
				GameObject gameObject = (useFpsVisual ? _fpsStuckTipInstance : _tpsStuckTipInstance);
				if (gameObject != null)
				{
					gameObject.transform.SetParent(hookPoint, worldPositionStays: false);
					gameObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
				}
				if (_grapple != null && transform != null)
				{
					_grapple.SetVisualTarget(transform);
				}
			}
		}

		[Rpc(SendTo.NotOwner)]
		private void ClientStartPullDropTargetVisualRpc(NetworkObjectReference targetReference)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3672133852u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in targetReference, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3672133852u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!base.IsOwner && targetReference.TryGet(out var networkObject) && networkObject.TryGetComponent<HarpoonPullDropTarget>(out var component))
				{
					AttachPullDropTargetHookLocal(component, useFpsVisual: false);
				}
			}
		}

		private bool CanStartAssistPullDropTargetChallengeFromRpc(HarpoonPullDropTarget target, HarpoonSuccessZoneNetData zoneNetData, byte completedZoneMask, ulong primaryClientId, out string rejectReason)
		{
			rejectReason = string.Empty;
			if (target == null)
			{
				rejectReason = "Target is null.";
				return false;
			}
			if (!target.IsSpawned)
			{
				rejectReason = "Target is not spawned.";
				return false;
			}
			if (primaryClientId == base.OwnerClientId)
			{
				rejectReason = "Owner is primary client.";
				return false;
			}
			int num = Mathf.Clamp(zoneNetData.Count, 0, 8);
			if (num <= 0)
			{
				rejectReason = "Zone count is zero.";
				return false;
			}
			int num2 = (1 << num) - 1;
			if ((completedZoneMask & num2) == num2)
			{
				rejectReason = "All pull target zones are already completed.";
				return false;
			}
			return true;
		}

		private void RejectPullDropTargetChallengeStart(HarpoonPullDropTarget target, string reason)
		{
			UtilityMethods.Log("[HarpoonTool_TargetAssist] Challenge start rejected locally. Reason=" + reason, LogType.Warning);
			if (target != null && target.IsSpawned)
			{
				target.ServerLeavePullSessionRpc();
			}
			if (_challengeStart)
			{
				Challenge_End();
			}
			_currentPullDropTarget = null;
			if (IsReloading)
			{
				ReloadDone();
			}
			else if (!IsIdle)
			{
				StartReload(instant: true);
			}
			else
			{
				ForceResetVisualState(hardStopGrapple: true, restoreHeldSpearGraphic: true);
			}
		}

		private int GetChallengeZoneCountForPullDropTarget(HarpoonPullDropTarget target)
		{
			if (target == null)
			{
				return 1;
			}
			return Mathf.Clamp(target.SuccessZoneCount, 1, Mathf.Max(1, _maxSuccessZoneCount));
		}

		[Rpc(SendTo.Server)]
		private void ServerRegisterPullDropTargetSuccessZoneNetDataRpc(NetworkObjectReference targetReference, HarpoonSuccessZoneNetData zoneNetData)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3113247157u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in targetReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in zoneNetData, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3113247157u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (targetReference.TryGet(out var networkObject) && networkObject.TryGetComponent<HarpoonPullDropTarget>(out var component) && component.PullSessionActive && component.PullPrimaryClientId == base.OwnerClientId)
				{
					component.ServerSetPullSuccessZoneNetData(zoneNetData);
				}
			}
		}

		[Rpc(SendTo.Owner)]
		private void ClientStartAssistCreatureChallengeRpc(NetworkObjectReference creatureReference, HarpoonSuccessZoneNetData zoneNetData, byte completedZoneMask, ulong primaryClientId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1659981981u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in creatureReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in zoneNetData, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in completedZoneMask, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, primaryClientId);
				__endSendRpc(ref bufferWriter, 1659981981u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsOwner)
			{
				CatchableSeaCreature component;
				string rejectReason;
				if (!creatureReference.TryGet(out var networkObject))
				{
					UtilityMethods.Log("[HarpoonTool_Assist] Assist challenge failed. Creature reference could not resolve.", LogType.Warning);
					RejectAssistChallengeStart(null, "Creature reference could not resolve.");
				}
				else if (!networkObject.TryGetComponent<CatchableSeaCreature>(out component))
				{
					UtilityMethods.Log("[HarpoonTool_Assist] Assist challenge failed. Target is not CatchableSeaCreature.", LogType.Warning);
					RejectAssistChallengeStart(null, "Target is not CatchableSeaCreature.");
				}
				else if (!CanStartAssistChallengeFromRpc(component, zoneNetData, completedZoneMask, primaryClientId, out rejectReason))
				{
					RejectAssistChallengeStart(component, rejectReason);
				}
				else
				{
					BeginAssistCreatureChallenge(component, zoneNetData, completedZoneMask, primaryClientId);
				}
			}
		}

		private void BeginAssistCreatureChallenge(CatchableSeaCreature creature, HarpoonSuccessZoneNetData zoneNetData, byte completedZoneMask, ulong primaryClientId)
		{
			if (creature == null)
			{
				StartReload();
				return;
			}
			_currentFish = creature;
			_fishChallengeStartWorldPosition = creature.transform.position;
			_hasCachedFishChallengeStartWorldPosition = true;
			_currentFishPullProgress = 0f;
			if (_hookedPoint == null)
			{
				_hookedPoint = new GameObject("HookPoint").transform;
			}
			creature.ClientHooked(_player, _hookedPoint);
			SetState(HarpoonState.Challenge);
			StartAssistFishChallenge(zoneNetData, completedZoneMask, primaryClientId);
		}

		private bool CanStartAssistChallengeFromRpc(CatchableSeaCreature creature, HarpoonSuccessZoneNetData zoneNetData, byte completedZoneMask, ulong primaryClientId, out string rejectReason)
		{
			rejectReason = string.Empty;
			if (creature == null)
			{
				rejectReason = "Creature is null.";
				return false;
			}
			if (!creature.IsSpawned)
			{
				rejectReason = "Creature is not spawned.";
				return false;
			}
			if (!creature.IsHooked.Value)
			{
				rejectReason = "Creature is no longer hooked.";
				return false;
			}
			if (HasRecentlyInvalidatedAssistCreature(creature))
			{
				rejectReason = "Creature assist session was already finished or cancelled.";
				return false;
			}
			if (primaryClientId == base.OwnerClientId)
			{
				rejectReason = "Owner is primary client.";
				return false;
			}
			int num = Mathf.Clamp(zoneNetData.Count, 0, 8);
			if (num <= 0)
			{
				rejectReason = "Zone count is zero.";
				return false;
			}
			int num2 = (1 << num) - 1;
			if ((completedZoneMask & num2) == num2)
			{
				rejectReason = "All assist zones are already completed.";
				return false;
			}
			return true;
		}

		private void RejectAssistChallengeStart(CatchableSeaCreature creature, string reason)
		{
			UtilityMethods.Log("[HarpoonTool_Assist] Assist challenge start rejected locally. Reason=" + reason, LogType.Warning);
			if (creature != null && creature.IsSpawned)
			{
				creature.ServerLeaveAssistCatchSessionRpc();
			}
			if (_challengeStart)
			{
				Challenge_End();
			}
			_currentFish = null;
			if (IsReloading)
			{
				ReloadDone();
			}
			else if (!IsIdle)
			{
				StartReload(instant: true);
			}
			else
			{
				ForceResetVisualState(hardStopGrapple: true, restoreHeldSpearGraphic: true);
			}
		}

		private void Challenge_LeaveAssistSessionIfNeeded()
		{
			if (_sharedCatchActive)
			{
				if (_currentFish != null)
				{
					_currentFish.ServerLeaveAssistCatchSessionRpc();
				}
				else if (_currentPullDropTarget != null)
				{
					_currentPullDropTarget.ServerLeavePullSessionRpc();
				}
			}
		}

		[Rpc(SendTo.NotOwner)]
		private void ClientStartAssistCreatureVisualRpc(NetworkObjectReference creatureReference)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2871375145u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in creatureReference, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 2871375145u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!base.IsOwner && creatureReference.TryGet(out var networkObject) && networkObject.TryGetComponent<CatchableSeaCreature>(out var component))
				{
					AttachCreatureHookVisual(component);
				}
			}
		}

		private void AttachCreatureHookVisual(CatchableSeaCreature creature)
		{
			if (!(creature == null))
			{
				if (_hookedPoint == null)
				{
					_hookedPoint = new GameObject("HookPoint").transform;
				}
				_currentFish = creature;
				creature.ClientHooked(_player, _hookedPoint);
				Vector3 position = creature.HookedPoint.position;
				Vector3 forward = creature.HookedPoint.forward;
				Transform transform = PlaceStuck(_tpsTipGraphic, ref _tpsStuckTipInstance, position, forward, "ClientPlayer");
				if (_tpsStuckTipInstance != null)
				{
					_tpsStuckTipInstance.transform.SetParent(creature.HookedPoint, worldPositionStays: false);
					_tpsStuckTipInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
				}
				if (_grapple != null && transform != null)
				{
					_grapple.SetVisualTarget(transform);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerRegisterAssistSuccessZoneNetDataRpc(NetworkObjectReference creatureReference, HarpoonSuccessZoneNetData zoneNetData)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2647506744u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in creatureReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in zoneNetData, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 2647506744u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (creatureReference.TryGet(out var networkObject) && networkObject.TryGetComponent<CatchableSeaCreature>(out var component) && component.AssistCatchActive && component.AssistPrimaryClientId == base.OwnerClientId)
				{
					component.ServerSetAssistSuccessZoneNetData(zoneNetData);
					UtilityMethods.Log("[HarpoonTool_Assist] Success zone net data registered. " + $"PrimaryClientId={base.OwnerClientId} ZoneCount={zoneNetData.Count}");
				}
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(2301285499u, __rpc_handler_2301285499, "ClientBarBlockStatusRpc", RpcInvokePermission.Everyone);
			__registerRpc(1428109325u, __rpc_handler_1428109325, "OwnerFailPullableRpc", RpcInvokePermission.Everyone);
			__registerRpc(2855019682u, __rpc_handler_2855019682, "ServerReportAssistBarValueRpc", RpcInvokePermission.Everyone);
			__registerRpc(3863465075u, __rpc_handler_3863465075, "ClientReceiveAssistBarValueRpc", RpcInvokePermission.Everyone);
			__registerRpc(395073671u, __rpc_handler_395073671, "ServerShootVisualsToRpc", RpcInvokePermission.Everyone);
			__registerRpc(3877412408u, __rpc_handler_3877412408, "ClientShootVisualsRpc", RpcInvokePermission.Everyone);
			__registerRpc(3410618994u, __rpc_handler_3410618994, "ServerStartReloadVisualsToRpc", RpcInvokePermission.Everyone);
			__registerRpc(862990163u, __rpc_handler_862990163, "ClientStartReloadVisualsRpc", RpcInvokePermission.Everyone);
			__registerRpc(3280889285u, __rpc_handler_3280889285, "ServerHardStopGrappleToRpc", RpcInvokePermission.Everyone);
			__registerRpc(241023933u, __rpc_handler_241023933, "ClientHardStopGrappleRpc", RpcInvokePermission.Everyone);
			__registerRpc(552906656u, __rpc_handler_552906656, "ClientStopSuccessfulCatchVisualsRpc", RpcInvokePermission.Everyone);
			__registerRpc(500683969u, __rpc_handler_500683969, "ServerResetHarpoonToRpc", RpcInvokePermission.Everyone);
			__registerRpc(3703591482u, __rpc_handler_3703591482, "ServerResetHarpoonRpc", RpcInvokePermission.Everyone);
			__registerRpc(2611128831u, __rpc_handler_2611128831, "ServerTipHitEnvironmentToRpc", RpcInvokePermission.Everyone);
			__registerRpc(4294135301u, __rpc_handler_4294135301, "ClientTipHitEnvironmentRpc", RpcInvokePermission.Everyone);
			__registerRpc(3491376826u, __rpc_handler_3491376826, "ServerTipHitPlayerToRpc", RpcInvokePermission.Everyone);
			__registerRpc(2620572696u, __rpc_handler_2620572696, "ClientTipHitPlayerRpc", RpcInvokePermission.Everyone);
			__registerRpc(4266322301u, __rpc_handler_4266322301, "ServerRequestHookCreatureRpc", RpcInvokePermission.Everyone);
			__registerRpc(3902902839u, __rpc_handler_3902902839, "ServerRequestHitHostileCreatureRpc", RpcInvokePermission.Everyone);
			__registerRpc(2284852911u, __rpc_handler_2284852911, "ServerRequestHitJellyfishTrapRpc", RpcInvokePermission.Everyone);
			__registerRpc(3278989640u, __rpc_handler_3278989640, "ServerRequestHitOctopusCreatureRpc", RpcInvokePermission.Everyone);
			__registerRpc(3400116543u, __rpc_handler_3400116543, "ServerRequestHitCrabBossRpc", RpcInvokePermission.Everyone);
			__registerRpc(2030621906u, __rpc_handler_2030621906, "ClientStartCreatureChallengeRpc", RpcInvokePermission.Everyone);
			__registerRpc(3319068633u, __rpc_handler_3319068633, "ClientRejectCreatureHookRpc", RpcInvokePermission.Everyone);
			__registerRpc(3059836996u, __rpc_handler_3059836996, "ClientChallangeSuccessRpc", RpcInvokePermission.Everyone);
			__registerRpc(1501152810u, __rpc_handler_1501152810, "ClientShowCatchInfoRpc", RpcInvokePermission.Everyone);
			__registerRpc(2099277671u, __rpc_handler_2099277671, "ServerFinalizeCreatureChallengeSuccessRpc", RpcInvokePermission.Everyone);
			__registerRpc(4085371659u, __rpc_handler_4085371659, "ServerTipHitDestructibleToRpc", RpcInvokePermission.Everyone);
			__registerRpc(2283807490u, __rpc_handler_2283807490, "ClientTipHitDestructibleRpc", RpcInvokePermission.Everyone);
			__registerRpc(1077796824u, __rpc_handler_1077796824, "ServerUpdateChallengeFishPullRpc", RpcInvokePermission.Everyone);
			__registerRpc(1950513083u, __rpc_handler_1950513083, "ServerRequestPullDropTargetChallengeRpc", RpcInvokePermission.Everyone);
			__registerRpc(2326852655u, __rpc_handler_2326852655, "ClientStartPullDropTargetChallengeRpc", RpcInvokePermission.Everyone);
			__registerRpc(396542152u, __rpc_handler_396542152, "ServerCancelUnstartedPullDropTargetChallengeRpc", RpcInvokePermission.Everyone);
			__registerRpc(2539352796u, __rpc_handler_2539352796, "ClientStartAssistPullDropTargetChallengeRpc", RpcInvokePermission.Everyone);
			__registerRpc(1910288464u, __rpc_handler_1910288464, "ClientRejectPullDropTargetHookRpc", RpcInvokePermission.Everyone);
			__registerRpc(3672133852u, __rpc_handler_3672133852, "ClientStartPullDropTargetVisualRpc", RpcInvokePermission.Everyone);
			__registerRpc(3113247157u, __rpc_handler_3113247157, "ServerRegisterPullDropTargetSuccessZoneNetDataRpc", RpcInvokePermission.Everyone);
			__registerRpc(1659981981u, __rpc_handler_1659981981, "ClientStartAssistCreatureChallengeRpc", RpcInvokePermission.Everyone);
			__registerRpc(2871375145u, __rpc_handler_2871375145, "ClientStartAssistCreatureVisualRpc", RpcInvokePermission.Everyone);
			__registerRpc(2647506744u, __rpc_handler_2647506744, "ServerRegisterAssistSuccessZoneNetDataRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_2301285499(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ClientBarBlockStatusRpc(value);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1428109325(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).OwnerFailPullableRpc();
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2855019682(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ServerReportAssistBarValueRpc(value, value2, value3);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3863465075(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
				reader.ReadValueSafe(out float value4, default(FastBufferWriter.ForPrimitives));
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ClientReceiveAssistBarValueRpc(value, value2, value3, value4);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_395073671(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ServerShootVisualsToRpc(value, value2);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3877412408(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ClientShootVisualsRpc(value, value2);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3410618994(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ServerStartReloadVisualsToRpc(value);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_862990163(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ClientStartReloadVisualsRpc(value);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3280889285(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ServerHardStopGrappleToRpc();
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_241023933(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ClientHardStopGrappleRpc();
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_552906656(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ClientStopSuccessfulCatchVisualsRpc();
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_500683969(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ServerResetHarpoonToRpc();
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3703591482(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ServerResetHarpoonRpc();
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2611128831(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Vector3 value2);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ServerTipHitEnvironmentToRpc(value, value2);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4294135301(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Vector3 value2);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ClientTipHitEnvironmentRpc(value, value2);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3491376826(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out Vector3 value3);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ServerTipHitPlayerToRpc(value, value2, value3);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2620572696(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out Vector3 value3);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ClientTipHitPlayerRpc(value, value2, value3);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4266322301(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ServerRequestHookCreatureRpc(value, value2);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3902902839(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ServerRequestHitHostileCreatureRpc(value, value2, ext);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2284852911(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ServerRequestHitJellyfishTrapRpc(value, value2);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3278989640(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ServerRequestHitOctopusCreatureRpc(value, value2);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3400116543(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ServerRequestHitCrabBossRpc(value, value2, ext);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2030621906(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ClientStartCreatureChallengeRpc(value, value2);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3319068633(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ClientRejectCreatureHookRpc(ext);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3059836996(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
				reader.ReadValueSafe(out float value3, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ClientChallangeSuccessRpc(value, value2, value3, ext);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1501152810(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ClientShowCatchInfoRpc(value, value2, ext);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2099277671(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ServerFinalizeCreatureChallengeSuccessRpc(value);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4085371659(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out Vector3 value2);
				RpcParams ext = rpcParams.Ext;
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ServerTipHitDestructibleToRpc(value, value2, ext);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2283807490(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out Vector3 value2);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ClientTipHitDestructibleRpc(value, value2);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1077796824(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ServerUpdateChallengeFishPullRpc(value, value2);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1950513083(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out Vector3 value3);
				reader.ReadValueSafe(out Vector3 value4);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ServerRequestPullDropTargetChallengeRpc(value, value2, value3, value4);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2326852655(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				RpcParams ext = rpcParams.Ext;
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ClientStartPullDropTargetChallengeRpc(value, value2, ext);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_396542152(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ServerCancelUnstartedPullDropTargetChallengeRpc(value, ext);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2539352796(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out HarpoonSuccessZoneNetData value2, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out byte value3, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value4);
				RpcParams ext = rpcParams.Ext;
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ClientStartAssistPullDropTargetChallengeRpc(value, value2, value3, value4, ext);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1910288464(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ClientRejectPullDropTargetHookRpc(ext);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3672133852(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ClientStartPullDropTargetVisualRpc(value);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3113247157(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out HarpoonSuccessZoneNetData value2, default(FastBufferWriter.ForNetworkSerializable));
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ServerRegisterPullDropTargetSuccessZoneNetDataRpc(value, value2);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1659981981(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out HarpoonSuccessZoneNetData value2, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out byte value3, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value4);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ClientStartAssistCreatureChallengeRpc(value, value2, value3, value4);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2871375145(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ClientStartAssistCreatureVisualRpc(value);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2647506744(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out HarpoonSuccessZoneNetData value2, default(FastBufferWriter.ForNetworkSerializable));
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((HarpoonTool)target).ServerRegisterAssistSuccessZoneNetDataRpc(value, value2);
				((HarpoonTool)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "HarpoonTool";
		}
	}
}
