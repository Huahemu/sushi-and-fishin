using System;
using UnityEngine;

namespace PaperPixelGames.Core.Tablet
{
	[Serializable]
	public struct MapZoomSettings
	{
		[Min(0.01f)]
		public float MinScale;

		[Min(0.01f)]
		public float InitialScale;

		[Min(0.01f)]
		public float MaxScale;

		[Min(0.001f)]
		public float ZoomSpeed;

		public static MapZoomSettings Default => new MapZoomSettings(0.15f, 0.3f, 0.5f, 0.01f);

		public MapZoomSettings(float minScale, float initialScale, float maxScale, float zoomSpeed)
		{
			MinScale = minScale;
			InitialScale = initialScale;
			MaxScale = maxScale;
			ZoomSpeed = zoomSpeed;
		}
	}
}
