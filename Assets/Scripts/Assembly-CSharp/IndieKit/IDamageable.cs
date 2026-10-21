using UnityEngine;

namespace IndieKit
{
	public interface IDamageable
	{
		void ApplyDamage(float damage, Vector3 hitPoint);
	}
}
