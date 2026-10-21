using PaperPixelGames.Core.Player;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	public readonly struct CreatureReleasedEvent : IEvent
	{
		public readonly CatchableSeaCreature Creature;

		public readonly PPG_Player Owner;

		public CreatureReleasedEvent(CatchableSeaCreature creature, PPG_Player owner)
		{
			Creature = creature;
			Owner = owner;
		}
	}
}
