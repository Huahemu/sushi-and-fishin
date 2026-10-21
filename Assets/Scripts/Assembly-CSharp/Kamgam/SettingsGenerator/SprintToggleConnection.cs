using PaperPixelGames.Core.Settings;

namespace Kamgam.SettingsGenerator
{
	public class SprintToggleConnection : Connection<bool>
	{
		public override bool Get()
		{
			return RuntimeSettings.ToggleSprintEnabled;
		}

		public override bool GetDefault()
		{
			return true;
		}

		public override void Set(bool enabled)
		{
			if (RuntimeSettings.ToggleSprintEnabled != enabled)
			{
				RuntimeSettings.SetToggleSprint(enabled);
				NotifyListenersIfChanged(enabled);
			}
		}
	}
}
