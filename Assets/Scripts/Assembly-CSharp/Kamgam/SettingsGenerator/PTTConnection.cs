using PaperPixelGames.Core.Settings;

namespace Kamgam.SettingsGenerator
{
	public class PTTConnection : Connection<bool>
	{
		public override bool Get()
		{
			return RuntimeSettings.PushToTalkEnabled;
		}

		public override bool GetDefault()
		{
			return true;
		}

		public override void Set(bool status)
		{
			if (RuntimeSettings.PushToTalkEnabled != status)
			{
				RuntimeSettings.SetPushToTalk(status);
				NotifyListenersIfChanged(status);
			}
		}
	}
}
