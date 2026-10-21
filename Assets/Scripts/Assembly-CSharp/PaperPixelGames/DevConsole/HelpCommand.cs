using System;

namespace PaperPixelGames.DevConsole
{
	public sealed class HelpCommand : IDevCommand
	{
		public string Name => "help";

		public string Description => "help";

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			feedback?.Invoke("Dev Commands\n  time 13 | time 13 30 | time freeze | time start\n  money +500 | money -200\n  scrap +50 | scrap -10\n  oxygen +25 | oxygen -10 | oxygen loss on | oxygen loss off\n  tp boat | tp rest | tp gabe | tp marina | tp boss | tp c1 | tp c2 | tp c3 | tp 0 2 0\n  spawn w 0 | spawn fish 3 | spawn deadfish 2\n  tool status | tool off | tool harpoon on | tool harpoon off | tool harpoon 2\n  fishbook test\n  trophy 0\n  chef fill\n  ui on | ui off\n  quest done | group done | tuto status\n  qtest list | qtest goto 38 2 | qtest status | qtest lighthouse | qtest boss kill\n  xp 100 | progress");
		}
	}
}
