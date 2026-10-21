using PaperPixelGames.Core.Settings;

namespace Kamgam.SettingsGenerator
{
	public class InvertYaxisConnection : Connection<bool>
	{
		public override bool Get()
		{
			return RuntimeSettings.InvertYAxisEnabled;
		}

		public override bool GetDefault()
		{
			return false;
		}

		public override void Set(bool invertEnable)
		{
			if (RuntimeSettings.InvertYAxisEnabled != invertEnable)
			{
				RuntimeSettings.SetInvertYAxis(invertEnable);
				NotifyListenersIfChanged(invertEnable);
			}
		}
	}
}
