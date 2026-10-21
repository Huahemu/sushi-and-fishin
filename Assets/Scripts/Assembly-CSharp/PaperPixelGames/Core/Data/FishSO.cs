using PaperPixelGames.Core.Data.Tools;
using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "Fish", menuName = "Data/FishSO")]
	public class FishSO : CreatureSO
	{
		public ItemOffset FilletOffset;

		public bool FilletBigInput;

		public float StartMagnetFactor = 0.2f;

		public float EndMagnetFactor = 1f;

		public float MinWeight = 0.01f;

		public float MaxWeight = 1f;

		public float DeadFishSalePrice;

		public float StackOffset = 0.1f;

		public FishingMode FishingMode;

		public BaitType LikedBaits;

		public FishingRodFishSettings FishingRodSettings = new FishingRodFishSettings();

		[Tooltip("Local fish pose relative to the cable attachment point while waiting for collection.")]
		public ItemOffset FishingRodCollectOffset = new ItemOffset(Vector3.zero, new Vector3(25f, 180f, 90f));

		[Tooltip("Exact local rotation and scale for the trophy visual. Position is applied after the visual is centered on the trophy socket.")]
		public ItemOffset TrophyOffset = new ItemOffset(Vector3.zero, Vector3.zero);

		[Tooltip("Whether this creature can participate in the restaurant fish trophy display.")]
		public bool CanDisplayAsTrophy = true;

		[Tooltip("Authoring-time trophy visual prefab. Fish-specific Bend Deformer settings live on this prefab.")]
		public GameObject TrophyGraphicPrefab;

		public ColliderType ColliderShape;

		public Vector3 ColliderCenter;

		public float ColliderRadius;

		public int CapsuleDirection = 2;

		public float ColliderHeight;

		public Vector3 HookPointOffset;

		public Vector3 BoxSize;

		public bool UseDeadCollider;

		public ColliderType DeadColliderShape;

		public Vector3 DeadColliderCenter;

		public float DeadColliderRadius;

		public int DeadCapsuleDirection = 2;

		public float DeadColliderHeight;

		public Vector3 DeadBoxCenter;

		public Vector3 DeadBoxSize;

		[Header("DeformPresets")]
		public SinePreset MovePreset;

		public SinePreset SprintPreset;

		public SinePreset StrugglePreset;

		public int RollFilletYield()
		{
			return Random.Range(MinYieldRange, MaxYieldRange);
		}

		public float GetRandomWeight()
		{
			return Random.Range(MinWeight, MaxWeight);
		}

		public override string ToString()
		{
			return "<b><color=cyan>[Fish]</color></b> " + Name + " | " + $"<color=cyan>Mode:</color> {FishingMode} | " + $"<color=cyan>Baits:</color> {LikedBaits} | " + $"<color=cyan>Layer:</color> {ZoneLayer} | " + $"<color=cyan>Rarity:</color> {Rarity} | " + $"<color=cyan>Size:</color> {Size}";
		}
	}
}
