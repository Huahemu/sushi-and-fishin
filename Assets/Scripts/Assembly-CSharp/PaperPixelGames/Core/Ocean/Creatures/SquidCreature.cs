using DG.Tweening;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	public sealed class SquidCreature : CatchableSeaCreature
	{
		[Header("Squid Refs")]
		[SerializeField]
		private Animator _animator;

		[SerializeField]
		private CreatureShieldAbility _shieldAbility;

		[Header("Embedded Graphic")]
		[SerializeField]
		private GameObject _embeddedGraphicRoot;

		[Header("Ink VFX")]
		[SerializeField]
		private ParticleSystem _inkVfx;

		[SerializeField]
		private Transform _inkVfxStartPoint;

		[SerializeField]
		private AudioSource _inkAttackAudioSource;

		[Header("Ink Response")]
		[SerializeField]
		[Min(0f)]
		private float _inkTravelDuration = 1.5f;

		[SerializeField]
		[Min(0f)]
		private float _playerEffectDelay = 1.25f;

		private PPG_Player _lastInkTarget;

		private Tween _inkMoveTween;

		private bool _isInkResponseRunning;

		private bool _inkTargetEffectsApplied;

		private uint _inkResponseToken;

		protected override bool UseEmbeddedGraphic => true;

		protected override GameObject EmbeddedGraphicRoot => _embeddedGraphicRoot;

		protected override void CacheComponents()
		{
			base.CacheComponents();
			if (_shieldAbility == null)
			{
				TryGetComponent<CreatureShieldAbility>(out _shieldAbility);
			}
			if (_animator == null)
			{
				_animator = GetComponentInChildren<Animator>(includeInactive: true);
			}
			if (_embeddedGraphicRoot == null && _animator != null)
			{
				_embeddedGraphicRoot = _animator.gameObject;
			}
			if (_inkVfx == null)
			{
				_inkVfx = GetComponentInChildren<ParticleSystem>(includeInactive: true);
			}
			if (_inkAttackAudioSource == null)
			{
				_inkAttackAudioSource = GetComponentInChildren<AudioSource>(includeInactive: true);
			}
		}

		protected override void OnGraphicSpawned(GameObject graphic)
		{
			base.OnGraphicSpawned(graphic);
			CacheGraphicReferences(graphic);
			ResetAnimatorAliveState();
		}

		protected override void OnClientVisualInitialized()
		{
			base.OnClientVisualInitialized();
			if (_activeGraphic != null)
			{
				CacheGraphicReferences(_activeGraphic);
				ResetAnimatorAliveState();
			}
		}

		private void CacheGraphicReferences(GameObject graphic)
		{
			if (!(graphic == null))
			{
				if (_animator == null)
				{
					_animator = graphic.GetComponentInChildren<Animator>(includeInactive: true);
				}
				if (_shieldAbility == null)
				{
					_shieldAbility = GetComponentInChildren<CreatureShieldAbility>(includeInactive: true);
				}
				if (_inkVfx == null)
				{
					_inkVfx = graphic.GetComponentInChildren<ParticleSystem>(includeInactive: true);
				}
				if (_inkAttackAudioSource == null)
				{
					_inkAttackAudioSource = graphic.GetComponentInChildren<AudioSource>(includeInactive: true);
				}
				if (_embeddedGraphicRoot == null)
				{
					_embeddedGraphicRoot = graphic;
				}
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			if (_shieldAbility != null)
			{
				_shieldAbility.ShieldChanged += OnShieldChanged;
			}
			ResetAnimatorAliveState();
		}

		public override void OnNetworkDespawn()
		{
			if (_shieldAbility != null)
			{
				_shieldAbility.ShieldChanged -= OnShieldChanged;
			}
			KillInkTween();
			ClearLastInkTargetEffects();
			base.OnNetworkDespawn();
		}

		protected override void OnServerSimulationStarted()
		{
			_lastInkTarget = null;
			ResetAnimatorAliveState();
			base.OnServerSimulationStarted();
		}

		protected override void OnServerCatchableHit(PPG_Player player)
		{
			if (base.IsServer && !(player == null))
			{
				if (ShouldBlockCatchWithShield())
				{
					RunShieldInkCounter(player);
				}
				base.OnServerCatchableHit(player);
			}
		}

		protected override bool ServerCanInstantCatch(PPG_Player owner)
		{
			if (ShouldBlockCatchWithShield())
			{
				RunShieldInkCounter(owner);
			}
			return true;
		}

		private bool ShouldBlockCatchWithShield()
		{
			if (_shieldAbility != null)
			{
				return _shieldAbility.CanBlockHit();
			}
			return false;
		}

		private void RunShieldInkCounter(PPG_Player target)
		{
			if (!base.IsServer || target == null)
			{
				return;
			}
			_lastInkTarget = target;
			_isInkResponseRunning = true;
			_inkTargetEffectsApplied = false;
			_inkResponseToken++;
			uint tokenAtIssue = _inkResponseToken;
			if (_shieldAbility != null)
			{
				_shieldAbility.TryBreakShield();
			}
			Vector3 targetPosition = ResolveInkTargetPosition(target);
			ClientThrowInkRpc(targetPosition);
			Singleton<DelayHelper>.Instance.DelayAction(_playerEffectDelay, () =>
			{
				if (base.IsServer && tokenAtIssue == _inkResponseToken && _isInkResponseRunning && !(_lastInkTarget == null) && _lastInkTarget.IsSpawned)
				{
					ApplyInkTargetEffects(_lastInkTarget);
					_inkTargetEffectsApplied = true;
				}
			});
			Log("Shield ink counter triggered. Target=" + target.name);
		}

		private Vector3 ResolveInkTargetPosition(PPG_Player target)
		{
			if (target == null)
			{
				return base.transform.position;
			}
			if (target.RigBones != null && target.RigBones.TpsRig != null && target.RigBones.TpsRig.Head != null)
			{
				return target.RigBones.TpsRig.Head.position;
			}
			return target.transform.position;
		}

		private void ApplyInkTargetEffects(PPG_Player target)
		{
			if (!(target == null))
			{
				target.ClientOwnerPlaySquidVFXRpc();
				if (target.Harpoon != null)
				{
					target.Harpoon.ClientBarBlockStatusRpc(status: true);
				}
			}
		}

		private void ClearLastInkTargetEffects()
		{
			if (base.IsServer)
			{
				EndSquidInkResponse(_lastInkTarget);
			}
		}

		private void ClearInkTargetEffects(PPG_Player target)
		{
			if (!(target == null))
			{
				target.ClientOwnerStopSquidVFXRpc();
				if (target.Harpoon != null)
				{
					target.Harpoon.ClientBarBlockStatusRpc(status: false);
				}
			}
		}

		private void OnShieldChanged(bool isActive)
		{
		}

		[Rpc(SendTo.Server)]
		public void ServerEndSquidInkResponseRpc(NetworkObjectReference playerRef)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2255680183u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in playerRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 2255680183u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsServer)
			{
				PPG_Player component = null;
				if (playerRef.TryGet(out var networkObject))
				{
					networkObject.TryGetComponent<PPG_Player>(out component);
				}
				EndSquidInkResponse(component);
			}
		}

		private void EndSquidInkResponse(PPG_Player preferredTarget)
		{
			if (base.IsServer)
			{
				_inkResponseToken++;
				PPG_Player pPG_Player = ((preferredTarget != null) ? preferredTarget : _lastInkTarget);
				if (pPG_Player != null && pPG_Player.IsSpawned)
				{
					ClearInkTargetEffects(pPG_Player);
				}
				_lastInkTarget = null;
				_isInkResponseRunning = false;
				_inkTargetEffectsApplied = false;
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientThrowInkRpc(Vector3 targetPosition)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3969621731u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in targetPosition);
				__endSendRpc(ref bufferWriter, 3969621731u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ChangeLocalSquidAnim(CreatureAnim.SpecialStart);
			if (_inkAttackAudioSource != null)
			{
				_inkAttackAudioSource.Play();
			}
			if (_inkVfx == null)
			{
				return;
			}
			KillInkTween();
			if (_inkVfxStartPoint != null)
			{
				_inkVfx.transform.position = _inkVfxStartPoint.position;
			}
			_inkVfx.gameObject.SetActive(value: true);
			_inkVfx.Play();
			_inkMoveTween = _inkVfx.transform.DOMove(targetPosition, Mathf.Max(0.01f, _inkTravelDuration)).SetEase(Ease.OutQuad).OnComplete(() =>
			{
				if (_inkVfx != null)
				{
					_inkVfx.Stop();
				}
				if (_inkVfx != null && _inkVfxStartPoint != null)
				{
					_inkVfx.transform.position = _inkVfxStartPoint.position;
				}
				_inkMoveTween = null;
			});
		}

		private void KillInkTween()
		{
			if (_inkMoveTween != null)
			{
				_inkMoveTween.Kill();
				_inkMoveTween = null;
				if (_inkVfx != null)
				{
					_inkVfx.Stop();
				}
				if (_inkVfx != null && _inkVfxStartPoint != null)
				{
					_inkVfx.transform.position = _inkVfxStartPoint.position;
				}
			}
		}

		protected override void OnHookedStateChanged(bool hooked)
		{
			base.OnHookedStateChanged(hooked);
			if (!hooked)
			{
				ClearLastInkTargetEffects();
			}
		}

		protected override void ApplyAnim(CreatureAnim anim)
		{
			ChangeLocalSquidAnim(anim);
		}

		private void ChangeLocalSquidAnim(CreatureAnim anim)
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
			case CreatureAnim.Struggle:
				ResetLocomotionAnimFlags();
				_animator.SetBool("Struggle", value: true);
				break;
			case CreatureAnim.Attack:
				ResetLocomotionAnimFlags();
				_animator.SetTrigger("Attack");
				break;
			case CreatureAnim.AttackSwim:
				ResetLocomotionAnimFlags();
				_animator.SetBool("AttackSwim", value: true);
				break;
			case CreatureAnim.Hit:
				_animator.ResetTrigger("CriticalHit");
				_animator.SetTrigger("Hit");
				break;
			case CreatureAnim.CriticalHit:
				_animator.ResetTrigger("Hit");
				_animator.SetTrigger("CriticalHit");
				break;
			case CreatureAnim.SpecialStart:
				_animator.ResetTrigger("SpecialEnd");
				_animator.SetTrigger("SpecialStart");
				break;
			case CreatureAnim.SpecialEnd:
				_animator.ResetTrigger("SpecialStart");
				_animator.SetTrigger("SpecialEnd");
				break;
			case CreatureAnim.Dead:
				ApplyDeadAnim();
				break;
			}
		}

		private void ApplyDeadAnim()
		{
			if (!(_animator == null))
			{
				ResetLocomotionAnimFlags();
				ResetCombatAnimTriggers();
				_animator.SetBool("Dead", value: true);
				_animator.Update(0f);
			}
		}

		private void ResetAnimatorAliveState()
		{
			if (!(_animator == null))
			{
				ResetLocomotionAnimFlags();
				ResetCombatAnimTriggers();
				_animator.SetBool("Dead", value: false);
			}
		}

		private void ResetLocomotionAnimFlags()
		{
			if (!(_animator == null))
			{
				_animator.SetBool("Run", value: false);
				_animator.SetBool("Struggle", value: false);
				_animator.SetBool("AttackSwim", value: false);
			}
		}

		private void ResetCombatAnimTriggers()
		{
			if (!(_animator == null))
			{
				_animator.ResetTrigger("Attack");
				_animator.ResetTrigger("Hit");
				_animator.ResetTrigger("CriticalHit");
				_animator.ResetTrigger("SpecialStart");
				_animator.ResetTrigger("SpecialEnd");
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(2255680183u, __rpc_handler_2255680183, "ServerEndSquidInkResponseRpc", RpcInvokePermission.Everyone);
			__registerRpc(3969621731u, __rpc_handler_3969621731, "ClientThrowInkRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_2255680183(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((SquidCreature)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((SquidCreature)target).ServerEndSquidInkResponseRpc(value);
				((SquidCreature)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3969621731(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				((SquidCreature)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((SquidCreature)target).ClientThrowInkRpc(value);
				((SquidCreature)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "SquidCreature";
		}
	}
}
