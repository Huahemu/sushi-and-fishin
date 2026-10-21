using UnityEngine;

namespace PaperPixelGames.Core.Data.Tools
{
	[CreateAssetMenu(fileName = "HarpoonLevelData", menuName = "Data/PlayerTools/HarpoonLevel")]
	public class HarpoonLevelData : BaseLevelData
	{
		public ItemOffset TipGraphicOffset;

		[Space]
		public float SuccessRangeFactor;

		public float IncreasePerInput;

		public float DecreasePerSecond;

		public float HoldTime;

		[Tooltip("Base time available for a fish challenge before rarity difficulty is applied.")]
		[Min(0.1f)]
		public float BaseChallengeDuration = 20f;

		[Tooltip("Reduces the duration penalty from difficult fish. Higher values provide more time against harder fish.")]
		[Min(0.01f)]
		public float ChallengeDifficultyResistance = 1f;

		public float ShootRange;

		public float ShootSpeed;

		public float HarpoonForce = 5f;

		[Header("Combat")]
		[Tooltip("Server-authoritative damage dealt to hostile sea creatures per confirmed hit.")]
		[Min(1f)]
		public byte HostileDamage = 1;

		[Tooltip("Minimum launch speed when releasing from a harpoon grapple point.")]
		[Min(0f)]
		public float ReleaseSpeed = 5f;
	}
}
