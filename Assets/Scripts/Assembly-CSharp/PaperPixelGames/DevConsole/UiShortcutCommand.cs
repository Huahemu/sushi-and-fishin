using System;

namespace PaperPixelGames.DevConsole
{
	public sealed class UiShortcutCommand : IDevCommand
	{
		private readonly SetCommand _setCommand;

		public string Name => "ui";

		public string Description => "ui on | ui off";

		public UiShortcutCommand(SetCommand setCommand)
		{
			_setCommand = setCommand;
		}

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			string[] array = DevCommandArgs.Clean(args);
			if (array.Length < 2)
			{
				feedback?.Invoke(Description);
				return;
			}
			_setCommand.Execute(context, new string[3]
			{
				"set",
				"ui",
				array[1]
			}, feedback);
		}
	}
}
