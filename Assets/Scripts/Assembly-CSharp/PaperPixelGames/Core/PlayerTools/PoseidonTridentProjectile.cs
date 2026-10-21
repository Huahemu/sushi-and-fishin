using System;
using System.Collections.Generic;
using System.Diagnostics;
using PaperPixelGames.Core.Animals;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Data.Tools;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.PlayerTools
{
	[RequireComponent(typeof(NetworkObject))]
	[RequireComponent(typeof(Rigidbody))]
	public sealed class PoseidonTridentProjectile : NetworkBehaviour
	{
		private const string UnderwaterPhysicsVolumeName = "Underwater Physic Volume";

		private const string LegacyUnderwaterVolumeName = "UnderWater Volume";

		private const int ProjectileHitBufferCapacity = 64;

		private const int WaterHitBufferCapacity = 64;

		private const float ImpactVfxLifetime = 5f;

		private const int HarpoonHitsPerImpact = 3;

		[SerializeField]
		private Transform _visualRoot;

		[Header("Polish")]
		[SerializeField]
		private ParticleSystem _underwaterTrail;

		[SerializeField]
		private ParticleSystem _stoneImpactVfx;

		[SerializeField]
		private ParticleSystem _sandImpactVfx;

		[SerializeField]
		private ParticleSystem _grassImpactVfx;

		[SerializeField]
		private ParticleSystem _fishImpactVfx;

		[SerializeField]
		private ParticleSystem _playerImpactVfx;

		private readonly HashSet<ulong> _processedCreatureIds = new HashSet<ulong>();

		private readonly HashSet<ulong> _damagedCreatureIds = new HashSet<ulong>();

		private readonly RaycastHit[] _projectileHitBuffer = new RaycastHit[64];

		private readonly RaycastHit[] _waterHitBuffer = new RaycastHit[64];

		private readonly Collider[] _waterProbeHits = new Collider[64];

		private readonly NetworkVariable<Vector3> _outboundVisualPosition = new NetworkVariable<Vector3>();

		private readonly NetworkVariable<Vector3> _outboundVisualRotation = new NetworkVariable<Vector3>();

		private readonly NetworkVariable<Vector3> _outboundVisualScale = new NetworkVariable<Vector3>(Vector3.one);

		private readonly NetworkVariable<Vector3> _returnVisualPosition = new NetworkVariable<Vector3>();

		private readonly NetworkVariable<Vector3> _returnVisualRotation = new NetworkVariable<Vector3>();

		private readonly NetworkVariable<Vector3> _returnVisualScale = new NetworkVariable<Vector3>(Vector3.one);

		private readonly NetworkVariable<bool> _returnVisualActive = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<bool> _underwaterTrailActive = new NetworkVariable<bool>(value: false);

		private PoseidonTrident _ownerTool;

		private PPG_Player _ownerPlayer;

		private byte _hostileDamage;

		private Rigidbody _rigidbody;

		private LayerMask _hitMask;

		private LayerMask _waterProbeMask;

		private float _collisionRadius;

		private float _autoReturnDelay;

		private float _returnSpeed;

		private float _returnArrivalDistance;

		private float _maximumDistance;

		private float _alignToVelocitySpeed;

		private float _waterLinearDamping;

		private float _waterAngularDamping;

		private float _waterVelocityCap;

		private float _waterEffectsActivationVelocity;

		private float _waterProbeRadius;

		private float _distanceTravelled;

		private float _spawnedAt;

		private Vector3 _lastPhysicsPosition;

		private Vector3 _launchDirection;

		private bool _playWaterImpactFx;

		private bool _waterContactDetected;

		private bool _waterEffectsActive;

		private bool _initialized;

		private bool _returning;

		private bool _stopped;

		public override void OnNetworkSpawn()
		{
			_rigidbody = GetComponent<Rigidbody>();
			NetworkVariable<bool> underwaterTrailActive = _underwaterTrailActive;
			underwaterTrailActive.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(underwaterTrailActive.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnUnderwaterTrailActiveChanged));
			ApplyUnderwaterTrailState(_underwaterTrailActive.Value);
			if (!base.IsServer && _rigidbody != null)
			{
				_rigidbody.isKinematic = true;
			}
			base.OnNetworkSpawn();
		}

		public void ServerInitialize(PoseidonTrident ownerTool, PPG_Player ownerPlayer, Vector3 direction, float charge, PoseidonTridentData data)
		{
			if (!base.IsServer || data == null)
			{
				return;
			}
			if ((object)_rigidbody == null)
			{
				_rigidbody = GetComponent<Rigidbody>();
			}
			if (_rigidbody == null)
			{
				ServerDespawnUnexpected();
				return;
			}
			Vector3 vector = ((direction.sqrMagnitude > 0.0001f) ? direction.normalized : base.transform.forward);
			float t = Mathf.Clamp01(charge);
			bool num = ownerPlayer != null && ownerPlayer.IsUnderWater;
			_ownerTool = ownerTool;
			_ownerPlayer = ownerPlayer;
			int num2 = ((!(ownerPlayer != null) || !(ownerPlayer.Harpoon != null)) ? 1 : ownerPlayer.Harpoon.GetHostileHitDamage());
			_hostileDamage = (byte)Mathf.Clamp(num2 * 3, 1, 255);
			_collisionRadius = Mathf.Max(0.01f, data.CollisionRadius);
			_autoReturnDelay = Mathf.Max(0f, data.AutoReturnDelay);
			_returnSpeed = Mathf.Max(0.01f, data.ReturnSpeed);
			_returnArrivalDistance = Mathf.Max(0.01f, data.ReturnArrivalDistance);
			_maximumDistance = Mathf.Max(1f, data.MaxSafetyDistance);
			_alignToVelocitySpeed = Mathf.Max(0f, data.AlignToVelocitySpeed);
			_hitMask = data.ProjectileHitMask;
			_waterProbeMask = data.WaterProbeMask;
			_waterLinearDamping = Mathf.Max(0f, data.WaterLinearDamping);
			_waterAngularDamping = Mathf.Max(0f, data.WaterAngularDamping);
			_waterVelocityCap = Mathf.Max(0.01f, data.WaterVelocityCap);
			_waterEffectsActivationVelocity = Mathf.Max(0.01f, data.WaterEffectsActivationVelocity);
			_waterProbeRadius = Mathf.Max(0.01f, data.WaterProbeRadius);
			_playWaterImpactFx = data.PlayWaterImpactFx;
			_distanceTravelled = 0f;
			_spawnedAt = Time.time;
			_lastPhysicsPosition = base.transform.position;
			_launchDirection = vector;
			_processedCreatureIds.Clear();
			_damagedCreatureIds.Clear();
			_waterContactDetected = false;
			_waterEffectsActive = false;
			_returning = false;
			_stopped = false;
			_initialized = true;
			SetNetworkVisualOffset(data.OutboundOffset, returning: false);
			SetNetworkVisualOffset(data.ReturningOffset, returning: true);
			_returnVisualActive.Value = false;
			base.transform.rotation = Quaternion.LookRotation(vector, Vector3.up);
			_rigidbody.position = base.transform.position;
			_rigidbody.rotation = base.transform.rotation;
			_rigidbody.mass = Mathf.Max(0.01f, data.ProjectileMass);
			_rigidbody.useGravity = data.UseGravity;
			_rigidbody.linearDamping = Mathf.Max(0f, data.AirLinearDamping);
			_rigidbody.angularDamping = Mathf.Max(0f, data.AirAngularDamping);
			_rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
			_rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
			_rigidbody.isKinematic = false;
			_rigidbody.linearVelocity = Vector3.zero;
			_rigidbody.angularVelocity = Vector3.zero;
			float num3 = Mathf.Lerp(Mathf.Max(0.01f, data.MinThrowImpulse), Mathf.Max(0.01f, data.MaxThrowImpulse), t);
			float num4 = Mathf.Lerp(Mathf.Max(0f, data.MinUpwardImpulse), Mathf.Max(0f, data.MaxUpwardImpulse), t);
			if (num)
			{
				num3 *= Mathf.Clamp(data.UnderwaterForceMultiplier, 0.01f, 1f);
				num4 *= Mathf.Clamp01(data.UnderwaterUpwardForceMultiplier);
				RegisterWaterContact(base.transform.position, 0f, playImpactFx: false);
			}
			Vector3 force = vector * num3 + Vector3.up * num4;
			_rigidbody.AddForce(force, ForceMode.Impulse);
		}

		private void LateUpdate()
		{
			ApplyVisualOffset();
		}

		public override void OnNetworkDespawn()
		{
			NetworkVariable<bool> underwaterTrailActive = _underwaterTrailActive;
			underwaterTrailActive.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(underwaterTrailActive.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnUnderwaterTrailActiveChanged));
			ApplyUnderwaterTrailState(active: false);
			if (base.IsServer && _ownerTool != null)
			{
				_ownerTool.ServerProjectileDespawned(this);
			}
			if (_rigidbody != null)
			{
				_rigidbody.linearVelocity = Vector3.zero;
				_rigidbody.angularVelocity = Vector3.zero;
				_rigidbody.isKinematic = true;
			}
			_processedCreatureIds.Clear();
			_damagedCreatureIds.Clear();
			_ownerTool = null;
			_ownerPlayer = null;
			_initialized = false;
			base.OnNetworkDespawn();
		}

		private void FixedUpdate()
		{
			if (!base.IsServer || !_initialized)
			{
				return;
			}
			if (_ownerTool == null || _ownerPlayer == null || !_ownerTool.ToolStatus)
			{
				ServerDespawnUnexpected();
				return;
			}
			if (!_returning && Time.time - _spawnedAt >= _autoReturnDelay)
			{
				ServerBeginReturn();
			}
			if (_returning)
			{
				TickReturn();
			}
			else if (!_stopped)
			{
				TickOutboundPhysics();
			}
		}

		public void ServerBeginReturn()
		{
			if (base.IsServer && _initialized && !_returning)
			{
				_returning = true;
				_stopped = false;
				_processedCreatureIds.Clear();
				_returnVisualActive.Value = true;
				SetUnderwaterTrailActive(active: false);
				DisableOutboundPhysics();
				_ownerTool?.ServerSetProjectileState(this, PoseidonTridentState.Returning);
			}
		}

		private void TickOutboundPhysics()
		{
			Vector3 position = _rigidbody.position;
			_distanceTravelled += Vector3.Distance(_lastPhysicsPosition, position);
			_lastPhysicsPosition = position;
			if (_distanceTravelled >= _maximumDistance)
			{
				ServerStop();
				return;
			}
			ProbeWaterVolume();
			TryActivateWaterEffects();
			Vector3 linearVelocity = _rigidbody.linearVelocity;
			if (_rigidbody.useGravity)
			{
				linearVelocity += Physics.gravity * Time.fixedDeltaTime;
			}
			Vector3 vector = linearVelocity * Time.fixedDeltaTime;
			float magnitude = vector.magnitude;
			if (magnitude <= 0.0001f)
			{
				return;
			}
			Vector3 direction = vector / magnitude;
			bool waterEffectsActive = _waterEffectsActive;
			ProbeWaterAlongPath(position, direction, magnitude);
			TryActivateWaterEffects();
			if (!waterEffectsActive && _waterEffectsActive)
			{
				linearVelocity = _rigidbody.linearVelocity;
				if (_rigidbody.useGravity)
				{
					linearVelocity += Physics.gravity * Time.fixedDeltaTime;
				}
				vector = linearVelocity * Time.fixedDeltaTime;
				magnitude = vector.magnitude;
				if (magnitude <= 0.0001f)
				{
					return;
				}
				direction = vector / magnitude;
			}
			float num = Mathf.Max(0f, _maximumDistance - _distanceTravelled);
			float maxDistance = Mathf.Min(magnitude, num);
			int num2 = Physics.SphereCastNonAlloc(position, _collisionRadius, direction, _projectileHitBuffer, maxDistance, _hitMask, QueryTriggerInteraction.Collide);
			SortHitsByDistance(_projectileHitBuffer, num2);
			for (int i = 0; i < num2; i++)
			{
				RaycastHit raycastHit = _projectileHitBuffer[i];
				Collider collider = raycastHit.collider;
				if (collider == null || IsOwnerCollider(collider) || IsProjectileCollider(collider))
				{
					continue;
				}
				CrabSmallController componentInParent = collider.GetComponentInParent<CrabSmallController>();
				if (componentInParent != null)
				{
					componentInParent.TryDodge(raycastHit.point);
					continue;
				}
				OctopusCreature componentInParent2 = collider.GetComponentInParent<OctopusCreature>();
				if (componentInParent2 != null)
				{
					if (TryHitOctopus(componentInParent2, raycastHit.point))
					{
						ServerPlayImpact(PoseidonTridentImpactSurface.Fish, raycastHit.point, raycastHit.normal);
					}
					continue;
				}
				CatchableSeaCreature componentInParent3 = collider.GetComponentInParent<CatchableSeaCreature>();
				if (componentInParent3 != null)
				{
					if (TryInstantCatch(componentInParent3, raycastHit.point))
					{
						ServerPlayImpact(PoseidonTridentImpactSurface.Fish, raycastHit.point, raycastHit.normal);
					}
					continue;
				}
				HostileSeaCreature componentInParent4 = collider.GetComponentInParent<HostileSeaCreature>();
				if (componentInParent4 != null)
				{
					if (TryDamageHostile(componentInParent4))
					{
						ServerPlayImpact(PoseidonTridentImpactSurface.Fish, raycastHit.point, raycastHit.normal);
					}
					StopAtHit(position, direction, raycastHit.distance);
					return;
				}
				if (!collider.isTrigger)
				{
					ServerPlayImpact(ResolveImpactSurface(collider, raycastHit.point), raycastHit.point, raycastHit.normal);
					StopAtHit(position, direction, raycastHit.distance);
					return;
				}
			}
			if (magnitude >= num)
			{
				StopAtHit(position, direction, num);
			}
			else
			{
				AlignToVelocity(linearVelocity);
			}
		}

		private void AlignToVelocity(Vector3 velocity)
		{
			if (!(_alignToVelocitySpeed <= 0f) && !(velocity.sqrMagnitude <= 0.0001f))
			{
				Quaternion b = Quaternion.LookRotation(velocity.normalized, Vector3.up);
				Quaternion rotation = Quaternion.Slerp(_rigidbody.rotation, b, Mathf.Clamp01(_alignToVelocitySpeed * Time.fixedDeltaTime));
				_rigidbody.MoveRotation(rotation);
			}
		}

		private void StopAtHit(Vector3 start, Vector3 direction, float hitDistance)
		{
			float num = Mathf.Max(0f, hitDistance - 0.01f);
			Vector3 vector = start + direction * num;
			_rigidbody.position = vector;
			base.transform.position = vector;
			_lastPhysicsPosition = vector;
			_distanceTravelled += num;
			ServerStop();
		}

		private void ProbeWaterVolume()
		{
			if (_waterContactDetected)
			{
				return;
			}
			int num = Physics.OverlapSphereNonAlloc(_rigidbody.position, _waterProbeRadius, _waterProbeHits, _waterProbeMask, QueryTriggerInteraction.Collide);
			for (int i = 0; i < num; i++)
			{
				Collider collider = _waterProbeHits[i];
				if (!(collider == null) && IsWaterCollider(collider))
				{
					float magnitude = _rigidbody.linearVelocity.magnitude;
					Vector3 impactPosition = (collider.enabled ? collider.ClosestPoint(_rigidbody.position) : _rigidbody.position);
					RegisterWaterContact(impactPosition, magnitude, playImpactFx: true);
					break;
				}
			}
		}

		private void ProbeWaterAlongPath(Vector3 start, Vector3 direction, float distance)
		{
			if (_waterContactDetected || distance <= 0.0001f)
			{
				return;
			}
			int num = Physics.SphereCastNonAlloc(start, _waterProbeRadius, direction, _waterHitBuffer, distance, _waterProbeMask, QueryTriggerInteraction.Collide);
			int num2 = -1;
			float num3 = float.PositiveInfinity;
			for (int i = 0; i < num; i++)
			{
				RaycastHit raycastHit = _waterHitBuffer[i];
				if (!(raycastHit.collider == null) && IsWaterCollider(raycastHit.collider) && !(raycastHit.distance >= num3))
				{
					num2 = i;
					num3 = raycastHit.distance;
				}
			}
			if (num2 >= 0)
			{
				RaycastHit raycastHit2 = _waterHitBuffer[num2];
				RegisterWaterContact(raycastHit2.point, _rigidbody.linearVelocity.magnitude, playImpactFx: true);
			}
		}

		private void RegisterWaterContact(Vector3 impactPosition, float impactSpeed, bool playImpactFx)
		{
			if (!_waterContactDetected)
			{
				_waterContactDetected = true;
				SetUnderwaterTrailActive(active: true);
				if (playImpactFx && _playWaterImpactFx)
				{
					bool isBigImpact = impactSpeed >= 4f;
					ClientPlayWaterImpactRpc(impactPosition, impactSpeed, isBigImpact);
				}
			}
		}

		private void TryActivateWaterEffects()
		{
			if (_waterContactDetected && !_waterEffectsActive && !(_rigidbody == null) && !(Mathf.Abs(Vector3.Dot(_rigidbody.linearVelocity, _launchDirection)) > _waterEffectsActivationVelocity))
			{
				_waterEffectsActive = true;
				_rigidbody.linearDamping = Mathf.Max(_rigidbody.linearDamping, _waterLinearDamping);
				_rigidbody.angularDamping = Mathf.Max(_rigidbody.angularDamping, _waterAngularDamping);
				if (_rigidbody.linearVelocity.magnitude > _waterVelocityCap)
				{
					_rigidbody.linearVelocity = _rigidbody.linearVelocity.normalized * _waterVelocityCap;
				}
				if (_rigidbody.angularVelocity.magnitude > _waterVelocityCap)
				{
					_rigidbody.angularVelocity = _rigidbody.angularVelocity.normalized * _waterVelocityCap;
				}
			}
		}

		private static bool IsWaterCollider(Collider collider)
		{
			Transform transform = ((collider != null) ? collider.transform : null);
			while (transform != null)
			{
				GameObject gameObject = transform.gameObject;
				if (gameObject.name == "Underwater Physic Volume" || gameObject.name == "UnderWater Volume" || gameObject.CompareTag("Water"))
				{
					return true;
				}
				transform = transform.parent;
			}
			return false;
		}

		private void TickReturn()
		{
			Vector3 serverReturnTargetPosition = _ownerTool.GetServerReturnTargetPosition();
			Vector3 position = base.transform.position;
			Vector3 vector = serverReturnTargetPosition - position;
			float magnitude = vector.magnitude;
			if (magnitude <= _returnArrivalDistance)
			{
				_ownerTool.ServerProjectileReturned(this);
				return;
			}
			Vector3 vector2 = vector / magnitude;
			float num = Mathf.Min(_returnSpeed * Time.fixedDeltaTime, magnitude);
			ProcessReturnCreatures(position, vector2, num);
			Vector3 position2 = position + vector2 * num;
			Quaternion rotation = Quaternion.LookRotation(vector2, Vector3.up);
			_rigidbody.position = position2;
			_rigidbody.rotation = rotation;
			base.transform.SetPositionAndRotation(position2, rotation);
		}

		private void ProcessReturnCreatures(Vector3 start, Vector3 direction, float distance)
		{
			int num = Physics.SphereCastNonAlloc(start, _collisionRadius, direction, _projectileHitBuffer, distance, _hitMask, QueryTriggerInteraction.Collide);
			SortHitsByDistance(_projectileHitBuffer, num);
			for (int i = 0; i < num; i++)
			{
				RaycastHit raycastHit = _projectileHitBuffer[i];
				Collider collider = raycastHit.collider;
				if (collider == null || IsOwnerCollider(collider) || IsProjectileCollider(collider))
				{
					continue;
				}
				OctopusCreature componentInParent = collider.GetComponentInParent<OctopusCreature>();
				if (componentInParent != null)
				{
					if (TryHitOctopus(componentInParent, raycastHit.point))
					{
						ServerPlayImpact(PoseidonTridentImpactSurface.Fish, raycastHit.point, raycastHit.normal);
					}
					continue;
				}
				HostileSeaCreature componentInParent2 = collider.GetComponentInParent<HostileSeaCreature>();
				if (componentInParent2 != null)
				{
					if (TryDamageHostile(componentInParent2))
					{
						ServerPlayImpact(PoseidonTridentImpactSurface.Fish, raycastHit.point, raycastHit.normal);
					}
					continue;
				}
				CatchableSeaCreature componentInParent3 = collider.GetComponentInParent<CatchableSeaCreature>();
				if (componentInParent3 != null && TryInstantCatch(componentInParent3, raycastHit.point))
				{
					ServerPlayImpact(PoseidonTridentImpactSurface.Fish, raycastHit.point, raycastHit.normal);
				}
			}
		}

		private void DisableOutboundPhysics()
		{
			if (!(_rigidbody == null))
			{
				_rigidbody.linearVelocity = Vector3.zero;
				_rigidbody.angularVelocity = Vector3.zero;
				_rigidbody.useGravity = false;
				_rigidbody.isKinematic = true;
			}
		}

		private void SetNetworkVisualOffset(ItemOffset offset, bool returning)
		{
			Vector3 value = offset?.PositionOffset ?? Vector3.zero;
			Vector3 value2 = offset?.RotationOffset ?? Vector3.zero;
			Vector3 value3 = offset?.ScaleOffset ?? Vector3.one;
			if (returning)
			{
				_returnVisualPosition.Value = value;
				_returnVisualRotation.Value = value2;
				_returnVisualScale.Value = value3;
			}
			else
			{
				_outboundVisualPosition.Value = value;
				_outboundVisualRotation.Value = value2;
				_outboundVisualScale.Value = value3;
			}
		}

		private void ApplyVisualOffset()
		{
			if (!(_visualRoot == null))
			{
				bool value = _returnVisualActive.Value;
				_visualRoot.localPosition = (value ? _returnVisualPosition.Value : _outboundVisualPosition.Value);
				_visualRoot.localRotation = Quaternion.Euler(value ? _returnVisualRotation.Value : _outboundVisualRotation.Value);
				_visualRoot.localScale = (value ? _returnVisualScale.Value : _outboundVisualScale.Value);
			}
		}

		private bool TryDamageHostile(HostileSeaCreature creature)
		{
			if (!base.IsServer || _ownerPlayer == null || creature.NetworkObject == null || !creature.IsSpawned || creature.IsDead || !_damagedCreatureIds.Add(creature.NetworkObjectId))
			{
				return false;
			}
			creature.ServerReceiveHarpoonHit(_ownerPlayer, _hostileDamage);
			return true;
		}

		private bool TryHitOctopus(OctopusCreature creature, Vector3 hitPosition)
		{
			if (!base.IsServer || _ownerPlayer == null || creature.NetworkObject == null || !creature.IsSpawned || creature.IsHooked.Value || creature.CatchState == CatchableCreatureState.Caught || !_damagedCreatureIds.Add(creature.NetworkObjectId))
			{
				return false;
			}
			creature.ServerReceiveTridentHit(_ownerPlayer, 3);
			TryInstantCatch(creature, hitPosition);
			return true;
		}

		private bool TryInstantCatch(CatchableSeaCreature creature, Vector3 hitPosition)
		{
			if (creature.NetworkObject == null)
			{
				return false;
			}
			ulong networkObjectId = creature.NetworkObjectId;
			if (!_processedCreatureIds.Add(networkObjectId))
			{
				return false;
			}
			if (!creature.ServerTryInstantCatch(_ownerPlayer, hitPosition))
			{
				return false;
			}
			QuestReferenceHub.Instance?.AddProgress(QuestId.CatchTwentyFishWithTheTrident);
			return true;
		}

		private PoseidonTridentImpactSurface ResolveImpactSurface(Collider collider, Vector3 hitPosition)
		{
			if (collider.GetComponentInParent<PPG_Player>() != null)
			{
				return PoseidonTridentImpactSurface.Player;
			}
			Terrain componentInParent = collider.GetComponentInParent<Terrain>();
			if (componentInParent == null || _ownerPlayer == null || _ownerPlayer.SurfaceChecker == null)
			{
				return PoseidonTridentImpactSurface.Stone;
			}
			return _ownerPlayer.SurfaceChecker.TryGetTerrainSurface(componentInParent.transform, hitPosition) switch
			{
				SurfaceChecker.SurfaceType.Sand => PoseidonTridentImpactSurface.Sand, 
				SurfaceChecker.SurfaceType.Grass => PoseidonTridentImpactSurface.Grass, 
				_ => PoseidonTridentImpactSurface.Stone, 
			};
		}

		private void ServerPlayImpact(PoseidonTridentImpactSurface surface, Vector3 hitPosition, Vector3 hitNormal)
		{
			if (base.IsServer)
			{
				ClientPlayImpactRpc(surface, hitPosition, hitNormal);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientPlayImpactRpc(PoseidonTridentImpactSurface surface, Vector3 hitPosition, Vector3 hitNormal)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				UnityEngine.Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3854281186u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in surface, default(FastBufferWriter.ForEnums));
				bufferWriter.WriteValueSafe(in hitPosition);
				bufferWriter.WriteValueSafe(in hitNormal);
				__endSendRpc(ref bufferWriter, 3854281186u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ParticleSystem particleSystem = surface switch
				{
					PoseidonTridentImpactSurface.Sand => _sandImpactVfx, 
					PoseidonTridentImpactSurface.Grass => _grassImpactVfx, 
					PoseidonTridentImpactSurface.Fish => _fishImpactVfx, 
					PoseidonTridentImpactSurface.Player => _playerImpactVfx, 
					_ => _stoneImpactVfx, 
				};
				SoundType type = surface switch
				{
					PoseidonTridentImpactSurface.Sand => SoundType.Harpoon_Hit_Sand, 
					PoseidonTridentImpactSurface.Grass => SoundType.Harpoon_Hit_Sand, 
					PoseidonTridentImpactSurface.Fish => SoundType.Harpoon_Hit_Flesh, 
					PoseidonTridentImpactSurface.Player => SoundType.Harpoon_Hit_Flesh, 
					_ => SoundType.Harpoon_Hit_Concrete, 
				};
				if (particleSystem != null)
				{
					Quaternion rotation = ((hitNormal.sqrMagnitude > 0.0001f) ? Quaternion.LookRotation(hitNormal) : Quaternion.identity);
					ParticleSystem particleSystem2 = UnityEngine.Object.Instantiate(particleSystem, hitPosition, rotation);
					particleSystem2.Play(withChildren: true);
					UnityEngine.Object.Destroy(particleSystem2.gameObject, 5f);
				}
				if (Singleton<SoundManager>.Instance != null)
				{
					Singleton<SoundManager>.Instance.PlaySFX(type, hitPosition);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientPlayWaterImpactRpc(Vector3 impactPosition, float impactSpeed, bool isBigImpact)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				UnityEngine.Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1233123143u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in impactPosition);
				bufferWriter.WriteValueSafe(in impactSpeed, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in isBigImpact, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1233123143u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Singleton<OceanFXManager>.Instance?.PlayObjectWaterImpact(impactPosition, impactSpeed, isBigImpact);
			}
		}

		private static void SortHitsByDistance(RaycastHit[] hits, int hitCount)
		{
			for (int i = 1; i < hitCount; i++)
			{
				RaycastHit raycastHit = hits[i];
				int num = i - 1;
				while (num >= 0 && hits[num].distance > raycastHit.distance)
				{
					hits[num + 1] = hits[num];
					num--;
				}
				hits[num + 1] = raycastHit;
			}
		}

		[Conditional("UNITY_EDITOR")]
		[Conditional("DEVELOPMENT_BUILD")]
		private void CheckHitBufferCapacity(int hitCount)
		{
		}

		private void SetUnderwaterTrailActive(bool active)
		{
			if (base.IsServer && _underwaterTrailActive.Value != active)
			{
				_underwaterTrailActive.Value = active;
			}
		}

		private void OnUnderwaterTrailActiveChanged(bool previousValue, bool currentValue)
		{
			ApplyUnderwaterTrailState(currentValue);
		}

		private void ApplyUnderwaterTrailState(bool active)
		{
			if (_underwaterTrail == null)
			{
				return;
			}
			if (active)
			{
				if (!_underwaterTrail.isPlaying)
				{
					_underwaterTrail.Play(withChildren: true);
				}
			}
			else
			{
				_underwaterTrail.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
			}
		}

		private bool IsOwnerCollider(Collider collider)
		{
			PPG_Player componentInParent = collider.GetComponentInParent<PPG_Player>();
			if (componentInParent != null)
			{
				return componentInParent == _ownerPlayer;
			}
			return false;
		}

		private bool IsProjectileCollider(Collider collider)
		{
			if (!(collider.transform == base.transform))
			{
				return collider.transform.IsChildOf(base.transform);
			}
			return true;
		}

		private void ServerStop()
		{
			if (!_stopped && !_returning)
			{
				_stopped = true;
				SetUnderwaterTrailActive(active: false);
				DisableOutboundPhysics();
				_ownerTool?.ServerSetProjectileState(this, PoseidonTridentState.Stopped);
			}
		}

		private void ServerDespawnUnexpected()
		{
			if (base.NetworkObject != null && base.NetworkObject.IsSpawned)
			{
				base.NetworkObject.Despawn();
			}
		}

		protected override void __initializeVariables()
		{
			if (_outboundVisualPosition == null)
			{
				throw new Exception("PoseidonTridentProjectile._outboundVisualPosition cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_outboundVisualPosition.Initialize(this);
			__nameNetworkVariable(_outboundVisualPosition, "_outboundVisualPosition");
			NetworkVariableFields.Add(_outboundVisualPosition);
			if (_outboundVisualRotation == null)
			{
				throw new Exception("PoseidonTridentProjectile._outboundVisualRotation cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_outboundVisualRotation.Initialize(this);
			__nameNetworkVariable(_outboundVisualRotation, "_outboundVisualRotation");
			NetworkVariableFields.Add(_outboundVisualRotation);
			if (_outboundVisualScale == null)
			{
				throw new Exception("PoseidonTridentProjectile._outboundVisualScale cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_outboundVisualScale.Initialize(this);
			__nameNetworkVariable(_outboundVisualScale, "_outboundVisualScale");
			NetworkVariableFields.Add(_outboundVisualScale);
			if (_returnVisualPosition == null)
			{
				throw new Exception("PoseidonTridentProjectile._returnVisualPosition cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_returnVisualPosition.Initialize(this);
			__nameNetworkVariable(_returnVisualPosition, "_returnVisualPosition");
			NetworkVariableFields.Add(_returnVisualPosition);
			if (_returnVisualRotation == null)
			{
				throw new Exception("PoseidonTridentProjectile._returnVisualRotation cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_returnVisualRotation.Initialize(this);
			__nameNetworkVariable(_returnVisualRotation, "_returnVisualRotation");
			NetworkVariableFields.Add(_returnVisualRotation);
			if (_returnVisualScale == null)
			{
				throw new Exception("PoseidonTridentProjectile._returnVisualScale cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_returnVisualScale.Initialize(this);
			__nameNetworkVariable(_returnVisualScale, "_returnVisualScale");
			NetworkVariableFields.Add(_returnVisualScale);
			if (_returnVisualActive == null)
			{
				throw new Exception("PoseidonTridentProjectile._returnVisualActive cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_returnVisualActive.Initialize(this);
			__nameNetworkVariable(_returnVisualActive, "_returnVisualActive");
			NetworkVariableFields.Add(_returnVisualActive);
			if (_underwaterTrailActive == null)
			{
				throw new Exception("PoseidonTridentProjectile._underwaterTrailActive cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_underwaterTrailActive.Initialize(this);
			__nameNetworkVariable(_underwaterTrailActive, "_underwaterTrailActive");
			NetworkVariableFields.Add(_underwaterTrailActive);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3854281186u, __rpc_handler_3854281186, "ClientPlayImpactRpc", RpcInvokePermission.Everyone);
			__registerRpc(1233123143u, __rpc_handler_1233123143, "ClientPlayWaterImpactRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3854281186(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out PoseidonTridentImpactSurface value, default(FastBufferWriter.ForEnums));
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out Vector3 value3);
				((PoseidonTridentProjectile)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PoseidonTridentProjectile)target).ClientPlayImpactRpc(value, value2, value3);
				((PoseidonTridentProjectile)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1233123143(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
				((PoseidonTridentProjectile)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PoseidonTridentProjectile)target).ClientPlayWaterImpactRpc(value, value2, value3);
				((PoseidonTridentProjectile)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "PoseidonTridentProjectile";
		}
	}
}
