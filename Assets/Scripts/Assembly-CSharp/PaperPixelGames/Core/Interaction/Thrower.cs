using PaperPixelGames.Core.Player;
using UnityEngine;

namespace PaperPixelGames.Core.Interaction
{
	public class Thrower : MonoBehaviour
	{
		public struct ThrowPhysicsRequestData
		{
			public Vector3 StartPosition;

			public Vector3 Direction;

			public float Force;

			public float UpwardForce;

			public Vector3 TorqueImpulse;

			public int ForceModeId;

			public float Mass;

			public bool UseGravity;

			public float LinearDamping;

			public float AngularDamping;

			public int CollisionDetectionModeId;

			public int InterpolationId;

			public int ExcludeLayerMaskValue;

			public float SettleVelocity;

			public float SettleStableDuration;

			public float MaxFlightDuration;

			public bool StartedUnderwater;

			public bool IsValid;
		}

		[SerializeField]
		private PPG_Player _player;

		[SerializeField]
		private Transform _cameraLookPoint;

		[SerializeField]
		private Transform _throwStartPoint;

		[SerializeField]
		private float _throwForce = 5f;

		[SerializeField]
		private float _upwardForce = 0.6f;

		[SerializeField]
		private Vector3 _torqueImpulse = new Vector3(0.75f, 1.4f, 0.45f);

		[SerializeField]
		private bool _randomizeTorqueSign = true;

		[SerializeField]
		private ForceMode _forceMode = ForceMode.Impulse;

		[SerializeField]
		[Range(0.01f, 1f)]
		private float _underwaterForceMultiplier = 0.25f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _underwaterUpwardForceMultiplier = 0.15f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _underwaterTorqueMultiplier = 0.35f;

		[SerializeField]
		private float _mass = 1f;

		[SerializeField]
		private bool _useGravity = true;

		[SerializeField]
		private float _linearDamping = 0.1f;

		[SerializeField]
		private float _angularDamping = 0.05f;

		[SerializeField]
		private CollisionDetectionMode _collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

		[SerializeField]
		private RigidbodyInterpolation _interpolation = RigidbodyInterpolation.Interpolate;

		[SerializeField]
		private LayerMask _throwExcludeLayers = 0;

		[SerializeField]
		private float _settleVelocity = 0.08f;

		[SerializeField]
		private float _settleStableDuration = 0.3f;

		[SerializeField]
		private float _maxFlightDuration = 5f;

		[SerializeField]
		private bool _flattenForwardOnGround;

		[SerializeField]
		private bool _drawDebug = true;

		[SerializeField]
		private float _debugDrawDuration = 1f;

		private BaseHoldable _currentHeld;

		public bool InThrowMode { get; private set; }

		public float CurrentStrength { get; private set; }

		public float CurrentCharge01 => CurrentStrength;

		public void SetHeld(BaseHoldable held)
		{
			_currentHeld = held;
			CurrentStrength = Mathf.Max(0.01f, _throwForce);
		}

		public void StartThrow(BaseHoldable held)
		{
			_currentHeld = held;
			InThrowMode = true;
			CurrentStrength = Mathf.Max(0.01f, _throwForce);
		}

		public void EndThrow()
		{
			InThrowMode = false;
			CurrentStrength = 0f;
			_currentHeld = null;
		}

		public bool TryGetThrowRequestPayload(out ThrowPhysicsRequestData data)
		{
			data = default;
			if (_currentHeld == null || !_currentHeld.IsThrowable)
			{
				return false;
			}
			if (!InThrowMode)
			{
				StartThrow(_currentHeld);
			}
			Transform transform = ResolveCameraTransform();
			if (transform == null)
			{
				return false;
			}
			Vector3 vector = ResolveThrowDirection(transform.forward);
			if (!IsFinite(vector) || vector.sqrMagnitude < 0.0001f)
			{
				return false;
			}
			Vector3 vector2 = ResolveStartPosition(transform.position, vector);
			if (!IsFinite(vector2))
			{
				return false;
			}
			data = BuildThrowRequestPayload(vector2, vector, transform);
			CurrentStrength = data.Force;
			DrawDebug(data);
			return true;
		}

		public bool TryBuildDirectedThrowRequestPayload(BaseHoldable holdable, Vector3 startPosition, Vector3 direction, out ThrowPhysicsRequestData data)
		{
			data = default;
			if (holdable == null || !holdable.IsThrowable)
			{
				return false;
			}
			if (!IsFinite(startPosition) || !IsFinite(direction) || direction.sqrMagnitude < 0.0001f)
			{
				return false;
			}
			Transform transform = ResolveCameraTransform();
			if (transform == null)
			{
				transform = base.transform;
			}
			data = BuildThrowRequestPayload(startPosition, direction.normalized, transform);
			DrawDebug(data);
			return true;
		}

