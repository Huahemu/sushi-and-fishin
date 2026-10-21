using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ECM2;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Data.Tools;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Core.Tutorials;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Wheel;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityServiceLocator;

namespace PaperPixelGames.Core.PlayerTools
{
	public sealed class FishingRod : BaseTool
	{
		private enum FishingSessionState : byte
		{
			Inactive = 0,
			Ready = 1,
			Charging = 2,
			Casting = 3,
			Waiting = 4,
			Challenge = 5,
			Success = 6,
			Failure = 7,
			CanceledFailure = 8,
			Returning = 9
		}

		private enum FishingCancelReason : byte
		{
			None = 0,
			ToolDisabled = 1,
			InvalidCast = 2,
			NoEligibleFish = 3,
			PlayerCanceled = 4,
			BobberPulled = 5,
			PlayerStateChanged = 6,
			NetworkDespawn = 7
		}

		private enum CastLocationState : byte
		{
			NoWaterVolume = 0,
			ShallowWater = 1,
			FishableWater = 2
		}

		private enum FishingRodSteerDirection : byte
		{
			None = 0,
			Left = 1,
			Right = 2,
			Back = 3
		}

		private enum ServerFishingPhase : byte
		{
			None = 0,
			Waiting = 1,
			Challenge = 2,
			Success = 3,
			Failed = 4,
			Delivery = 5,
			Collected = 6
		}

		private const float MaxLineTension = 1f;

		private const float MaxCatchProgress = 1f;

		private const float TiredProgressBonus = 1.35f;

		private const float PresentationSyncInterval = 0.125f;

		private const float SuccessCollectFallbackDelay = 1.5f;

		private const string DefaultRodBodyMaterialName = "rod06";

		private const string WaitingAlertHeader = "LINE IN THE WATER";

		private const string WaitingAlertDescription = "Waiting for a bite.";

		private const string FishingRodTutorialSeenKey = "StationTutorial.FishingRod.Seen";

		private string _activeWaitingAlertHeader;

		private string _activeWaitingAlertDescription;

