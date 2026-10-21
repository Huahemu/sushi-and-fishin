namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::UnityEngine;

namespace EpicToonFX
{
	public class ETFXTarget : MonoBehaviour
	{
		public TargetEffects effects;

		[Header("General Settings")]
		public int hitsToDestroy = 5;

		public float respawnTime = 3f;

		[Header("Squash & Stretch")]
		public bool enableSquashAndStretch = true;

		public float duration = 0.07f;

		public Vector3 squashScale = new Vector3(0.8f, 1.2f, 1f);

		public Vector3 stretchScale = new Vector3(1.2f, 0.8f, 1f);

		private Renderer targetRenderer;

		private Collider targetCollider;

		private AudioSource audioSource;

		private int currentHits;

		private Vector3 originalScale;

		private void Start()
		{
			targetRenderer = GetComponent<Renderer>();
			targetCollider = GetComponent<Collider>();
			audioSource = GetComponent<AudioSource>();
			originalScale = base.transform.localScale;
		}

		private void SpawnTarget()
		{
			targetRenderer.enabled = true;
			targetCollider.enabled = true;
			if ((bool)effects.respawnParticle)
			{
				Object.Destroy(Object.Instantiate(effects.respawnParticle, base.transform.position, base.transform.rotation), 3.5f);
			}
			if ((bool)effects.respawnSound && (bool)audioSource)
			{
				audioSource.PlayOneShot(effects.respawnSound);
			}
			currentHits = 0;
			base.transform.localScale = originalScale;
		}

		private IEnumerator Respawn()
		{
			yield return new WaitForSeconds(respawnTime);
			SpawnTarget();
		}

		public void OnHit()
		{
			currentHits++;
			if (currentHits >= hitsToDestroy)
			{
				DestroyTarget();
				return;
			}
			if ((bool)effects.hitParticle)
			{
				Object.Destroy(Object.Instantiate(effects.hitParticle, base.transform.position, base.transform.rotation), 2f);
			}
			if (enableSquashAndStretch)
			{
				StartCoroutine(SquashAndStretch());
			}
		}

		private IEnumerator SquashAndStretch()
		{
			float timeElapsed = 0f;
			Vector3 startScale = originalScale;
			Vector3 endScale = Vector3.Scale(originalScale, squashScale);
			while (timeElapsed < duration)
			{
				base.transform.localScale = Vector3.Lerp(startScale, endScale, timeElapsed / duration);
				timeElapsed += Time.deltaTime;
				yield return null;
			}
			timeElapsed = 0f;
			startScale = endScale;
			endScale = Vector3.Scale(originalScale, stretchScale);
			while (timeElapsed < duration)
			{
				base.transform.localScale = Vector3.Lerp(startScale, endScale, timeElapsed / duration);
				timeElapsed += Time.deltaTime;
				yield return null;
			}
			timeElapsed = 0f;
			startScale = endScale;
			endScale = originalScale;
			while (timeElapsed < duration)
			{
				base.transform.localScale = Vector3.Lerp(startScale, endScale, timeElapsed / duration);
				timeElapsed += Time.deltaTime;
				yield return null;
			}
		}

		private void DestroyTarget()
		{
			if (effects.deathParticles.Count > 0)
			{
				GameObject obj;
				if (effects.deathParticles.Count == 1)
				{
					obj = Object.Instantiate(effects.deathParticles[0], base.transform.position, base.transform.rotation);
				}
				else
				{
					int index = Random.Range(0, effects.deathParticles.Count);
					obj = Object.Instantiate(effects.deathParticles[index], base.transform.position, base.transform.rotation);
				}
				Object.Destroy(obj, 2f);
			}
			targetRenderer.enabled = false;
			targetCollider.enabled = false;
			if ((bool)effects.destroySound && (bool)audioSource)
			{
				audioSource.PlayOneShot(effects.destroySound);
			}
			StartCoroutine(Respawn());
		}
	}
}

}
