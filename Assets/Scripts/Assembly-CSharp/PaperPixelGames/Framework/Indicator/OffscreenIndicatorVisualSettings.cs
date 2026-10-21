using System;
using UnityEngine;

namespace PaperPixelGames.Framework.Indicator
{
	[Serializable]
	public class OffscreenIndicatorVisualSettings
	{
		public Sprite IconSprite;

		public bool ApplyIconSize;

		public Vector2 IconSize;

		public bool ApplyIconColor;

		public Color IconColor;

		public bool ApplyDistanceVisible;

		public bool DistanceVisible;

		public bool ApplyDistanceColor;

		public Color DistanceColor;

		public bool ApplyRootScale;

		public float RootScale;

		public Vector3 WorldOffset;

		public bool ResetRotation;

		public OffscreenIndicatorVisualSettings()
		{
			IconSprite = null;
			ApplyIconSize = true;
			IconSize = new Vector2(50f, 50f);
			ApplyIconColor = false;
			IconColor = Color.white;
			ApplyDistanceVisible = false;
			DistanceVisible = false;
			ApplyDistanceColor = false;
			DistanceColor = Color.white;
			ApplyRootScale = false;
			RootScale = 1f;
			WorldOffset = new Vector3(0f, 0.35f, 0f);
			ResetRotation = false;
		}

		public OffscreenIndicatorVisualSettings Clone()
		{
			return new OffscreenIndicatorVisualSettings
			{
				IconSprite = IconSprite,
				ApplyIconSize = ApplyIconSize,
				IconSize = IconSize,
				ApplyIconColor = ApplyIconColor,
				IconColor = IconColor,
				ApplyDistanceVisible = ApplyDistanceVisible,
				DistanceVisible = DistanceVisible,
				ApplyDistanceColor = ApplyDistanceColor,
				DistanceColor = DistanceColor,
				ApplyRootScale = ApplyRootScale,
				RootScale = RootScale,
				WorldOffset = WorldOffset,
				ResetRotation = ResetRotation
			};
		}
	}
}
