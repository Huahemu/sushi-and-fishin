using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public static class ResidentDebugLogger
	{
		public static bool Enabled = true;

		public static void Log(string message, LogType type = LogType.Log)
		{
			if (Enabled)
			{
				UtilityMethods.Log("[Resident] " + message, type);
			}
		}
	}
}
