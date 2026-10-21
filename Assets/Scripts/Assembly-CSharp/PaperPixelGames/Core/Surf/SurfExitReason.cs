namespace PaperPixelGames.Core.Surf
{
	public enum SurfExitReason : byte
	{
		Drop = 0,
		Shore = 1,
		Stuck = 2,
		Knocked = 3,
		Disconnect = 4,
		EndDay = 5,
		Teleport = 6,
		Despawn = 7,
		OutOfBounds = 8
	}
}
