using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	public interface IAnimSphereHitReceiver
	{
		bool CanReceiveAnimSphereHit { get; }

		void ReceiveAnimSphereHit(Collider hitCollider);
	}
}
