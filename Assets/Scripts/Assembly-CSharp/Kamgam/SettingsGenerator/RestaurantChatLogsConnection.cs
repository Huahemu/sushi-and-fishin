using PaperPixelGames.Core.Settings;

namespace Kamgam.SettingsGenerator
{
	public sealed class RestaurantChatLogsConnection : Connection<bool>
	{
		public override bool Get()
		{
			return RuntimeSettings.RestaurantChatLogsEnabled;
		}

		public override bool GetDefault()
		{
			return true;
		}

		public override void Set(bool status)
		{
			if (RuntimeSettings.RestaurantChatLogsEnabled != status)
			{
				RuntimeSettings.SetRestaurantChatLogs(status);
				NotifyListenersIfChanged(status);
			}
		}
	}
}
