using PaperPixelGames.Core.Localization;
using TMPro;

namespace PaperPixelGames.Framework.States.MainMenu
{
	public sealed class LobbyLocalizedText : TextMeshProUGUI, ITextPreprocessor
	{
		private ArabicTextPreprocessor _processor;

		protected override void Awake()
		{
			base.Awake();
			base.textPreprocessor = this;
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			base.textPreprocessor = this;
		}

		public string PreprocessText(string source)
		{
			if (_processor == null)
			{
				_processor = new ArabicTextPreprocessor(this);
			}
			return _processor.PreprocessText(source);
		}
	}
}
