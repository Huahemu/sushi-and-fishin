using UnityEngine;

namespace PixPlays.ElementalVFX
{
	public class WaterShield : Shield
	{
		[SerializeField]
		private ParticleSystem _HitEffectPrefab;

		[SerializeField]
		private Animation _Anim;

		[SerializeField]
		private AnimationClip _SpawnAnimation;

		[SerializeField]
		private AnimationClip _DespawnAnimation;

		[SerializeField]
		private ParticleSystem _WaterAdditionalParticles;

		protected override void HitImplementation(Vector3 point, Vector3 normal)
		{
			ParticleSystem particleSystem = Object.Instantiate(_HitEffectPrefab, point, Quaternion.identity);
			particleSystem.transform.forward = normal;
			particleSystem.Play();
		}

		protected override void PlayImplementation()
		{
			StopAllCoroutines();
			base.gameObject.SetActive(value: true);
			_WaterAdditionalParticles.Play();
			_Anim.clip = _SpawnAnimation;
			_Anim.Play();
		}

		protected override void StopImplemenation()
		{
			_WaterAdditionalParticles.Stop();
			_Anim.clip = _DespawnAnimation;
			_Anim.Play();
			StopAllCoroutines();
		}
	}
}
