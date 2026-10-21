namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::FMODUnity;
using global::SushiAndFish.Casino.Steamworks;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.SceneManagement;
using global::UnityEngine.UI;

public class GoOnlineClient : MonoBehaviour
{
	[Header("UI Progress Elements")]
	[SerializeField]
	private Image[] chipImages = new Image[5];

	[SerializeField]
	private TextMeshProUGUI statusText;

	[Header("Progress Settings")]
	[SerializeField]
	private float statusDisplayDelay = 1f;

	[SerializeField]
	private float chipAppearDuration = 0.5f;

	[Header("SFX")]
	[SerializeField]
	private EventReference loadingSFX;

	private readonly string[] statusMessages = new string[5] { "Rolling the dice...", "Shuffling the deck...", "Dealing the cards...", "Stacking the chips...", "All bets are in!" };

	private LobbySettings lobbySettings;

	private CallResult<LobbyCreated_t> lobbyCreated;

	private Callback<LobbyEnter_t> lobbyEnterCallback;

	private bool hasProcessedLobbyEnter;

	private int lastShownChipIndex = -1;

	private void Start()
	{
		StartCoroutine(InitializeWithRetry());
	}

	private void OnDestroy()
	{
		lobbyCreated?.Dispose();
		lobbyEnterCallback?.Dispose();
		if (chipImages == null)
		{
			return;
		}
		Image[] array = chipImages;
		foreach (Image image in array)
		{
			if (image != null)
			{
				image.DOKill();
			}
		}
	}

	private IEnumerator InitializeWithRetry()
	{
		yield return StartCoroutine(SteamInitialization());
		if (!SteamManager.Initialized)
		{
			Debug.LogError("Initialization failed - Steam not initialized or authentication failed. Cannot proceed.");
			yield break;
		}
		LoadLobbySettings();
		LeaveExistingLobbyIfAny();
		InitializeUI();
		SetupLobbyCallbacks();
		StartProgressSequence();
	}

	private IEnumerator SteamInitialization()
	{
		int maxRetries = 3;
		float retryDelay = 1f;
		for (int retryCount = 0; retryCount < maxRetries; retryCount++)
		{
			if (SteamManager.Initialized)
			{
				break;
			}
			if (retryCount > 0)
			{
				UpdateRetryUI(retryCount, maxRetries);
				yield return new WaitForSeconds(retryDelay);
				retryDelay = Mathf.Min(retryDelay * 1.5f, 5f);
			}
			yield return new WaitForSeconds(0.5f);
		}
		if (!SteamManager.Initialized)
		{
			HandleSteamInitFailure();
		}
		else
		{
			yield return StartCoroutine(VerifyGameOwnership());
		}
	}

	private IEnumerator VerifyGameOwnership()
	{
		if (!SteamApps.BIsSubscribedApp(new AppId_t(3892270u)))
		{
			HandleGameOwnershipFailure();
		}
		else
		{
			Debug.Log("[Verified] Steam Initialization - Game ownership - [SUCCESSFUL]");
		}
		yield break;
	}

	private void HandleGameOwnershipFailure()
	{
		Debug.LogError("Steam Initialization - Game ownership verification failed! You do not own this game.");
		if (statusText != null)
		{
			statusText.text = "Access Denied: You do not own this game.";
		}
		base.enabled = false;
	}

	private void UpdateRetryUI(int retryCount, int maxRetries)
	{
		Debug.LogWarning($"Steam initialization attempt {retryCount + 1}/{maxRetries} failed. Retrying...");
		if (statusText != null)
		{
			statusText.text = $"Steam initialization failed. Retrying... ({retryCount + 1}/{maxRetries})";
		}
	}

	private void HandleSteamInitFailure()
	{
		Debug.LogError("Steam failed to initialize after multiple attempts!");
		if (statusText != null)
		{
			statusText.text = "Steam initialization failed! Please ensure Steam is running.";
		}
		if (chipImages != null)
		{
			Image[] array = chipImages;
			foreach (Image image in array)
			{
				if (image != null)
				{
					image.color = Color.red;
					image.gameObject.SetActive(value: true);
				}
			}
		}
		base.enabled = false;
	}

