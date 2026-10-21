using UnityEngine;

namespace PaperPixelGames.Core.Data.Tools
{
	[CreateAssetMenu(fileName = "SeaScooterLevelData", menuName = "Data/PlayerTools/SeaScooterLevel")]
	public class SeaScooterLevelData : BaseLevelData
	{
		[Header("Base Movement")]
		[Tooltip("The maximum sea scooter speed.")]
		public float MaxSeaScooterSpeed;

		[Tooltip("Deceleration when using the sea scooter and not applying acceleration.")]
		public float BrakingDecelerationSeaScooter;

		[Tooltip("Friction to apply to movement when using the sea scooter.")]
		public float SeaScooterFriction;
	}
}
