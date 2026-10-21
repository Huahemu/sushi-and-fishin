namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Reflection;
using global::Discord.Sdk;
using SushiAndFish.Casino.Original.Extensions;
using global::SushiAndFish.Casino.Steamworks;
using global::UnityEngine;
using global::UnityEngine.SceneManagement;

public class DiscordRichPresenceManager : MonoSingleton<DiscordRichPresenceManager>
{
	[Header("Discord Settings")]
	[SerializeField]
	private ulong discordClientId = 1466437432097636467uL;

	private LobbySettings lobbySettings;

	private Client discordClient;

	private bool isDiscordReady;

	private bool userRequestedDisconnect;

	private Activity currentActivity;

	private string codeVerifier;

	private const string PREFS_ACCESS_TOKEN = "Discord_AccessToken";

	private const string PREFS_TOKEN_EXPIRES_AT = "Discord_TokenExpiresAt";

	public bool IsConnected
	{
		get
		{
			if (isDiscordReady)
			{
				return discordClient != null;
			}
			return false;
		}
	}

	public event Action<bool> ConnectionStateChanged;

	protected override void OnAwake()
	{
		base.OnAwake();
		lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	public void ConnectDiscord(bool allowOAuthPrompt = true)
	{
		if (SteamManager.Initialized && !isDiscordReady)
		{
			if (discordClient == null)
			{
				InitializeDiscord(allowOAuthPrompt);
			}
			else if (allowOAuthPrompt && string.IsNullOrEmpty(codeVerifier))
			{
				StartOAuthFlow();
			}
		}
	}

	public void DisconnectDiscord()
	{
		PlayerPrefs.DeleteKey("Discord_AccessToken");
		PlayerPrefs.DeleteKey("Discord_TokenExpiresAt");
		PlayerPrefs.Save();
		if (discordClient != null)
		{
			userRequestedDisconnect = true;
			ShutdownDiscord();
		}
	}

	public static bool HasUserAcceptedDiscordToaster()
	{
		return PlayerPrefs.GetInt("Discord_ToasterAccepted", 0) == 1;
	}

	public static void SetUserAcceptedDiscordToaster()
	{
		PlayerPrefs.SetInt("Discord_ToasterAccepted", 1);
		PlayerPrefs.Save();
	}

	public static void ResetDiscordToasterAccepted()
	{
		PlayerPrefs.DeleteKey("Discord_ToasterAccepted");
		PlayerPrefs.Save();
	}

	[ContextMenu("Reset Discord Toaster Accepted")]
	private void ResetDiscordToasterAcceptedContextMenu()
	{
		ResetDiscordToasterAccepted();
	}

	public void Update()
	{
		if (discordClient != null)
		{
			NativeMethods.Discord_RunCallbacks();
		}
	}

	private void OnDestroy()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
		ShutdownDiscord();
	}

	private void InitializeDiscord(bool allowOAuthPrompt)
	{
		if (discordClientId == 0L)
		{
			Debug.LogWarning("[Discord] Client ID not set! Discord integration disabled.");
			return;
		}
		try
		{
			discordClient = new Client();
			discordClient.SetApplicationId(discordClientId);
			discordClient.AddLogCallback(OnDiscordLog, LoggingSeverity.Error);
			discordClient.SetStatusChangedCallback(OnDiscordStatusChanged);
			discordClient.SetActivityJoinCallback(OnDiscordJoin);
			string text = PlayerPrefs.GetString("Discord_AccessToken", "");
			if (!string.IsNullOrEmpty(text))
			{
				OnReceivedToken(text);
			}
			else if (allowOAuthPrompt)
			{
				StartOAuthFlow();
			}
		}
		catch (Exception ex)
		{
			Debug.LogError("[Discord] Failed to initialize: " + ex.Message);
		}
	}

	private void OnDiscordLog(string message, LoggingSeverity severity)
	{
		if (severity == LoggingSeverity.Error)
		{
			Debug.LogError("[Discord] " + message);
		}
	}

