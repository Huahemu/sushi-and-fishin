using System.Collections.Generic;
using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Data.Tools
{
	[CreateAssetMenu(fileName = "DivingEquipmentData", menuName = "Data/PlayerTools/DivingEquipment")]
	public class DivingEquipmentData : BaseToolData
	{
		public ToolType ToolType;

		public List<DivingEquipmentLevelData> Levels;
	}
}
