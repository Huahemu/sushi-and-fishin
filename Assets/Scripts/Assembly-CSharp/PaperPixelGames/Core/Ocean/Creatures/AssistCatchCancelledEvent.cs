namespace PaperPixelGames.Core.Ocean.Creatures
{
	public readonly struct AssistCatchCancelledEvent : IEvent
	{
		public readonly CatchableSeaCreature Creature;

		public readonly ulong PrimaryClientId;

		public readonly AssistCatchCancelReason Reason;

		public AssistCatchCancelledEvent(CatchableSeaCreature creature, ulong primaryClientId, AssistCatchCancelReason reason)
		{
			Creature = creature;
			PrimaryClientId = primaryClientId;
			Reason = reason;
		}
	}
}