	private void OnDiscordStatusChanged(Client.Status status, Client.Error error, int errorCode)
	{
		if (error != Client.Error.None)
		{
			Debug.LogError($"[Discord] Error: {error}, code: {errorCode}");
		}
		switch (status)
		{
		case Client.Status.Ready:
			isDiscordReady = true;
			ConnectionStateChanged?.Invoke(obj: true);
			UpdatePresenceForCurrentScene();
			break;
		case Client.Status.Disconnected:
		{
			bool num = userRequestedDisconnect;
			userRequestedDisconnect = false;
			isDiscordReady = false;
			ConnectionStateChanged?.Invoke(obj: false);
			if (!num && error != Client.Error.None)
			{
				PlayerPrefs.DeleteKey("Discord_AccessToken");
				PlayerPrefs.DeleteKey("Discord_TokenExpiresAt");
				PlayerPrefs.Save();
				if (string.IsNullOrEmpty(codeVerifier))
				{
					StartOAuthFlow();
				}
			}
			break;
		}
		}
	}

	private void StartOAuthFlow()
	{
		if (discordClient == null)
		{
			return;
		}
		try
		{
			AuthorizationCodeVerifier authorizationCodeVerifier = discordClient.CreateAuthorizationCodeVerifier();
			codeVerifier = authorizationCodeVerifier.Verifier();
			AuthorizationArgs authorizationArgs = new AuthorizationArgs();
			authorizationArgs.SetClientId(discordClientId);
			authorizationArgs.SetScopes(Client.GetDefaultPresenceScopes());
			authorizationArgs.SetCodeChallenge(authorizationCodeVerifier.Challenge());
			discordClient.Authorize(authorizationArgs, OnAuthorizeResult);
		}
		catch (Exception ex)
		{
			Debug.LogError("[Discord] Failed to start OAuth flow: " + ex.Message);
		}
	}

	private void OnAuthorizeResult(ClientResult result, string code, string redirectUri)
	{
		if (!result.Successful())
		{
			Debug.LogError("[Discord] Authorization failed: " + result.Error());
			return;
		}
		string text = codeVerifier;
		codeVerifier = null;
		if (string.IsNullOrEmpty(text))
		{
			Debug.LogError("[Discord] Missing OAuth code verifier; aborting token exchange.");
		}
		else
		{
			GetTokenFromCode(code, redirectUri, text);
		}
	}

	private void GetTokenFromCode(string code, string redirectUri, string verifier)
	{
		discordClient.GetToken(discordClientId, code, verifier, redirectUri, (ClientResult result, string token, string refreshToken, AuthorizationTokenType tokenType, int expiresIn, string scope) =>
		{
			codeVerifier = null;
			if (token != "")
			{
				OnReceivedToken(token);
			}
			else
			{
				Debug.LogError("[Discord] Failed to retrieve token");
			}
		});
	}

	private void OnReceivedToken(string token)
	{
		PlayerPrefs.SetString("Discord_AccessToken", token);
		PlayerPrefs.SetString("Discord_TokenExpiresAt", DateTime.Now.AddDays(7.0).ToBinary().ToString());
		PlayerPrefs.Save();
		discordClient.UpdateToken(AuthorizationTokenType.Bearer, token, (ClientResult result) =>
		{
			if (result.Successful())
			{
				discordClient.Connect();
			}
			else
			{
				Debug.LogError("[Discord] Failed to update token: " + result.Error());
			}
		});
	}

	private void OnDiscordJoin(string joinSecret)
	{
		if (string.IsNullOrEmpty(joinSecret))
		{
			return;
		}
		ulong result = 0uL;
		if (joinSecret.StartsWith("steam_lobby:"))
		{
			string text = joinSecret.Substring("steam_lobby:".Length);
			if (!ulong.TryParse(text, out result))
			{
				Debug.LogError("[Discord] Failed to parse lobby ID: " + text);
				return;
			}
		}
		else if (!ulong.TryParse(joinSecret, out result))
		{
			Debug.LogError("[Discord] Invalid join secret format: " + joinSecret);
			return;
		}
		JoinSteamLobby(new CSteamID(result));
	}

