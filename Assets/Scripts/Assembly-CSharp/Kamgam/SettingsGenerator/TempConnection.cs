namespace Kamgam.SettingsGenerator
{
	public class TempConnection : Connection<bool>
	{
		public override bool Get()
		{
			return false;
		}

		public override void Set(bool status)
		{
			NotifyListenersIfChanged(status);
		}
	}
}
