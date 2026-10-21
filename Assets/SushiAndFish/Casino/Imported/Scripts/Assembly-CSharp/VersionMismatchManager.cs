namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.Extensions;
using global::SushiAndFish.Casino.Steamworks;
using global::UnityEngine;

public class VersionMismatchManager : MonoSingleton<VersionMismatchManager>
{
	private LobbySettings lobbySettings;

	private bool hasVersionMismatch;

	private Callback<LobbyDataUpdate_t> lobbyDataUpdateCallback;

	public static event Action<bool> OnVersionMismatchChanged;

	protected override void OnAwake()
	{
		base.OnAwake();
		lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
	}

	private void OnEnable()
	{
		LobbyManager.OnLobbyEnteredEvent += OnLobbyEntered;
		LobbyManager.OnLobbyLeftEvent += OnLobbyLeft;
		if (SteamManager.Initialized)
		{
			lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
		}
	}

	private void OnDisable()
	{
		LobbyManager.OnLobbyEnteredEvent -= OnLobbyEntered;
		LobbyManager.OnLobbyLeftEvent -= OnLobbyLeft;
		lobbyDataUpdateCallback?.Dispose();
	}

	private void OnLobbyDataUpdate(LobbyDataUpdate_t cb)
	{
		if (cb.m_ulSteamIDMember == 0L || lobbySettings == null || lobbySettings.steamLobbyID == CSteamID.Nil)
		{
			return;
		}
		CSteamID cSteamID = new CSteamID(cb.m_ulSteamIDMember);
		CSteamID steamLobbyID = lobbySettings.steamLobbyID;
		int numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(steamLobbyID);
		bool flag = false;
		for (int i = 0; i < numLobbyMembers; i++)
		{
			if (SteamMatchmaking.GetLobbyMemberByIndex(steamLobbyID, i) == cSteamID)
			{
				flag = true;
				break;
			}
		}
		if (flag)
		{
			CheckVersionMismatches();
		}
	}

	private void OnLobbyEntered()
	{
		if (hasVersionMismatch)
		{
			hasVersionMismatch = false;
			OnVersionMismatchChanged?.Invoke(obj: false);
		}
	}

	private void OnLobbyLeft()
	{
		if (hasVersionMismatch)
		{
			hasVersionMismatch = false;
			OnVersionMismatchChanged?.Invoke(obj: false);
		}
	}

	public void CheckVersionMismatches()
	{
		if (lobbySettings == null || lobbySettings.steamLobbyID == CSteamID.Nil || !SteamManager.Initialized)
		{
			return;
		}
		CSteamID steamLobbyID = lobbySettings.steamLobbyID;
		int numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(steamLobbyID);
		if (numLobbyMembers == 0)
		{
			return;
		}
		CSteamID lobbyOwner = SteamMatchmaking.GetLobbyOwner(steamLobbyID);
		if (lobbyOwner == CSteamID.Nil)
		{
			return;
		}
		string lobbyMemberData = SteamMatchmaking.GetLobbyMemberData(steamLobbyID, lobbyOwner, "GameVersion");
		if (string.IsNullOrEmpty(lobbyMemberData))
		{
			return;
		}
		bool flag = false;
		LobbyMemberList lobbyMemberList = UnityEngine.Object.FindFirstObjectByType<LobbyMemberList>();
		for (int i = 0; i < numLobbyMembers; i++)
		{
			CSteamID lobbyMemberByIndex = SteamMatchmaking.GetLobbyMemberByIndex(steamLobbyID, i);
			if (!(lobbyMemberByIndex != CSteamID.Nil) || lobbyMemberByIndex == lobbyOwner)
			{
				continue;
			}
			string lobbyMemberData2 = SteamMatchmaking.GetLobbyMemberData(steamLobbyID, lobbyMemberByIndex, "GameVersion");
			if (string.IsNullOrEmpty(lobbyMemberData2) || lobbyMemberData2 != lobbyMemberData)
			{
				Debug.Log(string.Format("[VersionMismatchManager] Version mismatch detected - Host: {0}, Player: {1} (SteamID: {2})", lobbyMemberData, lobbyMemberData2 ?? "empty", lobbyMemberByIndex.m_SteamID));
				flag = true;
				if (lobbyMemberList != null)
				{
					lobbyMemberList.UpdatePlayerVersionMismatch(lobbyMemberByIndex, lobbyMemberData2 ?? "unknown", hasMismatch: true);
				}
			}
			else if (lobbyMemberList != null)
			{
				lobbyMemberList.UpdatePlayerVersionMismatch(lobbyMemberByIndex, lobbyMemberData2, hasMismatch: false);
			}
		}
		if (hasVersionMismatch != flag)
		{
			hasVersionMismatch = flag;
			OnVersionMismatchChanged?.Invoke(hasVersionMismatch);
			Debug.Log($"Version mismatch status changed: {hasVersionMismatch}");
		}
	}

	public bool HasVersionMismatch()
	{
		return hasVersionMismatch;
	}
}

}
