using RTLTMPro;
using TMPro;

namespace PaperPixelGames.Core.Localization
{
	public sealed class ArabicTextPreprocessor : ITextPreprocessor
	{
		private readonly TMP_Text _label;

		private readonly FastStringBuilder _shaped = new FastStringBuilder(2048);

		private bool _wasRightToLeft;

		private HorizontalAlignmentOptions _alignmentBeforeRightToLeft;

		public ArabicTextPreprocessor(TMP_Text label)
		{
			_label = label;
		}

		public static void Install(TMP_Text label)
		{
			if (label != null && label.textPreprocessor == null)
			{
				label.textPreprocessor = new ArabicTextPreprocessor(label);
			}
		}

		public string PreprocessText(string source)
		{
			bool flag = false;
			string text = source ?? string.Empty;
			for (int i = 0; i < text.Length; i++)
			{
				if (TextUtils.IsRTLCharacter(text[i]))
				{
					flag = true;
					break;
				}
			}
			_label.isRightToLeftText = flag;
			if (flag && !_wasRightToLeft)
			{
				_alignmentBeforeRightToLeft = _label.horizontalAlignment;
			}
			if (flag && (_label.horizontalAlignment == HorizontalAlignmentOptions.Left || _label.horizontalAlignment == HorizontalAlignmentOptions.Right))
			{
				_label.horizontalAlignment = HorizontalAlignmentOptions.Right;
			}
			else if (!flag && _wasRightToLeft)
			{
				_label.horizontalAlignment = _alignmentBeforeRightToLeft;
			}
			_wasRightToLeft = flag;
			if (!flag)
			{
				return source;
			}
			_shaped.Clear();
			RTLSupport.FixRTL(source, _shaped, farsi: false, _label.richText, preserveNumbers: true);
			_shaped.Reverse();
			return _shaped.ToString();
		}
	}
}
