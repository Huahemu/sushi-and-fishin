using PaperPixelGames.Core.Player;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	public readonly struct CreatureCaughtEvent : IEvent
	{
		public readonly CatchableSeaCreature Creature;

		public readonly PPG_Player Owner;

		public readonly float Weight;

		public readonly bool CanHold;

		public CreatureCaughtEvent(CatchableSeaCreature creature, PPG_Player owner, float weight, bool canHold)
		{
			Creature = creature;
			Owner = owner;
			Weight = weight;
			CanHold = canHold;
		}
	}
}
