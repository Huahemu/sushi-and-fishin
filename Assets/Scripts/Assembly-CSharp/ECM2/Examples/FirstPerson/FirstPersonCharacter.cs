using System;
using System.Collections;
using PaperPixelGames.Core.Player;
using UnityEngine;

namespace ECM2.Examples.FirstPerson
{
	[DefaultExecutionOrder(50)]
	public class FirstPersonCharacter : Character
	{
		private enum MotionActionType
		{
			None = 0,
			Drag = 1,
			Swing = 2,
			OrbitSwing = 3,
			GrapplePull = 4
		}

		[Tooltip("The first person camera parent.")]
		public GameObject cameraParent;

		[Header("Swimming Glide")]
		[SerializeField]
		[Min(0f)]
		private float _swimEntryGlideTime = 0.55f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _swimEntryFrictionMultiplier = 0.2f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _swimEntryBrakingMultiplier;

		[SerializeField]
		[Range(0f, 1f)]
		private float _swimNoInputFrictionMultiplier = 0.45f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _swimNoInputBrakingMultiplier = 0.2f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _swimSurfaceUpClampDepth = 0.6f;

		[SerializeField]
		[Range(0f, 0.5f)]
		private float _swimSurfaceUpSpeedPercent = 0.08f;

		[SerializeField]
		[Min(0f)]
		private float _swimSurfaceVerticalDamping = 1.25f;

		private float _swimEntryGlideTimer;

		[Header("Swing")]
		[SerializeField]
		[Min(0f)]
		private float _swingEpsilon = 0.05f;

		[SerializeField]
		private bool _swingDebug;

		[Header("Motion")]
		[SerializeField]
		[Min(0f)]
		private float _impulseGroundPauseTime = 0.1f;

		[SerializeField]
		[Min(0.01f)]
		private float _dragRampDurationFraction = 0.3f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _dragStartForceFraction = 0.75f;

		[SerializeField]
		[Range(0f, 1.5f)]
		private float _dragEndForceFraction = 1f;

		[SerializeField]
		[Min(0.01f)]
		private float _dragCurveExponent = 0.9f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _dragNearForceReduction = 0.5f;

		[SerializeField]
		[HideInInspector]
		private bool _swingUseHardConstraint = true;

		[SerializeField]
		[HideInInspector]
		[Range(0f, 1f)]
		private float _swingHardConstraintLerp = 0.35f;

		private float _cameraPitch;

		private PPG_Player _player;

		private Coroutine _lookCoroutine;

		private Coroutine _activeMotionRoutine;

		private Transform _swingAnchor;

		private MotionActionType _activeMotionType;

		private bool _activeMotionOwnsGroundConstraint;

		private Transform _orbitAnchor;

		private Vector3 _orbitLocalPivot;

		private Vector3 _orbitAxis;

		private Vector3 _orbitInitialForward;

		private float _orbitDirectionSign;

		private float _orbitRadius;

		private float _orbitStartDistance;

		private float _orbitForwardTargetSpeed;

		private float _orbitForwardAcceleration;

		private float _orbitTargetSpeed;

		private float _orbitAcceleration;

		private float _orbitRadialSpring;

		private float _orbitRadialDamp;

		private float _orbitPlaneDamp;

		private float _orbitMaxAcceleration;

		private float _orbitBreakDistance;

		private bool _orbitBreakAppliesReleaseVelocity;

		private float _orbitReleaseSpeed;

		private Action _orbitAutoReleaseCallback;

		public bool IsSwinging => _activeMotionType == MotionActionType.Swing;

		public bool IsOrbitSwinging => _activeMotionType == MotionActionType.OrbitSwing;

		public bool HasActiveMotion => _activeMotionRoutine != null;

		public float CameraPitch => _cameraPitch;

		protected override void Awake()
		{
			base.Awake();
			_player = GetComponent<PPG_Player>();
			base.enableAutoSimulation = false;
		}

		protected override void OnEnable()
		{
			base.enableAutoSimulation = false;
			base.OnEnable();
			if (_player == null || _player.IsOwner)
			{
				base.characterMovement.interpolation = RigidbodyInterpolation.None;
			}
		}

		protected override void OnDisable()
		{
			StopLookAt();
			StopActiveMotion();
			base.OnDisable();
		}

