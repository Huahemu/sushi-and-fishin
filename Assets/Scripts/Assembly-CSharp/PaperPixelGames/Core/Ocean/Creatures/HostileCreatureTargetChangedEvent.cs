using PaperPixelGames.Core.Player;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	public readonly struct HostileCreatureTargetChangedEvent : IEvent
	{
		public readonly HostileSeaCreature Creature;

		public readonly PPG_Player OldTarget;

		public readonly PPG_Player NewTarget;

		public HostileCreatureTargetChangedEvent(HostileSeaCreature creature, PPG_Player oldTarget, PPG_Player newTarget)
		{
			Creature = creature;
			OldTarget = oldTarget;
			NewTarget = newTarget;
		}
	}
}
