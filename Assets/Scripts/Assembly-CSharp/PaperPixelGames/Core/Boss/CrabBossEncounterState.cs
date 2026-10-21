namespace PaperPixelGames.Core.Boss
{
	public enum CrabBossEncounterState : byte
	{
		Inactive = 0,
		Shielded = 1,
		Vulnerable = 2,
		PhaseTransition = 3,
		Defeated = 4,
		Sleeping = 5
	}
}
