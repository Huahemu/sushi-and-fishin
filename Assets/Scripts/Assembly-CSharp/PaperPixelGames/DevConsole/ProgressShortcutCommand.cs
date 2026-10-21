using System;

namespace PaperPixelGames.DevConsole
{
	public sealed class ProgressShortcutCommand : IDevCommand
	{
		private readonly ProgressionCommand _progressionCommand;

		public string Name => "progress";

		public string Description => "progress";

		public ProgressShortcutCommand(ProgressionCommand progressionCommand)
		{
			_progressionCommand = progressionCommand;
		}

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			_progressionCommand.Execute(context, new string[2] { "progression", "status" }, feedback);
		}
	}
}
