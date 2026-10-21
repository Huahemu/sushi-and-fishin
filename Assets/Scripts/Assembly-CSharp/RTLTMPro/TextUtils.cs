using System;

namespace RTLTMPro
{
	public static class TextUtils
	{
		private const char LowerCaseA = 'a';

		private const char UpperCaseA = 'A';

		private const char LowerCaseZ = 'z';

		private const char UpperCaseZ = 'Z';

		private const char HebrewLow = '\u0591';

		private const char HebrewHigh = '״';

		private const char ArabicBaseBlockLow = '\u0600';

		private const char ArabicBaseBlockHigh = 'ۿ';

		private const char ArabicExtendedABlockLow = 'ࢠ';

		private const char ArabicExtendedABlockHigh = '\u08ff';

		private const char ArabicExtendedBBlockLow = 'ࡰ';

		private const char ArabicExtendedBBlockHigh = '\u089f';

		private const char ArabicPresentationFormsABlockLow = 'ﭐ';

		private const char ArabicPresentationFormsABlockHigh = '﷿';

		private const char ArabicPresentationFormsBBlockLow = 'ﹰ';

		private const char ArabicPresentationFormsBBlockHigh = '\ufeff';

		public static bool IsArabicPresentationForm(char ch)
		{
			if (ch < 'ﭐ' || ch > '﷿')
			{
				if (ch >= 'ﹰ')
				{
					return ch <= '\ufeff';
				}
				return false;
			}
			return true;
		}

		public static bool IsPunctuation(char ch)
		{
			throw new NotImplementedException();
		}

		public static bool IsNumber(char ch, bool preserveNumbers, bool farsi)
		{
			if (preserveNumbers)
			{
				return IsEnglishNumber(ch);
			}
			if (farsi)
			{
				return IsFarsiNumber(ch);
			}
			return IsHinduNumber(ch);
		}

		public static bool IsEnglishNumber(char ch)
		{
			if (ch >= '0')
			{
				return ch <= '9';
			}
			return false;
		}

		public static bool IsFarsiNumber(char ch)
		{
			if (ch >= '۰')
			{
				return ch <= '۹';
			}
			return false;
		}

		public static bool IsHinduNumber(char ch)
		{
			if (ch >= '٠')
			{
				return ch <= '٩';
			}
			return false;
		}

		public static bool IsEnglishLetter(char ch)
		{
			if (ch < 'A' || ch > 'Z')
			{
				if (ch >= 'a')
				{
					return ch <= 'z';
				}
				return false;
			}
			return true;
		}

		public static bool IsHebrewCharacter(char ch)
		{
			if (ch >= '\u0591')
			{
				return ch <= '״';
			}
			return false;
		}

		public static bool IsArabicCharacter(char ch)
		{
			if ((ch < '\u0600' || ch > 'ۿ') && (ch < 'ࢠ' || ch > '\u08ff') && (ch < 'ࡰ' || ch > '\u089f') && (ch < 'ﭐ' || ch > '﷿'))
			{
				if (ch >= 'ﹰ')
				{
					return ch <= '\ufeff';
				}
				return false;
			}
			return true;
		}

		public static bool IsRTLCharacter(char ch)
		{
			if (IsHebrewCharacter(ch))
			{
				return true;
			}
			if (IsArabicCharacter(ch))
			{
				return true;
			}
			return false;
		}

		public static bool IsGlyphFixedArabicCharacter(char ch)
		{
			switch (ch)
			{
			case 'ء':
			case 'آ':
			case 'أ':
			case 'ؤ':
			case 'إ':
			case 'ئ':
			case 'ا':
			case 'ب':
			case 'ة':
			case 'ت':
			case 'ث':
			case 'ج':
			case 'ح':
			case 'خ':
			case 'د':
			case 'ذ':
			case 'ر':
			case 'ز':
			case 'س':
			case 'ش':
			case 'ص':
			case 'ض':
			case 'ط':
			case 'ظ':
			case 'ع':
			case 'غ':
			case 'ف':
			case 'ق':
			case 'ك':
			case 'ل':
			case 'م':
			case 'ن':
			case 'ه':
			case 'و':
			case 'ى':
			case 'ي':
			case 'پ':
			case 'چ':
			case 'ژ':
			case 'ک':
			case 'گ':
			case 'ی':
				return true;
			default:
				return false;
			}
		}

		public static bool IsRTLInput(string input)
		{
			bool flag = false;
			foreach (char c in input)
			{
				switch (c)
				{
				case '<':
					flag = true;
					continue;
				case '>':
					flag = false;
					continue;
				}
				if (!flag && char.IsLetter(c))
				{
					return IsRTLCharacter(c);
				}
			}
			return false;
		}
	}
}
