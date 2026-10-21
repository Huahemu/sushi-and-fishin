namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::SushiAndFish.Casino.Steamworks;
using global::UnityEngine;

public class LobbyMembersManager : MonoSingleton<LobbyMembersManager>
{
	private LobbySettings lobbySettings;

	private ulong lastProcessedLobbyID;

	private Callback<LobbyEnter_t> lobbyEnterCallback;

	private Callback<LobbyChatUpdate_t> lobbyChatUpdateCallback;

	private Callback<LobbyDataUpdate_t> lobbyDataUpdateCallback;

	protected override void OnAwake()
	{
		base.OnAwake();
		lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
	}

	private void OnEnable()
	{
		lobbyEnterCallback = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
		lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
		lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
		LobbyManager.OnLobbyEnteredEvent += OnLobbyEnteredEvent;
		if (lobbySettings != null && lobbySettings.steamLobbyID != CSteamID.Nil && SteamManager.Initialized)
		{
			EnsureLocalPlayerVersionSet();
			RefreshPlayersList();
		}
	}

	private void OnDisable()
	{
		lobbyEnterCallback?.Dispose();
		lobbyChatUpdateCallback?.Dispose();
		lobbyDataUpdateCallback?.Dispose();
		LobbyManager.OnLobbyEnteredEvent -= OnLobbyEnteredEvent;
	}

	private void OnLobbyEntered(LobbyEnter_t cb)
	{
		if (lastProcessedLobbyID != cb.m_ulSteamIDLobby)
		{
			lastProcessedLobbyID = cb.m_ulSteamIDLobby;
			if (lobbySettings != null)
			{
				lobbySettings.steamLobbyID = new CSteamID(cb.m_ulSteamIDLobby);
			}
			EnsureLocalPlayerVersionSet();
			StartCoroutine(DelayedRefreshPlayersList());
		}
	}

	private void OnLobbyEnteredEvent()
	{
		EnsureLocalPlayerVersionSet();
		StartCoroutine(DelayedRefreshPlayersList());
	}

	private void OnLobbyChatUpdate(LobbyChatUpdate_t cb)
	{
		if (lobbySettings == null || lobbySettings.steamLobbyID.m_SteamID != cb.m_ulSteamIDLobby)
		{
			return;
		}
		uint rgfChatMemberStateChange = cb.m_rgfChatMemberStateChange;
		bool flag = (rgfChatMemberStateChange & 1) != 0;
		bool flag2 = (rgfChatMemberStateChange & 2) != 0;
		bool flag3 = (rgfChatMemberStateChange & 4) != 0;
		bool flag4 = (rgfChatMemberStateChange & 8) != 0;
		if (flag | flag2 | flag3 | flag4)
		{
			EnsureLocalPlayerVersionSet();
			if (flag)
			{
				StartCoroutine(DelayedRefreshPlayersList());
			}
			else
			{
				RefreshPlayersList();
			}
		}
	}

	private void EnsureLocalPlayerVersionSet()
	{
		if (SteamManager.Initialized && !(lobbySettings == null) && !(lobbySettings.steamLobbyID == CSteamID.Nil))
		{
			CSteamID steamID = SteamUser.GetSteamID();
			CSteamID steamLobbyID = lobbySettings.steamLobbyID;
			if (string.IsNullOrEmpty(SteamMatchmaking.GetLobbyMemberData(steamLobbyID, steamID, "GameVersion")))
			{
				SteamMatchmaking.SetLobbyMemberData(steamLobbyID, "GameVersion", Application.version);
			}
		}
	}

	private void RefreshPlayersList()
	{
		if (lobbySettings == null || lobbySettings.steamLobbyID == CSteamID.Nil || !SteamManager.Initialized)
		{
			return;
		}
		EnsureLocalPlayerVersionSet();
		CSteamID steamLobbyID = lobbySettings.steamLobbyID;
		int numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(steamLobbyID);
		CSteamID lobbyOwner = SteamMatchmaking.GetLobbyOwner(steamLobbyID);
		SteamMatchmaking.GetLobbyMemberData(steamLobbyID, lobbyOwner, "GameVersion");
		List<PlayerInfo> list = new List<PlayerInfo>();
		for (int i = 0; i < numLobbyMembers; i++)
		{
			CSteamID lobbyMemberByIndex = SteamMatchmaking.GetLobbyMemberByIndex(steamLobbyID, i);
			if (lobbyMemberByIndex == CSteamID.Nil)
			{
				continue;
			}
			string lobbyMemberData = SteamMatchmaking.GetLobbyMemberData(steamLobbyID, lobbyMemberByIndex, "PlayerColor");
			string lobbyMemberData2 = SteamMatchmaking.GetLobbyMemberData(steamLobbyID, lobbyMemberByIndex, "GameVersion");
			if (!string.IsNullOrEmpty(lobbyMemberData) && !string.IsNullOrEmpty(lobbyMemberData2))
			{
				string value = SteamFriends.GetFriendPersonaName(lobbyMemberByIndex);
				if (string.IsNullOrEmpty(value))
				{
					value = "Player " + lobbyMemberByIndex.m_SteamID;
				}
				Color color = ColorHexUtility.HexToColor(lobbyMemberData);
				list.Add(new PlayerInfo(value, lobbyMemberByIndex.m_SteamID, color));
			}
		}
		if (MonoSingleton<VersionMismatchManager>.Instance != null)
		{
			MonoSingleton<VersionMismatchManager>.Instance.CheckVersionMismatches();
		}
		lobbySettings.UpdatePlayers(list);
	}

	private IEnumerator DelayedRefreshPlayersList()
	{
		yield return new WaitForSeconds(0.5f);
		EnsureLocalPlayerVersionSet();
		RefreshPlayersList();
	}

	private void OnLobbyDataUpdate(LobbyDataUpdate_t cb)
	{
		if (cb.m_ulSteamIDMember != 0L && !(lobbySettings == null) && !(lobbySettings.steamLobbyID == CSteamID.Nil) && !((CSteamID)cb.m_ulSteamIDLobby != lobbySettings.steamLobbyID))
		{
			CSteamID steamIDUser = new CSteamID(cb.m_ulSteamIDMember);
			CSteamID steamLobbyID = lobbySettings.steamLobbyID;
			string lobbyMemberData = SteamMatchmaking.GetLobbyMemberData(steamLobbyID, steamIDUser, "PlayerColor");
			string lobbyMemberData2 = SteamMatchmaking.GetLobbyMemberData(steamLobbyID, steamIDUser, "GameVersion");
			if (!string.IsNullOrEmpty(lobbyMemberData) && !string.IsNullOrEmpty(lobbyMemberData2))
			{
				RefreshPlayersList();
			}
		}
	}
}

}
