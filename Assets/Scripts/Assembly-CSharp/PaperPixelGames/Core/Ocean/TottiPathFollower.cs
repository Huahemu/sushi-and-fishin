using System;
using DG.Tweening;
using PaperPixelGames.Framework.Utility;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Splines;

namespace PaperPixelGames.Core.Ocean
{
	public class TottiPathFollower : NetworkBehaviour
	{
		public enum RideState
		{
			Idle = 0,
			PreStart = 1,
			GoingToDestination = 2,
			ReturningHome = 3,
			ReturnHomeTween = 4
		}

		[Header("Refs")]
		[SerializeField]
		private SplineContainer _spline;

		[Header("Spline Motion")]
		[SerializeField]
		private float _unitsPerSecond = 4f;

		[SerializeField]
		private bool _faceForward = true;

		[SerializeField]
		private float _rotationLerp = 10f;

		[Header("Pre Start Tween")]
		[SerializeField]
		private bool _usePreStart = true;

		[SerializeField]
		private float _preStartDuration = 2f;

		[SerializeField]
		private Ease _preStartEase = Ease.InOutSine;

		[Header("Return Home Tween")]
		[SerializeField]
		private float _returnHomeTweenDuration = 1.5f;

		[SerializeField]
		private Ease _returnHomeTweenEase = Ease.InOutSine;

		[Header("Stuck Check")]
		[SerializeField]
		private float _stuckMinMoveDistance = 0.01f;

		[SerializeField]
		private float _stuckTimeoutSeconds = 2f;

		[Header("Debug")]
		[SerializeField]
		private bool _logDebug;

		private RideState _state;

		private float _t;

		private Vector3 _lastPosition;

		private float _stuckTimer;

		private Tween _preStartMoveTween;

		private Tween _preStartRotateTween;

		private Sequence _returnHomeSequence;

		private Vector3 _cachedStartPosition;

		private Quaternion _cachedStartRotation;

		private bool _hasCachedStartPose;

		public RideState State => _state;

		public bool HasValidPath => _spline != null;

		public bool IsMoving
		{
			get
			{
				if (_state != RideState.PreStart && _state != RideState.GoingToDestination && _state != RideState.ReturningHome)
				{
					return _state == RideState.ReturnHomeTween;
				}
				return true;
			}
		}

		public bool IsBusy => IsMoving;

		public event Action OnPreStartStarted;

		public event Action OnRideStarted;

		public event Action OnReachedDestination;

		public event Action OnReturnedHome;

		public event Action OnRideAborted;

		public bool TryGetDestinationPose(out Vector3 position, out Quaternion rotation)
		{
			position = default;
			rotation = default;
			if (_spline == null)
			{
				return false;
			}
			EvaluatePoseAt(1f, 1, out position, out rotation);
			return true;
		}

		private void OnValidate()
		{
			_unitsPerSecond = Mathf.Max(0.01f, _unitsPerSecond);
			_rotationLerp = Mathf.Max(0.01f, _rotationLerp);
			_preStartDuration = Mathf.Max(0.01f, _preStartDuration);
			_returnHomeTweenDuration = Mathf.Max(0.01f, _returnHomeTweenDuration);
			_stuckMinMoveDistance = Mathf.Max(0f, _stuckMinMoveDistance);
			_stuckTimeoutSeconds = Mathf.Max(0.1f, _stuckTimeoutSeconds);
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			if (base.IsServer)
			{
				CacheStartPose();
				SnapToCachedStartPose();
			}
		}

		public override void OnDestroy()
		{
			KillAllTweens();
			base.OnDestroy();
		}

		private void Update()
		{
			if (base.IsServer && !(_spline == null) && (_state == RideState.GoingToDestination || _state == RideState.ReturningHome))
			{
				UpdateSplineMovementServer();
			}
		}

		public void StartRideServer()
		{
			if (!base.IsServer)
			{
				return;
			}
			if (_spline == null)
			{
				UtilityMethods.Log("[TottiPathFollower] StartRideServer failed. Spline is null.");
				return;
			}
			if (_state != RideState.Idle)
			{
				if (_logDebug)
				{
					UtilityMethods.Log($"[TottiPathFollower] StartRideServer ignored. CurrentState={_state}");
				}
				return;
			}
			if (!_hasCachedStartPose)
			{
				CacheStartPose();
			}
			if (_usePreStart)
			{
				StartPreStartServer();
			}
			else
			{
				StartForwardRideServer();
			}
		}

		public void StartPreStartServer()
		{
			if (base.IsServer && !(_spline == null))
			{
				KillAllTweens();
				_state = RideState.PreStart;
				_stuckTimer = 0f;
				_lastPosition = base.transform.position;
				EvaluatePoseAt(0f, 1, out var targetPosition, out var targetRotation);
				OnPreStartStarted?.Invoke();
				if (_logDebug)
				{
					UtilityMethods.Log("[TottiPathFollower] PreStart started.");
				}
				_preStartMoveTween = base.transform.DOMove(targetPosition, _preStartDuration).SetEase(_preStartEase);
				_preStartRotateTween = base.transform.DORotateQuaternion(targetRotation, _preStartDuration).SetEase(_preStartEase).OnComplete(() =>
				{
					base.transform.SetPositionAndRotation(targetPosition, targetRotation);
					StartForwardRideServer();
				});
			}
		}

		private void StartForwardRideServer()
		{
			_state = RideState.GoingToDestination;
			_t = 0f;
			_stuckTimer = 0f;
			EvaluatePoseAt(_t, 1, out var position, out var rotation);
			base.transform.SetPositionAndRotation(position, rotation);
			_lastPosition = base.transform.position;
			OnRideStarted?.Invoke();
			if (_logDebug)
			{
				UtilityMethods.Log("[TottiPathFollower] Ride started.");
			}
		}

