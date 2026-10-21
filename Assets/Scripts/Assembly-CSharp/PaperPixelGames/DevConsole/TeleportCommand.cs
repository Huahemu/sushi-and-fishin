using System;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;

namespace PaperPixelGames.DevConsole
{
	public class TeleportCommand : IDevCommand
	{
		public string Name => "tp";

		public string Description => "  (tp <x> <y> <z>  tp <x> <y> <z> <updateGround>)\n  tp <special_keyword>\n  tp 0 2 0\n  tp 10 3 -5 true\n  tp boat\n  tp boss\n  tp lighthouse";

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			if (args == null || args.Length < 2)
			{
				feedback(Description);
				return;
			}
			bool result = true;
			Vector3 v;
			if (args.Length == 2)
			{
				if (!SpecialTeleportTargets.TryGetTarget(args[1], out var target))
				{
					feedback("Unknown teleport keyword: " + args[1] + ". Available: " + SpecialTeleportTargets.KeywordList);
				}
				else
				{
					TeleportLocalPlayer(target.Position, result, feedback, args[1], target.LookAt);
				}
			}
			else if (args.Length < 4)
			{
				feedback(Description);
			}
			else if (!TryParseVector3(args, 1, out v))
			{
				feedback("Invalid position values. Example: tp 10 2 -5");
			}
			else if (args.Length >= 5 && !bool.TryParse(args[4], out result))
			{
				feedback("Invalid updateGround. Use: true / false");
			}
			else
			{
				TeleportLocalPlayer(v, result, feedback);
			}
		}

		private void TeleportLocalPlayer(Vector3 position, bool updateGround, Action<string> feedback, string keyword = null, Vector3? lookAt = null)
		{
			PlayersManager instance = Singleton<PlayersManager>.Instance;
			if (instance == null)
			{
				feedback("PlayersManager not found.");
				return;
			}
			PPG_Player localPlayer = instance.LocalPlayer;
			if (localPlayer == null)
			{
				feedback("LocalPlayer not found.");
				return;
			}
			localPlayer.TeleportPlayer(position, updateGround);
			if (lookAt.HasValue && localPlayer.FirstPersonCharacter != null)
			{
				localPlayer.FirstPersonCharacter.LookAtInstant(lookAt.Value);
			}
			if (string.IsNullOrWhiteSpace(keyword))
			{
				feedback($"Teleported to {position} (updateGround: {updateGround})");
			}
			else if (lookAt.HasValue)
			{
				feedback($"Teleported to {keyword}: {position} lookAt: {lookAt.Value} (updateGround: {updateGround})");
			}
			else
			{
				feedback($"Teleported to {keyword}: {position} (updateGround: {updateGround})");
			}
		}

		private bool TryParseVector3(string[] args, int startIndex, out Vector3 v)
		{
			v = default;
			if (!float.TryParse(args[startIndex], out var result))
			{
				return false;
			}
			if (!float.TryParse(args[startIndex + 1], out var result2))
			{
				return false;
			}
			if (!float.TryParse(args[startIndex + 2], out var result3))
			{
				return false;
			}
			v = new Vector3(result, result2, result3);
			return true;
		}
	}
}
