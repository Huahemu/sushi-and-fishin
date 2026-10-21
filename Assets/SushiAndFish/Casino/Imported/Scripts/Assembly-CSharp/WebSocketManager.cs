namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Net.Security;
using global::System.Security.Authentication;
using global::System.Security.Cryptography.X509Certificates;
using global::System.Text;
using global::System.Text.RegularExpressions;
using global::System.Threading;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::SkyBrave_Toolkit.Scripts.Scriptable_Game_Events;
using global::SushiAndFish.Casino.Steamworks;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.SceneManagement;
using global::WebSocketSharp;

public class WebSocketManager : MonoSingleton<WebSocketManager>
{
	[Serializable]
	public class ServerMessage
	{
		public string message;
	}

	[Serializable]
	public class VoiceMessage
	{
		public string type;

		public string message;

		public string data;

		public int sampleRate;

		public int channels;

		public string format;
	}

	[Serializable]
	public class GameSettingsChangedMessage
	{
		public string type;

		public string gameId;

		public string subGameName;

		public string message;

		public bool isMainGameSettings;
	}

	public interface IMessageHandler
	{
		bool CanHandle(string message);

		void Handle(string message);

		string GetDisplayText(string message);
	}

	public abstract class BaseMessageHandler : IMessageHandler
	{
		protected GameObject messagePrefab;

		protected Transform messageParent;

		protected float displayDuration = 2f;

		public BaseMessageHandler(GameObject prefab, Transform parent, float duration = 2f)
		{
			messagePrefab = prefab;
			messageParent = parent;
			displayDuration = duration;
		}

		public abstract bool CanHandle(string message);

		public abstract void Handle(string message);

		public abstract string GetDisplayText(string message);

		public void ShowMessage(string text, float duration = -1f)
		{
			if ((bool)messagePrefab && (bool)messageParent)
			{
				GameObject gameObject = UnityEngine.Object.Instantiate(messagePrefab, messageParent);
				gameObject.GetComponentInChildren<TextMeshProUGUI>().text = text;
				UnityEngine.Object.Destroy(gameObject, (duration > 0f) ? duration : displayDuration);
			}
		}
	}

	public class SpeedMessageHandler : BaseMessageHandler
	{
		private GameSettings gameSettings;

		public SpeedMessageHandler(GameObject prefab, Transform parent, GameSettings settings)
			: base(prefab, parent)
		{
			gameSettings = settings;
		}

		public override bool CanHandle(string message)
		{
			return message.ToLower().Contains("speed");
		}

		public override void Handle(string message)
		{
			Match match = Regex.Match(message, "-?\\d*\\.?\\d+");
			if (match.Success && float.TryParse(match.Value, out var result))
			{
				gameSettings.SetTimeScale(result);
			}
		}

		public override string GetDisplayText(string message)
		{
			return message;
		}
	}

	public class DefaultMessageHandler : BaseMessageHandler
	{
		public DefaultMessageHandler(GameObject prefab, Transform parent)
			: base(prefab, parent, 5f)
		{
		}

		public override bool CanHandle(string message)
		{
			return true;
		}

		public override void Handle(string message)
		{
		}

		public override string GetDisplayText(string message)
		{
			return message;
		}
	}

	[SerializeField]
	private GameEvent webSocketEvent;

	[SerializeField]
	private TextMeshProUGUI sessionCodeLabel;

	[Tooltip("When off, no outbound WebSocket connection, ping, or reconnect logic runs.")]
	[SerializeField]
	private bool webSocketFeaturesEnabled = true;

	private WebSocket ws;

	private string sessionCode = "";

	private string ownerNickname = "";

	private SynchronizationContext mainThreadCtx;

	private Thread wsThread;

	private bool isConnecting;

	private readonly object wsLock = new object();

	private const float PING_INTERVAL = 30f;

	private const float RECONNECT_INTERVAL = 300f;

	private bool shouldPing = true;

	private float nextConnectAttemptTime;

	public GameObject messagePrefab;

	public Transform messageParent;

	private GameSettings gameSettings;

	private LobbySettings lobbySettings;

	private VoiceAnnouncementPlayer voicePlayer;

	private Dictionary<string, IMessageHandler> messageHandlers = new Dictionary<string, IMessageHandler>();

