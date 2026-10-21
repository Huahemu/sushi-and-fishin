using UnityEngine;

namespace Kamgam.SettingsGenerator
{
	public class PlayingBackground : Connection<bool>
	{
		public override bool Get()
		{
			return Application.runInBackground;
		}

		public override void Set(bool runInBackgroundEnabled)
		{
			Application.runInBackground = runInBackgroundEnabled;
			NotifyListenersIfChanged(runInBackgroundEnabled);
		}
	}
}
