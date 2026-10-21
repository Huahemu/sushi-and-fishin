using System;

namespace PaperPixelGames.DevConsole
{
	public sealed class QuestShortcutCommand : IDevCommand
	{
		private readonly SetCommand _setCommand;

		private readonly string _target;

		public string Name => _target;

		public string Description => _target + " done";

		public QuestShortcutCommand(string target, SetCommand setCommand)
		{
			_target = target;
			_setCommand = setCommand;
		}

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			string[] array = DevCommandArgs.Clean(args);
			if (array.Length < 2 || !IsDone(array[1]))
			{
				feedback?.Invoke(Description);
				return;
			}
			_setCommand.Execute(context, new string[3] { "set", _target, "complete" }, feedback);
		}

		private bool IsDone(string value)
		{
			if (!string.Equals(value, "done", StringComparison.OrdinalIgnoreCase) && !string.Equals(value, "complete", StringComparison.OrdinalIgnoreCase))
			{
				return string.Equals(value, "completed", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}
	}
}
