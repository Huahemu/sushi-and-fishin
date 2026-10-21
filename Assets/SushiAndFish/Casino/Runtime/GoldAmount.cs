using System;
using System.Globalization;

namespace PaperPixelGames.Core.Economy
{
    // Exact integer cents, serialized as invariant digits. The existing int
    // remains a compatibility projection for original shop APIs and old saves.
    public static class GoldAmount
    {
        public const string Infinite = "infinite";
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        public static string Encode(decimal storedCents, bool infinite = false)
        {
            if (infinite) return Infinite;
            if (decimal.Truncate(storedCents) != storedCents)
                throw new ArgumentException("Gold storage must use whole cents.");
            return storedCents.ToString("0", Culture);
        }

        public static decimal Decode(string encoded, int legacy, out bool infinite)
        {
            if (string.IsNullOrEmpty(encoded))
            {
                infinite = legacy == int.MaxValue;
                return infinite ? 0 : legacy;
            }
            infinite = encoded == Infinite;
            if (infinite) return 0;
            if (!decimal.TryParse(encoded, NumberStyles.AllowLeadingSign, Culture, out var result))
                throw new FormatException("The saved exact gold amount is invalid: " + encoded);
            return result;
        }

        public static int LegacyProjection(decimal storedCents, bool infinite = false)
        {
            if (infinite) return int.MaxValue;
            return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue - 1m, storedCents));
        }

        public static long CasinoProjection(decimal storedCents, bool infinite = false)
        {
            if (infinite) return long.MaxValue;
            return (long)Math.Max(0, Math.Min(long.MaxValue, decimal.Truncate(storedCents / 100m)));
        }

        public static bool TryChange(decimal currentStoredCents, bool infinite, decimal deltaStoredCents, out decimal next)
        {
            next = currentStoredCents;
            if (infinite) return true;
            if (decimal.Truncate(deltaStoredCents) != deltaStoredCents) return false;
            decimal candidate;
            try { candidate = checked(currentStoredCents + deltaStoredCents); }
            catch (OverflowException) { return false; }
            if (candidate < 0) return false;
            next = candidate;
            return true;
        }

        public static string FullNumber(decimal storedCents, bool infinite = false, bool includeSymbol = false)
        {
            if (infinite) return includeSymbol ? "$∞" : "∞";
            decimal rounded = decimal.Round(storedCents, 0, MidpointRounding.AwayFromZero);
            bool negative = rounded < 0;
            decimal absolute = Math.Abs(rounded);
            decimal whole = decimal.Truncate(absolute / 100m);
            int cents = (int)(absolute % 100m);
            string text = whole.ToString("N0", Culture) + (cents == 0 ? "" : "." + cents.ToString("00", Culture));
            return (negative ? "-" : "") + (includeSymbol ? "$" : "") + text;
        }
    }
}
