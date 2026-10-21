using PaperPixelGames.Core.Settings;
using Unity.Netcode;

namespace Kamgam.SettingsGenerator
{
	public class PlayerCollisionConnection : Connection<bool>
	{
		public const string SettingId = "playerCollision";

		public override bool Get()
		{
			return RuntimeSettings.PlayerCollisionEnabled;
		}

		public override bool GetDefault()
		{
			return false;
		}

		public override void Set(bool enabled)
		{
			NetworkManager singleton = NetworkManager.Singleton;
			if (singleton != null && singleton.IsClient && !singleton.IsHost)
			{
				PullSettingFromAuthoritativeValue();
				return;
			}
			RuntimeSettings.SetPlayerCollision(enabled);
			if (singleton != null && singleton.IsHost)
			{
				RpcRelay.ActiveInstance?.HostSetPlayerCollision(enabled);
			}
			NotifyListenersIfChanged(enabled);
		}

		public static void PullSettingFromAuthoritativeValue()
		{
			(SettingsGeneratorSettings.GetProvider()?.Settings?.GetBool("playerCollision"))?.PullFromConnection();
		}
	}
}
