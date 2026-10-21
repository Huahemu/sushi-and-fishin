using System;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;

namespace PaperPixelGames.DevConsole
{
	public sealed class SharkCommand : IDevCommand
	{
		public string Name => "shark";

		public string Description => "shark on | shark off";

		public void Execute(DevConsoleContext context, string[] args, Action<string> feedback)
		{
			string[] array = DevCommandArgs.Clean(args);
			if (array.Length != 2)
			{
				feedback?.Invoke(Description);
				return;
			}
			bool flag;
			if (string.Equals(array[1], "on", StringComparison.OrdinalIgnoreCase))
			{
				flag = true;
			}
			else
			{
				if (!string.Equals(array[1], "off", StringComparison.OrdinalIgnoreCase))
				{
					feedback?.Invoke(Description);
					return;
				}
				flag = false;
			}
			RpcRelay rpcRelay = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RpcRelay : null);
			if (rpcRelay == null || !rpcRelay.IsSpawned)
			{
				feedback?.Invoke("RPC relay is not available.");
				return;
			}
			rpcRelay.ServerSetDebugSharkRpc(flag);
			feedback?.Invoke(flag ? "Shark spawn requested." : "Shark removal requested.");
		}
	}
}
