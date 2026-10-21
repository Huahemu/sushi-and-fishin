namespace PaperPixelGames.Core.Boss.Attacks
{
	public interface ICrabBossAttack
	{
		CrabBossAttackId AttackId { get; }

		bool ValidateAttack(out string error);

		bool ServerStartAttack(CrabBossAttackCommand command);

		void ServerEndAttack(CrabBossAttackCommand command, CrabBossAttackEndReason reason);
	}
}
