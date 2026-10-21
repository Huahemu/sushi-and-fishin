using PaperPixelGames.Core.Settings;

namespace Kamgam.SettingsGenerator
{
	public class InvertXaxisConnection : Connection<bool>
	{
		public override bool Get()
		{
			return RuntimeSettings.InvertXAxisEnabled;
		}

		public override bool GetDefault()
		{
			return false;
		}

		public override void Set(bool invertEnable)
		{
			if (RuntimeSettings.InvertXAxisEnabled != invertEnable)
			{
				RuntimeSettings.SetInvertXAxis(invertEnable);
				NotifyListenersIfChanged(invertEnable);
			}
		}
	}
}