		private void LateUpdate()
		{
			if (!(_player != null) || _player.IsOwner)
			{
				SimulateFrame(Time.deltaTime);
				UpdateCameraParentRotation();
			}
		}

		public void SimulateFrame(float deltaTime)
		{
			if (deltaTime <= 0f)
				return;
			if (!IsWalking() && !IsFalling())
			{
				Simulate(deltaTime);
				return;
			}
			// Large render frames otherwise apply gravity before moving for the
			// whole frame, erasing the jump impulse at low editor frame rates.
			int steps = Mathf.CeilToInt(deltaTime / (1f / 120f));
			float stepTime = deltaTime / steps;
			for (int step = 0; step < steps; step++)
				Simulate(stepTime);
		}

		protected override void OnMovementModeChanged(MovementMode prevMovementMode, int prevCustomMode)
		{
			base.OnMovementModeChanged(prevMovementMode, prevCustomMode);
			if (base.movementMode == MovementMode.Swimming && prevMovementMode != MovementMode.Swimming && prevMovementMode != MovementMode.SeaScooter)
			{
				_swimEntryGlideTimer = ((base.characterMovement.velocity.sqrMagnitude > 0.01f) ? _swimEntryGlideTime : 0f);
			}
			else if (base.movementMode != MovementMode.Swimming)
			{
				_swimEntryGlideTimer = 0f;
			}
		}

		protected override void SwimmingMovementMode(float deltaTime)
		{
			float num = Mathf.Clamp01(CalcImmersionDepth());
			float num2 = num * num * (3f - 2f * num);
			float num3 = base.buoyancy * num2;
			Vector3 vector = GetDesiredVelocity();
			Vector3 vector2 = base.characterMovement.velocity;
			Vector3 vector3 = -GetGravityDirection();
			if (num < _swimSurfaceUpClampDepth)
			{
				float b = Vector3.Dot(vector, vector3);
				float a = base.maxSwimSpeed * _swimSurfaceUpSpeedPercent;
				vector = Vector3.ProjectOnPlane(vector, vector3) + vector3 * Mathf.Min(a, b);
			}
			if (base.useRootMotion && (bool)base.rootMotionController)
			{
				Vector3 vector4 = Vector3.Project(vector2, vector3);
				vector2 = Vector3.ProjectOnPlane(vector, vector3) + vector4;
			}
			else
			{
				bool num4 = vector.sqrMagnitude > 0.0001f;
				bool flag = vector2.magnitude > base.maxSwimSpeed + 0.001f;
				float num5 = (IsInWaterPhysicsVolume() ? base.physicsVolume.friction : base.swimmingFriction) * num2;
				float num6 = base.brakingDecelerationSwimming;
				if (!num4 | flag)
				{
					num5 *= _swimNoInputFrictionMultiplier;
				}
				if (!num4)
				{
					num6 *= _swimNoInputBrakingMultiplier;
				}
				if (_swimEntryGlideTimer > 0f && _swimEntryGlideTime > 0f)
				{
					float t = Mathf.Clamp01(_swimEntryGlideTimer / _swimEntryGlideTime);
					num5 *= Mathf.Lerp(1f, _swimEntryFrictionMultiplier, t);
					num6 *= Mathf.Lerp(1f, _swimEntryBrakingMultiplier, t);
					_swimEntryGlideTimer = Mathf.Max(0f, _swimEntryGlideTimer - deltaTime);
				}
				vector2 = CalcSwimVelocityWithGlide(vector2, vector, num5, num6, deltaTime);
			}
			vector2 += base.gravity * ((1f - num3) * deltaTime);
			if (_swimEntryGlideTimer <= 0f && num > 0.05f && num < 0.95f && _swimSurfaceVerticalDamping > 0f)
			{
				float num7 = Vector3.Dot(vector2, vector3);
				float num8 = Mathf.Clamp01(_swimSurfaceVerticalDamping * deltaTime);
				vector2 -= vector3 * (num7 * num8);
			}
			base.characterMovement.velocity = vector2;
		}

