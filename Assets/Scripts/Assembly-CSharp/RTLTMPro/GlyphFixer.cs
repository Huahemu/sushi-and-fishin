using System.Collections.Generic;

namespace RTLTMPro
{
	public static class GlyphFixer
	{
		public static Dictionary<char, char> EnglishToFarsiNumberMap = new Dictionary<char, char>
		{
			['0'] = '۰',
			['1'] = '۱',
			['2'] = '۲',
			['3'] = '۳',
			['4'] = '۴',
			['5'] = '۵',
			['6'] = '۶',
			['7'] = '۷',
			['8'] = '۸',
			['9'] = '۹'
		};

		public static Dictionary<char, char> EnglishToHinduNumberMap = new Dictionary<char, char>
		{
			['0'] = '٠',
			['1'] = '١',
			['2'] = '٢',
			['3'] = '٣',
			['4'] = '٤',
			['5'] = '٥',
			['6'] = '٦',
			['7'] = '٧',
			['8'] = '٨',
			['9'] = '٩'
		};

		public static void Fix(FastStringBuilder input, FastStringBuilder output, bool preserveNumbers, bool farsi, bool fixTextTags)
		{
			FixYah(input, farsi);
			output.SetValue(input);
			for (int i = 0; i < input.Length; i++)
			{
				int num = input.Get(i);
				if (num == 1604 && i < input.Length - 1 && HandleSpecialLam(input, output, i))
				{
					if (i > 0)
					{
						char c = (char)output.Get(i);
						if (CanCharacterBeConnectedToNext(input.Get(i - 1)))
						{
							output.Set(i, (ushort)(c + 1));
						}
					}
					i++;
				}
				else if (TextUtils.IsGlyphFixedArabicCharacter((char)num))
				{
					char c2 = GlyphTable.Convert((char)num);
					if (IsMiddleLetter(input, i))
					{
						output.Set(i, (ushort)(c2 + 3));
					}
					else if (IsFinishingLetter(input, i))
					{
						output.Set(i, (ushort)(c2 + 1));
					}
					else if (IsLeadingLetter(input, i))
					{
						output.Set(i, (ushort)(c2 + 2));
					}
					else
					{
						output.Set(i, c2);
					}
				}
			}
			if (!preserveNumbers)
			{
				if (fixTextTags)
				{
					FixNumbersOutsideOfTags(output, farsi);
				}
				else
				{
					FixNumbers(output, farsi);
				}
			}
		}

		public static void FixYah(FastStringBuilder text, bool farsi)
		{
			for (int i = 0; i < text.Length; i++)
			{
				if (farsi && text.Get(i) == 1610)
				{
					text.Set(i, 1740);
				}
				else if (!farsi && text.Get(i) == 1740)
				{
					text.Set(i, 1610);
				}
			}
		}

		private static bool HandleSpecialLam(FastStringBuilder input, FastStringBuilder output, int i)
		{
			bool flag;
			switch (input.Get(i + 1))
			{
			case 1573:
				output.Set(i, 65273);
				flag = true;
				break;
			case 1575:
				output.Set(i, 65275);
				flag = true;
				break;
			case 1571:
				output.Set(i, 65271);
				flag = true;
				break;
			case 1570:
				output.Set(i, 65269);
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				output.Set(i + 1, 65535);
			}
			return flag;
		}

		public static void FixNumbers(FastStringBuilder text, bool farsi)
		{
			text.Replace(48, farsi ? 1776 : 1632);
			text.Replace(49, farsi ? 1777 : 1633);
			text.Replace(50, farsi ? 1778 : 1634);
			text.Replace(51, farsi ? 1779 : 1635);
			text.Replace(52, farsi ? 1780 : 1636);
			text.Replace(53, farsi ? 1781 : 1637);
			text.Replace(54, farsi ? 1782 : 1638);
			text.Replace(55, farsi ? 1783 : 1639);
			text.Replace(56, farsi ? 1784 : 1640);
			text.Replace(57, farsi ? 1785 : 1641);
		}

		public static void FixNumbersOutsideOfTags(FastStringBuilder text, bool farsi)
		{
			HashSet<char> hashSet = new HashSet<char>(EnglishToFarsiNumberMap.Keys);
			for (int i = 0; i < text.Length; i++)
			{
				int num = text.Get(i);
				if (num == 60)
				{
					bool flag = false;
					for (int j = i + 1; j < text.Length; j++)
					{
						int num2 = text.Get(j);
						if (j != i + 1 || num2 != 32)
						{
							switch (num2)
							{
							case 62:
								i = j;
								flag = true;
								break;
							default:
								continue;
							case 60:
								break;
							}
						}
						break;
					}
					if (flag)
					{
						continue;
					}
				}
				if (hashSet.Contains((char)num))
				{
					text.Set(i, farsi ? EnglishToFarsiNumberMap[(char)num] : EnglishToHinduNumberMap[(char)num]);
				}
			}
		}

		private static bool IsLeadingLetter(FastStringBuilder letters, int index)
		{
			if (index == letters.Length - 1)
			{
				return false;
			}
			int character = letters.Get(index);
			int character2 = ((index != 0) ? letters.Get(index - 1) : 0);
			int character3 = letters.Get(index + 1);
			if ((index == 0 || !CanCharacterBeConnectedToNext(character2) || !CanCharacterBeConnectedToPrevious(character)) && CanCharacterBeConnectedToNext(character))
			{
				return CanCharacterBeConnectedToPrevious(character3);
			}
			return false;
		}

		private static bool IsFinishingLetter(FastStringBuilder letters, int index)
		{
			if (index == 0)
			{
				return false;
			}
			int character = letters.Get(index);
			int character2 = letters.Get(index - 1);
			int character3 = ((index < letters.Length - 1) ? letters.Get(index + 1) : 0);
			if (CanCharacterBeConnectedToNext(character2) && CanCharacterBeConnectedToPrevious(character))
			{
				if (index != letters.Length - 1 && CanCharacterBeConnectedToPrevious(character3))
				{
					return !CanCharacterBeConnectedToNext(character);
				}
				return true;
			}
			return false;
		}

		private static bool IsMiddleLetter(FastStringBuilder letters, int index)
		{
			if (index == 0 || index == letters.Length - 1)
			{
				return false;
			}
			int character = letters.Get(index);
			int character2 = letters.Get(index - 1);
			int character3 = letters.Get(index + 1);
			if (CanCharacterBeConnectedToNext(character2) && CanCharacterBeConnectedToPrevious(character) && CanCharacterBeConnectedToNext(character))
			{
				return CanCharacterBeConnectedToPrevious(character3);
			}
			return false;
		}

		private static bool CanCharacterBeConnectedToNext(int character)
		{
			if (character != 1569 && character != 1570 && character != 1571 && character != 1573 && character != 1572 && character != 1575 && character != 1583 && character != 1584 && character != 1585 && character != 1586 && character != 1688 && character != 1608)
			{
				return TextUtils.IsGlyphFixedArabicCharacter((char)character);
			}
			return false;
		}

		private static bool CanCharacterBeConnectedToPrevious(int character)
		{
			if (character != 1569)
			{
				return TextUtils.IsGlyphFixedArabicCharacter((char)character);
			}
			return false;
		}
	}
}
