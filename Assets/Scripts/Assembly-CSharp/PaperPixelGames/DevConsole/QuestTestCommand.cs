using System;
using System.Text;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Framework.Enums;
using Unity.Netcode;

namespace PaperPixelGames.DevConsole
{
	public sealed class QuestTestCommand : IDevCommand
	{
		private readonly ToolCommand _toolCommand = new ToolCommand();

		private readonly BossCommand _bossCommand = new BossCommand();

		public string Name => "qtest";

		public string Description => "qtest list | qtest goto <mg> <mq> | qtest status | qtest lighthouse | qtest boss kill\nExamples: qtest goto 38 2, qtest lighthouse, qtest boss kill";

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			string[] array = DevCommandArgs.Clean(args);
			if (array.Length < 2)
			{
				feedback?.Invoke(Description);
				return;
			}
			if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer)
			{
				feedback?.Invoke("[qtest] Only the host or offline instance can use quest test commands.");
				return;
			}
			if (string.Equals(array[1], "boss", StringComparison.OrdinalIgnoreCase))
			{
				if (array.Length != 3 || !string.Equals(array[2], "kill", StringComparison.OrdinalIgnoreCase))
				{
					feedback?.Invoke("[qtest] Invalid usage. Example: qtest boss kill");
					return;
				}
				_bossCommand.Execute(context, new string[2] { "boss", "defeat" }, (string message) =>
				{
					feedback?.Invoke(message);
				});
				return;
			}
			QuestManager instance = QuestManager.Instance;
			if (instance == null)
			{
				feedback?.Invoke("[qtest] QuestManager.Instance not found.");
				return;
			}
			switch (array[1].ToLowerInvariant())
			{
			case "list":
				feedback?.Invoke(instance.DebugBuildMainQuestTestList());
				break;
			case "status":
				feedback?.Invoke(instance.DebugBuildCurrentQuestStatus());
				break;
			case "goto":
				ExecuteGoto(instance, array, feedback);
				break;
			case "lighthouse":
				ExecuteLighthouse(context, instance, feedback);
				break;
			default:
				feedback?.Invoke(Description);
				break;
			}
		}

		private static void ExecuteGoto(QuestManager questManager, string[] tokens, Action<string> feedback)
		{
			if (tokens.Length < 4 || !int.TryParse(tokens[2], out var result) || !int.TryParse(tokens[3], out var result2))
			{
				feedback?.Invoke("[qtest] Invalid usage. Example: qtest goto 38 2");
				return;
			}
			questManager.DebugTryPrepareMainQuestForTesting(result, result2, out var message);
			feedback?.Invoke(message);
		}

		private void ExecuteLighthouse(DevConsoleContext context, QuestManager questManager, Action<string> feedback)
		{
			if (!questManager.DebugTryPrepareMainQuestForTesting(QuestId.DiscoverTheLighthouse, out var message))
			{
				feedback?.Invoke(message);
				return;
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine(message);
			RunToolCommand(context, "compass", "on", stringBuilder);
			RunToolCommand(context, "compass", "4", stringBuilder);
			RunToolCommand(context, "diving", "on", stringBuilder);
			RunToolCommand(context, "diving", "4", stringBuilder);
			stringBuilder.Append("[qtest] Lighthouse setup ready. Equip the Compass manually.");
			feedback?.Invoke(stringBuilder.ToString());
		}

		private void RunToolCommand(DevConsoleContext context, string toolName, string operation, StringBuilder result)
		{
			_toolCommand.Execute(context, new string[3] { "tool", toolName, operation }, (string message) =>
			{
				result.AppendLine("[qtest] " + message);
			});
		}
	}
}
