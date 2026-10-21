using System;
using System.Globalization;

namespace PaperPixelGames.Framework.Utility
{
	public static class MoneyHelper
	{
		public const int InfiniteCents = int.MaxValue;

		public static bool IsInfinite(int moneyCents)
		{
			return moneyCents == int.MaxValue;
		}

		public static string ConvertMoneyText(this int moneyCents, bool includeDollarSign = true, bool includeSign = false)
		{
			if (IsInfinite(moneyCents))
			{
				if (!includeDollarSign)
				{
					return "∞";
				}
				return "$∞";
			}
			if (moneyCents == 0)
			{
				if (!includeDollarSign)
				{
					return "0";
				}
				return "$0";
			}
			bool flag = moneyCents < 0;
			long num = Math.Abs((long)moneyCents);
			string text = ((num >= 100000000000L) ? FormatLargeNumberCents(num, 100000000000.0, "B") : ((num >= 100000000) ? FormatLargeNumberCents(num, 100000000.0, "M") : ((num < 100000) ? FormatSmallNumberCents(num) : FormatLargeNumberCents(num, 100000.0, "K"))));
			if (includeSign)
			{
				text = (flag ? "-" : "+") + text;
			}
			else if (flag)
			{
				text = "-" + text;
			}
			if (includeDollarSign)
			{
				text = (text.StartsWith("-", StringComparison.Ordinal) ? ("-$" + text.Substring(1)) : ((!text.StartsWith("+", StringComparison.Ordinal)) ? ("$" + text) : ("+$" + text.Substring(1))));
			}
			return text;
		}

		private static string FormatLargeNumberCents(double absCents, double unitCents, string suffix)
		{
			double num = absCents / unitCents;
			return ((num >= 10.0) ? num.ToString("0", CultureInfo.InvariantCulture) : num.ToString("0.#", CultureInfo.InvariantCulture)) + suffix;
		}

		private static string FormatSmallNumberCents(long absCents)
		{
			long num = absCents / 100;
			long num2 = absCents % 100;
			if (num2 == 0L)
			{
				return num.ToString("N0", CultureInfo.InvariantCulture);
			}
			return string.Format("{0}.{1:00}", num.ToString("N0", CultureInfo.InvariantCulture), num2);
		}
	}
}