		private Vector3 CalcSwimVelocityWithGlide(Vector3 velocity, Vector3 desiredVelocity, float friction, float brakingDeceleration, float deltaTime)
		{
			if (deltaTime < 1E-06f)
			{
				return velocity;
			}
			float magnitude = desiredVelocity.magnitude;
			Vector3 vector = ((magnitude > 0.0001f) ? (desiredVelocity / magnitude) : Vector3.zero);
			float num = ((base.maxSwimSpeed > 0.0001f) ? Mathf.Clamp01(magnitude / base.maxSwimSpeed) : 0f);
			Vector3 vector2 = GetMaxAcceleration() * num * vector;
			float num2 = Mathf.Max(GetMinAnalogSpeed(), base.maxSwimSpeed * num);
			bool num3 = vector2.sqrMagnitude > 0.0001f;
			bool flag = velocity.magnitude > num2 + 0.001f;
			bool flag2 = velocity.magnitude > base.maxSwimSpeed + 0.001f;
			if (!num3)
			{
				if (!flag2)
				{
					velocity = ApplyVelocityBraking(velocity, friction, brakingDeceleration, deltaTime);
				}
			}
			else
			{
				Vector3 normalized = vector2.normalized;
				float magnitude2 = velocity.magnitude;
				velocity -= (velocity - normalized * magnitude2) * Mathf.Min(friction * deltaTime, 1f);
				if (flag2 && velocity.sqrMagnitude > 0.0001f)
				{
					velocity = velocity.normalized * magnitude2;
				}
			}
			velocity *= 1f - Mathf.Min(friction * deltaTime, 1f);
			if (num3)
			{
				float num4 = (flag ? Mathf.Max(num2, velocity.magnitude) : num2);
				velocity += vector2 * deltaTime;
				if (num4 > 0f && velocity.sqrMagnitude > num4 * num4)
				{
					velocity = velocity.normalized * num4;
				}
			}
			return velocity;
		}

		public void AddControlYawInput(float value)
		{
			if (!Mathf.Approximately(value, 0f))
			{
				AddYawInput(value);
			}
		}

		public void AddControlPitchInput(float value, float minPitch = -80f, float maxPitch = 80f)
		{
			if (!Mathf.Approximately(value, 0f))
			{
				_cameraPitch = MathLib.ClampAngle(_cameraPitch + value, minPitch, maxPitch);
			}
		}

		public void PitchInstant(float pitch)
		{
			_cameraPitch = Mathf.Clamp(pitch, -89f, 89f);
			UpdateCameraParentRotation();
		}

		public void LookAtInstant(Vector3 worldTarget)
		{
			Vector3 lookOrigin = GetLookOrigin();
			Vector3 vector = worldTarget - lookOrigin;
			if (!(vector.sqrMagnitude < 0.0001f))
			{
				Vector3 vector2 = new Vector3(vector.x, 0f, vector.z);
				if (vector2.sqrMagnitude > 0.0001f)
				{
					base.transform.rotation = Quaternion.LookRotation(vector2.normalized, Vector3.up);
				}
				Vector3 vector3 = base.transform.InverseTransformDirection(vector.normalized);
				float num = Mathf.Atan2(vector3.y, new Vector2(vector3.x, vector3.z).magnitude) * 57.29578f;
				_cameraPitch = Mathf.Clamp(0f - num, -80f, 80f);
				UpdateCameraParentRotation();
			}
		}

		public void LookAtOverTime(Vector3 worldTarget, float duration)
		{
			StopLookAt();
			_lookCoroutine = StartCoroutine(LookAtCoroutine(worldTarget, duration));
		}

		public void StopLookAt()
		{
			if (_lookCoroutine != null)
			{
				StopCoroutine(_lookCoroutine);
				_lookCoroutine = null;
			}
		}

		private IEnumerator LookAtCoroutine(Vector3 worldTarget, float duration)
		{
			if (duration <= 0f)
			{
				LookAtInstant(worldTarget);
				_lookCoroutine = null;
				yield break;
			}
			Quaternion startRotation = base.transform.rotation;
			float startPitch = _cameraPitch;
			Vector3 lookOrigin = GetLookOrigin();
			Vector3 vector = worldTarget - lookOrigin;
			if (vector.sqrMagnitude < 0.0001f)
			{
				_lookCoroutine = null;
				yield break;
			}
			Vector3 vector2 = new Vector3(vector.x, 0f, vector.z);
			Quaternion targetRotation = ((vector2.sqrMagnitude > 0.0001f) ? Quaternion.LookRotation(vector2.normalized, Vector3.up) : startRotation);
			Vector3 vector3 = Quaternion.Inverse(targetRotation) * vector.normalized;
			float targetPitch = Mathf.Atan2(vector3.y, new Vector2(vector3.x, vector3.z).magnitude) * 57.29578f;
			targetPitch = Mathf.Clamp(0f - targetPitch, -80f, 80f);
			float elapsed = 0f;
			while (elapsed < duration)
			{
				float t = Mathf.Clamp01(elapsed / duration);
				base.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
				_cameraPitch = Mathf.Lerp(startPitch, targetPitch, t);
				UpdateCameraParentRotation();
				elapsed += Time.deltaTime;
				yield return null;
			}
			base.transform.rotation = targetRotation;
			_cameraPitch = targetPitch;
			UpdateCameraParentRotation();
			_lookCoroutine = null;
		}

