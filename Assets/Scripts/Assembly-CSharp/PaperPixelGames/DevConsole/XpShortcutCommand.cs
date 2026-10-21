using System;

namespace PaperPixelGames.DevConsole
{
	public sealed class XpShortcutCommand : IDevCommand
	{
		private readonly ProgressionCommand _progressionCommand;

		public string Name => "xp";

		public string Description => "xp <amount>";

		public XpShortcutCommand(ProgressionCommand progressionCommand)
		{
			_progressionCommand = progressionCommand;
		}

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			string[] array = DevCommandArgs.Clean(args);
			if (array.Length < 2)
			{
				feedback?.Invoke(Description);
				return;
			}
			_progressionCommand.Execute(context, new string[3]
			{
				"progression",
				"xp",
				array[1]
			}, feedback);
		}
	}
}
