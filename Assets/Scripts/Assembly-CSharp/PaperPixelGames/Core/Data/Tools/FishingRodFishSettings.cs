using System;
using UnityEngine;

namespace PaperPixelGames.Core.Data.Tools
{
	[Serializable]
	public sealed class FishingRodFishSettings
	{
		public bool Enabled = true;

		public FishingRodDifficulty Difficulty = FishingRodDifficulty.Level1;

		[Min(0.01f)]
		public float SelectionWeight = 1f;

		[Min(0f)]
		public float LeftWeight = 1f;

		[Min(0f)]
		public float RightWeight = 1f;

		[Min(0f)]
		public float ForwardWeight = 0.75f;

		[Min(0.1f)]
		public float MovementDuration = 2.2f;

		[Min(0.05f)]
		public float CounterRequiredDuration = 0.45f;

		[Min(0.1f)]
		public float CenteredDuration = 1.8f;

		[Range(0f, 1f)]
		public float TiredChance = 0.2f;

		[Min(0.1f)]
		public float TiredDuration = 3f;

		[Min(0f)]
		public float CatchProgressPerSecond = 24f;

		[Min(0f)]
		public float TensionGainPerSecond = 22f;

		[Min(0f)]
		public float TensionRecoveryPerSecond = 10f;

		[Min(1f)]
		public float WrongReelTensionMultiplier = 1.8f;

		[Min(0f)]
		public float PresentationStrength = 1f;
	}
}
