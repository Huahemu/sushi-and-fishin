using PaperPixelGames.Core.Settings;

namespace Kamgam.SettingsGenerator
{
	public class PlayerNamesConnection : Connection<bool>
	{
		public override bool Get()
		{
			return RuntimeSettings.PlayerNamesEnabled;
		}

		public override bool GetDefault()
		{
			return true;
		}

		public override void Set(bool status)
		{
			if (RuntimeSettings.PlayerNamesEnabled != status)
			{
				RuntimeSettings.SetPlayerNames(status);
				NotifyListenersIfChanged(status);
			}
		}
	}
}
