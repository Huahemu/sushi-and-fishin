using UnityEngine;

namespace UnityUtils
{
	public static class Vector2Extensions
	{
		public static Vector2 Add(this Vector2 vector2, float x = 0f, float y = 0f)
		{
			return new Vector2(vector2.x + x, vector2.y + y);
		}

		public static Vector2 With(this Vector2 vector2, float? x = null, float? y = null)
		{
			return new Vector2(x ?? vector2.x, y ?? vector2.y);
		}

		public static bool InRangeOf(this Vector2 current, Vector2 target, float range)
		{
			return (current - target).sqrMagnitude <= range * range;
		}
	}
}
