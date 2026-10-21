using System.Collections;
using System.Collections.Generic;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Boss.Attacks
{
	[RequireComponent(typeof(CrabBossPresentationController))]
	public sealed class CrabBossRockRainAttack : CrabBossAttackBase
	{
		private sealed class ServerRainRock
		{
			public bool Active;

			public Vector3 StartPosition;

			public Vector3 GroundPosition;

			public Vector3 PreviousPosition;

			public Quaternion Rotation;

			public double FallStartTime;

			public double ExpireTime;

			public double ReleaseTime;

			public void Begin(Vector3 position, Vector3 groundPosition, Quaternion rotation, double now, float lifetime)
			{
				Active = true;
				StartPosition = position;
				GroundPosition = groundPosition;
				PreviousPosition = position;
				Rotation = rotation;
				FallStartTime = now;
				ExpireTime = now + (double)lifetime;
			}
		}

		[Header("Local Pool and Area")]
		[SerializeField]
		private CrabBossRockPool _rockPool;

		[SerializeField]
		private BoxCollider _spawnArea;

		[SerializeField]
		private float _spawnHeight;

		[Header("Timing and Limits")]
		[SerializeField]
		[Min(0.01f)]
		private float _rainDuration = 8f;

		[SerializeField]
		[Min(0.01f)]
		private float _rockSpawnInterval = 0.5f;

		[SerializeField]
		[Min(0f)]
		private float _settleDuration = 2f;

		[SerializeField]
		[Range(1f, 10f)]
		private int _maximumActiveRocks = 10;

		[Header("Falling Rock Gameplay")]
		[SerializeField]
		[Min(0.01f)]
		private float _fallSpeed = 4f;

		[SerializeField]
		[Min(0.01f)]
		private float _maximumLifetime = 15f;

		[SerializeField]
		private LayerMask _worldImpactLayers;

		[SerializeField]
		[Min(0.01f)]
		private float _hitRadius = 1.5f;

		[SerializeField]
		private float _playerHitCenterOffset = 1f;

		[SerializeField]
		private byte _oxygenDamage = 50;

		[Header("Debug")]
		[SerializeField]
		private bool _log;

		private readonly List<ServerRainRock> _serverRocks = new List<ServerRainRock>();

		private readonly List<PPG_Player> _collisionPlayers = new List<PPG_Player>();

		private Coroutine _serverRainCoroutine;

		private int _activeRockCount;

		private uint _localAttackToken;

		protected override void Awake()
		{
			base.Awake();
			ResolveRockPool();
		}

		private void FixedUpdate()
		{
			if (base.IsServer && base.ActiveToken != 0 && !(base.Boss == null) && base.Boss.IsAttackRunning && base.Boss.CurrentAttackId == CrabBossAttackId.RockRain && base.Boss.CurrentAttackToken == base.ActiveToken && !(base.NetworkManager == null))
			{
				AdvanceServerRocks(base.NetworkManager.ServerTime.Time);
			}
		}

		public override void OnNetworkDespawn()
		{
			StopServerRain();
			ClearRuntime();
			_localAttackToken = 0u;
			_rockPool?.ResetFallingRocks();
			base.OnNetworkDespawn();
		}

		public override void ServerSyncLateJoinClient(ulong clientId)
		{
			if (!base.IsServer || base.ActiveToken == 0 || base.Boss == null || !base.Boss.IsAttackRunning || base.Boss.CurrentAttackId != CrabBossAttackId.RockRain || base.Boss.CurrentAttackToken != base.ActiveToken)
			{
				return;
			}
			ClientBeginRainForLateJoinRpc(base.ActiveToken, base.RpcTarget.Single(clientId, RpcTargetUse.Temp));
			for (int i = 0; i < _serverRocks.Count; i++)
			{
				ServerRainRock serverRainRock = _serverRocks[i];
				if (serverRainRock.Active)
				{
					ClientBeginRainRockForLateJoinRpc(base.ActiveToken, i, serverRainRock.StartPosition, serverRainRock.GroundPosition, serverRainRock.Rotation, serverRainRock.FallStartTime, _fallSpeed, base.RpcTarget.Single(clientId, RpcTargetUse.Temp));
				}
			}
		}

		protected override void OnServerAttackStarted(CrabBossAttackCommand command)
		{
			StopServerRain();
			ClearRuntime();
			ResolveRockPool();
			if (_rockPool == null)
			{
				CompleteAttack(command.Token);
				return;
			}
			EnsureSlotCapacity();
			ClientBeginRainRpc(command.Token);
			_serverRainCoroutine = StartCoroutine(ServerRockRainRoutine(command.Token));
		}

		protected override void OnServerAttackEnded(CrabBossAttackCommand command, CrabBossAttackEndReason reason)
		{
			StopServerRain();
			ClearRuntime();
			ClientStopRainRpc(command.Token);
		}

		protected override void OnServerForceCleanup(uint activeToken)
		{
			StopServerRain();
			ClearRuntime();
			if (activeToken != 0)
			{
				ClientStopRainRpc(activeToken);
			}
		}

		private IEnumerator ServerRockRainRoutine(uint token)
		{
			float elapsed = 0f;
			while (elapsed < _rainDuration)
			{
				if (_activeRockCount < _maximumActiveRocks)
				{
					StartRandomRock();
				}
				float waitElapsed = 0f;
				while (waitElapsed < _rockSpawnInterval && elapsed < _rainDuration)
				{
					float deltaTime = Time.deltaTime;
					waitElapsed += deltaTime;
					elapsed += deltaTime;
					yield return null;
				}
			}
			if (_settleDuration > 0f)
			{
				yield return new WaitForSeconds(_settleDuration);
			}
			while (HasUnfinishedRocks(base.NetworkManager.ServerTime.Time))
			{
				yield return null;
			}
			_serverRainCoroutine = null;
			if (base.IsServer && base.Boss != null && base.Boss.IsAttackRunning && base.Boss.CurrentAttackToken == token)
			{
				CompleteAttack(token);
			}
		}

		private void StartRandomRock()
		{
			if (!(_spawnArea == null) && !(base.NetworkManager == null) && TryAcquireSlot(out var slotIndex))
			{
				Vector3 vector = _spawnArea.size * 0.5f;
				Vector3 position = _spawnArea.center + new Vector3(Random.Range(0f - vector.x, vector.x), vector.y, Random.Range(0f - vector.z, vector.z));
				Vector3 vector2 = _spawnArea.transform.TransformPoint(position);
				Vector3 vector3 = vector2 + Vector3.up * _spawnHeight;
				double time = base.NetworkManager.ServerTime.Time;
				CrabBossLocalRock fallingRockSlot = _rockPool.GetFallingRockSlot(slotIndex);
				_serverRocks[slotIndex].Begin(vector3, vector2, fallingRockSlot.transform.rotation, time, _maximumLifetime);
				_activeRockCount++;
				ClientBeginRainRockRpc(base.ActiveToken, slotIndex, vector3, vector2, fallingRockSlot.transform.rotation, time, _fallSpeed);
			}
		}

		private void AdvanceServerRocks(double now)
		{
			base.Boss.ServerCollectPlayersInRoom(_collisionPlayers);
			for (int i = 0; i < _serverRocks.Count; i++)
			{
				ServerRainRock serverRainRock = _serverRocks[i];
				if (!serverRainRock.Active)
				{
					continue;
				}
				Vector3 vector = serverRainRock.StartPosition + Vector3.down * (_fallSpeed * Mathf.Clamp((float)(now - serverRainRock.FallStartTime), 0f, _maximumLifetime));
				if (CrabBossRockCollisionUtility.TryFindFirstImpact(base.Boss, _collisionPlayers, serverRainRock.PreviousPosition, vector, _playerHitCenterOffset, _hitRadius, _worldImpactLayers, out var result))
				{
					if (result.ImpactType == CrabBossRockImpactType.Player && result.Player != null)
					{
						base.Boss.ServerTryApplyContactHit(result.Player, serverRainRock.StartPosition, _oxygenDamage, 0f, 0f);
					}
					ResolveRock(i, result.ImpactType, result.Position, result.Normal);
				}
				else
				{
					serverRainRock.PreviousPosition = vector;
					if (now >= serverRainRock.ExpireTime)
					{
						ResolveRock(i, CrabBossRockImpactType.None, vector, Vector3.up);
					}
				}
			}
		}

		private void ResolveRock(int slotIndex, CrabBossRockImpactType impactType, Vector3 position, Vector3 normal)
		{
			ServerRainRock serverRainRock = _serverRocks[slotIndex];
			if (serverRainRock.Active)
			{
				serverRainRock.Active = false;
				CrabBossLocalRock fallingRockSlot = _rockPool.GetFallingRockSlot(slotIndex);
				serverRainRock.ReleaseTime = base.NetworkManager.ServerTime.Time + ((impactType == CrabBossRockImpactType.None || fallingRockSlot == null) ? 0.0 : ((double)fallingRockSlot.ImpactVfxDuration));
				_activeRockCount = Mathf.Max(0, _activeRockCount - 1);
				ClientResolveRainRockRpc(base.ActiveToken, slotIndex, impactType, position, normal);
			}
		}

		private bool TryAcquireSlot(out int slotIndex)
		{
			slotIndex = -1;
			for (int i = 0; i < _serverRocks.Count; i++)
			{
				if (!_serverRocks[i].Active && base.NetworkManager.ServerTime.Time >= _serverRocks[i].ReleaseTime && _rockPool.GetFallingRockSlot(i) != null)
				{
					slotIndex = i;
					return true;
				}
			}
			return false;
		}

		private bool HasUnfinishedRocks(double now)
		{
			for (int i = 0; i < _serverRocks.Count; i++)
			{
				if (_serverRocks[i].Active || now < _serverRocks[i].ReleaseTime)
				{
					return true;
				}
			}
			return false;
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientBeginRainRpc(uint token)
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
				FastBufferWriter bufferWriter = __beginSendRpc(159689037u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				__endSendRpc(ref bufferWriter, 159689037u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				BeginLocalRain(token);
			}
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientBeginRainForLateJoinRpc(uint token, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(932097817u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				__endSendRpc(ref bufferWriter, 932097817u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				BeginLocalRain(token);
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientBeginRainRockRpc(uint token, int slotIndex, Vector3 spawnPosition, Vector3 groundPosition, Quaternion rotation, double fallStartTime, float fallSpeed)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2264826521u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				BytePacker.WriteValueBitPacked(bufferWriter, slotIndex);
				bufferWriter.WriteValueSafe(in spawnPosition);
				bufferWriter.WriteValueSafe(in groundPosition);
				bufferWriter.WriteValueSafe(in rotation);
				bufferWriter.WriteValueSafe(in fallStartTime, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in fallSpeed, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2264826521u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				BeginLocalRainRock(token, slotIndex, spawnPosition, groundPosition, rotation, fallStartTime, fallSpeed);
			}
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientBeginRainRockForLateJoinRpc(uint token, int slotIndex, Vector3 spawnPosition, Vector3 groundPosition, Quaternion rotation, double fallStartTime, float fallSpeed, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2209830069u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				BytePacker.WriteValueBitPacked(bufferWriter, slotIndex);
				bufferWriter.WriteValueSafe(in spawnPosition);
				bufferWriter.WriteValueSafe(in groundPosition);
				bufferWriter.WriteValueSafe(in rotation);
				bufferWriter.WriteValueSafe(in fallStartTime, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in fallSpeed, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2209830069u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				BeginLocalRainRock(token, slotIndex, spawnPosition, groundPosition, rotation, fallStartTime, fallSpeed);
			}
		}

		private void BeginLocalRain(uint token)
		{
			if (base.IsClient && token != 0)
			{
				ResolveRockPool();
				_rockPool?.ResetFallingRocks();
				_localAttackToken = token;
			}
		}

		private void BeginLocalRainRock(uint token, int slotIndex, Vector3 spawnPosition, Vector3 groundPosition, Quaternion rotation, double fallStartTime, float fallSpeed)
		{
			if (base.IsClient && token != 0 && token == _localAttackToken)
			{
				CrabBossLocalRock obj = _rockPool?.GetFallingRockSlot(slotIndex);
				obj?.SetIgnoredCollisionRoot((base.Boss != null) ? base.Boss.transform : null);
				obj?.SetWorldImpactLayers(_worldImpactLayers);
				obj?.SetWorldImpactSurfaceAlignment(enabled: false);
				obj?.BeginFall(spawnPosition, groundPosition, rotation, fallStartTime, fallSpeed);
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientResolveRainRockRpc(uint token, int slotIndex, CrabBossRockImpactType impactType, Vector3 position, Vector3 normal)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1691082044u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				BytePacker.WriteValueBitPacked(bufferWriter, slotIndex);
				bufferWriter.WriteValueSafe(in impactType, default(FastBufferWriter.ForEnums));
				bufferWriter.WriteValueSafe(in position);
				bufferWriter.WriteValueSafe(in normal);
				__endSendRpc(ref bufferWriter, 1691082044u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsClient && token != 0 && token == _localAttackToken)
			{
				CrabBossLocalRock crabBossLocalRock = _rockPool?.GetFallingRockSlot(slotIndex);
				if (impactType == CrabBossRockImpactType.None)
				{
					crabBossLocalRock?.ResetSlot();
					return;
				}
				crabBossLocalRock?.PlayImpact(impactType, position, normal);
				base.Presentation?.PlayRockImpactSfx();
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientStopRainRpc(uint token)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1216272751u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				__endSendRpc(ref bufferWriter, 1216272751u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsClient && token != 0 && token == _localAttackToken)
				{
					_rockPool?.ResetFallingRocks();
					_localAttackToken = 0u;
				}
			}
		}

		protected override bool ValidateSpecificSettings(out string error)
		{
			ResolveRockPool();
			if (base.AttackId != CrabBossAttackId.RockRain)
			{
				error = "Attack ID must be RockRain.";
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
			if (_spawnArea == null)
			{
				error = "Rock rain spawn area is missing.";
				return false;
			}
			if (!IsPositiveFinite(_spawnArea.size.x) || !IsPositiveFinite(_spawnArea.size.z))
			{
				error = "Rock rain spawn area must have positive X and Z size.";
				return false;
			}
			if (!IsNonNegativeFinite(_spawnHeight) || !IsPositiveFinite(_rainDuration) || !IsPositiveFinite(_rockSpawnInterval) || !IsNonNegativeFinite(_settleDuration) || !IsPositiveFinite(_fallSpeed) || !IsPositiveFinite(_maximumLifetime) || !IsPositiveFinite(_hitRadius) || float.IsNaN(_playerHitCenterOffset) || float.IsInfinity(_playerHitCenterOffset))
			{
				error = "Rock rain timing, speed, lifetime or hit radius is invalid.";
				return false;
			}
			if (_maximumActiveRocks <= 0 || _maximumActiveRocks > 10)
			{
				error = "Maximum active rocks must fit the local falling rock pool.";
				return false;
			}
			if (_worldImpactLayers.value == 0 || _oxygenDamage == 0)
			{
				error = "Rock rain world layers and oxygen damage must be configured.";
				return false;
			}
			error = null;
			return true;
		}

		private void EnsureSlotCapacity()
		{
			int num = _rockPool?.FallingRockSlotCount ?? 0;
			while (_serverRocks.Count < num)
			{
				_serverRocks.Add(new ServerRainRock());
			}
		}

		private void ClearRuntime()
		{
			for (int i = 0; i < _serverRocks.Count; i++)
			{
				_serverRocks[i].Active = false;
				_serverRocks[i].ReleaseTime = 0.0;
			}
			_collisionPlayers.Clear();
			_activeRockCount = 0;
		}

		private void StopServerRain()
		{
			if (_serverRainCoroutine != null)
			{
				StopCoroutine(_serverRainCoroutine);
				_serverRainCoroutine = null;
			}
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
			if (value > 0f && !float.IsNaN(value))
			{
				return !float.IsInfinity(value);
			}
			return false;
		}

		private static bool IsNonNegativeFinite(float value)
		{
			if (value >= 0f && !float.IsNaN(value))
			{
				return !float.IsInfinity(value);
			}
			return false;
		}

		private void Log(string message)
		{
			if (_log)
			{
				UtilityMethods.Log("[CrabBoss][RockRainAttack] " + message);
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(159689037u, __rpc_handler_159689037, "ClientBeginRainRpc", RpcInvokePermission.Server);
			__registerRpc(932097817u, __rpc_handler_932097817, "ClientBeginRainForLateJoinRpc", RpcInvokePermission.Server);
			__registerRpc(2264826521u, __rpc_handler_2264826521, "ClientBeginRainRockRpc", RpcInvokePermission.Server);
			__registerRpc(2209830069u, __rpc_handler_2209830069, "ClientBeginRainRockForLateJoinRpc", RpcInvokePermission.Server);
			__registerRpc(1691082044u, __rpc_handler_1691082044, "ClientResolveRainRockRpc", RpcInvokePermission.Server);
			__registerRpc(1216272751u, __rpc_handler_1216272751, "ClientStopRainRpc", RpcInvokePermission.Server);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_159689037(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				((CrabBossRockRainAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossRockRainAttack)target).ClientBeginRainRpc(value);
				((CrabBossRockRainAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_932097817(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				RpcParams ext = rpcParams.Ext;
				((CrabBossRockRainAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossRockRainAttack)target).ClientBeginRainForLateJoinRpc(value, ext);
				((CrabBossRockRainAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2264826521(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
				((CrabBossRockRainAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossRockRainAttack)target).ClientBeginRainRockRpc(value, value2, value3, value4, value5, value6, value7);
				((CrabBossRockRainAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2209830069(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
				((CrabBossRockRainAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossRockRainAttack)target).ClientBeginRainRockForLateJoinRpc(value, value2, value3, value4, value5, value6, value7, ext);
				((CrabBossRockRainAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1691082044(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				reader.ReadValueSafe(out CrabBossRockImpactType value3, default(FastBufferWriter.ForEnums));
				reader.ReadValueSafe(out Vector3 value4);
				reader.ReadValueSafe(out Vector3 value5);
				((CrabBossRockRainAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossRockRainAttack)target).ClientResolveRainRockRpc(value, value2, value3, value4, value5);
				((CrabBossRockRainAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1216272751(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				((CrabBossRockRainAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossRockRainAttack)target).ClientStopRainRpc(value);
				((CrabBossRockRainAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "CrabBossRockRainAttack";
		}
	}
}
