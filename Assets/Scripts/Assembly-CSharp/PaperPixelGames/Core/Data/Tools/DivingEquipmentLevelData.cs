using UnityEngine;

namespace PaperPixelGames.Core.Data.Tools
{
	[CreateAssetMenu(fileName = "DivingEquipmentLevelData", menuName = "Data/PlayerTools/DivingEquipmentLevel")]
	public class DivingEquipmentLevelData : BaseLevelData
	{
		public float MaxOxygen = 100f;

		public float BaseDrainPerSec = 0.2f;

		public float DepthPenaltyStart = 30f;

		public float DeepPenaltyTickInterval = 1f;

		public int DeepPenaltyOxygenCost = 1;
	}
}
