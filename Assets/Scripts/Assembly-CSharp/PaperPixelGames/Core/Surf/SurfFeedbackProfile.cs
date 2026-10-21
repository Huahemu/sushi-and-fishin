using PaperPixelGames.Core.Data;
using UnityEngine;

namespace PaperPixelGames.Core.Surf
{
	[CreateAssetMenu(menuName = "Data/Surf feedback", fileName = "SurfFeedback")]
	public sealed class SurfFeedbackProfile : ScriptableObject
	{
		public AudioClipSO paddle;

		public AudioClipSO glideLoop;

		public AudioClipSO windLoop;

		public AudioClipSO mount;

		public AudioClipSO stance;

		public AudioClipSO catchWave;
	}
}