		public void AbortRideServer()
		{
			if (!base.IsServer)
			{
				return;
			}
			KillAllTweens();
			if (_state != RideState.Idle)
			{
				SnapToCachedStartPose();
				OnRideAborted?.Invoke();
				if (_logDebug)
				{
					UtilityMethods.Log("[TottiPathFollower] Ride aborted.");
				}
			}
		}

		public void ResetToStartServer()
		{
			if (base.IsServer)
			{
				KillAllTweens();
				SnapToCachedStartPose();
				OnReturnedHome?.Invoke();
			}
		}

		private void UpdateSplineMovementServer()
		{
			float deltaTime = Time.deltaTime;
			if (deltaTime <= 0f)
			{
				return;
			}
			int num = ((_state == RideState.GoingToDestination) ? 1 : (-1));
			float relativeDistance = _unitsPerSecond * deltaTime * (float)num;
			_spline.Spline.GetPointAtLinearDistance(_t, relativeDistance, out var resultPointT);
			_t = resultPointT;
			if (_state == RideState.GoingToDestination && _t >= 0.9999f)
			{
				_t = 1f;
				EvaluatePoseAt(_t, 1, out var position, out var rotation);
				base.transform.SetPositionAndRotation(position, rotation);
				_state = RideState.ReturningHome;
				_lastPosition = base.transform.position;
				_stuckTimer = 0f;
				OnReachedDestination?.Invoke();
				if (_logDebug)
				{
					UtilityMethods.Log("[TottiPathFollower] Reached destination. Returning home.");
				}
			}
			else if (_state == RideState.ReturningHome && _t <= 0.0001f)
			{
				_t = 0f;
				EvaluatePoseAt(_t, -1, out var position2, out var rotation2);
				base.transform.SetPositionAndRotation(position2, rotation2);
				StartReturnHomeTweenServer();
				if (_logDebug)
				{
					UtilityMethods.Log("[TottiPathFollower] Reached spline start. Tweening to cached home pose.");
				}
			}
			else
			{
				EvaluatePoseAt(_t, num, out var position3, out var rotation3);
				base.transform.SetPositionAndRotation(position3, rotation3);
				RunStuckCheckServer();
			}
		}

		private void StartReturnHomeTweenServer()
		{
			KillAllTweens();
			_state = RideState.ReturnHomeTween;
			_stuckTimer = 0f;
			_lastPosition = base.transform.position;
			_returnHomeSequence = DOTween.Sequence();
			_returnHomeSequence.Join(base.transform.DOMove(_cachedStartPosition, _returnHomeTweenDuration).SetEase(_returnHomeTweenEase)).Join(base.transform.DORotateQuaternion(_cachedStartRotation, _returnHomeTweenDuration).SetEase(_returnHomeTweenEase)).OnComplete(() =>
			{
				base.transform.SetPositionAndRotation(_cachedStartPosition, _cachedStartRotation);
				_state = RideState.Idle;
				_t = 0f;
				_stuckTimer = 0f;
				_lastPosition = base.transform.position;
				OnReturnedHome?.Invoke();
				if (_logDebug)
				{
					UtilityMethods.Log("[TottiPathFollower] Returned home. Idle.");
				}
			});
		}

		private void EvaluatePoseAt(float normalizedT, int direction, out Vector3 position, out Quaternion rotation)
		{
			float3 float5 = _spline.EvaluatePosition(normalizedT);
			float3 float6 = _spline.EvaluateTangent(normalizedT);
			position = float5;
			rotation = base.transform.rotation;
			if (_faceForward)
			{
				Vector3 vector = (Vector3)float6 * (float)direction;
				vector.y = 0f;
				if (!(vector.sqrMagnitude <= 1E-06f))
				{
					rotation = Quaternion.LookRotation(vector.normalized, Vector3.up);
				}
			}
		}

		private void RunStuckCheckServer()
		{
			if (Vector3.Distance(base.transform.position, _lastPosition) < _stuckMinMoveDistance)
			{
				_stuckTimer += Time.deltaTime;
				if (_stuckTimer >= _stuckTimeoutSeconds)
				{
					if (_logDebug)
					{
						UtilityMethods.Log("[TottiPathFollower] Stuck detected. Aborting ride.");
					}
					AbortRideServer();
					return;
				}
			}
			else
			{
				_stuckTimer = 0f;
			}
			_lastPosition = base.transform.position;
		}

		private void CacheStartPose()
		{
			_cachedStartPosition = base.transform.position;
			_cachedStartRotation = base.transform.rotation;
			_hasCachedStartPose = true;
			if (_logDebug)
			{
				UtilityMethods.Log("[TottiPathFollower] Start pose cached.");
			}
		}

		private void SnapToCachedStartPose()
		{
			if (!_hasCachedStartPose)
			{
				CacheStartPose();
			}
			_state = RideState.Idle;
			_t = 0f;
			_stuckTimer = 0f;
			base.transform.SetPositionAndRotation(_cachedStartPosition, _cachedStartRotation);
			_lastPosition = base.transform.position;
			if (_logDebug)
			{
				UtilityMethods.Log("[TottiPathFollower] Snapped to cached start pose.");
			}
		}

		private void KillAllTweens()
		{
			_preStartMoveTween?.Kill();
			_preStartMoveTween = null;
			_preStartRotateTween?.Kill();
			_preStartRotateTween = null;
			_returnHomeSequence?.Kill();
			_returnHomeSequence = null;
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
			return "TottiPathFollower";
		}
	}
}
