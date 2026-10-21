using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace PaperPixelGames.Core.Data.Tools
{
	[Serializable]
	public struct HarpoonChallengePreset
	{
		[Tooltip("Total combined success width on the challenge bar. If multiple zones are used, this value is split between them.")]
		public float SuccessRange;

		[Tooltip("Minimum number of success zones that will be generated.")]
		[FormerlySerializedAs("SuccessZoneCount")]
		[Range(1f, 5f)]
		public int MinSuccessZoneCount;

		[Tooltip("Maximum number of success zones that will be generated. Existing assets with 0 will fallback to the minimum value at runtime.")]
		[Range(0f, 5f)]
		public int MaxSuccessZoneCount;

		[Tooltip("Multiplier applied to the bar decrease speed.")]
		public float DecreaseFactor;

		[Tooltip("Multiplier applied to the bar increase amount per input.")]
		public float IncreaseFactor;

		[Tooltip("Divides the total required hold time. Higher values make the challenge faster.")]
		public float HoldTimeDivider;

		[Tooltip("Seconds removed from the harpoon level's base challenge duration before level resistance is applied.")]
		[Min(0f)]
		public float ChallengeDurationPenalty;

		public int GetMinimumSuccessZoneCount()
		{
			return Mathf.Max(1, MinSuccessZoneCount);
		}

		public int GetRandomSuccessZoneCount()
		{
			int minimumSuccessZoneCount = GetMinimumSuccessZoneCount();
			int num = Mathf.Max(minimumSuccessZoneCount, MaxSuccessZoneCount);
			return UnityEngine.Random.Range(minimumSuccessZoneCount, num + 1);
		}
	}
}
