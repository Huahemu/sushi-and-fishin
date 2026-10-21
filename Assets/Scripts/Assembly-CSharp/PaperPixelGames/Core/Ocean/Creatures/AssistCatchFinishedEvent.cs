namespace PaperPixelGames.Core.Ocean.Creatures
{
	public readonly struct AssistCatchFinishedEvent : IEvent
	{
		public readonly CatchableSeaCreature Creature;

		public readonly ulong PrimaryClientId;

		public readonly ulong FinisherClientId;

		public AssistCatchFinishedEvent(CatchableSeaCreature creature, ulong primaryClientId, ulong finisherClientId)
		{
			Creature = creature;
			PrimaryClientId = primaryClientId;
			FinisherClientId = finisherClientId;
		}
	}
}