		public bool TryBuildFreeFallRequestPayload(BaseHoldable holdable, Vector3 startPosition, out ThrowPhysicsRequestData data)
		{
			data = default;
			if (holdable == null || !holdable.IsThrowable || !IsFinite(startPosition))
			{
				return false;
			}
			Transform transform = ResolveCameraTransform();
			if (transform == null)
			{
				transform = base.transform;
			}
			data = BuildThrowRequestPayload(startPosition, Vector3.down, transform);
			data.Force = 0.01f;
			data.UpwardForce = 0f;
			data.TorqueImpulse = Vector3.zero;
			data.UseGravity = true;
			return true;
		}

		private ThrowPhysicsRequestData BuildThrowRequestPayload(Vector3 startPosition, Vector3 direction, Transform torqueTransform)
		{
			bool flag = _player != null && _player.IsUnderWater;
			float num = (flag ? _underwaterForceMultiplier : 1f);
			float num2 = (flag ? _underwaterUpwardForceMultiplier : 1f);
			float num3 = (flag ? _underwaterTorqueMultiplier : 1f);
			return new ThrowPhysicsRequestData
			{
				StartPosition = startPosition,
				Direction = direction.normalized,
				Force = Mathf.Max(0.01f, _throwForce * num),
				UpwardForce = Mathf.Max(0f, _upwardForce * num2),
				TorqueImpulse = BuildTorqueImpulse(torqueTransform) * num3,
				ForceModeId = (int)_forceMode,
				Mass = Mathf.Max(0.01f, _mass),
				UseGravity = _useGravity,
				LinearDamping = Mathf.Max(0f, _linearDamping),
				AngularDamping = Mathf.Max(0f, _angularDamping),
				CollisionDetectionModeId = (int)_collisionDetectionMode,
				InterpolationId = (int)_interpolation,
				ExcludeLayerMaskValue = _throwExcludeLayers.value,
				SettleVelocity = Mathf.Max(0.01f, _settleVelocity),
				SettleStableDuration = Mathf.Max(0.01f, _settleStableDuration),
				MaxFlightDuration = Mathf.Max(0.1f, _maxFlightDuration),
				StartedUnderwater = flag,
				IsValid = true
			};
		}

		private Transform ResolveCameraTransform()
		{
			if (_cameraLookPoint != null)
			{
				return _cameraLookPoint;
			}
			if (_player != null && _player.CameraTransform != null)
			{
				return _player.CameraTransform;
			}
			return null;
		}

		private Vector3 ResolveStartPosition(Vector3 cameraOrigin, Vector3 direction)
		{
			if (_throwStartPoint != null)
			{
				return _throwStartPoint.position;
			}
			return cameraOrigin + direction * 0.35f;
		}

		private Vector3 ResolveThrowDirection(Vector3 cameraForward)
		{
			Vector3 vector = cameraForward;
			if (_flattenForwardOnGround)
			{
				vector = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
				if (vector.sqrMagnitude < 0.0001f && _player != null)
				{
					vector = Vector3.ProjectOnPlane(_player.transform.forward, Vector3.up);
				}
			}
			if (vector.sqrMagnitude < 0.0001f)
			{
				return Vector3.zero;
			}
			return vector.normalized;
		}

		private void DrawDebug(ThrowPhysicsRequestData data)
		{
			if (_drawDebug)
			{
				Debug.DrawRay(data.StartPosition, data.Direction * Mathf.Max(0.25f, data.Force), Color.cyan, _debugDrawDuration);
				if (data.UpwardForce > 0f)
				{
					Debug.DrawRay(data.StartPosition, Vector3.up * data.UpwardForce, Color.yellow, _debugDrawDuration);
				}
				if (data.TorqueImpulse.sqrMagnitude > 0.0001f)
				{
					Debug.DrawRay(data.StartPosition, data.TorqueImpulse, Color.magenta, _debugDrawDuration);
				}
			}
		}

		private Vector3 BuildTorqueImpulse(Transform cameraTransform)
		{
			Vector3 one = Vector3.one;
			if (_randomizeTorqueSign)
			{
				one.x = ((Random.value < 0.5f) ? (-1f) : 1f);
				one.y = ((Random.value < 0.5f) ? (-1f) : 1f);
				one.z = ((Random.value < 0.5f) ? (-1f) : 1f);
			}
			return cameraTransform.right * (_torqueImpulse.x * one.x) + cameraTransform.up * (_torqueImpulse.y * one.y) + cameraTransform.forward * (_torqueImpulse.z * one.z);
		}

		private static bool IsFinite(Vector3 value)
		{
			if (float.IsFinite(value.x) && float.IsFinite(value.y))
			{
				return float.IsFinite(value.z);
			}
			return false;
		}
	}
}
