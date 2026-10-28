using System;
using System.Collections;
using System.Collections.Generic;
using Bozo.ModularCharacters;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Dissonance;
using Dissonance.Integrations.Unity_NFGO;
using ECM2;
using ECM2.Examples.FirstPerson;
using ECM2.Walkthrough.Ex31;
using EPOOutline;
using HoaxGames;
using Kamgam.SettingsGenerator;
using Netcode.Transports.Facepunch;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Data.Tools;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Interaction.ToolTip;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Player.Visuals;
using PaperPixelGames.Core.PlayerTools;
using PaperPixelGames.Core.PlayerTools.Harpoon;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Core.Settings;
using PaperPixelGames.Core.Surf;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.Dissonance;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States.Game;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking;
using PaperPixelGames.Networking.Chat.UI;
using PaperPixelGames.Wheel;
using Steamworks;
using StylizedWater3;
using TMPro;
using Unity.Cinemachine;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Player
{
	public class PPG_Player : NetworkBehaviour, ISaveable<PlayerData>
	{
		private readonly struct ExpressionBlendShape
		{
			public readonly string Name;

			public readonly float Value;

			public ExpressionBlendShape(string name, float value)
			{
				Name = name;
				Value = value;
			}
		}

		private const float FishSlipServerValidationDistance = 3f;

		[Header("Player")]
		[SerializeField]
		private Interactor _interactor;

		[SerializeField]
		private Thrower _thrower;

		[SerializeField]
		private Placer _placer;

		[SerializeField]
		private PlayerInteraction _playerInteractable;

		[SerializeField]
		private SurfaceChecker _surfaceChecker;

		[SerializeField]
		private PlayerCharacterVisuals _playerVisuals;

		[SerializeField]
		private PlayerRigBoneReferences _rigBones;

		[SerializeField]
		private ParentConstraint _parentConstraint;

		[SerializeField]
		private SwayHandler _sway;

		[SerializeField]
		private AudioClipSO _fishSlipSFX;

		[SerializeField]
		private AudioClipSO _umbrellaBoostSFX;

		[SerializeField]
		[Min(0f)]
		private float _fishSlipCooldownSeconds = 1f;

		public NetworkVariable<ulong> SteamID = new NetworkVariable<ulong>(0uL, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		public NetworkVariable<FixedString64Bytes> PlayerName = new NetworkVariable<FixedString64Bytes>("", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		public NetworkVariable<byte> ColorIndex = new NetworkVariable<byte>(byte.MaxValue);

		public NetworkVariable<FixedString64Bytes> ZoneName = new NetworkVariable<FixedString64Bytes>("Boat", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		public NetworkVariable<bool> InCage = new NetworkVariable<bool>(value: false);

		private Character _character;

		private SeaLayer _primarySeaLayer;

		private SharkCage _serverActiveSharkCage;

		private IPlayerTrapRecoveryOwner _serverTrapRecoveryOwner;

		private EventBinding<ToolDataEvent> _toolDataBinding;

		private float _nextFishSlipTime;

		private bool _fishSlipRecoveryPending;

		private bool _fishSlipAnimatorStateObserved;

		private int _fishSlipStartFrame;

		private uint _electrocutionRecoverySequence;

		private bool _electrocutionRecoveryPending;

		private bool _initialLocomotionReconciled;

		private bool _localGameplayReadyReported;

		private bool _criticalSetupCompleted;

		private bool _initialToolSnapshotReady;

		private PlayerPreparationOperation _customizationOperation;

		private bool _waitingForBootstrapCoordinator;

		private uint _spawnGeneration;

		private bool _localIdentityPublished;

		private bool _localCustomizationPayloadPublished;

		private bool _localCustomizationApplyStarted;

		private bool _localCustomizationApplyFinished;

		private bool _localRegistrationCompleted;

		private bool _clientRegistrationCompleted;

		private bool _bootstrapSpawnReported;

		private Coroutine _bootstrapSpawnRetryCoroutine;

		private const int MaxCustomizationPayloadBytes = 131072;

		private const int CustomizationSnapshotWarningAttempt = 5;

		private const float CustomizationSnapshotRetryIntervalSeconds = 2f;

		private const int RemoteCustomizationApplyWarningAttempt = 3;

		private const int RemoteCustomizationApplyRetryDelayMilliseconds = 1000;

		private const int RemoteCustomizationApplyMaxRetryDelayMilliseconds = 5000;

		private byte[] _lastCustomizationPayload;

		private byte[] _lastAppliedRemoteCustomizationPayload;

		private byte[] _pendingRemoteCustomizationPayload;

		private bool _isApplyingRemoteCustomization;

		private bool _remoteCustomizationApplied;

		private bool _customizationSnapshotWaitReported;

		private Coroutine _customizationSnapshotRetryCoroutine;

		private readonly HashSet<string> _reportedInitializationFailureStages = new HashSet<string>();

		private const float DivePredictionRefreshInterval = 0.15f;

		private const float DivePredictionStep = 0.2f;

		private const float DivePredictionHorizon = 10f;

		private const float AuthoredWalkSpeed = 2f;

		private const float AuthoredRunSpeed = 4.5f;

		private const float MaximumLocomotionPlaybackSpeed = 2.5f;

		public static readonly int EMPTY_LEFT = Animator.StringToHash("EmptyLeft");

		public static readonly int EMPTY_RIGHT = Animator.StringToHash("EmptyRight");

		private static readonly int IS_HOLDING = Animator.StringToHash("IsHolding");

		private static readonly int START_HOLD = Animator.StringToHash("StartHold");

		private static readonly int HOLD_TYPE = Animator.StringToHash("HoldType");

		private static readonly int IS_MOVING = Animator.StringToHash("IsMoving");

		private static readonly int IS_SPRINTING = Animator.StringToHash("IsSprinting");

		private static readonly int IS_PLACING = Animator.StringToHash("IsPlacing");

		public static readonly int HARPOON = Animator.StringToHash("Harpoon");

		public static readonly int HARPOON_AIM = Animator.StringToHash("HarpoonAim");

		public static readonly int HARPOON_SHOOT = Animator.StringToHash("HarpoonShoot");

		public static readonly int FISHING_ROD = Animator.StringToHash("FishingRod");

		public static readonly int FISHING_ROD_CAST_HOLD = Animator.StringToHash("FishingRodCastHold");

		public static readonly int FISHING_ROD_CAST_END = Animator.StringToHash("FishingRodCastEnd");

		public static readonly int FISHING_ROD_REEL = Animator.StringToHash("FishingRodReel");

		public static readonly int FISHING_ROD_STEER = Animator.StringToHash("FishingRodSteer");

		public static readonly int FISHING_ROD_STEER_ACTIVE = Animator.StringToHash("FishingRodSteerActive");

		public static readonly int FISHING_ROD_FAIL = Animator.StringToHash("FishingRodFail");

		public static readonly int FISHING_ROD_SUCCESS = Animator.StringToHash("FishingRodSuccess");

		private static readonly int FISHING_ROD_SUCCESS_STATE = Animator.StringToHash("Rod_Success");

		private static readonly int FISHING_ROD_FAIL_STATE = Animator.StringToHash("Rod_Fail");

		private static readonly int FISHING_ROD_REEL_STATE = Animator.StringToHash("Rod_Reel_Loop");

		public static readonly int TRIDENT = Animator.StringToHash("Trident");

		public static readonly int TRIDENT_AIM = Animator.StringToHash("TridentAim");

		public static readonly int TRIDENT_THROW = Animator.StringToHash("TridentThrow");

		public static readonly int SCOOTER = Animator.StringToHash("Scooter");

		public static readonly int SCOOTER_AIRBORNE = Animator.StringToHash("ScooterAirborne");

		public static readonly int DIVE = Animator.StringToHash("Dive");

		public static readonly int TOTTI = Animator.StringToHash("Totti");

		public static readonly int DRAG = Animator.StringToHash("Drag");

		private static readonly int SHARK_DRAG_PUNCH = Animator.StringToHash("SharkDragPunch");

		public static readonly int SWEEP = Animator.StringToHash("Sweep");

		public static readonly int DROWN = Animator.StringToHash("Drown");

		public static readonly int DROWN_END = Animator.StringToHash("DrownEnd");

		public static readonly int IS_UNDERWATER = Animator.StringToHash("IsUnderwater");

		public static readonly int IS_EMOTE = Animator.StringToHash("IsEmote");

		public static readonly int IS_EMOTE_STATIC = Animator.StringToHash("IsEmoteStatic");

		public static readonly int EMOTE_TYPE = Animator.StringToHash("EmoteType");

		public static readonly int EXPRESSION = Animator.StringToHash("Expression");

		private static readonly int EMPTY_STATIC_EMOTE_STATE = Animator.StringToHash("EmoteLayer.Empty");

		public static readonly int MOVE_X = Animator.StringToHash("MoveX");

		public static readonly int MOVE_Y = Animator.StringToHash("MoveY");

		private static readonly int SEA_SCOOTER_X = Animator.StringToHash("SeaScooterX");

		private static readonly int SEA_SCOOTER_Y = Animator.StringToHash("SeaScooterY");

		public static readonly int SPEED_01 = Animator.StringToHash("Speed01");

		private static readonly int LOCOMOTION_PLAYBACK_SPEED = Animator.StringToHash("LocomotionPlaybackSpeed");

		public static readonly int IS_GROUNDED = Animator.StringToHash("IsGrounded");

		public static readonly int VERTICAL_SPEED = Animator.StringToHash("VerticalSpeed");

		public static readonly int JUMP_TRIGGER = Animator.StringToHash("JumpTrigger");

		public static readonly int SWIM_TRAP = Animator.StringToHash("SwimTrap");

		public NetworkVariable<bool> ConstraintStatus = new NetworkVariable<bool>(value: true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		[Header("ViewModel")]
		[SerializeField]
		private Animator _fpsAnimator;

		[Header("ECM2")]
		[SerializeField]
		private Animator _tpsAnimator;

		[SerializeField]
		private FootIK _footIK;

		[Header("Animator Tunning")]
		[SerializeField]
		private float _idleSpeedEpsilon = 0.05f;

		[SerializeField]
		private float _walkReferenceSpeed = 3f;

		[SerializeField]
		private float _runReferenceSpeed = 7f;

		[SerializeField]
		private float _locomotionDampTime = 0.1f;

		[SerializeField]
		[Min(0f)]
		private float _diveMinimumAirborneTime = 1f;

		private bool _holdActive;

		private bool _staticEmoteActive;

		private bool _staticEmoteFpsCameraActive;

		private bool _staticEmoteCameraToggleEnabled;

		private bool _swingEmoteActive;

		private bool _swingFpsCameraActive;

		private bool _swingFootIKStateCached;

		private bool _swingFootIKWasEnabled;

		private bool _observedSwingVisualLocked;

		private bool _observedSwingNetworkTransformWasEnabled;

		private bool _observedSwingHeadNetworkTransformWasEnabled;

		private bool _observedSwingSpineNetworkTransformWasEnabled;

		private bool _observedSwingNetworkAnimatorWasEnabled;

		private bool _observedSwingFootIKWasEnabled;

		private bool _swingExitHardResetActive;

		private bool _swingExitHardResetLockTransform;

		private bool _staticEmoteExitHardResetActive;

		private float _swingExitHardResetUntil;

		private float _swingExitPoseLockUntil;

		private float _staticEmoteExitHardResetUntil;

		private Vector3 _swingExitHardResetPosition;

		private Quaternion _swingExitHardResetRotation = Quaternion.identity;

		private int _observedSwingVisualLockSequence;

		private Coroutine _observedSwingNetworkAnimatorRestoreRoutine;

		private NetworkAnimator _observedSwingNetworkAnimator;

		private int _staticEmoteSequence;

		private int _emoteExitInputFrame = -1;

		private const float StaticEmoteExitHardResetDuration = 0.55f;

		private const float SwingExitHardResetDuration = 0.55f;

		private const float SwingExitTransformLockDuration = 0.2f;

		private const float ObservedSwingNetworkAnimatorRestoreDelay = 0.55f;

		private int _holdType;

		private bool _jumpAnimArmed;

		private bool _locomotionAnimationSuppressed;

		private Renderer[] _sharkDragFpsRenderers;

		private bool[] _sharkDragFpsRendererStates;

		private bool _sharkDragFpsPresentationActive;

		private uint _sharkDragFpsPunchToken;

		private bool _diveAnimatorActive;

		private bool _divePausedConstraints;

		private bool _underwaterHarpoonAimRuleActive;

		private float _diveAirborneTime;

		private float _nextDivePredictionTime;

		private readonly RaycastHit[] _divePredictionHits = new RaycastHit[32];

		private bool _fpsHasSeaScooterBlendParameters;

		private SwingEmoteArea _activeSwingArea;

		private float _lastSwingPumpInput;

		private const float RemoteFootIkDisableDistance = 28f;

		private const float RemoteFootIkEnableDistance = 24f;

		private const float RemoteFootIkEvaluationInterval = 0.25f;

		private float _nextRemoteFootIkEvaluationTime;

		private bool _remoteFootIkSuppressed;

		public EmoteArea CurrentEmoteArea;

		[Header("Camera")]
		[SerializeField]
		private CamShakeConnectionSO _camShakeConnectionSO;

		[SerializeField]
		private RenderDistanceConnectionSO _renderDistanceConnectionSO;

		[SerializeField]
		private Transform _cameraParent;

		[SerializeField]
		private Camera _playerCamera;

		[SerializeField]
		private Camera _viewModelCamera;

		[SerializeField]
		private LookAtConstraint _lookAtConstraint;

		[SerializeField]
		private LookAtConstraint _lookAtConstraint2;

		[SerializeField]
		private AudioListener _audioListener;

		[SerializeField]
		private Outliner _outliner;

		[SerializeField]
		private UniversalAdditionalCameraData _additionalCameraData;

		[SerializeField]
		private CinemachineBrain _cinemachineBrain;

		[SerializeField]
		private CinemachineCamera _cinemachineCamera;

		[SerializeField]
		private CinemachineCamera _emoteCamera;

		[SerializeField]
		private CinemachineCamera _tottiCamera;

		[SerializeField]
		private CinemachineCamera _actionCamera;

		[SerializeField]
		private CinemachineCamera _tpsActionCamera;

		[SerializeField]
		private CinemachineImpulseSource _impulseSource;

		[Header("Emote FPS Look")]
		[SerializeField]
		[Range(0f, 90f)]
		private float _emoteFpsMaxYaw = 30f;

		[SerializeField]
		[Range(0f, 45f)]
		private float _emoteFpsMaxPitch = 10f;

		[Header("Camera Bob")]
		[SerializeField]
		private bool _enableCameraBob = true;

		[SerializeField]
		[Min(0f)]
		private float _bobPositionAmount = 0.028f;

		[SerializeField]
		[Min(0f)]
		private float _bobRotationAmount = 1.25f;

		[SerializeField]
		[Min(0f)]
		private float _walkBobFrequency = 1.65f;

		[SerializeField]
		[Min(0f)]
		private float _runBobFrequency = 2.35f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _strafeBobBias = 0.65f;

		[SerializeField]
		[Min(0.001f)]
		private float _bobSmoothTime = 0.07f;

		[SerializeField]
		private bool _bobUseUnscaledTime;

		[Header("Fishing Camera Shake")]
		[SerializeField]
		private bool _enableFishingCameraShake = true;

		[SerializeField]
		[Min(0f)]
		private float _fishingShakeAmplitude = 0.18f;

		[SerializeField]
		[Min(0f)]
		private float _fishingShakeFrequency = 1.15f;

		[SerializeField]
		[Min(0.001f)]
		private float _fishingShakeBlendTime = 0.25f;

		[SerializeField]
		private bool _fishingShakeUseUnscaledTime;

		[Header("Landing Polish")]
		[SerializeField]
		private bool _enableLandingPolish = true;

		[SerializeField]
		[Min(0f)]
		private float _landingMinImpactSpeed = 5f;

		[SerializeField]
		[Min(0.01f)]
		private float _landingMaxImpactSpeed = 13f;

		[SerializeField]
		[Min(0f)]
		private float _landingAmount = 1f;

		[SerializeField]
		[Min(0.01f)]
		private float _landingSmoothTime = 0.16f;

		private const float LandingBaseDownOffset = 0.07f;

		private const float LandingBasePitch = 3f;

		private float _baseGameplayFovCurrent;

		private float _baseGameplayFovTarget;

		private float _landingWeightCurrent;

		private float _landingWeightTarget;

		private float _landingWeightVelocity;

		private bool _landingReturning;

		private Vector3 _landingPosOffsetCurrent;

		private Vector3 _landingRotOffsetCurrent;

		private CamShakeConnection _camShakeConnection;

		private RenderDistanceConnection _renderDistanceConnection;

		private bool _isFishingShakeActive;

		private float _fishingShakeAmplitudeCurrent;

		private float _fishingShakeAmplitudeVel;

		private float _fishingShakeFrequencyCurrent;

		private float _fishingShakeFrequencyVel;

		private Vector3 _bobPosCurrent;

		private Vector3 _bobPosVel;

		private Vector3 _bobRotCurrent;

		private Vector3 _bobRotVel;

		private float _bobPhase;

		private float _bobSpeed01;

		private bool _bobIsMoving;

		private bool _bobIsRunning;

		private float _bobStrafe01;

		private bool _cameraBobSuppressed;

		private Coroutine _zoomRoutine;

		private Tweener _fovTween;

		private Sequence _shakeTween;

		private CinemachineCamera _activeCinemachineCam;

		private CinemachineBasicMultiChannelPerlin _cmPerlin;

		private CameraLookMode _cameraLookMode;

		private float _freeLookYaw;

		private float _freeLookPitch;

		private float minYaw = -30f;

		private float maxYaw = 30f;

		private float _freeLookMinPitch;

		private float _freeLookMaxPitch;

		private bool _freeLookUseWorldUpYaw;

		private Quaternion _freeLookBaseLocalRotation = Quaternion.identity;

		private float _crouchYOffsetCurrent;

		private Tweener _crouchOffsetTween;

		private float _baseShakeAmplitude;

		private float _baseShakeFrequency;

		private float _oneShotShakeAmplitudeCurrent;

		private float _oneShotShakeFrequencyCurrent;

		private Tweener _oneShotAmpTween;

		private Tweener _oneShotFreqTween;

		[Header("Canvas")]
		[SerializeField]
		private GameObject _canvasParent;

		[Header("Player Name")]
		[SerializeField]
		private TMP_Text _playerNameText;

		[Header("Feedback")]
		[SerializeField]
		private CanvasGroup _damageFeedback;

		[Header("PTT")]
		[SerializeField]
		private GameObject _pttObject;

		[SerializeField]
		private GameObject _pttChildObject;

		[Header("Crosshair")]
		[SerializeField]
		private GameObject _chParent;

		[SerializeField]
		private Transform _chCirlce;

		[SerializeField]
		private GameObject _chHit;

		[SerializeField]
		private Image _chCenter;

		[SerializeField]
		private Color _chCenterDefault;

		[SerializeField]
		private Color _chCenterHit;

		[Header("Tween Settings")]
		[SerializeField]
		private float _hitDuration = 0.15f;

		[SerializeField]
		private float _colorDuration = 0.1f;

		[SerializeField]
		private float _aimDuration = 0.2f;

		[Space]
		[SerializeField]
		private PlayerNamesConnectionSO _playerNamesConnectionSO;

		private PlayerNamesConnection _playerNamesConnection;

		private bool _alwaysShowPlayerNames;

		private bool _contextPlayerNameVisible;

		private Tweener _hitTween;

		private CanvasGroup _expandingPulseFeedback;

		private bool _surfCursorSuppressed;

		private bool _cursorRequestedVisible = true;

		private bool _willHit;

		[Header("Dissonance - Simple Mouth")]
		[Tooltip("Nfgo player component used to resolve the Dissonance voice player id.")]
		[SerializeField]
		private NfgoPlayer _nfgoPlayer;

		[Tooltip("Blendshape value used when the mouth is fully closed.")]
		[SerializeField]
		private float _closedMouthValue;

		[Tooltip("Maximum blendshape value used when the player speaks loudly.")]
		[SerializeField]
		private float _maxMouthOpenValue = 85f;

		[Header("Simple Mouth - Voice Mapping")]
		[Tooltip("Amplitude below this value is treated as silence.")]
		[SerializeField]
		private float _silenceAmplitude = 0.01f;

		[Tooltip("Amplitude at or above this value maps to maximum mouth opening.")]
		[SerializeField]
		private float _shoutAmplitude = 0.18f;

		[Tooltip("Controls the mouth response curve; lower values open the mouth more easily.")]
		[SerializeField]
		private float _mouthPower = 0.85f;

		[Header("Simple Mouth - Motion")]
		[Tooltip("How fast the mouth opens toward the target value.")]
		[SerializeField]
		private float _openSpeed = 220f;

		[Tooltip("How fast the mouth closes back to the resting value.")]
		[SerializeField]
		private float _closeSpeed = 260f;

		[Tooltip("How quickly raw voice amplitude is smoothed before driving the mouth.")]
		[SerializeField]
		private float _amplitudeSharpness = 24f;

		[Header("Dissonance - Voice Activation Visual")]
		[Tooltip("Minimum voice amplitude required to show the speaking visual state.")]
		[SerializeField]
		private float _voiceActivationThreshold = 0.015f;

		[Tooltip("How long the speaking visual stays active after voice energy drops.")]
		[SerializeField]
		private float _voiceActivationHoldSeconds = 0.1f;

		[Header("Underwater Voice Particle")]
		[Tooltip("Particle played when this player speaks underwater.")]
		[SerializeField]
		private ParticleSystem _underwaterVoiceParticle;

		[Tooltip("Minimum amplitude required before underwater speaking particles start.")]
		[SerializeField]
		private float _underwaterVoiceParticleMinAmplitude = 0.012f;

		[Tooltip("Amplitude that maps to maximum underwater particle emission.")]
		[SerializeField]
		private float _underwaterVoiceParticleMaxAmplitude = 0.18f;

		[Tooltip("Minimum particle emission rate while speaking underwater.")]
		[SerializeField]
		private float _underwaterVoiceParticleMinRate = 2f;

		[Tooltip("Maximum particle emission rate while speaking loudly underwater.")]
		[SerializeField]
		private float _underwaterVoiceParticleMaxRate = 35f;

		[Tooltip("Controls particle emission response curve; lower values increase particles more easily.")]
		[SerializeField]
		private float _underwaterVoiceParticlePower = 0.85f;

		private SkinnedMeshRenderer _headRenderer;

		private VoicePlayerState _voiceState;

		private const string JawOpenBlendShapeName = "Expression_Jaw_Open";

		private string _boundVoicePlayerId;

		private float _smoothedAmplitude;

		private float _mouthValue;

		private float _voiceActiveUntil = -1f;

		private int _resolvedJawOpenBlendShapeIndex = 17;

		private Mesh _resolvedJawMesh;

		private float _lastAppliedMouthValue = float.NaN;

		private const float MouthBlendShapeWriteEpsilon = 0.25f;

		private bool _lastVoiceActivationState;

		private bool _loggedMissingFaceRenderer;

		private bool _loggedMissingFaceMesh;

		private bool _loggedMissingJawBlendShape;

		[Header("VFX")]
		[SerializeField]
		private ParticleSystem _electricityVFX;

		[SerializeField]
		private ParticleSystem _oceanEnterTPSVFX;

		[SerializeField]
		private ParticleSystem _oceanEnterFPSVFX;

		[SerializeField]
		private ParticleSystem _oceanRippleImpactVFX;

		[SerializeField]
		private ParticleSystem _tpsSquidVFX;

		[SerializeField]
		private ParticleSystem _fpsSquidVFX;

		[SerializeField]
		private DynamicEffect _oceanRippleImpact;

		[SerializeField]
		private ParticleTrailEmitter _oceanRippleTrail;

		[Header("Screen Space VFX")]
		[SerializeField]
		private GameObject _octopusTrapScreenVfxPrefab;

		[SerializeField]
		private GameObject _jellyfishTrapScreenVfxPrefab;

		[SerializeField]
		private GameObject _squidInkScreenVfxPrefab;

		[SerializeField]
		private GameObject _sharkTargetScreenVfxPrefab;

		[Header("SFX")]
		[SerializeField]
		private AudioSource _electrcitySFX;

		[SerializeField]
		private AudioSource _audioSourceSwimNormal;

		[SerializeField]
		private AudioSource _audioSourceSwimSprint;

		[SerializeField]
		private AudioSource _squidSFX;

		[SerializeField]
		private AudioSource _sharkChaseSFX;

		[SerializeField]
		[Min(0f)]
		private float _squidVfxMaximumDuration = 50f;

		private bool _isOceanRippleTrailActive;

		private Coroutine _squidVfxTimeoutRoutine;

		private GameObject _octopusTrapScreenVfxInstance;

		private GameObject _jellyfishTrapScreenVfxInstance;

		private GameObject _sharkTargetScreenVfxInstance;

		private readonly HashSet<ulong> _targetingSharkNetworkIds = new HashSet<ulong>();

		private const string DEFAULT_ACTION_MAP = "Player";

		[Header("Input")]
		[SerializeField]
		private PlayerInput _playerInput;

		[SerializeField]
		private SettingsProvider _settingsProvider;

		private EventBinding<GlobalChatEvent> _eventBinding;

		private readonly HashSet<PlayerUIInputOwner> _uiInputOwners = new HashSet<PlayerUIInputOwner>();

		private string _actionMapBeforeOwnedUI;

		private bool _cursorVisibleBeforeOwnedUI;

		[SerializeField]
		private FirstPersonCharacter _firstPersonCharacter;

		[SerializeField]
		private FirstPersonCharacterInput _firstPersonCharacterInput;

		[SerializeField]
		private FirstPersonCharacterLookInput _firstPersonCharacterLookInput;

		[SerializeField]
		private AnimationController _animationController;

		[SerializeField]
		private BiomeLayerType _currentBiome;

		[Header("NetworkTransforms")]
		[SerializeField]
		private NetworkTransform _networkTransform;

		[SerializeField]
		private NetworkTransform _networkTransformHead;

		[SerializeField]
		private NetworkTransform _networkTransformSpine;

		[Header("High Speed Wind Feedback")]
		[SerializeField]
		private AudioClipSO _windLoopSfx;

		[SerializeField]
		private AudioClipSO _underwaterFastLoopSfx;

		[SerializeField]
		private GameObject _highSpeedWindScreenVfxPrefab;

		[SerializeField]
		[Min(0f)]
		private float _windLoopStartSpeed = 22f;

		[SerializeField]
		[Min(0f)]
		private float _windLoopStopSpeed = 18f;

		[SerializeField]
		[Min(0.01f)]
		private float _windLoopMaxSpeed = 45f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _windLoopMinVolumeMultiplier;

		[SerializeField]
		[Range(0f, 1f)]
		private float _windLoopMaxVolumeMultiplier = 1f;

		[SerializeField]
		[Min(0.01f)]
		private float _windLoopVolumeFadeSpeed = 2f;

		[SerializeField]
		[Range(0.1f, 3f)]
		private float _windLoopMinPitch = 0.85f;

		[SerializeField]
		[Range(0.1f, 3f)]
		private float _windLoopMaxPitch = 1.2f;

		private float _cachedSwimSpeed;

		private Totti _seatedTotti;

		private Vector3 _cachedCameraParentLocalPosition;

		private bool _hasCachedCameraParentLocalPosition;

		private PlayerAttachmentMode _activeAttachmentMode;

		private NetworkObject _activeAttachmentNetworkObject;

		private uint _activeAttachmentSessionId;

		private float _nextSharkDragFeedbackTime;

		private OwnedLoopHandle _windLoopHandle;

		private AudioSource _windLoopSource;

		private AudioClipSO _activeWindLoopSfx;

		private GameObject _highSpeedWindScreenVfxInstance;

		private float _windLoopBaseVolume = 1f;

		private Vector3 _serverGameplayVelocity;

		private Vector3 _serverVelocitySamplePosition;

		private float _serverVelocitySampleTime;

		private bool _serverVelocitySampleInitialized;

		private int _baseCharacterCollisionMask;

		private bool _hasBaseCharacterCollisionMask;

		public NetworkVariable<bool> IsSwimming = new NetworkVariable<bool>(value: false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		public NetworkVariable<bool> IsTrappedState = new NetworkVariable<bool>(value: false);

		private bool _localIsTrapped;

		public GameObject graphicsObject;

		private string _lastLoadedClientProfileKey = string.Empty;

		[Header("Surf camera")]
		[SerializeField]
		private SurfCameraSettings _surfCameraSettings = new SurfCameraSettings();

		private Surfboard _surfBoard;

		private uint _surfSession;

		private readonly SurfSessionHistory _surfHistory = new SurfSessionHistory();

		private InputHandler _surfInput;

		private Vector2 _surfMove;

		private Vector2 _surfGamepadLook;

		private float _surfYaw;

		private float _surfPitch = 18f;

		private float _surfBlend;

		private float _surfSendAt;

		private CinemachineCamera _surfCamera;

		private SurfCameraRig _surfCameraRig;

		private SurfboardWorld _surfAlertWorld;

		private uint _surfLastAlertWaveId;

		private string _surfAlertHeader;

		private string _surfAlertDescription;

		private CinemachineCamera[] _surfPreviousCameras;

		private bool[] _surfPreviousCameraStates;

		private int _surfPreviousCulling;

		private bool _surfPreviousViewModel;

		private bool _surfPreviousFootIk;

		private bool _surfPreviousOxygen;

		private Vector3 _surfVisualPosition;

		private Quaternion _surfVisualRotation;

		private int _surfLayer = -1;

		private bool _surfLastStanding;

		private SurfPoseDriver _surfPoseDriver;

		private float _surfPoseFrom;

		private float _surfPoseTo;

		private double _surfPoseStarted;

		[Header("Tablet")]
		[SerializeField]
		private TabletController _tabletController;

		[SerializeField]
		private Transform _fpsTabletUIParent;

		[SerializeField]
		private GameObject _tabletFpsObject;

		[SerializeField]
		private GameObject _tabletTpsObject;

		private Coroutine _disableCor;

		private Coroutine _tabletCursorRefreshCor;

		private bool _checkTabletAnim;

		private bool _tabletExtraCheck;

		private bool _isMovementInputActive;

		[Header("Tools")]
		[SerializeField]
		private DivingEquipment _divingEquipment;

		[SerializeField]
		private HarpoonTool _harpoon;

		[SerializeField]
		private FishingRod _fishingRod;

		[SerializeField]
		private Mop _mop;

		[SerializeField]
		private AlienCompass _compass;

		[SerializeField]
		private SeaScooter _seaScooter;

		[SerializeField]
		private PoseidonTrident _poseidonTrident;

		[Header("PhysicsVolumeSFX")]
		[SerializeField]
		private AudioSource _waterVolumeEnter;

		[SerializeField]
		private AudioResource _waterJumpBigGenerator;

		[SerializeField]
		private AudioResource _waterJumpSmallGenerator;

		private RpcRelay _relay;

		private readonly NetworkVariable<bool> _isUnderwater = new NetworkVariable<bool>(value: false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		private byte _divingEquipmentEquipedLevel;

		private byte _harpoonEquipedLevel;

		private byte _fishingRodEquipedLevel;

		private byte _scooterEquipedLevel;

		private bool _hasObservedToolData;

		private ToolDataNet _observedToolData;

		public float speedThreshold = 10f;

		private bool _emoteActive;

		private bool _expressionCameraActive;

		private uint _emoteSequence;

		private const int NeutralExpressionKey = 0;

		private static readonly string[] ManualExpressionBlendShapeNames = new string[27]
		{
			"Expression_Brow_Angry_L", "Expression_Brow_Angry_R", "Expression_Brows_Lowered_L", "Expression_Brows_Lowered_R", "Expression_Brows_Raised_L", "Expression_Brows_Raised_R", "Expression_Brows_Sad_L", "Expression_Brows_Sad_R", "Expression_Eyes_LowerClosed_L", "Expression_Eyes_LowerClosed_R",
			"Expression_Eyes_LowerOpen_L", "Expression_Eyes_LowerOpen_R", "Expression_Eyes_UpperOpen_L", "Expression_Eyes_UpperOpen_R", "Expression_Jaw_Left", "Expression_Mouth_Grimace_L", "Expression_Mouth_Grimace_R", "Expression_Mouth_Happy_L", "Expression_Mouth_Happy_R", "Expression_Mouth_Oh_L",
			"Expression_Mouth_Oh_R", "Expression_Mouth_Press_L", "Expression_Mouth_Press_R", "Expression_Mouth_Sad_L", "Expression_Mouth_Sad_R", "Expression_Mouth_Wide_L", "Expression_Mouth_Wide_R"
		};

		private static readonly ExpressionBlendShape[] SmileExpression = new ExpressionBlendShape[12]
		{
			new ExpressionBlendShape("Expression_Mouth_Happy_R", 52f),
			new ExpressionBlendShape("Expression_Mouth_Happy_L", 52.6f),
			new ExpressionBlendShape("Expression_Mouth_Wide_R", 80.1f),
			new ExpressionBlendShape("Expression_Mouth_Wide_L", 80.9f),
			new ExpressionBlendShape("Expression_Mouth_Oh_R", 63f),
			new ExpressionBlendShape("Expression_Mouth_Oh_L", 62.5f),
			new ExpressionBlendShape("Expression_Brows_Raised_R", 29.3f),
			new ExpressionBlendShape("Expression_Brows_Raised_L", 28.8f),
			new ExpressionBlendShape("Expression_Brows_Sad_L", 5.9f),
			new ExpressionBlendShape("Expression_Brows_Sad_R", 6.2f),
			new ExpressionBlendShape("Expression_Eyes_LowerClosed_R", 5.9f),
			new ExpressionBlendShape("Expression_Eyes_LowerClosed_L", 6.2f)
		};

		private static readonly ExpressionBlendShape[] AngryExpression = new ExpressionBlendShape[8]
		{
			new ExpressionBlendShape("Expression_Brow_Angry_L", 51.6f),
			new ExpressionBlendShape("Expression_Brow_Angry_R", 48.7f),
			new ExpressionBlendShape("Expression_Brows_Lowered_R", 100f),
			new ExpressionBlendShape("Expression_Brows_Lowered_L", 100f),
			new ExpressionBlendShape("Expression_Mouth_Sad_R", 41.4f),
			new ExpressionBlendShape("Expression_Mouth_Sad_L", 41.8f),
			new ExpressionBlendShape("Expression_Eyes_LowerClosed_L", 9.7f),
			new ExpressionBlendShape("Expression_Eyes_LowerClosed_R", 8.9f)
		};

		private static readonly ExpressionBlendShape[] SadExpression = new ExpressionBlendShape[14]
		{
			new ExpressionBlendShape("Expression_Brows_Sad_L", 99.2f),
			new ExpressionBlendShape("Expression_Brows_Sad_R", 99.8f),
			new ExpressionBlendShape("Expression_Mouth_Sad_R", 43.8f),
			new ExpressionBlendShape("Expression_Mouth_Sad_L", 40.4f),
			new ExpressionBlendShape("Expression_Brows_Lowered_L", 100f),
			new ExpressionBlendShape("Expression_Brows_Lowered_R", 100f),
			new ExpressionBlendShape("Expression_Eyes_LowerClosed_R", 9.8f),
			new ExpressionBlendShape("Expression_Eyes_LowerClosed_L", 9.9f),
			new ExpressionBlendShape("Expression_Mouth_Grimace_L", 0f),
			new ExpressionBlendShape("Expression_Mouth_Grimace_R", 0f),
			new ExpressionBlendShape("Expression_Mouth_Oh_R", 32.2f),
			new ExpressionBlendShape("Expression_Mouth_Oh_L", 34.3f),
			new ExpressionBlendShape("Expression_Mouth_Press_R", 32.4f),
			new ExpressionBlendShape("Expression_Mouth_Press_L", 33.5f)
		};

		private static readonly ExpressionBlendShape[] SurprisedExpression = new ExpressionBlendShape[10]
		{
			new ExpressionBlendShape("Expression_Brows_Sad_R", 34.1f),
			new ExpressionBlendShape("Expression_Brows_Sad_L", 33.7f),
			new ExpressionBlendShape("Expression_Brows_Raised_R", 100f),
			new ExpressionBlendShape("Expression_Brows_Raised_L", 100f),
			new ExpressionBlendShape("Expression_Eyes_UpperOpen_R", 100f),
			new ExpressionBlendShape("Expression_Eyes_UpperOpen_L", 100f),
			new ExpressionBlendShape("Expression_Eyes_LowerOpen_R", 94.6f),
			new ExpressionBlendShape("Expression_Eyes_LowerOpen_L", 100f),
			new ExpressionBlendShape("Expression_Mouth_Oh_L", 68.7f),
			new ExpressionBlendShape("Expression_Mouth_Oh_R", 68.9f)
		};

		private static readonly ExpressionBlendShape[] DeviousExpression = new ExpressionBlendShape[14]
		{
			new ExpressionBlendShape("Expression_Eyes_LowerClosed_L", 19.7f),
			new ExpressionBlendShape("Expression_Eyes_LowerClosed_R", 17.5f),
			new ExpressionBlendShape("Expression_Mouth_Wide_R", 79.4f),
			new ExpressionBlendShape("Expression_Mouth_Wide_L", 100f),
			new ExpressionBlendShape("Expression_Mouth_Oh_R", 43f),
			new ExpressionBlendShape("Expression_Mouth_Oh_L", 43.3f),
			new ExpressionBlendShape("Expression_Mouth_Happy_R", 46.4f),
			new ExpressionBlendShape("Expression_Mouth_Happy_L", 71.3f),
			new ExpressionBlendShape("Expression_Brows_Sad_R", 34.3f),
			new ExpressionBlendShape("Expression_Brows_Sad_L", 0f),
			new ExpressionBlendShape("Expression_Brow_Angry_L", 67.7f),
			new ExpressionBlendShape("Expression_Brow_Angry_R", 74.4f),
			new ExpressionBlendShape("Expression_Mouth_Press_R", 33f),
			new ExpressionBlendShape("Expression_Mouth_Press_L", 27.3f)
		};

		private static readonly ExpressionBlendShape[] HappyExpression = new ExpressionBlendShape[17]
		{
			new ExpressionBlendShape("Expression_Eyes_LowerClosed_R", 0f),
			new ExpressionBlendShape("Expression_Eyes_LowerClosed_L", 0f),
			new ExpressionBlendShape("Expression_Mouth_Happy_R", 99.3f),
			new ExpressionBlendShape("Expression_Mouth_Happy_L", 100f),
			new ExpressionBlendShape("Expression_Mouth_Wide_R", 63.4f),
			new ExpressionBlendShape("Expression_Mouth_Wide_L", 63.2f),
			new ExpressionBlendShape("Expression_Jaw_Left", 0f),
			new ExpressionBlendShape("Expression_Eyes_UpperOpen_R", 34.6f),
			new ExpressionBlendShape("Expression_Eyes_UpperOpen_L", 33.9f),
			new ExpressionBlendShape("Expression_Eyes_LowerOpen_R", 15.7f),
			new ExpressionBlendShape("Expression_Eyes_LowerOpen_L", 16.7f),
			new ExpressionBlendShape("Expression_Mouth_Oh_R", 0f),
			new ExpressionBlendShape("Expression_Mouth_Oh_L", 0f),
			new ExpressionBlendShape("Expression_Mouth_Press_R", 0f),
			new ExpressionBlendShape("Expression_Brows_Raised_L", 88.4f),
			new ExpressionBlendShape("Expression_Brows_Sad_L", 0f),
			new ExpressionBlendShape("Expression_Brows_Raised_R", 100f)
		};

		private static readonly ExpressionBlendShape[] EvilExpression = new ExpressionBlendShape[11]
		{
			new ExpressionBlendShape("Expression_Brow_Angry_L", 100f),
			new ExpressionBlendShape("Expression_Brow_Angry_R", 100f),
			new ExpressionBlendShape("Expression_Mouth_Wide_L", 100f),
			new ExpressionBlendShape("Expression_Mouth_Wide_R", 100f),
			new ExpressionBlendShape("Expression_Mouth_Grimace_R", 22.2f),
			new ExpressionBlendShape("Expression_Mouth_Happy_L", 81.1f),
			new ExpressionBlendShape("Expression_Mouth_Happy_R", 82.3f),
			new ExpressionBlendShape("Expression_Mouth_Oh_R", 45f),
			new ExpressionBlendShape("Expression_Mouth_Oh_L", 43.8f),
			new ExpressionBlendShape("Expression_Mouth_Grimace_L", 21.4f),
			new ExpressionBlendShape("Expression_Brows_Lowered_L", 94.6f)
		};

		public Action<bool> LastToolAction;

		public Action LastEmoteAction;

		private EmoteSO _lastEmoteSO;

		private readonly NetworkVariable<byte> _manualExpressionKey = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		public Interactor Interactor => _interactor;

		public Thrower Thrower => _thrower;

		public PlayerInteraction PlayerInteraction => _playerInteractable;

		public Placer Placer => _placer;

		public PlayerRigBoneReferences RigBones => _rigBones;

		public SurfaceChecker SurfaceChecker => _surfaceChecker;

		public ParentConstraint ParentConstraint => _parentConstraint;

		public bool IsThisHost { get; private set; }

		public string Zone => ZoneName.Value.ToString();

		public SeaLayer PrimarySeaLayer => _primarySeaLayer;

		public SeaLayer CurrentSeaLayer => PrimarySeaLayer;

		public PlayerCharacterVisuals CharacterVisuals => _playerVisuals;

		public SwayHandler Sway => _sway;

		public bool IsInCage => InCage.Value;

		internal SharkCage ServerActiveSharkCage => _serverActiveSharkCage;

		public bool IsLocalGameplayPreparationReady
		{
			get
			{
				if (_criticalSetupCompleted && _localRegistrationCompleted)
				{
					return _initialToolSnapshotReady;
				}
				return false;
			}
		}

		public GameObject FPS_Character => _fpsAnimator.gameObject;

		public GameObject TPS_Character => _tpsAnimator.gameObject;

		public FootIK FootIK => _footIK;

		public bool IsUsingEmoteArea
		{
			get
			{
				if (!(CurrentEmoteArea != null) || !_staticEmoteActive)
				{
					return _swingEmoteActive;
				}
				return true;
			}
		}

		public bool IsLocomotionAnimationSuppressed => _locomotionAnimationSuppressed;

		public bool DidExitEmoteAreaThisFrame => _emoteExitInputFrame == Time.frameCount;

		public bool AllowOpenEmotesInputInActiveArea
		{
			get
			{
				if (!(CurrentEmoteArea != null) || !CurrentEmoteArea.AllowOpenEmotesInput)
				{
					if (_activeSwingArea != null)
					{
						return _activeSwingArea.AllowOpenEmotesInput;
					}
					return false;
				}
				return true;
			}
		}

		public bool CanEnterEmoteArea
		{
			get
			{
				if (CanStartControlAction(PlayerControlAction.EnterEmoteArea) && !IsUsingEmoteArea && !DidExitEmoteAreaThisFrame)
				{
					if (!(Interactor == null))
					{
						return !(Interactor.CurrentHeld is BeachBall);
					}
					return true;
				}
				return false;
			}
		}

		public Transform CameraTransform => _cinemachineCamera.transform;

		public CinemachineCamera ActionCamera => _actionCamera;

		public Transform CameraParent => _cameraParent;

		public Camera MainCamera => _viewModelCamera;

        // World-space aiming and physical gameplay must not use the hand-only overlay camera.
        public Camera GameplayCamera => _playerCamera;

		public CinemachineBrain CinemachineBrain => _cinemachineBrain;

		public LookAtConstraint LookAtConstraint => _lookAtConstraint;

		public LookAtConstraint LookAtConstraint2 => _lookAtConstraint2;

		public NfgoPlayer NfgoPlayer => _nfgoPlayer;

		public PlayerInput PlayerInput => _playerInput;

		public string CurrentMap => _playerInput.currentActionMap.id.ToString();

		public Totti SeatedTotti => _seatedTotti;

		public bool IsSharkDragActive
		{
			get
			{
				if (_activeAttachmentMode == PlayerAttachmentMode.SharkDrag)
				{
					return _activeAttachmentNetworkObject != null;
				}
				return false;
			}
		}

		public FirstPersonCharacter FirstPersonCharacter => _firstPersonCharacter;

		public FirstPersonCharacterInput FirstPersonCharacterInput => _firstPersonCharacterInput;

		public FirstPersonCharacterLookInput FirstPersonCharacterLookInput => _firstPersonCharacterLookInput;

		public Vector3 ServerGameplayVelocity
		{
			get
			{
				if (base.IsServer && base.IsOwner && _firstPersonCharacter != null && _firstPersonCharacter.characterMovement != null)
				{
					return _firstPersonCharacter.characterMovement.velocity;
				}
				return _serverGameplayVelocity;
			}
		}

		public BiomeLayerType CurrentBiome => _currentBiome;

		public bool IsTrapped
		{
			get
			{
				if (!_localIsTrapped)
				{
					return IsTrappedState.Value;
				}
				return true;
			}
			set
			{
				_localIsTrapped = value;
				if (value && base.IsOwner)
				{
					_interactor?.ResetEmptyAnims();
				}
				if (!base.IsSpawned || base.IsServer)
				{
					IsTrappedState.Value = value;
				}
			}
		}

		public GameObject graphicsClone { get; set; }

		public Vector3 previousOffsetFromPortal { get; set; }

		public Material[] originalMaterials { get; set; }

		public Material[] cloneMaterials { get; set; }

		public bool IsSurfing => _surfBoard != null;

		public bool HasSurfboard
		{
			get
			{
				if (IsSurfing)
				{
					return true;
				}
				if (base.IsOwner && Singleton<QuickSlotHandler>.Instance != null && Singleton<QuickSlotHandler>.Instance.HeldSlotElement != null && Singleton<QuickSlotHandler>.Instance.HeldSlotElement.HeldItem is Surfboard)
				{
					return true;
				}
				if (base.IsServer)
				{
					foreach (Surfboard instance in Surfboard.Instances)
					{
						if (instance != null && instance.IsSpawned && instance.Holder == base.OwnerClientId)
						{
							return true;
						}
					}
				}
				return false;
			}
		}

		public bool CanBeginSurf
		{
			get
			{
				if (_activeAttachmentMode == PlayerAttachmentMode.None && !IsTrapped && (_divingEquipment == null || !_divingEquipment.IsKnocked) && !_emoteActive && !_staticEmoteActive && !_swingEmoteActive)
				{
					if (base.IsOwner)
					{
						if (_uiInputOwners.Count == 0)
						{
							if (!(_tabletController == null))
							{
								return !_tabletController.IsTabletActive;
							}
							return true;
						}
						return false;
					}
					return true;
				}
				return false;
			}
		}

		private bool SurfInputAllowed
		{
			get
			{
				if (base.IsOwner && _uiInputOwners.Count == 0 && _playerInput != null && _playerInput.currentActionMap != null)
				{
					return _playerInput.currentActionMap.name == "Player";
				}
				return false;
			}
		}

		public bool IsTabletActive { get; private set; }

		public TabletController TabletController => _tabletController;

		public ToolDataNet ToolsData => _relay.ToolDataNV.Value;

		public byte EquippedDivingEquipmentLevel => _divingEquipmentEquipedLevel;

		public byte EquippedHarpoonLevel => _harpoonEquipedLevel;

		public Mop Mop => _mop;

		public SeaScooter SeaScooter => _seaScooter;

		public DivingEquipment DivingEquipment => _divingEquipment;

		public HarpoonTool Harpoon => _harpoon;

		public FishingRod FishingRod => _fishingRod;

		public AlienCompass Compass => _compass;

		public PoseidonTrident PoseidonTrident => _poseidonTrident;

		public BaseTool ActiveTool { get; private set; }

		public bool IsToolActive
		{
			get
			{
				if (!_mop.ToolStatus && !_harpoon.ToolStatus && !_fishingRod.ToolStatus && !_compass.ToolStatus && !_seaScooter.ToolStatus)
				{
					if (_poseidonTrident != null)
					{
						return _poseidonTrident.ToolStatus;
					}
					return false;
				}
				return true;
			}
		}

		public bool CanDisableActiveTool
		{
			get
			{
				if (IsToolActive && !(ActiveTool == null))
				{
					return ActiveTool.DisableIf();
				}
				return true;
			}
		}

		public bool CanCloseActiveToolFromShortcut
		{
			get
			{
				if (IsToolActive && !(ActiveTool == null))
				{
					return ActiveTool.DisableIf();
				}
				return true;
			}
		}

		public bool CanChangeTool
		{
			get
			{
				if (!_harpoon.IsChallenge && !_fishingRod.IsFishingActive && !IsTrapped)
				{
					return !IsUsingEmoteArea;
				}
				return false;
			}
		}

		public bool IsUnderWater => _isUnderwater.Value;

		public event Action<bool> UnderwaterStateChanged;

		public void SetPrimarySeaLayer(SeaLayer seaLayer)
		{
			if (!(_primarySeaLayer == seaLayer))
			{
				_primarySeaLayer = seaLayer;
				if (base.IsOwner)
				{
					_divingEquipment?.HandleOwnerPrimarySeaLayerChanged();
				}
			}
		}

		public void ClearPrimarySeaLayer(SeaLayer seaLayer)
		{
			if (!(_primarySeaLayer != seaLayer))
			{
				_primarySeaLayer = null;
				if (base.IsOwner)
				{
					_divingEquipment?.HandleOwnerPrimarySeaLayerChanged();
				}
			}
		}

		internal void ServerEnterSharkCage(SharkCage cage)
		{
			if (!base.IsServer || cage == null)
			{
				return;
			}
			if (_serverActiveSharkCage == cage)
			{
				InCage.Value = true;
				return;
			}
			if (_serverActiveSharkCage != null)
			{
				_serverActiveSharkCage.ServerRemovePlayer(this);
			}
			_serverActiveSharkCage = cage;
			InCage.Value = true;
		}

		internal void ServerExitSharkCage(SharkCage cage)
		{
			if (base.IsServer && !(cage == null) && !(_serverActiveSharkCage != cage))
			{
				InCage.Value = false;
				_serverActiveSharkCage = null;
			}
		}

		internal void ServerSetTrapRecoveryOwner(IPlayerTrapRecoveryOwner owner)
		{
			if (base.IsServer)
			{
				_serverTrapRecoveryOwner = owner;
			}
		}

		internal void ServerClearTrapRecoveryOwner(IPlayerTrapRecoveryOwner owner)
		{
			if (base.IsServer && _serverTrapRecoveryOwner == owner)
			{
				_serverTrapRecoveryOwner = null;
			}
		}

		private void Awake()
		{
			ResolveCoreReferences();
			NetworkVariable<FixedString64Bytes> playerName = PlayerName;
			playerName.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Combine(playerName.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(OnPlayerNameChanged));
			NetworkVariable<ulong> steamID = SteamID;
			steamID.OnValueChanged = (NetworkVariable<ulong>.OnValueChangedDelegate)Delegate.Combine(steamID.OnValueChanged, new NetworkVariable<ulong>.OnValueChangedDelegate(OnSteamIDChanged));
			NetworkVariable<FixedString128Bytes> nV_PlayerId = _nfgoPlayer.NV_PlayerId;
			nV_PlayerId.OnValueChanged = (NetworkVariable<FixedString128Bytes>.OnValueChangedDelegate)Delegate.Combine(nV_PlayerId.OnValueChanged, new NetworkVariable<FixedString128Bytes>.OnValueChangedDelegate(DissonancePlayerIdChanged));
			NetworkVariable<bool> isUnderwater = _isUnderwater;
			isUnderwater.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isUnderwater.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnUnderwaterStateChanged));
			NetworkVariable<byte> manualExpressionKey = _manualExpressionKey;
			manualExpressionKey.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(manualExpressionKey.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnManualExpressionKeyChanged));
			_character = GetComponent<Character>();
		}

		private void ResolveCoreReferences()
		{
			if (_playerVisuals == null)
			{
				_playerVisuals = GetComponentInChildren<PlayerCharacterVisuals>(includeInactive: true);
			}
			if (_playerInteractable == null)
			{
				_playerInteractable = GetComponentInChildren<PlayerInteraction>(includeInactive: true);
			}
			if (_surfaceChecker == null)
			{
				_surfaceChecker = GetComponentInChildren<SurfaceChecker>(includeInactive: true);
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			SushiAndFish.FishingIntegration.PlayerResumeController.Ensure(this);
			_spawnGeneration++;
			_waitingForBootstrapCoordinator = false;
			StopCustomizationOperation();
			StopCustomizationSnapshotRetry();
			_localGameplayReadyReported = (_criticalSetupCompleted = (_localRegistrationCompleted = false));
			_initialToolSnapshotReady = false;
			_clientRegistrationCompleted = (_localCustomizationApplyStarted = (_localCustomizationApplyFinished = (_isApplyingRemoteCustomization = false)));
			_bootstrapSpawnReported = (_localIdentityPublished = (_localCustomizationPayloadPublished = false));
			_customizationOperation = new PlayerPreparationOperation(MultiplayerDiagnostics.CurrentAttempt, (Singleton<MultiplayerCoordinator>.Instance != null) ? Singleton<MultiplayerCoordinator>.Instance.SessionEpoch : 0, base.OwnerClientId, base.NetworkObjectId, MultiplayerDiagnostics.Now);
			RunInitializationStage("Spawn.ResolveReferences", ResolveCoreReferences);
			RunInitializationStage("Spawn.ResolveRpcRelay", TryResolveRpcRelay);
			InitializeCriticalNetworkPresentation();
			RunInitializationStage("Spawn.SetupPlayer", SetupPlayer);
			RunInitializationStage("Spawn.TrappedStateSubscription", () =>
			{
				NetworkVariable<bool> isTrappedState = IsTrappedState;
				isTrappedState.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isTrappedState.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnIsTrappedStateChanged));
			});
		}

		private void TryResolveRpcRelay()
		{
			if (!(_relay != null))
			{
				GameProgressManager instance = Singleton<GameProgressManager>.Instance;
				if (instance != null)
				{
					_relay = instance.RpcRelay;
				}
			}
		}

		private void InitializeCriticalNetworkPresentation()
		{
			if (base.IsOwner)
			{
				RunInitializationStage("Critical.Identity", PublishLocalIdentity);
				RunInitializationStage("Critical.BootstrapSpawn", ReportLocalPlayerSpawnedForBootstrap);
				return;
			}
			RunInitializationStage("Critical.RemoteName", () =>
			{
				SetPlayerName(PlayerName.Value.ToString());
			});
			RunInitializationStage("Critical.CustomizationSnapshot", RequestCustomizationSnapshotIfNeeded);
		}

		private void PublishLocalIdentity()
		{
			if (!_localIdentityPublished && base.IsOwner && !(base.NetworkManager == null))
			{
				if (Netcode.Transports.Local.OfflineSingleplayerMode.Enabled)
				{
					PlayerName.Value = "Player";
					SteamID.Value = Netcode.Transports.Local.OfflineSingleplayerMode.LocalUserId;
				}
				else if (base.NetworkManager.TryGetComponent<FacepunchTransport>(out var _) && SteamClient.IsValid)
				{
					string text = SteamClient.Name;
					PlayerName.Value = (string.IsNullOrWhiteSpace(text) ? $"Client {base.OwnerClientId}" : text);
					SteamID.Value = SteamClient.SteamId;
				}
				else
				{
					PlayerName.Value = $"Client {base.OwnerClientId}";
					SteamID.Value = base.NetworkManager.LocalClientId;
				}
				_localIdentityPublished = true;
			}
		}

		private void ReportLocalPlayerSpawnedForBootstrap()
		{
			if (!_bootstrapSpawnReported && base.IsOwner)
			{
				if (Singleton<JoinBootstrapCoordinator>.TryGetInstance(out var instance))
				{
					_bootstrapSpawnReported = true;
					instance.NotifyLocalPlayerSpawned(this);
				}
				else if (_bootstrapSpawnRetryCoroutine == null)
				{
					_bootstrapSpawnRetryCoroutine = StartCoroutine(CoReportLocalPlayerSpawnedWhenCoordinatorExists());
				}
			}
		}

		private IEnumerator CoReportLocalPlayerSpawnedWhenCoordinatorExists()
		{
			JoinBootstrapCoordinator instance;
			while (base.IsOwner && base.IsSpawned && !Singleton<JoinBootstrapCoordinator>.TryGetInstance(out instance))
			{
				yield return null;
			}
			_bootstrapSpawnRetryCoroutine = null;
			if (base.IsOwner && base.IsSpawned)
			{
				ReportLocalPlayerSpawnedForBootstrap();
			}
		}

		public void SetupPlayer()
		{
			bool flag = RunInitializationStage("Setup.Camera", SetupCamera);
			RunInitializationStage("Setup.Canvas", SetupCanvas);
			flag &= RunInitializationStage("Setup.Input", SetupInput);
			flag &= RunInitializationStage("Setup.Movement", SetupMovement);
			RunInitializationStage("Setup.FX", SetupFX);
			RunInitializationStage("Setup.Animation", SetupAnim);
			RunInitializationStage("Setup.Tablet", SetupTablet);
			RunInitializationStage("Setup.Presentation", () =>
			{
				UtilityMethods.SetLayerRecursive(base.gameObject, base.IsOwner ? "Player" : "ClientPlayer", includeChildren: false);
				_playerVisuals?.RefreshForOwner(base.IsOwner);
				if (_interactor != null)
				{
					_interactor.enabled = true;
				}
				if (_placer != null)
				{
					_placer.enabled = base.IsOwner;
				}
				if (_thrower != null)
				{
					_thrower.enabled = base.IsOwner;
				}
				if (_surfaceChecker != null)
				{
					_surfaceChecker.Initialize(_firstPersonCharacterInput);
				}
			});
			IsThisHost = base.OwnerClientId == 0;
			RunInitializationStage("Setup.ToolEvents", () =>
			{
				_toolDataBinding = new EventBinding<ToolDataEvent>(OnToolDataNVChanged);
				EventBus<ToolDataEvent>.Register(_toolDataBinding);
			});
			_initialToolSnapshotReady = RunInitializationStage("Setup.ToolSnapshot", () =>
			{
				TryResolveRpcRelay();
				if (_relay == null)
				{
					throw new InvalidOperationException("RpcRelay is unavailable while applying the initial tool snapshot.");
				}
				ApplyCurrentToolDataSnapshot();
			});
			flag &= RunInitializationStage("Registration.PlayersManager", () =>
			{
				PlayersManager instance = Singleton<PlayersManager>.Instance;
				if (instance == null)
				{
					throw new InvalidOperationException("PlayersManager is unavailable while registering the network player.");
				}
				instance.RegisterPlayer(this);
			});
			RunInitializationStage("Setup.HeldPresentation", () =>
			{
				_interactor?.ClientReconcileHeldPresentation();
			});
			RunInitializationStage("Setup.TemporaryAppearance", () =>
			{
				_playerVisuals?.SetCustomizationPending(pending: true);
			});
			if (base.IsOwner)
			{
				flag &= RunInitializationStage("Setup.ValidateGameplay", () =>
				{
					if (_playerCamera == null || !_playerCamera.enabled || _playerInput == null || _playerInput.actions == null || _firstPersonCharacterInput == null || _firstPersonCharacterLookInput == null || _firstPersonCharacter == null || _firstPersonCharacter.characterMovement == null || !_hasBaseCharacterCollisionMask || !_localRegistrationCompleted || Singleton<PlayersManager>.Instance == null || Singleton<PlayersManager>.Instance.LocalPlayer != this)
					{
						throw new InvalidOperationException("Required camera, input, collision or local player registration is incomplete.");
					}
				});
				_criticalSetupCompleted = flag;
				NotifyLocalGameplayReadyForBootstrap();
			}
			UtilityMethods.Log($"[PPG_Player] {base.name} IsThisHost: {IsThisHost}");
			if (base.IsOwner)
			{
				StartLocalCustomizationApply();
				StartCoroutine(CoRequestBootstrapWhenReady());
				StartCoroutine(CoReconcileInitialLocomotion());
			}
		}

		private bool RunInitializationStage(string stage, Action action)
		{
			try
			{
				action?.Invoke();
				return true;
			}
			catch (Exception exception)
			{
				ReportPlayerInitializationFailure(stage, exception);
				return false;
			}
		}

		private void ReportPlayerInitializationFailure(string stage, Exception exception)
		{
			UtilityMethods.Log($"[PlayerInitialization] Stage failed. Stage:{stage} Owner:{base.OwnerClientId} " + $"NetworkObject:{base.NetworkObjectId} IsOwner:{base.IsOwner} IsServer:{base.IsServer} Exception:{exception}", LogType.Error);
			_reportedInitializationFailureStages.Add(stage);
		}

		private IEnumerator CoReconcileInitialLocomotion()
		{
			if (_initialLocomotionReconciled)
			{
				yield break;
			}
			_initialLocomotionReconciled = true;
			yield return null;
			yield return new WaitForFixedUpdate();
			if (base.IsOwner && base.IsSpawned && !(_firstPersonCharacter == null))
			{
				_firstPersonCharacter.TeleportPosition(base.transform.position, interpolating: false, updateGround: true);
				if (_firstPersonCharacter.IsInWaterPhysicsVolume())
				{
					UtilityMethods.Log($"[PPG_Player] Initial locomotion preserved in water. ClientId={base.OwnerClientId}, " + $"Mode={_firstPersonCharacter.movementMode}");
				}
				else if (_firstPersonCharacter.movementMode == Character.MovementMode.Swimming || IsSwimming.Value)
				{
					OnPhysicsVolumeChanged(null);
					_firstPersonCharacter.SetMovementMode(Character.MovementMode.Falling);
					UtilityMethods.Log($"[PPG_Player] Initial locomotion reconciled to falling. ClientId={base.OwnerClientId}");
				}
			}
		}

		private IEnumerator CoRequestBootstrapWhenReady()
		{
			uint generation = _spawnGeneration;
			yield return null;
			while (generation == _spawnGeneration && base.IsOwner && base.IsSpawned)
			{
				if (_relay == null && Singleton<GameProgressManager>.Instance != null)
				{
					_relay = Singleton<GameProgressManager>.Instance.RpcRelay;
				}
				if (_relay != null && _relay.IsSpawned && base.NetworkManager != null && base.IsSpawned)
				{
					break;
				}
				yield return null;
			}
			if (generation != _spawnGeneration || !base.IsOwner || !base.IsSpawned || (Singleton<GameSessionLifecycle>.TryGetInstance(out var instance) && instance.ReturnToMenuInProgress))
			{
				yield break;
			}
			if (_relay == null || !_relay.IsSpawned || base.NetworkManager == null || !base.IsSpawned)
			{
				UtilityMethods.Log("[PPG_Player] Bootstrap request cancelled because the player despawned before RpcRelay became ready.", LogType.Warning);
				yield break;
			}
			if (!_initialToolSnapshotReady)
			{
				_initialToolSnapshotReady = RunInitializationStage("Setup.DeferredToolSnapshot", ApplyCurrentToolDataSnapshot);
			}
			NotifyLocalGameplayReadyForBootstrap();
			Singleton<JoinBootstrapCoordinator>.Instance?.NotifyBootstrapRequested();
			_relay.ServerRequestBootstrapRpc((Singleton<MultiplayerCoordinator>.Instance != null) ? Singleton<MultiplayerCoordinator>.Instance.SessionEpoch : 0);
		}

		public void LocalRegistered()
		{
			if (_localRegistrationCompleted)
			{
				return;
			}
			bool flag = RunInitializationStage("LocalRegistration.Input", () =>
			{
				Singleton<ToolTipHandler>.Instance?.Initialize(base.transform);
				Singleton<QuickSlotHandler>.Instance?.Initialize(this);
				_interactor?.RegisterInput();
				_firstPersonCharacterInput?.Register(this);
				_firstPersonCharacterLookInput?.Register();
				RegisterWheelEvents();
				RegisterToolEvents();
				RegisterTabletEvents();
				RegisterAnimEvents();
			});
			RunInitializationStage("LocalRegistration.Settings", () =>
			{
				BindCamShakeConnection();
				BindRenderDistanceConnection();
				BindCameraComfortSettings();
				if (Singleton<InputManager>.Instance?.InputHandler != null && Singleton<GameUIManager>.Instance?.PauseScreen != null)
				{
					Singleton<InputManager>.Instance.InputHandler.OpenSettings += Singleton<GameUIManager>.Instance.PauseScreen.OnOpenSettings;
				}
				if (Singleton<OceanFXManager>.Instance != null)
				{
					Singleton<OceanFXManager>.Instance.Player = this;
				}
			});
			PublishLocalIdentity();
			if (IsThisHost)
			{
				flag &= RunInitializationStage("LocalRegistration.HostSave", () =>
				{
					Singleton<SaveManager>.Instance.SaveHostSteamId(SteamID.Value.ToString());
					if (Singleton<SaveManager>.Instance.TryGetCurrentSaveSlot(out var data))
					{
						if (data.PlayerData != null)
						{
							Load(data.PlayerData);
						}
						else
						{
							UtilityMethods.Log("[PPG_Player] Host PlayerData is null. Load skipped.", LogType.Warning);
						}
						return;
					}
					throw new InvalidOperationException("Host save slot is invalid during required player preparation.");
				});
			}
			UtilityMethods.Log("[PPG_Player] ID: " + base.OwnerClientId + " host? " + IsThisHost);
			RunInitializationStage("LocalRegistration.Services", () =>
			{
				_tabletController?.Initialize(this);
				Singleton<DissonanceManager>.Instance?.Init(this);
			});
			_localRegistrationCompleted = flag;
			if (flag)
			{
				RunInitializationStage("LocalRegistration.ReadyEvent", () =>
				{
					EventBus<LocalPlayerReadyEvent>.Raise(new LocalPlayerReadyEvent
					{
						LocalPlayer = this
					});
				});
			}
		}

		public void ClientRegistered()
		{
			if (_clientRegistrationCompleted)
			{
				return;
			}
			ResolveCoreReferences();
			RunInitializationStage("ClientRegistration.References", () =>
			{
				if (!(_playerInteractable != null))
				{
					throw new InvalidOperationException("ClientRegistered is missing PlayerInteraction.");
				}
				_playerInteractable.OwnerPlayer = this;
				if (_playerVisuals == null || _playerVisuals.TpsOutfitSystem == null)
				{
					throw new InvalidOperationException("ClientRegistered is missing PlayerCharacterVisuals or TPS OutfitSystem.");
				}
				RegisterDissonanceFaceRendererEvents(_playerVisuals.TpsOutfitSystem);
				RefreshDissonanceHeadRenderer();
			});
			RunInitializationStage("ClientRegistration.State", () =>
			{
				ApplyUnderwaterState(_isUnderwater.Value);
			});
			if (!base.IsOwner)
			{
				SetPlayerName(PlayerName.Value.ToString());
				RequestCustomizationSnapshotIfNeeded();
				RunInitializationStage("ClientRegistration.Voice", SetupVoiceState);
				if (base.IsServer)
				{
					RunInitializationStage("ClientRegistration.ServerTools", () =>
					{
						_relay?.ServerApplyToolsFromSave();
					});
				}
			}
			_clientRegistrationCompleted = true;
		}

		private void PublishLocalCustomizationPayload()
		{
			if (_localCustomizationPayloadPublished || !base.IsOwner)
			{
				return;
			}
			PlayerCustomizationSession.EnsureInitialCharacterData();
			if (!PlayerCustomizationSession.TryGetCurrentCharacterDataPayload(out var payload) || !IsValidCustomizationPayload(payload, "local submit"))
			{
				UtilityMethods.Log("[PlayerCustomizationSync] Local customization payload was unavailable.", LogType.Warning);
				return;
			}
			if (base.IsServer)
			{
				StoreAndBroadcastCustomizationPayload(payload);
			}
			else
			{
				ServerSubmitCustomizationRpc(payload);
			}
			_localCustomizationPayloadPublished = true;
		}

		private void StartLocalCustomizationApply()
		{
			if (!_localCustomizationApplyStarted && base.IsOwner)
			{
				_localCustomizationApplyStarted = true;
				MultiplayerDiagnostics.Record("customization_begin", "", base.OwnerClientId);
				ApplyLocalCustomizationAsync(_customizationOperation).Forget();
			}
		}

		private async UniTaskVoid ApplyLocalCustomizationAsync(PlayerPreparationOperation operation)
		{
			if (!base.IsOwner || operation == null)
			{
				return;
			}
			long diagnosticAttempt = operation.Attempt;
			bool applied = false;
			try
			{
				await UniTask.Yield(PlayerLoopTiming.Update, operation.Token);
				if (IsCurrentCustomizationOperation(operation))
				{
					PublishLocalCustomizationPayload();
					applied = await PlayerCustomizationSession.ApplyToLocalPlayerAsync(this, operation.Token);
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex2)
			{
				ReportPlayerInitializationFailure("Customization.LocalApply", ex2);
				MultiplayerDiagnostics.Record("customization_exception", $"sourceAttempt={diagnosticAttempt} exception={ex2.GetType().Name}", base.OwnerClientId, critical: true);
			}
			finally
			{
				if (_customizationOperation == operation)
				{
					_localCustomizationApplyFinished = true;
					MultiplayerDiagnostics.Record("customization_finished", string.Format("sourceAttempt={0} applied={1} stop={2}", diagnosticAttempt, applied, operation.StopReason ?? "None"), base.OwnerClientId);
					if (applied && IsCurrentCustomizationOperation(operation))
					{
						_playerVisuals?.SetCustomizationPending(pending: false);
					}
				}
			}
		}

		private void NotifyLocalGameplayReadyForBootstrap()
		{
			if (_localGameplayReadyReported || !base.IsOwner || !base.IsSpawned || !IsLocalGameplayPreparationReady || (Singleton<GameSessionLifecycle>.TryGetInstance(out var instance) && instance.ReturnToMenuInProgress))
			{
				return;
			}
			JoinBootstrapCoordinator instance2 = Singleton<JoinBootstrapCoordinator>.Instance;
			if (instance2 == null)
			{
				if (!_waitingForBootstrapCoordinator)
				{
					StartCoroutine(CoNotifyLocalGameplayReadyWhenCoordinatorExists());
				}
			}
			else
			{
				_localGameplayReadyReported = true;
				ReportLocalPlayerSpawnedForBootstrap();
				MultiplayerDiagnostics.Record("local_required_preparation_ready", "cosmeticsRequired=False", base.OwnerClientId);
				instance2.NotifyLocalPlayerRegistered(this);
			}
		}

		private IEnumerator CoNotifyLocalGameplayReadyWhenCoordinatorExists()
		{
			uint generation = _spawnGeneration;
			_waitingForBootstrapCoordinator = true;
			JoinBootstrapCoordinator instance;
			while (generation == _spawnGeneration && base.IsOwner && base.IsSpawned && !Singleton<JoinBootstrapCoordinator>.TryGetInstance(out instance))
			{
				yield return null;
			}
			if (generation == _spawnGeneration)
			{
				_waitingForBootstrapCoordinator = false;
				if (base.IsOwner && base.IsSpawned)
				{
					NotifyLocalGameplayReadyForBootstrap();
				}
			}
		}

		private void RequestCustomizationSnapshotIfNeeded()
		{
			if (!base.IsOwner && base.IsSpawned && (!base.IsServer || base.IsClient) && !_remoteCustomizationApplied && _customizationSnapshotRetryCoroutine == null && IsCurrentCustomizationOperation(_customizationOperation))
			{
				_customizationSnapshotRetryCoroutine = StartCoroutine(CoRequestCustomizationSnapshotWithRetry());
			}
		}

		private IEnumerator CoRequestCustomizationSnapshotWithRetry()
		{
			PlayerPreparationOperation operation = _customizationOperation;
			int attempt = 0;
			while (!base.IsOwner && IsCurrentCustomizationOperation(operation) && !_remoteCustomizationApplied)
			{
				attempt++;
				ServerRequestCustomizationSnapshotRpc();
				if (_remoteCustomizationApplied)
				{
					break;
				}
				if (!_customizationSnapshotWaitReported && attempt >= 5)
				{
					_customizationSnapshotWaitReported = true;
					UtilityMethods.Log("[PlayerCustomizationSync] Remote customization is still pending; background retry will continue. " + $"Owner:{base.OwnerClientId} Attempts:{attempt}", LogType.Warning);
					SendCustomizationDiagnostic("SnapshotPending", attempt);
				}
				yield return new WaitForSecondsRealtime(2f);
			}
			if (operation == _customizationOperation)
			{
				_customizationSnapshotRetryCoroutine = null;
			}
		}

		private void StopCustomizationSnapshotRetry()
		{
			if (_customizationSnapshotRetryCoroutine != null)
			{
				StopCoroutine(_customizationSnapshotRetryCoroutine);
				_customizationSnapshotRetryCoroutine = null;
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerSubmitCustomizationRpc(byte[] payload, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(373888146u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bool value = payload != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(payload, default(FastBufferWriter.ForPrimitives));
				}
				__endSendRpc(ref bufferWriter, 373888146u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
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
			if (rpcParams.Receive.SenderClientId != base.OwnerClientId)
			{
				UtilityMethods.Log($"[PlayerCustomizationSync] Rejected customization payload. Sender:{rpcParams.Receive.SenderClientId} Owner:{base.OwnerClientId}", LogType.Warning);
			}
			else if (IsValidCustomizationPayload(payload, "server submit"))
			{
				if (!PlayerCustomizationSession.TryReadCharacterDataPayload(payload, out var _))
				{
					UtilityMethods.Log($"[PlayerCustomizationSync] Rejected malformed customization payload. Owner:{base.OwnerClientId}", LogType.Warning);
				}
				else
				{
					StoreAndBroadcastCustomizationPayload(payload);
				}
			}
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
		private void ServerRequestCustomizationSnapshotRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2135905389u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2135905389u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && _lastCustomizationPayload != null && _lastCustomizationPayload.Length != 0 && IsValidCustomizationPayload(_lastCustomizationPayload, "snapshot"))
				{
					ClientApplyCustomizationSnapshotRpc(base.OwnerClientId, ClonePayload(_lastCustomizationPayload), base.RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
				}
			}
		}

		private void StoreAndBroadcastCustomizationPayload(byte[] payload)
		{
			_lastCustomizationPayload = ClonePayload(payload);
			ClientApplyCustomizationRpc(base.OwnerClientId, ClonePayload(payload));
		}

		[Rpc(SendTo.NotOwner)]
		private void ClientApplyCustomizationRpc(ulong ownerClientId, byte[] payload)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3023977014u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, ownerClientId);
				bool value = payload != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(payload, default(FastBufferWriter.ForPrimitives));
				}
				__endSendRpc(ref bufferWriter, 3023977014u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ApplyRemoteCustomizationPayload(ownerClientId, payload);
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientApplyCustomizationSnapshotRpc(ulong ownerClientId, byte[] payload, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2197951382u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, ownerClientId);
				bool value = payload != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(payload, default(FastBufferWriter.ForPrimitives));
				}
				__endSendRpc(ref bufferWriter, 2197951382u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ApplyRemoteCustomizationPayload(ownerClientId, payload);
			}
		}

		private void ApplyRemoteCustomizationPayload(ulong ownerClientId, byte[] payload)
		{
			if (!base.IsOwner && ownerClientId == base.OwnerClientId && IsValidCustomizationPayload(payload, "remote apply") && (!_remoteCustomizationApplied || !PayloadEquals(_lastAppliedRemoteCustomizationPayload, payload)))
			{
				if (_remoteCustomizationApplied)
				{
					StopCustomizationOperation();
					_customizationOperation = new PlayerPreparationOperation(MultiplayerDiagnostics.CurrentAttempt, (Singleton<MultiplayerCoordinator>.Instance != null) ? Singleton<MultiplayerCoordinator>.Instance.SessionEpoch : 0, base.OwnerClientId, base.NetworkObjectId, MultiplayerDiagnostics.Now);
					_playerVisuals?.SetCustomizationPending(pending: true);
				}
				_remoteCustomizationApplied = false;
				_pendingRemoteCustomizationPayload = ClonePayload(payload);
				if (!_isApplyingRemoteCustomization)
				{
					ApplyPendingRemoteCustomizationAsync().Forget();
				}
			}
		}

		private async UniTaskVoid ApplyPendingRemoteCustomizationAsync()
		{
			PlayerPreparationOperation operation = _customizationOperation;
			if (_isApplyingRemoteCustomization || !IsCurrentCustomizationOperation(operation))
			{
				return;
			}
			_isApplyingRemoteCustomization = true;
			try
			{
				await UniTask.Yield(PlayerLoopTiming.Update, operation.Token);
				byte[] payload = null;
				int attempt = 0;
				bool applyFailureReported = false;
				while (!base.IsOwner && IsCurrentCustomizationOperation(operation))
				{
					if (_pendingRemoteCustomizationPayload != null)
					{
						if (!PayloadEquals(payload, _pendingRemoteCustomizationPayload))
						{
							payload = _pendingRemoteCustomizationPayload;
							attempt = 0;
							applyFailureReported = false;
						}
						_pendingRemoteCustomizationPayload = null;
					}
					if (payload == null)
					{
						break;
					}
					attempt++;
					bool flag;
					try
					{
						flag = await PlayerCustomizationSession.ApplyToRemotePlayerAsync(this, payload, operation.Token);
					}
					catch (OperationCanceledException)
					{
						break;
					}
					catch (Exception exception)
					{
						flag = false;
						ReportPlayerInitializationFailure("Customization.RemoteApply", exception);
					}
					if (!IsCurrentCustomizationOperation(operation) || base.IsOwner)
					{
						break;
					}
					if (flag)
					{
						_lastAppliedRemoteCustomizationPayload = ClonePayload(payload);
						_remoteCustomizationApplied = true;
						_customizationSnapshotWaitReported = false;
						StopCustomizationSnapshotRetry();
						if (_pendingRemoteCustomizationPayload == null || PayloadEquals(payload, _pendingRemoteCustomizationPayload))
						{
							_pendingRemoteCustomizationPayload = null;
							_playerVisuals?.SetCustomizationPending(pending: false);
							UtilityMethods.Log("[PlayerCustomizationSync] Remote customization applied. " + $"Owner:{base.OwnerClientId} Attempts:{attempt}");
							if (attempt > 1)
							{
								SendCustomizationDiagnostic("ApplyRecovered", attempt);
							}
							break;
						}
						_remoteCustomizationApplied = false;
						payload = null;
					}
					else
					{
						_remoteCustomizationApplied = false;
						RequestCustomizationSnapshotIfNeeded();
						if (!applyFailureReported && attempt >= 3)
						{
							applyFailureReported = true;
							UtilityMethods.Log("[PlayerCustomizationSync] Remote customization apply is still pending; background retry will continue. " + $"Owner:{base.OwnerClientId} Attempts:{attempt}", LogType.Warning);
							SendCustomizationDiagnostic("ApplyPending", attempt);
						}
						if (PayloadEquals(payload, _pendingRemoteCustomizationPayload))
						{
							_pendingRemoteCustomizationPayload = null;
						}
						await UniTask.Delay(Mathf.Min(attempt, 5) * 1000, ignoreTimeScale: true, PlayerLoopTiming.Update, operation.Token);
					}
				}
			}
			catch (OperationCanceledException)
			{
			}
			finally
			{
				if (operation == _customizationOperation)
				{
					_isApplyingRemoteCustomization = false;
					if (!base.IsOwner && IsCurrentCustomizationOperation(operation) && _pendingRemoteCustomizationPayload != null)
					{
						ApplyPendingRemoteCustomizationAsync().Forget();
					}
				}
			}
		}

		private bool IsCurrentCustomizationOperation(PlayerPreparationOperation operation)
		{
			if (operation == null || operation != _customizationOperation)
			{
				return false;
			}
			bool current = this != null && base.IsSpawned && base.OwnerClientId == operation.OwnerId && base.NetworkObjectId == operation.ObjectId && MultiplayerDiagnostics.CurrentAttempt == operation.Attempt && Singleton<MultiplayerCoordinator>.TryGetInstance(out var instance) && instance.SessionEpoch == operation.SessionEpoch && (!Singleton<GameSessionLifecycle>.TryGetInstance(out var instance2) || !instance2.ReturnToMenuInProgress);
			bool isCancellationRequested = operation.Token.IsCancellationRequested;
			bool num = operation.Check(MultiplayerDiagnostics.Now, current);
			if (!num && !isCancellationRequested)
			{
				MultiplayerDiagnostics.Record("customization_stopped", $"sourceAttempt={operation.Attempt} reason={operation.StopReason}", base.OwnerClientId);
			}
			return num;
		}

		private void StopCustomizationOperation()
		{
			PlayerPreparationOperation customizationOperation = _customizationOperation;
			_customizationOperation = null;
			customizationOperation?.Dispose();
		}

		private void SendCustomizationDiagnostic(string state, int attempt)
		{
		}

		private static bool IsValidCustomizationPayload(byte[] payload, string context)
		{
			if (payload == null || payload.Length == 0)
			{
				return false;
			}
			if (payload.Length <= 131072)
			{
				return true;
			}
			UtilityMethods.Log($"[PlayerCustomizationSync] Ignored oversized customization payload. Context:{context} Bytes:{payload.Length}", LogType.Warning);
			return false;
		}

		private static byte[] ClonePayload(byte[] payload)
		{
			if (payload == null)
			{
				return null;
			}
			byte[] array = new byte[payload.Length];
			Buffer.BlockCopy(payload, 0, array, 0, payload.Length);
			return array;
		}

		private static bool PayloadEquals(byte[] left, byte[] right)
		{
			if (left == right)
			{
				return true;
			}
			if (left == null || right == null || left.Length != right.Length)
			{
				return false;
			}
			for (int i = 0; i < left.Length; i++)
			{
				if (left[i] != right[i])
				{
					return false;
				}
			}
			return true;
		}

		public override void OnNetworkDespawn()
		{
			SushiAndFish.FishingIntegration.PlayerResumeController.EndLocalSession(this);
			if (base.IsServer)
			{
				Surfboard.ReleasePlayer(this, SurfExitReason.Disconnect);
			}
			if (_surfBoard != null)
			{
				EndSurfLocal(_surfBoard, carry: false);
			}
			_surfHistory.Clear();
			_spawnGeneration++;
			StopCustomizationOperation();
			StopCustomizationSnapshotRetry();
			if (_bootstrapSpawnRetryCoroutine != null)
			{
				StopCoroutine(_bootstrapSpawnRetryCoroutine);
				_bootstrapSpawnRetryCoroutine = null;
			}
			_pendingRemoteCustomizationPayload = null;
			_lastAppliedRemoteCustomizationPayload = null;
			_remoteCustomizationApplied = false;
			NetworkVariable<bool> isTrappedState = IsTrappedState;
			isTrappedState.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isTrappedState.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnIsTrappedStateChanged));
			TeardownPlayerCollisionSetting();
			StopSquidVfxTimeout();
			StopSquidVfxLocal();
			_tpsSquidVFX?.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
			_harpoon?.SetBarBlockStatusLocal(status: false);
			if (base.IsServer && _serverActiveSharkCage != null)
			{
				_serverActiveSharkCage.ServerRemovePlayer(this);
			}
			StopHighSpeedWindLoop();
			StopAllStatusScreenVfx();
			UnregisterDissonanceFaceRendererEvents();
			UnRegisterConstraintStatusEvents();
			if (SeaCreatureManager.TryGetActiveInstance(out var manager))
			{
				manager.RemovePlayerLayerOccupancy(this);
			}
			base.OnNetworkDespawn();
		}

		public override void OnDestroy()
		{
			_spawnGeneration++;
			StopCustomizationOperation();
			StopCustomizationSnapshotRetry();
			if (_bootstrapSpawnRetryCoroutine != null)
			{
				StopCoroutine(_bootstrapSpawnRetryCoroutine);
				_bootstrapSpawnRetryCoroutine = null;
			}
			_pendingRemoteCustomizationPayload = null;
			_lastAppliedRemoteCustomizationPayload = null;
			_remoteCustomizationApplied = false;
			ResolveCoreReferences();
			TeardownPlayerCollisionSetting();
			StopSquidVfxTimeout();
			StopSquidVfxLocal();
			_tpsSquidVFX?.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
			_harpoon?.SetBarBlockStatusLocal(status: false);
			StopHighSpeedWindLoop();
			StopAllStatusScreenVfx();
			UnRegisterConstraintStatusEvents();
			UnregisterDissonanceFaceRendererEvents();
			if (Singleton<PlayersManager>.Instance != null)
			{
				Singleton<PlayersManager>.Instance.UnregisterPlayer(base.OwnerClientId);
			}
			NetworkVariable<FixedString64Bytes> playerName = PlayerName;
			playerName.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Remove(playerName.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(OnPlayerNameChanged));
			NetworkVariable<ulong> steamID = SteamID;
			steamID.OnValueChanged = (NetworkVariable<ulong>.OnValueChangedDelegate)Delegate.Remove(steamID.OnValueChanged, new NetworkVariable<ulong>.OnValueChangedDelegate(OnSteamIDChanged));
			NetworkVariable<FixedString128Bytes> nV_PlayerId = _nfgoPlayer.NV_PlayerId;
			nV_PlayerId.OnValueChanged = (NetworkVariable<FixedString128Bytes>.OnValueChangedDelegate)Delegate.Remove(nV_PlayerId.OnValueChanged, new NetworkVariable<FixedString128Bytes>.OnValueChangedDelegate(DissonancePlayerIdChanged));
			NetworkVariable<bool> isUnderwater = _isUnderwater;
			isUnderwater.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isUnderwater.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnUnderwaterStateChanged));
			NetworkVariable<byte> manualExpressionKey = _manualExpressionKey;
			manualExpressionKey.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(manualExpressionKey.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnManualExpressionKeyChanged));
			EventBus<ToolDataEvent>.Deregister(_toolDataBinding);
			if (base.IsOwner && Singleton<InputManager>.TryGetInstance(out var destroyInputManager) && destroyInputManager.InputHandler != null)
			{
				if (Singleton<GameUIManager>.TryGetInstance(out var destroyUiManager) && destroyUiManager.PauseScreen != null)
					destroyInputManager.InputHandler.OpenSettings -= destroyUiManager.PauseScreen.OnOpenSettings;
				UnbindCamShakeConnection();
				UnbindRenderDistanceConnection();
				UnbindCameraComfortSettings();
				UnbindPlayerNamesConnection();
				if (Singleton<QuickSlotHandler>.TryGetInstance(out var destroyQuickSlotHandler))
					destroyQuickSlotHandler.UnRegisterInput();
				_interactor.UnRegisterInput();
				_firstPersonCharacterInput.UnRegister();
				_firstPersonCharacterLookInput.UnRegister();
				UnRegisterWheelEvents();
				UnRegisterToolEvents();
				UnRegisterTabletEvents();
				UnRegisterAnimEvents();
				EventBus<GlobalChatEvent>.Deregister(_eventBinding);
			}
			base.OnDestroy();
		}

		private void Update()
		{
			if (_customizationOperation != null && !_customizationOperation.Token.IsCancellationRequested && !(base.IsOwner ? _localCustomizationApplyFinished : _remoteCustomizationApplied) && !IsCurrentCustomizationOperation(_customizationOperation))
			{
				StopCustomizationSnapshotRetry();
			}
			UpdateDissonance();
			TickStaticEmoteExitHardResetGuard();
			TickSwingExitHardResetGuard();
			TickServerGameplayVelocityEstimate();
			if (IsSurfing)
			{
				TickSurfOwner();
				return;
			}
			TickRemoteFootIkPerformance();
			if (base.IsOwner)
			{
				_firstPersonCharacterLookInput.LookInputUpdate();
				_firstPersonCharacterInput.CharacterInputUpdate();
				TickEmoteEscapeKeyFallback();
				ValidateSwingEmoteState();
				TickGroundLocomotionAnimator();
				TickDiveAnimator();
				TickUnderwaterHarpoonAimConstraints();
				SwayUpdate();
				CheckIfFellOutOfWorld();
				TickHighSpeedWindLoop();
				if (_seaScooter.ToolStatus)
				{
					UpdateMovementBlend();
				}
			}
		}

		private void LateUpdate()
		{
			TickSurfPresentation();
			LateUpdateDissonanceMouth();
			if (base.IsOwner)
			{
				TickFishSlipRecovery();
				CameraBobLateTick();
				FishingCameraShakeLateTick();
				EmoteFreeLookLateTick();
			}
		}

		private void UpdateMovementBlend()
		{
			if (CanUpdateSeaScooterMovementBlend())
			{
				Vector2 inputMove = _firstPersonCharacterInput.InputMove;
				SetSeaScooterMovementBlend(inputMove.x, inputMove.y, 0.15f, Time.deltaTime);
			}
		}

		private void SwayUpdate()
		{
			_ = _firstPersonCharacterLookInput.LookInput;
			_ = _firstPersonCharacterInput.InputMove;
			_firstPersonCharacter.IsGrounded();
		}

		private void CheckIfFellOutOfWorld()
		{
			if (base.transform.position.y < -3000f)
			{
				Debug.LogWarning($"Player {base.OwnerClientId} fell out of world. Teleporting to safe position.");
				TeleportPlayer(Singleton<PlayersManager>.Instance.PlayerSpawnPoint.position, updateGround: false);
			}
		}

		private void OnPlayerNameChanged(FixedString64Bytes old, FixedString64Bytes @new)
		{
			if (!base.IsOwner)
			{
				SetPlayerName(@new.ToString());
			}
		}

		private void OnSteamIDChanged(ulong old, ulong @new)
		{
			UtilityMethods.Log($"[PPG_Player] OnSteamIDChanged: {@new}");
		}

		public void StuckSave()
		{
			TeleportPlayer(Singleton<PlayersManager>.Instance.PlayerSpawnPoint.position, updateGround: false);
		}

		public void RecoverFromStuck()
		{
			if (base.IsOwner)
			{
				UtilityMethods.Log($"[PPG_Player] Stuck recovery started. OwnerClientId={base.OwnerClientId}");
				_electrocutionRecoverySequence++;
				_electrocutionRecoveryPending = false;
				_interactor?.CancelPendingUseRequest();
				ServerRequestStuckRecoveryRpc();
				if (_surfBoard != null)
				{
					EndSurfLocal(_surfBoard, carry: false);
				}
				if (_activeAttachmentMode != PlayerAttachmentMode.None)
				{
					ClientDeAssignParentConstraint();
				}
				_interactor?.ForceClearHeldActionMode(refreshHints: false);
				StopHarpoonedPullLocal();
				EndSwingEmote();
				EndStaticEmote();
				EndEmote();
				EndExpressionCamera();
				_tabletController?.CloseTablet();
				Singleton<PPG_WheelManager>.Instance?.DeactiveWheel(selectHoveredSlice: false);
				if (_divingEquipment != null && _divingEquipment.IsKnocked)
				{
					_divingEquipment.CancelKnockAndRecover();
				}
				CompleteFishSlipRecovery("StuckRecovery");
				ForceDisableToolsForStuckRecovery();
				if (_firstPersonCharacter != null)
				{
					_firstPersonCharacter.StopActiveMotion();
					_firstPersonCharacter.EnableGroundConstraint(enable: true);
				}
				RestoreMovement();
				SetActionCamera(status: false);
				SetTPSActionCamera(status: false);
				DisableFreeLook();
				PermaResume();
				ResumeInputs();
				ResumeLook();
				ResumeConstraintsIfAllowed();
				SwitchToFpsCam();
				SwitchToPlayer();
				_interactor?.SetInteractorStatus(status: true);
				UtilityMethods.Log($"[PPG_Player] Stuck recovery completed locally. OwnerClientId={base.OwnerClientId}");
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestStuckRecoveryRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2025415500u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2025415500u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsServer && rpcParams.Receive.SenderClientId == base.OwnerClientId)
			{
				_interactor?.ForceServerUseExit(null);
				Surfboard.ReleasePlayer(this, SurfExitReason.Stuck);
				bool flag = false;
				if (_activeAttachmentNetworkObject != null && _activeAttachmentNetworkObject.TryGetComponent<IAttachableObject>(out var component))
				{
					flag = component.ServerTryReleasePlayerForRecovery(this, "StuckRecovery");
				}
				if (!flag && _activeAttachmentMode != PlayerAttachmentMode.None)
				{
					ClientDeAssignParentConstraintRpc();
				}
				if (_serverTrapRecoveryOwner != null)
				{
					IPlayerTrapRecoveryOwner serverTrapRecoveryOwner = _serverTrapRecoveryOwner;
					serverTrapRecoveryOwner.ServerTryReleasePlayerForRecovery(this, "StuckRecovery");
					ServerClearTrapRecoveryOwner(serverTrapRecoveryOwner);
				}
				IsTrapped = false;
			}
		}

		public void ServerResetForEndDay()
		{
			if (base.IsServer)
			{
				_interactor?.ForceServerUseExit(null);
				SurfboardWorld.Instance?.ResetBoards("EndDay");
				ServerSetNetworkTransformRpc(enabled: true);
				ClientResetForEndDayRpc();
			}
		}

		[Rpc(SendTo.Owner)]
		private void ClientResetForEndDayRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1919755016u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1919755016u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ResetForEndDayLocal();
			}
		}

		private void ResetForEndDayLocal()
		{
			if (base.IsOwner)
			{
				UtilityMethods.Log($"[PPG_Player] End day local reset. OwnerClientId={base.OwnerClientId}");
				_interactor?.ForceClientUseExit(null);
				_interactor?.ForceClearHeldActionMode(refreshHints: false);
				EndSwingEmote();
				EndStaticEmote();
				EndEmote();
				_tabletController?.CloseTablet();
				Singleton<QuickSlotHandler>.Instance.DeselectSlot(setVisibility: false);
				ResetToolsForEndDayLocal();
				ResetMovementForEndDayLocal();
				ResetCameraForEndDayLocal();
				ResetAnimatorForEndDayLocal();
				ResetOceanStateForEndDayLocal();
				ResetPlayerEffectsForEndDayLocal();
				_interactor?.ResetEmptyAnims();
				SwitchToPlayer();
				ShowCursor();
			}
		}

		private void ResetToolsForEndDayLocal()
		{
			StopHarpoonedPullLocal();
			if (_harpoon != null)
			{
				_harpoon.ResetHarpoon();
			}
			_mop?.ForceDisableTool();
			_harpoon?.ForceDisableTool();
			_fishingRod?.ForceDisableTool();
			_compass?.ForceDisableTool();
			_seaScooter?.ForceDisableTool();
			_divingEquipment?.ResetLatchedPiranhasLocal(playRemoveSfx: false);
			ClearTool();
			_divingEquipment?.ResetDailyMaxDepth();
			if (_divingEquipment != null && _divingEquipment.IsKnocked)
			{
				_divingEquipment.CancelKnockAndRecover();
			}
		}

		private void ResetMovementForEndDayLocal()
		{
			if (_firstPersonCharacter != null)
			{
				_firstPersonCharacter.StopActiveMotion();
				_firstPersonCharacter.EnableGroundConstraint(enable: true);
				_firstPersonCharacter.TeleportRotation(Quaternion.identity);
			}
			base.transform.rotation = Quaternion.identity;
			IsTrapped = false;
			if (ParentConstraint != null && ParentConstraint.sourceCount > 0)
			{
				ClientDeAssignParentConstraint();
			}
			RestoreCameraAfterSeat();
			ResumeInputs();
			ResumeLook();
			ResumeConstraints();
			ResumeCharacter(clearState: true);
		}

		private void ResetCameraForEndDayLocal()
		{
			StopFishingCameraShake();
			StopAllCameraShakeImmediate();
			ResetCameraFov(0f);
			SetCameraCrouch(status: false);
			SetActionCamera(status: false);
			SetTPSActionCamera(status: false);
			DisableFreeLook();
			SwitchToFpsCam();
		}

		private void ResetAnimatorForEndDayLocal()
		{
			SetHolding(isHolding: false);
			SetMoving(isMoving: false);
			SetSprinting(isSprinting: false);
			SetPlacing(isPlacing: false);
			ResetGroundLocomotionAnimatorForUseMode();
			ResetDiveAnimatorState();
			SetSharkDragAnimator(isDragging: false);
			AnimSetBoolFPS(IS_UNDERWATER, value: false);
			AnimSetBoolTPS(SWIM_TRAP, value: false);
			AnimSetBoolTPS(IS_EMOTE, value: false);
			AnimSetBool(IS_EMOTE_STATIC, value: false);
			AnimSetBool(HARPOON, value: false);
			AnimSetBool(HARPOON_AIM, value: false);
			AnimSetBool(HARPOON_SHOOT, value: false);
			AnimSetBool(FISHING_ROD, value: false);
			AnimSetBool(FISHING_ROD_CAST_HOLD, value: false);
			AnimSetBool(FISHING_ROD_REEL, value: false);
			SetFishingRodSuccessAnimation(active: false);
			AnimSetBool(TRIDENT, value: false);
			AnimSetBool(TRIDENT_AIM, value: false);
			AnimSetBool(SCOOTER, value: false);
			AnimResetTrigger(DROWN);
			AnimResetTrigger(DROWN_END);
			AnimResetTrigger(FISHING_ROD_CAST_END);
			AnimResetTrigger(FISHING_ROD_FAIL);
			AnimResetTrigger(TRIDENT_THROW);
			AnimResetTriggerTPS(SWEEP);
			AnimResetTrigger(JUMP_TRIGGER);
		}

		private void ResetOceanStateForEndDayLocal()
		{
			_primarySeaLayer = null;
			if (base.IsOwner)
			{
				SetUnderwaterState(isUnderwater: false);
			}
			if (base.IsSpawned)
			{
				IsSwimming.Value = false;
			}
			_seaScooter.CanMotorBeStarted = false;
			_seaScooter.SetEngineRunServerRpc(run: false);
			Singleton<OceanFXManager>.Instance.SetSurface(isSwimming: false);
			Singleton<OceanFXManager>.Instance.SetLocomotionState(OceanLocomotionState.Idle, 0f);
			SetRippleTrail(s: false);
			PlayOceanExitVFX();
			if (_divingEquipment != null)
			{
				_divingEquipment.SetFlashlight(value: false);
				_divingEquipment.DepthUpdate?.Invoke(0f);
			}
		}

		private void ResetPlayerEffectsForEndDayLocal()
		{
			StopSquidVfxTimeout();
			StopSquidVfxLocal();
			_electricityVFX?.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
			_oceanEnterTPSVFX?.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
			_oceanEnterFPSVFX?.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
			_oceanRippleImpactVFX?.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
			_tpsSquidVFX?.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
			_electrcitySFX?.Stop();
			_audioSourceSwimNormal?.Stop();
			_audioSourceSwimSprint?.Stop();
			_sharkChaseSFX?.Stop();
			StopAllStatusScreenVfx();
			_sway?.StopScriptedSway();
			StopUnderwaterVoiceParticle();
		}

		[Rpc(SendTo.Server)]
		public void ServerKickMeRpc(ulong clientId, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2152064623u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, clientId);
				__endSendRpc(ref bufferWriter, 2152064623u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (LobbyModerationPolicy.CanProcessKick(base.IsServer, rpcParams.Receive.SenderClientId, clientId, 0uL))
				{
					Singleton<MultiplayerCoordinator>.Instance?.KickRoomClient(clientId);
				}
			}
		}

		private void OnTriggerEnter(Collider other)
		{
			if (other.CompareTag("Boost"))
			{
				_firstPersonCharacter.LaunchVelocity(Vector3.up * 15f, overrideVertical: true);
				Singleton<SoundManager>.Instance?.PlaySFX(_umbrellaBoostSFX, other.transform.position);
			}
		}

		public void HandleFishSlip(Collider fishCollider)
		{
			if (base.IsOwner && !IsUnderWater && !(Time.time < _nextFishSlipTime) && !(fishCollider == null) && fishCollider.TryGetComponent<DeadFish>(out var component) && CanStartControlAction(PlayerControlAction.FishSlip) && TryDisableActiveToolForModeChange())
			{
				_nextFishSlipTime = Time.time + _fishSlipCooldownSeconds;
				_fishSlipRecoveryPending = true;
				_fishSlipAnimatorStateObserved = false;
				_fishSlipStartFrame = Time.frameCount;
				Vector3 forward = base.transform.forward;
				ServerReportFishSlipRpc(component.NetworkObject.ToReference(), forward);
				Singleton<QuickSlotHandler>.Instance.SpillAllItemsForFishSlip(base.transform.position, forward);
				SetTPSActionCamera(status: true);
				PauseCharacter();
				AnimResetTriggerTPS(SWEEP);
				AnimSetTriggerTPS(SWEEP);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerReportFishSlipRpc(NetworkObjectReference deadFishRef, Vector3 slideDirection, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(121680056u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in deadFishRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in slideDirection);
				__endSendRpc(ref bufferWriter, 121680056u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsServer || base.OwnerClientId != rpcParams.Receive.SenderClientId || IsUnderWater || !float.IsFinite(slideDirection.x) || !float.IsFinite(slideDirection.y) || !float.IsFinite(slideDirection.z) || !deadFishRef.TryGetComponent<DeadFish>(out var component) || component.NetworkObject == null || !component.NetworkObject.IsSpawned || !component.CanHold.Value || !ServerIsDeadFishWithinSlipRange(component))
			{
				return;
			}
			component.ServerSlideForward(slideDirection);
			if (component.ServerTryGetLastHolderClientId(out var clientId) && clientId != base.OwnerClientId)
			{
				if (Singleton<PlayersManager>.Instance == null || !Singleton<PlayersManager>.Instance.TryGetPlayer(clientId, out var player) || player == null || !player.IsSpawned)
				{
					UtilityMethods.Log($"[FishSlip] Achievement target is unavailable. DropperClientId={clientId}, VictimClientId={base.OwnerClientId}", LogType.Warning);
				}
				else
				{
					Singleton<GameProgressManager>.Instance?.RpcRelay?.ServerUnlockSteamAchievementForClient("ACH_FISHY_BUSINESS", clientId);
				}
			}
		}

		private bool ServerIsDeadFishWithinSlipRange(DeadFish deadFish)
		{
			float num = 9f;
			Collider[] componentsInChildren = deadFish.GetComponentsInChildren<Collider>();
			foreach (Collider collider in componentsInChildren)
			{
				if (!(collider == null) && collider.enabled && (collider.ClosestPoint(base.transform.position) - base.transform.position).sqrMagnitude <= num)
				{
					return true;
				}
			}
			return (deadFish.transform.position - base.transform.position).sqrMagnitude <= num;
		}

		public void OnFallAnimationStart()
		{
			UtilityMethods.Log("[PPG_Player] OnFallAnimationStart");
			Singleton<SoundManager>.Instance.PlaySFX(_fishSlipSFX, base.transform.position);
		}

		public void OnStandUpAnimationComplete()
		{
			UtilityMethods.Log("[PPG_Player] OnStandUpAnimationComplete");
			CompleteFishSlipRecovery("AnimationEvent");
		}

		private void TickFishSlipRecovery()
		{
			if (!_fishSlipRecoveryPending)
			{
				return;
			}
			if (_tpsAnimator == null || !_tpsAnimator.enabled || !_tpsAnimator.gameObject.activeInHierarchy)
			{
				CompleteFishSlipRecovery("AnimatorUnavailable");
				return;
			}
			bool flag = IsFishSlipAnimatorState(_tpsAnimator.GetCurrentAnimatorStateInfo(0));
			if (_tpsAnimator.IsInTransition(0))
			{
				flag |= IsFishSlipAnimatorState(_tpsAnimator.GetNextAnimatorStateInfo(0));
			}
			if (flag)
			{
				_fishSlipAnimatorStateObserved = true;
			}
			else if (Time.frameCount > _fishSlipStartFrame + 1)
			{
				CompleteFishSlipRecovery(_fishSlipAnimatorStateObserved ? "AnimatorStateExitedWithoutEvent" : "AnimatorStateNotEntered");
			}
		}

		private static bool IsFishSlipAnimatorState(AnimatorStateInfo stateInfo)
		{
			if (!stateInfo.IsName("Base Layer.Sweep Fall") && !stateInfo.IsName("Base Layer.Getting Up") && !stateInfo.IsName("Sweep Fall"))
			{
				return stateInfo.IsName("Getting Up");
			}
			return true;
		}

		private void CompleteFishSlipRecovery(string reason)
		{
			if (_fishSlipRecoveryPending)
			{
				_fishSlipRecoveryPending = false;
				_fishSlipAnimatorStateObserved = false;
				AnimResetTriggerTPS(SWEEP);
				SetTPSActionCamera(status: false);
				TryRestoreCharacterControlAfterModeExit();
				UtilityMethods.Log("[FishSlip] Recovery completed. Reason=" + reason);
			}
		}

		[Rpc(SendTo.Owner)]
		public void ClientSharkAttackRpc(byte damageAmount)
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
				FastBufferWriter bufferWriter = __beginSendRpc(4248048454u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in damageAmount, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 4248048454u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_divingEquipment.ConsumeOxygen(damageAmount, withFeedback: true);
			}
		}

		[Rpc(SendTo.Everyone)]
		public void ClientElectrocutePlayerRpc(byte damage, byte stunDuration)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2946482843u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in damage, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in stunDuration, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2946482843u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsOwner)
			{
				uint recoverySequence = ++_electrocutionRecoverySequence;
				_electrocutionRecoveryPending = true;
				_harpoon.StartReload();
				DivingEquipment.ConsumeOxygen(damage, withFeedback: true);
				PauseCharacter();
				CameraShake();
				Singleton<DelayHelper>.Instance.DelayAction((int)stunDuration, () =>
				{
					if (recoverySequence == _electrocutionRecoverySequence)
					{
						_electrocutionRecoveryPending = false;
						TryRestoreCharacterControlAfterModeExit();
					}
				});
			}
			_electricityVFX.Play();
			ClientPlayElectricitySfxRpc();
		}

		public bool TryPrepareForEmoteArea()
		{
			if (!CanEnterEmoteArea)
			{
				return false;
			}
			if (!TryDisableActiveToolForModeChange())
			{
				return false;
			}
			if (Singleton<QuickSlotHandler>.Instance != null)
			{
				Singleton<QuickSlotHandler>.Instance.SuspendCurrentSelectionForMode(HeldSlotSuspensionReason.EmoteArea);
			}
			else if (Interactor != null && Interactor.CurrentHeld != null)
			{
				Interactor.DeselectHeldItem();
			}
			return true;
		}

		private void SetupAnim()
		{
			_fpsAnimator.enabled = base.IsOwner;
			_fpsAnimator.gameObject.SetActive(base.IsOwner);
			if (_tpsAnimator != null)
			{
				_tpsAnimator.cullingMode = ((!base.IsOwner && !base.IsServer) ? AnimatorCullingMode.CullUpdateTransforms : AnimatorCullingMode.AlwaysAnimate);
			}
			RefreshRemoteFootIk(force: true);
			_fpsHasSeaScooterBlendParameters = HasAnimatorParameter(_fpsAnimator, SEA_SCOOTER_X, AnimatorControllerParameterType.Float) && HasAnimatorParameter(_fpsAnimator, SEA_SCOOTER_Y, AnimatorControllerParameterType.Float);
			RegisterConstraintStatusEvents();
		}

		private void TickRemoteFootIkPerformance()
		{
			if (!(_footIK == null) && !base.IsOwner && !_swingEmoteActive && !_observedSwingVisualLocked && (!(_networkTransform != null) || _networkTransform.enabled) && (!(_networkTransformHead != null) || _networkTransformHead.enabled) && (!(_networkTransformSpine != null) || _networkTransformSpine.enabled) && !(Time.unscaledTime < _nextRemoteFootIkEvaluationTime))
			{
				_nextRemoteFootIkEvaluationTime = Time.unscaledTime + 0.25f;
				RefreshRemoteFootIk(force: false);
			}
		}

		private void RefreshRemoteFootIk(bool force)
		{
			if (_footIK == null || base.IsOwner)
			{
				return;
			}
			PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
			if (pPG_Player == null || pPG_Player == this)
			{
				if (force)
				{
					SetRemoteFootIkEnabled(enabled: true);
				}
			}
			else
			{
				float num = (_remoteFootIkSuppressed ? 24f : 28f);
				float sqrMagnitude = (base.transform.position - pPG_Player.transform.position).sqrMagnitude;
				SetRemoteFootIkEnabled(sqrMagnitude <= num * num);
			}
		}

		private void SetRemoteFootIkEnabled(bool enabled)
		{
			if (_footIK == null)
			{
				return;
			}
			if (!enabled)
			{
				if (_footIK.enabled)
				{
					_footIK.enabled = false;
					_remoteFootIkSuppressed = true;
				}
			}
			else if (_remoteFootIkSuppressed)
			{
				_footIK.enabled = true;
				_remoteFootIkSuppressed = false;
			}
		}

		private void RegisterAnimEvents()
		{
			FirstPersonCharacterInput firstPersonCharacterInput = _firstPersonCharacterInput;
			firstPersonCharacterInput.MovementEvent = (Action<bool>)Delegate.Combine(firstPersonCharacterInput.MovementEvent, new Action<bool>(SetMoving));
			Placer placer = _placer;
			placer.PlacementModeEvent = (Action<bool>)Delegate.Combine(placer.PlacementModeEvent, new Action<bool>(SetPlacing));
			if (base.IsOwner && Singleton<InputManager>.Instance != null && Singleton<InputManager>.Instance.InputHandler != null)
			{
				Singleton<InputManager>.Instance.InputHandler.Cancel -= OnEmoteCancelInput;
				Singleton<InputManager>.Instance.InputHandler.Cancel += OnEmoteCancelInput;
			}
		}

		private void UnRegisterAnimEvents()
		{
			FirstPersonCharacterInput firstPersonCharacterInput = _firstPersonCharacterInput;
			firstPersonCharacterInput.MovementEvent = (Action<bool>)Delegate.Remove(firstPersonCharacterInput.MovementEvent, new Action<bool>(SetMoving));
			Placer placer = _placer;
			placer.PlacementModeEvent = (Action<bool>)Delegate.Remove(placer.PlacementModeEvent, new Action<bool>(SetPlacing));
			if (base.IsOwner && Singleton<InputManager>.Instance != null && Singleton<InputManager>.Instance.InputHandler != null)
			{
				Singleton<InputManager>.Instance.InputHandler.Cancel -= OnEmoteCancelInput;
			}
		}

		public void ResumeConstraints()
		{
			if (IsSurfing)
			{
				PauseConstraints();
				return;
			}
			ApplyConstraints(enable: true);
			if (base.IsOwner && !ConstraintStatus.Value)
			{
				ConstraintStatus.Value = true;
			}
		}

		public void ResumeConstraintsIfAllowed()
		{
			if (!IsDrownedOrKnocked() && (!IsUnderWater || CanEnableUnderwaterHarpoonAimConstraints()))
			{
				ResumeConstraints();
			}
		}

		public void PauseConstraints()
		{
			ApplyConstraints(enable: false);
			if (base.IsOwner && ConstraintStatus.Value)
			{
				ConstraintStatus.Value = false;
			}
		}

		private void ApplyConstraints(bool enable)
		{
			enable &= !IsSurfing;
			ApplyConstraintState(_lookAtConstraint, enable);
			ApplyConstraintState(_lookAtConstraint2, enable);
		}

		private static void ApplyConstraintState(LookAtConstraint constraint, bool enable)
		{
			if (!(constraint == null))
			{
				constraint.enabled = enable;
				constraint.constraintActive = enable;
			}
		}

		private void RegisterConstraintStatusEvents()
		{
			NetworkVariable<bool> constraintStatus = ConstraintStatus;
			constraintStatus.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(constraintStatus.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnConstraintStatusChanged));
			NetworkVariable<bool> constraintStatus2 = ConstraintStatus;
			constraintStatus2.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(constraintStatus2.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnConstraintStatusChanged));
			ApplyConstraints(ConstraintStatus.Value);
		}

		private void UnRegisterConstraintStatusEvents()
		{
			NetworkVariable<bool> constraintStatus = ConstraintStatus;
			constraintStatus.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(constraintStatus.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnConstraintStatusChanged));
		}

		private void OnConstraintStatusChanged(bool previous, bool next)
		{
			ApplyConstraints(next);
		}

		private void TickUnderwaterHarpoonAimConstraints()
		{
			if (!base.IsOwner || !base.IsSpawned)
			{
				return;
			}
			if (!IsUnderWater)
			{
				if (_underwaterHarpoonAimRuleActive)
				{
					_underwaterHarpoonAimRuleActive = false;
					if (!HasActiveAimConstraintBlocker())
					{
						ResumeConstraintsIfAllowed();
					}
				}
			}
			else
			{
				_underwaterHarpoonAimRuleActive = true;
				SetOwnerConstraintStatus(CanEnableUnderwaterHarpoonAimConstraints());
			}
		}

		private bool CanEnableUnderwaterHarpoonAimConstraints()
		{
			if (Harpoon == null || !Harpoon.ToolStatus || _firstPersonCharacter == null)
			{
				return false;
			}
			if (HasActiveAimConstraintBlocker())
			{
				return false;
			}
			bool num = _firstPersonCharacter.GetMovementDirection().sqrMagnitude > 0.0001f;
			bool flag = _firstPersonCharacter.jumpInputPressed || _firstPersonCharacter.crouchInputPressed;
			if (!num)
			{
				return !flag;
			}
			return false;
		}

		private bool HasActiveAimConstraintBlocker()
		{
			if (!IsDrownedOrKnocked() && !IsTrapped && !_diveAnimatorActive && !_staticEmoteActive && !_swingEmoteActive && !_emoteActive && !_expressionCameraActive && _activeAttachmentMode == PlayerAttachmentMode.None)
			{
				if (_seaScooter != null)
				{
					return _seaScooter.ToolStatus;
				}
				return false;
			}
			return true;
		}

		private void SetOwnerConstraintStatus(bool enable)
		{
			if (base.IsOwner && base.IsSpawned)
			{
				enable &= !IsSurfing;
				if (!IsConstraintInState(_lookAtConstraint, enable) || !IsConstraintInState(_lookAtConstraint2, enable))
				{
					ApplyConstraints(enable);
				}
				if (ConstraintStatus.Value != enable)
				{
					ConstraintStatus.Value = enable;
				}
			}
		}

		private static bool IsConstraintInState(LookAtConstraint constraint, bool enable)
		{
			if (!(constraint == null))
			{
				if (constraint.enabled == enable)
				{
					return constraint.constraintActive == enable;
				}
				return false;
			}
			return true;
		}

		private float ComputeGroundSpeed01(float planarSpeed)
		{
			if (planarSpeed <= _idleSpeedEpsilon)
			{
				return 0f;
			}
			if (planarSpeed <= _walkReferenceSpeed)
			{
				return Mathf.Lerp(0f, 0.5f, planarSpeed / Mathf.Max(_walkReferenceSpeed, 0.0001f));
			}
			return Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(_walkReferenceSpeed, _runReferenceSpeed, planarSpeed));
		}

		private float ComputeLocomotionPlaybackSpeed(float planarSpeed)
		{
			if (planarSpeed <= _idleSpeedEpsilon)
			{
				return 1f;
			}
			float a;
			if (planarSpeed <= _walkReferenceSpeed)
			{
				float t = planarSpeed / Mathf.Max(_walkReferenceSpeed, 0.0001f);
				a = Mathf.Lerp(0f, 2f, t);
			}
			else
			{
				float t2 = Mathf.InverseLerp(_walkReferenceSpeed, _runReferenceSpeed, planarSpeed);
				a = Mathf.Lerp(2f, 4.5f, t2);
			}
			return Mathf.Clamp(planarSpeed / Mathf.Max(a, 0.0001f), 1f, 2.5f);
		}

		private Vector3 GetCharacterVelocityForAnimation(Character character)
		{
			return _firstPersonCharacter.GetVelocity();
		}

		private void TickGroundLocomotionAnimator()
		{
			if (_tpsAnimator == null || _firstPersonCharacterInput == null)
			{
				return;
			}
			Character firstPersonCharacter = _firstPersonCharacter;
			if (firstPersonCharacter == null)
			{
				return;
			}
			if (firstPersonCharacter.IsSwimming() || firstPersonCharacter.IsSeaScooter())
			{
				ResetGroundLocomotionAnimatorToNeutral();
				return;
			}
			Vector3 characterVelocityForAnimation = GetCharacterVelocityForAnimation(firstPersonCharacter);
			Vector3 direction = Vector3.ProjectOnPlane(characterVelocityForAnimation, Vector3.up);
			float magnitude = direction.magnitude;
			float value = ComputeGroundSpeed01(magnitude);
			float value2 = ComputeLocomotionPlaybackSpeed(magnitude);
			bool flag = firstPersonCharacter.IsGrounded();
			float y = characterVelocityForAnimation.y;
			if (flag && !_firstPersonCharacter.WasGrounded())
			{
				_jumpAnimArmed = false;
				AnimResetTriggerTPS(JUMP_TRIGGER);
			}
			Vector3 vector = base.transform.InverseTransformDirection(direction);
			Vector2 obj = ((magnitude > 0.0001f) ? (new Vector2(vector.x, vector.z) / magnitude) : Vector2.zero);
			float value3 = Mathf.Clamp(obj.x, -1f, 1f);
			float value4 = Mathf.Clamp(obj.y, -1f, 1f);
			float deltaTime = Time.deltaTime;
			AnimSetFloatTPS(MOVE_X, value3, _locomotionDampTime, deltaTime);
			AnimSetFloatTPS(MOVE_Y, value4, _locomotionDampTime, deltaTime);
			AnimSetFloatTPS(SPEED_01, value, _locomotionDampTime, deltaTime);
			AnimSetFloatTPS(LOCOMOTION_PLAYBACK_SPEED, value2, _locomotionDampTime, deltaTime);
			AnimSetFloatFPS(SPEED_01, value, _locomotionDampTime, deltaTime);
			AnimSetFloatTPS(VERTICAL_SPEED, y, 0.05f, deltaTime);
			AnimSetBoolTPS(IS_GROUNDED, flag);
		}

		private void TickDiveAnimator()
		{
			Character firstPersonCharacter = _firstPersonCharacter;
			if (!(firstPersonCharacter != null) || firstPersonCharacter.IsPaused() || _firstPersonCharacter.HasActiveMotion || IsTrapped || IsUsingEmoteArea || (!(_seaScooter == null) && _seaScooter.ToolStatus) || !firstPersonCharacter.IsFalling() || firstPersonCharacter.IsGrounded() || firstPersonCharacter.IsInWaterPhysicsVolume())
			{
				ResetDiveAnimatorState();
				return;
			}
			_diveAirborneTime += Time.deltaTime;
			if (_diveAirborneTime < _diveMinimumAirborneTime)
			{
				SetDiveAnimator(active: false);
				return;
			}
			Vector3 rhs = -firstPersonCharacter.GetGravityDirection();
			if (Vector3.Dot(firstPersonCharacter.GetVelocity(), rhs) > 0f)
			{
				SetDiveAnimator(active: false);
			}
			else if (!(Time.time < _nextDivePredictionTime))
			{
				_nextDivePredictionTime = Time.time + 0.15f;
				SetDiveAnimator(PredictDiveIntoWater(firstPersonCharacter));
			}
		}

		private bool PredictDiveIntoWater(Character character)
		{
			Vector3 vector = character.characterMovement.worldCenter;
			Vector3 vector2 = character.GetVelocity();
			Vector3 gravityVector = character.GetGravityVector();
			Vector3 vector3 = -character.GetGravityDirection();
			for (float num = 0.2f; num <= 10f; num += 0.2f)
			{
				Vector3 vector4 = vector + vector2 * 0.2f + 0.5f * gravityVector * 0.2f * 0.2f;
				Vector3 vector5 = vector4 - vector;
				float magnitude = vector5.magnitude;
				if (magnitude > 0.0001f && TryResolveDiveTrajectoryContact(vector, vector5 / magnitude, magnitude, out var entersWater))
				{
					return entersWater;
				}
				vector = vector4;
				vector2 += gravityVector * 0.2f;
				Vector3 vector6 = Vector3.Project(vector2, vector3);
				if (Vector3.Dot(vector6, vector3) < 0f - character.maxFallSpeed)
				{
					vector2 = vector2 - vector6 + Vector3.ClampMagnitude(vector6, character.maxFallSpeed);
				}
			}
			return false;
		}

		private void AnimSetFloatTPS(int hash, float value)
		{
			if (_tpsAnimator != null)
			{
				_tpsAnimator.SetFloat(hash, value);
			}
		}

		private void AnimSetFloatTPS(int hash, float value, float dampTime, float deltaTime)
		{
			if (_tpsAnimator != null)
			{
				_tpsAnimator.SetFloat(hash, value, dampTime, deltaTime);
			}
		}

		private void AnimSetFloatFPS(int hash, float value, float dampTime, float deltaTime)
		{
			if (_fpsAnimator != null)
			{
				_fpsAnimator.SetFloat(hash, value, dampTime, deltaTime);
			}
		}

		private bool CanUpdateSeaScooterMovementBlend()
		{
			if (_fpsAnimator != null)
			{
				return _fpsHasSeaScooterBlendParameters;
			}
			return false;
		}

		private void SetSeaScooterMovementBlend(float moveX, float moveY, float dampTime, float deltaTime)
		{
			if (CanUpdateSeaScooterMovementBlend())
			{
				_fpsAnimator.SetFloat(SEA_SCOOTER_X, moveX, dampTime, deltaTime);
				_fpsAnimator.SetFloat(SEA_SCOOTER_Y, moveY, dampTime, deltaTime);
			}
		}

		private bool TryResolveDiveTrajectoryContact(Vector3 origin, Vector3 direction, float distance, out bool entersWater)
		{
			int num = Physics.RaycastNonAlloc(origin, direction, _divePredictionHits, distance, -1, QueryTriggerInteraction.Collide);
			float num2 = float.PositiveInfinity;
			float num3 = float.PositiveInfinity;
			for (int i = 0; i < num; i++)
			{
				RaycastHit raycastHit = _divePredictionHits[i];
				Collider collider = raycastHit.collider;
				if (collider == null || collider.transform.IsChildOf(base.transform))
				{
					continue;
				}
				if (collider.isTrigger)
				{
					if (collider.TryGetComponent<PhysicsVolume>(out var component) && component.waterVolume)
					{
						num2 = Mathf.Min(num2, raycastHit.distance);
					}
				}
				else
				{
					num3 = Mathf.Min(num3, raycastHit.distance);
				}
			}
			if (float.IsPositiveInfinity(num2) && float.IsPositiveInfinity(num3))
			{
				entersWater = false;
				return false;
			}
			entersWater = num2 < num3;
			return true;
		}

		private void SetDiveAnimator(bool active)
		{
			if (_diveAnimatorActive != active)
			{
				if (active)
				{
					_divePausedConstraints = ConstraintStatus.Value;
					PauseConstraints();
				}
				else
				{
					RestoreConstraintsAfterDive();
				}
				_diveAnimatorActive = active;
				AnimSetBool(DIVE, active);
			}
		}

		private void RestoreConstraintsAfterDive()
		{
			bool divePausedConstraints = _divePausedConstraints;
			_divePausedConstraints = false;
			if (divePausedConstraints && !ShouldKeepConstraintsPausedAfterDive())
			{
				ResumeConstraintsIfAllowed();
			}
		}

		private bool ShouldKeepConstraintsPausedAfterDive()
		{
			if (!IsDrownedOrKnocked() && !IsTrapped && !IsUsingEmoteArea)
			{
				if (_seaScooter != null)
				{
					return _seaScooter.ToolStatus;
				}
				return false;
			}
			return true;
		}

		private void ResetDiveAnimatorState()
		{
			_diveAirborneTime = 0f;
			_nextDivePredictionTime = 0f;
			SetDiveAnimator(active: false);
		}

		private static bool HasAnimatorParameter(Animator animator, int hash, AnimatorControllerParameterType type)
		{
			if (animator == null)
			{
				return false;
			}
			AnimatorControllerParameter[] parameters = animator.parameters;
			foreach (AnimatorControllerParameter animatorControllerParameter in parameters)
			{
				if (animatorControllerParameter.nameHash == hash && animatorControllerParameter.type == type)
				{
					return true;
				}
			}
			return false;
		}

		private void AnimSetBoolTPS(int hash, bool value)
		{
			if (_tpsAnimator != null)
			{
				_tpsAnimator.SetBool(hash, value);
			}
		}

		private void AnimSetBoolFPS(int hash, bool value)
		{
			if (_fpsAnimator != null)
			{
				_fpsAnimator.SetBool(hash, value);
			}
		}

		private void AnimSetTriggerTPS(int hash)
		{
			if (_tpsAnimator != null)
			{
				_tpsAnimator.SetTrigger(hash);
			}
		}

		private void AnimResetTriggerTPS(int hash)
		{
			if (_tpsAnimator != null)
			{
				_tpsAnimator.ResetTrigger(hash);
			}
		}

		private void AnimSetTriggerFPS(int hash)
		{
			if (_fpsAnimator != null)
			{
				_fpsAnimator.SetTrigger(hash);
			}
		}

		private void AnimResetTriggerFPS(int hash)
		{
			if (_fpsAnimator != null)
			{
				_fpsAnimator.ResetTrigger(hash);
			}
		}

		public void TryTriggerJumpAnimator()
		{
			if (!(_tpsAnimator == null) && !(_firstPersonCharacter == null) && !_jumpAnimArmed && _firstPersonCharacter.IsGrounded() && !_firstPersonCharacter.IsSwimming() && !_firstPersonCharacter.IsSeaScooter())
			{
				AnimResetTriggerTPS(JUMP_TRIGGER);
				AnimSetTriggerTPS(JUMP_TRIGGER);
				AnimResetTriggerFPS(JUMP_TRIGGER);
				AnimSetTriggerFPS(JUMP_TRIGGER);
				_jumpAnimArmed = true;
			}
		}

		private void ResetGroundLocomotionAnimatorToNeutral()
		{
			float deltaTime = Time.deltaTime;
			AnimSetFloatTPS(MOVE_X, 0f, 0.1f, deltaTime);
			AnimSetFloatTPS(MOVE_Y, 0f, 0.1f, deltaTime);
			AnimSetFloatTPS(SPEED_01, 0f, 0.1f, deltaTime);
			AnimSetFloatTPS(LOCOMOTION_PLAYBACK_SPEED, 1f, 0.1f, deltaTime);
			AnimSetFloatFPS(SPEED_01, 0f, 0.1f, deltaTime);
			AnimSetFloatTPS(VERTICAL_SPEED, 0f, 0.05f, deltaTime);
			AnimSetBoolTPS(IS_GROUNDED, value: true);
		}

		public void ResetGroundLocomotionAnimatorForUseMode()
		{
			ClearJumpAnimatorRequest();
			ResetGroundLocomotionAnimatorToNeutral();
		}

		private void ClearJumpAnimatorRequest()
		{
			_jumpAnimArmed = false;
			AnimResetTriggerTPS(JUMP_TRIGGER);
		}

		public void AnimSetBool(int hash, bool value)
		{
			if (_tpsAnimator != null)
			{
				_tpsAnimator.SetBool(hash, value);
			}
			if (_fpsAnimator != null)
			{
				_fpsAnimator.SetBool(hash, value);
			}
		}

		public void AnimSetFloat(int hash, float value)
		{
			if (_tpsAnimator != null)
			{
				_tpsAnimator.SetFloat(hash, value);
			}
			if (_fpsAnimator != null)
			{
				_fpsAnimator.SetFloat(hash, value);
			}
		}

		public void SetFishingRodSuccessAnimation(bool active)
		{
			AnimSetBoolFPS(FISHING_ROD_SUCCESS, active);
		}

		public bool IsFishingRodSuccessPoseComplete()
		{
			if (_fpsAnimator == null || !_fpsAnimator.isActiveAndEnabled)
			{
				return true;
			}
			int layerIndex = _fpsAnimator.GetLayerIndex("Fishing Rod Layer");
			if (layerIndex < 0)
			{
				return true;
			}
			AnimatorStateInfo currentAnimatorStateInfo = _fpsAnimator.GetCurrentAnimatorStateInfo(layerIndex);
			if (currentAnimatorStateInfo.shortNameHash == FISHING_ROD_SUCCESS_STATE)
			{
				return currentAnimatorStateInfo.normalizedTime >= 1f;
			}
			return false;
		}

		public bool TryGetFishingRodFailAnimationPlaying(out bool playing)
		{
			playing = false;
			if (_fpsAnimator == null || !_fpsAnimator.isActiveAndEnabled)
			{
				return false;
			}
			int layerIndex = _fpsAnimator.GetLayerIndex("Fishing Rod Layer");
			if (layerIndex < 0)
			{
				return false;
			}
			playing = _fpsAnimator.GetCurrentAnimatorStateInfo(layerIndex).shortNameHash == FISHING_ROD_FAIL_STATE;
			if (!playing && _fpsAnimator.IsInTransition(layerIndex))
			{
				playing = _fpsAnimator.GetNextAnimatorStateInfo(layerIndex).shortNameHash == FISHING_ROD_FAIL_STATE;
			}
			return true;
		}

		public bool TryGetFishingRodReelPhase(bool useFpsAnimator, out float normalizedPhase)
		{
			normalizedPhase = 0f;
			Animator animator = (useFpsAnimator ? _fpsAnimator : _tpsAnimator);
			if (animator == null || !animator.isActiveAndEnabled)
			{
				return false;
			}
			int layerIndex = animator.GetLayerIndex("Fishing Rod Layer");
			if (layerIndex < 0)
			{
				return false;
			}
			AnimatorStateInfo animatorStateInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);
			if (animator.IsInTransition(layerIndex))
			{
				AnimatorStateInfo nextAnimatorStateInfo = animator.GetNextAnimatorStateInfo(layerIndex);
				if (nextAnimatorStateInfo.shortNameHash == FISHING_ROD_REEL_STATE)
				{
					animatorStateInfo = nextAnimatorStateInfo;
				}
			}
			if (animatorStateInfo.shortNameHash != FISHING_ROD_REEL_STATE)
			{
				return false;
			}
			normalizedPhase = Mathf.Repeat(animatorStateInfo.normalizedTime, 1f);
			return true;
		}

		public bool TryGetFishingRodReelRig(bool useFpsAnimator, out Animator animator, out Transform upperArm, out Transform lowerArm, out Transform hand)
		{
			animator = (useFpsAnimator ? _fpsAnimator : _tpsAnimator);
			upperArm = null;
			lowerArm = null;
			hand = null;
			if (animator == null)
			{
				return false;
			}
			if (animator.isHuman)
			{
				upperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
				lowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
				hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
			}
			if (upperArm == null || lowerArm == null || hand == null)
			{
				hand = ((!useFpsAnimator) ? RigBones?.TpsRig?.LeftHand : RigBones?.FpsRig?.LeftHand);
				lowerArm = ((hand != null) ? hand.parent : null);
				upperArm = ((lowerArm != null) ? lowerArm.parent : null);
			}
			if (upperArm != null && lowerArm != null && hand != null && hand.IsChildOf(lowerArm))
			{
				return lowerArm.IsChildOf(upperArm);
			}
			return false;
		}

		public void SetScooterAirborneAnimator(bool active)
		{
			AnimSetBoolTPS(SCOOTER_AIRBORNE, active);
		}

		public void AnimSetTrigger(int hash)
		{
			if (_tpsAnimator != null)
			{
				_tpsAnimator.SetTrigger(hash);
			}
			if (_fpsAnimator != null)
			{
				_fpsAnimator.SetTrigger(hash);
			}
		}

		public void AnimResetTrigger(int hash)
		{
			if (_tpsAnimator != null)
			{
				_tpsAnimator.ResetTrigger(hash);
			}
			if (_fpsAnimator != null)
			{
				_fpsAnimator.ResetTrigger(hash);
			}
		}

		public void AnimResetTrigger(string key)
		{
			if (_tpsAnimator != null)
			{
				_tpsAnimator.ResetTrigger(key);
			}
			if (_fpsAnimator != null)
			{
				_fpsAnimator.ResetTrigger(key);
			}
		}

		public void AnimSetTrigger(string key)
		{
			if (_tpsAnimator != null)
			{
				_tpsAnimator.SetTrigger(key);
			}
		}

		public void EquipHoldProfile(int type)
		{
			_holdType = type;
			if (_tpsAnimator != null)
			{
				_tpsAnimator.SetInteger(HOLD_TYPE, type);
			}
			if (_fpsAnimator != null)
			{
				_fpsAnimator.SetInteger(HOLD_TYPE, type);
			}
		}

		public void SetHolding(bool isHolding, bool startHold = false)
		{
			UtilityMethods.Log("[PPG_Player_Anim] SetHolding:" + isHolding + "-" + startHold);
			_holdActive = isHolding;
			if (_tpsAnimator != null)
			{
				_tpsAnimator.SetBool(IS_HOLDING, isHolding);
			}
			if ((isHolding & startHold) && _fpsAnimator != null)
			{
				_fpsAnimator.SetBool(START_HOLD, value: true);
			}
			if (!isHolding && _fpsAnimator != null)
			{
				_fpsAnimator.SetBool(START_HOLD, value: false);
			}
			if (_fpsAnimator != null)
			{
				_fpsAnimator.SetBool(IS_HOLDING, isHolding);
			}
			if (!isHolding)
			{
				EquipHoldProfile(0);
			}
		}

		public void SetMoving(bool isMoving)
		{
			if (_locomotionAnimationSuppressed)
			{
				isMoving = false;
			}
			if (_tpsAnimator != null)
			{
				_tpsAnimator.SetBool(IS_MOVING, isMoving);
			}
			if (_fpsAnimator != null)
			{
				_fpsAnimator.SetBool(IS_MOVING, isMoving);
			}
		}

		public void SetLocomotionAnimationSuppressed(bool suppressed)
		{
			if (_locomotionAnimationSuppressed != suppressed)
			{
				_locomotionAnimationSuppressed = suppressed;
				if (suppressed)
				{
					SetMoving(isMoving: false);
					ResetGroundLocomotionAnimatorForUseMode();
				}
			}
		}

		public void SetSprinting(bool isSprinting)
		{
			AnimSetBoolFPS(IS_SPRINTING, isSprinting);
		}

		public void SetPlacing(bool isPlacing)
		{
			if (_tpsAnimator != null)
			{
				_tpsAnimator.SetBool(IS_PLACING, isPlacing);
			}
			if (_fpsAnimator != null)
			{
				_fpsAnimator.SetBool(IS_PLACING, isPlacing);
			}
		}

		public void SetSharkDragAnimator(bool isDragging)
		{
			if (_tpsAnimator != null)
			{
				_tpsAnimator.SetBool(DRAG, isDragging);
			}
		}

		public void SetSharkDragFPSPresentation(bool active)
		{
			if (!base.IsOwner || _fpsAnimator == null || _sharkDragFpsPresentationActive == active)
			{
				return;
			}
			_sharkDragFpsPresentationActive = active;
			_sharkDragFpsPunchToken++;
			if (active)
			{
				_sharkDragFpsRenderers = _fpsAnimator.GetComponentsInChildren<Renderer>(includeInactive: true);
				_sharkDragFpsRendererStates = new bool[_sharkDragFpsRenderers.Length];
				for (int i = 0; i < _sharkDragFpsRenderers.Length; i++)
				{
					Renderer renderer = _sharkDragFpsRenderers[i];
					if (!(renderer == null))
					{
						_sharkDragFpsRendererStates[i] = renderer.enabled;
						renderer.enabled = false;
					}
				}
			}
			else
			{
				RestoreSharkDragFpsRenderers();
			}
		}

		public void PlaySharkDragPunchFPS(float visibleDuration)
		{
			if (base.IsOwner && _sharkDragFpsPresentationActive && !(_fpsAnimator == null) && _fpsAnimator.isActiveAndEnabled)
			{
				SetSharkDragFpsRenderersVisible(visible: true);
				_fpsAnimator.ResetTrigger(SHARK_DRAG_PUNCH);
				_fpsAnimator.SetTrigger(SHARK_DRAG_PUNCH);
				StartCoroutine(HideSharkDragFpsRenderersAfterDelay(++_sharkDragFpsPunchToken, Mathf.Max(0.01f, visibleDuration)));
			}
		}

		private IEnumerator HideSharkDragFpsRenderersAfterDelay(uint token, float delay)
		{
			yield return new WaitForSecondsRealtime(delay);
			if (_sharkDragFpsPresentationActive && token == _sharkDragFpsPunchToken)
			{
				SetSharkDragFpsRenderersVisible(visible: false);
			}
		}

		private void SetSharkDragFpsRenderersVisible(bool visible)
		{
			if (_sharkDragFpsRenderers == null || _sharkDragFpsRendererStates == null)
			{
				return;
			}
			int num = Mathf.Min(_sharkDragFpsRenderers.Length, _sharkDragFpsRendererStates.Length);
			for (int i = 0; i < num; i++)
			{
				Renderer renderer = _sharkDragFpsRenderers[i];
				if (renderer != null)
				{
					renderer.enabled = visible && _sharkDragFpsRendererStates[i];
				}
			}
		}

		private void RestoreSharkDragFpsRenderers()
		{
			if (_sharkDragFpsRenderers != null && _sharkDragFpsRendererStates != null)
			{
				int num = Mathf.Min(_sharkDragFpsRenderers.Length, _sharkDragFpsRendererStates.Length);
				for (int i = 0; i < num; i++)
				{
					Renderer renderer = _sharkDragFpsRenderers[i];
					if (renderer != null)
					{
						renderer.enabled = _sharkDragFpsRendererStates[i];
					}
				}
			}
			_sharkDragFpsRenderers = null;
			_sharkDragFpsRendererStates = null;
		}

		[Rpc(SendTo.Owner)]
		public void ClientSetOctoTrapAnimRpc(bool isActive)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2049903745u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in isActive, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2049903745u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				AnimSetBoolTPS(SWIM_TRAP, isActive);
			}
		}

		public void StartStaticEmote(StaticEmotes @static, Transform target = null, bool enableCameraToggle = true, bool disableNetworkTransforms = true)
		{
			if (target == null)
			{
				UtilityMethods.Log($"[EmoteArea] StartStaticEmote called without target. Emote={@static}", LogType.Warning);
				target = base.transform;
			}
			if (_tpsAnimator == null)
			{
				UtilityMethods.Log("[EmoteArea] StartStaticEmote failed. Animator is null.", LogType.Error);
				Singleton<QuickSlotHandler>.Instance?.ResumeCurrentSelectionForMode(HeldSlotSuspensionReason.EmoteArea);
				return;
			}
			PauseConstraints();
			if (disableNetworkTransforms)
			{
				ServerSetNetworkTransformRpc(enabled: false);
			}
			PermaPause();
			if (_tpsAnimator != null)
			{
				_tpsAnimator.SetBool(IS_EMOTE_STATIC, value: false);
			}
			int emoteSequence = ++_staticEmoteSequence;
			UtilityMethods.Log($"[EmoteArea] _tpsAnimator is null {_tpsAnimator == null}");
			Singleton<DelayHelper>.Instance.DelayActionUntilEndOfFrame(() =>
			{
				if (_tpsAnimator != null && _staticEmoteActive && emoteSequence == _staticEmoteSequence)
				{
					_tpsAnimator.SetBool(IS_EMOTE_STATIC, value: true);
					UtilityMethods.Log("[EmoteArea]  _tpsAnimator.SetBool(IS_EMOTE_STATIC, true);");
				}
			});
			if (_tpsAnimator != null)
			{
				_tpsAnimator.SetFloat(EMOTE_TYPE, (float)@static);
				UtilityMethods.Log($"[EmoteArea]  _tpsAnimator.SetFloat(EMOTE_TYPE, (int)@static);  static/{(int)@static}");
			}
			SwitchToTpsCam();
			HideCursor();
			_staticEmoteActive = true;
			_staticEmoteFpsCameraActive = false;
			_staticEmoteCameraToggleEnabled = enableCameraToggle;
			if (_staticEmoteCameraToggleEnabled)
			{
				Singleton<InputManager>.Instance.InputHandler.Mouse2 -= OnStaticEmoteCameraToggleInput;
				Singleton<InputManager>.Instance.InputHandler.Mouse2 += OnStaticEmoteCameraToggleInput;
				if (CurrentEmoteArea != null)
				{
					RefreshEmoteCameraHint(fpsCameraActive: false);
				}
			}
			if (base.IsOwner && CurrentEmoteArea != null)
			{
				Singleton<HintsManager>.Instance?.AddPermanentHint("OpenSettings", "key_exit");
			}
		}

		public void EndStaticEmote()
		{
			if (_staticEmoteActive)
			{
				bool num = CurrentEmoteArea != null && !_swingEmoteActive;
				Singleton<InputManager>.Instance.InputHandler.Mouse2 -= OnStaticEmoteCameraToggleInput;
				_staticEmoteFpsCameraActive = false;
				_staticEmoteCameraToggleEnabled = false;
				Singleton<HintsManager>.Instance.RemovePermanentHint("RightMouseMechanic");
				Singleton<HintsManager>.Instance.RemovePermanentHint("OpenSettings");
				ResetStaticEmoteAnimator();
				RestoreEmoteAreaFpsLocalVisuals();
				DisableEmoteFpsLook();
				ResumeConstraintsIfAllowed();
				_staticEmoteActive = false;
				Vector3 position = base.transform.position;
                if (CurrentEmoteArea != null && CurrentEmoteArea.TryGetComponent<SushiAndFish.CityExpansion.CityDiningSeat>(out var diningSeat) && diningSeat.approach)
                    position = diningSeat.approach.position;
				Quaternion quaternion = FlattenPitchAndRoll(base.transform.rotation);
				ApplyStaticEmoteExitPoseLocal(position, quaternion);
				TryRestoreCharacterControlAfterModeExit();
				if (CurrentEmoteArea != null)
				{
					CurrentEmoteArea.ServerChangeSomeoneUsingRpc(value: false);
					CurrentEmoteArea.WorldUIAnchor.SetVisible(value: true);
					ServerApplyStaticEmoteExitPoseRpc(position, quaternion);
					CurrentEmoteArea = null;
				}
				SetTPSActionCamera(status: false);
				SwitchToFpsCam();
				ShowCursor();
				if (num)
				{
					StartStaticEmoteExitHardResetGuard();
				}
				Singleton<QuickSlotHandler>.Instance?.ResumeCurrentSelectionForMode(HeldSlotSuspensionReason.EmoteArea);
			}
		}

		private void ResetEmoteAreaExitRotation()
		{
			base.transform.rotation = FlattenPitchAndRoll(base.transform.rotation);
			if (TPS_Character != null)
			{
				TPS_Character.transform.localRotation = FlattenPitchAndRoll(TPS_Character.transform.localRotation);
			}
		}

		private static Quaternion FlattenPitchAndRoll(Quaternion rotation)
		{
			return Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
		}

		private void ApplyStaticEmoteExitPoseLocal(Vector3 position, Quaternion rotation)
		{
			Quaternion quaternion = FlattenPitchAndRoll(rotation);
			base.transform.SetPositionAndRotation(position, quaternion);
			if (TPS_Character != null)
			{
				TPS_Character.transform.localRotation = Quaternion.identity;
			}
			if (base.IsOwner && !(_firstPersonCharacter == null))
			{
				_firstPersonCharacter.TeleportPosition(position);
				_firstPersonCharacter.TeleportRotation(quaternion);
				_firstPersonCharacter.ClearExternalMotionForEmoteExit();
			}
		}

		private void ResetStaticEmoteAnimator()
		{
			_staticEmoteSequence++;
			if (!(_tpsAnimator == null))
			{
				_tpsAnimator.SetBool(IS_EMOTE_STATIC, value: false);
				_tpsAnimator.SetFloat(EMOTE_TYPE, 0f);
				int layerIndex = _tpsAnimator.GetLayerIndex("EmoteLayer");
				if (layerIndex >= 0 && _tpsAnimator.HasState(layerIndex, EMPTY_STATIC_EMOTE_STATE))
				{
					_tpsAnimator.Play(EMPTY_STATIC_EMOTE_STATE, layerIndex, 0f);
				}
				_tpsAnimator.Update(0f);
			}
		}

		private void TickStaticEmoteExitHardResetGuard()
		{
			if (_staticEmoteExitHardResetActive)
			{
				ResetStaticEmoteExitPoseNow();
				if (!(Time.time <= _staticEmoteExitHardResetUntil))
				{
					_staticEmoteExitHardResetActive = false;
					ResetStaticEmoteExitPoseNow();
				}
			}
		}

		private void StartStaticEmoteExitHardResetGuard()
		{
			_staticEmoteExitHardResetActive = true;
			_staticEmoteExitHardResetUntil = Time.time + 0.55f;
			ResetStaticEmoteExitPoseNow();
		}

		private void ResetStaticEmoteExitPoseNow()
		{
			ResetStaticEmoteAnimator();
			ResetGroundLocomotionAnimatorToNeutral();
			AnimSetBoolTPS(IS_MOVING, value: false);
			ResetEmoteAreaExitRotation();
			if (TPS_Character != null)
			{
				TPS_Character.transform.localRotation = Quaternion.identity;
			}
			if (_tpsAnimator != null)
			{
				_tpsAnimator.Update(0f);
			}
		}

		public void StartSwingEmote(StaticEmotes @static, SwingEmoteArea swingArea)
		{
			if (IsUsingEmoteArea || DidExitEmoteAreaThisFrame)
			{
				return;
			}
			if (swingArea == null)
			{
				UtilityMethods.Log("[SwingEmoteArea] StartSwingEmote failed. SwingEmoteArea is null.", LogType.Error);
				return;
			}
			CurrentEmoteArea = null;
			StartStaticEmote(@static, (swingArea.SeatPoint != null) ? swingArea.SeatPoint : swingArea.transform, enableCameraToggle: false, disableNetworkTransforms: false);
			if (!_staticEmoteActive)
			{
				Singleton<QuickSlotHandler>.Instance?.ResumeCurrentSelectionForMode(HeldSlotSuspensionReason.EmoteArea);
				return;
			}
			PauseInputs();
			_activeSwingArea = swingArea;
			_swingEmoteActive = true;
			_lastSwingPumpInput = 0f;
			SetSwingFootIKActive(active: false);
			Singleton<InputManager>.Instance.InputHandler.Move -= OnSwingMoveInput;
			Singleton<InputManager>.Instance.InputHandler.Move += OnSwingMoveInput;
			Singleton<InputManager>.Instance.InputHandler.Interact -= OnSwingInteractInput;
			Singleton<InputManager>.Instance.InputHandler.Interact += OnSwingInteractInput;
			Singleton<InputManager>.Instance.InputHandler.Mouse2 -= OnSwingCameraToggleInput;
			Singleton<InputManager>.Instance.InputHandler.Mouse2 += OnSwingCameraToggleInput;
			RefreshEmoteCameraHint(fpsCameraActive: false);
			Singleton<HintsManager>.Instance?.AddPermanentHint("OpenSettings", "key_exit");
			swingArea.ServerStartSwingRpc(base.NetworkObject);
		}

		public void StartObservedSwingEmote(StaticEmotes @static)
		{
			BeginObservedSwingVisualLock(@static);
		}

		public void EndObservedSwingEmote()
		{
			EndObservedSwingVisualLock(base.transform.position, base.transform.rotation);
		}

		public void BeginObservedSwingVisualLock(StaticEmotes @static)
		{
			if (!base.IsOwner)
			{
				bool networkAnimatorRestoreWasPending = _observedSwingNetworkAnimatorRestoreRoutine != null;
				bool observedSwingNetworkAnimatorWasEnabled = _observedSwingNetworkAnimatorWasEnabled;
				CancelObservedSwingNetworkAnimatorRestore();
				_observedSwingVisualLockSequence++;
				if (!_observedSwingVisualLocked)
				{
					CacheObservedSwingVisualLockState(networkAnimatorRestoreWasPending, observedSwingNetworkAnimatorWasEnabled);
					_observedSwingVisualLocked = true;
				}
				ApplyObservedSwingVisualLockState();
				ForceObservedSwingStaticEmotePose(@static);
			}
		}

		public void EndObservedSwingVisualLock(Vector3 exitPosition, Quaternion exitRotation)
		{
			if (base.IsOwner)
			{
				return;
			}
			bool observedSwingVisualLocked = _observedSwingVisualLocked;
			bool observedSwingNetworkAnimatorWasEnabled = _observedSwingNetworkAnimatorWasEnabled;
			NetworkAnimator networkAnimator = ResolveObservedSwingNetworkAnimator();
			base.transform.SetPositionAndRotation(exitPosition, exitRotation);
			ResetObservedSwingPoseNow();
			StartSwingExitHardResetGuard(exitPosition, exitRotation, lockTransform: true);
			if (!observedSwingVisualLocked)
			{
				if (_observedSwingNetworkAnimatorRestoreRoutine != null)
				{
					CancelObservedSwingNetworkAnimatorRestore();
					if (networkAnimator != null)
					{
						networkAnimator.enabled = observedSwingNetworkAnimatorWasEnabled;
					}
				}
			}
			else
			{
				int sequence = ++_observedSwingVisualLockSequence;
				_observedSwingVisualLocked = false;
				RestoreObservedSwingVisualLockState(restoreNetworkAnimator: false);
				RestoreObservedSwingNetworkAnimatorAfterDelay(networkAnimator, observedSwingNetworkAnimatorWasEnabled, sequence);
			}
		}

		public void ForceCancelObservedSwingVisualLock()
		{
			if (!base.IsOwner)
			{
				_observedSwingVisualLockSequence++;
				bool observedSwingNetworkAnimatorWasEnabled = _observedSwingNetworkAnimatorWasEnabled;
				NetworkAnimator networkAnimator = ResolveObservedSwingNetworkAnimator();
				CancelObservedSwingNetworkAnimatorRestore();
				if (_observedSwingVisualLocked)
				{
					ResetObservedSwingPoseNow();
					_observedSwingVisualLocked = false;
					RestoreObservedSwingVisualLockState(restoreNetworkAnimator: true);
				}
				else if (networkAnimator != null)
				{
					networkAnimator.enabled = observedSwingNetworkAnimatorWasEnabled;
				}
			}
		}

		public void ApplySwingSeatVisualPose(Vector3 position, Quaternion rotation)
		{
			base.transform.SetPositionAndRotation(position, rotation);
		}

		private void TickSwingExitHardResetGuard()
		{
			if (_swingExitHardResetActive)
			{
				if (_swingExitHardResetLockTransform && Time.time <= _swingExitPoseLockUntil)
				{
					base.transform.SetPositionAndRotation(_swingExitHardResetPosition, _swingExitHardResetRotation);
				}
				ResetSwingExitAnimatorPoseNow();
				if (!(Time.time <= _swingExitHardResetUntil))
				{
					_swingExitHardResetActive = false;
					_swingExitHardResetLockTransform = false;
					ResetSwingExitAnimatorPoseNow();
				}
			}
		}

		private void StartSwingExitHardResetGuard(Vector3 position, Quaternion rotation, bool lockTransform)
		{
			_swingExitHardResetActive = true;
			_swingExitHardResetLockTransform = lockTransform;
			_swingExitHardResetUntil = Time.time + 0.55f;
			_swingExitPoseLockUntil = Time.time + 0.2f;
			_swingExitHardResetPosition = position;
			_swingExitHardResetRotation = rotation;
			ResetSwingExitAnimatorPoseNow();
		}

		private void ResetObservedSwingPoseNow()
		{
			if (!base.IsOwner)
			{
				ResetSwingExitAnimatorPoseNow();
			}
		}

		private void ResetSwingExitAnimatorPoseNow()
		{
			ResetStaticEmoteAnimator();
			ResetGroundLocomotionAnimatorToNeutral();
			AnimSetBoolTPS(IS_MOVING, value: false);
			if (_tpsAnimator != null)
			{
				_tpsAnimator.Update(0f);
			}
			if (TPS_Character != null)
			{
				TPS_Character.transform.localRotation = Quaternion.identity;
			}
		}

		private void QueueSwingExitAnimatorPoseReset()
		{
			if (Singleton<DelayHelper>.Instance == null)
			{
				return;
			}
			int sequence = _staticEmoteSequence;
			Singleton<DelayHelper>.Instance.DelayActionUntilEndOfFrame(() =>
			{
				if (!_staticEmoteActive && !_swingEmoteActive && sequence == _staticEmoteSequence)
				{
					ResetSwingExitAnimatorPoseNow();
				}
			});
		}

		private void CacheObservedSwingVisualLockState(bool networkAnimatorRestoreWasPending, bool pendingNetworkAnimatorWasEnabled)
		{
			_observedSwingNetworkTransformWasEnabled = _networkTransform != null && _networkTransform.enabled;
			_observedSwingHeadNetworkTransformWasEnabled = _networkTransformHead != null && _networkTransformHead.enabled;
			_observedSwingSpineNetworkTransformWasEnabled = _networkTransformSpine != null && _networkTransformSpine.enabled;
			_observedSwingFootIKWasEnabled = _footIK != null && _footIK.enabled;
			NetworkAnimator networkAnimator = ResolveObservedSwingNetworkAnimator();
			_observedSwingNetworkAnimatorWasEnabled = (networkAnimatorRestoreWasPending ? pendingNetworkAnimatorWasEnabled : (networkAnimator != null && networkAnimator.enabled));
		}

		private void ApplyObservedSwingVisualLockState()
		{
			if (_networkTransform != null)
			{
				_networkTransform.enabled = false;
			}
			if (_networkTransformHead != null)
			{
				_networkTransformHead.enabled = false;
			}
			if (_networkTransformSpine != null)
			{
				_networkTransformSpine.enabled = false;
			}
			NetworkAnimator networkAnimator = ResolveObservedSwingNetworkAnimator();
			if (networkAnimator != null)
			{
				networkAnimator.enabled = false;
			}
			if (_footIK != null)
			{
				_footIK.enabled = false;
			}
			if (_lookAtConstraint != null)
			{
				_lookAtConstraint.constraintActive = false;
				_lookAtConstraint.enabled = false;
			}
			if (_lookAtConstraint2 != null)
			{
				_lookAtConstraint2.constraintActive = false;
				_lookAtConstraint2.enabled = false;
			}
		}

		private void RestoreObservedSwingVisualLockState(bool restoreNetworkAnimator)
		{
			if (_networkTransform != null)
			{
				_networkTransform.enabled = _observedSwingNetworkTransformWasEnabled;
			}
			if (_networkTransformHead != null)
			{
				_networkTransformHead.enabled = _observedSwingHeadNetworkTransformWasEnabled;
			}
			if (_networkTransformSpine != null)
			{
				_networkTransformSpine.enabled = _observedSwingSpineNetworkTransformWasEnabled;
			}
			if (restoreNetworkAnimator)
			{
				NetworkAnimator networkAnimator = ResolveObservedSwingNetworkAnimator();
				if (networkAnimator != null)
				{
					networkAnimator.enabled = _observedSwingNetworkAnimatorWasEnabled;
				}
			}
			if (_footIK != null)
			{
				_footIK.enabled = _observedSwingFootIKWasEnabled;
			}
			ApplyConstraints(ConstraintStatus.Value);
		}

		private void ForceObservedSwingStaticEmotePose(StaticEmotes @static)
		{
			if (!(_tpsAnimator == null))
			{
				_staticEmoteSequence++;
				_tpsAnimator.SetBool(IS_EMOTE_STATIC, value: false);
				_tpsAnimator.SetFloat(EMOTE_TYPE, (float)@static);
				_tpsAnimator.Update(0f);
				_tpsAnimator.SetBool(IS_EMOTE_STATIC, value: true);
				_tpsAnimator.Update(0f);
			}
		}

		private NetworkAnimator ResolveObservedSwingNetworkAnimator()
		{
			if (_observedSwingNetworkAnimator != null)
			{
				return _observedSwingNetworkAnimator;
			}
			if (_tpsAnimator != null && _tpsAnimator.TryGetComponent<NetworkAnimator>(out _observedSwingNetworkAnimator))
			{
				return _observedSwingNetworkAnimator;
			}
			if (TryGetComponent<NetworkAnimator>(out _observedSwingNetworkAnimator))
			{
				return _observedSwingNetworkAnimator;
			}
			_observedSwingNetworkAnimator = GetComponentInChildren<NetworkAnimator>(includeInactive: true);
			return _observedSwingNetworkAnimator;
		}

		private void CancelObservedSwingNetworkAnimatorRestore()
		{
			if (_observedSwingNetworkAnimatorRestoreRoutine != null)
			{
				if (Singleton<DelayHelper>.Instance != null)
				{
					Singleton<DelayHelper>.Instance.StopCoroutine(_observedSwingNetworkAnimatorRestoreRoutine);
				}
				_observedSwingNetworkAnimatorRestoreRoutine = null;
			}
		}

		private void RestoreObservedSwingNetworkAnimatorAfterDelay(NetworkAnimator networkAnimator, bool shouldRestoreNetworkAnimator, int sequence)
		{
			if (networkAnimator == null)
			{
				return;
			}
			if (!shouldRestoreNetworkAnimator)
			{
				networkAnimator.enabled = false;
				return;
			}
			if (Singleton<DelayHelper>.Instance == null)
			{
				networkAnimator.enabled = true;
				return;
			}
			_observedSwingNetworkAnimatorRestoreRoutine = Singleton<DelayHelper>.Instance.DelayAction(0.55f, () =>
			{
				_observedSwingNetworkAnimatorRestoreRoutine = null;
				if (!(this == null) && !_observedSwingVisualLocked && sequence == _observedSwingVisualLockSequence)
				{
					ResetSwingExitAnimatorPoseNow();
					if (networkAnimator != null)
					{
						networkAnimator.enabled = true;
					}
					QueueSwingExitAnimatorPoseReset();
				}
			});
		}

		public void EndSwingEmote()
		{
			EndSwingEmote(notifyServer: true);
		}

		public void HandleSwingStartRejected(SwingEmoteArea swingArea)
		{
			if (_swingEmoteActive && !(_activeSwingArea != swingArea))
			{
				_emoteExitInputFrame = Time.frameCount;
				EndSwingEmote(notifyServer: false);
			}
		}

		private void EndSwingEmote(bool notifyServer)
		{
			if (!_swingEmoteActive)
			{
				return;
			}
			Singleton<InputManager>.Instance.InputHandler.Move -= OnSwingMoveInput;
			Singleton<InputManager>.Instance.InputHandler.Interact -= OnSwingInteractInput;
			Singleton<InputManager>.Instance.InputHandler.Mouse2 -= OnSwingCameraToggleInput;
			SwingEmoteArea activeSwingArea = _activeSwingArea;
			Vector3 launchVelocity = Vector3.zero;
			if (activeSwingArea != null)
			{
				launchVelocity = activeSwingArea.GetReleaseVelocity();
				activeSwingArea.ReleasePlayerLocally(base.transform);
			}
			_activeSwingArea = null;
			_swingEmoteActive = false;
			_swingFpsCameraActive = false;
			_lastSwingPumpInput = 0f;
			SetSwingFootIKActive(active: true);
			Singleton<HintsManager>.Instance.RemovePermanentHint("RightMouseMechanic");
			EndStaticEmote();
			Singleton<QuickSlotHandler>.Instance?.ResumeCurrentSelectionForMode(HeldSlotSuspensionReason.EmoteArea);
			ResetSwingExitAnimatorPoseNow();
			StartSwingExitHardResetGuard(base.transform.position, base.transform.rotation, lockTransform: false);
			QueueSwingExitAnimatorPoseReset();
			ResumeInputs();
			if (activeSwingArea != null)
			{
				activeSwingArea.SetLocalSwingPumpInput(0f);
				if (notifyServer)
				{
					activeSwingArea.ServerStopSwingRpc();
				}
				if (activeSwingArea.WorldUIAnchor != null)
				{
					activeSwingArea.WorldUIAnchor.SetVisible(value: true);
				}
			}
			if (launchVelocity.sqrMagnitude > 0.0001f)
			{
				_firstPersonCharacter.LaunchVelocity(launchVelocity, overrideVertical: false, overrideLateral: true);
			}
		}

		private void ValidateSwingEmoteState()
		{
			if (_swingEmoteActive && (_activeSwingArea == null || !_activeSwingArea.isActiveAndEnabled))
			{
				EndSwingEmote();
			}
		}

		private void OnSwingMoveInput(InputActionPhase phase, Vector2 input)
		{
			if (_swingEmoteActive && !(_activeSwingArea == null))
			{
				float value = ((phase == InputActionPhase.Canceled) ? 0f : (0f - input.y));
				value = Mathf.Clamp(value, -1f, 1f);
				if (!Mathf.Approximately(value, _lastSwingPumpInput))
				{
					_lastSwingPumpInput = value;
					_activeSwingArea.SetLocalSwingPumpInput(value);
				}
			}
		}

		private void SetSwingFootIKActive(bool active)
		{
			if (_footIK == null)
			{
				return;
			}
			if (!active)
			{
				if (!_swingFootIKStateCached)
				{
					_swingFootIKWasEnabled = _footIK.enabled;
					_swingFootIKStateCached = true;
				}
				_footIK.enabled = false;
			}
			else if (_swingFootIKStateCached)
			{
				_footIK.enabled = _swingFootIKWasEnabled;
				_swingFootIKStateCached = false;
			}
		}

		private void OnStaticEmoteCameraToggleInput(InputActionPhase phase)
		{
			if (_staticEmoteActive && _staticEmoteCameraToggleEnabled && phase == InputActionPhase.Started && !(CurrentEmoteArea == null))
			{
				_staticEmoteFpsCameraActive = !_staticEmoteFpsCameraActive;
				if (_staticEmoteFpsCameraActive)
				{
					SwitchToFpsCam();
					ShowCursor();
					SetTPSActionCamera(status: true);
					EnableEmoteFpsLook();
					HideEmoteAreaFpsLocalVisuals();
				}
				else
				{
					RestoreEmoteAreaFpsLocalVisuals();
					DisableEmoteFpsLook();
					SetTPSActionCamera(status: false);
					SwitchToTpsCam();
					HideCursor();
				}
				RefreshEmoteCameraHint(_staticEmoteFpsCameraActive);
			}
		}

		private void RefreshEmoteCameraHint(bool fpsCameraActive)
		{
			if (!(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.AddPermanentHint("RightMouseMechanic", fpsCameraActive ? "key_switch_to_tps" : "key_switch_to_fps");
			}
		}

		private void OnSwingInteractInput(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Canceled)
			{
				_emoteExitInputFrame = Time.frameCount;
				EndSwingEmote();
			}
		}

		private void OnEmoteCancelInput(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && ExitActiveEmoteAreaFromCancel())
			{
				_emoteExitInputFrame = Time.frameCount;
			}
		}

		private void TickEmoteEscapeKeyFallback()
		{
			if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && ExitActiveEmoteAreaFromCancel())
			{
				_emoteExitInputFrame = Time.frameCount;
			}
		}

		private bool ExitActiveEmoteAreaFromCancel()
		{
			if (_swingEmoteActive)
			{
				EndSwingEmote();
				return true;
			}
			if (_staticEmoteActive && CurrentEmoteArea != null)
			{
				EndStaticEmote();
				return true;
			}
			return false;
		}

		private void OnSwingCameraToggleInput(InputActionPhase phase)
		{
			if (_swingEmoteActive && phase == InputActionPhase.Started)
			{
				_swingFpsCameraActive = !_swingFpsCameraActive;
				if (_swingFpsCameraActive)
				{
					SwitchToFpsCam();
					ShowCursor();
					EnableEmoteFpsLook((_activeSwingArea != null) ? _activeSwingArea.LookMaxYaw : _emoteFpsMaxYaw, (_activeSwingArea != null) ? _activeSwingArea.LookMaxPitch : _emoteFpsMaxPitch, useWorldUpYaw: true);
				}
				else
				{
					DisableEmoteFpsLook();
					SwitchToTpsCam();
					HideCursor();
				}
				RefreshEmoteCameraHint(_swingFpsCameraActive);
			}
		}

		public void ServerTpPlayerRpc(Vector3 pos, Vector3 rot)
		{
			ClientTpPlayerRpc(pos, rot);
		}

		public void ServerSetNetworkTransformRpc(bool enabled)
		{
			ClientSetNetworkTransformRpc(enabled);
		}

		[Rpc(SendTo.Server)]
		private void ServerApplyStaticEmoteExitPoseRpc(Vector3 exitPosition, Quaternion exitRotation, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2289590177u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in exitPosition);
				bufferWriter.WriteValueSafe(in exitRotation);
				__endSendRpc(ref bufferWriter, 2289590177u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (rpcParams.Receive.SenderClientId != base.OwnerClientId)
				{
					UtilityMethods.Log($"[EmoteArea] Exit pose rejected. Sender={rpcParams.Receive.SenderClientId} Owner={base.OwnerClientId}", LogType.Warning);
					return;
				}
				Quaternion exitRotation2 = FlattenPitchAndRoll(exitRotation);
				ClientApplyStaticEmoteExitPoseRpc(exitPosition, exitRotation2);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientApplyStaticEmoteExitPoseRpc(Vector3 exitPosition, Quaternion exitRotation)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3223578054u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in exitPosition);
				bufferWriter.WriteValueSafe(in exitRotation);
				__endSendRpc(ref bufferWriter, 3223578054u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ApplyStaticEmoteExitPoseLocal(exitPosition, exitRotation);
				SetNetworkTransformComponentsEnabledLocal(enabled: true);
				StartStaticEmoteExitHardResetGuard();
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientTpPlayerRpc(Vector3 pos, Vector3 rot)
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
				FastBufferWriter bufferWriter = __beginSendRpc(4160951381u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in pos);
				bufferWriter.WriteValueSafe(in rot);
				__endSendRpc(ref bufferWriter, 4160951381u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Quaternion rotation = Quaternion.Euler(rot);
				if (base.IsOwner && _firstPersonCharacter != null)
				{
					TeleportPlayer(pos, rotation, updateGround: false);
					return;
				}
				base.transform.SetPositionAndRotation(pos, rotation);
				HandleSeaLayerOccupancyAfterTeleport();
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientSetNetworkTransformRpc(bool enabled)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2062431952u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in enabled, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2062431952u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				SetNetworkTransformComponentsEnabledLocal(enabled);
			}
		}

		private void SetNetworkTransformComponentsEnabledLocal(bool enabled)
		{
			if (_networkTransform != null)
			{
				_networkTransform.enabled = enabled;
			}
			if (_networkTransformHead != null)
			{
				_networkTransformHead.enabled = enabled;
			}
			if (_networkTransformSpine != null)
			{
				_networkTransformSpine.enabled = enabled;
			}
			if (_footIK != null)
			{
				_footIK.enabled = enabled;
			}
		}

		private void BindCamShakeConnection()
		{
			if (!(_camShakeConnectionSO == null))
			{
				_camShakeConnection = _camShakeConnectionSO.GetConnection() as CamShakeConnection;
				if (_camShakeConnection != null)
				{
					_camShakeConnection.RemoveChangeListener(OnCamShakeSettingChanged);
					_camShakeConnection.AddChangeListener(OnCamShakeSettingChanged);
				}
			}
		}

		private void UnbindCamShakeConnection()
		{
			if (_camShakeConnection != null)
			{
				_camShakeConnection.RemoveChangeListener(OnCamShakeSettingChanged);
				_camShakeConnection = null;
			}
		}

		private void OnCamShakeSettingChanged(bool enabled)
		{
			if (!enabled)
			{
				StopAllCameraShakeImmediate();
			}
		}

		private void BindRenderDistanceConnection()
		{
			if (!(_renderDistanceConnectionSO == null))
			{
				_renderDistanceConnection = _renderDistanceConnectionSO.GetConnection() as RenderDistanceConnection;
				if (_renderDistanceConnection != null)
				{
					_renderDistanceConnection.RemoveChangeListener(OnRenderDistanceSettingChanged);
					_renderDistanceConnection.AddChangeListener(OnRenderDistanceSettingChanged);
					ApplyRenderDistanceSetting(_renderDistanceConnection.Get());
				}
			}
		}

		private void UnbindRenderDistanceConnection()
		{
			if (_renderDistanceConnection != null)
			{
				_renderDistanceConnection.RemoveChangeListener(OnRenderDistanceSettingChanged);
				_renderDistanceConnection = null;
			}
		}

		private void OnRenderDistanceSettingChanged(float distance)
		{
			ApplyRenderDistanceSetting(distance);
		}

		private void BindCameraComfortSettings()
		{
			RuntimeSettings.FieldOfViewChanged -= OnFieldOfViewSettingChanged;
			RuntimeSettings.FieldOfViewChanged += OnFieldOfViewSettingChanged;
			RuntimeSettings.DynamicFovEffectsChanged -= OnDynamicFovEffectsSettingChanged;
			RuntimeSettings.DynamicFovEffectsChanged += OnDynamicFovEffectsSettingChanged;
		}

		private void UnbindCameraComfortSettings()
		{
			RuntimeSettings.FieldOfViewChanged -= OnFieldOfViewSettingChanged;
			RuntimeSettings.DynamicFovEffectsChanged -= OnDynamicFovEffectsSettingChanged;
		}

		private void OnFieldOfViewSettingChanged(float fieldOfView)
		{
			SetBaseCameraFov(fieldOfView, 0.1f);
		}

		private void OnDynamicFovEffectsSettingChanged(bool enabled)
		{
			if (!enabled)
			{
				ResetCameraFov(0.15f);
			}
		}

		private void SetupCamera()
		{
			_playerCamera.enabled = base.IsOwner;
			_playerCamera.tag = ((!base.IsOwner) ? "Untagged" : "MainCamera");
			_viewModelCamera.enabled = base.IsOwner;
			_viewModelCamera.tag = "Untagged";
			_viewModelCamera.gameObject.SetActive(base.IsOwner);
			_lookAtConstraint.enabled = base.IsOwner;
			_lookAtConstraint2.enabled = base.IsOwner;
			_audioListener.enabled = base.IsOwner;
			_outliner.enabled = base.IsOwner;
			_additionalCameraData.enabled = base.IsOwner;
			_cinemachineBrain.enabled = base.IsOwner;
			_cinemachineCamera.enabled = base.IsOwner;
			_impulseSource.enabled = base.IsOwner;
			_sway.enabled = base.IsOwner;
			if (base.IsOwner)
			{
				SetActiveCamera();
				CursorHelper.CursorVisible = false;
				CinemachineComponentBase cinemachineComponent = _cinemachineCamera.GetCinemachineComponent(CinemachineCore.Stage.Noise);
				if (cinemachineComponent != null)
				{
					_cmPerlin = cinemachineComponent.GetComponent<CinemachineBasicMultiChannelPerlin>();
				}
				if (_cmPerlin != null)
				{
					_baseShakeAmplitude = _cmPerlin.AmplitudeGain;
					_baseShakeFrequency = _cmPerlin.FrequencyGain;
					_fishingShakeAmplitudeCurrent = 0f;
					_fishingShakeFrequencyCurrent = 0f;
					_oneShotShakeAmplitudeCurrent = 0f;
					_oneShotShakeFrequencyCurrent = 0f;
					ApplyFinalCameraShake();
				}
				_crouchYOffsetCurrent = 0.175f;
				ResetLandingPolishImmediate();
				_baseGameplayFovCurrent = RuntimeSettings.FieldOfView;
				_baseGameplayFovTarget = _baseGameplayFovCurrent;
				ApplyGameplayFov();
				ApplyRenderDistanceSetting(RuntimeSettings.RenderDistance);
			}
		}

		private void ApplyRenderDistanceSetting(float distance)
		{
			if (base.IsOwner)
			{
				float num = Mathf.Max(1f, distance);
				if (_playerCamera != null)
				{
					_playerCamera.farClipPlane = num;
					// The retained town has no matching baked occlusion after the
					// decorative buildings were removed from the authoring scenes.
					_playerCamera.useOcclusionCulling = false;
				}
				ApplyCinemachineFarClip(_cinemachineCamera, num);
				ApplyCinemachineFarClip(_emoteCamera, num);
				ApplyCinemachineFarClip(_tottiCamera, num);
				ApplyCinemachineFarClip(_actionCamera, num);
				ApplyCinemachineFarClip(_tpsActionCamera, num);
			}
		}

		private static void ApplyCinemachineFarClip(CinemachineCamera cinemachineCamera, float distance)
		{
			if (!(cinemachineCamera == null))
			{
				cinemachineCamera.Lens.FarClipPlane = distance;
			}
		}

		private void SetActiveCamera()
		{
			_activeCinemachineCam = ((_cinemachineBrain != null) ? (_cinemachineBrain.ActiveVirtualCamera as CinemachineCamera) : null) ?? ((_cinemachineCamera != null && _cinemachineCamera.enabled) ? _cinemachineCamera : null) ?? ((_actionCamera != null && _actionCamera.enabled) ? _actionCamera : null) ?? ((_tpsActionCamera != null && _tpsActionCamera.enabled) ? _tpsActionCamera : null) ?? ((_emoteCamera != null && _emoteCamera.enabled) ? _emoteCamera : null) ?? ((_tottiCamera != null && _tottiCamera.enabled) ? _tottiCamera : null);
		}

		private CinemachineCamera ResolveGameplayFovCamera()
		{
			if (_cinemachineCamera != null && _cinemachineCamera.enabled)
			{
				return _cinemachineCamera;
			}
			if (_actionCamera != null && _actionCamera.enabled)
			{
				return _actionCamera;
			}
			if (_tpsActionCamera != null && _tpsActionCamera.enabled)
			{
				return _tpsActionCamera;
			}
			if (_emoteCamera != null && _emoteCamera.enabled)
			{
				return _emoteCamera;
			}
			if (_tottiCamera != null && _tottiCamera.enabled)
			{
				return _tottiCamera;
			}
			if (_activeCinemachineCam != null)
			{
				return _activeCinemachineCam;
			}
			if (_cinemachineBrain != null)
			{
				return _cinemachineBrain.ActiveVirtualCamera as CinemachineCamera;
			}
			return null;
		}

		private void ApplyGameplayFov()
		{
			CinemachineCamera cinemachineCamera = ResolveGameplayFovCamera();
			if (!(cinemachineCamera == null))
			{
				cinemachineCamera.Lens.FieldOfView = _baseGameplayFovCurrent;
			}
		}

		public void ResetCameraFov(float duration = 0.25f)
		{
			SetBaseCameraFov(RuntimeSettings.FieldOfView, duration);
		}

		public void SetTransientCameraFovOffset(float offset, float duration = 0.25f, bool instant = false)
		{
			SetCameraFov(RuntimeSettings.FieldOfView + offset, duration, instant);
		}

		public void SetCameraCrouch(bool status)
		{
			float num = (status ? (-0.3f) : 0.175f);
			_crouchOffsetTween?.Kill();
			_crouchOffsetTween = DOTween.To(() => _crouchYOffsetCurrent, (float x) =>
			{
				_crouchYOffsetCurrent = x;
				ApplyCameraLocalPose();
			}, num, 0.25f).SetEase(Ease.OutQuad);
			UtilityMethods.Log($"[PPG_Player_Camera] crouchOffsetY:{num}");
		}

		private void ApplyCameraLocalPose()
		{
			if (base.IsOwner && !(_cinemachineCamera == null))
			{
				Vector3 localPosition = _bobPosCurrent + _landingPosOffsetCurrent;
				localPosition.y += _crouchYOffsetCurrent;
				Vector3 euler = _bobRotCurrent + _landingRotOffsetCurrent;
				if (_cameraLookMode == CameraLookMode.FreeLook)
				{
					euler.x += _freeLookPitch;
					euler.y += _freeLookYaw;
				}
				_cinemachineCamera.transform.localPosition = localPosition;
				_cinemachineCamera.transform.localRotation = Quaternion.Euler(euler);
			}
		}

		public void SwitchToTpsCam()
		{
			if (!_emoteCamera.enabled)
			{
				ShowPlayer();
				PauseLook();
				_cinemachineCamera.enabled = false;
				_tottiCamera.enabled = false;
				_emoteCamera.enabled = true;
				SetActiveCamera();
			}
		}

		public void SwitchToTottiCam()
		{
			if (!_tottiCamera.enabled)
			{
				ShowPlayer();
				PauseLook();
				_cinemachineCamera.enabled = false;
				_emoteCamera.enabled = false;
				_tottiCamera.enabled = true;
				SetActiveCamera();
			}
		}

		public void SwitchToFpsCam()
		{
			if (!_cinemachineCamera.enabled)
			{
				ShowViewModel();
				ResumeLook();
				_emoteCamera.enabled = false;
				_tottiCamera.enabled = false;
				_cinemachineCamera.enabled = true;
				SetActiveCamera();
			}
		}

		private void ShowPlayer()
		{
			_viewModelCamera.enabled = false;
			_playerCamera.cullingMask |= 1 << LayerMask.NameToLayer("Player");
		}

		private void ShowViewModel()
		{
			_viewModelCamera.enabled = true;
			_playerCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Player"));
		}

		private void HideEmoteAreaFpsLocalVisuals()
		{
			SetViewModelCameraVisible(visible: false);
			RemovePlayerCameraLayer("ViewModel");
			RemovePlayerCameraLayer("Player");
			if (_fpsAnimator != null)
			{
				UtilityMethods.SetLayerRecursive(_fpsAnimator.gameObject, "ViewModel", includeChildren: true);
			}
		}

		private void RestoreEmoteAreaFpsLocalVisuals()
		{
			if (_fpsAnimator != null)
			{
				UtilityMethods.SetLayerRecursive(_fpsAnimator.gameObject, base.IsOwner ? "ViewModel" : "ClientPlayer", includeChildren: true);
			}
		}

		private void SetViewModelCameraVisible(bool visible)
		{
			if (_viewModelCamera != null)
			{
				_viewModelCamera.enabled = visible;
			}
		}

		private void RemovePlayerCameraLayer(string layerName)
		{
			if (!(_playerCamera == null))
			{
				int num = LayerMask.NameToLayer(layerName);
				if (num >= 0)
				{
					_playerCamera.cullingMask &= ~(1 << num);
				}
			}
		}

		private void FishingCameraShakeLateTick()
		{
			if (!base.IsOwner || !_enableFishingCameraShake || _cmPerlin == null)
			{
				return;
			}
			if (!RuntimeSettings.CameraShakeEnabled)
			{
				_isFishingShakeActive = false;
				_fishingShakeAmplitudeCurrent = 0f;
				_fishingShakeAmplitudeVel = 0f;
				_fishingShakeFrequencyCurrent = 0f;
				_fishingShakeFrequencyVel = 0f;
				ApplyFinalCameraShake();
				return;
			}
			float num = (_fishingShakeUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
			if (!(num <= 0f))
			{
				float target = (_isFishingShakeActive ? _fishingShakeAmplitude : 0f);
				float target2 = (_isFishingShakeActive ? _fishingShakeFrequency : 0f);
				_fishingShakeAmplitudeCurrent = Mathf.SmoothDamp(_fishingShakeAmplitudeCurrent, target, ref _fishingShakeAmplitudeVel, _fishingShakeBlendTime, float.PositiveInfinity, num);
				_fishingShakeFrequencyCurrent = Mathf.SmoothDamp(_fishingShakeFrequencyCurrent, target2, ref _fishingShakeFrequencyVel, _fishingShakeBlendTime, float.PositiveInfinity, num);
				ApplyFinalCameraShake();
			}
		}

		public void SetCameraBobState(bool isMoving, float speed01, bool isRunning, float strafe01 = 0f)
		{
			if (_cameraBobSuppressed)
			{
				_bobIsMoving = false;
				_bobSpeed01 = 0f;
				_bobIsRunning = false;
				_bobStrafe01 = 0f;
			}
			else
			{
				_bobIsMoving = isMoving;
				_bobSpeed01 = Mathf.Clamp01(speed01);
				_bobIsRunning = isRunning;
				_bobStrafe01 = Mathf.Clamp(strafe01, -1f, 1f);
			}
		}

		public void SetCameraBobSuppressed(bool suppressed)
		{
			if (_cameraBobSuppressed != suppressed)
			{
				_cameraBobSuppressed = suppressed;
				if (suppressed)
				{
					SetCameraBobState(isMoving: false, 0f, isRunning: false);
				}
			}
		}

		private void CameraBobLateTick()
		{
			if (!base.IsOwner || _cinemachineCamera == null)
			{
				return;
			}
			float num = (_bobUseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
			if (num <= 0f)
			{
				ApplyCameraLocalPose();
				return;
			}
			if (_enableCameraBob && RuntimeSettings.CameraBobEnabled)
			{
				float num2 = (_bobIsMoving ? _bobSpeed01 : 0f);
				float b = (_bobIsRunning ? _runBobFrequency : _walkBobFrequency);
				b = Mathf.Max(0.01f, b);
				_bobPhase += num * b * Mathf.Lerp(0.2f, 1f, num2);
				float num3 = Mathf.Sin(_bobPhase * MathF.PI * 2f);
				float num4 = Mathf.Cos(_bobPhase * MathF.PI * 2f);
				float num5 = _bobStrafe01 * _strafeBobBias;
				Vector3 target = new Vector3((num4 * 0.55f + num3 * 0.2f) * _bobPositionAmount * 0.55f * num5, Mathf.Abs(num3) * _bobPositionAmount, num4 * 0.35f * _bobPositionAmount * 0.35f) * num2;
				Vector3 target2 = new Vector3((0f - num4) * 0.85f * _bobRotationAmount, num3 * 0.35f * _bobRotationAmount * 0.25f * num5, (0f - num3) * 0.95f * _bobRotationAmount * 0.55f * num5) * num2;
				_bobPosCurrent = Vector3.SmoothDamp(_bobPosCurrent, target, ref _bobPosVel, _bobSmoothTime, float.PositiveInfinity, num);
				_bobRotCurrent = Vector3.SmoothDamp(_bobRotCurrent, target2, ref _bobRotVel, _bobSmoothTime, float.PositiveInfinity, num);
			}
			else
			{
				_bobPosCurrent = Vector3.SmoothDamp(_bobPosCurrent, Vector3.zero, ref _bobPosVel, _bobSmoothTime, float.PositiveInfinity, num);
				_bobRotCurrent = Vector3.SmoothDamp(_bobRotCurrent, Vector3.zero, ref _bobRotVel, _bobSmoothTime, float.PositiveInfinity, num);
			}
			LandingPolishLateTick(num);
			ApplyCameraLocalPose();
		}

		private void ApplyFinalCameraShake()
		{
			if (!(_cmPerlin == null))
			{
				if (!RuntimeSettings.CameraShakeEnabled)
				{
					_cmPerlin.AmplitudeGain = 0f;
					_cmPerlin.FrequencyGain = 0f;
					return;
				}
				float amplitudeGain = _baseShakeAmplitude + _fishingShakeAmplitudeCurrent + _oneShotShakeAmplitudeCurrent;
				float frequencyGain = Mathf.Max(_baseShakeFrequency, _fishingShakeFrequencyCurrent, _oneShotShakeFrequencyCurrent);
				_cmPerlin.AmplitudeGain = amplitudeGain;
				_cmPerlin.FrequencyGain = frequencyGain;
			}
		}

		public void StartFishingCameraShake(float amplitude = -1f, float frequency = -1f)
		{
			if (!base.IsOwner)
			{
				return;
			}
			if (!RuntimeSettings.CameraShakeEnabled)
			{
				UtilityMethods.Log("[RuntimeSettings] CameraShakeEnabled is false!");
				return;
			}
			_isFishingShakeActive = true;
			if (amplitude >= 0f)
			{
				_fishingShakeAmplitude = amplitude;
			}
			if (frequency >= 0f)
			{
				_fishingShakeFrequency = frequency;
			}
		}

		public void StopFishingCameraShake()
		{
			if (base.IsOwner)
			{
				_isFishingShakeActive = false;
			}
		}

		public void CameraShake(ShakeIntensity intensity, ShakeDuration duration)
		{
			if (!RuntimeSettings.CameraShakeEnabled)
			{
				UtilityMethods.Log("[RuntimeSettings] CameraShakeEnabled is false!");
				return;
			}
			GetShakePreset(intensity, duration, out var amplitude, out var frequency, out var attack, out var sustain, out var decay);
			CameraShake(amplitude, frequency, attack, sustain, decay);
		}

		private static void GetShakePreset(ShakeIntensity intensity, ShakeDuration duration, out float amplitude, out float frequency, out float attack, out float sustain, out float decay)
		{
			switch (intensity)
			{
			case ShakeIntensity.Light:
				amplitude = 0.35f;
				frequency = 1.4f;
				break;
			case ShakeIntensity.Heavy:
				amplitude = 1.6f;
				frequency = 3.1f;
				break;
			default:
				amplitude = 0.85f;
				frequency = 2.2f;
				break;
			}
			switch (duration)
			{
			case ShakeDuration.Instant:
				attack = 0.03f;
				sustain = 0.04f;
				decay = 0.1f;
				break;
			case ShakeDuration.Long:
				attack = 0.07f;
				sustain = 0.22f;
				decay = 0.45f;
				break;
			default:
				attack = 0.05f;
				sustain = 0.1f;
				decay = 0.22f;
				break;
			}
		}

		public void CameraShake(float amplitude = 1.2f, float frequency = 2f, float attack = 0.05f, float sustain = 0.08f, float decay = 0.18f, Ease attackEase = Ease.OutQuad, Ease decayEase = Ease.OutQuad, bool useUnscaledTime = true)
		{
			if (!base.IsOwner)
			{
				return;
			}
			if (!RuntimeSettings.CameraShakeEnabled)
			{
				UtilityMethods.Log("[RuntimeSettings] CameraShakeEnabled is false!");
				return;
			}
			if (_cmPerlin == null)
			{
				UtilityMethods.Log("[PPG_Player_Camera] CameraShake perlin is null!", LogType.Warning);
				return;
			}
			amplitude = Mathf.Max(0f, amplitude);
			frequency = Mathf.Max(0f, frequency);
			attack = Mathf.Max(0f, attack);
			sustain = Mathf.Max(0f, sustain);
			decay = Mathf.Max(0f, decay);
			_shakeTween?.Kill();
			_shakeTween = null;
			_oneShotAmpTween?.Kill();
			_oneShotFreqTween?.Kill();
			_oneShotShakeAmplitudeCurrent = 0f;
			_oneShotShakeFrequencyCurrent = 0f;
			ApplyFinalCameraShake();
			Sequence sequence = DOTween.Sequence();
			if (useUnscaledTime)
			{
				sequence.SetUpdate(isIndependentUpdate: true);
			}
			if (attack > 0f)
			{
				_oneShotAmpTween = DOTween.To(() => _oneShotShakeAmplitudeCurrent, (float x) =>
				{
					_oneShotShakeAmplitudeCurrent = x;
					ApplyFinalCameraShake();
				}, amplitude, attack).SetEase(attackEase);
				_oneShotFreqTween = DOTween.To(() => _oneShotShakeFrequencyCurrent, (float x) =>
				{
					_oneShotShakeFrequencyCurrent = x;
					ApplyFinalCameraShake();
				}, frequency, attack).SetEase(attackEase);
				if (useUnscaledTime)
				{
					_oneShotAmpTween.SetUpdate(isIndependentUpdate: true);
					_oneShotFreqTween.SetUpdate(isIndependentUpdate: true);
				}
				sequence.Append(_oneShotAmpTween);
				sequence.Join(_oneShotFreqTween);
			}
			else
			{
				_oneShotShakeAmplitudeCurrent = amplitude;
				_oneShotShakeFrequencyCurrent = frequency;
				ApplyFinalCameraShake();
			}
			if (sustain > 0f)
			{
				sequence.AppendInterval(sustain);
			}
			if (decay > 0f)
			{
				_oneShotAmpTween = DOTween.To(() => _oneShotShakeAmplitudeCurrent, (float x) =>
				{
					_oneShotShakeAmplitudeCurrent = x;
					ApplyFinalCameraShake();
				}, 0f, decay).SetEase(decayEase);
				_oneShotFreqTween = DOTween.To(() => _oneShotShakeFrequencyCurrent, (float x) =>
				{
					_oneShotShakeFrequencyCurrent = x;
					ApplyFinalCameraShake();
				}, 0f, decay).SetEase(decayEase);
				if (useUnscaledTime)
				{
					_oneShotAmpTween.SetUpdate(isIndependentUpdate: true);
					_oneShotFreqTween.SetUpdate(isIndependentUpdate: true);
				}
				sequence.Append(_oneShotAmpTween);
				sequence.Join(_oneShotFreqTween);
			}
			else
			{
				_oneShotShakeAmplitudeCurrent = 0f;
				_oneShotShakeFrequencyCurrent = 0f;
				ApplyFinalCameraShake();
			}
			sequence.OnComplete(() =>
			{
				_oneShotShakeAmplitudeCurrent = 0f;
				_oneShotShakeFrequencyCurrent = 0f;
				ApplyFinalCameraShake();
			});
			_shakeTween = sequence;
		}

		public void SetCameraFov(float targetFov, float duration = 0.25f, bool instant = false, CinemachineCamera customCam = null)
		{
			if (!base.IsOwner)
			{
				return;
			}
			_zoomRoutine = null;
			if (customCam != null)
			{
				if (!RuntimeSettings.DynamicFovEffectsEnabled)
				{
					targetFov = RuntimeSettings.FieldOfView;
				}
				_fovTween?.Kill();
				if (instant || duration <= 0f)
				{
					customCam.Lens.FieldOfView = targetFov;
					return;
				}
				_fovTween = DOTween.To(() => customCam.Lens.FieldOfView, (float x) =>
				{
					customCam.Lens.FieldOfView = x;
				}, targetFov, duration).SetEase(Ease.OutQuad);
			}
			else if (!RuntimeSettings.DynamicFovEffectsEnabled)
			{
				SetBaseCameraFov(RuntimeSettings.FieldOfView, duration, instant);
			}
			else
			{
				SetBaseCameraFov(targetFov, duration, instant);
			}
		}

		private void SetBaseCameraFov(float targetFov, float duration = 0.25f, bool instant = false)
		{
			if (!base.IsOwner || ResolveGameplayFovCamera() == null)
			{
				return;
			}
			_baseGameplayFovTarget = targetFov;
			_fovTween?.Kill();
			if (instant || duration <= 0f)
			{
				_baseGameplayFovCurrent = targetFov;
				ApplyGameplayFov();
				return;
			}
			_fovTween = DOTween.To(() => _baseGameplayFovCurrent, (float x) =>
			{
				_baseGameplayFovCurrent = x;
				ApplyGameplayFov();
			}, targetFov, duration).SetEase(Ease.OutQuad);
		}

		public void SetActionCamera(bool status)
		{
			_actionCamera.enabled = status;
			SetActiveCamera();
			if (status)
			{
				_playerCamera.cullingMask |= 1 << LayerMask.NameToLayer("ViewModel");
				_viewModelCamera.enabled = false;
				SetCameraFov(Mathf.Max(30f, RuntimeSettings.FieldOfView - 20f), 3.5f, instant: false, _actionCamera);
			}
			else
			{
				SetCameraFov(RuntimeSettings.FieldOfView, 1f, instant: false, _actionCamera);
				_playerCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("ViewModel"));
				_viewModelCamera.enabled = true;
			}
		}

		public void SetTPSActionCamera(bool status)
		{
			_tpsActionCamera.enabled = status;
			SetActiveCamera();
			if (status)
			{
				_lookAtConstraint.enabled = false;
				_lookAtConstraint2.enabled = false;
				_viewModelCamera.enabled = false;
				_playerCamera.cullingMask |= 1 << LayerMask.NameToLayer("Player");
				_playerVisuals?.SetTpsHeadMeshesVisible(visible: false);
				_divingEquipment?.SetTpsHeadGraphicVisible(visible: false);
			}
			else
			{
				ApplyConstraints(ConstraintStatus.Value);
				_viewModelCamera.enabled = true;
				_playerCamera.cullingMask &= ~(1 << LayerMask.NameToLayer("Player"));
				_playerVisuals?.SetTpsHeadMeshesVisible(visible: true);
				_divingEquipment?.SetTpsHeadGraphicVisible(visible: true);
			}
		}

		public void SetCameraLookMode(CameraLookMode mode)
		{
			_cameraLookMode = mode;
			switch (mode)
			{
			case CameraLookMode.CharacterFollow:
				_freeLookYaw = 0f;
				_freeLookPitch = 0f;
				if (_cinemachineCamera != null)
				{
					_cinemachineCamera.transform.localRotation = Quaternion.identity;
				}
				if (_tpsActionCamera != null)
				{
					_tpsActionCamera.transform.localRotation = _freeLookBaseLocalRotation;
				}
				UtilityMethods.SetLayerRecursive(_fpsAnimator.gameObject, base.IsOwner ? "ViewModel" : "ClientPlayer", includeChildren: true);
				break;
			case CameraLookMode.FreeLook:
			{
				CinemachineCamera cinemachineCamera = ResolveEmoteFpsLookCamera();
				if (cinemachineCamera != null)
				{
					_freeLookBaseLocalRotation = cinemachineCamera.transform.localRotation;
					_freeLookYaw = 0f;
					_freeLookPitch = 0f;
					UtilityMethods.SetLayerRecursive(_fpsAnimator.gameObject, "ClientPlayer", includeChildren: true);
				}
				break;
			}
			}
		}

		public void EnableFreeLook()
		{
			SetCameraLookMode(CameraLookMode.FreeLook);
			if (_character != null)
			{
				_character.IsTottiControlled = true;
			}
		}

		public void DisableFreeLook()
		{
			SetCameraLookMode(CameraLookMode.CharacterFollow);
			if (_character != null)
			{
				_character.IsTottiControlled = false;
			}
		}

		public void BeginLimitedFreeLook(float maxYawDegrees)
		{
			minYaw = 0f - Mathf.Abs(maxYawDegrees);
			maxYaw = Mathf.Abs(maxYawDegrees);
			SetCameraLookMode(CameraLookMode.FreeLook);
		}

		public void ApplyLimitedFreeLookDelta(Vector2 delta, float maxPitchDegrees)
		{
			ApplyFreeLookDelta(delta, invertLook: false, 0f - Mathf.Abs(maxPitchDegrees), Mathf.Abs(maxPitchDegrees));
		}

		public void EndLimitedFreeLook()
		{
			SetCameraLookMode(CameraLookMode.CharacterFollow);
		}

		public void ApplyFreeLookDelta(Vector2 delta, bool invertLook, float minPitch, float maxPitch)
		{
			if (base.IsOwner && _cameraLookMode == CameraLookMode.FreeLook)
			{
				_freeLookYaw += delta.x;
				_freeLookPitch += (invertLook ? (0f - delta.y) : delta.y);
				_freeLookPitch = Mathf.Clamp(_freeLookPitch, minPitch, maxPitch);
				_freeLookYaw = Mathf.Clamp(_freeLookYaw, minYaw, maxYaw);
				_freeLookMinPitch = minPitch;
				_freeLookMaxPitch = maxPitch;
				ApplyFreeLookRotation();
			}
		}

		public bool TryApplyEmoteFpsLookDelta(Vector2 delta)
		{
			if (_cameraLookMode != CameraLookMode.FreeLook)
			{
				return false;
			}
			ApplyFreeLookDelta(delta, invertLook: false, _freeLookMinPitch, _freeLookMaxPitch);
			return true;
		}

		private void EnableEmoteFpsLook()
		{
			EnableEmoteFpsLook(_emoteFpsMaxYaw, _emoteFpsMaxPitch, useWorldUpYaw: false);
		}

		private void EnableEmoteFpsLook(float maxYawDegrees, float maxPitchDegrees, bool useWorldUpYaw)
		{
			minYaw = 0f - Mathf.Abs(maxYawDegrees);
			maxYaw = Mathf.Abs(maxYawDegrees);
			_freeLookMinPitch = 0f - Mathf.Abs(maxPitchDegrees);
			_freeLookMaxPitch = Mathf.Abs(maxPitchDegrees);
			_freeLookUseWorldUpYaw = useWorldUpYaw;
			SetCameraLookMode(CameraLookMode.FreeLook);
		}

		private void DisableEmoteFpsLook()
		{
			_freeLookUseWorldUpYaw = false;
			SetCameraLookMode(CameraLookMode.CharacterFollow);
		}

		private void ApplyFreeLookRotation()
		{
			CinemachineCamera cinemachineCamera = ResolveEmoteFpsLookCamera();
			if (!(cinemachineCamera == null))
			{
				_freeLookPitch = Mathf.Clamp(_freeLookPitch, _freeLookMinPitch, _freeLookMaxPitch);
				Quaternion quaternion = Quaternion.AngleAxis(_freeLookPitch, Vector3.right);
				if (!_freeLookUseWorldUpYaw || cinemachineCamera.transform.parent == null)
				{
					Quaternion quaternion2 = Quaternion.AngleAxis(_freeLookYaw, Vector3.up) * quaternion;
					cinemachineCamera.transform.localRotation = _freeLookBaseLocalRotation * quaternion2;
				}
				else
				{
					Quaternion quaternion3 = cinemachineCamera.transform.parent.rotation * _freeLookBaseLocalRotation;
					cinemachineCamera.transform.rotation = Quaternion.AngleAxis(_freeLookYaw, Vector3.up) * quaternion3 * quaternion;
				}
			}
		}

		private void EmoteFreeLookLateTick()
		{
			if (_cameraLookMode == CameraLookMode.FreeLook && _freeLookUseWorldUpYaw)
			{
				ApplyFreeLookRotation();
			}
		}

		private CinemachineCamera ResolveEmoteFpsLookCamera()
		{
			if (_tpsActionCamera != null && _tpsActionCamera.enabled)
			{
				return _tpsActionCamera;
			}
			if (_cinemachineCamera != null && _cinemachineCamera.enabled)
			{
				return _cinemachineCamera;
			}
			return null;
		}

		private void StopAllCameraShakeImmediate()
		{
			_isFishingShakeActive = false;
			_shakeTween?.Kill();
			_shakeTween = null;
			_oneShotAmpTween?.Kill();
			_oneShotFreqTween?.Kill();
			_fishingShakeAmplitudeCurrent = 0f;
			_fishingShakeAmplitudeVel = 0f;
			_fishingShakeFrequencyCurrent = 0f;
			_fishingShakeFrequencyVel = 0f;
			_oneShotShakeAmplitudeCurrent = 0f;
			_oneShotShakeFrequencyCurrent = 0f;
			ApplyFinalCameraShake();
		}

		public void PlayLandingCameraPolish(float impactSpeed)
		{
			if (base.IsOwner && _enableLandingPolish && RuntimeSettings.LandingCameraMotionEnabled && !(_cinemachineCamera == null))
			{
				float num = Mathf.InverseLerp(_landingMinImpactSpeed, _landingMaxImpactSpeed, impactSpeed);
				if (!(num <= 0f))
				{
					num = Mathf.SmoothStep(0f, 1f, num);
					_landingWeightTarget = Mathf.Max(_landingWeightTarget, num);
					_landingReturning = false;
				}
			}
		}

		private void LandingPolishLateTick(float dt)
		{
			if (!_enableLandingPolish || !RuntimeSettings.LandingCameraMotionEnabled)
			{
				ResetLandingPolishImmediate();
			}
			else if (!(dt <= 0f))
			{
				float smoothTime = (_landingReturning ? _landingSmoothTime : Mathf.Max(0.025f, _landingSmoothTime * 0.35f));
				_landingWeightCurrent = Mathf.SmoothDamp(_landingWeightCurrent, _landingWeightTarget, ref _landingWeightVelocity, smoothTime, float.PositiveInfinity, dt);
				if (!_landingReturning && Mathf.Abs(_landingWeightCurrent - _landingWeightTarget) <= 0.015f)
				{
					_landingWeightTarget = 0f;
					_landingReturning = true;
				}
				if (_landingReturning && _landingWeightCurrent <= 0.001f)
				{
					ResetLandingPolishImmediate();
					return;
				}
				float num = Mathf.Max(0f, _landingAmount);
				float num2 = _landingWeightCurrent * num;
				_landingPosOffsetCurrent = new Vector3(0f, -0.07f * num2, 0f);
				_landingRotOffsetCurrent = new Vector3(3f * num2, 0f, 0f);
			}
		}

		private void ResetLandingPolishImmediate()
		{
			_landingWeightCurrent = 0f;
			_landingWeightTarget = 0f;
			_landingWeightVelocity = 0f;
			_landingReturning = false;
			_landingPosOffsetCurrent = Vector3.zero;
			_landingRotOffsetCurrent = Vector3.zero;
		}

		public void DisableAudioListener()
		{
			_audioListener.enabled = false;
		}

		public void SetVisibility(bool v)
		{
			_canvasParent.SetActive(v);
		}

		private void SetupCanvas()
		{
			if (base.IsOwner && _damageFeedback != null)
			{
				_damageFeedback.alpha = 0f;
			}
			if (_canvasParent != null)
			{
				_canvasParent.SetActive(base.IsOwner);
			}
			if (_playerNameText != null)
			{
				_playerNameText.enabled = !base.IsOwner;
			}
			BindPlayerNamesConnection();
			_alwaysShowPlayerNames = RuntimeSettings.PlayerNamesEnabled;
		}

		private void BindPlayerNamesConnection()
		{
			if (!(_playerNamesConnectionSO == null))
			{
				_playerNamesConnection = _playerNamesConnectionSO.GetConnection() as PlayerNamesConnection;
				if (_playerNamesConnection != null)
				{
					_playerNamesConnection.RemoveChangeListener(OnPlayerNamesSettingChanged);
					_playerNamesConnection.AddChangeListener(OnPlayerNamesSettingChanged);
					_alwaysShowPlayerNames = _playerNamesConnection.Get();
				}
			}
		}

		private void UnbindPlayerNamesConnection()
		{
			if (_playerNamesConnection != null)
			{
				_playerNamesConnection.RemoveChangeListener(OnPlayerNamesSettingChanged);
				_playerNamesConnection = null;
			}
		}

		private void OnPlayerNamesSettingChanged(bool alwaysShow)
		{
			_alwaysShowPlayerNames = alwaysShow;
			RefreshPlayerNameVisibility();
		}

		private void RefreshPlayerNameVisibility()
		{
			if (base.IsOwner)
			{
				SetPlayerNameVisible(visible: false);
				return;
			}
			bool playerNameVisible = _alwaysShowPlayerNames || _contextPlayerNameVisible;
			SetPlayerNameVisible(playerNameVisible);
		}

		private void SetPlayerName(string name)
		{
			if (!(_playerNameText == null))
			{
				_playerNameText.text = (string.IsNullOrWhiteSpace(name) ? $"Client {base.OwnerClientId}" : name);
			}
		}

		private void SetPlayerNameVisible(bool visible)
		{
			if (!base.IsOwner && _playerNameText != null)
			{
				_playerNameText.enabled = visible;
			}
		}

		public void SetContextPlayerNameVisible(bool visible)
		{
			_contextPlayerNameVisible = visible;
			RefreshPlayerNameVisibility();
		}

		public void ShowCursor()
		{
			_cursorRequestedVisible = true;
			RefreshCursorVisibility();
		}

		public void HideCursor()
		{
			_cursorRequestedVisible = false;
			RefreshCursorVisibility();
		}

		private void SetSurfCursorSuppressed(bool suppressed)
		{
			if (suppressed && !_surfCursorSuppressed && _chParent != null)
			{
				_cursorRequestedVisible = _chParent.activeSelf;
			}
			_surfCursorSuppressed = suppressed;
			RefreshCursorVisibility();
		}

		private void RefreshCursorVisibility()
		{
			if (_chParent != null)
			{
				_chParent.SetActive(_cursorRequestedVisible && !_surfCursorSuppressed);
			}
		}

		public void ChangePTTChildStatus(bool s)
		{
			if (!(_pttChildObject == null))
			{
				_pttChildObject.SetActive(s);
			}
		}

		public void ForKitchenUsable(bool value)
		{
			_cursorRequestedVisible = value;
			RefreshCursorVisibility();
			_pttObject.SetActive(value);
		}

		public void UIHitFeedback()
		{
			_hitTween?.Kill();
			_chHit.SetActive(value: true);
			_chHit.transform.localScale = Vector3.one * 0.8f;
			_hitTween = _chHit.transform.DOScale(1.2f, _hitDuration).SetEase(Ease.OutBack).OnComplete(() =>
			{
				_chHit.SetActive(value: false);
			});
		}

		public void UICenterFeedback(bool willHit)
		{
			if (_willHit != willHit)
			{
				_willHit = willHit;
				_chCenter.DOColor(_willHit ? _chCenterHit : _chCenterDefault, _colorDuration).SetEase(Ease.Linear);
			}
		}

		public void UIAimFeedback(bool status)
		{
			Vector3 endValue = (status ? Vector3.zero : Vector3.one);
			_chCirlce.DOScale(endValue, _aimDuration).SetEase(status ? Ease.InBack : Ease.OutBack);
		}

		public void DamageFeedback()
		{
			if (base.IsOwner)
			{
				_damageFeedback.DOKill();
				_damageFeedback.alpha = 1f;
				_damageFeedback.DOFade(0f, 2f).SetEase(Ease.OutBack);
			}
		}

		public void RefreshExpandingPulseFeedback()
		{
			if (base.IsOwner && !(_canvasParent == null))
			{
				if (_expandingPulseFeedback == null)
				{
					GameObject gameObject = new GameObject("ExpandingPulseFeedback", typeof(RectTransform));
					gameObject.transform.SetParent(_canvasParent.transform, worldPositionStays: false);
					gameObject.transform.SetAsFirstSibling();
					RectTransform component = gameObject.GetComponent<RectTransform>();
					component.anchorMin = Vector2.zero;
					component.anchorMax = Vector2.one;
					component.offsetMin = Vector2.zero;
					component.offsetMax = Vector2.zero;
					Image image = gameObject.AddComponent<Image>();
					image.color = new Color(0.01f, 0.04f, 0.09f, 1f);
					image.raycastTarget = false;
					_expandingPulseFeedback = gameObject.AddComponent<CanvasGroup>();
					_expandingPulseFeedback.alpha = 0f;
					_expandingPulseFeedback.blocksRaycasts = false;
					_expandingPulseFeedback.interactable = false;
				}
				_expandingPulseFeedback.DOKill();
				_expandingPulseFeedback.alpha = 0.45f;
				_expandingPulseFeedback.DOFade(0f, 0.9f).SetDelay(0.2f).SetEase(Ease.OutQuad);
			}
		}

		public ulong GetPlayerBySpeakerId(string speakerId)
		{
			if (_nfgoPlayer != null && _nfgoPlayer.PlayerId == speakerId)
			{
				return base.OwnerClientId;
			}
			return 0uL;
		}

		private void SetupVoiceState()
		{
			if (!(_nfgoPlayer == null) && !string.IsNullOrWhiteSpace(_nfgoPlayer.PlayerId) && !(Singleton<DissonanceManager>.Instance == null) && !(Singleton<DissonanceManager>.Instance.Comms == null))
			{
				_boundVoicePlayerId = _nfgoPlayer.PlayerId;
				_voiceState = Singleton<DissonanceManager>.Instance.Comms.FindPlayer(_boundVoicePlayerId);
				RefreshVoiceActivationVisualMode();
				UtilityMethods.Log("<b><color=#7ED6FF>[PPG_Player_Dissonance]</color></b> SetupVoiceState\n   <b>PlayerId:</b> " + _boundVoicePlayerId + $"\n   <b>IsOwner:</b> {base.IsOwner}" + $"\n   <b>State Found:</b> {_voiceState != null}");
			}
		}

		private void DissonancePlayerIdChanged(FixedString128Bytes oldValue, FixedString128Bytes newValue)
		{
			_voiceState = null;
			_boundVoicePlayerId = null;
			SetupVoiceState();
		}

		private void OnUpdateDissonanceHeadMesh(Outfit outfit)
		{
			if (outfit == null || outfit.Type == null || outfit.Type.name != "Head")
			{
				return;
			}
			if (outfit.skinnedRenderer == null)
			{
				LogMissingFaceRendererOnce("Head outfit renderer is null");
				return;
			}
			Animator componentInChildren = outfit.GetComponentInChildren<Animator>();
			if (componentInChildren != null)
			{
				componentInChildren.enabled = false;
			}
			if (!outfit.mergeMesh)
			{
				BindDissonanceFaceRenderer(outfit.skinnedRenderer, "non-merged Head outfit");
			}
			else
			{
				RefreshDissonanceHeadRenderer();
			}
		}

		private void OnUpdateDissonanceRig(SkinnedMeshRenderer characterBody)
		{
			BindDissonanceFaceRenderer(characterBody, "merged CharacterBody");
		}

		private void RegisterDissonanceFaceRendererEvents(OutfitSystem outfitSystem)
		{
			if (!(outfitSystem == null))
			{
				outfitSystem.OnOutfitChanged = (UnityAction<Outfit>)Delegate.Remove(outfitSystem.OnOutfitChanged, new UnityAction<Outfit>(OnUpdateDissonanceHeadMesh));
				outfitSystem.OnOutfitChanged = (UnityAction<Outfit>)Delegate.Combine(outfitSystem.OnOutfitChanged, new UnityAction<Outfit>(OnUpdateDissonanceHeadMesh));
				outfitSystem.OnRigChanged = (UnityAction<SkinnedMeshRenderer>)Delegate.Remove(outfitSystem.OnRigChanged, new UnityAction<SkinnedMeshRenderer>(OnUpdateDissonanceRig));
				outfitSystem.OnRigChanged = (UnityAction<SkinnedMeshRenderer>)Delegate.Combine(outfitSystem.OnRigChanged, new UnityAction<SkinnedMeshRenderer>(OnUpdateDissonanceRig));
			}
		}

		private void UnregisterDissonanceFaceRendererEvents()
		{
			OutfitSystem outfitSystem = ((CharacterVisuals != null) ? CharacterVisuals.TpsOutfitSystem : null);
			if (!(outfitSystem == null))
			{
				outfitSystem.OnOutfitChanged = (UnityAction<Outfit>)Delegate.Remove(outfitSystem.OnOutfitChanged, new UnityAction<Outfit>(OnUpdateDissonanceHeadMesh));
				outfitSystem.OnRigChanged = (UnityAction<SkinnedMeshRenderer>)Delegate.Remove(outfitSystem.OnRigChanged, new UnityAction<SkinnedMeshRenderer>(OnUpdateDissonanceRig));
			}
		}

		public void RefreshDissonanceHeadRenderer()
		{
			OutfitSystem outfitSystem = ((CharacterVisuals != null) ? CharacterVisuals.TpsOutfitSystem : null);
			if (outfitSystem == null)
			{
				LogMissingFaceRendererOnce("TPS OutfitSystem is unavailable");
				return;
			}
			Outfit headOutfit = null;
			TryGetCurrentHeadOutfit(out headOutfit);
			if (headOutfit != null && !headOutfit.mergeMesh && headOutfit.skinnedRenderer != null)
			{
				BindDissonanceFaceRenderer(headOutfit.skinnedRenderer, "non-merged Head outfit refresh");
				return;
			}
			SkinnedMeshRenderer characterBody = outfitSystem.GetCharacterBody();
			if (characterBody == null)
			{
				LogMissingFaceRendererOnce("merged CharacterBody is unavailable");
			}
			else
			{
				BindDissonanceFaceRenderer(characterBody, "CharacterBody refresh");
			}
		}

		private void BindDissonanceFaceRenderer(SkinnedMeshRenderer renderer, string source)
		{
			if (renderer == null)
			{
				LogMissingFaceRendererOnce(source + " renderer is null");
				return;
			}
			Mesh sharedMesh = renderer.sharedMesh;
			if (sharedMesh == null)
			{
				_loggedMissingFaceRenderer = false;
				if (!_loggedMissingFaceMesh)
				{
					_loggedMissingFaceMesh = true;
					UtilityMethods.Log("[PPG_Player_Dissonance] Face renderer has no shared mesh. " + $"Source:{source} Renderer:{renderer.name} Active:{renderer.gameObject.activeInHierarchy}", LogType.Warning);
				}
				return;
			}
			bool num = _headRenderer != renderer || _resolvedJawMesh != sharedMesh;
			_headRenderer = renderer;
			_resolvedJawMesh = null;
			_resolvedJawOpenBlendShapeIndex = -1;
			_lastAppliedMouthValue = float.NaN;
			_loggedMissingFaceRenderer = false;
			_loggedMissingFaceMesh = false;
			_loggedMissingJawBlendShape = false;
			ResetMouth();
			ApplyCurrentManualExpression();
			if (num)
			{
				UtilityMethods.Log("<b><color=#7ED6FF>[PPG_Player_Dissonance]</color></b> Face renderer bound\n   <b>Source:</b> " + source + "\n   <b>Renderer:</b> " + _headRenderer.name + "\n   <b>Mesh:</b> " + sharedMesh.name + $"\n   <b>Active:</b> {_headRenderer.gameObject.activeInHierarchy}" + $"\n   <b>BlendShape Count:</b> {GetBlendShapeCount()}");
			}
		}

		private void LogMissingFaceRendererOnce(string reason)
		{
			if (!_loggedMissingFaceRenderer)
			{
				_loggedMissingFaceRenderer = true;
				UtilityMethods.Log("[PPG_Player_Dissonance] Face renderer binding failed. Reason:" + reason + " " + $"OwnerClientId:{base.OwnerClientId} IsOwner:{base.IsOwner}", LogType.Warning);
			}
		}

		private bool TryGetCurrentHeadOutfit(out Outfit headOutfit)
		{
			headOutfit = null;
			OutfitSystem outfitSystem = ((CharacterVisuals != null) ? CharacterVisuals.TpsOutfitSystem : null);
			if (outfitSystem == null)
			{
				return false;
			}
			headOutfit = outfitSystem.GetOutfit("Head");
			if (headOutfit != null && headOutfit.skinnedRenderer != null)
			{
				return true;
			}
			Outfit[] componentsInChildren = outfitSystem.GetComponentsInChildren<Outfit>(includeInactive: true);
			foreach (Outfit outfit in componentsInChildren)
			{
				if (!(outfit == null) && !(outfit.Type == null) && !(outfit.skinnedRenderer == null) && string.Equals(outfit.Type.name, "Head", StringComparison.Ordinal))
				{
					headOutfit = outfit;
					return true;
				}
			}
			headOutfit = null;
			return false;
		}

		private void UpdateDissonance()
		{
			if (_nfgoPlayer == null || string.IsNullOrWhiteSpace(_nfgoPlayer.PlayerId))
			{
				ReleaseMouth();
				SetVoiceActivationVisual(isActive: false);
				return;
			}
			if (_voiceState == null || _boundVoicePlayerId != _nfgoPlayer.PlayerId)
			{
				SetupVoiceState();
				if (_voiceState == null)
				{
					ReleaseMouth();
					SetVoiceActivationVisual(isActive: false);
					return;
				}
			}
			float num = Mathf.Max(0f, _voiceState.Amplitude);
			float t = 1f - Mathf.Exp((0f - _amplitudeSharpness) * Time.unscaledDeltaTime);
			_smoothedAmplitude = Mathf.Lerp(_smoothedAmplitude, num, t);
			if (_voiceState.IsSpeaking && (_smoothedAmplitude >= _voiceActivationThreshold || num >= _voiceActivationThreshold))
			{
				_voiceActiveUntil = Time.unscaledTime + _voiceActivationHoldSeconds;
			}
			bool flag = Time.unscaledTime < _voiceActiveUntil;
			SetVoiceActivationVisual(flag);
			UpdateUnderwaterVoiceParticle(flag, _smoothedAmplitude);
			if (_headRenderer == null)
			{
				RefreshDissonanceHeadRenderer();
				if (_headRenderer == null)
				{
					return;
				}
			}
			if (!(_headRenderer.sharedMesh == null))
			{
				float num2 = (flag ? CalculateMouthOpenValue(_smoothedAmplitude) : _closedMouthValue);
				float num3 = ((num2 > _mouthValue) ? _openSpeed : _closeSpeed);
				_mouthValue = Mathf.MoveTowards(_mouthValue, num2, num3 * Time.unscaledDeltaTime);
			}
		}

		private void LateUpdateDissonanceMouth()
		{
			ApplyMouthBlendShape(_mouthValue);
		}

		private float CalculateMouthOpenValue(float amplitude)
		{
			float value = Mathf.InverseLerp(_silenceAmplitude, _shoutAmplitude, amplitude);
			value = Mathf.Clamp01(value);
			value = Mathf.Pow(value, _mouthPower);
			return Mathf.Lerp(_closedMouthValue, _maxMouthOpenValue, value);
		}

		private void ReleaseMouth()
		{
			_smoothedAmplitude = 0f;
			_mouthValue = Mathf.MoveTowards(_mouthValue, _closedMouthValue, _closeSpeed * Time.unscaledDeltaTime);
			UpdateUnderwaterVoiceParticle(isVoiceActive: false, 0f);
			StopUnderwaterVoiceParticle();
		}

		private void ResetMouth()
		{
			_smoothedAmplitude = 0f;
			_mouthValue = _closedMouthValue;
			_lastAppliedMouthValue = float.NaN;
			_voiceActiveUntil = -1f;
			_lastVoiceActivationState = false;
			RefreshVoiceActivationVisualMode();
		}

		private void ApplyMouthBlendShape(float value)
		{
			if (_headRenderer == null)
			{
				LogMissingFaceRendererOnce("mouth blendshape target is null");
				return;
			}
			int num = ResolveJawBlendShapeIndex();
			if (num >= 0)
			{
				float num2 = Mathf.Clamp(value, 0f, 100f);
				if (float.IsNaN(_lastAppliedMouthValue) || !(Mathf.Abs(num2 - _lastAppliedMouthValue) < 0.25f))
				{
					_headRenderer.SetBlendShapeWeight(num, num2);
					_lastAppliedMouthValue = num2;
				}
			}
		}

		private int ResolveJawBlendShapeIndex()
		{
			if (_headRenderer == null)
			{
				return -1;
			}
			Mesh sharedMesh = _headRenderer.sharedMesh;
			if (sharedMesh == null)
			{
				if (!_loggedMissingFaceMesh)
				{
					_loggedMissingFaceMesh = true;
					UtilityMethods.Log("[PPG_Player_Dissonance] Face renderer mesh is missing while resolving jaw. " + $"Renderer:{_headRenderer.name} Active:{_headRenderer.gameObject.activeInHierarchy}", LogType.Warning);
				}
				return -1;
			}
			if (_resolvedJawMesh != sharedMesh)
			{
				_resolvedJawMesh = sharedMesh;
				_resolvedJawOpenBlendShapeIndex = FindBlendShapeIndex(_headRenderer, "Expression_Jaw_Open");
				_lastAppliedMouthValue = float.NaN;
				_loggedMissingJawBlendShape = false;
			}
			if (_resolvedJawOpenBlendShapeIndex < 0 && !_loggedMissingJawBlendShape)
			{
				_loggedMissingJawBlendShape = true;
				UtilityMethods.Log("[PPG_Player_Dissonance] Jaw blendshape not found. Renderer:" + _headRenderer.name + " Mesh:" + sharedMesh.name + " Shape:Expression_Jaw_Open " + $"Active:{_headRenderer.gameObject.activeInHierarchy} BlendShapeCount:{sharedMesh.blendShapeCount}", LogType.Warning);
			}
			return _resolvedJawOpenBlendShapeIndex;
		}

		private int GetBlendShapeCount()
		{
			if (_headRenderer == null || _headRenderer.sharedMesh == null)
			{
				return 0;
			}
			return _headRenderer.sharedMesh.blendShapeCount;
		}

		private static int FindBlendShapeIndex(SkinnedMeshRenderer renderer, string blendShapeName)
		{
			if (renderer == null || renderer.sharedMesh == null || string.IsNullOrWhiteSpace(blendShapeName))
			{
				return -1;
			}
			Mesh sharedMesh = renderer.sharedMesh;
			int blendShapeIndex = sharedMesh.GetBlendShapeIndex(blendShapeName);
			if (blendShapeIndex >= 0)
			{
				return blendShapeIndex;
			}
			for (int i = 0; i < sharedMesh.blendShapeCount; i++)
			{
				string blendShapeName2 = sharedMesh.GetBlendShapeName(i);
				if (string.Equals(blendShapeName2, blendShapeName, StringComparison.Ordinal))
				{
					return i;
				}
				int num = blendShapeName2.LastIndexOf('.');
				if (num >= 0 && num < blendShapeName2.Length - 1 && string.Equals(blendShapeName2.Substring(num + 1), blendShapeName, StringComparison.Ordinal))
				{
					return i;
				}
			}
			return -1;
		}

		private bool ShouldDriveVoiceActivationVisual()
		{
			if (Singleton<DissonanceManager>.Instance == null)
			{
				return false;
			}
			return Singleton<DissonanceManager>.Instance.GetActivationMode() == CommActivationMode.VoiceActivation;
		}

		private void RefreshVoiceActivationVisualMode()
		{
			if (!ShouldDriveVoiceActivationVisual())
			{
				_lastVoiceActivationState = false;
			}
		}

		private void SetVoiceActivationVisual(bool isActive)
		{
			if (ShouldDriveVoiceActivationVisual() && _lastVoiceActivationState != isActive)
			{
				_lastVoiceActivationState = isActive;
				ChangePTTChildStatus(isActive);
			}
		}

		private void UpdateUnderwaterVoiceParticle(bool isVoiceActive, float amplitude)
		{
			if (_underwaterVoiceParticle == null)
			{
				return;
			}
			if (!(IsUnderwaterForVoiceParticle() & isVoiceActive) || !(amplitude >= _underwaterVoiceParticleMinAmplitude))
			{
				SetUnderwaterVoiceParticleEmission(0f);
				if (_underwaterVoiceParticle.isPlaying)
				{
					_underwaterVoiceParticle.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
				}
				return;
			}
			float value = Mathf.InverseLerp(_underwaterVoiceParticleMinAmplitude, _underwaterVoiceParticleMaxAmplitude, amplitude);
			value = Mathf.Clamp01(value);
			value = Mathf.Pow(value, _underwaterVoiceParticlePower);
			float underwaterVoiceParticleEmission = Mathf.Lerp(_underwaterVoiceParticleMinRate, _underwaterVoiceParticleMaxRate, value);
			SetUnderwaterVoiceParticleEmission(underwaterVoiceParticleEmission);
			if (!_underwaterVoiceParticle.isPlaying)
			{
				_underwaterVoiceParticle.Play(withChildren: true);
			}
		}

		private void SetUnderwaterVoiceParticleEmission(float rate)
		{
			if (!(_underwaterVoiceParticle == null))
			{
				ParticleSystem.EmissionModule emission = _underwaterVoiceParticle.emission;
				emission.enabled = rate > 0f;
				emission.rateOverTime = rate;
			}
		}

		private bool IsUnderwaterForVoiceParticle()
		{
			return IsUnderWater;
		}

		private void StopUnderwaterVoiceParticle()
		{
			if (!(_underwaterVoiceParticle == null))
			{
				SetUnderwaterVoiceParticleEmission(0f);
				if (_underwaterVoiceParticle.isPlaying)
				{
					_underwaterVoiceParticle.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
				}
			}
		}

		private void SetupFX()
		{
			if (!(_oceanRippleTrail == null) && !(_oceanRippleTrail.particleSystem == null))
			{
				ParticleSystem.EmissionModule emission = _oceanRippleTrail.particleSystem.emission;
				emission.enabled = false;
				_oceanRippleTrail.particleSystem.Stop();
				_isOceanRippleTrailActive = false;
			}
		}

		public void PlayOceanEnterFPSVFX()
		{
			if (_oceanEnterFPSVFX != null)
			{
				_oceanEnterFPSVFX.Play();
			}
			if (_fpsAnimator != null)
			{
				_fpsAnimator.SetBool(IS_UNDERWATER, value: true);
			}
		}

		public void PlayEnterRippleImpact(bool isBig)
		{
			_oceanRippleImpact.heightScale = (isBig ? 0.5f : 0.1f);
			_oceanRippleImpactVFX.Play();
		}

		public void PlayOceanExitVFX()
		{
			if (_fpsAnimator != null)
			{
				_fpsAnimator.SetBool(IS_UNDERWATER, value: false);
			}
		}

		[Rpc(SendTo.Everyone)]
		public void ClientPlayElectricitySfxRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3515567780u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3515567780u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_electrcitySFX.Play();
			}
		}

		public void SetRippleTrail(bool s)
		{
			if (_oceanRippleTrail == null || _oceanRippleTrail.particleSystem == null || _isOceanRippleTrailActive == s)
			{
				return;
			}
			_isOceanRippleTrailActive = s;
			ParticleSystem particleSystem = _oceanRippleTrail.particleSystem;
			ParticleSystem.EmissionModule emission = particleSystem.emission;
			emission.enabled = s;
			if (s)
			{
				_oceanRippleTrail.ResetEmitter();
				if (!particleSystem.isPlaying)
				{
					particleSystem.Play();
				}
			}
			else if (particleSystem.isPlaying)
			{
				particleSystem.Stop();
			}
			if (base.IsOwner)
			{
				ClientRippleTrailRpc(s);
			}
		}

		[Rpc(SendTo.NotOwner)]
		private void ClientRippleTrailRpc(bool s)
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
				FastBufferWriter bufferWriter = __beginSendRpc(536206294u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in s, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 536206294u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				SetRippleTrail(s);
			}
		}

		public void PlaySwimNormal()
		{
			if (IsUnderWater)
			{
				_audioSourceSwimNormal.Play();
			}
		}

		[Rpc(SendTo.Owner)]
		public void ClientOwnerPlaySquidVFXRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3988887507u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3988887507u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				StopSquidVfxTimeout();
				_squidSFX?.Play();
				_fpsSquidVFX?.Play();
				PlayOneShotScreenVfx(_squidInkScreenVfxPrefab);
				ClientPlaySquidVFXRpc();
				if (_squidVfxMaximumDuration > 0f)
				{
					_squidVfxTimeoutRoutine = StartCoroutine(StopSquidVfxAfterTimeout());
				}
			}
		}

		[Rpc(SendTo.Owner)]
		public void ClientOwnerStopSquidVFXRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1760465321u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1760465321u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				StopSquidVfxTimeout();
				StopSquidVfxLocal();
				ClientStopSquidVFXRpc();
			}
		}

		[Rpc(SendTo.Everyone)]
		public void ClientPlaySquidVFXRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(2125506552u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2125506552u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_tpsSquidVFX?.Play();
			}
		}

		[Rpc(SendTo.Everyone)]
		public void ClientStopSquidVFXRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1271757271u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1271757271u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_tpsSquidVFX?.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
			}
		}

		private IEnumerator StopSquidVfxAfterTimeout()
		{
			yield return new WaitForSeconds(_squidVfxMaximumDuration);
			_squidVfxTimeoutRoutine = null;
			StopSquidVfxLocal();
			Harpoon?.SetBarBlockStatusLocal(status: false);
			ClientStopSquidVFXRpc();
		}

		private void StopSquidVfxTimeout()
		{
			if (_squidVfxTimeoutRoutine != null)
			{
				StopCoroutine(_squidVfxTimeoutRoutine);
				_squidVfxTimeoutRoutine = null;
			}
		}

		private void StopSquidVfxLocal()
		{
			_squidSFX?.Stop();
			_fpsSquidVFX?.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
		}

		[Rpc(SendTo.Owner)]
		public void ClientOwnerPlaySharkChaseSFXRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(2445840379u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2445840379u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_sharkChaseSFX.Play();
			}
		}

		public void SetOctopusTrapScreenVfx(bool active)
		{
			SetPersistentScreenVfx(_octopusTrapScreenVfxPrefab, ref _octopusTrapScreenVfxInstance, active, useGameplayCameraWhenViewModelHidden: true);
		}

		public void SetJellyfishTrapScreenVfx(bool active)
		{
			SetPersistentScreenVfx(_jellyfishTrapScreenVfxPrefab, ref _jellyfishTrapScreenVfxInstance, active, useGameplayCameraWhenViewModelHidden: false);
		}

		[Rpc(SendTo.Owner)]
		public void ClientSetSharkTargetScreenVfxRpc(ulong sharkNetworkObjectId, bool active)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3271259337u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sharkNetworkObjectId);
				bufferWriter.WriteValueSafe(in active, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3271259337u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (active)
				{
					_targetingSharkNetworkIds.Add(sharkNetworkObjectId);
				}
				else
				{
					_targetingSharkNetworkIds.Remove(sharkNetworkObjectId);
				}
				SetPersistentScreenVfx(_sharkTargetScreenVfxPrefab, ref _sharkTargetScreenVfxInstance, _targetingSharkNetworkIds.Count > 0, useGameplayCameraWhenViewModelHidden: false);
			}
		}

		private void SetPersistentScreenVfx(GameObject prefab, ref GameObject instance, bool active, bool useGameplayCameraWhenViewModelHidden)
		{
			if (base.IsOwner)
			{
				if (!active)
				{
					StopScreenVfx(ref instance);
				}
				else if (!(instance != null))
				{
					instance = CreateScreenVfxInstance(prefab, useGameplayCameraWhenViewModelHidden);
				}
			}
		}

		private void PlayOneShotScreenVfx(GameObject prefab)
		{
			if (!base.IsOwner)
			{
				return;
			}
			GameObject gameObject = CreateScreenVfxInstance(prefab, useGameplayCameraWhenViewModelHidden: false);
			if (!(gameObject == null))
			{
				ParticleSystem[] componentsInChildren = gameObject.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
				float num = 0.5f;
				ParticleSystem[] array = componentsInChildren;
				foreach (ParticleSystem obj in array)
				{
					ParticleSystem.MainModule main = obj.main;
					main.loop = false;
					num = Mathf.Max(num, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
					obj.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmittingAndClear);
					obj.Play(withChildren: false);
				}
				UnityEngine.Object.Destroy(gameObject, num);
			}
		}

		private GameObject CreateScreenVfxInstance(GameObject prefab, bool useGameplayCameraWhenViewModelHidden)
		{
			if (prefab == null)
			{
				UtilityMethods.Log("[PlayerScreenVFX] Screen VFX prefab is not assigned.", LogType.Warning);
				return null;
			}
			Transform transform = ResolveScreenVfxParent(useGameplayCameraWhenViewModelHidden);
			if (transform == null)
			{
				UtilityMethods.Log("[PlayerScreenVFX] Could not play " + prefab.name + " because no local camera is available.", LogType.Warning);
				return null;
			}
			GameObject gameObject = UnityEngine.Object.Instantiate(prefab, transform, worldPositionStays: false);
			gameObject.name = prefab.name + " (Runtime)";
			MainCameraAutoParent[] componentsInChildren = gameObject.GetComponentsInChildren<MainCameraAutoParent>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].enabled = false;
			}
			ParentConstraint[] componentsInChildren2 = gameObject.GetComponentsInChildren<ParentConstraint>(includeInactive: true);
			for (int i = 0; i < componentsInChildren2.Length; i++)
			{
				componentsInChildren2[i].enabled = false;
			}
			Transform obj = gameObject.transform;
			obj.localPosition = Vector3.zero;
			obj.localRotation = Quaternion.identity;
			obj.localScale = Vector3.one;
			if (_playerCamera != null && transform == _playerCamera.transform)
			{
				SetLayerRecursively(gameObject, LayerMask.NameToLayer("UI"));
			}
			return gameObject;
		}

		private Transform ResolveScreenVfxParent(bool useGameplayCameraWhenViewModelHidden)
		{
			if (useGameplayCameraWhenViewModelHidden && (_viewModelCamera == null || !_viewModelCamera.enabled) && _playerCamera != null)
			{
				return _playerCamera.transform;
			}
			if (!(MainCamera != null))
			{
				return null;
			}
			return MainCamera.transform;
		}

		private static void SetLayerRecursively(GameObject root, int layer)
		{
			if (!(root == null) && layer >= 0)
			{
				Transform[] componentsInChildren = root.GetComponentsInChildren<Transform>(includeInactive: true);
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					componentsInChildren[i].gameObject.layer = layer;
				}
			}
		}

		private static void StopScreenVfx(ref GameObject instance)
		{
			if (!(instance == null))
			{
				instance.SetActive(value: false);
				UnityEngine.Object.Destroy(instance);
				instance = null;
			}
		}

		private void StopAllStatusScreenVfx()
		{
			StopScreenVfx(ref _octopusTrapScreenVfxInstance);
			StopScreenVfx(ref _jellyfishTrapScreenVfxInstance);
			StopScreenVfx(ref _sharkTargetScreenVfxInstance);
			_targetingSharkNetworkIds.Clear();
		}

		public void PlaySwimSprint()
		{
			if (IsUnderWater)
			{
				_audioSourceSwimSprint.Play();
			}
		}

		public bool CanStartControlAction(PlayerControlAction action)
		{
			if (HasSurfboard)
			{
				return false;
			}
			if (!base.IsOwner)
			{
				return false;
			}
			bool flag = action == PlayerControlAction.OpenEmoteWheel && IsUsingEmoteArea && AllowOpenEmotesInputInActiveArea;
			if (_uiInputOwners.Count > 0 || (_tabletController != null && _tabletController.IsTabletActive) || (_interactor != null && (_interactor.IsUsing || _interactor.IsUseRequestPending)) || IsTrapped || (_divingEquipment != null && _divingEquipment.IsKnocked) || _fishSlipRecoveryPending || _electrocutionRecoveryPending || _activeAttachmentMode != PlayerAttachmentMode.None)
			{
				return false;
			}
			if (flag)
			{
				return true;
			}
			if (_emoteActive || _expressionCameraActive || _staticEmoteActive || _swingEmoteActive)
			{
				return false;
			}
			if (_firstPersonCharacter == null || !_firstPersonCharacter.enabled || _firstPersonCharacter.characterMovement == null || !_firstPersonCharacter.characterMovement.enabled || _firstPersonCharacter.IsPaused())
			{
				return false;
			}
			return action switch
			{
				PlayerControlAction.OpenToolWheel => CanChangeTool, 
				PlayerControlAction.ActivateTool => CanChangeTool, 
				PlayerControlAction.CloseActiveTool => CanCloseActiveToolFromShortcut, 
				PlayerControlAction.FishSlip => CanDisableActiveTool, 
				_ => true, 
			};
		}

		public bool TryRestoreCharacterControlAfterModeExit(bool clearState = true)
		{
			if (HasCharacterControlResumeBlocker())
			{
				return false;
			}
			PermaResume();
			ResumeMovement(clearState);
			ResumeInputs();
			ResumeLook();
			ResumeConstraintsIfAllowed();
			return true;
		}

		private bool HasCharacterControlResumeBlocker()
		{
			if ((!(_divingEquipment != null) || !_divingEquipment.IsKnocked) && !IsTrapped && _activeAttachmentMode == PlayerAttachmentMode.None && (!(_interactor != null) || !_interactor.IsUsing) && !_fishSlipRecoveryPending && !_electrocutionRecoveryPending && !_emoteActive && !_expressionCameraActive && !_staticEmoteActive)
			{
				return _swingEmoteActive;
			}
			return true;
		}

		private void SetupInput()
		{
			_playerInput.enabled = base.IsOwner;
			if (base.IsOwner)
			{
				_playerInput.actions = Singleton<InputManager>.Instance.InputHandler.InputMap.asset;
				_playerInput.defaultActionMap = "Player";
				_playerInput.neverAutoSwitchControlSchemes = false;
				_settingsProvider.Settings.SetInputActionAsset(_playerInput.actions);
				_eventBinding = new EventBinding<GlobalChatEvent>(OnGlobalChatStatus);
				EventBus<GlobalChatEvent>.Register(_eventBinding);
			}
		}

		public void SwitchToUI()
		{
			SwitchTo("UI", cursorVisible: true);
		}

		public void SwitchToPlayer()
		{
			SwitchTo("Player", cursorVisible: false);
		}

		public void SwitchTo(string map, bool cursorVisible)
		{
			if (_uiInputOwners.Count > 0)
			{
				_actionMapBeforeOwnedUI = map;
				_cursorVisibleBeforeOwnedUI = cursorVisible;
				ApplyInputState("UI", cursorVisible: true);
			}
			else
			{
				ApplyInputState(map, cursorVisible);
			}
		}

		public void AcquireUIInput(PlayerUIInputOwner owner)
		{
			if (_uiInputOwners.Add(owner))
			{
				if (_uiInputOwners.Count == 1)
				{
					_actionMapBeforeOwnedUI = CurrentMap;
					_cursorVisibleBeforeOwnedUI = CursorHelper.CursorVisible;
				}
				ApplyInputState("UI", cursorVisible: true);
			}
		}

		public void ReleaseUIInput(PlayerUIInputOwner owner)
		{
			if (_uiInputOwners.Remove(owner))
			{
				if (_uiInputOwners.Count > 0)
				{
					ApplyInputState("UI", cursorVisible: true);
					return;
				}
				string map = (string.IsNullOrEmpty(_actionMapBeforeOwnedUI) ? "Player" : _actionMapBeforeOwnedUI);
				ApplyInputState(map, _cursorVisibleBeforeOwnedUI);
				_actionMapBeforeOwnedUI = null;
				_cursorVisibleBeforeOwnedUI = false;
			}
		}

		private void ApplyInputState(string map, bool cursorVisible)
		{
			ChangeActionMap(map);
			CursorHelper.CursorVisible = cursorVisible;
		}

		private void ChangeActionMap(string actionMap)
		{
			_playerInput.SwitchCurrentActionMap(actionMap);
			UtilityMethods.Log("Current Input: " + _playerInput.currentActionMap.name);
		}

		private void OnGlobalChatStatus(GlobalChatEvent @event)
		{
			if (@event.Status)
			{
				AcquireUIInput(PlayerUIInputOwner.GlobalChat);
			}
			else
			{
				ReleaseUIInput(PlayerUIInputOwner.GlobalChat);
			}
		}

		private void OnIsTrappedStateChanged(bool previousValue, bool currentValue)
		{
			if (!(!base.IsOwner | currentValue))
			{
				TryRestoreCharacterControlAfterModeExit();
			}
		}

		private void SetupMovement()
		{
			ResolveMovementReferences();
			if (_firstPersonCharacterInput != null)
			{
				_firstPersonCharacterInput.enabled = base.IsOwner;
			}
			else
			{
				UtilityMethods.Log("[PPG_Player_Movement] SetupMovement missing FirstPersonCharacterInput.", LogType.Error);
			}
			if (_firstPersonCharacterLookInput != null)
			{
				_firstPersonCharacterLookInput.enabled = base.IsOwner;
			}
			else
			{
				UtilityMethods.Log("[PPG_Player_Movement] SetupMovement missing FirstPersonCharacterLookInput.", LogType.Error);
			}
			if (_firstPersonCharacter != null)
			{
				if (base.IsOwner && _firstPersonCharacter.characterMovement != null)
				{
					_firstPersonCharacter.characterMovement.interpolation = RigidbodyInterpolation.None;
				}
				_firstPersonCharacter.enabled = base.IsOwner;
			}
			else
			{
				UtilityMethods.Log("[PPG_Player_Movement] SetupMovement missing FirstPersonCharacter.", LogType.Error);
			}
			SetupPlayerCollisionSetting();
			if (_animationController != null)
			{
				_animationController.enabled = base.IsOwner;
			}
		}

		private void SetupPlayerCollisionSetting()
		{
			if (base.IsOwner && !(_firstPersonCharacter?.characterMovement == null))
			{
				if (!_hasBaseCharacterCollisionMask)
				{
					_baseCharacterCollisionMask = _firstPersonCharacter.characterMovement.collisionLayers.value;
					_hasBaseCharacterCollisionMask = true;
				}
				RuntimeSettings.PlayerCollisionChanged -= OnPlayerCollisionSettingChanged;
				RuntimeSettings.PlayerCollisionChanged += OnPlayerCollisionSettingChanged;
				ApplyPlayerCollisionSetting(RuntimeSettings.PlayerCollisionEnabled);
			}
		}

		private void TeardownPlayerCollisionSetting()
		{
			RuntimeSettings.PlayerCollisionChanged -= OnPlayerCollisionSettingChanged;
		}

		private void OnPlayerCollisionSettingChanged(bool enabled)
		{
			ApplyPlayerCollisionSetting(enabled);
		}

		private void ApplyPlayerCollisionSetting(bool enabled)
		{
			if (_hasBaseCharacterCollisionMask && !(_firstPersonCharacter?.characterMovement == null))
			{
				int num = _baseCharacterCollisionMask;
				int num2 = LayerMask.NameToLayer("ClientPlayer");
				if (!enabled && num2 >= 0)
				{
					num &= ~(1 << num2);
				}
				_firstPersonCharacter.characterMovement.collisionLayers = num;
			}
		}

		private void TickServerGameplayVelocityEstimate()
		{
			if (!base.IsServer || !base.IsSpawned)
			{
				_serverVelocitySampleInitialized = false;
				_serverGameplayVelocity = Vector3.zero;
				return;
			}
			Vector3 position = base.transform.position;
			float unscaledTime = Time.unscaledTime;
			if (!_serverVelocitySampleInitialized)
			{
				_serverVelocitySampleInitialized = true;
				_serverVelocitySamplePosition = position;
				_serverVelocitySampleTime = unscaledTime;
				_serverGameplayVelocity = Vector3.zero;
				return;
			}
			Vector3 vector = position - _serverVelocitySamplePosition;
			if (vector.sqrMagnitude > 1E-06f)
			{
				float num = Mathf.Max(0.0001f, unscaledTime - _serverVelocitySampleTime);
				if (vector.sqrMagnitude > 4f)
				{
					_serverGameplayVelocity = Vector3.zero;
				}
				else
				{
					Vector3 b = Vector3.ClampMagnitude(vector / num, 12f);
					_serverGameplayVelocity = Vector3.Lerp(_serverGameplayVelocity, b, 0.65f);
				}
				_serverVelocitySamplePosition = position;
				_serverVelocitySampleTime = unscaledTime;
			}
			else if (unscaledTime - _serverVelocitySampleTime > 0.2f)
			{
				_serverGameplayVelocity = Vector3.MoveTowards(_serverGameplayVelocity, Vector3.zero, 20f * Time.unscaledDeltaTime);
			}
		}

		private void ResolveMovementReferences()
		{
			if (_firstPersonCharacter == null)
			{
				_firstPersonCharacter = GetComponent<FirstPersonCharacter>();
			}
			if (_firstPersonCharacter == null)
			{
				_firstPersonCharacter = GetComponentInChildren<FirstPersonCharacter>(includeInactive: true);
			}
			if (_firstPersonCharacterInput == null)
			{
				_firstPersonCharacterInput = GetComponentInChildren<FirstPersonCharacterInput>(includeInactive: true);
			}
			if (_firstPersonCharacterLookInput == null)
			{
				_firstPersonCharacterLookInput = GetComponentInChildren<FirstPersonCharacterLookInput>(includeInactive: true);
			}
			if (_animationController == null)
			{
				_animationController = GetComponentInChildren<AnimationController>(includeInactive: true);
			}
		}

		public void PermaPause()
		{
			PauseMovement();
			_firstPersonCharacter.enabled = false;
			_firstPersonCharacterInput.enabled = false;
			_firstPersonCharacter.characterMovement.enabled = false;
		}

		public void PermaResume()
		{
			ResumeMovement();
			_firstPersonCharacter.enabled = true;
			_firstPersonCharacterInput.enabled = true;
			_firstPersonCharacter.characterMovement.enabled = true;
		}

		public void ResumeCharacter(bool clearState = false)
		{
			if ((!(_divingEquipment != null) || !_divingEquipment.IsKnocked) && !IsTrapped && _activeAttachmentMode == PlayerAttachmentMode.None && !_fishSlipRecoveryPending && !_electrocutionRecoveryPending && !_emoteActive && !_expressionCameraActive && !_staticEmoteActive && !_swingEmoteActive)
			{
				ResumeMovement(clearState);
				ResumeLook();
			}
		}

		public void PauseCharacter()
		{
			PauseMovement();
			PauseLook();
		}

		public void ResumeMovement(bool clearState = false)
		{
			UtilityMethods.Log("[PPG_Player_Movement] ResumeMovement");
			_firstPersonCharacter.Pause(pause: false, clearState);
		}

		public void PauseMovement(bool clearState = true)
		{
			UtilityMethods.Log("[PPG_Player_Movement] PauseMovement");
			_firstPersonCharacter.Pause(pause: true, clearState);
		}

		private void TickHighSpeedWindLoop()
		{
			if (!base.IsOwner || _firstPersonCharacter == null || _firstPersonCharacter.characterMovement == null)
			{
				StopHighSpeedWindLoop();
				return;
			}
			AudioClipSO audioClipSO = ResolveHighSpeedLoopSfx();
			if (audioClipSO == null || Singleton<SoundManager>.Instance == null)
			{
				StopHighSpeedWindLoop();
				return;
			}
			float num = Mathf.Max(0f, _windLoopStartSpeed);
			float num2 = Mathf.Min(num, Mathf.Max(0f, _windLoopStopSpeed));
			float magnitude = _firstPersonCharacter.characterMovement.velocity.magnitude;
			if (IsWindLoopSourceValid() && _activeWindLoopSfx != audioClipSO)
			{
				StopHighSpeedWindLoop();
			}
			if (!IsWindLoopSourceValid())
			{
				_windLoopSource = null;
				StopHighSpeedWindScreenVfx();
				if (magnitude < num)
				{
					return;
				}
				StartHighSpeedWindLoop(audioClipSO);
			}
			UpdateHighSpeedWindLoopTuning(magnitude);
			EnsureHighSpeedWindScreenVfx();
			if (magnitude <= num2 && IsWindLoopSourceValid() && _windLoopSource.volume <= 0.01f)
			{
				StopHighSpeedWindLoop();
			}
		}

		private AudioClipSO ResolveHighSpeedLoopSfx()
		{
			if (!IsUnderWater || !(_underwaterFastLoopSfx != null))
			{
				return _windLoopSfx;
			}
			return _underwaterFastLoopSfx;
		}

		private void StartHighSpeedWindLoop(AudioClipSO sfx)
		{
			_windLoopHandle = Singleton<SoundManager>.Instance.PlayOwnedLoop(sfx, base.transform.position, base.transform, $"PlayerWind-{base.OwnerClientId}");
			_windLoopSource = _windLoopHandle?.Source;
			if (_windLoopSource == null)
			{
				_windLoopHandle = null;
				return;
			}
			_activeWindLoopSfx = sfx;
			_windLoopSource.loop = true;
			_windLoopBaseVolume = Mathf.Max(0f, _windLoopSource.volume);
			_windLoopSource.volume = 0f;
			EnsureHighSpeedWindScreenVfx();
		}

		private void UpdateHighSpeedWindLoopTuning(float speed)
		{
			if (IsWindLoopSourceValid())
			{
				float b = Mathf.Max(_windLoopStartSpeed + 0.01f, _windLoopMaxSpeed);
				float t = Mathf.InverseLerp(_windLoopStartSpeed, b, speed);
				float num = Mathf.Clamp01(_windLoopMinVolumeMultiplier);
				float num2 = Mathf.Clamp01(_windLoopMaxVolumeMultiplier);
				if (num2 < num)
				{
					float num3 = num;
					float num4 = num2;
					num2 = num3;
					num = num4;
				}
				float num5 = Mathf.Max(0.1f, _windLoopMinPitch);
				float num6 = Mathf.Max(0.1f, _windLoopMaxPitch);
				if (num6 < num5)
				{
					float num7 = num5;
					float num4 = num6;
					num6 = num7;
					num5 = num4;
				}
				float target = ((speed <= Mathf.Max(0f, _windLoopStopSpeed)) ? 0f : (_windLoopBaseVolume * Mathf.Lerp(num, num2, t)));
				_windLoopSource.volume = Mathf.MoveTowards(_windLoopSource.volume, target, Mathf.Max(0.01f, _windLoopVolumeFadeSpeed) * Time.deltaTime);
				_windLoopSource.pitch = Mathf.Lerp(num5, num6, t);
				if (_activeWindLoopSfx != null && _activeWindLoopSfx.Is3D)
				{
					_windLoopSource.transform.position = base.transform.position;
				}
			}
		}

		private bool IsWindLoopSourceValid()
		{
			if (_windLoopHandle != null && _windLoopHandle.IsActive && _windLoopSource != null)
			{
				return _windLoopSource.gameObject.activeSelf;
			}
			return false;
		}

		private void StopHighSpeedWindLoop()
		{
			if (Singleton<SoundManager>.Instance != null)
			{
				Singleton<SoundManager>.Instance.ReleaseOwnedLoop(_windLoopHandle);
			}
			StopHighSpeedWindScreenVfx();
			_windLoopHandle = null;
			_windLoopSource = null;
			_activeWindLoopSfx = null;
			_windLoopBaseVolume = 1f;
		}

		private void EnsureHighSpeedWindScreenVfx()
		{
			if (IsWindLoopSourceValid() && !(_highSpeedWindScreenVfxInstance != null) && !(_highSpeedWindScreenVfxPrefab == null) && !(MainCamera == null))
			{
				_highSpeedWindScreenVfxInstance = CreateScreenVfxInstance(_highSpeedWindScreenVfxPrefab, useGameplayCameraWhenViewModelHidden: false);
			}
		}

		private void StopHighSpeedWindScreenVfx()
		{
			StopScreenVfx(ref _highSpeedWindScreenVfxInstance);
		}

		public void ResumeInputs()
		{
			_firstPersonCharacterInput.IsInputPaused = false;
		}

		public void PauseInputs()
		{
			_firstPersonCharacterInput.IsInputPaused = true;
		}

		public void ResumeLook()
		{
			UtilityMethods.Log("[PPG_Player_Movement] ResumeLook");
			_firstPersonCharacterLookInput.PauseLook = false;
		}

		public void PauseLook()
		{
			UtilityMethods.Log("[PPG_Player_Movement] PauseLook");
			_firstPersonCharacterLookInput.PauseLook = true;
		}

		public void TeleportPlayer(Vector3 pos, bool updateGround)
		{
			CancelSurfForTeleport(SurfExitReason.Teleport);
			UtilityMethods.Log($"[PPG_Player_Movement] TeleportPlayer to {pos}");
			_firstPersonCharacter.TeleportPosition(pos, interpolating: true, updateGround);
			base.transform.rotation = Quaternion.identity;
			HandleSeaLayerOccupancyAfterTeleport();
		}

		public void TeleportPlayer(Vector3 pos, Quaternion rotation, bool updateGround)
		{
			CancelSurfForTeleport(SurfExitReason.Teleport);
			UtilityMethods.Log($"[PPG_Player_Movement] TeleportPlayer to {pos}");
			Vector3 vector = rotation * Quaternion.Inverse(base.transform.rotation) * _firstPersonCharacter.characterMovement.velocity;
			_firstPersonCharacter.TeleportPosition(pos, interpolating: true, updateGround);
			_firstPersonCharacter.TeleportRotation(rotation);
			_firstPersonCharacter.characterMovement.velocity = vector;
			base.transform.SetPositionAndRotation(pos, rotation);
			HandleSeaLayerOccupancyAfterTeleport();
		}

		private void HandleSeaLayerOccupancyAfterTeleport()
		{
			if (IsTeleportDestinationInsideWaterVolume())
			{
				return;
			}
			bool flag = IsSwimming.Value || PrimarySeaLayer != null;
			if (base.IsOwner && SeaCreatureManager.TryGetActiveInstance(out var manager))
			{
				flag |= manager.ClearLocalPlayerLayerOccupancy(this);
			}
			if (!flag)
			{
				return;
			}
			if (base.IsServer)
			{
				if (SeaCreatureManager.TryGetActiveInstance(out var manager2))
				{
					manager2.RemoveServerClientOccupancy(base.OwnerClientId);
				}
			}
			else if (base.IsOwner && base.IsSpawned)
			{
				ServerClearSeaLayerOccupancyAfterTeleportRpc();
			}
		}

		private bool IsTeleportDestinationInsideWaterVolume()
		{
			Vector3 vector = ((_firstPersonCharacter != null && _firstPersonCharacter.characterMovement != null) ? _firstPersonCharacter.characterMovement.worldCenter : base.transform.position);
			Collider[] array = Physics.OverlapSphere(vector, 0.05f, -1, QueryTriggerInteraction.Collide);
			foreach (Collider collider in array)
			{
				if (!(collider == null) && collider.TryGetComponent<PhysicsVolume>(out var component) && component.waterVolume && !(component.boxCollider == null) && component.boxCollider.ClosestPoint(vector) == vector)
				{
					return true;
				}
			}
			return false;
		}

		[Rpc(SendTo.Server)]
		private void ServerClearSeaLayerOccupancyAfterTeleportRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2156617051u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2156617051u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsServer)
			{
				SeaCreatureManager manager;
				if (rpcParams.Receive.SenderClientId != base.OwnerClientId)
				{
					UtilityMethods.Log("[SeaLayerOccupancy] Teleport clear rejected. " + $"Sender={rpcParams.Receive.SenderClientId} Owner={base.OwnerClientId}", LogType.Warning);
				}
				else if (SeaCreatureManager.TryGetActiveInstance(out manager))
				{
					manager.RemoveServerClientOccupancy(base.OwnerClientId);
				}
			}
		}

		public void ApplySwingExitPose(Vector3 position, Quaternion rotation)
		{
			base.transform.SetPositionAndRotation(position, rotation);
			if (base.IsOwner && !(_firstPersonCharacter == null))
			{
				_firstPersonCharacter.TeleportPosition(position);
				_firstPersonCharacter.TeleportRotation(rotation);
			}
		}

		public void EnterHarpoonReel()
		{
			_firstPersonCharacterInput.SprintAbility.SprintDisabled = true;
			_cachedSwimSpeed = _firstPersonCharacter.maxSwimSpeed;
			_firstPersonCharacter.maxSwimSpeed = 0.15f;
			_firstPersonCharacterLookInput.SensivityFactor = 0.3f;
			UtilityMethods.Log("[PPG_Player_Movement] EnterHarpoonReel : " + _cachedSwimSpeed);
		}

		public void ExitHarpoonReel()
		{
			if (_cachedSwimSpeed != 0f)
			{
				_firstPersonCharacterInput.SprintAbility.SprintDisabled = false;
				_firstPersonCharacter.maxSwimSpeed = _cachedSwimSpeed;
				_firstPersonCharacterLookInput.SensivityFactor = 1f;
				UtilityMethods.Log("[PPG_Player_Movement] ExitHarpoonReel : " + _cachedSwimSpeed);
			}
		}

		public void ReverseMovement()
		{
			_firstPersonCharacterInput.IsMovementReversed = true;
		}

		public void RestoreMovement()
		{
			_firstPersonCharacterInput.IsMovementReversed = false;
		}

		[Rpc(SendTo.Owner)]
		public void ClientPauseCharacterRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3316237444u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3316237444u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				PauseMovement();
			}
		}

		[Rpc(SendTo.Owner)]
		public void ClientResumeCharacterRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(2540472773u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2540472773u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ResumeMovement();
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server, AllowTargetOverride = true)]
		public void ClientAssignParentConstraintSeatRpc(NetworkObjectReference objRef, byte seatIndex, uint attachmentSessionId, RpcParams rpcParams = default(RpcParams))
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
					InvokePermission = RpcInvokePermission.Server,
					AllowTargetOverride = true
				};
				FastBufferWriter bufferWriter = __beginSendRpc(1280526477u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in objRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in seatIndex, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, attachmentSessionId);
				__endSendRpc(ref bufferWriter, 1280526477u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!TryGetAttachSeat(objRef, seatIndex, out var nObj, out var attachObj, out var seat))
			{
				return;
			}
			if (_surfBoard != null)
			{
				EndSurfLocal(_surfBoard, carry: false);
			}
			if (base.IsServer)
			{
				Surfboard.ReleasePlayer(this, SurfExitReason.Knocked);
			}
			if (ParentConstraint == null)
			{
				UtilityMethods.Log("[PPG_Player] ParentConstraint is null");
				return;
			}
			if (_networkTransform != null)
			{
				_networkTransform.enabled = false;
			}
			if (_networkTransformHead != null)
			{
				_networkTransformHead.enabled = false;
			}
			if (_networkTransformSpine != null)
			{
				_networkTransformSpine.enabled = false;
			}
			if (base.IsOwner)
			{
				PauseMovement();
				IsTrapped = true;
				if (Harpoon != null)
				{
					Harpoon.Challenge_Fail();
				}
				if (IsToolActive)
				{
					DisableActiveTool();
				}
				Singleton<QuickSlotHandler>.Instance?.SuspendCurrentSelectionForMode(HeldSlotSuspensionReason.Attachment);
				if (!_hasCachedCameraParentLocalPosition && _interactor != null && _interactor.Player != null)
				{
					_cachedCameraParentLocalPosition = _interactor.Player.CameraParent.localPosition;
					_hasCachedCameraParentLocalPosition = true;
				}
			}
			PlayerAttachmentMode attachmentMode = attachObj.AttachmentMode;
			bool flag = attachmentMode == PlayerAttachmentMode.SharkDrag;
			bool num = attachmentMode == PlayerAttachmentMode.Mount || attachmentMode == PlayerAttachmentMode.SharkDrag || attachmentMode == PlayerAttachmentMode.BossGrab;
			SetupSeatConstraint(seat);
			SetSharkDragAnimator(flag);
			SetLocomotionAnimationSuppressed(flag);
			SetSharkDragFPSPresentation(flag);
			_activeAttachmentMode = attachmentMode;
			_activeAttachmentNetworkObject = nObj;
			_activeAttachmentSessionId = attachmentSessionId;
			if (flag)
			{
				_nextSharkDragFeedbackTime = 0f;
			}
			if (base.IsOwner & flag)
			{
				PauseInputs();
				Harpoon?.StartSharkDragPunchPrompt();
				if (Singleton<HintsManager>.Instance != null)
				{
					Singleton<HintsManager>.Instance.ClearAllHints();
					Singleton<HintsManager>.Instance.ShowExclusiveHint(this, "RapidPress", "gameplay.shark.drag.punch", "UiTable");
				}
			}
			if (num)
			{
				PauseConstraints();
			}
			if (base.IsOwner && attachmentMode == PlayerAttachmentMode.Mount)
			{
				if (nObj.TryGetComponent<Totti>(out var component))
				{
					_seatedTotti = component;
				}
				if (_interactor != null && _interactor.Player != null)
				{
					_interactor.Player.CameraParent.localPosition = new Vector3(0f, 1.35f, 1f);
					_interactor.Player.FirstPersonCharacter.PitchInstant(22.5f);
				}
				if (_divingEquipment != null)
				{
					_divingEquipment.PreventOxygenLoss = true;
				}
			}
			if (base.IsOwner && seat.LookAt != Vector3.zero)
			{
				_interactor.Player.FirstPersonCharacter.LookAtInstant(seat.LookAt);
			}
		}

		[Rpc(SendTo.Everyone)]
		public void ClientDeAssignParentConstraintRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3282599242u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3282599242u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientDeAssignParentConstraint();
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		public void ClientReleaseBossGrabRpc(Vector3 throwDirection, float throwImpulse, byte oxygenDamage)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2694631059u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in throwDirection);
				bufferWriter.WriteValueSafe(in throwImpulse, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in oxygenDamage, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2694631059u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ClientDeAssignParentConstraint();
			if (base.IsOwner)
			{
				if (oxygenDamage > 0 && _divingEquipment != null)
				{
					_divingEquipment.ConsumeOxygen(oxygenDamage, withFeedback: true);
				}
				if (throwImpulse > 0f && _firstPersonCharacter != null)
				{
					_firstPersonCharacter.ImpulseInDirection(throwDirection, throwImpulse);
				}
			}
		}

		public void ClientDeAssignParentConstraint()
		{
			if (_surfBoard != null)
			{
				EndSurfLocal(_surfBoard, carry: false);
				return;
			}
			if (ParentConstraint == null)
			{
				UtilityMethods.Log("[PPG_Player] ParentConstraint is null");
				return;
			}
			Vector3 position = base.transform.position;
			Quaternion quaternion = BuildFlatYawRotation(base.transform.rotation);
			if (_networkTransform != null)
			{
				_networkTransform.enabled = false;
			}
			if (_networkTransformHead != null)
			{
				_networkTransformHead.enabled = false;
			}
			if (_networkTransformSpine != null)
			{
				_networkTransformSpine.enabled = false;
			}
			ReleaseSeatConstraint();
			SetSharkDragAnimator(isDragging: false);
			bool flag = _activeAttachmentMode == PlayerAttachmentMode.SharkDrag;
			bool flag2 = _activeAttachmentMode == PlayerAttachmentMode.Mount || _activeAttachmentMode == PlayerAttachmentMode.SharkDrag || _activeAttachmentMode == PlayerAttachmentMode.BossGrab;
			_activeAttachmentMode = PlayerAttachmentMode.None;
			_activeAttachmentNetworkObject = null;
			_activeAttachmentSessionId = 0u;
			_nextSharkDragFeedbackTime = 0f;
			SetLocomotionAnimationSuppressed(suppressed: false);
			SetSharkDragFPSPresentation(active: false);
			ForceDetachPose(position, quaternion);
			if (base.IsOwner)
			{
				IsTrapped = false;
				if (flag)
				{
					ResumeInputs();
					Harpoon?.StopSharkDragPunchPrompt();
					if (Singleton<HintsManager>.Instance != null)
					{
						Singleton<HintsManager>.Instance.ClearAllHints();
						Singleton<HintsManager>.Instance.HideExclusiveHint(this);
					}
				}
				RestoreCameraAfterSeat();
				SyncControllerRotationAfterDetach(quaternion);
				ForceDetachPose(position, quaternion);
				TryRestoreCharacterControlAfterModeExit();
				if (flag2)
				{
					ResumeConstraintsIfAllowed();
				}
				ForceDetachPose(position, quaternion);
				if (flag)
				{
					Interactor?.RefreshCurrentHints();
				}
				Singleton<QuickSlotHandler>.Instance?.ResumeCurrentSelectionForMode(HeldSlotSuspensionReason.Attachment);
			}
			if (_divingEquipment != null)
			{
				_divingEquipment.PreventOxygenLoss = false;
			}
			_seatedTotti = null;
			if (_networkTransform != null)
			{
				_networkTransform.enabled = true;
			}
			if (_networkTransformHead != null)
			{
				_networkTransformHead.enabled = true;
			}
			if (_networkTransformSpine != null)
			{
				_networkTransformSpine.enabled = true;
			}
			ForceDetachPose(position, quaternion);
		}

		public bool TryHandleSharkDragEscapeInput(InputActionPhase phase)
		{
			if (!base.IsOwner || _activeAttachmentMode != PlayerAttachmentMode.SharkDrag || _activeAttachmentNetworkObject == null)
			{
				return false;
			}
			if (phase != InputActionPhase.Performed)
			{
				return true;
			}
			TryPlaySharkDragEscapeFeedback();
			ServerSubmitSharkDragEscapePressRpc(_activeAttachmentNetworkObject.ToReference(), _activeAttachmentSessionId);
			return true;
		}

		private void TryPlaySharkDragEscapeFeedback()
		{
			if (!_activeAttachmentNetworkObject.TryGetComponent<SharkCreature>(out var component))
			{
				return;
			}
			SharkSO sharkSO = component.SharkSO;
			float num = ((sharkSO != null) ? Mathf.Max(0.01f, sharkSO.DragEscapeFeedbackInterval) : 0.4f);
			float unscaledTime = Time.unscaledTime;
			if (!(unscaledTime < _nextSharkDragFeedbackTime))
			{
				_nextSharkDragFeedbackTime = unscaledTime + num;
				float visibleDuration = ((sharkSO != null) ? Mathf.Max(0.01f, sharkSO.DragEscapePunchVisibleDuration) : 0.3f);
				PlaySharkDragPunchFPS(visibleDuration);
				if (sharkSO?.DragEscapePunchSfx != null && Singleton<SoundManager>.Instance != null)
				{
					Singleton<SoundManager>.Instance.PlaySFX(sharkSO.DragEscapePunchSfx);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerSubmitSharkDragEscapePressRpc(NetworkObjectReference sharkReference, uint dragSessionId, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2043642383u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in sharkReference, default(FastBufferWriter.ForNetworkSerializable));
				BytePacker.WriteValueBitPacked(bufferWriter, dragSessionId);
				__endSendRpc(ref bufferWriter, 2043642383u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && rpcParams.Receive.SenderClientId == base.OwnerClientId && sharkReference.TryGet(out var networkObject) && !(networkObject == null) && networkObject.TryGetComponent<SharkCreature>(out var component))
				{
					component.ServerTryRegisterDragEscapePress(this, dragSessionId);
				}
			}
		}

		private bool IsDrownedOrKnocked()
		{
			if (_divingEquipment != null)
			{
				return _divingEquipment.IsKnocked;
			}
			return false;
		}

		private void ForceFlatPlayerRotation(Vector3 position, Quaternion flatRotation)
		{
			base.transform.SetPositionAndRotation(position, flatRotation);
			Vector3 eulerAngles = base.transform.eulerAngles;
			base.transform.eulerAngles = new Vector3(0f, eulerAngles.y, 0f);
		}

		private IEnumerator DetachRotationCorrectionCoroutine(Vector3 position, Quaternion flatRotation)
		{
			yield return null;
			ForceFlatPlayerRotation(position, flatRotation);
			yield return new WaitForEndOfFrame();
			ForceFlatPlayerRotation(position, flatRotation);
		}

		private Quaternion BuildFlatYawRotation(Quaternion sourceRotation)
		{
			Vector3 vector = Vector3.ProjectOnPlane(sourceRotation * Vector3.forward, Vector3.up);
			if (vector.sqrMagnitude < 0.0001f)
			{
				vector = Vector3.ProjectOnPlane(base.transform.forward, Vector3.up);
			}
			if (vector.sqrMagnitude < 0.0001f)
			{
				vector = Vector3.forward;
			}
			return Quaternion.LookRotation(vector.normalized, Vector3.up);
		}

		private void ForceDetachPose(Vector3 position, Quaternion rotation)
		{
			base.transform.SetPositionAndRotation(position, rotation);
			if (_firstPersonCharacter != null)
			{
				_firstPersonCharacter.TeleportPosition(position);
				_firstPersonCharacter.TeleportRotation(rotation);
			}
		}

		private void SyncControllerRotationAfterDetach(Quaternion flatRotation)
		{
			if (!(_interactor == null) && !(_interactor.Player == null) && !(_interactor.Player.FirstPersonCharacter == null))
			{
				Vector3 vector = Vector3.ProjectOnPlane(flatRotation * Vector3.forward, Vector3.up);
				if (vector.sqrMagnitude < 0.0001f)
				{
					vector = Vector3.forward;
				}
				Vector3 worldTarget = base.transform.position + vector.normalized * 5f;
				_interactor.Player.FirstPersonCharacter.LookAtInstant(worldTarget);
				_interactor.Player.FirstPersonCharacter.PitchInstant(0f);
			}
		}

		private bool TryGetAttachSeat(NetworkObjectReference objRef, byte seatIndex, out NetworkObject nObj, out IAttachableObject attachObj, out AttachSeat seat)
		{
			nObj = null;
			attachObj = null;
			seat = null;
			if (!objRef.TryGet(out nObj) || !nObj.TryGetComponent<IAttachableObject>(out attachObj))
			{
				UtilityMethods.Log("[PPG_Player] AttachableObject is null");
				return false;
			}
			if (attachObj.Seats == null || attachObj.Seats.Count == 0)
			{
				UtilityMethods.Log("[PPG_Player] Seats list is empty");
				return false;
			}
			if (seatIndex >= attachObj.Seats.Count)
			{
				UtilityMethods.Log($"[PPG_Player] Invalid seat index: {seatIndex}");
				return false;
			}
			seat = attachObj.Seats[seatIndex];
			if (seat == null)
			{
				UtilityMethods.Log("[PPG_Player] Seat is null");
				return false;
			}
			if (seat.AttachPoint == null)
			{
				UtilityMethods.Log("[PPG_Player] Seat AttachPoint is null");
				return false;
			}
			return true;
		}

		private void SetupSeatConstraint(AttachSeat seat)
		{
			ParentConstraint parentConstraint = ParentConstraint;
			Transform attachPoint = seat.AttachPoint;
			Vector3 positionOffset = seat.PositionOffset;
			Vector3 rotationOffset = seat.RotationOffset;
			parentConstraint.constraintActive = false;
			parentConstraint.locked = false;
			parentConstraint.SetSources(new List<ConstraintSource>());
			ConstraintSource source = new ConstraintSource
			{
				sourceTransform = attachPoint,
				weight = 1f
			};
			parentConstraint.AddSource(source);
			parentConstraint.SetTranslationOffset(0, positionOffset);
			parentConstraint.SetRotationOffset(0, rotationOffset);
			base.transform.SetPositionAndRotation(seat.AttachPoint.TransformPoint(seat.PositionOffset), seat.AttachPoint.rotation * Quaternion.Euler(seat.RotationOffset));
			parentConstraint.locked = true;
			parentConstraint.constraintActive = true;
		}

		private void ReleaseSeatConstraint()
		{
			ParentConstraint parentConstraint = ParentConstraint;
			parentConstraint.constraintActive = false;
			parentConstraint.locked = false;
			parentConstraint.SetSources(new List<ConstraintSource>());
		}

		private void RestoreCameraAfterSeat()
		{
			if (_hasCachedCameraParentLocalPosition && !(_interactor == null) && !(_interactor.Player == null))
			{
				_interactor.Player.CameraParent.localPosition = _cachedCameraParentLocalPosition;
				_hasCachedCameraParentLocalPosition = false;
			}
		}

		public virtual void Teleport(Transform fromPortal, Transform toPortal, Vector3 pos, Quaternion rot)
		{
			TeleportPlayer(pos, rot, updateGround: false);
		}

		public virtual void EnterPortalThreshold()
		{
			if (graphicsClone == null)
			{
				graphicsClone = UnityEngine.Object.Instantiate(graphicsObject);
				graphicsClone.transform.parent = graphicsObject.transform.parent;
				graphicsClone.transform.localScale = graphicsObject.transform.localScale;
				originalMaterials = GetMaterials(graphicsObject);
				cloneMaterials = GetMaterials(graphicsClone);
			}
			else
			{
				graphicsClone.SetActive(value: true);
			}
		}

		public virtual void ExitPortalThreshold()
		{
			graphicsClone.SetActive(value: false);
			for (int i = 0; i < originalMaterials.Length; i++)
			{
				originalMaterials[i].SetVector("sliceNormal", Vector3.zero);
			}
		}

		public void SetSliceOffsetDst(float dst, bool clone)
		{
			for (int i = 0; i < originalMaterials.Length; i++)
			{
				if (clone)
				{
					cloneMaterials[i].SetFloat("sliceOffsetDst", dst);
				}
				else
				{
					originalMaterials[i].SetFloat("sliceOffsetDst", dst);
				}
			}
		}

		private Material[] GetMaterials(GameObject g)
		{
			MeshRenderer[] componentsInChildren = g.GetComponentsInChildren<MeshRenderer>();
			List<Material> list = new List<Material>();
			MeshRenderer[] array = componentsInChildren;
			for (int i = 0; i < array.Length; i++)
			{
				Material[] materials = array[i].materials;
				foreach (Material item in materials)
				{
					list.Add(item);
				}
			}
			return list.ToArray();
		}

		public PlayerData Save()
		{
			UtilityMethods.Log($"[PPG_Player_Save] Save {base.OwnerClientId}");
			ServiceLocator.ForSceneOf(this).Get<TabletNetworkHandler>(out var _);
			return new PlayerData
			{
				ToolData = ToolSave()
			};
		}

		public ClientData SaveClientData()
		{
			ClientData obj = Singleton<SaveManager>.Instance.CurrentClientData ?? new ClientData();
			obj.QuickSlotData = Singleton<QuickSlotHandler>.Instance.Save();
			return obj;
		}

		public void Load(PlayerData data)
		{
			if (data == null)
			{
				UtilityMethods.Log($"[PPG_Player_Save] Load skipped. Data is null. OwnerClientId={base.OwnerClientId}", LogType.Warning);
				return;
			}
			UtilityMethods.Log($"[PPG_Player_Save] Load {base.OwnerClientId} \n {data.ToolData}");
			ToolLoad(data.ToolData);
		}

		public void LoadClientData(ClientData data)
		{
			if (data == null)
			{
				UtilityMethods.Log("[PPG_Player_Save] LoadClientData skipped. Data is null.", LogType.Warning);
				return;
			}
			string text = $"{data.HostSteamID}_{data.SlotID}";
			if (_lastLoadedClientProfileKey == text)
			{
				UtilityMethods.Log("[PPG_Player_Save] LoadClientData skipped. Profile already applied.\n   ProfileKey: " + text);
				return;
			}
			if (data.QuickSlotData != null)
			{
				Singleton<QuickSlotHandler>.Instance.Load(data.QuickSlotData);
			}
			_lastLoadedClientProfileKey = text;
		}

		public void BeginSurfLocal(Surfboard board)
		{
			if (board == null || !board.IsSpawned || !board.IsRiding || board.Holder != base.OwnerClientId || !_surfHistory.CanEnter(board.NetworkObjectId, board.Session) || (_activeAttachmentMode != PlayerAttachmentMode.None && _activeAttachmentMode != PlayerAttachmentMode.Surfboard) || (_surfBoard == board && _surfSession == board.Session))
			{
				return;
			}
			if (_surfBoard != null)
			{
				EndSurfLocal(_surfBoard, carry: false);
			}
			_surfBoard = board;
			_surfSession = board.Session;
			_activeAttachmentMode = PlayerAttachmentMode.Surfboard;
			_activeAttachmentNetworkObject = board.NetworkObject;
			_activeAttachmentSessionId = board.Session;
			if (_networkTransform != null)
			{
				_networkTransform.enabled = false;
			}
			if (_networkTransformHead != null)
			{
				_networkTransformHead.enabled = false;
			}
			if (_networkTransformSpine != null)
			{
				_networkTransformSpine.enabled = false;
			}
			_surfPreviousFootIk = _footIK != null && _footIK.enabled;
			if (_footIK != null)
			{
				_footIK.enabled = false;
			}
			PauseConstraints();
			SetLocomotionAnimationSuppressed(suppressed: true);
			SetHolding(isHolding: false);
			if (_tpsAnimator != null)
			{
				_surfVisualPosition = _tpsAnimator.transform.localPosition;
				_surfVisualRotation = _tpsAnimator.transform.localRotation;
				_surfLayer = _tpsAnimator.GetLayerIndex("Surf");
				if (_surfLayer >= 0)
				{
					_tpsAnimator.SetLayerWeight(_surfLayer, 1f);
					_tpsAnimator.Play(board.Standing ? "Surf.Stand" : "Surf.Prone", _surfLayer, 0f);
				}
			}
			_surfBlend = (board.Standing ? 1 : 0);
			_surfLastStanding = board.Standing;
			_surfPoseFrom = (_surfPoseTo = _surfBlend);
			_surfPoseStarted = board.PoseTime;
			_surfPoseDriver = new SurfPoseDriver(_tpsAnimator);
			if (!base.IsOwner)
			{
				return;
			}
			SetSurfCursorSuppressed(suppressed: true);
			PermaPause();
			PauseLook();
			IsSwimming.Value = false;
			StopHighSpeedWindLoop();
			_audioSourceSwimNormal?.Stop();
			_audioSourceSwimSprint?.Stop();
			_firstPersonCharacterInput?.OutUnderWater();
			SetRippleTrail(s: false);
			if (_divingEquipment != null)
			{
				_surfPreviousOxygen = _divingEquipment.PreventOxygenLoss;
				_divingEquipment.PreventOxygenLoss = true;
			}
			Singleton<QuickSlotHandler>.Instance?.SuspendCurrentSelectionForMode(HeldSlotSuspensionReason.Attachment);
			Singleton<HintsManager>.Instance?.AddHint("Jump", "Surf.TogglePose");
			Singleton<HintsManager>.Instance?.AddHint("Throw", "Surf.Dismount");
			UpdateSurfControlHint(board.Standing);
			_surfMove = Vector2.zero;
			_surfYaw = ((_playerCamera != null) ? _playerCamera.transform.eulerAngles.y : board.transform.eulerAngles.y);
			_surfPitch = 14f;
			_surfGamepadLook = Vector2.zero;
			_surfInput = ((Singleton<InputManager>.Instance != null) ? Singleton<InputManager>.Instance.InputHandler : null);
			if (_surfInput != null)
			{
				_surfInput.Move += OnSurfMove;
				_surfInput.Look += OnSurfLook;
				_surfInput.Jump += OnSurfJump;
			}
			if (SurfInputAllowed)
			{
				_surfMove = new Vector2(0f, Mathf.Clamp01(_playerInput.actions["Move"].ReadValue<Vector2>().y));
			}
			_surfSendAt = 0f;
			_surfPreviousCameras = new CinemachineCamera[5] { _cinemachineCamera, _emoteCamera, _tottiCamera, _actionCamera, _tpsActionCamera };
			_surfPreviousCameraStates = new bool[_surfPreviousCameras.Length];
			for (int i = 0; i < _surfPreviousCameras.Length; i++)
			{
				if (_surfPreviousCameras[i] != null)
				{
					_surfPreviousCameraStates[i] = _surfPreviousCameras[i].enabled;
					_surfPreviousCameras[i].enabled = false;
				}
			}
			_surfPreviousCulling = _playerCamera.cullingMask;
			_surfPreviousViewModel = _viewModelCamera.enabled;
			ShowPlayer();
			_playerVisuals?.SetTpsHeadMeshesVisible(visible: true);
			GameObject gameObject = new GameObject("Surf camera");
			_surfCamera = gameObject.AddComponent<CinemachineCamera>();
			_surfCamera.Lens = _cinemachineCamera.Lens;
			_surfCamera.Lens.NearClipPlane = 0.15f;
			_surfCamera.Priority = 100;
			_surfCameraRig = new SurfCameraRig();
			UpdateSurfCamera(instant: true);
		}

		private void OnSurfMove(InputActionPhase phase, Vector2 input)
		{
			_surfMove = ((phase == InputActionPhase.Canceled || !SurfInputAllowed) ? Vector2.zero : new Vector2(0f, Mathf.Clamp01(input.y)));
		}

		private void OnSurfLook(InputActionPhase phase, Vector2 input, bool isMouse)
		{
			if (!SurfInputAllowed)
			{
				_surfGamepadLook = Vector2.zero;
			}
			else if (!isMouse)
			{
				_surfGamepadLook = ((phase == InputActionPhase.Canceled) ? Vector2.zero : input);
			}
			else if (phase == InputActionPhase.Performed)
			{
				ApplySurfLook(input * RuntimeSettings.Sensitivity * 0.5f);
			}
		}

		private void ApplySurfLook(Vector2 input)
		{
			if (RuntimeSettings.InvertXAxisEnabled)
			{
				input.x = 0f - input.x;
			}
			if (RuntimeSettings.InvertYAxisEnabled)
			{
				input.y = 0f - input.y;
			}
			_surfYaw = Mathf.Repeat(_surfYaw + input.x + 180f, 360f) - 180f;
			_surfPitch = Mathf.Clamp(_surfPitch - input.y, 6f, 45f);
		}

		private void OnSurfJump(InputActionPhase phase)
		{
			if (SurfInputAllowed && phase == InputActionPhase.Started && _surfBoard != null)
			{
				_surfBoard.TogglePoseRpc(_surfSession);
			}
		}

		public bool TryDropSurfboard(InputActionPhase phase)
		{
			Surfboard surfboard = _surfBoard;
			if (surfboard == null && base.IsOwner && Singleton<QuickSlotHandler>.Instance != null)
			{
				surfboard = Singleton<QuickSlotHandler>.Instance.HeldSlotElement?.HeldItem as Surfboard;
			}
			if (surfboard == null)
			{
				return false;
			}
			if (SurfInputAllowed && phase == InputActionPhase.Started)
			{
				surfboard.RequestReleaseRpc(surfboard.Session);
			}
			return true;
		}

		private void TickSurfOwner()
		{
			if (!base.IsOwner || _surfBoard == null)
			{
				return;
			}
			SetOwnerConstraintStatus(enable: false);
			if (!SurfInputAllowed)
			{
				_surfMove = Vector2.zero;
				_surfGamepadLook = Vector2.zero;
			}
			else
			{
				ApplySurfLook(_surfGamepadLook * RuntimeSettings.Sensitivity * Time.unscaledDeltaTime * 100f);
			}
			if (IsTrapped || (_divingEquipment != null && _divingEquipment.IsKnocked))
			{
				CancelSurfForTeleport(SurfExitReason.Knocked);
				return;
			}
			TickSurfWaveAlert();
			if (!(Time.unscaledTime < _surfSendAt))
			{
				_surfSendAt = Time.unscaledTime + 0.05f;
				_surfBoard.SubmitInputRpc(_surfSession, _surfMove, Quaternion.Euler(0f, _surfYaw, 0f) * Vector3.forward, SurfInputAllowed);
			}
		}

		private void TickSurfPresentation()
		{
			if (_surfBoard == null)
			{
				return;
			}
			if (!_surfBoard.IsSpawned || !_surfBoard.IsRiding || _surfBoard.Holder != base.OwnerClientId)
			{
				EndSurfLocal(_surfBoard, _surfBoard.IsCarried);
				return;
			}
			ApplyConstraints(enable: false);
			if (_footIK != null)
			{
				_footIK.enabled = false;
			}
			base.transform.SetPositionAndRotation(_surfBoard.riderAnchor.position, _surfBoard.riderAnchor.rotation);
			bool standing = _surfBoard.Standing;
			if (base.IsOwner && standing != _surfLastStanding)
			{
				UpdateSurfControlHint(standing);
			}
			if (standing != _surfLastStanding)
			{
				_surfPoseFrom = _surfBlend;
				_surfPoseTo = (standing ? 1 : 0);
				_surfPoseStarted = _surfBoard.PoseTime;
				if (_surfLayer >= 0 && _tpsAnimator != null)
				{
					_tpsAnimator.CrossFadeInFixedTime(standing ? "Surf.Stand" : "Surf.Prone", 0.55f, _surfLayer);
				}
			}
			_surfLastStanding = standing;
			double num = ((SurfboardWorld.Instance != null) ? SurfboardWorld.Instance.TimeNow : Time.timeAsDouble);
			_surfBlend = Mathf.Lerp(_surfPoseFrom, _surfPoseTo, Mathf.SmoothStep(0f, 1f, (float)(num - _surfPoseStarted) / 0.55f));
			if (_tpsAnimator != null)
			{
				_tpsAnimator.transform.localPosition = _surfVisualPosition + Vector3.Lerp(new Vector3(0f, 0.28f, -0.85f), Vector3.zero, _surfBlend);
				_tpsAnimator.transform.localRotation = SurfPosePlacement.BodyRotation(_surfBlend) * _surfVisualRotation;
				_surfPoseDriver?.Apply(_surfBoard.riderAnchor.position, _surfBoard.riderAnchor.rotation, _surfBoard.HalfWidth, _surfBoard.HalfLength, _surfBlend, _surfBoard.PaddleBlend, _surfBoard.PaddlePhase, _surfBoard.TurnLean, num);
			}
			if (base.IsOwner)
			{
				UpdateSurfCamera(instant: false);
			}
		}

		private static void UpdateSurfControlHint(bool standing)
		{
			Singleton<HintsManager>.Instance?.RemoveHint("Move");
			Singleton<HintsManager>.Instance?.RemoveHint("Look");
			Singleton<HintsManager>.Instance?.AddHint(standing ? "Look" : "Move", standing ? "Surf.StandingControls" : "Surf.ProneControls");
		}

		private void TickSurfWaveAlert()
		{
			SurfboardWorld instance = SurfboardWorld.Instance;
			if (!(instance == null) && SurfInputAllowed && !(Singleton<NotifyManager>.Instance == null))
			{
				if (_surfAlertWorld != instance)
				{
					_surfAlertWorld = instance;
					_surfLastAlertWaveId = 0u;
				}
				SurfWave activeWave = instance.ActiveWave;
				if (!(activeWave.Envelope(instance.TimeNow) < 0.1f) && activeWave.Id != _surfLastAlertWaveId && activeWave.TryGetArrival(_surfBoard.transform.position, instance.TimeNow, out var seconds) && !(seconds > 18f) && !Singleton<NotifyManager>.Instance.IsAlertShowing)
				{
					_surfLastAlertWaveId = activeWave.Id;
					string format = LocalizationDatabase.GetString("GameStateTable", "Surf.WaveIncomingTitle", "Surf wave: ~{0} seconds");
					_surfAlertHeader = string.Format(format, Mathf.CeilToInt(seconds));
					Vector3 normalized = Vector3.ProjectOnPlane(activeWave.Center(instance.TimeNow) - _surfBoard.transform.position, Vector3.up).normalized;
					float num = Vector3.SignedAngle(Quaternion.Euler(0f, _surfYaw, 0f) * Vector3.forward, normalized, Vector3.up);
					string text = ((Mathf.Abs(num) < 45f) ? "Front" : ((Mathf.Abs(num) > 135f) ? "Behind" : ((num > 0f) ? "Right" : "Left")));
					_surfAlertDescription = LocalizationDatabase.GetString("GameStateTable", "Surf.WaveFrom" + text, "A wave is approaching. Face the restaurant and stand up to ride it.");
					Singleton<NotifyManager>.Instance.ShowAlert(_surfAlertHeader, _surfAlertDescription, 4f, AlertPriority.Low, autoClose: true, usePriorityPulse: false);
				}
			}
		}

		private void UpdateSurfCamera(bool instant)
		{
			if (!(_surfCamera == null) && !(_surfBoard == null) && _surfCameraRig != null)
			{
				_surfCameraRig.Update(_surfBoard.transform, SurfboardWorld.Instance, _surfCameraSettings, _surfBlend, _surfYaw, _surfPitch, Time.deltaTime, instant);
				_surfCamera.transform.SetPositionAndRotation(_surfCameraRig.Position, _surfCameraRig.Rotation);
				_surfCamera.Lens.FieldOfView = _surfCameraRig.FieldOfView;
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		public void ClientEndSurfRpc(NetworkObjectReference boardReference, uint session, bool carry)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1539903271u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in boardReference, default(FastBufferWriter.ForNetworkSerializable));
				BytePacker.WriteValueBitPacked(bufferWriter, session);
				bufferWriter.WriteValueSafe(in carry, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1539903271u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (boardReference.TryGet(out var networkObject) && networkObject.TryGetComponent<Surfboard>(out var component))
			{
				if (_surfBoard == component && _surfSession == session)
				{
					EndSurfLocal(component, carry);
				}
				_surfHistory.End(networkObject.NetworkObjectId, session);
			}
		}

		public void EndSurfLocal(Surfboard board, bool carry)
		{
			if (_surfBoard == null || _surfBoard != board)
			{
				return;
			}
			_surfHistory.End(board.NetworkObjectId, _surfSession);
			if (base.IsOwner && !carry && Vector3.Distance(base.transform.position, board.transform.position) < 4f)
			{
				base.transform.position = board.SafeExitPosition(base.transform.position);
			}
			_surfBoard = null;
			if (_surfInput != null)
			{
				_surfInput.Move -= OnSurfMove;
				_surfInput.Look -= OnSurfLook;
				_surfInput.Jump -= OnSurfJump;
				_surfInput = null;
			}
			_activeAttachmentMode = PlayerAttachmentMode.None;
			_activeAttachmentNetworkObject = null;
			_activeAttachmentSessionId = 0u;
			if (_tpsAnimator != null)
			{
				if (_surfLayer >= 0)
				{
					_tpsAnimator.SetLayerWeight(_surfLayer, 0f);
				}
				_tpsAnimator.transform.SetLocalPositionAndRotation(_surfVisualPosition, _surfVisualRotation);
			}
			_surfLayer = -1;
			_surfPoseDriver = null;
			base.transform.rotation = Quaternion.Euler(0f, base.transform.eulerAngles.y, 0f);
			if (_footIK != null)
			{
				_footIK.enabled = _surfPreviousFootIk;
			}
			SetLocomotionAnimationSuppressed(suppressed: false);
			if (_networkTransform != null)
			{
				_networkTransform.enabled = true;
			}
			if (_networkTransformHead != null)
			{
				_networkTransformHead.enabled = true;
			}
			if (_networkTransformSpine != null)
			{
				_networkTransformSpine.enabled = true;
			}
			if (!base.IsOwner)
			{
				ResumeConstraintsIfAllowed();
				return;
			}
			SetSurfCursorSuppressed(suppressed: false);
			if (_surfAlertHeader != null)
			{
				Singleton<NotifyManager>.Instance?.TryHideAlert(_surfAlertHeader, _surfAlertDescription);
			}
			_surfAlertHeader = (_surfAlertDescription = null);
			Singleton<HintsManager>.Instance?.RemoveHint("Jump");
			Singleton<HintsManager>.Instance?.RemoveHint("Throw");
			Singleton<HintsManager>.Instance?.RemoveHint("Move");
			Singleton<HintsManager>.Instance?.RemoveHint("Look");
			if (!carry)
			{
				Singleton<QuickSlotHandler>.Instance?.RemoveSpecificItem(board, selectNextIfPossible: false);
			}
			Singleton<QuickSlotHandler>.Instance?.ResumeCurrentSelectionForMode(HeldSlotSuspensionReason.Attachment);
			if (_divingEquipment != null)
			{
				_divingEquipment.PreventOxygenLoss = _surfPreviousOxygen;
			}
			if (_surfCamera != null)
			{
				_surfCamera.enabled = false;
				UnityEngine.Object.Destroy(_surfCamera.gameObject);
				_surfCamera = null;
			}
			_surfCameraRig = null;
			if (_surfPreviousCameras != null)
			{
				for (int i = 0; i < _surfPreviousCameras.Length; i++)
				{
					if (_surfPreviousCameras[i] != null)
					{
						_surfPreviousCameras[i].enabled = _surfPreviousCameraStates[i];
					}
				}
			}
			_playerCamera.cullingMask = _surfPreviousCulling;
			_viewModelCamera.enabled = _surfPreviousViewModel;
			_playerVisuals?.SetTpsHeadMeshesVisible(visible: false);
			SetActiveCamera();
			_firstPersonCharacter.TeleportPosition(base.transform.position);
			_firstPersonCharacter.TeleportRotation(base.transform.rotation);
			_firstPersonCharacter.SetMovementMode(Character.MovementMode.Falling);
			_firstPersonCharacter.characterMovement.velocity = Vector3.zero;
			TryRestoreCharacterControlAfterModeExit();
		}

		private void CancelSurfForTeleport(SurfExitReason reason)
		{
			if (base.IsOwner && HasSurfboard)
			{
				ServerCancelSurfRpc(reason);
				if (_surfBoard != null)
				{
					EndSurfLocal(_surfBoard, carry: false);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerCancelSurfRpc(SurfExitReason reason, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2958814783u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in reason, default(FastBufferWriter.ForEnums));
				__endSendRpc(ref bufferWriter, 2958814783u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (rpcParams.Receive.SenderClientId == base.OwnerClientId)
				{
					Surfboard.ReleasePlayer(this, (reason == SurfExitReason.Knocked) ? reason : SurfExitReason.Teleport);
				}
			}
		}

		private void SetupTablet()
		{
			UtilityMethods.SetLayerRecursive(_tabletTpsObject, base.IsOwner ? "Player" : "ClientPlayer", includeChildren: false);
			_tabletFpsObject.SetActive(value: false);
			_tabletTpsObject.SetActive(value: false);
		}

		private void RegisterTabletEvents()
		{
			_tabletController.RegisterEvents();
			FirstPersonCharacterInput firstPersonCharacterInput = _firstPersonCharacterInput;
			firstPersonCharacterInput.MovementEvent = (Action<bool>)Delegate.Combine(firstPersonCharacterInput.MovementEvent, new Action<bool>(OnCheckTabletAnim));
		}

		private void UnRegisterTabletEvents()
		{
			_tabletController.UnregisterEvents();
			FirstPersonCharacterInput firstPersonCharacterInput = _firstPersonCharacterInput;
			firstPersonCharacterInput.MovementEvent = (Action<bool>)Delegate.Remove(firstPersonCharacterInput.MovementEvent, new Action<bool>(OnCheckTabletAnim));
		}

		public void OnCheckTabletAnim(bool condition)
		{
			_isMovementInputActive = condition;
			if (_tabletExtraCheck && IsTabletActive && _checkTabletAnim != condition)
			{
				ApplyTabletCursorStateByMovementInput(condition);
				_checkTabletAnim = condition;
			}
		}

		private void ApplyTabletCursorStateByMovementInput(bool isMovementInputActive)
		{
			if (!isMovementInputActive)
			{
				PauseLook();
				HideCursor();
				CursorHelper.CursorVisible = true;
			}
			else
			{
				ResumeLook();
				ShowCursor();
				CursorHelper.CursorVisible = false;
			}
		}

		private void ApplyTabletClosedCursorState()
		{
			ResumeLook();
			ShowCursor();
			CursorHelper.CursorVisible = false;
		}

		private void RefreshTabletCursorStateEndOfFrame()
		{
			if (_tabletCursorRefreshCor != null)
			{
				Singleton<DelayHelper>.Instance.StopCoroutine(_tabletCursorRefreshCor);
			}
			_tabletCursorRefreshCor = Singleton<DelayHelper>.Instance.DelayActionUntilEndOfFrame(() =>
			{
				_tabletCursorRefreshCor = null;
				if (_tabletExtraCheck && IsTabletActive)
				{
					ApplyTabletCursorStateByMovementInput(_isMovementInputActive);
					_checkTabletAnim = _isMovementInputActive;
				}
			});
		}

		public void SetTabletFpsStatus(bool status)
		{
			if (!status && !_tabletFpsObject.activeSelf)
			{
				return;
			}
			if (_disableCor != null)
			{
				Singleton<DelayHelper>.Instance.StopCoroutine(_disableCor);
			}
			if (status)
			{
				Singleton<QuickSlotHandler>.Instance.DeselectSlot();
			}
			if (status)
			{
				EquipHoldProfile(4);
			}
			SetHolding(status, startHold: true);
			if (!status)
			{
				ApplyTabletClosedCursorState();
			}
			else
			{
				ApplyTabletCursorStateByMovementInput(_isMovementInputActive);
			}
			_tabletExtraCheck = status;
			_checkTabletAnim = _isMovementInputActive;
			if (status)
			{
				TabletStatus(status: true);
				RefreshTabletCursorStateEndOfFrame();
			}
			else
			{
				_disableCor = Singleton<DelayHelper>.Instance.DelayAction(0.5f, () =>
				{
					TabletStatus(status: false);
				});
			}
		}

		private void TabletStatus(bool status)
		{
            if (!this || !_tabletFpsObject || !IsSpawned) return;
			IsTabletActive = status;
			_tabletFpsObject.SetActive(status);
			ClientSyncTabletStatusRpc(status);
		}

		[Rpc(SendTo.NotOwner)]
		private void ClientSyncTabletStatusRpc(bool status)
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
				FastBufferWriter bufferWriter = __beginSendRpc(28955413u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in status, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 28955413u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				SetTabletTpsStatus(status);
			}
		}

		public void SetTabletTpsStatus(bool status)
		{
			if (_tabletTpsObject.activeSelf != status)
			{
				_tabletTpsObject.SetActive(status);
			}
		}

		public ToolData ToolSave()
		{
			return _relay.ToolDataNV.Value.ToToolData();
		}

		public bool ServerUnlockPoseidonTrident()
		{
			if (!base.IsServer || _relay == null)
			{
				return false;
			}
			ToolDataNet value = _relay.ToolDataNV.Value;
			if (value.PoseidonTridentUnlocked)
			{
				return true;
			}
			value.PoseidonTridentUnlocked = true;
			_relay.ToolDataNV.Value = value;
			return true;
		}

		public void ToolLoad(ToolData data)
		{
			if (data != null && base.IsServer)
			{
				ToolDataNet value = new ToolDataNet(data);
				_relay.ToolDataNV.Value = value;
				_relay.ServerReconcileHarpoonUnlockFromQuest();
				value = _relay.ToolDataNV.Value;
				_divingEquipment.LoadLevel(value);
				_harpoon.LoadLevel(value);
				_fishingRod.LoadLevel(value);
				_compass.LoadLevel(value);
				_seaScooter.LoadLevel(value);
				_poseidonTrident?.LoadLevel(value);
			}
		}

		private void OnToolDataNVChanged(ToolDataEvent @event)
		{
			UtilityMethods.Log($"[PPG_Player_Tools-{base.OwnerClientId}] OnToolDataNVChanged");
			ApplyToolData(@event.ToolDataNet);
		}

		private void ApplyCurrentToolDataSnapshot()
		{
			ToolDataNet value = _relay.ToolDataNV.Value;
			UtilityMethods.Log($"[PPG_Player_Tools-{base.OwnerClientId}] Applying initial tool data snapshot. " + $"Revision:{value.SyncRevision}");
			ApplyToolData(value);
		}

		private void ApplyToolData(ToolDataNet newData)
		{
			_divingEquipment.LoadLevel(newData);
			_harpoon.LoadLevel(newData);
			_fishingRod.LoadLevel(newData);
			_compass.LoadLevel(newData);
			_seaScooter.LoadLevel(newData);
			_poseidonTrident?.LoadLevel(newData);
			_divingEquipmentEquipedLevel = newData.DivingEquipmentLevel;
			_harpoonEquipedLevel = newData.HarpoonLevel;
			_fishingRodEquipedLevel = newData.FishingRodLevel;
			_scooterEquipedLevel = newData.ScooterLevel;
			RefreshCompassWorldState();
			ShowNewToolProgressFeedback(newData);
		}

		private void ShowNewToolProgressFeedback(ToolDataNet newData)
		{
			if (!_hasObservedToolData)
			{
				_observedToolData = newData;
				_hasObservedToolData = true;
				return;
			}
			ToolDataNet observedToolData = _observedToolData;
			_observedToolData = newData;
			if (base.IsOwner)
			{
				ShowToolProgressFeedback(observedToolData.DivingEquipmentUnlocked, newData.DivingEquipmentUnlocked, observedToolData.DivingEquipmentLevel, newData.DivingEquipmentLevel, ToolType.DivingEquipment, showLevelUpgrade: true);
				ShowToolProgressFeedback(observedToolData.HarpoonUnlocked, newData.HarpoonUnlocked, observedToolData.HarpoonLevel, newData.HarpoonLevel, ToolType.Harpoon, showLevelUpgrade: true);
				ShowToolProgressFeedback(observedToolData.ScooterUnlocked, newData.ScooterUnlocked, observedToolData.ScooterLevel, newData.ScooterLevel, ToolType.SeaScooter, showLevelUpgrade: true);
				ShowToolProgressFeedback(observedToolData.FishingRodUnlocked, newData.FishingRodUnlocked, observedToolData.FishingRodLevel, newData.FishingRodLevel, ToolType.FishingRod, showLevelUpgrade: true);
				ShowToolProgressFeedback(observedToolData.MopUnlocked, newData.MopUnlocked, 0, 0, ToolType.Mop, showLevelUpgrade: false);
				ShowToolProgressFeedback(observedToolData.CompassUnlocked, newData.CompassUnlocked, observedToolData.CompassLevel, newData.CompassLevel, ToolType.Compass, showLevelUpgrade: true);
				ShowToolProgressFeedback(observedToolData.PoseidonTridentUnlocked, newData.PoseidonTridentUnlocked, 0, 0, ToolType.PoseidonTrident, showLevelUpgrade: false);
			}
		}

		private static void ShowToolProgressFeedback(bool wasUnlocked, bool isUnlocked, byte previousLevelIndex, byte currentLevelIndex, ToolType toolType, bool showLevelUpgrade)
		{
			if (isUnlocked && !(Singleton<NotifyManager>.Instance == null))
			{
				if (!wasUnlocked)
				{
					Singleton<NotifyManager>.Instance.ShowToolUnlocked(toolType);
				}
				else if (showLevelUpgrade && currentLevelIndex > previousLevelIndex)
				{
					Singleton<NotifyManager>.Instance.ShowToolUpgrade(toolType, previousLevelIndex + 1, currentLevelIndex + 1);
				}
			}
		}

		private void RegisterToolEvents()
		{
			if (base.IsOwner)
			{
				FirstPersonCharacter.PhysicsVolumeChanged += OnPhysicsVolumeChanged;
			}
		}

		private void UnRegisterToolEvents()
		{
			if (base.IsOwner)
			{
				FirstPersonCharacter.PhysicsVolumeChanged -= OnPhysicsVolumeChanged;
			}
		}

		public bool DisableActiveToolIf()
		{
			if (!CanDisableActiveTool)
			{
				return false;
			}
			DisableActiveTool();
			return true;
		}

		public void DisableActiveTool()
		{
			if (!IsToolActive)
			{
				return;
			}
			if (ActiveTool == null)
			{
				if (TryDisableOtherTools(null))
				{
					ClearTool();
				}
			}
			else if (CanDeactivateTool(ActiveTool))
			{
				ActiveTool.ToggleTool();
				ClearTool();
			}
		}

		public void CloseActiveToolFromShortcut()
		{
			if (IsToolActive)
			{
				DisableActiveTool();
			}
		}

		public bool TryDisableActiveToolForModeChange()
		{
			if (!IsToolActive)
			{
				return true;
			}
			if (ActiveTool == null)
			{
				if (!TryDisableOtherTools(null))
				{
					return false;
				}
				ClearTool();
				return true;
			}
			if (!CanDeactivateTool(ActiveTool))
			{
				return false;
			}
			ActiveTool.ToggleTool();
			ClearTool();
			return true;
		}

		private void ToggleMop(bool forceActive = false)
		{
			if (HasHeldItemBlockingToolActivation(_mop))
			{
				UtilityMethods.Log("HeldSlotElement is full!");
			}
			else if (_firstPersonCharacter.IsSwimming())
			{
				UtilityMethods.Log("Cant use mop in water!");
			}
			else if (CanToggleTool(_mop) && TryDisableOtherTools(_mop))
			{
				_mop.ToggleTool(forceActive);
				HandleHeldStatus();
				SetActiveTool(_mop);
			}
		}

		public void ToggleCompass(bool forceActive = false)
		{
			if (base.IsOwner && (!forceActive || !(ActiveTool is AlienCompass)))
			{
				if (HasHeldItemBlockingToolActivation(_compass))
				{
					UtilityMethods.Log("HeldSlotElement is full!");
				}
				else if (CanToggleTool(_compass) && TryDisableOtherTools(_compass))
				{
					_compass.ToggleTool(forceActive);
					HandleHeldStatus();
					SetActiveTool(_compass);
					QuestReferenceHub.Instance.QuestComplete(QuestId.EquipCompass);
				}
			}
		}

		private void ToggleDivingEquipment(bool forceActive = false)
		{
			if (!forceActive && _firstPersonCharacter.IsSwimming())
			{
				UtilityMethods.Log("Cant disable in water!");
			}
			else
			{
				_divingEquipment.ToggleTool(forceActive);
			}
		}

		public void ToggleHarpoon(bool forceActive = false)
		{
			if (HasHeldItemBlockingToolActivation(_harpoon))
			{
				UtilityMethods.Log("HeldSlotElement is full!");
			}
			else if (CanToggleTool(_harpoon) && TryDisableOtherTools(_harpoon))
			{
				_harpoon.ToggleTool(forceActive);
				HandleHeldStatus();
				SetActiveTool(_harpoon);
				QuestReferenceHub.Instance.QuestComplete(QuestId.EquipTheHarpoon);
			}
		}

        public bool TryEquipMigratedFishing()=>TryEquipFishingBackend(true);
        public bool TryEquipFishingBackend(bool migrated){
            if(!IsOwner||!_fishingRod||!_fishingRod.IsUnlocked||!CanStartControlAction(PlayerControlAction.ActivateTool))return false;
            if(ActiveTool==_fishingRod&&_fishingRod.ToolStatus&&_fishingRod.UsesMigratedFishing==migrated)return true;
            if(ActiveTool==_fishingRod)_fishingRod.CancelForBackendChange();
            if(!TryDisableActiveToolForModeChange()||!_fishingRod.TrySelectBackend(migrated))return false;
            ToggleFishingRod(true);return ActiveTool==_fishingRod&&_fishingRod.ToolStatus;
        }
		private void ToggleFishingRod(bool forceActive = false)
		{
			if (HasHeldItemBlockingToolActivation(_fishingRod))
			{
				UtilityMethods.Log("HeldSlotElement is full!");
			}
			else if (_firstPersonCharacter.IsSwimming())
			{
				UtilityMethods.Log("Cant use rod in water!");
			}
			else if (CanToggleTool(_fishingRod) && TryDisableOtherTools(_fishingRod))
			{
				_fishingRod.ToggleTool(forceActive);
				HandleHeldStatus();
				SetActiveTool(_fishingRod);
				if (_fishingRod.ToolStatus)
				{
					QuestReferenceHub.Instance.QuestComplete(QuestId.EquipTheFishingRod);
				}
			}
		}

		private void ToggleSeaScooter(bool forceActive = false)
		{
			if (HasHeldItemBlockingToolActivation(_seaScooter))
			{
				UtilityMethods.Log("HeldSlotElement is full!");
			}
			else if (CanToggleTool(_seaScooter) && TryDisableOtherTools(_seaScooter))
			{
				_seaScooter.ToggleTool(forceActive);
				HandleHeldStatus();
				SetActiveTool(_seaScooter);
				QuestReferenceHub.Instance.QuestComplete(QuestId.EquipTheSeaScooter);
			}
		}

		public bool TryAutoEquipPoseidonTridentReward()
		{
			if (!base.IsOwner || !base.IsSpawned || _relay == null || !_relay.IsSpawned || !IsLocalGameplayPreparationReady || _poseidonTrident == null || !_poseidonTrident.IsUnlocked)
			{
				return false;
			}
			if (_poseidonTrident.ToolStatus)
			{
				return true;
			}
			if (_divingEquipment.IsKnocked || !CanChangeTool || !CanDisableActiveTool || !CanStartControlAction(PlayerControlAction.ActivateTool) || Singleton<QuickSlotHandler>.Instance == null || HasHeldItemBlockingToolActivation(_poseidonTrident))
			{
				return false;
			}
			TogglePoseidonTrident(forceActive: true);
			return _poseidonTrident.ToolStatus;
		}

		public void TogglePoseidonTrident(bool forceActive = false)
		{
			if (_poseidonTrident == null || !_poseidonTrident.IsUnlocked)
			{
				return;
			}
			if (HasHeldItemBlockingToolActivation(_poseidonTrident))
			{
				UtilityMethods.Log("HeldSlotElement is full!");
			}
			else if (CanToggleTool(_poseidonTrident) && TryDisableOtherTools(_poseidonTrident))
			{
				_poseidonTrident.ToggleTool(forceActive);
				HandleHeldStatus();
				SetActiveTool(_poseidonTrident);
				if (_poseidonTrident.ToolStatus)
				{
					QuestReferenceHub.Instance?.CompleteIfActive(QuestId.EquipTheTrident);
				}
			}
		}

		private bool HasHeldItemBlockingToolActivation(BaseTool targetTool)
		{
			if (targetTool != null && !targetTool.ToolStatus && !IsToolActive && Singleton<QuickSlotHandler>.Instance.HeldSlotElement != null)
			{
				return !Singleton<QuickSlotHandler>.Instance.HeldSlotElement.IsEmpty;
			}
			return false;
		}

		private bool CanToggleTool(BaseTool targetTool)
		{
			if (targetTool != null && !targetTool.ToolStatus && !CanStartControlAction(PlayerControlAction.ActivateTool))
			{
				UtilityMethods.Log("Cant activate tool during another player control mode!");
				return false;
			}
			if (IsUsingEmoteArea)
			{
				UtilityMethods.Log("Cant change tool during emote area interaction!");
				return false;
			}
			if (IsTrapped)
			{
				UtilityMethods.Log("Cant change tool right now!");
				return false;
			}
			return CanDeactivateTool(targetTool);
		}

		private void ForceDisableToolsForStuckRecovery()
		{
			_harpoon?.ForceDisableTool();
			_fishingRod?.ForceDisableTool();
			_compass?.ForceDisableTool();
			_seaScooter?.ForceDisableTool();
			_poseidonTrident?.ForceDisableTool();
			ClearTool();
		}

		private bool CanDeactivateTool(BaseTool tool)
		{
			if (tool == null || !tool.ToolStatus)
			{
				return true;
			}
			if (tool == _harpoon && _harpoon.IsChallenge)
			{
				UtilityMethods.Log("Cant change tool during harpoon challenge!");
				return false;
			}
			if (tool == _fishingRod && _fishingRod.IsFishingActive)
			{
				UtilityMethods.Log("Cant change tool while fishing!");
				return false;
			}
			return true;
		}

		private bool TryDisableOtherTools(BaseTool targetTool)
		{
			if (TryDisableTool(_mop, targetTool) && TryDisableTool(_harpoon, targetTool) && TryDisableTool(_fishingRod, targetTool) && TryDisableTool(_compass, targetTool) && TryDisableTool(_seaScooter, targetTool))
			{
				return TryDisableTool(_poseidonTrident, targetTool);
			}
			return false;
		}

		private bool TryDisableTool(BaseTool tool, BaseTool targetTool)
		{
			if (tool == null || tool == targetTool || !tool.ToolStatus)
			{
				return true;
			}
			if (!CanDeactivateTool(tool))
			{
				return false;
			}
			tool.ToggleTool();
			if (ActiveTool == tool)
			{
				ClearTool();
			}
			return true;
		}

		private void SetActiveTool(BaseTool tool)
		{
			if (tool.ToolStatus)
			{
				Singleton<QuickSlotHandler>.Instance.HeldSlotElement.Set(tool.LocalizedName, tool.Icon);
			}
			else
			{
				Singleton<QuickSlotHandler>.Instance.HeldSlotElement.Clear();
			}
			ActiveTool = (tool.ToolStatus ? tool : null);
			if (ActiveTool != null)
			{
				LastToolAction = GetToolToggleAction(ActiveTool);
			}
			RefreshCompassWorldState();
		}

		private Action<bool> GetToolToggleAction(BaseTool tool)
		{
			if (tool == _mop)
			{
				return ToggleMop;
			}
			if (tool == _harpoon)
			{
				return ToggleHarpoon;
			}
			if (tool == _fishingRod)
			{
				return ToggleFishingRod;
			}
			if (tool == _compass)
			{
				return ToggleCompass;
			}
			if (tool == _seaScooter)
			{
				return ToggleSeaScooter;
			}
			if (tool == _poseidonTrident)
			{
				return TogglePoseidonTrident;
			}
			if (tool == _divingEquipment)
			{
				return ToggleDivingEquipment;
			}
			return LastToolAction;
		}

		private void ClearTool()
		{
			Singleton<QuickSlotHandler>.Instance.HeldSlotElement.Clear();
			ActiveTool = null;
			RefreshCompassWorldState();
		}

		public void ClearToolIfActive(BaseTool tool)
		{
			if (!(tool == null) && !(ActiveTool != tool))
			{
				Singleton<QuickSlotHandler>.Instance.HeldSlotElement.Clear();
				ActiveTool = null;
				RefreshCompassWorldState();
			}
		}

		private void RefreshCompassWorldState()
		{
			if (base.IsOwner)
			{
				RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
				if (!(restaurantManager == null))
				{
					restaurantManager.RefreshCompassTargetForLocalPlayer();
					restaurantManager.RefreshLighthouseCompassDoorForLocalPlayer();
				}
			}
		}

		private void HandleHeldStatus()
		{
			if (!(Interactor.CurrentHeld == null) && IsToolActive)
			{
				Singleton<QuickSlotHandler>.Instance.DeselectSlot();
			}
		}

		private void OnPhysicsVolumeChanged(PhysicsVolume newPhysicsVolume)
		{
			bool flag = newPhysicsVolume != null && newPhysicsVolume.waterVolume;
			_seaScooter.CanMotorBeStarted = flag;
			if (!flag)
			{
				ResetCameraFov(0.5f);
				_divingEquipment.UnlatchAllPiranhas();
				if (base.IsOwner)
				{
					_divingEquipment.SetFlashlight(value: false);
					IsSwimming.Value = false;
					_firstPersonCharacterInput.OutUnderWater();
					Singleton<OceanFXManager>.Instance.SetSurface(isSwimming: false);
					SetRippleTrail(s: false);
					_divingEquipment.DepthUpdate?.Invoke(0f);
				}
				SetHolding(isHolding: false);
			}
			else if (newPhysicsVolume.waterVolume)
			{
				if (base.IsOwner)
				{
					ResetDiveAnimatorState();
				}
				float t = Mathf.InverseLerp(0f, 100f, _divingEquipment.CurrentDepth);
				float offset = Mathf.Lerp(10f, -5f, t);
				WaterVolumeEnterSFX(_firstPersonCharacter.speed > speedThreshold);
				SetTransientCameraFovOffset(offset, 0.5f);
				_divingEquipment.SetSurface(GetSurfaceY(newPhysicsVolume.boxCollider));
				if (_fishingRod.ToolStatus)
				{
					_fishingRod.HandleOwnerWaterVolumeEntered();
				}
				if (_mop.ToolStatus)
				{
					_mop.ToggleTool();
				}
				ToggleDivingEquipment(forceActive: true);
				Interactor.SetStateHolding(startHold: true);
				if (base.IsOwner)
				{
					IsSwimming.Value = true;
					Singleton<OceanFXManager>.Instance.SetSurface(isSwimming: true);
				}
			}
		}

		public void SetUnderwaterState(bool isUnderwater)
		{
			if (base.IsOwner && base.IsSpawned && _isUnderwater.Value != isUnderwater)
			{
				_isUnderwater.Value = isUnderwater;
			}
		}

		private void OnUnderwaterStateChanged(bool previousValue, bool currentValue)
		{
			ApplyUnderwaterState(currentValue);
		}

		private void ApplyUnderwaterState(bool isUnderwater)
		{
			if (_nfgoPlayer != null && Singleton<PlayersManager>.Instance != null)
			{
				if (isUnderwater)
				{
					Singleton<PlayersManager>.Instance.EnterUnderwater(_nfgoPlayer.PlayerId);
				}
				else
				{
					Singleton<PlayersManager>.Instance.ExitUnderwater(_nfgoPlayer.PlayerId);
				}
			}
			UnderwaterStateChanged?.Invoke(isUnderwater);
			UtilityMethods.Log($"[PlayerMapState] ClientId={base.OwnerClientId} Underwater={isUnderwater}");
		}

		[Rpc(SendTo.Server)]
		public void ServerEnterWaterVolumeRpc(bool isBig)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3644048322u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in isBig, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3644048322u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientEnterWaterVolumeRpc(isBig);
			}
		}

		[Rpc(SendTo.Everyone)]
		public void ClientEnterWaterVolumeRpc(bool isBig)
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
				FastBufferWriter bufferWriter = __beginSendRpc(4008138646u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in isBig, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 4008138646u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!base.IsOwner)
				{
					WaterVolumeEnterSFX(isBig);
				}
			}
		}

		private void WaterVolumeEnterSFX(bool isBig)
		{
			if (base.IsOwner)
			{
				ServerEnterWaterVolumeRpc(_firstPersonCharacter.speed > speedThreshold);
			}
			PlayEnterRippleImpact(isBig);
			_waterVolumeEnter.transform.position = base.transform.position;
			_waterVolumeEnter.generator = (isBig ? (_waterJumpBigGenerator as IAudioGenerator) : (_waterJumpSmallGenerator as IAudioGenerator));
			_waterVolumeEnter.Play();
			_oceanEnterTPSVFX.Play();
		}

		public void Harpooned(NetworkObject whoHarpooned, float pullForce, float arriveDistance, float safetyMaxSeconds, bool allowLeaveGround)
		{
			if (!(whoHarpooned == null))
			{
				ServerHarpoonedRpc(whoHarpooned.ToReference(), pullForce, arriveDistance, safetyMaxSeconds, allowLeaveGround);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerHarpoonedRpc(NetworkObjectReference clientRef, float pullForce, float arriveDistance, float safetyMaxSeconds, bool allowLeaveGround)
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
				FastBufferWriter bufferWriter = __beginSendRpc(535816777u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in clientRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in pullForce, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in arriveDistance, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in safetyMaxSeconds, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in allowLeaveGround, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 535816777u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!(_interactor != null) || !_interactor.IsUsing)
				{
					ClientHarpoonedRpc(clientRef, pullForce, arriveDistance, safetyMaxSeconds, allowLeaveGround);
				}
			}
		}

		[Rpc(SendTo.Owner)]
		private void ClientHarpoonedRpc(NetworkObjectReference clientRef, float pullForce, float arriveDistance, float safetyMaxSeconds, bool allowLeaveGround)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3897221701u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in clientRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in pullForce, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in arriveDistance, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in safetyMaxSeconds, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in allowLeaveGround, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3897221701u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (clientRef.TryGetComponent<PPG_Player>(out var component) && !(_firstPersonCharacter == null))
				{
					_firstPersonCharacter.DragTowardUntilArrive(component.transform, pullForce, arriveDistance, safetyMaxSeconds, allowLeaveGround);
				}
			}
		}

		public void StopHarpoonedPull()
		{
			ServerStopHarpoonedPullRpc();
		}

		public void StopHarpoonedPullLocal()
		{
			if (!(_firstPersonCharacter == null))
			{
				_firstPersonCharacter.StopDrag();
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerStopHarpoonedPullRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(2245479343u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2245479343u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientStopHarpoonedPullRpc();
			}
		}

		[Rpc(SendTo.Owner)]
		private void ClientStopHarpoonedPullRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1332040085u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1332040085u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				StopHarpoonedPullLocal();
			}
		}

		private float GetSurfaceY(BoxCollider box)
		{
			return box.bounds.max.y;
		}

		public void SetCompassTarget(Transform target, byte index)
		{
			_compass.SetTarget(target, index);
		}

		public void EquipTool(ToolType tool, byte equipLevel)
		{
			switch (tool)
			{
			case ToolType.DivingEquipment:
				_divingEquipmentEquipedLevel = equipLevel;
				_divingEquipment.LoadLevel(new ToolDataNet
				{
					DivingEquipmentLevel = equipLevel
				});
				break;
			case ToolType.Harpoon:
				_harpoonEquipedLevel = equipLevel;
				_harpoon.LoadLevel(new ToolDataNet
				{
					HarpoonUnlocked = true,
					HarpoonLevel = equipLevel
				});
				break;
			case ToolType.SeaScooter:
				_scooterEquipedLevel = equipLevel;
				_seaScooter.LoadLevel(new ToolDataNet
				{
					ScooterUnlocked = true,
					ScooterLevel = equipLevel
				});
				break;
			case ToolType.FishingRod:
				_fishingRodEquipedLevel = equipLevel;
				_fishingRod.LoadLevel(new ToolDataNet
				{
					FishingRodUnlocked = true,
					FishingRodLevel = equipLevel
				});
				break;
			}
			if (base.IsOwner)
			{
				ClientEquipRpc((byte)tool, equipLevel);
			}
		}

		public byte GetEquipedToolLevel(ToolType tool)
		{
			return tool switch
			{
				ToolType.DivingEquipment => _divingEquipmentEquipedLevel, 
				ToolType.Harpoon => _harpoonEquipedLevel, 
				ToolType.FishingRod => _fishingRodEquipedLevel, 
				ToolType.SeaScooter => _scooterEquipedLevel, 
				_ => 0, 
			};
		}

		[Rpc(SendTo.NotOwner)]
		private void ClientEquipRpc(byte tool, byte equipLevel)
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
				FastBufferWriter bufferWriter = __beginSendRpc(689450816u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in tool, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in equipLevel, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 689450816u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				EquipTool((ToolType)tool, equipLevel);
			}
		}

		private void RegisterWheelEvents()
		{
			if (base.IsOwner)
			{
				PPG_Wheel toolWheel = Singleton<PPG_WheelManager>.Instance.ToolWheel;
				toolWheel.SelectSlice = (Action<IWheelSlice>)Delegate.Combine(toolWheel.SelectSlice, new Action<IWheelSlice>(OnToolSelect));
				PPG_Wheel emoteWheel = Singleton<PPG_WheelManager>.Instance.EmoteWheel;
				emoteWheel.SelectSlice = (Action<IWheelSlice>)Delegate.Combine(emoteWheel.SelectSlice, new Action<IWheelSlice>(OnEmoteSelect));
				if (Singleton<PPG_WheelManager>.Instance.ExpressionWheel != null)
				{
					PPG_Wheel expressionWheel = Singleton<PPG_WheelManager>.Instance.ExpressionWheel;
					expressionWheel.SelectSlice = (Action<IWheelSlice>)Delegate.Combine(expressionWheel.SelectSlice, new Action<IWheelSlice>(OnExpressionSelect));
				}
				Singleton<PPG_WheelManager>.Instance.ShowWheel += OnShowWheel;
				Singleton<PPG_WheelManager>.Instance.HideWheel += OnHideWheel;
				Singleton<InputManager>.Instance.InputHandler.Any += OnAnyKey;
			}
		}

		private void UnRegisterWheelEvents()
		{
			if (base.IsOwner)
			{
				if (Singleton<PPG_WheelManager>.TryGetInstance(out var wheelManager))
				{
					PPG_Wheel toolWheel = wheelManager.ToolWheel;
					if (toolWheel != null) toolWheel.SelectSlice = (Action<IWheelSlice>)Delegate.Remove(toolWheel.SelectSlice, new Action<IWheelSlice>(OnToolSelect));
					PPG_Wheel emoteWheel = wheelManager.EmoteWheel;
					if (emoteWheel != null) emoteWheel.SelectSlice = (Action<IWheelSlice>)Delegate.Remove(emoteWheel.SelectSlice, new Action<IWheelSlice>(OnEmoteSelect));
					if (wheelManager.ExpressionWheel != null)
					{
						PPG_Wheel expressionWheel = wheelManager.ExpressionWheel;
						expressionWheel.SelectSlice = (Action<IWheelSlice>)Delegate.Remove(expressionWheel.SelectSlice, new Action<IWheelSlice>(OnExpressionSelect));
					}
					wheelManager.ShowWheel -= OnShowWheel;
					wheelManager.HideWheel -= OnHideWheel;
				}
				if (Singleton<InputManager>.TryGetInstance(out var inputManager) && inputManager.InputHandler != null)
					inputManager.InputHandler.Any -= OnAnyKey;
			}
		}

		public void OnToolSelect(IWheelSlice data)
		{
            if(SushiAndFish.FishingIntegration.FishingMigration.OpenFishingEquipment(this,data)){LastToolAction=ToggleFishingRod;return;}
			if (data == null || !(data is BaseToolData baseToolData) || IsUsingEmoteArea)
			{
				return;
			}
			UtilityMethods.Log("[PPG_Player_Wheel] Selected Tool:" + baseToolData.Name);
			if (!(baseToolData is DivingEquipmentData))
			{
				if (!(baseToolData is HarpoonData))
				{
					if (!(baseToolData is FishingRodData))
					{
						if (!(baseToolData is CompassData))
						{
							if (!(baseToolData is SeaScooterData))
							{
								if (baseToolData is PoseidonTridentData)
								{
									LastToolAction = TogglePoseidonTrident;
								}
							}
							else
							{
								LastToolAction = ToggleSeaScooter;
							}
						}
						else
						{
							LastToolAction = ToggleCompass;
						}
					}
					else
					{
						LastToolAction = ToggleFishingRod;
					}
				}
				else
				{
					LastToolAction = ToggleHarpoon;
				}
			}
			else
			{
				LastToolAction = ToggleDivingEquipment;
			}
			LastToolAction?.Invoke(obj: false);
			Singleton<PPG_WheelManager>.Instance.DeactiveWheel();
		}

		public void OnEmoteSelect(IWheelSlice data)
		{
			if (data != null && data is EmoteSO emoteSO)
			{
				UtilityMethods.Log("[PPG_Player_Wheel] Selected Emote:" + emoteSO.Name);
				_lastEmoteSO = emoteSO;
				LastEmoteAction = StartEmote;
				LastEmoteAction?.Invoke();
			}
		}

		public void OnExpressionSelect(IWheelSlice data)
		{
			if (data != null && data is EmoteSO emoteSO)
			{
				UtilityMethods.Log("[PPG_Player_Wheel] Selected Expression:" + emoteSO.Name);
				ApplyManualExpression(emoteSO.AnimationKey);
				StartExpressionCamera();
				Singleton<PPG_WheelManager>.Instance.DeactiveWheel();
			}
		}

		private void ApplyManualExpression(int expressionKey)
		{
			if (!base.IsOwner || !base.IsSpawned)
			{
				return;
			}
			if (expressionKey < 0 || expressionKey > 7)
			{
				UtilityMethods.Log($"[PPG_Player_Expression] Invalid expression key: {expressionKey}", LogType.Warning);
				return;
			}
			_manualExpressionKey.Value = (byte)expressionKey;
			if (_tpsAnimator != null)
			{
				_tpsAnimator.SetFloat(EXPRESSION, 0f);
			}
			ApplyCurrentManualExpression();
		}

		private void OnManualExpressionKeyChanged(byte previousValue, byte newValue)
		{
			ApplyCurrentManualExpression();
		}

		private void ApplyCurrentManualExpression()
		{
			if (_headRenderer == null)
			{
				UtilityMethods.Log("[PPG_Player_Expression] Cannot apply expression. Head renderer is null.", LogType.Warning);
				return;
			}
			ClearManualExpressionBlendShapes();
			int value = _manualExpressionKey.Value;
			if (value == 0)
			{
				return;
			}
			if (!TryGetManualExpression(value, out var expression))
			{
				UtilityMethods.Log($"[PPG_Player_Expression] Unknown expression key: {value}", LogType.Warning);
				return;
			}
			int num = 0;
			for (int i = 0; i < expression.Length; i++)
			{
				if (SetExpressionBlendShape(expression[i].Name, expression[i].Value))
				{
					num++;
				}
			}
			if (num == 0)
			{
				UtilityMethods.Log("[PPG_Player_Expression] Expression could not be applied. " + $"Renderer:{_headRenderer.name} Mesh:{_headRenderer.sharedMesh.name} Key:{value}", LogType.Warning);
			}
		}

		private void ClearManualExpressionBlendShapes()
		{
			for (int i = 0; i < ManualExpressionBlendShapeNames.Length; i++)
			{
				SetExpressionBlendShape(ManualExpressionBlendShapeNames[i], 0f);
			}
		}

		private bool SetExpressionBlendShape(string blendShapeName, float value)
		{
			if (_headRenderer == null || _headRenderer.sharedMesh == null)
			{
				return false;
			}
			int num = FindBlendShapeIndex(_headRenderer, blendShapeName);
			if (num < 0)
			{
				return false;
			}
			_headRenderer.SetBlendShapeWeight(num, Mathf.Clamp(value, 0f, 100f));
			return true;
		}

		private static bool TryGetManualExpression(int expressionKey, out ExpressionBlendShape[] expression)
		{
			switch (expressionKey)
			{
			case 1:
				expression = SmileExpression;
				return true;
			case 2:
				expression = AngryExpression;
				return true;
			case 3:
				expression = SadExpression;
				return true;
			case 4:
				expression = SurprisedExpression;
				return true;
			case 5:
				expression = DeviousExpression;
				return true;
			case 6:
				expression = HappyExpression;
				return true;
			case 7:
				expression = EvilExpression;
				return true;
			default:
				expression = null;
				return false;
			}
		}

		private void StartExpressionCamera()
		{
			if (!_expressionCameraActive && !_emoteActive && !_staticEmoteActive && !_swingEmoteActive && CanStartControlAction(PlayerControlAction.StartEmote) && TryDisableActiveToolForModeChange())
			{
				PauseConstraints();
				PermaPause();
				SwitchToTpsCam();
				_expressionCameraActive = true;
			}
		}

		private void EndExpressionCamera()
		{
			if (_expressionCameraActive)
			{
				_expressionCameraActive = false;
				ResumeConstraintsIfAllowed();
				TryRestoreCharacterControlAfterModeExit();
				SwitchToFpsCam();
			}
		}

		private void StartEmote()
		{
			if (_lastEmoteSO == null || !CanStartControlAction(PlayerControlAction.StartEmote) || !TryDisableActiveToolForModeChange())
			{
				return;
			}
			PauseConstraints();
			PermaPause();
			if (_tpsAnimator != null)
			{
				_tpsAnimator.SetFloat(EMOTE_TYPE, _lastEmoteSO.AnimationKey);
			}
			if (_tpsAnimator != null)
			{
				_tpsAnimator.SetBool(IS_EMOTE, value: false);
			}
			uint emoteSequence = ++_emoteSequence;
			Singleton<DelayHelper>.Instance.DelayActionUntilEndOfFrame(() =>
			{
				if (_tpsAnimator != null && _emoteActive && emoteSequence == _emoteSequence)
				{
					_tpsAnimator.SetBool(IS_EMOTE, value: true);
				}
			});
			SwitchToTpsCam();
			Singleton<PPG_WheelManager>.Instance.DeactiveWheel();
			_emoteActive = true;
		}

		public void EndEmote()
		{
			if (_emoteActive)
			{
				_emoteSequence++;
				if (_tpsAnimator != null)
				{
					_tpsAnimator.SetBool(IS_EMOTE, value: false);
				}
				_emoteActive = false;
				ResumeConstraintsIfAllowed();
				TryRestoreCharacterControlAfterModeExit();
				SwitchToFpsCam();
			}
		}

		public void OnShowWheel()
		{
			HideCursor();
			PauseLook();
			CursorHelper.CursorVisible = true;
		}

		public void OnHideWheel()
		{
			ShowCursor();
			ResumeLook();
			CursorHelper.CursorVisible = false;
		}

		private void OnAnyKey(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Performed && !_swingEmoteActive && !IsAllowedActiveAreaOpenEmotesInputThisFrame() && !IsEmoteWheelPageToggleInputThisFrame())
			{
				EndEmote();
				EndStaticEmote();
				EndExpressionCamera();
			}
		}

		private bool IsAllowedActiveAreaOpenEmotesInputThisFrame()
		{
			if (!IsUsingEmoteArea || !AllowOpenEmotesInputInActiveArea)
			{
				return false;
			}
			if (Keyboard.current != null)
			{
				return Keyboard.current.tKey.wasPressedThisFrame;
			}
			return false;
		}

		private bool IsEmoteWheelPageToggleInputThisFrame()
		{
			if (Singleton<PPG_WheelManager>.Instance == null || !Singleton<PPG_WheelManager>.Instance.IsEmotePageWheelOpen)
			{
				return false;
			}
			if (Mouse.current != null)
			{
				return Mouse.current.rightButton.wasPressedThisFrame;
			}
			return false;
		}

		protected override void __initializeVariables()
		{
			if (SteamID == null)
			{
				throw new Exception("PPG_Player.SteamID cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			SteamID.Initialize(this);
			__nameNetworkVariable(SteamID, "SteamID");
			NetworkVariableFields.Add(SteamID);
			if (PlayerName == null)
			{
				throw new Exception("PPG_Player.PlayerName cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			PlayerName.Initialize(this);
			__nameNetworkVariable(PlayerName, "PlayerName");
			NetworkVariableFields.Add(PlayerName);
			if (ColorIndex == null)
			{
				throw new Exception("PPG_Player.ColorIndex cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			ColorIndex.Initialize(this);
			__nameNetworkVariable(ColorIndex, "ColorIndex");
			NetworkVariableFields.Add(ColorIndex);
			if (ZoneName == null)
			{
				throw new Exception("PPG_Player.ZoneName cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			ZoneName.Initialize(this);
			__nameNetworkVariable(ZoneName, "ZoneName");
			NetworkVariableFields.Add(ZoneName);
			if (InCage == null)
			{
				throw new Exception("PPG_Player.InCage cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			InCage.Initialize(this);
			__nameNetworkVariable(InCage, "InCage");
			NetworkVariableFields.Add(InCage);
			if (ConstraintStatus == null)
			{
				throw new Exception("PPG_Player.ConstraintStatus cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			ConstraintStatus.Initialize(this);
			__nameNetworkVariable(ConstraintStatus, "ConstraintStatus");
			NetworkVariableFields.Add(ConstraintStatus);
			if (IsSwimming == null)
			{
				throw new Exception("PPG_Player.IsSwimming cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			IsSwimming.Initialize(this);
			__nameNetworkVariable(IsSwimming, "IsSwimming");
			NetworkVariableFields.Add(IsSwimming);
			if (IsTrappedState == null)
			{
				throw new Exception("PPG_Player.IsTrappedState cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			IsTrappedState.Initialize(this);
			__nameNetworkVariable(IsTrappedState, "IsTrappedState");
			NetworkVariableFields.Add(IsTrappedState);
			if (_isUnderwater == null)
			{
				throw new Exception("PPG_Player._isUnderwater cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isUnderwater.Initialize(this);
			__nameNetworkVariable(_isUnderwater, "_isUnderwater");
			NetworkVariableFields.Add(_isUnderwater);
			if (_manualExpressionKey == null)
			{
				throw new Exception("PPG_Player._manualExpressionKey cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_manualExpressionKey.Initialize(this);
			__nameNetworkVariable(_manualExpressionKey, "_manualExpressionKey");
			NetworkVariableFields.Add(_manualExpressionKey);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(373888146u, __rpc_handler_373888146, "ServerSubmitCustomizationRpc", RpcInvokePermission.Everyone);
			__registerRpc(2135905389u, __rpc_handler_2135905389, "ServerRequestCustomizationSnapshotRpc", RpcInvokePermission.Everyone);
			__registerRpc(3023977014u, __rpc_handler_3023977014, "ClientApplyCustomizationRpc", RpcInvokePermission.Everyone);
			__registerRpc(2197951382u, __rpc_handler_2197951382, "ClientApplyCustomizationSnapshotRpc", RpcInvokePermission.Everyone);
			__registerRpc(2025415500u, __rpc_handler_2025415500, "ServerRequestStuckRecoveryRpc", RpcInvokePermission.Everyone);
			__registerRpc(1919755016u, __rpc_handler_1919755016, "ClientResetForEndDayRpc", RpcInvokePermission.Everyone);
			__registerRpc(2152064623u, __rpc_handler_2152064623, "ServerKickMeRpc", RpcInvokePermission.Everyone);
			__registerRpc(121680056u, __rpc_handler_121680056, "ServerReportFishSlipRpc", RpcInvokePermission.Everyone);
			__registerRpc(4248048454u, __rpc_handler_4248048454, "ClientSharkAttackRpc", RpcInvokePermission.Everyone);
			__registerRpc(2946482843u, __rpc_handler_2946482843, "ClientElectrocutePlayerRpc", RpcInvokePermission.Everyone);
			__registerRpc(2049903745u, __rpc_handler_2049903745, "ClientSetOctoTrapAnimRpc", RpcInvokePermission.Everyone);
			__registerRpc(2289590177u, __rpc_handler_2289590177, "ServerApplyStaticEmoteExitPoseRpc", RpcInvokePermission.Everyone);
			__registerRpc(3223578054u, __rpc_handler_3223578054, "ClientApplyStaticEmoteExitPoseRpc", RpcInvokePermission.Everyone);
			__registerRpc(4160951381u, __rpc_handler_4160951381, "ClientTpPlayerRpc", RpcInvokePermission.Everyone);
			__registerRpc(2062431952u, __rpc_handler_2062431952, "ClientSetNetworkTransformRpc", RpcInvokePermission.Everyone);
			__registerRpc(3515567780u, __rpc_handler_3515567780, "ClientPlayElectricitySfxRpc", RpcInvokePermission.Everyone);
			__registerRpc(536206294u, __rpc_handler_536206294, "ClientRippleTrailRpc", RpcInvokePermission.Everyone);
			__registerRpc(3988887507u, __rpc_handler_3988887507, "ClientOwnerPlaySquidVFXRpc", RpcInvokePermission.Everyone);
			__registerRpc(1760465321u, __rpc_handler_1760465321, "ClientOwnerStopSquidVFXRpc", RpcInvokePermission.Everyone);
			__registerRpc(2125506552u, __rpc_handler_2125506552, "ClientPlaySquidVFXRpc", RpcInvokePermission.Everyone);
			__registerRpc(1271757271u, __rpc_handler_1271757271, "ClientStopSquidVFXRpc", RpcInvokePermission.Everyone);
			__registerRpc(2445840379u, __rpc_handler_2445840379, "ClientOwnerPlaySharkChaseSFXRpc", RpcInvokePermission.Everyone);
			__registerRpc(3271259337u, __rpc_handler_3271259337, "ClientSetSharkTargetScreenVfxRpc", RpcInvokePermission.Everyone);
			__registerRpc(2156617051u, __rpc_handler_2156617051, "ServerClearSeaLayerOccupancyAfterTeleportRpc", RpcInvokePermission.Everyone);
			__registerRpc(3316237444u, __rpc_handler_3316237444, "ClientPauseCharacterRpc", RpcInvokePermission.Everyone);
			__registerRpc(2540472773u, __rpc_handler_2540472773, "ClientResumeCharacterRpc", RpcInvokePermission.Everyone);
			__registerRpc(1280526477u, __rpc_handler_1280526477, "ClientAssignParentConstraintSeatRpc", RpcInvokePermission.Server);
			__registerRpc(3282599242u, __rpc_handler_3282599242, "ClientDeAssignParentConstraintRpc", RpcInvokePermission.Everyone);
			__registerRpc(2694631059u, __rpc_handler_2694631059, "ClientReleaseBossGrabRpc", RpcInvokePermission.Server);
			__registerRpc(2043642383u, __rpc_handler_2043642383, "ServerSubmitSharkDragEscapePressRpc", RpcInvokePermission.Everyone);
			__registerRpc(1539903271u, __rpc_handler_1539903271, "ClientEndSurfRpc", RpcInvokePermission.Server);
			__registerRpc(2958814783u, __rpc_handler_2958814783, "ServerCancelSurfRpc", RpcInvokePermission.Everyone);
			__registerRpc(28955413u, __rpc_handler_28955413, "ClientSyncTabletStatusRpc", RpcInvokePermission.Everyone);
			__registerRpc(3644048322u, __rpc_handler_3644048322, "ServerEnterWaterVolumeRpc", RpcInvokePermission.Everyone);
			__registerRpc(4008138646u, __rpc_handler_4008138646, "ClientEnterWaterVolumeRpc", RpcInvokePermission.Everyone);
			__registerRpc(535816777u, __rpc_handler_535816777, "ServerHarpoonedRpc", RpcInvokePermission.Everyone);
			__registerRpc(3897221701u, __rpc_handler_3897221701, "ClientHarpoonedRpc", RpcInvokePermission.Everyone);
			__registerRpc(2245479343u, __rpc_handler_2245479343, "ServerStopHarpoonedPullRpc", RpcInvokePermission.Everyone);
			__registerRpc(1332040085u, __rpc_handler_1332040085, "ClientStopHarpoonedPullRpc", RpcInvokePermission.Everyone);
			__registerRpc(689450816u, __rpc_handler_689450816, "ClientEquipRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_373888146(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				byte[] value2 = null;
				if (value)
				{
					reader.ReadValueSafe(out value2, default(FastBufferWriter.ForPrimitives));
				}
				RpcParams ext = rpcParams.Ext;
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ServerSubmitCustomizationRpc(value2, ext);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2135905389(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ServerRequestCustomizationSnapshotRpc(ext);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3023977014(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				byte[] value3 = null;
				if (value2)
				{
					reader.ReadValueSafe(out value3, default(FastBufferWriter.ForPrimitives));
				}
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientApplyCustomizationRpc(value, value3);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2197951382(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				byte[] value3 = null;
				if (value2)
				{
					reader.ReadValueSafe(out value3, default(FastBufferWriter.ForPrimitives));
				}
				RpcParams ext = rpcParams.Ext;
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientApplyCustomizationSnapshotRpc(value, value3, ext);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2025415500(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ServerRequestStuckRecoveryRpc(ext);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1919755016(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientResetForEndDayRpc();
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2152064623(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				RpcParams ext = rpcParams.Ext;
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ServerKickMeRpc(value, ext);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_121680056(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out Vector3 value2);
				RpcParams ext = rpcParams.Ext;
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ServerReportFishSlipRpc(value, value2, ext);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4248048454(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientSharkAttackRpc(value);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2946482843(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientElectrocutePlayerRpc(value, value2);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2049903745(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientSetOctoTrapAnimRpc(value);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2289590177(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Quaternion value2);
				RpcParams ext = rpcParams.Ext;
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ServerApplyStaticEmoteExitPoseRpc(value, value2, ext);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3223578054(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Quaternion value2);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientApplyStaticEmoteExitPoseRpc(value, value2);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4160951381(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Vector3 value2);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientTpPlayerRpc(value, value2);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2062431952(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientSetNetworkTransformRpc(value);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3515567780(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientPlayElectricitySfxRpc();
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_536206294(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientRippleTrailRpc(value);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3988887507(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientOwnerPlaySquidVFXRpc();
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1760465321(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientOwnerStopSquidVFXRpc();
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2125506552(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientPlaySquidVFXRpc();
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1271757271(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientStopSquidVFXRpc();
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2445840379(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientOwnerPlaySharkChaseSFXRpc();
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3271259337(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientSetSharkTargetScreenVfxRpc(value, value2);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2156617051(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ServerClearSeaLayerOccupancyAfterTeleportRpc(ext);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3316237444(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientPauseCharacterRpc();
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2540472773(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientResumeCharacterRpc();
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1280526477(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out uint value3);
				RpcParams ext = rpcParams.Ext;
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientAssignParentConstraintSeatRpc(value, value2, value3, ext);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3282599242(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientDeAssignParentConstraintRpc();
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2694631059(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out byte value3, default(FastBufferWriter.ForPrimitives));
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientReleaseBossGrabRpc(value, value2, value3);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2043642383(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				ByteUnpacker.ReadValueBitPacked(reader, out uint value2);
				RpcParams ext = rpcParams.Ext;
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ServerSubmitSharkDragEscapePressRpc(value, value2, ext);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1539903271(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				ByteUnpacker.ReadValueBitPacked(reader, out uint value2);
				reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientEndSurfRpc(value, value2, value3);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2958814783(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out SurfExitReason value, default(FastBufferWriter.ForEnums));
				RpcParams ext = rpcParams.Ext;
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ServerCancelSurfRpc(value, ext);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_28955413(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientSyncTabletStatusRpc(value);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3644048322(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ServerEnterWaterVolumeRpc(value);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4008138646(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientEnterWaterVolumeRpc(value);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_535816777(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value3, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value4, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value5, default(FastBufferWriter.ForPrimitives));
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ServerHarpoonedRpc(value, value2, value3, value4, value5);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3897221701(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value3, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value4, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value5, default(FastBufferWriter.ForPrimitives));
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientHarpoonedRpc(value, value2, value3, value4, value5);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2245479343(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ServerStopHarpoonedPullRpc();
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1332040085(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientStopHarpoonedPullRpc();
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_689450816(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PPG_Player)target).ClientEquipRpc(value, value2);
				((PPG_Player)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "PPG_Player";
		}
	}
}
