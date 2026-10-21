using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using StylizedWater3;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Interaction
{
	[RequireComponent(typeof(NetworkObject))]
	[RequireComponent(typeof(BeachBallNetworkTransform))]
	[RequireComponent(typeof(Rigidbody))]
	public sealed class BeachBall : BaseHoldable
	{
		internal enum MotionState : byte
		{
			Held = 0,
			FreePhysics = 1,
			Floating = 2
		}

		private enum SfxKind : byte
		{
			Pickup = 0,
			PlayerHit = 1,
			SurfaceBounce = 2,
			WaterImpact = 3,
			UmbrellaBoost = 4
		}

		[Header("Beach Ball References")]
		[SerializeField]
		private Rigidbody _rigidbody;

		[SerializeField]
		private Collider _physicsCollider;

		[SerializeField]
		private SphereCollider _floatingInteractionCollider;

		[SerializeField]
		private AlignToWater _alignToWater;

		[SerializeField]
		private BeachBallNetworkTransform _networkTransform;

		[Header("Beach Ball Throw")]
		[SerializeField]
		[Min(0.01f)]
		private float _throwForceMultiplier = 0.25f;

		[SerializeField]
		[Min(0.01f)]
		private float _mass = 0.25f;

		[SerializeField]
		[Min(0f)]
		private float _linearDamping = 0.18f;

		[SerializeField]
		[Min(0f)]
		private float _angularDamping = 0.25f;

		[SerializeField]
		[Min(0.01f)]
		private float _maximumAngularSpeed = 8f;

		[SerializeField]
		private LayerMask _physicsExcludeLayers;

		[SerializeField]
		[Min(0f)]
		private float _throwerCollisionIgnoreDuration = 0.18f;

		[Header("Beach Ball Owner Prediction")]
		[SerializeField]
		[Min(0.01f)]
		private float _predictionReconcileDuration = 0.1f;

		[SerializeField]
		[Min(0.01f)]
		private float _predictionHardSnapDistance = 1.5f;

		[Header("Beach Ball Ground Roll")]
		[SerializeField]
		[Range(0f, 1f)]
		private float _groundRollSpeedMultiplier = 0.65f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _groundRollInfluence = 0.7f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _minimumGroundNormalUpDot = 0.5f;

		[Header("Beach Ball Player Hit")]
		[SerializeField]
		[Min(0f)]
		private float _baseHitSpeed = 1.5f;

		[SerializeField]
		[Min(0f)]
		private float _playerVelocityContribution = 3f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _playerTangentInfluence = 0.35f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _ballTangentRetention = 0.8f;

		[SerializeField]
		[Min(0f)]
		private float _upwardLift = 0.45f;

		[SerializeField]
		[Min(0f)]
		private float _minimumClosingSpeed = 0.3f;

		[SerializeField]
		[Min(0.01f)]
		private float _maximumBallSpeed = 9f;

		[SerializeField]
		[Min(0f)]
		private float _perPlayerHitCooldown = 0.12f;

		[Header("Beach Ball Surface")]
		[SerializeField]
		[Min(0f)]
		private float _surfaceBounceMinSpeed = 1.2f;

		[SerializeField]
		[Min(0f)]
		private float _surfaceBounceCooldown = 0.1f;

		[SerializeField]
		private LayerMask _surfaceBounceExcludeLayers;

		[Header("Beach Ball Umbrella Boost")]
		[SerializeField]
		[Min(0.01f)]
		private float _umbrellaBoostVelocity = 12f;

		[SerializeField]
		[Range(0f, 1f)]
		private float _umbrellaPlanarRetention = 0.8f;

		[SerializeField]
		[Min(0.01f)]
		private float _umbrellaMaximumSpeed = 14f;

		[Header("Beach Ball Water")]
		[SerializeField]
		[Min(0f)]
		private float _waterBigSplashSpeed = 4f;

		[SerializeField]
		[Min(0f)]
		private float _waterEntryDepth = 0.12f;

		[SerializeField]
		[Min(0f)]
		private float _waterReentryGraceDuration = 0.45f;

		[Header("Beach Ball Reset")]
		[SerializeField]
		private float _outOfBoundsHeight = -20f;

		[SerializeField]
		[Min(1f)]
		private float _maximumDistanceFromSpawn = 80f;

		[SerializeField]
		[Min(1f)]
		private float _maximumHeightAboveSpawn = 30f;

		[SerializeField]
		[Min(1f)]
		private float _runawaySpeed = 18f;

		[SerializeField]
		[Min(0f)]
		private float _runawaySpeedDuration = 0.35f;

		[Header("Beach Ball SFX")]
		[SerializeField]
		private AudioClipSO _pickupSfx;

		[SerializeField]
		private AudioClipSO _playerHitSfx;

		[SerializeField]
		private AudioClipSO _surfaceBounceSfx;

		[SerializeField]
		private AudioClipSO _waterImpactSfx;

		[SerializeField]
		private AudioClipSO _umbrellaBoostSfx;

		private readonly NetworkVariable<MotionState> _motionState = new NetworkVariable<MotionState>(MotionState.FreePhysics);

		private readonly Dictionary<ulong, float> _nextPlayerHitTimes = new Dictionary<ulong, float>();

		private readonly List<Collider> _ignoredThrowerColliders = new List<Collider>();

		private readonly Collider[] _waterOverlapProbeHits = new Collider[8];

		private Vector3 _serverResetPosition;

		private Quaternion _serverResetRotation;

		private float _nextSurfaceBounceTime;

		private float _ignoreWaterUntilTime;

		private float _nextWaterOverlapProbeTime;

		private float _runawaySpeedElapsed;

		private float _restoreThrowerCollisionsTime;

		private ulong _serverHoldingClientId;

		private RestaurantManager _restaurantManager;

		private Transform _waterPresentationTransform;

		private Vector3 _waterPresentationLocalPosition;

		private Quaternion _waterPresentationLocalRotation;

		private bool _awaitingReplicatedFreePhysicsState;

		private GameObject _predictionObject;

		private Rigidbody _predictionRigidbody;

		private SphereCollider _predictionCollider;

		private Vector3 _predictionReconcilePositionOffset;

		private Quaternion _predictionReconcileRotationOffset;

		private Vector3 _pendingAuthoritativeTeleportPosition;

		private Quaternion _pendingAuthoritativeTeleportRotation;

		private float _predictionReconcileElapsed;

		private bool _localThrowPredictionActive;

		private bool _predictionReconciling;

		private bool _replicatedThrowAccepted;

		private bool _hasPendingAuthoritativeTeleport;

		private const float WaterOverlapProbeInterval = 0.1f;

		private const string PredictionLayerName = "Ignore Raycast";

		public override bool RequiresHeldSlot => true;

		public override bool CanPersistInQuickSlotSave => false;

		protected override bool UsesServerAuthoritativePersistentThrow => true;

		public override void EnableGazeHints()
		{
			Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_hold");
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("Interact");
			Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
		}

		public override void EnableHeldHints()
		{
			AddHeldThrowHint();
		}

		public override void DisableHeldHints()
		{
			base.DisableHeldHints();
		}

		protected override void Awake()
		{
			base.Awake();
			if (_rigidbody == null)
			{
				_rigidbody = GetComponent<Rigidbody>();
			}
			if (_alignToWater == null)
			{
				_alignToWater = GetComponentInChildren<AlignToWater>(includeInactive: true);
			}
			if (_floatingInteractionCollider == null && _alignToWater != null)
			{
				_floatingInteractionCollider = _alignToWater.GetComponent<SphereCollider>();
			}
			if (_networkTransform == null)
			{
				_networkTransform = GetComponent<BeachBallNetworkTransform>();
			}
			if (_alignToWater != null)
			{
				_waterPresentationTransform = _alignToWater.transform;
				_waterPresentationLocalPosition = _waterPresentationTransform.localPosition;
				_waterPresentationLocalRotation = _waterPresentationTransform.localRotation;
				if (Application.isPlaying)
				{
					_alignToWater.enabled = false;
				}
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<MotionState> motionState = _motionState;
			motionState.OnValueChanged = (NetworkVariable<MotionState>.OnValueChangedDelegate)Delegate.Combine(motionState.OnValueChanged, new NetworkVariable<MotionState>.OnValueChangedDelegate(OnMotionStateChanged));
			if (_networkTransform != null)
			{
				_networkTransform.AuthoritativeTeleportReceived += OnAuthoritativeTeleportReceived;
			}
			if (base.IsServer)
			{
				_serverResetPosition = base.transform.position;
				_serverResetRotation = base.transform.rotation;
				TryRegisterWithRestaurantManager();
			}
			ApplyLocalMotionState(_motionState.Value);
		}

		public override void OnNetworkDespawn()
		{
			NetworkVariable<MotionState> motionState = _motionState;
			motionState.OnValueChanged = (NetworkVariable<MotionState>.OnValueChangedDelegate)Delegate.Remove(motionState.OnValueChanged, new NetworkVariable<MotionState>.OnValueChangedDelegate(OnMotionStateChanged));
			if (_networkTransform != null)
			{
				_networkTransform.AuthoritativeTeleportReceived -= OnAuthoritativeTeleportReceived;
			}
			if (base.IsServer && _restaurantManager != null)
			{
				_restaurantManager.UnregisterBeachBall(this);
			}
			_restaurantManager = null;
			_awaitingReplicatedFreePhysicsState = false;
			CancelLocalThrowPrediction(showAuthoritativePresentation: false);
			DestroyPredictionObject();
			RestoreThrowerCollisions();
			if (_floatingInteractionCollider != null)
			{
				_floatingInteractionCollider.enabled = false;
			}
			if (_alignToWater != null)
			{
				_alignToWater.enabled = false;
				RestoreWaterPresentationTransform();
			}
			_nextPlayerHitTimes.Clear();
			base.OnNetworkDespawn();
		}

		private void FixedUpdate()
		{
			if (!base.IsServer || !base.IsSpawned)
			{
				return;
			}
			TryRestoreThrowerCollisions();
			if (_restaurantManager == null)
			{
				TryRegisterWithRestaurantManager();
			}
			if (_motionState.Value == MotionState.Held)
			{
				return;
			}
			if (_motionState.Value == MotionState.FreePhysics)
			{
				TryEnterWaterFromOverlapProbe();
				if (_motionState.Value == MotionState.FreePhysics)
				{
					ClampServerAngularVelocity();
				}
			}
			if (ShouldResetRunawayBall())
			{
				ServerResetToInitialSpawn("Runaway guard");
			}
		}

		private void LateUpdate()
		{
			if (_predictionReconciling && !(_predictionObject == null))
			{
				float num = Mathf.Max(0.01f, _predictionReconcileDuration);
				_predictionReconcileElapsed += Time.deltaTime;
				float num2 = Mathf.Clamp01(_predictionReconcileElapsed / num);
				float t = Mathf.SmoothStep(0f, 1f, num2);
				_predictionObject.transform.SetPositionAndRotation(base.transform.position + Vector3.Lerp(_predictionReconcilePositionOffset, Vector3.zero, t), Quaternion.Slerp(_predictionReconcileRotationOffset, Quaternion.identity, t) * base.transform.rotation);
				if (num2 >= 1f)
				{
					CompleteLocalThrowPredictionHandoff();
				}
			}
		}

		public override void ServerHold(Interactor interactor)
		{
			if (base.IsServer && !(interactor == null))
			{
				RestoreThrowerCollisions();
				_serverHoldingClientId = interactor.OwnerClientId;
				SetServerMotionState(MotionState.Held);
				_nextPlayerHitTimes.Clear();
				ClientsPlaySfxRpc(SfxKind.Pickup, base.transform.position, 1f);
				base.ServerHold(interactor);
			}
		}

		public override void RequestThrow(Thrower.ThrowPhysicsRequestData throwData)
		{
			if (!_localThrowPredictionActive && !_predictionReconciling)
			{
				float num = Mathf.Max(0.01f, _throwForceMultiplier);
				throwData.Force *= num;
				throwData.UpwardForce *= num;
				BeginLocalThrowPrediction(throwData);
				base.RequestThrow(throwData);
			}
		}

		protected override void ServerOnPersistentPhysicsThrowStarted(PhysicsThrowData data)
		{
			if (base.IsServer && !(_rigidbody == null))
			{
				SetServerMotionState(MotionState.FreePhysics);
				ConfigureServerFreePhysics();
				_ignoreWaterUntilTime = Time.time + Mathf.Max(0f, _waterReentryGraceDuration);
				_nextWaterOverlapProbeTime = _ignoreWaterUntilTime;
				_runawaySpeedElapsed = 0f;
				IgnoreThrowerCollisions(_serverHoldingClientId);
				BaseHoldable.ApplyThrowForces(_rigidbody, data.Direction, data.Force, data.UpwardForce, data.TorqueImpulse, data.ForceModeId);
			}
		}

		protected override void ServerSetPersistentPhysicsThrowPose(Vector3 startPosition, Quaternion startRotation)
		{
			if (_rigidbody != null)
			{
				_rigidbody.linearVelocity = Vector3.zero;
				_rigidbody.angularVelocity = Vector3.zero;
				_rigidbody.position = startPosition;
				_rigidbody.rotation = startRotation;
			}
			base.transform.SetPositionAndRotation(startPosition, startRotation);
			if (_networkTransform != null)
			{
				_networkTransform.Teleport(startPosition, startRotation, base.transform.localScale);
			}
			else
			{
				base.ServerSetPersistentPhysicsThrowPose(startPosition, startRotation);
			}
		}

		protected override void ClientOnPersistentPhysicsThrowStarted(bool isLocalHolder)
		{
			if (base.IsServer)
			{
				_awaitingReplicatedFreePhysicsState = false;
				ApplyLocalMotionState(MotionState.FreePhysics);
				return;
			}
			_replicatedThrowAccepted = true;
			_awaitingReplicatedFreePhysicsState = true;
			ObjectsStatus(status: false);
			ApplyLocalMotionState(MotionState.FreePhysics);
			TryBeginAuthoritativeHandoff();
		}

		protected override void ClientOnThrowRejected()
		{
			CancelLocalThrowPrediction(showAuthoritativePresentation: false);
			_awaitingReplicatedFreePhysicsState = false;
			ObjectsStatus(status: false);
			CollidersStatus(status: false);
			ApplyLocalMotionState(MotionState.Held);
			_ownerInteractor?.ClientReconcileHeldPresentation();
		}

		protected override void ClientOnHeldActionForceCleared()
		{
			CancelLocalThrowPrediction(showAuthoritativePresentation: false);
			_awaitingReplicatedFreePhysicsState = false;
			bool flag = _motionState.Value == MotionState.Held;
			ObjectsStatus(!flag);
			ApplyLocalMotionState(_motionState.Value);
		}

		protected override void ClientHoldEveryone()
		{
			CancelLocalThrowPrediction(showAuthoritativePresentation: false);
			_awaitingReplicatedFreePhysicsState = false;
			_replicatedThrowAccepted = false;
			_hasPendingAuthoritativeTeleport = false;
			if (!base.IsServer && base.IsOwner)
			{
				EnsurePredictionObject();
			}
			ApplyLocalMotionState(MotionState.Held);
		}

		protected override void ClientPlayHoldSfx()
		{
		}

		public override void ClientRefreshRuntimeRepresentationState()
		{
			MotionState state = (_awaitingReplicatedFreePhysicsState ? MotionState.FreePhysics : _motionState.Value);
			ApplyLocalMotionState(state);
			if (!base.IsServer && (_awaitingReplicatedFreePhysicsState || _localThrowPredictionActive || _predictionReconciling))
			{
				ObjectsStatus(status: false);
			}
		}

		protected override void OnCollisionEnter(Collision collision)
		{
			if (base.IsServer && _motionState.Value == MotionState.FreePhysics && collision != null)
			{
				if (collision.collider != null && BaseHoldable.IsThrowWaterCollider(collision.collider) && CanEnterWater(collision.collider))
				{
					Vector3 impactPosition = ((collision.contactCount > 0) ? collision.GetContact(0).point : base.transform.position);
					ServerEnterWater(impactPosition, collision.relativeVelocity.magnitude);
				}
				else if (!TryApplyUmbrellaBoost(collision.collider) && !TryApplyPlayerHit(collision) && !TryCrashSandCastle(collision.collider))
				{
					TryApplyGroundRoll(collision);
					TryPlaySurfaceBounce(collision);
				}
			}
		}

		protected override void OnTriggerEnter(Collider other)
		{
			if (base.IsServer && _motionState.Value == MotionState.FreePhysics && !TryApplyUmbrellaBoost(other) && !TryCrashSandCastle(other))
			{
				TryEnterWaterFromTrigger(other);
			}
		}

		private bool TryCrashSandCastle(Collider other)
		{
			SandCastle sandCastle = ((other != null) ? other.GetComponentInParent<SandCastle>() : null);
			if (sandCastle == null)
			{
				return false;
			}
			sandCastle.ServerCrashFromBeachBall();
			return true;
		}

		private bool TryApplyUmbrellaBoost(Collider other)
		{
			if (_rigidbody == null || other == null || !other.CompareTag("Boost"))
			{
				return false;
			}
			Vector3 vector = Vector3.ProjectOnPlane(_rigidbody.linearVelocity, Vector3.up) * _umbrellaPlanarRetention + Vector3.up * Mathf.Max(0.01f, _umbrellaBoostVelocity);
			_rigidbody.linearVelocity = Vector3.ClampMagnitude(vector, Mathf.Max(0.01f, _umbrellaMaximumSpeed));
			_rigidbody.WakeUp();
			Vector3 position = (other.enabled ? other.ClosestPoint(base.transform.position) : base.transform.position);
			ClientsPlaySfxRpc(SfxKind.UmbrellaBoost, position, 1f);
			return true;
		}

		private bool TryApplyPlayerHit(Collision collision)
		{
			if (_rigidbody == null || collision.collider == null)
			{
				return false;
			}
			PPG_Player componentInParent = collision.collider.GetComponentInParent<PPG_Player>();
			if (componentInParent == null)
			{
				return false;
			}
			float time = Time.time;
			if (_nextPlayerHitTimes.TryGetValue(componentInParent.OwnerClientId, out var value) && time < value)
			{
				return true;
			}
			Vector3 vector = ResolvePlayerHitNormal(collision, componentInParent);
			Vector3 serverGameplayVelocity = componentInParent.ServerGameplayVelocity;
			Vector3 linearVelocity = _rigidbody.linearVelocity;
			float num = Vector3.Dot(serverGameplayVelocity - linearVelocity, vector);
			if (num < _minimumClosingSpeed)
			{
				return true;
			}
			float num2 = _baseHitSpeed + num * _playerVelocityContribution;
			Vector3 vector2 = Vector3.ProjectOnPlane(serverGameplayVelocity, vector) * _playerTangentInfluence;
			Vector3 vector3 = Vector3.ProjectOnPlane(linearVelocity, vector) * _ballTangentRetention;
			Vector3 vector4 = vector * num2 + vector2 + vector3 + Vector3.up * _upwardLift;
			_rigidbody.linearVelocity = Vector3.ClampMagnitude(vector4, _maximumBallSpeed);
			_rigidbody.WakeUp();
			_nextPlayerHitTimes[componentInParent.OwnerClientId] = time + _perPlayerHitCooldown;
			Vector3 position = ((collision.contactCount > 0) ? collision.GetContact(0).point : base.transform.position);
			float strength = Mathf.InverseLerp(_minimumClosingSpeed, _maximumBallSpeed, num);
			ClientsPlaySfxRpc(SfxKind.PlayerHit, position, strength);
			return true;
		}

		private Vector3 ResolvePlayerHitNormal(Collision collision, PPG_Player player)
		{
			Vector3 vector = base.transform.position - player.transform.position;
			Vector3 vector2 = ((collision.contactCount > 0) ? collision.GetContact(0).normal : vector);
			if (vector2.sqrMagnitude < 0.0001f)
			{
				vector2 = Vector3.up;
			}
			vector2.Normalize();
			if (vector.sqrMagnitude > 0.0001f && Vector3.Dot(vector2, vector) < 0f)
			{
				vector2 = -vector2;
			}
			return vector2;
		}

		private void TryPlaySurfaceBounce(Collision collision)
		{
			if (!(Time.time < _nextSurfaceBounceTime) && !(collision.collider == null) && ((1 << collision.collider.gameObject.layer) & _surfaceBounceExcludeLayers.value) == 0)
			{
				float magnitude = collision.relativeVelocity.magnitude;
				if (!(magnitude < _surfaceBounceMinSpeed))
				{
					_nextSurfaceBounceTime = Time.time + _surfaceBounceCooldown;
					Vector3 position = ((collision.contactCount > 0) ? collision.GetContact(0).point : base.transform.position);
					float strength = Mathf.InverseLerp(_surfaceBounceMinSpeed, _maximumBallSpeed, magnitude);
					ClientsPlaySfxRpc(SfxKind.SurfaceBounce, position, strength);
				}
			}
		}

		private void TryApplyGroundRoll(Collision collision)
		{
			if (_rigidbody == null || collision == null || collision.contactCount == 0)
			{
				return;
			}
			Vector3 normal = collision.GetContact(0).normal;
			if (!(Vector3.Dot(normal, Vector3.up) < _minimumGroundNormalUpDot))
			{
				Vector3 vector = Vector3.ProjectOnPlane(_rigidbody.linearVelocity, normal);
				float magnitude = vector.magnitude;
				if (!(magnitude < 0.05f))
				{
					float num = ResolveWorldPhysicsRadius();
					float num2 = Mathf.Min(magnitude / num * Mathf.Clamp01(_groundRollSpeedMultiplier), Mathf.Max(0.01f, _maximumAngularSpeed));
					Vector3 vector2 = Vector3.Cross(normal, vector / magnitude) * num2;
					Vector3 vector3 = Vector3.Project(_rigidbody.angularVelocity, normal);
					_rigidbody.angularVelocity = Vector3.Lerp(_rigidbody.angularVelocity, vector2 + vector3, Mathf.Clamp01(_groundRollInfluence));
				}
			}
		}

		private float ResolveWorldPhysicsRadius()
		{
			if (!(_physicsCollider is SphereCollider sphereCollider))
			{
				return 0.15f;
			}
			Vector3 lossyScale = sphereCollider.transform.lossyScale;
			float num = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
			return Mathf.Max(0.01f, sphereCollider.radius * num);
		}

		private void ServerEnterWater(Vector3 impactPosition, float impactSpeed)
		{
			if (base.IsServer && _motionState.Value != MotionState.Floating)
			{
				RestoreThrowerCollisions();
				SetServerMotionState(MotionState.Floating);
				ClientsPlayWaterImpactRpc(impactPosition, impactSpeed);
				UtilityMethods.Log($"[BeachBall] Entered water surface alignment. Object:{base.name}, Speed:{impactSpeed:F2}");
			}
		}

		private void SetServerMotionState(MotionState state)
		{
			if (base.IsServer)
			{
				if (_motionState.Value != state)
				{
					_motionState.Value = state;
				}
				ApplyLocalMotionState(state);
			}
		}

		private void OnMotionStateChanged(MotionState previousState, MotionState currentState)
		{
			switch (currentState)
			{
			case MotionState.Held:
				CancelLocalThrowPrediction(showAuthoritativePresentation: false);
				_awaitingReplicatedFreePhysicsState = false;
				_replicatedThrowAccepted = false;
				_hasPendingAuthoritativeTeleport = false;
				break;
			case MotionState.Floating:
				CancelLocalThrowPrediction(showAuthoritativePresentation: false);
				_awaitingReplicatedFreePhysicsState = false;
				_replicatedThrowAccepted = false;
				_hasPendingAuthoritativeTeleport = false;
				ObjectsStatus(status: true);
				break;
			}
			ApplyLocalMotionState(currentState);
		}

		private void ApplyLocalMotionState(MotionState state)
		{
			bool flag = base.IsServer && state == MotionState.FreePhysics;
			bool flag2 = state == MotionState.Floating;
			if (_networkTransform != null && !base.IsServer && !_networkTransform.enabled)
			{
				_networkTransform.enabled = true;
			}
			if (_alignToWater != null)
			{
				if (_waterPresentationTransform != null && _waterPresentationTransform != base.transform)
				{
					if (!flag2)
					{
						_alignToWater.enabled = false;
						RestoreWaterPresentationTransform();
					}
					else
					{
						_alignToWater.rigidbody = null;
						_alignToWater.enabled = true;
					}
				}
				else
				{
					_alignToWater.rigidbody = _rigidbody;
					_alignToWater.enabled = base.IsServer & flag2;
				}
			}
			if (_rigidbody != null)
			{
				if (!flag)
				{
					_rigidbody.linearVelocity = Vector3.zero;
					_rigidbody.angularVelocity = Vector3.zero;
				}
				_rigidbody.isKinematic = !flag;
				_rigidbody.useGravity = flag;
				_rigidbody.mass = Mathf.Max(0.01f, _mass);
				_rigidbody.linearDamping = Mathf.Max(0f, _linearDamping);
				_rigidbody.angularDamping = Mathf.Max(0f, _angularDamping);
				_rigidbody.maxAngularVelocity = Mathf.Max(0.01f, _maximumAngularSpeed);
				_rigidbody.collisionDetectionMode = (flag ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete);
				_rigidbody.interpolation = (base.IsServer ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None);
				_rigidbody.excludeLayers = _physicsExcludeLayers;
			}
			if (_physicsCollider != null)
			{
				bool flag3 = !base.IsServer && (_awaitingReplicatedFreePhysicsState || _localThrowPredictionActive || _predictionReconciling);
				_physicsCollider.enabled = state == MotionState.FreePhysics && !flag3;
				_physicsCollider.excludeLayers = (base.IsServer ? _physicsExcludeLayers : ((LayerMask)(-1)));
			}
			if (_floatingInteractionCollider != null)
			{
				_floatingInteractionCollider.isTrigger = true;
				_floatingInteractionCollider.enabled = state == MotionState.Floating;
				_floatingInteractionCollider.excludeLayers = -1;
			}
		}

		private void BeginLocalThrowPrediction(Thrower.ThrowPhysicsRequestData throwData)
		{
			if (base.IsSpawned && !base.IsServer && base.IsOwner && !(_itemSO == null) && throwData.IsValid && IsFinite(throwData.StartPosition) && IsFinite(throwData.Direction) && !(throwData.Direction.sqrMagnitude < 0.0001f))
			{
				EnsurePredictionObject();
				if (!(_predictionObject == null) && !(_predictionRigidbody == null) && !(_predictionCollider == null))
				{
					_replicatedThrowAccepted = false;
					_hasPendingAuthoritativeTeleport = false;
					_awaitingReplicatedFreePhysicsState = false;
					_predictionReconciling = false;
					_predictionReconcileElapsed = 0f;
					DisableHeldHints();
					DestroyFpsShadow();
					DestroyTpsShadow();
					Quaternion rotation = Quaternion.LookRotation(throwData.Direction, Vector3.up);
					Transform obj = _predictionObject.transform;
					obj.localScale = base.transform.lossyScale;
					obj.SetPositionAndRotation(throwData.StartPosition, rotation);
					int num = throwData.ExcludeLayerMaskValue | _physicsExcludeLayers.value;
					_predictionCollider.enabled = true;
					_predictionCollider.isTrigger = false;
					_predictionCollider.excludeLayers = num;
					_predictionRigidbody.linearVelocity = Vector3.zero;
					_predictionRigidbody.angularVelocity = Vector3.zero;
					_predictionRigidbody.isKinematic = false;
					_predictionRigidbody.useGravity = true;
					_predictionRigidbody.mass = Mathf.Max(0.01f, _mass);
					_predictionRigidbody.linearDamping = Mathf.Max(0f, _linearDamping);
					_predictionRigidbody.angularDamping = Mathf.Max(0f, _angularDamping);
					_predictionRigidbody.maxAngularVelocity = Mathf.Max(0.01f, _maximumAngularSpeed);
					_predictionRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
					_predictionRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
					_predictionRigidbody.excludeLayers = num;
					_predictionObject.SetActive(value: true);
					_predictionRigidbody.WakeUp();
					_localThrowPredictionActive = true;
					ObjectsStatus(status: false);
					CollidersStatus(status: false);
					BaseHoldable.ApplyThrowForces(_predictionRigidbody, throwData.Direction, throwData.Force, throwData.UpwardForce, throwData.TorqueImpulse, throwData.ForceModeId);
				}
			}
		}

		private void EnsurePredictionObject()
		{
			if (!(_predictionObject != null) && !(_itemSO?.GraphicObjectPrefab == null))
			{
				_predictionObject = UnityEngine.Object.Instantiate(_itemSO.GraphicObjectPrefab);
				_predictionObject.name = base.name + "_OwnerThrowPrediction";
				int num = LayerMask.NameToLayer("Ignore Raycast");
				SetLayerRecursively(_predictionObject, (num >= 0) ? num : base.gameObject.layer);
				Collider[] componentsInChildren = _predictionObject.GetComponentsInChildren<Collider>(includeInactive: true);
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					componentsInChildren[i].enabled = false;
				}
				_predictionRigidbody = _predictionObject.GetComponent<Rigidbody>();
				if (_predictionRigidbody == null)
				{
					_predictionRigidbody = _predictionObject.AddComponent<Rigidbody>();
				}
				_predictionCollider = _predictionObject.GetComponent<SphereCollider>();
				if (_predictionCollider == null)
				{
					_predictionCollider = _predictionObject.AddComponent<SphereCollider>();
				}
				if (_physicsCollider is SphereCollider sphereCollider)
				{
					_predictionCollider.center = sphereCollider.center;
					_predictionCollider.radius = sphereCollider.radius;
					_predictionCollider.sharedMaterial = sphereCollider.sharedMaterial;
				}
				_predictionRigidbody.isKinematic = true;
				_predictionRigidbody.useGravity = false;
				_predictionCollider.enabled = false;
				_predictionObject.SetActive(value: false);
			}
		}

		private void OnAuthoritativeTeleportReceived(Vector3 position, Quaternion rotation)
		{
			if (!base.IsServer && base.IsSpawned && IsFinite(position))
			{
				_pendingAuthoritativeTeleportPosition = position;
				_pendingAuthoritativeTeleportRotation = rotation;
				_hasPendingAuthoritativeTeleport = true;
				TryBeginAuthoritativeHandoff();
			}
		}

		private void TryBeginAuthoritativeHandoff()
		{
			if (!base.IsServer && _replicatedThrowAccepted && _hasPendingAuthoritativeTeleport)
			{
				base.transform.SetPositionAndRotation(_pendingAuthoritativeTeleportPosition, _pendingAuthoritativeTeleportRotation);
				_hasPendingAuthoritativeTeleport = false;
				_awaitingReplicatedFreePhysicsState = false;
				ApplyLocalMotionState(MotionState.FreePhysics);
				if (_localThrowPredictionActive && _predictionObject != null && _predictionObject.activeSelf)
				{
					BeginLocalThrowPredictionReconcile();
					return;
				}
				_replicatedThrowAccepted = false;
				ObjectsStatus(status: true);
				ApplyLocalMotionState(ResolveThrowHandoffPresentationState());
			}
		}

		private void BeginLocalThrowPredictionReconcile()
		{
			if (_predictionObject == null || _predictionRigidbody == null || _predictionCollider == null)
			{
				CompleteLocalThrowPredictionHandoff();
				return;
			}
			_predictionRigidbody.linearVelocity = Vector3.zero;
			_predictionRigidbody.angularVelocity = Vector3.zero;
			_predictionRigidbody.isKinematic = true;
			_predictionRigidbody.useGravity = false;
			_predictionCollider.enabled = false;
			_predictionReconcilePositionOffset = _predictionObject.transform.position - base.transform.position;
			_predictionReconcileRotationOffset = _predictionObject.transform.rotation * Quaternion.Inverse(base.transform.rotation);
			_predictionReconcileElapsed = 0f;
			_localThrowPredictionActive = false;
			float num = Mathf.Max(0.01f, _predictionHardSnapDistance);
			if (_predictionReconcilePositionOffset.magnitude > num)
			{
				CompleteLocalThrowPredictionHandoff();
			}
			else
			{
				_predictionReconciling = true;
			}
		}

		private void CompleteLocalThrowPredictionHandoff()
		{
			if (_predictionObject != null)
			{
				_predictionObject.SetActive(value: false);
			}
			_localThrowPredictionActive = false;
			_predictionReconciling = false;
			_predictionReconcileElapsed = 0f;
			_replicatedThrowAccepted = false;
			_hasPendingAuthoritativeTeleport = false;
			_awaitingReplicatedFreePhysicsState = false;
			ObjectsStatus(status: true);
			ApplyLocalMotionState(ResolveThrowHandoffPresentationState());
		}

		private void CancelLocalThrowPrediction(bool showAuthoritativePresentation)
		{
			if (_predictionRigidbody != null)
			{
				_predictionRigidbody.linearVelocity = Vector3.zero;
				_predictionRigidbody.angularVelocity = Vector3.zero;
				_predictionRigidbody.isKinematic = true;
				_predictionRigidbody.useGravity = false;
			}
			if (_predictionCollider != null)
			{
				_predictionCollider.enabled = false;
			}
			if (_predictionObject != null)
			{
				_predictionObject.SetActive(value: false);
			}
			_localThrowPredictionActive = false;
			_predictionReconciling = false;
			_predictionReconcileElapsed = 0f;
			_replicatedThrowAccepted = false;
			_hasPendingAuthoritativeTeleport = false;
			if (showAuthoritativePresentation)
			{
				ObjectsStatus(status: true);
				ApplyLocalMotionState(_motionState.Value);
			}
		}

		private MotionState ResolveThrowHandoffPresentationState()
		{
			if (_motionState.Value != MotionState.Floating)
			{
				return MotionState.FreePhysics;
			}
			return MotionState.Floating;
		}

		private void DestroyPredictionObject()
		{
			if (_predictionObject != null)
			{
				UnityEngine.Object.Destroy(_predictionObject);
			}
			_predictionObject = null;
			_predictionRigidbody = null;
			_predictionCollider = null;
		}

		private static void SetLayerRecursively(GameObject root, int layer)
		{
			if (!(root == null))
			{
				root.layer = layer;
				Transform transform = root.transform;
				for (int i = 0; i < transform.childCount; i++)
				{
					SetLayerRecursively(transform.GetChild(i).gameObject, layer);
				}
			}
		}

		private void RestoreWaterPresentationTransform()
		{
			if (!(_waterPresentationTransform == null) && !(_waterPresentationTransform == base.transform))
			{
				_waterPresentationTransform.SetLocalPositionAndRotation(_waterPresentationLocalPosition, _waterPresentationLocalRotation);
			}
		}

		private void ConfigureServerFreePhysics()
		{
			ApplyLocalMotionState(MotionState.FreePhysics);
			if (!(_rigidbody == null))
			{
				_rigidbody.linearVelocity = Vector3.zero;
				_rigidbody.angularVelocity = Vector3.zero;
				_rigidbody.maxAngularVelocity = Mathf.Max(0.01f, _maximumAngularSpeed);
				_rigidbody.WakeUp();
			}
		}

		public void ServerResetToInitialSpawn(string reason)
		{
			if (!base.IsServer || !base.IsSpawned)
			{
				return;
			}
			RestoreThrowerCollisions();
			int num;
			if (_motionState.Value != MotionState.Held)
			{
				num = ((!CanHold.Value) ? 1 : 0);
				if (num == 0)
				{
					goto IL_004a;
				}
			}
			else
			{
				num = 1;
			}
			base.ServerDrop(_serverResetPosition, _serverResetRotation);
			goto IL_004a;
			IL_004a:
			if (num == 0 && base.OwnerClientId != 0L)
			{
				base.NetworkObject.RemoveOwnership();
			}
			_ignoreWaterUntilTime = Time.time + Mathf.Max(0f, _waterReentryGraceDuration);
			_nextWaterOverlapProbeTime = _ignoreWaterUntilTime;
			_runawaySpeedElapsed = 0f;
			_nextSurfaceBounceTime = 0f;
			_nextPlayerHitTimes.Clear();
			SetServerMotionState(MotionState.FreePhysics);
			ConfigureServerFreePhysics();
			if (_networkTransform != null)
			{
				_networkTransform.Teleport(_serverResetPosition, _serverResetRotation, base.transform.localScale);
			}
			else
			{
				base.transform.SetPositionAndRotation(_serverResetPosition, _serverResetRotation);
			}
			UtilityMethods.Log("[BeachBall] Reset to initial spawn. Object:" + base.name + ", Reason:" + reason);
		}

		private void TryEnterWaterFromTrigger(Collider other)
		{
			if (BaseHoldable.IsThrowWaterCollider(other) && CanEnterWater(other))
			{
				float impactSpeed = ((_rigidbody != null) ? _rigidbody.linearVelocity.magnitude : 0f);
				Vector3 impactPosition = ((other != null && other.enabled) ? other.ClosestPoint(base.transform.position) : base.transform.position);
				ServerEnterWater(impactPosition, impactSpeed);
			}
		}

		private void TryEnterWaterFromOverlapProbe()
		{
			if (Time.time < _ignoreWaterUntilTime || Time.time < _nextWaterOverlapProbeTime)
			{
				return;
			}
			_nextWaterOverlapProbeTime = Time.time + 0.1f;
			if (!TryResolveWaterSurfaceHeight(null, out var waterSurfaceHeight) || base.transform.position.y > waterSurfaceHeight - Mathf.Max(0f, _waterEntryDepth))
			{
				return;
			}
			float radius = 0.15f;
			if (_physicsCollider is SphereCollider sphereCollider)
			{
				Vector3 lossyScale = sphereCollider.transform.lossyScale;
				float num = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
				radius = Mathf.Max(0.01f, sphereCollider.radius * num);
			}
			int num2 = Physics.OverlapSphereNonAlloc(base.transform.position, radius, _waterOverlapProbeHits, -1, QueryTriggerInteraction.Collide);
			for (int i = 0; i < num2; i++)
			{
				Collider collider = _waterOverlapProbeHits[i];
				if (BaseHoldable.IsThrowWaterCollider(collider) && CanEnterWater(collider))
				{
					float impactSpeed = ((_rigidbody != null) ? _rigidbody.linearVelocity.magnitude : 0f);
					ServerEnterWater(base.transform.position, impactSpeed);
					break;
				}
			}
		}

		private bool CanEnterWater(Collider waterCollider)
		{
			if (Time.time < _ignoreWaterUntilTime)
			{
				return false;
			}
			if (!TryResolveWaterSurfaceHeight(waterCollider, out var waterSurfaceHeight))
			{
				return false;
			}
			return base.transform.position.y <= waterSurfaceHeight - Mathf.Max(0f, _waterEntryDepth);
		}

		private static bool TryResolveWaterSurfaceHeight(Collider waterCollider, out float waterSurfaceHeight)
		{
			if (OceanFollowBehaviour.Instance != null)
			{
				waterSurfaceHeight = OceanFollowBehaviour.Instance.transform.position.y;
				return true;
			}
			if (waterCollider != null)
			{
				waterSurfaceHeight = waterCollider.bounds.max.y;
				return true;
			}
			waterSurfaceHeight = 0f;
			return false;
		}

		private bool ShouldResetRunawayBall()
		{
			Vector3 position = base.transform.position;
			if (!IsFinite(position))
			{
				return true;
			}
			if (position.y < _outOfBoundsHeight)
			{
				return true;
			}
			if ((position - _serverResetPosition).sqrMagnitude > _maximumDistanceFromSpawn * _maximumDistanceFromSpawn)
			{
				return true;
			}
			if (position.y - _serverResetPosition.y > _maximumHeightAboveSpawn)
			{
				return true;
			}
			if (_rigidbody == null)
			{
				return true;
			}
			if (!IsFinite(_rigidbody.linearVelocity) || !IsFinite(_rigidbody.angularVelocity))
			{
				return true;
			}
			if (_rigidbody.linearVelocity.sqrMagnitude > _runawaySpeed * _runawaySpeed)
			{
				_runawaySpeedElapsed += Time.fixedDeltaTime;
			}
			else
			{
				_runawaySpeedElapsed = 0f;
			}
			return _runawaySpeedElapsed >= _runawaySpeedDuration;
		}

		private void TryRegisterWithRestaurantManager()
		{
			if (base.IsServer && !(_restaurantManager != null))
			{
				RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
				if (!(restaurantManager == null) && restaurantManager.RegisterBeachBall(this))
				{
					_restaurantManager = restaurantManager;
				}
			}
		}

		private static bool IsFinite(Vector3 value)
		{
			if (float.IsFinite(value.x) && float.IsFinite(value.y))
			{
				return float.IsFinite(value.z);
			}
			return false;
		}

		private void ClampServerAngularVelocity()
		{
			if (!(_rigidbody == null))
			{
				float num = Mathf.Max(0.01f, _maximumAngularSpeed);
				if (!(_rigidbody.angularVelocity.sqrMagnitude <= num * num))
				{
					_rigidbody.angularVelocity = Vector3.ClampMagnitude(_rigidbody.angularVelocity, num);
				}
			}
		}

		private void IgnoreThrowerCollisions(ulong holderClientId)
		{
			RestoreThrowerCollisions();
			if (_physicsCollider == null || _throwerCollisionIgnoreDuration <= 0f || Singleton<PlayersManager>.Instance == null || !Singleton<PlayersManager>.Instance.TryGetPlayer(holderClientId, out var player) || player == null)
			{
				return;
			}
			Collider[] componentsInChildren = player.GetComponentsInChildren<Collider>(includeInactive: true);
			foreach (Collider collider in componentsInChildren)
			{
				if (!(collider == null) && !(collider == _physicsCollider))
				{
					Physics.IgnoreCollision(_physicsCollider, collider, ignore: true);
					_ignoredThrowerColliders.Add(collider);
				}
			}
			_restoreThrowerCollisionsTime = Time.time + Mathf.Max(0f, _throwerCollisionIgnoreDuration);
		}

		private void TryRestoreThrowerCollisions()
		{
			if (_ignoredThrowerColliders.Count != 0 && !(Time.time < _restoreThrowerCollisionsTime))
			{
				RestoreThrowerCollisions();
			}
		}

		private void RestoreThrowerCollisions()
		{
			if (_physicsCollider != null)
			{
				for (int i = 0; i < _ignoredThrowerColliders.Count; i++)
				{
					Collider collider = _ignoredThrowerColliders[i];
					if (collider != null)
					{
						Physics.IgnoreCollision(_physicsCollider, collider, ignore: false);
					}
				}
			}
			_ignoredThrowerColliders.Clear();
			_restoreThrowerCollisionsTime = 0f;
		}

		[Rpc(SendTo.Everyone)]
		private void ClientsPlaySfxRpc(SfxKind kind, Vector3 position, float strength01)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1307886294u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in kind, default(FastBufferWriter.ForEnums));
				bufferWriter.WriteValueSafe(in position);
				bufferWriter.WriteValueSafe(in strength01, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1307886294u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				PlaySfxLocal(kind, position, strength01);
			}
		}

		private void PlaySfxLocal(SfxKind kind, Vector3 position, float strength01)
		{
			AudioClipSO audioClipSO = kind switch
			{
				SfxKind.Pickup => _pickupSfx, 
				SfxKind.PlayerHit => _playerHitSfx, 
				SfxKind.SurfaceBounce => _surfaceBounceSfx, 
				SfxKind.WaterImpact => _waterImpactSfx, 
				SfxKind.UmbrellaBoost => _umbrellaBoostSfx, 
				_ => null, 
			};
			if (Singleton<SoundManager>.Instance == null)
			{
				return;
			}
			if (audioClipSO == null)
			{
				if (!(_itemSO == null))
				{
					int num;
					switch (kind)
					{
					case SfxKind.WaterImpact:
					case SfxKind.UmbrellaBoost:
						return;
					default:
						num = 35;
						break;
					case SfxKind.Pickup:
						num = 34;
						break;
					}
					SoundType type = (SoundType)num;
					Singleton<SoundManager>.Instance.PlaySFX(type, _itemSO.HoldableMaterialType, _itemSO.FurnitureWeight, position);
				}
			}
			else
			{
				AudioSource audioSource = Singleton<SoundManager>.Instance.PlaySFX(audioClipSO, position);
				if (audioSource != null)
				{
					audioSource.volume *= Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(strength01));
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientsPlayWaterImpactRpc(Vector3 position, float impactSpeed)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2835115272u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in position);
				bufferWriter.WriteValueSafe(in impactSpeed, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2835115272u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				float strength = Mathf.InverseLerp(0f, _waterBigSplashSpeed, impactSpeed);
				PlaySfxLocal(SfxKind.WaterImpact, position, strength);
				if (Singleton<OceanFXManager>.Instance != null)
				{
					bool isBig = impactSpeed >= _waterBigSplashSpeed;
					Singleton<OceanFXManager>.Instance.PlayObjectWaterImpact(position, impactSpeed, isBig);
				}
			}
		}

		protected override void __initializeVariables()
		{
			if (_motionState == null)
			{
				throw new Exception("BeachBall._motionState cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_motionState.Initialize(this);
			__nameNetworkVariable(_motionState, "_motionState");
			NetworkVariableFields.Add(_motionState);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(1307886294u, __rpc_handler_1307886294, "ClientsPlaySfxRpc", RpcInvokePermission.Everyone);
			__registerRpc(2835115272u, __rpc_handler_2835115272, "ClientsPlayWaterImpactRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_1307886294(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out SfxKind value, default(FastBufferWriter.ForEnums));
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out float value3, default(FastBufferWriter.ForPrimitives));
				((BeachBall)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BeachBall)target).ClientsPlaySfxRpc(value, value2, value3);
				((BeachBall)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2835115272(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				((BeachBall)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((BeachBall)target).ClientsPlayWaterImpactRpc(value, value2);
				((BeachBall)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "BeachBall";
		}
	}
}