		public void ImpulseInDirection(Vector3 direction, float impulse, bool allowLeaveGround = true)
		{
			if (!(direction.sqrMagnitude < 0.0001f) && !(impulse <= 0f))
			{
				PauseGroundConstraintIfNeeded(allowLeaveGround, _impulseGroundPauseTime);
				AddForce(direction.normalized * impulse, ForceMode.Impulse);
			}
		}

		public void ApplyForceForSeconds(Vector3 direction, float force, float seconds, bool allowLeaveGround = false)
		{
			if (!(direction.sqrMagnitude < 0.0001f) && !(force <= 0f) && !(seconds <= 0f))
			{
				StartCoroutine(ApplyForceRoutine(direction.normalized * force, seconds, allowLeaveGround));
			}
		}

		private IEnumerator ApplyForceRoutine(Vector3 force, float seconds, bool allowLeaveGround)
		{
			if (allowLeaveGround)
			{
				PauseGroundConstraint(seconds);
			}
			float elapsed = 0f;
			WaitForFixedUpdate wait = new WaitForFixedUpdate();
			while (elapsed < seconds)
			{
				AddForce(force);
				elapsed += Time.fixedDeltaTime;
				yield return wait;
			}
		}

		public void PushFromPoint(Vector3 origin, float impulse, float upModifier = 0f, bool allowLeaveGround = true)
		{
			if (!(impulse <= 0f))
			{
				Vector3 vector = base.transform.position - origin;
				vector.y = 0f;
				if (vector.sqrMagnitude < 0.0001f)
				{
					vector = base.transform.forward;
				}
				Vector3 force = vector.normalized * impulse + Vector3.up * upModifier;
				PauseGroundConstraintIfNeeded(allowLeaveGround, _impulseGroundPauseTime);
				AddForce(force, ForceMode.Impulse);
			}
		}

		public void PullTowardsPoint(Vector3 target, float impulse, bool allowLeaveGround = true)
		{
			if (!(impulse <= 0f))
			{
				Vector3 vector = target - base.transform.position;
				if (!(vector.sqrMagnitude < 0.0001f))
				{
					PauseGroundConstraintIfNeeded(allowLeaveGround, _impulseGroundPauseTime);
					AddForce(vector.normalized * impulse, ForceMode.Impulse);
				}
			}
		}

		public void DragTowardUntilArrive(Transform target, float force, float arriveDistance = 1.25f, float safetyMaxSeconds = 6f, bool allowLeaveGround = false, Action endDrag = null)
		{
			if (!(target == null) && !(force <= 0f))
			{
				BeginMotionAction(MotionActionType.Drag, allowLeaveGround);
				_activeMotionRoutine = StartCoroutine(DragTowardUntilArriveRoutine(target, force, Mathf.Max(0f, arriveDistance), Mathf.Max(0f, safetyMaxSeconds), endDrag));
			}
		}

		private IEnumerator DragTowardUntilArriveRoutine(Transform target, float maxForce, float arriveDistance, float safetyMaxSeconds, Action endDrag)
		{
			float elapsed = 0f;
			float rampDuration = Mathf.Max(0.01f, safetyMaxSeconds * _dragRampDurationFraction);
			float startForce = maxForce * _dragStartForceFraction;
			float endForce = maxForce * _dragEndForceFraction;
			WaitForFixedUpdate wait = new WaitForFixedUpdate();
			while (target != null && (!(safetyMaxSeconds > 0f) || !(elapsed >= safetyMaxSeconds)))
			{
				Vector3 vector = target.position - base.transform.position;
				float magnitude = vector.magnitude;
				if (magnitude <= arriveDistance)
				{
					break;
				}
				Vector3 vector2 = vector / Mathf.Max(magnitude, 0.0001f);
				float t = Mathf.Pow((safetyMaxSeconds > 0f) ? Mathf.Clamp01(elapsed / rampDuration) : 1f, _dragCurveExponent);
				float num = Mathf.Lerp(startForce, endForce, t);
				float num2 = Mathf.Max(1f, arriveDistance * 3f);
				float num3 = Mathf.InverseLerp(arriveDistance + num2, arriveDistance, magnitude);
				num *= 1f - num3 * _dragNearForceReduction;
				AddForce(vector2 * num);
				elapsed += Time.fixedDeltaTime;
				yield return wait;
			}
			CompleteMotionAction(endDrag);
		}

