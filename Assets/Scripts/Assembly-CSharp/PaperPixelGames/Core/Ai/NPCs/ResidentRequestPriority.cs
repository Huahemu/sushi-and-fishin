namespace PaperPixelGames.Core.Ai.NPCs
{
	public enum ResidentRequestPriority
	{
		FallbackIdle = 0,
		ScheduledActivity = 10,
		Work = 20,
		PlayerInteraction = 30,
		RestaurantAssignment = 40,
		HardReset = 90,
		SnapshotApplying = 100
	}
}
