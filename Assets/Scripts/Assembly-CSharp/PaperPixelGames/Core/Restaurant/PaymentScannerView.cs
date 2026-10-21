using System;
using System.Collections.Generic;
using DG.Tweening;
using PaperPixelGames.Framework.Enums;
using TMPro;
using UnityEngine;

namespace PaperPixelGames.Core.Restaurant
{
	public class PaymentScannerView : MonoBehaviour
	{
		[Serializable]
		private class PlateCounterRow
		{
			public PlateColor Color;

			public TextMeshProUGUI CountText;
		}

		[Header("References")]
		[SerializeField]
		private Transform _visualRoot;

		[SerializeField]
		private Transform _scanRayOrigin;

		[Header("Scan Ray")]
		[SerializeField]
		private Vector3 _scanRayLocalOffset = Vector3.zero;

		[SerializeField]
		private Vector3 _scanRayEulerOffset = Vector3.zero;

		[Header("Payment UI")]
		[SerializeField]
		private List<PlateCounterRow> _plateCounterRows = new List<PlateCounterRow>();

		[SerializeField]
		private TextMeshProUGUI _plateTotalText;

		[SerializeField]
		private TextMeshProUGUI _paymentTotalText;

		[SerializeField]
		private GameObject _scanGuide;

		[Header("Look Rotation")]
		[SerializeField]
		private float _rotationSmooth = 14f;

		[SerializeField]
		private float _maxYaw = 18f;

		[SerializeField]
		private float _maxPitchUp = 10f;

		[SerializeField]
		private float _maxPitchDown = 16f;

		[SerializeField]
		private float _maxRoll = 4f;

		[Header("Idle Sway")]
		[SerializeField]
		private bool _useIdleSway = true;

		[SerializeField]
		private float _idleSwayAmplitude = 1.25f;

		[SerializeField]
		private float _idleSwaySpeed = 2.4f;

		[SerializeField]
		private float _idleRollAmplitude = 0.8f;

		[Header("Vertical Motion")]
		[SerializeField]
		[Min(0f)]
		private float _verticalMoveRange = 0.18f;

		[SerializeField]
		[Min(0f)]
		private float _verticalMoveSmooth = 14f;

		[Header("Scan Feedback")]
		[SerializeField]
		private float _successPunchStrength = 6f;

		[SerializeField]
		private float _successPunchDuration = 0.12f;

		[SerializeField]
		private int _successPunchVibrato = 8;

		[SerializeField]
		private float _failPunchStrength = 2.5f;

		[SerializeField]
		private float _failPunchDuration = 0.08f;

		[SerializeField]
		private int _failPunchVibrato = 6;

		private readonly Dictionary<PlateColor, int> _scannedCounts = new Dictionary<PlateColor, int>();

		private float _currentYaw;

		private float _currentPitch;

		private float _currentRoll;

		private int _totalScannedPlateCount;

		private int _totalPayment;

		private Quaternion _initialVisualLocalRotation;

		private Vector3 _initialVisualLocalPosition;

		private Vector3 _currentPositionOffset;

		public Transform ScanRayOrigin
		{
			get
			{
				if (!(_scanRayOrigin != null))
				{
					return base.transform;
				}
				return _scanRayOrigin;
			}
		}

		private void Awake()
		{
			if (_visualRoot == null)
			{
				_visualRoot = base.transform;
			}
			_initialVisualLocalRotation = _visualRoot.localRotation;
			_initialVisualLocalPosition = _visualRoot.localPosition;
			ResetPaymentUI();
		}

		public void ResetImmediate()
		{
			ResetVisualPoseImmediate();
			ResetPaymentUI();
		}

		public void ResetVisualPoseImmediate()
		{
			_currentYaw = 0f;
			_currentPitch = 0f;
			_currentRoll = 0f;
			_currentPositionOffset = Vector3.zero;
			if (_visualRoot != null)
			{
				_visualRoot.DOKill();
				_visualRoot.localRotation = _initialVisualLocalRotation;
				_visualRoot.localPosition = _initialVisualLocalPosition;
			}
		}

		public void InitializeBaseRotation()
		{
			if (_visualRoot == null)
			{
				_visualRoot = base.transform;
			}
			_initialVisualLocalRotation = _visualRoot.localRotation;
			_initialVisualLocalPosition = _visualRoot.localPosition;
			ResetImmediate();
		}

		public void ResetPaymentUI()
		{
			_scannedCounts.Clear();
			_totalScannedPlateCount = 0;
			_totalPayment = 0;
			for (int i = 0; i < _plateCounterRows.Count; i++)
			{
				PlateCounterRow plateCounterRow = _plateCounterRows[i];
				if (plateCounterRow != null)
				{
					_scannedCounts[plateCounterRow.Color] = 0;
					if (plateCounterRow.CountText != null)
					{
						plateCounterRow.CountText.text = "x 0";
					}
				}
			}
			RefreshTotals();
		}

		public void SetScanGuideVisible(bool visible)
		{
			if (_scanGuide != null && _scanGuide.activeSelf != visible)
			{
				_scanGuide.SetActive(visible);
			}
		}

		public void RegisterScannedPlate(PlateColor color, int dishPrice)
		{
			if (!_scannedCounts.ContainsKey(color))
			{
				_scannedCounts[color] = 0;
			}
			_scannedCounts[color]++;
			_totalScannedPlateCount++;
			_totalPayment += Mathf.Max(0, dishPrice);
			RefreshCounterRow(color);
			RefreshTotals();
		}

