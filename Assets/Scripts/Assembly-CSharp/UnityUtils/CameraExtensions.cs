using System;
using UnityEngine;

namespace UnityUtils
{
	public static class CameraExtensions
	{
		public static Vector2 GetViewportExtentsWithMargin(this Camera camera, Vector2? viewportMargin = null)
		{
			Vector2 vector = viewportMargin ?? new Vector2(0.2f, 0.2f);
			float f = camera.fieldOfView * 0.5f * (MathF.PI / 180f);
			Vector2 result = default;
			result.y = camera.nearClipPlane * Mathf.Tan(f);
			result.x = result.y * camera.aspect + vector.x;
			result.y += vector.y;
			return result;
		}
	}
}