	private void JoinSteamLobby(CSteamID lobbyId)
	{
		if (SteamManager.Initialized && (!(NetworkSingleton<GameManager>.Instance != null) || NetworkSingleton<GameManager>.Instance.state != GameState.Game || !(SceneManager.GetActiveScene().name == "CasinoScene")))
		{
			if (MonoSingleton<LobbyManager>.Instance != null)
			{
				MonoSingleton<LobbyManager>.Instance.GetType().GetMethod("CleanupCurrentLobby", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(MonoSingleton<LobbyManager>.Instance, null);
			}
			SteamMatchmaking.JoinLobby(lobbyId);
		}
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		if (scene.name == "MainMenuScene" && HasUserAcceptedDiscordToaster() && discordClient == null)
		{
			ConnectDiscord(allowOAuthPrompt: false);
		}
		if (isDiscordReady)
		{
			UpdatePresenceForCurrentScene();
		}
	}

	public void RefreshPresence()
	{
		if (isDiscordReady)
		{
			UpdatePresenceForCurrentScene();
		}
	}

	private void UpdatePresenceForCurrentScene()
	{
		if (SceneManager.GetActiveScene().name == "MainMenuScene")
		{
			SetMainMenuPresence();
		}
		else if (UnityEngine.Object.FindFirstObjectByType<GameManager>() != null)
		{
			if (NetworkSingleton<GameManager>.Instance.state == GameState.Lobby)
			{
				SetInHomePresence();
			}
			else if (NetworkSingleton<GameManager>.Instance.state == GameState.Game)
			{
				SetInGamePresence();
			}
		}
	}

	public void SetMainMenuPresence()
	{
		if (!isDiscordReady || discordClient == null)
		{
			return;
		}
		int partySize = 0;
		int partyMax = 0;
		string joinSecret = null;
		if (lobbySettings != null && lobbySettings.steamLobbyID != CSteamID.Nil)
		{
			partySize = SteamMatchmaking.GetNumLobbyMembers(lobbySettings.steamLobbyID);
			partyMax = SteamMatchmaking.GetLobbyMemberLimit(lobbySettings.steamLobbyID);
			if (DiscordJoinSecretAllowedForLobby(lobbySettings.steamLobbyID))
			{
				joinSecret = $"steam_lobby:{lobbySettings.steamLobbyID.m_SteamID}";
			}
		}
		else if (lobbySettings != null)
		{
			partySize = lobbySettings.currentPlayerCount;
			partyMax = lobbySettings.maxPlayers;
		}
		UpdateRichPresence("In Main Menu", "Getting the gang together", partySize, partyMax, joinSecret);
	}

	public void SetInHomePresence()
	{
		if (!isDiscordReady || discordClient == null)
		{
			return;
		}
		string arg = MoneyFormatter.FormatWithDollar((NetworkSingleton<MoneyManager>.Instance != null) ? NetworkSingleton<MoneyManager>.Instance.balance : 0);
		int num = ((!(NetworkSingleton<GameManager>.Instance != null)) ? 1 : (NetworkSingleton<GameManager>.Instance.daysPassed + 1));
		int partySize = 0;
		string joinSecret = null;
		if (lobbySettings != null && lobbySettings.steamLobbyID != CSteamID.Nil)
		{
			partySize = SteamMatchmaking.GetNumLobbyMembers(lobbySettings.steamLobbyID);
			if (DiscordJoinSecretAllowedForLobby(lobbySettings.steamLobbyID))
			{
				joinSecret = $"steam_lobby:{lobbySettings.steamLobbyID.m_SteamID}";
			}
		}
		UpdateRichPresence("Playing Gamble with Friends", $"Day {num} - Balance: {arg}", partySize, lobbySettings?.maxPlayers ?? 0, joinSecret);
	}

	public void SetInGamePresence()
	{
		if (isDiscordReady && discordClient != null && !(UnityEngine.Object.FindFirstObjectByType<GameManager>() == null))
		{
			long num = ((NetworkSingleton<MoneyManager>.Instance != null) ? NetworkSingleton<MoneyManager>.Instance.balance : 0);
			long currentQuota = NetworkSingleton<GameManager>.Instance.currentQuota;
			string arg = MoneyFormatter.FormatWithDollar(num);
			string arg2 = MoneyFormatter.FormatWithDollar(currentQuota);
			int num2 = Mathf.RoundToInt((currentQuota > 0) ? (Mathf.Clamp01((float)num / (float)currentQuota) * 100f) : 0f);
			UpdateRichPresence("Playing Gamble with Friends", $"Letting it ride - {arg}/{arg2} ({num2}%)", 0, 0, null);
		}
	}

	public void UpdatePlayerCount()
	{
		if (isDiscordReady && NetworkSingleton<GameManager>.Instance != null)
		{
			if (NetworkSingleton<GameManager>.Instance.state == GameState.Lobby)
			{
				SetInHomePresence();
			}
			else if (SceneManager.GetActiveScene().name == "MainMenuScene")
			{
				SetMainMenuPresence();
			}
		}
	}

	private static bool DiscordJoinSecretAllowedForLobby(CSteamID lobbyId)
	{
		if (lobbyId != CSteamID.Nil)
		{
			return SteamManager.Initialized;
		}
		return false;
	}

	private void UpdateRichPresence(string details, string state, int partySize, int partyMax, string joinSecret)
	{
		if (!isDiscordReady || discordClient == null)
		{
			return;
		}
		try
		{
			currentActivity?.Dispose();
			currentActivity = new Activity();
			currentActivity.SetType(ActivityTypes.Playing);
			currentActivity.SetDetails(details);
			currentActivity.SetState(state);
			if (partySize > 0 && partyMax > 0 && !string.IsNullOrEmpty(joinSecret))
			{
				ActivityParty activityParty = new ActivityParty();
				activityParty.SetId(lobbySettings?.steamLobbyID.m_SteamID.ToString() ?? "0");
				activityParty.SetCurrentSize(partySize);
				activityParty.SetMaxSize(partyMax);
				activityParty.SetPrivacy(ActivityPartyPrivacy.Private);
				currentActivity.SetParty(activityParty);
				ActivitySecrets activitySecrets = new ActivitySecrets();
				activitySecrets.SetJoin(joinSecret);
				currentActivity.SetSecrets(activitySecrets);
			}
			discordClient.UpdateRichPresence(currentActivity, (ClientResult result) =>
			{
				if (!result.Successful())
				{
					Debug.LogError("[Discord] Failed to update presence: " + result.Error());
				}
			});
		}
		catch (Exception ex)
		{
			Debug.LogError("[Discord] Error updating Rich Presence: " + ex.Message + "\n" + ex.StackTrace);
		}
	}

	public void ClearRichPresence()
	{
		if (!isDiscordReady || discordClient == null)
		{
			return;
		}
		try
		{
			Activity activity = new Activity();
			discordClient.UpdateRichPresence(activity, (ClientResult result) =>
			{
			});
			activity.Dispose();
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[Discord] Failed to clear presence: " + ex.Message);
		}
	}

	private void ShutdownDiscord()
	{
		if (discordClient == null)
		{
			return;
		}
		try
		{
			currentActivity?.Dispose();
			currentActivity = null;
			discordClient.Disconnect();
			discordClient.Dispose();
			discordClient = null;
			isDiscordReady = false;
			codeVerifier = null;
			ConnectionStateChanged?.Invoke(obj: false);
		}
		catch (Exception ex)
		{
			Debug.LogError("[Discord] Error during shutdown: " + ex.Message);
		}
	}
}

}
