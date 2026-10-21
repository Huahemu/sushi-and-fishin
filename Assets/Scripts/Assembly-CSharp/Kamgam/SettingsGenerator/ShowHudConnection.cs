using PaperPixelGames.Core.Settings;

namespace Kamgam.SettingsGenerator
{
	public class ShowHudConnection : Connection<bool>
	{
		public override bool Get()
		{
			return RuntimeSettings.ShowHudEnabled;
		}

		public override bool GetDefault()
		{
			return true;
		}

		public override void Set(bool status)
		{
			if (RuntimeSettings.ShowHudEnabled != status)
			{
				RuntimeSettings.SetShowHud(status);
				NotifyListenersIfChanged(status);
			}
		}
	}
}