		public void ExplodeFrom(Vector3 origin, float force, float radius, ForceMode mode = ForceMode.Impulse)
		{
			if (!(force <= 0f) && !(radius <= 0f))
			{
				PauseGroundConstraint(_impulseGroundPauseTime);
				AddExplosionForce(force, origin, radius, 1f, mode);
			}
		}

		public void LaunchVelocity(Vector3 launchVelocity, bool overrideVertical = false, bool overrideLateral = false)
		{
			if (!(launchVelocity.sqrMagnitude < 0.0001f))
			{
				PauseGroundConstraint(_impulseGroundPauseTime);
				LaunchCharacter(launchVelocity, overrideVertical, overrideLateral);
			}
		}

		public void GrappleTo(Vector3 target, float initialSpeed, bool overrideCurrentHoriz = true)
		{
			if (!(initialSpeed <= 0f))
			{
				Vector3 vector = target - base.transform.position;
				if (!(vector.sqrMagnitude < 0.0001f))
				{
					Vector3 launchVelocity = vector.normalized * initialSpeed;
					PauseGroundConstraint(_impulseGroundPauseTime);
					LaunchCharacter(launchVelocity, overrideVerticalVelocity: false, overrideCurrentHoriz);
				}
			}
		}

		public void DragToward(Transform target, float force, float seconds, float arriveDistance = 0.5f, bool allowLeaveGround = false, Action endDrag = null)
		{
			if (!(target == null) && !(force <= 0f) && !(seconds <= 0f))
			{
				BeginMotionAction(MotionActionType.Drag, allowLeaveGround);
				_activeMotionRoutine = StartCoroutine(DragTowardRoutine(target, force, seconds, Mathf.Max(0f, arriveDistance), endDrag));
			}
		}

		public void StopDrag()
		{
			if (_activeMotionType == MotionActionType.Drag)
			{
				StopActiveMotion();
			}
		}

		private IEnumerator DragTowardRoutine(Transform target, float maxForce, float seconds, float arriveDistance, Action endDrag)
		{
			float elapsed = 0f;
			float rampDuration = Mathf.Max(0.01f, seconds * _dragRampDurationFraction);
			float startForce = maxForce * _dragStartForceFraction;
			float endForce = maxForce * _dragEndForceFraction;
			WaitForFixedUpdate wait = new WaitForFixedUpdate();
			while (elapsed < seconds && target != null)
			{
				Vector3 vector = target.position - base.transform.position;
				float magnitude = vector.magnitude;
				if (magnitude <= arriveDistance)
				{
					break;
				}
				Vector3 vector2 = vector / Mathf.Max(magnitude, 0.0001f);
				float t = Mathf.Pow(Mathf.Clamp01(elapsed / rampDuration), _dragCurveExponent);
				float num = Mathf.Lerp(startForce, endForce, t);
				float num2 = Mathf.Max(1f, arriveDistance * 3f);
				float num3 = Mathf.InverseLerp(arriveDistance + num2, arriveDistance, magnitude);
				num *= 1f - num3 * _dragNearForceReduction;
				AddForce(vector2 * num);
				elapsed += Time.fixedDeltaTime;
				yield return wait;
			}
			CompleteMotionAction(endDrag);
		}

		public void StartSwing(Transform anchor, float ropeLength, float maxPullForce, float seconds, bool allowLeaveGround, Action endSwing = null)
		{
			if (!(anchor == null) && !(maxPullForce <= 0f))
			{
				BeginMotionAction(MotionActionType.Swing, allowLeaveGround, anchor);
				_activeMotionRoutine = StartCoroutine(SwingRoutine(anchor, Mathf.Max(0.01f, ropeLength), maxPullForce, seconds, endSwing));
			}
		}

