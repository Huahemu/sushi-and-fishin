using UnityEngine;
using UnityEngine.InputSystem;

namespace StarterAssets
{
	[RequireComponent(typeof(CharacterController))]
	[RequireComponent(typeof(PlayerInput))]
	public class ThirdPersonController : MonoBehaviour
	{
		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		public float MoveSpeed = 2f;

		[Tooltip("Sprint speed of the character in m/s")]
		public float SprintSpeed = 5.335f;

		[Tooltip("How fast the character turns to face movement direction")]
		[Range(0f, 0.3f)]
		public float RotationSmoothTime = 0.12f;

		[Tooltip("Acceleration and deceleration")]
		public float SpeedChangeRate = 10f;

		public AudioClip LandingAudioClip;

		public AudioClip[] FootstepAudioClips;

		[Range(0f, 1f)]
		public float FootstepAudioVolume = 0.5f;

		[Space(10f)]
		[Tooltip("The height the player can jump")]
		public float JumpHeight = 1.2f;

		[Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
		public float Gravity = -15f;

		[Space(10f)]
		[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
		public float JumpTimeout = 0.5f;

		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		public float FallTimeout = 0.15f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		public bool Grounded = true;

		[Tooltip("Useful for rough ground")]
		public float GroundedOffset = -0.14f;

		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		public float GroundedRadius = 0.28f;

		[Tooltip("What layers the character uses as ground")]
		public LayerMask GroundLayers;

		[Header("Cinemachine")]
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		public GameObject CinemachineCameraTarget;

		[Tooltip("How far in degrees can you move the camera up")]
		public float TopClamp = 70f;

		[Tooltip("How far in degrees can you move the camera down")]
		public float BottomClamp = -30f;

		[Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
		public float CameraAngleOverride;

		[Tooltip("For locking the camera position on all axis")]
		public bool LockCameraPosition;

		[Header("iStep Demo Extension")]
		[SerializeField]
		[Tooltip("Please check out the Awake function in this script to inspect what layer collision-pairs get disabled at runtime when this variable is set to true.")]
		protected bool m_applyCustomRuntimeGlobalPhysicsIgnoreCollisionSettings = true;

		[SerializeField]
		[Tooltip("Active / Deactivate iSteps algorithmic 'grounded behaviour' improvements")]
		protected bool m_useIStepGroundedImprovements = true;

		[SerializeField]
		[Range(0.01f, 1f)]
		[Tooltip("The radius for the spherecast used for abyss detection is the result of the multiplication of this value with the Grounded Radius")]
		protected float m_checkAbyssColliderRadiusMultiplier = 0.05f;

		protected Vector3 m_slopeVelocity = Vector3.zero;

		protected Vector3 m_prevAppliedSlopeVelocity = Vector3.zero;

		protected Vector3 m_currVelDir;

		protected Vector3 m_groundedNormal;

		private float _cinemachineTargetYaw;

		private float _cinemachineTargetPitch;

		private float _speed;

		private float _animationBlend;

		private float _targetRotation;

		private float _rotationVelocity;

		private float _verticalVelocity;

		private float _terminalVelocity = -53f;

		private float _jumpTimeoutDelta;

		private float _fallTimeoutDelta;

		private int _animIDSpeed;

		private int _animIDGrounded;

		private int _animIDJump;

		private int _animIDFreeFall;

		private int _animIDMotionSpeed;

		private PlayerInput _playerInput;

		private Animator _animator;

		private CharacterController _controller;

		private StarterAssetsInputs _input;

		private GameObject _mainCamera;

		private const float _threshold = 0.01f;

		private bool _hasAnimator;

		public bool useIstepGroundedImprovements
		{
			get
			{
				return m_useIStepGroundedImprovements;
			}
			set
			{
				m_useIStepGroundedImprovements = value;
			}
		}

		public Vector3 groundedNormal => m_groundedNormal;

		private bool IsCurrentDeviceMouse => _playerInput.currentControlScheme == "KeyboardMouse";

		private void Awake()
		{
			if (m_applyCustomRuntimeGlobalPhysicsIgnoreCollisionSettings)
			{
				Physics.IgnoreLayerCollision(0, 5);
				Physics.IgnoreLayerCollision(1, 5);
				Physics.IgnoreLayerCollision(2, 5);
				Physics.IgnoreLayerCollision(4, 5);
				Physics.IgnoreLayerCollision(5, 5);
			}
			if (_mainCamera == null)
			{
				_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			}
		}

		private void OnEnable()
		{
			_cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
			_cinemachineTargetPitch = CinemachineCameraTarget.transform.rotation.eulerAngles.x;
			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;
		}

		private void Start()
		{
			_cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
			_cinemachineTargetPitch = CinemachineCameraTarget.transform.rotation.eulerAngles.x;
			_animator = GetComponentInChildren<Animator>();
			_hasAnimator = _animator != null;
			_controller = GetComponent<CharacterController>();
			_input = GetComponent<StarterAssetsInputs>();
			_playerInput = GetComponent<PlayerInput>();
			AssignAnimationIDs();
			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;
		}

		private void Update()
		{
			JumpAndGravity();
			if (m_useIStepGroundedImprovements)
			{
				improvedGroundedCheckByiStep();
			}
			else
			{
				GroundedCheck();
			}
			Move();
		}

		private void LateUpdate()
		{
			CameraRotation();
		}

		private void AssignAnimationIDs()
		{
			_animIDSpeed = Animator.StringToHash("Speed");
			_animIDGrounded = Animator.StringToHash("Grounded");
			_animIDJump = Animator.StringToHash("Jump");
			_animIDFreeFall = Animator.StringToHash("FreeFall");
			_animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
		}

		private void improvedGroundedCheckByiStep()
		{
			Vector3 velocity = _controller.velocity;
			velocity.y = 0f;
			if (velocity.magnitude > 0.01f)
			{
				m_currVelDir = velocity.normalized;
			}
			m_groundedNormal = Vector3.up;
			if (_verticalVelocity > 0f)
			{
				Grounded = false;
				m_slopeVelocity = Vector3.Lerp(m_slopeVelocity, Vector3.zero, Time.deltaTime * 20f);
				if (_hasAnimator)
				{
					_animator.SetBool(_animIDGrounded, Grounded);
				}
				return;
			}
			GroundedRadius = _controller.radius * Mathf.Max(base.transform.localScale.x, base.transform.localScale.z);
			Vector3 vector = Vector3.zero;
			float num = Mathf.Abs(GroundedOffset);
			Vector3 origin = base.transform.position + base.transform.up * (GroundedRadius + num);
			float num2 = num * 2f;
			if (Physics.SphereCast(origin, GroundedRadius, -base.transform.up, out var hitInfo, num2, GroundLayers, QueryTriggerInteraction.Ignore))
			{
				Vector3 center = _controller.center;
				center.x *= base.transform.localScale.x;
				center.y *= base.transform.localScale.y;
				center.z *= base.transform.localScale.z;
				float num3 = _controller.height * base.transform.localScale.y;
				Vector3 point = base.transform.position + center + base.transform.up * (num3 * 0.5f - GroundedRadius);
				Vector3 point2 = base.transform.position + center - base.transform.up * (num3 * 0.5f - GroundedRadius);
				float num4 = GroundedRadius * 0.99f;
				float num5 = GroundedRadius - num4;
				Vector3 vector2 = base.transform.position - hitInfo.point;
				Vector3 normalized = vector2.normalized;
				float num6 = Mathf.Max(Vector3.ProjectOnPlane(vector2, base.transform.up).magnitude, 0.01f);
				if (Physics.CapsuleCast(point, point2, num4, normalized, ((GroundedRadius - num6) / num6 * vector2 + normalized * (num5 + num2)).magnitude, GroundLayers, QueryTriggerInteraction.Ignore))
				{
					Grounded = true;
				}
				else
				{
					m_groundedNormal = hitInfo.normal;
					RaycastHit hitInfo2;
					if (Vector3.Angle(hitInfo.normal, base.transform.up) > _controller.slopeLimit)
					{
						if (Physics.Raycast(base.transform.position + base.transform.up * (num + _controller.stepOffset) + m_currVelDir * GroundedRadius, -base.transform.up, out hitInfo2, num2 + _controller.stepOffset * 2f, GroundLayers, QueryTriggerInteraction.Ignore))
						{
							if (Vector3.Angle(hitInfo2.normal, base.transform.up) > _controller.slopeLimit)
							{
								Grounded = false;
								Vector3 normalized2 = Vector3.Cross(hitInfo.normal, base.transform.up).normalized;
								vector = Vector3.Cross(hitInfo.normal, normalized2).normalized;
							}
							else if (m_checkAbyssColliderRadiusMultiplier > 0.999f)
							{
								Grounded = true;
							}
							else
							{
								float num7 = m_checkAbyssColliderRadiusMultiplier * GroundedRadius;
								if (Physics.SphereCast(base.transform.position + base.transform.up * (num + num7), num7, -base.transform.up, out hitInfo2, num2 + _controller.stepOffset, GroundLayers, QueryTriggerInteraction.Ignore))
								{
									Grounded = true;
								}
								else
								{
									Grounded = false;
									Vector3 normalized3 = Vector3.Cross(hitInfo.normal, base.transform.up).normalized;
									vector = Vector3.Cross(hitInfo.normal, normalized3).normalized;
								}
							}
						}
						else
						{
							Grounded = false;
							Vector3 normalized4 = Vector3.Cross(hitInfo.normal, base.transform.up).normalized;
							vector = Vector3.Cross(hitInfo.normal, normalized4).normalized;
						}
					}
					else if (m_checkAbyssColliderRadiusMultiplier > 0.999f)
					{
						Grounded = true;
					}
					else
					{
						float num8 = m_checkAbyssColliderRadiusMultiplier * GroundedRadius;
						if (Physics.SphereCast(base.transform.position + base.transform.up * (num + num8), num8, -base.transform.up, out hitInfo2, num2 + _controller.stepOffset, GroundLayers, QueryTriggerInteraction.Ignore))
						{
							Grounded = true;
						}
						else
						{
							Grounded = false;
							Vector3 normalized5 = Vector3.Cross(hitInfo.normal, base.transform.up).normalized;
							vector = Vector3.Cross(hitInfo.normal, normalized5).normalized;
						}
					}
				}
			}
			else
			{
				Grounded = false;
			}
			if (vector.magnitude > 0.001f)
			{
				m_slopeVelocity += vector * Time.deltaTime * Mathf.Abs(Gravity);
			}
			else
			{
				m_slopeVelocity = Vector3.Lerp(m_slopeVelocity, Vector3.zero, Time.deltaTime * 20f);
			}
			if (_hasAnimator)
			{
				_animator.SetBool(_animIDGrounded, Grounded);
			}
		}

		private void GroundedCheck()
		{
			Vector3 position = new Vector3(base.transform.position.x, base.transform.position.y - GroundedOffset, base.transform.position.z);
			Grounded = Physics.CheckSphere(position, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
			if (_hasAnimator)
			{
				_animator.SetBool(_animIDGrounded, Grounded);
			}
		}

		private void CameraRotation()
		{
			if (_input.look.sqrMagnitude >= 0.01f && !LockCameraPosition)
			{
				float num = (IsCurrentDeviceMouse ? 1f : Time.deltaTime);
				_cinemachineTargetYaw += _input.look.x * num;
				_cinemachineTargetPitch += _input.look.y * num;
			}
			_cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
			_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);
			CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride, _cinemachineTargetYaw, 0f);
		}

		private void Move()
		{
			float num = (_input.sprint ? SprintSpeed : MoveSpeed);
			if (_input.move == Vector2.zero)
			{
				num = 0f;
			}
			float magnitude = new Vector3(_controller.velocity.x - m_prevAppliedSlopeVelocity.x, 0f, _controller.velocity.z - m_prevAppliedSlopeVelocity.z).magnitude;
			float num2 = 0.1f;
			float num3 = (_input.analogMovement ? _input.move.magnitude : 1f);
			if (magnitude < num - num2 || magnitude > num + num2)
			{
				_speed = Mathf.Lerp(magnitude, num * num3, Time.deltaTime * SpeedChangeRate);
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else
			{
				_speed = num;
			}
			_animationBlend = Mathf.Lerp(_animationBlend, num, Time.deltaTime * SpeedChangeRate);
			if (_animationBlend < 0.01f)
			{
				_animationBlend = 0f;
			}
			Vector3 normalized = new Vector3(_input.move.x, 0f, _input.move.y).normalized;
			if (_input.move != Vector2.zero)
			{
				_targetRotation = Mathf.Atan2(normalized.x, normalized.z) * 57.29578f + _mainCamera.transform.eulerAngles.y;
				float y = Mathf.SmoothDampAngle(base.transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, RotationSmoothTime);
				base.transform.rotation = Quaternion.Euler(0f, y, 0f);
			}
			Vector3 targetDirection = Quaternion.Euler(0f, _targetRotation, 0f) * Vector3.forward;
			if (m_useIStepGroundedImprovements)
			{
				Vector3 vector = calculateSlopeVelocityToUseByiStep(targetDirection, _speed);
				_controller.Move(targetDirection.normalized * _speed * Time.deltaTime + new Vector3(0f, _verticalVelocity, 0f) * Time.deltaTime + vector * Time.deltaTime);
				m_prevAppliedSlopeVelocity = vector;
			}
			else
			{
				_controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0f, _verticalVelocity, 0f) * Time.deltaTime);
			}
			if (_hasAnimator)
			{
				_animator.SetFloat(_animIDSpeed, _animationBlend);
				_animator.SetFloat(_animIDMotionSpeed, num3);
			}
		}

		private Vector3 calculateSlopeVelocityToUseByiStep(Vector3 targetDirection, float movingVel)
		{
			if (m_slopeVelocity.magnitude > 0.001f && movingVel > 0.001f)
			{
				Vector3 vector = Vector3.Project(m_slopeVelocity, base.transform.up);
				Vector3 vector2 = m_slopeVelocity - vector;
				Vector3 vector3 = Vector3.Project(targetDirection.normalized * movingVel, vector2.normalized);
				Vector3 vector4 = vector2 - vector3;
				if (Vector3.Dot(vector4.normalized, vector2.normalized) > 0f)
				{
					return vector4 + vector;
				}
				return Vector3.zero;
			}
			return m_slopeVelocity;
		}

		private void JumpAndGravity()
		{
			if (Grounded)
			{
				_fallTimeoutDelta = FallTimeout;
				if (_hasAnimator)
				{
					_animator.SetBool(_animIDJump, value: false);
					_animator.SetBool(_animIDFreeFall, value: false);
				}
				if (_verticalVelocity < 0f)
				{
					if (m_useIStepGroundedImprovements)
					{
						_verticalVelocity = Mathf.Lerp(Gravity, -2f, Vector3.Dot(Vector3.up, m_groundedNormal));
					}
					else
					{
						_verticalVelocity = -2f;
					}
				}
				if (_input.jump && _jumpTimeoutDelta <= 0f)
				{
					_jumpTimeoutDelta = JumpTimeout;
					_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
					if (_hasAnimator)
					{
						_animator.SetBool(_animIDJump, value: true);
					}
				}
				if (_jumpTimeoutDelta >= 0f)
				{
					_jumpTimeoutDelta -= Time.deltaTime;
				}
			}
			else
			{
				if (_fallTimeoutDelta >= 0f)
				{
					_fallTimeoutDelta -= Time.deltaTime;
				}
				else if (_hasAnimator)
				{
					_animator.SetBool(_animIDFreeFall, value: true);
				}
				_input.jump = false;
			}
			if (_verticalVelocity > _terminalVelocity)
			{
				_verticalVelocity += Gravity * Time.deltaTime;
			}
			if (_verticalVelocity > 0f)
			{
				Vector3 center = _controller.center;
				center.x *= base.transform.localScale.x;
				center.y *= base.transform.localScale.y;
				center.z *= base.transform.localScale.z;
				float num = _controller.height * base.transform.localScale.y;
				float num2 = _controller.radius * Mathf.Max(base.transform.localScale.x, base.transform.localScale.z);
				Vector3 origin = base.transform.position + center + base.transform.up * (num * 0.5f - num2);
				float num3 = Mathf.Abs(GroundedOffset);
				float num4 = num2 * 0.99f;
				float maxDistance = num2 - num4 + num3;
				if (Physics.SphereCast(origin, num4, base.transform.up, out var _, maxDistance, GroundLayers, QueryTriggerInteraction.Ignore))
				{
					_jumpTimeoutDelta = 0f;
					_verticalVelocity = Mathf.Lerp(_verticalVelocity, 0f, Time.deltaTime * 10f);
				}
			}
		}

		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f)
			{
				lfAngle += 360f;
			}
			if (lfAngle > 360f)
			{
				lfAngle -= 360f;
			}
			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}

		private void OnDrawGizmosSelected()
		{
			if (!m_useIStepGroundedImprovements)
			{
				Color color = new Color(0f, 1f, 0f, 0.35f);
				Color color2 = new Color(1f, 0f, 0f, 0.35f);
				if (Grounded)
				{
					Gizmos.color = color;
				}
				else
				{
					Gizmos.color = color2;
				}
				Gizmos.DrawSphere(new Vector3(base.transform.position.x, base.transform.position.y - GroundedOffset, base.transform.position.z), GroundedRadius);
			}
		}

		private void OnFootstep(AnimationEvent animationEvent)
		{
		}

		private void OnLand(AnimationEvent animationEvent)
		{
		}
	}
}