	private void LoadLobbySettings()
	{
		lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
		if (lobbySettings == null)
		{
			Debug.LogError("Failed to load LobbySettings!");
		}
	}

	private void LeaveExistingLobbyIfAny()
	{
		if (!(lobbySettings == null) && SteamManager.Initialized)
		{
			if (lobbySettings.steamLobbyID != CSteamID.Nil)
			{
				Debug.Log($"Leaving existing Steam lobby before creating a new one: {lobbySettings.steamLobbyID}");
				SteamMatchmaking.LeaveLobby(lobbySettings.steamLobbyID);
			}
			lobbySettings.steamLobbyID = CSteamID.Nil;
			lobbySettings.inALobby = false;
			lobbySettings.lobbyCode = "";
			lobbySettings.currentPlayerCount = 0;
			lobbySettings.NotifyChanged();
		}
	}

	private void SetupLobbyCallbacks()
	{
		lobbyCreated = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
		lobbyEnterCallback?.Dispose();
		lobbyEnterCallback = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
		hasProcessedLobbyEnter = false;
	}

	private void InitializeUI()
	{
		if (chipImages != null)
		{
			Image[] array = chipImages;
			foreach (Image image in array)
			{
				if (image != null)
				{
					image.gameObject.SetActive(value: false);
					image.color = Color.white;
				}
			}
		}
		if (statusText != null)
		{
			statusText.text = statusMessages[0];
		}
		lastShownChipIndex = -1;
	}

	private void StartProgressSequence()
	{
		StartCoroutine(ProgressSequenceWithDelays());
	}

	private IEnumerator ProgressSequenceWithDelays()
	{
		for (int i = 0; i < 5; i++)
		{
			if (statusText != null && i < statusMessages.Length)
			{
				statusText.text = statusMessages[i];
			}
			ShowChipForProgress(i);
			SFXParams[] sFXParams = new SFXParams[1]
			{
				new SFXParams("LoadingPercent", ((float)i + 1f) / 5f)
			};
			SFXManager.SFXOneShotWithParameters(loadingSFX, sFXParams);
			yield return new WaitForSeconds(statusDisplayDelay);
		}
		CreateLobby();
	}

	private void ShowChipForProgress(int statusIndex)
	{
		if (statusIndex > lastShownChipIndex)
		{
			int num = Mathf.Clamp(statusIndex, 0, chipImages.Length - 1);
			if (chipImages != null && num < chipImages.Length && chipImages[num] != null)
			{
				Image image = chipImages[num];
				ApplyRandomChipMaterial(image);
				image.gameObject.SetActive(value: true);
				image.transform.localScale = Vector3.zero;
				image.transform.DOScale(Vector3.one, chipAppearDuration).SetEase(Ease.OutBack);
				lastShownChipIndex = statusIndex;
			}
		}
	}

	private void ApplyRandomChipMaterial(Image chipImage)
	{
	}

