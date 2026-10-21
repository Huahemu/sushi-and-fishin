using System.Collections.Generic;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	public sealed class PiranhaCreature : HostileSeaCreature
	{
		[Header("Piranha Refs")]
		[SerializeField]
		private Animator _animator;

		[SerializeField]
		private AudioSource _audioSource;

		[SerializeField]
		private List<AudioClip> _attackClips = new List<AudioClip>();

		[Header("Latch")]
		[SerializeField]
		[Min(0f)]
		private float _despawnAfterLatchDelay = 0.1f;

		private bool _latched;

		private bool _isBossReinforcement;

		protected override bool UseEmbeddedGraphic => true;

		protected override GameObject EmbeddedGraphicRoot
		{
			get
			{
				if (!(_animator != null))
				{
					return null;
				}
				return _animator.gameObject;
			}
		}

		protected override bool KeepEmbeddedGraphicRootActive => true;

		protected override void OnServerSimulationStarted()
		{
			_latched = false;
			if (_animator != null)
			{
				_animator.SetBool("Dead", value: false);
			}
			base.OnServerSimulationStarted();
			if (_isBossReinforcement)
			{
				StartBossReinforcementChase();
			}
		}

		public void ServerSetBossReinforcement(bool isBossReinforcement)
		{
			if (base.IsServer)
			{
				_isBossReinforcement = isBossReinforcement;
				if (_isBossReinforcement && base.IsServerSimulationActive)
				{
					StartBossReinforcementChase();
				}
			}
		}

		protected override void OnServerSimulationStopped()
		{
			_latched = false;
			base.OnServerSimulationStopped();
		}

		protected override void TickAttack()
		{
			if (_latched)
			{
				return;
			}
			if (_isBossReinforcement && !IsTargetValidForAttack())
			{
				ClearTarget("BossPiranhaAttackTargetInvalid");
				if (!StartBossReinforcementChase())
				{
					SetState(HostileCreatureState.Patrol);
				}
			}
			else
			{
				base.TickAttack();
			}
		}

		protected override void TickChase()
		{
			if (_isBossReinforcement && !IsTargetValidForChase())
			{
				ClearTarget("BossPiranhaChaseTargetInvalid");
				if (!StartBossReinforcementChase())
				{
					SetState(HostileCreatureState.Patrol);
				}
			}
			else
			{
				base.TickChase();
			}
		}

		protected override void OnDetectedExit(Collider other)
		{
			if (!_isBossReinforcement)
			{
				base.OnDetectedExit(other);
			}
		}

		protected override void StartAttack()
		{
			if (!IsTargetValidForAttack())
			{
				ClearTarget("PiranhaStartAttackInvalidTarget");
				SetState(HostileCreatureState.Patrol);
				return;
			}
			StopNavigation();
			ChangeAnim(CreatureAnim.Attack);
			uint tokenAtIssue = _token;
			float seconds = ((_hostileSO != null) ? Mathf.Max(0.05f, _hostileSO.AttackTimeout) : 1.25f);
			Singleton<DelayHelper>.Instance.DelayAction(seconds, () =>
			{
				if (base.IsServer && IsTokenValid(tokenAtIssue) && _state == HostileCreatureState.Attack && !_latched)
				{
					if (IsTargetValidForChase())
					{
						SetState(HostileCreatureState.Chase);
					}
					else
					{
						ClearTarget("PiranhaAttackTimeout");
						SetState(HostileCreatureState.Patrol);
					}
				}
			});
			OnAttackStarted(_targetPlayer);
		}

		protected override void OnAttackHitPlayer(PPG_Player target)
		{
			if (base.IsServer && !(target == null) && !_latched)
			{
				PlayRandomAttackSfxRpc();
				if (target.DivingEquipment != null)
				{
					target.DivingEquipment.ServerConsumeOxygenRpc(GetDamageAmount(), withFeedback: true);
				}
				StartLatch(target);
			}
		}

		protected override Transform ResolveTargetTransform(PPG_Player player)
		{
			if (player == null)
			{
				return null;
			}
			if (player.RigBones != null && player.RigBones.TpsRig != null && player.RigBones.TpsRig.Head != null)
			{
				return player.RigBones.TpsRig.Head;
			}
			return base.ResolveTargetTransform(player);
		}

		private void StartLatch(PPG_Player target)
		{
			if (!base.IsServer || _latched)
			{
				return;
			}
			if (target == null)
			{
				ClearTarget("PiranhaLatchTargetNull");
				SetState(HostileCreatureState.Patrol);
				return;
			}
			_latched = true;
			StopNavigation();
			SetSensorActive(active: false);
			if (target.DivingEquipment != null)
			{
				target.DivingEquipment.LatchPiranha();
			}
			float despawnAfterLatchDelay = GetDespawnAfterLatchDelay();
			uint tokenAtIssue = _token;
			Singleton<DelayHelper>.Instance.DelayAction(despawnAfterLatchDelay, () =>
			{
				if (base.IsServer && IsTokenValid(tokenAtIssue) && _latched && base.NetworkObject != null && base.NetworkObject.IsSpawned)
				{
					base.NetworkObject.Despawn();
				}
			});
			Log("Piranha latched target=" + target.name);
		}

		protected override void OnServerHit(PPG_Player player)
		{
			if (!_latched)
			{
				base.OnServerHit(player);
			}
		}

		protected override void OnServerHarpoonHit(PPG_Player player, byte damage)
		{
			if (!_latched)
			{
				base.OnServerHarpoonHit(player, damage);
			}
		}

		protected override void OnHostileHit(PPG_Player hitter, byte hitCount)
		{
			if (!_latched)
			{
				ChangeAnim(CreatureAnim.Hit);
				Log($"Harpoon damage {_harpoonDamageTaken}/{GetHarpoonDurability()} Hits={hitCount}");
				if (!ShouldDieFromHarpoonDamage() && ShouldEscapeFromHarpoonDamage())
				{
					ChangeAnim(CreatureAnim.CriticalHit);
				}
			}
		}

		protected override bool ShouldEscapeFromHarpoonDamage()
		{
			if (_isBossReinforcement)
			{
				return false;
			}
			if (_latched)
			{
				return false;
			}
			return base.ShouldEscapeFromHarpoonDamage();
		}

		public override bool ServerTryBeginLayerRetreat(float speedMultiplier, out float estimatedDuration)
		{
			if (_latched || _isBossReinforcement)
			{
				estimatedDuration = 0f;
				return false;
			}
			return base.ServerTryBeginLayerRetreat(speedMultiplier, out estimatedDuration);
		}

		private bool StartBossReinforcementChase()
		{
			if (!base.IsServer || Singleton<PlayersManager>.Instance == null)
			{
				return false;
			}
			PPG_Player pPG_Player = null;
			float num = float.MaxValue;
			foreach (PPG_Player value in Singleton<PlayersManager>.Instance.Players.Values)
			{
				if (CanTargetPlayer(value))
				{
					float sqrMagnitude = (value.transform.position - base.transform.position).sqrMagnitude;
					if (!(sqrMagnitude >= num))
					{
						pPG_Player = value;
						num = sqrMagnitude;
					}
				}
			}
			if (pPG_Player == null)
			{
				return false;
			}
			SetTarget(pPG_Player);
			SetState(HostileCreatureState.Chase);
			return true;
		}

		protected override bool ShouldDieFromHarpoonDamage()
		{
			if (_latched)
			{
				return false;
			}
			return base.ShouldDieFromHarpoonDamage();
		}

		protected override void StartDead()
		{
			_latched = false;
			base.StartDead();
		}

		private byte GetDamageAmount()
		{
			if (!(_hostileSO != null))
			{
				return 10;
			}
			return _hostileSO.DamageAmount;
		}

		private float GetDespawnAfterLatchDelay()
		{
			return Mathf.Max(0f, _despawnAfterLatchDelay);
		}

		[Rpc(SendTo.Everyone)]
		private void PlayRandomAttackSfxRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1699738969u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1699738969u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!(_audioSource == null) && _attackClips != null && _attackClips.Count != 0)
			{
				AudioClip audioClip = _attackClips[Random.Range(0, _attackClips.Count)];
				if (!(audioClip == null))
				{
					_audioSource.PlayOneShot(audioClip);
				}
			}
		}

		protected override void ApplyAnim(CreatureAnim anim)
		{
			if (_animator == null)
			{
				Log("Animator is null.", LogType.Warning);
				return;
			}
			switch (anim)
			{
			case CreatureAnim.Idle:
				ResetLocomotionAnimFlags();
				break;
			case CreatureAnim.Move:
				ResetLocomotionAnimFlags();
				break;
			case CreatureAnim.Sprint:
				ResetLocomotionAnimFlags();
				_animator.SetBool("Run", value: true);
				break;
			case CreatureAnim.AttackSwim:
				ResetLocomotionAnimFlags();
				_animator.SetBool("AttackSwim", value: true);
				break;
			case CreatureAnim.Attack:
				ResetLocomotionAnimFlags();
				_animator.SetTrigger("Attack");
				break;
			case CreatureAnim.Hit:
				_animator.SetTrigger("Hit");
				break;
			case CreatureAnim.CriticalHit:
				_animator.SetTrigger("CriticalHit");
				break;
			case CreatureAnim.Dead:
				ResetLocomotionAnimFlags();
				_animator.SetBool("Dead", value: true);
				break;
			case CreatureAnim.Struggle:
				break;
			}
		}

		private void ResetLocomotionAnimFlags()
		{
			if (!(_animator == null))
			{
				_animator.SetBool("Run", value: false);
				_animator.SetBool("AttackSwim", value: false);
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(1699738969u, __rpc_handler_1699738969, "PlayRandomAttackSfxRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_1699738969(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PiranhaCreature)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PiranhaCreature)target).PlayRandomAttackSfxRpc();
				((PiranhaCreature)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "PiranhaCreature";
		}
	}
}
