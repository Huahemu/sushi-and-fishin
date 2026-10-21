namespace PaperPixelGames.Core.Ai.NPCs
{
	public static class ResidentSnapshotPackedFlags
	{
		public const byte IsWorking = 1;

		public const byte IsAvailable = 2;

		public const byte IsSwimming = 4;

		public const byte HasNewEntry = 8;

		public const byte Pulse = 16;

		public const byte HasArrived = 32;

		public const byte HasValidTarget = 64;
	}
}