	public void CreateLobby()
	{
		SteamAPICall_t hAPICall = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, lobbySettings.maxPlayers);
		lobbyCreated.Set(hAPICall);
	}

	private void OnLobbyCreated(LobbyCreated_t res, bool failure)
	{
		if (res.m_eResult != EResult.k_EResultOK)
		{
			Debug.LogError($"Lobby creation failed: {res.m_eResult}");
			if (statusText != null && 2 < statusMessages.Length)
			{
				statusText.text = statusMessages[2];
			}
		}
		else
		{
			StartCoroutine(ContinueAfterLobbyCreation(res));
		}
	}

	private IEnumerator ContinueAfterLobbyCreation(LobbyCreated_t res)
	{
		SetupLobbyData(res);
		InitializeWebSocket();
		if (SteamManager.Initialized)
		{
			StartCoroutine(JoinLobbyWithDelay());
		}
		else
		{
			Debug.LogError("Steam is not initialized! Cannot join lobby.");
		}
		yield break;
	}

	private void SetupLobbyData(LobbyCreated_t res)
	{
		lobbySettings.steamLobbyID = new CSteamID(res.m_ulSteamIDLobby);
		string pchValue = SteamUser.GetSteamID().ToString();
		SteamMatchmaking.SetLobbyData(lobbySettings.steamLobbyID, "name", SteamFriends.GetPersonaName() + "'s Lobby");
		SteamMatchmaking.SetLobbyData(lobbySettings.steamLobbyID, "HostAddress", pchValue);
		SteamMatchmaking.SetLobbyData(lobbySettings.steamLobbyID, "SelectedScene", "Gameplay");
		SteamMatchmaking.SetLobbyData(lobbySettings.steamLobbyID, "GameStarted", "0");
		SteamMatchmaking.SetLobbyData(lobbySettings.steamLobbyID, "LobbyCode", lobbySettings.lobbyCode);
		string version = Application.version;
		SteamMatchmaking.SetLobbyMemberData(lobbySettings.steamLobbyID, "GameVersion", version);
		Debug.Log("Set host game version on lobby creation: " + version);
	}

	private void InitializeWebSocket()
	{
		WebSocketManager webSocketManager = Object.FindFirstObjectByType<WebSocketManager>();
		if (webSocketManager != null)
		{
			if (webSocketManager.WebSocketFeaturesEnabled)
			{
				webSocketManager.Initialize();
			}
		}
		else
		{
			Debug.LogError("WebSocketManager not found! Cannot initialize WebSocket connection.");
		}
	}

	private IEnumerator JoinLobbyWithDelay()
	{
		yield return new WaitForSeconds(0.1f);
		if (SteamManager.Initialized)
		{
			SteamMatchmaking.JoinLobby(lobbySettings.steamLobbyID);
		}
		else
		{
			Debug.LogError("Steam is not initialized! Cannot join lobby.");
		}
	}

	private void OnLobbyEntered(LobbyEnter_t cb)
	{
		if (!hasProcessedLobbyEnter)
		{
			CSteamID cSteamID = new CSteamID(cb.m_ulSteamIDLobby);
			if (!(cSteamID != lobbySettings.steamLobbyID))
			{
				hasProcessedLobbyEnter = true;
				lobbySettings.inALobby = true;
				Debug.Log($"Successfully joined own lobby {cSteamID}, LobbyCode: {lobbySettings.lobbyCode}, ensuring lobby is fully ready...");
				StartCoroutine(WaitForLobbyReady());
				lobbyEnterCallback?.Dispose();
				lobbyEnterCallback = null;
			}
		}
	}

	private IEnumerator WaitForLobbyReady()
	{
		yield return null;
		int maxAttempts = 10;
		for (int attempts = 0; attempts < maxAttempts; attempts++)
		{
			if (IsLobbyReady())
			{
				yield return new WaitForSeconds(0.5f);
				StartCoroutine(LoadMainMenuSceneRoutine());
				yield break;
			}
			yield return new WaitForSeconds(0.1f);
		}
		Debug.LogWarning("Lobby ready timeout reached, loading MainMenuScene anyway.");
		StartCoroutine(LoadMainMenuSceneRoutine());
	}

	private bool IsLobbyReady()
	{
		if (lobbySettings.steamLobbyID == CSteamID.Nil)
		{
			return false;
		}
		if (SteamMatchmaking.GetNumLobbyMembers(lobbySettings.steamLobbyID) <= 0)
		{
			return false;
		}
		return SteamMatchmaking.GetLobbyMemberByIndex(lobbySettings.steamLobbyID, 0) != CSteamID.Nil;
	}

	private IEnumerator LoadMainMenuSceneRoutine()
	{
		int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
		if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
		{
			MonoSingleton<SceneTransitioner>.Instance.SetLoadingScreen(isEnabled: true, 0.3f, loadingScreen: false);
			MonoSingleton<SceneTransitioner>.Instance.SetLoadingScreen(isEnabled: false, 0.3f);
			yield return new WaitForSeconds(0.3f);
			SceneManager.LoadScene(nextSceneIndex);
		}
		else
		{
			Debug.LogWarning("No more scenes to load. Check your Build Settings.");
		}
	}
}

}
