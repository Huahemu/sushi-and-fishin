using System.Text;

namespace PaperPixelGames.Framework.Utility
{
	public class DebugTextBuilder
	{
		private readonly StringBuilder _builder;

		private readonly string _labelColor;

		private readonly int _padding;

		public DebugTextBuilder(string header = null, string labelColor = "#FFD700", int padding = 22)
		{
			_builder = new StringBuilder();
			_labelColor = labelColor;
			_padding = padding;
			if (!string.IsNullOrEmpty(header))
			{
				_builder.AppendLine("<b><color=" + _labelColor + ">[" + header + "]</color></b>");
			}
		}

		public DebugTextBuilder AddLine(string label, string value, string valueColor = "#FFFFFF")
		{
			string text = label.PadRight(_padding);
			_builder.AppendLine("<b><color=" + _labelColor + ">" + text + "</color></b><color=" + valueColor + ">" + value + "</color>");
			return this;
		}

		public override string ToString()
		{
			return _builder.ToString();
		}
	}
}
