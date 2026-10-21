using System;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Interaction
{
	public class Placer : NetworkBehaviour
	{
		public Action<bool> PlacementModeEvent;

		[SerializeField]
		private Interactor _interactor;

		[SerializeField]
		private LayerMask _placementLayer;

		[SerializeField]
		private float _maxDistance = 3f;

		[Space]
		[SerializeField]
		private float _rotatePerScroll = 15f;

		[SerializeField]
		private bool _ignoreTriggers = true;

		[SerializeField]
		private LayerMask _blockMask;

		[SerializeField]
		private string _ghostLayerName = "Ghost";

		[SerializeField]
		private Material _placementMaterial;

		[SerializeField]
		[Range(0.7f, 0.95f)]
		private float _skin;

		[Header("Debug")]
		[SerializeField]
		private bool _debugDraw = true;

		[SerializeField]
		private bool _debugLog = true;

		private BaseHoldable _currentHeld;

		private BaseInteractable _hoverBaseInteractable;

		private bool _isValid;

		private bool _isPlaceTagValid;

		private bool _ghostActive;

		private float _ghostYaw;

		private bool _snappedThisFrame;

		private bool _isPassivePreview;

		private bool _isPassiveZonePreview;

		private bool _passivePlacementAnimationActive;

		private bool _passivePlacementHintShown;

		private IQuickSnapPlacementZone _activeQuickSnapZone;

		private SnapPoint _hoverSnapPoint;

		private PlacementSurface _currentSurface;

		private GameObject _ghostRootGO;

		private Renderer[] _ghostRenderers;

		private BoxCollider _ghostBox;

		private MaterialPropertyBlock _mpb;

		private readonly Collider[] _overlapBuf = new Collider[64];

		private bool _debugBlocked;

		private bool _debugTagInvalid;

		private bool _debugSupportFailed;

		private bool _debugNoSnapPoint;

		private bool _lastValidState;

		private bool _debugStateInitialized;

		private int _debugSupportedCount;

		private int _debugTotalSamples;

		private float _debugSupportRatio;

		private float _debugRequiredRatio;

		private Vector3[] _supportSamplePoints = new Vector3[5];

		private bool[] _supportSampleHits = new bool[5];

		public bool InPlaceMode { get; private set; }

		public bool IsValid
		{
			get
			{
				if (_isValid)
				{
					return _ghostActive;
				}
				return false;
			}
		}

		public bool HasPassiveSnapPreview
		{
			get
			{
				if (_isPassivePreview)
				{
					return _ghostActive;
				}
				return false;
			}
		}

		public bool IsPassiveSnapPreviewValid
		{
			get
			{
				if (HasPassiveSnapPreview)
				{
					return IsValid;
				}
				return false;
			}
		}

		public bool IsTrayTrolleyPassivePreview
		{
			get
			{
				TrayTrolley trayTrolley;
				if (IsPassiveSnapPreviewValid && _currentHeld is Tray)
				{
					return TryGetTrayTrolley(_activeQuickSnapZone, out trayTrolley);
				}
				return false;
			}
		}

		public IQuickSnapPlacementZone ActiveQuickSnapZone
		{
			get
			{
				if (!_isPassivePreview || !_isPassiveZonePreview)
				{
					return null;
				}
				return _activeQuickSnapZone;
			}
		}

		public SurfaceTags CurrentPlaceTag { get; private set; }

		private void OnEnable()
		{
			Singleton<InputManager>.Instance.InputHandler.Scroll += OnScroll;
		}

		private void OnDisable()
		{
			Singleton<InputManager>.Instance.InputHandler.Scroll -= OnScroll;
		}

		private void OnScroll(InputActionPhase phase, Vector2 input)
		{
			if (InPlaceMode && !(_currentHeld == null) && phase == InputActionPhase.Performed)
			{
				_ghostYaw += input.y * _rotatePerScroll;
				_ghostYaw = Mathf.Repeat(_ghostYaw, 360f);
			}
		}

		private void Update()
		{
			if (base.IsOwner && !(_interactor == null))
			{
				if (InPlaceMode && !_interactor.IsHolding)
				{
					EndPlacement(placeDone: false);
				}
				else if (_isPassivePreview && !_interactor.IsHolding)
				{
					ClearPassiveSnapPreview();
				}
				else if (InPlaceMode)
				{
					RefreshPlacementPreview();
				}
			}
		}

		public void RefreshPlacementPreview()
		{
			if (_ghostRootGO == null || _interactor == null)
			{
				return;
			}
			if (_interactor.TryGetStorageStackTarget(out var _))
			{
				_ghostRootGO.SetActive(value: false);
				_ghostActive = false;
				_isValid = false;
				return;
			}
			Transform cameraTransform = _interactor.Player.CameraTransform;
			if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out var hitInfo, _maxDistance, _placementLayer))
			{
				ShowGhost(hitInfo);
				return;
			}
			Vector3 anchorPoint = cameraTransform.position + cameraTransform.forward * _maxDistance;
			ShowInvalidGhost(anchorPoint, refreshHints: true);
		}

		public void StartPlacement(BaseHoldable baseHoldable)
		{
			_currentHeld = baseHoldable;
			if (!_currentHeld)
			{
				UtilityMethods.Log("CurrentPlacementObject is null!", LogType.Warning);
				return;
			}
			CreateGhost(_currentHeld);
			_isPassivePreview = false;
			SetPassivePlacementAnimation(active: false);
			Singleton<HintsManager>.Instance.ClearAllHints();
			RefreshHeldActionHint();
			Singleton<HintsManager>.Instance.AddHint("Scroll", "key_rotate");
			InPlaceMode = true;
			PlacementModeEvent?.Invoke(obj: true);
			_currentHeld.OnPlacementStart();
			RefreshPlacementPreview();
		}

		public bool TryRefreshPassiveSnapPreview(BaseHoldable holdable, IQuickSnapPlacementZone zone, Vector3 hitPoint)
		{
			if (InPlaceMode)
			{
				return false;
			}
			if (holdable == null || !holdable.IsPlaceable || zone == null || (_interactor != null && _interactor.TryGetStorageStackTarget(out var _)))
			{
				ClearPassiveSnapPreview();
				return false;
			}
			if (TryGetTrayTrolley(zone, out var trayTrolley) && (!(holdable is Tray tray) || !(zone is FixedSnapPlacementZone snapZone) || !trayTrolley.CanPreviewTrayPlacement(tray, snapZone)))
			{
				ClearPassiveSnapPreview();
				return false;
			}
			bool isTrayTrolleyPassivePreview = IsTrayTrolleyPassivePreview;
			if (_ghostRootGO == null || _currentHeld != holdable || !_isPassivePreview)
			{
				CreateGhost(holdable);
			}
			_isPassivePreview = true;
			SetPassivePlacementAnimation(active: true);
			ShowPassiveZoneGhost(zone, hitPoint);
			bool isTrayTrolleyPassivePreview2 = IsTrayTrolleyPassivePreview;
			if (isTrayTrolleyPassivePreview != isTrayTrolleyPassivePreview2)
			{
				_interactor?.RefreshCurrentHints();
			}
			if (isTrayTrolleyPassivePreview2)
			{
				_passivePlacementHintShown = false;
			}
			else if (!_passivePlacementHintShown | isTrayTrolleyPassivePreview)
			{
				ShowPassivePlacementHint();
				_passivePlacementHintShown = true;
			}
			return HasPassiveSnapPreview;
		}

		public void ClearPassiveSnapPreview()
		{
			if (_isPassivePreview)
			{
				bool isTrayTrolleyPassivePreview = IsTrayTrolleyPassivePreview;
				if ((bool)_ghostRootGO)
				{
					_ghostRootGO.SetActive(value: false);
					UnityEngine.Object.Destroy(_ghostRootGO);
				}
				SetPassivePlacementAnimation(active: false);
				ResetGhostState();
				if (_interactor != null && _interactor.IsHolding)
				{
					_interactor.RefreshCurrentHints();
				}
				else if (isTrayTrolleyPassivePreview)
				{
					_interactor?.RefreshCurrentHints();
				}
			}
		}

		public void ResetForHeldItemTransition()
		{
			bool inPlaceMode = InPlaceMode;
			if ((bool)_ghostRootGO)
			{
				UnityEngine.Object.Destroy(_ghostRootGO);
			}
			SetPassivePlacementAnimation(active: false);
			ResetGhostState();
			InPlaceMode = false;
			if (inPlaceMode)
			{
				PlacementModeEvent?.Invoke(obj: false);
			}
		}

		public void EndPlacement(bool placeDone)
		{
			if ((bool)_ghostRootGO)
			{
				UnityEngine.Object.Destroy(_ghostRootGO);
			}
			Singleton<HintsManager>.Instance.ClearAllHints();
			if ((bool)_currentHeld)
			{
				if (!placeDone)
				{
					_currentHeld.EnableHeldHints();
				}
				_currentHeld.OnPlacementEnd(placeDone);
			}
			_hoverSnapPoint = null;
			_hoverBaseInteractable = null;
			SetPassivePlacementAnimation(active: false);
			ResetGhostState();
			InPlaceMode = false;
			PlacementModeEvent?.Invoke(obj: false);
			if ((bool)_interactor)
			{
				_interactor.TryGaze();
			}
		}

		public void SuspendPlacementForThrow()
		{
			if (InPlaceMode)
			{
				if ((bool)_ghostRootGO)
				{
					UnityEngine.Object.Destroy(_ghostRootGO);
				}
				Singleton<HintsManager>.Instance.ClearAllHints();
				if ((bool)_currentHeld)
				{
					_currentHeld.OnPlacementEnd(placed: false);
				}
				_hoverSnapPoint = null;
				_hoverBaseInteractable = null;
				SetPassivePlacementAnimation(active: false);
				ResetGhostState();
				InPlaceMode = false;
				PlacementModeEvent?.Invoke(obj: false);
			}
		}

		private void ShowGhost(RaycastHit hit)
		{
			if (!hit.collider.TryGetComponent<PlacementSurface>(out var component))
			{
				ShowInvalidGhost(hit.point, refreshHints: true);
			}
			else
			{
				ShowGhost(component, hit.point, refreshHints: true);
			}
		}

		private void ShowGhost(PlacementSurface surface, Vector3 hitPoint, bool refreshHints)
		{
			if (surface == null)
			{
				ShowInvalidGhost(hitPoint, refreshHints);
				return;
			}
			_snappedThisFrame = false;
			_currentSurface = surface;
			SurfaceTags surfaceTags = surface.Tags & _currentHeld.PlaceableTags;
			CurrentPlaceTag = SurfaceTags.None;
			foreach (SurfaceTags value in Enum.GetValues(typeof(SurfaceTags)))
			{
				if (value != SurfaceTags.None && (surfaceTags & value) != SurfaceTags.None)
				{
					CurrentPlaceTag = value;
					break;
				}
			}
			_isPlaceTagValid = surface.HasTag(_currentHeld.PlaceableTags);
			ItemOffset placeOffset = _currentHeld.ItemSO.PlaceOffset;
			Vector3 pos;
			Quaternion rot;
			if (surface.Mode == PlacementMode.SnapPoints)
			{
				_snappedThisFrame = TryApplySnapPoint(surface, hitPoint, placeOffset, out pos, out rot, out var chosen);
				_hoverSnapPoint = (_snappedThisFrame ? chosen : null);
				if (!_snappedThisFrame)
				{
					Vector3 worldPos = hitPoint + new Vector3(placeOffset.PositionOffset.x, 0f, placeOffset.PositionOffset.z);
					worldPos = ApplyYClampIfNeeded(surface, worldPos);
					worldPos.y += placeOffset.PositionOffset.y;
					pos = worldPos;
				}
				else
				{
					_hoverBaseInteractable = surface.TryGetDynamicInteractable();
				}
			}
			else
			{
				_hoverSnapPoint = null;
				_hoverBaseInteractable = null;
				_snappedThisFrame = false;
				Vector3 worldPos2 = hitPoint + new Vector3(placeOffset.PositionOffset.x, 0f, placeOffset.PositionOffset.z);
				worldPos2 = ApplyYClampIfNeeded(surface, worldPos2);
				worldPos2.y += placeOffset.PositionOffset.y;
				pos = worldPos2;
				rot = Quaternion.Euler(placeOffset.RotationOffset + new Vector3(0f, _ghostYaw, 0f));
			}
			_ghostRootGO.transform.SetPositionAndRotation(pos, rot);
			if (placeOffset.ScaleOffset != Vector3.zero)
			{
				_ghostRootGO.transform.localScale = placeOffset.ScaleOffset;
			}
			_ghostRootGO.SetActive(value: true);
			_ghostActive = true;
			ValidatePlacement(refreshHints);
		}

		private void ShowPassiveZoneGhost(IQuickSnapPlacementZone zone, Vector3 hitPoint)
		{
			if (zone == null)
			{
				ShowInvalidGhost(hitPoint, refreshHints: false);
				return;
			}
			_isPassiveZonePreview = true;
			_activeQuickSnapZone = zone;
			_snappedThisFrame = false;
			_currentSurface = null;
			_hoverSnapPoint = null;
			_hoverBaseInteractable = null;
			SurfaceTags surfaceTags = zone.Tags & _currentHeld.PlaceableTags;
			CurrentPlaceTag = SurfaceTags.None;
			foreach (SurfaceTags value in Enum.GetValues(typeof(SurfaceTags)))
			{
				if (value != SurfaceTags.None && (surfaceTags & value) != SurfaceTags.None)
				{
					CurrentPlaceTag = value;
					break;
				}
			}
			_isPlaceTagValid = zone.HasTag(_currentHeld.PlaceableTags);
			ItemOffset placeOffset = _currentHeld.ItemSO.PlaceOffset;
			Vector3 position = zone.GetPlacementPoint(hitPoint) + new Vector3(placeOffset.PositionOffset.x, placeOffset.PositionOffset.y, placeOffset.PositionOffset.z);
			Quaternion placementRotation = zone.GetPlacementRotation(Quaternion.Euler(placeOffset.RotationOffset));
			_ghostRootGO.transform.SetPositionAndRotation(position, placementRotation);
			if (placeOffset.ScaleOffset != Vector3.zero)
			{
				_ghostRootGO.transform.localScale = placeOffset.ScaleOffset;
			}
			_ghostRootGO.SetActive(value: true);
			_ghostActive = true;
			ValidatePlacement(refreshHints: false);
		}

		private bool TryApplySnapPoint(PlacementSurface surface, Vector3 hitPoint, ItemOffset soOffset, out Vector3 pos, out Quaternion rot, out SnapPoint chosen)
		{
			pos = Vector3.zero;
			rot = Quaternion.identity;
			chosen = null;
			_debugNoSnapPoint = false;
			if (surface == null)
			{
				_debugNoSnapPoint = true;
				return false;
			}
			if (surface.SnapPoints == null || surface.SnapPoints.Length == 0)
			{
				_debugNoSnapPoint = true;
				if (_debugLog)
				{
					UtilityMethods.Log("<b>[Placer]</b> SnapPoints EMPTY.", LogType.Warning);
				}
				return false;
			}
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			float num5 = float.MaxValue;
			SnapPoint snapPoint = null;
			Vector3 up = surface.transform.up;
			float num6 = surface.SnapRadius * surface.SnapRadius;
			SnapPoint[] snapPoints = surface.SnapPoints;
			foreach (SnapPoint snapPoint2 in snapPoints)
			{
				if (snapPoint2 == null)
				{
					num++;
					continue;
				}
				if (_currentHeld == null || _currentHeld.ItemSO == null)
				{
					num2++;
					continue;
				}
				if (!snapPoint2.CanSnap(_currentHeld))
				{
					num2++;
					continue;
				}
				Vector3 vector = hitPoint - snapPoint2.transform.position;
				float sqrMagnitude = (vector - up * Vector3.Dot(vector, up)).sqrMagnitude;
				if (sqrMagnitude > num6)
				{
					num3++;
					continue;
				}
				num4++;
				if (sqrMagnitude < num5)
				{
					num5 = sqrMagnitude;
					snapPoint = snapPoint2;
				}
			}
			if (snapPoint == null)
			{
				_debugNoSnapPoint = true;
				if (_debugLog)
				{
					UtilityMethods.Log($"<b>[Placer]</b> NoSnapPoint. null={num} tagReject={num2} radiusReject={num3} ok={num4}", LogType.Warning);
				}
				return false;
			}
			pos = snapPoint.transform.position + soOffset.PositionOffset;
			rot = snapPoint.transform.rotation * Quaternion.Euler(soOffset.RotationOffset);
			chosen = snapPoint;
			_debugNoSnapPoint = false;
			return true;
		}

		public bool TryGetPlacementTransform(out Vector3 pos, out Quaternion rot, out SnapPoint snapPoint, out BaseInteractable interactable)
		{
			snapPoint = null;
			interactable = null;
			if (!IsValid || _ghostRootGO == null)
			{
				pos = default;
				rot = default;
				return false;
			}
			pos = _ghostRootGO.transform.position;
			rot = _ghostRootGO.transform.rotation;
			if (_currentSurface != null && _currentSurface.Mode == PlacementMode.SnapPoints)
			{
				snapPoint = _hoverSnapPoint;
				interactable = _hoverBaseInteractable;
			}
			return true;
		}

		private void ShowInvalidGhost(Vector3 anchorPoint, bool refreshHints)
		{
			if ((bool)_ghostRootGO && !(_currentHeld == null))
			{
				ItemOffset placeOffset = _currentHeld.ItemSO.PlaceOffset;
				Vector3 position = anchorPoint + placeOffset.PositionOffset;
				Quaternion rotation = Quaternion.Euler(placeOffset.RotationOffset + new Vector3(0f, _ghostYaw, 0f));
				_ghostRootGO.transform.SetPositionAndRotation(position, rotation);
				if (placeOffset.ScaleOffset != Vector3.zero)
				{
					_ghostRootGO.transform.localScale = placeOffset.ScaleOffset;
				}
				_hoverSnapPoint = null;
				_hoverBaseInteractable = null;
				_snappedThisFrame = false;
				_currentSurface = null;
				CurrentPlaceTag = SurfaceTags.None;
				_isValid = false;
				_isPlaceTagValid = false;
				_ghostActive = true;
				_debugBlocked = false;
				_debugTagInvalid = true;
				_debugSupportFailed = false;
				_debugNoSnapPoint = false;
				_ghostRootGO.SetActive(value: true);
				SetGhostVisual(isValid: false);
				if (refreshHints)
				{
					RefreshHeldActionHint();
				}
				LogPlacementDebugIfNeeded();
			}
		}

		private void ValidatePlacement(bool refreshHints = true)
		{
			if (!_ghostRootGO)
			{
				_isValid = false;
				_debugBlocked = false;
				_debugTagInvalid = true;
				_debugSupportFailed = false;
				_debugNoSnapPoint = false;
				SetGhostVisual(isValid: false);
				if (refreshHints)
				{
					RefreshHeldActionHint();
				}
				LogPlacementDebugIfNeeded();
				return;
			}
			if (_currentSurface == null && !_isPassiveZonePreview)
			{
				_isValid = false;
				_debugBlocked = false;
				_debugTagInvalid = true;
				_debugSupportFailed = false;
				_debugNoSnapPoint = false;
				SetGhostVisual(isValid: false);
				if (refreshHints)
				{
					RefreshHeldActionHint();
				}
				LogPlacementDebugIfNeeded();
				return;
			}
			bool flag = _isPassiveZonePreview || _currentSurface.Mode == PlacementMode.Free;
			bool flag2 = !_isPassiveZonePreview && _currentSurface.Mode == PlacementMode.SnapPoints;
			bool flag3 = false;
			if (_ghostBox != null && (flag || (flag2 && _snappedThisFrame)))
			{
				Vector3 center = _ghostBox.transform.TransformPoint(_ghostBox.center);
				Vector3 halfExtents = HalfExtents(_ghostBox);
				Quaternion rotation = _ghostBox.transform.rotation;
				flag3 = HasBlockingOverlap(center, halfExtents, rotation);
			}
			bool flag4 = true;
			if ((!_isPassiveZonePreview & flag) && _currentSurface.RequireSupport)
			{
				flag4 = HasSufficientSupport(_currentSurface);
			}
			bool flag5 = true;
			if (flag2)
			{
				flag5 = _snappedThisFrame;
			}
			_debugBlocked = flag3;
			_debugTagInvalid = !_isPlaceTagValid;
			_debugSupportFailed = !flag4;
			_isValid = (!flag3 && _isPlaceTagValid) & flag4 & flag5;
			SetGhostVisual(_isValid);
			if (refreshHints)
			{
				RefreshHeldActionHint();
			}
			if (!_isValid)
			{
				CurrentPlaceTag = SurfaceTags.None;
			}
			LogPlacementDebugIfNeeded();
		}

		private void CreateGhost(BaseHoldable holdable)
		{
			_currentHeld = holdable;
			if ((bool)_ghostRootGO)
			{
				UnityEngine.Object.Destroy(_ghostRootGO);
			}
			GameObject gameObject = _currentHeld.CreatePlacementPreview(base.transform);
			_hoverBaseInteractable = null;
			_hoverSnapPoint = null;
			_snappedThisFrame = false;
			_ghostRootGO = gameObject;
			_ghostRenderers = gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
			ApplyPlacementMaterialToGhostRenderers();
			_ghostBox = gameObject.GetComponentInChildren<BoxCollider>();
			UtilityMethods.SetLayerRecursive(_ghostRootGO, _ghostLayerName, includeChildren: true);
			_ghostYaw = 0f;
			_debugStateInitialized = false;
		}

		private void ResetGhostState()
		{
			_snappedThisFrame = false;
			_isPassivePreview = false;
			_isPassiveZonePreview = false;
			_passivePlacementHintShown = false;
			_activeQuickSnapZone = null;
			_currentHeld = null;
			_ghostRootGO = null;
			_ghostRenderers = null;
			_ghostBox = null;
			_ghostActive = false;
			_currentSurface = null;
			_isValid = false;
			_isPlaceTagValid = false;
			_hoverSnapPoint = null;
			_hoverBaseInteractable = null;
			CurrentPlaceTag = SurfaceTags.None;
			_debugStateInitialized = false;
			_passivePlacementAnimationActive = false;
		}

		private void SetPassivePlacementAnimation(bool active)
		{
			if (_passivePlacementAnimationActive != active)
			{
				_passivePlacementAnimationActive = active;
				PlacementModeEvent?.Invoke(active);
			}
		}

		private void RefreshHeldActionHint()
		{
			if (!(_currentHeld == null) && !(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_place");
			}
		}

		private static void ShowPassivePlacementHint()
		{
			if (!(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.ClearAllHints();
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_place");
			}
		}

		private static bool TryGetTrayTrolley(IQuickSnapPlacementZone zone, out TrayTrolley trayTrolley)
		{
			trayTrolley = null;
			if (!(zone is FixedSnapPlacementZone fixedSnapPlacementZone))
			{
				return false;
			}
			trayTrolley = fixedSnapPlacementZone.GetComponentInParent<TrayTrolley>();
			return trayTrolley != null;
		}

		private bool HasSufficientSupport(PlacementSurface surface)
		{
			if (_ghostBox == null)
			{
				return true;
			}
			if (_ghostRenderers == null || _ghostRenderers.Length == 0)
			{
				return true;
			}
			Vector3[] supportSamplePoints = _supportSamplePoints;
			bool[] supportSampleHits = _supportSampleHits;
			if (_ghostBox != null)
			{
				Vector3 center = _ghostBox.center;
				Vector3 vector = _ghostBox.size * 0.5f;
				Vector3 vector2 = new Vector3(center.x, center.y - vector.y, center.z);
				supportSamplePoints[0] = _ghostBox.transform.TransformPoint(vector2 + new Vector3(0f - vector.x, 0f, 0f - vector.z));
				supportSamplePoints[1] = _ghostBox.transform.TransformPoint(vector2 + new Vector3(0f - vector.x, 0f, vector.z));
				supportSamplePoints[2] = _ghostBox.transform.TransformPoint(vector2 + new Vector3(vector.x, 0f, 0f - vector.z));
				supportSamplePoints[3] = _ghostBox.transform.TransformPoint(vector2 + new Vector3(vector.x, 0f, vector.z));
				supportSamplePoints[4] = _ghostBox.transform.TransformPoint(vector2);
			}
			int num = 0;
			int num2 = supportSamplePoints.Length;
			Vector3 direction = ((surface != null) ? (-surface.transform.up) : Vector3.down);
			float maxDistance = 0.12f;
			LayerMask layerMask = ((surface != null) ? surface.SupportMask : ((LayerMask)(-1)));
			for (int i = 0; i < num2; i++)
			{
				supportSampleHits[i] = false;
				if (Physics.Raycast(supportSamplePoints[i] + -direction.normalized * 0.02f, direction, out var hitInfo, maxDistance, layerMask, _ignoreTriggers ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide) && (!(surface != null) || hitInfo.collider.transform.IsChildOf(surface.transform)))
				{
					supportSampleHits[i] = true;
					num++;
				}
			}
			float num3 = (float)num / (float)num2;
			_debugSupportedCount = num;
			_debugTotalSamples = num2;
			_debugSupportRatio = num3;
			_debugRequiredRatio = ((surface != null) ? surface.SupportRatio : 0f);
			return num3 >= ((surface != null) ? surface.SupportRatio : 0f);
		}

		public bool TryGetGhostTransform(out Vector3 pos, out Quaternion rot)
		{
			if (_ghostRootGO == null)
			{
				pos = default;
				rot = default;
				return false;
			}
			pos = _ghostRootGO.transform.position;
			rot = _ghostRootGO.transform.rotation;
			return true;
		}

		private void EnsureMpb()
		{
			if (_mpb == null)
			{
				_mpb = new MaterialPropertyBlock();
			}
		}

		private void ApplyPlacementMaterialToGhostRenderers()
		{
			if (_placementMaterial == null || _ghostRenderers == null)
			{
				return;
			}
			Renderer[] ghostRenderers = _ghostRenderers;
			foreach (Renderer renderer in ghostRenderers)
			{
				if (!(renderer == null))
				{
					Material[] sharedMaterials = renderer.sharedMaterials;
					for (int j = 0; j < sharedMaterials.Length; j++)
					{
						sharedMaterials[j] = _placementMaterial;
					}
					renderer.sharedMaterials = sharedMaterials;
				}
			}
		}

		private void SetGhostVisual(bool isValid)
		{
			EnsureMpb();
			Renderer[] ghostRenderers = _ghostRenderers;
			foreach (Renderer renderer in ghostRenderers)
			{
				if ((bool)renderer)
				{
					renderer.GetPropertyBlock(_mpb);
					_mpb.SetColor("_BaseColor", isValid ? Color.green : Color.red);
					renderer.SetPropertyBlock(_mpb);
				}
			}
		}

		private Vector3 HalfExtents(BoxCollider box)
		{
			Vector3 lossyScale = box.transform.lossyScale;
			return new Vector3(box.size.x * 0.5f * Mathf.Abs(lossyScale.x), box.size.y * 0.5f * Mathf.Abs(lossyScale.y), box.size.z * 0.5f * Mathf.Abs(lossyScale.z));
		}

		private bool HasBlockingOverlap(Vector3 center, Vector3 halfExtents, Quaternion rot)
		{
			int num = Physics.OverlapBoxNonAlloc(center, halfExtents * _skin, _overlapBuf, rot, _blockMask, _ignoreTriggers ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.Collide);
			for (int i = 0; i < num; i++)
			{
				Collider collider = _overlapBuf[i];
				if ((bool)collider && !collider.transform.IsChildOf(_ghostRootGO.transform) && (!collider.attachedRigidbody || !collider.attachedRigidbody.transform.IsChildOf(_ghostRootGO.transform)) && (!(_currentSurface != null) || !collider.transform.IsChildOf(_currentSurface.transform)))
				{
					return true;
				}
			}
			return false;
		}

		private Vector3 ApplyYClampIfNeeded(PlacementSurface surface, Vector3 worldPos)
		{
			if (surface == null || !surface.ClampY)
			{
				return worldPos;
			}
			Vector3 position = surface.transform.InverseTransformPoint(worldPos);
			position.y = surface.ClampLocalY;
			return surface.transform.TransformPoint(position);
		}

		private void LogPlacementDebugIfNeeded()
		{
			if (!_debugLog || (_debugStateInitialized && _lastValidState == _isValid))
			{
				return;
			}
			_debugStateInitialized = true;
			_lastValidState = _isValid;
			if (_isValid)
			{
				UtilityMethods.Log("<b>[Placer]</b> Placement VALID.");
				return;
			}
			string text = "";
			if (_debugBlocked)
			{
				text += "BlockedOverlap; ";
			}
			if (_debugTagInvalid)
			{
				text += "InvalidSurfaceTag; ";
			}
			if (_debugSupportFailed)
			{
				text += $"Support({_debugSupportedCount}/{_debugTotalSamples} ratio={_debugSupportRatio:F2} req={_debugRequiredRatio:F2}); ";
			}
			if (_debugNoSnapPoint && _currentSurface != null && _currentSurface.Mode == PlacementMode.SnapPoints)
			{
				text += "NoFreeSnapPoint; ";
			}
			if (string.IsNullOrEmpty(text))
			{
				text = "UnknownReason";
			}
			UtilityMethods.Log("<b>[Placer]</b> Placement INVALID: " + text, LogType.Warning);
		}

		private void OnDrawGizmos()
		{
			if (!_debugDraw || !Application.isPlaying || !_ghostRootGO)
			{
				return;
			}
			if ((bool)_ghostBox)
			{
				Vector3 pos = _ghostBox.transform.TransformPoint(_ghostBox.center);
				Vector3 vector = HalfExtents(_ghostBox);
				Quaternion rotation = _ghostBox.transform.rotation;
				Matrix4x4 matrix = Gizmos.matrix;
				Gizmos.matrix = Matrix4x4.TRS(pos, rotation, Vector3.one);
				Gizmos.color = (_isValid ? Color.green : Color.red);
				Gizmos.DrawCube(Vector3.zero, vector * 2f * _skin);
				Gizmos.color = Color.yellow;
				Gizmos.DrawWireCube(Vector3.zero, vector * 2f * _skin);
				Gizmos.matrix = matrix;
			}
			if (_currentSurface != null && _currentSurface.Mode == PlacementMode.Free && _currentSurface.RequireSupport)
			{
				int num = _supportSamplePoints.Length;
				for (int i = 0; i < num; i++)
				{
					Vector3 vector2 = _supportSamplePoints[i];
					Gizmos.color = (_supportSampleHits[i] ? Color.green : Color.red);
					Gizmos.DrawSphere(vector2, 0.025f);
					Gizmos.DrawLine(vector2 + Vector3.up * 0.05f, vector2 + Vector3.down * 0.25f);
				}
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "Placer";
		}
	}
}