	private Callback<LobbyEnter_t> lobbyEnterCallback;

	private Callback<LobbyDataUpdate_t> lobbyDataUpdateCallback;

	public bool WebSocketFeaturesEnabled => webSocketFeaturesEnabled;

	private void RouteMessage(string message)
	{
		foreach (IMessageHandler value in messageHandlers.Values)
		{
			if (value.CanHandle(message))
			{
				value.Handle(message);
				if (value is BaseMessageHandler baseMessageHandler)
				{
					baseMessageHandler.ShowMessage(value.GetDisplayText(message));
				}
				break;
			}
		}
	}

	public void RegisterMessageHandler(string key, IMessageHandler handler)
	{
		if (messageHandlers.ContainsKey(key))
		{
			messageHandlers[key] = handler;
		}
		else
		{
			messageHandlers.Add(key, handler);
		}
	}

	protected override void OnAwake()
	{
		base.OnAwake();
		mainThreadCtx = SynchronizationContext.Current;
		gameSettings = SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
		lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
		InitializeVoicePlayer();
		InitializeMessageHandlers();
		if (!webSocketFeaturesEnabled)
		{
			Disconnect();
		}
	}

	private void OnEnable()
	{
		SceneManager.sceneLoaded += OnSceneLoaded;
		if (SteamManager.Initialized)
		{
			lobbyEnterCallback = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
			lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
		}
	}

