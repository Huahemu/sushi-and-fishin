using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using Michsky.MUIP;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.UI;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Indicator;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Tablet
{
	public class MapApp : BaseApp, IPointerClickHandler, IEventSystemHandler
	{
		private sealed class PlayerMarkerEntry
		{
			public PPG_Player Player;

			public PlayerIconUI View;

			public RectTransform Rect;

			public RectTransform DirectionalArrow;

			public Action<bool> UnderwaterChanged;

			public NetworkVariable<ulong>.OnValueChangedDelegate SteamIdChanged;

			public string DisplayedName;

			public int AvatarLoadVersion;

			public Vector2 GroundProbeXZ;

			public float MapGroundY;

			public bool HasGroundProbe;

			public bool HasMapGroundY;

			public bool GroundProbeMissLogged;

			public bool GroundHitBufferSaturationLogged;
		}

		private struct CreaturePopupEntry
		{
			public readonly string Name;

			public readonly string Rarity;

			public int Count { get; set; }

			public CreaturePopupEntry(string name, string rarity, int count)
			{
				Name = name;
				Rarity = rarity;
				Count = count;
			}
		}

		[SerializeField]
		private Image _backgroundImage;

		[Header("MAP DEFINITIONS")]
		[SerializeField]
		private MapDefinitionSO _surfaceMap;

		[SerializeField]
		private MapDefinitionSO _underwaterMap;

		[Header("MAP VIEWS")]
		[SerializeField]
		private GameObject _cityMapRoot;

		[SerializeField]
		private GameObject _underwaterMapRoot;

		[Header("LABEL")]
		[SerializeField]
		private CanvasGroup _labelGroup;

		[SerializeField]
		private float _labelScrollMin = 1.2f;

		[SerializeField]
		private float _labelScrollMax = 1.5f;

		[SerializeField]
		private RectTransform _contentRect;

		[SerializeField]
		private MapZoomer _mapZoomer;

		[SerializeField]
		[FormerlySerializedAs("_biomeDropdown")]
		private CustomDropdown _mapDropdown;

		[Header("PLAYER MARKERS")]
		[SerializeField]
		private RectTransform _playerMarkerContainer;

		[SerializeField]
		private PlayerIconUI _playerMarkerPrefab;

		[Header("UNDERWATER MAP GROUND PROJECTION")]
		[SerializeField]
		private LayerMask _underwaterMapGroundMask = 1;

		[SerializeField]
		private float _underwaterMapGroundRayStartY = 200f;

		[SerializeField]
		[Min(1f)]
		private float _underwaterMapGroundRayDistance = 1000f;

		[SerializeField]
		[Min(0.1f)]
		private float _underwaterMapGroundResampleDistance = 1f;

		[SerializeField]
		private string _underwaterMapGroundModelsRootName = "Underwater Models";

		[Header("HABITATS")]
		[SerializeField]
		private RectTransform _habitatMarkerContainer;

		[SerializeField]
		private MapHabitatMarker _habitatMarkerPrefab;

		[SerializeField]
		private MapHabitatRevealOverlay _habitatRevealOverlay;

		[Header("SEA AREA MARKERS")]
		[SerializeField]
		private RectTransform _seaAreaMarkerContainer;

		[SerializeField]
		private MapSeaAreaMarker _seaAreaMarkerPrefab;

		[Header("HABITAT POPUP")]
		[SerializeField]
		private MapHabitatPopupView _habitatPopupPrefab;

		[Header("LOCATION MARKER")]
		[SerializeField]
		private RectTransform _locationMarkerContainer;

		[SerializeField]
		private MapLocationMarker _locationMarkerPrefab;

		[SerializeField]
		[Min(0.1f)]
		private float _locationReachDistance = 5f;

		[SerializeField]
		private OffscreenIndicatorVisualSettings _locationIndicatorVisualSettings = new OffscreenIndicatorVisualSettings();

		private readonly Dictionary<byte, MapHabitatMarker> _habitatMarkersByIndex = new Dictionary<byte, MapHabitatMarker>();

		private readonly List<MapHabitatMarker> _habitatMarkers = new List<MapHabitatMarker>();

		private readonly List<MapSeaAreaMarker> _seaAreaMarkers = new List<MapSeaAreaMarker>();

		private readonly HashSet<byte> _discoveredHabitats = new HashSet<byte>();

		private readonly Dictionary<ulong, PlayerMarkerEntry> _playerMarkers = new Dictionary<ulong, PlayerMarkerEntry>();

		private TabletNetworkHandler _tabletNetworkHandler;

		private readonly MapProjection _projection = new MapProjection();

		private readonly MapGroundHeightResolver _groundHeightResolver = new MapGroundHeightResolver();

		private readonly MapGroundProjectionSolver _groundProjectionSolver = new MapGroundProjectionSolver();

		private MapDefinitionSO _currentMap;

		private bool _markersRegistered;

		private bool _seaAreaMarkersRegistered;

		private bool _seaAreaMarkersBuilt;

		private bool _playerEventsRegistered;

		private bool _isMapOpen;

		private bool _suppressDropdownCallback;

		private MapHabitatPopupView _habitatPopupInstance;

		private MapLocationMarker _locationMarkerInstance;

		private MapLocationWorldTarget _locationWorldTarget;

		private OffscreenIndicatorHandler _locationIndicatorHandler;

		private OffscreenIndicatorHandler.OffscreenIndicatorHandle _locationIndicatorHandle;

		private PPG_Player _locationOwner;

		private MapLayer _locationLayer;

		private Transform _underwaterMapGroundModelsRoot;

		private LiveSceneTabletMap _liveMap;

		public override void Initialize()
		{
			UtilityMethods.Log("[MapApp] Initialized");
			_liveMap = GetComponent<LiveSceneTabletMap>();
			if (_liveMap == null) _liveMap = gameObject.AddComponent<LiveSceneTabletMap>();
			_liveMap.Configure(_backgroundImage);
			if (_mapZoomer != null)
			{
				_mapZoomer.Initialize();
			}
			ResolveTabletNetworkHandler();
			RegisterStaticHabitatMarkers();
			RegisterStaticSeaAreaMarkers();
			if (_tabletNetworkHandler != null)
			{
				SetDiscoveredHabitats(_tabletNetworkHandler.GetDiscoveredHabitats(), null);
			}
			if (_mapDropdown != null)
			{
				_mapDropdown.SetDropdownIndex(0);
				_mapDropdown.onValueChanged.RemoveAllListeners();
				_mapDropdown.onValueChanged.AddListener(OnMapDropdownValueChanged);
			}
			RegisterPlayerEvents();
			ReconcilePlayerMarkers();
			ApplyMap(GetDefinition(MapLayer.Surface), resetView: false, centerOnLocalPlayer: false, null);
			HideHabitatPopup();
		}

		private void ResolveTabletNetworkHandler()
		{
			ServiceLocator.ForSceneOf(this).TryGet<TabletNetworkHandler>(out _tabletNetworkHandler);
		}

		private void RegisterStaticHabitatMarkers()
		{
			if (_markersRegistered)
			{
				return;
			}
			_markersRegistered = true;
			_habitatMarkersByIndex.Clear();
			_habitatMarkers.Clear();
			if (_habitatMarkerContainer == null)
			{
				UtilityMethods.Log("[MapApp] Habitat marker container is missing.", LogType.Error);
				return;
			}
			MapHabitatMarker[] componentsInChildren = _habitatMarkerContainer.GetComponentsInChildren<MapHabitatMarker>(includeInactive: true);
			_habitatMarkers.AddRange(componentsInChildren);
			for (int i = 0; i < _habitatMarkers.Count; i++)
			{
				MapHabitatMarker mapHabitatMarker = _habitatMarkers[i];
				if (!(mapHabitatMarker == null))
				{
					mapHabitatMarker.Initialize(ShowHabitatPopup);
					mapHabitatMarker.SetDiscovered(_discoveredHabitats.Contains(mapHabitatMarker.SeaLayerIndex));
					if (_habitatMarkersByIndex.ContainsKey(mapHabitatMarker.SeaLayerIndex))
					{
						UtilityMethods.Log($"[MapApp] Duplicate habitat marker SeaLayerIndex={mapHabitatMarker.SeaLayerIndex}. Keeping the first marker.", LogType.Warning);
					}
					else
					{
						_habitatMarkersByIndex.Add(mapHabitatMarker.SeaLayerIndex, mapHabitatMarker);
					}
				}
			}
		}

		private void RegisterStaticSeaAreaMarkers()
		{
			if (!_seaAreaMarkersRegistered)
			{
				_seaAreaMarkersRegistered = true;
				_seaAreaMarkers.Clear();
				if (_seaAreaMarkerContainer == null)
				{
					UtilityMethods.Log("[MapApp] SeaArea marker container is missing.", LogType.Error);
					return;
				}
				MapSeaAreaMarker[] componentsInChildren = _seaAreaMarkerContainer.GetComponentsInChildren<MapSeaAreaMarker>(includeInactive: true);
				_seaAreaMarkers.AddRange(componentsInChildren);
				_seaAreaMarkersBuilt = _seaAreaMarkers.Count > 0;
			}
		}

		private void EnsureSeaAreaMarkers()
		{
			RegisterStaticSeaAreaMarkers();
			if (_seaAreaMarkersBuilt || _seaAreaMarkerContainer == null || _seaAreaMarkerPrefab == null)
			{
				return;
			}
			SeaCreatureManager instance = Singleton<SeaCreatureManager>.Instance;
			if (instance == null || _underwaterMap == null)
			{
				return;
			}
			MapProjection mapProjection = new MapProjection();
			if (!mapProjection.Fit(_underwaterMap))
			{
				UtilityMethods.Log("[MapApp] SeaArea markers could not be built because the underwater projection is invalid.", LogType.Error);
				_seaAreaMarkersBuilt = true;
				return;
			}
			IReadOnlyList<SeaLayer> layers = instance.Layers;
			for (int i = 0; i < layers.Count; i++)
			{
				SeaLayer seaLayer = layers[i];
				if (seaLayer == null)
				{
					continue;
				}
				IReadOnlyList<SeaArea> seaAreas = seaLayer.SeaAreas;
				for (int j = 0; j < seaAreas.Count; j++)
				{
					SeaArea seaArea = seaAreas[j];
					if (!(seaArea == null) && seaArea.TryGetWorldBounds(out var bounds) && mapProjection.TryWorldToMapUV(bounds.center, out var mapUV))
					{
						MapSeaAreaMarker mapSeaAreaMarker = UnityEngine.Object.Instantiate(_seaAreaMarkerPrefab, _seaAreaMarkerContainer, worldPositionStays: false);
						mapSeaAreaMarker.Configure(seaLayer.Index, seaArea.Index);
						RectTransform rectTransform = mapSeaAreaMarker.RectTransform;
						Vector2 anchorMax = (rectTransform.anchorMin = new Vector2(Mathf.Clamp01(mapUV.x), Mathf.Clamp01(mapUV.y)));
						rectTransform.anchorMax = anchorMax;
						rectTransform.pivot = new Vector2(0.5f, 0.5f);
						rectTransform.anchoredPosition = Vector2.zero;
						mapSeaAreaMarker.name = $"SeaAreaFishMarker ({seaLayer.Index:D3}:{seaArea.Index:D3})";
						_seaAreaMarkers.Add(mapSeaAreaMarker);
					}
				}
			}
			_seaAreaMarkersBuilt = true;
		}

		private void OnMapDropdownValueChanged(int index)
		{
			if (!_suppressDropdownCallback)
			{
				MapLayer layer = ((index > 0) ? MapLayer.Underwater : MapLayer.Surface);
				ApplyMap(GetDefinition(layer), resetView: true, centerOnLocalPlayer: false, null);
			}
		}

		public void SetBiome()
		{
			if (!(_currentMap == null))
			{
				ApplyMap(_currentMap, resetView: false, centerOnLocalPlayer: false, null);
				if (_mapDropdown != null)
				{
					_mapDropdown.SetDropdownIndex((int)_currentMap.Layer);
				}
			}
		}

		public void SetDiscoveredHabitats(IReadOnlyList<byte> discoveredIndices, byte? animatedSeaLayerIndex)
		{
			_discoveredHabitats.Clear();
			if (discoveredIndices != null)
			{
				for (int i = 0; i < discoveredIndices.Count; i++)
				{
					_discoveredHabitats.Add(discoveredIndices[i]);
				}
			}
			if (_isMapOpen && base.isActiveAndEnabled)
			{
				RefreshHabitatVisuals(animatedSeaLayerIndex);
			}
		}

		public void Update()
		{
			UpdateLabelVisibility();
			UpdatePlayerMarkers();
		}

		private void ApplyMap(MapDefinitionSO definition, bool resetView, bool centerOnLocalPlayer, byte? animatedSeaLayerIndex)
		{
			if (definition == null)
			{
				UtilityMethods.Log("[MapApp] Map definition is missing.", LogType.Error);
				return;
			}
			_currentMap = definition;
			if (!_projection.FitWorldRect(_liveMap.SetLayer(definition.Layer)))
			{
				UtilityMethods.Log($"[MapApp] Projection is invalid for layer={definition.Layer}. Four valid non-degenerate anchors are required.", LogType.Error);
			}
			if (_backgroundImage != null)
			{
				_backgroundImage.enabled = false;
			}
			bool flag = definition.Layer == MapLayer.Underwater;
			if (_cityMapRoot != null)
			{
				_cityMapRoot.SetActive(false);
			}
			if (_underwaterMapRoot != null)
			{
				_underwaterMapRoot.SetActive(flag);
			}
			UpdatePlayerMarkers(force: true);
			if (_mapZoomer != null)
			{
				Canvas.ForceUpdateCanvases();
				var viewport = _contentRect.parent as RectTransform;
				float fit = Mathf.Min(viewport.rect.width / _contentRect.rect.width, viewport.rect.height / _contentRect.rect.height);
				var zoom = new MapZoomSettings(fit, fit, fit * 5f, 0.04f);
				Vector2? normalizedFocus = null;
				if (resetView)
				{
					_mapZoomer.ResetAndCenter(zoom, normalizedFocus);
				}
				else
				{
					_mapZoomer.ApplySettings(zoom, resetView: false);
				}
			}
			RefreshHabitatVisuals(animatedSeaLayerIndex);
			if (flag) ReprojectMarineMarkers();
			RefreshLocationMarkerVisibility();
			HideHabitatPopup();
		}

		private MapDefinitionSO GetDefinition(MapLayer layer)
		{
			if (layer != MapLayer.Underwater)
			{
				return _surfaceMap;
			}
			return _underwaterMap;
		}

		private void ReprojectMarineMarkers()
		{
			void Place(RectTransform rect, Vector3 world)
			{
				if (rect == null || !_projection.TryWorldToMapUV(world, out var uv)) return;
				rect.anchorMin = rect.anchorMax = uv;
				rect.anchoredPosition = Vector2.zero;
			}
			foreach (var marker in _habitatMarkers)
			{
				var layer = marker.SeaLayer;
				if (layer == null) continue;
				Bounds bounds = default; bool any = false;
				foreach (var area in layer.SeaAreas)
					if (area != null && area.TryGetWorldBounds(out var b)) { if (any) bounds.Encapsulate(b); else { bounds = b; any = true; } }
				if (any) Place(marker.RectTransform, bounds.center);
			}
			var manager = Singleton<SeaCreatureManager>.Instance;
			if (manager == null) return;
			foreach (var marker in _seaAreaMarkers)
				if (manager.TryGetLayer(marker.SeaLayerIndex, out var layer))
					foreach (var area in layer.SeaAreas)
						if (area != null && area.Index == marker.SeaAreaIndex && area.TryGetWorldBounds(out var b)) Place(marker.RectTransform, b.center);
		}

		private void RefreshHabitatVisuals(byte? animatedSeaLayerIndex)
		{
			RegisterStaticHabitatMarkers();
			EnsureSeaAreaMarkers();
			bool flag = _currentMap != null && _currentMap.Layer == MapLayer.Underwater;
			byte? animatedSeaLayerIndex2 = null;
			for (int i = 0; i < _habitatMarkers.Count; i++)
			{
				MapHabitatMarker mapHabitatMarker = _habitatMarkers[i];
				if (!(mapHabitatMarker == null))
				{
					mapHabitatMarker.gameObject.SetActive(flag);
					mapHabitatMarker.SetDiscovered(_discoveredHabitats.Contains(mapHabitatMarker.SeaLayerIndex));
					if (flag && animatedSeaLayerIndex.HasValue && mapHabitatMarker.SeaLayerIndex == animatedSeaLayerIndex.Value)
					{
						animatedSeaLayerIndex2 = animatedSeaLayerIndex;
					}
				}
			}
			for (int j = 0; j < _seaAreaMarkers.Count; j++)
			{
				MapSeaAreaMarker mapSeaAreaMarker = _seaAreaMarkers[j];
				if (!(mapSeaAreaMarker == null))
				{
					bool visible = flag && _discoveredHabitats.Contains(mapSeaAreaMarker.SeaLayerIndex);
					mapSeaAreaMarker.SetVisible(visible);
				}
			}
			if (_habitatRevealOverlay != null)
			{
				if (flag && _liveMap == null)
				{
					_habitatRevealOverlay.Render(_habitatMarkers, _discoveredHabitats, animatedSeaLayerIndex2);
				}
				else
				{
					_habitatRevealOverlay.Hide();
				}
			}
		}

		private void ShowHabitatPopup(MapHabitatMarker marker)
		{
			if (!(marker == null))
			{
				SeaLayer seaLayer = marker.SeaLayer;
				string displayName = marker.GetDisplayName();
				string body = BuildHabitatPopupBody(seaLayer);
				if (EnsureHabitatPopup())
				{
					_habitatPopupInstance.Show(displayName, body);
				}
			}
		}

		private static string BuildHabitatPopupBody(SeaLayer habitat)
		{
			IReadOnlyList<SpawnableCreature> readOnlyList = ((habitat != null) ? habitat.SpawnableCreatures : null);
			Dictionary<string, CreaturePopupEntry> dictionary = new Dictionary<string, CreaturePopupEntry>(StringComparer.Ordinal);
			if (readOnlyList != null)
			{
				for (int i = 0; i < readOnlyList.Count; i++)
				{
					SpawnableCreature spawnableCreature = readOnlyList[i];
					CreatureSO creatureSO = spawnableCreature?.CreatureData;
					if (!(creatureSO == null))
					{
						string key = ((!string.IsNullOrWhiteSpace(creatureSO.Id)) ? creatureSO.Id : creatureSO.name);
						string text = ((!string.IsNullOrWhiteSpace(creatureSO.LocalizedName)) ? creatureSO.LocalizedName : creatureSO.name);
						if (!dictionary.TryGetValue(key, out var value))
						{
							string text2 = creatureSO.Rarity.ToString();
							string rarity = TabletLocalization.Get("tablet.common.rarity." + text2.ToLowerInvariant(), text2);
							value = new CreaturePopupEntry(text, rarity, 0);
						}
						value.Count += Mathf.Max(0, spawnableCreature.Count);
						dictionary[key] = value;
					}
				}
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine(TabletLocalization.Get("tablet.map.creatures", "Creatures:"));
			if (dictionary.Count == 0)
			{
				stringBuilder.AppendLine(TabletLocalization.Get("tablet.map.no_known_creatures", "- No known creatures."));
				return stringBuilder.ToString().TrimEnd();
			}
			foreach (KeyValuePair<string, CreaturePopupEntry> item in dictionary)
			{
				stringBuilder.AppendLine($"- {item.Value.Name} x{item.Value.Count} ({item.Value.Rarity})");
			}
			return stringBuilder.ToString().TrimEnd();
		}

		private bool EnsureHabitatPopup()
		{
			if (_habitatPopupInstance != null)
			{
				return true;
			}
			if (_habitatPopupPrefab == null)
			{
				UtilityMethods.Log("[MapApp] Habitat popup prefab is missing.", LogType.Error);
				return false;
			}
			_habitatPopupInstance = UnityEngine.Object.Instantiate(_habitatPopupPrefab, base.transform, worldPositionStays: false);
			_habitatPopupInstance.Initialize(HideHabitatPopup);
			_habitatPopupInstance.Hide();
			return true;
		}

		private void HideHabitatPopup()
		{
			if (!(_habitatPopupInstance == null))
			{
				_habitatPopupInstance.Hide();
			}
		}

		private void RegisterPlayerEvents()
		{
			if (!_playerEventsRegistered && !(Singleton<PlayersManager>.Instance == null))
			{
				Singleton<PlayersManager>.Instance.PlayerRegistered += OnPlayerRegistered;
				Singleton<PlayersManager>.Instance.PlayerUnregistered += OnPlayerUnregistered;
				_playerEventsRegistered = true;
			}
		}

		private void UnregisterPlayerEvents()
		{
			if (_playerEventsRegistered && !(Singleton<PlayersManager>.Instance == null))
			{
				Singleton<PlayersManager>.Instance.PlayerRegistered -= OnPlayerRegistered;
				Singleton<PlayersManager>.Instance.PlayerUnregistered -= OnPlayerUnregistered;
				_playerEventsRegistered = false;
			}
		}

		private void OnPlayerRegistered(PPG_Player player)
		{
			CreatePlayerMarker(player);
		}

		private void OnPlayerUnregistered(PPG_Player player)
		{
			if (player != null && player == _locationOwner)
			{
				ClearLocalLocationMarker();
			}
			if (player != null)
			{
				RemovePlayerMarker(player.OwnerClientId);
			}
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			if (!_isMapOpen || eventData == null || eventData.button != PointerEventData.InputButton.Left || eventData.clickCount < 2 || _contentRect == null || _currentMap == null)
			{
				return;
			}
			GameObject gameObject = eventData.pointerPressRaycast.gameObject;
			if ((gameObject != null && gameObject.GetComponentInParent<Selectable>() != null) || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_contentRect, eventData.position, eventData.pressEventCamera, out var localPoint))
			{
				return;
			}
			Rect rect = _contentRect.rect;
			if (rect.width <= Mathf.Epsilon || rect.height <= Mathf.Epsilon)
			{
				return;
			}
			Vector2 vector = new Vector2((localPoint.x - rect.xMin) / rect.width, (localPoint.y - rect.yMin) / rect.height);
			if (vector.x < 0f || vector.x > 1f || vector.y < 0f || vector.y > 1f)
			{
				return;
			}
			Vector2 worldXZ;
			if (_currentMap.Layer == MapLayer.Underwater && _projection.UsesElevation)
			{
				if (!_groundProjectionSolver.TryMapUVToGroundWorld(_projection, _currentMap, _groundHeightResolver, vector, _underwaterMapGroundRayStartY, Mathf.Max(1f, _underwaterMapGroundRayDistance), _underwaterMapGroundMask, GetUnderwaterMapGroundModelsRoot(), out var worldPosition, out var errorPixels, out var converged, out var hitBufferSaturated))
				{
					UtilityMethods.Log("[MapApp GroundProjection] Location marker ground inverse failed. " + $"Layer={_currentMap.Layer} UV={vector} " + $"RayStartY={_underwaterMapGroundRayStartY:F1} " + $"RayDistance={_underwaterMapGroundRayDistance:F1} " + $"Mask={_underwaterMapGroundMask.value} " + $"HitBufferSaturated={hitBufferSaturated}", LogType.Error);
					return;
				}
				worldXZ = new Vector2(worldPosition.x, worldPosition.z);
				if (!converged)
				{
					UtilityMethods.Log("[MapApp GroundProjection] Location marker ground inverse exceeded round-trip tolerance. " + $"Layer={_currentMap.Layer} UV={vector} World={worldPosition} " + $"ErrorPixels={errorPixels:F2}", LogType.Warning);
				}
			}
			else if (!_projection.TryMapUVToWorldXZ(vector, out worldXZ))
			{
				UtilityMethods.Log($"[MapApp] Location marker inverse projection failed for layer={_currentMap.Layer}.", LogType.Error);
				return;
			}
			SetLocalLocationMarker(vector, worldXZ);
		}

		private void SetLocalLocationMarker(Vector2 mapUV, Vector2 worldXZ)
		{
			PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
			RectTransform rectTransform = ((_locationMarkerContainer != null) ? _locationMarkerContainer : _contentRect);
			OffscreenIndicatorHandler instance = Singleton<OffscreenIndicatorHandler>.Instance;
			if (pPG_Player == null || rectTransform == null || _locationMarkerPrefab == null || instance == null)
			{
				UtilityMethods.Log("[MapApp] Location marker requires a local player, UI marker prefab, container, and indicator handler.", LogType.Error);
				return;
			}
			ClearLocalLocationMarker();
			_locationOwner = pPG_Player;
			_locationLayer = _currentMap.Layer;
			_locationMarkerInstance = UnityEngine.Object.Instantiate(_locationMarkerPrefab, rectTransform, worldPositionStays: false);
			_locationMarkerInstance.Initialize(ClearLocalLocationMarker);
			RectTransform rectTransform2 = _locationMarkerInstance.RectTransform;
			Vector2 anchorMin = (rectTransform2.anchorMax = mapUV);
			rectTransform2.anchorMin = anchorMin;
			rectTransform2.pivot = new Vector2(0.5f, 0.5f);
			rectTransform2.anchoredPosition3D = Vector3.zero;
			rectTransform2.localRotation = Quaternion.identity;
			rectTransform2.localScale = Vector3.one;
			GameObject gameObject = new GameObject("LocalMapLocationTarget");
			_locationWorldTarget = gameObject.AddComponent<MapLocationWorldTarget>();
			_locationWorldTarget.Initialize(worldXZ, pPG_Player.transform);
			Sprite icon = ((_locationIndicatorVisualSettings != null) ? _locationIndicatorVisualSettings.IconSprite : null);
			Vector3 worldOffset = ((_locationIndicatorVisualSettings != null) ? _locationIndicatorVisualSettings.WorldOffset : Vector3.zero);
			_locationIndicatorHandler = instance;
			_locationIndicatorHandle = _locationIndicatorHandler.RegisterTransformUntilReached(_locationWorldTarget.transform, _locationReachDistance, ClearLocalLocationMarker, icon, showDistance: true, worldOffset, pPG_Player.transform, _locationIndicatorVisualSettings);
			if (!_locationIndicatorHandle.IsValid)
			{
				UtilityMethods.Log("[MapApp] Location marker indicator registration failed.", LogType.Error);
				ClearLocalLocationMarker();
			}
			else
			{
				RefreshLocationMarkerVisibility();
			}
		}

		private void RefreshLocationMarkerVisibility()
		{
			if (!(_locationMarkerInstance == null))
			{
				bool active = _currentMap != null && _currentMap.Layer == _locationLayer;
				_locationMarkerInstance.gameObject.SetActive(active);
			}
		}

		private void ClearLocalLocationMarker()
		{
			if (_locationIndicatorHandle.IsValid && _locationIndicatorHandler != null)
			{
				_locationIndicatorHandler.Unregister(_locationIndicatorHandle);
			}
			_locationIndicatorHandle = default;
			_locationIndicatorHandler = null;
			if (_locationMarkerInstance != null)
			{
				UnityEngine.Object.Destroy(_locationMarkerInstance.gameObject);
			}
			if (_locationWorldTarget != null)
			{
				UnityEngine.Object.Destroy(_locationWorldTarget.gameObject);
			}
			_locationMarkerInstance = null;
			_locationWorldTarget = null;
			_locationOwner = null;
		}

		private void ReconcilePlayerMarkers()
		{
			if (Singleton<PlayersManager>.Instance == null)
			{
				return;
			}
			List<ulong> list = new List<ulong>();
			foreach (KeyValuePair<ulong, PlayerMarkerEntry> playerMarker in _playerMarkers)
			{
				if (!Singleton<PlayersManager>.Instance.Players.ContainsKey(playerMarker.Key))
				{
					list.Add(playerMarker.Key);
				}
			}
			for (int i = 0; i < list.Count; i++)
			{
				RemovePlayerMarker(list[i]);
			}
			foreach (PPG_Player value in Singleton<PlayersManager>.Instance.Players.Values)
			{
				CreatePlayerMarker(value);
			}
		}

		private void CreatePlayerMarker(PPG_Player player)
		{
			if (player == null || _playerMarkers.ContainsKey(player.OwnerClientId))
			{
				return;
			}
			RectTransform rectTransform = ((_playerMarkerContainer != null) ? _playerMarkerContainer : _contentRect);
			if (rectTransform == null || _playerMarkerPrefab == null)
			{
				UtilityMethods.Log("[MapApp] Player marker container or prefab is missing.", LogType.Error);
				return;
			}
			PlayerIconUI playerIconUI = UnityEngine.Object.Instantiate(_playerMarkerPrefab, rectTransform, worldPositionStays: false);
			string text = player.PlayerName.Value.ToString();
			playerIconUI.Initialize(text);
			RectTransform rectTransform2 = playerIconUI.transform as RectTransform;
			Vector2 anchorMin = (rectTransform2.anchorMax = new Vector2(0.5f, 0.5f));
			rectTransform2.anchorMin = anchorMin;
			rectTransform2.pivot = new Vector2(0.5f, 0.5f);
			rectTransform2.anchoredPosition3D = Vector3.zero;
			rectTransform2.localRotation = Quaternion.identity;
			rectTransform2.localScale = Vector3.one;
			PlayerMarkerEntry entry = new PlayerMarkerEntry
			{
				Player = player,
				View = playerIconUI,
				Rect = rectTransform2,
				DirectionalArrow = playerIconUI.GetDirectionalArrow(),
				DisplayedName = text
			};
			entry.UnderwaterChanged = (bool _) =>
			{
				UpdatePlayerMarker(entry, force: true);
			};
			player.UnderwaterStateChanged += entry.UnderwaterChanged;
			entry.SteamIdChanged = (ulong _, ulong steamId) =>
			{
				LoadPlayerSteamAvatarAsync(entry, steamId).Forget();
			};
			NetworkVariable<ulong> steamID = player.SteamID;
			steamID.OnValueChanged = (NetworkVariable<ulong>.OnValueChangedDelegate)Delegate.Combine(steamID.OnValueChanged, entry.SteamIdChanged);
			_playerMarkers.Add(player.OwnerClientId, entry);
			LoadPlayerSteamAvatarAsync(entry, player.SteamID.Value).Forget();
			UpdatePlayerMarker(entry, force: true);
		}

		private async UniTaskVoid LoadPlayerSteamAvatarAsync(PlayerMarkerEntry entry, ulong steamIdValue)
		{
			int requestVersion = ++entry.AvatarLoadVersion;
			if (steamIdValue == 0L || entry.View == null)
			{
				return;
			}
			Image image = entry.View.GetImage();
			if (image == null)
			{
				return;
			}
			Sprite sprite = await SteamAvatarCache.GetAvatarSpriteAsync(steamIdValue, image.sprite);
			if (!(this == null) && !(entry.View == null) && requestVersion == entry.AvatarLoadVersion && !(entry.Player == null) && entry.Player.SteamID.Value == steamIdValue)
			{
				image = entry.View.GetImage();
				if (image != null && sprite != null)
				{
					image.sprite = sprite;
				}
			}
		}

		private void RemovePlayerMarker(ulong clientId)
		{
			if (_playerMarkers.Remove(clientId, out var value))
			{
				if (value.Player != null)
				{
					value.Player.UnderwaterStateChanged -= value.UnderwaterChanged;
					NetworkVariable<ulong> steamID = value.Player.SteamID;
					steamID.OnValueChanged = (NetworkVariable<ulong>.OnValueChangedDelegate)Delegate.Remove(steamID.OnValueChanged, value.SteamIdChanged);
				}
				value.AvatarLoadVersion++;
				if (value.View != null)
				{
					UnityEngine.Object.Destroy(value.View.gameObject);
				}
			}
		}

		private void UpdatePlayerMarkers(bool force = false)
		{
			if (!force && !_isMapOpen)
			{
				return;
			}
			foreach (PlayerMarkerEntry value in _playerMarkers.Values)
			{
				UpdatePlayerMarker(value, force);
			}
		}

		private void UpdatePlayerMarker(PlayerMarkerEntry entry, bool force)
		{
			if (entry?.Player == null || entry.View == null || _currentMap == null || !_projection.IsValid)
			{
				if (entry?.View != null)
				{
					entry.View.gameObject.SetActive(value: false);
				}
				return;
			}
			if (entry.Player.IsUnderWater != (_currentMap.Layer == MapLayer.Underwater) || !TryGetPlayerMapProjectionPosition(entry, force, out var projectionPosition) || !_projection.TryWorldToMapUV(projectionPosition, out var mapUV) || mapUV.x < 0f || mapUV.x > 1f || mapUV.y < 0f || mapUV.y > 1f)
			{
				entry.View.gameObject.SetActive(value: false);
				return;
			}
			entry.View.gameObject.SetActive(value: true);
			RectTransform rectTransform = ((_playerMarkerContainer != null) ? _playerMarkerContainer : _contentRect);
			entry.Rect.anchoredPosition = (mapUV - new Vector2(0.5f, 0.5f)) * rectTransform.rect.size;
			if (_liveMap != null) entry.Rect.localScale = Vector3.one * (.22f / Mathf.Max(.01f, _contentRect.localScale.x));
			if (entry.DirectionalArrow != null)
			{
				float z = 0f - entry.Player.transform.eulerAngles.y + (_liveMap != null ? 0f : _currentMap.MarkerRotationOffset);
				entry.DirectionalArrow.localEulerAngles = new Vector3(0f, 0f, z);
			}
			string text = entry.Player.PlayerName.Value.ToString();
			if (!string.Equals(entry.DisplayedName, text, StringComparison.Ordinal))
			{
				entry.DisplayedName = text;
				entry.View.SetInformationText(text);
			}
		}

		private bool TryGetPlayerMapProjectionPosition(PlayerMarkerEntry entry, bool forceGroundProbe, out Vector3 projectionPosition)
		{
			projectionPosition = default;
			if (entry?.Player == null || _currentMap == null)
			{
				return false;
			}
			projectionPosition = entry.Player.transform.position;
			if (_currentMap.Layer != MapLayer.Underwater || !_projection.UsesElevation)
			{
				return true;
			}
			Vector2 vector = new Vector2(projectionPosition.x, projectionPosition.z);
			float num = Mathf.Max(0.1f, _underwaterMapGroundResampleDistance);
			if (forceGroundProbe || !entry.HasGroundProbe || (vector - entry.GroundProbeXZ).sqrMagnitude >= num * num)
			{
				entry.GroundProbeXZ = vector;
				entry.HasGroundProbe = true;
				if (_groundHeightResolver.TryResolveGroundHeight(vector, _underwaterMapGroundRayStartY, Mathf.Max(1f, _underwaterMapGroundRayDistance), _underwaterMapGroundMask, GetUnderwaterMapGroundModelsRoot(), out var groundY, out var hitBufferSaturated))
				{
					entry.MapGroundY = groundY;
					entry.HasMapGroundY = true;
					entry.GroundProbeMissLogged = false;
					entry.GroundHitBufferSaturationLogged = false;
				}
				else if (hitBufferSaturated)
				{
					if (!entry.GroundHitBufferSaturationLogged)
					{
						UtilityMethods.Log("[MapApp GroundProjection] Ground probe hit buffer saturated. " + $"ClientId={entry.Player.OwnerClientId} XZ={vector} " + $"RayStartY={_underwaterMapGroundRayStartY:F1} " + $"RayDistance={_underwaterMapGroundRayDistance:F1} " + $"Mask={_underwaterMapGroundMask.value}", LogType.Error);
						entry.GroundHitBufferSaturationLogged = true;
					}
				}
				else if (!entry.GroundProbeMissLogged)
				{
					UtilityMethods.Log("[MapApp GroundProjection] Ground probe missed. " + $"ClientId={entry.Player.OwnerClientId} XZ={vector} " + $"RayStartY={_underwaterMapGroundRayStartY:F1} " + $"RayDistance={_underwaterMapGroundRayDistance:F1} " + $"Mask={_underwaterMapGroundMask.value}", LogType.Warning);
					entry.GroundProbeMissLogged = true;
				}
			}
			if (!entry.HasMapGroundY)
			{
				return false;
			}
			projectionPosition.y = entry.MapGroundY;
			return true;
		}

		private Transform GetUnderwaterMapGroundModelsRoot()
		{
			if (_underwaterMapGroundModelsRoot != null)
			{
				return _underwaterMapGroundModelsRoot;
			}
			if (string.IsNullOrWhiteSpace(_underwaterMapGroundModelsRootName))
			{
				return null;
			}
			GameObject gameObject = GameObject.Find(_underwaterMapGroundModelsRootName);
			if (gameObject != null)
			{
				_underwaterMapGroundModelsRoot = gameObject.transform;
			}
			return _underwaterMapGroundModelsRoot;
		}

		private Vector2? GetLocalPlayerMapUV()
		{
			PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
			if (pPG_Player == null || _currentMap == null)
			{
				return null;
			}
			if (pPG_Player.IsUnderWater != (_currentMap.Layer == MapLayer.Underwater))
			{
				return null;
			}
			Vector3 projectionPosition = pPG_Player.transform.position;
			if (_currentMap.Layer == MapLayer.Underwater && _projection.UsesElevation && (!_playerMarkers.TryGetValue(pPG_Player.OwnerClientId, out var value) || !TryGetPlayerMapProjectionPosition(value, forceGroundProbe: false, out projectionPosition)))
			{
				return null;
			}
			if (!_projection.TryWorldToMapUV(projectionPosition, out var mapUV))
			{
				return null;
			}
			if (mapUV.x < 0f || mapUV.x > 1f || mapUV.y < 0f || mapUV.y > 1f)
			{
				return null;
			}
			return mapUV;
		}

		private void UpdateLabelVisibility()
		{
			if (!(_contentRect == null) && !(_labelGroup == null))
			{
				float x = _contentRect.localScale.x;
				if (x >= _labelScrollMin)
				{
					_labelGroup.alpha = Mathf.Clamp01((x - _labelScrollMin) / (_labelScrollMax - _labelScrollMin));
				}
				else
				{
					_labelGroup.alpha = 0f;
				}
			}
		}

		public override void Show()
		{
			base.Show();
			_isMapOpen = true;
			_liveMap.SetVisible(true);
			RegisterPlayerEvents();
			ReconcilePlayerMarkers();
			PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
			MapLayer mapLayer = ((pPG_Player != null && pPG_Player.IsUnderWater) ? MapLayer.Underwater : MapLayer.Surface);
			_suppressDropdownCallback = true;
			if (_mapDropdown != null)
			{
				_mapDropdown.SetDropdownIndex((int)mapLayer);
			}
			_suppressDropdownCallback = false;
			ApplyMap(GetDefinition(mapLayer), resetView: true, centerOnLocalPlayer: true, null);
			EventBus<MapActivityEvent>.Raise(new MapActivityEvent
			{
				IsOpened = true
			});
			QuestReferenceHub.Instance.QuestComplete(QuestId.OpenMapApp);
		}

		public override void Hide()
		{
			base.Hide();
			_isMapOpen = false;
			if (_liveMap != null) _liveMap.SetVisible(false);
			HideHabitatPopup();
			EventBus<MapActivityEvent>.Raise(new MapActivityEvent
			{
				IsOpened = false
			});
		}

		private void OnDestroy()
		{
			if (_mapDropdown != null)
			{
				_mapDropdown.onValueChanged.RemoveListener(OnMapDropdownValueChanged);
			}
			UnregisterPlayerEvents();
			ClearLocalLocationMarker();
			List<ulong> list = new List<ulong>(_playerMarkers.Keys);
			for (int i = 0; i < list.Count; i++)
			{
				RemovePlayerMarker(list[i]);
			}
		}
	}
}
