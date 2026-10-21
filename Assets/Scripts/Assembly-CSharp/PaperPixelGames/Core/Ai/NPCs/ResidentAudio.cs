using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentAudio : MonoBehaviour
	{
		[SerializeField]
		private AudioClipSO _greeting;

		[SerializeField]
		private AudioClipSO _farewell;

		[SerializeField]
		private AudioClipSO _quest;

		[SerializeField]
		private AudioClipSO _joke;

		[SerializeField]
		private AudioClipSO _laugh;

		public void Play(ResidentSounds sound)
		{
			AudioClipSO audioClipSO = sound switch
			{
				ResidentSounds.Greeting => _greeting, 
				ResidentSounds.Farewell => _farewell, 
				ResidentSounds.Quest => _quest, 
				ResidentSounds.Joke => _joke, 
				ResidentSounds.Laugh => _laugh, 
				_ => null, 
			};
			if (!(audioClipSO == null) && !(Singleton<SoundManager>.Instance == null))
			{
				Singleton<SoundManager>.Instance.PlaySFX(audioClipSO, base.transform.position, base.transform);
			}
		}
	}
}
