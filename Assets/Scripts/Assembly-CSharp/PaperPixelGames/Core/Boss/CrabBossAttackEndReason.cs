namespace PaperPixelGames.Core.Boss
{
	public enum CrabBossAttackEndReason : byte
	{
		Completed = 0,
		TimedOut = 1,
		PhaseChanged = 2,
		FightStopped = 3,
		BossSleeping = 4,
		FailedToStart = 5
	}
}
