using System.Collections;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace PaperPixelGames.Core.Boss.Attacks
{
	[RequireComponent(typeof(CrabBossPresentationController))]
	[MovedFrom(true, "PaperPixelGames.Core.Boss.Attacks", "Assembly-CSharp", "CrabBossJumpSlamAttack")]
	public sealed class CrabBossExpandingPulseAttack : CrabBossAttackBase
	{
		[Header("Timing")]
		[SerializeField]
		private float _completeDelay;

		[SerializeField]
		private float _shockwaveSpawnDelay;

		[Header("Expanding Pulse")]
		[SerializeField]
		private CrabBossExpandingPulse _expandingPulsePrefab;

		[SerializeField]
		private Transform _shockwaveSpawnPoint;

		[Header("Animator")]
		[SerializeField]
		private CrabBossAnimatorTrigger _fallTriggerName;

		[SerializeField]
		private float _fallTriggerDelay;

		[Header("Debug")]
		[SerializeField]
		private bool _log;

		private Coroutine _serverCompletionCoroutine;

		private Coroutine _localFallCoroutine;

		private CrabBossExpandingPulse _activeExpandingPulse;

		private CrabBossExpandingPulse _serverExpandingPulsePool;

		private CrabBossExpandingPulse _localExpandingPulsePool;

		private uint _localAttackToken;

		private double _serverAttackStartTime;

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			PrepareExpandingPulsePool();
		}

		public override void OnNetworkDespawn()
		{
			StopServerCompletion();
			StopLocalFall();
			ServerDespawnExpandingPulse();
			DestroyExpandingPulsePool();
			_localAttackToken = 0u;
			base.OnNetworkDespawn();
		}

		protected override void OnServerAttackStarted(CrabBossAttackCommand command)
		{
			StopServerCompletion();
			_serverAttackStartTime = base.NetworkManager.ServerTime.Time;
			ClientStartExpandingPulseRpc(command.Token);
			_serverCompletionCoroutine = StartCoroutine(ServerExpandingPulseRoutine(command.Token));
			Log($"Started. Token={command.Token}.");
		}

		protected override void OnServerAttackEnded(CrabBossAttackCommand command, CrabBossAttackEndReason reason)
		{
			StopServerCompletion();
			ServerDespawnExpandingPulse();
			ClientStopExpandingPulseRpc(command.Token);
			Log($"Ended. Token={command.Token} Reason={reason}.");
		}

		protected override void OnServerForceCleanup(uint activeToken)
		{
			StopServerCompletion();
			ServerDespawnExpandingPulse();
			if (base.IsSpawned)
			{
				ClientStopExpandingPulseRpc(activeToken);
			}
		}

		public override void ServerSyncLateJoinClient(ulong clientId)
		{
			if (base.IsServer && base.ActiveToken != 0)
			{
				bool flag = _activeExpandingPulse != null && _activeExpandingPulse.IsRunning;
				ClientSyncExpandingPulseRpc(base.ActiveToken, Mathf.Max(0f, (float)(base.NetworkManager.ServerTime.Time - _serverAttackStartTime)), flag, flag ? _activeExpandingPulse.transform.position : Vector3.zero, flag ? _activeExpandingPulse.transform.rotation : Quaternion.identity, flag ? _activeExpandingPulse.Elapsed : 0f, base.NetworkManager.ServerTime.Time, base.RpcTarget.Single(clientId, RpcTargetUse.Temp));
			}
		}

		private IEnumerator ServerExpandingPulseRoutine(uint token)
		{
			float elapsed = 0f;
			bool shockwaveSpawned = false;
			while (elapsed < _completeDelay)
			{
				if (!shockwaveSpawned && elapsed >= _shockwaveSpawnDelay)
				{
					shockwaveSpawned = true;
					ServerSpawnExpandingPulse();
				}
				elapsed += Time.deltaTime;
				yield return null;
			}
			_serverCompletionCoroutine = null;
			if (base.IsServer && !(base.Boss == null) && base.Boss.IsAttackRunning && base.Boss.CurrentAttackToken == token)
			{
				CompleteAttack(token);
			}
		}

		private void ServerSpawnExpandingPulse()
		{
			if (base.IsServer && !(_expandingPulsePrefab == null) && !(_shockwaveSpawnPoint == null))
			{
				PrepareExpandingPulsePool();
				if (!(_serverExpandingPulsePool == null))
				{
					_serverExpandingPulsePool.transform.SetPositionAndRotation(_shockwaveSpawnPoint.position, _shockwaveSpawnPoint.rotation);
					_activeExpandingPulse = _serverExpandingPulsePool;
					_activeExpandingPulse.BeginRuntime(handlesGameplay: true, ServerHandleExpandingPulseHit, ServerHandleExpandingPulseOxygenTick);
					ClientPlayExpandingPulseRpc(_shockwaveSpawnPoint.position, _shockwaveSpawnPoint.rotation, base.Boss.CurrentAttackToken, base.NetworkManager.ServerTime.Time);
					Log("Expanding Pulse played.");
				}
			}
		}

		private void ServerDespawnExpandingPulse()
		{
			if (base.IsServer && !(_activeExpandingPulse == null))
			{
				_activeExpandingPulse.StopRuntime();
				_activeExpandingPulse = null;
			}
		}

		private void ServerHandleExpandingPulseHit(PPG_Player player)
		{
			if (base.IsServer && !(player == null) && player.IsSpawned)
			{
				ClientApplyExpandingPulseKnockbackRpc(player.NetworkObject, (_activeExpandingPulse != null) ? _activeExpandingPulse.transform.position : base.transform.position, _expandingPulsePrefab.KnockbackImpulse, _expandingPulsePrefab.UpwardForce, base.RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));
			}
		}

		private void ServerHandleExpandingPulseOxygenTick(PPG_Player player)
		{
			if (base.IsServer && !(player == null) && player.IsSpawned && !(_activeExpandingPulse == null))
			{
				base.Boss.ServerTryApplyContactHit(player, _activeExpandingPulse.transform.position, _activeExpandingPulse.OxygenDamagePerTick, 0f, 0f, showExpandingPulseFeedback: true);
			}
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientApplyExpandingPulseKnockbackRpc(NetworkObjectReference playerReference, Vector3 origin, float impulse, float upwardForce, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(877159116u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in playerReference, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in origin);
				bufferWriter.WriteValueSafe(in impulse, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in upwardForce, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 877159116u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (playerReference.TryGetComponent<PPG_Player>(out var component) && !(component.FirstPersonCharacter == null))
				{
					component.FirstPersonCharacter.PushFromPoint(origin, impulse, upwardForce);
				}
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientStartExpandingPulseRpc(uint token)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2465034298u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				__endSendRpc(ref bufferWriter, 2465034298u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				StartLocalExpandingPulse(token, 0f);
			}
		}

		private void StartLocalExpandingPulse(uint token, float elapsed)
		{
			StopLocalFall();
			_localExpandingPulsePool?.StopRuntime();
			_localAttackToken = token;
			if (base.Presentation == null || !base.Presentation.PlayAttackAnimation(CrabBossAttackAnimation.ExpandingPulse))
			{
				UtilityMethods.Log("[CrabBoss][ExpandingPulseAttack] Animator bool 'ExpandingPulse' could not be enabled.", LogType.Warning);
			}
			if (base.Presentation != null)
			{
				base.Boss.ResetLocalCrabAnimatorTrigger(_fallTriggerName);
			}
			if (_fallTriggerName != CrabBossAnimatorTrigger.None && elapsed <= _fallTriggerDelay)
			{
				_localFallCoroutine = StartCoroutine(LocalFallAfterDelay(token, _fallTriggerDelay - elapsed));
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientStopExpandingPulseRpc(uint token)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3110762549u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				__endSendRpc(ref bufferWriter, 3110762549u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				StopLocalExpandingPulse(token);
			}
		}

		private void StopLocalExpandingPulse(uint token)
		{
			if (token == 0 || _localAttackToken == token)
			{
				StopLocalFall();
				_localAttackToken = 0u;
				_localExpandingPulsePool?.StopRuntime();
				base.Presentation?.StopAttackAnimation(CrabBossAttackAnimation.ExpandingPulse);
				base.Boss?.ResetLocalCrabAnimatorTrigger(_fallTriggerName);
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientPlayExpandingPulseRpc(Vector3 position, Quaternion rotation, uint token, double sentAt)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3051198151u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in position);
				bufferWriter.WriteValueSafe(in rotation);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				bufferWriter.WriteValueSafe(in sentAt, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3051198151u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				PlayLocalPulse(position, rotation, token, Mathf.Max(0f, (float)(base.NetworkManager.ServerTime.Time - sentAt)));
			}
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientSyncExpandingPulseRpc(uint token, float attackElapsed, bool pulseRunning, Vector3 position, Quaternion rotation, float pulseElapsed, double sentAt, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(4132274157u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, token);
				bufferWriter.WriteValueSafe(in attackElapsed, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in pulseRunning, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in position);
				bufferWriter.WriteValueSafe(in rotation);
				bufferWriter.WriteValueSafe(in pulseElapsed, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in sentAt, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 4132274157u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				float num = Mathf.Max(0f, (float)(base.NetworkManager.ServerTime.Time - sentAt));
				StartLocalExpandingPulse(token, attackElapsed + num);
				if (pulseRunning)
				{
					PlayLocalPulse(position, rotation, token, pulseElapsed + num);
				}
			}
		}

		private void PlayLocalPulse(Vector3 position, Quaternion rotation, uint token, float elapsed)
		{
			if (_localAttackToken == token && !base.IsServer)
			{
				PrepareExpandingPulsePool();
				if (!(_localExpandingPulsePool == null))
				{
					_localExpandingPulsePool.transform.SetPositionAndRotation(position, rotation);
					_localExpandingPulsePool.BeginRuntime(handlesGameplay: false, null, null, elapsed);
				}
			}
		}

		private IEnumerator LocalFallAfterDelay(uint token, float delay)
		{
			yield return new WaitForSeconds(delay);
			_localFallCoroutine = null;
			if (_localAttackToken == token && !(base.Presentation == null))
			{
				base.Boss.PlayLocalCrabAnimatorTrigger(_fallTriggerName);
			}
		}

		protected override bool ValidateSpecificSettings(out string error)
		{
			if (base.Presentation == null)
			{
				error = "CrabBossPresentationController is missing.";
				return false;
			}
			if (!IsPositiveFinite(_completeDelay))
			{
				error = "Complete delay must be greater than zero and finite.";
				return false;
			}
			if (!IsNonNegativeFinite(_fallTriggerDelay))
			{
				error = "Fall trigger delay must be zero or greater and finite.";
				return false;
			}
			if (_expandingPulsePrefab == null)
			{
				error = "Expanding Pulse prefab is missing.";
				return false;
			}
			if (_shockwaveSpawnPoint == null)
			{
				error = "Expanding Pulse spawn point is missing.";
				return false;
			}
			if (!IsNonNegativeFinite(_shockwaveSpawnDelay) || _shockwaveSpawnDelay >= _completeDelay)
			{
				error = "Expanding Pulse spawn delay must be finite, non-negative and lower than complete delay.";
				return false;
			}
			if (!_expandingPulsePrefab.ValidateConfiguration(out var error2))
			{
				error = "Expanding Pulse prefab is invalid: " + error2;
				return false;
			}
			if (_shockwaveSpawnDelay + _expandingPulsePrefab.LifeDuration > _completeDelay)
			{
				error = "Complete delay must include Expanding Pulse spawn delay plus its life duration.";
				return false;
			}
			if (_fallTriggerName != CrabBossAnimatorTrigger.None && _fallTriggerDelay >= _completeDelay)
			{
				error = "Fall trigger delay must be lower than complete delay.";
				return false;
			}
			error = null;
			return true;
		}

		private void StopServerCompletion()
		{
			if (_serverCompletionCoroutine != null)
			{
				StopCoroutine(_serverCompletionCoroutine);
				_serverCompletionCoroutine = null;
			}
		}

		private void StopLocalFall()
		{
			if (_localFallCoroutine != null)
			{
				StopCoroutine(_localFallCoroutine);
				_localFallCoroutine = null;
			}
		}

		private void PrepareExpandingPulsePool()
		{
			if (!(_expandingPulsePrefab == null))
			{
				if (base.IsServer && _serverExpandingPulsePool == null)
				{
					_serverExpandingPulsePool = CreateExpandingPulsePoolInstance("Server");
				}
				if (!base.IsServer && _localExpandingPulsePool == null)
				{
					_localExpandingPulsePool = CreateExpandingPulsePoolInstance("Client");
				}
			}
		}

		private CrabBossExpandingPulse CreateExpandingPulsePoolInstance(string suffix)
		{
			CrabBossExpandingPulse crabBossExpandingPulse = Object.Instantiate(_expandingPulsePrefab);
			crabBossExpandingPulse.name = _expandingPulsePrefab.name + "_" + suffix + "_Pool";
			crabBossExpandingPulse.StopRuntime();
			return crabBossExpandingPulse;
		}

		private void DestroyExpandingPulsePool()
		{
			DestroyExpandingPulsePoolInstance(ref _serverExpandingPulsePool);
			DestroyExpandingPulsePoolInstance(ref _localExpandingPulsePool);
			_activeExpandingPulse = null;
		}

		private static void DestroyExpandingPulsePoolInstance(ref CrabBossExpandingPulse instance)
		{
			if (!(instance == null))
			{
				Object.Destroy(instance.gameObject);
				instance = null;
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
				UtilityMethods.Log("[CrabBoss][ExpandingPulseAttack] " + message);
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(877159116u, __rpc_handler_877159116, "ClientApplyExpandingPulseKnockbackRpc", RpcInvokePermission.Server);
			__registerRpc(2465034298u, __rpc_handler_2465034298, "ClientStartExpandingPulseRpc", RpcInvokePermission.Server);
			__registerRpc(3110762549u, __rpc_handler_3110762549, "ClientStopExpandingPulseRpc", RpcInvokePermission.Server);
			__registerRpc(3051198151u, __rpc_handler_3051198151, "ClientPlayExpandingPulseRpc", RpcInvokePermission.Server);
			__registerRpc(4132274157u, __rpc_handler_4132274157, "ClientSyncExpandingPulseRpc", RpcInvokePermission.Server);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_877159116(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out Vector3 value2);
				reader.ReadValueSafe(out float value3, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out float value4, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((CrabBossExpandingPulseAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossExpandingPulseAttack)target).ClientApplyExpandingPulseKnockbackRpc(value, value2, value3, value4, ext);
				((CrabBossExpandingPulseAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2465034298(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				((CrabBossExpandingPulseAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossExpandingPulseAttack)target).ClientStartExpandingPulseRpc(value);
				((CrabBossExpandingPulseAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3110762549(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				((CrabBossExpandingPulseAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossExpandingPulseAttack)target).ClientStopExpandingPulseRpc(value);
				((CrabBossExpandingPulseAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3051198151(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out Quaternion value2);
				ByteUnpacker.ReadValueBitPacked(reader, out uint value3);
				reader.ReadValueSafe(out double value4, default(FastBufferWriter.ForPrimitives));
				((CrabBossExpandingPulseAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossExpandingPulseAttack)target).ClientPlayExpandingPulseRpc(value, value2, value3, value4);
				((CrabBossExpandingPulseAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4132274157(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out Vector3 value4);
				reader.ReadValueSafe(out Quaternion value5);
				reader.ReadValueSafe(out float value6, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out double value7, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((CrabBossExpandingPulseAttack)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CrabBossExpandingPulseAttack)target).ClientSyncExpandingPulseRpc(value, value2, value3, value4, value5, value6, value7, ext);
				((CrabBossExpandingPulseAttack)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "CrabBossExpandingPulseAttack";
		}
	}
}
