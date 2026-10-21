namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

public static class ColorHexUtility
{
	public static string ColorToHex(Color color)
	{
		int num = Mathf.RoundToInt(color.r * 255f);
		int num2 = Mathf.RoundToInt(color.g * 255f);
		int num3 = Mathf.RoundToInt(color.b * 255f);
		int num4 = Mathf.RoundToInt(color.a * 255f);
		return $"{num:X2}{num2:X2}{num3:X2}{num4:X2}";
	}

	public static Color HexToColor(string hex)
	{
		if (string.IsNullOrEmpty(hex))
		{
			return Color.white;
		}
		hex = hex.TrimStart('#');
		if (hex.Length != 8)
		{
			return Color.white;
		}
		try
		{
			int num = Convert.ToInt32(hex.Substring(0, 2), 16);
			int num2 = Convert.ToInt32(hex.Substring(2, 2), 16);
			int num3 = Convert.ToInt32(hex.Substring(4, 2), 16);
			return new Color(a: (float)Convert.ToInt32(hex.Substring(6, 2), 16) / 255f, r: (float)num / 255f, g: (float)num2 / 255f, b: (float)num3 / 255f);
		}
		catch
		{
			return Color.white;
		}
	}
}

}