	private void OnDisable()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
		lobbyEnterCallback?.Dispose();
		lobbyDataUpdateCallback?.Dispose();
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		if (scene.name == "MainMenuScene")
		{
			sessionCode = GenerateSessionCode();
			UpdateSessionCodeFromLobby();
		}
		else if (scene.name == "NetworkSetupScene")
		{
			Disconnect();
		}
	}

	private void OnLobbyEntered(LobbyEnter_t cb)
	{
		if (lobbySettings != null)
		{
			lobbySettings.steamLobbyID = new CSteamID(cb.m_ulSteamIDLobby);
			lobbySettings.inALobby = true;
			UpdateSessionCodeFromLobby();
		}
	}

	private void OnLobbyDataUpdate(LobbyDataUpdate_t cb)
	{
		if (!(lobbySettings == null) && !(lobbySettings.steamLobbyID == CSteamID.Nil) && !((CSteamID)cb.m_ulSteamIDLobby != lobbySettings.steamLobbyID) && cb.m_ulSteamIDMember == cb.m_ulSteamIDLobby)
		{
			string lobbyData = SteamMatchmaking.GetLobbyData(lobbySettings.steamLobbyID, "LobbyCode");
			if (!string.IsNullOrEmpty(lobbyData) && lobbyData != sessionCode && !IsLobbyOwner())
			{
				sessionCode = lobbyData;
				ownerNickname = GetLobbyOwnerNickname();
				UpdateUI();
			}
		}
	}

	private void UpdateSessionCodeFromLobby()
	{
		if (IsLobbyOwner())
		{
			SteamMatchmaking.SetLobbyData(lobbySettings.steamLobbyID, "LobbyCode", sessionCode);
			lobbySettings.lobbyCode = sessionCode;
		}
		else
		{
			string lobbyData = SteamMatchmaking.GetLobbyData(lobbySettings.steamLobbyID, "LobbyCode");
			if (!string.IsNullOrEmpty(lobbyData))
			{
				sessionCode = lobbyData;
			}
		}
		ownerNickname = GetLobbyOwnerNickname();
		UpdateUI();
	}

	private bool IsLobbyOwner()
	{
		if (!SteamManager.Initialized || lobbySettings == null || lobbySettings.steamLobbyID == CSteamID.Nil)
		{
			return false;
		}
		try
		{
			return SteamMatchmaking.GetLobbyOwner(lobbySettings.steamLobbyID) == SteamUser.GetSteamID();
		}
		catch
		{
			return false;
		}
	}

	private void InitializeVoicePlayer()
	{
		voicePlayer = UnityEngine.Object.FindFirstObjectByType<VoiceAnnouncementPlayer>();
		if (voicePlayer == null)
		{
			GameObject gameObject = new GameObject("VoiceAnnouncementPlayer");
			voicePlayer = gameObject.AddComponent<VoiceAnnouncementPlayer>();
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
		}
	}

	private void InitializeMessageHandlers()
	{
		RegisterMessageHandler("speed", new SpeedMessageHandler(messagePrefab, messageParent, gameSettings));
		RegisterMessageHandler("default", new DefaultMessageHandler(messagePrefab, messageParent));
	}

	private string GenerateSessionCode()
	{
		if (lobbySettings == null)
		{
			return "";
		}
		int num = ((lobbySettings.codeLength > 0) ? lobbySettings.codeLength : 6);
		StringBuilder stringBuilder = new StringBuilder(num);
		global::System.Random random = new global::System.Random();
		for (int i = 0; i < num; i++)
		{
			stringBuilder.Append("1234567890"[random.Next("1234567890".Length)]);
		}
		return stringBuilder.ToString();
	}

	public void Initialize()
	{
	}

	private void Disconnect()
	{
		shouldPing = false;
		lock (wsLock)
		{
			if (ws != null)
			{
				if (ws.IsAlive)
				{
					ws.Close();
				}
				ws = null;
			}
		}
		isConnecting = false;
	}

	private void UpdateUI()
	{
		if (sessionCodeLabel != null)
		{
			if (!string.IsNullOrEmpty(ownerNickname))
			{
				sessionCodeLabel.text = sessionCode + " | " + ownerNickname + "'s Lobby";
			}
			else
			{
				sessionCodeLabel.text = sessionCode;
			}
		}
	}

	private string GetLobbyOwnerNickname()
	{
		if (!SteamManager.Initialized || lobbySettings == null || lobbySettings.steamLobbyID == CSteamID.Nil)
		{
			return string.Empty;
		}
		try
		{
			CSteamID lobbyOwner = SteamMatchmaking.GetLobbyOwner(lobbySettings.steamLobbyID);
			if (lobbyOwner != CSteamID.Nil)
			{
				string friendPersonaName = SteamFriends.GetFriendPersonaName(lobbyOwner);
				return string.IsNullOrEmpty(friendPersonaName) ? SteamFriends.GetPersonaName() : friendPersonaName;
			}
		}
		catch
		{
		}
		return string.Empty;
	}

	private void StartWebSocketThread()
	{
		if (!WebSocketFeaturesEnabled || isConnecting)
		{
			return;
		}
		shouldPing = true;
		isConnecting = true;
		Debug.Log("[WebSocket] Connection attempt started. sessionCode=" + sessionCode + ", ownerNickname=" + ownerNickname);
		wsThread = new Thread(() =>
		{
			try
			{
				Connect();
			}
			catch (Exception ex)
			{
				Debug.LogError("WebSocket thread error: " + ex.Message);
				lock (wsLock)
				{
					isConnecting = false;
				}
			}
		});
		wsThread.IsBackground = true;
		wsThread.Start();
	}

	private void Connect()
	{
		lock (wsLock)
		{
			if (!WebSocketFeaturesEnabled)
			{
				isConnecting = false;
				return;
			}
			try
			{
				string url = BuildWebSocketUrl();
				ws = new WebSocket(url);
				ws.SslConfiguration.EnabledSslProtocols = SslProtocols.Tls12;
				ws.SslConfiguration.ServerCertificateValidationCallback = (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;
				ws.WaitTime = TimeSpan.FromHours(24.0);
				ws.OnOpen += (object s, EventArgs e) =>
				{
					Debug.Log("[WebSocket] Connected.");
					UnityMainThreadDispatcher.Enqueue(() =>
					{
						webSocketEvent.Raise();
					});
					Thread thread = new Thread(() =>
					{
						while (shouldPing && ws != null && ws.IsAlive)
						{
							SendPing();
							Thread.Sleep(30000);
						}
					});
					thread.IsBackground = true;
					thread.Start();
				};
				ws.OnMessage += (object s, MessageEventArgs e) =>
				{
					string rawData = e.Data;
					string messageType = "text";
					try
					{
						VoiceMessage voiceMsg = JsonUtility.FromJson<VoiceMessage>(rawData);
						if (!string.IsNullOrEmpty(voiceMsg.type) && (voiceMsg.type == "voice_start" || voiceMsg.type == "voice_audio" || voiceMsg.type == "voice_stop"))
						{
							messageType = "voice";
							UnityMainThreadDispatcher.Enqueue(() =>
							{
								RelayMessageToClients(rawData, messageType);
								HandleVoiceMessage(voiceMsg);
							});
							return;
						}
					}
					catch
					{
					}
					try
					{
						GameSettingsChangedMessage settingsChangedMsg = JsonUtility.FromJson<GameSettingsChangedMessage>(rawData);
						if ((!string.IsNullOrEmpty(settingsChangedMsg.type) && (settingsChangedMsg.type == "gameSettingsChanged" || settingsChangedMsg.type == "mainGameSettingsChanged")) || settingsChangedMsg.isMainGameSettings)
						{
							messageType = "settings";
							UnityMainThreadDispatcher.Enqueue(() =>
							{
								RelayMessageToClients(rawData, messageType);
								HandleGameSettingsChanged(settingsChangedMsg);
							});
							return;
						}
					}
					catch
					{
					}
					UnityMainThreadDispatcher.Enqueue(() =>
					{
						RelayMessageToClients(rawData, messageType);
						try
						{
							ServerMessage serverMessage = JsonUtility.FromJson<ServerMessage>(rawData);
							RouteMessage(serverMessage?.message ?? rawData);
						}
						catch
						{
							RouteMessage(rawData);
						}
					});
				};
				ws.OnClose += (object s, CloseEventArgs e) =>
				{
					Debug.Log($"[WebSocket] Disconnected. code={e.Code}, reason={e.Reason}, clean={e.WasClean}");
					lock (wsLock)
					{
						isConnecting = false;
					}
				};
				ws.OnError += (object s, ErrorEventArgs e) =>
				{
					Debug.LogError("[WebSocket] Error: " + e.Message);
					lock (wsLock)
					{
						isConnecting = false;
					}
				};
				Debug.Log("[WebSocket] Connecting...");
				ws.Connect();
			}
			catch (Exception ex)
			{
				Debug.LogError("[WebSocket] Failed to establish connection: " + ex.Message);
				lock (wsLock)
				{
					isConnecting = false;
				}
			}
		}
	}

	private string BuildWebSocketUrl()
	{
		string text = Uri.EscapeDataString(sessionCode ?? string.Empty);
		string text2 = Uri.EscapeDataString(ownerNickname ?? string.Empty);
		return "wss://api.diabolical.studio/socket/?sessionCode=" + text + "&ownerNickname=" + text2;
	}

	private void SendPing()
	{
		lock (wsLock)
		{
			if (ws != null && ws.IsAlive)
			{
				try
				{
					ws.Ping();
					return;
				}
				catch (Exception ex)
				{
					Debug.LogError("Ping failed: " + ex.Message);
					return;
				}
			}
		}
	}

	private void HandleVoiceMessage(VoiceMessage voiceMsg)
	{
		if (voicePlayer == null)
		{
			InitializeVoicePlayer();
			if (voicePlayer == null)
			{
				return;
			}
		}
		switch (voiceMsg.type)
		{
		case "voice_start":
		{
			voicePlayer.OnVoiceStart();
			BossAnnouncement[] array = UnityEngine.Object.FindObjectsByType<BossAnnouncement>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			for (int i = 0; i < array.Length; i++)
			{
				array[i].StartSlideIn();
			}
			break;
		}
		case "voice_audio":
			if (!string.IsNullOrEmpty(voiceMsg.data))
			{
				voicePlayer.OnVoiceAudio(voiceMsg.data);
			}
			break;
		case "voice_stop":
		{
			voicePlayer.OnVoiceStop();
			BossAnnouncement[] array = UnityEngine.Object.FindObjectsByType<BossAnnouncement>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			for (int i = 0; i < array.Length; i++)
			{
				array[i].StartSlideOut();
			}
			break;
		}
		}
	}

	private void HandleGameSettingsChanged(GameSettingsChangedMessage settingsMsg)
	{
		if (settingsMsg.isMainGameSettings || (!string.IsNullOrEmpty(settingsMsg.type) && settingsMsg.type == "mainGameSettingsChanged") || string.IsNullOrEmpty(settingsMsg.subGameName))
		{
			if (NetworkSingleton<GameSettingsAPIManager>.Instance != null)
			{
				NetworkSingleton<GameSettingsAPIManager>.Instance.ReloadMainSettings();
			}
		}
		else if (NetworkSingleton<RequestSettingsFromApi>.Instance != null)
		{
			NetworkSingleton<RequestSettingsFromApi>.Instance.ReloadSettings();
		}
	}

	private void Start()
	{
		StartCoroutine(CheckForGameStart());
		StartCoroutine(MonitorAndRegisterClientHandler());
	}

	private IEnumerator CheckForGameStart()
	{
		bool wasServerActive = false;
		while (true)
		{
			bool active = NetworkServer.active;
			if (!WebSocketFeaturesEnabled)
			{
				Disconnect();
				nextConnectAttemptTime = 0f;
				wasServerActive = active;
				yield return new WaitForSeconds(0.5f);
				continue;
			}
			if (wasServerActive && !active)
			{
				Disconnect();
				nextConnectAttemptTime = 0f;
			}
			if ((WebSocketFeaturesEnabled & active) && !isConnecting && (ws == null || !ws.IsAlive) && !string.IsNullOrEmpty(sessionCode) && IsLobbyOwner() && Time.unscaledTime >= nextConnectAttemptTime)
			{
				StartWebSocketThread();
				nextConnectAttemptTime = Time.unscaledTime + 300f;
			}
			wasServerActive = active;
			yield return new WaitForSeconds(0.5f);
		}
	}

	private IEnumerator MonitorAndRegisterClientHandler()
	{
		bool wasClientActive = false;
		while (true)
		{
			bool active = NetworkClient.active;
			if (active && !wasClientActive)
			{
				NetworkClient.ReplaceHandler<WebSocketRelayMessage>(OnWebSocketRelayMessage);
			}
			wasClientActive = active;
			yield return new WaitForSeconds(0.5f);
		}
	}

	private bool IsNetworkHost()
	{
		return NetworkServer.active;
	}

	private void RelayMessageToClients(string rawMessage, string messageType = "text")
	{
		if (IsNetworkHost() && NetworkServer.active)
		{
			NetworkServer.SendToAll(new WebSocketRelayMessage
			{
				rawData = rawMessage,
				messageType = messageType
			});
		}
	}

	private void ProcessRelayedMessage(WebSocketRelayMessage message)
	{
		if (IsNetworkHost())
		{
			return;
		}
		string rawData = message.rawData;
		try
		{
			VoiceMessage voiceMessage = JsonUtility.FromJson<VoiceMessage>(rawData);
			if (!string.IsNullOrEmpty(voiceMessage.type) && (voiceMessage.type == "voice_start" || voiceMessage.type == "voice_audio" || voiceMessage.type == "voice_stop"))
			{
				HandleVoiceMessage(voiceMessage);
				return;
			}
		}
		catch
		{
		}
		try
		{
			GameSettingsChangedMessage gameSettingsChangedMessage = JsonUtility.FromJson<GameSettingsChangedMessage>(rawData);
			if ((!string.IsNullOrEmpty(gameSettingsChangedMessage.type) && (gameSettingsChangedMessage.type == "gameSettingsChanged" || gameSettingsChangedMessage.type == "mainGameSettingsChanged")) || gameSettingsChangedMessage.isMainGameSettings)
			{
				HandleGameSettingsChanged(gameSettingsChangedMessage);
				return;
			}
		}
		catch
		{
		}
		try
		{
			RouteMessage(JsonUtility.FromJson<ServerMessage>(rawData)?.message ?? rawData);
		}
		catch
		{
			RouteMessage(rawData);
		}
	}

	private void OnWebSocketRelayMessage(WebSocketRelayMessage message)
	{
		ProcessRelayedMessage(message);
	}

	private void OnApplicationQuit()
	{
		shouldPing = false;
		Disconnect();
		if (wsThread != null && wsThread.IsAlive)
		{
			wsThread.Join(1000);
		}
	}

	private void OnDestroy()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
		lobbyEnterCallback?.Dispose();
		lobbyDataUpdateCallback?.Dispose();
		if (NetworkClient.active)
		{
			NetworkClient.UnregisterHandler<WebSocketRelayMessage>();
		}
		Disconnect();
	}
}

}
