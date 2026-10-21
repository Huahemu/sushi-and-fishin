using System.Collections;
using UnityEngine;

namespace PilotoStudio
{
	[ExecuteAlways]
	public class ParticleHandler : MonoBehaviour
	{
		public GameObject castParticle;

		public float castFXDuration;

		public GameObject loopingParticle;

		public float loopDuration;

		public GameObject endParticle;

		private ParticleSystem castParticleSystem;

		private ParticleSystem loopingParticleSystem;

		private ParticleSystem endParticleSystem;

		private float startEmission;

		private void OnEnable()
		{
			castParticleSystem = castParticle.GetComponent<ParticleSystem>();
			loopingParticleSystem = loopingParticle.GetComponent<ParticleSystem>();
			endParticleSystem = endParticle.GetComponent<ParticleSystem>();
			if (!castParticleSystem || !loopingParticleSystem || !endParticleSystem)
			{
				Debug.LogError("ParticleHandler: Missing particle systems. Ensure they are referenced correctly.");
			}
			else
			{
				Cast();
			}
		}

		public void Cast()
		{
			StartCoroutine(Flow());
		}

		private IEnumerator Flow()
		{
			PlayParticles(castParticleSystem, castFXDuration);
			yield return new WaitForSeconds(castFXDuration);
			PlayParticles(loopingParticleSystem, loopDuration);
			yield return new WaitForSeconds(loopDuration);
			PlayParticles(endParticleSystem);
			yield return WaitUntilParticleSystemStops(endParticleSystem);
		}

		private IEnumerator WaitUntilParticleSystemStops(ParticleSystem particleSystem)
		{
			while (particleSystem.IsAlive(withChildren: true))
			{
				yield return null;
			}
		}

		private void PlayParticles(ParticleSystem particleSystem, float duration = 0f)
		{
			particleSystem.gameObject.SetActive(value: true);
			ParticleSystem.EmissionModule emission = particleSystem.emission;
			if (startEmission == 0f)
			{
				startEmission = emission.rateOverTimeMultiplier;
			}
			if (particleSystem.main.startLifetime.constantMax == float.PositiveInfinity)
			{
				StartCoroutine(WaitUntilParticleSystemStops(particleSystem));
			}
			else
			{
				emission.rateOverTimeMultiplier = startEmission;
			}
			particleSystem.Play();
			if (duration > 0f && particleSystem.main.startLifetime.constantMax != float.PositiveInfinity)
			{
				StartCoroutine(StopParticleAfterTime(particleSystem, duration));
			}
		}

		private IEnumerator StopParticleAfterTime(ParticleSystem particleSystem, float duration)
		{
			yield return new WaitForSeconds(duration);
			ParticleSystem.EmissionModule emission = particleSystem.emission;
			emission.rateOverTimeMultiplier = 0f;
		}
	}
}