		public void StopSwing()
		{
			if (_activeMotionType == MotionActionType.Swing)
			{
				StopActiveMotion();
			}
		}

		private IEnumerator SwingRoutine(Transform anchor, float ropeLength, float maxPullForce, float seconds, Action endSwing)
		{
			float elapsed = 0f;
			WaitForFixedUpdate wait = new WaitForFixedUpdate();
			while (anchor != null && (seconds <= 0f || elapsed < seconds))
			{
				Vector3 vector = anchor.position - base.transform.position;
				float magnitude = vector.magnitude;
				if (magnitude > 0.0001f)
				{
					Vector3 vector2 = vector / magnitude;
					float num = magnitude - ropeLength;
					if (num > _swingEpsilon)
					{
						float num2 = Mathf.Clamp01(num / Mathf.Max(ropeLength, 0.5f));
						float num3 = maxPullForce * num2;
						AddForce(vector2 * num3);
					}
					if (_swingDebug)
					{
						Debug.DrawLine(base.transform.position, anchor.position, (num > _swingEpsilon) ? Color.yellow : Color.gray, Time.fixedDeltaTime);
					}
				}
				elapsed += Time.fixedDeltaTime;
				yield return wait;
			}
			CompleteMotionAction(endSwing);
		}

		public void StartOrbitSwing(Transform anchor, Vector3 pivot, Vector3 initialForward, float upBoostVelocity, float initialForwardVelocity, float forwardTargetSpeed, float forwardAcceleration, float orbitStartDistance, float orbitRadius, float orbitTargetSpeed, float orbitAcceleration, float radialSpring, float radialDamp, float planeDamp, float maxAcceleration, float breakDistanceTolerance, bool breakAppliesReleaseVelocity, float releaseSpeed, Action onAutoRelease = null)
		{
			if (!(anchor == null))
			{
				BeginMotionAction(MotionActionType.OrbitSwing, allowLeaveGround: true);
				_orbitAnchor = anchor;
				_orbitLocalPivot = anchor.InverseTransformPoint(pivot);
				Vector3 vector = -GetGravityDirection();
				if (vector.sqrMagnitude < 0.0001f)
				{
					vector = Vector3.up;
				}
				vector.Normalize();
				Vector3 orbitInitialForward = Vector3.ProjectOnPlane(initialForward, vector);
				if (orbitInitialForward.sqrMagnitude < 0.0001f)
				{
					orbitInitialForward = Vector3.ProjectOnPlane(base.transform.forward, vector);
				}
				if (orbitInitialForward.sqrMagnitude < 0.0001f)
				{
					orbitInitialForward = base.transform.forward;
				}
				orbitInitialForward.Normalize();
				_orbitInitialForward = orbitInitialForward;
				_orbitAxis = Vector3.Cross(vector, _orbitInitialForward);
				if (_orbitAxis.sqrMagnitude < 0.0001f)
				{
					_orbitAxis = base.transform.right;
				}
				_orbitAxis.Normalize();
				Vector3 vector2 = base.transform.position - pivot;
				float magnitude = vector2.magnitude;
				_orbitRadius = ((orbitRadius > 0f) ? orbitRadius : Mathf.Max(1f, magnitude));
				_orbitStartDistance = Mathf.Max(0.25f, orbitStartDistance);
				_orbitForwardTargetSpeed = Mathf.Max(0f, forwardTargetSpeed);
				_orbitForwardAcceleration = Mathf.Max(0f, forwardAcceleration);
				_orbitTargetSpeed = Mathf.Max(0f, orbitTargetSpeed);
				_orbitAcceleration = Mathf.Max(0f, orbitAcceleration);
				_orbitRadialSpring = Mathf.Max(0f, radialSpring);
				_orbitRadialDamp = Mathf.Max(0f, radialDamp);
				_orbitPlaneDamp = Mathf.Max(0f, planeDamp);
				_orbitMaxAcceleration = Mathf.Max(0f, maxAcceleration);
				float num = Mathf.Max(0f, breakDistanceTolerance);
				_orbitBreakDistance = ((num > 0f) ? (magnitude + num) : 0f);
				_orbitBreakAppliesReleaseVelocity = breakAppliesReleaseVelocity;
				_orbitReleaseSpeed = Mathf.Max(0f, releaseSpeed);
				_orbitAutoReleaseCallback = onAutoRelease;
				Vector3 rhs = ((vector2.sqrMagnitude > 0.0001f) ? vector2.normalized : (-_orbitInitialForward));
				Vector3 lhs = Vector3.Cross(_orbitAxis, rhs);
				if (lhs.sqrMagnitude < 0.0001f)
				{
					lhs = _orbitInitialForward;
				}
				lhs.Normalize();
				_orbitDirectionSign = ((Vector3.Dot(lhs, _orbitInitialForward) >= 0f) ? 1f : (-1f));
				Vector3 launchVelocity = _orbitInitialForward * Mathf.Max(0f, initialForwardVelocity) + vector * Mathf.Max(0f, upBoostVelocity);
				if (launchVelocity.sqrMagnitude > 0.0001f)
				{
					LaunchCharacter(launchVelocity);
				}
				_activeMotionRoutine = StartCoroutine(OrbitSwingRoutine());
			}
		}

