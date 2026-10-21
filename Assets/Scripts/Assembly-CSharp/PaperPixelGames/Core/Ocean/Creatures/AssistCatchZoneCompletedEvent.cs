namespace PaperPixelGames.Core.Ocean.Creatures
{
	public readonly struct AssistCatchZoneCompletedEvent : IEvent
	{
		public readonly CatchableSeaCreature Creature;

		public readonly int ZoneIndex;

		public readonly float GlobalProgress01;

		public AssistCatchZoneCompletedEvent(CatchableSeaCreature creature, int zoneIndex, float globalProgress01)
		{
			Creature = creature;
			ZoneIndex = zoneIndex;
			GlobalProgress01 = globalProgress01;
		}
	}
}
