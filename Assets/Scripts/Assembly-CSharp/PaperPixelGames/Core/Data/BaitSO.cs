using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "NewBait", menuName = "Data/BaitSO")]
	public class BaitSO : HoldableSO, IWheelSlice
	{
		[Tooltip("Defines the logical category of this bait")]
		public BaitType BaitType;

		[Tooltip("Determines the quality of fish this bait can attract")]
		public CreatureRarity BaitRarity;

		[Tooltip("Adds extra chance (e.g. 0.1f = +10% catch chance)")]
		[Range(0f, 1f)]
		public float CatchChanceBonus;

		[Tooltip("Reduces or increases wait time (e.g. 0.8 = 20% faster)")]
		[Min(0.1f)]
		public float WaitTimeMultiplier = 1f;

		[Tooltip("Local wait time before the Fishing Rod challenge starts.")]
		[Min(0.1f)]
		public float RodWaitDuration = 8f;

		public bool IsFreeFishingBait => BaitType == BaitType.Bread;

		public string WheelName => Name;

		public Sprite WheelSprite => Sprite;
	}
}
