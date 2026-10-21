using System.Collections.Generic;
using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Data.Tools
{
	[CreateAssetMenu(fileName = "MopData", menuName = "Data/PlayerTools/Mop")]
	public class MopData : BaseToolData
	{
		public ToolType ToolType;

		public List<MopLevelData> Levels;
	}
}