		public bool StopOrbitSwing(bool applyReleaseVelocity, float releaseSpeed)
		{
			bool num = _activeMotionType == MotionActionType.OrbitSwing;
			StopActiveMotion();
			if (!num || !applyReleaseVelocity || releaseSpeed <= 0f)
			{
				return false;
			}
			ApplyOrbitReleaseVelocity(releaseSpeed);
			return true;
		}

		private IEnumerator OrbitSwingRoutine()
		{
			WaitForFixedUpdate wait = new WaitForFixedUpdate();
			while (_orbitAnchor != null)
			{
				Vector3 orbitPivotWorld = GetOrbitPivotWorld();
				Vector3 vector = base.transform.position - orbitPivotWorld;
				float magnitude = vector.magnitude;
				if (magnitude < 0.05f)
				{
					yield return wait;
					continue;
				}
				if (_orbitBreakDistance > 0f && magnitude > _orbitBreakDistance)
				{
					bool orbitBreakAppliesReleaseVelocity = _orbitBreakAppliesReleaseVelocity;
					float orbitReleaseSpeed = _orbitReleaseSpeed;
					Action orbitAutoReleaseCallback = _orbitAutoReleaseCallback;
					CompleteMotionAction(null);
					if (orbitBreakAppliesReleaseVelocity && orbitReleaseSpeed > 0f)
					{
						ApplyOrbitReleaseVelocity(orbitReleaseSpeed);
					}
					orbitAutoReleaseCallback?.Invoke();
					yield break;
				}
				Vector3 vector2 = vector / magnitude;
				Vector3 vector3 = Vector3.Cross(_orbitAxis, vector2) * _orbitDirectionSign;
				if (vector3.sqrMagnitude < 0.0001f)
				{
					vector3 = _orbitInitialForward;
				}
				vector3.Normalize();
				float t = Mathf.InverseLerp(_orbitStartDistance * 0.75f, _orbitStartDistance, magnitude);
				Vector3 vector4 = Vector3.Slerp(_orbitInitialForward, vector3, t);
				if (vector4.sqrMagnitude < 0.0001f)
				{
					vector4 = vector3;
				}
				vector4.Normalize();
				float num = Mathf.Lerp(_orbitForwardTargetSpeed, _orbitTargetSpeed, t);
				float num2 = Mathf.Lerp(_orbitForwardAcceleration, _orbitAcceleration, t);
				Vector3 vector5 = base.velocity;
				Vector3 vector6 = Vector3.ProjectOnPlane(vector5, _orbitAxis);
				Vector3 vector7 = Vector3.Project(vector6, vector4);
				Vector3 vector8 = (vector4 * num - vector7) * num2;
				float num3 = Vector3.Dot(vector6, vector2);
				Vector3 vector9 = vector2 * num3;
				float num4 = _orbitRadius - magnitude;
				Vector3 vector10 = vector2 * (num4 * _orbitRadialSpring) - vector9 * _orbitRadialDamp;
				Vector3 vector11 = -(vector5 - vector6) * _orbitPlaneDamp;
				Vector3 force = vector8 + vector10 + vector11;
				if (_orbitMaxAcceleration > 0f)
				{
					float num5 = _orbitMaxAcceleration * _orbitMaxAcceleration;
					if (force.sqrMagnitude > num5)
					{
						force = force.normalized * _orbitMaxAcceleration;
					}
				}
				AddForce(force, ForceMode.Acceleration);
				if (_swingDebug)
				{
					Debug.DrawLine(base.transform.position, orbitPivotWorld, Color.cyan, Time.fixedDeltaTime);
					Debug.DrawRay(base.transform.position, vector4, Color.green, Time.fixedDeltaTime);
					Debug.DrawRay(base.transform.position, vector10.normalized, Color.yellow, Time.fixedDeltaTime);
				}
				yield return wait;
			}
			CompleteMotionAction(null);
		}

