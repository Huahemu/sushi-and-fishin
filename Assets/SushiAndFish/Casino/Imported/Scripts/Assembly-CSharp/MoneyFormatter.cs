namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Globalization;

public static class MoneyFormatter
{
	private static readonly NumberFormatInfo DotSeparatorFormat = new NumberFormatInfo
	{
		NumberGroupSeparator = ".",
		NumberDecimalSeparator = "."
	};

	public static string Format(long amount)
	{
        return amount.ToString("N0", CultureInfo.InvariantCulture);
	}

	public static string FormatWithDollar(long amount)
	{
        return (amount < 0 ? "-$" : "$") + Math.Abs((decimal)amount).ToString("N0", CultureInfo.InvariantCulture);
	}

	public static string GetSuffix(long amount)
	{
        return "";
	}

	public static float FormatFloat(long amount)
	{
        return (float)amount;
	}
}

}
