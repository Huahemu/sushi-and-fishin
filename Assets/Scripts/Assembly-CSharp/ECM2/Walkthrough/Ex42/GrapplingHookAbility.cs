using UnityEngine;

namespace ECM2.Walkthrough.Ex42
{
	[RequireComponent(typeof(Character))]
	public class GrapplingHookAbility : MonoBehaviour
	{
		[Header("General")]
		public bool canEverGrapple = true;

		public Transform cameraTransform;

		public LayerMask grappleMask = -1;

		public float maxGrappleDistance = 35f;

		public float ropeLength = 12f;

		public float ropeMinLength = 2f;

		public float ropeMaxLength = 40f;

		[Header("Feel")]
		public float initialYankSpeed = 12f;

		public float releaseBoost = 4f;

		public float springK = 40f;

		public float radialDamping = 8f;

		public float reelInSpeed = 6f;

		public float reelOutSpeed = 8f;

		[Header("Optional Built-in Input (for quick testing)")]
		public bool useBuiltInKeys = true;

		public KeyCode keyFireGrapple = KeyCode.Mouse1;

		public KeyCode keyRelease = KeyCode.Space;

		public KeyCode keyReelIn = KeyCode.E;

		public KeyCode keyReelOut = KeyCode.Q;

		private Character _character;

		private bool _grappling;

		private Vector3 _anchorPoint;

		private Character.RotationMode _prevRotMode;

		private bool _prevUseRootMotion;

		private bool _prevConstrainToGround;

		private void Awake()
		{
			_character = GetComponent<Character>();
			if (cameraTransform == null && Camera.main != null)
			{
				cameraTransform = Camera.main.transform;
			}
		}

		private void OnEnable()
		{
			_character.BeforeSimulationUpdated += OnBeforeSimulationUpdated;
			_character.Collided += OnCollided;
		}

		private void OnDisable()
		{
			_character.BeforeSimulationUpdated -= OnBeforeSimulationUpdated;
			_character.Collided -= OnCollided;
			if (_grappling)
			{
				StopGrapple(addReleaseBoost: false);
			}
		}

		private void Update()
		{
			if (useBuiltInKeys)
			{
				if (Input.GetKeyDown(keyFireGrapple))
				{
					TryStartGrappleFromView();
				}
				if (_grappling && Input.GetKeyDown(keyRelease))
				{
					StopGrapple(addReleaseBoost: true);
				}
			}
		}

		public bool TryStartGrappleFromView()
		{
			if (!IsGrappleAllowed())
			{
				return false;
			}
			if (cameraTransform == null)
			{
				return false;
			}
			if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out var hitInfo, maxGrappleDistance, grappleMask, QueryTriggerInteraction.Ignore))
			{
				float initialRopeLength = Mathf.Clamp(Vector3.Distance(_character.GetPosition(), hitInfo.point), ropeMinLength, ropeMaxLength);
				StartGrapple(hitInfo.point, initialRopeLength);
				return true;
			}
			return false;
		}

		public void StartGrapple(Vector3 anchorPoint, float initialRopeLength)
		{
			if (IsGrappleAllowed())
			{
				_grappling = true;
				_anchorPoint = anchorPoint;
				ropeLength = Mathf.Clamp(initialRopeLength, ropeMinLength, ropeMaxLength);
				_prevRotMode = _character.GetRotationMode();
				_prevUseRootMotion = _character.useRootMotion;
				_prevConstrainToGround = _character.characterMovement.constrainToGround;
				_character.useRootMotion = false;
				_character.characterMovement.constrainToGround = false;
				_character.SetMovementMode(Character.MovementMode.Falling);
				Vector3 vector = _anchorPoint - _character.GetPosition();
				if (vector.sqrMagnitude > 0.0001f)
				{
					Vector3 normalized = vector.normalized;
					Vector3 obj = (cameraTransform ? cameraTransform.forward : base.transform.forward);
					Vector3 normalized2 = (obj - Vector3.Project(obj, normalized)).normalized;
					Vector3 velocity = _character.GetVelocity();
					Vector3 vector2 = normalized2 * initialYankSpeed;
					_character.LaunchCharacter(velocity + vector2, overrideVerticalVelocity: false, overrideLateralVelocity: true);
				}
			}
		}

		public void StopGrapple(bool addReleaseBoost)
		{
			if (!_grappling)
			{
				return;
			}
			if (addReleaseBoost)
			{
				Vector3 vector = _character.GetPosition() - _anchorPoint;
				if (vector.sqrMagnitude > 0.0001f)
				{
					Vector3 normalized = vector.normalized;
					Vector3 velocity = _character.GetVelocity();
					Vector3 vector2 = velocity - Vector3.Project(velocity, normalized);
					if (vector2.sqrMagnitude > 0.0001f)
					{
						Vector3 launchVelocity = vector2.normalized * (vector2.magnitude + releaseBoost);
						_character.LaunchCharacter(launchVelocity, overrideVerticalVelocity: false, overrideLateralVelocity: true);
					}
				}
			}
			_grappling = false;
			_character.SetRotationMode(_prevRotMode);
			_character.useRootMotion = _prevUseRootMotion;
			_character.characterMovement.constrainToGround = _prevConstrainToGround;
			_character.SetMovementMode(Character.MovementMode.Falling);
		}

		private void OnBeforeSimulationUpdated(float deltaTime)
		{
			if (!_grappling)
			{
				return;
			}
			Vector3 position = _character.GetPosition();
			Vector3 velocity = _character.GetVelocity();
			Vector3 vector = position - _anchorPoint;
			float magnitude = vector.magnitude;
			if (magnitude < 0.0001f)
			{
				return;
			}
			Vector3 vector2 = vector / magnitude;
			if (useBuiltInKeys)
			{
				if (Input.GetKey(keyReelIn))
				{
					ropeLength -= reelInSpeed * deltaTime;
				}
				if (Input.GetKey(keyReelOut))
				{
					ropeLength += reelOutSpeed * deltaTime;
				}
			}
			ropeLength = Mathf.Clamp(ropeLength, ropeMinLength, ropeMaxLength);
			bool num = magnitude >= ropeLength - 0.01f;
			Vector3 vector3 = Vector3.Project(velocity, vector2);
			Vector3 vector4 = velocity - vector3;
			if (num)
			{
				if (Vector3.Dot(vector3, vector2) > 0f)
				{
					vector3 = Vector3.zero;
				}
				float num2 = magnitude - ropeLength;
				if (num2 > 0f)
				{
					Vector3 vector5 = -vector2 * (springK * num2);
					Vector3 vector6 = -Vector3.Project(velocity, vector2) * radialDamping;
					_character.AddForce(vector5 + vector6, ForceMode.Acceleration);
				}
				Vector3 launchVelocity = vector4 + vector3;
				_character.LaunchCharacter(launchVelocity, overrideVerticalVelocity: true, overrideLateralVelocity: true);
			}
			_character.SetRotationMode(Character.RotationMode.OrientRotationToMovement);
		}

		private void OnCollided(ref CollisionResult collisionResult)
		{
		}

		private bool IsGrappleAllowed()
		{
			if (!canEverGrapple)
			{
				return false;
			}
			if (!_character.IsWalking())
			{
				return _character.IsFalling();
			}
			return true;
		}
	}
}
