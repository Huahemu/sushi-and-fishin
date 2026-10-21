using System.Collections;
using UnityEngine;

namespace Michsky.LSS
{
	public class LSS_AudioSource : MonoBehaviour
	{
		[HideInInspector]
		public AudioSource audioSource;

		[HideInInspector]
		public float audioFadeDuration;

		public void DoFadeOut()
		{
			Object.DontDestroyOnLoad(base.gameObject);
			StartCoroutine("FadeOutAudioSource");
		}

		private IEnumerator FadeOutAudioSource()
		{
			float elapsedTime = 0f;
			while (elapsedTime < audioFadeDuration)
			{
				audioSource.volume = Mathf.Lerp(audioSource.volume, 0f, elapsedTime / audioFadeDuration);
				elapsedTime += Time.unscaledDeltaTime;
				yield return null;
			}
			Object.Destroy(base.gameObject);
		}
	}
}
