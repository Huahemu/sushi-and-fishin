namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::UnityEngine;

namespace EpicToonFX
{
	public class ETFXLoopScript : MonoBehaviour
	{
		public GameObject chosenEffect;

		public float loopTimeLimit = 2f;

		[Header("Spawn options")]
		public bool disableLights = true;

		public bool disableSound = true;

		public float spawnScale = 1f;

		private void Start()
		{
			PlayEffect();
		}

		public void PlayEffect()
		{
			StartCoroutine("EffectLoop");
		}

		private IEnumerator EffectLoop()
		{
			GameObject effectPlayer = Object.Instantiate(chosenEffect, base.transform.position, base.transform.rotation);
			effectPlayer.transform.localScale = new Vector3(spawnScale, spawnScale, spawnScale);
			if (disableLights && (bool)effectPlayer.GetComponent<Light>())
			{
				effectPlayer.GetComponent<Light>().enabled = false;
			}
			if (disableSound && (bool)effectPlayer.GetComponent<AudioSource>())
			{
				effectPlayer.GetComponent<AudioSource>().enabled = false;
			}
			yield return new WaitForSeconds(loopTimeLimit);
			Object.Destroy(effectPlayer);
			PlayEffect();
		}
	}
}

}
