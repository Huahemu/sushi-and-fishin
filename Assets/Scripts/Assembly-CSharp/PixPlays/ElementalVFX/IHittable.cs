using UnityEngine;

namespace PixPlays.ElementalVFX
{
	public interface IHittable
	{
		void OnHit(Vector3 hitPoint, Vector3 normal);
	}
}
