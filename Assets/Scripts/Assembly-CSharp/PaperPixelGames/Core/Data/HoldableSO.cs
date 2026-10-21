using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Framework.Enums;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "NewHoldable", menuName = "Data/HoldableSO")]
	public class HoldableSO : ItemSO
	{
		public int MaxStack = 1;

		public int HoldType;

		public bool IsPlaceable;

		public bool IsThrowable;

		public SurfaceTags PlaceableTags;

		public HoldableMaterialType HoldableMaterialType;

		public FurnitureWeight FurnitureWeight;

		public GameObject PlacementObjectPrefab;

		public GameObject GraphicObjectPrefab;

		public ThrowBouncePlaceSettings ThrowBouncePlace = new ThrowBouncePlaceSettings
		{
			EnableBouncePlace = true,
			BounceHeight = 0.12f,
			BounceUpDuration = 0.08f,
			SettleDuration = 0.12f
		};

		public ItemOffset FPSHoldOffset;

		public ItemOffset TPSHoldOffset;

		public ItemOffset PlaceOffset;

		public TooltipConfig Tooltip;

		public void ApplyTpsHoldOffset(Transform graphic)
		{
			if (TPSHoldOffset.ScaleOffset != Vector3.zero)
			{
				graphic.localScale = TPSHoldOffset.ScaleOffset;
			}
			graphic.SetLocalPositionAndRotation(TPSHoldOffset.PositionOffset, Quaternion.Euler(TPSHoldOffset.RotationOffset));
		}
	}
}
