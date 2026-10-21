namespace PaperPixelGames.Core.Boss
{
	public readonly struct CrabBossAttackCommand
	{
		public CrabBossAttackId AttackId { get; }

		public uint Token { get; }

		public CrabBossAttackCommand(CrabBossAttackId attackId, uint token)
		{
			AttackId = attackId;
			Token = token;
		}
	}
}
