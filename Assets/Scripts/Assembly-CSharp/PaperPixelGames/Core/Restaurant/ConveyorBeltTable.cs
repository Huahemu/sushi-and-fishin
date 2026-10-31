using System;
using System.Collections.Generic;
using DG.Tweening;
using EPOOutline;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Economy;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Interaction.ToolTip;
using PaperPixelGames.Core.Kitchen;
using PaperPixelGames.Core.Kitchen.ServedDishes;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking.Chat;
using TMPro;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Restaurant
{
	public class ConveyorBeltTable : BaseUsable
	{
		[Serializable]
		private sealed class DirtyPlateMaterialDefinition
		{
			public PlateColor Color;

			public Material Material;
		}

		internal struct DirtyPlateSnapshot : INetworkSerializable, IEquatable<DirtyPlateSnapshot>
		{
			public byte Color;

			public int DishPrice;

			public DirtyPlateSnapshot(PlateColor color, int dishPrice)
			{
				Color = (byte)color;
				DishPrice = dishPrice;
			}

			public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
			{
				serializer.SerializeValue(ref Color);
				serializer.SerializeValue(ref DishPrice, default(FastBufferWriter.ForPrimitives));
			}

			public bool Equals(DirtyPlateSnapshot other)
			{
				if (Color == other.Color)
				{
					return DishPrice == other.DishPrice;
				}
				return false;
			}
		}

		private sealed class DirtyPlateStackView
		{
			private readonly ConveyorBeltTable _table;

			private readonly List<GameObject> _visuals = new List<GameObject>();

			private readonly List<DirtyPlateScanTarget> _scanTargets = new List<DirtyPlateScanTarget>();

			public int Count => _visuals.Count;

			public bool HasVisuals => _visuals.Count > 0;

			public DirtyPlateStackView(ConveyorBeltTable table)
			{
				_table = table;
			}

			public void Spawn(int index, PlateColor color, int dishPrice)
			{
				if (!(_table._dirtyPlateSpawnAnchor == null) && !(_table._dirtyPlateVisualPrefab == null) && _visuals.Count < _table._dirtyPlateVisualMax)
				{
					index = Mathf.Max(0, index);
					GameObject gameObject = UnityEngine.Object.Instantiate(_table._dirtyPlateVisualPrefab, _table._dirtyPlateSpawnAnchor);
					gameObject.SetActive(value: true);
					gameObject.transform.localPosition = new Vector3(0f, (float)index * _table._dirtyPlateStackHeightStep, 0f);
					gameObject.transform.localRotation = Quaternion.Euler(0f, (float)index * _table._dirtyPlateStackRotationStep, 0f);
					_visuals.Add(gameObject);
					DirtyPlateScanTarget component = null;
					if (!gameObject.TryGetComponent<DirtyPlateScanTarget>(out component))
					{
						component = gameObject.GetComponentInChildren<DirtyPlateScanTarget>(includeInactive: true);
					}
					if (component == null)
					{
						UtilityMethods.Log("[ConveyorBeltTable][DirtyPlateStack] Dirty plate prefab missing DirtyPlateScanTarget.", LogType.Warning);
						return;
					}
					component.Setup(index, color, dishPrice);
					Material dirtyPlateMaterial = _table.GetDirtyPlateMaterial(color);
					component.ApplyPlateMaterial(dirtyPlateMaterial, _table._dirtyPlateMaterialIndex);
					_scanTargets.Add(component);
				}
			}

			public void Clear()
			{
				for (int i = 0; i < _visuals.Count; i++)
				{
					if (_visuals[i] != null)
					{
						UnityEngine.Object.Destroy(_visuals[i]);
					}
				}
				_visuals.Clear();
				_scanTargets.Clear();
			}

			public void ResetScannedState()
			{
				for (int i = 0; i < _scanTargets.Count; i++)
				{
					if (_scanTargets[i] != null)
					{
						_scanTargets[i].SetScanned(scanned: false);
					}
				}
			}

			public bool TryGetScanBounds(Vector3 localOffset, out Vector3 bottomScanPosition, out Vector3 topScanPosition)
			{
				bottomScanPosition = default;
				topScanPosition = default;
				DirtyPlateScanTarget dirtyPlateScanTarget = null;
				DirtyPlateScanTarget dirtyPlateScanTarget2 = null;
				for (int i = 0; i < _scanTargets.Count; i++)
				{
					DirtyPlateScanTarget dirtyPlateScanTarget3 = _scanTargets[i];
					if (!(dirtyPlateScanTarget3 == null) && dirtyPlateScanTarget3.gameObject.activeInHierarchy)
					{
						if (dirtyPlateScanTarget == null || dirtyPlateScanTarget3.PlateIndex < dirtyPlateScanTarget.PlateIndex)
						{
							dirtyPlateScanTarget = dirtyPlateScanTarget3;
						}
						if (dirtyPlateScanTarget2 == null || dirtyPlateScanTarget3.PlateIndex > dirtyPlateScanTarget2.PlateIndex)
						{
							dirtyPlateScanTarget2 = dirtyPlateScanTarget3;
						}
					}
				}
				if (dirtyPlateScanTarget == null || dirtyPlateScanTarget2 == null)
				{
					return false;
				}
				Vector3 worldOffset = GetWorldOffset(localOffset);
				bottomScanPosition = GetScanWorldPosition(dirtyPlateScanTarget) + worldOffset;
				topScanPosition = GetScanWorldPosition(dirtyPlateScanTarget2) + worldOffset;
				return true;
			}

			public void CollectTargetsCrossedByScanHeight(float fromHeight, float toHeight, List<DirtyPlateScanTarget> results)
			{
				if (results == null)
				{
					return;
				}
				results.Clear();
				int num = int.MaxValue;
				int num2 = int.MinValue;
				for (int i = 0; i < _scanTargets.Count; i++)
				{
					DirtyPlateScanTarget dirtyPlateScanTarget = _scanTargets[i];
					if (!(dirtyPlateScanTarget == null) && dirtyPlateScanTarget.gameObject.activeInHierarchy)
					{
						num = Mathf.Min(num, dirtyPlateScanTarget.PlateIndex);
						num2 = Mathf.Max(num2, dirtyPlateScanTarget.PlateIndex);
					}
				}
				if (num == int.MaxValue)
				{
					return;
				}
				float num3 = Mathf.Clamp01(Mathf.Min(fromHeight, toHeight));
				float num4 = Mathf.Clamp01(Mathf.Max(fromHeight, toHeight));
				int num5 = Mathf.Max(1, num2 - num);
				for (int j = 0; j < _scanTargets.Count; j++)
				{
					DirtyPlateScanTarget dirtyPlateScanTarget2 = _scanTargets[j];
					if (!(dirtyPlateScanTarget2 == null) && dirtyPlateScanTarget2.gameObject.activeInHierarchy)
					{
						float num6 = (float)(dirtyPlateScanTarget2.PlateIndex - num) / (float)num5;
						if (!(num6 < num3 - 0.001f) && !(num6 > num4 + 0.001f))
						{
							results.Add(dirtyPlateScanTarget2);
						}
					}
				}
				results.Sort((DirtyPlateScanTarget left, DirtyPlateScanTarget right) => (!(toHeight < fromHeight)) ? left.PlateIndex.CompareTo(right.PlateIndex) : right.PlateIndex.CompareTo(left.PlateIndex));
			}

			private Vector3 GetScanWorldPosition(DirtyPlateScanTarget target)
			{
				if (target.ScanCollider != null)
				{
					return target.ScanCollider.bounds.center;
				}
				return target.transform.position;
			}

			private Vector3 GetWorldOffset(Vector3 localOffset)
			{
				if (_table._dirtyPlateSpawnAnchor != null)
				{
					return _table._dirtyPlateSpawnAnchor.TransformVector(localOffset);
				}
				return _table.transform.TransformVector(localOffset);
			}
		}

		private sealed class ConveyorPaymentScanSession
		{
			private const string LogPrefix = "[ConveyorBeltTable][PaymentScan]";

			private readonly ConveyorBeltTable _table;

			private readonly HashSet<int> _scannedPlateIndexes = new HashSet<int>();

			private readonly Dictionary<PlateColor, int> _scannedPlateCounts = new Dictionary<PlateColor, int>();

			private readonly List<DirtyPlateScanTarget> _sweepTargets = new List<DirtyPlateScanTarget>();

			private int _requiredScanCount;

			private bool _isHolding;

			private bool _isActive;

			private bool _isCompleted;

			private bool _isReadyToComplete;

			private float _scanHeight;

			private Vector2 _holdLookOffset;

			private float _nextScanTime;

			private float _nextFailFeedbackTime;

			public bool IsActive => _isActive;

			public bool IsCompleted => _isCompleted;

			public bool IsReadyToComplete => _isReadyToComplete;

			public ConveyorPaymentScanSession(ConveyorBeltTable table)
			{
				_table = table;
			}

			public void Begin(int requiredScanCount)
			{
				_requiredScanCount = Mathf.Max(0, requiredScanCount);
				_isActive = true;
				_isHolding = false;
				_isCompleted = false;
				_isReadyToComplete = false;
				_scanHeight = 1f;
				_holdLookOffset = Vector2.zero;
				_nextScanTime = 0f;
				_nextFailFeedbackTime = 0f;
				ClearScannedRuntime();
				_table._activePaymentScanner?.ResetPaymentUI();
				_table._activePaymentScanner?.SetScanGuideVisible(visible: true);
				_table.RefreshPaymentCounterLocal(0, _requiredScanCount);
				SetScannerToTopPlateImmediate();
			}

			public void Stop()
			{
				_isActive = false;
				_isHolding = false;
				_isCompleted = false;
				_isReadyToComplete = false;
				_scanHeight = 1f;
				_holdLookOffset = Vector2.zero;
				_nextScanTime = 0f;
				_nextFailFeedbackTime = 0f;
				_table._activePaymentScanner?.SetScanGuideVisible(visible: false);
				ClearScannedRuntime();
			}

			public void ClearScannedRuntime()
			{
				_scannedPlateIndexes.Clear();
				_scannedPlateCounts.Clear();
				_isCompleted = false;
				_isReadyToComplete = false;
				_scanHeight = 1f;
				_holdLookOffset = Vector2.zero;
				_table._dirtyPlateStack?.ResetScannedState();
			}

			public void SetHolding(bool holding)
			{
				if (!holding)
				{
					_isHolding = false;
					_scanHeight = 1f;
					_holdLookOffset = Vector2.zero;
				}
				else if (_isActive && !_isCompleted && !_isReadyToComplete && _table._currentState.Value == ConveyorBeltTableState.AwaitingPayment)
				{
					_isHolding = true;
					_nextScanTime = 0f;
				}
			}

			public void Tick(Vector2 normalizedMouseDelta, float deltaTime)
			{
				if (_isActive)
				{
					float scanHeight = _scanHeight;
					if (_isHolding)
					{
						AccumulateHoldInput(normalizedMouseDelta);
					}
					TickScannerVisual(GetVisualInputOffset(), deltaTime);
					if (_table._currentState.Value != ConveyorBeltTableState.AwaitingPayment)
					{
						_isHolding = false;
						_scanHeight = 1f;
						_holdLookOffset = Vector2.zero;
					}
					else if (_isHolding && !_isCompleted && !TryScanDirtyPlatesAlongSweep(scanHeight, _scanHeight) && !(Time.time < _nextScanTime))
					{
						_nextScanTime = Time.time + Mathf.Max(0.01f, _table._scanTickInterval);
						TryScanDirtyPlate();
					}
				}
			}

			private bool TryScanDirtyPlatesAlongSweep(float fromHeight, float toHeight)
			{
				if (_table._dirtyPlateStack == null)
				{
					return false;
				}
				_table._dirtyPlateStack.CollectTargetsCrossedByScanHeight(fromHeight, toHeight, _sweepTargets);
				bool result = false;
				for (int i = 0; i < _sweepTargets.Count; i++)
				{
					DirtyPlateScanTarget dirtyPlateScanTarget = _sweepTargets[i];
					if (!(dirtyPlateScanTarget == null) && !dirtyPlateScanTarget.IsScanned && !_scannedPlateIndexes.Contains(dirtyPlateScanTarget.PlateIndex))
					{
						RegisterScanTarget(dirtyPlateScanTarget);
						result = true;
						if (_isReadyToComplete || _isCompleted)
						{
							break;
						}
					}
				}
				return result;
			}

			private void TickScannerVisual(Vector2 normalizedOffset, float deltaTime)
			{
				PaymentScannerView activePaymentScanner = _table._activePaymentScanner;
				if (!(activePaymentScanner == null))
				{
					if (TryGetActiveScanBounds(out var bottomScanPosition, out var topScanPosition))
					{
						Vector3 targetScanWorldPosition = Vector3.Lerp(bottomScanPosition, topScanPosition, GetTargetScanHeight());
						activePaymentScanner.TickVisualAtScanPosition(normalizedOffset, deltaTime, targetScanWorldPosition);
						DrawRayPreview(activePaymentScanner);
					}
					else
					{
						activePaymentScanner.TickVisual(normalizedOffset, deltaTime);
						DrawRayPreview(activePaymentScanner);
					}
				}
			}

			private void AccumulateHoldInput(Vector2 normalizedMouseDelta)
			{
				float num = Mathf.Max(0f, _table._holdScanMouseSensitivity);
				Vector2 vector = normalizedMouseDelta * num;
				_scanHeight = Mathf.Clamp01(_scanHeight + vector.y);
				_holdLookOffset.x = Mathf.Clamp(_holdLookOffset.x + vector.x, -1f, 1f);
				_holdLookOffset.y = Mathf.Clamp(_holdLookOffset.y + vector.y, -1f, 1f);
			}

			private Vector2 GetVisualInputOffset()
			{
				if (!_isHolding)
				{
					return Vector2.zero;
				}
				return _holdLookOffset;
			}

			private float GetTargetScanHeight()
			{
				if (!_isHolding)
				{
					return 1f;
				}
				return _scanHeight;
			}

			private bool TryGetActiveScanBounds(out Vector3 bottomScanPosition, out Vector3 topScanPosition)
			{
				bottomScanPosition = default;
				topScanPosition = default;
				if (_table._dirtyPlateStack == null)
				{
					return false;
				}
				return _table._dirtyPlateStack.TryGetScanBounds(GetActiveScanOffset(), out bottomScanPosition, out topScanPosition);
			}

			private Vector3 GetActiveScanOffset()
			{
				if (!_isHolding)
				{
					return _table._paymentScannerPlateScanOffset;
				}
				return _table._paymentScannerPlateScanOffset + _table._paymentScannerHoldScanOffset;
			}

			private void SetScannerToTopPlateImmediate()
			{
				PaymentScannerView activePaymentScanner = _table._activePaymentScanner;
				if (!(activePaymentScanner == null))
				{
					if (TryGetActiveScanBounds(out var _, out var topScanPosition))
					{
						activePaymentScanner.SetVisualScanPositionImmediate(topScanPosition);
					}
					else
					{
						activePaymentScanner.ResetVisualPoseImmediate();
					}
				}
			}

			private void DrawRayPreview(PaymentScannerView scanner)
			{
				if (_table._debugPaymentRay && _table._debugPaymentRayPreview)
				{
					Ray ray = scanner.CreateScanRay();
					Debug.DrawRay(ray.origin, ray.direction * _table._paymentScanDistance, new Color(0.35f, 0.85f, 1f), 0f);
					_table.DrawDebugOriginMarker(ray.origin, new Color(0.35f, 0.85f, 1f));
				}
			}

			private void TryScanDirtyPlate()
			{
				if (_table._currentInteractor == null)
				{
					return;
				}
				PaymentScannerView activePaymentScanner = _table._activePaymentScanner;
				if (activePaymentScanner == null)
				{
					return;
				}
				Ray ray = activePaymentScanner.CreateScanRay();
				RaycastHit[] array = Physics.RaycastAll(ray, _table._paymentScanDistance, _table._paymentPlateLayerMask);
				if (array == null || array.Length == 0)
				{
					_table.DrawPaymentRayDebug(ray, _table._paymentScanDistance, Color.red);
					PlayFailFeedback();
					return;
				}
				Array.Sort(array, (RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance));
				DirtyPlateScanTarget dirtyPlateScanTarget = null;
				RaycastHit raycastHit = default;
				RaycastHit raycastHit2 = default;
				bool flag = false;
				for (int num = 0; num < array.Length; num++)
				{
					RaycastHit raycastHit3 = array[num];
					if (!raycastHit3.collider.TryGetComponent<DirtyPlateScanTarget>(out var component))
					{
						component = raycastHit3.collider.GetComponentInParent<DirtyPlateScanTarget>();
					}
					if (component == null)
					{
						if (!flag)
						{
							raycastHit2 = raycastHit3;
							flag = true;
						}
						continue;
					}
					if (component.IsScanned || _scannedPlateIndexes.Contains(component.PlateIndex))
					{
						if ((object)dirtyPlateScanTarget == null)
						{
							dirtyPlateScanTarget = component;
						}
						raycastHit = raycastHit3;
						continue;
					}
					RegisterScanHit(ray, raycastHit3, component);
					return;
				}
				if (dirtyPlateScanTarget != null)
				{
					_table.DrawPaymentRayDebug(ray, raycastHit.distance, Color.blue);
					_table.DrawHitPointDebug(raycastHit.point, Color.blue);
					UtilityMethods.Log(string.Format("{0} Target already scanned. SeatId={1}, PlateIndex={2}", "[ConveyorBeltTable][PaymentScan]", _table._seatId, dirtyPlateScanTarget.PlateIndex));
				}
				else if (flag)
				{
					_table.DrawPaymentRayDebug(ray, raycastHit2.distance, new Color(1f, 0.5f, 0f));
					_table.DrawHitPointDebug(raycastHit2.point, new Color(1f, 0.5f, 0f));
					UtilityMethods.Log(string.Format("{0} Hit non-target collider. SeatId={1}, Collider={2}, Point={3}", "[ConveyorBeltTable][PaymentScan]", _table._seatId, raycastHit2.collider.name, raycastHit2.point), LogType.Warning);
					PlayFailFeedback();
				}
				else
				{
					_table.DrawPaymentRayDebug(ray, _table._paymentScanDistance, Color.red);
					PlayFailFeedback();
				}
			}

			private void RegisterScanHit(Ray ray, RaycastHit hit, DirtyPlateScanTarget target)
			{
				_table.DrawPaymentRayDebug(ray, hit.distance, Color.green);
				_table.DrawHitPointDebug(hit.point, Color.cyan);
				RegisterScanTarget(target);
			}

			private void RegisterScanTarget(DirtyPlateScanTarget target)
			{
				if (!(target == null) && !target.IsScanned && !_scannedPlateIndexes.Contains(target.PlateIndex))
				{
					UtilityMethods.Log(string.Format("{0} Valid target scan. SeatId={1}, PlateIndex={2}, Color={3}, DishPrice={4}", "[ConveyorBeltTable][PaymentScan]", _table._seatId, target.PlateIndex, target.PlateColor, target.DishPrice));
					target.SetScanned(scanned: true);
					_scannedPlateIndexes.Add(target.PlateIndex);
					if (!_scannedPlateCounts.ContainsKey(target.PlateColor))
					{
						_scannedPlateCounts[target.PlateColor] = 0;
					}
					_scannedPlateCounts[target.PlateColor]++;
					_table.PlayPaymentSuccessLocal();
					target.PlayScanHighlight();
					_table._activePaymentScanner?.RegisterScannedPlate(target.PlateColor, target.DishPrice);
					_table._activePaymentScanner?.PlaySuccessFeedback();
					_table.RefreshPaymentCounterLocal(_scannedPlateIndexes.Count, _requiredScanCount);
					SetReadyToCompleteIfAllPlatesScanned();
				}
			}

			private void SetReadyToCompleteIfAllPlatesScanned()
			{
				if (!_isReadyToComplete && !_isCompleted)
				{
					UtilityMethods.Log(string.Format("{0} Progress. SeatId={1}, Scanned={2}, Required={3}", "[ConveyorBeltTable][PaymentScan]", _table._seatId, _scannedPlateIndexes.Count, _requiredScanCount));
					if (_requiredScanCount > 0 && _scannedPlateIndexes.Count >= _requiredScanCount)
					{
						_isReadyToComplete = true;
						_isHolding = false;
						_scanHeight = 1f;
						_holdLookOffset = Vector2.zero;
						_table._activePaymentScanner?.SetScanGuideVisible(visible: false);
						_table.SetPaymentUseHintsReadyToComplete();
					}
				}
			}

			public void TryCompletePayment()
			{
				if (!_isCompleted && _isActive && _isReadyToComplete)
				{
					_isCompleted = true;
					_isHolding = false;
					_isActive = false;
					_isReadyToComplete = false;
					_scanHeight = 1f;
					_holdLookOffset = Vector2.zero;
					_table._activePaymentScanner?.SetScanGuideVisible(visible: false);
					_table.ClientCompletePaymentScan(GetScannedPlateCount(PlateColor.None), GetScannedPlateCount(PlateColor.Blue), GetScannedPlateCount(PlateColor.Green), GetScannedPlateCount(PlateColor.Red), GetScannedPlateCount(PlateColor.Yellow));
				}
			}

			private byte GetScannedPlateCount(PlateColor color)
			{
				if (!_scannedPlateCounts.TryGetValue(color, out var value))
				{
					return 0;
				}
				return (byte)Mathf.Clamp(value, 0, 255);
			}

			private void PlayFailFeedback()
			{
				if (!(Time.time < _nextFailFeedbackTime))
				{
					_nextFailFeedbackTime = Time.time + Mathf.Max(0f, _table._failFeedbackCooldown);
					_table._activePaymentScanner?.PlayFailFeedback();
				}
			}
		}

		[Header("Eating Tuning")]
		[SerializeField]
		private int _maxEatingTime = 35;

		[SerializeField]
		private int _minEatingTime = 20;

		[Header("Customer Meal Count (Server)")]
		[SerializeField]
		private int _minMealsPerCustomer = 2;

		[SerializeField]
		private int _maxMealsPerCustomer = 30;

		[SerializeField]
		private float _waitingTimeForMeal = 90f;

		[SerializeField]
		[Min(1f)]
		private float _mealTimeoutWarningSeconds = 30f;

		[Header("UI")]
		[SerializeField]
		private TextMeshProUGUI _debugText;

		[SerializeField]
		private TextMeshProUGUI _paymentCountText;

		[Header("Dish Anchors")]
		[SerializeField]
		private Transform _servedDishAnchor;

		[SerializeField]
		private Transform _dirtyPlateSpawnAnchor;

		[Header("Unlock")]
		[SerializeField]
		private int _seatId;

		[SerializeField]
		private PhysicalStateController _physicalStateController;

		[Header("Conveyor Belt")]
		[SerializeField]
		private ConveyorBelt _belt;

		[SerializeField]
		[Min(0.02f)]
		private float _dishClaimProbeInterval = 0.1f;

		[SerializeField]
		[Min(0f)]
		private float _dishClaimProbePadding = 0.2f;

		[Header("Dirty Plate Visual Default")]
		[SerializeField]
		private GameObject _dirtyPlateVisualPrefab;

		[SerializeField]
		private int _dirtyPlateVisualMax = 30;

		[SerializeField]
		private float _dirtyPlateStackHeightStep = 0.05f;

		[SerializeField]
		private float _dirtyPlateStackRotationStep = 8f;

		[Header("Dirty Plate Visuals By Color")]
		[SerializeField]
		private List<DirtyPlateMaterialDefinition> _dirtyPlateMaterialDefinitions = new List<DirtyPlateMaterialDefinition>();

		[SerializeField]
		[Min(0f)]
		private int _dirtyPlateMaterialIndex;

		[Header("Dirty Plate Payment Focus")]
		[SerializeField]
		private float _dirtyPlatePaymentMoveDuration = 0.25f;

		[SerializeField]
		private Ease _dirtyPlatePaymentMoveEase = Ease.OutCubic;

		[SerializeField]
		private Vector3 _dirtyPlatePaymentFocusLocalOffset = new Vector3(0f, 0.15f, 0f);

		[Header("Payment Scan")]
		[SerializeField]
		private LayerMask _paymentPlateLayerMask;

		[SerializeField]
		private float _paymentScanDistance = 2.5f;

		[SerializeField]
		private Vector3 _paymentScannerPlateScanOffset = Vector3.zero;

		[SerializeField]
		private Vector3 _paymentScannerHoldScanOffset = Vector3.zero;

		[Header("Payment Hold Scan")]
		[SerializeField]
		private float _scanTickInterval = 0.1f;

		[SerializeField]
		private float _failFeedbackCooldown = 0.2f;

		[SerializeField]
		[Min(0f)]
		private float _holdScanMouseSensitivity = 1f;

		[Header("Payment Audio")]
		[SerializeField]
		private AudioClip _scanSuccessClip;

		private AudioSource _paymentScanAudio;

		[Header("Payment Scanner")]
		[SerializeField]
		private PaymentScannerView _paymentScannerPrefab;

		[SerializeField]
		private Transform _scannerLocalPosition;

		[SerializeField]
		private Vector3 _scannerLocalEuler = new Vector3(8f, -12f, 0f);

		[Header("Debug")]
		[SerializeField]
		private bool _debugPaymentRay = true;

		[SerializeField]
		private bool _debugPaymentRayPreview = true;

		[SerializeField]
		private float _debugRayDuration = 0.15f;

		[SerializeField]
		private Transform _customerSeatPoint;

		private PaymentScannerView _activePaymentScanner;

		private DirtyPlateStackView _dirtyPlateStack;

		private ConveyorPaymentScanSession _paymentScanSession;

		private bool _cursorHiddenForPaymentUse;

		private bool _cursorVisibleBeforePaymentUse;

		private CursorLockMode _cursorLockStateBeforePaymentUse;

		private bool _paymentUseCameraTargetCached;

		private Transform _usableCameraTrackingTargetBeforePaymentUse;

		private Transform _usableCameraLookAtTargetBeforePaymentUse;

		private bool _usableCameraCustomLookAtBeforePaymentUse;

		private ResidentController _paymentHighlightResident;

		private int _stateVersion;

		private bool _isChangingState;

		private bool _serverPaymentCompleted;

		private bool _isClaimingDish;

		private float _nextDishClaimProbeAt;

		private ConveyorBeltTableTrigger _dishClaimProbe;

		[SerializeField]
		private int _debugMealCount = 3;

		[SerializeField]
		private int _debugDishPrice = 25;

		[SerializeField]
		private bool _debugRepairIfNeeded = true;

		[SerializeField]
		private ConveyorBeltTableState _debugTargetState;

		private int _initialMealCount;

		private float _takingOrderTimer;

		private bool _takingOrderTimerActive;

		private bool _mealTimeoutWarningShown;

		private int _totalPayment;

		private int _currentDishPrice;

		private PlateColor _currentPlateColor;

		private NetworkObjectReference _currentDishRef;

		private bool _hasIncomingCustomer;

		private bool _restaurantClosePendingPaymentAfterEating;

		private Tween _dirtyPlateAnchorTween;

		private bool _dirtyPlateAnchorDefaultCached;

		private Vector3 _dirtyPlateAnchorDefaultLocalPosition;

		private Quaternion _dirtyPlateAnchorDefaultLocalRotation;

		private readonly Dictionary<PlateColor, int> _serverRequiredPlateCounts = new Dictionary<PlateColor, int>();

		private readonly NetworkVariable<ConveyorBeltTableState> _currentState = new NetworkVariable<ConveyorBeltTableState>(ConveyorBeltTableState.Empty);

		private readonly NetworkVariable<int> _mealsRemaining = new NetworkVariable<int>(0);

		private readonly NetworkVariable<int> _dirtyPlateCount = new NetworkVariable<int>(0);

		private readonly NetworkList<DirtyPlateSnapshot> _dirtyPlateSnapshots = new NetworkList<DirtyPlateSnapshot>();

		private readonly NetworkVariable<double> _staffScanStart = new NetworkVariable<double>(0.0);

		private readonly NetworkVariable<double> _staffScanEnd = new NetworkVariable<double>(0.0);

		private int _staffScanner;

		private int _staffScanVersion;

		private LineRenderer _staffScanGraphic;

		private Material _staffScanMaterial;

		private int _staffLastScannedPlate = -1;

		public Transform CustomerSeatPoint => _customerSeatPoint;

		public bool HasIncomingCustomer => _hasIncomingCustomer;

		public ConveyorBeltTableState CurrentState => _currentState.Value;

		public PhysicalStateController PhysicalStateController => _physicalStateController;

		public int SeatId => _seatId;

		protected override bool AppliesBaseUsePose => false;

		public int ServerStaffPaymentVersion => _stateVersion;

		public bool ServerStaffPaymentAvailable
		{
			get
			{
				if (base.IsServer && base.IsSpawned && CurrentState == ConveyorBeltTableState.AwaitingPayment && !_serverPaymentCompleted && _dirtyPlateCount.Value > 0)
				{
					return _currentInteractor == null;
				}
				return false;
			}
		}

		protected override void Awake()
		{
			base.Awake();
			if (_outlinable == null)
			{
				_outlinable = GetComponentInChildren<Outlinable>(includeInactive: true);
			}
			_dirtyPlateStack = new DirtyPlateStackView(this);
			_paymentScanSession = new ConveyorPaymentScanSession(this);
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			if (_physicalStateController != null)
			{
				_physicalStateController.OnPhysicallyUsableChanged += HandlePhysicalRepair;
			}
			else
			{
				UtilityMethods.Log("[ConveyorBeltTable] PhysicalStateController is missing.", LogType.Warning);
			}
			if (!base.IsServer)
			{
				Singleton<DelayHelper>.Instance.DelayActionUntilEndOfFrame(ClientRestoreLateJoinVisualState);
			}
		}

		public override void OnNetworkDespawn()
		{
			StaffScanDispose();
			ClearPaymentResidentHighlight();
			if (_physicalStateController != null)
			{
				_physicalStateController.OnPhysicallyUsableChanged -= HandlePhysicalRepair;
			}
			base.OnNetworkDespawn();
		}

		private void Update()
		{
			StaffScanPresentation();
			if (base.IsServer && base.IsSpawned && base.NetworkManager != null && base.NetworkManager.IsListening && !base.NetworkManager.ShutdownInProgress)
			{
				ServerTickTakingOrderTimer(Time.deltaTime);
				ServerTickDishClaimProbe();
			}
			if (_paymentScanSession == null || !_paymentScanSession.IsActive)
			{
				return;
			}
			_paymentScanSession.Tick(GetNormalizedMouseScreenDelta(), Time.deltaTime);
			if (_paymentScanSession.IsReadyToComplete)
			{
				Keyboard current = Keyboard.current;
				if (current != null && current.eKey.wasPressedThisFrame)
				{
					_paymentScanSession.TryCompletePayment();
				}
			}
		}

		private void ClientRestoreLateJoinVisualState()
		{
			ClientRestoreDirtyPlateVisualsFromNetworkState();
			ClientApplyCurrentNetworkVisualState();
		}

		private void ClientApplyCurrentNetworkVisualState()
		{
			switch (_currentState.Value)
			{
			case ConveyorBeltTableState.Empty:
				if (_debugText != null)
				{
					_debugText.text = "State: Empty";
				}
				if (_paymentCountText != null)
				{
					_paymentCountText.text = string.Empty;
				}
				StopPaymentScanSession();
				ClearDirtyPlateVisualsLocal();
				break;
			case ConveyorBeltTableState.TakingOrder:
				if (_debugText != null)
				{
					_debugText.text = $"State: Taking Order ({_mealsRemaining.Value} meals left)";
				}
				StopPaymentScanSession();
				break;
			case ConveyorBeltTableState.Eating:
				if (_debugText != null)
				{
					_debugText.text = "State: Eating";
				}
				StopPaymentScanSession();
				break;
			case ConveyorBeltTableState.AwaitingPayment:
				if (_debugText != null)
				{
					_debugText.text = $"State: Awaiting Payment (Dirty: {_dirtyPlateCount.Value})";
				}
				if (CanRunLocalPaymentScanSession())
				{
					BeginPaymentScanSession(_dirtyPlateCount.Value);
				}
				break;
			}
			RefreshPaymentResidentHighlight();
		}

		private bool IsTablePhysicallyBrokenSafe()
		{
			if (_physicalStateController != null)
			{
				return !_physicalStateController.IsPhysicallyUsable;
			}
			return false;
		}

		private bool IsTablePhysicallyUsableSafe()
		{
			if (!(_physicalStateController == null))
			{
				return _physicalStateController.IsPhysicallyUsable;
			}
			return true;
		}

		private bool IsBeltPhysicallyUsableSafe()
		{
			if (_belt != null && _belt.PhysicalStateController != null)
			{
				return _belt.PhysicalStateController.IsPhysicallyUsable;
			}
			return false;
		}

		private bool CanRunServerDebug(string actionName)
		{
			if (!Application.isPlaying)
			{
				UtilityMethods.Log("<b><color=orange>[ConveyorBeltTable DEBUG]</color></b> " + actionName + " blocked. Application is not playing.", LogType.Warning);
				return false;
			}
			if (!base.IsServer)
			{
				UtilityMethods.Log("<b><color=orange>[ConveyorBeltTable DEBUG]</color></b> " + actionName + " blocked. Only server/host can run this debug action.", LogType.Warning);
				return false;
			}
			return true;
		}

		private void ServerInvalidateDelayedActions()
		{
			_stateVersion++;
		}

		private void ShowLocalToast(string message, NotificationState state = NotificationState.Info, float duration = 2f)
		{
			if (!(Singleton<NotifyManager>.Instance == null))
			{
				Singleton<NotifyManager>.Instance.ShowToast(message, state, duration);
			}
		}

		private DirtyPlateMaterialDefinition GetDirtyPlateMaterialDefinition(PlateColor color)
		{
			if (_dirtyPlateMaterialDefinitions == null)
			{
				return null;
			}
			for (int i = 0; i < _dirtyPlateMaterialDefinitions.Count; i++)
			{
				DirtyPlateMaterialDefinition dirtyPlateMaterialDefinition = _dirtyPlateMaterialDefinitions[i];
				if (dirtyPlateMaterialDefinition != null && dirtyPlateMaterialDefinition.Color == color)
				{
					return dirtyPlateMaterialDefinition;
				}
			}
			return null;
		}

		private Material GetDirtyPlateMaterial(PlateColor color)
		{
			return GetDirtyPlateMaterialDefinition(color)?.Material;
		}

		private PlateColor GetRandomConfiguredPlateColor()
		{
			if (_dirtyPlateMaterialDefinitions == null || _dirtyPlateMaterialDefinitions.Count == 0)
			{
				return PlateColor.None;
			}
			List<PlateColor> list = new List<PlateColor>();
			for (int i = 0; i < _dirtyPlateMaterialDefinitions.Count; i++)
			{
				DirtyPlateMaterialDefinition dirtyPlateMaterialDefinition = _dirtyPlateMaterialDefinitions[i];
				if (dirtyPlateMaterialDefinition != null && !(dirtyPlateMaterialDefinition.Material == null))
				{
					list.Add(dirtyPlateMaterialDefinition.Color);
				}
			}
			if (list.Count == 0)
			{
				return PlateColor.None;
			}
			return list[UnityEngine.Random.Range(0, list.Count)];
		}

		private void ServerRegisterRequiredPlate(PlateColor color)
		{
			if (!_serverRequiredPlateCounts.ContainsKey(color))
			{
				_serverRequiredPlateCounts[color] = 0;
			}
			_serverRequiredPlateCounts[color]++;
		}

		private byte GetServerRequiredPlateCount(PlateColor color)
		{
			if (!_serverRequiredPlateCounts.TryGetValue(color, out var value))
			{
				return 0;
			}
			return (byte)Mathf.Clamp(value, 0, 255);
		}

		private bool ServerValidatePlateScanCounts(byte noneCount, byte blueCount, byte greenCount, byte redCount, byte yellowCount)
		{
			if (noneCount == GetServerRequiredPlateCount(PlateColor.None) && blueCount == GetServerRequiredPlateCount(PlateColor.Blue) && greenCount == GetServerRequiredPlateCount(PlateColor.Green) && redCount == GetServerRequiredPlateCount(PlateColor.Red))
			{
				return yellowCount == GetServerRequiredPlateCount(PlateColor.Yellow);
			}
			return false;
		}

		private void ClearServerPlatePaymentRuntime()
		{
			_serverRequiredPlateCounts.Clear();
			_dirtyPlateSnapshots.Clear();
			_totalPayment = 0;
			_currentDishPrice = 0;
			_currentPlateColor = PlateColor.None;
		}

		private Vector2 GetNormalizedMouseScreenDelta()
		{
			if (Mouse.current == null)
			{
				return Vector2.zero;
			}
			Vector2 vector = Mouse.current.delta.ReadValue();
			float num = Mathf.Max(1, Screen.width);
			float num2 = Mathf.Max(1, Screen.height);
			float x = vector.x / num * 2f;
			float y = vector.y / num2 * 2f;
			return new Vector2(x, y);
		}

		private void HideCursorForPaymentUse()
		{
			if (!_cursorHiddenForPaymentUse)
			{
				_cursorVisibleBeforePaymentUse = Cursor.visible;
				_cursorLockStateBeforePaymentUse = Cursor.lockState;
				Cursor.visible = false;
				_cursorHiddenForPaymentUse = true;
			}
		}

		private void RestoreCursorForPaymentUse()
		{
			if (_cursorHiddenForPaymentUse)
			{
				Cursor.lockState = _cursorLockStateBeforePaymentUse;
				Cursor.visible = _cursorVisibleBeforePaymentUse;
				_cursorHiddenForPaymentUse = false;
			}
		}

		private void SetPaymentCameraTrackingTarget(Transform target)
		{
			if (!(base.UsableCam == null))
			{
				if (!_paymentUseCameraTargetCached)
				{
					_usableCameraTrackingTargetBeforePaymentUse = base.UsableCam.Target.TrackingTarget;
					_usableCameraLookAtTargetBeforePaymentUse = base.UsableCam.Target.LookAtTarget;
					_usableCameraCustomLookAtBeforePaymentUse = base.UsableCam.Target.CustomLookAtTarget;
					_paymentUseCameraTargetCached = true;
				}
				CameraTarget target2 = base.UsableCam.Target;
				target2.TrackingTarget = target;
				base.UsableCam.Target = target2;
			}
		}

		private void RestorePaymentCameraTrackingTarget()
		{
			if (!(base.UsableCam == null) && _paymentUseCameraTargetCached)
			{
				CameraTarget target = base.UsableCam.Target;
				target.TrackingTarget = _usableCameraTrackingTargetBeforePaymentUse;
				target.LookAtTarget = _usableCameraLookAtTargetBeforePaymentUse;
				target.CustomLookAtTarget = _usableCameraCustomLookAtBeforePaymentUse;
				base.UsableCam.Target = target;
				_usableCameraTrackingTargetBeforePaymentUse = null;
				_usableCameraLookAtTargetBeforePaymentUse = null;
				_usableCameraCustomLookAtBeforePaymentUse = false;
				_paymentUseCameraTargetCached = false;
			}
		}

		public void ServerResetForEndDay()
		{
			if (base.IsServer)
			{
				ServerInvalidateDelayedActions();
				ServerDespawnCurrentDishIfAny();
				_isChangingState = false;
				_isClaimingDish = false;
				_hasIncomingCustomer = false;
				_restaurantClosePendingPaymentAfterEating = false;
				_mealsRemaining.Value = 0;
				_dirtyPlateCount.Value = 0;
				_currentDishRef = default;
				_initialMealCount = 0;
				_takingOrderTimer = 0f;
				_takingOrderTimerActive = false;
				_serverPaymentCompleted = false;
				ClearServerPlatePaymentRuntime();
				InUse.Value = false;
				CanUse.Value = true;
				if (base.NetworkObject != null && base.NetworkObject.IsSpawned && base.OwnerClientId != 0L)
				{
					base.NetworkObject.RemoveOwnership();
				}
				if (_currentState.Value != ConveyorBeltTableState.Empty)
				{
					ServerChangeState(ConveyorBeltTableState.Empty);
				}
				ClientEndDayResetRpc();
				UtilityMethods.Log($"[ConveyorBeltTable] End-day reset completed. SeatId={_seatId}");
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
				FastBufferWriter bufferWriter = __beginSendRpc(3619955438u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3619955438u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				StopPaymentScanSession();
				DespawnPaymentScannerLocal();
				ClientResetDirtyPlateStackTransformImmediate();
				ClearDirtyPlateVisualsLocal();
				ClientForceExitUseModeForReset();
				if (_debugText != null)
				{
					_debugText.text = "State: Empty";
				}
				if (_paymentCountText != null)
				{
					_paymentCountText.text = string.Empty;
				}
				RefreshGazeFeedback();
			}
		}

		private void ClientForceExitUseModeForReset()
		{
			if (_currentInteractor == null)
			{
				RestoreCursorForPaymentUse();
				return;
			}
			Singleton<InputManager>.Instance.InputHandler.KnifeAction -= OnPaymentScanInput;
			Singleton<InputManager>.Instance.InputHandler.Scan -= OnPaymentScanInput;
			DisableUseHints();
			SetCamera(status: false);
			_currentInteractor.SetInteractorStatus(status: true);
			RestoreCursorForPaymentUse();
			_currentInteractor.Player.SwitchTo("Player", cursorVisible: false);
			_currentInteractor.Player.ForKitchenUsable(value: true);
			_currentInteractor.Player.ResumeCharacter();
			_currentInteractor = null;
		}

		private void HandlePhysicalRepair(bool value)
		{
			if (value && _interactableTooltip != null)
			{
				_interactableTooltip.ClearRuntimeTooltip();
				_interactableTooltip.IsEnabled = false;
				if (Singleton<ToolTipHandler>.Instance != null)
				{
					Singleton<ToolTipHandler>.Instance.OnGazeExit(_interactableTooltip);
				}
			}
		}

		public override void GazeEnter()
		{
			base.GazeEnter();
			ClearGazeHighlightUnlessAwaitingPayment();
			RefreshPaymentResidentHighlight();
			if (IsBeltPhysicallyUsableSafe() && IsTablePhysicallyBrokenSafe())
			{
				RefreshUnlockTooltipLocal();
				if (_interactableTooltip != null && _interactableTooltip.IsEnabled && Singleton<ToolTipHandler>.Instance != null)
				{
					Singleton<ToolTipHandler>.Instance.OnGazeEnter(_interactableTooltip, _interactableTooltip.GetWorldPosition());
				}
			}
		}

		private void ClearGazeHighlightUnlessAwaitingPayment()
		{
			if (_currentState.Value != ConveyorBeltTableState.AwaitingPayment && !(_outlinable == null))
			{
				ClearTableGazeHighlight();
			}
		}

		private void ClearTableGazeHighlight()
		{
			if (!(_outlinable == null))
			{
				Singleton<HighlightManager>.Instance?.ClearCurrentHighlight(_outlinable);
				_outlinable.enabled = false;
			}
		}

		private void RefreshPaymentResidentHighlight()
		{
			if (!_inGaze || _currentState.Value != ConveyorBeltTableState.AwaitingPayment)
			{
				ClearPaymentResidentHighlight();
			}
			else
			{
				ShowPaymentResidentHighlight();
			}
		}

		private void ShowPaymentResidentHighlight()
		{
			ResidentManager instance = Singleton<ResidentManager>.Instance;
			if (!(instance == null) && instance.TryGetRestaurantResident(_seatId, ResidentRestaurantTargetType.ConveyorTable, out var resident) && !(resident.Outlinable == null))
			{
				if (_paymentHighlightResident != resident)
				{
					ClearPaymentResidentHighlight();
				}
				_paymentHighlightResident = resident;
				resident.Outlinable.enabled = true;
			}
		}

		private void ClearPaymentResidentHighlight()
		{
			if (!(_paymentHighlightResident == null))
			{
				if (_paymentHighlightResident.Outlinable != null)
				{
					_paymentHighlightResident.Outlinable.enabled = false;
				}
				_paymentHighlightResident = null;
			}
		}

		public override void GazeExit()
		{
			base.GazeExit();
			ClearPaymentResidentHighlight();
			if (IsBeltPhysicallyUsableSafe())
			{
				ClearTooltip();
			}
		}

		private void ClearTooltip()
		{
			if (!(_interactableTooltip == null) && _interactableTooltip.IsEnabled && Singleton<ToolTipHandler>.Instance != null)
			{
				Singleton<ToolTipHandler>.Instance.OnGazeExit(_interactableTooltip);
			}
		}

		public int GetCurrentUnlockPrice()
		{
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (restaurantManager == null)
			{
				return 100;
			}
			return restaurantManager.GetNextConveyorTableUnlockPrice();
		}

		private void RefreshUnlockTooltipLocal()
		{
			if (!(_interactableTooltip == null))
			{
				TooltipData baseTooltip = _interactableTooltip.GetBaseTooltip();
				if (string.IsNullOrWhiteSpace(baseTooltip.Title))
				{
					baseTooltip.Title = "Broken Seat";
				}
				baseTooltip.Body = GetUnlockTooltipBody();
				baseTooltip.OnlyHeader = false;
				_interactableTooltip.SetRuntimeTooltip(baseTooltip);
			}
		}

		private string GetUnlockTooltipBody()
		{
			GameProgressManager instance = Singleton<GameProgressManager>.Instance;
			if (instance != null && !instance.IsConveyorTableProgressionUnlocked(_seatId))
			{
				int requiredConveyorTableProgressionLevel = instance.GetRequiredConveyorTableProgressionLevel(_seatId);
				if (requiredConveyorTableProgressionLevel <= 0)
				{
					return "Reach the required restaurant level first.";
				}
				return $"Reach Restaurant Level {requiredConveyorTableProgressionLevel}";
			}
			return $"Repair Cost: ${GetCurrentUnlockPrice()}";
		}

		public bool CanAcceptResidentArrival()
		{
			if (_currentState.Value != ConveyorBeltTableState.Empty)
			{
				return false;
			}
			if (!_hasIncomingCustomer)
			{
				return false;
			}
			if (!IsTablePhysicallyUsableSafe())
			{
				return false;
			}
			if (!IsBeltPhysicallyUsableSafe())
			{
				return false;
			}
			return true;
		}

		public bool OnServerCustomerArrived()
		{
			if (!base.IsServer)
			{
				return true;
			}
			if (!CanAcceptResidentArrival())
			{
				UtilityMethods.Log($"[ConveyorBeltTable] Customer arrival rejected. SeatId={_seatId}, State={_currentState.Value}, HasIncoming={_hasIncomingCustomer}", LogType.Warning);
				return false;
			}
			_hasIncomingCustomer = false;
			_restaurantClosePendingPaymentAfterEating = false;
			int value = (_initialMealCount = GetRandomMealCount());
			_mealsRemaining.Value = value;
			_dirtyPlateCount.Value = 0;
			_currentDishRef = default;
			_takingOrderTimer = 0f;
			_takingOrderTimerActive = false;
			_serverPaymentCompleted = false;
			_isClaimingDish = false;
			ClearServerPlatePaymentRuntime();
			ServerChangeState(ConveyorBeltTableState.TakingOrder);
			return true;
		}

		private int GetRandomMealCount()
		{
			int num = Mathf.Max(1, _minMealsPerCustomer);
			int allowedMax = Mathf.Max(num, _maxMealsPerCustomer);
			float value = UnityEngine.Random.value;
			if (value < 0.7f)
			{
				return GetRandomMealCountInRange(num, allowedMax, num, 6);
			}
			if (value < 0.93f)
			{
				return GetRandomMealCountInRange(num, allowedMax, 7, 12);
			}
			if (value < 0.99f)
			{
				return GetRandomMealCountInRange(num, allowedMax, 13, 20);
			}
			return GetRandomMealCountInRange(num, allowedMax, 21, 30);
		}

		private static int GetRandomMealCountInRange(int allowedMin, int allowedMax, int rangeMin, int rangeMax)
		{
			int num = Mathf.Clamp(rangeMin, allowedMin, allowedMax);
			int num2 = Mathf.Clamp(rangeMax, num, allowedMax);
			return UnityEngine.Random.Range(num, num2 + 1);
		}

		public void ServerReserveIncomingCustomer()
		{
			if (base.IsServer)
			{
				_hasIncomingCustomer = true;
				_restaurantClosePendingPaymentAfterEating = false;
			}
		}

		public void ServerHandleRestaurantClosed()
		{
			if (!base.IsServer)
			{
				return;
			}
			switch (_currentState.Value)
			{
			case ConveyorBeltTableState.Empty:
				if (_hasIncomingCustomer)
				{
					_hasIncomingCustomer = false;
					_restaurantClosePendingPaymentAfterEating = false;
					Singleton<ResidentManager>.Instance?.ForceRestaurantResidentsLeave(_seatId, ResidentRestaurantTargetType.ConveyorTable);
				}
				break;
			case ConveyorBeltTableState.TakingOrder:
				_restaurantClosePendingPaymentAfterEating = false;
				if (_dirtyPlateCount.Value <= 0)
				{
					ServerChangeState(ConveyorBeltTableState.Empty);
				}
				else
				{
					ServerChangeState(ConveyorBeltTableState.AwaitingPayment);
				}
				break;
			case ConveyorBeltTableState.Eating:
				_restaurantClosePendingPaymentAfterEating = true;
				break;
			case ConveyorBeltTableState.AwaitingPayment:
				_restaurantClosePendingPaymentAfterEating = false;
				break;
			}
		}

		private void ServerTickTakingOrderTimer(float deltaTime)
		{
			if (!_takingOrderTimerActive || _currentState.Value != ConveyorBeltTableState.TakingOrder)
			{
				return;
			}
			_takingOrderTimer -= deltaTime;
			if (!_mealTimeoutWarningShown && _takingOrderTimer > 0f && _takingOrderTimer <= _mealTimeoutWarningSeconds)
			{
				_mealTimeoutWarningShown = true;
			}
			if (!(_takingOrderTimer > 0f))
			{
				_takingOrderTimerActive = false;
				if (Mathf.Max(0, _initialMealCount - _mealsRemaining.Value) <= 0)
				{
					ServerChangeState(ConveyorBeltTableState.Empty);
				}
				else
				{
					ServerChangeState(ConveyorBeltTableState.AwaitingPayment);
				}
			}
		}

		public override void ServerUseEnter(Interactor interactor)
		{
			base.ServerUseEnter(interactor);
			_currentInteractor = interactor;
			if (base.NetworkObject.OwnerClientId != interactor.OwnerClientId)
			{
				base.NetworkObject.ChangeOwnership(interactor.OwnerClientId);
			}
		}

		public override void ServerUseExit(Interactor interactor)
		{
			base.ServerUseExit(interactor);
			if (_currentInteractor == interactor)
			{
				_currentInteractor = null;
			}
			base.NetworkObject.RemoveOwnership();
		}

		public override void ClientUseEnter(Interactor interactor)
		{
			interactor.Player.SwitchTo("PrepStation", cursorVisible: true);
			HideCursorForPaymentUse();
			base.ClientUseEnter(interactor);
			Singleton<InputManager>.Instance.InputHandler.KnifeAction += OnPaymentScanInput;
			Singleton<InputManager>.Instance.InputHandler.Scan += OnPaymentScanInput;
			interactor.Player.ForKitchenUsable(value: false);
			SpawnPaymentScannerLocal();
			if (_currentState.Value == ConveyorBeltTableState.AwaitingPayment)
			{
				BeginPaymentScanSession(_dirtyPlateCount.Value);
			}
			ClientMoveDirtyPlateStackToPaymentFocus();
		}

		public override void ClientUseExit(Interactor interactor)
		{
			base.ClientUseExit(interactor);
			Singleton<InputManager>.Instance.InputHandler.KnifeAction -= OnPaymentScanInput;
			Singleton<InputManager>.Instance.InputHandler.Scan -= OnPaymentScanInput;
			RestoreCursorForPaymentUse();
			interactor.Player.SwitchTo("Player", cursorVisible: false);
			interactor.Player.ForKitchenUsable(value: true);
			StopPaymentScanSession();
			DespawnPaymentScannerLocal();
			ClientReturnDirtyPlateStackToDefault();
		}

		public override void EnableUseHints()
		{
			base.EnableUseHints();
			SetPaymentUseHintsReadyToScan();
		}

		public override void DisableUseHints()
		{
			base.DisableUseHints();
			Singleton<HintsManager>.Instance.RemoveHint("KnifeAction");
			Singleton<HintsManager>.Instance.RemoveHint("Interact");
		}

		private void SetPaymentUseHintsReadyToScan()
		{
			Singleton<HintsManager>.Instance.RemoveHint("Exit");
			Singleton<HintsManager>.Instance.AddHint("Exit", "key_close");
			Singleton<HintsManager>.Instance.RemoveHint("Interact");
			Singleton<HintsManager>.Instance.RemoveHint("KnifeAction");
			Singleton<HintsManager>.Instance.AddHint("KnifeAction", "key_scan_hold");
		}

		private void SetPaymentUseHintsReadyToComplete()
		{
			Singleton<HintsManager>.Instance.RemoveHint("KnifeAction");
			Singleton<HintsManager>.Instance.RemoveHint("Interact");
			Singleton<HintsManager>.Instance.AddHint("Interact", "key_collect_payment");
		}

		public override bool IsAvailable()
		{
			bool num = base.IsAvailable() && _currentState.Value == ConveyorBeltTableState.AwaitingPayment;
			bool flag = _physicalStateController != null && !_physicalStateController.IsPhysicallyUsable;
			return num | flag;
		}

		protected override void ClientInteract(Interactor interactor, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				if (!IsBeltPhysicallyUsableSafe())
				{
					ShowLocalToast("Repair the conveyor belt first.");
				}
				else if (IsTablePhysicallyBrokenSafe())
				{
					TryUnlockTableClient(interactor);
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
				if (!IsBeltPhysicallyUsableSafe())
				{
					ShowLocalToast("Repair the conveyor belt first.");
				}
				else if (IsTablePhysicallyBrokenSafe())
				{
					TryUnlockTableClient(interactor);
				}
				else
				{
					base.ClientCrossInteract(interactor, held, phase);
				}
			}
		}

		private void OnPaymentScanInput(InputActionPhase phase)
		{
			switch (phase)
			{
			case InputActionPhase.Started:
				_paymentScanSession?.SetHolding(holding: true);
				break;
			case InputActionPhase.Canceled:
				_paymentScanSession?.SetHolding(holding: false);
				break;
			}
		}

		private void TryUnlockTableClient(Interactor interactor)
		{
			if (IsTablePhysicallyBrokenSafe())
			{
				ServerTryUnlockTableRpc(interactor.NetworkObject.ToReference());
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerTryUnlockTableRpc(NetworkObjectReference interactorRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1170270361u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 1170270361u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
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
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!interactorRef.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != senderClientId)
			{
				return;
			}
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (restaurantManager == null)
			{
				UtilityMethods.Log("[ConveyorBeltTable] Unlock failed. RestaurantManager is null.", LogType.Warning);
				return;
			}
			if (!Singleton<GameProgressManager>.Instance.IsConveyorTableProgressionUnlocked(_seatId))
			{
				BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(component.OwnerClientId, RpcTargetUse.Temp);
				ClientShowProgressionLockedNotifyRpc(baseRpcTarget);
				return;
			}
			int nextConveyorTableUnlockPrice = restaurantManager.GetNextConveyorTableUnlockPrice();
			ServiceLocator.ForSceneOf(this).Get<EconomyHandler>(out var service);
			if (!service.HasSufficientBalance(nextConveyorTableUnlockPrice))
			{
				BaseRpcTarget baseRpcTarget2 = base.RpcTarget.Single(component.OwnerClientId, RpcTargetUse.Temp);
				ClientShowUnlockPriceNotifyRpc(nextConveyorTableUnlockPrice, baseRpcTarget2);
			}
			else
			{
				service.SpendMoney(nextConveyorTableUnlockPrice);
				restaurantManager.ServerNotifyConveyorTableUnlocked(SeatId);
				TryRepairPhysicalState();
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientShowUnlockPriceNotifyRpc(int requiredMoney, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1653954238u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, requiredMoney);
				__endSendRpc(ref bufferWriter, 1653954238u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ShowLocalToast($"Not enough money to repair the table. Required: ${requiredMoney}", NotificationState.Error);
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientShowProgressionLockedNotifyRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3603008409u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3603008409u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ShowLocalToast("Reach the required restaurant level first.");
			}
		}

		private void TryRepairPhysicalState()
		{
			if (!(_physicalStateController == null) && !_physicalStateController.IsPhysicallyUsable)
			{
				_physicalStateController.Repair();
				QuestReferenceHub.Instance?.QuestComplete(QuestId.RepairConveyorChair);
			}
		}

		public void ServerChangeState(ConveyorBeltTableState newState)
		{
			if (!base.IsServer)
			{
				UtilityMethods.Log($"[ConveyorBeltTable] ServerChangeState blocked on client. RequestedState={newState}", LogType.Warning);
			}
			else if (_isChangingState)
			{
				UtilityMethods.Log($"[ConveyorBeltTable] ServerChangeState blocked because another state transition is already running. RequestedState={newState}", LogType.Warning);
			}
			else if (_currentState.Value != newState)
			{
				_stateVersion++;
				_isChangingState = true;
				try
				{
					ConveyorBeltTableState value = _currentState.Value;
					UtilityMethods.Log($"[ConveyorBeltTable] State change. SeatId={_seatId}, {value} -> {newState}");
					ServerExitState();
					_currentState.Value = newState;
					ServerEnterState();
					ClientChangeStateRpc((byte)newState);
				}
				finally
				{
					_isChangingState = false;
				}
			}
		}

		private void ServerEnterState()
		{
			switch (_currentState.Value)
			{
			case ConveyorBeltTableState.Empty:
				ServerEmptyStateEnter();
				ClientEmptyStateEnterRpc();
				break;
			case ConveyorBeltTableState.TakingOrder:
				ServerTakingOrderStateEnter();
				ClientTakingOrderEnterRpc();
				break;
			case ConveyorBeltTableState.Eating:
				ServerEatingStateEnter();
				ClientEatingEnterRpc();
				break;
			case ConveyorBeltTableState.AwaitingPayment:
				ServerAwaitingPaymentEnter();
				ClientAwaitingPaymentEnterRpc(_dirtyPlateCount.Value);
				break;
			}
		}

		private void ServerExitState()
		{
			switch (_currentState.Value)
			{
			case ConveyorBeltTableState.Empty:
				ServerEmptyStateExit();
				ClientEmptyStateExitRpc();
				break;
			case ConveyorBeltTableState.TakingOrder:
				ServerTakingOrderStateExit();
				ClientTakingOrderExitRpc();
				break;
			case ConveyorBeltTableState.Eating:
				ServerEatingStateExit();
				ClientEatingExitRpc();
				break;
			case ConveyorBeltTableState.AwaitingPayment:
				ServerAwaitingPaymentExit();
				ClientAwaitingPaymentExitRpc();
				break;
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientChangeStateRpc(byte newState)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3520613501u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in newState, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3520613501u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				EventBus<ConveyorBeltTableStateChangeEvent>.Raise(new ConveyorBeltTableStateChangeEvent
				{
					NewState = (ConveyorBeltTableState)newState,
					TableId = _seatId
				});
			}
		}

		private void ServerEmptyStateEnter()
		{
			Singleton<ResidentManager>.Instance?.ForceRestaurantResidentsLeave(_seatId, ResidentRestaurantTargetType.ConveyorTable);
			_hasIncomingCustomer = false;
			_restaurantClosePendingPaymentAfterEating = false;
			_mealsRemaining.Value = 0;
			_dirtyPlateCount.Value = 0;
			_currentDishRef = default;
			_serverPaymentCompleted = false;
			_isClaimingDish = false;
			_initialMealCount = 0;
			_takingOrderTimer = 0f;
			_takingOrderTimerActive = false;
			ClearServerPlatePaymentRuntime();
			if (base.IsServer && Singleton<GameProgressManager>.Instance != null && Singleton<GameProgressManager>.Instance.RestaurantManager != null)
			{
				Singleton<GameProgressManager>.Instance.RestaurantManager.ServerNotifyConveyorTableEmptied(_seatId);
			}
		}

		private void ServerEmptyStateExit()
		{
		}

		[Rpc(SendTo.Everyone)]
		private void ClientEmptyStateEnterRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(2352785190u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2352785190u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (_debugText != null)
				{
					_debugText.text = "State: Empty";
				}
				if (_paymentCountText != null)
				{
					_paymentCountText.text = string.Empty;
				}
				StopPaymentScanSession();
				ClientResetDirtyPlateStackTransformImmediate();
				ClearDirtyPlateVisualsLocal();
				ClearTableGazeHighlight();
				ClearPaymentResidentHighlight();
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientEmptyStateExitRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(4004408735u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 4004408735u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private void ServerTakingOrderStateEnter()
		{
			_isClaimingDish = false;
			_nextDishClaimProbeAt = 0f;
			_takingOrderTimer = _waitingTimeForMeal;
			_takingOrderTimerActive = true;
			_mealTimeoutWarningShown = false;
		}

		private void ServerTakingOrderStateExit()
		{
			_takingOrderTimerActive = false;
		}

		[Rpc(SendTo.Everyone)]
		private void ClientTakingOrderEnterRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1639693462u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1639693462u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (_debugText != null)
				{
					_debugText.text = $"State: Taking Order ({_mealsRemaining.Value} meals left)";
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientTakingOrderExitRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1519393206u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1519393206u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private void ServerEatingStateEnter()
		{
			_isClaimingDish = false;
			int stateVersion = _stateVersion;
			int num = UnityEngine.Random.Range(_minEatingTime, _maxEatingTime + 1);
			Singleton<DelayHelper>.Instance.DelayAction(num, () =>
			{
				if (base.IsServer)
				{
					if (stateVersion != _stateVersion)
					{
						UtilityMethods.Log($"[ConveyorBeltTable] Eating delay ignored because state version changed. SeatId={_seatId}");
					}
					else if (_currentState.Value != ConveyorBeltTableState.Eating)
					{
						UtilityMethods.Log($"[ConveyorBeltTable] Eating delay ignored because table is no longer Eating. SeatId={_seatId}, CurrentState={_currentState.Value}");
					}
					else
					{
						PlateColor plateColor = _currentPlateColor;
						if (plateColor == PlateColor.None)
						{
							plateColor = GetRandomConfiguredPlateColor();
						}
						int num2 = Mathf.Max(0, _currentDishPrice);
						_dirtyPlateCount.Value = Mathf.Max(0, _dirtyPlateCount.Value + 1);
						ServerRegisterRequiredPlate(plateColor);
						_dirtyPlateSnapshots.Add(new DirtyPlateSnapshot(plateColor, num2));
						_totalPayment += num2;
						ClientSpawnDirtyPlateVisualRpc(_dirtyPlateCount.Value, (byte)plateColor, num2);
						_mealsRemaining.Value = Mathf.Max(0, _mealsRemaining.Value - 1);
						ServerDespawnCurrentDishIfAny();
						if (_restaurantClosePendingPaymentAfterEating)
						{
							_restaurantClosePendingPaymentAfterEating = false;
							ServerChangeState(ConveyorBeltTableState.AwaitingPayment);
						}
						else if (_mealsRemaining.Value <= 0)
						{
							ServerChangeState(ConveyorBeltTableState.AwaitingPayment);
						}
						else
						{
							ServerChangeState(ConveyorBeltTableState.TakingOrder);
						}
					}
				}
			});
		}

		private void ServerEatingStateExit()
		{
		}

		[Rpc(SendTo.Everyone)]
		private void ClientEatingEnterRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1574554941u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1574554941u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (_debugText != null)
				{
					_debugText.text = "State: Eating";
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientEatingExitRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(519679536u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 519679536u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private void ServerAwaitingPaymentEnter()
		{
			GlobalChatManager.BroadcastLocalizedInfoFromServer(this, "chat.info.conveyor_ready_payment", "Conveyor seat {0} is ready for payment.", _seatId.ToString());
		}

		private void ServerAwaitingPaymentExit()
		{
		}

		[Rpc(SendTo.Everyone)]
		private void ClientAwaitingPaymentEnterRpc(int dirtyPlateCount)
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
				FastBufferWriter bufferWriter = __beginSendRpc(247722522u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, dirtyPlateCount);
				__endSendRpc(ref bufferWriter, 247722522u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				dirtyPlateCount = Mathf.Clamp(dirtyPlateCount, 0, _dirtyPlateVisualMax);
				if (_debugText != null)
				{
					_debugText.text = $"State: Awaiting Payment (Dirty: {dirtyPlateCount})";
				}
				if (_activePaymentScanner != null)
				{
					_activePaymentScanner.ResetPaymentUI();
				}
				if (CanRunLocalPaymentScanSession())
				{
					BeginPaymentScanSession(dirtyPlateCount);
				}
				RefreshGazeFeedback();
				RefreshPaymentResidentHighlight();
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientAwaitingPaymentExitRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1149949200u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1149949200u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				StopPaymentScanSession();
				ClearTableGazeHighlight();
				ClearPaymentResidentHighlight();
				if (_paymentCountText != null)
				{
					_paymentCountText.text = string.Empty;
				}
				DespawnPaymentScannerLocal();
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerCompletePaymentScanRpc(byte noneCount, byte blueCount, byte greenCount, byte redCount, byte yellowCount, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(641833588u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in noneCount, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in blueCount, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in greenCount, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in redCount, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in yellowCount, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 641833588u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
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
			if (_serverPaymentCompleted)
			{
				LogPaymentRejected("AlreadyCompleted", rpcParams.Receive.SenderClientId, noneCount, blueCount, greenCount, redCount, yellowCount);
				return;
			}
			if (_currentState.Value != ConveyorBeltTableState.AwaitingPayment)
			{
				LogPaymentRejected("StateMismatch:" + _currentState.Value, rpcParams.Receive.SenderClientId, noneCount, blueCount, greenCount, redCount, yellowCount);
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (_currentInteractor == null)
			{
				LogPaymentRejected("InteractorMissing", senderClientId, noneCount, blueCount, greenCount, redCount, yellowCount);
				return;
			}
			if (_currentInteractor.OwnerClientId != senderClientId)
			{
				LogPaymentRejected("OwnerMismatch", senderClientId, noneCount, blueCount, greenCount, redCount, yellowCount);
				return;
			}
			if (_dirtyPlateCount.Value <= 0)
			{
				LogPaymentRejected("NoDirtyPlates", senderClientId, noneCount, blueCount, greenCount, redCount, yellowCount);
				return;
			}
			if (noneCount + blueCount + greenCount + redCount + yellowCount != _dirtyPlateCount.Value || !ServerValidatePlateScanCounts(noneCount, blueCount, greenCount, redCount, yellowCount))
			{
				LogPaymentRejected("CountMismatch", senderClientId, noneCount, blueCount, greenCount, redCount, yellowCount);
				return;
			}
			_serverPaymentCompleted = true;
			int num = Mathf.Max(0, _totalPayment);
			if (num > 0)
			{
				ServiceLocator.ForSceneOf(this).Get<EconomyHandler>(out var service);
				service.EarnMoney(num, senderClientId);
				AddConveyorEarnQuestProgress(num);
			}
			Interactor currentInteractor = _currentInteractor;
			ulong num2 = senderClientId;
			UtilityMethods.Log("[ConveyorBeltTable] Payment accepted. " + BuildPaymentDebug(num2, noneCount, blueCount, greenCount, redCount, yellowCount));
			Singleton<GameProgressManager>.Instance.ServerGrantProgressionXp(ProgressionXpSource.CustomerServed, _seatId.ToString(), num2);
			ClearServerPlatePaymentRuntime();
			if (currentInteractor != null)
			{
				currentInteractor.ForceServerUseExit(this);
			}
			ServerChangeState(ConveyorBeltTableState.Empty);
		}

		private static void AddConveyorEarnQuestProgress(int earnedDollars)
		{
			if (earnedDollars > 0)
			{
				QuestReferenceHub.Instance?.AddProgress(QuestId.EarnMoneyFromConveyor, earnedDollars);
			}
		}

		private void LogPaymentRejected(string reason, ulong senderClientId, byte noneCount, byte blueCount, byte greenCount, byte redCount, byte yellowCount)
		{
			UtilityMethods.Log("[ConveyorBeltTable] Payment rejected. Reason=" + reason + ". " + BuildPaymentDebug(senderClientId, noneCount, blueCount, greenCount, redCount, yellowCount), LogType.Warning);
		}

		private string BuildPaymentDebug(ulong senderClientId, byte noneCount, byte blueCount, byte greenCount, byte redCount, byte yellowCount)
		{
			ulong num = ((_currentInteractor != null) ? _currentInteractor.OwnerClientId : ulong.MaxValue);
			int num2 = noneCount + blueCount + greenCount + redCount + yellowCount;
			return $"SeatId={_seatId}, Sender={senderClientId}, InteractorOwner={num}, " + $"State={_currentState.Value}, Dirty={_dirtyPlateCount.Value}, ClientTotal={num2}, " + $"ClientCounts=None:{noneCount}, Blue:{blueCount}, Green:{greenCount}, Red:{redCount}, Yellow:{yellowCount}, " + $"ServerCounts=None:{GetServerRequiredPlateCount(PlateColor.None)}, Blue:{GetServerRequiredPlateCount(PlateColor.Blue)}, Green:{GetServerRequiredPlateCount(PlateColor.Green)}, Red:{GetServerRequiredPlateCount(PlateColor.Red)}, Yellow:{GetServerRequiredPlateCount(PlateColor.Yellow)}";
		}

		public bool TryApplyServedDishAnchorPose(Transform dishTransform)
		{
			if (dishTransform == null)
			{
				return false;
			}
			if (_servedDishAnchor == null)
			{
				return false;
			}
			Vector3 localPosition = base.transform.InverseTransformPoint(_servedDishAnchor.position);
			Quaternion localRotation = Quaternion.Inverse(base.transform.rotation) * _servedDishAnchor.rotation;
			dishTransform.localPosition = localPosition;
			dishTransform.localRotation = localRotation;
			return true;
		}

		public void ServerTryClaimDishFromTrigger(ServedDishSushi dish)
		{
			if (!base.IsServer || _isClaimingDish || !IsTablePhysicallyUsableSafe() || !IsBeltPhysicallyUsableSafe() || _currentState.Value != ConveyorBeltTableState.TakingOrder || _mealsRemaining.Value <= 0 || dish == null || _belt == null || _servedDishAnchor == null)
			{
				return;
			}
			NetworkObject networkObject = dish.NetworkObject;
			if (networkObject == null || !networkObject.IsSpawned)
			{
				return;
			}
			if (!SushiAndFish.FriendsLife.FriendsSushiDeliveryGate.BeforeDelivery(this, _seatId, ResidentRestaurantTargetType.ConveyorTable, 0, -1, networkObject.NetworkObjectId, () => Mathf.Max(0, (TabletNetworkHandler.Instance != null) ? TabletNetworkHandler.Instance.GetEffectiveRecipePrice(dish.RuntimeRecipeIdentityId, dish.RuntimeDishPrice) : dish.RuntimeDishPrice)))
			{
				return;
			}
			_isClaimingDish = true;
			try
			{
				if (_currentState.Value == ConveyorBeltTableState.TakingOrder && _belt.ServerReleaseFromSlot(networkObject) && networkObject.TrySetParent(base.NetworkObject, worldPositionStays: false))
				{
					TryApplyServedDishAnchorPose(networkObject.transform);
					dish.CanHold.Value = false;
					dish.CanGaze.Value = false;
					_currentDishPrice = 0;
					_currentDishPrice = ((TabletNetworkHandler.Instance != null) ? TabletNetworkHandler.Instance.GetEffectiveRecipePrice(dish.RuntimeRecipeIdentityId, dish.RuntimeDishPrice) : dish.RuntimeDishPrice);
					_currentPlateColor = dish.PlateColor;
					if (_currentPlateColor == PlateColor.None)
					{
						_currentPlateColor = GetRandomConfiguredPlateColor();
					}
					_currentDishRef = networkObject.ToReference();
					ServerChangeState(ConveyorBeltTableState.Eating);
				}
			}
			finally
			{
				_isClaimingDish = false;
			}
		}

		public void RegisterDishClaimProbe(ConveyorBeltTableTrigger probe)
		{
			if (probe != null)
			{
				_dishClaimProbe = probe;
			}
		}

		public void UnregisterDishClaimProbe(ConveyorBeltTableTrigger probe)
		{
			if ((object)_dishClaimProbe == probe)
			{
				_dishClaimProbe = null;
			}
		}

		private void ServerTickDishClaimProbe()
		{
			if (_currentState.Value != ConveyorBeltTableState.TakingOrder || _isClaimingDish || _mealsRemaining.Value <= 0 || _belt == null || Time.unscaledTime < _nextDishClaimProbeAt)
			{
				return;
			}
			_nextDishClaimProbeAt = Time.unscaledTime + Mathf.Max(0.02f, _dishClaimProbeInterval);
			Bounds probeBounds;
			if (_dishClaimProbe != null && _dishClaimProbe.TryGetProbeBounds(out var bounds))
			{
				probeBounds = bounds;
			}
			else
			{
				if (!(_servedDishAnchor != null))
				{
					return;
				}
				probeBounds = new Bounds(_servedDishAnchor.position, Vector3.one * 0.1f);
			}
			if (_belt.ServerTryGetClosestDishInProbe(probeBounds, Mathf.Max(0f, _dishClaimProbePadding), out var closestDish))
			{
				ServerTryClaimDishFromTrigger(closestDish);
			}
		}

		private void ServerDespawnCurrentDishIfAny()
		{
			if (base.IsServer)
			{
				ServedDishSushi component;
				if (!_currentDishRef.TryGet(out var networkObject))
				{
					_currentDishRef = default;
					_currentPlateColor = PlateColor.None;
					_currentDishPrice = 0;
				}
				else if (networkObject == null || !networkObject.IsSpawned)
				{
					_currentDishRef = default;
					_currentPlateColor = PlateColor.None;
					_currentDishPrice = 0;
				}
				else if (!networkObject.TryGetComponent<ServedDishSushi>(out component))
				{
					_currentDishRef = default;
					_currentPlateColor = PlateColor.None;
					_currentDishPrice = 0;
				}
				else
				{
					networkObject.Despawn();
					_currentDishRef = default;
					_currentPlateColor = PlateColor.None;
					_currentDishPrice = 0;
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientSpawnDirtyPlateVisualRpc(int dirtyCount, byte plateColor, int dishPrice)
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
				FastBufferWriter bufferWriter = __beginSendRpc(683894983u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, dirtyCount);
				bufferWriter.WriteValueSafe(in plateColor, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, dishPrice);
				__endSendRpc(ref bufferWriter, 683894983u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				int index = Mathf.Max(0, dirtyCount - 1);
				SpawnDirtyPlateVisualLocal(index, (PlateColor)plateColor, dishPrice);
			}
		}

		private void SpawnDirtyPlateVisualLocal(int index, PlateColor color, int dishPrice)
		{
			_dirtyPlateStack?.Spawn(index, color, dishPrice);
		}

		private void ClientRestoreDirtyPlateVisualsFromNetworkState()
		{
			ClearDirtyPlateVisualsLocal();
			for (int i = 0; i < _dirtyPlateSnapshots.Count; i++)
			{
				DirtyPlateSnapshot dirtyPlateSnapshot = _dirtyPlateSnapshots[i];
				SpawnDirtyPlateVisualLocal(i, (PlateColor)dirtyPlateSnapshot.Color, dirtyPlateSnapshot.DishPrice);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientClearDirtyPlateVisualsRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1853577401u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1853577401u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClearDirtyPlateVisualsLocal();
			}
		}

		private void ClearDirtyPlateVisualsLocal()
		{
			_dirtyPlateAnchorTween?.Kill();
			_dirtyPlateAnchorTween = null;
			_dirtyPlateStack?.Clear();
			_paymentScanSession?.ClearScannedRuntime();
		}

		private void CacheDirtyPlateAnchorDefaultIfNeeded()
		{
			if (!_dirtyPlateAnchorDefaultCached && !(_dirtyPlateSpawnAnchor == null))
			{
				_dirtyPlateAnchorDefaultLocalPosition = _dirtyPlateSpawnAnchor.localPosition;
				_dirtyPlateAnchorDefaultLocalRotation = _dirtyPlateSpawnAnchor.localRotation;
				_dirtyPlateAnchorDefaultCached = true;
			}
		}

		private void ClientMoveDirtyPlateStackToPaymentFocus()
		{
			if (_currentState.Value != ConveyorBeltTableState.AwaitingPayment || _dirtyPlateSpawnAnchor == null || _servedDishAnchor == null || _dirtyPlateStack == null || !_dirtyPlateStack.HasVisuals)
			{
				return;
			}
			CacheDirtyPlateAnchorDefaultIfNeeded();
			Vector3 endValue;
			Quaternion endValue2;
			if (_dirtyPlateSpawnAnchor.parent == _servedDishAnchor.parent)
			{
				endValue = _servedDishAnchor.localPosition;
				endValue2 = _servedDishAnchor.localRotation;
			}
			else
			{
				Transform parent = _dirtyPlateSpawnAnchor.parent;
				if (parent == null)
				{
					endValue = _servedDishAnchor.position;
					endValue2 = _servedDishAnchor.rotation;
				}
				else
				{
					endValue = parent.InverseTransformPoint(_servedDishAnchor.position);
					endValue2 = Quaternion.Inverse(parent.rotation) * _servedDishAnchor.rotation;
				}
			}
			endValue += _dirtyPlatePaymentFocusLocalOffset;
			_dirtyPlateAnchorTween?.Kill();
			Sequence sequence = DOTween.Sequence();
			sequence.Join(_dirtyPlateSpawnAnchor.DOLocalMove(endValue, _dirtyPlatePaymentMoveDuration));
			sequence.Join(_dirtyPlateSpawnAnchor.DOLocalRotateQuaternion(endValue2, _dirtyPlatePaymentMoveDuration));
			sequence.SetEase(_dirtyPlatePaymentMoveEase);
			_dirtyPlateAnchorTween = sequence;
		}

		private void ClientReturnDirtyPlateStackToDefault()
		{
			if (!(_dirtyPlateSpawnAnchor == null) && _dirtyPlateAnchorDefaultCached)
			{
				_dirtyPlateAnchorTween?.Kill();
				Sequence sequence = DOTween.Sequence();
				sequence.Join(_dirtyPlateSpawnAnchor.DOLocalMove(_dirtyPlateAnchorDefaultLocalPosition, _dirtyPlatePaymentMoveDuration));
				sequence.Join(_dirtyPlateSpawnAnchor.DOLocalRotateQuaternion(_dirtyPlateAnchorDefaultLocalRotation, _dirtyPlatePaymentMoveDuration));
				sequence.SetEase(_dirtyPlatePaymentMoveEase);
				_dirtyPlateAnchorTween = sequence;
			}
		}

		private void ClientResetDirtyPlateStackTransformImmediate()
		{
			if (!(_dirtyPlateSpawnAnchor == null) && _dirtyPlateAnchorDefaultCached)
			{
				_dirtyPlateAnchorTween?.Kill();
				_dirtyPlateAnchorTween = null;
				_dirtyPlateSpawnAnchor.localPosition = _dirtyPlateAnchorDefaultLocalPosition;
				_dirtyPlateSpawnAnchor.localRotation = _dirtyPlateAnchorDefaultLocalRotation;
			}
		}

		private void SpawnPaymentScannerLocal()
		{
			if (!(_activePaymentScanner != null) && !(_paymentScannerPrefab == null) && !(_currentInteractor == null))
			{
				Transform transform = ((_scannerLocalPosition != null) ? _scannerLocalPosition : base.transform);
				Quaternion rotation = transform.rotation * Quaternion.Euler(_scannerLocalEuler);
				_activePaymentScanner = UnityEngine.Object.Instantiate(_paymentScannerPrefab, transform.position, rotation);
				_activePaymentScanner.InitializeBaseRotation();
				_activePaymentScanner.ResetPaymentUI();
				SetPaymentCameraTrackingTarget(_activePaymentScanner.ScanRayOrigin);
			}
		}

		private void DespawnPaymentScannerLocal()
		{
			if (!(_activePaymentScanner == null))
			{
				RestorePaymentCameraTrackingTarget();
				UnityEngine.Object.Destroy(_activePaymentScanner.gameObject);
				_activePaymentScanner = null;
			}
		}

		private void BeginPaymentScanSession(int requiredScanCount)
		{
			if (_paymentScanSession == null)
			{
				_paymentScanSession = new ConveyorPaymentScanSession(this);
			}
			_paymentScanSession.Begin(requiredScanCount);
		}

		private void StopPaymentScanSession()
		{
			_paymentScanSession?.Stop();
			ClearPaymentCounterLocal();
		}

		private void RefreshPaymentCounterLocal(int scannedCount, int requiredScanCount)
		{
			if (!(_paymentCountText == null))
			{
				_paymentCountText.text = $"{scannedCount}/{requiredScanCount}";
			}
		}

		private void ClearPaymentCounterLocal()
		{
			if (!(_paymentCountText == null))
			{
				_paymentCountText.text = string.Empty;
			}
		}

		private bool CanRunLocalPaymentScanSession()
		{
			if (_currentInteractor != null)
			{
				return _currentInteractor.IsOwner;
			}
			return false;
		}

		private void ClientCompletePaymentScan(byte noneCount, byte blueCount, byte greenCount, byte redCount, byte yellowCount)
		{
			ServerCompletePaymentScanRpc(noneCount, blueCount, greenCount, redCount, yellowCount);
		}

		private void PlayPaymentSuccessLocal()
		{
			if (!(_scanSuccessClip == null))
			{
				if (_paymentScanAudio == null)
				{
					GameObject gameObject = new GameObject("Payment scan audio");
					gameObject.transform.SetParent(base.transform, worldPositionStays: false);
					_paymentScanAudio = gameObject.AddComponent<AudioSource>();
					_paymentScanAudio.playOnAwake = false;
					_paymentScanAudio.spatialBlend = 1f;
					_paymentScanAudio.rolloffMode = AudioRolloffMode.Linear;
					_paymentScanAudio.minDistance = 1f;
					_paymentScanAudio.maxDistance = 8f;
					_paymentScanAudio.dopplerLevel = 0f;
				}
				_paymentScanAudio.PlayOneShot(_scanSuccessClip);
			}
		}

		private void DrawPaymentRayDebug(Ray ray, float distance, Color color)
		{
			if (_debugPaymentRay)
			{
				Debug.DrawRay(ray.origin, ray.direction * distance, color, _debugRayDuration);
			}
		}

		private void DrawHitPointDebug(Vector3 point, Color color)
		{
			if (_debugPaymentRay)
			{
				Debug.DrawLine(point - Vector3.up * 0.06f, point + Vector3.up * 0.06f, color, _debugRayDuration);
				Debug.DrawLine(point - Vector3.right * 0.06f, point + Vector3.right * 0.06f, color, _debugRayDuration);
				Debug.DrawLine(point - Vector3.forward * 0.06f, point + Vector3.forward * 0.06f, color, _debugRayDuration);
			}
		}

		private void DrawDebugOriginMarker(Vector3 point, Color color)
		{
			if (_debugPaymentRay)
			{
				Debug.DrawLine(point - Vector3.up * 0.035f, point + Vector3.up * 0.035f, color, 0f);
				Debug.DrawLine(point - Vector3.right * 0.035f, point + Vector3.right * 0.035f, color, 0f);
				Debug.DrawLine(point - Vector3.forward * 0.035f, point + Vector3.forward * 0.035f, color, 0f);
			}
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("Interact");
			Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
			if (_currentInteractor == null)
			{
				Singleton<HintsManager>.Instance.RemoveHint("KnifeAction");
			}
		}

		public override void EnableGazeHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("Interact");
			Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
			Singleton<HintsManager>.Instance.RemoveHint("KnifeAction");
			if (IsBeltPhysicallyUsableSafe())
			{
				if (IsTablePhysicallyBrokenSafe())
				{
					Singleton<HintsManager>.Instance.AddHint("Interact", "key_repair");
				}
				else if (_currentState.Value == ConveyorBeltTableState.AwaitingPayment)
				{
					Singleton<HintsManager>.Instance.AddHint("Interact", "key_collect_payment");
				}
			}
		}

		private void DEBUG_RepairConveyorTable()
		{
			if (CanRunServerDebug("DEBUG_RepairConveyorTable"))
			{
				TryRepairPhysicalState();
				DEBUG_PrintRuntimeState();
			}
		}

		private void DEBUG_ResetConveyorTableFull()
		{
			if (CanRunServerDebug("DEBUG_ResetConveyorTableFull"))
			{
				ServerResetForEndDay();
				DEBUG_PrintRuntimeState();
			}
		}

		private void DEBUG_StartFakeConveyorCustomer()
		{
			if (CanRunServerDebug("DEBUG_StartFakeConveyorCustomer"))
			{
				if (_debugRepairIfNeeded && IsTablePhysicallyBrokenSafe())
				{
					TryRepairPhysicalState();
				}
				if (_currentState.Value != ConveyorBeltTableState.Empty)
				{
					UtilityMethods.Log($"[ConveyorBeltTable DEBUG] Cannot start fake customer because table is not empty. SeatId={_seatId}, State={_currentState.Value}", LogType.Warning);
					return;
				}
				ServerInvalidateDelayedActions();
				_hasIncomingCustomer = false;
				_restaurantClosePendingPaymentAfterEating = false;
				int value = (_initialMealCount = Mathf.Max(1, _debugMealCount));
				_mealsRemaining.Value = value;
				_dirtyPlateCount.Value = 0;
				_currentDishRef = default;
				_takingOrderTimer = 0f;
				_takingOrderTimerActive = false;
				_serverPaymentCompleted = false;
				ClearServerPlatePaymentRuntime();
				ClientClearDirtyPlateVisualsRpc();
				ServerChangeState(ConveyorBeltTableState.TakingOrder);
				DEBUG_PrintRuntimeState();
			}
		}

		private void DEBUG_StartThirtyPlatePaymentTest()
		{
			if (CanRunServerDebug("DEBUG_StartThirtyPlatePaymentTest"))
			{
				if (_debugRepairIfNeeded && IsTablePhysicallyBrokenSafe())
				{
					TryRepairPhysicalState();
				}
				ServerResetForEndDay();
				_hasIncomingCustomer = false;
				_restaurantClosePendingPaymentAfterEating = false;
				_initialMealCount = 30;
				_mealsRemaining.Value = 0;
				_dirtyPlateCount.Value = 0;
				_takingOrderTimer = 0f;
				_takingOrderTimerActive = false;
				_serverPaymentCompleted = false;
				for (int i = 1; i <= 30; i++)
				{
					PlateColor randomConfiguredPlateColor = GetRandomConfiguredPlateColor();
					int num = Mathf.Max(0, _debugDishPrice);
					_dirtyPlateCount.Value = i;
					ServerRegisterRequiredPlate(randomConfiguredPlateColor);
					_dirtyPlateSnapshots.Add(new DirtyPlateSnapshot(randomConfiguredPlateColor, num));
					_totalPayment += num;
					ClientSpawnDirtyPlateVisualRpc(i, (byte)randomConfiguredPlateColor, num);
				}
				ServerChangeState(ConveyorBeltTableState.AwaitingPayment);
				DEBUG_PrintRuntimeState();
			}
		}

		private void DEBUG_SimulateDishConsumed()
		{
			if (!CanRunServerDebug("DEBUG_SimulateDishConsumed"))
			{
				return;
			}
			if (_currentState.Value != ConveyorBeltTableState.TakingOrder && _currentState.Value != ConveyorBeltTableState.Eating)
			{
				UtilityMethods.Log($"[ConveyorBeltTable DEBUG] SimulateDishConsumed blocked. SeatId={_seatId}, State={_currentState.Value}", LogType.Warning);
				return;
			}
			ServerInvalidateDelayedActions();
			_isClaimingDish = false;
			_takingOrderTimerActive = false;
			PlateColor randomConfiguredPlateColor = GetRandomConfiguredPlateColor();
			int num = Mathf.Max(0, _debugDishPrice);
			_dirtyPlateCount.Value = Mathf.Max(0, _dirtyPlateCount.Value + 1);
			ServerRegisterRequiredPlate(randomConfiguredPlateColor);
			_dirtyPlateSnapshots.Add(new DirtyPlateSnapshot(randomConfiguredPlateColor, num));
			_totalPayment += num;
			ClientSpawnDirtyPlateVisualRpc(_dirtyPlateCount.Value, (byte)randomConfiguredPlateColor, num);
			_mealsRemaining.Value = Mathf.Max(0, _mealsRemaining.Value - 1);
			if (_mealsRemaining.Value <= 0)
			{
				ServerChangeState(ConveyorBeltTableState.AwaitingPayment);
			}
			else
			{
				ServerChangeState(ConveyorBeltTableState.TakingOrder);
			}
			DEBUG_PrintRuntimeState();
		}

		private void DEBUG_ForceTargetState()
		{
			if (CanRunServerDebug("DEBUG_ForceTargetState"))
			{
				ServerChangeState(_debugTargetState);
				DEBUG_PrintRuntimeState();
			}
		}

		private void DEBUG_ForceAwaitingPayment()
		{
			if (CanRunServerDebug("DEBUG_ForceAwaitingPayment"))
			{
				if (_dirtyPlateCount.Value <= 0)
				{
					PlateColor randomConfiguredPlateColor = GetRandomConfiguredPlateColor();
					int num = Mathf.Max(0, _debugDishPrice);
					_dirtyPlateCount.Value = 1;
					ServerRegisterRequiredPlate(randomConfiguredPlateColor);
					_dirtyPlateSnapshots.Add(new DirtyPlateSnapshot(randomConfiguredPlateColor, num));
					_totalPayment += num;
					ClientSpawnDirtyPlateVisualRpc(_dirtyPlateCount.Value, (byte)randomConfiguredPlateColor, num);
				}
				ServerChangeState(ConveyorBeltTableState.AwaitingPayment);
				DEBUG_PrintRuntimeState();
			}
		}

		private void DEBUG_CompletePaymentServer()
		{
			if (!CanRunServerDebug("DEBUG_CompletePaymentServer"))
			{
				return;
			}
			if (_currentState.Value != ConveyorBeltTableState.AwaitingPayment)
			{
				UtilityMethods.Log($"[ConveyorBeltTable DEBUG] CompletePayment blocked. SeatId={_seatId}, State={_currentState.Value}", LogType.Warning);
			}
			else if (!_serverPaymentCompleted)
			{
				_serverPaymentCompleted = true;
				if (_totalPayment > 0)
				{
					ServiceLocator.ForSceneOf(this).Get<EconomyHandler>(out var service);
					service.EarnMoney(_totalPayment, ulong.MaxValue);
					AddConveyorEarnQuestProgress(_totalPayment);
				}
				ClearServerPlatePaymentRuntime();
				ServerChangeState(ConveyorBeltTableState.Empty);
				DEBUG_PrintRuntimeState();
			}
		}

		private void DEBUG_PrintRuntimeState()
		{
			UtilityMethods.Log("<b><color=#7BDFF2>[ConveyorBeltTable DEBUG]</color></b>\n" + $"  SeatId                 : {_seatId}\n" + $"  State                  : {_currentState.Value}\n" + $"  StateVersion           : {_stateVersion}\n" + $"  HasIncomingCustomer    : {_hasIncomingCustomer}\n" + $"  InitialMealCount       : {_initialMealCount}\n" + $"  MealsRemaining         : {_mealsRemaining.Value}\n" + $"  DirtyPlateCount        : {_dirtyPlateCount.Value}\n" + $"  DirtyPlateVisualCount  : {((_dirtyPlateStack != null) ? _dirtyPlateStack.Count : 0)}\n" + $"  TotalPayment           : {_totalPayment}\n" + $"  CurrentPlateColor      : {_currentPlateColor}\n" + $"  CurrentDishPrice       : {_currentDishPrice}\n" + $"  RequiredPlateCounts    : None={GetServerRequiredPlateCount(PlateColor.None)}, Blue={GetServerRequiredPlateCount(PlateColor.Blue)}, Green={GetServerRequiredPlateCount(PlateColor.Green)}, Red={GetServerRequiredPlateCount(PlateColor.Red)}, Yellow={GetServerRequiredPlateCount(PlateColor.Yellow)}\n" + $"  TakingOrderTimer       : {_takingOrderTimer:0.00}\n" + $"  TakingOrderTimerActive : {_takingOrderTimerActive}\n" + $"  PaymentScanningActive  : {_paymentScanSession != null && _paymentScanSession.IsActive}\n" + $"  ClientPaymentCompleted : {_paymentScanSession != null && _paymentScanSession.IsCompleted}\n" + $"  ServerPaymentCompleted : {_serverPaymentCompleted}\n" + $"  TableUsable            : {IsTablePhysicallyUsableSafe()}\n" + $"  BeltUsable             : {IsBeltPhysicallyUsableSafe()}");
		}

		public bool ServerStaffBeginScan(int employeeId, int expectedVersion, float minimum, float maximum)
		{
			if (!ServerStaffPaymentAvailable || expectedVersion != _stateVersion || (_staffScanner != 0 && _staffScanner != employeeId))
			{
				return false;
			}
			_staffScanner = employeeId;
			_staffScanVersion = _stateVersion;
			_staffScanStart.Value = base.NetworkManager.ServerTime.Time;
			_staffScanEnd.Value = _staffScanStart.Value + (double)Mathf.Lerp(minimum, Mathf.Max(minimum, maximum), Mathf.Clamp01((float)_dirtyPlateCount.Value / 30f));
			return true;
		}

		public bool ServerStaffFinishScan(StaffEmployeeEntry employee)
		{
			if (!ServerStaffPaymentAvailable || _staffScanner != employee.EmployeeId || _staffScanVersion != _stateVersion || base.NetworkManager.ServerTime.Time < _staffScanEnd.Value)
			{
				return false;
			}
			if (!StaffServiceGameplayApi.Earn(this, Mathf.Max(0, _totalPayment)))
			{
				return false;
			}
			AddConveyorEarnQuestProgress(Mathf.Max(0, _totalPayment));
			_serverPaymentCompleted = true;
			ServerStaffCancelScan(employee.EmployeeId);
			ClearServerPlatePaymentRuntime();
			ServerChangeState(ConveyorBeltTableState.Empty);
			StaffServiceGameplayApi.Record(employee, 0, 1, 0, _seatId.ToString());
			return true;
		}

		public bool ServerStaffScanning(int employeeId)
		{
			if (ServerStaffPaymentAvailable && _staffScanner == employeeId)
			{
				return _staffScanVersion == _stateVersion;
			}
			return false;
		}

		public void ServerStaffCancelScan(int employeeId)
		{
			if (this.CanWriteServerState() && _staffScanner == employeeId)
			{
				_staffScanner = 0;
				_staffScanEnd.Value = 0.0;
			}
		}

		private void StaffScanPresentation()
		{
			if (!base.IsSpawned || !(base.NetworkManager != null) || !base.NetworkManager.IsListening || base.NetworkManager.ShutdownInProgress || !(_staffScanEnd.Value > 0.0) || !(base.NetworkManager.ServerTime.Time <= _staffScanEnd.Value) || CurrentState != ConveyorBeltTableState.AwaitingPayment)
			{
				if (_staffScanGraphic != null)
				{
					_staffScanGraphic.enabled = false;
				}
				_staffLastScannedPlate = -1;
				return;
			}
			if (_staffScanGraphic == null)
			{
				GameObject gameObject = new GameObject("Staff Payment Scanner");
				gameObject.transform.SetParent(base.transform, worldPositionStays: false);
				_staffScanGraphic = gameObject.AddComponent<LineRenderer>();
				_staffScanGraphic.positionCount = 2;
				LineRenderer staffScanGraphic = _staffScanGraphic;
				float startWidth = (_staffScanGraphic.endWidth = 0.018f);
				staffScanGraphic.startWidth = startWidth;
				Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
				if (shader != null)
				{
					_staffScanMaterial = new Material(shader);
					_staffScanMaterial.color = Color.green;
					_staffScanGraphic.sharedMaterial = _staffScanMaterial;
				}
			}
			_staffScanGraphic.enabled = true;
			float num2 = Mathf.Clamp01((float)((base.NetworkManager.ServerTime.Time - _staffScanStart.Value) / Math.Max(0.01, _staffScanEnd.Value - _staffScanStart.Value)));
			Vector3 vector = ((_dirtyPlateSpawnAnchor != null) ? _dirtyPlateSpawnAnchor.position : (base.transform.position + Vector3.up)) + Vector3.up * Mathf.Lerp((float)_dirtyPlateCount.Value * _dirtyPlateStackHeightStep + 0.1f, 0.02f, num2);
			_staffScanGraphic.SetPosition(0, vector - base.transform.right * 0.18f);
			_staffScanGraphic.SetPosition(1, vector + base.transform.right * 0.18f);
			int num3 = Mathf.FloorToInt(num2 * (float)_dirtyPlateCount.Value);
			if (num3 != _staffLastScannedPlate)
			{
				_staffLastScannedPlate = num3;
				PlayPaymentSuccessLocal();
			}
		}

		private void StaffScanDispose()
		{
			if (_staffScanGraphic != null)
			{
				UnityEngine.Object.Destroy(_staffScanGraphic.gameObject);
			}
			if (_staffScanMaterial != null)
			{
				UnityEngine.Object.Destroy(_staffScanMaterial);
			}
		}

		protected override void __initializeVariables()
		{
			if (_currentState == null)
			{
				throw new Exception("ConveyorBeltTable._currentState cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_currentState.Initialize(this);
			__nameNetworkVariable(_currentState, "_currentState");
			NetworkVariableFields.Add(_currentState);
			if (_mealsRemaining == null)
			{
				throw new Exception("ConveyorBeltTable._mealsRemaining cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_mealsRemaining.Initialize(this);
			__nameNetworkVariable(_mealsRemaining, "_mealsRemaining");
			NetworkVariableFields.Add(_mealsRemaining);
			if (_dirtyPlateCount == null)
			{
				throw new Exception("ConveyorBeltTable._dirtyPlateCount cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_dirtyPlateCount.Initialize(this);
			__nameNetworkVariable(_dirtyPlateCount, "_dirtyPlateCount");
			NetworkVariableFields.Add(_dirtyPlateCount);
			if (_dirtyPlateSnapshots == null)
			{
				throw new Exception("ConveyorBeltTable._dirtyPlateSnapshots cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_dirtyPlateSnapshots.Initialize(this);
			__nameNetworkVariable(_dirtyPlateSnapshots, "_dirtyPlateSnapshots");
			NetworkVariableFields.Add(_dirtyPlateSnapshots);
			if (_staffScanStart == null)
			{
				throw new Exception("ConveyorBeltTable._staffScanStart cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_staffScanStart.Initialize(this);
			__nameNetworkVariable(_staffScanStart, "_staffScanStart");
			NetworkVariableFields.Add(_staffScanStart);
			if (_staffScanEnd == null)
			{
				throw new Exception("ConveyorBeltTable._staffScanEnd cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_staffScanEnd.Initialize(this);
			__nameNetworkVariable(_staffScanEnd, "_staffScanEnd");
			NetworkVariableFields.Add(_staffScanEnd);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3619955438u, __rpc_handler_3619955438, "ClientEndDayResetRpc", RpcInvokePermission.Everyone);
			__registerRpc(1170270361u, __rpc_handler_1170270361, "ServerTryUnlockTableRpc", RpcInvokePermission.Everyone);
			__registerRpc(1653954238u, __rpc_handler_1653954238, "ClientShowUnlockPriceNotifyRpc", RpcInvokePermission.Everyone);
			__registerRpc(3603008409u, __rpc_handler_3603008409, "ClientShowProgressionLockedNotifyRpc", RpcInvokePermission.Everyone);
			__registerRpc(3520613501u, __rpc_handler_3520613501, "ClientChangeStateRpc", RpcInvokePermission.Everyone);
			__registerRpc(2352785190u, __rpc_handler_2352785190, "ClientEmptyStateEnterRpc", RpcInvokePermission.Everyone);
			__registerRpc(4004408735u, __rpc_handler_4004408735, "ClientEmptyStateExitRpc", RpcInvokePermission.Everyone);
			__registerRpc(1639693462u, __rpc_handler_1639693462, "ClientTakingOrderEnterRpc", RpcInvokePermission.Everyone);
			__registerRpc(1519393206u, __rpc_handler_1519393206, "ClientTakingOrderExitRpc", RpcInvokePermission.Everyone);
			__registerRpc(1574554941u, __rpc_handler_1574554941, "ClientEatingEnterRpc", RpcInvokePermission.Everyone);
			__registerRpc(519679536u, __rpc_handler_519679536, "ClientEatingExitRpc", RpcInvokePermission.Everyone);
			__registerRpc(247722522u, __rpc_handler_247722522, "ClientAwaitingPaymentEnterRpc", RpcInvokePermission.Everyone);
			__registerRpc(1149949200u, __rpc_handler_1149949200, "ClientAwaitingPaymentExitRpc", RpcInvokePermission.Everyone);
			__registerRpc(641833588u, __rpc_handler_641833588, "ServerCompletePaymentScanRpc", RpcInvokePermission.Everyone);
			__registerRpc(683894983u, __rpc_handler_683894983, "ClientSpawnDirtyPlateVisualRpc", RpcInvokePermission.Everyone);
			__registerRpc(1853577401u, __rpc_handler_1853577401, "ClientClearDirtyPlateVisualsRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3619955438(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBeltTable)target).ClientEndDayResetRpc();
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1170270361(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBeltTable)target).ServerTryUnlockTableRpc(value, ext);
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1653954238(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				RpcParams ext = rpcParams.Ext;
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBeltTable)target).ClientShowUnlockPriceNotifyRpc(value, ext);
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3603008409(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBeltTable)target).ClientShowProgressionLockedNotifyRpc(ext);
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3520613501(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBeltTable)target).ClientChangeStateRpc(value);
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2352785190(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBeltTable)target).ClientEmptyStateEnterRpc();
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4004408735(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBeltTable)target).ClientEmptyStateExitRpc();
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1639693462(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBeltTable)target).ClientTakingOrderEnterRpc();
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1519393206(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBeltTable)target).ClientTakingOrderExitRpc();
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1574554941(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBeltTable)target).ClientEatingEnterRpc();
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_519679536(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBeltTable)target).ClientEatingExitRpc();
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_247722522(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBeltTable)target).ClientAwaitingPaymentEnterRpc(value);
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1149949200(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBeltTable)target).ClientAwaitingPaymentExitRpc();
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_641833588(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out byte value3, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out byte value4, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out byte value5, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBeltTable)target).ServerCompletePaymentScanRpc(value, value2, value3, value4, value5, ext);
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_683894983(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out int value3);
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBeltTable)target).ClientSpawnDirtyPlateVisualRpc(value, value2, value3);
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1853577401(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ConveyorBeltTable)target).ClientClearDirtyPlateVisualsRpc();
				((ConveyorBeltTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "ConveyorBeltTable";
		}
	}
}
