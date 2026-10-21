using System.Collections.Generic;
using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Data.Tools
{
	[CreateAssetMenu(fileName = "SeaScooterData", menuName = "Data/PlayerTools/SeaScooter")]
	public class SeaScooterData : BaseToolData
	{
		public ToolType ToolType;

		public List<SeaScooterLevelData> Levels;
	}
}
