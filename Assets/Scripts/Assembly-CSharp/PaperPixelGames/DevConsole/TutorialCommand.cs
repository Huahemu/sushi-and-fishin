using System;
using System.Linq;
using PaperPixelGames.Core.Quests;
using UnityEngine;

namespace PaperPixelGames.DevConsole
{
	public class TutorialCommand : IDevCommand
	{
		private readonly TimeCommand _timeCommand = new TimeCommand();

		public string Name => "tuto";

		public string Description => "\n\nTutorial / Quest Debug Commands\n  tuto quest complete\n  tuto group complete\n  tuto station reset\n  tuto status\n  time <hour> [minute] [checkFishSpawn]\n  time 0 | time 1\n\nExamples:\n  tuto quest complete\n  tuto group complete\n  tuto station reset\n  tuto status\n  time 10\n  time 10 30\n  time 10 30 false\n  time 0";

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			args = args?.Where((string x) => !string.IsNullOrWhiteSpace(x)).ToArray();
			if (args == null || args.Length == 0)
			{
				feedback?.Invoke(Description);
				return;
			}
			if (string.Equals(args[0], Name, StringComparison.OrdinalIgnoreCase))
			{
				args = args.Skip(1).ToArray();
			}
			if (args.Length == 0)
			{
				feedback?.Invoke(Description);
				return;
			}
			string text = args[0].ToLowerInvariant();
			if (text == "time")
			{
				_timeCommand.Execute(context, args, feedback);
				return;
			}
			QuestManager instance = QuestManager.Instance;
			if (instance == null)
			{
				feedback?.Invoke("[tuto] QuestManager.Instance not found.");
				return;
			}
			switch (text)
			{
			case "status":
				feedback?.Invoke(instance.DebugBuildCurrentQuestStatus());
				break;
			case "quest":
				ExecuteQuestCommand(instance, args, feedback);
				break;
			case "group":
				ExecuteGroupCommand(instance, args, feedback);
				break;
			case "station":
				ExecuteStationCommand(args, feedback);
				break;
			default:
				feedback?.Invoke(Description);
				break;
			}
		}

		private static void ExecuteStationCommand(string[] args, Action<string> feedback)
		{
			if (args.Length < 2 || !string.Equals(args[1], "reset", StringComparison.OrdinalIgnoreCase))
			{
				feedback?.Invoke("[tuto] Invalid usage. Example: tuto station reset");
				return;
			}
			PlayerPrefs.DeleteKey("StationTutorial.ChefDesk.Seen");
			PlayerPrefs.DeleteKey("StationTutorial.PrepStation.Seen");
			PlayerPrefs.DeleteKey("StationTutorial.FishFridge.Seen");
			PlayerPrefs.DeleteKey("StationTutorial.BeverageFridge.Seen");
			PlayerPrefs.Save();
			feedback?.Invoke("[tuto] Station tutorial pop-up progress reset. Use each station again to replay its video.");
		}

		private void ExecuteQuestCommand(QuestManager questManager, string[] args, Action<string> feedback)
		{
			if (args.Length < 2 || !string.Equals(args[1], "complete", StringComparison.OrdinalIgnoreCase))
			{
				feedback?.Invoke("[tuto] Invalid usage. Example: tuto quest complete");
				return;
			}
			questManager.DebugTryCompleteCurrentMainQuest(out var message);
			feedback?.Invoke(message);
		}

		private void ExecuteGroupCommand(QuestManager questManager, string[] args, Action<string> feedback)
		{
			if (args.Length < 2 || !string.Equals(args[1], "complete", StringComparison.OrdinalIgnoreCase))
			{
				feedback?.Invoke("[tuto] Invalid usage. Example: tuto group complete");
				return;
			}
			questManager.DebugTryCompleteCurrentMainGroup(out var message);
			feedback?.Invoke(message);
		}
	}
}
