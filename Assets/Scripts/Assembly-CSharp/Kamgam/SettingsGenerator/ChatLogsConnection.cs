using PaperPixelGames.Core.Settings;

namespace Kamgam.SettingsGenerator
{
	public sealed class ChatLogsConnection : Connection<bool>
	{
		public override bool Get()
		{
			return RuntimeSettings.ChatLogsEnabled;
		}

		public override bool GetDefault()
		{
			return true;
		}

		public override void Set(bool status)
		{
			if (RuntimeSettings.ChatLogsEnabled != status)
			{
				RuntimeSettings.SetChatLogs(status);
				NotifyListenersIfChanged(status);
			}
		}
	}
}
