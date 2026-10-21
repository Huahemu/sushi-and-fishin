using System.Collections.Generic;

namespace RTLTMPro
{
	public static class LigatureFixer
	{
		private static readonly List<int> LtrTextHolder = new List<int>(512);

		private static readonly List<int> TagTextHolder = new List<int>(512);

		private static readonly Dictionary<char, char> MirroredCharsMap = new Dictionary<char, char>
		{
			['('] = ')',
			[')'] = '(',
			['»'] = '«',
			['«'] = '»',
			['{'] = '}',
			['}'] = '{',
			['['] = ']',
			[']'] = '['
		};

		private static readonly HashSet<char> MirroredCharsSet = new HashSet<char>(MirroredCharsMap.Keys);

		private static void FlushBufferToOutput(List<int> buffer, FastStringBuilder output)
		{
			for (int i = 0; i < buffer.Count; i++)
			{
				output.Append(buffer[buffer.Count - 1 - i]);
			}
			buffer.Clear();
		}

		public static void Fix(FastStringBuilder input, FastStringBuilder output, bool farsi, bool fixTextTags, bool preserveNumbers)
		{
			LtrTextHolder.Clear();
			TagTextHolder.Clear();
			for (int num = input.Length - 1; num >= 0; num--)
			{
				bool flag = num > 0 && num < input.Length - 1;
				bool flag2 = num == 0;
				bool flag3 = num == input.Length - 1;
				int num2 = input.Get(num);
				int ch = 0;
				if (!flag3)
				{
					ch = input.Get(num + 1);
				}
				int ch2 = 0;
				if (!flag2)
				{
					ch2 = input.Get(num - 1);
				}
				if (fixTextTags && num2 == 62)
				{
					bool flag4 = false;
					int num3 = num;
					TagTextHolder.Add(num2);
					for (int num4 = num - 1; num4 >= 0; num4--)
					{
						int num5 = input.Get(num4);
						TagTextHolder.Add(num5);
						if (num5 == 60)
						{
							if (input.Get(num4 + 1) != 32)
							{
								flag4 = true;
								num3 = num4;
							}
							break;
						}
					}
					if (flag4)
					{
						FlushBufferToOutput(LtrTextHolder, output);
						FlushBufferToOutput(TagTextHolder, output);
						num = num3;
						continue;
					}
					TagTextHolder.Clear();
				}
				if (Char32Utils.IsPunctuation(num2) || Char32Utils.IsSymbol(num2))
				{
					if (MirroredCharsSet.Contains((char)num2))
					{
						bool num6 = Char32Utils.IsRTLCharacter(ch2);
						bool flag5 = Char32Utils.IsRTLCharacter(ch);
						if (num6 | flag5)
						{
							num2 = MirroredCharsMap[(char)num2];
						}
					}
					if (flag)
					{
						bool flag6 = Char32Utils.IsRTLCharacter(ch2);
						bool flag7 = Char32Utils.IsRTLCharacter(ch);
						bool flag8 = Char32Utils.IsWhiteSpace(ch);
						bool flag9 = Char32Utils.IsWhiteSpace(ch2);
						bool flag10 = num2 == 95;
						bool flag11 = num2 == 46 || num2 == 1548 || num2 == 1563;
						if ((flag7 & flag6) || (flag9 & flag11) || (flag8 & flag6) || (flag7 & flag9) || ((flag7 | flag6) & flag10))
						{
							FlushBufferToOutput(LtrTextHolder, output);
							output.Append(num2);
						}
						else
						{
							LtrTextHolder.Add(num2);
						}
					}
					else if (flag3)
					{
						LtrTextHolder.Add(num2);
					}
					else if (flag2)
					{
						output.Append(num2);
					}
					continue;
				}
				if (flag)
				{
					bool flag12 = Char32Utils.IsEnglishLetter(ch2);
					bool flag13 = Char32Utils.IsEnglishLetter(ch);
					bool flag14 = Char32Utils.IsNumber(ch2, preserveNumbers, farsi);
					bool flag15 = Char32Utils.IsNumber(ch, preserveNumbers, farsi);
					bool flag16 = Char32Utils.IsSymbol(ch2);
					bool flag17 = Char32Utils.IsSymbol(ch);
					if (num2 == 32 && (flag13 | flag15 | flag17) && (flag12 | flag14 | flag16))
					{
						LtrTextHolder.Add(num2);
						continue;
					}
				}
				if (Char32Utils.IsEnglishLetter(num2) || Char32Utils.IsNumber(num2, preserveNumbers, farsi))
				{
					LtrTextHolder.Add(num2);
					continue;
				}
				if ((num2 >= 55296 && num2 <= 56319) || (num2 >= 56320 && num2 <= 57343))
				{
					LtrTextHolder.Add(num2);
					continue;
				}
				FlushBufferToOutput(LtrTextHolder, output);
				if (num2 != 65535 && num2 != 8204)
				{
					output.Append(num2);
				}
			}
			FlushBufferToOutput(LtrTextHolder, output);
		}
	}
}