		private readonly NetworkVariable<byte> _presentationState = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		private readonly NetworkVariable<byte> _presentationDirection = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		private readonly NetworkVariable<byte> _presentationSteerDirection = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		private readonly NetworkVariable<Vector3> _presentationCastPoint = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		private readonly NetworkVariable<uint> _presentationSessionId = new NetworkVariable<uint>(0u, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		private readonly NetworkVariable<Vector3> _presentationCastOrigin = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		private readonly NetworkVariable<double> _presentationCastStartTime = new NetworkVariable<double>(0.0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		private readonly NetworkVariable<float> _presentationCastDuration = new NetworkVariable<float>(0.65f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		private readonly NetworkVariable<float> _presentationCastArcHeight = new NetworkVariable<float>(2.5f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		private readonly NetworkVariable<float> _presentationTension = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		private readonly NetworkVariable<float> _presentationStrength = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		private readonly NetworkVariable<bool> _presentationReeling = new NetworkVariable<bool>(value: false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		private readonly NetworkVariable<bool> _presentationStruggling = new NetworkVariable<bool>(value: false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		private readonly NetworkVariable<FixedString64Bytes> _presentationCaughtFishId = new NetworkVariable<FixedString64Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		private readonly NetworkVariable<float> _presentationCaughtWeight = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

		[SerializeField]
		private FishingRodData _data;

		[SerializeField]
		private StationTutorialData _tutorialData;

		[SerializeField]
		private FishingRodUI _fishingRodUI;

		[SerializeField]
		private FishingRodBobber _bobberPrefab;

		[SerializeField]
		private ItemDatabaseSO _itemDatabaseSO;

		[SerializeField]
		[Min(0.1f)]
		private float _castChargeDuration = 2f;

		[SerializeField]
		[Min(1f)]
		private float _waterProbeHeight = 35f;

		[SerializeField]
		[Min(1f)]
		private float _waterProbeDistance = 80f;

		[Tooltip("Safety timeout used only if the fail animation cannot enter or exit correctly.")]
		[SerializeField]
		[Min(0f)]
		private float _resultDisplayDuration = 3f;

		[SerializeField]
		[Min(0.05f)]
		private float _challengeCancelHoldDuration = 0.5f;

		private FishingSessionState _state;

		private FishingRodFishDirection _fishDirection;

		private FishingRodSteerDirection _steerDirection;

		private FishingCancelReason _lastCancelReason;

		private FishingRodLevelData _currentLevel;

		private RodDifficultyMatchup _currentMatchup;

		private FishSO _currentFish;

		private FishingRodFishSettings _currentFishSettings;

		private OceanLayer _currentZone;

		private FishingLineVisual _fpsLineVisual;

		private FishingLineVisual _tpsLineVisual;

		private FishingRodVisual _fpsRodVisual;

		private FishingRodVisual _tpsRodVisual;

		private FishingRodReelIKController _reelIKController;

		private FishingRodHangingCableVisual _caughtFishCableVisual;

		private FishingRodHangingCableVisual _tpsCaughtFishCableVisual;

		private FishingRodHangingCableVisual _fpsIdleBobberCableVisual;

		private FishingRodHangingCableVisual _tpsIdleBobberCableVisual;

		private FishingRodBobber _activeBobber;

		private FishingRodBobber _presentationBobber;

		private uint _presentationBobberSessionId;

		private GameObject _caughtFishGraphic;

		private OwnedLoopHandle _reelLoopHandle;

		private OwnedLoopHandle _tensionLoopHandle;

		private Vector2 _counterInput;

		private Vector3 _castPoint;

		private Vector3 _fpsCastOrigin;

		private Vector3 _tpsCastOrigin;

		private Vector3 _currentLineDirectionOffset;

		private float _castElapsed;

		private float _castChargeElapsed;

		private float _currentCastCharge;

		private float _currentCastTravelDuration;

		private float _currentCastArcHeight;

		private double _castStartedAt;

		private float _waitRemaining;

		private float _directionRemaining;

		private float _counterHeldDuration;

		private float _catchProgress;

		private float _lineTension;

		private float _resultRemaining;

		private float _successCollectWaitElapsed;

		private float _presentationSyncRemaining;

		private float _challengeCancelHoldTimer;

		private float _caughtWeight;

		private float _currentPresentationStrength = 1f;

		private bool _isReeling;

		private bool _failureAnimationEntered;

		private bool _challengeCancelHoldActive;

		private bool _catchReadyToCollect;

		private bool _collectRequested;

		private bool _reelStruggleActive;

		private bool _fishingSwayActive;

		private bool _ownsMovementPause;

		private bool _ownsLookPause;

		private bool _inputRegistered;

		private CastLocationState _castLocationState;

		private int _pendingLevel = -1;

		private uint _sessionId;

		private uint _serverActiveBobberSessionId;

		private uint _lastResolvedSessionId;

		private Vector2 _stickCounterInput;

		private Vector2 _moveCounterInput;

		private float _collectBufferedUntil = -1f;

		private uint _pendingCollectionSession;

		private FixedString64Bytes _pendingCollectionFish;

		private bool _pendingCollectionFallback;

		private LineRenderer _castPreview;

		private Material _castPreviewMaterial;

		private float _nextPreviewProbe;

		private string _previewStatus = "";

		private readonly Vector3[] _previewPoints = new Vector3[33];

		private bool _guidedFirstCast;

		private bool _guidedFirstChallenge;

		private ServerFishingPhase _serverPhase;

		private uint _serverSessionId;

		private FishSO _serverFish;

		private FishingRodChallenge _serverChallenge;

		private float _serverWeight;

		private double _serverBiteAt;

		private double _serverInputAt;

		private double _serverSnapshotAt;

		private double _serverBobberAt;

		private Vector2 _serverCounter;

		private bool _serverReeling;

		private Vector3 _serverCastPoint;

		private DeadFish _serverReward;

		private NetworkObjectReference _serverRewardReference;

		private bool _serverRewardAllocated;

		private bool _serverHeldFallback;

		private byte _serverMilestone;

		private double _serverDeliveryDeadline;

		private bool _serverAcceptedCast;

		private float _counterProgress;

		private float _preparationRemaining;

		private int _completedCounters;

		private bool _failedWhileReeling;

		private float _nextInputSend;

		private float _collectRetryAt;

		public override byte Level
		{
			get
			{
				if (_player == null || !IsUnlocked)
				{
					return 0;
				}
				return _player.ToolsData.FishingRodLevel;
			}
		}

		public override bool IsUnlocked
		{
			get
			{
				if (_player != null)
				{
					return _player.ToolsData.FishingRodUnlocked;
				}
				return false;
			}
		}

		public bool IsFishingActive
		{
			get
			{
				FishingSessionState state = _state;
				return state == FishingSessionState.Charging || state == FishingSessionState.Casting || state == FishingSessionState.Waiting || state == FishingSessionState.Challenge || state == FishingSessionState.Success || state == FishingSessionState.Failure || state == FishingSessionState.CanceledFailure || state == FishingSessionState.Returning;
			}
		}

		public override string Name
		{
			get
			{
				if (!(_data != null))
				{
					return "Fishing Rod";
				}
				return _data.Name;
			}
		}

		public override Sprite Icon
		{
			get
			{
				if (!(_data != null))
				{
					return null;
				}
				return _data.Sprite;
			}
		}

		public override bool CapturesInteractInput => _state == FishingSessionState.Success;

		protected override bool ShouldShowCloseToolHint => !IsFishingActive;

		private Vector2 EffectiveCounterInput
		{
			get
			{
				if (!(_moveCounterInput.sqrMagnitude > 0.1f))
				{
					if (!(_stickCounterInput.sqrMagnitude > 0.04f))
					{
						return _counterInput;
					}
					return _stickCounterInput;
				}
				return _moveCounterInput;
			}
		}

		private string ReelBindingLabel => BindingLabel("LeftMouseMechanic");

		private string CollectBindingLabel => BindingLabel("Interact");

		private string CollectBindingIcon => BindingLabel("Interact", useIcon: true);

		private void Start()
		{
			if (_tpsGraphicParent != null && _player?.RigBones?.TpsRig?.RightHand != null)
			{
				_tpsGraphicParent.SetParent(_player.RigBones.TpsRig.RightHand);
				_tpsGraphicParent.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			}
			if (base.IsOwner && _fpsGraphicParent != null && _player?.RigBones?.FpsRig?.RightHand != null)
			{
				_fpsGraphicParent.SetParent(_player.RigBones.FpsRig.RightHand);
				_fpsGraphicParent.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			}
		}

		protected override void NetworkSpawn()
		{
			if (_player != null)
			{
				NetworkVariable<byte> colorIndex = _player.ColorIndex;
				colorIndex.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(colorIndex.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(HandlePlayerColorIndexChanged));
			}
			NetworkVariable<byte> presentationState = _presentationState;
			presentationState.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(presentationState.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnPresentationStateChanged));
			NetworkVariable<byte> presentationDirection = _presentationDirection;
			presentationDirection.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(presentationDirection.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnPresentationDirectionChanged));
			NetworkVariable<byte> presentationSteerDirection = _presentationSteerDirection;
			presentationSteerDirection.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(presentationSteerDirection.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnPresentationSteerDirectionChanged));
			NetworkVariable<Vector3> presentationCastPoint = _presentationCastPoint;
			presentationCastPoint.OnValueChanged = (NetworkVariable<Vector3>.OnValueChangedDelegate)Delegate.Combine(presentationCastPoint.OnValueChanged, new NetworkVariable<Vector3>.OnValueChangedDelegate(OnPresentationCastPointChanged));
			NetworkVariable<uint> presentationSessionId = _presentationSessionId;
			presentationSessionId.OnValueChanged = (NetworkVariable<uint>.OnValueChangedDelegate)Delegate.Combine(presentationSessionId.OnValueChanged, new NetworkVariable<uint>.OnValueChangedDelegate(OnPresentationSessionChanged));
			NetworkVariable<Vector3> presentationCastOrigin = _presentationCastOrigin;
			presentationCastOrigin.OnValueChanged = (NetworkVariable<Vector3>.OnValueChangedDelegate)Delegate.Combine(presentationCastOrigin.OnValueChanged, new NetworkVariable<Vector3>.OnValueChangedDelegate(OnPresentationCastOriginChanged));
			NetworkVariable<double> presentationCastStartTime = _presentationCastStartTime;
			presentationCastStartTime.OnValueChanged = (NetworkVariable<double>.OnValueChangedDelegate)Delegate.Combine(presentationCastStartTime.OnValueChanged, new NetworkVariable<double>.OnValueChangedDelegate(OnPresentationCastStartTimeChanged));
			NetworkVariable<float> presentationCastDuration = _presentationCastDuration;
			presentationCastDuration.OnValueChanged = (NetworkVariable<float>.OnValueChangedDelegate)Delegate.Combine(presentationCastDuration.OnValueChanged, new NetworkVariable<float>.OnValueChangedDelegate(OnPresentationCastDurationChanged));
			NetworkVariable<float> presentationTension = _presentationTension;
			presentationTension.OnValueChanged = (NetworkVariable<float>.OnValueChangedDelegate)Delegate.Combine(presentationTension.OnValueChanged, new NetworkVariable<float>.OnValueChangedDelegate(OnPresentationTensionChanged));
			NetworkVariable<float> presentationStrength = _presentationStrength;
			presentationStrength.OnValueChanged = (NetworkVariable<float>.OnValueChangedDelegate)Delegate.Combine(presentationStrength.OnValueChanged, new NetworkVariable<float>.OnValueChangedDelegate(OnPresentationStrengthChanged));
			NetworkVariable<bool> presentationReeling = _presentationReeling;
			presentationReeling.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(presentationReeling.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnPresentationReelingChanged));
			NetworkVariable<bool> presentationStruggling = _presentationStruggling;
			presentationStruggling.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(presentationStruggling.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnPresentationStrugglingChanged));
			NetworkVariable<FixedString64Bytes> presentationCaughtFishId = _presentationCaughtFishId;
			presentationCaughtFishId.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Combine(presentationCaughtFishId.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(OnPresentationCaughtFishChanged));
			ApplyFishingRodMaterial((_player != null) ? _player.ColorIndex.Value : byte.MaxValue);
			if (base.IsOwner)
			{
				RegisterLocalInput();
				SetLocalState(base.ToolStatus ? FishingSessionState.Ready : FishingSessionState.Inactive);
			}
			else
			{
				ApplyRemotePresentation();
			}
		}

		protected override void NetworkDespawn()
		{
			if (base.IsServer)
			{
				ServerReleaseUndeliveredReward();
				ServerAbortFishing();
			}
			if (_player != null)
			{
				NetworkVariable<byte> colorIndex = _player.ColorIndex;
				colorIndex.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(colorIndex.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(HandlePlayerColorIndexChanged));
			}
			NetworkVariable<byte> presentationState = _presentationState;
			presentationState.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(presentationState.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnPresentationStateChanged));
			NetworkVariable<byte> presentationDirection = _presentationDirection;
			presentationDirection.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(presentationDirection.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnPresentationDirectionChanged));
			NetworkVariable<byte> presentationSteerDirection = _presentationSteerDirection;
			presentationSteerDirection.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(presentationSteerDirection.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnPresentationSteerDirectionChanged));
			NetworkVariable<Vector3> presentationCastPoint = _presentationCastPoint;
			presentationCastPoint.OnValueChanged = (NetworkVariable<Vector3>.OnValueChangedDelegate)Delegate.Remove(presentationCastPoint.OnValueChanged, new NetworkVariable<Vector3>.OnValueChangedDelegate(OnPresentationCastPointChanged));
			NetworkVariable<uint> presentationSessionId = _presentationSessionId;
			presentationSessionId.OnValueChanged = (NetworkVariable<uint>.OnValueChangedDelegate)Delegate.Remove(presentationSessionId.OnValueChanged, new NetworkVariable<uint>.OnValueChangedDelegate(OnPresentationSessionChanged));
			NetworkVariable<Vector3> presentationCastOrigin = _presentationCastOrigin;
			presentationCastOrigin.OnValueChanged = (NetworkVariable<Vector3>.OnValueChangedDelegate)Delegate.Remove(presentationCastOrigin.OnValueChanged, new NetworkVariable<Vector3>.OnValueChangedDelegate(OnPresentationCastOriginChanged));
			NetworkVariable<double> presentationCastStartTime = _presentationCastStartTime;
			presentationCastStartTime.OnValueChanged = (NetworkVariable<double>.OnValueChangedDelegate)Delegate.Remove(presentationCastStartTime.OnValueChanged, new NetworkVariable<double>.OnValueChangedDelegate(OnPresentationCastStartTimeChanged));
			NetworkVariable<float> presentationCastDuration = _presentationCastDuration;
			presentationCastDuration.OnValueChanged = (NetworkVariable<float>.OnValueChangedDelegate)Delegate.Remove(presentationCastDuration.OnValueChanged, new NetworkVariable<float>.OnValueChangedDelegate(OnPresentationCastDurationChanged));
			NetworkVariable<float> presentationTension = _presentationTension;
			presentationTension.OnValueChanged = (NetworkVariable<float>.OnValueChangedDelegate)Delegate.Remove(presentationTension.OnValueChanged, new NetworkVariable<float>.OnValueChangedDelegate(OnPresentationTensionChanged));
			NetworkVariable<float> presentationStrength = _presentationStrength;
			presentationStrength.OnValueChanged = (NetworkVariable<float>.OnValueChangedDelegate)Delegate.Remove(presentationStrength.OnValueChanged, new NetworkVariable<float>.OnValueChangedDelegate(OnPresentationStrengthChanged));
			NetworkVariable<bool> presentationReeling = _presentationReeling;
			presentationReeling.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(presentationReeling.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnPresentationReelingChanged));
			NetworkVariable<bool> presentationStruggling = _presentationStruggling;
			presentationStruggling.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(presentationStruggling.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnPresentationStrugglingChanged));
			NetworkVariable<FixedString64Bytes> presentationCaughtFishId = _presentationCaughtFishId;
			presentationCaughtFishId.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Remove(presentationCaughtFishId.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(OnPresentationCaughtFishChanged));
			UnregisterLocalInput();
			CancelFishingSession(FishingCancelReason.NetworkDespawn, returnToReady: false, releaseMovement: true);
			ResetFishingRodAnimator();
		}

		protected override void EnableHints()
		{
			RefreshFishingRodHints();
		}

		protected override void DisableHints()
		{
			if (!(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
				Singleton<HintsManager>.Instance.RemoveHint("RightMouseMechanic");
				Singleton<HintsManager>.Instance.RemoveHint("Interact");
			}
		}

		private void RefreshFishingRodHints()
		{
			if (!base.IsOwner || !base.ToolStatus || Singleton<HintsManager>.Instance == null)
			{
				return;
			}
			Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
			Singleton<HintsManager>.Instance.RemoveHint("RightMouseMechanic");
			Singleton<HintsManager>.Instance.RemoveHint("Interact");
			switch (_state)
			{
			case FishingSessionState.Ready:
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_cast");
				Singleton<HintsManager>.Instance.AddHint("Interact", "gameplay.fishing.polish.help", "UiTable");
				break;
			case FishingSessionState.Charging:
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_cast");
				Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_cancel");
				break;
			case FishingSessionState.Casting:
			case FishingSessionState.Waiting:
				Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_cancel");
				break;
			case FishingSessionState.Challenge:
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_reel");
				Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_cancel");
				break;
			case FishingSessionState.Success:
				if (_catchReadyToCollect && !_collectRequested)
				{
					Singleton<HintsManager>.Instance.AddHint("Interact", "key_collect");
				}
				break;
			}
		}

		public override bool DisableIf()
		{
			return !IsFishingActive;
		}

		protected override void ApplyToolStatus(bool status)
		{
			base.ApplyToolStatus(status);
			SetFishingRodEquippedAnimation(status);
			if (!base.IsOwner)
			{
				if (!status)
				{
					ResetPresentationVisuals();
					ResetFishingRodAnimator();
				}
				RefreshIdleBobberPresentation();
				return;
			}
			PlayRodSfx((!status) ? _data?.UnequipSfx : _data?.EquipSfx);
			if (status)
			{
				SetLocalState(FishingSessionState.Ready);
				ApplyPendingLevel();
				_fishingRodUI?.ShowReady();
			}
			else
			{
				CancelFishingSession(FishingCancelReason.ToolDisabled, returnToReady: false, releaseMovement: true);
				SetLocalState(FishingSessionState.Inactive);
				ResetFishingRodAnimator();
			}
		}

		public override void LoadLevel(ToolDataNet data)
		{
			SetUnlock(data.FishingRodUnlocked);
			if (!data.FishingRodUnlocked)
			{
				if (base.IsOwner && base.ToolStatus)
				{
					ForceDisableTool();
				}
				return;
			}
			byte fishingRodLevel = data.FishingRodLevel;
			if (IsFishingActive)
			{
				_pendingLevel = fishingRodLevel;
			}
			else
			{
				ApplyLevel(fishingRodLevel);
			}
		}

		protected override void SetUnlock(bool isUnlocked)
		{
			if (base.IsOwner && Singleton<PPG_WheelManager>.Instance != null)
			{
				Singleton<PPG_WheelManager>.Instance.UnlockFishingRod(isUnlocked);
			}
		}

		public override void ApplyLevel(byte level)
		{
			if (_data == null)
			{
				UtilityMethods.Log("[FishingRod] FishingRodData is not assigned.", LogType.Error);
				return;
			}
			FishingRodLevelData level2 = _data.GetLevel(level);
			if (level2 == null)
			{
				UtilityMethods.Log($"[FishingRod] Level {level} is not configured.", LogType.Error);
				return;
			}
			_currentLevel = level2;
			base.ApplyLevel(level);
		}

		protected override void SpawnGraphics()
		{
			base.SpawnGraphics();
			if (_tpsGraphic != null)
			{
				_tpsGraphic.transform.SetLocalPositionAndRotation(_data.OffsetTps.PositionOffset, Quaternion.Euler(_data.OffsetTps.RotationOffset));
				_tpsGraphic.transform.localScale = _data.OffsetTps.ScaleOffset;
				_tpsRodVisual = InitializeRodVisual(_tpsGraphic, "TPS", useFpsAnimator: false);
				_tpsLineVisual = CreateLineVisual((_tpsRodVisual != null) ? _tpsRodVisual.LineOrigin : ResolveLineOrigin(_tpsGraphic.transform), _data.LineOriginOffsetTps, "FishingLine_TPS");
			}
			if (base.IsOwner && _fpsGraphic != null)
			{
				_fpsGraphic.transform.SetLocalPositionAndRotation(_data.OffsetFps.PositionOffset, Quaternion.Euler(_data.OffsetFps.RotationOffset));
				_fpsGraphic.transform.localScale = _data.OffsetFps.ScaleOffset;
				_fpsRodVisual = InitializeRodVisual(_fpsGraphic, "FPS", useFpsAnimator: true);
				_fpsLineVisual = CreateLineVisual((_fpsRodVisual != null) ? _fpsRodVisual.LineOrigin : ResolveLineOrigin(_fpsGraphic.transform), _data.LineOriginOffsetFps, "FishingLine_FPS");
			}
			EnsureReelIKController();
			_reelIKController?.BindTargets(_fpsRodVisual?.ReelHandTarget, _tpsRodVisual?.ReelHandTarget);
			ApplyFishingRodMaterial((_player != null) ? _player.ColorIndex.Value : byte.MaxValue);
			ApplyRemotePresentation();
			RefreshIdleBobberPresentation();
		}

		private void HandlePlayerColorIndexChanged(byte previous, byte current)
		{
			ApplyFishingRodMaterial(current);
		}

		private void ApplyFishingRodMaterial(byte colorIndex)
		{
			if (!(Singleton<PlayersManager>.Instance == null) && Singleton<PlayersManager>.Instance.TryGetFishingRodMaterial(colorIndex, out var material))
			{
				ApplyFishingRodMaterial(_tpsGraphic, material);
				if (base.IsOwner)
				{
					ApplyFishingRodMaterial(_fpsGraphic, material);
				}
			}
		}

		private static void ApplyFishingRodMaterial(GameObject graphic, Material material)
		{
			if (graphic == null)
			{
				return;
			}
			IReadOnlyList<Material> fishingRodMaterialPalette = Singleton<PlayersManager>.Instance.FishingRodMaterialPalette;
			Renderer[] componentsInChildren = graphic.GetComponentsInChildren<Renderer>(includeInactive: true);
			foreach (Renderer renderer in componentsInChildren)
			{
				Material[] sharedMaterials = renderer.sharedMaterials;
				bool flag = false;
				for (int j = 0; j < sharedMaterials.Length; j++)
				{
					Material material2 = sharedMaterials[j];
					if (!(material2 == null) && (!(material2.name != "rod06") || fishingRodMaterialPalette.Contains(material2)))
					{
						sharedMaterials[j] = material;
						flag = true;
					}
				}
				if (flag)
				{
					renderer.sharedMaterials = sharedMaterials;
				}
			}
		}

		protected override void DestroyGraphics()
		{
			_reelIKController?.SetReeling(reeling: false);
			_reelIKController?.BindTargets(null, null);
			DestroyIdleBobberPresentation();
			DestroyCaughtFishGraphic();
			_fpsRodVisual?.ResetPose();
			_tpsRodVisual?.ResetPose();
			_fpsLineVisual = null;
			_tpsLineVisual = null;
			_fpsRodVisual = null;
			_tpsRodVisual = null;
			base.DestroyGraphics();
		}

		protected override GameObject GetGraphicByLevel()
		{
			if (_data == null)
			{
				return null;
			}
			FishingRodLevelData fishingRodLevelData = _data.GetLevel(Level);
			if (fishingRodLevelData == null && _data.Levels != null)
			{
				fishingRodLevelData = _data.Levels.FirstOrDefault((FishingRodLevelData entry) => entry != null);
			}
			return fishingRodLevelData?.GraphicPrefab;
		}

		protected override int GetLevelCount()
		{
			return (_data?.Levels?.Count).GetValueOrDefault();
		}

		public override void Mouse1(InputActionPhase phase)
		{
			if (!base.IsOwner || !base.ToolStatus)
			{
				return;
			}
			if (_state == FishingSessionState.Ready)
			{
				if (phase == InputActionPhase.Started)
				{
					BeginCastCharge();
				}
			}
			else if (_state == FishingSessionState.Charging)
			{
				if (phase == InputActionPhase.Canceled)
				{
					CompleteCastCharge();
				}
			}
			else
			{
				if (_state != FishingSessionState.Challenge)
				{
					return;
				}
				switch (phase)
				{
				case InputActionPhase.Started:
					SetReeling(!FishingRodPreferences.ToggleReel || !_isReeling);
					break;
				case InputActionPhase.Canceled:
					if (!FishingRodPreferences.ToggleReel)
					{
						SetReeling(reeling: false);
					}
					break;
				}
			}
		}

		public override void Interact(InputActionPhase phase)
		{
			if (base.IsOwner && base.ToolStatus && phase == InputActionPhase.Started && _state == FishingSessionState.Ready)
			{
				OpenFishingTutorial();
			}
			else if (base.IsOwner && base.ToolStatus && phase == InputActionPhase.Started && _state == FishingSessionState.Success && !(_currentFish == null) && !_collectRequested)
			{
				if (!_catchReadyToCollect)
				{
					_collectBufferedUntil = Time.unscaledTime + 0.35f;
				}
				else
				{
					CollectCaughtFish();
				}
			}
		}

		public override void Mouse2(InputActionPhase phase)
		{
			if (!base.IsOwner || !base.ToolStatus)
			{
				return;
			}
			if (phase == InputActionPhase.Started)
			{
				FishingSessionState state = _state;
				if (state == FishingSessionState.Charging || state == FishingSessionState.Casting || state == FishingSessionState.Waiting)
				{
					CancelFishingRodCastAnimation();
					CancelFishingSession(FishingCancelReason.PlayerCanceled, returnToReady: true, releaseMovement: true);
					return;
				}
			}
			if (IsCancelHoldState(_state))
			{
				switch (phase)
				{
				case InputActionPhase.Started:
				case InputActionPhase.Performed:
					StartFishingCancelHold();
					break;
				case InputActionPhase.Canceled:
					ResetFishingCancelHold();
					break;
				}
			}
			else
			{
				ResetFishingCancelHold();
			}
		}

		private void Update()
		{
			TickServerFishing();
			if (!base.IsOwner)
			{
				if (_state == FishingSessionState.Casting)
				{
					UpdateCastElapsedFromClock();
				}
				UpdateLineAndRodPresentation();
			}
			else
			{
				if (!base.ToolStatus)
				{
					return;
				}
				if (IsFishingActive && IsPlayerInWaterMovementMode())
				{
					HandleOwnerWaterVolumeEntered();
				}
				else if (IsFishingActive && IsPlayerStateInvalid())
				{
					ForceDisableTool();
				}
				else if (!TickFishingCancelHold(Time.deltaTime))
				{
					switch (_state)
					{
					case FishingSessionState.Charging:
						TickCharging();
						break;
					case FishingSessionState.Casting:
						TickCasting();
						break;
					case FishingSessionState.Waiting:
						TickWaiting();
						break;
					case FishingSessionState.Challenge:
						TickChallenge();
						break;
					case FishingSessionState.Success:
					case FishingSessionState.Failure:
					case FishingSessionState.CanceledFailure:
						TickResult();
						break;
					}
					TickPresentationSync();
					UpdateLineAndRodPresentation();
				}
			}
		}

		private void BeginCastCharge()
		{
			if (_pendingCollectionSession == 0)
			{
				if (_currentLevel == null)
				{
					_currentLevel = _data?.GetLevel(Level);
				}
				if (!(_currentLevel == null) && _player.FirstPersonCharacter.IsGrounded() && !_player.FirstPersonCharacter.IsSwimming())
				{
					BeginFishingRodCastAnimation();
					_castChargeElapsed = 0f;
					_currentCastCharge = 0f;
					SetLocalState(FishingSessionState.Charging);
					_fishingRodUI?.StartCast(_data, _currentLevel);
					UpdateCastChargeFeedback();
					ShowFirstCastGuidance();
				}
			}
		}

		private void CompleteCastCharge()
		{
			HideCastPreview();
			EndFishingRodCastAnimation();
			float normalizedCastCharge = GetNormalizedCastCharge();
			_fishingRodUI?.StopCast();
			SetCurrentCastDynamics(normalizedCastCharge);
			if (!TryResolveCastPoint(normalizedCastCharge, out var point, out var horizontalDistance, out var castLocationState))
			{
				ShowNoFishHereFeedback();
				CancelFishingSession(FishingCancelReason.InvalidCast, returnToReady: true, releaseMovement: true);
			}
			else if (castLocationState != CastLocationState.FishableWater)
			{
				BeginNoFishCast(point, castLocationState);
			}
			else
			{
				BeginAcceptedCast(point, _data.GetZone(horizontalDistance));
			}
		}

		private void TickCharging()
		{
			_castChargeElapsed += Time.deltaTime;
			UpdateCastChargeFeedback();
			UpdateCastPreview();
		}

		private void UpdateCastChargeFeedback()
		{
			if (!(_data == null) && !(_currentLevel == null))
			{
				float normalizedCastCharge = GetNormalizedCastCharge();
				float castDistance = _data.GetCastDistance(_currentLevel, normalizedCastCharge);
				_fishingRodUI?.SetCastCharge(_data.GetScaledCastCharge(normalizedCastCharge));
				_fishingRodUI?.SetCastZoneFeedback(_data.GetCastZoneFeedback(castDistance) + "\n" + _previewStatus);
			}
		}

		private float GetNormalizedCastCharge()
		{
			return FishingRodChallenge.Charge(_castChargeElapsed, _castChargeDuration, FishingRodPreferences.HoldFullCharge);
		}

		private void SetCurrentCastDynamics(float normalizedCharge)
		{
			_currentCastCharge = Mathf.Clamp01(normalizedCharge);
			_currentCastTravelDuration = _data.GetCastTravelDuration(_currentCastCharge);
			_currentCastArcHeight = _data.GetCastArcHeight(_currentCastCharge);
		}

		private void BeginAcceptedCast(Vector3 castPoint, OceanLayer zone)
		{
			_sessionId++;
			if (_sessionId == 0)
			{
				_sessionId = 1u;
			}
			_castPoint = castPoint;
			_castLocationState = CastLocationState.FishableWater;
			_castElapsed = 0f;
			_castStartedAt = GetSynchronizedTime();
			_fpsCastOrigin = ResolveCastOrigin(_fpsRodVisual);
			_tpsCastOrigin = ResolveCastOrigin(_tpsRodVisual);
			_currentZone = zone;
			_currentFish = null;
			_currentFishSettings = null;
			_currentMatchup = null;
			_serverAcceptedCast = false;
			_completedCounters = 0;
			_counterProgress = 0f;
			_waitRemaining = 0f;
			_catchProgress = 0f;
			_lineTension = 0f;
			_counterHeldDuration = 0f;
			_counterInput = Vector2.zero;
			_currentLineDirectionOffset = Vector3.zero;
			SetReeling(reeling: false);
			if (base.IsSpawned)
			{
				_presentationCaughtFishId.Value = default;
			}
			AcquireMovementPause();
			PlayRodSfx(_data.CastSfx);
			PushCastPresentation();
			SetLocalState(FishingSessionState.Casting);
			_fishingRodUI?.ShowWaiting();
			ServerBeginFishingRpc(_sessionId, _castPoint, _currentCastCharge);
			UtilityMethods.Log($"[FishingRod] Cast accepted. Session={_sessionId} Zone={_currentZone} " + $"Wait={_waitRemaining:0.0}s");
		}

		private void BeginNoFishCast(Vector3 castPoint, CastLocationState castLocationState)
		{
			_sessionId++;
			if (_sessionId == 0)
			{
				_sessionId = 1u;
			}
			_castPoint = castPoint;
			_castLocationState = castLocationState;
			_castElapsed = 0f;
			_castStartedAt = GetSynchronizedTime();
			_fpsCastOrigin = ResolveCastOrigin(_fpsRodVisual);
			_tpsCastOrigin = ResolveCastOrigin(_tpsRodVisual);
			_currentZone = OceanLayer.None;
			_currentFish = null;
			_currentFishSettings = null;
			_currentMatchup = null;
			_catchProgress = 0f;
			_lineTension = 0f;
			_counterHeldDuration = 0f;
			_counterInput = Vector2.zero;
			_currentLineDirectionOffset = Vector3.zero;
			SetReeling(reeling: false);
			AcquireMovementPause();
			PlayRodSfx(_data.CastSfx);
			PushCastPresentation();
			SetLocalState(FishingSessionState.Casting);
			_fishingRodUI?.ShowWaiting();
			UtilityMethods.Log("[FishingRod] No-fish cast is traveling. " + $"Session={_sessionId} Location={_castLocationState}");
		}

		private void TickCasting()
		{
			UpdateCastElapsedFromClock();
			if (_castElapsed < GetCastTravelDuration())
			{
				return;
			}
			_castElapsed = GetCastTravelDuration();
			if (_castLocationState != CastLocationState.FishableWater)
			{
				if (_castLocationState == CastLocationState.ShallowWater)
				{
					PlayRodSfx(_data.CastWaterSplashSfx, _castPoint);
				}
				ShowNoFishHereFeedback();
				CancelFishingSession(FishingCancelReason.InvalidCast, returnToReady: true, releaseMovement: true);
			}
			else
			{
				PlayRodSfx(_data.CastWaterSplashSfx, _castPoint);
				SetLocalState(FishingSessionState.Waiting);
				ShowWaitingGuidance();
			}
		}

		private void TickWaiting()
		{
			if (_serverAcceptedCast)
			{
				_waitRemaining = Mathf.Max(0f, _waitRemaining - Time.deltaTime);
			}
			UpdateWaitingBobber();
			if (!_serverAcceptedCast && GetSynchronizedTime() - _castStartedAt > 10.0)
			{
				ShowNoFishHereFeedback();
				CancelFishingSession(FishingCancelReason.InvalidCast, returnToReady: true, releaseMovement: true);
			}
		}

		private void BeginChallenge()
		{
			if (_currentFish == null || _currentFishSettings == null || _currentMatchup == null)
			{
				CancelFishingSession(FishingCancelReason.NoEligibleFish, returnToReady: true, releaseMovement: true);
				return;
			}
			SetLocalState(FishingSessionState.Challenge);
			StartChallengeLookAtBobber();
			_counterInput = Vector2.zero;
			_stickCounterInput = Vector2.zero;
			_moveCounterInput = Vector2.zero;
			_nextInputSend = 0f;
			PlayRodSfx((_data.BiteSfx != null) ? _data.BiteSfx : _data.FishSplashSfx, _castPoint);
			ShowChallengeGuidance();
		}

		private void TickChallenge()
		{
			TickCounterInputDecay(Time.deltaTime);
			UpdateFishingRodSteerAnimation();
			UpdateFishingSway();
			UpdateTensionLoop();
			if (Time.unscaledTime >= _nextInputSend)
			{
				_nextInputSend = Time.unscaledTime + 0.05f;
				ServerFishingInputRpc(_sessionId, EffectiveCounterInput, _isReeling);
			}
			_fishingRodUI?.ShowChallenge(_fishDirection, _catchProgress, _lineTension, _isReeling, _counterProgress, _preparationRemaining, ReelBindingLabel);
		}

		private void StartFishingCancelHold()
		{
			if (!_challengeCancelHoldActive && IsCancelHoldState(_state))
			{
				_challengeCancelHoldActive = true;
				_challengeCancelHoldTimer = 0f;
				UtilityMethods.Log($"[FishingRod] Cancel hold started. State={_state}");
			}
		}

		private bool TickFishingCancelHold(float deltaTime)
		{
			if (!_challengeCancelHoldActive)
			{
				return false;
			}
			if (!IsCancelHoldState(_state))
			{
				ResetFishingCancelHold();
				return false;
			}
			_challengeCancelHoldTimer += deltaTime;
			_fishingRodUI?.SetCancelProgress(_challengeCancelHoldTimer / _challengeCancelHoldDuration);
			if (_challengeCancelHoldTimer < _challengeCancelHoldDuration)
			{
				return false;
			}
			ResetFishingCancelHold();
			ResolveCanceledFailure();
			return true;
		}

		private void ResetFishingCancelHold()
		{
			_challengeCancelHoldActive = false;
			_challengeCancelHoldTimer = 0f;
			_fishingRodUI?.SetCancelProgress(0f);
		}

		private static bool IsCancelHoldState(FishingSessionState state)
		{
			if (state != FishingSessionState.Charging && state != FishingSessionState.Casting && state != FishingSessionState.Waiting)
			{
				return state == FishingSessionState.Challenge;
			}
			return true;
		}

		private void SetFishDirection(FishingRodFishDirection direction, float duration)
		{
			_fishDirection = direction;
			_directionRemaining = Mathf.Max(0.1f, duration);
			if (direction == FishingRodFishDirection.Centered || direction == FishingRodFishDirection.Tired)
			{
				SetFishingRodSteerAnimation(FishingRodSteerDirection.None);
			}
			bool flag = direction == FishingRodFishDirection.Left || direction == FishingRodFishDirection.Right || direction == FishingRodFishDirection.Forward;
			if (flag)
			{
				PlayRodSfx(_data.FishSplashSfx, _castPoint);
			}
			UpdateReelStruggleAudio(flag);
			if (base.IsSpawned)
			{
				_presentationDirection.Value = (byte)_fishDirection;
			}
		}

		private bool IsCorrectCounterInput()
		{
			return FishingRodChallenge.IsCorrect(_fishDirection, FishingRodChallenge.ResolveCounter(EffectiveCounterInput, _data.MouseCounterThreshold));
		}

		private void SetReeling(bool reeling)
		{
			_isReeling = reeling && _state == FishingSessionState.Challenge;
			if (_isReeling)
			{
				SetFishingRodSteerAnimation(FishingRodSteerDirection.None);
			}
			SetFishingRodReelAnimation(_isReeling);
			if (_isReeling)
			{
				StartReelLoop();
			}
			else
			{
				StopOwnedLoop(ref _reelLoopHandle);
			}
			UpdateReelStruggleAudio(forceRetrigger: false);
			if (base.IsSpawned)
			{
				_presentationReeling.Value = _isReeling;
			}
		}

		private void PlayRodSfx(AudioClipSO sfx, Vector3? worldPosition = null)
		{
			if (base.IsOwner && !(sfx == null) && !(Singleton<SoundManager>.Instance == null))
			{
				Vector3 position = worldPosition ?? ((_player != null) ? _player.transform.position : base.transform.position);
				Singleton<SoundManager>.Instance.PlaySFX(sfx, position);
			}
		}

		private void ConfigureRemoteRodAudioSource(AudioSource source, Vector3 position, Transform followTarget = null)
		{
			if (!(source == null))
			{
				float num = Mathf.Max(0.1f, (_data != null) ? _data.RemoteSfxHearDistance : 5f);
				source.transform.position = position;
				if (followTarget != null)
				{
					source.transform.SetParent(followTarget, worldPositionStays: true);
				}
				source.spatialBlend = 1f;
				source.minDistance = Mathf.Min(1f, num);
				source.maxDistance = num;
				source.rolloffMode = AudioRolloffMode.Linear;
			}
		}

		private void StartReelLoop()
		{
			if (_reelLoopHandle != null && _reelLoopHandle.IsActive)
			{
				return;
			}
			_reelLoopHandle = null;
			if (!(_data?.ReelLoopSfx == null) && !(Singleton<SoundManager>.Instance == null))
			{
				_reelLoopHandle = Singleton<SoundManager>.Instance.PlayOwnedLoop(_data.ReelLoopSfx, (_player != null) ? _player.transform.position : base.transform.position, (_player != null) ? _player.transform : base.transform, $"FishingRod.Reel-{base.OwnerClientId}");
				if (!base.IsOwner)
				{
					ConfigureRemoteRodAudioSource(_reelLoopHandle?.Source, (_player != null) ? _player.transform.position : base.transform.position, (_player != null) ? _player.transform : base.transform);
				}
			}
		}

		private void UpdateReelStruggleAudio(bool forceRetrigger)
		{
			int num;
			if (_state == FishingSessionState.Challenge && _isReeling)
			{
				FishingRodFishDirection fishDirection = _fishDirection;
				num = ((fishDirection == FishingRodFishDirection.Left || fishDirection == FishingRodFishDirection.Right || fishDirection == FishingRodFishDirection.Forward) ? 1 : 0);
			}
			else
			{
				num = 0;
			}
			bool flag = (byte)num != 0;
			if (forceRetrigger & flag)
			{
				_reelStruggleActive = false;
			}
			if (flag && !_reelStruggleActive)
			{
				PlayRodSfx(_data?.ReelStruggleSfx);
			}
			_reelStruggleActive = flag;
			if (base.IsOwner && base.IsSpawned)
			{
				_presentationStruggling.Value = flag;
			}
		}

		private void UpdateTensionLoop()
		{
			if (!base.IsOwner || _data == null)
			{
				return;
			}
			if (_tensionLoopHandle != null && !_tensionLoopHandle.IsActive)
			{
				_tensionLoopHandle = null;
			}
			float num = Mathf.Clamp01(_data.TensionLoopStartThreshold);
			float num2 = Mathf.Min(num, Mathf.Clamp01(_data.TensionLoopStopThreshold));
			if (_tensionLoopHandle == null)
			{
				if (_lineTension < num || _data.LineTensionLoopSfx == null || Singleton<SoundManager>.Instance == null)
				{
					return;
				}
				_tensionLoopHandle = Singleton<SoundManager>.Instance.PlayOwnedLoop(_data.LineTensionLoopSfx, (_player != null) ? _player.transform.position : base.transform.position, (_player != null) ? _player.transform : base.transform, $"FishingRod.Tension-{base.OwnerClientId}");
			}
			else if (_lineTension <= num2)
			{
				StopOwnedLoop(ref _tensionLoopHandle);
				return;
			}
			AudioSource audioSource = _tensionLoopHandle?.Source;
			if (!(audioSource == null))
			{
				float t = Mathf.InverseLerp(num, 1f, _lineTension);
				audioSource.pitch = Mathf.Clamp(Mathf.Lerp(_data.TensionLoopPitchRange.x, _data.TensionLoopPitchRange.y, t), 0.1f, 3f);
				float num3 = Mathf.Max(0f, Mathf.Lerp(_data.TensionLoopVolumeMultiplierRange.x, _data.TensionLoopVolumeMultiplierRange.y, t));
				audioSource.volume = Mathf.Clamp01(_data.LineTensionLoopSfx.Volume * num3);
			}
		}

		private void StopRodAudioLoops()
		{
			StopOwnedLoop(ref _reelLoopHandle);
			StopOwnedLoop(ref _tensionLoopHandle);
			_reelStruggleActive = false;
		}

		private static void StopOwnedLoop(ref OwnedLoopHandle handle)
		{
			if (handle != null)
			{
				Singleton<SoundManager>.Instance?.ReleaseOwnedLoop(handle);
				handle = null;
			}
		}

		private void ResolveSuccess()
		{
			if (_state == FishingSessionState.Challenge && !(_currentFish == null))
			{
				SetReeling(reeling: false);
				StopOwnedLoop(ref _tensionLoopHandle);
				_catchReadyToCollect = false;
				_collectRequested = false;
				_successCollectWaitElapsed = 0f;
				if (base.IsSpawned)
				{
					_presentationCaughtFishId.Value = new FixedString64Bytes(_currentFish.Id);
					_presentationCaughtWeight.Value = _caughtWeight;
				}
				SpawnCaughtFishGraphic();
				PlayRodSfx(_data.CatchSuccessSfx);
				SetLocalState(FishingSessionState.Success);
				ReleaseMovementPause();
				SetFishingRodSuccessAnimation(active: true);
				_fishingRodUI?.ShowCatch(_currentFish.LocalizedName, _caughtWeight, ready: false, collecting: false, CollectBindingIcon);
			}
		}

		private void CollectCaughtFish()
		{
			_collectRequested = true;
			_pendingCollectionSession = _sessionId;
			_pendingCollectionFish = new FixedString64Bytes(_currentFish.Id);
			_pendingCollectionFallback = Singleton<QuickSlotHandler>.Instance != null && !Singleton<QuickSlotHandler>.Instance.CanHoldSlot(_currentFish);
			_collectRetryAt = Time.unscaledTime + 3f;
			RefreshFishingRodHints();
			_fishingRodUI?.ShowCatch(_currentFish.LocalizedName, _caughtWeight, ready: true, collecting: true, CollectBindingIcon);
			ServerCompleteCatchRpc(_sessionId, _pendingCollectionFish, _pendingCollectionFallback);
		}

		private void ResolveFailure()
		{
			if (_state == FishingSessionState.Challenge)
			{
				BeginFailure(FishingSessionState.Failure, playBreakEffects: true);
			}
		}

		private void OpenFishingTutorial()
		{
			if (base.IsOwner && !(_tutorialData == null) && !(_player == null) && !(Singleton<GameUIManager>.Instance == null) && !(Singleton<GameUIManager>.Instance.StationTutorialPopupUI == null))
			{
				PlayerPrefs.SetInt("StationTutorial.FishingRod.Seen", 1);
				PlayerPrefs.Save();
				Singleton<GameUIManager>.Instance.StationTutorialPopupUI.OpenAfterRepair(_tutorialData, _player);
			}
		}

		private void ResolveCanceledFailure()
		{
			if (IsCancelHoldState(_state))
			{
				CancelFishingRodCastAnimation();
				CancelFishingSession(FishingCancelReason.PlayerCanceled, returnToReady: true, releaseMovement: true);
			}
		}

		private void BeginFailure(FishingSessionState failureState, bool playBreakEffects)
		{
			SetReeling(reeling: false);
			StopOwnedLoop(ref _tensionLoopHandle);
			if (base.IsSpawned)
			{
				_presentationCaughtFishId.Value = default;
			}
			if (playBreakEffects)
			{
				PlayRodSfx(_data.LineBreakSfx);
				PlayRodSfx(_data.FishSplashSfx, _castPoint);
			}
			ServerCancelFishingRpc(_sessionId);
			SetLocalState(failureState);
			_resultRemaining = _resultDisplayDuration;
			_failureAnimationEntered = false;
			TriggerFishingRodFailAnimation();
			ResetFailurePresentation(playBreakEffects);
		}

		private void ResetFailurePresentation(bool showLineBreak)
		{
			_currentLineDirectionOffset = Vector3.zero;
			_fpsLineVisual?.ResetLine();
			_tpsLineVisual?.ResetLine();
			if (showLineBreak)
			{
				_fishingRodUI?.ShowFailure(_failedWhileReeling);
			}
			else
			{
				_fishingRodUI?.StopFishing();
			}
		}

		private void TickResult()
		{
			if (_state == FishingSessionState.Success)
			{
				_successCollectWaitElapsed += Time.deltaTime;
				bool flag = _player == null || _player.IsFishingRodSuccessPoseComplete();
				bool flag2 = _successCollectWaitElapsed >= 1.5f;
				if (!_catchReadyToCollect && (flag | flag2))
				{
					if (!flag)
					{
						UtilityMethods.Log("[FishingRod] Success animation did not complete in time. Enabling catch collection through the safety fallback.", LogType.Warning);
					}
					_catchReadyToCollect = true;
					RefreshFishingRodHints();
				}
				if (_catchReadyToCollect && !_collectRequested)
				{
					_fishingRodUI?.ShowCatch(_currentFish?.LocalizedName ?? "", _caughtWeight, ready: true, collecting: false, CollectBindingIcon);
					if (_collectBufferedUntil >= Time.unscaledTime)
					{
						CollectCaughtFish();
					}
				}
				if (_collectRequested && Time.unscaledTime >= _collectRetryAt)
				{
					_collectRetryAt = Time.unscaledTime + 3f;
					ServerCompleteCatchRpc(_pendingCollectionSession, _pendingCollectionFish, _pendingCollectionFallback);
				}
				return;
			}
			_resultRemaining = Mathf.Max(0f, _resultRemaining - Time.deltaTime);
			if (_player != null && _player.TryGetFishingRodFailAnimationPlaying(out var playing))
			{
				if (playing && _resultRemaining > 0f)
				{
					_failureAnimationEntered = true;
					return;
				}
				if (!_failureAnimationEntered && _resultRemaining > 0f)
				{
					return;
				}
			}
			FishingCancelReason reason = ((_state == FishingSessionState.CanceledFailure) ? FishingCancelReason.PlayerCanceled : FishingCancelReason.None);
			bool num = _state == FishingSessionState.Failure;
			CancelFishingSession(reason, returnToReady: true, releaseMovement: true);
			if (num && PlayerPrefs.GetInt("StationTutorial.FishingRod.Seen", 0) == 0)
			{
				ShowRodNotice(FishingRodText.Get("guide_optional", "Fishing guide · {0}", "Olta rehberi · {0}", CollectBindingLabel));
			}
		}

		private void CancelFishingSession(FishingCancelReason reason, bool returnToReady, bool releaseMovement)
		{
			HideCastPreview();
			if (base.IsOwner && base.IsSpawned && _sessionId != 0)
			{
				ServerCloseBobberRpc(_sessionId);
				if (_pendingCollectionSession == 0)
				{
					ServerCancelFishingRpc(_sessionId);
				}
			}
			if (base.IsOwner && base.IsSpawned)
			{
				_presentationCaughtFishId.Value = default;
				_presentationStruggling.Value = false;
				_presentationSteerDirection.Value = 0;
			}
			_lastCancelReason = reason;
			if (reason != FishingCancelReason.None)
			{
				UtilityMethods.Log($"[FishingRod] Session canceled. Session={_sessionId} Reason={_lastCancelReason}");
			}
			StopRodAudioLoops();
			_counterInput = (_stickCounterInput = (_moveCounterInput = Vector2.zero));
			_serverAcceptedCast = false;
			_counterProgress = (_preparationRemaining = 0f);
			_collectBufferedUntil = -1f;
			SetFishingRodSteerAnimation(FishingRodSteerDirection.None);
			_currentLineDirectionOffset = Vector3.zero;
			_isReeling = false;
			ResetFishingCancelHold();
			StopFishingSway();
			_waitRemaining = 0f;
			_castElapsed = 0f;
			_castChargeElapsed = 0f;
			_currentCastCharge = 0f;
			_currentCastTravelDuration = 0f;
			_currentCastArcHeight = 0f;
			_castStartedAt = 0.0;
			_castLocationState = CastLocationState.NoWaterVolume;
			_directionRemaining = 0f;
			_counterHeldDuration = 0f;
			_catchProgress = 0f;
			_lineTension = 0f;
			_failureAnimationEntered = false;
			_successCollectWaitElapsed = 0f;
			_catchReadyToCollect = false;
			_collectRequested = false;
			DestroyCaughtFishGraphic();
			_currentFish = null;
			_currentFishSettings = null;
			_currentMatchup = null;
			SetFishingRodReelAnimation(reeling: false);
			SetFishingRodSuccessAnimation(active: false);
			_fishingRodUI?.StopFishing();
			if (releaseMovement)
			{
				ReleaseMovementPause();
			}
			ReleaseChallengeLookPause();
			_fpsLineVisual?.ResetLine();
			_tpsLineVisual?.ResetLine();
			if (returnToReady && base.ToolStatus)
			{
				SetLocalState(FishingSessionState.Ready);
				_fishingRodUI?.ShowReady();
				ApplyPendingLevel();
			}
			else if (!base.ToolStatus)
			{
				SetLocalState(FishingSessionState.Inactive);
				ApplyPendingLevel();
			}
		}

		private bool TryResolveCastPoint(float normalizedCharge, out Vector3 point, out float horizontalDistance, out CastLocationState castLocationState)
		{
			point = default;
			horizontalDistance = 0f;
			castLocationState = CastLocationState.NoWaterVolume;
			if (_player == null || _player.MainCamera == null || _currentLevel == null)
			{
				return false;
			}
			Vector3 normalized = Vector3.ProjectOnPlane(_player.MainCamera.transform.forward, Vector3.up).normalized;
			if (normalized.sqrMagnitude < 0.01f)
			{
				normalized = Vector3.ProjectOnPlane(_player.transform.forward, Vector3.up).normalized;
			}
			Vector3 desired = _player.transform.position + normalized * _data.GetCastDistance(_currentLevel, normalizedCharge);
			if (!TryResolveSurface(desired, out point, out castLocationState, verbose: false))
			{
				return false;
			}
			horizontalDistance = Vector3.ProjectOnPlane(point - _player.transform.position, Vector3.up).magnitude;
			return horizontalDistance >= Mathf.Max(0f, _data.MinimumCastDistance) * 0.75f;
		}

		private bool TryResolveSurface(Vector3 desired, out Vector3 point, out CastLocationState location, bool verbose)
		{
			point = desired;
			location = CastLocationState.NoWaterVolume;
			if (_data == null || _player == null)
			{
				return false;
			}
			RaycastHit[] array = Physics.RaycastAll(new Vector3(desired.x, _player.transform.position.y + _waterProbeHeight, desired.z), Vector3.down, _waterProbeDistance, -5, QueryTriggerInteraction.Collide);
			PhysicsVolume physicsVolume = null;
			float num = float.PositiveInfinity;
			float num2 = float.PositiveInfinity;
			RaycastHit raycastHit = default;
			RaycastHit[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				RaycastHit raycastHit2 = array2[i];
				if (TryGetWaterPhysicsVolume(raycastHit2.collider, out var waterVolume))
				{
					if (raycastHit2.distance < num)
					{
						physicsVolume = waterVolume;
						num = raycastHit2.distance;
					}
				}
				else if (!raycastHit2.collider.isTrigger && raycastHit2.distance < num2)
				{
					raycastHit = raycastHit2;
					num2 = raycastHit2.distance;
				}
			}
			if (physicsVolume != null && num <= num2)
			{
				float y = physicsVolume.boxCollider.bounds.max.y;
				point = new Vector3(desired.x, y + 0.03f, desired.z);
				location = GetCastLocationState(point, y, physicsVolume, verbose);
				return true;
			}
			if (float.IsPositiveInfinity(num2))
			{
				return false;
			}
			point = raycastHit.point + Vector3.up * 0.03f;
			return true;
		}

		private CastLocationState GetCastLocationState(Vector3 surfacePoint, float waterSurfaceY, PhysicsVolume waterVolume, bool verbose = true)
		{
			float num = Mathf.Max(0f, _data.MinimumFishableWaterDepth);
			float maxDistance = Mathf.Max(num, _data.WaterDepthProbeDistance);
			if (!Physics.Raycast(new Vector3(surfacePoint.x, waterSurfaceY - 0.01f, surfacePoint.z), Vector3.down, out var hitInfo, maxDistance, _data.WaterDepthGroundMask, QueryTriggerInteraction.Ignore))
			{
				return CastLocationState.FishableWater;
			}
			float num2 = Mathf.Max(0f, waterSurfaceY - hitInfo.point.y);
			if (num2 >= num)
			{
				return CastLocationState.FishableWater;
			}
			if (verbose)
			{
				UtilityMethods.Log("[FishingRod] Cast landed in shallow water. WaterVolume=" + GetTransformHierarchyPath(waterVolume.transform) + " Ground=" + GetTransformHierarchyPath(hitInfo.collider.transform) + " GroundCollider=" + hitInfo.collider.GetType().Name + " " + $"Depth={num2:0.00} " + $"RequiredDepth={num:0.00} " + $"SurfacePoint={surfacePoint}", LogType.Warning);
			}
			return CastLocationState.ShallowWater;
		}

		private static bool TryGetWaterPhysicsVolume(Collider collider, out PhysicsVolume waterVolume)
		{
			waterVolume = ((collider != null) ? collider.GetComponentInParent<PhysicsVolume>() : null);
			if (waterVolume != null && waterVolume.waterVolume && waterVolume.boxCollider != null)
			{
				return waterVolume.boxCollider.enabled;
			}
			return false;
		}

		private static string GetTransformHierarchyPath(Transform target)
		{
			if (target == null)
			{
				return "None";
			}
			string text = target.name;
			while (target.parent != null)
			{
				target = target.parent;
				text = target.name + "/" + text;
			}
			return text;
		}

		private static void ShowNoFishHereFeedback()
		{
			Singleton<NotifyManager>.Instance?.ShowAlert(LocalizationDatabase.GetString("UiTable", "gameplay.fishing.no_fish.header", "NO FISH HERE"), LocalizationDatabase.GetString("UiTable", "gameplay.fishing.no_fish.description", "There are no fish here."), 2f, AlertPriority.Normal, autoClose: true, usePriorityPulse: false);
		}

		private bool TrySelectFish(OceanLayer zone, out FishSO selectedFish, FishingRodLevelData level = null)
		{
			selectedFish = null;
			if ((object)level == null)
			{
				level = _currentLevel;
			}
			if (_itemDatabaseSO == null || level == null)
			{
				return false;
			}
			List<FishSO> list = (from fish in _itemDatabaseSO.GetAllOfType(ItemType.Creature).OfType<FishSO>()
				where fish != null && fish.FishingRodSettings != null && fish.FishingRodSettings.Enabled && fish.DeadPrefab != null && (fish.ZoneLayer & zone) != 0
				select fish).ToList();
			if (list.Count == 0)
			{
				return false;
			}
			float maxInclusive = list.Sum((FishSO fish) => GetFishSelectionWeight(fish, level));
			float num = UnityEngine.Random.Range(0f, maxInclusive);
			for (int num2 = 0; num2 < list.Count; num2++)
			{
				num -= GetFishSelectionWeight(list[num2], level);
				if (num <= 0f)
				{
					selectedFish = list[num2];
					return true;
				}
			}
			selectedFish = list[list.Count - 1];
			return true;
		}

		private float GetFishSelectionWeight(FishSO fish, FishingRodLevelData level)
		{
			FishingRodFishSettings fishingRodSettings = fish.FishingRodSettings;
			RodDifficultyMatchup matchup = level.GetMatchup(fishingRodSettings.Difficulty);
			return Mathf.Max(0.001f, fishingRodSettings.SelectionWeight * matchup.SelectionWeightMultiplier);
		}

		private bool IsPlayerStateInvalid()
		{
			if (!(_player == null) && !_player.IsTrapped && !_player.IsUsingEmoteArea)
			{
				return _player.DivingEquipment.IsKnocked;
			}
			return true;
		}

		internal void HandleOwnerWaterVolumeEntered()
		{
			if (!base.IsOwner || !base.ToolStatus)
			{
				return;
			}
			UtilityMethods.Log("[FishingRod] Water volume entered. " + string.Format("State={0} Session={1} Fish={2}", _state, _sessionId, _currentFish?.Name ?? "None"));
			if (_state == FishingSessionState.Success && _currentFish != null && !_collectRequested)
			{
				_collectRequested = true;
				if (base.IsSpawned)
				{
					_presentationCaughtFishId.Value = default;
					ServerReleaseCaughtFishRpc(_sessionId, new FixedString64Bytes(_currentFish.Id));
				}
				UtilityMethods.Log("[FishingRod] Caught fish escaped because the player entered water. " + $"Session={_sessionId} Fish={_currentFish.Name}");
			}
			ForceDisableTool();
			_player?.ClearToolIfActive(this);
		}

		private bool IsPlayerInWaterMovementMode()
		{
			if (_player == null)
			{
				return false;
			}
			if (_player.IsSwimming.Value)
			{
				return true;
			}
			if (_player.FirstPersonCharacter != null)
			{
				if (!_player.FirstPersonCharacter.IsSwimming())
				{
					return _player.FirstPersonCharacter.IsSeaScooter();
				}
				return true;
			}
			return false;
		}

		private void AcquireMovementPause()
		{
			if (!_ownsMovementPause)
			{
				_player.PauseMovement();
				_player.SetLocomotionAnimationSuppressed(suppressed: true);
				_player.SetCameraBobSuppressed(suppressed: true);
				_player.Sway?.SetMovementSwaySuppressed(suppressed: true);
				_ownsMovementPause = true;
			}
		}

		private void ReleaseMovementPause()
		{
			if (!_ownsMovementPause)
			{
				return;
			}
			_ownsMovementPause = false;
			if (!(_player == null))
			{
				_player.SetLocomotionAnimationSuppressed(suppressed: false);
				_player.SetCameraBobSuppressed(suppressed: false);
				_player.Sway?.SetMovementSwaySuppressed(suppressed: false);
				if (!_player.IsTrapped && !_player.IsUsingEmoteArea && !_player.DivingEquipment.IsKnocked)
				{
					_player.ResumeMovement(clearState: true);
				}
			}
		}

		private void AcquireChallengeLookPause()
		{
			if (!_ownsLookPause && !(_player == null))
			{
				_player.PauseLook();
				_ownsLookPause = true;
			}
		}

		private void ReleaseChallengeLookPause()
		{
			if (_ownsLookPause)
			{
				_ownsLookPause = false;
				if (!(_player == null) && !_player.IsTrapped && !_player.IsUsingEmoteArea && !_player.DivingEquipment.IsKnocked)
				{
					_player.ResumeLook();
				}
			}
		}

		private void RegisterLocalInput()
		{
			if (!_inputRegistered && !(Singleton<InputManager>.Instance == null))
			{
				Singleton<InputManager>.Instance.InputHandler.Look += OnLookInput;
				Singleton<InputManager>.Instance.InputHandler.Move += OnCounterMoveInput;
				_inputRegistered = true;
			}
		}

		private void UnregisterLocalInput()
		{
			if (_inputRegistered && !(Singleton<InputManager>.Instance == null))
			{
				Singleton<InputManager>.Instance.InputHandler.Look -= OnLookInput;
				Singleton<InputManager>.Instance.InputHandler.Move -= OnCounterMoveInput;
				_inputRegistered = false;
			}
		}

		private void OnLookInput(InputActionPhase phase, Vector2 value, bool isMouse)
		{
			if (base.IsOwner && _state == FishingSessionState.Challenge && !(_data == null))
			{
				if (!isMouse)
				{
					_stickCounterInput = ((phase == InputActionPhase.Canceled) ? Vector2.zero : value);
				}
				else if (phase == InputActionPhase.Performed)
				{
					_counterInput = Vector2.ClampMagnitude(_counterInput + value * _data.MouseCounterSensitivity * FishingRodPreferences.Sensitivity, 1f);
				}
			}
		}

		private void TickCounterInputDecay(float deltaTime)
		{
			if (_data != null)
			{
				_counterInput = Vector2.MoveTowards(_counterInput, Vector2.zero, _data.MouseCounterReturnSpeed * deltaTime * 0.55f);
			}
		}

		private FishingLineVisual CreateLineVisual(Transform lineOrigin, Vector3 localOriginOffset, string objectName)
		{
			FishingLineVisual fishingLineVisual = new GameObject(objectName).AddComponent<FishingLineVisual>();
			fishingLineVisual.Initialize(lineOrigin, localOriginOffset, _data.LineWidth, _data.LineSegmentCount);
			return fishingLineVisual;
		}

		private Vector3 ResolveCastOrigin(FishingRodVisual rodVisual)
		{
			if (rodVisual?.LineOrigin != null)
			{
				return rodVisual.LineOrigin.position;
			}
			if (!(_player != null))
			{
				return base.transform.position;
			}
			return _player.transform.position + Vector3.up * 1.2f;
		}

		private Vector3 EvaluateCastArc(Vector3 origin)
		{
			float num = Mathf.Clamp01(_castElapsed / Mathf.Max(0.05f, GetCastTravelDuration()));
			return Vector3.Lerp(origin, _castPoint, num) + Vector3.up * (4f * GetCastArcHeight() * num * (1f - num));
		}

		private void UpdateCastElapsedFromClock()
		{
			_castElapsed = Mathf.Clamp((float)(GetSynchronizedTime() - _castStartedAt), 0f, GetCastTravelDuration());
		}

		private float GetCastTravelDuration()
		{
			if (!base.IsOwner)
			{
				return Mathf.Max(0.05f, _presentationCastDuration.Value);
			}
			return Mathf.Max(0.05f, (_currentCastTravelDuration > 0f) ? _currentCastTravelDuration : _data.GetCastTravelDuration(_currentCastCharge));
		}

		private float GetCastArcHeight()
		{
			if (!base.IsOwner)
			{
				return Mathf.Max(0f, _presentationCastArcHeight.Value);
			}
			return Mathf.Max(0f, (_currentCastArcHeight > 0f) ? _currentCastArcHeight : _data.GetCastArcHeight(_currentCastCharge));
		}

		private double GetSynchronizedTime()
		{
			if (!base.IsSpawned || !(base.NetworkManager != null))
			{
				return Time.timeAsDouble;
			}
			return base.NetworkManager.ServerTime.Time;
		}

		private FishingRodVisual InitializeRodVisual(GameObject graphic, string viewName, bool useFpsAnimator)
		{
			FishingRodVisual component = graphic.GetComponent<FishingRodVisual>();
			if (component == null)
			{
				UtilityMethods.Log("[FishingRod] FishingRodVisual is missing from the " + viewName + " graphic prefab.", LogType.Error);
				return null;
			}
			component.Initialize(_data, _player, useFpsAnimator);
			return component;
		}

		private void EnsureReelIKController()
		{
			if (_player == null || _data == null)
			{
				return;
			}
			if (_reelIKController == null)
			{
				_reelIKController = GetComponent<FishingRodReelIKController>();
				if (_reelIKController == null)
				{
					_reelIKController = base.gameObject.AddComponent<FishingRodReelIKController>();
				}
			}
			_reelIKController.Initialize(_player, _data);
		}

		private static Transform ResolveLineOrigin(Transform graphic)
		{
			if (graphic == null)
			{
				return null;
			}
			Transform[] componentsInChildren = graphic.GetComponentsInChildren<Transform>(includeInactive: true);
			foreach (Transform transform in componentsInChildren)
			{
				if (transform.name == "Line_Tip")
				{
					return transform;
				}
			}
			UtilityMethods.Log("[FishingRod] Line_Tip was not found in the rod graphic. Falling back to the graphic root.", LogType.Warning);
			return graphic;
		}

		private void TickPresentationSync()
		{
			if (base.IsSpawned)
			{
				_presentationSyncRemaining -= Time.deltaTime;
				if (!(_presentationSyncRemaining > 0f))
				{
					_presentationSyncRemaining = 0.125f;
					_presentationTension.Value = _lineTension;
					_presentationReeling.Value = _isReeling;
				}
			}
		}

		private void PushCastPresentation()
		{
			if (base.IsSpawned)
			{
				_presentationCastPoint.Value = _castPoint;
				_presentationSessionId.Value = _sessionId;
				_presentationCastOrigin.Value = _tpsCastOrigin;
				_presentationCastStartTime.Value = _castStartedAt;
				_presentationCastDuration.Value = Mathf.Max(0.05f, _currentCastTravelDuration);
				_presentationCastArcHeight.Value = Mathf.Max(0f, _currentCastArcHeight);
				_presentationTension.Value = _lineTension;
				_presentationDirection.Value = (byte)_fishDirection;
				_presentationStrength.Value = _currentPresentationStrength;
			}
		}

		private void SetLocalState(FishingSessionState state)
		{
			bool flag = _state == FishingSessionState.Challenge;
			bool flag2 = _state == FishingSessionState.Waiting;
			_state = state;
			bool flag3 = state == FishingSessionState.Challenge;
			if (!flag3)
			{
				SetFishingRodSteerAnimation(FishingRodSteerDirection.None);
			}
			if ((base.IsOwner && !flag) & flag3)
			{
				StartFishingSway();
				AcquireChallengeLookPause();
			}
			else if ((base.IsOwner & flag) && !flag3)
			{
				_player?.FirstPersonCharacter?.StopLookAt();
				StopFishingSway();
				ReleaseChallengeLookPause();
			}
			if (base.IsOwner && base.IsSpawned)
			{
				_presentationState.Value = (byte)state;
			}
			if (base.IsOwner && !flag2 && state == FishingSessionState.Waiting && _castLocationState == CastLocationState.FishableWater)
			{
				ShowWaitingAlert();
			}
			else if ((base.IsOwner & flag2) && state != FishingSessionState.Waiting)
			{
				HideWaitingAlert();
			}
			if (base.IsOwner && base.ToolStatus)
			{
				RefreshFishingRodHints();
				RefreshCloseToolHint();
			}
			RefreshIdleBobberPresentation();
		}

		private void ShowWaitingAlert()
		{
			_activeWaitingAlertHeader = LocalizationDatabase.GetString("UiTable", "gameplay.fishing.waiting.header", "LINE IN THE WATER");
			_activeWaitingAlertDescription = LocalizationDatabase.GetString("UiTable", "gameplay.fishing.waiting.description", "Waiting for a bite.");
			Singleton<NotifyManager>.Instance?.ShowAlert(_activeWaitingAlertHeader, _activeWaitingAlertDescription, -1f, AlertPriority.Normal, autoClose: false, usePriorityPulse: false);
		}

		private void HideWaitingAlert()
		{
			Singleton<NotifyManager>.Instance?.TryHideAlert(string.IsNullOrEmpty(_activeWaitingAlertHeader) ? "LINE IN THE WATER" : _activeWaitingAlertHeader, string.IsNullOrEmpty(_activeWaitingAlertDescription) ? "Waiting for a bite." : _activeWaitingAlertDescription);
			_activeWaitingAlertHeader = null;
			_activeWaitingAlertDescription = null;
		}

		private void StartChallengeLookAtBobber()
		{
			if (base.IsOwner && !FishingRodPreferences.ReducedMotion && !(_player?.FirstPersonCharacter == null) && !(_data == null))
			{
				_player.FirstPersonCharacter.LookAtOverTime(_castPoint, Mathf.Max(0f, _data.ChallengeLookAtDuration));
			}
		}

		private void StartFishingSway()
		{
			if (!FishingRodPreferences.ReducedMotion && !_fishingSwayActive && !(_player?.Sway == null) && !(_data == null))
			{
				_player.Sway.StartDirectionalSway(_data.FishingSwayWeight, _data.FishingIdleSwayWeight);
				_fishingSwayActive = true;
			}
		}

		private void UpdateFishingSway()
		{
			if (_fishingSwayActive && !(_player?.Sway == null) && !(_data == null))
			{
				float num = Mathf.Clamp(EffectiveCounterInput.x, -1f, 1f);
				float num2 = Mathf.Abs(num);
				float num3 = Mathf.Clamp01(EffectiveCounterInput.y);
				float num4 = num2 + num3;
				if (num4 > 1f)
				{
					num /= num4;
					num2 /= num4;
					num3 /= num4;
				}
				Vector3 fishingHorizontalSwayPosition = _data.FishingHorizontalSwayPosition;
				Vector3 positionTarget = new Vector3(fishingHorizontalSwayPosition.x * num, fishingHorizontalSwayPosition.y * num2, fishingHorizontalSwayPosition.z * num2);
				Vector3 fishingHorizontalSwayRotation = _data.FishingHorizontalSwayRotation;
				Vector3 rotationTarget = new Vector3(fishingHorizontalSwayRotation.x * num2, fishingHorizontalSwayRotation.y * num, fishingHorizontalSwayRotation.z * num);
				positionTarget += _data.FishingBackwardSwayPosition * num3;
				rotationTarget += _data.FishingBackwardSwayRotation * num3;
				AddFishingStruggleSway(ref positionTarget, ref rotationTarget);
				_player.Sway.SetDirectionalSwayTarget(positionTarget, rotationTarget, _data.FishingSwaySmoothTime);
			}
		}

		private void AddFishingStruggleSway(ref Vector3 positionTarget, ref Vector3 rotationTarget)
		{
			if (_steerDirection != FishingRodSteerDirection.None && !(_data == null))
			{
				float num;
				switch (_steerDirection)
				{
				case FishingRodSteerDirection.Left:
				case FishingRodSteerDirection.Right:
					num = Mathf.Abs(EffectiveCounterInput.x);
					break;
				case FishingRodSteerDirection.Back:
					num = Mathf.Clamp01(EffectiveCounterInput.y);
					break;
				default:
					num = 0f;
					break;
				}
				float value = num;
				float a = Mathf.Clamp01(_data.MouseCounterThreshold);
				float num2 = Mathf.Lerp(0.65f, 1f, Mathf.InverseLerp(a, 1f, value));
				float phase = Time.time * Mathf.Max(0.01f, _data.FishingStruggleSwayFrequency);
				Vector3 b = new Vector3(GetSignedPerlinNoise(phase, 0.17f), GetSignedPerlinNoise(phase, 1.31f), GetSignedPerlinNoise(phase, 2.73f));
				Vector3 b2 = new Vector3(GetSignedPerlinNoise(phase, 3.47f), GetSignedPerlinNoise(phase, 4.89f), GetSignedPerlinNoise(phase, 6.11f));
				positionTarget += Vector3.Scale(_data.FishingStruggleSwayPosition, b) * num2;
				rotationTarget += Vector3.Scale(_data.FishingStruggleSwayRotation, b2) * num2;
			}
		}

		private static float GetSignedPerlinNoise(float phase, float seed)
		{
			return Mathf.PerlinNoise(phase, seed) * 2f - 1f;
		}

		private void StopFishingSway()
		{
			if (_fishingSwayActive)
			{
				if (_player?.Sway != null)
				{
					_player.Sway.StopScriptedSway();
				}
				_fishingSwayActive = false;
			}
		}

		private void UpdateLineAndRodPresentation()
		{
			FishingSessionState fishingSessionState = (base.IsOwner ? _state : ((FishingSessionState)_presentationState.Value));
			FishingRodFishDirection fishingRodFishDirection = (base.IsOwner ? _fishDirection : ((FishingRodFishDirection)_presentationDirection.Value));
			Vector3 vector = (base.IsOwner ? _castPoint : _presentationCastPoint.Value);
			float normalizedTension = (base.IsOwner ? _lineTension : _presentationTension.Value);
			bool flag = (base.IsOwner ? _isReeling : _presentationReeling.Value);
			float num = (base.IsOwner ? _currentPresentationStrength : _presentationStrength.Value);
			bool flag2 = fishingSessionState == FishingSessionState.Challenge;
			float caughtFishWeightBend = ((fishingSessionState == FishingSessionState.Success) ? _data.GetSuccessWeightBend(base.IsOwner ? _caughtWeight : _presentationCaughtWeight.Value) : 0f);
			_reelIKController?.SetReeling(flag2 & flag);
			bool visible = fishingSessionState == FishingSessionState.Casting || fishingSessionState == FishingSessionState.Waiting || fishingSessionState == FishingSessionState.Challenge;
			bool flag3 = (fishingSessionState == FishingSessionState.Waiting || fishingSessionState == FishingSessionState.Challenge) && _presentationBobber != null;
			Vector3 vector2 = ((!flag2) ? Vector3.zero : (fishingRodFishDirection switch
			{
				FishingRodFishDirection.Left => -_player.transform.right * _data.MaxHorizontalLinePull * num, 
				FishingRodFishDirection.Right => _player.transform.right * _data.MaxHorizontalLinePull * num, 
				FishingRodFishDirection.Forward => _player.transform.forward * _data.MaxForwardLinePull * num, 
				_ => Vector3.zero, 
			}));
			Vector3 b = vector2;
			float num2 = ((b.sqrMagnitude > 0.0001f) ? _data.LineDirectionTweenResponse : _data.LineReturnTweenResponse);
			_currentLineDirectionOffset = Vector3.Lerp(_currentLineDirectionOffset, b, 1f - Mathf.Exp((0f - num2) * Time.deltaTime));
			if (flag3)
			{
				if (fishingSessionState != FishingSessionState.Waiting)
				{
					_presentationBobber.SetPresentationOffset(_currentLineDirectionOffset);
				}
				vector = _presentationBobber.LineAttachmentPosition;
			}
			if (fishingSessionState == FishingSessionState.Casting)
			{
				_fpsLineVisual?.SetLine(EvaluateCastArc(_fpsCastOrigin), 0f, visible: true);
				_tpsLineVisual?.SetLine(EvaluateCastArc(_tpsCastOrigin), 0f, visible: true);
			}
			else
			{
				Vector3 targetPosition = (flag3 ? vector : (vector + _currentLineDirectionOffset));
				_fpsLineVisual?.SetLine(targetPosition, normalizedTension, visible);
				_tpsLineVisual?.SetLine(targetPosition, normalizedTension, visible);
			}
			FishingRodFishDirection fishingRodFishDirection2 = SwapHorizontalRodVisualDirection(fishingRodFishDirection);
			_fpsRodVisual?.SetPresentation(normalizedTension, flag, flag2, num, fishingRodFishDirection2, caughtFishWeightBend);
			_tpsRodVisual?.SetPresentation(normalizedTension, flag, flag2, num, fishingRodFishDirection2, caughtFishWeightBend);
			ApplyRodDirection(_fpsGraphic, _data.OffsetFps, (fishingSessionState == FishingSessionState.Success) ? _data.SuccessOffsetFps : null, fishingRodFishDirection2, fishingSessionState, num, _data.RodHorizontalTipEuler, _data.RodForwardTipEuler);
			ApplyRodDirection(_tpsGraphic, _data.OffsetTps, null, fishingRodFishDirection2, fishingSessionState, num, _data.RodHorizontalTipEuler, _data.RodForwardTipEuler);
			if (fishingSessionState == FishingSessionState.Success)
			{
				ApplyCaughtFishGraphicOffset();
			}
		}

		private static FishingRodFishDirection SwapHorizontalRodVisualDirection(FishingRodFishDirection direction)
		{
			return direction switch
			{
				FishingRodFishDirection.Left => FishingRodFishDirection.Right, 
				FishingRodFishDirection.Right => FishingRodFishDirection.Left, 
				_ => direction, 
			};
		}

		private static void ApplyRodDirection(GameObject graphic, ItemOffset baseOffset, ItemOffset stateOffset, FishingRodFishDirection direction, FishingSessionState state, float presentationStrength, Vector3 horizontalTipEuler, Vector3 forwardTipEuler)
		{
			if (!(graphic == null))
			{
				Vector3 vector = stateOffset?.PositionOffset ?? Vector3.zero;
				Vector3 vector2 = stateOffset?.RotationOffset ?? Vector3.zero;
				Vector3 b = stateOffset?.ScaleOffset ?? Vector3.one;
				Vector3 vector3 = FishingRodVisual.ResolveRootDirectionEuler(direction, state == FishingSessionState.Challenge, presentationStrength, horizontalTipEuler, forwardTipEuler);
				Quaternion b2 = Quaternion.Euler(baseOffset.RotationOffset + vector2 + vector3);
				graphic.transform.localPosition = Vector3.Lerp(graphic.transform.localPosition, baseOffset.PositionOffset + vector, 1f - Mathf.Exp(-12f * Time.deltaTime));
				graphic.transform.localRotation = Quaternion.Slerp(graphic.transform.localRotation, b2, 1f - Mathf.Exp(-12f * Time.deltaTime));
				graphic.transform.localScale = Vector3.Lerp(graphic.transform.localScale, Vector3.Scale(baseOffset.ScaleOffset, b), 1f - Mathf.Exp(-12f * Time.deltaTime));
			}
		}

		private void SpawnCaughtFishGraphic()
		{
			DestroyCaughtFishGraphic();
			FishSO currentFish = _currentFish;
			if (!base.IsOwner || currentFish?.DeadGraphic == null)
			{
				UtilityMethods.Log("[FishingRod] Caught fish preview could not be created. Fish=" + (currentFish?.Name ?? "None"), LogType.Warning);
				return;
			}
			if (_fpsRodVisual?.LineOrigin != null)
			{
				GameObject gameObject = new GameObject("CaughtFishCableVisual_FPS");
				_caughtFishCableVisual = gameObject.AddComponent<FishingRodHangingCableVisual>();
				if (!_caughtFishCableVisual.Initialize(_fpsRodVisual.LineOrigin, currentFish, _data, "ViewModel"))
				{
					UnityEngine.Object.Destroy(gameObject);
					_caughtFishCableVisual = null;
				}
				else
				{
					_caughtFishGraphic = _caughtFishCableVisual.Graphic;
				}
			}
			TrySpawnTpsCaughtFishGraphic(currentFish);
			if (_caughtFishCableVisual == null && _tpsCaughtFishCableVisual == null)
			{
				UtilityMethods.Log("[FishingRod] No valid FPS or TPS rod tip was available for the caught fish preview.", LogType.Warning);
			}
		}

		private void RefreshRemoteCaughtFishGraphic()
		{
			if (!base.IsOwner && _state == FishingSessionState.Success && !(_tpsCaughtFishCableVisual != null) && _presentationCaughtFishId.Value.Length != 0 && _itemDatabaseSO?.FindById(_presentationCaughtFishId.Value) is FishSO fishSO && !(fishSO.DeadGraphic == null) && !(_tpsRodVisual?.LineOrigin == null))
			{
				TrySpawnTpsCaughtFishGraphic(fishSO);
			}
		}

		private bool TrySpawnTpsCaughtFishGraphic(FishSO fish)
		{
			if (_tpsCaughtFishCableVisual != null || fish?.DeadGraphic == null || _tpsRodVisual?.LineOrigin == null || _data == null)
			{
				return false;
			}
			GameObject gameObject = new GameObject("CaughtFishCableVisual_TPS");
			_tpsCaughtFishCableVisual = gameObject.AddComponent<FishingRodHangingCableVisual>();
			string text = LayerMask.LayerToName(_tpsRodVisual.gameObject.layer);
			if (string.IsNullOrWhiteSpace(text))
			{
				text = "Default";
			}
			if (!_tpsCaughtFishCableVisual.Initialize(_tpsRodVisual.LineOrigin, fish, _data, text))
			{
				UnityEngine.Object.Destroy(gameObject);
				_tpsCaughtFishCableVisual = null;
				return false;
			}
			return true;
		}

		private void ApplyCaughtFishGraphicOffset()
		{
			if (!(_data == null))
			{
				_caughtFishCableVisual?.ApplyTuning(_data);
				_tpsCaughtFishCableVisual?.ApplyTuning(_data);
			}
		}

		private void DestroyCaughtFishGraphic()
		{
			if (_caughtFishCableVisual != null)
			{
				UnityEngine.Object.Destroy(_caughtFishCableVisual.gameObject);
			}
			else if (_caughtFishGraphic != null)
			{
				UnityEngine.Object.Destroy(_caughtFishGraphic);
			}
			_caughtFishCableVisual = null;
			_caughtFishGraphic = null;
			if (_tpsCaughtFishCableVisual != null)
			{
				UnityEngine.Object.Destroy(_tpsCaughtFishCableVisual.gameObject);
			}
			_tpsCaughtFishCableVisual = null;
		}

		private void RefreshIdleBobberPresentation()
		{
			if (base.ToolStatus)
			{
				FishingSessionState state = _state;
				if ((state == FishingSessionState.Ready || state == FishingSessionState.Charging) && _data?.IdleBobberGraphic != null)
				{
					if (_tpsIdleBobberCableVisual == null && _tpsRodVisual?.LineOrigin != null)
					{
						TryCreateIdleBobberPresentation(_tpsRodVisual, "IdleBobberCableVisual_TPS", out _tpsIdleBobberCableVisual);
					}
					if (base.IsOwner && _fpsIdleBobberCableVisual == null && _fpsRodVisual?.LineOrigin != null)
					{
						TryCreateIdleBobberPresentation(_fpsRodVisual, "IdleBobberCableVisual_FPS", out _fpsIdleBobberCableVisual);
					}
					return;
				}
			}
			DestroyIdleBobberPresentation();
		}

		private bool TryCreateIdleBobberPresentation(FishingRodVisual rodVisual, string objectName, out FishingRodHangingCableVisual cableVisual)
		{
			cableVisual = null;
			if (rodVisual?.LineOrigin == null || _data?.IdleBobberGraphic == null)
			{
				return false;
			}
			string text = LayerMask.LayerToName(rodVisual.gameObject.layer);
			if (string.IsNullOrWhiteSpace(text))
			{
				text = "Default";
			}
			GameObject gameObject = new GameObject(objectName);
			FishingRodHangingCableVisual fishingRodHangingCableVisual = gameObject.AddComponent<FishingRodHangingCableVisual>();
			if (!fishingRodHangingCableVisual.Initialize(rodVisual.LineOrigin, _data.IdleBobberGraphic, _data.IdleBobberGraphicOffset, _data, text, objectName.Replace("CableVisual", "Graphic")))
			{
				UnityEngine.Object.Destroy(gameObject);
				return false;
			}
			cableVisual = fishingRodHangingCableVisual;
			return true;
		}

		private void DestroyIdleBobberPresentation()
		{
			if (_fpsIdleBobberCableVisual != null)
			{
				UnityEngine.Object.Destroy(_fpsIdleBobberCableVisual.gameObject);
			}
			if (_tpsIdleBobberCableVisual != null)
			{
				UnityEngine.Object.Destroy(_tpsIdleBobberCableVisual.gameObject);
			}
			_fpsIdleBobberCableVisual = null;
			_tpsIdleBobberCableVisual = null;
		}

		private void ApplyRemotePresentation()
		{
			if (!base.IsOwner)
			{
				_state = (FishingSessionState)_presentationState.Value;
				_fishDirection = (FishingRodFishDirection)_presentationDirection.Value;
				_castPoint = _presentationCastPoint.Value;
				_tpsCastOrigin = _presentationCastOrigin.Value;
				_castStartedAt = _presentationCastStartTime.Value;
				if (_state == FishingSessionState.Casting)
				{
					UpdateCastElapsedFromClock();
				}
				if (_player != null)
				{
					SetFishingRodReelAnimation(_presentationReeling.Value);
					SetFishingRodSteerAnimation((FishingRodSteerDirection)((_state == FishingSessionState.Challenge && !_presentationReeling.Value) ? _presentationSteerDirection.Value : 0));
				}
				if (_state == FishingSessionState.Challenge && _presentationReeling.Value)
				{
					StartReelLoop();
				}
				else
				{
					StopOwnedLoop(ref _reelLoopHandle);
				}
				if (_state == FishingSessionState.Success)
				{
					RefreshRemoteCaughtFishGraphic();
				}
				else if (_tpsCaughtFishCableVisual != null)
				{
					UnityEngine.Object.Destroy(_tpsCaughtFishCableVisual.gameObject);
					_tpsCaughtFishCableVisual = null;
				}
				UpdateLineAndRodPresentation();
				RefreshIdleBobberPresentation();
			}
		}

		private void ResetPresentationVisuals()
		{
			_currentLineDirectionOffset = Vector3.zero;
			_fpsLineVisual?.ResetLine();
			_tpsLineVisual?.ResetLine();
			_fpsRodVisual?.SetPresentation(0f, reeling: false, challengeActive: false, 1f, FishingRodFishDirection.Centered);
			_tpsRodVisual?.SetPresentation(0f, reeling: false, challengeActive: false, 1f, FishingRodFishDirection.Centered);
			SetFishingRodReelAnimation(reeling: false);
			SetFishingRodSteerAnimation(FishingRodSteerDirection.None);
		}

		private void OnPresentationStateChanged(byte oldValue, byte newValue)
		{
			if (!base.IsOwner)
			{
				FishingSessionState fishingSessionState = (FishingSessionState)oldValue;
				FishingSessionState fishingSessionState2 = (_state = (FishingSessionState)newValue);
				if (fishingSessionState2 == FishingSessionState.Charging && _player != null)
				{
					BeginFishingRodCastAnimation();
				}
				if (fishingSessionState2 == FishingSessionState.Casting && _player != null)
				{
					EndFishingRodCastAnimation();
					_castStartedAt = _presentationCastStartTime.Value;
					_tpsCastOrigin = _presentationCastOrigin.Value;
					UpdateCastElapsedFromClock();
					PlayRemoteRodSfx(_data.CastSfx, _player.transform.position);
				}
				if (fishingSessionState2 == FishingSessionState.Waiting && fishingSessionState == FishingSessionState.Casting)
				{
					PlayRemoteRodSfx(_data.CastWaterSplashSfx, _presentationCastPoint.Value);
				}
				if ((fishingSessionState2 == FishingSessionState.Failure || fishingSessionState2 == FishingSessionState.CanceledFailure) && _player != null)
				{
					TriggerFishingRodFailAnimation();
					if (fishingSessionState2 == FishingSessionState.Failure)
					{
						PlayRemoteRodSfx(_data.LineBreakSfx, _player.transform.position);
						PlayRemoteRodSfx(_data.FishSplashSfx, _presentationCastPoint.Value);
					}
				}
				if (fishingSessionState2 != FishingSessionState.Challenge)
				{
					StopOwnedLoop(ref _reelLoopHandle);
				}
			}
			ApplyRemotePresentation();
			if (!base.IsOwner && !IsFishingActive)
			{
				ApplyPendingLevel();
			}
		}

		private void OnPresentationDirectionChanged(byte oldValue, byte newValue)
		{
			if (!base.IsOwner)
			{
				_fishDirection = (FishingRodFishDirection)newValue;
				if (_state == FishingSessionState.Challenge)
				{
					FishingRodFishDirection fishDirection = _fishDirection;
					if (fishDirection == FishingRodFishDirection.Left || fishDirection == FishingRodFishDirection.Right || fishDirection == FishingRodFishDirection.Forward)
					{
						PlayRemoteRodSfx(_data.FishSplashSfx, _presentationCastPoint.Value);
					}
				}
			}
			ApplyRemotePresentation();
		}

		private void OnPresentationSteerDirectionChanged(byte oldValue, byte newValue)
		{
			ApplyRemotePresentation();
		}

		private void OnPresentationSessionChanged(uint oldValue, uint newValue)
		{
			if (!base.IsOwner && oldValue != newValue)
			{
				_castStartedAt = _presentationCastStartTime.Value;
				_tpsCastOrigin = _presentationCastOrigin.Value;
			}
			ApplyRemotePresentation();
		}

		private void OnPresentationCastOriginChanged(Vector3 oldValue, Vector3 newValue)
		{
			if (!base.IsOwner)
			{
				_tpsCastOrigin = newValue;
			}
			ApplyRemotePresentation();
		}

		private void OnPresentationCastStartTimeChanged(double oldValue, double newValue)
		{
			if (!base.IsOwner)
			{
				_castStartedAt = newValue;
			}
			ApplyRemotePresentation();
		}

		private void OnPresentationCastDurationChanged(float oldValue, float newValue)
		{
			ApplyRemotePresentation();
		}

		private void OnPresentationCastPointChanged(Vector3 oldValue, Vector3 newValue)
		{
			if (!base.IsOwner)
			{
				_castPoint = newValue;
			}
			ApplyRemotePresentation();
		}

		private void OnPresentationTensionChanged(float oldValue, float newValue)
		{
			ApplyRemotePresentation();
		}

		private void OnPresentationStrengthChanged(float oldValue, float newValue)
		{
			ApplyRemotePresentation();
		}

		private void OnPresentationReelingChanged(bool oldValue, bool newValue)
		{
			ApplyRemotePresentation();
		}

		private void OnPresentationStrugglingChanged(bool oldValue, bool newValue)
		{
			if ((!base.IsOwner && !oldValue) & newValue)
			{
				PlayRemoteRodSfx(_data.ReelStruggleSfx, (_player != null) ? _player.transform.position : base.transform.position);
			}
		}

		private void OnPresentationCaughtFishChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
		{
			if (base.IsOwner)
			{
				return;
			}
			if (newValue.Length == 0)
			{
				if (_tpsCaughtFishCableVisual != null)
				{
					UnityEngine.Object.Destroy(_tpsCaughtFishCableVisual.gameObject);
				}
				_tpsCaughtFishCableVisual = null;
			}
			else
			{
				RefreshRemoteCaughtFishGraphic();
			}
		}

		private void PlayRemoteRodSfx(AudioClipSO sfx, Vector3 position)
		{
			if (!base.IsOwner && !(sfx == null) && !(Singleton<SoundManager>.Instance == null))
			{
				AudioSource source = Singleton<SoundManager>.Instance.PlaySFX(sfx, position);
				ConfigureRemoteRodAudioSource(source, position);
			}
		}

		private void ApplyPendingLevel()
		{
			if (_pendingLevel >= 0)
			{
				byte newLevel = (byte)_pendingLevel;
				_pendingLevel = -1;
				ApplyLevel(newLevel);
			}
		}

		private void ResetFishingRodAnimator()
		{
			if (!(_player == null))
			{
				SetFishingRodReelAnimation(reeling: false);
				SetFishingRodSteerAnimation(FishingRodSteerDirection.None);
				_player.AnimSetBool(PPG_Player.FISHING_ROD_CAST_HOLD, value: false);
				_player.AnimResetTrigger(PPG_Player.FISHING_ROD_CAST_END);
				_player.AnimResetTrigger(PPG_Player.FISHING_ROD_FAIL);
				SetFishingRodSuccessAnimation(active: false);
			}
		}

		private void SetFishingRodEquippedAnimation(bool equipped)
		{
			if (!(_player == null))
			{
				_player.AnimSetBool(PPG_Player.FISHING_ROD, equipped);
			}
		}

		private void SetFishingRodReelAnimation(bool reeling)
		{
			if (!(_player == null))
			{
				_player.AnimSetBool(PPG_Player.FISHING_ROD_REEL, reeling);
			}
		}

		private void UpdateFishingRodSteerAnimation()
		{
			FishingRodSteerDirection fishingRodSteerAnimation = FishingRodSteerDirection.None;
			if (_state == FishingSessionState.Challenge && !_isReeling && IsCorrectCounterInput())
			{
				fishingRodSteerAnimation = _fishDirection switch
				{
					FishingRodFishDirection.Left => FishingRodSteerDirection.Right, 
					FishingRodFishDirection.Right => FishingRodSteerDirection.Left, 
					FishingRodFishDirection.Forward => FishingRodSteerDirection.Back, 
					_ => FishingRodSteerDirection.None, 
				};
			}
			SetFishingRodSteerAnimation(fishingRodSteerAnimation);
		}

		private void SetFishingRodSteerAnimation(FishingRodSteerDirection direction)
		{
			_steerDirection = direction;
			bool flag = direction != FishingRodSteerDirection.None;
			_player?.AnimSetBool(PPG_Player.FISHING_ROD_STEER_ACTIVE, flag);
			if (flag)
			{
				_player?.AnimSetFloat(PPG_Player.FISHING_ROD_STEER, (int)direction);
			}
			if (base.IsOwner && base.IsSpawned && (uint)_presentationSteerDirection.Value != (uint)direction)
			{
				_presentationSteerDirection.Value = (byte)direction;
			}
		}

		private void BeginFishingRodCastAnimation()
		{
			if (!(_player == null))
			{
				_player.AnimResetTrigger(PPG_Player.FISHING_ROD_CAST_END);
				_player.AnimSetBool(PPG_Player.FISHING_ROD_CAST_HOLD, value: true);
			}
		}

		private void EndFishingRodCastAnimation()
		{
			if (!(_player == null))
			{
				_player.AnimSetBool(PPG_Player.FISHING_ROD_CAST_HOLD, value: false);
				_player.AnimResetTrigger(PPG_Player.FISHING_ROD_CAST_END);
				_player.AnimSetTrigger(PPG_Player.FISHING_ROD_CAST_END);
			}
		}

		private void CancelFishingRodCastAnimation()
		{
			if (!(_player == null))
			{
				_player.AnimSetBool(PPG_Player.FISHING_ROD_CAST_HOLD, value: false);
				_player.AnimResetTrigger(PPG_Player.FISHING_ROD_CAST_END);
			}
		}

		private void TriggerFishingRodFailAnimation()
		{
			if (!(_player == null))
			{
				_player.AnimResetTrigger(PPG_Player.FISHING_ROD_FAIL);
				_player.AnimSetTrigger(PPG_Player.FISHING_ROD_FAIL);
			}
		}

		private void SetFishingRodSuccessAnimation(bool active)
		{
			_player?.SetFishingRodSuccessAnimation(active);
		}

		private void OnApplicationFocus(bool hasFocus)
		{
			if (!hasFocus && base.IsOwner)
			{
				_counterInput = (_stickCounterInput = (_moveCounterInput = Vector2.zero));
				ResetFishingCancelHold();
				if (_state == FishingSessionState.Charging)
				{
					EndFishingRodCastAnimation();
					CancelFishingSession(FishingCancelReason.PlayerStateChanged, returnToReady: true, releaseMovement: false);
				}
				else if (_state == FishingSessionState.Challenge)
				{
					SetReeling(reeling: false);
					ServerFishingInputRpc(_sessionId, Vector2.zero, reeling: false);
				}
			}
		}

		private void OnApplicationPause(bool paused)
		{
			if (paused)
			{
				OnApplicationFocus(hasFocus: false);
			}
		}

		private void ServerSpawnBobber(uint sessionId, Vector3 castPoint)
		{
			if (base.IsServer && ServerHasSession(sessionId) && _serverPhase == ServerFishingPhase.Waiting && sessionId != 0 && !(_bobberPrefab == null) && !(_player == null) && IsFinite(castPoint))
			{
				FishingRodLevelData fishingRodLevelData = _data?.GetLevel(Level);
				if (!(fishingRodLevelData == null) && !(Vector3.ProjectOnPlane(castPoint - _player.transform.position, Vector3.up).magnitude > _data.GetMaximumCastDistance(fishingRodLevelData) + 1f))
				{
					ServerDespawnActiveBobber();
					_activeBobber = UnityEngine.Object.Instantiate(_bobberPrefab, castPoint, Quaternion.identity);
					_serverActiveBobberSessionId = sessionId;
					_activeBobber.InitializeServer(this, base.OwnerClientId, sessionId);
					_activeBobber.NetworkObject.Spawn(destroyWithScene: true);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerCloseBobberRpc(uint sessionId, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2113037881u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sessionId);
				__endSendRpc(ref bufferWriter, 2113037881u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && rpcParams.Receive.SenderClientId == base.OwnerClientId && sessionId != 0 && sessionId == _serverActiveBobberSessionId)
				{
					ServerDespawnActiveBobber();
				}
			}
		}

		internal bool ServerTryPullBobber(FishingRodBobber bobber, uint sessionId, ulong senderClientId)
		{
			if (!base.IsServer)
			{
				return RejectBobberPull(senderClientId, sessionId, "NotServer");
			}
			if (!ServerCanPullBobber(sessionId, GetSynchronizedTime()))
			{
				return RejectBobberPull(senderClientId, sessionId, "BiteAlreadyStarted");
			}
			if (senderClientId == base.OwnerClientId)
			{
				return RejectBobberPull(senderClientId, sessionId, "RodOwner");
			}
			if (bobber == null || bobber != _activeBobber)
			{
				return RejectBobberPull(senderClientId, sessionId, "InactiveBobber");
			}
			if (sessionId == 0 || sessionId != _serverActiveBobberSessionId)
			{
				return RejectBobberPull(senderClientId, sessionId, "SessionMismatch");
			}
			if (!TryResolveServerInteractor(senderClientId, out var interactor))
			{
				return RejectBobberPull(senderClientId, sessionId, "InteractorUnavailable");
			}
			if (_data == null || _player == null)
			{
				return RejectBobberPull(senderClientId, sessionId, "RodRuntimeUnavailable");
			}
			Vector3 position = interactor.Player.transform.position;
			float num = Vector3.Distance(position, bobber.transform.position);
			if (num > _data.BobberInteractionRange)
			{
				return RejectBobberPull(senderClientId, sessionId, $"OutOfRange Distance={num:F2} " + $"Range={_data.BobberInteractionRange:F2}");
			}
			Vector3 vector = _player.transform.position - position;
			float magnitude = Vector3.ProjectOnPlane(vector, Vector3.up).magnitude;
			Vector3 vector2 = Vector3.ProjectOnPlane(vector, Vector3.up);
			if (vector2.sqrMagnitude < 0.0001f)
			{
				vector2 = _player.transform.forward;
			}
			Vector3 normalized = (vector2.normalized + Vector3.up * _data.BobberLaunchUpwardBias).normalized;
			float num2 = Mathf.Clamp(_data.BobberLaunchMinimumImpulse + magnitude * _data.BobberLaunchImpulsePerMeter, _data.BobberLaunchMinimumImpulse, Mathf.Max(_data.BobberLaunchMinimumImpulse, _data.BobberLaunchMaximumImpulse));
			NetworkObjectReference interactorReference = interactor.NetworkObject.ToReference();
			ServerAbortFishing();
			ClientResetFromBobberPullRpc(sessionId, interactor.Player.PlayerName.Value, base.RpcTarget.Single(base.OwnerClientId, RpcTargetUse.Temp));
			ClientApplyBobberLaunchRpc(interactorReference, normalized, num2, base.RpcTarget.Single(interactor.OwnerClientId, RpcTargetUse.Temp));
			UtilityMethods.Log("[FishingRod.Bobber] Pull accepted. " + $"RodOwner={base.OwnerClientId} Interactor={senderClientId} " + $"Session={sessionId} Distance={num:F2} " + $"Impulse={num2:F2}");
			return true;
		}

		private bool TryResolveServerInteractor(ulong clientId, out Interactor interactor)
		{
			interactor = null;
			if (base.NetworkManager == null || !base.NetworkManager.ConnectedClients.TryGetValue(clientId, out var value) || value?.PlayerObject == null || !value.PlayerObject.TryGetComponent<Interactor>(out interactor) || interactor.Player == null || interactor.OwnerClientId != clientId)
			{
				interactor = null;
				return false;
			}
			return true;
		}

		private bool RejectBobberPull(ulong senderClientId, uint sessionId, string reason)
		{
			UtilityMethods.Log("[FishingRod.Bobber] Pull rejected. " + $"Reason={reason} RodOwner={base.OwnerClientId} " + $"Interactor={senderClientId} Session={sessionId} " + $"ActiveSession={_serverActiveBobberSessionId}", LogType.Warning);
			return false;
		}

		private void ServerDespawnActiveBobber()
		{
			if (base.IsServer)
			{
				FishingRodBobber activeBobber = _activeBobber;
				_activeBobber = null;
				_serverActiveBobberSessionId = 0u;
				if (activeBobber != null && activeBobber.NetworkObject != null && activeBobber.NetworkObject.IsSpawned)
				{
					activeBobber.NetworkObject.Despawn();
				}
			}
		}

		internal bool TryRegisterPresentationBobber(FishingRodBobber bobber, ulong rodOwnerClientId, uint sessionId)
		{
			if (bobber == null || rodOwnerClientId != base.OwnerClientId || sessionId == 0)
			{
				return false;
			}
			uint num = (base.IsOwner ? _sessionId : _presentationSessionId.Value);
			if (num != 0 && num != sessionId)
			{
				return false;
			}
			_presentationBobber = bobber;
			_presentationBobberSessionId = sessionId;
			return true;
		}

		internal void UnregisterPresentationBobber(FishingRodBobber bobber, uint sessionId)
		{
			if (!(_presentationBobber != bobber) && _presentationBobberSessionId == sessionId)
			{
				_presentationBobber = null;
				_presentationBobberSessionId = 0u;
			}
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientResetFromBobberPullRpc(uint sessionId, FixedString64Bytes playerName, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1840037164u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sessionId);
				bufferWriter.WriteValueSafe(in playerName, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 1840037164u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsOwner && sessionId == _sessionId)
			{
				FishingSessionState state = _state;
				if (state == FishingSessionState.Waiting || state == FishingSessionState.Challenge || state == FishingSessionState.Casting)
				{
					ShowRodNotice(FishingRodText.Get("pulled", "Your float was pulled by {0}.", "Şamandıranı {0} çekti.", playerName.ToString()));
					CancelFishingSession(FishingCancelReason.BobberPulled, returnToReady: true, releaseMovement: true);
				}
			}
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientApplyBobberLaunchRpc(NetworkObjectReference interactorReference, Vector3 direction, float impulse, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1047677388u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in direction);
				bufferWriter.WriteValueSafe(in impulse, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1047677388u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (interactorReference.TryGet(out var networkObject) && networkObject.TryGetComponent<Interactor>(out var component) && component.IsLocalPlayer && !(component.Player?.FirstPersonCharacter == null))
				{
					component.Player.FirstPersonCharacter.ImpulseInDirection(direction, impulse);
				}
			}
		}

		private static bool IsFinite(Vector3 value)
		{
			if (float.IsFinite(value.x) && float.IsFinite(value.y))
			{
				return float.IsFinite(value.z);
			}
			return false;
		}

		[Rpc(SendTo.Server)]
		private void ServerReleaseCaughtFishRpc(uint sessionId, FixedString64Bytes fishId, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2081330602u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sessionId);
				bufferWriter.WriteValueSafe(in fishId, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 2081330602u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsServer || rpcParams.Receive.SenderClientId != base.OwnerClientId || sessionId == 0 || !ServerCanReward(sessionId, fishId) || _player == null || !(_itemDatabaseSO?.FindById(fishId) is FishSO fishSO) || !SeaCreatureManager.TryGetActiveInstance(out var manager))
			{
				UtilityMethods.Log("[FishingRod] Escaped fish validation failed. " + $"Session={sessionId} FishId={fishId}", LogType.Warning);
				return;
			}
			Vector3 position = _player.transform.position;
			SeaLayer seaLayer = manager.FindNearestLayer(position);
			if (seaLayer == null)
			{
				UtilityMethods.Log("[FishingRod] Escaped fish could not resolve a SeaLayer. " + $"Session={sessionId} Fish={fishSO.Name}", LogType.Warning);
				return;
			}
			Vector3 spawnPosition = position + _player.transform.forward * 1.25f - Vector3.up * 0.35f;
			SeaCreature seaCreature = manager.SpawnCreatureAtPosition(fishSO, seaLayer, spawnPosition);
			if (seaCreature == null)
			{
				UtilityMethods.Log("[FishingRod] Escaped fish spawn failed. " + $"Session={sessionId} Fish={fishSO.Name} Layer={seaLayer.Index}", LogType.Warning);
				return;
			}
			seaCreature.SetServerSimulationActive(active: true);
			if (seaCreature is WanderingSeaCreature wanderingSeaCreature)
			{
				wanderingSeaCreature.ServerStartPanicFrom(_player);
			}
			else
			{
				UtilityMethods.Log("[FishingRod] Escaped creature has no wandering panic state. Fish=" + fishSO.Name + " Type=" + seaCreature.GetType().Name, LogType.Warning);
			}
			_lastResolvedSessionId = sessionId;
			_serverPhase = ServerFishingPhase.Collected;
			UtilityMethods.Log("[FishingRod] Caught fish returned alive to the ocean. " + $"Session={sessionId} Fish={fishSO.Name} Layer={seaLayer.Index}");
		}

		[Rpc(SendTo.Server)]
		private void ServerCompleteCatchRpc(uint sessionId, FixedString64Bytes fishId, bool useHeldSlotFallback, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3645958301u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sessionId);
				bufferWriter.WriteValueSafe(in fishId, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in useHeldSlotFallback, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3645958301u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsServer || rpcParams.Receive.SenderClientId != base.OwnerClientId)
			{
				return;
			}
			if (ServerHasSession(sessionId) && _serverRewardAllocated)
			{
				ServerFishingPhase serverPhase = _serverPhase;
				if (serverPhase == ServerFishingPhase.Delivery || serverPhase == ServerFishingPhase.Collected)
				{
					ClientConfirmCatchRpc(sessionId, new FixedString64Bytes(_serverFish.Id), _serverWeight, _serverHeldFallback, _serverMilestone, _serverRewardReference, base.RpcTarget.Single(base.OwnerClientId, RpcTargetUse.Temp));
					return;
				}
			}
			if (!ServerCanReward(sessionId, fishId))
			{
				ClientRejectCatchCollectionRpc(sessionId, base.RpcTarget.Single(base.OwnerClientId, RpcTargetUse.Temp));
				return;
			}
			if (!(_itemDatabaseSO?.FindById(fishId) is FishSO fishSO) || fishSO.DeadPrefab == null || _player == null || _player.Interactor == null || _player.Interactor.NetworkObject == null || Singleton<SeaCreatureManager>.Instance == null)
			{
				UtilityMethods.Log($"[FishingRod] Catch reward validation failed. Session={sessionId} FishId={fishId}", LogType.Warning);
				ClientRejectCatchCollectionRpc(sessionId, base.RpcTarget.Single(base.OwnerClientId, RpcTargetUse.Temp));
				return;
			}
			float serverWeight = _serverWeight;
			Vector3 vector = _player.transform.position + _player.transform.forward * 1.25f + Vector3.up * 0.5f;
			DeadFish deadFish = Singleton<SeaCreatureManager>.Instance.SpawnDeadFish(fishSO, vector, base.OwnerClientId, _player.Interactor.NetworkObject.ToReference(), select: false, canHold: false, useFallbackDropPosition: true, vector, !useHeldSlotFallback);
			if (deadFish == null)
			{
				ClientRejectCatchCollectionRpc(sessionId, base.RpcTarget.Single(base.OwnerClientId, RpcTargetUse.Temp));
				return;
			}
			deadFish.Weight.Value = serverWeight;
			deadFish.CanGaze.Value = false;
			deadFish.CanHold.Value = false;
			_serverReward = deadFish;
			_serverRewardReference = deadFish.NetworkObject.ToReference();
			_serverRewardAllocated = true;
			_serverHeldFallback = useHeldSlotFallback;
			_serverPhase = ServerFishingPhase.Delivery;
			_serverDeliveryDeadline = GetSynchronizedTime() + 8.0;
			_lastResolvedSessionId = sessionId;
			TabletNetworkHandler service = TabletNetworkHandler.Instance;
			if (service == null)
			{
				ServiceLocator.ForSceneOf(this).TryGet<TabletNetworkHandler>(out service);
			}
			if (service != null)
			{
				_serverMilestone = service.GetFishingCatchMilestone(fishSO, serverWeight, _player.SteamID.Value);
				service.ServerAddToFishBook(fishSO, serverWeight, _player.SteamID.Value, base.OwnerClientId);
				deadFish.ServerMarkFishBookCatchRegistered();
			}
			else
			{
				UtilityMethods.Log("[FishingRod] Fish Book registration skipped. TabletNetworkHandler not found. " + $"Session={sessionId} Fish={fishSO.Name}", LogType.Warning);
			}
			ServerRegisterCatchProgression(fishSO);
			ClientConfirmCatchRpc(sessionId, fishId, serverWeight, useHeldSlotFallback, _serverMilestone, _serverRewardReference, base.RpcTarget.Single(base.OwnerClientId, RpcTargetUse.Temp));
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientRejectCatchCollectionRpc(uint sessionId, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3071222915u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sessionId);
				__endSendRpc(ref bufferWriter, 3071222915u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsOwner || sessionId != _pendingCollectionSession)
			{
				return;
			}
			_collectRequested = false;
			_pendingCollectionSession = 0u;
			if (sessionId != _sessionId || _state != FishingSessionState.Success)
			{
				ServerCancelFishingRpc(sessionId);
				return;
			}
			_catchReadyToCollect = true;
			ShowRodNotice(FishingRodText.Get("retry", "Collection failed. Try again.", "Toplanamadı. Tekrar dene."));
			if (base.IsSpawned && _currentFish != null)
			{
				_presentationCaughtFishId.Value = new FixedString64Bytes(_currentFish.Id);
			}
			SpawnCaughtFishGraphic();
			RefreshFishingRodHints();
			UtilityMethods.Log($"[FishingRod] Catch collection failed and can be retried. Session={sessionId}", LogType.Warning);
		}

		private void ServerRegisterCatchProgression(FishSO fishSO)
		{
			if (!base.IsServer || fishSO == null || _player == null)
			{
				return;
			}
			GameProgressManager instance = Singleton<GameProgressManager>.Instance;
			instance?.RpcRelay?.ServerUnlockSteamAchievementForClient("ACH_FIRST_FISH_CAUGHT", base.OwnerClientId);
			instance?.ServerGrantProgressionXp(ProgressionXpSource.FishCaught, fishSO.Id);
			QuestReferenceHub instance2 = QuestReferenceHub.Instance;
			if (instance2 == null)
			{
				return;
			}
			instance2.AddProgress(QuestId.CatchFiveFishWithTheFishingRod);
			if (!(instance?.ItemDatabaseSO == null))
			{
				if (fishSO == instance.ItemDatabaseSO.MackerelSO)
				{
					instance2.AddProgress(QuestId.CatchAMackerel);
				}
				else if (fishSO == instance.ItemDatabaseSO.BlackSeaBreamSO)
				{
					instance2.AddProgress(QuestId.CatchBlackSeaBream);
				}
				else if (fishSO == instance.ItemDatabaseSO.ChinookSalmonSO)
				{
					instance2.AddProgress(QuestId.CatchChinookSalmonAtGoldenfinDrift);
				}
				else if (fishSO == instance.ItemDatabaseSO.SquidSO)
				{
					instance2.AddProgress(QuestId.CatchASquid);
				}
			}
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientConfirmCatchRpc(uint sessionId, FixedString64Bytes fishId, float weight, bool heldFallback, byte milestone, NetworkObjectReference reward, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(755528596u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sessionId);
				bufferWriter.WriteValueSafe(in fishId, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in weight, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in heldFallback, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in milestone, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in reward, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 755528596u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsOwner && sessionId == _pendingCollectionSession)
			{
				_pendingCollectionSession = 0u;
				_collectRequested = false;
				if (heldFallback)
				{
					ForceDisableTool();
				}
				else
				{
					CancelFishingSession(FishingCancelReason.None, returnToReady: true, releaseMovement: true);
				}
				ServerDeliverCatchRpc(sessionId);
				if (_itemDatabaseSO?.FindById(fishId) is FishSO fishSO)
				{
					string badge = milestone switch
					{
						2 => FishingRodText.Get("personal_record", "PERSONAL RECORD", "KİŞİSEL REKOR"), 
						1 => FishingRodText.Get("new_species", "NEW SPECIES", "YENİ TÜR"), 
						_ => "", 
					};
					StartCoroutine(ShowDeliveredCatch(reward, fishSO.LocalizedName, weight, badge));
					PlayerPrefs.SetInt("StationTutorial.FishingRod.Seen", 1);
					PlayerPrefs.Save();
				}
			}
		}

		private string BindingLabel(string actionName, bool useIcon = false)
		{
			InputAction inputAction = _player?.PlayerInput?.actions?.FindAction("Player/" + actionName);
			if (inputAction == null)
			{
				return actionName;
			}
			string currentControlScheme = _player.PlayerInput.currentControlScheme;
			if (useIcon)
			{
				int num = ((!string.IsNullOrEmpty(currentControlScheme)) ? inputAction.GetBindingIndex(InputBinding.MaskByGroup(currentControlScheme)) : 0);
				if (num < 0 && inputAction.bindings.Count > 0)
				{
					num = 0;
				}
				if (HintItem.TryGetBindingSpriteName(inputAction, num, out var spriteName))
				{
					return "<size=100%><sprite name=\"" + spriteName + "\"></size>";
				}
			}
			string bindingDisplayString = inputAction.GetBindingDisplayString((InputBinding.DisplayStringOptions)0, currentControlScheme);
			if (!string.IsNullOrWhiteSpace(bindingDisplayString))
			{
				return bindingDisplayString;
			}
			return inputAction.GetBindingDisplayString();
		}

		private void OnCounterMoveInput(InputActionPhase phase, Vector2 value)
		{
			if (base.IsOwner && _state == FishingSessionState.Challenge)
			{
				_moveCounterInput = ((phase == InputActionPhase.Canceled) ? Vector2.zero : new Vector2(value.x, 0f - value.y));
			}
		}

		private void ShowFirstCastGuidance()
		{
			if (!_guidedFirstCast && PlayerPrefs.GetInt("StationTutorial.FishingRod.Seen", 0) == 0)
			{
				_guidedFirstCast = true;
				ShowRodNotice(FishingRodText.Get("guide_cast", "Release {0} to cast. Aim at a circle in deep water.", "Atmak için {0} tuşunu bırak. Derin sudaki halkayı hedefle.", ReelBindingLabel));
			}
		}

		private void ShowWaitingGuidance()
		{
			_fishingRodUI?.ShowWaiting();
		}

		private void ShowChallengeGuidance()
		{
			if (!_guidedFirstChallenge && PlayerPrefs.GetInt("StationTutorial.FishingRod.Seen", 0) == 0)
			{
				_guidedFirstChallenge = true;
				ShowRodNotice(FishingRodText.Get("guide_counter", "Follow the arrow with the mouse, stick or movement keys. Reel only when the fish rests.", "Oku fare, stick veya hareket tuşlarıyla takip et. Yalnız balık sakinleşince sar."));
			}
		}

		private static void ShowRodNotice(string message)
		{
			Singleton<NotifyManager>.Instance?.ShowAlert(FishingRodText.Get("title", "FISHING", "BALIK TUTMA"), message, 3f, AlertPriority.Normal, autoClose: true, usePriorityPulse: false);
		}

		private IEnumerator ShowDeliveredCatch(NetworkObjectReference reward, string fishName, float weight, string badge)
		{
			float deadline = Time.unscaledTime + 3f;
			string destination = "";
			while (Time.unscaledTime < deadline)
			{
				if (reward.TryGet(out var networkObject) && networkObject.TryGetComponent<DeadFish>(out var component))
				{
					if (Singleton<QuickSlotHandler>.Instance != null && Singleton<QuickSlotHandler>.Instance.TryGetItemLocation(component, out var _, out var isInHeldSlot))
					{
						destination = (isInHeldSlot ? FishingRodText.Get("in_hand", "In your hand", "Eline alındı") : FishingRodText.Get("in_bag", "Added to inventory", "Envantere eklendi"));
						break;
					}
					if (component.CanGaze.Value && component.CanHold.Value)
					{
						destination = FishingRodText.Get("on_ground", "Placed nearby", "Yakına bırakıldı");
						break;
					}
				}
				yield return null;
			}
			ShowRodNotice(FishingRodText.Get("caught", "{0} · {1:0.00} kg — Caught!", "{0} · {1:0.00} kg — Yakalandı!", fishName, weight) + (string.IsNullOrEmpty(destination) ? "" : ("\n" + destination)) + (string.IsNullOrEmpty(badge) ? "" : ("\n" + badge)));
		}

		private void UpdateWaitingBobber()
		{
			if (!(_presentationBobber == null))
			{
				float f = Time.time * 2f;
				float num = ((_serverAcceptedCast && _waitRemaining < 0.75f) ? ((0f - Mathf.Abs(Mathf.Sin(Time.time * 14f))) * 0.08f) : 0f);
				_presentationBobber.SetPresentationOffset(Vector3.up * (Mathf.Sin(f) * 0.018f + num));
			}
		}

		private void UpdateCastPreview()
		{
			if (Time.unscaledTime < _nextPreviewProbe)
			{
				return;
			}
			_nextPreviewProbe = Time.unscaledTime + 0.1f;
			if (!TryResolveCastPoint(GetNormalizedCastCharge(), out var point, out var _, out var castLocationState))
			{
				if (_castPreview != null)
				{
					_castPreview.enabled = false;
				}
				_previewStatus = FishingRodText.Get("no_surface", "× No surface", "× Yüzey yok");
				return;
			}
			bool flag = castLocationState == CastLocationState.FishableWater;
			_previewStatus = (flag ? FishingRodText.Get("valid", "○ Fishable water", "○ Uygun su") : ((castLocationState == CastLocationState.ShallowWater) ? FishingRodText.Get("shallow", "△ Too shallow", "△ Çok sığ") : FishingRodText.Get("land", "× Land", "× Kara")));
			if (!flag && castLocationState != CastLocationState.ShallowWater)
			{
				if (_castPreview != null)
				{
					_castPreview.enabled = false;
				}
				return;
			}
			if (_castPreview == null)
			{
				_castPreview = new GameObject("FishingCastPreview").AddComponent<LineRenderer>();
				_castPreview.enabled = false;
				Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
				if (shader == null)
				{
					UnityEngine.Object.Destroy(_castPreview.gameObject);
					_castPreview = null;
					return;
				}
				_castPreviewMaterial = new Material(shader);
				_castPreview.sharedMaterial = _castPreviewMaterial;
				_castPreview.useWorldSpace = true;
				_castPreview.widthMultiplier = 0.035f;
				_castPreview.shadowCastingMode = ShadowCastingMode.Off;
				_castPreview.receiveShadows = false;
			}
			Color color = (flag ? new Color(0.25f, 1f, 0.7f) : new Color(1f, 0.75f, 0.15f));
			_castPreviewMaterial.color = color;
			LineRenderer castPreview = _castPreview;
			Color startColor = (_castPreview.endColor = color);
			castPreview.startColor = startColor;
			point += Vector3.up * 0.04f;
			int num = (flag ? 32 : 3);
			_castPreview.positionCount = num + 1;
			for (int i = 0; i <= num; i++)
			{
				float f = (float)i * MathF.PI * 2f / (float)num;
				_previewPoints[i] = point + new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f)) * 0.4f;
				_castPreview.SetPosition(i, _previewPoints[i]);
			}
			_castPreview.enabled = true;
		}

		private void HideCastPreview()
		{
			if (_castPreview != null)
			{
				_castPreview.enabled = false;
				UnityEngine.Object.Destroy(_castPreview.gameObject);
			}
			if (_castPreviewMaterial != null)
			{
				UnityEngine.Object.Destroy(_castPreviewMaterial);
			}
			_castPreview = null;
			_castPreviewMaterial = null;
			_previewStatus = "";
			_nextPreviewProbe = 0f;
		}

		private bool ServerHasSession(uint id)
		{
			if (id != 0)
			{
				return id == _serverSessionId;
			}
			return false;
		}

		private bool ServerCanPullBobber(uint id, double now)
		{
			if (ServerHasSession(id) && _serverPhase == ServerFishingPhase.Waiting)
			{
				return now < _serverBiteAt;
			}
			return false;
		}

		[Rpc(SendTo.Server)]
		private void ServerBeginFishingRpc(uint sessionId, Vector3 point, float charge, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1892664701u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sessionId);
				bufferWriter.WriteValueSafe(in point);
				bufferWriter.WriteValueSafe(in charge, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1892664701u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsServer || rpcParams.Receive.SenderClientId != base.OwnerClientId)
			{
				return;
			}
			int num;
			if (FishingRodChallenge.IsNewerSession(sessionId, _serverSessionId))
			{
				ServerFishingPhase serverPhase = _serverPhase;
				if (serverPhase != ServerFishingPhase.Waiting && serverPhase != ServerFishingPhase.Challenge && serverPhase != ServerFishingPhase.Success && serverPhase != ServerFishingPhase.Delivery && IsUnlocked && base.ToolStatus && !IsPlayerStateInvalid() && !IsPlayerInWaterMovementMode() && IsFinite(point) && float.IsFinite(charge) && charge >= 0f)
				{
					num = ((charge <= 1f) ? 1 : 0);
					goto IL_0171;
				}
			}
			num = 0;
			goto IL_0171;
			IL_0171:
			bool flag = (byte)num != 0;
			FishingRodLevelData fishingRodLevelData = _data?.GetLevel(Level);
			flag &= fishingRodLevelData != null;
			float num2 = ((_player != null) ? Vector3.ProjectOnPlane(point - _player.transform.position, Vector3.up).magnitude : 0f);
			if (flag)
			{
				flag = num2 >= _data.MinimumCastDistance * 0.75f && num2 <= _data.GetCastDistance(fishingRodLevelData, charge) + 1f && TryResolveSurface(point, out var point2, out var location, verbose: false) && location == CastLocationState.FishableWater && Vector3.Distance(point, point2) <= 0.5f;
			}
			FishSO selectedFish = null;
			if (flag)
			{
				flag = TrySelectFish(_data.GetZone(num2), out selectedFish, fishingRodLevelData);
			}
			if (!flag)
			{
				ClientFishingRejectedRpc(sessionId, base.RpcTarget.Single(base.OwnerClientId, RpcTargetUse.Temp));
				return;
			}
			_serverSessionId = sessionId;
			_serverPhase = ServerFishingPhase.Waiting;
			_serverFish = selectedFish;
			_serverMilestone = 0;
			_serverRewardAllocated = false;
			_serverWeight = selectedFish.GetRandomWeight();
			_serverChallenge = new FishingRodChallenge(selectedFish.FishingRodSettings, fishingRodLevelData.GetMatchup(selectedFish.FishingRodSettings.Difficulty), () => UnityEngine.Random.value);
			_serverCastPoint = point;
			_serverCounter = Vector2.zero;
			_serverReeling = false;
			_serverInputAt = GetSynchronizedTime();
			_serverBobberAt = _serverInputAt + (double)_data.GetCastTravelDuration(charge);
			_serverBiteAt = _serverBobberAt + (double)_data.GetRandomWaitDuration();
			ClientFishingAcceptedRpc(sessionId, new FixedString64Bytes(selectedFish.Id), _serverBiteAt, base.RpcTarget.Single(base.OwnerClientId, RpcTargetUse.Temp));
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientFishingAcceptedRpc(uint sessionId, FixedString64Bytes fishId, double biteAt, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(628510730u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sessionId);
				bufferWriter.WriteValueSafe(in fishId, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in biteAt, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 628510730u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsOwner || sessionId != _sessionId)
			{
				return;
			}
			FishingSessionState state = _state;
			if (state == FishingSessionState.Casting || state == FishingSessionState.Waiting)
			{
				if (!(_itemDatabaseSO?.FindById(fishId) is FishSO fishSO))
				{
					CancelFishingSession(FishingCancelReason.NoEligibleFish, returnToReady: true, releaseMovement: true);
					return;
				}
				_serverAcceptedCast = true;
				_currentFish = fishSO;
				_currentFishSettings = fishSO.FishingRodSettings;
				_currentMatchup = _currentLevel.GetMatchup(_currentFishSettings.Difficulty);
				_waitRemaining = Mathf.Max(0f, (float)(biteAt - GetSynchronizedTime()));
				_currentPresentationStrength = Mathf.Clamp01(_data.GetDifficultyPresentationStrength(_currentFishSettings.Difficulty) * Mathf.Max(0f, _currentFishSettings.PresentationStrength));
				_presentationStrength.Value = _currentPresentationStrength;
			}
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientFishingRejectedRpc(uint sessionId, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(542844130u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sessionId);
				__endSendRpc(ref bufferWriter, 542844130u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsOwner && sessionId == _sessionId && IsFishingActive)
				{
					ShowNoFishHereFeedback();
					CancelFishingSession(FishingCancelReason.InvalidCast, returnToReady: true, releaseMovement: true);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerFishingInputRpc(uint sessionId, Vector2 counter, bool reeling, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(193528005u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sessionId);
				bufferWriter.WriteValueSafe(in counter);
				bufferWriter.WriteValueSafe(in reeling, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 193528005u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && rpcParams.Receive.SenderClientId == base.OwnerClientId && ServerHasSession(sessionId) && _serverPhase == ServerFishingPhase.Challenge)
				{
					_serverCounter = FishingRodChallenge.ResolveCounter(counter, _data.MouseCounterThreshold);
					_serverReeling = reeling;
					_serverInputAt = GetSynchronizedTime();
				}
			}
		}

		private void TickServerFishing()
		{
			if (!base.IsSpawned || !base.IsServer)
			{
				return;
			}
			double synchronizedTime = GetSynchronizedTime();
			if (_serverPhase == ServerFishingPhase.Delivery)
			{
				if (synchronizedTime >= _serverDeliveryDeadline)
				{
					ServerReleaseUndeliveredReward();
				}
				return;
			}
			ServerFishingPhase serverPhase = _serverPhase;
			if (serverPhase != ServerFishingPhase.Waiting && serverPhase != ServerFishingPhase.Challenge)
			{
				return;
			}
			if (!IsUnlocked || IsPlayerStateInvalid() || IsPlayerInWaterMovementMode())
			{
				ServerAbortFishing();
				ClientFishingRejectedRpc(_serverSessionId, base.RpcTarget.Single(base.OwnerClientId, RpcTargetUse.Temp));
				return;
			}
			if (_serverPhase == ServerFishingPhase.Waiting)
			{
				if (synchronizedTime >= _serverBobberAt && synchronizedTime < _serverBiteAt && _activeBobber == null)
				{
					ServerSpawnBobber(_serverSessionId, _serverCastPoint);
				}
				if (synchronizedTime < _serverBiteAt)
				{
					return;
				}
				ServerDespawnActiveBobber();
				_serverPhase = ServerFishingPhase.Challenge;
				_serverSnapshotAt = 0.0;
			}
			bool flag = synchronizedTime - _serverInputAt <= 0.4;
			_serverChallenge.Tick(Time.deltaTime, flag ? _serverCounter : Vector2.zero, flag && _serverReeling);
			if (_serverChallenge.Succeeded)
			{
				_serverPhase = ServerFishingPhase.Success;
			}
			else if (_serverChallenge.Failed)
			{
				_serverPhase = ServerFishingPhase.Failed;
			}
			if (!(synchronizedTime < _serverSnapshotAt) || _serverPhase != ServerFishingPhase.Challenge)
			{
				_serverSnapshotAt = synchronizedTime + 0.08;
				ClientFishingSnapshotRpc(_serverSessionId, (byte)_serverPhase, (byte)_serverChallenge.Direction, _serverChallenge.Progress, _serverChallenge.Tension, _serverChallenge.CounterProgress, _serverChallenge.PreparationRemaining, _serverChallenge.CountersCompleted, _serverChallenge.FailedWhileReeling, (_serverPhase == ServerFishingPhase.Success) ? _serverWeight : 0f, base.RpcTarget.Single(base.OwnerClientId, RpcTargetUse.Temp));
			}
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientFishingSnapshotRpc(uint sessionId, byte phase, byte direction, float progress, float tension, float counterProgress, float preparation, int counters, bool wrongReel, float weight, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3872936447u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sessionId);
				bufferWriter.WriteValueSafe(in phase, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in direction, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in progress, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in tension, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in counterProgress, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in preparation, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, counters);
				bufferWriter.WriteValueSafe(in wrongReel, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in weight, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3872936447u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsOwner || sessionId != _sessionId || !base.ToolStatus)
			{
				return;
			}
			FishingSessionState state = _state;
			if (state != FishingSessionState.Casting && state != FishingSessionState.Waiting && state != FishingSessionState.Challenge)
			{
				return;
			}
			if (_state != FishingSessionState.Challenge)
			{
				BeginChallenge();
			}
			if (_state == FishingSessionState.Challenge)
			{
				FishingRodFishDirection fishingRodFishDirection = (FishingRodFishDirection)direction;
				if (_fishDirection != fishingRodFishDirection)
				{
					SetFishDirection(fishingRodFishDirection, 0.1f);
				}
				_catchProgress = progress;
				_lineTension = tension;
				_counterProgress = counterProgress;
				_preparationRemaining = preparation;
				if (counters > _completedCounters)
				{
					_completedCounters = counters;
					_fishingRodUI?.CounterSucceeded();
					PlayRodSfx((_data.CounterSuccessSfx != null) ? _data.CounterSuccessSfx : _data.CastWaterSplashSfx);
				}
				switch (phase)
				{
				case 3:
					_caughtWeight = weight;
					ResolveSuccess();
					break;
				case 4:
					_failedWhileReeling = wrongReel;
					ResolveFailure();
					break;
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerCancelFishingRpc(uint sessionId, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3733052550u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sessionId);
				__endSendRpc(ref bufferWriter, 3733052550u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsServer && rpcParams.Receive.SenderClientId == base.OwnerClientId && ServerHasSession(sessionId))
			{
				ServerFishingPhase serverPhase = _serverPhase;
				if (serverPhase != ServerFishingPhase.Delivery && serverPhase != ServerFishingPhase.Collected)
				{
					ServerAbortFishing();
				}
			}
		}

		private void ServerAbortFishing()
		{
			_serverPhase = ServerFishingPhase.None;
			_serverCounter = Vector2.zero;
			_serverReeling = false;
			ServerDespawnActiveBobber();
		}

		private bool ServerCanReward(uint sessionId, FixedString64Bytes fishId)
		{
			if (ServerHasSession(sessionId) && _serverPhase == ServerFishingPhase.Success && _serverChallenge != null && _serverChallenge.Succeeded && _serverFish != null)
			{
				return fishId.Equals(new FixedString64Bytes(_serverFish.Id));
			}
			return false;
		}

		[Rpc(SendTo.Server)]
		private void ServerDeliverCatchRpc(uint sessionId, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1242904913u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sessionId);
				__endSendRpc(ref bufferWriter, 1242904913u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && rpcParams.Receive.SenderClientId == base.OwnerClientId && ServerHasSession(sessionId) && _serverPhase == ServerFishingPhase.Delivery && !(_serverReward == null))
				{
					_serverPhase = ServerFishingPhase.Collected;
					_serverReward.ServerLazyHold(new FixedString64Bytes(_serverFish.Id), base.OwnerClientId, _player.Interactor.NetworkObject.ToReference(), select: false, hold: true, useFallbackDropPosition: true, _serverReward.transform.position, !_serverHeldFallback);
					_serverReward = null;
				}
			}
		}

		private void ServerReleaseUndeliveredReward()
		{
			if (_serverReward != null)
			{
				_serverReward.CanGaze.Value = true;
				_serverReward.CanHold.Value = true;
				_serverReward = null;
			}
			if (_serverPhase == ServerFishingPhase.Delivery)
			{
				_serverPhase = ServerFishingPhase.Collected;
			}
		}

		protected override void __initializeVariables()
		{
			if (_presentationState == null)
			{
				throw new Exception("FishingRod._presentationState cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_presentationState.Initialize(this);
			__nameNetworkVariable(_presentationState, "_presentationState");
			NetworkVariableFields.Add(_presentationState);
			if (_presentationDirection == null)
			{
				throw new Exception("FishingRod._presentationDirection cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_presentationDirection.Initialize(this);
			__nameNetworkVariable(_presentationDirection, "_presentationDirection");
			NetworkVariableFields.Add(_presentationDirection);
			if (_presentationSteerDirection == null)
			{
				throw new Exception("FishingRod._presentationSteerDirection cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_presentationSteerDirection.Initialize(this);
			__nameNetworkVariable(_presentationSteerDirection, "_presentationSteerDirection");
			NetworkVariableFields.Add(_presentationSteerDirection);
			if (_presentationCastPoint == null)
			{
				throw new Exception("FishingRod._presentationCastPoint cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_presentationCastPoint.Initialize(this);
			__nameNetworkVariable(_presentationCastPoint, "_presentationCastPoint");
			NetworkVariableFields.Add(_presentationCastPoint);
			if (_presentationSessionId == null)
			{
				throw new Exception("FishingRod._presentationSessionId cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_presentationSessionId.Initialize(this);
			__nameNetworkVariable(_presentationSessionId, "_presentationSessionId");
			NetworkVariableFields.Add(_presentationSessionId);
			if (_presentationCastOrigin == null)
			{
				throw new Exception("FishingRod._presentationCastOrigin cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_presentationCastOrigin.Initialize(this);
			__nameNetworkVariable(_presentationCastOrigin, "_presentationCastOrigin");
			NetworkVariableFields.Add(_presentationCastOrigin);
			if (_presentationCastStartTime == null)
			{
				throw new Exception("FishingRod._presentationCastStartTime cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_presentationCastStartTime.Initialize(this);
			__nameNetworkVariable(_presentationCastStartTime, "_presentationCastStartTime");
			NetworkVariableFields.Add(_presentationCastStartTime);
			if (_presentationCastDuration == null)
			{
				throw new Exception("FishingRod._presentationCastDuration cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_presentationCastDuration.Initialize(this);
			__nameNetworkVariable(_presentationCastDuration, "_presentationCastDuration");
			NetworkVariableFields.Add(_presentationCastDuration);
			if (_presentationCastArcHeight == null)
			{
				throw new Exception("FishingRod._presentationCastArcHeight cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_presentationCastArcHeight.Initialize(this);
			__nameNetworkVariable(_presentationCastArcHeight, "_presentationCastArcHeight");
			NetworkVariableFields.Add(_presentationCastArcHeight);
			if (_presentationTension == null)
			{
				throw new Exception("FishingRod._presentationTension cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_presentationTension.Initialize(this);
			__nameNetworkVariable(_presentationTension, "_presentationTension");
			NetworkVariableFields.Add(_presentationTension);
			if (_presentationStrength == null)
			{
				throw new Exception("FishingRod._presentationStrength cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_presentationStrength.Initialize(this);
			__nameNetworkVariable(_presentationStrength, "_presentationStrength");
			NetworkVariableFields.Add(_presentationStrength);
			if (_presentationReeling == null)
			{
				throw new Exception("FishingRod._presentationReeling cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_presentationReeling.Initialize(this);
			__nameNetworkVariable(_presentationReeling, "_presentationReeling");
			NetworkVariableFields.Add(_presentationReeling);
			if (_presentationStruggling == null)
			{
				throw new Exception("FishingRod._presentationStruggling cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_presentationStruggling.Initialize(this);
			__nameNetworkVariable(_presentationStruggling, "_presentationStruggling");
			NetworkVariableFields.Add(_presentationStruggling);
			if (_presentationCaughtFishId == null)
			{
				throw new Exception("FishingRod._presentationCaughtFishId cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_presentationCaughtFishId.Initialize(this);
			__nameNetworkVariable(_presentationCaughtFishId, "_presentationCaughtFishId");
			NetworkVariableFields.Add(_presentationCaughtFishId);
			if (_presentationCaughtWeight == null)
			{
				throw new Exception("FishingRod._presentationCaughtWeight cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_presentationCaughtWeight.Initialize(this);
			__nameNetworkVariable(_presentationCaughtWeight, "_presentationCaughtWeight");
			NetworkVariableFields.Add(_presentationCaughtWeight);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(2113037881u, __rpc_handler_2113037881, "ServerCloseBobberRpc", RpcInvokePermission.Everyone);
			__registerRpc(1840037164u, __rpc_handler_1840037164, "ClientResetFromBobberPullRpc", RpcInvokePermission.Server);
			__registerRpc(1047677388u, __rpc_handler_1047677388, "ClientApplyBobberLaunchRpc", RpcInvokePermission.Server);
			__registerRpc(2081330602u, __rpc_handler_2081330602, "ServerReleaseCaughtFishRpc", RpcInvokePermission.Everyone);
			__registerRpc(3645958301u, __rpc_handler_3645958301, "ServerCompleteCatchRpc", RpcInvokePermission.Everyone);
			__registerRpc(3071222915u, __rpc_handler_3071222915, "ClientRejectCatchCollectionRpc", RpcInvokePermission.Server);
			__registerRpc(755528596u, __rpc_handler_755528596, "ClientConfirmCatchRpc", RpcInvokePermission.Server);
			__registerRpc(1892664701u, __rpc_handler_1892664701, "ServerBeginFishingRpc", RpcInvokePermission.Everyone);
			__registerRpc(628510730u, __rpc_handler_628510730, "ClientFishingAcceptedRpc", RpcInvokePermission.Server);
			__registerRpc(542844130u, __rpc_handler_542844130, "ClientFishingRejectedRpc", RpcInvokePermission.Server);
			__registerRpc(193528005u, __rpc_handler_193528005, "ServerFishingInputRpc", RpcInvokePermission.Everyone);
			__registerRpc(3872936447u, __rpc_handler_3872936447, "ClientFishingSnapshotRpc", RpcInvokePermission.Server);
			__registerRpc(3733052550u, __rpc_handler_3733052550, "ServerCancelFishingRpc", RpcInvokePermission.Everyone);
			__registerRpc(1242904913u, __rpc_handler_1242904913, "ServerDeliverCatchRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_2113037881(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				RpcParams ext = rpcParams.Ext;
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishingRod)target).ServerCloseBobberRpc(value, ext);
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1840037164(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				reader.ReadValueSafe(out FixedString64Bytes value2, default(FastBufferWriter.ForFixedStrings));
				RpcParams ext = rpcParams.Ext;
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishingRod)target).ClientResetFromBobberPullRpc(value, value2, ext);
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1047677388(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out float value3, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishingRod)target).ClientApplyBobberLaunchRpc(value, value2, value3, ext);
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2081330602(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				reader.ReadValueSafe(out FixedString64Bytes value2, default(FastBufferWriter.ForFixedStrings));
				RpcParams ext = rpcParams.Ext;
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishingRod)target).ServerReleaseCaughtFishRpc(value, value2, ext);
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3645958301(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				reader.ReadValueSafe(out FixedString64Bytes value2, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishingRod)target).ServerCompleteCatchRpc(value, value2, value3, ext);
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3071222915(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				RpcParams ext = rpcParams.Ext;
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishingRod)target).ClientRejectCatchCollectionRpc(value, ext);
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_755528596(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				reader.ReadValueSafe(out FixedString64Bytes value2, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out float value3, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value4, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out byte value5, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out NetworkObjectReference value6, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishingRod)target).ClientConfirmCatchRpc(value, value2, value3, value4, value5, value6, ext);
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1892664701(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out float value3, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishingRod)target).ServerBeginFishingRpc(value, value2, value3, ext);
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_628510730(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				reader.ReadValueSafe(out FixedString64Bytes value2, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out double value3, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishingRod)target).ClientFishingAcceptedRpc(value, value2, value3, ext);
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_542844130(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				RpcParams ext = rpcParams.Ext;
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishingRod)target).ClientFishingRejectedRpc(value, ext);
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_193528005(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				reader.ReadValueSafe(out Vector2 value2);
				reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishingRod)target).ServerFishingInputRpc(value, value2, value3, ext);
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3872936447(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out byte value3, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value4, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value5, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value6, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value7, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out int value8);
				reader.ReadValueSafe(out bool value9, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value10, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishingRod)target).ClientFishingSnapshotRpc(value, value2, value3, value4, value5, value6, value7, value8, value9, value10, ext);
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3733052550(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				RpcParams ext = rpcParams.Ext;
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishingRod)target).ServerCancelFishingRpc(value, ext);
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1242904913(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				RpcParams ext = rpcParams.Ext;
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishingRod)target).ServerDeliverCatchRpc(value, ext);
				((FishingRod)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "FishingRod";
		}
	}
}