		private void RefreshCounterRow(PlateColor color)
		{
			for (int i = 0; i < _plateCounterRows.Count; i++)
			{
				PlateCounterRow plateCounterRow = _plateCounterRows[i];
				if (plateCounterRow != null && plateCounterRow.Color == color)
				{
					int num = (_scannedCounts.TryGetValue(color, out var value) ? value : 0);
					if (plateCounterRow.CountText != null)
					{
						plateCounterRow.CountText.text = $"x {num}";
					}
					break;
				}
			}
		}

		private void RefreshTotals()
		{
			if (_plateTotalText != null)
			{
				_plateTotalText.text = $"{_totalScannedPlateCount}";
			}
			if (_paymentTotalText != null)
			{
				_paymentTotalText.text = $"{_totalPayment}";
			}
		}

		public void TickVisual(Vector2 normalizedScreenOffset, float deltaTime)
		{
			Vector3 targetOffset = Vector3.up * (normalizedScreenOffset.y * _verticalMoveRange);
			TickVisualInternal(normalizedScreenOffset, deltaTime, targetOffset);
		}

		public void TickVisual(Vector2 normalizedScreenOffset, float deltaTime, Vector3 bottomScanWorldPosition, Vector3 topScanWorldPosition)
		{
			float t = Mathf.Clamp01((normalizedScreenOffset.y + 1f) * 0.5f);
			Vector3 targetScanWorldPosition = Vector3.Lerp(bottomScanWorldPosition, topScanWorldPosition, t);
			TickVisualAtScanPosition(normalizedScreenOffset, deltaTime, targetScanWorldPosition);
		}

		public void TickVisualAtScanPosition(Vector2 normalizedScreenOffset, float deltaTime, Vector3 targetScanWorldPosition)
		{
			Vector3 targetOffset = CalculateVisualOffsetForScanPosition(targetScanWorldPosition);
			TickVisualInternal(normalizedScreenOffset, deltaTime, targetOffset);
		}

		public void SetVisualScanPositionImmediate(Vector3 targetScanWorldPosition)
		{
			_currentYaw = 0f;
			_currentPitch = 0f;
			_currentRoll = 0f;
			_currentPositionOffset = CalculateVisualOffsetForScanPosition(targetScanWorldPosition);
			if (_visualRoot != null)
			{
				_visualRoot.DOKill();
				_visualRoot.localRotation = _initialVisualLocalRotation;
				_visualRoot.localPosition = _initialVisualLocalPosition + _currentPositionOffset;
			}
		}

		public Ray CreateScanRay()
		{
			Transform scanRayOrigin = ScanRayOrigin;
			Vector3 origin = scanRayOrigin.TransformPoint(_scanRayLocalOffset);
			return new Ray(origin, (scanRayOrigin.rotation * Quaternion.Euler(_scanRayEulerOffset) * Vector3.forward).normalized);
		}

		private void TickVisualInternal(Vector2 normalizedScreenOffset, float deltaTime, Vector3 targetOffset)
		{
			float b = ((!(normalizedScreenOffset.y >= 0f)) ? ((0f - normalizedScreenOffset.y) * _maxPitchDown) : ((0f - normalizedScreenOffset.y) * _maxPitchUp));
			float b2 = normalizedScreenOffset.x * _maxYaw;
			float b3 = (0f - normalizedScreenOffset.x) * _maxRoll;
			_currentPitch = Mathf.Lerp(_currentPitch, b, deltaTime * _rotationSmooth);
			_currentYaw = Mathf.Lerp(_currentYaw, b2, deltaTime * _rotationSmooth);
			_currentRoll = Mathf.Lerp(_currentRoll, b3, deltaTime * _rotationSmooth);
			_currentPositionOffset = Vector3.Lerp(_currentPositionOffset, targetOffset, deltaTime * _verticalMoveSmooth);
			float num = 0f;
			float num2 = 0f;
			if (_useIdleSway)
			{
				float f = Time.time * _idleSwaySpeed;
				num = Mathf.Sin(f) * _idleSwayAmplitude;
				num2 = Mathf.Cos(f) * _idleRollAmplitude;
			}
			Quaternion quaternion = Quaternion.Euler(_currentPitch + num, _currentYaw, _currentRoll + num2);
			_visualRoot.localRotation = _initialVisualLocalRotation * quaternion;
			_visualRoot.localPosition = _initialVisualLocalPosition + _currentPositionOffset;
		}

		private Vector3 CalculateVisualOffsetForScanPosition(Vector3 targetScanWorldPosition)
		{
			if (_visualRoot == null)
			{
				return Vector3.zero;
			}
			Transform scanRayOrigin = ScanRayOrigin;
			Transform parent = _visualRoot.parent;
			if (parent == null)
			{
				return targetScanWorldPosition - (scanRayOrigin.position - _currentPositionOffset);
			}
			Vector3 vector = parent.TransformVector(_currentPositionOffset);
			Vector3 vector2 = scanRayOrigin.position - vector;
			Vector3 vector3 = targetScanWorldPosition - vector2;
			return parent.InverseTransformVector(vector3);
		}

		public void PlaySuccessFeedback()
		{
			if (!(_visualRoot == null))
			{
				_visualRoot.DOKill();
				_visualRoot.DOPunchRotation(new Vector3(0f - _successPunchStrength, 0f, 0f), _successPunchDuration, _successPunchVibrato, 0.6f);
			}
		}

		public void PlayFailFeedback()
		{
			if (!(_visualRoot == null))
			{
				_visualRoot.DOKill();
				_visualRoot.DOPunchRotation(new Vector3(0f - _failPunchStrength, 0f, 0f), _failPunchDuration, _failPunchVibrato, 0.4f);
			}
		}
	}
}
