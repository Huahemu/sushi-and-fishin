namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Text;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::SushiAndFish.Casino.Steamworks;
using global::UnityEngine;
using global::UnityEngine.SceneManagement;

public class LobbyManager : MonoSingleton<LobbyManager>
{
	public const string JoinVisibilityLobbyKey = "JoinVisibility";

	[Header("Settings")]
	[SerializeField]
	private DropdownSettingItem lobbyModeSetting;

	private LobbySettings lobbySettings;

	private GameSettings gameSettings;

	private bool isStartingClient;

	private Callback<LobbyEnter_t> lobbyEnterCallback;

	private Callback<LobbyDataUpdate_t> lobbyDataUpdateCallback;

	private Callback<GameLobbyJoinRequested_t> joinRequestCallback;

	private Callback<LobbyChatUpdate_t> lobbyChatUpdateCallback;

	private Callback<LobbyKicked_t> lobbyKickedCallback;

	private Callback<GameRichPresenceJoinRequested_t> richPresenceJoinRequestedCallback;

	public static event Action<bool> OnLobbyOwnerStatusChanged;

	public static event Action OnLobbyEnteredEvent;

	public static event Action OnLobbyLeftEvent;

	protected override void OnAwake()
	{
		base.OnAwake();
		lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
		gameSettings = SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
		if (!SteamManager.Initialized)
		{
			Debug.LogError("Steam is not initialized!");
			base.enabled = false;
			return;
		}
		lobbyEnterCallback = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
		lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdated);
		joinRequestCallback = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequest);
		lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
		lobbyKickedCallback = Callback<LobbyKicked_t>.Create(OnLobbyKicked);
		richPresenceJoinRequestedCallback = Callback<GameRichPresenceJoinRequested_t>.Create(OnRichPresenceJoinRequested);
	}

	private void OnEnable()
	{
		SettingItemBase.SettingsChanged += OnSettingChanged;
		ApplyLobbyModeFromSetting();
		InputEvents.OnF3Event = (Action)Delegate.Combine(InputEvents.OnF3Event, new Action(DevStart));
	}

	private void OnDisable()
	{
		SettingItemBase.SettingsChanged -= OnSettingChanged;
		InputEvents.OnF3Event = (Action)Delegate.Remove(InputEvents.OnF3Event, new Action(DevStart));
	}

	private void OnDestroy()
	{
		lobbyEnterCallback?.Dispose();
		lobbyDataUpdateCallback?.Dispose();
		joinRequestCallback?.Dispose();
		lobbyChatUpdateCallback?.Dispose();
		lobbyKickedCallback?.Dispose();
		richPresenceJoinRequestedCallback?.Dispose();
	}

	public void StartGame()
	{
		if (gameSettings.gameHasStarted)
		{
			return;
		}
		if (lobbySettings.steamLobbyID == CSteamID.Nil)
		{
			Debug.LogWarning("Not in a lobby.");
			return;
		}
		if (SteamMatchmaking.GetLobbyOwner(lobbySettings.steamLobbyID) != SteamUser.GetSteamID())
		{
			Debug.LogWarning("Only the lobby owner can start the game.");
			return;
		}
		Debug.Log("Starting game as Host…");
		if (MonoSingleton<SteamRichPresenceManager>.Instance != null)
		{
			MonoSingleton<SteamRichPresenceManager>.Instance.SetInGamePresence();
		}
		if (MonoSingleton<DiscordRichPresenceManager>.Instance != null)
		{
			MonoSingleton<DiscordRichPresenceManager>.Instance.SetInGamePresence();
		}
		MonoSingleton<SceneTransitioner>.Instance.SetLoadingScreen(isEnabled: true, 0.5f);
		StartCoroutine(StartHostAfterFade());
		gameSettings.gameHasStarted = true;
		SteamMatchmaking.SetLobbyData(lobbySettings.steamLobbyID, "GameStarted", "1");
	}

	public void JoinLobby()
	{
		if (!SteamManager.Initialized)
		{
			Debug.LogError("Cannot open Steam overlay - Steam is not initialized!");
			return;
		}
		Debug.Log("Opening Steam overlay for friend lobby join...");
		SteamFriends.ActivateGameOverlay("Friends");
	}

	private void OnRichPresenceJoinRequested(GameRichPresenceJoinRequested_t cb)
	{
		if (NetworkSingleton<GameManager>.Instance != null && NetworkSingleton<GameManager>.Instance.state == GameState.Game && SceneManager.GetActiveScene().name == "CasinoScene")
		{
			Debug.Log("Join request rejected: Game is in progress");
			return;
		}
		CleanupCurrentLobby();
		string[] array = cb.m_rgchConnect.Split(' ');
		SteamMatchmaking.JoinLobby(new CSteamID(ulong.Parse((array.Length > 1) ? array[1] : array[0])));
	}

	public void InviteFriend()
	{
		if (!SteamManager.Initialized)
		{
			Debug.LogError("Cannot open Steam overlay - Steam is not initialized!");
			return;
		}
		if (lobbySettings.steamLobbyID == CSteamID.Nil)
		{
			Debug.LogWarning("Not in a lobby - cannot invite friends.");
			return;
		}
		Debug.Log($"Opening Steam overlay for game lobby invite to lobby: {lobbySettings.steamLobbyID}");
		SteamFriends.ActivateGameOverlayInviteDialog(lobbySettings.steamLobbyID);
	}

	private void OnLobbyEntered(LobbyEnter_t cb)
	{
		lobbySettings.steamLobbyID = new CSteamID(cb.m_ulSteamIDLobby);
		lobbySettings.inALobby = true;
		if (MonoSingleton<SteamRichPresenceManager>.Instance != null)
		{
			MonoSingleton<SteamRichPresenceManager>.Instance.SetMainMenuPresence();
		}
		if (MonoSingleton<DiscordRichPresenceManager>.Instance != null)
		{
			MonoSingleton<DiscordRichPresenceManager>.Instance.SetMainMenuPresence();
		}
		string lobbyData = SteamMatchmaking.GetLobbyData(lobbySettings.steamLobbyID, "LobbyCode");
		if (!string.IsNullOrEmpty(lobbyData))
		{
			lobbySettings.lobbyCode = lobbyData;
			lobbySettings.NotifyChanged();
			WebSocketManager webSocketManager = UnityEngine.Object.FindFirstObjectByType<WebSocketManager>();
			if (webSocketManager != null)
			{
				if (webSocketManager.WebSocketFeaturesEnabled)
				{
					webSocketManager.Initialize();
					Debug.Log("WebSocket initialized with new lobby code: " + lobbyData);
				}
			}
			else
			{
				Debug.LogWarning("WebSocketManager not found! Cannot initialize WebSocket connection.");
			}
		}
		UpdatePlayerCount();
		CheckWhetherGameAlreadyStarted();
		SyncSavedPlayerColorToSteamLobby();
		if (SteamMatchmaking.GetLobbyOwner(lobbySettings.steamLobbyID) == SteamUser.GetSteamID())
		{
			SyncJoinVisibilityLobbyData();
		}
		NotifyLobbyOwnerStatus();
		OnLobbyEnteredEvent?.Invoke();
	}

	private void OnLobbyDataUpdated(LobbyDataUpdate_t cb)
	{
		if ((CSteamID)cb.m_ulSteamIDLobby != lobbySettings.steamLobbyID || cb.m_bSuccess == 0)
		{
			return;
		}
		if (cb.m_ulSteamIDMember != cb.m_ulSteamIDLobby)
		{
			CSteamID steamIDUser = new CSteamID(cb.m_ulSteamIDMember);
			string lobbyMemberData = SteamMatchmaking.GetLobbyMemberData(lobbySettings.steamLobbyID, steamIDUser, "Kicked");
			string lobbyMemberData2 = SteamMatchmaking.GetLobbyMemberData(lobbySettings.steamLobbyID, steamIDUser, "KickTarget");
			if (lobbyMemberData == "1" && !string.IsNullOrEmpty(lobbyMemberData2))
			{
				CSteamID cSteamID = new CSteamID(ulong.Parse(lobbyMemberData2));
				if (cSteamID == SteamUser.GetSteamID())
				{
					Debug.Log("You have been kicked from the lobby!");
					ClearRichPresence();
					SteamMatchmaking.LeaveLobby(lobbySettings.steamLobbyID);
					lobbySettings.inALobby = false;
					lobbySettings.steamLobbyID = CSteamID.Nil;
					if (gameSettings != null)
					{
						gameSettings.gameHasStarted = false;
					}
					CreateNewLobby();
					return;
				}
				Debug.Log($"Player {cSteamID} has been kicked from the lobby");
				UpdatePlayerCount();
				if (MonoSingleton<SteamRichPresenceManager>.Instance != null)
				{
					MonoSingleton<SteamRichPresenceManager>.Instance.UpdatePlayerCount();
				}
				if (MonoSingleton<DiscordRichPresenceManager>.Instance != null)
				{
					MonoSingleton<DiscordRichPresenceManager>.Instance.UpdatePlayerCount();
				}
			}
		}
		if (SteamMatchmaking.GetLobbyData(lobbySettings.steamLobbyID, "Disbanded") == "1")
		{
			Debug.Log("Lobby has been disbanded by the host - leaving lobby...");
			ClearRichPresence();
			SteamMatchmaking.LeaveLobby(lobbySettings.steamLobbyID);
			lobbySettings.inALobby = false;
			lobbySettings.steamLobbyID = CSteamID.Nil;
			lobbySettings.lobbyCode = "";
			if (gameSettings != null)
			{
				gameSettings.gameHasStarted = false;
			}
			CreateNewLobby();
			return;
		}
		string lobbyData = SteamMatchmaking.GetLobbyData(lobbySettings.steamLobbyID, "LobbyCode");
		if (!string.IsNullOrEmpty(lobbyData) && lobbyData != lobbySettings.lobbyCode)
		{
			lobbySettings.lobbyCode = lobbyData;
			lobbySettings.NotifyChanged();
			WebSocketManager webSocketManager = UnityEngine.Object.FindFirstObjectByType<WebSocketManager>();
			if (webSocketManager != null && webSocketManager.WebSocketFeaturesEnabled)
			{
				webSocketManager.Initialize();
			}
		}
		if (cb.m_ulSteamIDMember == cb.m_ulSteamIDLobby && !string.IsNullOrEmpty(lobbySettings.lobbyCode))
		{
			UpdateLobbyNameDisplay();
		}
		CheckWhetherGameAlreadyStarted();
		if (cb.m_ulSteamIDMember == cb.m_ulSteamIDLobby && MonoSingleton<DiscordRichPresenceManager>.Instance != null)
		{
			MonoSingleton<DiscordRichPresenceManager>.Instance.RefreshPresence();
		}
		NotifyLobbyOwnerStatus();
	}

	private void OnLobbyChatUpdate(LobbyChatUpdate_t cb)
	{
		if (!((CSteamID)cb.m_ulSteamIDLobby != lobbySettings.steamLobbyID))
		{
			UpdatePlayerCount();
			if (MonoSingleton<SteamRichPresenceManager>.Instance != null)
			{
				MonoSingleton<SteamRichPresenceManager>.Instance.UpdatePlayerCount();
			}
			if (MonoSingleton<DiscordRichPresenceManager>.Instance != null)
			{
				MonoSingleton<DiscordRichPresenceManager>.Instance.UpdatePlayerCount();
			}
			NotifyLobbyOwnerStatus();
		}
	}

	private void UpdatePlayerCount()
	{
		if (!(lobbySettings.steamLobbyID == CSteamID.Nil))
		{
			int numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(lobbySettings.steamLobbyID);
			lobbySettings.currentPlayerCount = numLobbyMembers;
		}
	}

	private void SyncSavedPlayerColorToSteamLobby()
	{
		if (!SteamManager.Initialized || lobbySettings == null || lobbySettings.steamLobbyID == CSteamID.Nil)
		{
			return;
		}
		if (MonoSingleton<CosmeticsUnlockManager>.Instance == null)
		{
			StartCoroutine(RetrySyncPlayerColor());
			return;
		}
		Color? playerColor = MonoSingleton<CosmeticsUnlockManager>.Instance.GetPlayerColor();
		if (playerColor.HasValue)
		{
			string pchValue = ColorHexUtility.ColorToHex(playerColor.Value);
			SteamMatchmaking.SetLobbyMemberData(lobbySettings.steamLobbyID, "PlayerColor", pchValue);
		}
		SteamMatchmaking.SetLobbyMemberData(lobbySettings.steamLobbyID, "GameVersion", Application.version);
	}

	private IEnumerator RetrySyncPlayerColor()
	{
		yield return new WaitForSeconds(0.5f);
		if (MonoSingleton<CosmeticsUnlockManager>.Instance != null)
		{
			SyncSavedPlayerColorToSteamLobby();
		}
	}

	private void CheckWhetherGameAlreadyStarted()
	{
		if (SceneManager.GetActiveScene().name != "MainMenuScene" || SteamMatchmaking.GetLobbyData(lobbySettings.steamLobbyID, "GameStarted") != "1")
		{
			return;
		}
		string lobbyData = SteamMatchmaking.GetLobbyData(lobbySettings.steamLobbyID, "HostAddress");
		if (!(SteamUser.GetSteamID().ToString() == lobbyData) && !NetworkClient.active && !isStartingClient)
		{
			Debug.Log("Game has started → connecting to host " + lobbyData);
			if (MonoSingleton<SteamRichPresenceManager>.Instance != null)
			{
				MonoSingleton<SteamRichPresenceManager>.Instance.SetInGamePresence();
			}
			MonoSingleton<SceneTransitioner>.Instance.SetLoadingScreen(isEnabled: true, 0.5f);
			isStartingClient = true;
			StartCoroutine(StartClientAfterFade(lobbyData));
			gameSettings.gameHasStarted = true;
		}
	}

	private void OnJoinRequest(GameLobbyJoinRequested_t req)
	{
		CleanupCurrentLobby();
		Debug.Log("Friend invite received – joining lobby…");
		SteamMatchmaking.JoinLobby(req.m_steamIDLobby);
	}

	private void OnLobbyKicked(LobbyKicked_t cb)
	{
		Debug.Log("Player kicked from lobby.");
		UpdatePlayerCount();
		if (gameSettings != null)
		{
			gameSettings.gameHasStarted = false;
		}
		if (MonoSingleton<SteamRichPresenceManager>.Instance != null)
		{
			MonoSingleton<SteamRichPresenceManager>.Instance.UpdatePlayerCount();
		}
		if (MonoSingleton<DiscordRichPresenceManager>.Instance != null)
		{
			MonoSingleton<DiscordRichPresenceManager>.Instance.UpdatePlayerCount();
		}
	}

	public void CreateNewLobby()
	{
		if (!(lobbySettings == null))
		{
			CleanupCurrentLobby();
			lobbySettings.lobbyCode = GenerateCode(lobbySettings.codeLength);
			SteamAPICall_t hAPICall = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, lobbySettings.maxPlayers);
			CallResult<LobbyCreated_t>.Create(OnNewLobbyCreated).Set(hAPICall);
		}
	}

	private void OnNewLobbyCreated(LobbyCreated_t result, bool failure)
	{
		if (failure || result.m_eResult != EResult.k_EResultOK)
		{
			Debug.LogError($"Failed to create new lobby: {result.m_eResult}");
			SceneManager.LoadSceneAsync("NetworkSetupScene");
		}
		else if (lobbySettings != null)
		{
			lobbySettings.steamLobbyID = new CSteamID(result.m_ulSteamIDLobby);
			lobbySettings.inALobby = true;
			SteamMatchmaking.SetLobbyData(lobbySettings.steamLobbyID, "name", SteamFriends.GetPersonaName() + "'s Lobby");
			SteamMatchmaking.SetLobbyData(lobbySettings.steamLobbyID, "HostAddress", SteamUser.GetSteamID().ToString());
			SteamMatchmaking.SetLobbyData(lobbySettings.steamLobbyID, "SelectedScene", "Gameplay");
			SteamMatchmaking.SetLobbyData(lobbySettings.steamLobbyID, "GameStarted", "0");
			SteamMatchmaking.SetLobbyData(lobbySettings.steamLobbyID, "LobbyCode", lobbySettings.lobbyCode);
			SyncJoinVisibilityLobbyData();
			string version = Application.version;
			SteamMatchmaking.SetLobbyMemberData(lobbySettings.steamLobbyID, "GameVersion", version);
			Debug.Log("Set host game version on lobby creation: " + version);
			Debug.Log($"New lobby created: {lobbySettings.steamLobbyID} with code: {lobbySettings.lobbyCode}");
			SteamMatchmaking.JoinLobby(lobbySettings.steamLobbyID);
		}
	}

	private string GenerateCode(int length)
	{
		StringBuilder stringBuilder = new StringBuilder(length);
		global::System.Random random = new global::System.Random();
		for (int i = 0; i < length; i++)
		{
			stringBuilder.Append("1234567890"[random.Next("1234567890".Length)]);
		}
		return stringBuilder.ToString();
	}

	public CSteamID GetCurrentLobbyID()
	{
		return lobbySettings.steamLobbyID;
	}

	public void ClearRichPresence()
	{
		if (MonoSingleton<SteamRichPresenceManager>.Instance != null)
		{
			MonoSingleton<SteamRichPresenceManager>.Instance.ClearRichPresence();
		}
		if (MonoSingleton<DiscordRichPresenceManager>.Instance != null)
		{
			MonoSingleton<DiscordRichPresenceManager>.Instance.ClearRichPresence();
		}
	}

	public void CleanupCurrentLobby()
	{
		if (SteamManager.Initialized && !(lobbySettings == null) && !(lobbySettings.steamLobbyID == CSteamID.Nil) && SteamMatchmaking.GetLobbyOwner(lobbySettings.steamLobbyID) == SteamUser.GetSteamID())
		{
			Debug.Log($"Cleaning up current lobby {lobbySettings.steamLobbyID} before joining new one");
			SteamMatchmaking.SetLobbyData(lobbySettings.steamLobbyID, "GameStarted", "0");
			SteamMatchmaking.LeaveLobby(lobbySettings.steamLobbyID);
			lobbySettings.steamLobbyID = CSteamID.Nil;
			lobbySettings.inALobby = false;
			lobbySettings.lobbyCode = "";
			if (gameSettings != null)
			{
				gameSettings.gameHasStarted = false;
			}
		}
	}

	private void UpdateLobbyNameDisplay()
	{
		WebSocketManager webSocketManager = UnityEngine.Object.FindFirstObjectByType<WebSocketManager>();
		if (webSocketManager != null && webSocketManager.WebSocketFeaturesEnabled && lobbySettings != null && lobbySettings.steamLobbyID != CSteamID.Nil && !string.IsNullOrEmpty(lobbySettings.lobbyCode))
		{
			webSocketManager.Initialize();
		}
	}

	public void SetLobbyPrivacy(bool isPrivate)
	{
		if (SteamManager.Initialized && !(lobbySettings == null) && !(lobbySettings.steamLobbyID == CSteamID.Nil) && !(SteamMatchmaking.GetLobbyOwner(lobbySettings.steamLobbyID) != SteamUser.GetSteamID()))
		{
			ELobbyType eLobbyType = ((!isPrivate) ? ELobbyType.k_ELobbyTypeFriendsOnly : ELobbyType.k_ELobbyTypePrivate);
			if (SteamMatchmaking.SetLobbyType(lobbySettings.steamLobbyID, eLobbyType))
			{
				SteamMatchmaking.SetLobbyData(lobbySettings.steamLobbyID, "JoinVisibility", isPrivate ? "invite" : "friends");
				Debug.Log("Lobby privacy set to " + (isPrivate ? "Private" : "Public"));
			}
			else
			{
				Debug.LogWarning("Failed to set lobby privacy to " + (isPrivate ? "Private" : "Public"));
			}
		}
	}

	private void OnSettingChanged(SettingItemBase entry)
	{
		if (!(entry == null) && IsLobbyModeEntry(entry))
		{
			ApplyLobbyModeFromSetting();
		}
	}

	private bool IsLobbyModeEntry(SettingItemBase entry)
	{
		if (entry == null || string.IsNullOrWhiteSpace(entry.key))
		{
			return false;
		}
		return string.Equals(entry.key.Trim(), "lobbymode", StringComparison.OrdinalIgnoreCase);
	}

	private void ApplyLobbyModeFromSetting()
	{
		if (!SteamManager.Initialized || lobbySettings == null || lobbySettings.steamLobbyID == CSteamID.Nil || lobbyModeSetting == null || SteamMatchmaking.GetLobbyOwner(lobbySettings.steamLobbyID) != SteamUser.GetSteamID())
		{
			return;
		}
		ELobbyType eLobbyType = ((lobbyModeSetting.index != 1) ? ELobbyType.k_ELobbyTypeFriendsOnly : ELobbyType.k_ELobbyTypePrivate);
		if (SteamMatchmaking.SetLobbyType(lobbySettings.steamLobbyID, eLobbyType))
		{
			SyncJoinVisibilityLobbyData();
			if (MonoSingleton<DiscordRichPresenceManager>.Instance != null)
			{
				MonoSingleton<DiscordRichPresenceManager>.Instance.RefreshPresence();
			}
			Debug.Log($"Lobby visibility set to {eLobbyType}");
		}
		else
		{
			Debug.LogWarning($"Failed to set lobby visibility to {eLobbyType}");
		}
	}

	private void SyncJoinVisibilityLobbyData()
	{
		if (SteamManager.Initialized && !(lobbySettings == null) && !(lobbySettings.steamLobbyID == CSteamID.Nil) && !(SteamMatchmaking.GetLobbyOwner(lobbySettings.steamLobbyID) != SteamUser.GetSteamID()) && !(lobbyModeSetting == null))
		{
			string pchValue = ((lobbyModeSetting.index == 1) ? "invite" : "friends");
			SteamMatchmaking.SetLobbyData(lobbySettings.steamLobbyID, "JoinVisibility", pchValue);
		}
	}

	private IEnumerator StartHostAfterFade()
	{
		yield return new WaitForSeconds(1f);
		NetworkManager.singleton.StartHost();
	}

	private IEnumerator StartClientAfterFade(string hostAddress)
	{
		yield return new WaitForSeconds(1f);
		if (!NetworkClient.active)
		{
			NetworkManager.singleton.networkAddress = hostAddress;
			NetworkManager.singleton.StartClient();
		}
		isStartingClient = false;
	}

	public void LeaveLobby()
	{
		if (SteamManager.Initialized && !(lobbySettings == null) && !(lobbySettings.steamLobbyID == CSteamID.Nil))
		{
			SteamMatchmaking.LeaveLobby(lobbySettings.steamLobbyID);
			lobbySettings.inALobby = false;
			lobbySettings.steamLobbyID = CSteamID.Nil;
			lobbySettings.lobbyCode = "";
			if (gameSettings != null)
			{
				gameSettings.gameHasStarted = false;
			}
			if (MonoSingleton<SteamRichPresenceManager>.Instance != null)
			{
				MonoSingleton<SteamRichPresenceManager>.Instance.SetMainMenuPresence();
			}
			if (MonoSingleton<DiscordRichPresenceManager>.Instance != null)
			{
				MonoSingleton<DiscordRichPresenceManager>.Instance.SetMainMenuPresence();
			}
			OnLobbyLeftEvent?.Invoke();
			NotifyLobbyOwnerStatus();
		}
	}

	public void DisbandLobby()
	{
		if (!SteamManager.Initialized || lobbySettings == null || lobbySettings.steamLobbyID == CSteamID.Nil)
		{
			return;
		}
		if (SteamMatchmaking.GetLobbyOwner(lobbySettings.steamLobbyID) != SteamUser.GetSteamID())
		{
			Debug.LogWarning("Only the lobby owner can disband the lobby.");
			return;
		}
		Debug.Log("Disbanding lobby - kicking all players...");
		int numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(lobbySettings.steamLobbyID);
		CSteamID steamID = SteamUser.GetSteamID();
		for (int i = 0; i < numLobbyMembers; i++)
		{
			CSteamID lobbyMemberByIndex = SteamMatchmaking.GetLobbyMemberByIndex(lobbySettings.steamLobbyID, i);
			if (!(lobbyMemberByIndex == steamID))
			{
				try
				{
					SteamMatchmaking.SetLobbyMemberData(lobbySettings.steamLobbyID, "Kicked", "1");
					SteamMatchmaking.SetLobbyMemberData(lobbySettings.steamLobbyID, "KickTarget", lobbyMemberByIndex.ToString());
					Debug.Log($"Kicked player {lobbyMemberByIndex} during lobby disband");
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"Failed to kick player {lobbyMemberByIndex}: {ex.Message}");
				}
			}
		}
		SteamMatchmaking.SetLobbyData(lobbySettings.steamLobbyID, "GameStarted", "0");
		if (gameSettings != null)
		{
			gameSettings.gameHasStarted = false;
		}
		SteamMatchmaking.SetLobbyData(lobbySettings.steamLobbyID, "Disbanded", "1");
		StartCoroutine(LeaveLobbyAfterDisband());
	}

	private IEnumerator LeaveLobbyAfterDisband()
	{
		yield return new WaitForSeconds(0.5f);
		if (lobbySettings != null && lobbySettings.steamLobbyID != CSteamID.Nil)
		{
			SteamMatchmaking.LeaveLobby(lobbySettings.steamLobbyID);
			lobbySettings.inALobby = false;
			lobbySettings.steamLobbyID = CSteamID.Nil;
			lobbySettings.lobbyCode = "";
			OnLobbyLeftEvent?.Invoke();
			NotifyLobbyOwnerStatus();
			CreateNewLobby();
		}
	}

	private void NotifyLobbyOwnerStatus()
	{
		if (!SteamManager.Initialized || lobbySettings == null || lobbySettings.steamLobbyID == CSteamID.Nil)
		{
			OnLobbyOwnerStatusChanged?.Invoke(obj: false);
			return;
		}
		try
		{
			bool obj = SteamMatchmaking.GetLobbyOwner(lobbySettings.steamLobbyID) == SteamUser.GetSteamID();
			OnLobbyOwnerStatusChanged?.Invoke(obj);
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[LobbyManager] Failed to check lobby owner status: " + ex.Message);
			OnLobbyOwnerStatusChanged?.Invoke(obj: false);
		}
	}

	private void DevStart()
	{
		if (!gameSettings.gameHasStarted)
		{
			gameSettings.gameHasStarted = true;
			NetworkManager.singleton.StartHost();
		}
	}
}

}
