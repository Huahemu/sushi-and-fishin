using PaperPixelGames.Core.Player;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	public readonly struct CreatureHitEvent : IEvent
	{
		public readonly SeaCreature Creature;

		public readonly PPG_Player Hitter;

		public readonly Vector3 HitPosition;

		public CreatureHitEvent(SeaCreature creature, PPG_Player hitter, Vector3 hitPosition)
		{
			Creature = creature;
			Hitter = hitter;
			HitPosition = hitPosition;
		}
	}
}
