using PaperPixelGames.Core.Player;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	public readonly struct CreatureDiedEvent : IEvent
	{
		public readonly SeaCreature Creature;

		public readonly PPG_Player Killer;

		public readonly Vector3 DeathPosition;

		public CreatureDiedEvent(SeaCreature creature, PPG_Player killer, Vector3 deathPosition)
		{
			Creature = creature;
			Killer = killer;
			DeathPosition = deathPosition;
		}
	}
}
