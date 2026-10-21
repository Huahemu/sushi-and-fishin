namespace PaperPixelGames.Core.Ocean.Creatures
{
	public readonly struct AssistCatchParticipantCountChangedEvent : IEvent
	{
		public readonly CatchableSeaCreature Creature;

		public readonly int ParticipantCount;

		public AssistCatchParticipantCountChangedEvent(CatchableSeaCreature creature, int participantCount)
		{
			Creature = creature;
			ParticipantCount = participantCount;
		}
	}
}
