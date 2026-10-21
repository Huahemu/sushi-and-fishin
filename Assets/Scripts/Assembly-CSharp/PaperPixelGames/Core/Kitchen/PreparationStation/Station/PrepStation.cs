using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen.PreparationStation.GastronomPan;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Progress;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.StationTutorial;
using PaperPixelGames.Core.Tutorials;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Kitchen.PreparationStation.Station
{
	public class PrepStation : BaseUsable
	{
		private enum PrepStationInfoTextId : byte
		{
			None = 0,
			PlaceItemOnTable = 1,
			ClickKnife = 2,
			ClickGastroPan = 3,
			ClickGastroPanPile = 4
		}

		public enum PrepStationSfxId : byte
		{
			None = 0,
			CutSuccess = 1,
			InventoryMove = 2,
			KnifeSelect = 3,
			SpawnIngredient = 4,
			IngredientLand = 5,
			IngredientsFallIntoGastroPan = 6,
			GastroPanSpawn = 7,
			GastroPanMove = 8
		}

		[Serializable]
		private struct CutProgressSlot
		{
			public Image baseImage;

			public Image fillImage;
		}

		private readonly struct ReplicatedPrepCut
		{
			public readonly int SessionVersion;

			public readonly int Sequence;

			public readonly Vector3 CutPoint;

			public readonly Vector3 PlaneNormal;

			public readonly Vector3 CutDirection;

			public ReplicatedPrepCut(int sessionVersion, int sequence, Vector3 cutPoint, Vector3 planeNormal, Vector3 cutDirection)
			{
				SessionVersion = sessionVersion;
				Sequence = sequence;
				CutPoint = cutPoint;
				PlaneNormal = planeNormal;
				CutDirection = cutDirection;
			}
		}

		[Serializable]
		private sealed class IngredientVisualPlacementOverride
		{
			[SerializeField]
			private IngredientSO _ingredient;

			[SerializeField]
			private Vector3 _localPosition;

			[SerializeField]
			private Vector3 _localEulerAngles;

			public Vector3 LocalPosition => _localPosition;

			public Vector3 LocalEulerAngles => _localEulerAngles;

			public bool Matches(IngredientSO ingredient)
			{
				if (_ingredient != null)
				{
					return _ingredient == ingredient;
				}
				return false;
			}
		}

		[SerializeField]
		private ItemDatabaseSO _itemDatabaseSO;

		[SerializeField]
		private GastroPanSpawner _gastroPanSpawner;

		[SerializeField]
		private CuttingViewManager _cuttingViewManager;

		[SerializeField]
		private float _distanceFromCamera;

		[SerializeField]
		private Image _gastropanCenterInteractorImage;

		[SerializeField]
		private Transform _ingredientCenterPosition;

		[SerializeField]
		private Transform _gastropanCenterPosition;

		[SerializeField]
		private Transform _knifeModel;

		[SerializeField]
		private TextMeshProUGUI _instructionText;

		[SerializeField]
		private PhysicalStateController _physicalStateController;

		[SerializeField]
		private ProgressionUnlockKey _repairProgressionUnlockKey;

		[SerializeField]
		private PhysicalUnlockController _physicalUnlockController;

		[SerializeField]
		private GameObject _canvasGO;

		[SerializeField]
		private GameObject _3Dtext;

		[SerializeField]
		private StationTutorialPopupUI _stationTutorialPopupUI;

		[Space]
		[SerializeField]
		private StationTutorialData _tutorialData;

		[Header("ToolTip")]
		[SerializeField]
		private List<GastropanRecallInteractor> _recallInteractors;

		[Header("Spawn Settings")]
		[SerializeField]
		private Transform _spawnPointRoot;

		[Header("SFX")]
		[SerializeField]
		private AudioClipSO _cuttingSFX;

		[SerializeField]
		private AudioClipSO _cuttingFoodSFX;

		[Space]
		[SerializeField]
		private AudioClipSO _inventoryMoveSfx;

		[SerializeField]
		private AudioClipSO _knifeSelectSfx;

		[SerializeField]
		private AudioClipSO _spawnIngredientSfx;

		[SerializeField]
		private AudioClipSO _ingredientLandSfx;

		[SerializeField]
		private AudioClipSO _ingredientsFallIntoGastroPanSfx;

		[SerializeField]
		private AudioClipSO _gastroPanSpawnSfx;

		[SerializeField]
		private AudioClipSO _gastroPanMoveSfx;

		[Header("VFX's")]
		[SerializeField]
		private ParticleSystem _spawnPopVfx;

		[SerializeField]
		private ParticleSystem _landVfx;

		private List<Transform> _spawnPointList;

		private NetworkVariable<FixedString64Bytes> _currentIngredientID = new NetworkVariable<FixedString64Bytes>();

		private NetworkVariable<ushort> _currentIngredientItemType = new NetworkVariable<ushort>(0);

		private NetworkVariable<byte> _currentState = new NetworkVariable<byte>(0);

		private NetworkVariable<bool> _hasPendingProcessedOutputNetwork = new NetworkVariable<bool>(value: false);

		private NetworkVariable<FixedString64Bytes> _pendingProcessedIdNetwork = new NetworkVariable<FixedString64Bytes>();

		private NetworkVariable<int> _pendingProcessedYieldNetwork = new NetworkVariable<int>(0);

		private NetworkVariable<int> _pendingProcessedStartIndexNetwork = new NetworkVariable<int>(0);

		private readonly List<GameObject> _spawnedObjects = new List<GameObject>();

		private readonly List<Vector3> _mousePositions = new List<Vector3>();

		private ProcessedType _currentSelectedProcessedType;

		private Interactor _serverActiveInteractor;

		private bool _isKnifeHeld;

		private int _successfulCutCount;

		private int _requiredCutCount;

		private bool _isFallbackOccupied;

		private bool _localCuttingActive;

		private BaseHoldable _hiddenHoldableForStation;

		private PrepStationInfoTextId _currentInstructionTextId;

		private float _spawnVfxMinInterval = 0.1f;

		private float _lastPopVfxTime;

		private float _lastLandVfxTime;

		private float _spawnSfxMinInterval = 0.1f;

		private float _lastPopSfxTime;

		private float _lastLandSfxTime;

		private float _minPitch = 0.95f;

		private float _maxPitch = 1.05f;

		[Header("Cutting")]
		[SerializeField]
		private Transform _knifePressRoot;

		[Header("Knife Hit Points")]
		[SerializeField]
		private Transform _knifeTip;

		[SerializeField]
		private Transform _knifeHeel;

		private float _knifeThickness = 8f;

		private bool _mustExitCutPoint;

		private float _knifeDownTiltAngle = 25f;

		private float _knifeDownTiltSmooth = 12f;

		private float _knifeRotationSmooth = 35f;

		private float _knifeMinAngleDelta = 0.05f;

		private Vector3 _lastKnifeDirection;

		private bool _hasLastKnifeDirection;

		private float _currentTiltAngleY;

		[Header("Cut Item Feedback")]
		[SerializeField]
		private ParticleSystem _cutParticlePrefab;

		private float _cutItemScalePunch = 0.035f;

		private float _cutItemScaleDuration = 0.08f;

		[Header("Cut Flow")]
		[SerializeField]
		[Min(0f)]
		private float _nextCutPointDelayPadding = 0.025f;

		private Tween _cutFlowDelayTween;

		private readonly Vector3[] _worldCorners = new Vector3[4];

		[Header("Cut Progress UI (Static Slots)")]
		[SerializeField]
		private RectTransform _cutProgressParent;

		private int _maxCutSlots = 6;

		private float _cutFillDuration = 0.15f;

		private float _cutDotPunchScale = 1.2f;

		private readonly List<CutProgressSlot> _cutProgressSlots = new List<CutProgressSlot>();

		[Header("Ingredient Mesh Cutting")]
		[SerializeField]
		[Range(0f, 30f)]
		private float _ingredientMeshCutMaxPlaneAngle = 12f;

		private PrepStationIngredientMeshCutController _ingredientMeshCutController;

		private bool _clientCutSessionPending;

		private int _clientCutSessionVersion;

		private FixedString64Bytes _clientCutIngredientId;

		private int _clientCutRequiredCount;

		private int _clientCutOutputYield;

		private int _clientCutStartIndex;

		private FixedString64Bytes _clientCutProcessedId;

		private readonly Queue<ReplicatedPrepCut> _clientObserverCutQueue = new Queue<ReplicatedPrepCut>();

		private int _clientReceivedCutSequence;

		private int _clientAppliedCutSequence;

		private bool _clientMeshReplicationFailed;

		private bool _serverCutSessionActive;

		private int _serverCutSessionVersion;

		private int _serverCutSequence;

		private int _serverCutRequiredCount;

		private int _serverCutOutputYield;

		private int _serverCutStartIndex;

		private FixedString64Bytes _serverCutProcessedId;

		[Header("Prep Station Recovery Context")]
		[SerializeField]
		private bool _logRecoveryContext = true;

		private bool _hasPendingProcessedOutput;

		private FixedString64Bytes _pendingProcessedId;

		private int _pendingProcessedYield;

		private int _pendingProcessedStartIndex;

		private float _pendingProcessedCreatedAt;

		private const string PrepStationTutorialSeenKey = "StationTutorial.PrepStation.Seen";

		[Header("Processed Spawn Feel")]
		private float _spawnPopHeight = 0.05f;

		private float _spawnPopDuration = 0.18f;

		private float _spawnScaleOvershoot = 1.1f;

		private Vector2 _spawnRandomOffsetXZ = new Vector2(0.015f, 0.015f);

		private float _spawnRandomYaw = 12f;

		private float _spawnRandomRoll = 8f;

		[Header("Malzemeleri kuvete bosaltma")]
		private float _gastroLiftHeight = 0.25f;

		private float _gastroLiftDuration = 0.28f;

		private float _gastroDropDuration = 0.22f;

		private float _gastroDropStaggerMax = 0.06f;

		[Space]
		[SerializeField]
		private Transform _gastroLiftGatherCenter;

		private bool _clientPrepCutCompletionPending;

		private FixedString64Bytes _clientCompletionProcessedId;

		private int _clientCompletionOutputYield;

		private int _clientCompletionStartIndex;

		private bool _pendingGastroPanLift;

		private float _clientGastroPanLiftReadyAt;

		private Transform _pendingGastroPanTransferTarget;

		private float _pendingGastroPanTransferDuration;

		private float _pendingGastroPanTransferStagger;

		private bool _pendingGastroPanTransferShowsCenterInteractor;

		private readonly NetworkVariable<int> _staffEmployee = new NetworkVariable<int>(0);

		private readonly NetworkVariable<FixedString64Bytes> _staffRawVisual = new NetworkVariable<FixedString64Bytes>();

		private GameObject _staffVisual;

		[Header("Ingredient Visual Placement")]
		[SerializeField]
		private bool _autoFixIngredientVisualPlacement = true;

		[SerializeField]
		private float _ingredientTableSurfaceLocalY;

		[SerializeField]
		private float _ingredientSurfacePadding = 0.005f;

		[SerializeField]
		private Vector3 _defaultIngredientLocalPositionOffset = Vector3.zero;

		[SerializeField]
		private Vector3 _defaultIngredientLocalEulerAngles = Vector3.zero;

		[SerializeField]
		private List<IngredientVisualPlacementOverride> _ingredientVisualPlacementOverrides = new List<IngredientVisualPlacementOverride>();

		private ulong CurrentUserClientId
		{
			get
			{
				if (!(_serverActiveInteractor != null))
				{
					return ulong.MaxValue;
				}
				return _serverActiveInteractor.OwnerClientId;
			}
		}

		private bool HasActiveUser => _serverActiveInteractor != null;

		private bool IsCuttingActiveLocally
		{
			get
			{
				if (IsLocalActiveUser)
				{
					return _localCuttingActive;
				}
				return false;
			}
		}

		private bool IsLocalActiveUser
		{
			get
			{
				if (_currentInteractor != null)
				{
					return _currentInteractor.IsOwner;
				}
				return false;
			}
		}

		private bool CanRunLocalStationInput
		{
			get
			{
				if (IsLocalActiveUser && _canvasGO != null)
				{
					return _canvasGO.activeInHierarchy;
				}
				return false;
			}
		}

		private bool CanSelectItemLocally
		{
			get
			{
				if (CanRunLocalStationInput && !_localCuttingActive)
				{
					return CurrentState == PrepStationState.Idle;
				}
				return false;
			}
		}

		public PhysicalStateController PhysicalStateController => _physicalStateController;

		public List<GastropanRecallInteractor> GastropanRecallInteractors => _recallInteractors;

		public PrepStationState CurrentState => (PrepStationState)_currentState.Value;

		public BaseHoldable CurrentItem { get; set; }

		public GameObject CurrentItemGraphicObjectPrefab { get; set; }

		public Transform GastropanCenterTransform => _gastropanCenterPosition;

		public FixedString64Bytes CurrentIngredientID => _currentIngredientID.Value;

		public ItemDatabaseSO ItemDatabaseSO => _itemDatabaseSO;

		public ushort CurrentKnifeID { get; private set; }

		private bool KnifePressing => _isKnifeHeld;

		public bool HasPendingProcessedOutput => _hasPendingProcessedOutput;

		protected override bool UsesBaseExitInput => false;

		public int StaffEmployeeId => _staffEmployee.Value;

		public bool ServerStaffAvailable
		{
			get
			{
				if (base.IsServer && base.IsSpawned && _staffEmployee.Value == 0 && !InUse.Value && !HasActiveUser && _physicalStateController != null)
				{
					return _physicalStateController.IsPhysicallyUsable;
				}
				return false;
			}
		}

		public NetworkObject StaffPanPrefab
		{
			get
			{
				if (!(_gastroPanSpawner != null))
				{
					return null;
				}
				return _gastroPanSpawner.StaffPanPrefab;
			}
		}

		private string GetText(PrepStationInfoTextId id)
		{
			return id switch
			{
				PrepStationInfoTextId.PlaceItemOnTable => LocalizationDatabase.GetString("UiTable", "prep_station.instruction.place_item_on_table", "Place an item on the table"), 
				PrepStationInfoTextId.ClickKnife => LocalizationDatabase.GetString("UiTable", "prep_station.instruction.select_knife", "Select a knife"), 
				PrepStationInfoTextId.ClickGastroPan => LocalizationDatabase.GetString("UiTable", "prep_station.instruction.select_gastropan", "Select the GastroPan"), 
				PrepStationInfoTextId.ClickGastroPanPile => LocalizationDatabase.GetString("UiTable", "prep_station.instruction.select_prepared_ingredient_pile", "Select the prepared ingredient pile"), 
				_ => string.Empty, 
			};
		}

		public override bool IsAvailable()
		{
			if (StaffEmployeeId != 0)
			{
				return false;
			}
			if (base.IsOwner && !_gastroPanSpawner.ServerRequestSpawnAvailability())
			{
				UtilityMethods.Log("No space for a new GastroPan on table. Use failed.");
				Singleton<NotifyManager>.Instance.ShowToast("Station is full, clear some space");
				return false;
			}
			return base.IsAvailable();
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<FixedString64Bytes> staffRawVisual = _staffRawVisual;
			staffRawVisual.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Combine(staffRawVisual.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(StaffVisualChanged));
			StaffVisualChanged(default, _staffRawVisual.Value);
			CursorHelper.CursorVisible = false;
			_gastropanCenterInteractorImage.enabled = false;
			_knifeModel.gameObject.SetActive(value: false);
			_spawnPointList = new List<Transform>();
			foreach (Transform item in _spawnPointRoot)
			{
				_spawnPointList.Add(item);
			}
			InitCutProgressSlots();
			EnsureIngredientMeshCutController();
			_canvasGO.SetActive(value: false);
			_3Dtext.SetActive(value: false);
			_physicalStateController.OnPhysicallyUsableChanged += OnPhysicallyUsableChanged;
			NetworkVariable<byte> currentState = _currentState;
			currentState.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(currentState.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnCurrentStateChanged));
			NetworkVariable<bool> hasPendingProcessedOutputNetwork = _hasPendingProcessedOutputNetwork;
			hasPendingProcessedOutputNetwork.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(hasPendingProcessedOutputNetwork.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnPendingProcessedOutputChanged));
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
			ClientRestorePendingProcessedOutputIfNeeded();
		}

		public override void OnNetworkDespawn()
		{
			ClientCancelPrepCutSession(restoreSourcePresentation: true);
			NetworkVariable<FixedString64Bytes> staffRawVisual = _staffRawVisual;
			staffRawVisual.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Remove(staffRawVisual.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(StaffVisualChanged));
			_serverActiveInteractor = null;
			_physicalStateController.OnPhysicallyUsableChanged -= OnPhysicallyUsableChanged;
			NetworkVariable<byte> currentState = _currentState;
			currentState.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(currentState.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnCurrentStateChanged));
			NetworkVariable<bool> hasPendingProcessedOutputNetwork = _hasPendingProcessedOutputNetwork;
			hasPendingProcessedOutputNetwork.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(hasPendingProcessedOutputNetwork.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnPendingProcessedOutputChanged));
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			base.OnNetworkDespawn();
		}

		private void OnSelectedLocaleChanged(Locale locale)
		{
			if (IsLocalActiveUser)
			{
				ClientUpdateInstructionText(_currentInstructionTextId);
			}
		}

		private void OnCurrentStateChanged(byte previousValue, byte currentValue)
		{
			ClientRestorePendingProcessedOutputIfNeeded();
		}

		private void OnPendingProcessedOutputChanged(bool previousValue, bool currentValue)
		{
			ClientRestorePendingProcessedOutputIfNeeded();
		}

		private bool ShouldHideCursorForPrepStationUseMode()
		{
			if (!_localCuttingActive)
			{
				return CurrentState == PrepStationState.Cutting;
			}
			return true;
		}

		private void RefreshCursorForPrepStationUseMode()
		{
			if (IsLocalActiveUser)
			{
				if (ShouldHideCursorForPrepStationUseMode())
				{
					Cursor.visible = false;
				}
				else
				{
					CursorHelper.CursorVisible = true;
				}
			}
		}

		private void ShowCursorForLocalPrepStationUser()
		{
			if (IsLocalActiveUser)
			{
				CursorHelper.CursorVisible = true;
			}
		}

		private void OnPhysicallyUsableChanged(bool usable)
		{
			if (_inGaze)
			{
				DisableGazeHints();
				EnableGazeHints();
				if (usable)
				{
					_physicalUnlockController?.HideTooltipLocal();
				}
				else
				{
					_physicalUnlockController?.ShowTooltipLocal();
				}
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerSetStateRpc(byte newState)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3531767762u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in newState, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3531767762u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (StaffEmployeeId == 0)
				{
					_currentState.Value = newState;
				}
			}
		}

		private void OnPrepStationExit(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && !TryCloseOpenTutorialPopup())
			{
				_currentInteractor.ServerUseExitRpc();
			}
		}

		private bool TryCloseOpenTutorialPopup()
		{
			if (_stationTutorialPopupUI != null && _stationTutorialPopupUI.IsOpen)
			{
				_stationTutorialPopupUI.Close();
				RefreshCursorForPrepStationUseMode();
				return true;
			}
			StationTutorialPopupUI stationTutorialPopupUI = ((Singleton<GameUIManager>.Instance != null) ? Singleton<GameUIManager>.Instance.StationTutorialPopupUI : null);
			if (stationTutorialPopupUI == null || stationTutorialPopupUI == _stationTutorialPopupUI || !stationTutorialPopupUI.IsOpen)
			{
				return false;
			}
			stationTutorialPopupUI.Close();
			RefreshCursorForPrepStationUseMode();
			return true;
		}

		private void ClientUpdateInstructionText(PrepStationInfoTextId textId)
		{
			if (IsLocalActiveUser)
			{
				_currentInstructionTextId = textId;
				_instructionText.text = GetText(textId);
			}
		}

		public void PlayPrepStationSfxForActiveUser(PrepStationSfxId sfxId, float volume = 1f, bool useRandomPitch = false)
		{
			if (base.IsServer && HasActiveUser)
			{
				BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(CurrentUserClientId, RpcTargetUse.Temp);
				ReceivePrepStationSfxRpc(sfxId, Mathf.Clamp01(volume), useRandomPitch, baseRpcTarget);
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ReceivePrepStationSfxRpc(PrepStationSfxId sfxId, float volume, bool useRandomPitch, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(4234376363u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in sfxId, default(FastBufferWriter.ForEnums));
				bufferWriter.WriteValueSafe(in volume, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in useRandomPitch, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 4234376363u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (TryGetPrepStationSfx(sfxId, out var sfx))
				{
					PlayLocalPrepStationSfx(sfx, volume, useRandomPitch);
				}
			}
		}

		private bool TryGetPrepStationSfx(PrepStationSfxId sfxId, out AudioClipSO sfx)
		{
			sfx = sfxId switch
			{
				PrepStationSfxId.InventoryMove => _inventoryMoveSfx, 
				PrepStationSfxId.KnifeSelect => _knifeSelectSfx, 
				PrepStationSfxId.SpawnIngredient => _spawnIngredientSfx, 
				PrepStationSfxId.IngredientLand => _ingredientLandSfx, 
				PrepStationSfxId.IngredientsFallIntoGastroPan => _ingredientsFallIntoGastroPanSfx, 
				PrepStationSfxId.GastroPanSpawn => _gastroPanSpawnSfx, 
				PrepStationSfxId.GastroPanMove => _gastroPanMoveSfx, 
				_ => null, 
			};
			return sfx != null;
		}

		private void PlayLocalPrepStationSfx(AudioClipSO sfx, float volume = 1f, bool useRandomPitch = false)
		{
			if (sfx == null || !IsLocalActiveUser)
			{
				return;
			}
			AudioSource audioSource = Singleton<SoundManager>.Instance.PlaySFX(sfx, base.transform.position, base.transform);
			if (!(audioSource == null))
			{
				audioSource.volume *= Mathf.Clamp01(volume);
				if (useRandomPitch)
				{
					audioSource.pitch = GetPitch(useRandomPitch: true);
				}
			}
		}

		private float GetPitch(bool useRandomPitch)
		{
			if (!useRandomPitch)
			{
				return 1f;
			}
			if (_minPitch <= 0f || _maxPitch <= 0f)
			{
				return 1f;
			}
			if (_minPitch > _maxPitch)
			{
				return UnityEngine.Random.Range(_maxPitch, _minPitch);
			}
			return UnityEngine.Random.Range(_minPitch, _maxPitch);
		}

		private void PlayPopVFX()
		{
			if (!(_spawnPopVfx == null) && !(Time.time - _lastPopVfxTime < _spawnVfxMinInterval))
			{
				_lastPopVfxTime = Time.time;
				_spawnPopVfx.Play();
			}
		}

		private void PlayLandVFX()
		{
			if (!(_landVfx == null) && !(Time.time - _lastLandVfxTime < _spawnVfxMinInterval))
			{
				_lastLandVfxTime = Time.time;
				_landVfx.Play();
			}
		}

		private void PlayPopSFX()
		{
			if (!(Time.time - _lastPopSfxTime < _spawnSfxMinInterval))
			{
				_lastPopSfxTime = Time.time;
				PlayLocalPrepStationSfx(_spawnIngredientSfx);
			}
		}

		private void PlayLandSFX()
		{
			if (!(Time.time - _lastLandSfxTime < _spawnSfxMinInterval))
			{
				_lastLandSfxTime = Time.time;
				PlayLocalPrepStationSfx(_ingredientLandSfx);
			}
		}

		private void PlayRandomCutSuccessSfx()
		{
			if (!(_cuttingSFX == null) && !(_cuttingFoodSFX == null))
			{
				SoundManager instance = Singleton<SoundManager>.Instance;
				instance.PlaySFX(_cuttingSFX);
				instance.PlaySFX(_cuttingFoodSFX);
			}
		}

		private void HideHoldableForStation(BaseHoldable holdable)
		{
			if (!(holdable == null))
			{
				if (_hiddenHoldableForStation != null && _hiddenHoldableForStation != holdable)
				{
					RestoreHiddenHoldableForStation();
				}
				_hiddenHoldableForStation = holdable;
				_hiddenHoldableForStation.SetVisibility(visible: false);
			}
		}

		private void RestoreHiddenHoldableForStation()
		{
			if (!(_hiddenHoldableForStation == null))
			{
				_hiddenHoldableForStation.SetVisibility(visible: true);
				_hiddenHoldableForStation = null;
			}
		}

		private void ConsumeHiddenHoldableForStation()
		{
			_hiddenHoldableForStation = null;
		}

		private void ClearLocalCuttingRuntime()
		{
			ClientCancelPrepCutSession(restoreSourcePresentation: true);
			_localCuttingActive = false;
			_isKnifeHeld = false;
			_mustExitCutPoint = false;
			_successfulCutCount = 0;
			_requiredCutCount = 0;
			KillCutFlowDelay();
			ShowCursorForLocalPrepStationUser();
			if (_cuttingViewManager != null)
			{
				_cuttingViewManager.SetCuttingViewActive(isActive: false);
			}
			if (_knifeModel != null)
			{
				_knifeModel.gameObject.SetActive(value: false);
			}
			ResetCutProgressUI(deactivateParent: true);
		}

		public bool CanSelectKnife(int knifeID, ProcessedType processedType)
		{
			if (!CanRunLocalStationInput)
			{
				return false;
			}
			if (!CanUseKnifeOnCurrentItem(showToast: false))
			{
				return false;
			}
			return CurrentItemSupportsProcessedType(processedType);
		}

		public bool CurrentItemSupportsProcessedType(ProcessedType processedType)
		{
			if (processedType == ProcessedType.Empty)
			{
				return false;
			}
			IngredientSO currentIngredientSO = GetCurrentIngredientSO();
			if (currentIngredientSO == null)
			{
				return false;
			}
			return IngredientSupportsProcessedType(currentIngredientSO, processedType);
		}

		public bool ShouldShowQuestKnifeHighlight(ProcessedType processedType)
		{
			if (processedType == ProcessedType.Empty)
			{
				return false;
			}
			if (!CanSelectKnife(0, processedType))
			{
				return false;
			}
			return IsTutorialKnifeQuestActiveForProcessedType(processedType);
		}

		private bool IsTutorialKnifeQuestActiveForProcessedType(ProcessedType processedType)
		{
			QuestReferenceHub instance = QuestReferenceHub.Instance;
			if (instance == null)
			{
				return false;
			}
			return processedType switch
			{
				ProcessedType.LongStrip => instance.IsActive(QuestId.SliceCucumberAtPrepStation) || instance.IsActive(QuestId.MakeCucumberMaki), 
				ProcessedType.Sliced => instance.IsActive(QuestId.SliceMackerelFilletIntoNigiri) || instance.IsActive(QuestId.MakeMackerelNigiri), 
				_ => false, 
			};
		}

		private bool IngredientSupportsProcessedType(IngredientSO ingredient, ProcessedType processedType)
		{
			if (ingredient == null)
			{
				return false;
			}
			if (ingredient.ProcessedIngredients == null || ingredient.ProcessedIngredients.Count == 0)
			{
				return false;
			}
			for (int i = 0; i < ingredient.ProcessedIngredients.Count; i++)
			{
				ProcessedIngredientSO processedIngredientSO = ingredient.ProcessedIngredients[i];
				if (!(processedIngredientSO == null) && processedIngredientSO.ProcessedType == processedType)
				{
					return true;
				}
			}
			return false;
		}

		private ProcessedIngredientSO GetProcessedIngredientByType(IngredientSO ingredient, ProcessedType processedType)
		{
			if (ingredient == null)
			{
				return null;
			}
			if (ingredient.ProcessedIngredients == null || ingredient.ProcessedIngredients.Count == 0)
			{
				return null;
			}
			for (int i = 0; i < ingredient.ProcessedIngredients.Count; i++)
			{
				ProcessedIngredientSO processedIngredientSO = ingredient.ProcessedIngredients[i];
				if (!(processedIngredientSO == null) && processedIngredientSO.ProcessedType == processedType)
				{
					return processedIngredientSO;
				}
			}
			return null;
		}

		private IngredientSO GetCurrentIngredientSO()
		{
			if (CurrentItem != null && CurrentItem.ItemSO is IngredientSO result)
			{
				return result;
			}
			string text = _currentIngredientID.Value.ToString();
			if (string.IsNullOrEmpty(text))
			{
				return null;
			}
			return ItemDatabaseSO.FindById(text) as IngredientSO;
		}

		public bool HasItemOnPrepTable()
		{
			if (CurrentItem != null)
			{
				return CurrentItemGraphicObjectPrefab != null;
			}
			return false;
		}

		public override void GazeEnter()
		{
			base.GazeEnter();
			_3Dtext.SetActive(value: true);
			if (_physicalStateController != null && !_physicalStateController.IsPhysicallyUsable)
			{
				_physicalUnlockController?.ShowTooltipLocal();
			}
		}

		public override void GazeExit()
		{
			base.GazeExit();
			Singleton<HighlightManager>.Instance.ClearCurrentHighlight();
			_3Dtext.SetActive(value: false);
			_physicalUnlockController?.HideTooltipLocal();
		}

		public override void EnableGazeHints()
		{
			Singleton<HintsManager>.Instance.ClearAllHints();
			string locKey = (_physicalStateController.IsPhysicallyUsable ? "key_use" : "key_repair");
			Singleton<HintsManager>.Instance.AddHint("Interact", locKey);
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("Interact");
		}

		private void InitCutProgressSlots()
		{
			_cutProgressSlots.Clear();
			int num = Mathf.Min(_cutProgressParent.childCount, _maxCutSlots);
			for (int i = 0; i < num; i++)
			{
				RectTransform rectTransform = _cutProgressParent.GetChild(i) as RectTransform;
				if (rectTransform == null)
				{
					continue;
				}
				Image component = rectTransform.GetComponent<Image>();
				Image image = null;
				if (rectTransform.childCount > 0)
				{
					RectTransform rectTransform2 = rectTransform.GetChild(0) as RectTransform;
					if (rectTransform2 != null)
					{
						image = rectTransform2.GetComponent<Image>();
					}
				}
				if (!(component == null) && !(image == null))
				{
					component.gameObject.SetActive(value: false);
					image.gameObject.SetActive(value: false);
					image.rectTransform.localScale = Vector3.zero;
					_cutProgressSlots.Add(new CutProgressSlot
					{
						baseImage = component,
						fillImage = image
					});
				}
			}
		}

		public void ClientKnifeSelected(int knifeID, ProcessedType processedType)
		{
			if (CanRunLocalStationInput && CanUseKnifeOnCurrentItem())
			{
				if (!CurrentItemSupportsProcessedType(processedType))
				{
					Singleton<NotifyManager>.Instance.ShowToast("This cut type is not available for this item.");
					return;
				}
				_currentSelectedProcessedType = processedType;
				PlayLocalPrepStationSfx(_knifeSelectSfx, 0.8f);
				ServerBeginPrepCutSessionRpc(knifeID, (ushort)processedType);
			}
		}

		private bool CanUseKnifeOnCurrentItem(bool showToast = true)
		{
			if (CurrentItem == null || CurrentItemGraphicObjectPrefab == null)
			{
				if (showToast)
				{
					Singleton<NotifyManager>.Instance.ShowToast("No item on the table. You cannot use a knife.");
				}
				return false;
			}
			if (CurrentItem.ItemType != ItemType.RawIngredient)
			{
				if (showToast)
				{
					Singleton<NotifyManager>.Instance.ShowToast("Only raw ingredients can be cut here.");
				}
				return false;
			}
			if (CurrentItem.ItemSO is IngredientSO { PlacablePrepStation: false })
			{
				if (showToast)
				{
					Singleton<NotifyManager>.Instance.ShowToast("This item cannot be cut.");
				}
				return false;
			}
			return true;
		}

		private void ClientSetupCuttingUI(int requiredCutCount)
		{
			KillCutFlowDelay();
			_localCuttingActive = true;
			_requiredCutCount = requiredCutCount;
			_successfulCutCount = 0;
			_isKnifeHeld = false;
			_mustExitCutPoint = false;
			_hasLastKnifeDirection = false;
			_lastKnifeDirection = Vector3.zero;
			_currentTiltAngleY = 0f;
			if (_knifePressRoot != null)
			{
				_knifePressRoot.localEulerAngles = Vector3.zero;
			}
			_cuttingViewManager.SetCuttingViewActive(isActive: true);
			if (_knifeModel != null)
			{
				Singleton<DelayHelper>.Instance.DelayAction(_cuttingViewManager.CameraBlendTime, () =>
				{
					if (IsCuttingActiveLocally && _knifeModel != null)
					{
						_knifeModel.gameObject.SetActive(value: true);
					}
				});
			}
			RefreshCursorForPrepStationUseMode();
			ClientGenerateNextCutPoint();
			StartCutProgressUI();
			if (Singleton<QuickSlotHandler>.Instance != null)
			{
				Singleton<QuickSlotHandler>.Instance.gameObject.SetActive(value: false);
			}
		}

		private void StartCutProgressUI()
		{
			if (_cutProgressSlots.Count == 0)
			{
				return;
			}
			if (_cutProgressParent != null && !_cutProgressParent.gameObject.activeSelf)
			{
				_cutProgressParent.gameObject.SetActive(value: true);
			}
			foreach (CutProgressSlot cutProgressSlot2 in _cutProgressSlots)
			{
				if (cutProgressSlot2.baseImage != null)
				{
					cutProgressSlot2.baseImage.rectTransform.localScale = Vector3.one;
					cutProgressSlot2.baseImage.gameObject.SetActive(value: false);
				}
				if (cutProgressSlot2.fillImage != null)
				{
					cutProgressSlot2.fillImage.rectTransform.localScale = Vector3.zero;
					cutProgressSlot2.fillImage.gameObject.SetActive(value: false);
				}
			}
			int num = Mathf.Min(_requiredCutCount, _cutProgressSlots.Count);
			for (int i = 0; i < num; i++)
			{
				CutProgressSlot cutProgressSlot = _cutProgressSlots[i];
				if (cutProgressSlot.baseImage != null)
				{
					cutProgressSlot.baseImage.gameObject.SetActive(value: true);
					cutProgressSlot.baseImage.rectTransform.localScale = Vector3.one;
				}
				if (cutProgressSlot.fillImage != null)
				{
					cutProgressSlot.fillImage.gameObject.SetActive(value: true);
					cutProgressSlot.fillImage.rectTransform.localScale = Vector3.zero;
				}
			}
		}

		private void OnKnifeAction(InputActionPhase phase)
		{
			if (!IsCuttingActiveLocally)
			{
				UtilityMethods.Log("[PrepStation] Knife input ignored: local state is not Cutting.");
				return;
			}
			switch (phase)
			{
			case InputActionPhase.Started:
				HandleKnifePressed();
				break;
			case InputActionPhase.Canceled:
				HandleKnifeReleased();
				break;
			}
		}

		private void HandleKnifePressed()
		{
			_isKnifeHeld = true;
			bool mustExitCutPoint = IsKnifePassingThroughCutPointScreenSpace();
			_mustExitCutPoint = mustExitCutPoint;
		}

		private void HandleKnifeReleased()
		{
			_isKnifeHeld = false;
			_mustExitCutPoint = false;
		}

		private void Update()
		{
			if (!IsCuttingActiveLocally)
			{
				return;
			}
			FollowKnifeWithMouse();
			bool flag = IsKnifePassingThroughCutPointScreenSpace();
			if (_isKnifeHeld)
			{
				if (!flag)
				{
					_mustExitCutPoint = false;
				}
				else if (!_mustExitCutPoint)
				{
					ExecuteLiveCut();
					_mustExitCutPoint = true;
				}
			}
			if (_cuttingViewManager != null)
			{
				_cuttingViewManager.SetCutPointHoverState(flag);
			}
			UpdateKnifePressTilt();
		}

		private bool IsKnifePassingThroughCutPointScreenSpace()
		{
			if (_cuttingViewManager == null || !_cuttingViewManager.IsCutPointInteractable)
			{
				return false;
			}
			if (_cuttingViewManager.PlayerCamera == null || _cuttingViewManager.CutPointUI == null)
			{
				return false;
			}
			if (_knifeTip == null || _knifeHeel == null)
			{
				return false;
			}
			Camera playerCamera = _cuttingViewManager.PlayerCamera;
			RectTransform cutPointUI = _cuttingViewManager.CutPointUI;
			Vector3 vector = playerCamera.WorldToScreenPoint(_knifeTip.position);
			Vector3 vector2 = playerCamera.WorldToScreenPoint(_knifeHeel.position);
			if (vector.z <= 0f || vector2.z <= 0f)
			{
				return false;
			}
			Vector2 b = vector;
			Vector2 a = vector2;
			Vector3 vector3 = playerCamera.WorldToScreenPoint(cutPointUI.position);
			if (vector3.z <= 0f)
			{
				return false;
			}
			Vector2 p = vector3;
			cutPointUI.GetWorldCorners(_worldCorners);
			Vector2 a2 = playerCamera.WorldToScreenPoint(_worldCorners[0]);
			Vector2 b2 = playerCamera.WorldToScreenPoint(_worldCorners[2]);
			float num = Vector2.Distance(a2, b2) * 0.25f;
			return DistancePointToSegment(p, a, b) <= num + _knifeThickness;
		}

		private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
		{
			Vector2 vector = b - a;
			float sqrMagnitude = vector.sqrMagnitude;
			if (sqrMagnitude <= 1E-05f)
			{
				return Vector2.Distance(p, a);
			}
			float value = Vector2.Dot(p - a, vector) / sqrMagnitude;
			value = Mathf.Clamp01(value);
			Vector2 b2 = a + vector * value;
			return Vector2.Distance(p, b2);
		}

		private void FollowKnifeWithMouse()
		{
			if (!(_knifeModel == null) && !(_cuttingViewManager == null) && !(_cuttingViewManager.PlayerCamera == null) && Mouse.current != null)
			{
				Vector3 position = Mouse.current.position.ReadValue();
				position.z = _distanceFromCamera;
				Vector3 vector = _cuttingViewManager.PlayerCamera.ScreenToWorldPoint(position);
				Vector3 direction = vector - _knifeModel.position;
				ApplyKnifeTransform(vector, direction);
			}
		}

		private void ApplyKnifeTransform(Vector3 targetPosition, Vector3 direction)
		{
			if (_knifeModel == null)
			{
				return;
			}
			_knifeModel.position = targetPosition;
			Camera playerCamera = _cuttingViewManager.PlayerCamera;
			Vector3 vector = Vector3.ProjectOnPlane(direction, playerCamera.transform.forward);
			if (vector.sqrMagnitude <= 1E-06f)
			{
				if (!_hasLastKnifeDirection)
				{
					return;
				}
				vector = _lastKnifeDirection;
			}
			else
			{
				vector.Normalize();
			}
			if (_hasLastKnifeDirection && Vector3.Angle(_lastKnifeDirection, vector) < _knifeMinAngleDelta)
			{
				vector = _lastKnifeDirection;
			}
			float num = Mathf.Atan2(vector.x, vector.z) * 57.29578f;
			float angle = ((Mouse.current != null && Mouse.current.leftButton.isPressed) ? 0f : 90f);
			Quaternion b = Quaternion.AngleAxis(num - 90f, Vector3.up) * Quaternion.AngleAxis(angle, Vector3.right) * Quaternion.AngleAxis(90f, Vector3.forward);
			_knifeModel.rotation = Quaternion.Slerp(_knifeModel.rotation, b, _knifeRotationSmooth * Time.deltaTime);
			_lastKnifeDirection = vector;
			_hasLastKnifeDirection = true;
		}

		private void UpdateKnifePressTilt()
		{
			if (!(_knifePressRoot == null))
			{
				float b = (KnifePressing ? _knifeDownTiltAngle : 0f);
				_currentTiltAngleY = Mathf.Lerp(_currentTiltAngleY, b, _knifeDownTiltSmooth * Time.deltaTime);
				Vector3 localEulerAngles = _knifePressRoot.localEulerAngles;
				localEulerAngles.y = 0f - _currentTiltAngleY;
				_knifePressRoot.localEulerAngles = localEulerAngles;
			}
		}

		private void ExecuteLiveCut()
		{
			int sequence = _successfulCutCount + 1;
			Vector3 cutPoint = ((_cuttingViewManager != null) ? _cuttingViewManager.CutPoint : Vector3.zero);
			Vector3 planeNormal = ResolveIngredientMeshCutPlaneNormal();
			Vector3 cutDirection = ResolveIngredientCutDirection();
			if (_ingredientMeshCutController == null || !_ingredientMeshCutController.IsSessionActive)
			{
				UtilityMethods.Log($"[PrepStation Mesh Cutting] Live cut rejected because the local mesh session is missing. Sequence={sequence}", LogType.Error);
			}
			else if (!_ingredientMeshCutController.IsCutInProgress)
			{
				_cuttingViewManager.SetCutPointInteractionEnabled(enabled: false);
				_mustExitCutPoint = true;
				if (!_ingredientMeshCutController.RequestCut(sequence, cutPoint, planeNormal, (bool success) =>
				{
					if (IsCuttingActiveLocally)
					{
						if (!success)
						{
							UtilityMethods.Log($"[PrepStation Mesh Cutting] Cut transaction failed. Sequence={sequence}, " + $"IngredientId={CurrentIngredientID}", LogType.Error);
							_cuttingViewManager.SetCutPointInteractionEnabled(enabled: true);
						}
						else
						{
							ClientOnCutPointSuccessful(sequence, cutPoint, planeNormal, cutDirection);
						}
					}
				}))
				{
					UtilityMethods.Log($"[PrepStation Mesh Cutting] Cut request was rejected locally. Sequence={sequence}", LogType.Error);
					_cuttingViewManager.SetCutPointInteractionEnabled(enabled: true);
				}
			}
		}

		private void ClientOnCutPointSuccessful(int sequence, Vector3 cutPoint, Vector3 planeNormal, Vector3 cutDirection)
		{
			UtilityMethods.Log("[PrepStation] Cut success: passed through UI cut point.");
			_successfulCutCount++;
			ClientReplicateSuccessfulPrepCut(sequence, cutPoint, planeNormal, cutDirection);
			ClientPlayCutFeedback();
			PlayRandomCutSuccessSfx();
			float knifeBladeScreenAngleZ = GetKnifeBladeScreenAngleZ();
			float feedbackDuration = ((_cuttingViewManager != null) ? _cuttingViewManager.PlayCutPointSuccessFeedback(knifeBladeScreenAngleZ) : 0f);
			_mustExitCutPoint = true;
			if (_successfulCutCount < _requiredCutCount)
			{
				DelayNextCutPoint(feedbackDuration);
				return;
			}
			_ingredientMeshCutController?.HidePresentationIfFinalCut(sequence);
			DelayCuttingCompleted(feedbackDuration);
		}

		private float GetKnifeBladeScreenAngleZ()
		{
			if (_cuttingViewManager == null || _cuttingViewManager.PlayerCamera == null)
			{
				return 0f;
			}
			if (_knifeTip == null || _knifeHeel == null)
			{
				if (!(_cuttingViewManager.CutPointUI != null))
				{
					return 0f;
				}
				return _cuttingViewManager.CutPointUI.localEulerAngles.z;
			}
			Camera playerCamera = _cuttingViewManager.PlayerCamera;
			Vector3 vector = playerCamera.WorldToScreenPoint(_knifeTip.position);
			Vector3 vector2 = playerCamera.WorldToScreenPoint(_knifeHeel.position);
			if (vector.z <= 0f || vector2.z <= 0f)
			{
				if (!(_cuttingViewManager.CutPointUI != null))
				{
					return 0f;
				}
				return _cuttingViewManager.CutPointUI.localEulerAngles.z;
			}
			Vector2 vector3 = (Vector2)vector - (Vector2)vector2;
			if (vector3.sqrMagnitude < 0.0001f)
			{
				if (!(_cuttingViewManager.CutPointUI != null))
				{
					return 0f;
				}
				return _cuttingViewManager.CutPointUI.localEulerAngles.z;
			}
			return Mathf.Atan2(vector3.y, vector3.x) * 57.29578f;
		}

		private void DelayNextCutPoint(float feedbackDuration)
		{
			KillCutFlowDelay();
			float delay = Mathf.Max(0.01f, feedbackDuration + _nextCutPointDelayPadding);
			_cutFlowDelayTween = DOVirtual.DelayedCall(delay, () =>
			{
				if (IsCuttingActiveLocally)
				{
					ClientGenerateNextCutPoint();
				}
			});
		}

		private void DelayCuttingCompleted(float feedbackDuration)
		{
			KillCutFlowDelay();
			float delay = Mathf.Max(0.01f, feedbackDuration + _nextCutPointDelayPadding);
			_cutFlowDelayTween = DOVirtual.DelayedCall(delay, () =>
			{
				if (IsCuttingActiveLocally)
				{
					ClientCuttingCompleted();
				}
			});
		}

		private void KillCutFlowDelay()
		{
			if (_cutFlowDelayTween != null && _cutFlowDelayTween.IsActive())
			{
				_cutFlowDelayTween.Kill();
			}
			_cutFlowDelayTween = null;
		}

		private void ClientGenerateNextCutPoint()
		{
			if (IsCuttingActiveLocally && !(_cuttingViewManager == null))
			{
				if (_ingredientMeshCutController != null && _ingredientMeshCutController.TryGetNextCutPoint(out var cutPoint, out var displayPosition, out var displayRotationZ))
				{
					_cuttingViewManager.SetCutPoint(cutPoint, displayPosition, displayRotationZ);
				}
				else
				{
					UtilityMethods.Log("[PrepStation Mesh Cutting] Next cut point could not be generated from the retained mesh.", LogType.Error);
				}
			}
		}

		private void ClientCuttingCompleted()
		{
			KillCutFlowDelay();
			_localCuttingActive = false;
			ShowCursorForLocalPrepStationUser();
			if (_cuttingViewManager != null)
			{
				_cuttingViewManager.SetCuttingViewActive(isActive: false);
			}
			if (_knifeModel != null)
			{
				_knifeModel.gameObject.SetActive(value: false);
			}
			_successfulCutCount = 0;
			HandleKnifeReleased();
			ServerCompletePrepCutSessionRpc(_clientCutSessionVersion);
			ResetCutProgressUI(deactivateParent: true);
		}

		private void ClientConsumeIngredientAfterCutCompletion()
		{
			if (IsLocalActiveUser)
			{
				QuickSlotHandler instance = Singleton<QuickSlotHandler>.Instance;
				if (instance != null)
				{
					instance.DestroyCurrentItem();
					instance.gameObject.SetActive(value: true);
				}
				ConsumeHiddenHoldableForStation();
				if (instance != null)
				{
					HideHoldableForStation(instance.SelectedItem);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerDestroyCurrentItemGraphicRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(2576081075u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2576081075u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (StaffEmployeeId == 0)
				{
					DestroyCurrentItemGraphicRpc();
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void DestroyCurrentItemGraphicRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3958363457u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3958363457u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!(CurrentItemGraphicObjectPrefab == null))
				{
					UnityEngine.Object.Destroy(CurrentItemGraphicObjectPrefab);
					CurrentItemGraphicObjectPrefab = null;
				}
			}
		}

		private void ClientPlayCutFeedback()
		{
			PlayCutItemFlash();
			UpdateCutProgressUI();
		}

		private void PlayCutParticle(Vector3 cutPoint, Vector3 cutDirection)
		{
			if (!(_cutParticlePrefab == null) && IsFiniteVector(cutPoint))
			{
				Vector3 up = base.transform.up;
				Vector3 upwards = Vector3.ProjectOnPlane(cutDirection, up);
				if (upwards.sqrMagnitude <= 0.0001f)
				{
					upwards = Vector3.ProjectOnPlane(base.transform.forward, up);
				}
				if (upwards.sqrMagnitude <= 0.0001f)
				{
					upwards = Vector3.forward;
				}
				upwards.Normalize();
				Quaternion rotation = Quaternion.LookRotation(up, upwards) * _cutParticlePrefab.transform.localRotation;
				ParticleSystem particleSystem = UnityEngine.Object.Instantiate(_cutParticlePrefab, cutPoint, rotation);
				particleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
				ParticleSystem.MainModule main = particleSystem.main;
				main.loop = false;
				main.stopAction = ParticleSystemStopAction.Destroy;
				particleSystem.Play(withChildren: true);
			}
		}

		private void PlayCutItemFlash()
		{
			Transform transform = ((_ingredientMeshCutController != null) ? _ingredientMeshCutController.PresentationRoot : null);
			if (!(transform == null))
			{
				transform.DOKill();
				transform.DOPunchScale(Vector3.one * _cutItemScalePunch, _cutItemScaleDuration, 4, 0.45f);
			}
		}

		private void UpdateCutProgressUI()
		{
			if (_cutProgressSlots == null || _cutProgressSlots.Count == 0)
			{
				return;
			}
			int num = _successfulCutCount - 1;
			if (num >= 0 && num < _cutProgressSlots.Count)
			{
				CutProgressSlot cutProgressSlot = _cutProgressSlots[num];
				if (!(cutProgressSlot.fillImage == null) && !(cutProgressSlot.baseImage == null))
				{
					RectTransform rectTransform = cutProgressSlot.fillImage.rectTransform;
					rectTransform.DOKill();
					rectTransform.localScale = Vector3.zero;
					rectTransform.DOScale(Vector3.one, _cutFillDuration).SetEase(Ease.OutBack);
					RectTransform rectTransform2 = cutProgressSlot.baseImage.rectTransform;
					rectTransform2.DOKill();
					rectTransform2.localScale = Vector3.one;
					rectTransform2.DOPunchScale(Vector3.one * (_cutDotPunchScale - 1f), _cutFillDuration, 5, 0.6f);
				}
			}
		}

		public void ResetCutProgressUI(bool deactivateParent = false)
		{
			foreach (CutProgressSlot cutProgressSlot in _cutProgressSlots)
			{
				if (cutProgressSlot.baseImage != null)
				{
					cutProgressSlot.baseImage.DOKill();
					cutProgressSlot.baseImage.rectTransform.localScale = Vector3.one;
					cutProgressSlot.baseImage.gameObject.SetActive(value: false);
				}
				if (cutProgressSlot.fillImage != null)
				{
					cutProgressSlot.fillImage.DOKill();
					cutProgressSlot.fillImage.rectTransform.localScale = Vector3.zero;
					cutProgressSlot.fillImage.gameObject.SetActive(value: false);
				}
			}
			if (deactivateParent && _cutProgressParent != null)
			{
				_cutProgressParent.gameObject.SetActive(value: false);
			}
		}

		private void OnSlotNumber(InputActionPhase phase, int input)
		{
			if (phase == InputActionPhase.Started && TrySelectQuickSlotItem(input))
			{
				PlayLocalPrepStationSfx(_inventoryMoveSfx, 0.5f);
				ClientSetupCurrentItemPrefab();
			}
		}

		public void OnScroll(InputActionPhase phase, Vector2 input)
		{
			if (CanSelectItemLocally && !(Singleton<QuickSlotHandler>.Instance == null) && Singleton<QuickSlotHandler>.Instance.TryScrollSlotFromInput(phase, input))
			{
				PlayLocalPrepStationSfx(_inventoryMoveSfx, 0.5f);
				if (TryUseSelectedQuickSlotItem())
				{
					ClientSetupCurrentItemPrefab();
				}
			}
		}

		private bool TrySelectQuickSlotItem(int input)
		{
			if (!CanSelectItemLocally)
			{
				return false;
			}
			if (Singleton<QuickSlotHandler>.Instance == null)
			{
				return false;
			}
			if (!Singleton<QuickSlotHandler>.Instance.TrySelectSlotFromInput(InputActionPhase.Started, input, allowActiveToolFallback: false))
			{
				return false;
			}
			return TryUseSelectedQuickSlotItem();
		}

		public bool TrySelectValidItem(int input)
		{
			return TrySelectQuickSlotItem(input);
		}

		private bool TryUseSelectedQuickSlotItem()
		{
			if (!CanSelectItemLocally)
			{
				return false;
			}
			if (Singleton<QuickSlotHandler>.Instance == null)
			{
				return false;
			}
			CurrentItem = Singleton<QuickSlotHandler>.Instance.SelectedItem;
			if (CurrentItem == null)
			{
				HandleInvalidItem();
				return false;
			}
			if (CurrentItem.ItemType == ItemType.RawIngredient && CurrentItem.ItemSO is IngredientSO { PlacablePrepStation: not false })
			{
				return true;
			}
			HandleInvalidItem();
			return false;
		}

		[Rpc(SendTo.Owner)]
		public void ClientAutoSelectItemRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1141873982u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1141873982u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_currentInteractor == null)
			{
				return;
			}
			QuickSlotHandler instance = Singleton<QuickSlotHandler>.Instance;
			if (!(instance == null))
			{
				if (instance.SelectedItem == null)
				{
					instance.AutoSelectSlot();
				}
				CurrentItem = instance.SelectedItem;
				if (CurrentItem == null)
				{
					HandleInvalidItem();
					ClientUpdateInstructionText(PrepStationInfoTextId.PlaceItemOnTable);
				}
				else if (!(CurrentItem.ItemSO is IngredientSO ingredientSO))
				{
					HandleInvalidItem(autoSelect: true);
					ClientUpdateInstructionText(PrepStationInfoTextId.PlaceItemOnTable);
				}
				else if (!ingredientSO.PlacablePrepStation)
				{
					ClientUpdateInstructionText(PrepStationInfoTextId.PlaceItemOnTable);
				}
				else
				{
					ClientSetupCurrentItemPrefab();
					ClientUpdateInstructionText(PrepStationInfoTextId.ClickKnife);
				}
			}
		}

		private void HandleInvalidItem(bool autoSelect = false)
		{
			ClientCancelPrepCutSession(restoreSourcePresentation: true);
			if (CurrentItemGraphicObjectPrefab != null)
			{
				UnityEngine.Object.Destroy(CurrentItemGraphicObjectPrefab);
				CurrentItemGraphicObjectPrefab = null;
			}
			ClientUpdateInstructionText(PrepStationInfoTextId.PlaceItemOnTable);
			if (CurrentItem != null)
			{
				if (CurrentItem.TryGetComponent<DeadFish>(out var _))
				{
					Singleton<NotifyManager>.Instance.ShowToast("Use the fillet machine first");
				}
				else if (!autoSelect)
				{
					Singleton<NotifyManager>.Instance.ShowToast("You can’t use that item here");
				}
			}
			CurrentItem = null;
		}

		private void ClientSetupCurrentItemPrefab()
		{
			if (CurrentItem == null)
			{
				UtilityMethods.Log("[PrepStation] ClientSetupCurrentItemPrefab -> CurrentItem is null");
				return;
			}
			if (CurrentItem.NetworkObject == null)
			{
				UtilityMethods.Log("[PrepStation] ClientSetupCurrentItemPrefab -> CurrentItem.NetworkObject is null");
				return;
			}
			if (CurrentItem.ItemSO == null)
			{
				UtilityMethods.Log("[PrepStation] ClientSetupCurrentItemPrefab -> CurrentItem.ItemSO is null");
				return;
			}
			ServerRequestSetupCurrentIngredientVisualRpc(CurrentItem.NetworkObject.ToReference());
			HideHoldableForStation(CurrentItem);
			ServerUpdateCurrentIngredientRpc((ushort)CurrentItem.ItemSO.ItemType, CurrentItem.ItemSO.Id);
			ClientUpdateInstructionText(PrepStationInfoTextId.ClickKnife);
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestSetupCurrentIngredientVisualRpc(NetworkObjectReference objRef)
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
				FastBufferWriter bufferWriter = __beginSendRpc(828923149u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in objRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 828923149u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (StaffEmployeeId == 0)
			{
				if (!objRef.TryGet(out var networkObject) || networkObject == null)
				{
					UtilityMethods.Log("[ChefDeskGastroPan] ServerCagriRpc -> netObj not found from reference");
				}
				else
				{
					ClientsSetupCurrentIngredientVisualRpc(objRef);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientsSetupCurrentIngredientVisualRpc(NetworkObjectReference objRef)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2040171445u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in objRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 2040171445u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!objRef.TryGet(out var networkObject) || networkObject == null || !networkObject.TryGetComponent<BaseHoldable>(out var component) || component == null || component.ItemSO == null)
			{
				return;
			}
			GameObject graphicObjectPrefab = component.ItemSO.GraphicObjectPrefab;
			if (graphicObjectPrefab == null)
			{
				UtilityMethods.Log("[ChefDeskGastroPan] ClientsSetupCurrentIngredientVisualRpc -> GraphicObjectPrefab is null");
				return;
			}
			ClientFinalizePendingCutBeforeReplacingIngredientVisual();
			if (CurrentItemGraphicObjectPrefab != null)
			{
				ClientCancelPrepCutSession(restoreSourcePresentation: true);
				UnityEngine.Object.Destroy(CurrentItemGraphicObjectPrefab);
				CurrentItemGraphicObjectPrefab = null;
			}
			GameObject gameObject = UnityEngine.Object.Instantiate(graphicObjectPrefab, _ingredientCenterPosition, worldPositionStays: false);
			if (gameObject == null)
			{
				UtilityMethods.Log("[ChefDeskGastroPan] ClientsSetupCurrentIngredientVisualRpc -> Instantiate returned null");
				return;
			}
			CurrentItemGraphicObjectPrefab = gameObject;
			IngredientSO ingredientSO = component.ItemSO as IngredientSO;
			ApplyIngredientVisualPlacement(gameObject, ingredientSO);
			ClientTryStartPendingPrepCutSession();
		}

		private void EnsureIngredientMeshCutController()
		{
			if (_ingredientMeshCutController == null)
			{
				_ingredientMeshCutController = GetComponent<PrepStationIngredientMeshCutController>();
			}
			if (_ingredientMeshCutController == null)
			{
				_ingredientMeshCutController = base.gameObject.AddComponent<PrepStationIngredientMeshCutController>();
			}
			_ingredientMeshCutController.Initialize();
		}

		[Rpc(SendTo.Server)]
		private void ServerBeginPrepCutSessionRpc(int knifeID, ushort processedTypeValue, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2585979711u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, knifeID);
				BytePacker.WriteValueBitPacked(bufferWriter, processedTypeValue);
				__endSendRpc(ref bufferWriter, 2585979711u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (StaffEmployeeId != 0)
			{
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!ServerIsActivePrepStationUser(senderClientId) || CurrentState != PrepStationState.Idle)
			{
				UtilityMethods.Log($"[PrepStation Mesh Cutting] Session start rejected. SenderClientId={senderClientId}, " + $"ActiveClientId={CurrentUserClientId}, State={CurrentState}", LogType.Warning);
			}
			else
			{
				if (knifeID < 0)
				{
					return;
				}
				ProcessedType processedType = (ProcessedType)processedTypeValue;
				IngredientSO currentIngredientSO = GetCurrentIngredientSO();
				if (currentIngredientSO == null || !currentIngredientSO.PlacablePrepStation)
				{
					UtilityMethods.Log("[PrepStation Mesh Cutting] Session start rejected because the current ingredient is invalid.", LogType.Warning);
					return;
				}
				ProcessedIngredientSO processedIngredientByType = GetProcessedIngredientByType(currentIngredientSO, processedType);
				if (processedIngredientByType == null || processedIngredientByType.OutputYield <= 0)
				{
					UtilityMethods.Log("[PrepStation Mesh Cutting] Session start rejected. Ingredient=" + currentIngredientSO.name + ", " + $"ProcessedType={processedType}", LogType.Warning);
					return;
				}
				int num = ((_spawnPointList != null) ? _spawnPointList.Count : 0);
				if (num <= 0)
				{
					UtilityMethods.Log("[PrepStation Mesh Cutting] Session start rejected because processed spawn points are missing.", LogType.Error);
					return;
				}
				int num2 = Mathf.Min(5, processedIngredientByType.OutputYield);
				int num3 = UnityEngine.Random.Range(Mathf.Min(3, num2), num2 + 1);
				int num4 = (num / 2 - processedIngredientByType.OutputYield / 2) % num;
				if (num4 < 0)
				{
					num4 += num;
				}
				CurrentKnifeID = (ushort)knifeID;
				_currentSelectedProcessedType = processedType;
				_currentState.Value = 1;
				_serverCutSessionVersion++;
				_serverCutSequence = 0;
				_serverCutRequiredCount = num3;
				_serverCutOutputYield = processedIngredientByType.OutputYield;
				_serverCutStartIndex = num4;
				_serverCutProcessedId = processedIngredientByType.Id;
				_serverCutSessionActive = true;
				if (processedType == ProcessedType.LongStrip)
				{
					QuestReferenceHub.Instance?.CompleteIfActive(QuestId.SelectLongStripKnifePrepStation);
				}
				if (processedType == ProcessedType.Sliced)
				{
					QuestReferenceHub.Instance?.CompleteIfActive(QuestId.SelectNigiriCutKnifePrepStation);
				}
				ClientBeginPrepCutSessionRpc(_serverCutSessionVersion, currentIngredientSO.Id, processedIngredientByType.Id, processedIngredientByType.OutputYield, num3, num4);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientBeginPrepCutSessionRpc(int sessionVersion, FixedString64Bytes ingredientId, FixedString64Bytes processedId, int outputYield, int requiredCutCount, int startIndex)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2745064174u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sessionVersion);
				bufferWriter.WriteValueSafe(in ingredientId, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in processedId, default(FastBufferWriter.ForFixedStrings));
				BytePacker.WriteValueBitPacked(bufferWriter, outputYield);
				BytePacker.WriteValueBitPacked(bufferWriter, requiredCutCount);
				BytePacker.WriteValueBitPacked(bufferWriter, startIndex);
				__endSendRpc(ref bufferWriter, 2745064174u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ClientCancelPrepCutSession(restoreSourcePresentation: true);
			ClientDespawnIngredientVisuals();
			_clientCutSessionVersion = sessionVersion;
			_clientCutIngredientId = ingredientId;
			_clientCutProcessedId = processedId;
			_clientCutOutputYield = outputYield;
			_clientCutRequiredCount = requiredCutCount;
			_clientCutStartIndex = startIndex;
			_clientCutSessionPending = true;
			bool flag = ClientTryStartPendingPrepCutSession();
			if (IsLocalActiveUser)
			{
				if (!flag)
				{
					UtilityMethods.Log($"[PrepStation Mesh Cutting] Owner session could not start. SessionVersion={sessionVersion}, " + $"IngredientId={CurrentIngredientID}", LogType.Error);
					ServerAbortPrepCutSessionRpc(sessionVersion);
				}
				else
				{
					ClientSetupCuttingUI(requiredCutCount);
				}
			}
		}

		private bool ClientTryStartPendingPrepCutSession()
		{
			if (!_clientCutSessionPending)
			{
				if (_ingredientMeshCutController != null)
				{
					return _ingredientMeshCutController.IsSessionActive;
				}
				return false;
			}
			if (CurrentItemGraphicObjectPrefab == null || _ingredientCenterPosition == null)
			{
				return false;
			}
			if (!(ItemDatabaseSO.FindById(_clientCutIngredientId.ToString()) is IngredientSO ingredientSO) || ingredientSO.PrepStationCuttingPrefab == null)
			{
				UtilityMethods.Log($"[PrepStation Mesh Cutting] Generated cutting prefab is missing. IngredientId={_clientCutIngredientId}", LogType.Error);
				return false;
			}
			EnsureIngredientMeshCutController();
			if (!_ingredientMeshCutController.TryStartSession(ingredientSO.PrepStationCuttingPrefab, CurrentItemGraphicObjectPrefab, _ingredientCenterPosition, base.transform.up, _clientCutRequiredCount, out var failureReason))
			{
				UtilityMethods.Log("[PrepStation Mesh Cutting] Runtime session start failed. Ingredient=" + ingredientSO.name + ", Reason=" + failureReason, LogType.Error);
				return false;
			}
			_clientCutSessionPending = false;
			return true;
		}

		[Rpc(SendTo.Server)]
		private void ServerAbortPrepCutSessionRpc(int sessionVersion, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2604766197u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sessionVersion);
				__endSendRpc(ref bufferWriter, 2604766197u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (StaffEmployeeId == 0)
			{
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				if (ServerIsActivePrepStationUser(senderClientId) && _serverCutSessionActive && sessionVersion == _serverCutSessionVersion)
				{
					ServerResetStationInternal();
				}
			}
		}

		private Vector3 ResolveIngredientMeshCutPlaneNormal()
		{
			Vector3 up = base.transform.up;
			Vector3 vector = ((_knifeTip != null && _knifeHeel != null) ? (_knifeTip.position - _knifeHeel.position) : Vector3.zero);
			vector = Vector3.ProjectOnPlane(vector, up);
			Vector3 vector2 = ((vector.sqrMagnitude > 0.0001f) ? Vector3.Cross(up, vector).normalized : ((_ingredientMeshCutController != null) ? _ingredientMeshCutController.CutAxis : base.transform.forward));
			if (!(_ingredientMeshCutController != null) || !_ingredientMeshCutController.IsSessionActive)
			{
				return vector2;
			}
			return _ingredientMeshCutController.ConstrainPlaneNormal(vector2, _ingredientMeshCutMaxPlaneAngle);
		}

		private Vector3 ResolveIngredientCutDirection()
		{
			Vector3 up = base.transform.up;
			Vector3 vector = (_hasLastKnifeDirection ? _lastKnifeDirection : base.transform.right);
			vector = Vector3.ProjectOnPlane(vector, up);
			if (vector.sqrMagnitude <= 0.0001f)
			{
				vector = Vector3.ProjectOnPlane(base.transform.right, up);
			}
			if (!(vector.sqrMagnitude > 0.0001f))
			{
				return Vector3.right;
			}
			return vector.normalized;
		}

		private void ClientReplicateSuccessfulPrepCut(int sequence, Vector3 cutPoint, Vector3 planeNormal, Vector3 cutDirection)
		{
			_clientAppliedCutSequence = sequence;
			ClientSpawnProcessedVisualsForCut(sequence, cutPoint, cutDirection);
			ServerRegisterPrepCutRpc(_clientCutSessionVersion, sequence, cutPoint, planeNormal, cutDirection);
		}

		[Rpc(SendTo.Server)]
		private void ServerRegisterPrepCutRpc(int sessionVersion, int sequence, Vector3 cutPoint, Vector3 planeNormal, Vector3 cutDirection, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3301829188u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sessionVersion);
				BytePacker.WriteValueBitPacked(bufferWriter, sequence);
				bufferWriter.WriteValueSafe(in cutPoint);
				bufferWriter.WriteValueSafe(in planeNormal);
				bufferWriter.WriteValueSafe(in cutDirection);
				__endSendRpc(ref bufferWriter, 3301829188u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (StaffEmployeeId != 0)
			{
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!ServerIsActivePrepStationUser(senderClientId) || CurrentState != PrepStationState.Cutting || !_serverCutSessionActive || sessionVersion != _serverCutSessionVersion)
			{
				UtilityMethods.Log($"[PrepStation Mesh Cutting] Cut replication rejected. SenderClientId={senderClientId}, " + $"SessionVersion={sessionVersion}, ActiveSessionVersion={_serverCutSessionVersion}, State={CurrentState}", LogType.Warning);
				return;
			}
			int num = _serverCutSequence + 1;
			if (sequence != num || sequence > _serverCutRequiredCount)
			{
				UtilityMethods.Log($"[PrepStation Mesh Cutting] Cut sequence rejected. Sequence={sequence}, " + $"Expected={num}, Required={_serverCutRequiredCount}", LogType.Warning);
			}
			else if (IsFiniteVector(cutPoint) && IsFiniteVector(planeNormal) && IsFiniteVector(cutDirection) && !(planeNormal.sqrMagnitude <= 0.0001f) && !(cutDirection.sqrMagnitude <= 0.0001f) && !((cutPoint - base.transform.position).sqrMagnitude > 4f))
			{
				Vector3 normalized = planeNormal.normalized;
				if (!(Mathf.Abs(Vector3.Dot(normalized, base.transform.up)) > 0.25f))
				{
					_serverCutSequence = sequence;
					ClientApplyPrepCutForObserversRpc(sessionVersion, sequence, cutPoint, normalized, cutDirection.normalized);
				}
			}
		}

		[Rpc(SendTo.NotOwner)]
		private void ClientApplyPrepCutForObserversRpc(int sessionVersion, int sequence, Vector3 cutPoint, Vector3 planeNormal, Vector3 cutDirection)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2544185255u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sessionVersion);
				BytePacker.WriteValueBitPacked(bufferWriter, sequence);
				bufferWriter.WriteValueSafe(in cutPoint);
				bufferWriter.WriteValueSafe(in planeNormal);
				bufferWriter.WriteValueSafe(in cutDirection);
				__endSendRpc(ref bufferWriter, 2544185255u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (sessionVersion == _clientCutSessionVersion && !_clientMeshReplicationFailed)
			{
				int num = _clientReceivedCutSequence + 1;
				if (sequence != num || sequence > _clientCutRequiredCount)
				{
					UtilityMethods.Log($"[PrepStation Mesh Cutting] Observer cut sequence is invalid. Sequence={sequence}, " + $"Expected={num}, SessionVersion={sessionVersion}", LogType.Warning);
					return;
				}
				_clientObserverCutQueue.Enqueue(new ReplicatedPrepCut(sessionVersion, sequence, cutPoint, planeNormal, cutDirection));
				_clientReceivedCutSequence = sequence;
				ClientTryProcessNextObserverCut();
			}
		}

		private void ClientTryProcessNextObserverCut()
		{
			if (_clientMeshReplicationFailed || _clientObserverCutQueue.Count == 0 || (_ingredientMeshCutController != null && _ingredientMeshCutController.IsCutInProgress))
			{
				ClientTryFinalizePendingPrepCutCompletion();
				return;
			}
			if ((_ingredientMeshCutController == null || !_ingredientMeshCutController.IsSessionActive) && !ClientTryStartPendingPrepCutSession())
			{
				_clientMeshReplicationFailed = true;
				_clientObserverCutQueue.Clear();
				UtilityMethods.Log("[PrepStation Mesh Cutting] Observer session is missing. " + $"SessionVersion={_clientCutSessionVersion}", LogType.Error);
				ClientTryFinalizePendingPrepCutCompletion();
				return;
			}
			ReplicatedPrepCut cut = _clientObserverCutQueue.Dequeue();
			if (!_ingredientMeshCutController.RequestCut(cut.Sequence, cut.CutPoint, cut.PlaneNormal, (bool success) =>
			{
				if (success)
				{
					_clientAppliedCutSequence = cut.Sequence;
					ClientSpawnProcessedVisualsForCut(cut.Sequence, cut.CutPoint, cut.CutDirection);
					_ingredientMeshCutController.HidePresentationIfFinalCut(cut.Sequence);
					ClientTryProcessNextObserverCut();
				}
				else
				{
					_clientMeshReplicationFailed = true;
					_clientObserverCutQueue.Clear();
					UtilityMethods.Log($"[PrepStation Mesh Cutting] Observer cut failed. Sequence={cut.Sequence}, " + $"SessionVersion={cut.SessionVersion}", LogType.Error);
					ClientTryFinalizePendingPrepCutCompletion();
				}
			}))
			{
				_clientMeshReplicationFailed = true;
				_clientObserverCutQueue.Clear();
				UtilityMethods.Log($"[PrepStation Mesh Cutting] Observer cut was rejected locally. Sequence={cut.Sequence}, " + $"SessionVersion={cut.SessionVersion}", LogType.Error);
				ClientTryFinalizePendingPrepCutCompletion();
			}
		}

		private bool ServerIsActivePrepStationUser(ulong clientId)
		{
			if (_serverActiveInteractor != null && _serverActiveInteractor.OwnerClientId == clientId)
			{
				return base.NetworkObject.OwnerClientId == clientId;
			}
			return false;
		}

		private void ServerClearPrepCutSession()
		{
			if (base.IsServer)
			{
				_serverCutSessionActive = false;
				_serverCutSequence = 0;
				_serverCutRequiredCount = 0;
				_serverCutOutputYield = 0;
				_serverCutStartIndex = 0;
				_serverCutProcessedId = default;
			}
		}

		private void ClientCompletePrepCutSession()
		{
			if (_ingredientMeshCutController != null)
			{
				_ingredientMeshCutController.CompleteSession();
			}
			ClearClientPrepCutSessionContext();
		}

		private void ClientCancelPrepCutSession(bool restoreSourcePresentation)
		{
			if (_ingredientMeshCutController != null)
			{
				if (restoreSourcePresentation)
				{
					_ingredientMeshCutController.CancelSession();
				}
				else
				{
					_ingredientMeshCutController.CompleteSession();
				}
			}
			ClearClientPrepCutSessionContext();
		}

		private void ClearClientPrepCutSessionContext()
		{
			_clientCutSessionPending = false;
			_clientCutSessionVersion = 0;
			_clientCutIngredientId = default;
			_clientCutRequiredCount = 0;
			_clientCutOutputYield = 0;
			_clientCutStartIndex = 0;
			_clientCutProcessedId = default;
			_clientObserverCutQueue.Clear();
			_clientReceivedCutSequence = 0;
			_clientAppliedCutSequence = 0;
			_clientMeshReplicationFailed = false;
			ClientClearPendingPrepCutCompletion();
		}

		private static bool IsFiniteVector(Vector3 value)
		{
			if (IsFinite(value.x) && IsFinite(value.y))
			{
				return IsFinite(value.z);
			}
			return false;
		}

		private static bool IsFinite(float value)
		{
			if (!float.IsNaN(value))
			{
				return !float.IsInfinity(value);
			}
			return false;
		}

		private void ServerCachePendingProcessedOutput(FixedString64Bytes processedId, int outputYield, int startIndex)
		{
			if (base.IsServer)
			{
				_pendingProcessedId = processedId;
				_pendingProcessedYield = outputYield;
				_pendingProcessedStartIndex = startIndex;
				_pendingProcessedCreatedAt = Time.time;
				_hasPendingProcessedOutput = true;
				_pendingProcessedIdNetwork.Value = processedId;
				_pendingProcessedYieldNetwork.Value = outputYield;
				_pendingProcessedStartIndexNetwork.Value = startIndex;
				_hasPendingProcessedOutputNetwork.Value = true;
				LogRecoveryContext($"Cached pending processed output. Id={processedId}, Yield={outputYield}, StartIndex={startIndex}");
			}
		}

		public bool ServerTryRepairTransferVisualBeforeGastroPan()
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (CurrentState != PrepStationState.TransferToGastroPan)
			{
				return false;
			}
			if (!_hasPendingProcessedOutput)
			{
				LogRecoveryContext("Transfer visual repair failed. No pending processed output.");
				return false;
			}
			if (string.IsNullOrWhiteSpace(_pendingProcessedId.ToString()))
			{
				LogRecoveryContext("Transfer visual repair failed. Pending processed id is empty.");
				return false;
			}
			if (_pendingProcessedYield <= 0)
			{
				LogRecoveryContext("Transfer visual repair failed. Pending processed yield is invalid.");
				return false;
			}
			ClientEnsurePendingProcessedVisualRpc(_pendingProcessedId, _pendingProcessedYield, _pendingProcessedStartIndex);
			LogRecoveryContext($"Transfer visual ensure requested. Id={_pendingProcessedId}, Yield={_pendingProcessedYield}, StartIndex={_pendingProcessedStartIndex}");
			return true;
		}

		public void ServerClearPendingProcessedOutputContext(string source)
		{
			if (base.IsServer)
			{
				ServerClearPendingProcessedOutput();
				LogRecoveryContext("Pending processed output cleared. Source=" + source);
			}
		}

		public void ServerRecoverBrokenTransferToIdle(string reason)
		{
			if (base.IsServer)
			{
				LogRecoveryContext("Broken transfer recovered to Idle. Reason=" + reason);
				ServerResetStationInternal();
			}
		}

		private void ServerClearPendingProcessedOutput()
		{
			if (base.IsServer)
			{
				_hasPendingProcessedOutput = false;
				_pendingProcessedId = default;
				_pendingProcessedYield = 0;
				_pendingProcessedStartIndex = 0;
				_pendingProcessedCreatedAt = 0f;
				_hasPendingProcessedOutputNetwork.Value = false;
				_pendingProcessedIdNetwork.Value = default;
				_pendingProcessedYieldNetwork.Value = 0;
				_pendingProcessedStartIndexNetwork.Value = 0;
				LogRecoveryContext("Cleared pending processed output.");
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientEnsurePendingProcessedVisualRpc(FixedString64Bytes processedId, int outputYield, int startIndex)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1005326477u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in processedId, default(FastBufferWriter.ForFixedStrings));
				BytePacker.WriteValueBitPacked(bufferWriter, outputYield);
				BytePacker.WriteValueBitPacked(bufferWriter, startIndex);
				__endSendRpc(ref bufferWriter, 1005326477u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!ClientHasLiveProcessedIngredientVisuals())
			{
				ClientSpawnProcessedIngredientVisualLocal(processedId, outputYield, startIndex);
				if (IsLocalActiveUser)
				{
					ClientUpdateInstructionText(PrepStationInfoTextId.ClickGastroPanPile);
				}
			}
		}

		private bool ClientHasLiveProcessedIngredientVisuals()
		{
			int num = _spawnedObjects.Count - 1;
			while (num >= 0)
			{
				if (_spawnedObjects[num] == null)
				{
					_spawnedObjects.RemoveAt(num);
					num--;
					continue;
				}
				return true;
			}
			return false;
		}

		private void ClientRestorePendingProcessedOutputIfNeeded()
		{
			if (CurrentState != PrepStationState.TransferToGastroPan || !_hasPendingProcessedOutputNetwork.Value)
			{
				return;
			}
			FixedString64Bytes value = _pendingProcessedIdNetwork.Value;
			int value2 = _pendingProcessedYieldNetwork.Value;
			if (!string.IsNullOrWhiteSpace(value.ToString()) && value2 > 0)
			{
				if (!ClientHasLiveProcessedIngredientVisuals())
				{
					ClientSpawnProcessedIngredientVisualLocal(value, value2, _pendingProcessedStartIndexNetwork.Value);
				}
				if (IsLocalActiveUser)
				{
					ClientUpdateInstructionText(PrepStationInfoTextId.ClickGastroPanPile);
				}
			}
		}

		private void ClientSpawnProcessedIngredientVisualLocal(FixedString64Bytes processedId, int outputYield, int startIndex)
		{
			if (_spawnPointList == null || _spawnPointList.Count <= 0)
			{
				return;
			}
			string text = processedId.ToString();
			if (!(ItemDatabaseSO.FindById(text) is ProcessedIngredientSO processedIngredientSO))
			{
				UtilityMethods.Log("<b><color=red>[PrepStation Recovery]</color></b> Failed to rebuild processed visual. Invalid ProcessedIngredientSO: " + text, LogType.Warning);
			}
			else
			{
				if (processedIngredientSO.GraphicObjectPrefab == null)
				{
					return;
				}
				int count = _spawnPointList.Count;
				startIndex = (startIndex % count + count) % count;
				ClientDespawnIngredientVisuals();
				for (int i = 0; i < outputYield; i++)
				{
					int index = (startIndex + i) % count;
					Transform transform = _spawnPointList[index];
					if (!(transform == null))
					{
						GameObject gameObject = UnityEngine.Object.Instantiate(processedIngredientSO.GraphicObjectPrefab, transform.position, Quaternion.identity);
						gameObject.transform.SetParent(transform, worldPositionStays: true);
						_spawnedObjects.Add(gameObject);
						AnimateProcessedSpawn(gameObject.transform, transform);
					}
				}
			}
		}

		private void LogRecoveryContext(string message)
		{
			if (_logRecoveryContext)
			{
				UtilityMethods.Log("<b><color=#E6A23C>[PrepStation Recovery Context]</color></b> " + base.name + "\n" + message);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientPrepareRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(244959734u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 244959734u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_isFallbackOccupied = true;
			}
		}

		public override void ServerUseEnter(Interactor interactor)
		{
			if (StaffEmployeeId == 0 && !(_physicalStateController == null) && _physicalStateController.IsPhysicallyUsable)
			{
				base.ServerUseEnter(interactor);
				_serverActiveInteractor = interactor;
				QuestReferenceHub.Instance?.CompleteIfActive(QuestId.UsePrepStation);
				QuestReferenceHub.Instance?.CompleteIfActive(QuestId.UsePrepStation_01);
				if (base.NetworkObject.OwnerClientId != interactor.OwnerClientId)
				{
					base.NetworkObject.ChangeOwnership(interactor.OwnerClientId);
				}
				if (_gastroPanSpawner != null && _gastroPanSpawner.IsFallbackOccupied)
				{
					_isFallbackOccupied = true;
					_currentState.Value = 3;
					ClientPrepareRpc();
				}
				CanGaze.Value = false;
			}
		}

		public override void ServerUseExit(Interactor interactor)
		{
			if (CurrentState != PrepStationState.TransferToGastroPan && CurrentState != PrepStationState.GastroPanMovingToTarget)
			{
				ServerResetStationInternal();
			}
			base.ServerUseExit(interactor);
			base.NetworkObject.RemoveOwnership();
			CanGaze.Value = true;
			_serverActiveInteractor = null;
		}

		public override void ClientUseEnter(Interactor interactor)
		{
			if (!_physicalStateController.IsPhysicallyUsable)
			{
				return;
			}
			base.ClientUseEnter(interactor);
			_canvasGO.SetActive(value: true);
			interactor.Player.ForKitchenUsable(value: false);
			Singleton<GameUIManager>.Instance.TimeUI.SetVisibility(status: false);
			Singleton<InputManager>.Instance.InputHandler.KnifeAction += OnKnifeAction;
			Singleton<InputManager>.Instance.InputHandler.Exit += OnPrepStationExit;
			Singleton<InputManager>.Instance.InputHandler.PrepStationSlot += OnSlotNumber;
			Singleton<InputManager>.Instance.InputHandler.PrepStationScroll += OnScroll;
			_knifeModel.gameObject.SetActive(value: false);
			EventBus<DeskCam>.Raise(new DeskCam
			{
				CinemachineBrain = interactor.Player.CinemachineBrain
			});
			interactor.Player.SwitchTo("PrepStation", cursorVisible: true);
			ClientUpdateInstructionText(PrepStationInfoTextId.PlaceItemOnTable);
			if (interactor.Player.CharacterVisuals != null && interactor.Player.CharacterVisuals.FpsRootObject != null)
			{
				UtilityMethods.SetLayerRecursive(interactor.Player.CharacterVisuals.FpsRootObject, "Player", includeChildren: true);
			}
			if (_isFallbackOccupied)
			{
				ServerSetStateRpc(3);
				_gastropanCenterInteractorImage.enabled = true;
				ClientUpdateInstructionText(PrepStationInfoTextId.ClickGastroPan);
			}
			if (CurrentState == PrepStationState.TransferToGastroPan || CurrentState == PrepStationState.GastroPanMovingToTarget)
			{
				if (Singleton<QuickSlotHandler>.Instance.SelectedItem != null)
				{
					Singleton<QuickSlotHandler>.Instance.SelectedItem.SetVisibility(visible: false);
				}
				if (CurrentState == PrepStationState.TransferToGastroPan)
				{
					ClientUpdateInstructionText(PrepStationInfoTextId.ClickGastroPanPile);
					_gastroPanSpawner?.ClientPlayClickableStackFeedback();
				}
				if (CurrentState == PrepStationState.GastroPanMovingToTarget)
				{
					_gastropanCenterInteractorImage.enabled = true;
					ClientUpdateInstructionText(PrepStationInfoTextId.ClickGastroPan);
					_gastroPanSpawner?.ClientPlayCenterAttentionFeedback();
				}
			}
			else
			{
				ClientDespawnIngredientVisuals();
				if (Singleton<QuickSlotHandler>.Instance.SelectedItem != null && Singleton<QuickSlotHandler>.Instance.SelectedItem.ItemType == ItemType.RawIngredient && Singleton<QuickSlotHandler>.Instance.SelectedItem.ItemSO is IngredientSO { PlacablePrepStation: not false })
				{
					CurrentItem = Singleton<QuickSlotHandler>.Instance.SelectedItem;
					ClientSetupCurrentItemPrefab();
				}
				else
				{
					if (Singleton<QuickSlotHandler>.Instance.SelectedItem != null && Singleton<QuickSlotHandler>.Instance.SelectedItem.TryGetComponent<DeadFish>(out var _))
					{
						Singleton<NotifyManager>.Instance.ShowToast("Use the fillet machine first");
					}
					ClientUpdateInstructionText(PrepStationInfoTextId.PlaceItemOnTable);
				}
			}
			RefreshCursorForPrepStationUseMode();
			TryOpenTutorialForFirstUse(interactor);
		}

		public override void ClientUseExit(Interactor interactor)
		{
			base.ClientUseExit(interactor);
			_gastroPanSpawner?.ClientStopAttentionFeedback();
			ClearLocalCuttingRuntime();
			if (Singleton<GameUIManager>.Instance != null && Singleton<GameUIManager>.Instance.TimeUI != null)
			{
				Singleton<GameUIManager>.Instance.TimeUI.SetVisibility(status: true);
			}
			if (_canvasGO != null)
			{
				_canvasGO.SetActive(value: false);
			}
			Singleton<InputManager>.Instance.InputHandler.KnifeAction -= OnKnifeAction;
			Singleton<InputManager>.Instance.InputHandler.Exit -= OnPrepStationExit;
			Singleton<InputManager>.Instance.InputHandler.PrepStationSlot -= OnSlotNumber;
			Singleton<InputManager>.Instance.InputHandler.PrepStationScroll -= OnScroll;
			if (interactor != null && interactor.Player != null)
			{
				if (interactor.Player.CharacterVisuals != null && interactor.Player.CharacterVisuals.FpsRootObject != null)
				{
					UtilityMethods.SetLayerRecursive(interactor.Player.CharacterVisuals.FpsRootObject, "ViewModel", includeChildren: true);
				}
				interactor.Player.ForKitchenUsable(value: true);
				interactor.Player.SwitchTo("Player", cursorVisible: false);
			}
			if (Singleton<QuickSlotHandler>.Instance != null)
			{
				Singleton<QuickSlotHandler>.Instance.gameObject.SetActive(value: true);
			}
			EventBus<DeskCam>.Raise(new DeskCam
			{
				CinemachineBrain = null
			});
			RestoreHiddenHoldableForStation();
			if (_cuttingViewManager != null && _cuttingViewManager.CutPointUI != null)
			{
				_cuttingViewManager.CutPointUI.gameObject.SetActive(value: false);
			}
			ClientUpdateInstructionText(PrepStationInfoTextId.None);
			foreach (GastropanRecallInteractor recallInteractor in _recallInteractors)
			{
				if (recallInteractor != null && recallInteractor.ToolTipGO != null)
				{
					recallInteractor.ToolTipGO.SetActive(value: false);
				}
			}
			Singleton<QuickSlotHandler>.Instance.ForceHideAllInventoryItemVisualsOnly();
		}

		protected override void ClientInteract(Interactor interactor, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				if (!_physicalStateController.IsPhysicallyUsable)
				{
					TryRepairPhysicalState(interactor);
				}
				else
				{
					base.ClientInteract(interactor, phase);
				}
			}
		}

		protected override void ClientCrossInteract(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				if (!_physicalStateController.IsPhysicallyUsable)
				{
					TryRepairPhysicalState(interactor);
				}
				else
				{
					base.ClientCrossInteract(interactor, held, phase);
				}
			}
		}

		private bool TryRepairPhysicalState(Interactor interactor)
		{
			if (_physicalStateController == null)
			{
				return false;
			}
			if (_physicalStateController.IsPhysicallyUsable)
			{
				return true;
			}
			if (_physicalUnlockController != null)
			{
				return _physicalUnlockController.TryUnlock(interactor);
			}
			if (!Singleton<GameProgressManager>.Instance.IsProgressionUnlocked(_repairProgressionUnlockKey))
			{
				Singleton<NotifyManager>.Instance?.ShowToast("Reach the required restaurant level first.", NotificationState.Info, 2f);
				return false;
			}
			QuestReferenceHub.Instance.AddProgress(QuestId.RepairThePrepStation);
			_physicalStateController.Repair();
			return true;
		}

		private void TryOpenTutorialForFirstUse(Interactor interactor)
		{
			if (PlayerPrefs.GetInt("StationTutorial.PrepStation.Seen", 0) != 1 && !(Singleton<GameUIManager>.Instance == null) && !(Singleton<GameUIManager>.Instance.StationTutorialPopupUI == null))
			{
				PlayerPrefs.SetInt("StationTutorial.PrepStation.Seen", 1);
				PlayerPrefs.Save();
				Singleton<GameUIManager>.Instance.StationTutorialPopupUI.OpenAfterRepair(_tutorialData, interactor.Player);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerCompletePrepCutSessionRpc(int sessionVersion, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1971569076u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sessionVersion);
				__endSendRpc(ref bufferWriter, 1971569076u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (StaffEmployeeId != 0)
			{
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!ServerIsActivePrepStationUser(senderClientId) || CurrentState != PrepStationState.Cutting || !_serverCutSessionActive || sessionVersion != _serverCutSessionVersion || _serverCutSequence != _serverCutRequiredCount)
			{
				UtilityMethods.Log($"[PrepStation Mesh Cutting] Completion rejected. SenderClientId={senderClientId}, " + $"SessionVersion={sessionVersion}, ActiveSessionVersion={_serverCutSessionVersion}, " + $"Sequence={_serverCutSequence}, Required={_serverCutRequiredCount}, State={CurrentState}", LogType.Warning);
				return;
			}
			string id = _currentIngredientID.Value.ToString();
			if (!(ItemDatabaseSO.FindById(id) is IngredientSO ingredientSO))
			{
				return;
			}
			if (!(ItemDatabaseSO.FindById(_serverCutProcessedId.ToString()) is ProcessedIngredientSO processedIngredientSO) || !ingredientSO.ProcessedIngredients.Contains(processedIngredientSO) || processedIngredientSO.OutputYield != _serverCutOutputYield)
			{
				UtilityMethods.Log("[PrepStation Mesh Cutting] Completion data is invalid. Ingredient=" + ingredientSO.name + ", " + $"ProcessedId={_serverCutProcessedId}", LogType.Error);
				return;
			}
			int serverCutOutputYield = _serverCutOutputYield;
			int serverCutStartIndex = _serverCutStartIndex;
			ServerCachePendingProcessedOutput(processedIngredientSO.Id, serverCutOutputYield, serverCutStartIndex);
			RestaurantManager restaurantManager = Singleton<GameProgressManager>.Instance.RestaurantManager;
			restaurantManager.ServerAddStockRpc(processedIngredientSO.Id, (byte)serverCutOutputYield);
			restaurantManager.ServerRemoveStockRpc(ingredientSO.Id, 1);
			Singleton<GameProgressManager>.Instance.ServerGrantProgressionXp(ProgressionXpSource.PrepCutCompleted, processedIngredientSO.Id, senderClientId);
			if (processedIngredientSO.Id == _itemDatabaseSO.CucumberLongStripSO.Id)
			{
				QuestReferenceHub.Instance.QuestComplete(QuestId.SliceCucumberAtPrepStation);
			}
			if (processedIngredientSO.Id == _itemDatabaseSO.MackeralNigiriSO.Id)
			{
				QuestReferenceHub.Instance.QuestComplete(QuestId.SliceMackerelFilletIntoNigiri);
			}
			_currentState.Value = 2;
			_serverCutSessionActive = false;
			ClientCompletePrepCutSessionRpc(processedIngredientSO.Id, serverCutOutputYield, serverCutStartIndex);
			_gastroPanSpawner?.ServerHandleCompletedProcessedOutput(processedIngredientSO.Id, serverCutOutputYield);
			ServerClearPrepCutSession();
		}

		[Rpc(SendTo.Everyone)]
		private void ClientCompletePrepCutSessionRpc(FixedString64Bytes processedId, int outputYield, int startIndex)
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
				FastBufferWriter bufferWriter = __beginSendRpc(51130561u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in processedId, default(FastBufferWriter.ForFixedStrings));
				BytePacker.WriteValueBitPacked(bufferWriter, outputYield);
				BytePacker.WriteValueBitPacked(bufferWriter, startIndex);
				__endSendRpc(ref bufferWriter, 51130561u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_clientPrepCutCompletionPending = true;
				_clientCompletionProcessedId = processedId;
				_clientCompletionOutputYield = outputYield;
				_clientCompletionStartIndex = startIndex;
				ClientTryFinalizePendingPrepCutCompletion();
			}
		}

		private void ClientTryFinalizePendingPrepCutCompletion()
		{
			if (!_clientPrepCutCompletionPending)
			{
				return;
			}
			bool flag = _clientMeshReplicationFailed || (_clientAppliedCutSequence >= _clientCutRequiredCount && _clientObserverCutQueue.Count == 0 && (_ingredientMeshCutController == null || !_ingredientMeshCutController.IsCutInProgress));
			if (IsLocalActiveUser || flag)
			{
				FixedString64Bytes clientCompletionProcessedId = _clientCompletionProcessedId;
				int clientCompletionOutputYield = _clientCompletionOutputYield;
				int clientCompletionStartIndex = _clientCompletionStartIndex;
				_clientPrepCutCompletionPending = false;
				ClientCompletePrepCutSession();
				ClientEnsureProcessedIngredientVisualCount(clientCompletionProcessedId, clientCompletionOutputYield, clientCompletionStartIndex);
				ClientTryRunPendingGastroPanLift();
				ClientTryRunPendingGastroPanTransfer();
				if (CurrentItemGraphicObjectPrefab != null)
				{
					CurrentItemGraphicObjectPrefab.transform.DOKill(complete: true);
					UnityEngine.Object.Destroy(CurrentItemGraphicObjectPrefab);
					CurrentItemGraphicObjectPrefab = null;
				}
				if (IsLocalActiveUser)
				{
					ClientConsumeIngredientAfterCutCompletion();
					ClientUpdateInstructionText(PrepStationInfoTextId.ClickGastroPanPile);
					_gastroPanSpawner?.ClientPlayTransferTargetFeedback(clientCompletionProcessedId);
				}
			}
		}

		private void ClientClearPendingPrepCutCompletion()
		{
			_clientPrepCutCompletionPending = false;
			_clientCompletionProcessedId = default;
			_clientCompletionOutputYield = 0;
			_clientCompletionStartIndex = 0;
		}

		[Rpc(SendTo.Everyone)]
		private void ClientSpawnIngredientVisualRpc(FixedString64Bytes processedID, int outputYield, int startIndex)
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
				FastBufferWriter bufferWriter = __beginSendRpc(4174525010u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in processedID, default(FastBufferWriter.ForFixedStrings));
				BytePacker.WriteValueBitPacked(bufferWriter, outputYield);
				BytePacker.WriteValueBitPacked(bufferWriter, startIndex);
				__endSendRpc(ref bufferWriter, 4174525010u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			string text = processedID.ToString();
			ItemSO itemSO = ItemDatabaseSO.FindById(text);
			if (itemSO == null)
			{
				UtilityMethods.Log("<color=red>ItemSO not found: " + text + "</color>");
				return;
			}
			ProcessedIngredientSO processedIngredientSO = itemSO as ProcessedIngredientSO;
			if (processedIngredientSO == null)
			{
				UtilityMethods.Log("<color=red>Failed to cast to ProcessedIngredientSO.</color>");
				return;
			}
			int count = _spawnPointList.Count;
			startIndex = (startIndex % count + count) % count;
			ClientDespawnIngredientVisuals();
			for (int i = 0; i < outputYield; i++)
			{
				int index = (startIndex + i) % count;
				Transform transform = _spawnPointList[index];
				if (processedIngredientSO.GraphicObjectPrefab == null)
				{
					break;
				}
				GameObject gameObject = UnityEngine.Object.Instantiate(processedIngredientSO.GraphicObjectPrefab, transform.position, Quaternion.identity);
				gameObject.transform.SetParent(transform, worldPositionStays: true);
				_spawnedObjects.Add(gameObject);
				AnimateProcessedSpawn(gameObject.transform, transform);
			}
		}

		private void ClientSpawnProcessedVisualsForCut(int sequence, Vector3 cutPoint, Vector3 cutDirection)
		{
			if (sequence <= 0 || sequence > _clientCutRequiredCount || _clientCutRequiredCount <= 0 || _clientCutOutputYield <= 0)
			{
				return;
			}
			PlayCutParticle(cutPoint, cutDirection);
			if (!(ItemDatabaseSO.FindById(_clientCutProcessedId.ToString()) is ProcessedIngredientSO processedIngredientSO) || processedIngredientSO.GraphicObjectPrefab == null)
			{
				UtilityMethods.Log($"[PrepStation Processed Spawn] Processed visual is missing. ProcessedId={_clientCutProcessedId}", LogType.Error);
				return;
			}
			PruneProcessedIngredientVisuals();
			int value = Mathf.FloorToInt((float)(sequence * _clientCutOutputYield) / (float)_clientCutRequiredCount);
			value = Mathf.Clamp(value, 0, _clientCutOutputYield);
			for (int i = _spawnedObjects.Count; i < value; i++)
			{
				ClientSpawnProcessedVisualFromCut(processedIngredientSO, i, cutPoint, cutDirection);
			}
		}

		private void ClientSpawnProcessedVisualFromCut(ProcessedIngredientSO processedSO, int outputIndex, Vector3 cutPoint, Vector3 cutDirection)
		{
			if (_spawnPointList == null || _spawnPointList.Count == 0)
			{
				return;
			}
			int index = (_clientCutStartIndex + outputIndex) % _spawnPointList.Count;
			Transform transform = _spawnPointList[index];
			if (!(transform == null))
			{
				Vector3 vector = Vector3.ProjectOnPlane(cutDirection, base.transform.up);
				if (vector.sqrMagnitude <= 0.0001f)
				{
					vector = base.transform.right;
				}
				vector.Normalize();
				Vector3 vector2 = cutPoint + base.transform.up * 0.025f + vector * 0.012f;
				GameObject gameObject = UnityEngine.Object.Instantiate(processedSO.GraphicObjectPrefab, vector2, Quaternion.identity);
				if (!(gameObject == null))
				{
					_spawnedObjects.Add(gameObject);
					AnimateProcessedSpawnFromCut(gameObject.transform, transform, vector2);
				}
			}
		}

		private void AnimateProcessedSpawnFromCut(Transform spawned, Transform parentPoint, Vector3 spawnPosition)
		{
			if (spawned == null || parentPoint == null)
			{
				return;
			}
			float x = UnityEngine.Random.Range(0f - _spawnRandomOffsetXZ.x, _spawnRandomOffsetXZ.x);
			float z = UnityEngine.Random.Range(0f - _spawnRandomOffsetXZ.y, _spawnRandomOffsetXZ.y);
			Vector3 targetLocalPosition = new Vector3(x, 0f, z);
			Vector3 endValue = parentPoint.TransformPoint(targetLocalPosition);
			spawned.SetParent(parentPoint, worldPositionStays: true);
			spawned.position = spawnPosition;
			spawned.localRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f - _spawnRandomYaw, _spawnRandomYaw), UnityEngine.Random.Range(0f - _spawnRandomRoll, _spawnRandomRoll));
			spawned.localScale = Vector3.zero;
			float duration = _spawnPopDuration * UnityEngine.Random.Range(0.9f, 1.15f);
			float jumpPower = _spawnPopHeight * UnityEngine.Random.Range(0.8f, 1.2f);
			float num = _spawnScaleOvershoot * UnityEngine.Random.Range(0.95f, 1.08f);
			spawned.DOJump(endValue, jumpPower, 1, duration).SetEase(Ease.OutQuad).OnComplete(() =>
			{
				if (!(spawned == null))
				{
					spawned.localPosition = targetLocalPosition;
				}
			});
			float scaleUpDuration = duration * 0.65f;
			spawned.DOScale(Vector3.one * num, scaleUpDuration).SetEase(Ease.OutBack).OnComplete(() =>
			{
				if (spawned != null)
				{
					spawned.DOScale(Vector3.one, duration - scaleUpDuration).SetEase(Ease.InSine);
				}
			});
		}

		private void ClientEnsureProcessedIngredientVisualCount(FixedString64Bytes processedId, int outputYield, int startIndex)
		{
			if (_spawnPointList == null || _spawnPointList.Count == 0 || !(ItemDatabaseSO.FindById(processedId.ToString()) is ProcessedIngredientSO processedIngredientSO) || processedIngredientSO.GraphicObjectPrefab == null)
			{
				return;
			}
			PruneProcessedIngredientVisuals();
			for (int i = _spawnedObjects.Count; i < outputYield; i++)
			{
				int index = (startIndex + i) % _spawnPointList.Count;
				Transform transform = _spawnPointList[index];
				if (!(transform == null))
				{
					GameObject gameObject = UnityEngine.Object.Instantiate(processedIngredientSO.GraphicObjectPrefab, transform.position, Quaternion.identity);
					if (!(gameObject == null))
					{
						gameObject.transform.SetParent(transform, worldPositionStays: true);
						_spawnedObjects.Add(gameObject);
						AnimateProcessedSpawn(gameObject.transform, transform);
					}
				}
			}
		}

		private void PruneProcessedIngredientVisuals()
		{
			for (int num = _spawnedObjects.Count - 1; num >= 0; num--)
			{
				if (_spawnedObjects[num] == null)
				{
					_spawnedObjects.RemoveAt(num);
				}
			}
		}

		private void AnimateProcessedSpawn(Transform spawned, Transform parentPoint)
		{
			if (spawned == null)
			{
				return;
			}
			spawned.SetParent(parentPoint, worldPositionStays: false);
			float num = _spawnPopHeight * UnityEngine.Random.Range(0.8f, 1.5f);
			float x = UnityEngine.Random.Range(0f - _spawnRandomOffsetXZ.x, _spawnRandomOffsetXZ.x) * UnityEngine.Random.Range(0.7f, 1.3f);
			float z = UnityEngine.Random.Range(0f - _spawnRandomOffsetXZ.y, _spawnRandomOffsetXZ.y) * UnityEngine.Random.Range(0.7f, 1.3f);
			float num2 = _spawnScaleOvershoot * UnityEngine.Random.Range(0.85f, 1.2f);
			float num3 = _spawnPopDuration * UnityEngine.Random.Range(0.8f, 1.25f);
			float y = UnityEngine.Random.Range(0f - _spawnRandomYaw, _spawnRandomYaw) * UnityEngine.Random.Range(0.8f, 1.4f);
			float z2 = UnityEngine.Random.Range(0f - _spawnRandomRoll, _spawnRandomRoll) * UnityEngine.Random.Range(0.8f, 1.4f);
			Vector3 vector = new Vector3(x, 0f, z);
			Vector3 localPosition = vector + Vector3.up * num;
			spawned.localPosition = localPosition;
			spawned.localRotation = Quaternion.Euler(0f, y, z2);
			spawned.localScale = Vector3.zero;
			PlayPopVFX();
			PlayPopSFX();
			spawned.DOLocalMove(vector, num3).SetEase(Ease.OutQuad);
			float num4 = num3 * UnityEngine.Random.Range(0.6f, 0.75f);
			float scaleDownDuration = num3 - num4;
			spawned.DOScale(Vector3.one * num2, num4).SetEase(Ease.OutBack).OnComplete(() =>
			{
				spawned.DOScale(Vector3.one, scaleDownDuration).SetEase(Ease.InSine).OnComplete(() =>
				{
					PlayLandSFX();
					PlayLandVFX();
				});
			});
			float num5 = UnityEngine.Random.Range(0.0015f, 0.005f);
			float duration = UnityEngine.Random.Range(0.08f, 0.13f);
			spawned.DOPunchPosition(Vector3.down * num5, duration, 4, 0.6f);
		}

		[Rpc(SendTo.Everyone)]
		public void ClientLiftIngredientsForGastroPanRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1245674840u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1245674840u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientLiftIngredientsForGastroPanLocal();
			}
		}

		public void ClientLiftIngredientsForGastroPanLocal()
		{
			if (ClientShouldDeferGastroPanTransfer())
			{
				_pendingGastroPanLift = true;
			}
			else
			{
				if (_spawnedObjects.Count == 0)
				{
					return;
				}
				_pendingGastroPanLift = false;
				_clientGastroPanLiftReadyAt = Time.time + _gastroLiftDuration;
				foreach (GameObject spawnedObject in _spawnedObjects)
				{
					if (!(spawnedObject == null))
					{
						Transform transform = spawnedObject.transform;
						transform.DOKill(complete: true);
						Vector3 position = transform.position;
						float y = position.y + _gastroLiftHeight;
						Vector3 vector = Vector3.Lerp(b: (_gastroLiftGatherCenter != null) ? _gastroLiftGatherCenter.position : _gastropanCenterPosition.position, a: position, t: 0.25f);
						Vector3 endValue = new Vector3(vector.x, y, vector.z);
						Quaternion endValue2 = Quaternion.Euler(transform.rotation.eulerAngles.x, 90f, transform.rotation.eulerAngles.z);
						transform.DOMove(endValue, _gastroLiftDuration).SetEase(Ease.OutQuad);
						transform.DORotateQuaternion(endValue2, _gastroLiftDuration * 0.8f).SetEase(Ease.OutQuad);
					}
				}
			}
		}

		public void ClientDropIngredientsIntoGastroPan()
		{
			if (_spawnedObjects.Where((GameObject x) => x != null).ToList().Count == 0)
			{
				_spawnedObjects.Clear();
				if (IsLocalActiveUser)
				{
					ShowGastropanCenterInteractor();
				}
				return;
			}
			int activeTweenCount = 0;
			GameObject[] array = _spawnedObjects.ToArray();
			foreach (GameObject go in array)
			{
				if (go == null)
				{
					continue;
				}
				Transform transform = go.transform;
				transform.DOKill(complete: true);
				Vector3 position = transform.position;
				Vector3 position2 = _gastropanCenterPosition.position;
				position2.x += UnityEngine.Random.Range(-0.02f, 0.02f);
				position2.z += UnityEngine.Random.Range(-0.02f, 0.02f);
				position2.y += UnityEngine.Random.Range(-0.005f, 0.005f);
				transform.position = new Vector3(position2.x, position.y, position2.z);
				float delay = UnityEngine.Random.Range(0f, _gastroDropStaggerMax);
				float num2 = _gastroDropDuration * UnityEngine.Random.Range(0.9f, 1.1f);
				float duration = num2 * 0.7f;
				float duration2 = num2 * 0.15f;
				float duration3 = num2 * 0.15f;
				float num3 = UnityEngine.Random.Range(0.008f, 0.018f);
				Sequence sequence = DOTween.Sequence();
				int num4 = activeTweenCount;
				activeTweenCount = num4 + 1;
				sequence.SetDelay(delay);
				sequence.Append(transform.DOMoveY(position2.y, duration).SetEase(Ease.InCubic));
				sequence.AppendCallback(() =>
				{
					PlayLocalPrepStationSfx(_ingredientsFallIntoGastroPanSfx, 1f, useRandomPitch: true);
				});
				sequence.Append(transform.DOMoveY(position2.y + num3, duration2).SetEase(Ease.OutQuad));
				sequence.Append(transform.DOMoveY(position2.y, duration3).SetEase(Ease.InQuad));
				sequence.OnComplete(() =>
				{
					_spawnedObjects.Remove(go);
					UnityEngine.Object.Destroy(go);
					int num5 = activeTweenCount;
					activeTweenCount = num5 - 1;
					if (activeTweenCount == 0)
					{
						if (base.IsOwner)
						{
							ShowGastropanCenterInteractor();
						}
						ClientDespawnIngredientVisuals();
					}
				});
			}
		}

		private void ClientDespawnIngredientVisuals()
		{
			foreach (GameObject spawnedObject in _spawnedObjects)
			{
				if (!(spawnedObject == null))
				{
					spawnedObject.transform.DOKill(complete: true);
					UnityEngine.Object.Destroy(spawnedObject);
				}
			}
			_spawnedObjects.Clear();
		}

		public bool ServerTryGetPendingProcessedOutput(out FixedString64Bytes processedId, out int outputYield)
		{
			processedId = _pendingProcessedId;
			outputYield = _pendingProcessedYield;
			if (base.IsServer && _hasPendingProcessedOutput && !string.IsNullOrWhiteSpace(processedId.ToString()))
			{
				return outputYield > 0;
			}
			return false;
		}

		public void ClientTransferIngredientsDirectlyToGastroPanLocal(Transform gastroPanTarget, float duration, bool showCenterInteractorWhenComplete = false, float stagger = 0.04f)
		{
			if (gastroPanTarget == null)
			{
				return;
			}
			float num = _clientGastroPanLiftReadyAt - Time.time;
			if (ClientShouldDeferGastroPanTransfer() || num > 0f)
			{
				_pendingGastroPanTransferTarget = gastroPanTarget;
				_pendingGastroPanTransferDuration = duration;
				_pendingGastroPanTransferStagger = stagger;
				_pendingGastroPanTransferShowsCenterInteractor = showCenterInteractorWhenComplete;
				if (num > 0f)
				{
					DOVirtual.DelayedCall(num, ClientTryRunPendingGastroPanTransfer);
				}
				return;
			}
			PruneProcessedIngredientVisuals();
			if (_spawnedObjects.Count == 0)
			{
				if (showCenterInteractorWhenComplete)
				{
					ShowGastropanCenterInteractor();
				}
				return;
			}
			duration = Mathf.Max(0.08f, duration);
			GameObject[] array = _spawnedObjects.ToArray();
			int activeTransferCount = 0;
			for (int i = 0; i < array.Length; i++)
			{
				GameObject ingredientVisual = array[i];
				if (ingredientVisual == null)
				{
					continue;
				}
				int num2 = activeTransferCount;
				activeTransferCount = num2 + 1;
				Transform transform = ingredientVisual.transform;
				transform.DOKill();
				transform.SetParent(null, worldPositionStays: true);
				Vector3 position = gastroPanTarget.position;
				position.x += UnityEngine.Random.Range(-0.025f, 0.025f);
				position.y += UnityEngine.Random.Range(0.005f, 0.02f);
				position.z += UnityEngine.Random.Range(-0.025f, 0.025f);
				float delay = (float)i * Mathf.Max(0f, stagger);
				Sequence sequence = DOTween.Sequence();
				sequence.SetDelay(delay);
				sequence.Join(transform.DOJump(position, 0.08f, 1, duration).SetEase(Ease.InOutQuad));
				sequence.Join(transform.DOScale(Vector3.one * 0.35f, duration).SetEase(Ease.InQuad));
				sequence.OnComplete(() =>
				{
					_spawnedObjects.Remove(ingredientVisual);
					if (ingredientVisual != null)
					{
						UnityEngine.Object.Destroy(ingredientVisual);
					}
					int num3 = activeTransferCount;
					activeTransferCount = num3 - 1;
					if ((activeTransferCount == 0) & showCenterInteractorWhenComplete)
					{
						ShowGastropanCenterInteractor();
					}
				});
			}
			PlayLocalPrepStationSfx(_ingredientsFallIntoGastroPanSfx, 1f, useRandomPitch: true);
		}

		private bool ClientShouldDeferGastroPanTransfer()
		{
			if (!_clientPrepCutCompletionPending && !_clientCutSessionPending && _clientCutSessionVersion == 0)
			{
				if (_ingredientMeshCutController != null)
				{
					return _ingredientMeshCutController.IsCutInProgress;
				}
				return false;
			}
			return true;
		}

		private void ClientTryRunPendingGastroPanLift()
		{
			if (_pendingGastroPanLift && !ClientShouldDeferGastroPanTransfer())
			{
				_pendingGastroPanLift = false;
				ClientLiftIngredientsForGastroPanLocal();
			}
		}

		private void ClientTryRunPendingGastroPanTransfer()
		{
			if (!(_pendingGastroPanTransferTarget == null) && !ClientShouldDeferGastroPanTransfer())
			{
				Transform pendingGastroPanTransferTarget = _pendingGastroPanTransferTarget;
				float pendingGastroPanTransferDuration = _pendingGastroPanTransferDuration;
				float pendingGastroPanTransferStagger = _pendingGastroPanTransferStagger;
				bool pendingGastroPanTransferShowsCenterInteractor = _pendingGastroPanTransferShowsCenterInteractor;
				ClientClearPendingGastroPanTransfer();
				ClientTransferIngredientsDirectlyToGastroPanLocal(pendingGastroPanTransferTarget, pendingGastroPanTransferDuration, pendingGastroPanTransferShowsCenterInteractor, pendingGastroPanTransferStagger);
			}
		}

		private void ClientClearPendingGastroPanTransfer()
		{
			_pendingGastroPanTransferTarget = null;
			_pendingGastroPanTransferDuration = 0f;
			_pendingGastroPanTransferStagger = 0f;
			_pendingGastroPanTransferShowsCenterInteractor = false;
			_pendingGastroPanLift = false;
			_clientGastroPanLiftReadyAt = 0f;
		}

		private void ClientFinalizePendingCutBeforeReplacingIngredientVisual()
		{
			if (_clientPrepCutCompletionPending)
			{
				_clientMeshReplicationFailed = true;
				_clientObserverCutQueue.Clear();
				ClientTryFinalizePendingPrepCutCompletion();
			}
		}

		public bool ServerAssignStaff(int id)
		{
			if (id <= 0 || !ServerStaffAvailable)
			{
				return false;
			}
			_staffEmployee.Value = id;
			if (base.OwnerClientId != 0L)
			{
				base.NetworkObject.RemoveOwnership();
			}
			CanUse.Value = false;
			return true;
		}

		public void ServerReleaseStaff(int id)
		{
			if (this.CanWriteServerState() && _staffEmployee.Value == id)
			{
				_staffRawVisual.Value = default;
				_staffEmployee.Value = 0;
				CanUse.Value = true;
			}
		}

		public void ServerStaffVisual(int id, string rawId)
		{
			if (base.IsServer && _staffEmployee.Value == id)
			{
				_staffRawVisual.Value = rawId ?? string.Empty;
			}
		}

		private void StaffVisualChanged(FixedString64Bytes before, FixedString64Bytes after)
		{
			if (_staffVisual != null)
			{
				UnityEngine.Object.Destroy(_staffVisual);
			}
			if (!after.IsEmpty && !(_ingredientCenterPosition == null) && _itemDatabaseSO.FindById(after.ToString()) is HoldableSO holdableSO && holdableSO.GraphicObjectPrefab != null)
			{
				_staffVisual = UnityEngine.Object.Instantiate(holdableSO.GraphicObjectPrefab, _ingredientCenterPosition, worldPositionStays: false);
				ApplyIngredientVisualPlacement(_staffVisual, holdableSO as IngredientSO);
				Collider[] componentsInChildren = _staffVisual.GetComponentsInChildren<Collider>();
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					componentsInChildren[i].enabled = false;
				}
			}
		}

		public bool ServerStaffTakePending(int id, out ProcessedIngredientSO output, out int count)
		{
			output = null;
			count = 0;
			if (!base.IsServer || StaffEmployeeId != id || !ServerTryGetPendingProcessedOutput(out var processedId, out count))
			{
				return false;
			}
			output = _itemDatabaseSO.FindById(processedId.ToString()) as ProcessedIngredientSO;
			if (output != null)
			{
				return count > 0;
			}
			return false;
		}

		public void ServerStaffClearPending(int id)
		{
			if (base.IsServer && StaffEmployeeId == id)
			{
				ServerResetStationInternal();
			}
		}

		public bool ServerStaffDispatchPan(int id, GastroPan pan, Transform overflow, bool orderedSlot = false)
		{
			if (_gastroPanSpawner != null)
			{
				return _gastroPanSpawner.ServerStaffDispatchPan(id, pan, overflow, orderedSlot);
			}
			return false;
		}

		public void ServerStaffEjectItem(int id, Vector3 position)
		{
			if (base.IsServer && StaffEmployeeId == id && !(CurrentItem == null))
			{
				BaseHoldable currentItem = CurrentItem;
				currentItem.CanHold.Value = true;
				if (currentItem.ServerTryStaffPickup(id))
				{
					currentItem.ServerStaffDrop(id, position, Quaternion.identity);
				}
				ServerResetStationInternal();
			}
		}

		public void ShowGastropanCenterInteractor()
		{
			if (IsLocalActiveUser)
			{
				if (_gastropanCenterInteractorImage != null)
				{
					_gastropanCenterInteractorImage.enabled = true;
				}
				ClientUpdateInstructionText(PrepStationInfoTextId.ClickGastroPan);
				_gastroPanSpawner?.ClientPlayCenterAttentionFeedback();
			}
		}

		private void ServerResetStationInternal()
		{
			if (base.IsServer)
			{
				_currentIngredientItemType.Value = 0;
				_currentIngredientID.Value = string.Empty;
				_currentState.Value = 0;
				CurrentKnifeID = 0;
				_currentSelectedProcessedType = ProcessedType.Empty;
				_isFallbackOccupied = false;
				ServerClearPrepCutSession();
				ServerClearPendingProcessedOutput();
				ClientStationResetRpc();
			}
		}

		public void ServerResetForEndDay()
		{
			if (base.IsServer)
			{
				bool num = _gastroPanSpawner != null && _gastroPanSpawner.ServerResetRuntimeForEndDay("PrepStation end-day reset.");
				ServerResetStationInternal();
				if (num)
				{
					_isFallbackOccupied = true;
					ClientPrepareRpc();
				}
				InUse.Value = false;
				CanGaze.Value = true;
				if (base.NetworkObject != null)
				{
					base.NetworkObject.RemoveOwnership();
				}
				_serverActiveInteractor = null;
				ClientEndDayResetRpc();
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerStationResetRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3409758060u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3409758060u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (StaffEmployeeId == 0)
				{
					ServerResetStationInternal();
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		public void ClientHideGastropanCenterInteractorRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(838169810u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 838169810u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (_gastropanCenterInteractorImage != null)
				{
					_gastropanCenterInteractorImage.enabled = false;
				}
				ClientUpdateInstructionText(PrepStationInfoTextId.PlaceItemOnTable);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientStationResetRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3527625260u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3527625260u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientResetStationRuntime(forceExitUseMode: false);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientEndDayResetRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(730955800u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 730955800u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientResetStationRuntime(forceExitUseMode: true);
			}
		}

		private void ClientResetStationRuntime(bool forceExitUseMode)
		{
			_gastroPanSpawner?.ClientStopAttentionFeedback();
			ClientClearPendingGastroPanTransfer();
			ClientCancelPrepCutSession(restoreSourcePresentation: true);
			_localCuttingActive = false;
			_isKnifeHeld = false;
			_mustExitCutPoint = false;
			_currentSelectedProcessedType = ProcessedType.Empty;
			_hasLastKnifeDirection = false;
			_lastKnifeDirection = Vector3.zero;
			_currentTiltAngleY = 0f;
			KillCutFlowDelay();
			RestoreHiddenHoldableForStation();
			ShowCursorForLocalPrepStationUser();
			if (CurrentItemGraphicObjectPrefab != null)
			{
				CurrentItemGraphicObjectPrefab.transform.DOKill(complete: true);
				UnityEngine.Object.Destroy(CurrentItemGraphicObjectPrefab);
				CurrentItemGraphicObjectPrefab = null;
			}
			ClientDespawnIngredientVisuals();
			if (_cuttingViewManager != null)
			{
				_cuttingViewManager.SetCuttingViewActive(isActive: false);
			}
			if (_gastropanCenterInteractorImage != null)
			{
				_gastropanCenterInteractorImage.enabled = false;
			}
			if (_knifeModel != null)
			{
				_knifeModel.gameObject.SetActive(value: false);
			}
			if (_knifePressRoot != null)
			{
				_knifePressRoot.localEulerAngles = Vector3.zero;
			}
			if (_cuttingViewManager != null && _cuttingViewManager.CutPointUI != null)
			{
				_cuttingViewManager.CutPointUI.gameObject.SetActive(value: false);
			}
			_mousePositions.Clear();
			CurrentItem = null;
			_successfulCutCount = 0;
			_requiredCutCount = 0;
			_isFallbackOccupied = false;
			ResetCutProgressUI(deactivateParent: true);
			if (forceExitUseMode)
			{
				ClientForceExitUseModeForReset();
			}
		}

		private void ClientForceExitUseModeForReset()
		{
			if (Singleton<GameUIManager>.Instance != null && Singleton<GameUIManager>.Instance.TimeUI != null)
			{
				Singleton<GameUIManager>.Instance.TimeUI.SetVisibility(status: true);
			}
			if (_canvasGO != null)
			{
				_canvasGO.SetActive(value: false);
			}
			if (_stationTutorialPopupUI != null && _stationTutorialPopupUI.isActiveAndEnabled)
			{
				_stationTutorialPopupUI.Close();
			}
			if (Singleton<InputManager>.Instance != null && Singleton<InputManager>.Instance.InputHandler != null)
			{
				Singleton<InputManager>.Instance.InputHandler.KnifeAction -= OnKnifeAction;
				Singleton<InputManager>.Instance.InputHandler.Exit -= OnPrepStationExit;
				Singleton<InputManager>.Instance.InputHandler.PrepStationSlot -= OnSlotNumber;
				Singleton<InputManager>.Instance.InputHandler.PrepStationScroll -= OnScroll;
			}
			if (_currentInteractor != null && _currentInteractor.Player != null)
			{
				if (_currentInteractor.Player.CharacterVisuals != null && _currentInteractor.Player.CharacterVisuals.FpsRootObject != null)
				{
					UtilityMethods.SetLayerRecursive(_currentInteractor.Player.CharacterVisuals.FpsRootObject, "ViewModel", includeChildren: true);
				}
				_currentInteractor.SetInteractorStatus(status: true);
				_currentInteractor.Player.ResumeCharacter();
				_currentInteractor.Player.SwitchTo("Player", cursorVisible: false);
			}
			_currentInteractor = null;
			if (Singleton<QuickSlotHandler>.Instance != null)
			{
				Singleton<QuickSlotHandler>.Instance.gameObject.SetActive(value: true);
				Singleton<QuickSlotHandler>.Instance.ForceHideAllInventoryItemVisualsOnly();
			}
			EventBus<DeskCam>.Raise(new DeskCam
			{
				CinemachineBrain = null
			});
			ClientUpdateInstructionText(PrepStationInfoTextId.None);
			if (_recallInteractors == null)
			{
				return;
			}
			foreach (GastropanRecallInteractor recallInteractor in _recallInteractors)
			{
				if (recallInteractor != null && recallInteractor.ToolTipGO != null)
				{
					recallInteractor.ToolTipGO.SetActive(value: false);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerUpdateCurrentIngredientRpc(ushort itemType, FixedString64Bytes id)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2140466506u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, itemType);
				bufferWriter.WriteValueSafe(in id, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 2140466506u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (StaffEmployeeId == 0)
				{
					_currentIngredientItemType.Value = itemType;
					_currentIngredientID.Value = id;
				}
			}
		}

		private void ApplyIngredientVisualPlacement(GameObject instance, IngredientSO ingredientSO)
		{
			if (instance == null || _ingredientCenterPosition == null)
			{
				return;
			}
			Transform transform = instance.transform;
			transform.SetParent(_ingredientCenterPosition, worldPositionStays: false);
			IngredientVisualPlacementOverride ingredientVisualPlacementOverride = FindIngredientVisualPlacementOverride(ingredientSO);
			if (ingredientVisualPlacementOverride != null)
			{
				transform.localPosition = ingredientVisualPlacementOverride.LocalPosition;
				transform.localRotation = Quaternion.Euler(ingredientVisualPlacementOverride.LocalEulerAngles);
				return;
			}
			transform.localPosition = _defaultIngredientLocalPositionOffset;
			transform.localRotation = Quaternion.Euler(_defaultIngredientLocalEulerAngles);
			if (_autoFixIngredientVisualPlacement)
			{
				SnapIngredientVisualBottomToTable(instance, _ingredientSurfacePadding);
			}
		}

		private IngredientVisualPlacementOverride FindIngredientVisualPlacementOverride(IngredientSO ingredientSO)
		{
			if (ingredientSO == null || _ingredientVisualPlacementOverrides == null)
			{
				return null;
			}
			for (int i = 0; i < _ingredientVisualPlacementOverrides.Count; i++)
			{
				IngredientVisualPlacementOverride ingredientVisualPlacementOverride = _ingredientVisualPlacementOverrides[i];
				if (ingredientVisualPlacementOverride != null && ingredientVisualPlacementOverride.Matches(ingredientSO))
				{
					return ingredientVisualPlacementOverride;
				}
			}
			return null;
		}

		private void SnapIngredientVisualBottomToTable(GameObject instance, float surfacePadding)
		{
			if (TryGetVisualWorldBounds(instance, out var bounds))
			{
				float boundsMinLocalY = GetBoundsMinLocalY(bounds);
				float num = _ingredientTableSurfaceLocalY + surfacePadding - boundsMinLocalY;
				Transform obj = instance.transform;
				Vector3 localPosition = obj.localPosition;
				localPosition.y += num;
				obj.localPosition = localPosition;
			}
		}

		private bool TryGetVisualWorldBounds(GameObject instance, out Bounds bounds)
		{
			if (TryGetNonTriggerColliderBounds(instance, out bounds))
			{
				return true;
			}
			return TryGetRendererBounds(instance, out bounds);
		}

		private bool TryGetNonTriggerColliderBounds(GameObject instance, out Bounds bounds)
		{
			bounds = default;
			bool flag = false;
			Collider[] componentsInChildren = instance.GetComponentsInChildren<Collider>(includeInactive: true);
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
			return flag;
		}

		private bool TryGetRendererBounds(GameObject instance, out Bounds bounds)
		{
			bounds = default;
			bool flag = false;
			Renderer[] componentsInChildren = instance.GetComponentsInChildren<Renderer>(includeInactive: true);
			foreach (Renderer renderer in componentsInChildren)
			{
				if (!(renderer == null) && renderer.enabled)
				{
					if (!flag)
					{
						bounds = renderer.bounds;
						flag = true;
					}
					else
					{
						bounds.Encapsulate(renderer.bounds);
					}
				}
			}
			return flag;
		}

		private float GetBoundsMinLocalY(Bounds worldBounds)
		{
			Vector3 min = worldBounds.min;
			Vector3 max = worldBounds.max;
			float num = float.PositiveInfinity;
			for (int i = 0; i <= 1; i++)
			{
				for (int j = 0; j <= 1; j++)
				{
					for (int k = 0; k <= 1; k++)
					{
						Vector3 position = new Vector3((i == 0) ? min.x : max.x, (j == 0) ? min.y : max.y, (k == 0) ? min.z : max.z);
						float y = _ingredientCenterPosition.InverseTransformPoint(position).y;
						if (y < num)
						{
							num = y;
						}
					}
				}
			}
			return num;
		}

		protected override void __initializeVariables()
		{
			if (_currentIngredientID == null)
			{
				throw new Exception("PrepStation._currentIngredientID cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_currentIngredientID.Initialize(this);
			__nameNetworkVariable(_currentIngredientID, "_currentIngredientID");
			NetworkVariableFields.Add(_currentIngredientID);
			if (_currentIngredientItemType == null)
			{
				throw new Exception("PrepStation._currentIngredientItemType cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_currentIngredientItemType.Initialize(this);
			__nameNetworkVariable(_currentIngredientItemType, "_currentIngredientItemType");
			NetworkVariableFields.Add(_currentIngredientItemType);
			if (_currentState == null)
			{
				throw new Exception("PrepStation._currentState cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_currentState.Initialize(this);
			__nameNetworkVariable(_currentState, "_currentState");
			NetworkVariableFields.Add(_currentState);
			if (_hasPendingProcessedOutputNetwork == null)
			{
				throw new Exception("PrepStation._hasPendingProcessedOutputNetwork cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_hasPendingProcessedOutputNetwork.Initialize(this);
			__nameNetworkVariable(_hasPendingProcessedOutputNetwork, "_hasPendingProcessedOutputNetwork");
			NetworkVariableFields.Add(_hasPendingProcessedOutputNetwork);
			if (_pendingProcessedIdNetwork == null)
			{
				throw new Exception("PrepStation._pendingProcessedIdNetwork cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_pendingProcessedIdNetwork.Initialize(this);
			__nameNetworkVariable(_pendingProcessedIdNetwork, "_pendingProcessedIdNetwork");
			NetworkVariableFields.Add(_pendingProcessedIdNetwork);
			if (_pendingProcessedYieldNetwork == null)
			{
				throw new Exception("PrepStation._pendingProcessedYieldNetwork cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_pendingProcessedYieldNetwork.Initialize(this);
			__nameNetworkVariable(_pendingProcessedYieldNetwork, "_pendingProcessedYieldNetwork");
			NetworkVariableFields.Add(_pendingProcessedYieldNetwork);
			if (_pendingProcessedStartIndexNetwork == null)
			{
				throw new Exception("PrepStation._pendingProcessedStartIndexNetwork cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_pendingProcessedStartIndexNetwork.Initialize(this);
			__nameNetworkVariable(_pendingProcessedStartIndexNetwork, "_pendingProcessedStartIndexNetwork");
			NetworkVariableFields.Add(_pendingProcessedStartIndexNetwork);
			if (_staffEmployee == null)
			{
				throw new Exception("PrepStation._staffEmployee cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_staffEmployee.Initialize(this);
			__nameNetworkVariable(_staffEmployee, "_staffEmployee");
			NetworkVariableFields.Add(_staffEmployee);
			if (_staffRawVisual == null)
			{
				throw new Exception("PrepStation._staffRawVisual cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_staffRawVisual.Initialize(this);
			__nameNetworkVariable(_staffRawVisual, "_staffRawVisual");
			NetworkVariableFields.Add(_staffRawVisual);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3531767762u, __rpc_handler_3531767762, "ServerSetStateRpc", RpcInvokePermission.Everyone);
			__registerRpc(4234376363u, __rpc_handler_4234376363, "ReceivePrepStationSfxRpc", RpcInvokePermission.Everyone);
			__registerRpc(2576081075u, __rpc_handler_2576081075, "ServerDestroyCurrentItemGraphicRpc", RpcInvokePermission.Everyone);
			__registerRpc(3958363457u, __rpc_handler_3958363457, "DestroyCurrentItemGraphicRpc", RpcInvokePermission.Everyone);
			__registerRpc(1141873982u, __rpc_handler_1141873982, "ClientAutoSelectItemRpc", RpcInvokePermission.Everyone);
			__registerRpc(828923149u, __rpc_handler_828923149, "ServerRequestSetupCurrentIngredientVisualRpc", RpcInvokePermission.Everyone);
			__registerRpc(2040171445u, __rpc_handler_2040171445, "ClientsSetupCurrentIngredientVisualRpc", RpcInvokePermission.Everyone);
			__registerRpc(2585979711u, __rpc_handler_2585979711, "ServerBeginPrepCutSessionRpc", RpcInvokePermission.Everyone);
			__registerRpc(2745064174u, __rpc_handler_2745064174, "ClientBeginPrepCutSessionRpc", RpcInvokePermission.Everyone);
			__registerRpc(2604766197u, __rpc_handler_2604766197, "ServerAbortPrepCutSessionRpc", RpcInvokePermission.Everyone);
			__registerRpc(3301829188u, __rpc_handler_3301829188, "ServerRegisterPrepCutRpc", RpcInvokePermission.Everyone);
			__registerRpc(2544185255u, __rpc_handler_2544185255, "ClientApplyPrepCutForObserversRpc", RpcInvokePermission.Everyone);
			__registerRpc(1005326477u, __rpc_handler_1005326477, "ClientEnsurePendingProcessedVisualRpc", RpcInvokePermission.Everyone);
			__registerRpc(244959734u, __rpc_handler_244959734, "ClientPrepareRpc", RpcInvokePermission.Everyone);
			__registerRpc(1971569076u, __rpc_handler_1971569076, "ServerCompletePrepCutSessionRpc", RpcInvokePermission.Everyone);
			__registerRpc(51130561u, __rpc_handler_51130561, "ClientCompletePrepCutSessionRpc", RpcInvokePermission.Everyone);
			__registerRpc(4174525010u, __rpc_handler_4174525010, "ClientSpawnIngredientVisualRpc", RpcInvokePermission.Everyone);
			__registerRpc(1245674840u, __rpc_handler_1245674840, "ClientLiftIngredientsForGastroPanRpc", RpcInvokePermission.Everyone);
			__registerRpc(3409758060u, __rpc_handler_3409758060, "ServerStationResetRpc", RpcInvokePermission.Everyone);
			__registerRpc(838169810u, __rpc_handler_838169810, "ClientHideGastropanCenterInteractorRpc", RpcInvokePermission.Everyone);
			__registerRpc(3527625260u, __rpc_handler_3527625260, "ClientStationResetRpc", RpcInvokePermission.Everyone);
			__registerRpc(730955800u, __rpc_handler_730955800, "ClientEndDayResetRpc", RpcInvokePermission.Everyone);
			__registerRpc(2140466506u, __rpc_handler_2140466506, "ServerUpdateCurrentIngredientRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3531767762(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ServerSetStateRpc(value);
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4234376363(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out PrepStationSfxId value, default(FastBufferWriter.ForEnums));
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ReceivePrepStationSfxRpc(value, value2, value3, ext);
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2576081075(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ServerDestroyCurrentItemGraphicRpc();
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3958363457(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).DestroyCurrentItemGraphicRpc();
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1141873982(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ClientAutoSelectItemRpc();
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_828923149(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ServerRequestSetupCurrentIngredientVisualRpc(value);
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2040171445(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ClientsSetupCurrentIngredientVisualRpc(value);
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2585979711(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				ByteUnpacker.ReadValueBitPacked(reader, out ushort value2);
				RpcParams ext = rpcParams.Ext;
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ServerBeginPrepCutSessionRpc(value, value2, ext);
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2745064174(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				reader.ReadValueSafe(out FixedString64Bytes value2, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out FixedString64Bytes value3, default(FastBufferWriter.ForFixedStrings));
				ByteUnpacker.ReadValueBitPacked(reader, out int value4);
				ByteUnpacker.ReadValueBitPacked(reader, out int value5);
				ByteUnpacker.ReadValueBitPacked(reader, out int value6);
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ClientBeginPrepCutSessionRpc(value, value2, value3, value4, value5, value6);
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2604766197(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				RpcParams ext = rpcParams.Ext;
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ServerAbortPrepCutSessionRpc(value, ext);
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3301829188(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				reader.ReadValueSafe(out Vector3 value3);
				reader.ReadValueSafe(out Vector3 value4);
				reader.ReadValueSafe(out Vector3 value5);
				RpcParams ext = rpcParams.Ext;
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ServerRegisterPrepCutRpc(value, value2, value3, value4, value5, ext);
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2544185255(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				reader.ReadValueSafe(out Vector3 value3);
				reader.ReadValueSafe(out Vector3 value4);
				reader.ReadValueSafe(out Vector3 value5);
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ClientApplyPrepCutForObserversRpc(value, value2, value3, value4, value5);
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1005326477(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				ByteUnpacker.ReadValueBitPacked(reader, out int value3);
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ClientEnsurePendingProcessedVisualRpc(value, value2, value3);
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_244959734(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ClientPrepareRpc();
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1971569076(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				RpcParams ext = rpcParams.Ext;
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ServerCompletePrepCutSessionRpc(value, ext);
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_51130561(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				ByteUnpacker.ReadValueBitPacked(reader, out int value3);
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ClientCompletePrepCutSessionRpc(value, value2, value3);
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4174525010(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				ByteUnpacker.ReadValueBitPacked(reader, out int value3);
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ClientSpawnIngredientVisualRpc(value, value2, value3);
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1245674840(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ClientLiftIngredientsForGastroPanRpc();
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3409758060(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ServerStationResetRpc();
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_838169810(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ClientHideGastropanCenterInteractorRpc();
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3527625260(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ClientStationResetRpc();
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_730955800(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ClientEndDayResetRpc();
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2140466506(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ushort value);
				reader.ReadValueSafe(out FixedString64Bytes value2, default(FastBufferWriter.ForFixedStrings));
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PrepStation)target).ServerUpdateCurrentIngredientRpc(value, value2);
				((PrepStation)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "PrepStation";
		}
	}
}
