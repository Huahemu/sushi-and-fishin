using UnityEngine;

namespace PixPlays.ElementalVFX
{
	public class ParticleSystemShield : Shield
	{
		[SerializeField]
		private ParticleSystem _ShieldEffect;

		[SerializeField]
		private ParticleSystem _HitEffectPrefab;

		protected override void HitImplementation(Vector3 point, Vector3 normal)
		{
			ParticleSystem particleSystem = Object.Instantiate(_HitEffectPrefab, point, Quaternion.identity);
			particleSystem.transform.forward = normal;
			particleSystem.Play();
		}

		protected override void PlayImplementation()
		{
			_ShieldEffect.Play();
		}

		protected override void StopImplemenation()
		{
			_ShieldEffect.Stop();
		}
	}
}
