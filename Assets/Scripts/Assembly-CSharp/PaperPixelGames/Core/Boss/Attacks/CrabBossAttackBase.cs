using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Boss.Attacks
{
	[RequireComponent(typeof(NetworkObject))]
	[RequireComponent(typeof(CrabBossController))]
	public abstract class CrabBossAttackBase : NetworkBehaviour, ICrabBossAttack
	{
		[Header("Attack Identity")]
		[SerializeField]
		private CrabBossAttackId _attackId;

		private uint _activeToken;

		public CrabBossAttackId AttackId => _attackId;

		public virtual float ClientAnimationStartDelay => 0f;

		protected CrabBossController Boss { get; private set; }

		protected CrabBossPresentationController Presentation { get; private set; }

		protected uint ActiveToken => _activeToken;

		protected virtual void Awake()
		{
			Boss = GetComponent<CrabBossController>();
			Presentation = GetComponent<CrabBossPresentationController>();
		}

		public virtual bool ServerCanSelectAttack()
		{
			if (base.IsServer)
			{
				return base.isActiveAndEnabled;
			}
			return false;
		}

		public override void OnNetworkDespawn()
		{
			_activeToken = 0u;
			base.OnNetworkDespawn();
		}

		public bool ValidateAttack(out string error)
		{
			if (_attackId == CrabBossAttackId.None)
			{
				error = GetType().Name + " Attack ID is empty.";
				return false;
			}
			if (Boss == null)
			{
				error = GetType().Name + " CrabBossController is missing.";
				return false;
			}
			return ValidateSpecificSettings(out error);
		}

		public bool ServerStartAttack(CrabBossAttackCommand command)
		{
			if (!base.IsServer || !base.isActiveAndEnabled || Boss == null || command.Token == 0)
			{
				return false;
			}
			if (_attackId != command.AttackId)
			{
				return false;
			}
			_activeToken = command.Token;
			OnServerAttackStarted(command);
			return true;
		}

		public void ServerEndAttack(CrabBossAttackCommand command, CrabBossAttackEndReason reason)
		{
			if (base.IsServer && command.Token != 0 && command.Token == _activeToken)
			{
				_activeToken = 0u;
				OnServerAttackEnded(command, reason);
			}
		}

		public void ServerForceCleanup()
		{
			if (base.IsServer)
			{
				uint activeToken = _activeToken;
				_activeToken = 0u;
				OnServerForceCleanup(activeToken);
			}
		}

		public virtual void ServerSyncLateJoinClient(ulong clientId)
		{
		}

		protected bool CompleteAttack(uint token)
		{
			if (!base.IsServer || Boss == null || token == 0 || token != _activeToken)
			{
				return false;
			}
			return Boss.ServerCompleteAttack(token);
		}

		protected abstract bool ValidateSpecificSettings(out string error);

		protected abstract void OnServerAttackStarted(CrabBossAttackCommand command);

		protected abstract void OnServerAttackEnded(CrabBossAttackCommand command, CrabBossAttackEndReason reason);

		protected virtual void OnServerForceCleanup(uint activeToken)
		{
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "CrabBossAttackBase";
		}
	}
}
