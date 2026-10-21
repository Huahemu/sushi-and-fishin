namespace RTLTMPro
{
	public static class RTLSupport
	{
		public const int DefaultBufferSize = 2048;

		private static FastStringBuilder inputBuilder;

		private static FastStringBuilder glyphFixerOutput;

		static RTLSupport()
		{
			inputBuilder = new FastStringBuilder(2048);
			glyphFixerOutput = new FastStringBuilder(2048);
		}

		public static void FixRTL(string input, FastStringBuilder output, bool farsi = true, bool fixTextTags = true, bool preserveNumbers = false)
		{
			inputBuilder.SetValue(input);
			TashkeelFixer.RemoveTashkeel(inputBuilder);
			GlyphFixer.Fix(inputBuilder, glyphFixerOutput, preserveNumbers, farsi, fixTextTags);
			TashkeelFixer.RestoreTashkeel(glyphFixerOutput);
			TashkeelFixer.FixShaddaCombinations(glyphFixerOutput);
			LigatureFixer.Fix(glyphFixerOutput, output, farsi, fixTextTags, preserveNumbers);
			if (fixTextTags)
			{
				RichTextFixer.Fix(output);
			}
			inputBuilder.Clear();
		}
	}
}
