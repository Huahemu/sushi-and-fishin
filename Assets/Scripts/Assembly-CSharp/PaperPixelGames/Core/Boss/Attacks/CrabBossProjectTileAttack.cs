using System.Collections;
using System.Collections.Generic;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Boss.Attacks
{
	[RequireComponent(typeof(CrabBossPresentationController))]
	public sealed class CrabBossProjectTileAttack : CrabBossAttackBase
	{
		private sealed class ServerProjectile
		{
			public bool Active;

			public Vector3 StartPosition;

			public Vector3 EndPosition;

			public Vector3 PreviousPosition;

			public Quaternion Rotation;

			public double FlightStartTime;

			public double FlightEndTime;

			public void Begin(Vector3 startPosition, Vector3 endPosition, Quaternion rotation, double flightStartTime, double flightEndTime)
			{
				Active = true;
				StartPosition = startPosition;
				EndPosition = endPosition;
				PreviousPosition = startPosition;
				Rotation = rotation;
				FlightStartTime = flightStartTime;
				FlightEndTime = flightEndTime;
			}

			public void Reset()
			{
				Active = false;
			}
		}

		[Header("Local Pool")]
		[SerializeField]
		private CrabBossRockPool _rockPool;

		[Header("Launch")]
		[SerializeField]
		private Transform _launchPoint;

		[SerializeField]
		private Vector3 _visualRotationOffset;

		[SerializeField]
		[Min(0f)]
		private float _turnLeadTime = 0.35f;

		[Header("Flight")]
		[SerializeField]
		[Min(0f)]
		private float _networkLeadTime = 0.1f;

		[SerializeField]
		[Min(0.01f)]
		private float _projectileSpeed = 18f;

		[SerializeField]
		[Min(0.01f)]
		private float _maximumTravelDistance = 100f;

		[Header("Collision")]
		[SerializeField]
		private LayerMask _worldImpactLayers;

		[SerializeField]
		private float _hitCenterOffset = 1f;

		[Tooltip("Fallback world-space radius for slots without a mesh collider. Mesh slots use their scaled, rotated bounds.")]
		[SerializeField]
		[Min(0.01f)]
		private float _hitRadius = 0.8f;

		[Header("Player Hit")]
		[SerializeField]
		private byte _oxygenDamage = 20;

		[SerializeField]
		[Min(0.01f)]
		private float _knockbackImpulse = 10f;

		[SerializeField]
		[Min(0f)]
		private float _upwardForce = 2f;

		[SerializeField]
		private bool _playerHitCameraShake = true;

		[SerializeField]
		private ShakeIntensity _playerHitShakeIntensity = ShakeIntensity.Medium;

		[SerializeField]
		private ShakeDuration _playerHitShakeDuration = ShakeDuration.Mid;

		[Header("Debug")]
		[SerializeField]
		private bool _log;

		private readonly List<PPG_Player> _sequenceTargets = new List<PPG_Player>();

		private readonly List<PPG_Player> _collisionPlayers = new List<PPG_Player>();

		private readonly List<ServerProjectile> _serverProjectiles = new List<ServerProjectile>();

		private readonly List<double> _slotReleaseTimes = new List<double>();

		private int _currentTargetIndex;

		private int _activeProjectileCount;

		private int _sequenceIndex;

		private bool _releaseEventConsumed;

		private bool _finishEventConsumed;

		private bool _sequenceFinished;

		private uint _localAttackToken;

		private int _localSequenceIndex;

		private Coroutine _nextSequenceReplayCoroutine;

		public override float ClientAnimationStartDelay => _turnLeadTime;

		protected override void Awake()
		{
			base.Awake();
			ResolveRockPool();
		}

		private void FixedUpdate()
		{
			if (base.IsServer && base.ActiveToken != 0 && !(base.Boss == null) && base.Boss.IsAttackRunning && base.Boss.CurrentAttackId == CrabBossAttackId.ProjectTile && base.Boss.CurrentAttackToken == base.ActiveToken && !(base.NetworkManager == null))
			{
				ServerAdvanceProjectiles(base.NetworkManager.ServerTime.Time);
			}
		}

		public override void OnNetworkDespawn()
		{
			ClearServerRuntime();
			_localAttackToken = 0u;
			_rockPool?.ResetProjectiles();
			base.OnNetworkDespawn();
		}

		public override void ServerSyncLateJoinClient(ulong clientId)
		{
			if (!IsCurrentAttackValid())
			{
				return;
			}
			ClientBeginAttackForLateJoinRpc(base.ActiveToken, _sequenceIndex, base.RpcTarget.Single(clientId, RpcTargetUse.Temp));
			for (int i = 0; i < _serverProjectiles.Count; i++)
			{
				ServerProjectile serverProjectile = _serverProjectiles[i];
				if (serverProjectile.Active)
				{
					ClientBeginProjectileForLateJoinRpc(base.ActiveToken, _sequenceIndex, i, serverProjectile.StartPosition, serverProjectile.EndPosition, serverProjectile.Rotation, serverProjectile.FlightStartTime, serverProjectile.FlightEndTime, base.RpcTarget.Single(clientId, RpcTargetUse.Temp));
				}
			}
		}

		protected override void OnServerAttackStarted(CrabBossAttackCommand command)
		{
			ClearServerRuntime();
			ResolveRockPool();
			if (_rockPool == null)
			{
				Log("Completed because the local rock pool is missing.");
				CompleteAttack(command.Token);
				return;
			}
			int num = base.Boss.ServerCollectPlayersInRoom(_sequenceTargets);
			if (num <= 0)
			{
				Log($"Completed without targets. Token={command.Token}.");
				CompleteAttack(command.Token);
				return;
			}
			EnsureServerSlotCapacity();
			_currentTargetIndex = 0;
			_sequenceIndex = 0;
			_releaseEventConsumed = false;
			_finishEventConsumed = false;
			_sequenceFinished = false;
			UpdateFacingOverrideForCurrentTarget();
			ClientBeginAttackRpc(command.Token);
			Log($"Prepared. Token={command.Token} Targets={num}.");
		}

		protected override void OnServerAttackEnded(CrabBossAttackCommand command, CrabBossAttackEndReason reason)
		{
			ClearServerRuntime();
			ClientStopAttackRpc(command.Token, reason != CrabBossAttackEndReason.Completed);
			Log($"Ended. Token={command.Token} Reason={reason}.");
		}

		protected override void OnServerForceCleanup(uint activeToken)
		{
			ClearServerRuntime();
			if (activeToken != 0)
			{
				ClientStopAttackRpc(activeToken, clearVisuals: true);
			}
		}

		public void AnimationEventThrowProjectTile()
		{
			if (base.IsSpawned && base.IsServer && !(base.Boss == null) && base.Boss.LocalAttackAnimationToken == base.ActiveToken)
			{
				ServerConsumeThrowEvent();
			}
		}

		public void AnimationEventProjectTileFinished()
		{
			if (base.IsSpawned && base.IsServer && !(base.Boss == null) && base.Boss.LocalAttackAnimationToken == base.ActiveToken)
			{
				ServerConsumeAnimationFinishedEvent();
			}
		}

		private void ServerConsumeThrowEvent()
		{
			if (!IsCurrentAttackValid() || _releaseEventConsumed || _sequenceFinished || _nextSequenceReplayCoroutine != null)
			{
				return;
			}
			_releaseEventConsumed = true;
			PPG_Player currentValidTarget = GetCurrentValidTarget();
			if (currentValidTarget == null)
			{
				Log($"Throw skipped. SequenceIndex={_sequenceIndex} target is no longer valid.");
				return;
			}
			if (!TryAcquireProjectileSlot(out var slotIndex))
			{
				Log($"Throw skipped. Projectile pool is full. SequenceIndex={_sequenceIndex}.");
				return;
			}
			Vector3 position = _launchPoint.position;
			Vector3 vector = CrabBossRockCollisionUtility.ResolvePlayerHitCenter(currentValidTarget, _hitCenterOffset) - position;
			if (vector.sqrMagnitude <= Mathf.Epsilon)
			{
				vector = _launchPoint.forward;
			}
			vector.Normalize();
			Vector3 vector2 = position + vector * _maximumTravelDistance;
			Quaternion rotation = Quaternion.LookRotation(vector) * Quaternion.Euler(_visualRotationOffset);
			double num = base.NetworkManager.ServerTime.Time + (double)_networkLeadTime;
			double flightEndTime = num + (double)(_maximumTravelDistance / _projectileSpeed);
			_serverProjectiles[slotIndex].Begin(position, vector2, rotation, num, flightEndTime);
			_activeProjectileCount++;
			ClientBeginProjectileRpc(base.ActiveToken, _sequenceIndex, slotIndex, position, vector2, rotation, num, flightEndTime);
			Log($"Projectile started. Token={base.ActiveToken} Sequence={_sequenceIndex} " + $"Slot={slotIndex} Target={currentValidTarget.OwnerClientId} " + $"Start={position} End={vector2} Speed={_projectileSpeed} " + $"Radius={_hitRadius} WorldMask={_worldImpactLayers.value}.");
		}

		private void ServerConsumeAnimationFinishedEvent()
		{
			if (IsCurrentAttackValid() && !_finishEventConsumed && !_sequenceFinished && _releaseEventConsumed && _nextSequenceReplayCoroutine == null)
			{
				_finishEventConsumed = true;
				_currentTargetIndex++;
				while (_currentTargetIndex < _sequenceTargets.Count && !IsValidTarget(_sequenceTargets[_currentTargetIndex]))
				{
					_currentTargetIndex++;
				}
				if (_currentTargetIndex >= _sequenceTargets.Count)
				{
					_sequenceFinished = true;
					TryCompleteSequence();
					return;
				}
				_sequenceIndex++;
				_releaseEventConsumed = false;
				_finishEventConsumed = false;
				UpdateFacingOverrideForCurrentTarget();
				StartNextSequenceReplay(base.ActiveToken, _sequenceIndex);
			}
		}

		private void ServerAdvanceProjectiles(double now)
		{
			if (_activeProjectileCount <= 0)
			{
				TryCompleteSequence();
				return;
			}
			base.Boss.ServerCollectPlayersInRoom(_collisionPlayers);
			for (int i = 0; i < _serverProjectiles.Count; i++)
			{
				ServerProjectile serverProjectile = _serverProjectiles[i];
				if (!serverProjectile.Active || now < serverProjectile.FlightStartTime)
				{
					continue;
				}
				float b = (float)(serverProjectile.FlightEndTime - serverProjectile.FlightStartTime);
				Vector3 vector = Vector3.Lerp(serverProjectile.StartPosition, serverProjectile.EndPosition, (float)(now - serverProjectile.FlightStartTime) / Mathf.Max(0.001f, b));
				CrabBossLocalRock crabBossLocalRock = _rockPool?.GetProjectileSlot(i);
				if ((!(crabBossLocalRock != null) || !crabBossLocalRock.TryGetCollisionBox(serverProjectile.Rotation, out var centerOffset, out var halfExtents)) ? CrabBossRockCollisionUtility.TryFindFirstImpact(base.Boss, _collisionPlayers, serverProjectile.PreviousPosition, vector, _hitCenterOffset, _hitRadius, _worldImpactLayers, out var result) : CrabBossRockCollisionUtility.TryFindFirstBoxImpact(base.Boss, serverProjectile.PreviousPosition + centerOffset, vector + centerOffset, halfExtents, serverProjectile.Rotation, _worldImpactLayers, out result))
				{
					if (result.Player != null && base.Boss.ServerTryApplyContactHit(result.Player, serverProjectile.StartPosition, _oxygenDamage, _knockbackImpulse, _upwardForce) && _playerHitCameraShake)
					{
						ClientPlayPlayerHitCameraShakeRpc(result.Player.NetworkObject, _playerHitShakeIntensity, _playerHitShakeDuration, base.RpcTarget.Single(result.Player.OwnerClientId, RpcTargetUse.Temp));
					}
					LogImpact(i, result);
					ResolveProjectile(i, result.ImpactType, result.Position, result.Normal, now);
				}
				else
				{
					serverProjectile.PreviousPosition = vector;
					if (now >= serverProjectile.FlightEndTime)
					{
						Log("Projectile lifetime ended without an impact. " + $"Slot={i} End={serverProjectile.EndPosition}.");
						ResolveProjectile(i, CrabBossRockImpactType.None, serverProjectile.EndPosition, -(serverProjectile.Rotation * Vector3.forward), now);
					}
				}
			}
			TryCompleteSequence();
		}

		private void ResolveProjectile(int slotIndex, CrabBossRockImpactType impactType, Vector3 position, Vector3 normal, double now, bool usePhysicsPose = false, Vector3 physicsPosition = default(Vector3), Quaternion physicsRotation = default(Quaternion))
		{
			if (slotIndex >= 0 && slotIndex < _serverProjectiles.Count && _serverProjectiles[slotIndex].Active)
			{
				_serverProjectiles[slotIndex].Reset();
				_activeProjectileCount = Mathf.Max(0, _activeProjectileCount - 1);
				CrabBossLocalRock crabBossLocalRock = _rockPool?.GetProjectileSlot(slotIndex);
				_slotReleaseTimes[slotIndex] = now + ((impactType == CrabBossRockImpactType.None || crabBossLocalRock == null) ? 0.0 : ((double)crabBossLocalRock.ImpactVfxDuration));
				ClientResolveProjectileRpc(base.ActiveToken, slotIndex, impactType, position, normal, usePhysicsPose, physicsPosition, physicsRotation);
			}
		}

		private void TryCompleteSequence()
		{
			if (_sequenceFinished && _activeProjectileCount <= 0)
			{
				CompleteAttack(base.ActiveToken);
			}
		}

		private bool TryAcquireProjectileSlot(out int slotIndex)
		{
			slotIndex = -1;
			if (_rockPool == null || base.NetworkManager == null)
			{
				return false;
			}
			EnsureServerSlotCapacity();
			double time = base.NetworkManager.ServerTime.Time;
			for (int i = 0; i < _serverProjectiles.Count; i++)
			{
				if (!_serverProjectiles[i].Active && time >= _slotReleaseTimes[i] && _rockPool.GetProjectileSlot(i) != null)
				{
					slotIndex = i;
					return true;
				}
			}
			return false;
		}

		private PPG_Player GetCurrentValidTarget()
		{
			if (_currentTargetIndex < 0 || _currentTargetIndex >= _sequenceTargets.Count || !IsValidTarget(_sequenceTargets[_currentTargetIndex]))
			{
				return null;
			}
			return _sequenceTargets[_currentTargetIndex];
		}

		private bool IsValidTarget(PPG_Player player)
		{
			if (player != null && player.IsSpawned && base.Boss != null)
			{
				return base.Boss.ServerIsPlayerInRoom(player.OwnerClientId);
			}
			return false;
		}

		private bool IsCurrentAttackValid()
		{
			if (base.IsServer && base.ActiveToken != 0 && base.Boss != null && base.Boss.IsAttackRunning && base.Boss.CurrentAttackId == CrabBossAttackId.ProjectTile)
			{
				return base.Boss.CurrentAttackToken == base.ActiveToken;
			}
			return false;
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientBeginAttackRpc(uint token)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2313372710u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				__endSendRpc(ref bufferWriter, 2313372710u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				BeginLocalAttack(token);
			}
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientBeginAttackForLateJoinRpc(uint token, int sequenceIndex, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Server
				};
				FastBufferWriter bufferWriter = __beginSendRpc(767478520u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				BytePacker.WriteValueBitPacked(bufferWriter, sequenceIndex);
				__endSendRpc(ref bufferWriter, 767478520u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				BeginLocalAttack(token);
				_localSequenceIndex = sequenceIndex;
			}
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientPlayPlayerHitCameraShakeRpc(NetworkObjectReference playerReference, ShakeIntensity intensity, ShakeDuration duration, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Server
				};
				FastBufferWriter bufferWriter = __beginSendRpc(1064736799u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in playerReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in intensity, default(FastBufferWriter.ForEnums));
				bufferWriter.WriteValueSafe(in duration, default(FastBufferWriter.ForEnums));
				__endSendRpc(ref bufferWriter, 1064736799u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (playerReference.TryGetComponent<PPG_Player>(out var component))
				{
					component.CameraShake(intensity, duration);
				}
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientBeginProjectileRpc(uint token, int sequenceIndex, int slotIndex, Vector3 startPosition, Vector3 endPosition, Quaternion rotation, double flightStartTime, double flightEndTime)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1289622250u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				BytePacker.WriteValueBitPacked(bufferWriter, sequenceIndex);
				BytePacker.WriteValueBitPacked(bufferWriter, slotIndex);
				bufferWriter.WriteValueSafe(in startPosition);
				bufferWriter.WriteValueSafe(in endPosition);
				bufferWriter.WriteValueSafe(in rotation);
				bufferWriter.WriteValueSafe(in flightStartTime, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in flightEndTime, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1289622250u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				BeginLocalProjectile(token, sequenceIndex, slotIndex, startPosition, endPosition, rotation, flightStartTime, flightEndTime);
			}
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientBeginProjectileForLateJoinRpc(uint token, int sequenceIndex, int slotIndex, Vector3 startPosition, Vector3 endPosition, Quaternion rotation, double flightStartTime, double flightEndTime, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Server
				};
				FastBufferWriter bufferWriter = __beginSendRpc(1938665611u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				BytePacker.WriteValueBitPacked(bufferWriter, sequenceIndex);
				BytePacker.WriteValueBitPacked(bufferWriter, slotIndex);
				bufferWriter.WriteValueSafe(in startPosition);
				bufferWriter.WriteValueSafe(in endPosition);
				bufferWriter.WriteValueSafe(in rotation);
				bufferWriter.WriteValueSafe(in flightStartTime, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in flightEndTime, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1938665611u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				BeginLocalProjectile(token, sequenceIndex, slotIndex, startPosition, endPosition, rotation, flightStartTime, flightEndTime);
			}
		}

		private void BeginLocalAttack(uint token)
		{
			if ((base.IsClient || base.IsServer) && token != 0)
			{
				ResolveRockPool();
				_rockPool?.ResetProjectiles();
				_localAttackToken = token;
				_localSequenceIndex = 0;
			}
		}

		private void BeginLocalProjectile(uint token, int sequenceIndex, int slotIndex, Vector3 startPosition, Vector3 endPosition, Quaternion rotation, double flightStartTime, double flightEndTime)
		{
			if (base.IsClient && token != 0 && token == _localAttackToken && sequenceIndex >= 0)
			{
				ResolveRockPool();
				CrabBossLocalRock obj = _rockPool?.GetProjectileSlot(slotIndex);
				obj?.SetIgnoredCollisionRoot((base.Boss != null) ? base.Boss.transform : null);
				obj?.SetWorldImpactLayers(_worldImpactLayers);
				obj?.SetWorldImpactSurfaceAlignment(enabled: true);
				obj?.BeginFlight(startPosition, endPosition, rotation, flightStartTime, flightEndTime);
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientResolveProjectileRpc(uint token, int slotIndex, CrabBossRockImpactType impactType, Vector3 position, Vector3 normal, bool usePhysicsPose, Vector3 physicsPosition, Quaternion physicsRotation)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3475010593u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				BytePacker.WriteValueBitPacked(bufferWriter, slotIndex);
				bufferWriter.WriteValueSafe(in impactType, default(FastBufferWriter.ForEnums));
				bufferWriter.WriteValueSafe(in position);
				bufferWriter.WriteValueSafe(in normal);
				bufferWriter.WriteValueSafe(in usePhysicsPose, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in physicsPosition);
				bufferWriter.WriteValueSafe(in physicsRotation);
				__endSendRpc(ref bufferWriter, 3475010593u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsClient && token != 0 && token == _localAttackToken)
			{
				CrabBossLocalRock crabBossLocalRock = _rockPool?.GetProjectileSlot(slotIndex);
				Log($"Local projectile resolve received. Slot={slotIndex} " + $"Impact={impactType} Position={position} SlotFound={crabBossLocalRock != null} " + $"PlayerVfx={crabBossLocalRock != null && crabBossLocalRock.HasPlayerImpactVisual} " + $"WorldVfx={crabBossLocalRock != null && crabBossLocalRock.HasWorldImpactVisual} " + $"Sfx={crabBossLocalRock != null && crabBossLocalRock.HasImpactSfx}.");
				if (impactType == CrabBossRockImpactType.None)
				{
					crabBossLocalRock?.ResetSlot();
				}
				else if (usePhysicsPose)
				{
					crabBossLocalRock?.PlayPhysicsImpact(impactType, position, normal, physicsPosition, physicsRotation);
					base.Presentation?.PlayRockImpactSfx();
				}
				else
				{
					crabBossLocalRock?.PlayImpact(impactType, position, normal);
					base.Presentation?.PlayRockImpactSfx();
				}
			}
		}

		private void UpdateFacingOverrideForCurrentTarget()
		{
			PPG_Player currentValidTarget = GetCurrentValidTarget();
			if (currentValidTarget != null)
			{
				base.Boss?.ServerSetFacingOverride(currentValidTarget.OwnerClientId);
			}
			else
			{
				base.Boss?.ServerClearFacingOverride();
			}
		}

		private void StartNextSequenceReplay(uint token, int sequenceIndex)
		{
			StopNextSequenceReplay();
			if (_turnLeadTime <= 0f)
			{
				ClientReplayProjectTileAnimationRpc(token, sequenceIndex);
			}
			else
			{
				_nextSequenceReplayCoroutine = StartCoroutine(NextSequenceReplayCoroutine(token, sequenceIndex));
			}
		}

		private IEnumerator NextSequenceReplayCoroutine(uint token, int sequenceIndex)
		{
			yield return new WaitForSeconds(_turnLeadTime);
			_nextSequenceReplayCoroutine = null;
			if (IsCurrentAttackValid() && token == base.ActiveToken && sequenceIndex == _sequenceIndex && !_sequenceFinished)
			{
				ClientReplayProjectTileAnimationRpc(token, sequenceIndex);
			}
		}

		private void StopNextSequenceReplay()
		{
			if (_nextSequenceReplayCoroutine != null)
			{
				StopCoroutine(_nextSequenceReplayCoroutine);
				_nextSequenceReplayCoroutine = null;
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientReplayProjectTileAnimationRpc(uint token, int sequenceIndex)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3483650364u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				BytePacker.WriteValueBitPacked(bufferWriter, sequenceIndex);
				__endSendRpc(ref bufferWriter, 3483650364u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if ((base.IsClient || base.IsServer) && token != 0 && token == _localAttackToken && sequenceIndex > _localSequenceIndex)
				{
					_localSequenceIndex = sequenceIndex;
					base.Presentation?.PlayAttackAnimation(CrabBossAttackAnimation.ProjectTile);
				}
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientStopAttackRpc(uint token, bool clearVisuals)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(478306084u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				bufferWriter.WriteValueSafe(in clearVisuals, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 478306084u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if ((base.IsClient || base.IsServer) && token != 0 && token == _localAttackToken)
			{
				if (clearVisuals)
				{
					_rockPool?.ResetProjectiles();
				}
				_localAttackToken = 0u;
			}
		}

		protected override bool ValidateSpecificSettings(out string error)
		{
			ResolveRockPool();
			if (base.AttackId != CrabBossAttackId.ProjectTile)
			{
				error = "Attack ID must be ProjectTile.";
				return false;
			}
			if (_rockPool == null)
			{
				error = "BossArea CrabBossRockPool is missing.";
				return false;
			}
			if (!_rockPool.ValidateProjectileConfiguration(out error))
			{
				return false;
			}
			if (_launchPoint == null)
			{
				error = "ProjectTile launch point is missing.";
				return false;
			}
			if (!IsNonNegativeFinite(_networkLeadTime))
			{
				error = "Network lead time must be zero or greater and finite.";
				return false;
			}
			if (!IsNonNegativeFinite(_turnLeadTime))
			{
				error = "Turn lead time must be zero or greater and finite.";
				return false;
			}
			if (!IsPositiveFinite(_projectileSpeed))
			{
				error = "Projectile speed must be greater than zero and finite.";
				return false;
			}
			if (!IsPositiveFinite(_maximumTravelDistance))
			{
				error = "Maximum travel distance must be greater than zero and finite.";
				return false;
			}
			if (_worldImpactLayers.value == 0)
			{
				error = "World impact layers are empty.";
				return false;
			}
			if (!IsFinite(_hitCenterOffset) || !IsFiniteVector(_visualRotationOffset))
			{
				error = "Hit center and visual rotation offsets must be finite.";
				return false;
			}
			if (!IsPositiveFinite(_hitRadius))
			{
				error = "Hit radius must be greater than zero and finite.";
				return false;
			}
			if (_oxygenDamage == 0)
			{
				error = "Oxygen damage must be greater than zero.";
				return false;
			}
			if (!IsPositiveFinite(_knockbackImpulse))
			{
				error = "Knockback impulse must be greater than zero and finite.";
				return false;
			}
			if (!IsNonNegativeFinite(_upwardForce))
			{
				error = "Upward force must be zero or greater and finite.";
				return false;
			}
			error = null;
			return true;
		}

		private void EnsureServerSlotCapacity()
		{
			int num = _rockPool?.ProjectileSlotCount ?? 0;
			while (_serverProjectiles.Count < num)
			{
				_serverProjectiles.Add(new ServerProjectile());
			}
			while (_slotReleaseTimes.Count < num)
			{
				_slotReleaseTimes.Add(0.0);
			}
		}

		private void ClearServerRuntime()
		{
			StopNextSequenceReplay();
			base.Boss?.ServerClearFacingOverride();
			for (int i = 0; i < _serverProjectiles.Count; i++)
			{
				_serverProjectiles[i].Reset();
			}
			for (int j = 0; j < _slotReleaseTimes.Count; j++)
			{
				_slotReleaseTimes[j] = 0.0;
			}
			_sequenceTargets.Clear();
			_collisionPlayers.Clear();
			_currentTargetIndex = 0;
			_activeProjectileCount = 0;
			_sequenceIndex = 0;
			_releaseEventConsumed = false;
			_finishEventConsumed = false;
			_sequenceFinished = false;
		}

		private void ResolveRockPool()
		{
			if (_rockPool == null)
			{
				_rockPool = GetComponentInParent<CrabBossRockPool>(includeInactive: true);
			}
		}

		private static bool IsPositiveFinite(float value)
		{
			if (value > 0f)
			{
				return IsFinite(value);
			}
			return false;
		}

		private static bool IsNonNegativeFinite(float value)
		{
			if (value >= 0f)
			{
				return IsFinite(value);
			}
			return false;
		}

		private static bool IsFinite(float value)
		{
			if (!float.IsNaN(value))
			{
				return !float.IsInfinity(value);
			}
			return false;
		}

		private static bool IsFiniteVector(Vector3 value)
		{
			if (IsFinite(value.x) && IsFinite(value.y))
			{
				return IsFinite(value.z);
			}
			return false;
		}

		private void LogImpact(int slotIndex, CrabBossRockHit hit)
		{
			if (hit.ImpactType == CrabBossRockImpactType.World)
			{
				Collider worldCollider = hit.WorldCollider;
				int num = ((worldCollider != null) ? worldCollider.gameObject.layer : (-1));
				string arg = ((num >= 0) ? LayerMask.LayerToName(num) : "<missing>");
				Log($"World impact detected. Slot={slotIndex} " + "Collider=" + ((worldCollider != null) ? worldCollider.name : "<missing>") + " " + $"Layer={num} ({arg}) Position={hit.Position} " + $"Distance={hit.Distance}.");
			}
			else
			{
				Log($"Player impact detected. Slot={slotIndex} " + $"Player={((hit.Player != null) ? hit.Player.OwnerClientId : ulong.MaxValue)} " + $"Position={hit.Position} Distance={hit.Distance}.");
			}
		}

		private void Log(string message)
		{
			if (_log)
			{
				UtilityMethods.Log("[CrabBoss][ProjectTileAttack] " + message);
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(2313372710u, __rpc_handler_2313372710, "ClientBeginAttackRpc", RpcInvokePermission.Server);
			__registerRpc(767478520u, __rpc_handler_767478520, "ClientBeginAttackForLateJoinRpc", RpcInvokePermission.Server);
			__registerRpc(1064736799u, __rpc_handler_1064736799, "ClientPlayPlayerHitCameraShakeRpc", RpcInvokePermission.Server);
			__registerRpc(1289622250u, __rpc_handler_1289622250, "ClientBeginProjectileRpc", RpcInvokePermission.Server);
			__registerRpc(1938665611u, __rpc_handler_1938665611, "ClientBeginProjectileForLateJoinRpc", RpcInvokePermission.Server);
			__registerRpc(3475010593u, __rpc_handler_3475010593, "ClientResolveProjectileRpc", RpcInvokePermission.Server);
			__registerRpc(3483650364u, __rpc_handler_3483650364, "ClientReplayProjectTileAnimationRpc", RpcInvokePermission.Server);
			__registerRpc(478306084u, __rpc_handler_478306084, "ClientStopAttackRpc", RpcInvokePermission.Server);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_2313372710(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				((CrabBossProjectTileAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossProjectTileAttack)target).ClientBeginAttackRpc(value);
				((CrabBossProjectTileAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_767478520(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				RpcParams ext = rpcParams.Ext;
				((CrabBossProjectTileAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossProjectTileAttack)target).ClientBeginAttackForLateJoinRpc(value, value2, ext);
				((CrabBossProjectTileAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1064736799(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out ShakeIntensity value2, default(FastBufferWriter.ForEnums));
				reader.ReadValueSafe(out ShakeDuration value3, default(FastBufferWriter.ForEnums));
				RpcParams ext = rpcParams.Ext;
				((CrabBossProjectTileAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossProjectTileAttack)target).ClientPlayPlayerHitCameraShakeRpc(value, value2, value3, ext);
				((CrabBossProjectTileAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1289622250(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				ByteUnpacker.ReadValueBitPacked(reader, out int value3);
				reader.ReadValueSafe(out Vector3 value4);
				reader.ReadValueSafe(out Vector3 value5);
				reader.ReadValueSafe(out Quaternion value6);
				reader.ReadValueSafe(out double value7, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out double value8, default(FastBufferWriter.ForPrimitives));
				((CrabBossProjectTileAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossProjectTileAttack)target).ClientBeginProjectileRpc(value, value2, value3, value4, value5, value6, value7, value8);
				((CrabBossProjectTileAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1938665611(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				ByteUnpacker.ReadValueBitPacked(reader, out int value3);
				reader.ReadValueSafe(out Vector3 value4);
				reader.ReadValueSafe(out Vector3 value5);
				reader.ReadValueSafe(out Quaternion value6);
				reader.ReadValueSafe(out double value7, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out double value8, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((CrabBossProjectTileAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossProjectTileAttack)target).ClientBeginProjectileForLateJoinRpc(value, value2, value3, value4, value5, value6, value7, value8, ext);
				((CrabBossProjectTileAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3475010593(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				reader.ReadValueSafe(out CrabBossRockImpactType value3, default(FastBufferWriter.ForEnums));
				reader.ReadValueSafe(out Vector3 value4);
				reader.ReadValueSafe(out Vector3 value5);
				reader.ReadValueSafe(out bool value6, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out Vector3 value7);
				reader.ReadValueSafe(out Quaternion value8);
				((CrabBossProjectTileAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossProjectTileAttack)target).ClientResolveProjectileRpc(value, value2, value3, value4, value5, value6, value7, value8);
				((CrabBossProjectTileAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3483650364(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				((CrabBossProjectTileAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossProjectTileAttack)target).ClientReplayProjectTileAnimationRpc(value, value2);
				((CrabBossProjectTileAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_478306084(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				((CrabBossProjectTileAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossProjectTileAttack)target).ClientStopAttackRpc(value, value2);
				((CrabBossProjectTileAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "CrabBossProjectTileAttack";
		}
	}
}
