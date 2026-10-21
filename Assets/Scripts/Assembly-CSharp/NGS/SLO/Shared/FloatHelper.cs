using UnityEngine;

namespace NGS.SLO.Shared
{
	public static class FloatHelper
	{
		public static float SnapToInt(this float value, float epsilon = 0.01f)
		{
			float num = Mathf.Round(value);
			if (Mathf.Abs(value - num) <= epsilon)
			{
				return num;
			}
			return value;
		}

		public static float FloorToPowerOfTwo(this float value)
		{
			if (value < 1f)
			{
				return 1f;
			}
			int num = (int)value;
			int num2;
			for (num2 = 1; num2 <= num; num2 <<= 1)
			{
			}
			return num2 >> 1;
		}
	}
}
