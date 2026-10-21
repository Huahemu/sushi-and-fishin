using System;
using UnityEngine;

namespace PaperPixelGames.Core.Data.Tools
{
	[Serializable]
	public sealed class RodDifficultyMatchup
	{
		public FishingRodDifficulty Difficulty = FishingRodDifficulty.Level1;

		[Tooltip("Multiplies this difficulty's fish selection weight for this rod level.")]
		[Min(0.001f)]
		public float SelectionWeightMultiplier = 1f;

		[Min(0.05f)]
		public float CatchProgressMultiplier = 1f;

		[Min(0.05f)]
		public float TensionGainMultiplier = 1f;

		[Min(0f)]
		public float TensionRecoveryMultiplier = 1f;

		[Min(0.05f)]
		public float CounterDurationMultiplier = 1f;

		[Range(0f, 1f)]
		public float CounterForgiveness = 0.2f;

		public static RodDifficultyMatchup CreateRuntimeFallback(FishingRodDifficulty difficulty, int rodToFishDifference)
		{
			float num = Mathf.Clamp(rodToFishDifference, -4, 4);
			return new RodDifficultyMatchup
			{
				Difficulty = difficulty,
				SelectionWeightMultiplier = ((num < 0f) ? Mathf.Pow(0.45f, 0f - num) : Mathf.Lerp(1f, 0.6f, num / 4f)),
				CatchProgressMultiplier = Mathf.Clamp(1f + num * 0.12f, 0.55f, 1.5f),
				TensionGainMultiplier = Mathf.Clamp(1f - num * 0.1f, 0.55f, 1.55f),
				TensionRecoveryMultiplier = Mathf.Clamp(1f + num * 0.08f, 0.6f, 1.4f),
				CounterDurationMultiplier = Mathf.Clamp(1f - num * 0.08f, 0.65f, 1.4f),
				CounterForgiveness = Mathf.Clamp01(0.2f + num * 0.025f)
			};
		}
	}
}
