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
	public sealed class CrabBossStalactiteDropAttack : CrabBossAttackBase
	{
		private sealed class ServerFallingRock
		{
			public bool Active;

			public Vector3 StartPosition;

			public Vector3 TelegraphPosition;

			public Vector3 PreviousPosition;

			public Quaternion Rotation;

			public double FallStartTime;

			public double ExpireTime;

			public void Begin(Vector3 startPosition, Vector3 telegraphPosition, Quaternion rotation, double fallStartTime, double expireTime)
			{
				Active = true;
				StartPosition = startPosition;
				TelegraphPosition = telegraphPosition;
				PreviousPosition = startPosition;
				Rotation = rotation;
				FallStartTime = fallStartTime;
				ExpireTime = expireTime;
			}

			public void Reset()
			{
				Active = false;
			}
		}

		[Header("Local Pool")]
		[SerializeField]
		private CrabBossRockPool _rockPool;

		[SerializeField]
		private Transform _spawnHeightReference;

		[Header("Timing")]
		[SerializeField]
		[Min(0f)]
		private float _telegraphDuration = 2f;

		[SerializeField]
		[Min(0.01f)]
		private float _fallSpeed = 4f;

		[SerializeField]
		[Min(0.01f)]
		private float _maximumLifetime = 15f;

		[SerializeField]
		[Min(0f)]
		private float _groundReleaseDelay = 1.5f;

		[Header("Collision")]
		[SerializeField]
		private LayerMask _worldImpactLayers;

		[SerializeField]
		[Min(0.01f)]
		private float _hitRadius = 1.5f;

		[SerializeField]
		private float _playerHitCenterOffset = 1f;

		[Header("Player Hit")]
		[SerializeField]
		private byte _oxygenDamage = 50;

		[SerializeField]
		private bool _playerHitCameraShake = true;

		[SerializeField]
		private ShakeIntensity _playerHitShakeIntensity = ShakeIntensity.Medium;

		[SerializeField]
		private ShakeDuration _playerHitShakeDuration = ShakeDuration.Mid;

		[Header("Debug")]
		[SerializeField]
		private bool _log;

		private readonly List<PPG_Player> _targetPlayers = new List<PPG_Player>();

		private readonly List<PPG_Player> _collisionPlayers = new List<PPG_Player>();

		private readonly List<ServerFallingRock> _serverRocks = new List<ServerFallingRock>();

		private readonly List<double> _slotReleaseTimes = new List<double>();

		private int _activeRockCount;

		private bool _fallingStarted;

		private uint _localAttackToken;

		protected override void Awake()
		{
			base.Awake();
			ResolveRockPool();
		}

		private void FixedUpdate()
		{
			if (base.IsServer && base.ActiveToken != 0 && _fallingStarted && !(base.Boss == null) && base.Boss.IsAttackRunning && base.Boss.CurrentAttackId == CrabBossAttackId.StalactiteDrop && base.Boss.CurrentAttackToken == base.ActiveToken && !(base.NetworkManager == null))
			{
				ServerAdvanceFallingRocks(base.NetworkManager.ServerTime.Time);
			}
		}

		public override void OnNetworkDespawn()
		{
			ClearServerRuntime();
			_localAttackToken = 0u;
			_rockPool?.ResetFallingRocks();
			base.OnNetworkDespawn();
		}

		public override void ServerSyncLateJoinClient(ulong clientId)
		{
			if (!base.IsServer || base.ActiveToken == 0 || base.Boss == null || !base.Boss.IsAttackRunning || base.Boss.CurrentAttackId != CrabBossAttackId.StalactiteDrop || base.Boss.CurrentAttackToken != base.ActiveToken)
			{
				return;
			}
			ClientBeginAttackForLateJoinRpc(base.ActiveToken, base.RpcTarget.Single(clientId, RpcTargetUse.Temp));
			for (int i = 0; i < _serverRocks.Count; i++)
			{
				ServerFallingRock serverFallingRock = _serverRocks[i];
				if (serverFallingRock.Active)
				{
					ClientBeginFallingRockForLateJoinRpc(base.ActiveToken, i, serverFallingRock.StartPosition, serverFallingRock.TelegraphPosition, serverFallingRock.Rotation, serverFallingRock.FallStartTime, _fallSpeed, base.RpcTarget.Single(clientId, RpcTargetUse.Temp));
				}
			}
		}

		protected override void OnServerAttackStarted(CrabBossAttackCommand command)
		{
			ClearServerRuntime();
			ResolveRockPool();
			if (_rockPool == null)
			{
				CompleteAttack(command.Token);
				return;
			}
			int num = base.Boss.ServerCollectPlayersInRoom(_targetPlayers);
			if (num <= 0)
			{
				CompleteAttack(command.Token);
				return;
			}
			EnsureServerSlotCapacity();
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

		public void AnimationEventStartFallingRocks()
		{
			if (base.IsSpawned && base.IsServer && !(base.Boss == null) && base.Boss.LocalAttackAnimationToken == base.ActiveToken)
			{
				ServerStartFallingRocksFromAnimationEvent();
			}
		}

		private void ServerStartFallingRocksFromAnimationEvent()
		{
			if (!base.IsServer || _fallingStarted || base.Boss == null || !base.Boss.IsAttackRunning || base.Boss.CurrentAttackId != CrabBossAttackId.StalactiteDrop || base.Boss.CurrentAttackToken != base.ActiveToken || base.NetworkManager == null)
			{
				return;
			}
			_fallingStarted = true;
			EnsureServerSlotCapacity();
			double time = base.NetworkManager.ServerTime.Time;
			double num = time + (double)_telegraphDuration;
			int num2 = 0;
			for (int i = 0; i < _targetPlayers.Count; i++)
			{
				PPG_Player pPG_Player = _targetPlayers[i];
				if (IsValidTarget(pPG_Player))
				{
					if (!TryAcquireFallingRockSlot(time, out var slotIndex))
					{
						Log($"Falling rock skipped. Pool is full. TargetIndex={i}.");
						continue;
					}
					Vector3 vector = CrabBossRockCollisionUtility.ResolvePlayerHitCenter(pPG_Player, 0f);
					Vector3 vector2 = new Vector3(vector.x, _spawnHeightReference.position.y, vector.z);
					Quaternion rotation = _rockPool.GetFallingRockSlot(slotIndex).transform.rotation;
					_serverRocks[slotIndex].Begin(vector2, vector, rotation, num, num + (double)_maximumLifetime);
					_activeRockCount++;
					num2++;
					ClientBeginFallingRockRpc(base.ActiveToken, slotIndex, vector2, vector, rotation, num, _fallSpeed);
					Log($"Falling rock started. Slot={slotIndex} Target={pPG_Player.OwnerClientId} " + $"Spawn={vector2} Telegraph={vector} Speed={_fallSpeed} " + $"Radius={_hitRadius} WorldMask={_worldImpactLayers.value}.");
				}
			}
			Log($"Started {num2} local falling rocks. Token={base.ActiveToken}.");
			if (num2 <= 0)
			{
				CompleteAttack(base.ActiveToken);
			}
		}

		private void ServerAdvanceFallingRocks(double now)
		{
			if (_activeRockCount <= 0)
			{
				CompleteAttack(base.ActiveToken);
				return;
			}
			base.Boss.ServerCollectPlayersInRoom(_collisionPlayers);
			for (int i = 0; i < _serverRocks.Count; i++)
			{
				ServerFallingRock serverFallingRock = _serverRocks[i];
				if (!serverFallingRock.Active || now < serverFallingRock.FallStartTime)
				{
					continue;
				}
				float num = Mathf.Min(_maximumLifetime, (float)(now - serverFallingRock.FallStartTime));
				Vector3 vector = serverFallingRock.StartPosition + Vector3.down * (_fallSpeed * num);
				if (CrabBossRockCollisionUtility.TryFindFirstImpact(base.Boss, _collisionPlayers, serverFallingRock.PreviousPosition, vector, _playerHitCenterOffset, _hitRadius, _worldImpactLayers, out var result))
				{
					if (result.Player != null && base.Boss.ServerTryApplyContactHit(result.Player, serverFallingRock.StartPosition, _oxygenDamage, 0f, 0f) && _playerHitCameraShake)
					{
						ClientPlayPlayerHitCameraShakeRpc(result.Player.NetworkObject, _playerHitShakeIntensity, _playerHitShakeDuration, base.RpcTarget.Single(result.Player.OwnerClientId, RpcTargetUse.Temp));
					}
					LogImpact(i, result);
					ResolveFallingRock(i, result.ImpactType, result.Position, result.Normal, now);
				}
				else
				{
					serverFallingRock.PreviousPosition = vector;
					if (now >= serverFallingRock.ExpireTime)
					{
						Log("Falling rock lifetime expired without an impact. " + $"Slot={i} Lifetime={_maximumLifetime}.");
						ResolveFallingRock(i, CrabBossRockImpactType.None, vector, Vector3.up, now);
					}
				}
			}
			if (_activeRockCount <= 0)
			{
				CompleteAttack(base.ActiveToken);
			}
		}

		private void ResolveFallingRock(int slotIndex, CrabBossRockImpactType impactType, Vector3 position, Vector3 normal, double now, bool usePhysicsPose = false, Vector3 physicsPosition = default(Vector3), Quaternion physicsRotation = default(Quaternion))
		{
			if (slotIndex >= 0 && slotIndex < _serverRocks.Count && _serverRocks[slotIndex].Active)
			{
				_serverRocks[slotIndex].Reset();
				_activeRockCount = Mathf.Max(0, _activeRockCount - 1);
				CrabBossLocalRock crabBossLocalRock = _rockPool?.GetFallingRockSlot(slotIndex);
				double num = ((impactType == CrabBossRockImpactType.None || crabBossLocalRock == null) ? 0.0 : ((double)Mathf.Max(crabBossLocalRock.ImpactVfxDuration, _groundReleaseDelay)));
				_slotReleaseTimes[slotIndex] = now + num;
				ClientResolveFallingRockRpc(base.ActiveToken, slotIndex, impactType, position, normal, usePhysicsPose, physicsPosition, physicsRotation);
			}
		}

		private bool TryAcquireFallingRockSlot(double now, out int slotIndex)
		{
			slotIndex = -1;
			for (int i = 0; i < _serverRocks.Count; i++)
			{
				if (!_serverRocks[i].Active && now >= _slotReleaseTimes[i] && _rockPool.GetFallingRockSlot(i) != null)
				{
					slotIndex = i;
					return true;
				}
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
				FastBufferWriter bufferWriter = __beginSendRpc(4038655524u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				__endSendRpc(ref bufferWriter, 4038655524u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				BeginLocalAttack(token);
			}
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientBeginAttackForLateJoinRpc(uint token, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(4199642430u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				__endSendRpc(ref bufferWriter, 4199642430u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				BeginLocalAttack(token);
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
				FastBufferWriter bufferWriter = __beginSendRpc(2140891449u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in playerReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in intensity, default(FastBufferWriter.ForEnums));
				bufferWriter.WriteValueSafe(in duration, default(FastBufferWriter.ForEnums));
				__endSendRpc(ref bufferWriter, 2140891449u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
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
		private void ClientBeginFallingRockRpc(uint token, int slotIndex, Vector3 spawnPosition, Vector3 telegraphPosition, Quaternion rotation, double fallStartTime, float fallSpeed)
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
				FastBufferWriter bufferWriter = __beginSendRpc(4008290296u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				BytePacker.WriteValueBitPacked(bufferWriter, slotIndex);
				bufferWriter.WriteValueSafe(in spawnPosition);
				bufferWriter.WriteValueSafe(in telegraphPosition);
				bufferWriter.WriteValueSafe(in rotation);
				bufferWriter.WriteValueSafe(in fallStartTime, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in fallSpeed, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 4008290296u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				BeginLocalFallingRock(token, slotIndex, spawnPosition, telegraphPosition, rotation, fallStartTime, fallSpeed);
			}
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientBeginFallingRockForLateJoinRpc(uint token, int slotIndex, Vector3 spawnPosition, Vector3 telegraphPosition, Quaternion rotation, double fallStartTime, float fallSpeed, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(156221914u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				BytePacker.WriteValueBitPacked(bufferWriter, slotIndex);
				bufferWriter.WriteValueSafe(in spawnPosition);
				bufferWriter.WriteValueSafe(in telegraphPosition);
				bufferWriter.WriteValueSafe(in rotation);
				bufferWriter.WriteValueSafe(in fallStartTime, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in fallSpeed, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 156221914u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				BeginLocalFallingRock(token, slotIndex, spawnPosition, telegraphPosition, rotation, fallStartTime, fallSpeed);
			}
		}

		private void BeginLocalAttack(uint token)
		{
			if (base.IsClient && token != 0)
			{
				ResolveRockPool();
				_rockPool?.ResetFallingRocks();
				_localAttackToken = token;
			}
		}

		private void BeginLocalFallingRock(uint token, int slotIndex, Vector3 spawnPosition, Vector3 telegraphPosition, Quaternion rotation, double fallStartTime, float fallSpeed)
		{
			if (base.IsClient && token != 0 && token == _localAttackToken)
			{
				CrabBossLocalRock obj = _rockPool?.GetFallingRockSlot(slotIndex);
				obj?.SetIgnoredCollisionRoot((base.Boss != null) ? base.Boss.transform : null);
				obj?.SetWorldImpactLayers(_worldImpactLayers);
				obj?.SetWorldImpactSurfaceAlignment(enabled: false);
				obj?.BeginFall(spawnPosition, telegraphPosition, rotation, fallStartTime, fallSpeed);
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientResolveFallingRockRpc(uint token, int slotIndex, CrabBossRockImpactType impactType, Vector3 position, Vector3 normal, bool usePhysicsPose, Vector3 physicsPosition, Quaternion physicsRotation)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3427906269u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				BytePacker.WriteValueBitPacked(bufferWriter, slotIndex);
				bufferWriter.WriteValueSafe(in impactType, default(FastBufferWriter.ForEnums));
				bufferWriter.WriteValueSafe(in position);
				bufferWriter.WriteValueSafe(in normal);
				bufferWriter.WriteValueSafe(in usePhysicsPose, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in physicsPosition);
				bufferWriter.WriteValueSafe(in physicsRotation);
				__endSendRpc(ref bufferWriter, 3427906269u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsClient && token != 0 && token == _localAttackToken)
			{
				CrabBossLocalRock crabBossLocalRock = _rockPool?.GetFallingRockSlot(slotIndex);
				Log($"Local falling-rock resolve received. Slot={slotIndex} " + $"Impact={impactType} Position={position} SlotFound={crabBossLocalRock != null} " + $"PlayerVfx={crabBossLocalRock != null && crabBossLocalRock.HasPlayerImpactVisual} " + $"WorldVfx={crabBossLocalRock != null && crabBossLocalRock.HasWorldImpactVisual} " + $"Sfx={crabBossLocalRock != null && crabBossLocalRock.HasImpactSfx}.");
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
				FastBufferWriter bufferWriter = __beginSendRpc(852373639u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				bufferWriter.WriteValueSafe(in clearVisuals, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 852373639u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsClient && token != 0 && token == _localAttackToken)
			{
				if (clearVisuals)
				{
					_rockPool?.ResetFallingRocks();
				}
				_localAttackToken = 0u;
			}
		}

		protected override bool ValidateSpecificSettings(out string error)
		{
			ResolveRockPool();
			if (base.AttackId != CrabBossAttackId.StalactiteDrop)
			{
				error = "Attack ID must be StalactiteDrop.";
				return false;
			}
			if (_rockPool == null)
			{
				error = "BossArea CrabBossRockPool is missing.";
				return false;
			}
			if (!_rockPool.ValidateFallingRockConfiguration(out error))
			{
				return false;
			}
			if (_spawnHeightReference == null)
			{
				error = "Spawn height reference is missing.";
				return false;
			}
			if (!IsNonNegativeFinite(_telegraphDuration))
			{
				error = "Telegraph duration must be zero or greater and finite.";
				return false;
			}
			if (!IsPositiveFinite(_fallSpeed))
			{
				error = "Fall speed must be greater than zero and finite.";
				return false;
			}
			if (!IsPositiveFinite(_maximumLifetime))
			{
				error = "Maximum lifetime must be greater than zero and finite.";
				return false;
			}
			if (!IsNonNegativeFinite(_groundReleaseDelay))
			{
				error = "Ground release delay must be zero or greater and finite.";
				return false;
			}
			if (_worldImpactLayers.value == 0)
			{
				error = "World impact layers are empty.";
				return false;
			}
			if (!IsPositiveFinite(_hitRadius) || !IsFinite(_playerHitCenterOffset))
			{
				error = "Falling rock hit settings are invalid.";
				return false;
			}
			if (_oxygenDamage == 0)
			{
				error = "Falling rock oxygen damage must be greater than zero.";
				return false;
			}
			error = null;
			return true;
		}

		private bool IsValidTarget(PPG_Player player)
		{
			if (player != null && player.IsSpawned && base.Boss != null)
			{
				return base.Boss.ServerIsPlayerInRoom(player.OwnerClientId);
			}
			return false;
		}

		private void EnsureServerSlotCapacity()
		{
			int num = _rockPool?.FallingRockSlotCount ?? 0;
			while (_serverRocks.Count < num)
			{
				_serverRocks.Add(new ServerFallingRock());
			}
			while (_slotReleaseTimes.Count < num)
			{
				_slotReleaseTimes.Add(0.0);
			}
		}

		private void ClearServerRuntime()
		{
			for (int i = 0; i < _serverRocks.Count; i++)
			{
				_serverRocks[i].Reset();
			}
			for (int j = 0; j < _slotReleaseTimes.Count; j++)
			{
				_slotReleaseTimes[j] = 0.0;
			}
			_targetPlayers.Clear();
			_collisionPlayers.Clear();
			_activeRockCount = 0;
			_fallingStarted = false;
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
				UtilityMethods.Log("[CrabBoss][StalactiteDropAttack] " + message);
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(4038655524u, __rpc_handler_4038655524, "ClientBeginAttackRpc", RpcInvokePermission.Server);
			__registerRpc(4199642430u, __rpc_handler_4199642430, "ClientBeginAttackForLateJoinRpc", RpcInvokePermission.Server);
			__registerRpc(2140891449u, __rpc_handler_2140891449, "ClientPlayPlayerHitCameraShakeRpc", RpcInvokePermission.Server);
			__registerRpc(4008290296u, __rpc_handler_4008290296, "ClientBeginFallingRockRpc", RpcInvokePermission.Server);
			__registerRpc(156221914u, __rpc_handler_156221914, "ClientBeginFallingRockForLateJoinRpc", RpcInvokePermission.Server);
			__registerRpc(3427906269u, __rpc_handler_3427906269, "ClientResolveFallingRockRpc", RpcInvokePermission.Server);
			__registerRpc(852373639u, __rpc_handler_852373639, "ClientStopAttackRpc", RpcInvokePermission.Server);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_4038655524(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				((CrabBossStalactiteDropAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossStalactiteDropAttack)target).ClientBeginAttackRpc(value);
				((CrabBossStalactiteDropAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4199642430(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				RpcParams ext = rpcParams.Ext;
				((CrabBossStalactiteDropAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossStalactiteDropAttack)target).ClientBeginAttackForLateJoinRpc(value, ext);
				((CrabBossStalactiteDropAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2140891449(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out ShakeIntensity value2, default(FastBufferWriter.ForEnums));
				reader.ReadValueSafe(out ShakeDuration value3, default(FastBufferWriter.ForEnums));
				RpcParams ext = rpcParams.Ext;
				((CrabBossStalactiteDropAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossStalactiteDropAttack)target).ClientPlayPlayerHitCameraShakeRpc(value, value2, value3, ext);
				((CrabBossStalactiteDropAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4008290296(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				reader.ReadValueSafe(out Vector3 value3);
				reader.ReadValueSafe(out Vector3 value4);
				reader.ReadValueSafe(out Quaternion value5);
				reader.ReadValueSafe(out double value6, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value7, default(FastBufferWriter.ForPrimitives));
				((CrabBossStalactiteDropAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossStalactiteDropAttack)target).ClientBeginFallingRockRpc(value, value2, value3, value4, value5, value6, value7);
				((CrabBossStalactiteDropAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_156221914(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				reader.ReadValueSafe(out Vector3 value3);
				reader.ReadValueSafe(out Vector3 value4);
				reader.ReadValueSafe(out Quaternion value5);
				reader.ReadValueSafe(out double value6, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value7, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((CrabBossStalactiteDropAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossStalactiteDropAttack)target).ClientBeginFallingRockForLateJoinRpc(value, value2, value3, value4, value5, value6, value7, ext);
				((CrabBossStalactiteDropAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3427906269(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
				((CrabBossStalactiteDropAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossStalactiteDropAttack)target).ClientResolveFallingRockRpc(value, value2, value3, value4, value5, value6, value7, value8);
				((CrabBossStalactiteDropAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_852373639(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				((CrabBossStalactiteDropAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossStalactiteDropAttack)target).ClientStopAttackRpc(value, value2);
				((CrabBossStalactiteDropAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "CrabBossStalactiteDropAttack";
		}
	}
}
