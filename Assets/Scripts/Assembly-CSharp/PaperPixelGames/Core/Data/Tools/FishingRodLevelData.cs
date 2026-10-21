using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PaperPixelGames.Core.Data.Tools
{
	[CreateAssetMenu(fileName = "FishingRodLevelData", menuName = "Data/PlayerTools/FishingRodLevel")]
	public sealed class FishingRodLevelData : BaseLevelData
	{
		[Tooltip("Unscaled full-charge distance. FishingRodData converts it to world meters.")]
		[Min(1f)]
		public float MaxCastDistance = 20f;

		public List<RodDifficultyMatchup> DifficultyMatchups = new List<RodDifficultyMatchup>();

		public RodDifficultyMatchup GetMatchup(FishingRodDifficulty difficulty)
		{
			RodDifficultyMatchup rodDifficultyMatchup = DifficultyMatchups?.FirstOrDefault((RodDifficultyMatchup entry) => entry != null && entry.Difficulty == difficulty);
			if (rodDifficultyMatchup != null)
			{
				return rodDifficultyMatchup;
			}
			int num = (int)(difficulty - 1);
			int rodToFishDifference = Level - num;
			return RodDifficultyMatchup.CreateRuntimeFallback(difficulty, rodToFishDifference);
		}
	}
}
