namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::SRDebugger.Services;
using global::SushiAndFish.Casino.Steamworks;
using global::UnityEngine;

[DisallowMultipleComponent]
public class DevSteamAccessGate : MonoBehaviour
{
	[Serializable]
	public struct DevAccount
	{
		public string username;

		public ulong steamId;
	}

	[SerializeField]
	private List<DevAccount> devAccounts = new List<DevAccount>();

	private void Start()
	{
		bool num = IsCurrentUserDeveloper();
		InputEvents.SetDevMode(num);
		ApplySrDebuggerAccess(num);
	}

	private bool IsCurrentUserDeveloper()
	{
		if (!SteamManager.Initialized)
		{
			return false;
		}
		ulong steamID = SteamUser.GetSteamID().m_SteamID;
		for (int i = 0; i < devAccounts.Count; i++)
		{
			if (devAccounts[i].steamId == steamID)
			{
				return true;
			}
		}
		return false;
	}

	private static void ApplySrDebuggerAccess(bool isDev)
	{
		if (!SRDebug.IsInitialized)
		{
			SRDebug.Init();
		}
		IDebugService instance = SRDebug.Instance;
		instance.IsTriggerEnabled = isDev;
		instance.IsTriggerErrorNotificationEnabled = isDev;
		if (!isDev)
		{
			instance.DestroyDebugPanel();
		}
	}
}

}
