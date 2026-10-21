using System.Collections.Generic;
using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Data.Tools
{
	[CreateAssetMenu(fileName = "HarpoonData", menuName = "Data/PlayerTools/Harpoon")]
	public class HarpoonData : BaseToolData
	{
		public ToolType ToolType;

		public List<HarpoonLevelData> Levels;

		[Space]
		public HarpoonChallengePreset SpamCommon;

		public HarpoonChallengePreset SpamRare;

		public HarpoonChallengePreset SpamEpic;

		public HarpoonChallengePreset SpamLegendary;

		public HarpoonChallengePreset SpamMystic;

		[Space]
		public HarpoonChallengePreset PrecisionEasy;

		public HarpoonChallengePreset PrecisionNormal;

		public HarpoonChallengePreset PrecisionHard;

		public HarpoonChallengePreset GetPreset(FishingMode mode, CreatureRarity rarity)
		{
			return mode switch
			{
				FishingMode.Spam => rarity switch
				{
					CreatureRarity.Common => SpamCommon, 
					CreatureRarity.Rare => SpamRare, 
					CreatureRarity.Epic => SpamEpic, 
					CreatureRarity.Legendary => SpamLegendary, 
					CreatureRarity.Mystic => SpamMystic, 
					_ => SpamCommon, 
				}, 
				FishingMode.Precision => rarity switch
				{
					CreatureRarity.Common => PrecisionEasy, 
					CreatureRarity.Rare => PrecisionEasy, 
					CreatureRarity.Epic => PrecisionNormal, 
					CreatureRarity.Legendary => PrecisionNormal, 
					CreatureRarity.Mystic => PrecisionHard, 
					_ => PrecisionEasy, 
				}, 
				_ => default, 
			};
		}
	}
}
