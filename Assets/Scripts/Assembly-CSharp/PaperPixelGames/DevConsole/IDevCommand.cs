using System;

namespace PaperPixelGames.DevConsole
{
	public interface IDevCommand
	{
		string Name { get; }

		string Description { get; }

		void Execute(DevConsoleContext context, string[] args, Action<string> feedback);
	}
}
