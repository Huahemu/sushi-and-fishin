using System;
using System.Collections.Generic;
using System.Globalization;
using PaperPixelGames.Core.Localization;

namespace PaperPixelGames.Core.Staff
{
	public static class StaffNames
	{
		public const int NamesPerGender = 24;

		private static readonly string[] MaleCanonical = new string[24]
		{
			"Akira", "Daichi", "Haru", "Haruto", "Hayato", "Hiro", "Itsuki", "Jun", "Kaito", "Kenji",
			"Kenta", "Makoto", "Naoki", "Ren", "Riku", "Shin", "Sora", "Taichi", "Takumi", "Taro",
			"Yuki", "Yuto", "Minato", "Ryota"
		};

		private static readonly string[] FemaleCanonical = new string[24]
		{
			"Aiko", "Akari", "Aya", "Emi", "Hana", "Haruka", "Hikari", "Kaori", "Mai", "Mei",
			"Mika", "Mina", "Misaki", "Nami", "Naomi", "Rei", "Rin", "Sakura", "Yui", "Yuna",
			"Rika", "Mio", "Noa", "Saki"
		};

		private static readonly string[] MaleEnglish = new string[24]
		{
			"James", "Oliver", "Noah", "William", "Henry", "Jack", "Thomas", "George", "Charlie", "Jacob",
			"Daniel", "Samuel", "Joseph", "Arthur", "Edward", "Oscar", "Harry", "Leo", "Alexander", "Benjamin",
			"Lucas", "Ethan", "Matthew", "Owen"
		};

		private static readonly string[] FemaleEnglish = new string[24]
		{
			"Olivia", "Amelia", "Charlotte", "Isla", "Emily", "Sophie", "Grace", "Lily", "Alice", "Lucy",
			"Ella", "Isabella", "Mia", "Freya", "Florence", "Evelyn", "Chloe", "Hannah", "Emma", "Ruby",
			"Rose", "Phoebe", "Daisy", "Scarlett"
		};

		private static readonly IReadOnlyList<string> ReadOnlyMale = Array.AsReadOnly(MaleCanonical);

		private static readonly IReadOnlyList<string> ReadOnlyFemale = Array.AsReadOnly(FemaleCanonical);

		private static readonly Dictionary<string, (int index, StaffGender gender)> Slots = BuildSlots();

		public static IReadOnlyList<string> CanonicalNames(StaffGender gender)
		{
			if (gender != StaffGender.Female)
			{
				return ReadOnlyMale;
			}
			return ReadOnlyFemale;
		}

		public static string Key(StaffGender gender, int index)
		{
			if (index < 0 || index >= 24)
			{
				throw new ArgumentOutOfRangeException("index");
			}
			return "tablet.staff.name." + ((gender == StaffGender.Female) ? "female." : "male.") + index.ToString("D2", CultureInfo.InvariantCulture);
		}

		public static bool TryGetNameKey(string canonicalName, StaffGender gender, out string key)
		{
			if (canonicalName != null && Slots.TryGetValue(canonicalName, out (int, StaffGender) value))
			{
				key = Key(gender, value.Item1);
				return true;
			}
			key = null;
			return false;
		}

		public static string DisplayName(StaffEmployeeEntry employee)
		{
			return DisplayName(employee.Name.ToString(), employee.Gender);
		}

		public static string LocalizedArgument(StaffEmployeeEntry employee)
		{
			return LocalizedArgument(employee.Name.ToString(), employee.Gender);
		}

		public static string LocalizedArgument(string canonicalName, StaffGender gender)
		{
			if (canonicalName == null)
			{
				return string.Empty;
			}
			if (!Slots.TryGetValue(canonicalName, out (int, StaffGender) value))
			{
				return canonicalName;
			}
			string text = ((gender == StaffGender.Female) ? FemaleEnglish : MaleEnglish)[value.Item1];
			return "@" + Key(gender, value.Item1) + "|" + text;
		}

		public static string DisplayName(StaffReportEntry employee)
		{
			if (employee == null)
			{
				return string.Empty;
			}
			StaffGender gender = (employee.HasGender ? employee.Gender : ((employee.Name != null && Slots.TryGetValue(employee.Name, out (int, StaffGender) value)) ? value.Item2 : StaffGender.Male));
			return DisplayName(employee.Name, gender);
		}

		public static string DisplayName(string canonicalName, StaffGender gender)
		{
			if (canonicalName == null)
			{
				return string.Empty;
			}
			if (!Slots.TryGetValue(canonicalName, out (int, StaffGender) value))
			{
				return canonicalName;
			}
			string fallback = ((gender == StaffGender.Female) ? FemaleEnglish : MaleEnglish)[value.Item1];
			return LocalizationDatabase.GetStringOrFallback("UiTable", Key(gender, value.Item1), fallback);
		}

		private static Dictionary<string, (int index, StaffGender gender)> BuildSlots()
		{
			Dictionary<string, (int, StaffGender)> dictionary = new Dictionary<string, (int, StaffGender)>(48, StringComparer.Ordinal);
			for (int i = 0; i < 24; i++)
			{
				dictionary.Add(MaleCanonical[i], (i, StaffGender.Male));
				dictionary.Add(FemaleCanonical[i], (i, StaffGender.Female));
			}
			return dictionary;
		}
	}
}
