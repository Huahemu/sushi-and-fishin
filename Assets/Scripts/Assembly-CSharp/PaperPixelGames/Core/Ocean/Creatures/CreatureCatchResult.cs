using PaperPixelGames.Core.Player;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	public readonly struct CreatureCatchResult
	{
		public readonly CatchableSeaCreature Creature;

		public readonly PPG_Player Owner;

		public readonly float Weight;

		public CreatureCatchResult(CatchableSeaCreature creature, PPG_Player owner, float weight)
		{
			Creature = creature;
			Owner = owner;
			Weight = weight;
		}
	}
}