		private Vector3 GetOrbitPivotWorld()
		{
			if (_orbitAnchor == null)
			{
				return base.transform.position;
			}
			return _orbitAnchor.TransformPoint(_orbitLocalPivot);
		}

		private void ApplyOrbitReleaseVelocity(float releaseSpeed)
		{
			if (!(releaseSpeed <= 0f))
			{
				Vector3 forward = ((_player != null && _player.CameraTransform != null) ? _player.CameraTransform : base.transform).forward;
				if (forward.sqrMagnitude < 0.0001f)
				{
					forward = base.transform.forward;
				}
				float num = Mathf.Max(releaseSpeed, base.velocity.magnitude);
				LaunchVelocity(forward.normalized * num, overrideVertical: true, overrideLateral: true);
			}
		}

		public void StopActiveMotion()
		{
			if (_activeMotionRoutine != null)
			{
				StopCoroutine(_activeMotionRoutine);
				_activeMotionRoutine = null;
			}
			RestoreGroundConstraintIfOwned();
			ClearOrbitSwingState();
			_activeMotionType = MotionActionType.None;
			_swingAnchor = null;
		}

		public void ClearExternalMotionForEmoteExit()
		{
			StopActiveMotion();
			base.characterMovement.velocity = Vector3.zero;
			_swimEntryGlideTimer = 0f;
		}

		private void BeginMotionAction(MotionActionType motionType, bool allowLeaveGround, Transform swingAnchor = null)
		{
			StopActiveMotion();
			_activeMotionType = motionType;
			_swingAnchor = swingAnchor;
			if (!allowLeaveGround)
			{
				_activeMotionOwnsGroundConstraint = false;
				return;
			}
			_activeMotionOwnsGroundConstraint = true;
			EnableGroundConstraint(enable: false);
		}

		private void CompleteMotionAction(Action onComplete)
		{
			RestoreGroundConstraintIfOwned();
			ClearOrbitSwingState();
			_activeMotionRoutine = null;
			_activeMotionType = MotionActionType.None;
			_swingAnchor = null;
			onComplete?.Invoke();
		}

		private void ClearOrbitSwingState()
		{
			_orbitAnchor = null;
			_orbitLocalPivot = Vector3.zero;
			_orbitAxis = Vector3.zero;
			_orbitInitialForward = Vector3.zero;
			_orbitDirectionSign = 1f;
			_orbitRadius = 0f;
			_orbitStartDistance = 0f;
			_orbitForwardTargetSpeed = 0f;
			_orbitForwardAcceleration = 0f;
			_orbitTargetSpeed = 0f;
			_orbitAcceleration = 0f;
			_orbitRadialSpring = 0f;
			_orbitRadialDamp = 0f;
			_orbitPlaneDamp = 0f;
			_orbitMaxAcceleration = 0f;
			_orbitBreakDistance = 0f;
			_orbitBreakAppliesReleaseVelocity = false;
			_orbitReleaseSpeed = 0f;
			_orbitAutoReleaseCallback = null;
		}

		private void RestoreGroundConstraintIfOwned()
		{
			if (_activeMotionOwnsGroundConstraint)
			{
				_activeMotionOwnsGroundConstraint = false;
				EnableGroundConstraint(enable: true);
			}
		}

		private void PauseGroundConstraintIfNeeded(bool allowLeaveGround, float duration)
		{
			if (allowLeaveGround && !(duration <= 0f))
			{
				PauseGroundConstraint(duration);
			}
		}

		private void UpdateCameraParentRotation()
		{
			if (!(cameraParent == null))
			{
				cameraParent.transform.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
			}
		}

		private Vector3 GetLookOrigin()
		{
			if (!(cameraParent != null))
			{
				return base.transform.position;
			}
			return cameraParent.transform.position;
		}
	}
}
