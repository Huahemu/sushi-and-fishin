namespace UnityUtils
{
	public static class MathfExtension
	{
		public static double Min(double a, double b)
		{
			if (!(a < b))
			{
				return b;
			}
			return a;
		}

		public static double Min(params double[] values)
		{
			int num = values.Length;
			if (num == 0)
			{
				return 0.0;
			}
			double num2 = values[0];
			for (int i = 1; i < num; i++)
			{
				if (values[i] < num2)
				{
					num2 = values[i];
				}
			}
			return num2;
		}

		public static double Max(double a, double b)
		{
			if (!(a > b))
			{
				return b;
			}
			return a;
		}

		public static double Max(params double[] values)
		{
			int num = values.Length;
			if (num == 0)
			{
				return 0.0;
			}
			double num2 = values[0];
			for (int i = 1; i < num; i++)
			{
				if (values[i] > num2)
				{
					num2 = values[i];
				}
			}
			return num2;
		}
	}
}
