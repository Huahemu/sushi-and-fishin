using UnityEngine;

namespace UnityUtils
{
	public static class NumberExtensions
	{
		public static float PercentageOf(this int part, int whole)
		{
			if (whole == 0)
			{
				return 0f;
			}
			return (float)part / (float)whole;
		}

		public static int AtLeast(this int value, int min)
		{
			return Mathf.Max(value, min);
		}

		public static int AtMost(this int value, int max)
		{
			return Mathf.Min(value, max);
		}

		public static float AtLeast(this float value, float min)
		{
			return Mathf.Max(value, min);
		}

		public static float AtMost(this float value, float max)
		{
			return Mathf.Min(value, max);
		}

		public static double AtLeast(this double value, double min)
		{
			return MathfExtension.Max(value, min);
		}

		public static double AtMost(this double value, double min)
		{
			return MathfExtension.Min(value, min);
		}
	}
}
