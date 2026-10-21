using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DG.Tweening;
using PaperPixelGames.Core.DailyReport;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Interaction.ToolTip;
using PaperPixelGames.Core.Kitchen.ChefDesk.ChefDeskElemensUI;
using PaperPixelGames.Core.Kitchen.ServedDishes;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Progress;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Core.StationTutorial;
using PaperPixelGames.Core.Tutorials;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Kitchen.ChefDesk
{
	public class ChefDesk : BaseUsable
	{
		public enum ChefDeskSfxId : byte
		{
			None = 0,
			PlaceIngredientOnCounter = 3,
			SpawnObject = 4,
			MoveObject = 5,
			SeasoningSprinkle = 6
		}

		private enum ChefDeskInfoTextId : byte
		{
			None = 0,
			SelectRecipe = 1,
			CompleteRecipe = 2,
			RollSushi = 3,
			MovePlateOrSeason = 4
		}

		[Serializable]
		public class SushiVisualSet
		{
			public SushiType Type;

			[Header("Visual Objects")]
			public GameObject SingleNoriOBJ;

			public GameObject SushiRiceOBJ;

			[Header("Cutting Presentation")]
			public GameObject CuttingRollPrefab;

			[Header("Bend Transforms")]
			public Transform SingleNoriBend;

			public Transform SushiRiceBend;

			[Header("Default Object Positions")]
			public Vector3 DefaultNoriPosition;

			public Vector3 DefaultRicePosition;

			[Header("Bend Roll Offsets")]
			public Vector3 NoriBendRollOffset;

			public Vector3 RiceBendRollOffset;
		}

		[Serializable]
		private struct CutProgressSlot
		{
			public Image baseImage;

			public Image fillImage;
		}

		private readonly List<Transform> _assemblyIngredientAnchors = new List<Transform>();

		private readonly List<Vector3> _assemblyIngredientDefaultPositions = new List<Vector3>();

		private readonly List<Quaternion> _assemblyIngredientDefaultRotations = new List<Quaternion>();

		private SushiType _preparedAssemblyType;

		private int _preparedAssemblyIngredientCount;

		private bool _assemblyRootDefaultsCached;

		private Vector3 _makiAssemblyRootDefaultLocalPosition;

		private Quaternion _makiAssemblyRootDefaultLocalRotation;

		private Vector3 _makiAssemblyRootDefaultLocalScale;

		private Vector3 _temakiAssemblyRootDefaultLocalPosition;

		private Quaternion _temakiAssemblyRootDefaultLocalRotation;

		private Vector3 _temakiAssemblyRootDefaultLocalScale;

		private const float specialZ = -0.34f;

		private const float specialXNori = -0.15f;

		private const float specialXRice = -0.1426f;

		[Header("Center Assembly Ingredient Offset")]
		[SerializeField]
		private Vector3 _makiIngredientCenterOffset;

		[SerializeField]
		[Min(0f)]
		private float _rollBoardSurfaceClearance = 0.002f;

		private const float rollFinishThresholdT = 0.98f;

		private bool _isCenteringAssembly;

		[Header("Visuals")]
		[SerializeField]
		private List<SushiVisualSet> _sushiVisualSets;

		[SerializeField]
		private Transform _centerPoint;

		[SerializeField]
		private Transform _knifeModel;

		[SerializeField]
		private GameObject _nigiriRiceOBJ;

		[Header("Ingredient Travel Visuals")]
		[SerializeField]
		private GameObject _cookedRiceTravelVisualPrefab;

		[SerializeField]
		private GameObject _makiAvocadoTravelVisualPrefab;

		[SerializeField]
		private GameObject _nigiriAvocadoTravelVisualPrefab;

		[Header("Dish Spawn Feedback")]
		[SerializeField]
		private ParticleSystem _dishSpawnVfx;

		[SerializeField]
		[Min(0f)]
		private float _dishSpawnVfxMinInterval = 0.1f;

		[Header("Dependencies")]
		[SerializeField]
		private List<ChefDeskGastroPan> _gastroPanList;

		[SerializeField]
		private ItemDatabaseSO _itemDatabaseSO;

		[SerializeField]
		private GameObject _gastroPanPrefab;

		[SerializeField]
		private PhysicalStateController _physicalStateController;

		[SerializeField]
		private ProgressionUnlockKey _repairProgressionUnlockKey;

		[SerializeField]
		private PhysicalUnlockController _physicalUnlockController;

		[SerializeField]
		private RecipeInfoCanvas _recipeInfoCanvas;

		[SerializeField]
		private CuttingViewManager _cuttingManager;

		[SerializeField]
		private ChefDeskBookCanvas _chefDeskBookCanvas;

		[SerializeField]
		private Canvas _sushiRollCanvas;

		[SerializeField]
		private StationTutorialPopupUI _stationTutorialPopupUI;

		[Header("UI")]
		[SerializeField]
		private GameObject _objToolTipGO;

		[SerializeField]
		private GameObject _outCanvasGO;

		[SerializeField]
		private GameObject _inCanvasGO;

		[SerializeField]
		private TextMeshProUGUI _instructionText;

		[Header("Roll UI")]
		[SerializeField]
		private Image _chefDeskSushiRollImage;

		[SerializeField]
		private GameObject _leftToRightmouseIconTweenMover;

		[SerializeField]
		private GameObject _bottomToTopmouseIconTweenMover;

		[Header("Spawn Points")]
		[SerializeField]
		private Transform _ingredientSpawnPointParent;

		[SerializeField]
		private Transform _nigiriSpawnPointParent;

		[SerializeField]
		private Transform _nigiriRiceSpawnPointParent;

		[SerializeField]
		private Transform _nigiriMeatSpawnPointParent;

		[SerializeField]
		private Transform _temakiSpawnPointParent;

		[SerializeField]
		private Transform _sashimiSpawnPointParent;

		[Header("Settings")]
		[SerializeField]
		private float _knifeDistanceFromCamera;

		[Header("Edge Slots")]
		[SerializeField]
		private List<Transform> _edgePoints = new List<Transform>();

		private readonly List<bool> _edgeOccupied = new List<bool>();

		[Space]
		[SerializeField]
		private StationTutorialData _tutorialData;

		[Header("SFX")]
		[SerializeField]
		private AudioClipSO _cuttingSFX;

		[SerializeField]
		private AudioClipSO _cuttingFoodSFX;

		[SerializeField]
		private AudioClipSO _placeIngredientOnCounterSfx;

		[SerializeField]
		private AudioClipSO _spawnObjectSfx;

		[SerializeField]
		private AudioClipSO _moveObjectSfx;

		[SerializeField]
		private AudioClipSO _seasoningSprinkleSfx;

		[Header("Audio Pitch")]
		[SerializeField]
		private float _minPitch = 0.95f;

		[SerializeField]
		private float _maxPitch = 1.05f;

		[Header("Seasoning")]
		[SerializeField]
		private Transform[] _placementPoints;

		private BaseHoldable[] _placedSeasonings;

		[Header("Topping")]
		[SerializeField]
		private Transform _toppingWorkPlaneCenter;

		[SerializeField]
		private Vector3 _toppingPlaneNormal = Vector3.up;

		[SerializeField]
		private float _deskClickDistance = 10f;

		[SerializeField]
		private LayerMask _deskClickMask;

		private LocalSeasoningApplicator _currentApplicator;

		private Camera _outputCamera;

		private Seasoning _selectedSeasoning;

		private Seasoning _hoveredSeasoning;

		private ServedDishSushi _hoveredCenterDish;

		private ChefDeskBook _hoveredChefDeskBook;

		private float _lastDishSpawnVfxTime;

		[SerializeField]
		private bool _debugHoverLog;

		private Collider _lastHoveredCollider;

		private string _lastHoveredDebugName;

		private float _gastropanHoldStartDelay = 0.25f;

		private float _gastropanHoldRepeatInterval = 0.14f;

		private ChefDeskGastroPan _heldPlacementGastroPan;

		private float _nextGastropanHoldPlaceTime;

		private NetworkObjectReference _lastSpawnedSushi;

		private EventBinding<ServedDishTakenFromDeskEvent> _servedDishTakenBinding;

		private readonly NetworkVariable<bool> _centerOccupied = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<byte> _currentState = new NetworkVariable<byte>(0);

		private readonly NetworkVariable<byte> _currentSushiType = new NetworkVariable<byte>(0);

		private readonly NetworkVariable<byte> _currentRecipeStepIndex = new NetworkVariable<byte>(0);

		private readonly NetworkVariable<bool> _isCustomSushi = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<bool> _isCustomCompositionLocked = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<bool> _isRecipeCompleted = new NetworkVariable<bool>(value: true);

		private readonly NetworkVariable<FixedString64Bytes> _selectedSushiSO_ID = new NetworkVariable<FixedString64Bytes>();

		private readonly NetworkList<FixedString64Bytes> _placedIngredientIdsNetwork = new NetworkList<FixedString64Bytes>();

		private readonly NetworkVariable<byte> _currentIngredientPlacedCountNetwork = new NetworkVariable<byte>(0);

		private int _currentIngredientPlacedCount;

		private int _ingredientSpawnIndex;

		private int _nigiriRiceSpawnIndex;

		private int _nigiriMeatSpawnIndex;

		private string _previewedSushiID;

		private Dictionary<SushiType, SushiVisualSet> _visualMap;

		private readonly List<NetworkObjectReference> _servedDishesOnDesk = new List<NetworkObjectReference>();

		private readonly List<Vector3> _defaultSlotPositions = new List<Vector3>();

		private readonly List<Vector3> _temakiDefaultSlotPositions = new List<Vector3>();

		private readonly List<Quaternion> _defaultSlotRotations = new List<Quaternion>();

		private readonly List<Quaternion> _temakiDefaultSlotRotations = new List<Quaternion>();

		private ChefDeskSushiRoll _sushiRollController;

		private ulong _serverCurrentUserClientId = ulong.MaxValue;

		private BaseHoldable _hiddenHoldableForChefDesk;

		private float _nigiriCompletionDelay = 0.35f;

		private Tween _directDishCompletionDelayTween;

		private ChefDeskInfoTextId _currentInstructionId = ChefDeskInfoTextId.SelectRecipe;

		private string _instructionTextOverride;

		private Coroutine _instructionLocalizationRoutine;

		[Header("Custom Sushi UI")]
		[SerializeField]
		private BaseButton _customModeButton;

		[SerializeField]
		private GameObject _customTypePanel;

		[SerializeField]
		private BaseButton _customMakiButton;

		[SerializeField]
		private BaseButton _customUramakiButton;

		[SerializeField]
		private BaseButton _customTemakiButton;

		[SerializeField]
		private BaseButton _customFinishButton;

		[SerializeField]
		private GameObject _saveCustomRecipePanel;

		[SerializeField]
		private TMP_InputField _customRecipeNameInput;

		[SerializeField]
		private BaseButton _saveCustomRecipeButton;

		[SerializeField]
		private BaseButton _customCancelButton;

		[Header("Custom Sushi Physical Prefabs")]
		[SerializeField]
		private NetworkObject _customMakiPrefab;

		[SerializeField]
		private NetworkObject _customUramakiPrefab;

		[SerializeField]
		private NetworkObject _customTemakiPrefab;

		private readonly NetworkVariable<byte> _resolvedCustomRecipeKind = new NetworkVariable<byte>(2);

		private readonly NetworkVariable<NetworkObjectReference> _savableCustomSushi = new NetworkVariable<NetworkObjectReference>();

		private bool _isCustomTypeSelectionOpen;

		private const string TemporaryCutPieceLayerName = "Ignore Raycast";

		[Header("Cutting")]
		[SerializeField]
		private Transform _knifePressRoot;

		[Header("Knife Hit Points")]
		[SerializeField]
		private Transform _knifeTip;

		[SerializeField]
		private Transform _knifeHeel;

		private float _knifeThickness = 8f;

		private float _knifeDownTiltAngle = 25f;

		private float _knifeDownTiltSmooth = 12f;

		[SerializeField]
		[Min(1f)]
		private float _knifeRotationMaxDegreesPerSecond = 1440f;

		[SerializeField]
		[Min(0f)]
		private float _knifeDirectionSmooth = 32f;

		private float _knifeMinAngleDelta = 0.35f;

		private bool _isKnifeHeld;

		private bool _mustExitCutPoint;

		private int _successfulCutCount;

		private int _requiredCutCount;

		private Vector3 _lastKnifeDirection;

		private bool _hasLastKnifeDirection;

		private float _currentTiltAngleY;

		[Header("Cut Item Feedback")]
		[SerializeField]
		private ParticleSystem _cutParticlePrefab;

		private float _cutItemScalePunch = 0.035f;

		private float _cutItemScaleDuration = 0.08f;

		[Header("Single Sushi Cut Piece")]
		[SerializeField]
		private BoxCollider _singleSushiSurfaceCollider;

		[SerializeField]
		[Min(0f)]
		private float _singleSushiSpawnHeight = 0.01f;

		[SerializeField]
		[Min(0f)]
		private float _singleSushiDirectionalOffset = 0.025f;

		[SerializeField]
		[Min(0f)]
		private float _singleSushiCutImpulse = 0.06f;

		[SerializeField]
		[Min(0f)]
		private float _singleSushiUpwardImpulse = 0.015f;

		[SerializeField]
		[Min(0.01f)]
		private float _singleSushiMass = 0.12f;

		[SerializeField]
		[Min(0f)]
		private float _singleSushiLinearDamping = 2f;

		[SerializeField]
		[Min(0f)]
		private float _singleSushiAngularDamping = 4f;

		[Header("Cut Flow")]
		[SerializeField]
		[Min(0f)]
		private float _nextCutPointDelayPadding = 0.025f;

		[Header("Roll Mesh Cutting")]
		[SerializeField]
		[Range(0f, 45f)]
		private float _rollMeshCutMaxPlaneAngle = 12f;

		[SerializeField]
		[Min(0f)]
		private float _rollMeshCutPointHeight = 0.05f;

		private Tween _cutFlowDelayTween;

		private bool _localCuttingActive;

		private bool _pendingCuttingStartAfterCentering;

		private int _serverTemporaryCutPieceSpawnCount;

		private int _serverRollMeshCutSequence;

		private int _serverRollMeshCutRequiredCount;

		private bool _serverRollMeshCutSessionActive;

		private ChefDeskRollMeshCutController _rollMeshCutController;

		private readonly List<GameObject> _temporaryCutPieces = new List<GameObject>();

		private readonly Vector3[] _cutPointWorldCorners = new Vector3[4];

		[Header("Cut Progress UI (Static Slots)")]
		[SerializeField]
		private RectTransform _cutProgressParent;

		private int _maxCutSlots = 6;

		private float _cutFillDuration = 0.15f;

		private float _cutDotPunchScale = 1.2f;

		private readonly List<CutProgressSlot> _cutProgressSlots = new List<CutProgressSlot>();

		private const string ChefDeskTutorialSeenKey = "StationTutorial.ChefDesk.Seen";

		private readonly NetworkVariable<int> _staffEmployee = new NetworkVariable<int>(0);

		private readonly NetworkVariable<FixedString64Bytes> _staffRecipeVisual = new NetworkVariable<FixedString64Bytes>();

		private GameObject _staffVisual;

		public IReadOnlyList<Transform> AssemblyIngredientAnchors => _assemblyIngredientAnchors;

		public IReadOnlyList<Vector3> AssemblyIngredientDefaultPositions => _assemblyIngredientDefaultPositions;

		public IReadOnlyList<Quaternion> AssemblyIngredientDefaultRotations => _assemblyIngredientDefaultRotations;

		public GameObject CookedRiceTravelVisualPrefab => _cookedRiceTravelVisualPrefab;

		public PhysicalStateController PhysicalStateController => _physicalStateController;

		public GameObject GastroPanPrefab => _gastroPanPrefab;

		public ChefDeskState CurrentState => (ChefDeskState)_currentState.Value;

		public bool IsCustomSushi => _isCustomSushi.Value;

		public bool IsCustomCompositionLocked => _isCustomCompositionLocked.Value;

		public byte CurrentRecipeStepIndex => _currentRecipeStepIndex.Value;

		public Transform CenterPoint => _centerPoint;

		public ItemDatabaseSO ItemDatabaseSO => _itemDatabaseSO;

		public SushiType CurrentSushiType => (SushiType)_currentSushiType.Value;

		public FixedString64Bytes SelectedSushiID => _selectedSushiSO_ID.Value;

		public bool HasRecipeContentOnTable
		{
			get
			{
				if (!_centerOccupied.Value)
				{
					return _placedIngredientIdsNetwork.Count > 0;
				}
				return true;
			}
		}

		public string SingleNoriSO_ID => Singleton<GameProgressManager>.Instance.ItemDatabaseSO.ProcessedNoriSO.Id;

		public string CookedRiceSO_ID => Singleton<GameProgressManager>.Instance.ItemDatabaseSO.CookedRice.Id;

		public string PreviewedSushiID
		{
			get
			{
				return _previewedSushiID;
			}
			set
			{
				_previewedSushiID = value;
			}
		}

		public List<TableObjectData> ObjectsOnTable { get; private set; } = new List<TableObjectData>();

		public List<Transform> SushiIngredientSlots { get; private set; } = new List<Transform>();

		public List<Transform> NigiriIngredientSlots { get; private set; } = new List<Transform>();

		public List<Transform> NigiriRiceIngredientSlots { get; private set; } = new List<Transform>();

		public List<Transform> NigiriMeatIngredientSlots { get; private set; } = new List<Transform>();

		public List<Transform> TemakiIngredientSlots { get; private set; } = new List<Transform>();

		public List<Transform> SashimiIngredientSlots { get; private set; } = new List<Transform>();

		public IReadOnlyList<ChefDeskGastroPan> GastroPans => _gastroPanList;

		public List<Vector3> DefaultSlotPositions => _defaultSlotPositions;

		public List<Vector3> TemakiDefaultSlotPositions => _temakiDefaultSlotPositions;

		public List<Quaternion> DefaultSlotRotations => _defaultSlotRotations;

		public List<Quaternion> TemakiDefaultSlotRotations => _temakiDefaultSlotRotations;

		public Dictionary<SushiType, SushiVisualSet> VisualMap => _visualMap;

		private ulong CurrentUserClientId
		{
			get
			{
				if (!(_currentInteractor != null))
				{
					return _serverCurrentUserClientId;
				}
				return _currentInteractor.OwnerClientId;
			}
		}

		private bool HasActiveUser => CurrentUserClientId != ulong.MaxValue;

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

		public SushiRecipeIdentityKind CurrentResolvedRecipeKind => (SushiRecipeIdentityKind)_resolvedCustomRecipeKind.Value;

		public int CustomFillingCount => Mathf.Max(0, _placedIngredientIdsNetwork.Count - 2);

		public bool CanFinishCustomSushi
		{
			get
			{
				if (IsCustomSushi && !IsCustomCompositionLocked)
				{
					return CustomSushiRules.HasValidBase(CurrentSushiType, BuildPlacedIngredientIdList(), SingleNoriSO_ID, CookedRiceSO_ID);
				}
				return false;
			}
		}

		public bool CanSaveCurrentCustomRecipe
		{
			get
			{
				ServedDishSushi servedDish;
				return TryGetSavableCustomSushi(out servedDish);
			}
		}

		private bool KnifePressing => _isKnifeHeld;

		private bool IsCuttingActiveLocally
		{
			get
			{
				if (!_localCuttingActive)
				{
					return CurrentState == ChefDeskState.Cutting;
				}
				return true;
			}
		}

		public int StaffEmployeeId => _staffEmployee.Value;

		public bool StaffLocked => StaffEmployeeId != 0;

		public bool ServerStaffAvailable
		{
			get
			{
				if (base.IsServer && base.IsSpawned && !StaffLocked && !InUse.Value && !HasActiveUser && _physicalStateController != null)
				{
					return _physicalStateController.IsPhysicallyUsable;
				}
				return false;
			}
		}

		public SushiSO ServerStaffPartialRecipe
		{
			get
			{
				if (!base.IsServer || IsCustomSushi || _placedIngredientIdsNetwork.Count <= 0)
				{
					return null;
				}
				return _itemDatabaseSO.FindById(SelectedSushiID.ToString()) as SushiSO;
			}
		}

		public event Action ClientGastroPanStockChanged;

		[Rpc(SendTo.Everyone)]
		private void ClientPrepareAssemblyLayoutRpc(byte sushiTypeValue, ushort ingredientCount)
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
				FastBufferWriter bufferWriter = __beginSendRpc(4054705145u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in sushiTypeValue, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, ingredientCount);
				__endSendRpc(ref bufferWriter, 4054705145u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientPrepareAssemblyLayout((SushiType)sushiTypeValue, ingredientCount);
			}
		}

		private void ClientPrepareAssemblyLayout(SushiType sushiType, int ingredientCount)
		{
			ingredientCount = Mathf.Max(0, ingredientCount);
			ClientCacheAssemblyRootDefaults();
			if (_preparedAssemblyType == sushiType && _preparedAssemblyIngredientCount == ingredientCount && _assemblyIngredientAnchors.Count == ingredientCount)
			{
				return;
			}
			List<GameObject> placedInteriorObjects = ClientDetachPlacedInteriorObjectsFromAssemblyAnchors();
			ClientClearAssemblyLayout();
			if (IsRollAssemblyType(sushiType) && ingredientCount != 0)
			{
				List<Transform> list = ResolveAssemblyLayoutMarkers(sushiType);
				Transform transform = ResolveAssemblyAnchorParent(sushiType);
				if (transform == null || list == null || list.Count == 0)
				{
					UtilityMethods.Log($"[ChefDesk Assembly] Layout preparation failed. Type={sushiType}, IngredientCount={ingredientCount}", LogType.Warning);
					return;
				}
				_preparedAssemblyType = sushiType;
				_preparedAssemblyIngredientCount = ingredientCount;
				BuildDynamicAssemblyAnchors(sushiType, ingredientCount, transform, list);
				ClientReattachPlacedInteriorObjects(placedInteriorObjects);
			}
		}

		private List<GameObject> ClientDetachPlacedInteriorObjectsFromAssemblyAnchors()
		{
			List<GameObject> list = new List<GameObject>();
			for (int i = 0; i < ObjectsOnTable.Count; i++)
			{
				TableObjectData tableObjectData = ObjectsOnTable[i];
				if (!(tableObjectData.Obj == null) && !IsAssemblyBaseIngredient(tableObjectData.IngredientId.ToString()))
				{
					Transform transform = tableObjectData.Obj.transform;
					if (!(transform.parent == null) && _assemblyIngredientAnchors.Contains(transform.parent))
					{
						list.Add(tableObjectData.Obj);
						transform.SetParent(ResolveAssemblyAnchorParent(CurrentSushiType), worldPositionStays: true);
					}
				}
			}
			return list;
		}

		private void ClientReattachPlacedInteriorObjects(List<GameObject> placedInteriorObjects)
		{
			int num = placedInteriorObjects?.Count ?? 0;
			int num2 = Mathf.Min(num, _assemblyIngredientAnchors.Count);
			for (int i = 0; i < num2; i++)
			{
				GameObject gameObject = placedInteriorObjects[i];
				Transform transform = _assemblyIngredientAnchors[i];
				if (!(gameObject == null) && !(transform == null))
				{
					Transform obj = gameObject.transform;
					obj.SetParent(transform, worldPositionStays: true);
					obj.position = transform.position;
					obj.rotation = transform.rotation;
				}
			}
			_ingredientSpawnIndex = num2;
			if (num > _assemblyIngredientAnchors.Count)
			{
				UtilityMethods.Log("[ChefDesk Assembly] Recipe switch could not reattach every placed ingredient. " + $"Placed={num}, Anchors={_assemblyIngredientAnchors.Count}", LogType.Warning);
			}
		}

		private void BuildDynamicAssemblyAnchors(SushiType sushiType, int ingredientCount, Transform anchorParent, List<Transform> layoutMarkers)
		{
			if (ingredientCount <= layoutMarkers.Count)
			{
				for (int i = 0; i < ingredientCount; i++)
				{
					Transform transform = layoutMarkers[i];
					Vector3 localPosition = ((transform != null) ? transform.localPosition : Vector3.zero);
					Quaternion localRotation = ((transform != null) ? transform.localRotation : Quaternion.identity);
					CreateAssemblyAnchor(i, anchorParent, localPosition, localRotation);
				}
				return;
			}
			GetMarkerBounds(sushiType, layoutMarkers, out var primaryMin, out var primaryMax, out var secondaryMin, out var secondaryMax);
			int num = ResolveAssemblyRowCount(sushiType, ingredientCount, layoutMarkers, secondaryMin, secondaryMax);
			int num2 = Mathf.CeilToInt((float)ingredientCount / (float)num);
			float num3 = (secondaryMin + secondaryMax) * 0.5f;
			if (Mathf.Abs(secondaryMax - secondaryMin) < 0.0001f && num > 1)
			{
				secondaryMin = num3 - 0.004f;
				secondaryMax = num3 + 0.004f;
			}
			for (int j = 0; j < ingredientCount; j++)
			{
				int num4 = j % num;
				int num5 = j / num;
				float num6 = ((num2 <= 1) ? 0.5f : ((float)num5 / (float)(num2 - 1)));
				float t = ((num <= 1) ? 0.5f : ((float)num4 / (float)(num - 1)));
				Transform transform2 = ResolveNearestLayoutMarker(layoutMarkers, num6);
				Vector3 localPosition2 = ((transform2 != null) ? transform2.localPosition : Vector3.zero);
				if (sushiType == SushiType.Temaki)
				{
					localPosition2.x = Mathf.Lerp(primaryMin, primaryMax, num6);
					localPosition2.z = Mathf.Lerp(secondaryMin, secondaryMax, t);
				}
				else
				{
					localPosition2.z = Mathf.Lerp(primaryMin, primaryMax, num6);
					localPosition2.y = Mathf.Lerp(secondaryMin, secondaryMax, t);
				}
				Quaternion localRotation2 = ((transform2 != null) ? transform2.localRotation : Quaternion.identity);
				CreateAssemblyAnchor(j, anchorParent, localPosition2, localRotation2);
			}
		}

		private void CreateAssemblyAnchor(int index, Transform anchorParent, Vector3 localPosition, Quaternion localRotation)
		{
			Transform transform = new GameObject($"AssemblyIngredientAnchor_{index:00}").transform;
			transform.SetParent(anchorParent, worldPositionStays: false);
			transform.localPosition = localPosition;
			transform.localRotation = localRotation;
			transform.localScale = Vector3.one;
			_assemblyIngredientAnchors.Add(transform);
			_assemblyIngredientDefaultPositions.Add(localPosition);
			_assemblyIngredientDefaultRotations.Add(localRotation);
		}

		private static int ResolveAssemblyRowCount(SushiType sushiType, int ingredientCount, List<Transform> layoutMarkers, float secondaryMin, float secondaryMax)
		{
			if (ingredientCount <= 1)
			{
				return 1;
			}
			if (Mathf.Abs(secondaryMax - secondaryMin) > 0.0001f)
			{
				return 2;
			}
			if (ingredientCount <= layoutMarkers.Count && (sushiType != SushiType.Temaki || ingredientCount <= 4))
			{
				return 1;
			}
			return 2;
		}

		private static Transform ResolveNearestLayoutMarker(List<Transform> layoutMarkers, float normalizedIndex)
		{
			if (layoutMarkers == null || layoutMarkers.Count == 0)
			{
				return null;
			}
			int index = Mathf.RoundToInt(Mathf.Clamp01(normalizedIndex) * (float)(layoutMarkers.Count - 1));
			return layoutMarkers[index];
		}

		private static void GetMarkerBounds(SushiType sushiType, List<Transform> layoutMarkers, out float primaryMin, out float primaryMax, out float secondaryMin, out float secondaryMax)
		{
			primaryMin = float.PositiveInfinity;
			primaryMax = float.NegativeInfinity;
			secondaryMin = float.PositiveInfinity;
			secondaryMax = float.NegativeInfinity;
			for (int i = 0; i < layoutMarkers.Count; i++)
			{
				Transform transform = layoutMarkers[i];
				if (!(transform == null))
				{
					Vector3 localPosition = transform.localPosition;
					float b = ((sushiType == SushiType.Temaki) ? localPosition.x : localPosition.z);
					float b2 = ((sushiType == SushiType.Temaki) ? localPosition.z : localPosition.y);
					primaryMin = Mathf.Min(primaryMin, b);
					primaryMax = Mathf.Max(primaryMax, b);
					secondaryMin = Mathf.Min(secondaryMin, b2);
					secondaryMax = Mathf.Max(secondaryMax, b2);
				}
			}
			if (float.IsNaN(primaryMin) || float.IsInfinity(primaryMin) || float.IsNaN(primaryMax) || float.IsInfinity(primaryMax))
			{
				primaryMin = (primaryMax = 0f);
			}
			if (float.IsNaN(secondaryMin) || float.IsInfinity(secondaryMin) || float.IsNaN(secondaryMax) || float.IsInfinity(secondaryMax))
			{
				secondaryMin = (secondaryMax = 0f);
			}
		}

		private List<Transform> ResolveAssemblyLayoutMarkers(SushiType sushiType)
		{
			if (sushiType != SushiType.Temaki)
			{
				return SushiIngredientSlots;
			}
			return TemakiIngredientSlots;
		}

		private Transform ResolveAssemblyAnchorParent(SushiType sushiType)
		{
			if (sushiType != SushiType.Temaki)
			{
				return _ingredientSpawnPointParent;
			}
			return _temakiSpawnPointParent;
		}

		internal void ClientEnsureAssemblyLayout()
		{
			if (IsRollAssemblyType(CurrentSushiType) && (_assemblyIngredientAnchors.Count <= 0 || _preparedAssemblyType != CurrentSushiType) && TryResolveRecipeDefinition(SelectedSushiID.ToString(), out var recipe))
			{
				ClientPrepareAssemblyLayout(recipe.SushiType, GetInteriorIngredientVisualCount(recipe));
			}
		}

		private void ClientRestorePlacedIngredientVisuals()
		{
			if (_placedIngredientIdsNetwork.Count == 0 || ObjectsOnTable.Count > 0)
			{
				return;
			}
			ClientEnsureAssemblyLayout();
			for (int i = 0; i < _placedIngredientIdsNetwork.Count; i++)
			{
				FixedString64Bytes ingredientId = _placedIngredientIdsNetwork[i];
				if (!(_itemDatabaseSO.FindById(ingredientId.ToString()) is ProcessedIngredientSO processedIngredient))
				{
					continue;
				}
				Transform transform = CurrentSushiType switch
				{
					SushiType.Nigiri => GetNextNigiriSpawnPoint(ingredientId.ToString()), 
					SushiType.Temaki => GetNextTemakiSpawnPoint(), 
					SushiType.Sashimi => GetNextSashimiSpawnPoint(), 
					_ => GetNextTableSpawnPoint(), 
				};
				if (!(transform == null))
				{
					GameObject gameObject = ((ingredientId.ToString() == CookedRiceSO_ID) ? CookedRiceTravelVisualPrefab : ResolveTableIngredientVisualPrefab(processedIngredient));
					if (!(gameObject == null))
					{
						GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject, transform.position, transform.rotation);
						gameObject2.SetActive(value: true);
						gameObject2.transform.SetParent(transform, worldPositionStays: true);
						ObjectsOnTable.Add(new TableObjectData(ingredientId, gameObject2));
					}
				}
			}
		}

		private int GetInteriorIngredientVisualCount(SushiSO sushiSO)
		{
			if (sushiSO?.Recipe == null)
			{
				return 0;
			}
			int num = 0;
			if (sushiSO.Recipe.Ingredients != null)
			{
				for (int i = 0; i < sushiSO.Recipe.Ingredients.Count; i++)
				{
					BaseIngredientNeed baseIngredientNeed = sushiSO.Recipe.Ingredients[i];
					if (baseIngredientNeed != null && !IsAssemblyBaseIngredient(baseIngredientNeed.IngredientId))
					{
						num += baseIngredientNeed.RequiredAmount;
					}
				}
			}
			if (sushiSO.Recipe.ProcessedIngredients != null)
			{
				for (int j = 0; j < sushiSO.Recipe.ProcessedIngredients.Count; j++)
				{
					BaseIngredientNeed baseIngredientNeed2 = sushiSO.Recipe.ProcessedIngredients[j];
					if (baseIngredientNeed2 != null && !IsAssemblyBaseIngredient(baseIngredientNeed2.IngredientId))
					{
						num += baseIngredientNeed2.RequiredAmount;
					}
				}
			}
			return num;
		}

		private bool IsAssemblyBaseIngredient(string ingredientId)
		{
			if (!(ingredientId == SingleNoriSO_ID))
			{
				return ingredientId == CookedRiceSO_ID;
			}
			return true;
		}

		private static bool IsRollAssemblyType(SushiType sushiType)
		{
			if (sushiType != SushiType.Maki && sushiType != SushiType.Uramaki)
			{
				return sushiType == SushiType.Temaki;
			}
			return true;
		}

		private Transform ClientGetNextAssemblyIngredientAnchor()
		{
			ClientEnsureAssemblyLayout();
			if (_ingredientSpawnIndex < 0 || _ingredientSpawnIndex >= _assemblyIngredientAnchors.Count)
			{
				UtilityMethods.Log($"[ChefDesk Assembly] No unique ingredient anchor is available. Type={CurrentSushiType}, " + $"PlacementIndex={_ingredientSpawnIndex}, AnchorCount={_assemblyIngredientAnchors.Count}", LogType.Warning);
				return null;
			}
			Transform result = _assemblyIngredientAnchors[_ingredientSpawnIndex];
			_ingredientSpawnIndex++;
			return result;
		}

		internal void ClientReleaseLastAssemblyIngredientAnchor()
		{
			if (IsRollAssemblyType(CurrentSushiType) && _ingredientSpawnIndex > 0)
			{
				_ingredientSpawnIndex--;
			}
		}

		internal void ClientCompactAssemblyIngredientAnchors()
		{
			if (IsRollAssemblyType(CurrentSushiType))
			{
				List<GameObject> placedInteriorObjects = ClientDetachPlacedInteriorObjectsFromAssemblyAnchors();
				ClientReattachPlacedInteriorObjects(placedInteriorObjects);
			}
		}

		internal void ClientResetAssemblyAnchorTransforms()
		{
			ClientResetAssemblyRootTransforms();
			for (int i = 0; i < _assemblyIngredientAnchors.Count; i++)
			{
				Transform transform = _assemblyIngredientAnchors[i];
				if (!(transform == null))
				{
					if (i < _assemblyIngredientDefaultPositions.Count)
					{
						transform.localPosition = _assemblyIngredientDefaultPositions[i];
					}
					if (i < _assemblyIngredientDefaultRotations.Count)
					{
						transform.localRotation = _assemblyIngredientDefaultRotations[i];
					}
				}
			}
		}

		private void ClientClearAssemblyLayout()
		{
			ClientResetAssemblyRootTransforms();
			for (int i = 0; i < _assemblyIngredientAnchors.Count; i++)
			{
				Transform transform = _assemblyIngredientAnchors[i];
				if (transform != null)
				{
					UnityEngine.Object.Destroy(transform.gameObject);
				}
			}
			_assemblyIngredientAnchors.Clear();
			_assemblyIngredientDefaultPositions.Clear();
			_assemblyIngredientDefaultRotations.Clear();
			_preparedAssemblyType = SushiType.Empty;
			_preparedAssemblyIngredientCount = 0;
		}

		internal Transform ClientGetAssemblyIngredientRoot()
		{
			return ResolveAssemblyAnchorParent(CurrentSushiType);
		}

		internal bool ClientTryGetActiveAssemblyIngredientCenter(out Vector3 center)
		{
			center = Vector3.zero;
			Bounds bounds = default;
			bool flag = false;
			for (int i = 0; i < _assemblyIngredientAnchors.Count; i++)
			{
				Transform transform = _assemblyIngredientAnchors[i];
				if (transform == null || !ClientSlotHasActiveIngredient(transform))
				{
					continue;
				}
				Renderer[] componentsInChildren = transform.GetComponentsInChildren<Renderer>(includeInactive: false);
				foreach (Renderer renderer in componentsInChildren)
				{
					if (!(renderer == null) && renderer.enabled && renderer.gameObject.activeInHierarchy)
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
			}
			if (!flag)
			{
				return false;
			}
			center = bounds.center;
			return true;
		}

		private void ClientCacheAssemblyRootDefaults()
		{
			if (!_assemblyRootDefaultsCached)
			{
				if (_ingredientSpawnPointParent != null)
				{
					_makiAssemblyRootDefaultLocalPosition = _ingredientSpawnPointParent.localPosition;
					_makiAssemblyRootDefaultLocalRotation = _ingredientSpawnPointParent.localRotation;
					_makiAssemblyRootDefaultLocalScale = _ingredientSpawnPointParent.localScale;
				}
				if (_temakiSpawnPointParent != null)
				{
					_temakiAssemblyRootDefaultLocalPosition = _temakiSpawnPointParent.localPosition;
					_temakiAssemblyRootDefaultLocalRotation = _temakiSpawnPointParent.localRotation;
					_temakiAssemblyRootDefaultLocalScale = _temakiSpawnPointParent.localScale;
				}
				_assemblyRootDefaultsCached = true;
			}
		}

		private void ClientResetAssemblyRootTransforms()
		{
			ClientCacheAssemblyRootDefaults();
			ClientResetAssemblyRootTransform(_ingredientSpawnPointParent, _makiAssemblyRootDefaultLocalPosition, _makiAssemblyRootDefaultLocalRotation, _makiAssemblyRootDefaultLocalScale);
			ClientResetAssemblyRootTransform(_temakiSpawnPointParent, _temakiAssemblyRootDefaultLocalPosition, _temakiAssemblyRootDefaultLocalRotation, _temakiAssemblyRootDefaultLocalScale);
		}

		private static void ClientResetAssemblyRootTransform(Transform root, Vector3 defaultLocalPosition, Quaternion defaultLocalRotation, Vector3 defaultLocalScale)
		{
			if (!(root == null))
			{
				root.DOKill();
				root.localPosition = defaultLocalPosition;
				root.localRotation = defaultLocalRotation;
				root.localScale = defaultLocalScale;
			}
		}

		internal bool ServerIsActiveChefDeskUser(ulong clientId)
		{
			if (base.IsServer && _serverCurrentUserClientId != ulong.MaxValue)
			{
				return clientId == _serverCurrentUserClientId;
			}
			return false;
		}

		internal bool ServerTryBeginCenterAssembly(ulong senderClientId, float rollCompletionT)
		{
			if (!ServerIsActiveChefDeskUser(senderClientId))
			{
				UtilityMethods.Log($"[ChefDesk] Roll finish blocked. Sender is not active user. SenderClientId={senderClientId}, ActiveClientId={_serverCurrentUserClientId}, State={CurrentState}");
				return false;
			}
			if (CurrentState != ChefDeskState.WaitingForRoll)
			{
				UtilityMethods.Log($"[ChefDesk] Roll finish blocked. Invalid state: {CurrentState}");
				return false;
			}
			if (rollCompletionT < 0.98f)
			{
				UtilityMethods.Log($"[ChefDesk] Roll finish blocked. Roll is not completed. RollT={rollCompletionT:0.000}, Required={0.98f:0.000}");
				return false;
			}
			if (!_isRecipeCompleted.Value)
			{
				UtilityMethods.Log("[ChefDesk] Roll finish blocked. Recipe is not completed.");
				return false;
			}
			_currentState.Value = 6;
			CenterAssemblyClientRpc();
			return true;
		}

		[Rpc(SendTo.Everyone)]
		private void CenterAssemblyClientRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(697800329u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 697800329u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientBeginCenterAssemblyMovement();
			}
		}

		private void ClientBeginCenterAssemblyMovement()
		{
			_isCenteringAssembly = true;
			_pendingCuttingStartAfterCentering = false;
			_sushiRollController?.ClientCompleteRollPresentation();
			ClientApplyCenterAssemblyMovement();
			_chefDeskSushiRollImage.enabled = false;
			_leftToRightmouseIconTweenMover.SetActive(value: false);
			_bottomToTopmouseIconTweenMover.SetActive(value: false);
		}

		private void ClientApplyCenterAssemblyMovement(float moveDuration = 0.35f, Ease ease = Ease.InOutSine)
		{
			List<Transform> list = ClientGetCurrentAssemblyParts();
			if (list.Count == 0)
			{
				ClientOnCenterAssemblyMovementCompleted();
				return;
			}
			int remaining = list.Count;
			SushiVisualSet value = null;
			_visualMap.TryGetValue(CurrentSushiType, out value);
			GameObject gameObject = value?.SingleNoriOBJ;
			GameObject gameObject2 = value?.SushiRiceOBJ;
			Transform outerShell = ((CurrentSushiType != SushiType.Uramaki) ? gameObject?.transform : gameObject2?.transform);
			Vector3 rollShellCenteringOffset = GetRollShellCenteringOffset(outerShell);
			Vector3 temakiAssemblyCenteringOffset = GetTemakiAssemblyCenteringOffset(gameObject?.transform);
			foreach (Transform item in list)
			{
				bool flag = gameObject != null && item == gameObject.transform;
				bool flag2 = gameObject2 != null && item == gameObject2.transform;
				if (flag | flag2)
				{
					if (CurrentSushiType == SushiType.Temaki)
					{
						Vector3 localPosition = item.localPosition;
						if (flag)
						{
							localPosition.x = -0.15f;
						}
						else
						{
							localPosition.x = -0.1426f;
						}
						item.DOLocalMove(localPosition, moveDuration).SetEase(ease).OnComplete(DoneOne);
					}
					else
					{
						item.DOMove(item.position + rollShellCenteringOffset, moveDuration).SetEase(ease).OnComplete(DoneOne);
					}
				}
				else
				{
					Vector3 endValue = ((CurrentSushiType == SushiType.Temaki) ? (item.position + temakiAssemblyCenteringOffset) : GetIngredientCenterTargetPosition(item, rollShellCenteringOffset));
					item.DOMove(endValue, moveDuration).SetEase(ease).OnComplete(DoneOne);
				}
			}
			void DoneOne()
			{
				if (--remaining == 0)
				{
					ClientOnCenterAssemblyMovementCompleted();
				}
			}
		}

		private Vector3 GetRollShellCenteringOffset(Transform outerShell)
		{
			if (outerShell == null || CurrentSushiType == SushiType.Temaki)
			{
				return Vector3.zero;
			}
			if (_centerPoint != null)
			{
				Vector3 up = _centerPoint.up;
				Vector3 vector = ((_sushiRollController != null && _sushiRollController.ClientTryGetVerticalRollCore(out var center)) ? center : ((!ClientTryGetRendererBoundsCenter(outerShell, out var center2)) ? outerShell.position : center2));
				Vector3 vector2 = Vector3.ProjectOnPlane(_centerPoint.position - vector, up);
				if (ClientTryGetRendererBounds(outerShell, out var bounds))
				{
					float boundsExtentAlongDirection = GetBoundsExtentAlongDirection(bounds, up);
					float num = Vector3.Dot(bounds.center, up) - boundsExtentAlongDirection;
					float num2 = Vector3.Dot(_centerPoint.position, up) + _rollBoardSurfaceClearance;
					Vector3 vector3 = up * (num2 - num);
					return vector2 + vector3;
				}
				return _centerPoint.position - vector;
			}
			Vector3 localPosition = outerShell.localPosition;
			localPosition.z = -0.34f;
			Transform parent = outerShell.parent;
			return ((parent != null) ? parent.TransformPoint(localPosition) : localPosition) - outerShell.position;
		}

		private Vector3 GetIngredientCenterTargetPosition(Transform ingredientRoot, Vector3 rollShellCenteringOffset)
		{
			if (ingredientRoot == null)
			{
				return base.transform.position;
			}
			Vector3 vector = ingredientRoot.position + rollShellCenteringOffset;
			Vector3 vector2 = ((_centerPoint != null) ? _centerPoint.TransformVector(_makiIngredientCenterOffset) : base.transform.TransformVector(_makiIngredientCenterOffset));
			return vector + vector2;
		}

		private Vector3 GetTemakiAssemblyCenteringOffset(Transform nori)
		{
			if (CurrentSushiType != SushiType.Temaki || nori == null)
			{
				return Vector3.zero;
			}
			Vector3 localPosition = nori.localPosition;
			localPosition.x = -0.15f;
			Transform parent = nori.parent;
			return ((parent != null) ? parent.TransformPoint(localPosition) : localPosition) - nori.position;
		}

		internal static bool ClientTryGetRendererBoundsCenter(Transform root, out Vector3 center)
		{
			center = Vector3.zero;
			if (!ClientTryGetRendererBounds(root, out var bounds))
			{
				return false;
			}
			center = bounds.center;
			return true;
		}

		private static bool ClientTryGetRendererBounds(Transform root, out Bounds bounds)
		{
			bounds = default;
			if (root == null)
			{
				return false;
			}
			Renderer[] componentsInChildren = root.GetComponentsInChildren<Renderer>(includeInactive: false);
			bool flag = false;
			foreach (Renderer renderer in componentsInChildren)
			{
				if (!(renderer == null) && renderer.enabled && renderer.gameObject.activeInHierarchy)
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

		private static float GetBoundsExtentAlongDirection(Bounds bounds, Vector3 direction)
		{
			return Vector3.Dot(rhs: new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z)), lhs: bounds.extents);
		}

		private List<Transform> ClientGetCurrentAssemblyParts()
		{
			List<Transform> list = new List<Transform>();
			SushiVisualSet value = null;
			_visualMap.TryGetValue(CurrentSushiType, out value);
			GameObject gameObject = value?.SingleNoriOBJ;
			GameObject gameObject2 = value?.SushiRiceOBJ;
			if (gameObject != null && gameObject.activeInHierarchy)
			{
				list.Add(gameObject.transform);
			}
			if (gameObject2 != null && gameObject2.activeInHierarchy)
			{
				list.Add(gameObject2.transform);
			}
			Transform transform = ClientGetAssemblyIngredientRoot();
			if (transform != null && ClientTryGetActiveAssemblyIngredientCenter(out var _))
			{
				list.Add(transform);
			}
			return list;
		}

		private static bool ClientSlotHasActiveIngredient(Transform slot)
		{
			for (int i = 0; i < slot.childCount; i++)
			{
				Transform child = slot.GetChild(i);
				if (child != null && child.gameObject.activeInHierarchy)
				{
					return true;
				}
			}
			return false;
		}

		private void ClientOnCenterAssemblyMovementCompleted()
		{
			_isCenteringAssembly = false;
			if (base.IsOwner)
			{
				if (_pendingCuttingStartAfterCentering || CurrentState == ChefDeskState.Cutting)
				{
					StartCutting();
				}
				else
				{
					ClientCompleteCenterAssembly();
				}
			}
		}

		private void ClientCompleteCenterAssembly()
		{
			if (CurrentSushiType == SushiType.Temaki)
			{
				ClientCompleteDishWithoutCutting(ChefDeskFinalizeReason.TemakiCompleted);
			}
			else
			{
				ServerRequestStartCuttingAfterCenteringRpc();
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestStartCuttingAfterCenteringRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1892314360u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1892314360u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!StaffLocked)
				{
					ulong senderClientId = rpcParams.Receive.SenderClientId;
					ServerTryStartCuttingAfterCentering(senderClientId, "ClientCompleteCenterAssembly");
				}
			}
		}

		private bool ServerTryStartCuttingAfterCentering(ulong senderClientId, string source)
		{
			if (_serverCurrentUserClientId == ulong.MaxValue || senderClientId != _serverCurrentUserClientId)
			{
				UtilityMethods.Log($"[ChefDesk] Cutting start blocked after centering. Source={source}, SenderClientId={senderClientId}, ActiveClientId={_serverCurrentUserClientId}, State={CurrentState}");
				return false;
			}
			if (CurrentState != ChefDeskState.Centering)
			{
				UtilityMethods.Log($"[ChefDesk] Cutting start blocked after centering. Source={source}, Invalid state: {CurrentState}");
				return false;
			}
			_currentState.Value = 3;
			return true;
		}

		internal GameObject ResolveTableIngredientVisualPrefab(ProcessedIngredientSO processedIngredient)
		{
			if (processedIngredient == null)
			{
				return null;
			}
			return ResolveAvocadoTravelVisualVariant(processedIngredient.IngredientType, CurrentSushiType) switch
			{
				1 => (_makiAvocadoTravelVisualPrefab != null) ? _makiAvocadoTravelVisualPrefab : processedIngredient.GraphicObjectPrefab, 
				2 => (_nigiriAvocadoTravelVisualPrefab != null) ? _nigiriAvocadoTravelVisualPrefab : processedIngredient.GraphicObjectPrefab, 
				_ => processedIngredient.GraphicObjectPrefab, 
			};
		}

		private static byte ResolveAvocadoTravelVisualVariant(IngredientType ingredientType, SushiType sushiType)
		{
			if (ingredientType != IngredientType.Avocado)
			{
				return 0;
			}
			switch (sushiType)
			{
			case SushiType.Maki:
			case SushiType.Uramaki:
				return 1;
			case SushiType.Nigiri:
				return 2;
			default:
				return 0;
			}
		}

		public bool ServerHasActiveRecipe(string recipeId)
		{
			if (!base.IsServer || string.IsNullOrWhiteSpace(recipeId) || !string.Equals(SelectedSushiID.ToString(), recipeId, StringComparison.Ordinal))
			{
				return false;
			}
			if (!InUse.Value && !HasRecipeContentOnTable)
			{
				ChefDeskState currentState = CurrentState;
				if (currentState != ChefDeskState.Idle)
				{
					return currentState != ChefDeskState.RecipeSelected;
				}
				return false;
			}
			return true;
		}

		protected override void Awake()
		{
			base.Awake();
			_visualMap = _sushiVisualSets.ToDictionary((SushiVisualSet x) => x.Type, (SushiVisualSet x) => x);
			_sushiRollController = ((_chefDeskSushiRollImage != null) ? _chefDeskSushiRollImage.GetComponent<ChefDeskSushiRoll>() : null);
			InitializeRollMeshCutController();
			_sushiRollCanvas.enabled = false;
		}

		public override void OnNetworkSpawn()
		{
			NetworkVariable<FixedString64Bytes> staffRecipeVisual = _staffRecipeVisual;
			staffRecipeVisual.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Combine(staffRecipeVisual.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(StaffRecipeVisualChanged));
			StaffRecipeVisualChanged(default, _staffRecipeVisual.Value);
			LocalizationSettings.SelectedLocaleChanged += OnInstructionLocaleChanged;
			_instructionLocalizationRoutine = StartCoroutine(RefreshInstructionWhenLocalizationReady());
			NetworkVariable<byte> currentState = _currentState;
			currentState.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(currentState.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnCurrentStateChanged));
			InitializeCustomSushiNetworkState();
			InitializeVisualState();
			InitializeGastroPans();
			InitializeIngredientSlots();
			InitializeEdgeSlots();
			InitializeEventBindings();
			InitializeSeasoningData();
			InitializeSlotDefaults();
			ClientEnsureAssemblyLayout();
			InitCutProgressSlots();
			_inCanvasGO.SetActive(value: false);
			DOVirtual.DelayedCall(0.1f, ClientRestoreLateJoinPresentation);
			DOVirtual.DelayedCall(0.15f, ClientRestoreLateJoinDishPositions);
		}

		public override void OnNetworkDespawn()
		{
			NetworkVariable<FixedString64Bytes> staffRecipeVisual = _staffRecipeVisual;
			staffRecipeVisual.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Remove(staffRecipeVisual.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(StaffRecipeVisualChanged));
			if (_staffVisual != null)
			{
				UnityEngine.Object.Destroy(_staffVisual);
			}
			LocalizationSettings.SelectedLocaleChanged -= OnInstructionLocaleChanged;
			if (_instructionLocalizationRoutine != null)
			{
				StopCoroutine(_instructionLocalizationRoutine);
			}
			_instructionLocalizationRoutine = null;
			NetworkVariable<byte> currentState = _currentState;
			currentState.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(currentState.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnCurrentStateChanged));
			ShutdownCustomSushiNetworkState();
			ClientResetRollMeshCutSession();
			ClientClearTemporaryCutPieces();
			base.OnNetworkDespawn();
			DeregisterEventBindings();
		}

		private void OnCurrentStateChanged(byte previousValue, byte currentValue)
		{
			ChefDeskState chefDeskState = (ChefDeskState)currentValue;
			if (previousValue == 5 && chefDeskState != ChefDeskState.Topping && IsLocalActiveUser)
			{
				ClearSelection();
			}
			if (chefDeskState == ChefDeskState.Cutting)
			{
				StartCutting();
			}
			if (IsCustomSushi && IsLocalActiveUser)
			{
				ClientRefreshCustomSushiUI();
			}
		}

		private void ClientRestoreLateJoinPresentation()
		{
			if (base.IsSpawned && CurrentState != ChefDeskState.Idle && (!SelectedSushiID.IsEmpty || IsCustomSushi))
			{
				ClientRestorePlacedIngredientVisuals();
				ChefDeskState currentState = CurrentState;
				if (currentState == ChefDeskState.Centering || currentState == ChefDeskState.Cutting)
				{
					ClientApplyCenterAssemblyMovement(0f);
					_chefDeskSushiRollImage.enabled = false;
				}
			}
		}

		private void ClientRestoreLateJoinDishPositions()
		{
			ServedDishSushi[] array = UnityEngine.Object.FindObjectsByType<ServedDishSushi>(FindObjectsSortMode.None);
			foreach (ServedDishSushi servedDishSushi in array)
			{
				if (servedDishSushi.ClientBelongsToChefDesk(this))
				{
					int assignedEdgeIndex = servedDishSushi.AssignedEdgeIndex;
					if (assignedEdgeIndex >= 0 && assignedEdgeIndex < _edgePoints.Count && _edgePoints[assignedEdgeIndex] != null)
					{
						servedDishSushi.transform.SetPositionAndRotation(_edgePoints[assignedEdgeIndex].position, _edgePoints[assignedEdgeIndex].rotation);
					}
				}
			}
		}

		private void Update()
		{
			if (base.IsOwner && !HandleCuttingUpdate())
			{
				HandleGastropanHoldPlacementUpdate();
				HandleDeskInteractionUpdate();
			}
		}

		private bool HandleCuttingUpdate()
		{
			if (!IsCuttingActiveLocally)
			{
				return false;
			}
			UpdateCuttingMouseTracking();
			UpdateKnifePressTilt();
			return true;
		}

		private void HandleDeskInteractionUpdate()
		{
			if (!CanProcessDeskInteraction())
			{
				ClientClearDeskHoverState();
				return;
			}
			if (_currentApplicator != null)
			{
				ClientClearDeskHoverState();
				return;
			}
			if (!TryDeskRaycast(out var hit))
			{
				if (_lastHoveredCollider != null)
				{
					_lastHoveredCollider = null;
					_lastHoveredDebugName = null;
					UtilityMethods.Log("[ChefDesk Hover] Nothing hit.");
				}
				ClientClearDeskHoverState();
				return;
			}
			if (_debugHoverLog)
			{
				DebugHoverHit(hit);
			}
			ClientUpdateChefDeskBookHover(hit);
			if (_hoveredChefDeskBook != null)
			{
				ClientClearSeasoningHover();
				ClientClearCenterDishHover();
				if (Input.GetMouseButtonDown(0) && _currentApplicator == null && CurrentState != ChefDeskState.Topping)
				{
					_hoveredChefDeskBook.OpenBookUI();
					ClientClearChefDeskBookHover();
				}
			}
			else if (!IsDeskInteractionState())
			{
				ClientClearSeasoningHover();
				ClientClearCenterDishHover();
			}
			else
			{
				ClientUpdateSeasoningHover(hit);
				ClientUpdateCenterDishHover(hit);
				ClientHandleDeskMouseActions(hit);
			}
		}

		private bool CanProcessDeskInteraction()
		{
			if (_currentInteractor != null)
			{
				return _currentInteractor.IsOwner;
			}
			return false;
		}

		private bool ShouldHideCursorForChefDeskUseMode()
		{
			if (!IsCuttingActiveLocally && CurrentState != ChefDeskState.Cutting && CurrentState != ChefDeskState.Topping && !(_currentApplicator != null))
			{
				return _selectedSeasoning != null;
			}
			return true;
		}

		private void RefreshCursorForChefDeskUseMode()
		{
			if (IsLocalActiveUser)
			{
				if (ShouldHideCursorForChefDeskUseMode())
				{
					Cursor.visible = false;
				}
				else
				{
					CursorHelper.CursorVisible = true;
				}
			}
		}

		private void ShowCursorForLocalChefDeskUser()
		{
			if (IsLocalActiveUser)
			{
				CursorHelper.CursorVisible = true;
			}
		}

		private bool IsDeskInteractionState()
		{
			if (CurrentState != ChefDeskState.TransferToEdge)
			{
				return CurrentState == ChefDeskState.Topping;
			}
			return true;
		}

		private void InitializeVisualState()
		{
			_objToolTipGO.SetActive(value: false);
			_chefDeskSushiRollImage.enabled = false;
			_leftToRightmouseIconTweenMover.SetActive(value: false);
			_bottomToTopmouseIconTweenMover.SetActive(value: false);
		}

		private void InitializeGastroPans()
		{
			for (int i = 0; i < _gastroPanList.Count; i++)
			{
				ChefDeskGastroPan chefDeskGastroPan = _gastroPanList[i];
				if (!(chefDeskGastroPan == null))
				{
					chefDeskGastroPan.ClientInitialize((byte)i);
					chefDeskGastroPan.ClientIngredientStateChanged -= OnClientGastroPanIngredientStateChanged;
					chefDeskGastroPan.ClientIngredientStateChanged += OnClientGastroPanIngredientStateChanged;
				}
			}
		}

		private void InitializeIngredientSlots()
		{
			SushiIngredientSlots.Clear();
			NigiriIngredientSlots.Clear();
			NigiriRiceIngredientSlots.Clear();
			NigiriMeatIngredientSlots.Clear();
			TemakiIngredientSlots.Clear();
			SashimiIngredientSlots.Clear();
			AddChildrenToList(_ingredientSpawnPointParent, SushiIngredientSlots);
			AddChildrenToList(_nigiriSpawnPointParent, NigiriIngredientSlots);
			InitializeNigiriIngredientSlots();
			AddChildrenToList(_temakiSpawnPointParent, TemakiIngredientSlots);
			AddChildrenToList(_sashimiSpawnPointParent, SashimiIngredientSlots);
		}

		private void InitializeNigiriIngredientSlots()
		{
			AddChildrenToList(_nigiriRiceSpawnPointParent, NigiriRiceIngredientSlots);
			AddChildrenToList(_nigiriMeatSpawnPointParent, NigiriMeatIngredientSlots);
			if (NigiriRiceIngredientSlots.Count > 0 || NigiriMeatIngredientSlots.Count > 0)
			{
				return;
			}
			int num = NigiriIngredientSlots.Count / 2;
			for (int i = 0; i < NigiriIngredientSlots.Count; i++)
			{
				if (i < num)
				{
					NigiriRiceIngredientSlots.Add(NigiriIngredientSlots[i]);
				}
				else
				{
					NigiriMeatIngredientSlots.Add(NigiriIngredientSlots[i]);
				}
			}
		}

		private void InitializeEdgeSlots()
		{
			_edgeOccupied.Clear();
			for (int i = 0; i < _edgePoints.Count; i++)
			{
				_edgeOccupied.Add(item: false);
			}
		}

		private void InitializeEventBindings()
		{
			_servedDishTakenBinding = new EventBinding<ServedDishTakenFromDeskEvent>(OnServedDishTakenFromDesk);
			EventBus<ServedDishTakenFromDeskEvent>.Register(_servedDishTakenBinding);
			_physicalStateController.OnPhysicallyUsableChanged += OnPhysicallyUsableChanged;
		}

		private void DeregisterEventBindings()
		{
			EventBus<ServedDishTakenFromDeskEvent>.Deregister(_servedDishTakenBinding);
			_physicalStateController.OnPhysicallyUsableChanged -= OnPhysicallyUsableChanged;
			DeregisterGastroPanEvents();
		}

		private void DeregisterGastroPanEvents()
		{
			if (_gastroPanList == null)
			{
				return;
			}
			for (int i = 0; i < _gastroPanList.Count; i++)
			{
				ChefDeskGastroPan chefDeskGastroPan = _gastroPanList[i];
				if (!(chefDeskGastroPan == null))
				{
					chefDeskGastroPan.ClientIngredientStateChanged -= OnClientGastroPanIngredientStateChanged;
				}
			}
		}

		private void InitializeSeasoningData()
		{
			_placedSeasonings = new BaseHoldable[_placementPoints.Length];
		}

		private void InitializeSlotDefaults()
		{
			CacheLocalPositions(SushiIngredientSlots, _defaultSlotPositions);
			CacheLocalPositions(TemakiIngredientSlots, _temakiDefaultSlotPositions);
			CacheLocalRotations(SushiIngredientSlots, _defaultSlotRotations);
			CacheLocalRotations(TemakiIngredientSlots, _temakiDefaultSlotRotations);
		}

		private static void AddChildrenToList(Transform parent, List<Transform> targetList)
		{
			if (parent == null)
			{
				return;
			}
			foreach (Transform item in parent)
			{
				targetList.Add(item);
			}
		}

		private static void CacheLocalPositions(List<Transform> slots, List<Vector3> targetPositions)
		{
			targetPositions.Clear();
			foreach (Transform slot in slots)
			{
				targetPositions.Add((slot != null) ? slot.localPosition : Vector3.zero);
			}
		}

		private static void CacheLocalRotations(List<Transform> slots, List<Quaternion> targetRotations)
		{
			targetRotations.Clear();
			foreach (Transform slot in slots)
			{
				targetRotations.Add((slot != null) ? slot.localRotation : Quaternion.identity);
			}
		}

		private static string GetText(ChefDeskInfoTextId id)
		{
			var (key, fallback) = id switch
			{
				ChefDeskInfoTextId.SelectRecipe => ("chef_desk.action.select_recipe", "Select Recipe"), 
				ChefDeskInfoTextId.CompleteRecipe => ("chef_desk.instruction.complete_recipe", "Complete recipe"), 
				ChefDeskInfoTextId.RollSushi => ("chef_desk.instruction.roll_sushi", "Roll sushi"), 
				ChefDeskInfoTextId.MovePlateOrSeason => ("chef_desk.instruction.move_plate", "Move plate"), 
				_ => (string.Empty, string.Empty), 
			};
			return LocalizationDatabase.GetStringOrFallback("UiTable", key, fallback);
		}

		private IEnumerator RefreshInstructionWhenLocalizationReady()
		{
			RefreshInstructionLocalization();
			yield return LocalizationSettings.InitializationOperation;
			RefreshInstructionLocalization();
			_instructionLocalizationRoutine = null;
		}

		private void OnInstructionLocaleChanged(Locale _)
		{
			if (IsCustomSushi || _isCustomTypeSelectionOpen)
			{
				ClientRefreshCustomSushiUI();
			}
			else
			{
				RefreshInstructionLocalization();
			}
		}

		private void RefreshInstructionLocalization()
		{
			if (_instructionText != null)
			{
				_instructionText.text = (string.IsNullOrEmpty(_instructionTextOverride) ? GetText(_currentInstructionId) : _instructionTextOverride);
			}
		}

		private void ClientUpdateInstructionText(ChefDeskInfoTextId textId)
		{
			if (!(_currentInteractor == null) && _currentInteractor.OwnerClientId == base.NetworkObject.OwnerClientId)
			{
				_instructionTextOverride = null;
				_currentInstructionId = textId;
				RefreshInstructionLocalization();
			}
		}

		private void ClientUpdateInstructionText(string text)
		{
			if (!(_currentInteractor == null) && _currentInteractor.OwnerClientId == base.NetworkObject.OwnerClientId)
			{
				_instructionTextOverride = text ?? string.Empty;
				RefreshInstructionLocalization();
			}
		}

		private void HideHoldableForChefDesk(BaseHoldable holdable)
		{
			if (!(holdable == null))
			{
				_hiddenHoldableForChefDesk = holdable;
				_hiddenHoldableForChefDesk.SetVisibility(visible: false);
			}
		}

		private void RestoreHiddenHoldableForChefDesk()
		{
			if (!(_hiddenHoldableForChefDesk == null))
			{
				_hiddenHoldableForChefDesk.SetVisibility(visible: true);
				_hiddenHoldableForChefDesk = null;
			}
		}

		private void ClientHandleDeskMouseActions(RaycastHit hit)
		{
			if (Input.GetMouseButtonDown(0) && !ClientTryHandleSeasoningClick(hit))
			{
				ClientTryHandleServedDishClick(hit);
			}
		}

		private bool ClientTryHandleSeasoningClick(RaycastHit hit)
		{
			if (_currentApplicator != null)
			{
				return false;
			}
			Seasoning componentInParent = hit.collider.GetComponentInParent<Seasoning>();
			if (componentInParent == null)
			{
				return false;
			}
			ClientSelectSeasoning(_currentInteractor, componentInParent);
			return true;
		}

		private bool ClientTryHandleServedDishClick(RaycastHit hit)
		{
			if (_currentApplicator != null)
			{
				return false;
			}
			if (!IsDeskInteractionState())
			{
				return false;
			}
			ServedDishSushi componentInParent = hit.collider.GetComponentInParent<ServedDishSushi>();
			if (componentInParent == null)
			{
				return false;
			}
			if (componentInParent.AssignedEdgeIndex >= 0)
			{
				return true;
			}
			ClientClearCenterDishHover();
			ServerRequestMoveSushiToEdgeRpc();
			return true;
		}

		private void ClientSelectSeasoning(Interactor interactor, Seasoning seasoning)
		{
			if (interactor == null || !interactor.IsOwner || seasoning == null || seasoning.GraphicObjectPrefab == null)
			{
				return;
			}
			if (!CanSelectedSushiReceiveSeasoning())
			{
				Singleton<NotifyManager>.Instance.ShowToast("This sushi type cannot be seasoned.");
			}
			else if (TryEnsureOutputCamera() && !(_toppingWorkPlaneCenter == null))
			{
				ClearSelection();
				GameObject gameObject = UnityEngine.Object.Instantiate(seasoning.GraphicObjectPrefab);
				_currentApplicator = gameObject.GetComponent<LocalSeasoningApplicator>();
				if (_currentApplicator == null)
				{
					UnityEngine.Object.Destroy(gameObject);
					return;
				}
				_selectedSeasoning = seasoning;
				_selectedSeasoning.ClientSetHoverVisual(isHovering: false);
				_selectedSeasoning.SetVisibility(visible: false);
				Vector3 inNormal = ((_toppingPlaneNormal.sqrMagnitude > Mathf.Epsilon) ? _toppingPlaneNormal.normalized : Vector3.up);
				Plane workPlane = new Plane(inNormal, _toppingWorkPlaneCenter.position);
				_currentApplicator.Initialize(_outputCamera, workPlane, seasoning);
				RefreshCursorForChefDeskUseMode();
				ServerRequestBeginToppingRpc();
			}
		}

		private bool CanSelectedSushiReceiveSeasoning()
		{
			return SeasoningReceiver.AllowedSushiTypes.Contains((ushort)CurrentSushiType);
		}

		private void ClearSelection()
		{
			if (_currentApplicator != null)
			{
				UnityEngine.Object.Destroy(_currentApplicator.gameObject);
			}
			_currentApplicator = null;
			if (_selectedSeasoning != null)
			{
				_selectedSeasoning.SetVisibility(visible: true);
				_selectedSeasoning = null;
			}
			RefreshCursorForChefDeskUseMode();
		}

		private bool TryDeskRaycast(out RaycastHit hit)
		{
			hit = default;
			if (!TryEnsureOutputCamera())
			{
				return false;
			}
			return Physics.Raycast(_outputCamera.ScreenPointToRay(Input.mousePosition), out hit, _deskClickDistance, _deskClickMask, QueryTriggerInteraction.Ignore);
		}

		private bool TryEnsureOutputCamera()
		{
			if (_outputCamera != null)
			{
				return true;
			}
			if (_currentInteractor == null || _currentInteractor.Player == null)
			{
				return false;
			}
			_outputCamera = _currentInteractor.Player.CinemachineBrain.OutputCamera ?? _currentInteractor.Player.CinemachineBrain.GetComponent<Camera>();
			return _outputCamera != null;
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestBeginToppingRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3556829776u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3556829776u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!StaffLocked && (CurrentState == ChefDeskState.TransferToEdge || CurrentState == ChefDeskState.Topping) && _centerOccupied.Value)
				{
					_currentState.Value = 5;
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerSetStateRpc(ChefDeskState newState)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1364185859u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in newState, default(FastBufferWriter.ForEnums));
				__endSendRpc(ref bufferWriter, 1364185859u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!StaffLocked)
			{
				_currentState.Value = (byte)newState;
				if (newState == ChefDeskState.WaitingForRoll)
				{
					ClientPrepareRollPresentationRpc();
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientPrepareRollPresentationRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(4086558293u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 4086558293u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_sushiRollController?.ClientPrepareRollPresentation();
			}
		}

		public SushiSO GetSelectedSushi()
		{
			if (SelectedSushiID.IsEmpty)
			{
				UtilityMethods.Log("[ChefDeskBook] SelectedSushiID is null or empty.");
				return null;
			}
			if (!(_itemDatabaseSO.FindById(SelectedSushiID) is SushiSO result))
			{
				UtilityMethods.Log("[ChefDeskBook] Item found by ID is not a SushiSO. ID=" + SelectedSushiID.ToString());
				return null;
			}
			return result;
		}

		[Rpc(SendTo.Owner)]
		public void ClientShowNotifyRpc(FixedString64Bytes header, FixedString64Bytes message)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2323104352u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in header, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in message, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 2323104352u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Singleton<NotifyManager>.Instance.ShowToast(message.ToString());
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		public void ClientShowNotifyRpc(FixedString64Bytes header, FixedString128Bytes message, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(588815474u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in header, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in message, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 588815474u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Singleton<NotifyManager>.Instance.ShowToast(message.ToString());
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		public void ClientGastropanSFXRpc(ulong placingClientId, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1767421952u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, placingClientId);
				__endSendRpc(ref bufferWriter, 1767421952u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsClient && !(base.NetworkManager == null) && base.NetworkManager.LocalClientId == placingClientId)
				{
					Singleton<SoundManager>.Instance.PlaySFX(SoundType.Item_Pick, HoldableMaterialType.Plastic, FurnitureWeight.Medium);
				}
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

		public void PlayChefDeskSfxForActiveUser(ChefDeskSfxId sfxId, float volume = 1f, bool useRandomPitch = false)
		{
			if (base.IsServer && HasActiveUser)
			{
				BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(CurrentUserClientId, RpcTargetUse.Temp);
				ReceiveChefDeskSfxRpc(sfxId, Mathf.Clamp01(volume), useRandomPitch, baseRpcTarget);
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ReceiveChefDeskSfxRpc(ChefDeskSfxId sfxId, float volume, bool useRandomPitch, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(399358711u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in sfxId, default(FastBufferWriter.ForEnums));
				bufferWriter.WriteValueSafe(in volume, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in useRandomPitch, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 399358711u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (TryGetChefDeskSfx(sfxId, out var sfx))
				{
					PlayLocalChefDeskSfx(sfx, volume, useRandomPitch);
				}
			}
		}

		private bool TryGetChefDeskSfx(ChefDeskSfxId sfxId, out AudioClipSO sfx)
		{
			sfx = sfxId switch
			{
				ChefDeskSfxId.PlaceIngredientOnCounter => _placeIngredientOnCounterSfx, 
				ChefDeskSfxId.SpawnObject => _spawnObjectSfx, 
				ChefDeskSfxId.MoveObject => _moveObjectSfx, 
				ChefDeskSfxId.SeasoningSprinkle => _seasoningSprinkleSfx, 
				_ => null, 
			};
			return sfx != null;
		}

		private void PlayLocalChefDeskSfx(AudioClipSO sfx, float volume = 1f, bool useRandomPitch = false)
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

		public override void GazeEnter()
		{
			base.GazeEnter();
			_objToolTipGO.SetActive(value: true);
			if (_physicalStateController != null && !_physicalStateController.IsPhysicallyUsable)
			{
				_physicalUnlockController?.ShowTooltipLocal();
			}
		}

		public override void GazeExit()
		{
			base.GazeExit();
			_objToolTipGO.SetActive(value: false);
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

		public bool ServerBeginToppingFromRack()
		{
			if (!ServerCanBeginToppingFromRack())
			{
				return false;
			}
			_currentState.Value = 5;
			return true;
		}

		private bool ServerCanBeginToppingFromRack()
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (CurrentState != ChefDeskState.TransferToEdge)
			{
				return false;
			}
			if (!_centerOccupied.Value)
			{
				return false;
			}
			if (!_lastSpawnedSushi.TryGet(out var networkObject) || networkObject == null)
			{
				return false;
			}
			if (!networkObject.TryGetComponent<ServedDishSushi>(out var component))
			{
				return false;
			}
			return component.IsSpawned;
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientSetDeskDishCollidersRpc(NetworkObjectReference[] dishRefs, bool isTrigger, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1906048811u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bool value = dishRefs != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(dishRefs, default(FastBufferWriter.ForNetworkSerializable));
				}
				bufferWriter.WriteValueSafe(in isTrigger, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1906048811u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			for (int i = 0; i < dishRefs.Length; i++)
			{
				if (dishRefs[i].TryGet(out var networkObject) && !(networkObject == null) && networkObject.TryGetComponent<ServedDishSushi>(out var component))
				{
					Collider component2 = component.GetComponent<Collider>();
					if (component2 != null)
					{
						component2.isTrigger = isTrigger;
					}
				}
			}
		}

		private void OnServedDishTakenFromDesk(ServedDishTakenFromDeskEvent eventData)
		{
			if (!base.IsServer || eventData.DishNetworkObject == null || !ServerContainsServedDish(eventData.DishNetworkObject))
			{
				return;
			}
			int num = -2;
			bool num2 = eventData.DishNetworkObject.TryGetComponent<ServedDishSushi>(out var component);
			if (num2)
			{
				num = component.AssignedEdgeIndex;
			}
			bool num3 = IsCurrentCenterDish(matchesLastSpawned: _lastSpawnedSushi.TryGet(out var networkObject) && networkObject == eventData.DishNetworkObject, centerOccupied: _centerOccupied.Value, assignedEdgeIndex: num);
			ServerUnregisterServedDish(eventData.DishNetworkObject);
			ServerClearSavableCustomSushiIfMatches(eventData.DishNetworkObject);
			if (num3)
			{
				_lastSpawnedSushi = default;
				_centerOccupied.Value = false;
				ServerReturnToRecipeSelectionAfterCenterDishWasTaken();
			}
			if (num2)
			{
				if (num >= 0 && num < _edgeOccupied.Count)
				{
					_edgeOccupied[num] = false;
				}
				component.SetAssignedEdgeIndexServer(-2);
				component.ServerAssignChefDesk(null);
			}
		}

		private static bool IsCurrentCenterDish(bool centerOccupied, int assignedEdgeIndex, bool matchesLastSpawned)
		{
			return (centerOccupied && assignedEdgeIndex == -1) & matchesLastSpawned;
		}

		private void ServerReturnToRecipeSelectionAfterCenterDishWasTaken()
		{
			_currentState.Value = 0;
			_currentRecipeStepIndex.Value = 0;
			_isRecipeCompleted.Value = true;
			if (IsCustomSushi)
			{
				ServerPrepareCustomModeForNextSushi();
				ClientReadyForNextCustomSushiRpc();
			}
			else
			{
				SendInstructionToOwnerRpc(ChefDeskInfoTextId.CompleteRecipe);
			}
		}

		private bool ServerContainsServedDish(NetworkObject dishNetworkObject)
		{
			if (!base.IsServer || dishNetworkObject == null)
			{
				return false;
			}
			for (int num = _servedDishesOnDesk.Count - 1; num >= 0; num--)
			{
				if (!_servedDishesOnDesk[num].TryGet(out var networkObject) || networkObject == null)
				{
					_servedDishesOnDesk.RemoveAt(num);
				}
				else if (networkObject == dishNetworkObject)
				{
					return true;
				}
			}
			return false;
		}

		private void ServerRegisterServedDish(NetworkObject dishNetworkObject)
		{
			if (!base.IsServer || dishNetworkObject == null)
			{
				return;
			}
			for (int num = _servedDishesOnDesk.Count - 1; num >= 0; num--)
			{
				if (!_servedDishesOnDesk[num].TryGet(out var networkObject) || networkObject == null)
				{
					_servedDishesOnDesk.RemoveAt(num);
				}
				else if (networkObject == dishNetworkObject)
				{
					return;
				}
			}
			_servedDishesOnDesk.Add(dishNetworkObject);
		}

		private void ServerUnregisterServedDish(NetworkObject dishNetworkObject)
		{
			if (!base.IsServer || dishNetworkObject == null)
			{
				return;
			}
			for (int num = _servedDishesOnDesk.Count - 1; num >= 0; num--)
			{
				if (!_servedDishesOnDesk[num].TryGet(out var networkObject) || networkObject == null || networkObject == dishNetworkObject)
				{
					_servedDishesOnDesk.RemoveAt(num);
				}
			}
		}

		private void OnClientGastroPanIngredientStateChanged(ChefDeskGastroPan gastroPan)
		{
			ClientGastroPanStockChanged?.Invoke();
		}

		[Rpc(SendTo.Owner)]
		private void SetRecipeCanvasVisibilityRpc(bool isVisible)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1893322649u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in isVisible, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1893322649u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (isVisible)
				{
					_recipeInfoCanvas.Show();
				}
				else
				{
					_recipeInfoCanvas.Hide();
				}
			}
		}

		private void ClientRefreshRecipeCanvasFromState()
		{
			if (SelectedSushiID.IsEmpty)
			{
				_recipeInfoCanvas.gameObject.SetActive(value: false);
				return;
			}
			if (!TryResolveRecipeDefinition(SelectedSushiID.ToString(), out var recipe))
			{
				_recipeInfoCanvas.gameObject.SetActive(value: false);
				return;
			}
			_recipeInfoCanvas.ClientPrepare(recipe, _itemDatabaseSO);
			if (ObjectsOnTable != null)
			{
				foreach (TableObjectData item in ObjectsOnTable)
				{
					if (!item.IngredientId.IsEmpty)
					{
						_recipeInfoCanvas.ClientIngredientItemOnTablePlaced(item.IngredientId);
					}
				}
			}
			int num = ((recipe.IdentityKind == SushiRecipeIdentityKind.SavedCustom) ? recipe.Composition.Count : (GetSortedNeeds()?.Count ?? 0));
			if (num > 0 && !_isRecipeCompleted.Value)
			{
				Mathf.Clamp(_currentRecipeStepIndex.Value + 1, 1, num);
			}
		}

		public int ClientGetIngredientAmountInGastroPans(string ingredientId)
		{
			if (string.IsNullOrWhiteSpace(ingredientId))
			{
				return 0;
			}
			if (_gastroPanList == null || _gastroPanList.Count == 0)
			{
				return 0;
			}
			int num = 0;
			for (int i = 0; i < _gastroPanList.Count; i++)
			{
				ChefDeskGastroPan chefDeskGastroPan = _gastroPanList[i];
				if (!(chefDeskGastroPan == null) && chefDeskGastroPan.IngredientCount > 0 && !(chefDeskGastroPan.CurrentIngredientID.ToString() != ingredientId))
				{
					num += chefDeskGastroPan.IngredientCount;
				}
			}
			return num;
		}

		private void DebugHoverHit(RaycastHit hit)
		{
			Collider collider = hit.collider;
			if (!(collider == _lastHoveredCollider))
			{
				_lastHoveredCollider = collider;
				string text = ((collider != null) ? collider.name : "NULL");
				string text2 = ((collider != null) ? collider.gameObject.name : "NULL");
				ChefDeskBook chefDeskBook = ((collider != null) ? collider.GetComponentInParent<ChefDeskBook>() : null);
				Seasoning seasoning = ((collider != null) ? collider.GetComponentInParent<Seasoning>() : null);
				ServedDishSushi servedDishSushi = ((collider != null) ? collider.GetComponentInParent<ServedDishSushi>() : null);
				string text3 = "[ChefDesk Hover]\nCollider   : " + text + "\nGameObject : " + text2 + "\nBook       : " + ((chefDeskBook != null) ? chefDeskBook.name : "NULL") + "\nSeasoning  : " + ((seasoning != null) ? seasoning.name : "NULL") + "\nDish       : " + ((servedDishSushi != null) ? servedDishSushi.name : "NULL") + "\nLayer      : " + ((collider != null) ? LayerMask.LayerToName(collider.gameObject.layer) : "NULL");
				if (!(_lastHoveredDebugName == text3))
				{
					_lastHoveredDebugName = text3;
					UtilityMethods.Log(text3);
				}
			}
		}

		public void ClientBeginGastropanHoldPlacement(ChefDeskGastroPan gastroPan)
		{
			if (base.IsOwner && !(gastroPan == null) && ClientCanProcessGastropanHoldPlacement(gastroPan))
			{
				_heldPlacementGastroPan = gastroPan;
				_nextGastropanHoldPlaceTime = Time.unscaledTime + _gastropanHoldStartDelay;
			}
		}

		public void ClientEndGastropanHoldPlacement(ChefDeskGastroPan gastroPan)
		{
			if (!(_heldPlacementGastroPan != gastroPan))
			{
				ClientClearGastropanHoldPlacement();
			}
		}

		private void HandleGastropanHoldPlacementUpdate()
		{
			if (!(_heldPlacementGastroPan == null))
			{
				if (!Input.GetMouseButton(0))
				{
					ClientClearGastropanHoldPlacement();
				}
				else if (!ClientCanProcessGastropanHoldPlacement(_heldPlacementGastroPan))
				{
					ClientClearGastropanHoldPlacement();
				}
				else if (!(Time.unscaledTime < _nextGastropanHoldPlaceTime))
				{
					ServerPlaceProductOnTableRpc(_heldPlacementGastroPan.Index);
					_nextGastropanHoldPlaceTime = Time.unscaledTime + _gastropanHoldRepeatInterval;
				}
			}
		}

		private bool ClientCanProcessGastropanHoldPlacement(ChefDeskGastroPan gastroPan)
		{
			if (gastroPan == null)
			{
				return false;
			}
			if (CurrentState == ChefDeskState.Topping)
			{
				return false;
			}
			if (CurrentState == ChefDeskState.TransferToEdge)
			{
				return false;
			}
			return true;
		}

		private void ClientClearGastropanHoldPlacement()
		{
			_heldPlacementGastroPan = null;
			_nextGastropanHoldPlaceTime = 0f;
		}

		private void InitializeCustomSushiNetworkState()
		{
			RegisterCustomSushiUIListeners();
			NetworkVariable<bool> isCustomSushi = _isCustomSushi;
			isCustomSushi.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isCustomSushi.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnCustomModeNetworkChanged));
			NetworkVariable<byte> currentSushiType = _currentSushiType;
			currentSushiType.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(currentSushiType.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnCustomSushiTypeNetworkChanged));
			NetworkVariable<bool> isCustomCompositionLocked = _isCustomCompositionLocked;
			isCustomCompositionLocked.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isCustomCompositionLocked.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnCustomCompositionLockedNetworkChanged));
			_placedIngredientIdsNetwork.OnListChanged += OnCustomCompositionListChanged;
			NetworkVariable<NetworkObjectReference> savableCustomSushi = _savableCustomSushi;
			savableCustomSushi.OnValueChanged = (NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate)Delegate.Combine(savableCustomSushi.OnValueChanged, new NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate(OnSavableCustomSushiChanged));
			_isCustomTypeSelectionOpen = false;
			if (_customRecipeNameInput != null)
			{
				_customRecipeNameInput.characterLimit = 40;
			}
			SetCustomSushiPanelActive(_customTypePanel, isActive: false);
			SetCustomSushiPanelActive(_saveCustomRecipePanel, CanSaveCurrentCustomRecipe);
			SetCustomSushiButtonActive(_customModeButton, isActive: true);
			ClientRefreshCustomSushiUI();
		}

		private void ShutdownCustomSushiNetworkState()
		{
			UnregisterCustomSushiUIListeners();
			NetworkVariable<bool> isCustomSushi = _isCustomSushi;
			isCustomSushi.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isCustomSushi.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnCustomModeNetworkChanged));
			NetworkVariable<byte> currentSushiType = _currentSushiType;
			currentSushiType.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(currentSushiType.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(OnCustomSushiTypeNetworkChanged));
			NetworkVariable<bool> isCustomCompositionLocked = _isCustomCompositionLocked;
			isCustomCompositionLocked.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isCustomCompositionLocked.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnCustomCompositionLockedNetworkChanged));
			_placedIngredientIdsNetwork.OnListChanged -= OnCustomCompositionListChanged;
			NetworkVariable<NetworkObjectReference> savableCustomSushi = _savableCustomSushi;
			savableCustomSushi.OnValueChanged = (NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate)Delegate.Remove(savableCustomSushi.OnValueChanged, new NetworkVariable<NetworkObjectReference>.OnValueChangedDelegate(OnSavableCustomSushiChanged));
		}

		private void OnCustomModeNetworkChanged(bool previousValue, bool newValue)
		{
			if (!newValue)
			{
				_isCustomTypeSelectionOpen = false;
			}
			else if (CurrentSushiType == SushiType.Empty)
			{
				_isCustomTypeSelectionOpen = true;
			}
			ClientRefreshCustomSushiUI();
		}

		private void OnCustomSushiTypeNetworkChanged(byte previousValue, byte newValue)
		{
			ClientRefreshCustomSushiUI();
		}

		private void OnCustomCompositionLockedNetworkChanged(bool previousValue, bool newValue)
		{
			if (newValue)
			{
				_isCustomTypeSelectionOpen = false;
			}
			ClientRefreshCustomSushiUI();
		}

		private void OnCustomCompositionListChanged(NetworkListEvent<FixedString64Bytes> changeEvent)
		{
			if (CanSaveCurrentCustomRecipe)
			{
				_isCustomTypeSelectionOpen = false;
			}
			ClientRefreshCustomSushiUI();
		}

		private void OnSavableCustomSushiChanged(NetworkObjectReference previousValue, NetworkObjectReference newValue)
		{
			ClientRefreshCustomSushiUI();
		}

		private void RegisterCustomSushiUIListeners()
		{
			RegisterCustomSushiButton(_customModeButton, ClientSelectCustomMode);
			RegisterCustomSushiButton(_customMakiButton, ClientSelectCustomMaki);
			RegisterCustomSushiButton(_customUramakiButton, ClientSelectCustomUramaki);
			RegisterCustomSushiButton(_customTemakiButton, ClientSelectCustomTemaki);
			RegisterCustomSushiButton(_customFinishButton, ClientFinishCustomSushi);
			RegisterCustomSushiButton(_saveCustomRecipeButton, ClientSaveCurrentCustomRecipe);
			RegisterCustomSushiButton(_customCancelButton, ClientCancelCustomPreparation);
		}

		private void UnregisterCustomSushiUIListeners()
		{
			UnregisterCustomSushiButton(_customModeButton, ClientSelectCustomMode);
			UnregisterCustomSushiButton(_customMakiButton, ClientSelectCustomMaki);
			UnregisterCustomSushiButton(_customUramakiButton, ClientSelectCustomUramaki);
			UnregisterCustomSushiButton(_customTemakiButton, ClientSelectCustomTemaki);
			UnregisterCustomSushiButton(_customFinishButton, ClientFinishCustomSushi);
			UnregisterCustomSushiButton(_saveCustomRecipeButton, ClientSaveCurrentCustomRecipe);
			UnregisterCustomSushiButton(_customCancelButton, ClientCancelCustomPreparation);
		}

		private static void RegisterCustomSushiButton(BaseButton button, Action listener)
		{
			if (!(button == null))
			{
				button.Clicked -= listener;
				button.Clicked += listener;
			}
		}

		private static void UnregisterCustomSushiButton(BaseButton button, Action listener)
		{
			if (button != null)
			{
				button.Clicked -= listener;
			}
		}

		private void ClientRefreshCustomSushiUI()
		{
			bool canSaveCurrentCustomRecipe = CanSaveCurrentCustomRecipe;
			bool flag = IsCustomSushi || _isCustomTypeSelectionOpen;
			bool flag2 = IsCustomSushi && _placedIngredientIdsNetwork.Count > 0;
			SetCustomSushiPanelActive(_customTypePanel, _isCustomTypeSelectionOpen && !canSaveCurrentCustomRecipe);
			SetCustomSushiPanelActive(_saveCustomRecipePanel, canSaveCurrentCustomRecipe);
			SetCustomSushiButtonActive(_customModeButton, !canSaveCurrentCustomRecipe);
			RefreshCustomTypeButton(_customMakiButton, SushiType.Maki, flag2);
			RefreshCustomTypeButton(_customUramakiButton, SushiType.Uramaki, flag2);
			RefreshCustomTypeButton(_customTemakiButton, SushiType.Temaki, flag2);
			SetCustomSushiButtonActive(_customFinishButton, flag2);
			if (_customModeButton != null)
			{
				RectTransform rectTransform = _customModeButton.transform.parent as RectTransform;
				if (rectTransform != null)
				{
					SetCustomSushiPanelActive(rectTransform.gameObject, !canSaveCurrentCustomRecipe);
					rectTransform.anchoredPosition = new Vector2(flag ? (-327f) : 474f, 282f);
					rectTransform.sizeDelta = new Vector2(flag ? 325f : 359f, 98f);
					Transform transform = rectTransform.Find("SelectedBackdrop");
					if (transform != null)
					{
						SetCustomSushiPanelActive(transform.gameObject, flag);
					}
				}
				Transform transform2 = _customModeButton.transform.Find("SelectedMark");
				if (transform2 != null)
				{
					SetCustomSushiPanelActive(transform2.gameObject, flag);
				}
			}
			_customModeButton?.SetSelected(flag);
			_customMakiButton?.SetSelected(IsCustomSushi && CurrentSushiType == SushiType.Maki);
			_customUramakiButton?.SetSelected(IsCustomSushi && CurrentSushiType == SushiType.Uramaki);
			_customTemakiButton?.SetSelected(IsCustomSushi && CurrentSushiType == SushiType.Temaki);
			if (_customFinishButton != null)
			{
				_customFinishButton.interactable = CanFinishCustomSushi;
			}
			if (_saveCustomRecipeButton != null)
			{
				_saveCustomRecipeButton.interactable = canSaveCurrentCustomRecipe;
			}
			ClientRefreshCustomGuidance(flag, canSaveCurrentCustomRecipe);
		}

		private void RefreshCustomTypeButton(BaseButton button, SushiType type, bool preparationStarted)
		{
			if (!(button == null))
			{
				bool flag = preparationStarted && CurrentSushiType == type;
				SetCustomSushiButtonActive(button, !preparationStarted | flag);
				button.interactable = !preparationStarted;
				if (button.transform is RectTransform rectTransform)
				{
					float num = type switch
					{
						SushiType.Maki => -278f, 
						SushiType.Uramaki => -15f, 
						_ => 257f, 
					};
					rectTransform.anchoredPosition = new Vector2(flag ? (-278f) : num, 0f);
				}
				Transform transform = button.transform.Find("TypeBadgeIcon");
				if (transform != null)
				{
					SetCustomSushiPanelActive(transform.gameObject, flag);
				}
			}
		}

		private static void SetCustomSushiPanelActive(GameObject panel, bool isActive)
		{
			if (panel != null && panel.activeSelf != isActive)
			{
				panel.SetActive(isActive);
			}
		}

		private static void SetCustomSushiButtonActive(BaseButton button, bool isActive)
		{
			if (button != null)
			{
				SetCustomSushiPanelActive(button.gameObject, isActive);
			}
		}

		private void ClientCloseCustomSushiPanels()
		{
			_isCustomTypeSelectionOpen = false;
			SetCustomSushiPanelActive(_customTypePanel, isActive: false);
			SetCustomSushiPanelActive(_saveCustomRecipePanel, isActive: false);
		}

		public void ClientSelectCustomMode()
		{
			if (!base.IsOwner || !InUse.Value)
			{
				return;
			}
			if (IsCustomSushi || _isCustomTypeSelectionOpen)
			{
				if (!HasRecipeContentOnTable)
				{
					ChefDeskState currentState = CurrentState;
					if (currentState == ChefDeskState.Idle || currentState == ChefDeskState.RecipeSelected)
					{
						ClientCloseCustomSushiPanels();
						_customModeButton?.SetSelected(selected: false);
						_recipeInfoCanvas?.Hide();
						ClientUpdateInstructionText(ChefDeskInfoTextId.SelectRecipe);
						ServerSelectRecipeModeRpc();
						return;
					}
				}
				ServerChefDeskResetRpc();
			}
			else if (!ClientCanRequestDeskModeChange())
			{
				ServerSelectCustomModeRpc();
				ClientRefreshCustomSushiUI();
			}
			else
			{
				ServerSelectCustomModeRpc();
				_chefDeskBookCanvas?.ToggleBookUI(isVisible: false);
				SetCustomSushiPanelActive(_saveCustomRecipePanel, isActive: false);
				_isCustomTypeSelectionOpen = true;
				ClientRefreshCustomSushiUI();
			}
		}

		public void ClientSelectCustomMaki()
		{
			ClientSelectCustomSushiType(SushiType.Maki);
		}

		public void ClientSelectCustomUramaki()
		{
			ClientSelectCustomSushiType(SushiType.Uramaki);
		}

		public void ClientSelectCustomTemaki()
		{
			ClientSelectCustomSushiType(SushiType.Temaki);
		}

		public void ClientSelectCustomSushiType(SushiType sushiType)
		{
			if (base.IsOwner && InUse.Value && CustomSushiRules.IsSupportedType(sushiType))
			{
				_isCustomTypeSelectionOpen = true;
				SetCustomSushiPanelActive(_saveCustomRecipePanel, isActive: false);
				ServerSelectCustomSushiTypeRpc((byte)sushiType);
				ClientRefreshCustomSushiUI();
			}
		}

		public void ClientFinishCustomSushi()
		{
			if (base.IsOwner && InUse.Value && CanFinishCustomSushi)
			{
				_isCustomTypeSelectionOpen = false;
				SetCustomSushiPanelActive(_customTypePanel, isActive: false);
				ServerFinishCustomSushiRpc();
			}
		}

		public void ClientSaveCurrentCustomRecipe()
		{
			ClientSaveCurrentCustomRecipe((_customRecipeNameInput != null) ? _customRecipeNameInput.text : string.Empty);
		}

		public void ClientSaveCurrentCustomRecipe(string displayName)
		{
			string text = displayName?.Trim() ?? string.Empty;
			if (base.IsOwner && InUse.Value)
			{
				if (Encoding.UTF8.GetByteCount(text) > FixedString128Bytes.UTF8MaxLengthInBytes)
				{
					Singleton<NotifyManager>.Instance?.ShowToast("notification.chef.custom_recipe_save_failed");
					return;
				}
				byte[] array = (TryGetSavableCustomSushi(out var servedDish) ? ClientCaptureCustomSushiThumbnail(servedDish) : null);
				ServerSaveCurrentCustomRecipeRpc(text, array ?? Array.Empty<byte>());
			}
		}

		private static byte[] ClientCaptureCustomSushiThumbnail(ServedDishSushi servedDish)
		{
			if (servedDish == null || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
			{
				return null;
			}
			PlateGraphicHandler componentInChildren = servedDish.GetComponentInChildren<PlateGraphicHandler>();
			if (componentInChildren == null)
			{
				return null;
			}
			GameObject gameObject = null;
			GameObject gameObject2 = null;
			GameObject gameObject3 = null;
			RenderTexture renderTexture = null;
			Texture2D texture2D = null;
			RenderTexture active = RenderTexture.active;
			try
			{
				gameObject = UnityEngine.Object.Instantiate(componentInChildren.gameObject, componentInChildren.transform.position, componentInChildren.transform.rotation);
				gameObject.hideFlags = HideFlags.HideAndDontSave;
				gameObject.transform.localScale = componentInChildren.transform.lossyScale;
				Transform[] componentsInChildren = gameObject.GetComponentsInChildren<Transform>(includeInactive: true);
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					componentsInChildren[i].gameObject.layer = 31;
				}
				Collider[] componentsInChildren2 = gameObject.GetComponentsInChildren<Collider>(includeInactive: true);
				for (int i = 0; i < componentsInChildren2.Length; i++)
				{
					componentsInChildren2[i].enabled = false;
				}
				Bounds bounds = default;
				bool flag = false;
				bool flag2 = false;
				MeshRenderer[] componentsInChildren3 = gameObject.GetComponentsInChildren<MeshRenderer>();
				foreach (MeshRenderer meshRenderer in componentsInChildren3)
				{
					MeshFilter component = meshRenderer.GetComponent<MeshFilter>();
					if (meshRenderer.enabled && !(component == null) && !(component.sharedMesh == null))
					{
						flag2 |= IsSushiMeshForThumbnail(meshRenderer);
						if (!flag)
						{
							bounds = meshRenderer.bounds;
							flag = true;
						}
						else
						{
							bounds.Encapsulate(meshRenderer.bounds);
						}
					}
				}
				if (!flag || !flag2)
				{
					Debug.LogWarning("[ChefDesk Custom] Thumbnail graphic has no visible plated sushi mesh.");
					return null;
				}
				gameObject2 = new GameObject("Custom Sushi Thumbnail Camera");
				gameObject2.hideFlags = HideFlags.HideAndDontSave;
				Camera camera = gameObject2.AddComponent<Camera>();
				camera.enabled = false;
				camera.orthographic = true;
				camera.orthographicSize = Mathf.Max(0.12f, bounds.extents.magnitude * 0.72f);
				camera.clearFlags = CameraClearFlags.Color;
				camera.backgroundColor = new Color(0.97f, 0.95f, 0.9f, 1f);
				camera.cullingMask = int.MinValue;
				camera.nearClipPlane = 0.01f;
				Vector3 normalized = servedDish.transform.TransformDirection(new Vector3(0.55f, 1.55f, -0.55f)).normalized;
				float num = Mathf.Max(1f, bounds.extents.magnitude * 4f);
				camera.farClipPlane = num * 3f;
				camera.transform.position = bounds.center + normalized * num;
				camera.transform.LookAt(bounds.center);
				gameObject2.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
				gameObject3 = new GameObject("Custom Sushi Thumbnail Light")
				{
					hideFlags = HideFlags.HideAndDontSave
				};
				Light light = gameObject3.AddComponent<Light>();
				light.type = LightType.Directional;
				light.intensity = 1.2f;
				light.cullingMask = int.MinValue;
				light.shadows = LightShadows.None;
				gameObject3.transform.rotation = Quaternion.LookRotation(-normalized);
				renderTexture = (camera.targetTexture = RenderTexture.GetTemporary(192, 192, 24, RenderTextureFormat.ARGB32));
				UniversalRenderPipeline.SingleCameraRequest data = new UniversalRenderPipeline.SingleCameraRequest
				{
					destination = renderTexture
				};
				RenderPipeline.SubmitRenderRequest(camera, data);
				RenderTexture.active = renderTexture;
				texture2D = new Texture2D(192, 192, TextureFormat.RGB24, mipChain: false);
				texture2D.ReadPixels(new Rect(0f, 0f, 192f, 192f), 0, 0);
				texture2D.Apply();
				byte[] array = texture2D.EncodeToJPG(72);
				if (array.Length > 32768)
				{
					array = texture2D.EncodeToJPG(50);
				}
				return CustomSushiThumbnailStore.IsValidJpeg(array) ? array : null;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[ChefDesk Custom] Thumbnail capture failed: " + ex.Message);
				return null;
			}
			finally
			{
				RenderTexture.active = active;
				if (gameObject != null)
				{
					gameObject.SetActive(value: false);
					UnityEngine.Object.Destroy(gameObject);
				}
				if (texture2D != null)
				{
					UnityEngine.Object.Destroy(texture2D);
				}
				if (renderTexture != null)
				{
					RenderTexture.ReleaseTemporary(renderTexture);
				}
				if (gameObject2 != null)
				{
					UnityEngine.Object.Destroy(gameObject2);
				}
				if (gameObject3 != null)
				{
					UnityEngine.Object.Destroy(gameObject3);
				}
			}
		}

		private static bool IsSushiMeshForThumbnail(MeshRenderer renderer)
		{
			if (renderer == null || !renderer.enabled || !renderer.TryGetComponent<MeshFilter>(out var component) || component.sharedMesh == null)
			{
				return false;
			}
			if (!(renderer.name == "__PlatedDishCombinedVisual"))
			{
				return renderer.GetComponentInParent<CustomSushiIngredientVisuals>() != null;
			}
			return true;
		}

		public void ClientCancelCustomPreparation()
		{
			if (base.IsOwner && InUse.Value)
			{
				if (IsCustomSushi)
				{
					ServerChefDeskResetRpc();
				}
				else
				{
					ClientCloseCustomSushiPanels();
				}
			}
		}

		private void ClientResetCustomSushiUI()
		{
			ClientCloseCustomSushiPanels();
			if (_customFinishButton != null)
			{
				_customFinishButton.interactable = false;
			}
			if (_saveCustomRecipeButton != null)
			{
				_saveCustomRecipeButton.interactable = false;
			}
		}

		private bool ClientCanRequestDeskModeChange()
		{
			if (!HasRecipeContentOnTable)
			{
				ChefDeskState currentState = CurrentState;
				return currentState == ChefDeskState.Idle || currentState == ChefDeskState.RecipeSelected;
			}
			return false;
		}

		private void ClientRefreshCustomGuidance(bool customModeActive, bool canSave)
		{
			if (!base.IsOwner || !InUse.Value)
			{
				return;
			}
			if (!customModeActive)
			{
				_recipeInfoCanvas?.Hide();
				return;
			}
			ClientUpdateInstructionText(GetCustomInstructionText(canSave));
			if (IsCustomSushi && CustomSushiRules.IsSupportedType(CurrentSushiType) && !canSave && !IsCustomCompositionLocked)
			{
				ChefDeskState currentState = CurrentState;
				if (currentState == ChefDeskState.Idle || currentState == ChefDeskState.RecipeSelected)
				{
					_recipeInfoCanvas.ClientPrepareCustom(CurrentSushiType, BuildPlacedIngredientIdList(), SingleNoriSO_ID, CookedRiceSO_ID, _itemDatabaseSO, 20);
					_recipeInfoCanvas.Show();
					return;
				}
			}
			_recipeInfoCanvas?.Hide();
		}

		private string GetCustomInstructionText(bool canSave)
		{
			if (canSave)
			{
				return LocalizationDatabase.GetStringOrFallback("UiTable", "chef_desk.custom.save_recipe", "Save custom recipe");
			}
			if (!CustomSushiRules.IsSupportedType(CurrentSushiType))
			{
				return LocalizationDatabase.GetStringOrFallback("UiTable", "chef_desk.custom.select_type", "Select Type");
			}
			if (CurrentState == ChefDeskState.TransferToEdge)
			{
				return GetText(ChefDeskInfoTextId.MovePlateOrSeason);
			}
			if (IsCustomCompositionLocked || CurrentState == ChefDeskState.WaitingForRoll)
			{
				return GetText(ChefDeskInfoTextId.RollSushi);
			}
			int count = _placedIngredientIdsNetwork.Count;
			if (count < 2 && CustomSushiRules.TryGetRequiredBase(CurrentSushiType, SingleNoriSO_ID, CookedRiceSO_ID, out var firstIngredientId, out var secondIngredientId))
			{
				string text = ((count == 0) ? firstIngredientId : secondIngredientId);
				string text2 = _itemDatabaseSO?.FindById(text)?.LocalizedName;
				if (string.IsNullOrWhiteSpace(text2))
				{
					text2 = text;
				}
				return FormatCustomInstruction(LocalizationDatabase.GetStringOrFallback("UiTable", "chef_desk.custom.add_ingredient", "Add {0}"), text2);
			}
			int num = Mathf.Clamp(count - 2, 0, 20);
			if (num >= 20)
			{
				return LocalizationDatabase.GetStringOrFallback("UiTable", "chef_desk.custom.finish_sushi", "Finish sushi");
			}
			return FormatCustomInstruction(LocalizationDatabase.GetStringOrFallback("UiTable", "chef_desk.custom.add_filling_or_finish", "Add filling ({0}/{1}) or finish sushi"), num, 20);
		}

		private static string FormatCustomInstruction(string format, params object[] arguments)
		{
			try
			{
				return string.Format(format, arguments);
			}
			catch (FormatException)
			{
				return format;
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerSelectRecipeModeRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1443386404u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1443386404u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (ServerCanChangeChefDeskMode(rpcParams.Receive.SenderClientId))
				{
					_isCustomSushi.Value = false;
					_isCustomCompositionLocked.Value = false;
					_resolvedCustomRecipeKind.Value = 2;
					_currentSushiType.Value = 0;
					_selectedSushiSO_ID.Value = default;
					_savableCustomSushi.Value = default;
					_isRecipeCompleted.Value = true;
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerSelectCustomModeRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(511688556u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 511688556u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (ServerCanChangeChefDeskMode(rpcParams.Receive.SenderClientId))
				{
					_isCustomSushi.Value = true;
					_isCustomCompositionLocked.Value = false;
					_resolvedCustomRecipeKind.Value = 2;
					_currentSushiType.Value = 0;
					_selectedSushiSO_ID.Value = default;
					_currentRecipeStepIndex.Value = 0;
					_currentIngredientPlacedCount = 0;
					_currentIngredientPlacedCountNetwork.Value = 0;
					_isRecipeCompleted.Value = true;
					ClientSetCustomRecipePanelVisibilityRpc(isVisible: false);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerSelectCustomSushiTypeRpc(byte sushiTypeValue, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(477253040u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in sushiTypeValue, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 477253040u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			SushiType sushiType = (SushiType)sushiTypeValue;
			if (!CustomSushiRules.IsSupportedType(sushiType))
			{
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!ServerIsActiveChefDeskUser(senderClientId))
			{
				return;
			}
			if ((_placedIngredientIdsNetwork.Count <= 0 || (IsCustomSushi && CurrentSushiType == sushiType)) && !_centerOccupied.Value)
			{
				ChefDeskState currentState = CurrentState;
				if (currentState == ChefDeskState.Idle || currentState == ChefDeskState.RecipeSelected)
				{
					_isCustomSushi.Value = true;
					_isCustomCompositionLocked.Value = false;
					_resolvedCustomRecipeKind.Value = 2;
					_currentSushiType.Value = sushiTypeValue;
					_selectedSushiSO_ID.Value = default;
					_currentRecipeStepIndex.Value = (byte)_placedIngredientIdsNetwork.Count;
					_currentIngredientPlacedCount = 0;
					_currentIngredientPlacedCountNetwork.Value = 0;
					_isRecipeCompleted.Value = false;
					_sushiRollController?.ServerPrepareRollSession(sushiType);
					ClientPrepareAssemblyLayoutRpc(sushiTypeValue, 20);
					ClientSetCustomRecipePanelVisibilityRpc(isVisible: true);
					return;
				}
			}
			ClientShowNotifyRpc("Info", "notification.chef.reset_before_changing_mode");
		}

		private bool ServerCanChangeChefDeskMode(ulong senderClientId)
		{
			if (!ServerIsActiveChefDeskUser(senderClientId))
			{
				return false;
			}
			if (_placedIngredientIdsNetwork.Count <= 0 && !_centerOccupied.Value)
			{
				ChefDeskState currentState = CurrentState;
				if (currentState == ChefDeskState.Idle || currentState == ChefDeskState.RecipeSelected)
				{
					return true;
				}
			}
			ClientShowNotifyRpc("Info", "notification.chef.reset_before_changing_mode");
			return false;
		}

		[Rpc(SendTo.Owner)]
		private void ClientSetCustomRecipePanelVisibilityRpc(bool isVisible)
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
				FastBufferWriter bufferWriter = __beginSendRpc(538201732u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in isVisible, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 538201732u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (isVisible)
				{
					_recipeInfoCanvas.Show();
				}
				else
				{
					_recipeInfoCanvas.Hide();
				}
			}
		}

		private bool ServerTryPlaceCustomIngredient(ChefDeskGastroPan gastroPan)
		{
			if (!base.IsServer || gastroPan == null)
			{
				return false;
			}
			ChefDeskState currentState = CurrentState;
			if (currentState != ChefDeskState.Idle && currentState != ChefDeskState.RecipeSelected)
			{
				return false;
			}
			string text = gastroPan.CurrentIngredientID.ToString();
			if (!(_itemDatabaseSO.FindById(text) is ProcessedIngredientSO processedIngredientSO))
			{
				ClientShowNotifyRpc("Info", "notification.chef.invalid_custom_ingredient");
				return false;
			}
			CustomSushiPlacementRejection customSushiPlacementRejection = CustomSushiRules.ValidateNextPlacement(CurrentSushiType, BuildPlacedIngredientIdList(), text, SingleNoriSO_ID, CookedRiceSO_ID, IsCustomCompositionLocked);
			if (customSushiPlacementRejection != CustomSushiPlacementRejection.None)
			{
				ClientShowNotifyRpc("Info", ResolveCustomPlacementMessage(customSushiPlacementRejection));
				return false;
			}
			if (!CustomSushiRules.IsSupportedProcessedIngredient(text, processedIngredientSO.ProcessedType, SingleNoriSO_ID, CookedRiceSO_ID))
			{
				ClientShowNotifyRpc("Info", "notification.chef.invalid_custom_ingredient");
				return false;
			}
			if (!gastroPan.ServerTryConsumeForPlacement())
			{
				ClientShowNotifyRpc("Info", "notification.chef.gastropan_empty");
				return false;
			}
			_placedIngredientIdsNetwork.Add(text);
			_currentRecipeStepIndex.Value = (byte)_placedIngredientIdsNetwork.Count;
			ClientIngredientPlacedRpc(text);
			if (CurrentState == ChefDeskState.Idle)
			{
				_currentState.Value = 1;
			}
			return true;
		}

		[Rpc(SendTo.Server)]
		private void ServerFinishCustomSushiRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3837474383u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3837474383u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!ServerIsActiveChefDeskUser(rpcParams.Receive.SenderClientId) || !IsCustomSushi || IsCustomCompositionLocked || _centerOccupied.Value)
			{
				return;
			}
			ChefDeskState currentState = CurrentState;
			if (currentState != ChefDeskState.Idle && currentState != ChefDeskState.RecipeSelected)
			{
				return;
			}
			SushiCompositionData sushiCompositionData = BuildCurrentCustomComposition();
			if (!CustomSushiRules.HasValidBase(sushiCompositionData.SushiType, sushiCompositionData.IngredientIds, SingleNoriSO_ID, CookedRiceSO_ID))
			{
				ClientShowNotifyRpc("Info", "notification.chef.custom_base_incomplete");
				return;
			}
			CustomSushiRecipeCatalog customCatalog = Singleton<GameProgressManager>.Instance?.RestaurantManager?.CustomSushiRecipeCatalog;
			SushiRecipeDefinition sushiRecipeDefinition = new SushiRecipeResolver(_itemDatabaseSO, customCatalog).ResolveExact(sushiCompositionData);
			if (sushiRecipeDefinition != null)
			{
				_selectedSushiSO_ID.Value = sushiRecipeDefinition.RecipeId;
				_resolvedCustomRecipeKind.Value = (byte)sushiRecipeDefinition.IdentityKind;
			}
			else
			{
				_selectedSushiSO_ID.Value = default;
				_resolvedCustomRecipeKind.Value = 2;
			}
			_isCustomCompositionLocked.Value = true;
			_isRecipeCompleted.Value = true;
			ClientHandleCustomCompositionFinishedRpc();
		}

		[Rpc(SendTo.Owner)]
		private void ClientHandleCustomCompositionFinishedRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3684510283u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3684510283u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientHandleRecipeCompleted();
			}
		}

		private SushiCompositionData BuildCurrentCustomComposition()
		{
			return new SushiCompositionData
			{
				SushiType = CurrentSushiType,
				IngredientIds = BuildPlacedIngredientIdList()
			};
		}

		public NetworkObject GetCustomPhysicalPrefab(SushiType sushiType)
		{
			return sushiType switch
			{
				SushiType.Maki => _customMakiPrefab, 
				SushiType.Uramaki => _customUramakiPrefab, 
				SushiType.Temaki => _customTemakiPrefab, 
				_ => null, 
			};
		}

		private GameObject ResolveCustomCutPiecePrefab()
		{
			NetworkObject customPhysicalPrefab = GetCustomPhysicalPrefab(CurrentSushiType);
			if (customPhysicalPrefab == null || !customPhysicalPrefab.TryGetComponent<ServedDishSushi>(out var component))
			{
				return null;
			}
			if (component.CustomCutPiecePrefab != null)
			{
				return component.CustomCutPiecePrefab;
			}
			if (!(component.ItemSO is SushiSO sushiSO))
			{
				return null;
			}
			return sushiSO.SingleSushiPrefab;
		}

		private GameObject ResolveCurrentSingleSushiPrefab()
		{
			if (CurrentResolvedRecipeKind == SushiRecipeIdentityKind.BuiltIn)
			{
				return GetSelectedSushi()?.SingleSushiPrefab;
			}
			return ResolveCustomCutPiecePrefab();
		}

		private int ServerCalculateCustomSushiPrice(SushiCompositionData composition)
		{
			if (!base.IsServer || composition?.IngredientIds == null)
			{
				return 0;
			}
			int? exactRecipePrice = new SushiRecipeResolver(_itemDatabaseSO, Singleton<GameProgressManager>.Instance?.RestaurantManager?.CustomSushiRecipeCatalog).ResolveExact(composition)?.Price;
			if (!exactRecipePrice.HasValue && CustomSushiPricing.TryGetEquivalentBuiltInPrice(composition, _itemDatabaseSO.GetAllOfType(ItemType.ServedDish).OfType<SushiSO>(), out var price))
			{
				exactRecipePrice = price;
			}
			return CustomSushiPricing.Calculate(composition, SingleNoriSO_ID, CookedRiceSO_ID, (string ingredientId) => _itemDatabaseSO.FindById(ingredientId) as ProcessedIngredientSO, exactRecipePrice);
		}

		private bool TryGetSavableCustomSushi(out ServedDishSushi servedDish)
		{
			servedDish = null;
			if (_savableCustomSushi.Value.TryGet(out var networkObject) && networkObject != null && networkObject.TryGetComponent<ServedDishSushi>(out servedDish))
			{
				return servedDish.IsUnsavedCustomSushi;
			}
			return false;
		}

		private void ServerSetSavableCustomSushi(ServedDishSushi servedDish)
		{
			if (base.IsServer)
			{
				_savableCustomSushi.Value = ((servedDish != null) ? new NetworkObjectReference(servedDish.NetworkObject) : default(NetworkObjectReference));
			}
		}

		private void ServerClearSavableCustomSushiIfMatches(NetworkObject networkObject)
		{
			if (base.IsServer && !(networkObject == null) && _savableCustomSushi.Value.TryGet(out var networkObject2) && !(networkObject2 != networkObject))
			{
				_savableCustomSushi.Value = default;
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerSaveCurrentCustomRecipeRpc(FixedString128Bytes displayName, byte[] thumbnailJpeg, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(798157445u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in displayName, default(FastBufferWriter.ForFixedStrings));
				bool value = thumbnailJpeg != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(thumbnailJpeg, default(FastBufferWriter.ForPrimitives));
				}
				__endSendRpc(ref bufferWriter, 798157445u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!ServerIsActiveChefDeskUser(rpcParams.Receive.SenderClientId) || !TryGetSavableCustomSushi(out var servedDish))
			{
				return;
			}
			RestaurantManager restaurantManager = Singleton<GameProgressManager>.Instance?.RestaurantManager;
			if (restaurantManager == null)
			{
				return;
			}
			SushiCompositionData customComposition = servedDish.CustomComposition;
			if (restaurantManager.ServerTryCreateCustomSushiRecipe(displayName.ToString(), customComposition, servedDish.CustomPrice, thumbnailJpeg, out var recipe, out var existingRecipe))
			{
				servedDish.ServerAdoptSavedCustomRecipe(recipe);
				_savableCustomSushi.Value = default;
				ClientHandleCustomRecipeSavedRpc();
				ClientShowNotifyRpc("Info", "notification.chef.custom_recipe_saved");
			}
			else if (existingRecipe != null && SushiCompositionComparer.Equals(customComposition, existingRecipe.Composition))
			{
				if (existingRecipe.IdentityKind == SushiRecipeIdentityKind.SavedCustom && CustomSushiThumbnailStore.NeedsPhotoRepair(restaurantManager.CustomSushiRecipeCatalog.FindById(existingRecipe.RecipeId)) && CustomSushiThumbnailStore.IsValidJpeg(thumbnailJpeg))
				{
					restaurantManager.ServerTryAttachCustomSushiThumbnail(existingRecipe.RecipeId, thumbnailJpeg);
				}
				servedDish.ServerAdoptRecipeIdentity(existingRecipe);
				_savableCustomSushi.Value = default;
				ClientHandleCustomRecipeSavedRpc();
				ClientShowNotifyRpc("Info", "notification.chef.custom_recipe_already_exists");
			}
			else
			{
				ClientShowNotifyRpc("Info", "notification.chef.custom_recipe_save_failed");
			}
		}

		[Rpc(SendTo.Owner)]
		private void ClientHandleCustomRecipeSavedRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(563892524u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 563892524u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (_customRecipeNameInput != null)
				{
					_customRecipeNameInput.text = string.Empty;
				}
				ClientCloseCustomSushiPanels();
			}
		}

		[Rpc(SendTo.Owner)]
		private void ClientHandleCustomSushiSpawnedRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(4125091314u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 4125091314u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientCloseCustomSushiPanels();
				_recipeInfoCanvas?.Hide();
				if (_customRecipeNameInput != null)
				{
					_customRecipeNameInput.text = string.Empty;
				}
				ClientRefreshCustomSushiUI();
			}
		}

		[Rpc(SendTo.Owner)]
		private void ClientReadyForNextCustomSushiRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(2352889263u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2352889263u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_isCustomTypeSelectionOpen = true;
				ClientRefreshCustomSushiUI();
			}
		}

		private void ServerPrepareCustomModeForNextSushi()
		{
			if (base.IsServer && IsCustomSushi)
			{
				_savableCustomSushi.Value = default;
				_currentSushiType.Value = 0;
				_selectedSushiSO_ID.Value = default;
				_resolvedCustomRecipeKind.Value = 2;
				_isCustomCompositionLocked.Value = false;
				_isRecipeCompleted.Value = false;
				_currentRecipeStepIndex.Value = 0;
				_currentIngredientPlacedCount = 0;
				_currentIngredientPlacedCountNetwork.Value = 0;
				_sushiRollController?.ServerPrepareRollSession(SushiType.Empty);
				ClientPrepareAssemblyLayoutRpc(0, 0);
				ClientSetCustomRecipePanelVisibilityRpc(isVisible: false);
			}
		}

		private List<string> BuildPlacedIngredientIdList()
		{
			List<string> list = new List<string>(_placedIngredientIdsNetwork.Count);
			for (int i = 0; i < _placedIngredientIdsNetwork.Count; i++)
			{
				list.Add(_placedIngredientIdsNetwork[i].ToString());
			}
			return list;
		}

		private static string ResolveCustomPlacementMessage(CustomSushiPlacementRejection rejection)
		{
			return rejection switch
			{
				CustomSushiPlacementRejection.WrongBaseIngredient => "notification.chef.wrong_custom_base_ingredient", 
				CustomSushiPlacementRejection.BaseIngredientCannotRepeat => "notification.chef.custom_base_cannot_repeat", 
				CustomSushiPlacementRejection.FillingLimitReached => "notification.chef.custom_filling_limit_reached", 
				CustomSushiPlacementRejection.CompositionLocked => "notification.chef.custom_composition_locked", 
				CustomSushiPlacementRejection.UnsupportedSushiType => "notification.chef.custom_type_required", 
				_ => "notification.chef.invalid_custom_ingredient", 
			};
		}

		private void InitializeRollMeshCutController()
		{
			if (_rollMeshCutController == null)
			{
				_rollMeshCutController = GetComponent<ChefDeskRollMeshCutController>();
			}
			if (_rollMeshCutController == null)
			{
				_rollMeshCutController = base.gameObject.AddComponent<ChefDeskRollMeshCutController>();
			}
			_rollMeshCutController.Initialize();
		}

		private void ClientResetRollMeshCutSession()
		{
			_pendingCuttingStartAfterCentering = false;
			_rollMeshCutController?.CancelSession();
		}

		private void KillCutFlowDelay()
		{
			if (_cutFlowDelayTween != null && _cutFlowDelayTween.IsActive())
			{
				_cutFlowDelayTween.Kill();
			}
			_cutFlowDelayTween = null;
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

		public void StartCutting()
		{
			if (base.IsOwner && !_localCuttingActive)
			{
				if (_isCenteringAssembly)
				{
					_pendingCuttingStartAfterCentering = true;
					UtilityMethods.Log("[ChefDesk] Cutting start deferred until assembly centering completes.");
				}
				else if (CanStartCuttingLocally())
				{
					_pendingCuttingStartAfterCentering = false;
					ClientSetupCuttingUI();
				}
			}
		}

		private void ClientSetupCuttingUI()
		{
			KillCutFlowDelay();
			_localCuttingActive = true;
			ClientClearTemporaryCutPieces();
			_requiredCutCount = UnityEngine.Random.Range(3, 6);
			_successfulCutCount = 0;
			bool rollMeshCutSessionStarted = ClientTryStartRollMeshCutSession(_requiredCutCount);
			ServerBeginTemporaryCutPieceSessionRpc(_requiredCutCount, rollMeshCutSessionStarted);
			_isKnifeHeld = false;
			_mustExitCutPoint = false;
			_hasLastKnifeDirection = false;
			_lastKnifeDirection = Vector3.zero;
			_currentTiltAngleY = 0f;
			if (_knifePressRoot != null)
			{
				_knifePressRoot.localEulerAngles = Vector3.zero;
			}
			_cuttingManager.SetCuttingViewActive(isActive: true);
			if (_knifeModel != null)
			{
				Singleton<DelayHelper>.Instance.DelayAction(_cuttingManager.CameraBlendTime, () =>
				{
					if (IsCuttingActiveLocally)
					{
						_knifeModel.gameObject.SetActive(value: true);
					}
				});
			}
			RefreshCursorForChefDeskUseMode();
			StartCutProgressUI();
			ClientGenerateNextCutPoint();
			_recipeInfoCanvas.gameObject.SetActive(value: false);
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

		private void ClientGenerateNextCutPoint()
		{
			if (!IsCuttingActiveLocally)
			{
				return;
			}
			SushiVisualSet value;
			if (_rollMeshCutController != null && _rollMeshCutController.IsSessionActive)
			{
				if (!_rollMeshCutController.TryGetNextCutPoint(out var cutPoint))
				{
					UtilityMethods.Log($"[ChefDesk Mesh Cutting] No valid point was found on the remaining roll mesh. Sequence={_successfulCutCount + 1}, SushiType={CurrentSushiType}", LogType.Error);
					return;
				}
				Vector3 displayPosition = cutPoint + GetCutSurfaceNormal() * _rollMeshCutPointHeight;
				float rotationZ = UnityEngine.Random.Range(0f, 360f);
				_cuttingManager.SetCutPoint(cutPoint, displayPosition, rotationZ);
			}
			else if (_visualMap == null)
			{
				UtilityMethods.Log("<color=red>[ChefDesk] Visual map is null, cannot generate cut point.</color>");
			}
			else if (!_visualMap.TryGetValue(CurrentSushiType, out value) || value == null)
			{
				UtilityMethods.Log("<color=red>[ChefDesk] Visual data for current sushi type is missing.</color>");
			}
			else
			{
				GameObject gameObject = ResolveCutTargetObject(value);
				if (gameObject == null)
				{
					UtilityMethods.Log($"<color=red>[ChefDesk Cutting] Cut point target is missing. SushiType={CurrentSushiType}</color>");
				}
				else
				{
					_cuttingManager.GenerateRandomCutPoint(gameObject);
				}
			}
		}

		private bool ClientTryStartRollMeshCutSession(int requiredCutCount)
		{
			SushiType currentSushiType = CurrentSushiType;
			if (currentSushiType != SushiType.Maki && currentSushiType != SushiType.Uramaki)
			{
				return false;
			}
			if (_visualMap == null || !_visualMap.TryGetValue(CurrentSushiType, out var value) || value == null)
			{
				UtilityMethods.Log($"[ChefDesk Mesh Cutting] Session could not start because visual data is missing. SushiType={CurrentSushiType}", LogType.Error);
				return false;
			}
			InitializeRollMeshCutController();
			Transform ingredientRoot = ClientGetAssemblyIngredientRoot();
			GameObject presentationOuterShell = ResolveCutTargetObject(value);
			bool num = _rollMeshCutController.TryStartSession(value.CuttingRollPrefab, value.SingleNoriOBJ, value.SushiRiceOBJ, ingredientRoot, presentationOuterShell, _centerPoint, GetCutSurfaceNormal(), requiredCutCount, out var failureReason);
			if (!num)
			{
				UtilityMethods.Log($"[ChefDesk Mesh Cutting] Session could not start. SushiType={CurrentSushiType}, Reason={failureReason}", LogType.Error);
			}
			return num;
		}

		private GameObject ResolveCutTargetObject(SushiVisualSet visual)
		{
			if (visual == null)
			{
				return null;
			}
			if (CurrentSushiType != SushiType.Uramaki)
			{
				return visual.SingleNoriOBJ;
			}
			return visual.SushiRiceOBJ;
		}

		private void OnKnifeAction(InputActionPhase phase)
		{
			if (!IsCuttingActiveLocally)
			{
				UtilityMethods.Log("[ChefDesk] Knife input ignored: local state is not Cutting.");
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
			bool mustExitCutPoint = false;
			if (_cuttingManager != null && _cuttingManager.CutPointUI != null && _cuttingManager.PlayerCamera != null)
			{
				mustExitCutPoint = IsKnifePassingThroughCutPoint();
			}
			_mustExitCutPoint = mustExitCutPoint;
		}

		private void HandleKnifeReleased()
		{
			_isKnifeHeld = false;
			_mustExitCutPoint = false;
		}

		private bool IsKnifePassingThroughCutPoint()
		{
			if (_cuttingManager == null || !_cuttingManager.IsCutPointInteractable)
			{
				return false;
			}
			if (_cuttingManager.CutPointUI == null || _cuttingManager.PlayerCamera == null)
			{
				return false;
			}
			if (_knifeTip == null || _knifeHeel == null)
			{
				return false;
			}
			Camera playerCamera = _cuttingManager.PlayerCamera;
			RectTransform cutPointUI = _cuttingManager.CutPointUI;
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
			cutPointUI.GetWorldCorners(_cutPointWorldCorners);
			Vector2 a2 = playerCamera.WorldToScreenPoint(_cutPointWorldCorners[0]);
			Vector2 b2 = playerCamera.WorldToScreenPoint(_cutPointWorldCorners[2]);
			float num = Vector2.Distance(a2, b2) * 0.25f;
			return DistancePointToSegment(p, a, b) <= num + _knifeThickness;
		}

		private float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
		{
			Vector2 vector = b - a;
			float sqrMagnitude = vector.sqrMagnitude;
			if (sqrMagnitude < 1E-05f)
			{
				return Vector2.Distance(p, a);
			}
			float value = Vector2.Dot(p - a, vector) / sqrMagnitude;
			value = Mathf.Clamp01(value);
			Vector2 b2 = a + vector * value;
			return Vector2.Distance(p, b2);
		}

		private void UpdateCuttingMouseTracking()
		{
			if (_cuttingManager == null || _cuttingManager.CutPointUI == null || _cuttingManager.PlayerCamera == null)
			{
				return;
			}
			FollowKnifeWithMouse();
			bool flag = IsKnifePassingThroughCutPoint();
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
			_cuttingManager.SetCutPointHoverState(flag);
		}

		private void FollowKnifeWithMouse()
		{
			if (!(_knifeModel == null) && !(_cuttingManager == null) && !(_cuttingManager.PlayerCamera == null))
			{
				Vector3 position = Mouse.current.position.ReadValue();
				position.z = _knifeDistanceFromCamera;
				Vector3 vector = _cuttingManager.PlayerCamera.ScreenToWorldPoint(position);
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
			Camera playerCamera = _cuttingManager.PlayerCamera;
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
			if (_hasLastKnifeDirection)
			{
				if (Vector3.Angle(_lastKnifeDirection, vector) < _knifeMinAngleDelta)
				{
					vector = _lastKnifeDirection;
				}
				else
				{
					float t = 1f - Mathf.Exp((0f - _knifeDirectionSmooth) * Time.deltaTime);
					vector = Vector3.Slerp(_lastKnifeDirection, vector, t).normalized;
				}
			}
			float num = Mathf.Atan2(vector.x, vector.z) * 57.29578f;
			float angle = ((Mouse.current != null && Mouse.current.leftButton.isPressed) ? 0f : 90f);
			Quaternion to = Quaternion.AngleAxis(num - 90f, Vector3.up) * Quaternion.AngleAxis(angle, Vector3.right) * Quaternion.AngleAxis(90f, Vector3.forward);
			_knifeModel.rotation = Quaternion.RotateTowards(_knifeModel.rotation, to, _knifeRotationMaxDegreesPerSecond * Time.deltaTime);
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
			if (!TryGetTemporaryCutPieceSpawnData(out var cutPoint, out var cutDirection))
			{
				return;
			}
			int sequence = _successfulCutCount + 1;
			Vector3 planeNormal = ResolveRollMeshCutPlaneNormal();
			if (_rollMeshCutController == null || !_rollMeshCutController.IsSessionActive)
			{
				UtilityMethods.Log("[ChefDesk] Live cut executed without runtime mesh cutting.");
				ClientOnCutPointSuccessful(sequence, cutPoint, planeNormal, cutDirection, usedRollMeshCutting: false);
			}
			else if (!_rollMeshCutController.IsCutInProgress)
			{
				_cuttingManager.SetCutPointInteractionEnabled(enabled: false);
				_mustExitCutPoint = true;
				if (!_rollMeshCutController.RequestCut(sequence, cutPoint, planeNormal, (bool success) =>
				{
					if (IsCuttingActiveLocally)
					{
						if (!success)
						{
							UtilityMethods.Log($"[ChefDesk Mesh Cutting] Cut transaction failed. Sequence={sequence}, SushiType={CurrentSushiType}", LogType.Error);
							_cuttingManager.SetCutPointInteractionEnabled(enabled: true);
						}
						else
						{
							ClientOnCutPointSuccessful(sequence, cutPoint, planeNormal, cutDirection, usedRollMeshCutting: true);
						}
					}
				}))
				{
					UtilityMethods.Log($"[ChefDesk Mesh Cutting] Cut request was rejected by the local session. Sequence={sequence}", LogType.Error);
					_cuttingManager.SetCutPointInteractionEnabled(enabled: true);
				}
			}
		}

		private Vector3 ResolveRollMeshCutPlaneNormal()
		{
			Vector3 cutSurfaceNormal = GetCutSurfaceNormal();
			Vector3 vector = ((_knifeTip != null && _knifeHeel != null) ? (_knifeTip.position - _knifeHeel.position) : Vector3.zero);
			vector = Vector3.ProjectOnPlane(vector, cutSurfaceNormal);
			Vector3 vector2 = ((vector.sqrMagnitude > 0.0001f) ? Vector3.Cross(cutSurfaceNormal, vector).normalized : ((_rollMeshCutController != null) ? _rollMeshCutController.RollAxis : base.transform.forward));
			if (!(_rollMeshCutController != null) || !_rollMeshCutController.IsSessionActive)
			{
				return vector2;
			}
			return _rollMeshCutController.ConstrainPlaneNormal(vector2, _rollMeshCutMaxPlaneAngle);
		}

		private void ClientOnCutPointSuccessful(int sequence, Vector3 cutPoint, Vector3 planeNormal, Vector3 cutDirection, bool usedRollMeshCutting)
		{
			UtilityMethods.Log("[ChefDesk] Cut successful: passed through UI cut point.");
			_successfulCutCount++;
			if (usedRollMeshCutting)
			{
				ClientSpawnTemporaryCutPiece(cutPoint, cutDirection);
				ServerRequestReplicateRollMeshCutRpc(sequence, cutPoint, planeNormal, cutDirection);
			}
			else
			{
				ClientSpawnAndReplicateTemporaryCutPiece(cutPoint, cutDirection);
			}
			PlayCutParticle(cutPoint, cutDirection);
			ClientPlayCutFeedback();
			float knifeBladeScreenAngleZ = GetKnifeBladeScreenAngleZ();
			float feedbackDuration = ((_cuttingManager != null) ? _cuttingManager.PlayCutPointSuccessFeedback(knifeBladeScreenAngleZ) : 0f);
			_mustExitCutPoint = true;
			if (_successfulCutCount < _requiredCutCount)
			{
				PlayRandomCutSuccessSfx();
				DelayNextCutPoint(feedbackDuration);
			}
			else
			{
				_rollMeshCutController?.HidePresentationIfFinalCut(sequence);
				DelayCuttingCompleted(feedbackDuration);
			}
		}

		private void ClientSpawnAndReplicateTemporaryCutPiece(Vector3 cutPoint, Vector3 cutDirection)
		{
			if (ClientSpawnTemporaryCutPiece(cutPoint, cutDirection))
			{
				ServerRequestReplicateTemporaryCutPieceRpc(cutPoint, cutDirection);
			}
		}

		private bool TryGetTemporaryCutPieceSpawnData(out Vector3 cutPoint, out Vector3 cutDirection)
		{
			cutPoint = default;
			cutDirection = default;
			if (_cuttingManager == null)
			{
				return false;
			}
			cutPoint = _cuttingManager.CutPoint;
			if (!IsFiniteVector(cutPoint))
			{
				return false;
			}
			Vector3 cutSurfaceNormal = GetCutSurfaceNormal();
			Vector3 vector = (_hasLastKnifeDirection ? _lastKnifeDirection : base.transform.forward);
			cutDirection = Vector3.ProjectOnPlane(vector, cutSurfaceNormal);
			if (cutDirection.sqrMagnitude <= 0.0001f)
			{
				Vector3 vector2 = ((_centerPoint != null) ? _centerPoint.right : base.transform.right);
				cutDirection = Vector3.ProjectOnPlane(vector2, cutSurfaceNormal);
			}
			if (cutDirection.sqrMagnitude <= 0.0001f)
			{
				return false;
			}
			cutDirection.Normalize();
			return true;
		}

		[Rpc(SendTo.Server)]
		private void ServerBeginTemporaryCutPieceSessionRpc(int requiredCutCount, bool rollMeshCutSessionStarted, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2176335985u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, requiredCutCount);
				bufferWriter.WriteValueSafe(in rollMeshCutSessionStarted, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2176335985u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (StaffLocked)
			{
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!ServerIsActiveChefDeskUser(senderClientId) || CurrentState != ChefDeskState.Cutting)
			{
				UtilityMethods.Log($"[ChefDesk Cut Piece] Session start rejected. SenderClientId={senderClientId}, ActiveClientId={_serverCurrentUserClientId}, State={CurrentState}", LogType.Warning);
				return;
			}
			if (requiredCutCount <= 0 || requiredCutCount > Mathf.Max(1, _maxCutSlots))
			{
				UtilityMethods.Log($"[ChefDesk Cut Piece] Session start rejected because cut count is invalid. SenderClientId={senderClientId}, RequiredCutCount={requiredCutCount}", LogType.Warning);
				return;
			}
			_serverTemporaryCutPieceSpawnCount = 0;
			_serverRollMeshCutSequence = 0;
			_serverRollMeshCutRequiredCount = requiredCutCount;
			int serverRollMeshCutSessionActive;
			if (rollMeshCutSessionStarted)
			{
				SushiType currentSushiType = CurrentSushiType;
				serverRollMeshCutSessionActive = ((currentSushiType == SushiType.Maki || currentSushiType == SushiType.Uramaki) ? 1 : 0);
			}
			else
			{
				serverRollMeshCutSessionActive = 0;
			}
			_serverRollMeshCutSessionActive = (byte)serverRollMeshCutSessionActive != 0;
			ClientClearTemporaryCutPiecesForObserversRpc();
			if (_serverRollMeshCutSessionActive)
			{
				ClientBeginRollMeshCutSessionForObserversRpc(requiredCutCount);
			}
		}

		[Rpc(SendTo.NotOwner)]
		private void ClientBeginRollMeshCutSessionForObserversRpc(int requiredCutCount)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2377123185u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, requiredCutCount);
				__endSendRpc(ref bufferWriter, 2377123185u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!ClientTryStartRollMeshCutSession(requiredCutCount))
				{
					UtilityMethods.Log($"[ChefDesk Mesh Cutting] Observer session could not start. RequiredCutCount={requiredCutCount}, SushiType={CurrentSushiType}", LogType.Error);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestReplicateTemporaryCutPieceRpc(Vector3 cutPoint, Vector3 cutDirection, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3792718373u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in cutPoint);
				bufferWriter.WriteValueSafe(in cutDirection);
				__endSendRpc(ref bufferWriter, 3792718373u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (StaffLocked)
			{
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!ServerIsActiveChefDeskUser(senderClientId) || CurrentState != ChefDeskState.Cutting)
			{
				UtilityMethods.Log($"[ChefDesk Cut Piece] Spawn rejected. SenderClientId={senderClientId}, ActiveClientId={_serverCurrentUserClientId}, State={CurrentState}", LogType.Warning);
				return;
			}
			SushiType currentSushiType = CurrentSushiType;
			if (currentSushiType != SushiType.Maki && currentSushiType != SushiType.Uramaki)
			{
				return;
			}
			if (_serverRollMeshCutSessionActive)
			{
				UtilityMethods.Log($"[ChefDesk Cut Piece] Legacy spawn request rejected during an active roll mesh cut session. SenderClientId={senderClientId}", LogType.Warning);
			}
			else
			{
				if (!IsFiniteVector(cutPoint) || !IsFiniteVector(cutDirection))
				{
					return;
				}
				Vector3 vector = ((_centerPoint != null) ? _centerPoint.position : base.transform.position);
				if ((cutPoint - vector).sqrMagnitude > 4f)
				{
					UtilityMethods.Log($"[ChefDesk Cut Piece] Spawn point rejected because it is outside the desk range. SenderClientId={senderClientId}, CutPoint={cutPoint}", LogType.Warning);
					return;
				}
				int num = Mathf.Max(1, _maxCutSlots);
				if (_serverTemporaryCutPieceSpawnCount >= num)
				{
					UtilityMethods.Log($"[ChefDesk Cut Piece] Spawn limit reached. SenderClientId={senderClientId}, Limit={num}", LogType.Warning);
					return;
				}
				if (ResolveCurrentSingleSushiPrefab() == null)
				{
					UtilityMethods.Log($"[ChefDesk Cut Piece] Single sushi prefab is missing. SushiId={SelectedSushiID}, SushiType={CurrentSushiType}", LogType.Warning);
					return;
				}
				_serverTemporaryCutPieceSpawnCount++;
				ClientSpawnTemporaryCutPieceForObserversRpc(cutPoint, cutDirection);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestReplicateRollMeshCutRpc(int sequence, Vector3 cutPoint, Vector3 planeNormal, Vector3 cutDirection, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(4156759997u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sequence);
				bufferWriter.WriteValueSafe(in cutPoint);
				bufferWriter.WriteValueSafe(in planeNormal);
				bufferWriter.WriteValueSafe(in cutDirection);
				__endSendRpc(ref bufferWriter, 4156759997u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (StaffLocked)
			{
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!ServerIsActiveChefDeskUser(senderClientId) || CurrentState != ChefDeskState.Cutting || !_serverRollMeshCutSessionActive)
			{
				UtilityMethods.Log($"[ChefDesk Mesh Cutting] Replication rejected. SenderClientId={senderClientId}, ActiveClientId={_serverCurrentUserClientId}, State={CurrentState}, SessionActive={_serverRollMeshCutSessionActive}", LogType.Warning);
				return;
			}
			int num = _serverRollMeshCutSequence + 1;
			if (sequence != num || sequence > _serverRollMeshCutRequiredCount)
			{
				UtilityMethods.Log($"[ChefDesk Mesh Cutting] Sequence rejected. SenderClientId={senderClientId}, Sequence={sequence}, Expected={num}, Required={_serverRollMeshCutRequiredCount}", LogType.Warning);
			}
			else
			{
				if (!IsFiniteVector(cutPoint) || !IsFiniteVector(planeNormal) || !IsFiniteVector(cutDirection) || planeNormal.sqrMagnitude <= 0.0001f || cutDirection.sqrMagnitude <= 0.0001f)
				{
					return;
				}
				Vector3 normalized = planeNormal.normalized;
				if (Mathf.Abs(Vector3.Dot(normalized, GetCutSurfaceNormal())) > 0.25f)
				{
					UtilityMethods.Log($"[ChefDesk Mesh Cutting] Plane normal rejected because it is not aligned with the desk surface. SenderClientId={senderClientId}, PlaneNormal={planeNormal}", LogType.Warning);
					return;
				}
				Vector3 vector = ((_centerPoint != null) ? _centerPoint.position : base.transform.position);
				if ((cutPoint - vector).sqrMagnitude > 4f)
				{
					UtilityMethods.Log($"[ChefDesk Mesh Cutting] Cut point rejected because it is outside the desk range. SenderClientId={senderClientId}, CutPoint={cutPoint}", LogType.Warning);
					return;
				}
				if (ResolveCurrentSingleSushiPrefab() == null)
				{
					UtilityMethods.Log($"[ChefDesk Mesh Cutting] Replication rejected because the single sushi prefab is missing. SushiId={SelectedSushiID}, SushiType={CurrentSushiType}", LogType.Warning);
					return;
				}
				int num2 = Mathf.Max(1, _maxCutSlots);
				if (_serverTemporaryCutPieceSpawnCount < num2)
				{
					_serverRollMeshCutSequence = sequence;
					_serverTemporaryCutPieceSpawnCount++;
					ClientApplyRollMeshCutForObserversRpc(sequence, cutPoint, normalized, cutDirection.normalized);
				}
			}
		}

		[Rpc(SendTo.NotOwner)]
		private void ClientApplyRollMeshCutForObserversRpc(int sequence, Vector3 cutPoint, Vector3 planeNormal, Vector3 cutDirection)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2649109984u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sequence);
				bufferWriter.WriteValueSafe(in cutPoint);
				bufferWriter.WriteValueSafe(in planeNormal);
				bufferWriter.WriteValueSafe(in cutDirection);
				__endSendRpc(ref bufferWriter, 2649109984u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Vector3 cutPoint2 = cutPoint;
				Vector3 cutDirection2 = cutDirection;
				int sequence2 = sequence;
				if (_rollMeshCutController == null || !_rollMeshCutController.IsSessionActive)
				{
					UtilityMethods.Log($"[ChefDesk Mesh Cutting] Observer cut was ignored because its local session is missing. Sequence={sequence2}", LogType.Error);
				}
				else if (!_rollMeshCutController.RequestCut(sequence2, cutPoint2, planeNormal, (bool success) =>
				{
					if (success)
					{
						ClientSpawnTemporaryCutPiece(cutPoint2, cutDirection2);
						PlayCutParticle(cutPoint2, cutDirection2);
						_rollMeshCutController.HidePresentationIfFinalCut(sequence2);
					}
					else
					{
						UtilityMethods.Log($"[ChefDesk Mesh Cutting] Observer cut transaction failed. Sequence={sequence2}, SushiType={CurrentSushiType}", LogType.Error);
					}
				}))
				{
					UtilityMethods.Log($"[ChefDesk Mesh Cutting] Observer cut request was rejected. Sequence={sequence2}", LogType.Error);
				}
			}
		}

		[Rpc(SendTo.NotOwner)]
		private void ClientSpawnTemporaryCutPieceForObserversRpc(Vector3 cutPoint, Vector3 cutDirection)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1859301407u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in cutPoint);
				bufferWriter.WriteValueSafe(in cutDirection);
				__endSendRpc(ref bufferWriter, 1859301407u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientSpawnTemporaryCutPiece(cutPoint, cutDirection);
				PlayCutParticle(cutPoint, cutDirection);
			}
		}

		private bool ClientSpawnTemporaryCutPiece(Vector3 cutPoint, Vector3 cutDirection)
		{
			if (!base.IsClient)
			{
				return false;
			}
			PruneDestroyedTemporaryCutPieces();
			if (_temporaryCutPieces.Count >= Mathf.Max(1, _maxCutSlots))
			{
				return false;
			}
			GameObject gameObject = ResolveCurrentSingleSushiPrefab();
			if (gameObject == null)
			{
				UtilityMethods.Log($"[ChefDesk Cut Piece] Cannot spawn because the recipe has no single sushi prefab. SushiId={SelectedSushiID}, SushiType={CurrentSushiType}", LogType.Warning);
				return false;
			}
			Vector3 cutSurfaceNormal = GetCutSurfaceNormal();
			Vector3 vector = Vector3.ProjectOnPlane(cutDirection, cutSurfaceNormal);
			if (vector.sqrMagnitude <= 0.0001f)
			{
				return false;
			}
			vector.Normalize();
			Transform transform = ((_centerPoint != null) ? _centerPoint : base.transform);
			Vector3 position = cutPoint + cutSurfaceNormal * _singleSushiSpawnHeight + vector * _singleSushiDirectionalOffset;
			int num = LayerMask.NameToLayer("Ignore Raycast");
			if (num < 0)
			{
				UtilityMethods.Log("[ChefDesk Cut Piece] Required presentation layer is missing. Layer=Ignore Raycast", LogType.Error);
				return false;
			}
			Quaternion rotation = transform.rotation * gameObject.transform.localRotation;
			GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject, position, rotation);
			gameObject2.name = gameObject.name + "_TemporaryCutPiece";
			if (CurrentResolvedRecipeKind != SushiRecipeIdentityKind.BuiltIn && gameObject2.TryGetComponent<CustomSushiIngredientVisuals>(out var component))
			{
				component.Apply(BuildCurrentCustomComposition(), isCustomSushi: true);
			}
			SetLayerRecursively(gameObject2.transform, num);
			if (!TryConfigureTemporaryCutPiecePhysics(gameObject2, num, out var pieceRigidbody))
			{
				UnityEngine.Object.Destroy(gameObject2);
				UtilityMethods.Log("[ChefDesk Cut Piece] Runtime physics setup failed. Prefab=" + gameObject.name, LogType.Error);
				return false;
			}
			if (!ClientSetTemporaryCutPieceSurfaceActive(active: true, num))
			{
				UnityEngine.Object.Destroy(gameObject2);
				UtilityMethods.Log("[ChefDesk Cut Piece] Presentation surface is missing or invalid.", LogType.Error);
				return false;
			}
			_temporaryCutPieces.Add(gameObject2);
			Vector3 force = vector * _singleSushiCutImpulse + cutSurfaceNormal * _singleSushiUpwardImpulse;
			pieceRigidbody.AddForce(force, ForceMode.Impulse);
			return true;
		}

		private bool TryConfigureTemporaryCutPiecePhysics(GameObject piece, int presentationLayer, out Rigidbody pieceRigidbody)
		{
			pieceRigidbody = null;
			if (piece == null)
			{
				return false;
			}
			Collider[] componentsInChildren = piece.GetComponentsInChildren<Collider>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				if (componentsInChildren[i] != null)
				{
					componentsInChildren[i].enabled = false;
				}
			}
			BoxCollider boxCollider = piece.GetComponent<BoxCollider>();
			if (boxCollider == null)
			{
				boxCollider = piece.AddComponent<BoxCollider>();
			}
			if (!TryGetLocalRendererBounds(piece.transform, out var localBounds))
			{
				return false;
			}
			Vector3 size = localBounds.size;
			size.x = Mathf.Max(0.01f, size.x);
			size.y = Mathf.Max(0.01f, size.y);
			size.z = Mathf.Max(0.01f, size.z);
			boxCollider.center = localBounds.center;
			boxCollider.size = size;
			boxCollider.isTrigger = false;
			boxCollider.enabled = true;
			LayerMask includeLayers = new LayerMask
			{
				value = 1 << presentationLayer
			};
			LayerMask excludeLayers = new LayerMask
			{
				value = ~includeLayers.value
			};
			boxCollider.includeLayers = includeLayers;
			boxCollider.excludeLayers = excludeLayers;
			boxCollider.layerOverridePriority = 1;
			pieceRigidbody = piece.GetComponent<Rigidbody>();
			if (pieceRigidbody == null)
			{
				pieceRigidbody = piece.AddComponent<Rigidbody>();
			}
			pieceRigidbody.mass = Mathf.Max(0.01f, _singleSushiMass);
			pieceRigidbody.useGravity = true;
			pieceRigidbody.isKinematic = false;
			pieceRigidbody.linearDamping = Mathf.Max(0f, _singleSushiLinearDamping);
			pieceRigidbody.angularDamping = Mathf.Max(0f, _singleSushiAngularDamping);
			pieceRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
			pieceRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
			pieceRigidbody.excludeLayers = excludeLayers;
			pieceRigidbody.WakeUp();
			return true;
		}

		private bool ClientSetTemporaryCutPieceSurfaceActive(bool active, int presentationLayer = -1)
		{
			if (_singleSushiSurfaceCollider == null)
			{
				return !active;
			}
			if (active)
			{
				if (presentationLayer < 0)
				{
					return false;
				}
				LayerMask includeLayers = new LayerMask
				{
					value = 1 << presentationLayer
				};
				_singleSushiSurfaceCollider.gameObject.layer = presentationLayer;
				_singleSushiSurfaceCollider.includeLayers = includeLayers;
				_singleSushiSurfaceCollider.excludeLayers = new LayerMask
				{
					value = ~includeLayers.value
				};
				_singleSushiSurfaceCollider.layerOverridePriority = 1;
			}
			_singleSushiSurfaceCollider.enabled = active;
			return true;
		}

		private static bool TryGetLocalRendererBounds(Transform root, out Bounds localBounds)
		{
			localBounds = default;
			if (root == null)
			{
				return false;
			}
			Renderer[] componentsInChildren = root.GetComponentsInChildren<Renderer>(includeInactive: true);
			bool flag = false;
			foreach (Renderer renderer in componentsInChildren)
			{
				if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
				{
					continue;
				}
				Bounds localBounds2 = renderer.localBounds;
				Vector3 min = localBounds2.min;
				Vector3 max = localBounds2.max;
				for (int j = 0; j < 8; j++)
				{
					Vector3 position = new Vector3(((j & 1) == 0) ? min.x : max.x, ((j & 2) == 0) ? min.y : max.y, ((j & 4) == 0) ? min.z : max.z);
					Vector3 position2 = renderer.transform.TransformPoint(position);
					Vector3 vector = root.InverseTransformPoint(position2);
					if (!flag)
					{
						localBounds = new Bounds(vector, Vector3.zero);
						flag = true;
					}
					else
					{
						localBounds.Encapsulate(vector);
					}
				}
			}
			return flag;
		}

		private Vector3 GetCutSurfaceNormal()
		{
			Vector3 vector = ((_centerPoint != null) ? _centerPoint.up : base.transform.up);
			if (!(vector.sqrMagnitude > 0.0001f))
			{
				return Vector3.up;
			}
			return vector.normalized;
		}

		private static void SetLayerRecursively(Transform root, int layer)
		{
			if (!(root == null))
			{
				root.gameObject.layer = layer;
				for (int i = 0; i < root.childCount; i++)
				{
					SetLayerRecursively(root.GetChild(i), layer);
				}
			}
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

		private void PruneDestroyedTemporaryCutPieces()
		{
			for (int num = _temporaryCutPieces.Count - 1; num >= 0; num--)
			{
				if (_temporaryCutPieces[num] == null)
				{
					_temporaryCutPieces.RemoveAt(num);
				}
			}
		}

		private void ClientClearTemporaryCutPieces()
		{
			for (int num = _temporaryCutPieces.Count - 1; num >= 0; num--)
			{
				GameObject gameObject = _temporaryCutPieces[num];
				if (gameObject != null)
				{
					UnityEngine.Object.Destroy(gameObject);
				}
			}
			_temporaryCutPieces.Clear();
			ClientSetTemporaryCutPieceSurfaceActive(active: false);
		}

		private void ServerClearTemporaryCutPieceSessionForAllClients()
		{
			if (base.IsServer)
			{
				_serverTemporaryCutPieceSpawnCount = 0;
				_serverRollMeshCutSequence = 0;
				_serverRollMeshCutRequiredCount = 0;
				_serverRollMeshCutSessionActive = false;
				ClientClearTemporaryCutPiecesRpc();
			}
		}

		private void ServerResetTemporaryCutPieceSpawnCount()
		{
			if (base.IsServer)
			{
				_serverTemporaryCutPieceSpawnCount = 0;
				_serverRollMeshCutSequence = 0;
				_serverRollMeshCutRequiredCount = 0;
				_serverRollMeshCutSessionActive = false;
			}
		}

		[Rpc(SendTo.NotOwner)]
		private void ClientClearTemporaryCutPiecesForObserversRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(2575532333u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2575532333u, rpcParams, attributeParams, SendTo.NotOwner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientResetRollMeshCutSession();
				ClientClearTemporaryCutPieces();
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientClearTemporaryCutPiecesRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3197777309u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3197777309u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientResetRollMeshCutSession();
				ClientClearTemporaryCutPieces();
			}
		}

		private float GetKnifeBladeScreenAngleZ()
		{
			if (_cuttingManager == null || _cuttingManager.PlayerCamera == null)
			{
				return 0f;
			}
			if (_knifeTip == null || _knifeHeel == null)
			{
				if (!(_cuttingManager.CutPointUI != null))
				{
					return 0f;
				}
				return _cuttingManager.CutPointUI.localEulerAngles.z;
			}
			Camera playerCamera = _cuttingManager.PlayerCamera;
			Vector3 vector = playerCamera.WorldToScreenPoint(_knifeTip.position);
			Vector3 vector2 = playerCamera.WorldToScreenPoint(_knifeHeel.position);
			if (vector.z <= 0f || vector2.z <= 0f)
			{
				if (!(_cuttingManager.CutPointUI != null))
				{
					return 0f;
				}
				return _cuttingManager.CutPointUI.localEulerAngles.z;
			}
			Vector2 vector3 = (Vector2)vector - (Vector2)vector2;
			if (vector3.sqrMagnitude < 0.0001f)
			{
				if (!(_cuttingManager.CutPointUI != null))
				{
					return 0f;
				}
				return _cuttingManager.CutPointUI.localEulerAngles.z;
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

		private void ClientCuttingCompleted()
		{
			if (IsCuttingActiveLocally)
			{
				KillCutFlowDelay();
				_localCuttingActive = false;
				_cuttingManager.SetCuttingViewActive(isActive: false);
				if (_knifeModel != null)
				{
					_knifeModel.gameObject.SetActive(value: false);
				}
				ShowCursorForLocalChefDeskUser();
				_isKnifeHeld = false;
				_mustExitCutPoint = false;
				UtilityMethods.Log("[ChefDesk] Cutting sequence completed successfully.");
				ServerRequestFinalizePreparedSushiRpc(ChefDeskFinalizeReason.CuttingCompleted);
				SendInstructionToOwnerRpc(ChefDeskInfoTextId.MovePlateOrSeason);
				ResetCutProgressUI(deactivateParent: true);
			}
		}

		private void ClientCompleteDishWithoutCutting(ChefDeskFinalizeReason reason)
		{
			KillCutFlowDelay();
			_localCuttingActive = false;
			_cuttingManager.SetCuttingViewActive(isActive: false);
			if (_knifeModel != null)
			{
				_knifeModel.gameObject.SetActive(value: false);
			}
			ShowCursorForLocalChefDeskUser();
			_isKnifeHeld = false;
			_mustExitCutPoint = false;
			UtilityMethods.Log("[ChefDesk] Dish completed without cutting.");
			ServerRequestFinalizePreparedSushiRpc(reason);
			SendInstructionToOwnerRpc(ChefDeskInfoTextId.MovePlateOrSeason);
			ResetCutProgressUI(deactivateParent: true);
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
				Vector3 cutSurfaceNormal = GetCutSurfaceNormal();
				Vector3 upwards = Vector3.ProjectOnPlane(cutDirection, cutSurfaceNormal);
				if (upwards.sqrMagnitude <= 0.0001f)
				{
					upwards = Vector3.ProjectOnPlane(base.transform.forward, cutSurfaceNormal);
				}
				if (upwards.sqrMagnitude <= 0.0001f)
				{
					upwards = Vector3.forward;
				}
				upwards.Normalize();
				Quaternion rotation = Quaternion.LookRotation(cutSurfaceNormal, upwards) * _cutParticlePrefab.transform.localRotation;
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
			Transform transform = ((_rollMeshCutController != null && _rollMeshCutController.IsSessionActive) ? _rollMeshCutController.PresentationRoot : null);
			if (transform != null)
			{
				transform.DOKill();
				transform.DOPunchScale(Vector3.one * _cutItemScalePunch, _cutItemScaleDuration, 4, 0.45f);
				return;
			}
			if (_visualMap == null)
			{
				UtilityMethods.Log("<color=red>[ChefDesk] Visual map is null, cannot play cut flash.</color>");
				return;
			}
			if (!_visualMap.TryGetValue(CurrentSushiType, out var value) || value == null)
			{
				UtilityMethods.Log("<color=red>[ChefDesk] Visual data for current sushi type is missing.</color>");
				return;
			}
			GameObject gameObject = ResolveCutTargetObject(value);
			if (gameObject == null)
			{
				UtilityMethods.Log($"<color=red>[ChefDesk Cutting] Cut feedback target is missing. SushiType={CurrentSushiType}</color>");
				return;
			}
			Transform target = gameObject.transform;
			target.DOKill();
			target.DOPunchScale(Vector3.one * _cutItemScalePunch, _cutItemScaleDuration, 4, 0.45f);
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

		private bool CanStartCuttingLocally()
		{
			if (!base.IsOwner)
			{
				return false;
			}
			if (CurrentState != ChefDeskState.Cutting)
			{
				return false;
			}
			if (_isCenteringAssembly)
			{
				return false;
			}
			if (_currentInteractor == null)
			{
				return false;
			}
			if (_currentInteractor.Player == null)
			{
				return false;
			}
			return true;
		}

		private void ClientClearDeskHoverState()
		{
			ClientClearChefDeskBookHover();
			ClientClearSeasoningHover();
			ClientClearCenterDishHover();
		}

		private void ClientUpdateChefDeskBookHover(RaycastHit hit)
		{
			ChefDeskBook componentInParent = hit.collider.GetComponentInParent<ChefDeskBook>();
			if (!(componentInParent == _hoveredChefDeskBook))
			{
				SetChefDeskBookHover(componentInParent);
			}
		}

		private void SetChefDeskBookHover(ChefDeskBook book)
		{
			if (_hoveredChefDeskBook != null)
			{
				_hoveredChefDeskBook.ClientSetHoverVisual(isHovering: false);
			}
			_hoveredChefDeskBook = book;
			if (_hoveredChefDeskBook != null)
			{
				_hoveredChefDeskBook.ClientSetHoverVisual(isHovering: true);
			}
		}

		private void ClientClearChefDeskBookHover()
		{
			if (!(_hoveredChefDeskBook == null))
			{
				_hoveredChefDeskBook.ClientSetHoverVisual(isHovering: false);
				_hoveredChefDeskBook = null;
			}
		}

		private void ClientUpdateSeasoningHover(RaycastHit hit)
		{
			if (_currentApplicator != null)
			{
				ClientClearSeasoningHover();
				return;
			}
			Seasoning componentInParent = hit.collider.GetComponentInParent<Seasoning>();
			if (!(componentInParent == _hoveredSeasoning))
			{
				SetSeasoningHover(componentInParent);
			}
		}

		private void SetSeasoningHover(Seasoning seasoning)
		{
			if (_hoveredSeasoning != null)
			{
				Singleton<ToolTipHandler>.Instance.OnGazeExit(_hoveredSeasoning);
				_hoveredSeasoning.ClientSetHoverVisual(isHovering: false);
			}
			_hoveredSeasoning = seasoning;
			if (_hoveredSeasoning != null)
			{
				_hoveredSeasoning.ClientSetHoverVisual(isHovering: true);
				Singleton<ToolTipHandler>.Instance.OnGazeEnter(_hoveredSeasoning, _hoveredSeasoning.transform.position);
			}
		}

		private void ClientClearSeasoningHover()
		{
			if (!(_hoveredSeasoning == null))
			{
				Singleton<ToolTipHandler>.Instance.OnGazeExit(_hoveredSeasoning);
				_hoveredSeasoning.ClientSetHoverVisual(isHovering: false);
				_hoveredSeasoning = null;
			}
		}

		private void ClientUpdateCenterDishHover(RaycastHit hit)
		{
			if (_currentApplicator != null)
			{
				ClientClearCenterDishHover();
				return;
			}
			ServedDishSushi componentInParent = hit.collider.GetComponentInParent<ServedDishSushi>();
			if (componentInParent == null || componentInParent.AssignedEdgeIndex >= 0)
			{
				ClientClearCenterDishHover();
			}
			else if (!(_hoveredCenterDish == componentInParent))
			{
				SetCenterDishHover(componentInParent);
			}
		}

		private void SetCenterDishHover(ServedDishSushi sushi)
		{
			if (_hoveredCenterDish != null)
			{
				_hoveredCenterDish.ClientSetHoverVisual(isHovering: false);
			}
			_hoveredCenterDish = sushi;
			_hoveredCenterDish.ClientSetHoverVisual(isHovering: true);
		}

		private void ClientClearCenterDishHover()
		{
			if (!(_hoveredCenterDish == null))
			{
				_hoveredCenterDish.ClientSetHoverVisual(isHovering: false);
				_hoveredCenterDish = null;
			}
		}

		public override void ServerUseEnter(Interactor interactor)
		{
			if (!StaffLocked && _physicalStateController.IsPhysicallyUsable)
			{
				QuestReferenceHub.Instance?.CompleteIfActive(QuestId.UseChefDesk);
				QuestReferenceHub.Instance?.CompleteIfActive(QuestId.UseChefDesk_01);
				base.ServerUseEnter(interactor);
				_serverCurrentUserClientId = interactor.OwnerClientId;
				BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(interactor.OwnerClientId, RpcTargetUse.Temp);
				NetworkObjectReference[] dishRefs = _servedDishesOnDesk.ToArray();
				ClientSetDeskDishCollidersRpc(dishRefs, isTrigger: false, baseRpcTarget);
				if (base.NetworkObject.OwnerClientId != interactor.OwnerClientId)
				{
					base.NetworkObject.ChangeOwnership(interactor.OwnerClientId);
				}
				CanGaze.Value = false;
			}
		}

		public override void ServerUseExit(Interactor interactor)
		{
			ServerUseExitInternal(interactor, notifyOwner: true);
		}

		public override void ServerUseInterrupted(Interactor interactor)
		{
			ServerUseExitInternal(interactor, notifyOwner: false);
		}

		private void ServerUseExitInternal(Interactor interactor, bool notifyOwner)
		{
			bool num = CurrentState == ChefDeskState.Cutting;
			base.ServerUseExit(interactor);
			if (num)
			{
				UtilityMethods.Log("[ChefDesk] User exited during Cutting. Local cutting input and temporary cut pieces will be reset.");
				ServerClearTemporaryCutPieceSessionForAllClients();
				ClientCuttingStateResetRpc();
			}
			if (notifyOwner)
			{
				BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(interactor.OwnerClientId, RpcTargetUse.Temp);
				NetworkObjectReference[] dishRefs = _servedDishesOnDesk.ToArray();
				ClientSetDeskDishCollidersRpc(dishRefs, isTrigger: true, baseRpcTarget);
			}
			if (_serverCurrentUserClientId == interactor.OwnerClientId)
			{
				_serverCurrentUserClientId = ulong.MaxValue;
			}
			base.NetworkObject.RemoveOwnership();
			CanGaze.Value = true;
		}

		public override void ClientUseEnter(Interactor interactor)
		{
			if (!_physicalStateController.IsPhysicallyUsable)
			{
				return;
			}
			base.ClientUseEnter(interactor);
			_sushiRollCanvas.enabled = true;
			Singleton<GameUIManager>.Instance.TimeUI.SetVisibility(status: false);
			interactor.Player.ForKitchenUsable(value: false);
			Singleton<QuickSlotHandler>.Instance.gameObject.SetActive(value: false);
			if (interactor.Player.CharacterVisuals != null && interactor.Player.CharacterVisuals.FpsRootObject != null)
			{
				UtilityMethods.SetLayerRecursive(interactor.Player.CharacterVisuals.FpsRootObject, "Player", includeChildren: true);
			}
			interactor.Player.SwitchTo("ChefStation", cursorVisible: true);
			Singleton<InputManager>.Instance.InputHandler.ChefDeskExit += OnChefDeskExit;
			Singleton<InputManager>.Instance.InputHandler.KnifeAction += OnKnifeAction;
			if (Singleton<QuickSlotHandler>.Instance.SelectedItem != null)
			{
				HideHoldableForChefDesk(Singleton<QuickSlotHandler>.Instance.SelectedItem);
			}
			EventBus<DeskCam>.Raise(new DeskCam
			{
				CinemachineBrain = interactor.Player.CinemachineBrain
			});
			List<BaseIngredientNeed> sortedNeeds = GetSortedNeeds();
			_outCanvasGO.SetActive(value: false);
			_inCanvasGO.SetActive(value: true);
			foreach (ChefDeskGastroPan gastroPan in _gastroPanList)
			{
				gastroPan.GastropanInteractor.ToolTipGO_01.SetActive(value: false);
			}
			if (!SelectedSushiID.IsEmpty)
			{
				ClientRefreshRecipeCanvasFromState();
			}
			_chefDeskSushiRollImage.enabled = false;
			_leftToRightmouseIconTweenMover.SetActive(value: false);
			_bottomToTopmouseIconTweenMover.SetActive(value: false);
			switch (CurrentState)
			{
			case ChefDeskState.Idle:
				if (IsCustomSushi)
				{
					if (_placedIngredientIdsNetwork.Count == 0 && !_centerOccupied.Value)
					{
						_isCustomTypeSelectionOpen = true;
					}
					ClientRefreshCustomSushiUI();
				}
				else if (sortedNeeds != null && sortedNeeds.Count > 0)
				{
					_ = sortedNeeds[0];
					ClientUpdateInstructionText(ChefDeskInfoTextId.CompleteRecipe);
				}
				else
				{
					ClientUpdateInstructionText(ChefDeskInfoTextId.SelectRecipe);
				}
				break;
			case ChefDeskState.Cutting:
				StartCutting();
				break;
			case ChefDeskState.Centering:
				ClientBeginCenterAssemblyMovement();
				break;
			case ChefDeskState.WaitingForRoll:
				_recipeInfoCanvas.gameObject.SetActive(value: true);
				_chefDeskSushiRollImage.enabled = true;
				_sushiRollController?.ClientPrepareRollPresentation();
				if (_currentSushiType.Value == 1 || _currentSushiType.Value == 2)
				{
					_bottomToTopmouseIconTweenMover.SetActive(value: true);
				}
				else
				{
					_leftToRightmouseIconTweenMover.SetActive(value: true);
				}
				ClientUpdateInstructionText(ChefDeskInfoTextId.RollSushi);
				break;
			case ChefDeskState.RecipeSelected:
				if (IsCustomSushi)
				{
					ClientRefreshCustomSushiUI();
					break;
				}
				_recipeInfoCanvas.gameObject.SetActive(value: true);
				if (sortedNeeds != null && _currentRecipeStepIndex.Value < sortedNeeds.Count)
				{
					_ = sortedNeeds[_currentRecipeStepIndex.Value];
					ClientUpdateInstructionText(ChefDeskInfoTextId.CompleteRecipe);
				}
				else
				{
					ClientUpdateInstructionText(ChefDeskInfoTextId.RollSushi);
				}
				break;
			case ChefDeskState.TransferToEdge:
				if (IsCustomSushi)
				{
					ClientRefreshCustomSushiUI();
					break;
				}
				_recipeInfoCanvas.gameObject.SetActive(value: true);
				ClientUpdateInstructionText(ChefDeskInfoTextId.MovePlateOrSeason);
				break;
			}
			RefreshCursorForChefDeskUseMode();
			TryOpenTutorialForFirstUse(interactor);
		}

		public override void ClientUseExit(Interactor interactor)
		{
			if (IsCuttingActiveLocally || CurrentState == ChefDeskState.Cutting)
			{
				ClientResetCuttingSessionOnly();
			}
			else
			{
				KillCutFlowDelay();
			}
			ClearSelection();
			ClientClearGastropanHoldPlacement();
			base.ClientUseExit(interactor);
			foreach (ChefDeskGastroPan gastroPan in _gastroPanList)
			{
				gastroPan.GastropanInteractor.ToolTipGO_01.SetActive(value: false);
			}
			RestoreHiddenHoldableForChefDesk();
			Singleton<QuickSlotHandler>.Instance.gameObject.SetActive(value: true);
			_leftToRightmouseIconTweenMover.SetActive(value: false);
			_bottomToTopmouseIconTweenMover.SetActive(value: false);
			if (interactor.Player.CharacterVisuals != null && interactor.Player.CharacterVisuals.FpsRootObject != null)
			{
				UtilityMethods.SetLayerRecursive(interactor.Player.CharacterVisuals.FpsRootObject, "ViewModel", includeChildren: true);
			}
			Singleton<GameUIManager>.Instance.TimeUI.SetVisibility(status: true);
			interactor.Player.ForKitchenUsable(value: true);
			EventBus<DeskCam>.Raise(new DeskCam
			{
				CinemachineBrain = null
			});
			interactor.Player.SwitchTo("Player", cursorVisible: false);
			Singleton<InputManager>.Instance.InputHandler.ChefDeskExit -= OnChefDeskExit;
			Singleton<InputManager>.Instance.InputHandler.KnifeAction -= OnKnifeAction;
			_recipeInfoCanvas.gameObject.SetActive(value: false);
			Cursor.visible = true;
			_sushiRollCanvas.enabled = true;
			_outCanvasGO.SetActive(value: true);
			_inCanvasGO.SetActive(value: false);
			ClientClearDeskHoverState();
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

		protected override void ClientCrossMouse1(Interactor interactor, BaseHoldable holdable, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && !(holdable == null) && holdable.ItemSO.ItemType == ItemType.Seasoning)
			{
				base.ClientCrossMouse1(interactor, holdable, phase);
				ServerPlaceSeasoningOnRackRpc(interactor.NetworkObject.ToReference(), holdable.NetworkObject.ToReference());
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerPlaceSeasoningOnRackRpc(NetworkObjectReference interactorRef, NetworkObjectReference holdableRef)
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
				FastBufferWriter bufferWriter = __beginSendRpc(789288353u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in holdableRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 789288353u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (StaffLocked || !interactorRef.TryGetComponent<Interactor>(out var component) || !holdableRef.TryGetComponent<BaseHoldable>(out var component2))
			{
				return;
			}
			int num = ServerGetFirstEmptySlotIndex();
			if (num < 0)
			{
				return;
			}
			Transform transform = _placementPoints[num];
			if (component2 is Seasoning seasoning && seasoning.OwnerClientId == component.OwnerClientId && !seasoning.CanHold.Value)
			{
				_placedSeasonings[num] = seasoning;
				if (!seasoning.ServerPlaceOnChefDeskRack(this, num, transform.position, Vector3.zero))
				{
					ClearSlot(num, seasoning);
				}
			}
		}

		public void ClearSlot(int slotIndex, BaseHoldable holdable = null)
		{
			if (slotIndex >= 0 && slotIndex < _placedSeasonings.Length && (!(holdable != null) || !(_placedSeasonings[slotIndex] != holdable)))
			{
				_placedSeasonings[slotIndex] = null;
			}
		}

		private int ServerGetFirstEmptySlotIndex()
		{
			for (int i = 0; i < _placedSeasonings.Length; i++)
			{
				if (_placedSeasonings[i] == null)
				{
					return i;
				}
			}
			return -1;
		}

		private void OnChefDeskExit(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && !TryCloseOpenTutorialPopup())
			{
				if (_currentApplicator != null)
				{
					ClearSelection();
				}
				else if (_chefDeskBookCanvas.DeskResetCanvasGO.activeInHierarchy)
				{
					_chefDeskBookCanvas.DeskResetCanvasGO.SetActive(value: false);
				}
				else if (_chefDeskBookCanvas.isActiveAndEnabled)
				{
					_chefDeskBookCanvas.ToggleBookUI(isVisible: false);
				}
				else
				{
					_currentInteractor.ServerUseExitRpc();
				}
			}
		}

		private bool TryCloseOpenTutorialPopup()
		{
			if (_stationTutorialPopupUI != null && _stationTutorialPopupUI.IsOpen)
			{
				_stationTutorialPopupUI.Close();
				RefreshCursorForChefDeskUseMode();
				return true;
			}
			StationTutorialPopupUI stationTutorialPopupUI = ((Singleton<GameUIManager>.Instance != null) ? Singleton<GameUIManager>.Instance.StationTutorialPopupUI : null);
			if (stationTutorialPopupUI == null || stationTutorialPopupUI == _stationTutorialPopupUI || !stationTutorialPopupUI.IsOpen)
			{
				return false;
			}
			stationTutorialPopupUI.Close();
			RefreshCursorForChefDeskUseMode();
			return true;
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
			QuestReferenceHub.Instance.AddProgress(QuestId.RepairTheChefDesk);
			_physicalStateController.Repair();
			return true;
		}

		private void TryOpenTutorialForFirstUse(Interactor interactor)
		{
			if (PlayerPrefs.GetInt("StationTutorial.ChefDesk.Seen", 0) != 1 && !(Singleton<GameUIManager>.Instance == null) && !(Singleton<GameUIManager>.Instance.StationTutorialPopupUI == null))
			{
				PlayerPrefs.SetInt("StationTutorial.ChefDesk.Seen", 1);
				PlayerPrefs.Save();
				Singleton<GameUIManager>.Instance.StationTutorialPopupUI.OpenAfterRepair(_tutorialData, interactor.Player);
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerSushiSelectedRpc(FixedString64Bytes previewSushiID)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2474426632u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in previewSushiID, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 2474426632u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (StaffLocked)
			{
				return;
			}
			string text = previewSushiID.ToString();
			if (!TryResolveRecipeDefinition(text, out var recipe))
			{
				ClientShowNotifyRpc("Info", "notification.chef.recipe_selection_failed");
				UtilityMethods.Log("[ChefDesk RecipeSwitch] Rejected. Requested=" + text + ", Reason=InvalidRecipe", LogType.Warning);
				return;
			}
			if (CurrentState != ChefDeskState.Idle && CurrentState != ChefDeskState.RecipeSelected)
			{
				ClientShowNotifyRpc("Info", "You can’t select a recipe right now");
				UtilityMethods.Log($"[ChefDesk RecipeSwitch] Rejected. Selected={_selectedSushiSO_ID.Value}, Requested={text}, " + $"PlacedIngredients={_placedIngredientIdsNetwork.Count}, Reason=InvalidState:{CurrentState}", LogType.Warning);
				return;
			}
			if (!TryValidateRecipeSelection(recipe, out var rejectionMessage))
			{
				ClientShowNotifyRpc("Info", rejectionMessage);
				UtilityMethods.Log($"[ChefDesk RecipeSwitch] Rejected. Selected={_selectedSushiSO_ID.Value}, Requested={text}, " + $"PlacedIngredients={_placedIngredientIdsNetwork.Count}, CenterOccupied={_centerOccupied.Value}, " + "Reason=" + rejectionMessage, LogType.Warning);
				return;
			}
			bool restorePlacedProgress = _placedIngredientIdsNetwork.Count > 0;
			FixedString64Bytes value = _selectedSushiSO_ID.Value;
			_isCustomSushi.Value = false;
			_isCustomCompositionLocked.Value = false;
			_resolvedCustomRecipeKind.Value = (byte)recipe.IdentityKind;
			_currentSushiType.Value = (byte)recipe.SushiType;
			_selectedSushiSO_ID.Value = recipe.RecipeId;
			if (recipe.BuiltInSushi != null && TryGetSortedNeeds(recipe.BuiltInSushi, out var sortedNeeds))
			{
				ApplyRecipeProgressFromPlacedIngredients(sortedNeeds);
			}
			else
			{
				_currentRecipeStepIndex.Value = (byte)_placedIngredientIdsNetwork.Count;
				_currentIngredientPlacedCount = 0;
				_currentIngredientPlacedCountNetwork.Value = 0;
				_isRecipeCompleted.Value = _placedIngredientIdsNetwork.Count >= recipe.Composition.Count;
			}
			ClientUpdateInstructionText(ChefDeskInfoTextId.CompleteRecipe);
			ServerSetStateRpc(ChefDeskState.RecipeSelected);
			_sushiRollController?.ServerPrepareRollSession(recipe.SushiType);
			ClientInfoPanelPrepareRpc(previewSushiID, restorePlacedProgress);
			ClientPrepareAssemblyLayoutRpc((byte)recipe.SushiType, (ushort)Mathf.Clamp(GetInteriorIngredientVisualCount(recipe), 0, 65535));
			UtilityMethods.Log($"[ChefDesk RecipeSwitch] Accepted. Previous={value}, Requested={text}, " + $"PlacedIngredients={_placedIngredientIdsNetwork.Count}, Step={_currentRecipeStepIndex.Value}, " + $"StepPlaced={_currentIngredientPlacedCount}");
			if (_isRecipeCompleted.Value)
			{
				ServerCheckRecipeCompleted();
			}
			if (recipe.BuiltInSushi != null && recipe.RecipeId == Singleton<GameProgressManager>.Instance.ItemDatabaseSO.CucumberMakiServeredDishSO.Id)
			{
				QuestReferenceHub.Instance.QuestComplete(QuestId.SelectCucumberMakiRecipeFromRecipeBook);
			}
			if (recipe.BuiltInSushi != null && recipe.RecipeId == Singleton<GameProgressManager>.Instance.ItemDatabaseSO.MackarelNigiriServeredDishSO.Id)
			{
				QuestReferenceHub.Instance.QuestComplete(QuestId.SelectMackerelNigiriRecipeFromRecipeBook);
			}
		}

		public bool ClientCanSelectRecipe(string sushiId, out string rejectionMessage)
		{
			rejectionMessage = string.Empty;
			if (!TryResolveRecipeDefinition(sushiId, out var recipe))
			{
				rejectionMessage = "notification.chef.recipe_selection_failed";
				return false;
			}
			return TryValidateRecipeSelection(recipe, out rejectionMessage);
		}

		private bool TryValidateRecipeSelection(SushiRecipeDefinition targetRecipe, out string rejectionMessage)
		{
			rejectionMessage = string.Empty;
			if (_centerOccupied.Value)
			{
				rejectionMessage = "notification.chef.clear_center_to_change_recipe";
				return false;
			}
			List<string> list = targetRecipe?.Composition?.IngredientIds;
			if (targetRecipe == null || list == null || list.Count == 0)
			{
				rejectionMessage = "notification.chef.recipe_selection_failed";
				return false;
			}
			if (_placedIngredientIdsNetwork.Count == 0)
			{
				return true;
			}
			if (CurrentSushiType != targetRecipe.SushiType)
			{
				rejectionMessage = "notification.chef.recipe_sushi_type_mismatch";
				return false;
			}
			if (_placedIngredientIdsNetwork.Count > list.Count)
			{
				rejectionMessage = "notification.chef.recipe_prefix_mismatch";
				return false;
			}
			for (int i = 0; i < _placedIngredientIdsNetwork.Count; i++)
			{
				if (!(_placedIngredientIdsNetwork[i].ToString() == list[i]))
				{
					rejectionMessage = "notification.chef.recipe_prefix_mismatch";
					return false;
				}
			}
			return true;
		}

		private static bool TryBuildExpandedIngredientSequence(List<BaseIngredientNeed> sortedNeeds, out List<string> ingredientSequence)
		{
			ingredientSequence = new List<string>();
			if (sortedNeeds == null || sortedNeeds.Count == 0 || sortedNeeds.Count > 255)
			{
				return false;
			}
			for (int i = 0; i < sortedNeeds.Count; i++)
			{
				BaseIngredientNeed baseIngredientNeed = sortedNeeds[i];
				if (baseIngredientNeed == null || string.IsNullOrWhiteSpace(baseIngredientNeed.IngredientId) || baseIngredientNeed.RequiredAmount == 0)
				{
					return false;
				}
				for (int j = 0; j < baseIngredientNeed.RequiredAmount; j++)
				{
					ingredientSequence.Add(baseIngredientNeed.IngredientId);
				}
			}
			return ingredientSequence.Count > 0;
		}

		private void ApplyRecipeProgressFromPlacedIngredients(List<BaseIngredientNeed> sortedNeeds)
		{
			int num = _placedIngredientIdsNetwork.Count;
			int i = 0;
			int num2 = 0;
			for (; i < sortedNeeds.Count; i++)
			{
				int requiredAmount = sortedNeeds[i].RequiredAmount;
				if (num < requiredAmount)
				{
					num2 = num;
					break;
				}
				num -= requiredAmount;
			}
			_currentRecipeStepIndex.Value = (byte)i;
			_currentIngredientPlacedCount = num2;
			_currentIngredientPlacedCountNetwork.Value = (byte)num2;
			_isRecipeCompleted.Value = i >= sortedNeeds.Count;
		}

		[Rpc(SendTo.Owner)]
		private void ClientInfoPanelPrepareRpc(FixedString64Bytes sushiSO_ID, bool restorePlacedProgress = false)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1797742243u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in sushiSO_ID, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in restorePlacedProgress, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1797742243u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			string recipeId = sushiSO_ID.ToString();
			if (!TryResolveRecipeDefinition(recipeId, out var recipe))
			{
				return;
			}
			_recipeInfoCanvas.ClientPrepare(recipe, _itemDatabaseSO);
			if (restorePlacedProgress)
			{
				for (int i = 0; i < _placedIngredientIdsNetwork.Count; i++)
				{
					_recipeInfoCanvas.ClientIngredientItemOnTablePlaced(_placedIngredientIdsNetwork[i]);
				}
			}
		}

		[Rpc(SendTo.Owner)]
		private void SendInstructionToOwnerRpc(ChefDeskInfoTextId chefDeskInfoTextId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1118370342u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in chefDeskInfoTextId, default(FastBufferWriter.ForEnums));
				__endSendRpc(ref bufferWriter, 1118370342u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientUpdateInstructionText(chefDeskInfoTextId);
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerPlaceProductOnTableRpc(byte index)
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
				FastBufferWriter bufferWriter = __beginSendRpc(747287262u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in index, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 747287262u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			byte index2 = index;
			if (StaffLocked)
			{
				return;
			}
			ChefDeskGastroPan chefDeskGastroPan = _gastroPanList.FirstOrDefault((ChefDeskGastroPan x) => x.Index == index2);
			if (chefDeskGastroPan == null)
			{
				UtilityMethods.Log("<color=red>[ChefDesk - ServerCheckRecipeStepRpc] ChefDeskGastroPan bulunamadi</color>");
				return;
			}
			if (_isCustomSushi.Value)
			{
				ServerTryPlaceCustomIngredient(chefDeskGastroPan);
				return;
			}
			if (CurrentResolvedRecipeKind == SushiRecipeIdentityKind.SavedCustom)
			{
				ServerTryPlaceSavedCustomRecipeIngredient(chefDeskGastroPan);
				return;
			}
			if (chefDeskGastroPan.IngredientCount <= 0)
			{
				ClientShowNotifyRpc("Info", "notification.chef.gastropan_empty");
				return;
			}
			List<BaseIngredientNeed> sortedNeeds = GetSortedNeeds();
			if (sortedNeeds == null)
			{
				ClientShowNotifyRpc("Info", "notification.chef.select_recipe_first");
				UtilityMethods.Log("[ChefDesk- ClientUseEnter] SelectedSushiID is null or empty.");
			}
			else
			{
				if (CurrentState == ChefDeskState.Idle && _isRecipeCompleted.Value && !ServerPrepareSelectedRecipeForNextDish(sortedNeeds))
				{
					return;
				}
				if (_isRecipeCompleted.Value || _currentRecipeStepIndex.Value >= sortedNeeds.Count)
				{
					ClientShowNotifyRpc("Info", "notification.chef.recipe_completed");
					return;
				}
				BaseIngredientNeed baseIngredientNeed = sortedNeeds[_currentRecipeStepIndex.Value];
				if ((FixedString64Bytes)baseIngredientNeed.IngredientId == chefDeskGastroPan.CurrentIngredientID)
				{
					if (!chefDeskGastroPan.ServerTryConsumeForPlacement())
					{
						ClientShowNotifyRpc("Info", "notification.chef.gastropan_empty");
						return;
					}
					_currentIngredientPlacedCount++;
					_currentIngredientPlacedCountNetwork.Value = (byte)_currentIngredientPlacedCount;
					_placedIngredientIdsNetwork.Add(baseIngredientNeed.IngredientId);
					ClientIngredientPlacedRpc(baseIngredientNeed.IngredientId);
					if (CurrentState == ChefDeskState.Idle)
					{
						ServerSetStateRpc(ChefDeskState.RecipeSelected);
					}
					if (_currentIngredientPlacedCount >= baseIngredientNeed.RequiredAmount)
					{
						_currentRecipeStepIndex.Value++;
						_currentIngredientPlacedCount = 0;
						_currentIngredientPlacedCountNetwork.Value = 0;
						byte max = (byte)sortedNeeds.Count;
						Mathf.Clamp(_currentRecipeStepIndex.Value + 1, 1, max);
					}
					ServerCheckRecipeCompleted();
				}
				else
				{
					ClientShowNotifyRpc("Info", "notification.chef.wrong_recipe_ingredient");
				}
			}
		}

		internal bool ClientCanPlaceIngredientFromPan(ChefDeskGastroPan pan)
		{
			if (!(pan == null) && !StaffLocked && pan.IngredientCount != 0)
			{
				ChefDeskState currentState = CurrentState;
				if (currentState == ChefDeskState.Idle || currentState == ChefDeskState.RecipeSelected)
				{
					string text = pan.CurrentIngredientID.ToString();
					if (!(_itemDatabaseSO?.FindById(text) is ProcessedIngredientSO processedIngredientSO))
					{
						return false;
					}
					if (IsCustomSushi)
					{
						if (CustomSushiRules.IsSupportedProcessedIngredient(text, processedIngredientSO.ProcessedType, SingleNoriSO_ID, CookedRiceSO_ID))
						{
							return CustomSushiRules.ValidateNextPlacement(CurrentSushiType, BuildPlacedIngredientIdList(), text, SingleNoriSO_ID, CookedRiceSO_ID, IsCustomCompositionLocked) == CustomSushiPlacementRejection.None;
						}
						return false;
					}
					if (CurrentResolvedRecipeKind == SushiRecipeIdentityKind.SavedCustom)
					{
						if (!_isRecipeCompleted.Value && TryResolveRecipeDefinition(SelectedSushiID.ToString(), out var recipe) && _placedIngredientIdsNetwork.Count < recipe.Composition.Count)
						{
							return recipe.Composition.IngredientIds[_placedIngredientIdsNetwork.Count] == text;
						}
						return false;
					}
					List<BaseIngredientNeed> sortedNeeds = GetSortedNeeds();
					int num = ((CurrentState != ChefDeskState.Idle || !_isRecipeCompleted.Value) ? _currentRecipeStepIndex.Value : 0);
					if (sortedNeeds != null && (CurrentState == ChefDeskState.Idle || !_isRecipeCompleted.Value) && num < sortedNeeds.Count)
					{
						return sortedNeeds[num].IngredientId == text;
					}
					return false;
				}
			}
			return false;
		}

		internal bool ClientCanReturnIngredientToPan(ChefDeskGastroPan pan)
		{
			if (!(pan == null) && !StaffLocked && _placedIngredientIdsNetwork.Count != 0)
			{
				ChefDeskState currentState = CurrentState;
				if ((currentState == ChefDeskState.Idle || currentState == ChefDeskState.RecipeSelected) && (!IsCustomSushi || !IsCustomCompositionLocked))
				{
					string currentPanIngredientId = pan.CurrentIngredientID.ToString();
					string lastEmptiedPlacementIngredientId = pan.LastEmptiedPlacementIngredientId.ToString();
					int num = (IsCustomSushi ? CustomSushiRules.FindReturnableCustomIngredientIndex(BuildPlacedIngredientIdList(), currentPanIngredientId, lastEmptiedPlacementIngredientId) : (CustomSushiRules.CanReturnTopIngredient(BuildPlacedIngredientIdList(), currentPanIngredientId, lastEmptiedPlacementIngredientId) ? (_placedIngredientIdsNetwork.Count - 1) : (-1)));
					if (num < 0)
					{
						return false;
					}
					string id = _placedIngredientIdsNetwork[num].ToString();
					if (_itemDatabaseSO.FindById(id) is ProcessedIngredientSO processedIngredientSO)
					{
						return pan.IngredientCount < processedIngredientSO.MaxPanCapacity;
					}
					return false;
				}
			}
			return false;
		}

		[Rpc(SendTo.Server)]
		public void ServerRetrieveProductFromTableRpc(byte index, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1653697738u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in index, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1653697738u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			byte index2 = index;
			if (StaffLocked || !ServerIsActiveChefDeskUser(rpcParams.Receive.SenderClientId))
			{
				return;
			}
			ChefDeskGastroPan chefDeskGastroPan = _gastroPanList.FirstOrDefault((ChefDeskGastroPan x) => x.Index == index2);
			if (chefDeskGastroPan == null)
			{
				UtilityMethods.Log("<color=red>[ChefDesk - ServerRetrieveProductFromTableRpc] ChefDeskGastroPan not found.</color>");
				return;
			}
			if (_placedIngredientIdsNetwork.Count == 0)
			{
				ClientShowNotifyRpc("Info", "notification.chef.nothing_to_pick_up");
				UtilityMethods.Log("<color=yellow>[ChefDesk] No product on table.</color>");
				return;
			}
			ChefDeskState currentState = CurrentState;
			if (currentState != ChefDeskState.Idle && currentState != ChefDeskState.RecipeSelected)
			{
				return;
			}
			if (_isCustomSushi.Value && _isCustomCompositionLocked.Value)
			{
				ClientShowNotifyRpc("Info", "notification.chef.custom_composition_locked");
				return;
			}
			List<string> placedIngredientIds = BuildPlacedIngredientIdList();
			int num = (_isCustomSushi.Value ? CustomSushiRules.FindReturnableCustomIngredientIndex(placedIngredientIds, chefDeskGastroPan.CurrentIngredientID.ToString(), chefDeskGastroPan.LastEmptiedPlacementIngredientId.ToString()) : (CustomSushiRules.CanReturnTopIngredient(placedIngredientIds, chefDeskGastroPan.CurrentIngredientID.ToString(), chefDeskGastroPan.LastEmptiedPlacementIngredientId.ToString()) ? (_placedIngredientIdsNetwork.Count - 1) : (-1)));
			if (num >= 0)
			{
				FixedString64Bytes ingredientId = _placedIngredientIdsNetwork[num];
				if (!chefDeskGastroPan.ServerTryRestoreIngredientFromTable(ingredientId))
				{
					ClientShowNotifyRpc("Info", "notification.chef.gastropan_capacity_reached");
					return;
				}
				_placedIngredientIdsNetwork.RemoveAt(num);
				if (_isCustomSushi.Value)
				{
					_currentRecipeStepIndex.Value = (byte)_placedIngredientIdsNetwork.Count;
				}
				else if (CurrentResolvedRecipeKind == SushiRecipeIdentityKind.SavedCustom)
				{
					_currentRecipeStepIndex.Value = (byte)_placedIngredientIdsNetwork.Count;
					_isRecipeCompleted.Value = false;
					ServerCheckRecipeCompleted();
					ClientIngredientRetrievedRpc(ingredientId);
				}
				else
				{
					List<BaseIngredientNeed> sortedNeeds = GetSortedNeeds();
					if (sortedNeeds != null)
					{
						ApplyRecipeProgressFromPlacedIngredients(sortedNeeds);
						ServerCheckRecipeCompleted();
						ClientIngredientRetrievedRpc(ingredientId);
					}
				}
				chefDeskGastroPan.ClientRetrieveProductFromTableRpc(ingredientId, chefDeskGastroPan.IngredientCount, _isCustomSushi.Value ? num : (-1));
			}
			else
			{
				ClientShowNotifyRpc("Info", "notification.chef.top_ingredient_mismatch");
				UtilityMethods.Log("<color=yellow>[ChefDesk] Top item mismatch with gastropan ingredient.</color>");
			}
		}

		private void ServerCheckRecipeCompleted()
		{
			if (CurrentResolvedRecipeKind == SushiRecipeIdentityKind.SavedCustom)
			{
				if (TryResolveRecipeDefinition(SelectedSushiID.ToString(), out var recipe))
				{
					bool flag = _placedIngredientIdsNetwork.Count >= recipe.Composition.Count;
					_isRecipeCompleted.Value = flag;
					if (flag)
					{
						ClientHandleRecipeProgressRpc(default, recipeCompleted: true);
					}
					else
					{
						ClientHandleRecipeProgressRpc(recipe.Composition.IngredientIds[_placedIngredientIdsNetwork.Count], recipeCompleted: false);
					}
				}
				return;
			}
			List<BaseIngredientNeed> sortedNeeds = GetSortedNeeds();
			if (sortedNeeds == null)
			{
				UtilityMethods.Log("[ChefDesk- ClientUseEnter] SelectedSushiID is null or empty.");
			}
			else if (_currentRecipeStepIndex.Value >= sortedNeeds.Count)
			{
				_isRecipeCompleted.Value = true;
				ClientHandleRecipeProgressRpc(new FixedString64Bytes(""), recipeCompleted: true);
			}
			else
			{
				BaseIngredientNeed baseIngredientNeed = sortedNeeds[_currentRecipeStepIndex.Value];
				ClientHandleRecipeProgressRpc(new FixedString64Bytes(baseIngredientNeed.IngredientName), recipeCompleted: false);
			}
		}

		[Rpc(SendTo.Owner)]
		private void ClientIngredientRetrievedRpc(FixedString64Bytes ingredientId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(347136103u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in ingredientId, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 347136103u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_recipeInfoCanvas.ClientIngredientItemOnTableRemoved(ingredientId);
			}
		}

		private bool ServerPrepareSelectedRecipeForNextDish(List<BaseIngredientNeed> sortedNeeds)
		{
			SushiSO selectedSushi = GetSelectedSushi();
			if (selectedSushi == null || sortedNeeds == null || sortedNeeds.Count == 0)
			{
				ClientShowNotifyRpc("Info", "notification.chef.select_recipe_first");
				return false;
			}
			_currentSushiType.Value = (byte)selectedSushi.SushiType;
			_currentRecipeStepIndex.Value = 0;
			_currentIngredientPlacedCount = 0;
			_currentIngredientPlacedCountNetwork.Value = 0;
			_placedIngredientIdsNetwork.Clear();
			_isRecipeCompleted.Value = false;
			ServerSetStateRpc(ChefDeskState.RecipeSelected);
			_sushiRollController?.ServerPrepareRollSession(selectedSushi.SushiType);
			ClientInfoPanelPrepareRpc(_selectedSushiSO_ID.Value);
			ClientPrepareAssemblyLayoutRpc((byte)selectedSushi.SushiType, (ushort)Mathf.Clamp(GetInteriorIngredientVisualCount(selectedSushi), 0, 65535));
			return true;
		}

		[Rpc(SendTo.Owner)]
		private void ClientHandleRecipeProgressRpc(FixedString64Bytes nextIngredientName, bool recipeCompleted)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1967278592u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in nextIngredientName, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in recipeCompleted, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1967278592u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (recipeCompleted)
				{
					ClientHandleRecipeCompleted();
				}
				else
				{
					SendInstructionToOwnerRpc(ChefDeskInfoTextId.CompleteRecipe);
				}
			}
		}

		private void ClientHandleRecipeCompleted()
		{
			_recipeInfoCanvas.Hide();
			if (CurrentSushiType == SushiType.Nigiri)
			{
				ClientDelayDirectDishCompletion(ChefDeskFinalizeReason.NigiriCompleted);
			}
			else if (CurrentSushiType == SushiType.Sashimi)
			{
				ClientDelayDirectDishCompletion(ChefDeskFinalizeReason.SashimiCompleted);
			}
			else
			{
				ClientStartRollingPhase();
			}
		}

		private void ClientDelayDirectDishCompletion(ChefDeskFinalizeReason chefDeskFinalizeReason)
		{
			_directDishCompletionDelayTween?.Kill();
			_directDishCompletionDelayTween = null;
			float delay = Mathf.Max(0f, _nigiriCompletionDelay);
			_directDishCompletionDelayTween = DOVirtual.DelayedCall(delay, () =>
			{
				_directDishCompletionDelayTween = null;
				if (!(this == null) && base.isActiveAndEnabled)
				{
					SkipCuttingAndComplete(chefDeskFinalizeReason);
				}
			}).SetLink(base.gameObject, LinkBehaviour.KillOnDestroy);
		}

		private void SkipCuttingAndComplete(ChefDeskFinalizeReason chefDeskFinalizeReason)
		{
			ClientCompleteDishWithoutCutting(chefDeskFinalizeReason);
		}

		private void ClientStartRollingPhase()
		{
			ClientUpdateInstructionText(ChefDeskInfoTextId.RollSushi);
			_chefDeskSushiRollImage.enabled = true;
			if (_currentSushiType.Value == 1 || _currentSushiType.Value == 2)
			{
				_bottomToTopmouseIconTweenMover.SetActive(value: true);
			}
			else
			{
				_leftToRightmouseIconTweenMover.SetActive(value: true);
			}
			ServerSetStateRpc(ChefDeskState.WaitingForRoll);
		}

		[Rpc(SendTo.Everyone)]
		private void ClientIngredientPlacedRpc(FixedString64Bytes ingredientID)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2487486278u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in ingredientID, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 2487486278u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_recipeInfoCanvas.ClientIngredientItemOnTablePlaced(ingredientID);
			}
		}

		private List<BaseIngredientNeed> GetSortedNeeds()
		{
			if (!TryGetSortedNeeds(GetSelectedSushi(), out var sortedNeeds))
			{
				return null;
			}
			return sortedNeeds;
		}

		private bool TryResolveRecipeDefinition(string recipeId, out SushiRecipeDefinition recipe)
		{
			CustomSushiRecipeCatalog customCatalog = Singleton<GameProgressManager>.Instance?.RestaurantManager?.CustomSushiRecipeCatalog;
			return new SushiRecipeResolver(_itemDatabaseSO, customCatalog).TryResolveById(recipeId, out recipe);
		}

		private int GetInteriorIngredientVisualCount(SushiRecipeDefinition recipe)
		{
			if (recipe?.BuiltInSushi != null)
			{
				return GetInteriorIngredientVisualCount(recipe.BuiltInSushi);
			}
			return Mathf.Max(0, (recipe?.Composition?.Count).GetValueOrDefault() - 2);
		}

		private bool ServerTryPlaceSavedCustomRecipeIngredient(ChefDeskGastroPan gastroPan)
		{
			if (!base.IsServer || gastroPan == null || !TryResolveRecipeDefinition(SelectedSushiID.ToString(), out var recipe) || recipe.IdentityKind != SushiRecipeIdentityKind.SavedCustom)
			{
				ClientShowNotifyRpc("Info", "notification.chef.select_recipe_first");
				return false;
			}
			if (gastroPan.IngredientCount <= 0)
			{
				ClientShowNotifyRpc("Info", "notification.chef.gastropan_empty");
				return false;
			}
			int count = _placedIngredientIdsNetwork.Count;
			if (_isRecipeCompleted.Value || count >= recipe.Composition.Count)
			{
				ClientShowNotifyRpc("Info", "notification.chef.recipe_completed");
				return false;
			}
			string text = recipe.Composition.IngredientIds[count];
			if (!string.Equals(text, gastroPan.CurrentIngredientID.ToString(), StringComparison.Ordinal))
			{
				ClientShowNotifyRpc("Info", "notification.chef.wrong_recipe_ingredient");
				return false;
			}
			if (!gastroPan.ServerTryConsumeForPlacement())
			{
				ClientShowNotifyRpc("Info", "notification.chef.gastropan_empty");
				return false;
			}
			_placedIngredientIdsNetwork.Add(text);
			_currentRecipeStepIndex.Value = (byte)_placedIngredientIdsNetwork.Count;
			_currentIngredientPlacedCount = 0;
			_currentIngredientPlacedCountNetwork.Value = 0;
			ClientIngredientPlacedRpc(text);
			if (CurrentState == ChefDeskState.Idle)
			{
				ServerSetStateRpc(ChefDeskState.RecipeSelected);
			}
			ServerCheckRecipeCompleted();
			return true;
		}

		private static bool TryGetSortedNeeds(SushiSO sushiSO, out List<BaseIngredientNeed> sortedNeeds)
		{
			sortedNeeds = null;
			if (sushiSO == null || sushiSO.Recipe == null)
			{
				return false;
			}
			List<BaseIngredientNeed> list = new List<BaseIngredientNeed>();
			if (sushiSO.Recipe.Ingredients != null)
			{
				list.AddRange(sushiSO.Recipe.Ingredients);
			}
			if (sushiSO.Recipe.ProcessedIngredients != null)
			{
				list.AddRange(sushiSO.Recipe.ProcessedIngredients);
			}
			sortedNeeds = list.OrderBy((BaseIngredientNeed x) => x.Order).ToList();
			return sortedNeeds.Count > 0;
		}

		public bool ClientTryDebugFillSelectedRecipeGastroPans(out string message)
		{
			if (!base.IsSpawned)
			{
				message = "The chef desk is not network-spawned.";
				return false;
			}
			if (!base.IsOwner || !InUse.Value)
			{
				message = "You must be the active chef desk user.";
				return false;
			}
			SushiSO selectedSushi = GetSelectedSushi();
			if (selectedSushi == null)
			{
				message = "Select a recipe first.";
				return false;
			}
			ServerDebugFillSelectedRecipeGastroPansRpc();
			message = "Filling chef desk gastropans for recipe '" + selectedSushi.Name + "'.";
			return true;
		}

		[Rpc(SendTo.Server)]
		private void ServerDebugFillSelectedRecipeGastroPansRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3078435841u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3078435841u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (StaffLocked)
			{
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
			if (!InUse.Value || senderClientId != _serverCurrentUserClientId)
			{
				UtilityMethods.Log($"[ChefDesk DebugFill] Rejected. SenderClientId={senderClientId}, ActiveClientId={_serverCurrentUserClientId}, InUse={InUse.Value}", LogType.Warning);
				ClientShowNotifyRpc("Info", "notification.chef.debug_active_user_required", baseRpcTarget);
				return;
			}
			SushiSO selectedSushi = GetSelectedSushi();
			List<BaseIngredientNeed> sortedNeeds = GetSortedNeeds();
			if (selectedSushi == null || sortedNeeds == null || sortedNeeds.Count == 0)
			{
				ClientShowNotifyRpc("Info", "notification.chef.debug_select_recipe_first", baseRpcTarget);
				return;
			}
			List<ProcessedIngredientSO> list = new List<ProcessedIngredientSO>();
			HashSet<string> hashSet = new HashSet<string>();
			for (int i = 0; i < sortedNeeds.Count; i++)
			{
				string text = sortedNeeds[i]?.IngredientId;
				if (!string.IsNullOrWhiteSpace(text) && hashSet.Add(text))
				{
					if (!(_itemDatabaseSO.FindById(text) is ProcessedIngredientSO { MaxPanCapacity: >0 } processedIngredientSO))
					{
						UtilityMethods.Log("[ChefDesk DebugFill] Ingredient is not gastropan-compatible. SushiId=" + selectedSushi.Id + ", IngredientId=" + text, LogType.Warning);
						ClientShowNotifyRpc("Info", "notification.chef.debug_ingredient_incompatible", baseRpcTarget);
						return;
					}
					list.Add(processedIngredientSO);
				}
			}
			List<ChefDeskGastroPan> list2 = _gastroPanList.Where((ChefDeskGastroPan pan) => pan != null && pan.IsSpawned).ToList();
			if (list.Count == 0 || list.Count > list2.Count)
			{
				UtilityMethods.Log($"[ChefDesk DebugFill] Not enough gastropans. SushiId={selectedSushi.Id}, IngredientCount={list.Count}, PanCount={list2.Count}", LogType.Warning);
				ClientShowNotifyRpc("Info", "notification.chef.debug_not_enough_pans", baseRpcTarget);
				return;
			}
			for (int num = 0; num < list.Count; num++)
			{
				list2[num].ServerDebugFillToCapacity(list[num]);
			}
			for (int num2 = list.Count; num2 < list2.Count; num2++)
			{
				list2[num2].ServerResetPan();
			}
			UtilityMethods.Log($"[ChefDesk DebugFill] Completed. ClientId={senderClientId}, SushiId={selectedSushi.Id}, FilledPanCount={list.Count}");
			ClientShowNotifyRpc("Info", "notification.chef.debug_pans_filled", baseRpcTarget);
		}

		[Rpc(SendTo.Server)]
		public void ServerChefDeskResetRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1546633182u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1546633182u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ServerTryResetChefDesk();
			}
		}

		private bool ServerTryResetChefDesk(bool showSuccessToast = true)
		{
			if (StaffLocked || !base.IsServer)
			{
				return false;
			}
			if (!ServerReturnTableObjectsToPansBeforeReset(out var usedEmptyPan))
			{
				ClientShowNotifyRpc("Info", "notification.chef.table_return_blocked");
				return false;
			}
			bool value = _isCustomSushi.Value;
			bool flag = ServerRepairServedDishDeskStateForReset();
			_currentState.Value = (byte)(flag ? 4 : 0);
			_currentRecipeStepIndex.Value = 0;
			_isRecipeCompleted.Value = true;
			_currentIngredientPlacedCount = 0;
			_currentIngredientPlacedCountNetwork.Value = 0;
			_placedIngredientIdsNetwork.Clear();
			_isCustomCompositionLocked.Value = false;
			_resolvedCustomRecipeKind.Value = 2;
			if (value)
			{
				_isCustomSushi.Value = false;
				_currentSushiType.Value = 0;
				_selectedSushiSO_ID.Value = default;
				_savableCustomSushi.Value = default;
			}
			if (usedEmptyPan)
			{
				ClientShowNotifyRpc("Info", "notification.chef.table_returned_to_empty_pan");
			}
			else if (showSuccessToast)
			{
				ClientShowNotifyRpc("Info", "notification.chef.table_reset_success");
			}
			SendInstructionToOwnerRpc(flag ? ChefDeskInfoTextId.MovePlateOrSeason : (SelectedSushiID.IsEmpty ? ChefDeskInfoTextId.SelectRecipe : ChefDeskInfoTextId.CompleteRecipe));
			CuttingResetRpc();
			ClientChefDeskResetRpc();
			return true;
		}

		public void ServerResetForEndDay()
		{
			if (StaffLocked || !base.IsServer)
			{
				return;
			}
			if (!ServerReturnTableObjectsToPansBeforeReset(out var usedEmptyPan))
			{
				UtilityMethods.Log("[ChefDesk] End-day reset preserved the table because its ingredients cannot fit in the gastropans.", LogType.Warning);
				if (_serverCurrentUserClientId != ulong.MaxValue)
				{
					ClientShowNotifyRpc("Info", "notification.chef.table_return_blocked");
				}
				_serverCurrentUserClientId = ulong.MaxValue;
				InUse.Value = false;
				CanGaze.Value = true;
				if (base.NetworkObject != null)
				{
					base.NetworkObject.RemoveOwnership();
				}
				ClientChefDeskEndDayPreserveAssemblyRpc();
				return;
			}
			if (usedEmptyPan && _serverCurrentUserClientId != ulong.MaxValue)
			{
				ClientShowNotifyRpc("Info", "notification.chef.table_returned_to_empty_pan");
			}
			bool flag = ServerRepairServedDishDeskStateForReset();
			_currentState.Value = (byte)(flag ? 4 : 0);
			_currentRecipeStepIndex.Value = 0;
			_isRecipeCompleted.Value = true;
			_isCustomSushi.Value = false;
			_isCustomCompositionLocked.Value = false;
			_resolvedCustomRecipeKind.Value = 2;
			_currentSushiType.Value = 0;
			_selectedSushiSO_ID.Value = default;
			_savableCustomSushi.Value = default;
			_currentIngredientPlacedCount = 0;
			_ingredientSpawnIndex = 0;
			_nigiriRiceSpawnIndex = 0;
			_nigiriMeatSpawnIndex = 0;
			_serverCurrentUserClientId = ulong.MaxValue;
			InUse.Value = false;
			CanGaze.Value = true;
			if (base.NetworkObject != null)
			{
				base.NetworkObject.RemoveOwnership();
			}
			CuttingResetRpc();
			ClientChefDeskEndDayResetRpc(flag);
		}

		[Rpc(SendTo.Everyone)]
		private void ClientChefDeskEndDayPreserveAssemblyRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(480021319u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 480021319u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientResetCuttingSessionOnly();
				ClientForceExitUseModeForReset(centerDishPreserved: false);
			}
		}

		private bool ServerReturnTableObjectsToPansBeforeReset(out bool usedEmptyPan)
		{
			usedEmptyPan = false;
			if (_placedIngredientIdsNetwork.Count == 0)
			{
				return true;
			}
			if (!ServerCanReturnAllTableIngredientsToPans())
			{
				return false;
			}
			for (int num = _placedIngredientIdsNetwork.Count - 1; num >= 0; num--)
			{
				FixedString64Bytes fixedString64Bytes = _placedIngredientIdsNetwork[num];
				if (!ServerTryRestoreTableIngredientToPan(fixedString64Bytes, out var usedEmptyPan2))
				{
					UtilityMethods.Log($"[ChefDesk] Pan restore failed after capacity validation. IngredientId={fixedString64Bytes}", LogType.Error);
					return false;
				}
				usedEmptyPan |= usedEmptyPan2;
			}
			return true;
		}

		private bool ServerCanReturnAllTableIngredientsToPans()
		{
			if (_gastroPanList == null || _itemDatabaseSO == null)
			{
				return false;
			}
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			Dictionary<string, int> dictionary2 = new Dictionary<string, int>();
			for (int i = 0; i < _placedIngredientIdsNetwork.Count; i++)
			{
				string text = _placedIngredientIdsNetwork[i].ToString();
				if (!(_itemDatabaseSO.FindById(text) is ProcessedIngredientSO { MaxPanCapacity: >0 } processedIngredientSO))
				{
					return false;
				}
				dictionary.TryGetValue(text, out var value);
				dictionary[text] = value + 1;
				dictionary2[text] = processedIngredientSO.MaxPanCapacity;
			}
			List<string> list = new List<string>(_gastroPanList.Count);
			List<int> list2 = new List<int>(_gastroPanList.Count);
			foreach (ChefDeskGastroPan gastroPan in _gastroPanList)
			{
				if (!(gastroPan == null))
				{
					list.Add(gastroPan.CurrentIngredientID.ToString());
					list2.Add(gastroPan.IngredientCount);
				}
			}
			return CanReturnAllTableIngredients(dictionary, dictionary2, list, list2);
		}

		private static bool CanReturnAllTableIngredients(IReadOnlyDictionary<string, int> pending, IReadOnlyDictionary<string, int> capacities, IReadOnlyList<string> panIds, IReadOnlyList<int> panCounts)
		{
			if (pending == null || capacities == null || panIds == null || panCounts == null || panIds.Count != panCounts.Count)
			{
				return false;
			}
			int num = 0;
			for (int i = 0; i < panCounts.Count; i++)
			{
				if (panCounts[i] == 0)
				{
					num++;
				}
			}
			foreach (KeyValuePair<string, int> item in pending)
			{
				if (!capacities.TryGetValue(item.Key, out var value) || value <= 0)
				{
					return false;
				}
				int num2 = item.Value;
				for (int j = 0; j < panCounts.Count; j++)
				{
					if (panCounts[j] > 0 && panIds[j] == item.Key)
					{
						num2 -= Math.Max(0, value - panCounts[j]);
					}
				}
				if (num2 > 0)
				{
					num -= (num2 + value - 1) / value;
					if (num < 0)
					{
						return false;
					}
				}
			}
			return true;
		}

		private bool ServerTryRestoreTableIngredientToPan(FixedString64Bytes ingredientId, out bool usedEmptyPan)
		{
			usedEmptyPan = false;
			if (_gastroPanList == null)
			{
				return false;
			}
			for (int i = 0; i < _gastroPanList.Count; i++)
			{
				ChefDeskGastroPan chefDeskGastroPan = _gastroPanList[i];
				if (!(chefDeskGastroPan == null) && !(chefDeskGastroPan.CurrentIngredientID != ingredientId))
				{
					bool flag = chefDeskGastroPan.IngredientCount == 0;
					if (chefDeskGastroPan.ServerTryRestoreIngredientFromTable(ingredientId))
					{
						usedEmptyPan = flag;
						return true;
					}
				}
			}
			for (int j = 0; j < _gastroPanList.Count; j++)
			{
				ChefDeskGastroPan chefDeskGastroPan2 = _gastroPanList[j];
				if (!(chefDeskGastroPan2 == null) && !(chefDeskGastroPan2.CurrentIngredientID == ingredientId) && chefDeskGastroPan2.ServerTryRestoreIngredientFromTable(ingredientId))
				{
					usedEmptyPan = true;
					return true;
				}
			}
			return false;
		}

		private bool ServerRepairServedDishDeskStateForReset()
		{
			List<ServedDishSushi> list = ServerGetValidServedDishesOnDesk();
			for (int i = 0; i < _edgeOccupied.Count; i++)
			{
				_edgeOccupied[i] = false;
			}
			_centerOccupied.Value = false;
			_lastSpawnedSushi = default;
			ServedDishSushi servedDishSushi = null;
			for (int j = 0; j < list.Count; j++)
			{
				ServedDishSushi servedDishSushi2 = list[j];
				int assignedEdgeIndex = servedDishSushi2.AssignedEdgeIndex;
				if (assignedEdgeIndex >= 0 && assignedEdgeIndex < _edgeOccupied.Count && !_edgeOccupied[assignedEdgeIndex])
				{
					_edgeOccupied[assignedEdgeIndex] = true;
				}
				else if (servedDishSushi == null)
				{
					servedDishSushi = servedDishSushi2;
				}
				else
				{
					ServerMoveDishToFreeEdgeIfPossible(servedDishSushi2);
				}
			}
			if (servedDishSushi == null)
			{
				return false;
			}
			if (ServerMoveDishToFreeEdgeIfPossible(servedDishSushi))
			{
				return false;
			}
			servedDishSushi.SetAssignedEdgeIndexServer(-1);
			_lastSpawnedSushi = servedDishSushi.NetworkObject;
			_centerOccupied.Value = true;
			return true;
		}

		private List<ServedDishSushi> ServerGetValidServedDishesOnDesk()
		{
			List<ServedDishSushi> list = new List<ServedDishSushi>();
			for (int num = _servedDishesOnDesk.Count - 1; num >= 0; num--)
			{
				ServedDishSushi component;
				if (!_servedDishesOnDesk[num].TryGet(out var networkObject) || networkObject == null)
				{
					_servedDishesOnDesk.RemoveAt(num);
				}
				else if (!networkObject.TryGetComponent<ServedDishSushi>(out component) || component == null)
				{
					_servedDishesOnDesk.RemoveAt(num);
				}
				else if (!component.IsSpawned || component.IsStaffCarried || component.IsPlayerInventoryOwnedForPersistence || component.IsConveryorBelt || !component.ClientBelongsToChefDesk(this) || component.AssignedEdgeIndex < -1 || (component.transform.parent != null && !component.transform.IsChildOf(base.transform)))
				{
					_servedDishesOnDesk.RemoveAt(num);
					if (component.IsSpawned && component.ClientBelongsToChefDesk(this))
					{
						component.SetAssignedEdgeIndexServer(-2);
						component.ServerAssignChefDesk(null);
					}
				}
				else
				{
					list.Add(component);
				}
			}
			return list;
		}

		private bool ServerMoveDishToFreeEdgeIfPossible(ServedDishSushi dish)
		{
			if (dish == null || dish.NetworkObject == null)
			{
				return false;
			}
			sbyte b = ServerFindFirstFreeEdgeIndex();
			if (b < 0)
			{
				return false;
			}
			_edgeOccupied[b] = true;
			dish.SetAssignedEdgeIndexServer(b);
			ClientMoveSushiToEdgeRpc(dish.NetworkObject, (byte)b);
			return true;
		}

		[Rpc(SendTo.Everyone)]
		private void ClientChefDeskResetRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1816838245u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1816838245u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				KillCutFlowDelay();
				ClearSelection();
				ClientClearGastropanHoldPlacement();
				ClientClearDeskHoverState();
				ClientResetCustomSushiUI();
				_isCenteringAssembly = false;
				_cuttingManager.SetCuttingViewActive(isActive: false);
				_isKnifeHeld = false;
				_recipeInfoCanvas.gameObject.SetActive(value: false);
				_successfulCutCount = 0;
				_requiredCutCount = 0;
				_knifeModel.gameObject.SetActive(value: false);
				_chefDeskSushiRollImage.enabled = false;
				_bottomToTopmouseIconTweenMover.SetActive(value: false);
				_leftToRightmouseIconTweenMover.SetActive(value: false);
				_outCanvasGO.SetActive(value: true);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientChefDeskEndDayResetRpc(bool centerDishPreserved)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2749113648u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in centerDishPreserved, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2749113648u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientChefDeskResetRpc();
				ClientForceExitUseModeForReset(centerDishPreserved);
			}
		}

		private void ClientForceExitUseModeForReset(bool centerDishPreserved)
		{
			KillCutFlowDelay();
			ClearSelection();
			ClientClearGastropanHoldPlacement();
			ClientClearDeskHoverState();
			RestoreHiddenHoldableForChefDesk();
			ShowCursorForLocalChefDeskUser();
			if (_chefDeskBookCanvas != null)
			{
				if (_chefDeskBookCanvas.DeskResetCanvasGO != null)
				{
					_chefDeskBookCanvas.DeskResetCanvasGO.SetActive(value: false);
				}
				if (_chefDeskBookCanvas.isActiveAndEnabled)
				{
					_chefDeskBookCanvas.ToggleBookUI(isVisible: false);
				}
			}
			if (_stationTutorialPopupUI != null && _stationTutorialPopupUI.isActiveAndEnabled)
			{
				_stationTutorialPopupUI.Close();
			}
			if (_cuttingManager != null && _cuttingManager.CutPointUI != null)
			{
				_cuttingManager.CutPointUI.gameObject.SetActive(value: false);
			}
			if (_currentInteractor != null && _currentInteractor.Player != null)
			{
				if (_currentInteractor.Player.CharacterVisuals != null && _currentInteractor.Player.CharacterVisuals.FpsRootObject != null)
				{
					UtilityMethods.SetLayerRecursive(_currentInteractor.Player.CharacterVisuals.FpsRootObject, "ViewModel", includeChildren: true);
				}
				_currentInteractor.SetInteractorStatus(status: true);
				_currentInteractor.Player.ResumeCharacter();
				_currentInteractor.Player.ForKitchenUsable(value: true);
				_currentInteractor.Player.SwitchTo("Player", cursorVisible: false);
			}
			_currentInteractor = null;
			if (Singleton<InputManager>.Instance != null && Singleton<InputManager>.Instance.InputHandler != null)
			{
				Singleton<InputManager>.Instance.InputHandler.ChefDeskExit -= OnChefDeskExit;
				Singleton<InputManager>.Instance.InputHandler.KnifeAction -= OnKnifeAction;
			}
			if (Singleton<QuickSlotHandler>.Instance != null)
			{
				Singleton<QuickSlotHandler>.Instance.gameObject.SetActive(value: true);
			}
			if (Singleton<GameUIManager>.Instance != null && Singleton<GameUIManager>.Instance.TimeUI != null)
			{
				Singleton<GameUIManager>.Instance.TimeUI.SetVisibility(status: true);
			}
			EventBus<DeskCam>.Raise(new DeskCam
			{
				CinemachineBrain = null
			});
			_sushiRollCanvas.enabled = true;
			_inCanvasGO.SetActive(value: false);
			_outCanvasGO.SetActive(value: true);
			if (_gastroPanList != null)
			{
				foreach (ChefDeskGastroPan gastroPan in _gastroPanList)
				{
					if (gastroPan != null && gastroPan.GastropanInteractor != null)
					{
						gastroPan.GastropanInteractor.ToolTipGO_01.SetActive(value: false);
					}
				}
			}
			if (!centerDishPreserved)
			{
				ClientUpdateInstructionText(ChefDeskInfoTextId.SelectRecipe);
			}
			else
			{
				ClientUpdateInstructionText(ChefDeskInfoTextId.MovePlateOrSeason);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void CuttingResetRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(196734092u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 196734092u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ClientResetRollMeshCutSession();
			if (base.IsServer)
			{
				ServerResetTemporaryCutPieceSpawnCount();
				_sushiRollController?.ServerResetRollSession();
			}
			ClientClearTemporaryCutPieces();
			if (_visualMap != null)
			{
				foreach (KeyValuePair<SushiType, SushiVisualSet> item in _visualMap)
				{
					SushiVisualSet value = item.Value;
					if (value != null)
					{
						if (value.SingleNoriOBJ != null)
						{
							value.SingleNoriOBJ.transform.localPosition = value.DefaultNoriPosition;
						}
						if (value.SushiRiceOBJ != null)
						{
							value.SushiRiceOBJ.transform.localPosition = value.DefaultRicePosition;
						}
						if (value.SingleNoriBend != null)
						{
							value.SingleNoriBend.localPosition = Vector3.zero;
						}
						if (value.SushiRiceBend != null)
						{
							value.SushiRiceBend.localPosition = Vector3.zero;
						}
					}
				}
			}
			_sushiRollController?.ClientResetRollPresentation();
			if (_visualMap != null)
			{
				foreach (KeyValuePair<SushiType, SushiVisualSet> item2 in _visualMap)
				{
					SushiVisualSet value2 = item2.Value;
					if (value2 != null)
					{
						if (value2.SingleNoriOBJ != null)
						{
							value2.SingleNoriOBJ.SetActive(value: false);
						}
						if (value2.SushiRiceOBJ != null)
						{
							value2.SushiRiceOBJ.SetActive(value: false);
						}
					}
				}
			}
			ClientResetAssemblyAnchorTransforms();
			_successfulCutCount = 0;
			_ingredientSpawnIndex = 0;
			_nigiriRiceSpawnIndex = 0;
			_nigiriMeatSpawnIndex = 0;
			_currentIngredientPlacedCount = 0;
			if (base.IsServer)
			{
				_currentIngredientPlacedCountNetwork.Value = 0;
				_placedIngredientIdsNetwork.Clear();
			}
			ClientClearObjectsOnTable();
			_recipeInfoCanvas.ClientResetPlacedProgress();
		}

		private void ClientClearObjectsOnTable()
		{
			foreach (TableObjectData item in ObjectsOnTable)
			{
				if (item.Obj != null)
				{
					UnityEngine.Object.Destroy(item.Obj);
				}
			}
			ObjectsOnTable.Clear();
			ClientClearAssemblyLayout();
			UtilityMethods.Log("[ChefDesk - ClientClearObjectsOnTable] ObjectsOnTable temizlendi.");
		}

		[Rpc(SendTo.Owner)]
		private void ClientCuttingStateResetRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(2554599794u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2554599794u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientResetCuttingSessionOnly();
			}
		}

		private void ClientResetCuttingSessionOnly()
		{
			KillCutFlowDelay();
			ClientResetRollMeshCutSession();
			ClientClearTemporaryCutPieces();
			_localCuttingActive = false;
			_pendingCuttingStartAfterCentering = false;
			_cuttingManager.SetCuttingViewActive(isActive: false);
			_isKnifeHeld = false;
			_mustExitCutPoint = false;
			_successfulCutCount = 0;
			_requiredCutCount = 0;
			_hasLastKnifeDirection = false;
			_lastKnifeDirection = Vector3.zero;
			_currentTiltAngleY = 0f;
			if (_knifePressRoot != null)
			{
				_knifePressRoot.localEulerAngles = Vector3.zero;
			}
			ShowCursorForLocalChefDeskUser();
			if (_knifeModel != null)
			{
				_knifeModel.gameObject.SetActive(value: false);
			}
			ResetCutProgressUI(deactivateParent: true);
		}

		public Transform GetNextTableSpawnPoint()
		{
			return ClientGetNextAssemblyIngredientAnchor();
		}

		public Transform GetNextNigiriSpawnPoint(string ingredientId)
		{
			bool flag = ingredientId == CookedRiceSO_ID;
			List<Transform> list = (flag ? NigiriRiceIngredientSlots : NigiriMeatIngredientSlots);
			if (list == null || list.Count == 0)
			{
				return null;
			}
			ref int reference = ref flag ? ref _nigiriRiceSpawnIndex : ref _nigiriMeatSpawnIndex;
			Transform result = list[reference];
			reference = (reference + 1) % list.Count;
			return result;
		}

		public Transform GetNextTemakiSpawnPoint()
		{
			return ClientGetNextAssemblyIngredientAnchor();
		}

		public Transform GetNextSashimiSpawnPoint()
		{
			if (SashimiIngredientSlots == null || SashimiIngredientSlots.Count == 0)
			{
				return null;
			}
			Transform result = SashimiIngredientSlots[_ingredientSpawnIndex];
			_ingredientSpawnIndex = (_ingredientSpawnIndex + 1) % SashimiIngredientSlots.Count;
			return result;
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestFinalizePreparedSushiRpc(ChefDeskFinalizeReason reason, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2320784291u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in reason, default(FastBufferWriter.ForEnums));
				__endSendRpc(ref bufferWriter, 2320784291u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!StaffLocked)
			{
				if (ServerTryFinalizePreparedSushi(rpcParams.Receive.SenderClientId, reason))
				{
					CuttingResetRpc();
				}
				else if (ServerIsActiveChefDeskUser(rpcParams.Receive.SenderClientId) && !ServerTryResetChefDesk(showSuccessToast: false))
				{
					ServerClearTemporaryCutPieceSessionForAllClients();
					ClientCuttingStateResetRpc();
				}
			}
		}

		private bool ServerTryFinalizePreparedSushi(ulong senderClientId, ChefDeskFinalizeReason reason)
		{
			if (!base.IsServer)
			{
				return false;
			}
			SushiSO selectedSushi = GetSelectedSushi();
			bool flag = CurrentResolvedRecipeKind == SushiRecipeIdentityKind.SavedCustom || (IsCustomSushi && CurrentResolvedRecipeKind == SushiRecipeIdentityKind.UnsavedCustom);
			string text = ((selectedSushi != null) ? selectedSushi.Id : (SelectedSushiID.IsEmpty ? "UnsavedCustom" : SelectedSushiID.ToString()));
			if (!flag && selectedSushi == null)
			{
				UtilityMethods.Log($"[ChefDesk Finalize] Blocked. Selected sushi is invalid. SenderClientId={senderClientId}, Reason={reason}, State={CurrentState}");
				ClientShowNotifyRpc("Info", "notification.chef.sushi_completion_failed");
				return false;
			}
			if (_serverCurrentUserClientId == ulong.MaxValue)
			{
				UtilityMethods.Log($"[ChefDesk Finalize] Blocked. No active user. SenderClientId={senderClientId}, Reason={reason}, SushiId={text}, State={CurrentState}");
				ClientShowNotifyRpc("Info", "notification.chef.no_active_user_to_complete");
				return false;
			}
			if (senderClientId != _serverCurrentUserClientId)
			{
				UtilityMethods.Log($"[ChefDesk Finalize] Blocked. Sender is not active user. SenderClientId={senderClientId}, ActiveClientId={_serverCurrentUserClientId}, Reason={reason}, SushiId={text}, State={CurrentState}");
				return false;
			}
			if (!_isRecipeCompleted.Value)
			{
				UtilityMethods.Log($"[ChefDesk Finalize] Blocked. Recipe is not completed. ClientId={senderClientId}, Reason={reason}, SushiId={text}, State={CurrentState}");
				ClientShowNotifyRpc("Info", "notification.chef.recipe_incomplete");
				return false;
			}
			if (!ServerCanFinalizeFromCurrentState(reason))
			{
				UtilityMethods.Log($"[ChefDesk Finalize] Blocked. Invalid state for finalize. ClientId={senderClientId}, Reason={reason}, SushiId={text}, State={CurrentState}, SushiType={CurrentSushiType}");
				ClientShowNotifyRpc("Info", "notification.chef.sushi_completion_failed");
				return false;
			}
			if (_centerOccupied.Value)
			{
				UtilityMethods.Log($"[ChefDesk Finalize] Blocked. Center is already occupied. ClientId={senderClientId}, Reason={reason}, SushiId={text}, State={CurrentState}");
				ClientShowNotifyRpc("Info", "notification.chef.clear_center_to_make_sushi");
				return false;
			}
			if (!(flag ? ServerSpawnFinalizedCustomSushi(BuildCurrentCustomComposition(), _serverCurrentUserClientId) : ServerSpawnFinalizedSushi(selectedSushi, _serverCurrentUserClientId)))
			{
				ClientShowNotifyRpc("Info", "notification.chef.sushi_completion_failed");
				return false;
			}
			if (IsCustomSushi)
			{
				bool flag2 = CurrentResolvedRecipeKind != SushiRecipeIdentityKind.UnsavedCustom;
				ClientHandleCustomSushiSpawnedRpc();
				ClientShowNotifyRpc("Info", flag2 ? "notification.chef.custom_sushi_spawned_existing" : "notification.chef.custom_sushi_spawned");
			}
			_placedIngredientIdsNetwork.Clear();
			_currentIngredientPlacedCountNetwork.Value = 0;
			ServerSetStateRpc(ChefDeskState.TransferToEdge);
			SendInstructionToOwnerRpc(ChefDeskInfoTextId.MovePlateOrSeason);
			return true;
		}

		private bool ServerCanFinalizeFromCurrentState(ChefDeskFinalizeReason reason)
		{
			if (CurrentState == ChefDeskState.Cutting)
			{
				return reason == ChefDeskFinalizeReason.CuttingCompleted;
			}
			return reason switch
			{
				ChefDeskFinalizeReason.NigiriCompleted => CurrentSushiType == SushiType.Nigiri && CurrentState == ChefDeskState.RecipeSelected, 
				ChefDeskFinalizeReason.SashimiCompleted => CurrentSushiType == SushiType.Sashimi && CurrentState == ChefDeskState.RecipeSelected, 
				ChefDeskFinalizeReason.TemakiCompleted => CurrentSushiType == SushiType.Temaki && CurrentState == ChefDeskState.Centering, 
				_ => false, 
			};
		}

		private bool ServerSpawnFinalizedSushi(SushiSO sushiSO, ulong targetClientId)
		{
			if (sushiSO?.Prefab == null)
			{
				return false;
			}
			NetworkObject networkObject = UnityEngine.Object.Instantiate(sushiSO.Prefab, _centerPoint);
			networkObject.Spawn(destroyWithScene: true);
			_lastSpawnedSushi = networkObject;
			_centerOccupied.Value = true;
			if (networkObject.TryGetComponent<ServedDishSushi>(out var component))
			{
				component.ServerAssignChefDesk(this);
			}
			ServerRegisterServedDish(networkObject);
			ServerSetDishCollidersForClient(networkObject, targetClientId, isTrigger: false);
			ServerPlayFinalizedSushiSpawnFeedback();
			RestaurantManager restaurantManager = Singleton<GameProgressManager>.Instance.RestaurantManager;
			foreach (ProcessedIngredientNeeds processedIngredient in sushiSO.Recipe.ProcessedIngredients)
			{
				restaurantManager.ServerRemoveStockRpc(processedIngredient.IngredientId, processedIngredient.Amount);
			}
			foreach (IngredientNeeds ingredient in sushiSO.Recipe.Ingredients)
			{
				restaurantManager.ServerRemoveStockRpc(ingredient.IngredientId, ingredient.Amount);
			}
			ServerRecordDailyReportSushiMade(targetClientId, sushiSO.Id);
			Singleton<GameProgressManager>.Instance.ServerGrantProgressionXp(ProgressionXpSource.SushiPrepared, sushiSO.Id, targetClientId);
			Singleton<GameProgressManager>.Instance.RpcRelay?.ServerUnlockSteamAchievementForClient("ACH_FIRST_SUSHI_MADE", targetClientId);
			if (sushiSO.Id == _itemDatabaseSO.CucumberMakiServeredDishSO.Id)
			{
				QuestReferenceHub.Instance.AddProgress(QuestId.MakeCucumberMaki);
			}
			else if (sushiSO.Id == _itemDatabaseSO.MackarelNigiriServeredDishSO.Id)
			{
				QuestReferenceHub.Instance.AddProgress(QuestId.MakeMackerelNigiri);
			}
			else if (sushiSO.Id == _itemDatabaseSO.ChinookSalmonTemakiServeredDishSO.Id)
			{
				QuestReferenceHub.Instance.AddProgress(QuestId.MakeAChinookSalmonDish);
			}
			else if (sushiSO.Id == _itemDatabaseSO.SquidNigiriServeredDishSO.Id)
			{
				QuestReferenceHub.Instance.AddProgress(QuestId.MakeSquidNigiri);
			}
			return true;
		}

		private bool ServerSpawnFinalizedCustomSushi(SushiCompositionData composition, ulong targetClientId)
		{
			if (!CustomSushiRules.HasValidBase(composition?.SushiType ?? SushiType.Empty, composition?.IngredientIds, SingleNoriSO_ID, CookedRiceSO_ID))
			{
				return false;
			}
			NetworkObject customPhysicalPrefab = GetCustomPhysicalPrefab(composition.SushiType);
			if (customPhysicalPrefab == null)
			{
				UtilityMethods.Log($"[ChefDesk Custom] Generic physical prefab is not assigned. SushiType={composition.SushiType}", LogType.Error);
				return false;
			}
			NetworkObject networkObject = UnityEngine.Object.Instantiate(customPhysicalPrefab, _centerPoint);
			if (!networkObject.TryGetComponent<ServedDishSushi>(out var component))
			{
				UnityEngine.Object.Destroy(networkObject.gameObject);
				UtilityMethods.Log("[ChefDesk Custom] Generic prefab requires ServedDishSushi.", LogType.Error);
				return false;
			}
			string text = string.Empty;
			string displayName = "Custom Sushi";
			int price = ServerCalculateCustomSushiPrice(composition);
			SushiRecipeIdentityKind currentResolvedRecipeKind = CurrentResolvedRecipeKind;
			if (currentResolvedRecipeKind == SushiRecipeIdentityKind.SavedCustom)
			{
				CustomSushiRecipeData customSushiRecipeData = Singleton<GameProgressManager>.Instance?.RestaurantManager?.CustomSushiRecipeCatalog.FindById(SelectedSushiID.ToString());
				if (customSushiRecipeData == null || !SushiCompositionComparer.Equals(composition, customSushiRecipeData.Composition))
				{
					UnityEngine.Object.Destroy(networkObject.gameObject);
					return false;
				}
				text = customSushiRecipeData.RecipeId;
				displayName = customSushiRecipeData.DisplayName;
				price = customSushiRecipeData.Price;
			}
			if (!component.ServerInitializeCustomSushi(composition, currentResolvedRecipeKind, text, displayName, price))
			{
				UnityEngine.Object.Destroy(networkObject.gameObject);
				return false;
			}
			networkObject.Spawn(destroyWithScene: true);
			if (currentResolvedRecipeKind == SushiRecipeIdentityKind.UnsavedCustom)
			{
				ServerSetSavableCustomSushi(component);
			}
			_lastSpawnedSushi = networkObject;
			_centerOccupied.Value = true;
			component.ServerAssignChefDesk(this);
			ServerRegisterServedDish(networkObject);
			ServerSetDishCollidersForClient(networkObject, targetClientId, isTrigger: false);
			ServerPlayFinalizedSushiSpawnFeedback();
			RestaurantManager restaurantManager = Singleton<GameProgressManager>.Instance.RestaurantManager;
			foreach (KeyValuePair<string, int> item in CustomSushiRules.CountIngredientPlacements(composition.IngredientIds))
			{
				restaurantManager.ServerRemoveStockRpc(item.Key, item.Value);
			}
			ServerRecordDailyReportSushiMade(targetClientId, text);
			Singleton<GameProgressManager>.Instance.ServerGrantProgressionXp(ProgressionXpSource.SushiPrepared, text, targetClientId);
			Singleton<GameProgressManager>.Instance.RpcRelay?.ServerUnlockSteamAchievementForClient("ACH_FIRST_SUSHI_MADE", targetClientId);
			return true;
		}

		private void ServerPlayFinalizedSushiSpawnFeedback()
		{
			if (base.IsServer)
			{
				PlayChefDeskSfxForActiveUser(ChefDeskSfxId.SpawnObject);
				ClientPlayDishSpawnVfxRpc();
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientPlayDishSpawnVfxRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(2835087832u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2835087832u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				PlayLocalDishSpawnVfx();
			}
		}

		private void PlayLocalDishSpawnVfx()
		{
			if (!(_dishSpawnVfx == null) && !(Time.time - _lastDishSpawnVfxTime < _dishSpawnVfxMinInterval))
			{
				_lastDishSpawnVfxTime = Time.time;
				_dishSpawnVfx.Play();
			}
		}

		private void ServerRecordDailyReportSushiMade(ulong clientId, string sushiId)
		{
			if (!base.IsServer)
			{
				return;
			}
			if (Singleton<PlayersManager>.Instance == null || !Singleton<PlayersManager>.Instance.TryGetPlayer(clientId, out var player) || player == null)
			{
				UtilityMethods.Log($"[ChefDesk] Daily report sushi count skipped. Player not found. ClientId={clientId}, SushiId={sushiId}", LogType.Warning);
				return;
			}
			ulong value = player.SteamID.Value;
			if (value == 0L || value == ulong.MaxValue)
			{
				UtilityMethods.Log($"[ChefDesk] Daily report sushi count skipped. Invalid SteamId={value}, ClientId={clientId}, SushiId={sushiId}", LogType.Warning);
				return;
			}
			ServiceLocator.ForSceneOf(this).Get<DailyReportBroadcaster>(out var service);
			if (service == null)
			{
				UtilityMethods.Log($"[ChefDesk] Daily report sushi count skipped. Broadcaster not found. ClientId={clientId}, SushiId={sushiId}", LogType.Warning);
			}
			else
			{
				service.ServerRecordSushiMade(value);
			}
		}

		private void ServerSetDishCollidersForClient(NetworkObject dishObject, ulong clientId, bool isTrigger)
		{
			if (!(dishObject == null))
			{
				BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(clientId, RpcTargetUse.Temp);
				NetworkObjectReference[] dishRefs = new NetworkObjectReference[1]
				{
					new NetworkObjectReference(dishObject)
				};
				ClientSetDeskDishCollidersRpc(dishRefs, isTrigger, baseRpcTarget);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerRequestMoveSushiToEdgeRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3403386461u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3403386461u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (StaffLocked || !_centerOccupied.Value)
			{
				return;
			}
			if (!_lastSpawnedSushi.TryGet(out var networkObject) || networkObject == null)
			{
				ServerRecoverFromMissingCenterDish("No sushi is available in the center to move.");
				return;
			}
			if (!networkObject.TryGetComponent<ServedDishSushi>(out var component))
			{
				ServerRecoverFromMissingCenterDish("Last spawned object is not a ServedDishSushi.");
				return;
			}
			int assignedEdgeIndex = component.AssignedEdgeIndex;
			if (assignedEdgeIndex >= 0 && assignedEdgeIndex < _edgeOccupied.Count)
			{
				_edgeOccupied[assignedEdgeIndex] = false;
			}
			sbyte b = ServerFindFirstFreeEdgeIndex();
			if (b < 0)
			{
				UtilityMethods.Log("[ChefDesk] No free edge slot found. Sushi will stay in the center.");
				ClientShowNotifyRpc("Info", "notification.chef.no_space_for_sushi");
				return;
			}
			_centerOccupied.Value = false;
			_edgeOccupied[b] = true;
			component.SetAssignedEdgeIndexServer(b);
			ClientMoveSushiToEdgeRpc(_lastSpawnedSushi, (byte)b);
			_lastSpawnedSushi = default;
			_currentState.Value = 0;
			if (IsCustomSushi)
			{
				ServerPrepareCustomModeForNextSushi();
				ClientReadyForNextCustomSushiRpc();
			}
			else
			{
				ServerSushiSelectedRpc(SelectedSushiID);
			}
			ClientShowNotifyRpc("Info", IsCustomSushi ? "notification.chef.custom_mode_ready" : "notification.chef.recipe_continues");
			SetRecipeCanvasVisibilityRpc(isVisible: false);
		}

		[Rpc(SendTo.Everyone)]
		private void ClientMoveSushiToEdgeRpc(NetworkObjectReference objRef, byte edgeIndex)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3130027331u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in objRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in edgeIndex, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3130027331u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (objRef.TryGet(out var networkObject) && edgeIndex < _edgePoints.Count && !(_edgePoints[edgeIndex] == null))
			{
				Transform transform = _edgePoints[edgeIndex];
				Transform transform2 = networkObject.transform;
				Vector3 endValue = transform2.position + Vector3.up * 0.1f;
				float duration = 0.5f * 0.5f;
				DOTween.Sequence().Append(transform2.DOMove(endValue, duration).SetEase(Ease.OutSine)).Append(transform2.DOMove(transform.position, duration).SetEase(Ease.InSine))
					.OnComplete(() =>
					{
						PlayChefDeskSfxForActiveUser(ChefDeskSfxId.MoveObject);
					});
			}
		}

		private sbyte ServerFindFirstFreeEdgeIndex()
		{
			ServerRefreshEdgeOccupancy();
			for (sbyte b = 0; b < _edgePoints.Count; b++)
			{
				if (!(_edgePoints[b] == null))
				{
					if (b >= _edgeOccupied.Count)
					{
						_edgeOccupied.Add(item: false);
					}
					if (!_edgeOccupied[b])
					{
						return b;
					}
				}
			}
			return -1;
		}

		private void ServerRefreshEdgeOccupancy()
		{
			if (!base.IsServer)
			{
				return;
			}
			while (_edgeOccupied.Count < _edgePoints.Count)
			{
				_edgeOccupied.Add(item: false);
			}
			for (int i = 0; i < _edgeOccupied.Count; i++)
			{
				_edgeOccupied[i] = false;
			}
			foreach (ServedDishSushi item in ServerGetValidServedDishesOnDesk())
			{
				int assignedEdgeIndex = item.AssignedEdgeIndex;
				if (assignedEdgeIndex >= 0 && assignedEdgeIndex < _edgeOccupied.Count)
				{
					_edgeOccupied[assignedEdgeIndex] = true;
				}
			}
		}

		private void ServerRecoverFromMissingCenterDish(string reason)
		{
			UtilityMethods.Log("[ChefDesk] " + reason, LogType.Warning);
			_lastSpawnedSushi = default;
			_centerOccupied.Value = false;
			if (CurrentState == ChefDeskState.TransferToEdge)
			{
				_currentState.Value = 0;
			}
			SendInstructionToOwnerRpc(ChefDeskInfoTextId.CompleteRecipe);
			ClientShowNotifyRpc("Info", "notification.chef.sushi_no_longer_on_table");
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
			if (!this.CanWriteServerState() || StaffEmployeeId != id)
			{
				return;
			}
			_staffRecipeVisual.Value = default;
			_staffEmployee.Value = 0;
			ServerRefreshEdgeOccupancy();
			foreach (ChefDeskGastroPan gastroPan in GastroPans)
			{
				if (gastroPan != null && gastroPan.IsSpawned && gastroPan.IngredientCount == 0)
				{
					gastroPan.ServerResetPan();
				}
			}
			CanUse.Value = true;
		}

		public Dictionary<string, int> ServerStaffCredits(SushiSO recipe)
		{
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			if (ServerStaffPartialRecipe != recipe)
			{
				return dictionary;
			}
			foreach (FixedString64Bytes item in _placedIngredientIdsNetwork)
			{
				string key = item.ToString();
				dictionary.TryGetValue(key, out var value);
				dictionary[key] = value + 1;
			}
			return dictionary;
		}

		public bool ServerStaffPreserveUnsupported(int id, Vector3 output, StaffOutputPlacement placement = null, IEnumerable<Transform> slots = null)
		{
			if (!base.IsServer || StaffEmployeeId != id)
			{
				return false;
			}
			if (_placedIngredientIdsNetwork.Count == 0 || ServerStaffPartialRecipe != null)
			{
				return true;
			}
			IGrouping<string, string>[] array = (from i in Enumerable.Range(0, _placedIngredientIdsNetwork.Count)
				select _placedIngredientIdsNetwork[i].ToString() into i
				group i by i).ToArray();
			NetworkObject networkObject = ((_gastroPanPrefab != null) ? _gastroPanPrefab.GetComponent<NetworkObject>() : null);
			if (networkObject == null || array.Any((IGrouping<string, string> g) => !(_itemDatabaseSO.FindById(g.Key) is ProcessedIngredientSO)))
			{
				return false;
			}
			Dictionary<string, Transform> dictionary = new Dictionary<string, Transform>();
			IGrouping<string, string>[] array2;
			if (placement != null && slots != null)
			{
				array2 = array;
				foreach (IGrouping<string, string> grouping in array2)
				{
					if (!placement.TryReserve(slots, grouping.Key, -id, out var point))
					{
						placement.Release(-id);
						return false;
					}
					dictionary[grouping.Key] = point;
				}
			}
			array2 = array;
			foreach (IGrouping<string, string> grouping2 in array2)
			{
				ProcessedIngredientSO processedIngredientSO = (ProcessedIngredientSO)_itemDatabaseSO.FindById(grouping2.Key);
				for (int num2 = grouping2.Count(); num2 > 0; num2 -= processedIngredientSO.MaxPanCapacity)
				{
					StaffPrepGameplayApi.CreatePan(networkObject, processedIngredientSO, Mathf.Min(num2, processedIngredientSO.MaxPanCapacity), dictionary.TryGetValue(grouping2.Key, out var value) ? value.position : output);
				}
			}
			placement?.Release(-id);
			ServerStaffClearAssembly();
			return true;
		}

		public void ServerStaffBegin(int id, SushiSO recipe)
		{
			ServerStaffBegin(id, SushiRecipeResolver.CreateBuiltInDefinition(recipe));
		}

		public void ServerStaffBegin(int id, SushiRecipeDefinition recipe)
		{
			if (this.CanWriteServerState() && StaffEmployeeId == id && recipe != null)
			{
				ServerStaffClearAssembly();
				_staffRecipeVisual.Value = recipe.RecipeId;
			}
		}

		public void ServerStaffFinish(int id)
		{
			if (base.IsServer && StaffEmployeeId == id)
			{
				_staffRecipeVisual.Value = default;
			}
		}

		private void ServerStaffClearAssembly()
		{
			_placedIngredientIdsNetwork.Clear();
			_currentIngredientPlacedCountNetwork.Value = 0;
			_currentIngredientPlacedCount = 0;
			_currentRecipeStepIndex.Value = 0;
			_isRecipeCompleted.Value = true;
			_isCustomSushi.Value = false;
			_isCustomCompositionLocked.Value = false;
			_resolvedCustomRecipeKind.Value = 2;
			_currentState.Value = 0;
			ClientStaffClearAssemblyRpc();
		}

		[Rpc(SendTo.Everyone)]
		private void ClientStaffClearAssemblyRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3201915914u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3201915914u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientClearObjectsOnTable();
				ClientChefDeskResetRpc();
				if (_nigiriRiceOBJ != null)
				{
					_nigiriRiceOBJ.SetActive(value: false);
				}
			}
		}

		private void StaffRecipeVisualChanged(FixedString64Bytes before, FixedString64Bytes after)
		{
			if (_staffVisual != null)
			{
				UnityEngine.Object.Destroy(_staffVisual);
			}
			if (after.IsEmpty || _centerPoint == null)
			{
				return;
			}
			SushiSO sushiSO = _itemDatabaseSO.FindById(after.ToString()) as SushiSO;
			if (sushiSO == null && TryResolveRecipeDefinition(after.ToString(), out var recipe))
			{
				NetworkObject customPhysicalPrefab = GetCustomPhysicalPrefab(recipe.SushiType);
				if (customPhysicalPrefab != null && customPhysicalPrefab.TryGetComponent<ServedDishSushi>(out var component))
				{
					sushiSO = component.ItemSO as SushiSO;
				}
			}
			if (!(sushiSO?.GraphicObjectPrefab == null))
			{
				_staffVisual = UnityEngine.Object.Instantiate(sushiSO.GraphicObjectPrefab, _centerPoint);
				_staffVisual.transform.localPosition = Vector3.zero;
				Collider[] componentsInChildren = _staffVisual.GetComponentsInChildren<Collider>();
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					componentsInChildren[i].enabled = false;
				}
			}
		}

		protected override void __initializeVariables()
		{
			if (_centerOccupied == null)
			{
				throw new Exception("ChefDesk._centerOccupied cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_centerOccupied.Initialize(this);
			__nameNetworkVariable(_centerOccupied, "_centerOccupied");
			NetworkVariableFields.Add(_centerOccupied);
			if (_currentState == null)
			{
				throw new Exception("ChefDesk._currentState cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_currentState.Initialize(this);
			__nameNetworkVariable(_currentState, "_currentState");
			NetworkVariableFields.Add(_currentState);
			if (_currentSushiType == null)
			{
				throw new Exception("ChefDesk._currentSushiType cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_currentSushiType.Initialize(this);
			__nameNetworkVariable(_currentSushiType, "_currentSushiType");
			NetworkVariableFields.Add(_currentSushiType);
			if (_currentRecipeStepIndex == null)
			{
				throw new Exception("ChefDesk._currentRecipeStepIndex cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_currentRecipeStepIndex.Initialize(this);
			__nameNetworkVariable(_currentRecipeStepIndex, "_currentRecipeStepIndex");
			NetworkVariableFields.Add(_currentRecipeStepIndex);
			if (_isCustomSushi == null)
			{
				throw new Exception("ChefDesk._isCustomSushi cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isCustomSushi.Initialize(this);
			__nameNetworkVariable(_isCustomSushi, "_isCustomSushi");
			NetworkVariableFields.Add(_isCustomSushi);
			if (_isCustomCompositionLocked == null)
			{
				throw new Exception("ChefDesk._isCustomCompositionLocked cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isCustomCompositionLocked.Initialize(this);
			__nameNetworkVariable(_isCustomCompositionLocked, "_isCustomCompositionLocked");
			NetworkVariableFields.Add(_isCustomCompositionLocked);
			if (_isRecipeCompleted == null)
			{
				throw new Exception("ChefDesk._isRecipeCompleted cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isRecipeCompleted.Initialize(this);
			__nameNetworkVariable(_isRecipeCompleted, "_isRecipeCompleted");
			NetworkVariableFields.Add(_isRecipeCompleted);
			if (_selectedSushiSO_ID == null)
			{
				throw new Exception("ChefDesk._selectedSushiSO_ID cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_selectedSushiSO_ID.Initialize(this);
			__nameNetworkVariable(_selectedSushiSO_ID, "_selectedSushiSO_ID");
			NetworkVariableFields.Add(_selectedSushiSO_ID);
			if (_placedIngredientIdsNetwork == null)
			{
				throw new Exception("ChefDesk._placedIngredientIdsNetwork cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_placedIngredientIdsNetwork.Initialize(this);
			__nameNetworkVariable(_placedIngredientIdsNetwork, "_placedIngredientIdsNetwork");
			NetworkVariableFields.Add(_placedIngredientIdsNetwork);
			if (_currentIngredientPlacedCountNetwork == null)
			{
				throw new Exception("ChefDesk._currentIngredientPlacedCountNetwork cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_currentIngredientPlacedCountNetwork.Initialize(this);
			__nameNetworkVariable(_currentIngredientPlacedCountNetwork, "_currentIngredientPlacedCountNetwork");
			NetworkVariableFields.Add(_currentIngredientPlacedCountNetwork);
			if (_resolvedCustomRecipeKind == null)
			{
				throw new Exception("ChefDesk._resolvedCustomRecipeKind cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_resolvedCustomRecipeKind.Initialize(this);
			__nameNetworkVariable(_resolvedCustomRecipeKind, "_resolvedCustomRecipeKind");
			NetworkVariableFields.Add(_resolvedCustomRecipeKind);
			if (_savableCustomSushi == null)
			{
				throw new Exception("ChefDesk._savableCustomSushi cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_savableCustomSushi.Initialize(this);
			__nameNetworkVariable(_savableCustomSushi, "_savableCustomSushi");
			NetworkVariableFields.Add(_savableCustomSushi);
			if (_staffEmployee == null)
			{
				throw new Exception("ChefDesk._staffEmployee cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_staffEmployee.Initialize(this);
			__nameNetworkVariable(_staffEmployee, "_staffEmployee");
			NetworkVariableFields.Add(_staffEmployee);
			if (_staffRecipeVisual == null)
			{
				throw new Exception("ChefDesk._staffRecipeVisual cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_staffRecipeVisual.Initialize(this);
			__nameNetworkVariable(_staffRecipeVisual, "_staffRecipeVisual");
			NetworkVariableFields.Add(_staffRecipeVisual);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(4054705145u, __rpc_handler_4054705145, "ClientPrepareAssemblyLayoutRpc", RpcInvokePermission.Everyone);
			__registerRpc(697800329u, __rpc_handler_697800329, "CenterAssemblyClientRpc", RpcInvokePermission.Everyone);
			__registerRpc(1892314360u, __rpc_handler_1892314360, "ServerRequestStartCuttingAfterCenteringRpc", RpcInvokePermission.Everyone);
			__registerRpc(3556829776u, __rpc_handler_3556829776, "ServerRequestBeginToppingRpc", RpcInvokePermission.Everyone);
			__registerRpc(1364185859u, __rpc_handler_1364185859, "ServerSetStateRpc", RpcInvokePermission.Everyone);
			__registerRpc(4086558293u, __rpc_handler_4086558293, "ClientPrepareRollPresentationRpc", RpcInvokePermission.Everyone);
			__registerRpc(2323104352u, __rpc_handler_2323104352, "ClientShowNotifyRpc", RpcInvokePermission.Everyone);
			__registerRpc(588815474u, __rpc_handler_588815474, "ClientShowNotifyRpc", RpcInvokePermission.Everyone);
			__registerRpc(1767421952u, __rpc_handler_1767421952, "ClientGastropanSFXRpc", RpcInvokePermission.Everyone);
			__registerRpc(399358711u, __rpc_handler_399358711, "ReceiveChefDeskSfxRpc", RpcInvokePermission.Everyone);
			__registerRpc(1906048811u, __rpc_handler_1906048811, "ClientSetDeskDishCollidersRpc", RpcInvokePermission.Everyone);
			__registerRpc(1893322649u, __rpc_handler_1893322649, "SetRecipeCanvasVisibilityRpc", RpcInvokePermission.Everyone);
			__registerRpc(1443386404u, __rpc_handler_1443386404, "ServerSelectRecipeModeRpc", RpcInvokePermission.Everyone);
			__registerRpc(511688556u, __rpc_handler_511688556, "ServerSelectCustomModeRpc", RpcInvokePermission.Everyone);
			__registerRpc(477253040u, __rpc_handler_477253040, "ServerSelectCustomSushiTypeRpc", RpcInvokePermission.Everyone);
			__registerRpc(538201732u, __rpc_handler_538201732, "ClientSetCustomRecipePanelVisibilityRpc", RpcInvokePermission.Everyone);
			__registerRpc(3837474383u, __rpc_handler_3837474383, "ServerFinishCustomSushiRpc", RpcInvokePermission.Everyone);
			__registerRpc(3684510283u, __rpc_handler_3684510283, "ClientHandleCustomCompositionFinishedRpc", RpcInvokePermission.Everyone);
			__registerRpc(798157445u, __rpc_handler_798157445, "ServerSaveCurrentCustomRecipeRpc", RpcInvokePermission.Everyone);
			__registerRpc(563892524u, __rpc_handler_563892524, "ClientHandleCustomRecipeSavedRpc", RpcInvokePermission.Everyone);
			__registerRpc(4125091314u, __rpc_handler_4125091314, "ClientHandleCustomSushiSpawnedRpc", RpcInvokePermission.Everyone);
			__registerRpc(2352889263u, __rpc_handler_2352889263, "ClientReadyForNextCustomSushiRpc", RpcInvokePermission.Everyone);
			__registerRpc(2176335985u, __rpc_handler_2176335985, "ServerBeginTemporaryCutPieceSessionRpc", RpcInvokePermission.Everyone);
			__registerRpc(2377123185u, __rpc_handler_2377123185, "ClientBeginRollMeshCutSessionForObserversRpc", RpcInvokePermission.Everyone);
			__registerRpc(3792718373u, __rpc_handler_3792718373, "ServerRequestReplicateTemporaryCutPieceRpc", RpcInvokePermission.Everyone);
			__registerRpc(4156759997u, __rpc_handler_4156759997, "ServerRequestReplicateRollMeshCutRpc", RpcInvokePermission.Everyone);
			__registerRpc(2649109984u, __rpc_handler_2649109984, "ClientApplyRollMeshCutForObserversRpc", RpcInvokePermission.Everyone);
			__registerRpc(1859301407u, __rpc_handler_1859301407, "ClientSpawnTemporaryCutPieceForObserversRpc", RpcInvokePermission.Everyone);
			__registerRpc(2575532333u, __rpc_handler_2575532333, "ClientClearTemporaryCutPiecesForObserversRpc", RpcInvokePermission.Everyone);
			__registerRpc(3197777309u, __rpc_handler_3197777309, "ClientClearTemporaryCutPiecesRpc", RpcInvokePermission.Everyone);
			__registerRpc(789288353u, __rpc_handler_789288353, "ServerPlaceSeasoningOnRackRpc", RpcInvokePermission.Everyone);
			__registerRpc(2474426632u, __rpc_handler_2474426632, "ServerSushiSelectedRpc", RpcInvokePermission.Everyone);
			__registerRpc(1797742243u, __rpc_handler_1797742243, "ClientInfoPanelPrepareRpc", RpcInvokePermission.Everyone);
			__registerRpc(1118370342u, __rpc_handler_1118370342, "SendInstructionToOwnerRpc", RpcInvokePermission.Everyone);
			__registerRpc(747287262u, __rpc_handler_747287262, "ServerPlaceProductOnTableRpc", RpcInvokePermission.Everyone);
			__registerRpc(1653697738u, __rpc_handler_1653697738, "ServerRetrieveProductFromTableRpc", RpcInvokePermission.Everyone);
			__registerRpc(347136103u, __rpc_handler_347136103, "ClientIngredientRetrievedRpc", RpcInvokePermission.Everyone);
			__registerRpc(1967278592u, __rpc_handler_1967278592, "ClientHandleRecipeProgressRpc", RpcInvokePermission.Everyone);
			__registerRpc(2487486278u, __rpc_handler_2487486278, "ClientIngredientPlacedRpc", RpcInvokePermission.Everyone);
			__registerRpc(3078435841u, __rpc_handler_3078435841, "ServerDebugFillSelectedRecipeGastroPansRpc", RpcInvokePermission.Everyone);
			__registerRpc(1546633182u, __rpc_handler_1546633182, "ServerChefDeskResetRpc", RpcInvokePermission.Everyone);
			__registerRpc(480021319u, __rpc_handler_480021319, "ClientChefDeskEndDayPreserveAssemblyRpc", RpcInvokePermission.Everyone);
			__registerRpc(1816838245u, __rpc_handler_1816838245, "ClientChefDeskResetRpc", RpcInvokePermission.Everyone);
			__registerRpc(2749113648u, __rpc_handler_2749113648, "ClientChefDeskEndDayResetRpc", RpcInvokePermission.Everyone);
			__registerRpc(196734092u, __rpc_handler_196734092, "CuttingResetRpc", RpcInvokePermission.Everyone);
			__registerRpc(2554599794u, __rpc_handler_2554599794, "ClientCuttingStateResetRpc", RpcInvokePermission.Everyone);
			__registerRpc(2320784291u, __rpc_handler_2320784291, "ServerRequestFinalizePreparedSushiRpc", RpcInvokePermission.Everyone);
			__registerRpc(2835087832u, __rpc_handler_2835087832, "ClientPlayDishSpawnVfxRpc", RpcInvokePermission.Everyone);
			__registerRpc(3403386461u, __rpc_handler_3403386461, "ServerRequestMoveSushiToEdgeRpc", RpcInvokePermission.Everyone);
			__registerRpc(3130027331u, __rpc_handler_3130027331, "ClientMoveSushiToEdgeRpc", RpcInvokePermission.Everyone);
			__registerRpc(3201915914u, __rpc_handler_3201915914, "ClientStaffClearAssemblyRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_4054705145(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out ushort value2);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientPrepareAssemblyLayoutRpc(value, value2);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_697800329(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).CenterAssemblyClientRpc();
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1892314360(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ServerRequestStartCuttingAfterCenteringRpc(ext);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3556829776(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ServerRequestBeginToppingRpc();
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1364185859(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out ChefDeskState value, default(FastBufferWriter.ForEnums));
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ServerSetStateRpc(value);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4086558293(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientPrepareRollPresentationRpc();
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2323104352(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out FixedString64Bytes value2, default(FastBufferWriter.ForFixedStrings));
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientShowNotifyRpc(value, value2);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_588815474(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out FixedString128Bytes value2, default(FastBufferWriter.ForFixedStrings));
				RpcParams ext = rpcParams.Ext;
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientShowNotifyRpc(value, value2, ext);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1767421952(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				RpcParams ext = rpcParams.Ext;
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientGastropanSFXRpc(value, ext);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_399358711(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out ChefDeskSfxId value, default(FastBufferWriter.ForEnums));
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ReceiveChefDeskSfxRpc(value, value2, value3, ext);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1906048811(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				NetworkObjectReference[] value2 = null;
				if (value)
				{
					reader.ReadValueSafe(out value2, default(FastBufferWriter.ForNetworkSerializable));
				}
				reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientSetDeskDishCollidersRpc(value2, value3, ext);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1893322649(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).SetRecipeCanvasVisibilityRpc(value);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1443386404(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ServerSelectRecipeModeRpc(ext);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_511688556(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ServerSelectCustomModeRpc(ext);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_477253040(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ServerSelectCustomSushiTypeRpc(value, ext);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_538201732(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientSetCustomRecipePanelVisibilityRpc(value);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3837474383(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ServerFinishCustomSushiRpc(ext);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3684510283(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientHandleCustomCompositionFinishedRpc();
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_798157445(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString128Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				byte[] value3 = null;
				if (value2)
				{
					reader.ReadValueSafe(out value3, default(FastBufferWriter.ForPrimitives));
				}
				RpcParams ext = rpcParams.Ext;
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ServerSaveCurrentCustomRecipeRpc(value, value3, ext);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_563892524(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientHandleCustomRecipeSavedRpc();
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4125091314(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientHandleCustomSushiSpawnedRpc();
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2352889263(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientReadyForNextCustomSushiRpc();
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2176335985(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ServerBeginTemporaryCutPieceSessionRpc(value, value2, ext);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2377123185(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientBeginRollMeshCutSessionForObserversRpc(value);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3792718373(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Vector3 value2);
				RpcParams ext = rpcParams.Ext;
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ServerRequestReplicateTemporaryCutPieceRpc(value, value2, ext);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4156759997(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out Vector3 value3);
				reader.ReadValueSafe(out Vector3 value4);
				RpcParams ext = rpcParams.Ext;
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ServerRequestReplicateRollMeshCutRpc(value, value2, value3, value4, ext);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2649109984(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out Vector3 value3);
				reader.ReadValueSafe(out Vector3 value4);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientApplyRollMeshCutForObserversRpc(value, value2, value3, value4);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1859301407(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Vector3 value2);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientSpawnTemporaryCutPieceForObserversRpc(value, value2);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2575532333(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientClearTemporaryCutPiecesForObserversRpc();
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3197777309(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientClearTemporaryCutPiecesRpc();
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_789288353(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ServerPlaceSeasoningOnRackRpc(value, value2);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2474426632(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ServerSushiSelectedRpc(value);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1797742243(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientInfoPanelPrepareRpc(value, value2);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1118370342(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out ChefDeskInfoTextId value, default(FastBufferWriter.ForEnums));
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).SendInstructionToOwnerRpc(value);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_747287262(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ServerPlaceProductOnTableRpc(value);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1653697738(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ServerRetrieveProductFromTableRpc(value, ext);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_347136103(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientIngredientRetrievedRpc(value);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1967278592(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientHandleRecipeProgressRpc(value, value2);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2487486278(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientIngredientPlacedRpc(value);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3078435841(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ServerDebugFillSelectedRecipeGastroPansRpc(ext);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1546633182(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ServerChefDeskResetRpc();
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_480021319(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientChefDeskEndDayPreserveAssemblyRpc();
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1816838245(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientChefDeskResetRpc();
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2749113648(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientChefDeskEndDayResetRpc(value);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_196734092(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).CuttingResetRpc();
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2554599794(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientCuttingStateResetRpc();
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2320784291(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out ChefDeskFinalizeReason value, default(FastBufferWriter.ForEnums));
				RpcParams ext = rpcParams.Ext;
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ServerRequestFinalizePreparedSushiRpc(value, ext);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2835087832(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientPlayDishSpawnVfxRpc();
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3403386461(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ServerRequestMoveSushiToEdgeRpc();
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3130027331(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientMoveSushiToEdgeRpc(value, value2);
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3201915914(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ChefDesk)target).ClientStaffClearAssemblyRpc();
				((ChefDesk)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "ChefDesk";
		}
	}
}
