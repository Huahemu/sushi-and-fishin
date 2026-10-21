using PlayFab;
using UnityEngine;

namespace PaperPixelGames.Core.Reporting
{
	public class PlayFabBootstrap : MonoBehaviour
	{
		[SerializeField]
		private PlayFabAuthService _authService;

		public bool Initialize()
		{
			string text = PlayFabSettings.staticSettings?.TitleId;
			if (string.IsNullOrWhiteSpace(text))
			{
				Debug.LogError("[PlayFabBootstrap] PlayFab TitleId is missing in PlayFabSharedSettings.");
				return false;
			}
			if (_authService == null)
			{
				Debug.LogError("[PlayFabBootstrap] AuthService reference is missing.");
				return false;
			}
			PlayFabSettings.DisableDeviceInfo = true;
			PlayFabSettings.DisableFocusTimeCollection = true;
			_authService.Initialize();
			Debug.Log("[PlayFabBootstrap] PlayFab report services initialized. TitleId: " + text + "\n   AutomaticDeviceInfo: Disabled\n   AutomaticFocusTime: Disabled");
			return true;
		}
	}
}
