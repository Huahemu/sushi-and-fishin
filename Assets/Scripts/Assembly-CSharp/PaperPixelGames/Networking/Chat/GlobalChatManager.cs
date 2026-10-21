using System;
using System.Collections.Generic;
using System.Text;
using PaperPixelGames.Core;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.DevConsole;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States.Game;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking.Chat.UI;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine;
using UnityServiceLocator;

namespace PaperPixelGames.Networking.Chat
{
	public sealed class GlobalChatManager : Singleton<GlobalChatManager>
	{
		[Header("Config")]
		[SerializeField]
		private int _maxMessageLength = 160;

		[SerializeField]
		private int _maxDevConsoleMessageLength = 512;

		[SerializeField]
		private int _maxHistoryCount = 50;

		[SerializeField]
		private float _sendCooldownSeconds = 0.75f;

		[SerializeField]
		private GlobalChatUI _globalChatUI;

		[Header("Debug")]
		[SerializeField]
		private bool _logMessages;

		private const string RuntimeDebugToolsUnlockKeyword = "irmikhelvasi";

		private readonly List<ChatMessageData> _history = new List<ChatMessageData>();

		private SteamLobbyService _steamLobbyService;

		private bool _isInitialized;

		private float _lastSendTime = -999f;

		private ulong _activeLobbyId;

		private bool _hasPendingNextLobbyFailure;

		private string _pendingNextLobbyFailureKey = string.Empty;

		private string _pendingNextLobbyFailureFallback = string.Empty;

		private RestaurantChatTone _pendingNextLobbyFailureTone;

		public override SingletonScope Scope => SingletonScope.Application;

		public IReadOnlyList<ChatMessageData> History => _history;

		public bool HasActiveLobby
		{
			get
			{
				if (_steamLobbyService != null)
				{
					return (ulong)_steamLobbyService.CurrentLobby.Id != 0;
				}
				return false;
			}
		}

		public event Action<ChatMessageData> MessageReceived;

		public event Action SessionCleared;

		public void Initialize(SteamLobbyService steamLobbyService)
		{
			if (steamLobbyService == null)
			{
				UtilityMethods.Log("[GlobalChat] Initialize failed. SteamLobbyService is null.", LogType.Warning);
			}
			else if (!_isInitialized || !(_steamLobbyService == steamLobbyService))
			{
				UnbindSteamLobbyService();
				_steamLobbyService = steamLobbyService;
				BindSteamLobbyService();
				_isInitialized = true;
				_globalChatUI.Initialize();
				UtilityMethods.Log("[GlobalChat] Initialized.");
			}
		}

		public void ClearNewContext()
		{
			_globalChatUI.ClearNewContext();
		}

		private void OnDestroy()
		{
			UnbindSteamLobbyService();
			_isInitialized = false;
		}

		private void BindSteamLobbyService()
		{
			if (!(_steamLobbyService == null))
			{
				_steamLobbyService.LobbyEntered -= OnLobbyEntered;
				_steamLobbyService.LobbyMemberJoined -= OnLobbyMemberJoined;
				_steamLobbyService.LobbyMemberLeft -= OnLobbyMemberLeft;
				_steamLobbyService.LobbyChatMessageReceived -= OnLobbyChatMessageReceived;
				_steamLobbyService.LobbyHostRecommendationReceived -= OnLobbyHostRecommendationReceived;
				_steamLobbyService.LobbyEntered += OnLobbyEntered;
				_steamLobbyService.LobbyMemberJoined += OnLobbyMemberJoined;
				_steamLobbyService.LobbyMemberLeft += OnLobbyMemberLeft;
				_steamLobbyService.LobbyChatMessageReceived += OnLobbyChatMessageReceived;
				_steamLobbyService.LobbyHostRecommendationReceived += OnLobbyHostRecommendationReceived;
			}
		}

		private void UnbindSteamLobbyService()
		{
			if (!(_steamLobbyService == null))
			{
				_steamLobbyService.LobbyEntered -= OnLobbyEntered;
				_steamLobbyService.LobbyMemberJoined -= OnLobbyMemberJoined;
				_steamLobbyService.LobbyMemberLeft -= OnLobbyMemberLeft;
				_steamLobbyService.LobbyChatMessageReceived -= OnLobbyChatMessageReceived;
				_steamLobbyService.LobbyHostRecommendationReceived -= OnLobbyHostRecommendationReceived;
				_steamLobbyService = null;
			}
		}

		public bool TrySendGlobalMessage(string rawMessage)
		{
			string error;
			return TrySendGlobalMessage(rawMessage, out error);
		}

		public bool TrySendGlobalMessage(string rawMessage, out string error)
		{
			error = string.Empty;
			string text = NormalizeMessage(rawMessage, _maxMessageLength);
			if (string.IsNullOrWhiteSpace(text))
			{
				error = "Message is empty.";
				return false;
			}
			if (Time.unscaledTime - _lastSendTime < _sendCooldownSeconds)
			{
				error = "You are sending messages too quickly.";
				return false;
			}
			if (Singleton<MultiplayerCoordinator>.TryGetInstance(out var instance) && instance.IsSingleplayerSession)
			{
				PPG_Player pPG_Player = (Singleton<PlayersManager>.TryGetInstance(out var instance2) ? instance2.LocalPlayer : null);
				AddToHistory(new ChatMessageData(ChatChannel.Global, ChatMessageType.Player, (pPG_Player != null) ? pPG_Player.SteamID.Value : 0, ResolvePlayerChatZone(pPG_Player), ResolvePlayerDisplayName(pPG_Player), text, isLocal: true));
				_lastSendTime = Time.unscaledTime;
				return true;
			}
			if (_steamLobbyService == null)
			{
				error = "Chat is not initialized.";
				return false;
			}
			if (!HasActiveLobby)
			{
				error = "You are not in a lobby.";
				return false;
			}
			if (!_steamLobbyService.SendLobbyChatMessage(text))
			{
				error = "Steam lobby chat send failed.";
				UtilityMethods.Log("[GlobalChat] Send failed. " + error, LogType.Warning);
				return false;
			}
			_lastSendTime = Time.unscaledTime;
			return true;
		}

		public void AddSystemMessage(string message, RestaurantChatTone tone = RestaurantChatTone.Info)
		{
			AddLocalMessage(ChatMessageType.System, GetSenderName("chat.sender.system", "System"), message, _maxMessageLength, tone);
		}

		public void AddInfoMessage(string message)
		{
			AddLocalMessage(ChatMessageType.Info, GetSenderName("chat.sender.info", "Info"), message);
		}

		public void AddRestaurantMessage(string message, RestaurantChatTone tone = RestaurantChatTone.Info)
		{
			AddLocalMessage(ChatMessageType.Restaurant, GetSenderName("chat.sender.restaurant", "Restaurant"), message, _maxMessageLength, tone);
		}

		public void AddDevConsoleMessage(string message)
		{
			AddLocalMessage(ChatMessageType.Info, "DevConsole", message, _maxDevConsoleMessageLength);
		}

		public void AddLocalizedMessage(ChatMessageType type, string key, string fallback, RestaurantChatTone tone, params string[] arguments)
		{
			object[] arguments2 = ((arguments == null) ? Array.Empty<object>() : Array.ConvertAll(arguments, (Converter<string, object>)((string argument) => argument ?? string.Empty)));
			string formattedString = LocalizationDatabase.GetFormattedString("UiTable", key, fallback, arguments2);
			switch (type)
			{
			case ChatMessageType.System:
				AddSystemMessage(formattedString, tone);
				break;
			case ChatMessageType.Restaurant:
				AddRestaurantMessage(formattedString, tone);
				break;
			default:
				AddInfoMessage(formattedString);
				break;
			}
		}

		public void AddLocalizedMultiplayerFailure(MultiplayerFailure failure, bool preserveForNextLobby)
		{
			if (failure.HasFailure)
			{
				RestaurantChatTone restaurantChatTone = ResolveMultiplayerFailureTone(failure.Code);
				AddLocalizedMessage(ChatMessageType.System, failure.LocalizationKey, failure.UserMessage, restaurantChatTone);
				if (preserveForNextLobby && _activeLobbyId == 0L)
				{
					_hasPendingNextLobbyFailure = true;
					_pendingNextLobbyFailureKey = failure.LocalizationKey;
					_pendingNextLobbyFailureFallback = failure.UserMessage;
					_pendingNextLobbyFailureTone = restaurantChatTone;
				}
			}
		}

		public static RestaurantChatTone ResolveMultiplayerFailureTone(MultiplayerFailureCode code)
		{
			if (code - 1 <= MultiplayerFailureCode.LobbyJoinFailed || code - 9 <= MultiplayerFailureCode.ClientStartFailed)
			{
				return RestaurantChatTone.Warning;
			}
			return RestaurantChatTone.Error;
		}

		public static void BroadcastLocalizedInfoFromServer(NetworkBehaviour source, string key, string fallback, params string[] arguments)
		{
			BroadcastLocalizedFromServer(source, ChatMessageType.Info, key, fallback, RestaurantChatTone.Info, arguments);
		}

		public static void BroadcastLocalizedRestaurantFromServer(NetworkBehaviour source, string key, string fallback, RestaurantChatTone tone = RestaurantChatTone.Info, params string[] arguments)
		{
			BroadcastLocalizedFromServer(source, ChatMessageType.Restaurant, key, fallback, tone, arguments);
		}

		public static void BroadcastInfoFromServer(NetworkBehaviour source, string message)
		{
			if (source == null || !source.IsServer)
			{
				return;
			}
			int maxLength = ((Singleton<GlobalChatManager>.Instance != null) ? Singleton<GlobalChatManager>.Instance._maxMessageLength : 160);
			string text = NormalizeMessage(message, maxLength);
			if (string.IsNullOrWhiteSpace(text))
			{
				return;
			}
			try
			{
				ServiceLocator.ForSceneOf(source).Get<RpcRelay>(out var service);
				if (service != null)
				{
					service.ClientGlobalChatInfoRpc(text);
					return;
				}
			}
			catch
			{
				UtilityMethods.Log("[GlobalChat] Info broadcast failed. RpcRelay was not found.", LogType.Warning);
			}
			Singleton<GlobalChatManager>.Instance?.AddInfoMessage(text);
		}

		public static void BroadcastRestaurantFromServer(NetworkBehaviour source, string message, RestaurantChatTone tone = RestaurantChatTone.Info)
		{
			if (!TryPrepareServerMessage(source, message, out var safeMessage))
			{
				return;
			}
			try
			{
				ServiceLocator.ForSceneOf(source).Get<RpcRelay>(out var service);
				if (service != null)
				{
					service.ClientGlobalChatRestaurantRpc(safeMessage, (byte)tone);
					return;
				}
			}
			catch
			{
				UtilityMethods.Log("[GlobalChat] Restaurant broadcast failed. RpcRelay was not found.", LogType.Warning);
			}
			Singleton<GlobalChatManager>.Instance?.AddRestaurantMessage(safeMessage, tone);
		}

		public static void SendRestaurantToClientFromServer(NetworkBehaviour source, ulong clientId, string message, RestaurantChatTone tone = RestaurantChatTone.Info)
		{
			if (!TryPrepareServerMessage(source, message, out var safeMessage))
			{
				return;
			}
			try
			{
				ServiceLocator.ForSceneOf(source).Get<RpcRelay>(out var service);
				if (service != null)
				{
					service.ClientGlobalChatRestaurantTargetRpc(safeMessage, (byte)tone, service.RpcTarget.Single(clientId, RpcTargetUse.Temp));
					return;
				}
			}
			catch
			{
				UtilityMethods.Log("[GlobalChat] Targeted restaurant message failed. RpcRelay was not found.", LogType.Warning);
			}
			if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.LocalClientId == clientId)
			{
				Singleton<GlobalChatManager>.Instance?.AddRestaurantMessage(safeMessage, tone);
			}
		}

		public static void BroadcastPlayerTrappedFromServer(NetworkBehaviour source, PPG_Player player, string trapKey, string trapFallback)
		{
			if (!(source == null) && source.IsServer && !(player == null))
			{
				string text = ResolvePlayerDisplayName(player);
				string text2 = SanitizeText(trapFallback, 48);
				if (string.IsNullOrWhiteSpace(text2))
				{
					BroadcastLocalizedInfoFromServer(source, "chat.info.player_trapped", "{0} was trapped.", text);
				}
				else
				{
					BroadcastLocalizedFromServer(source, ChatMessageType.Info, "chat.info.player_trapped_by", "{0} was trapped by {1}.", RestaurantChatTone.Info, new string[2]
					{
						text,
						"@" + trapKey + "|" + text2
					});
				}
			}
		}

		private static void BroadcastLocalizedFromServer(NetworkBehaviour source, ChatMessageType type, string key, string fallback, RestaurantChatTone tone, string[] arguments)
		{
			if (source == null || !source.IsServer || string.IsNullOrWhiteSpace(key))
			{
				return;
			}
			string text = SanitizeText(key, 120);
			string text2 = SanitizeText(fallback, 480);
			string[] array = PrepareLocalizedArguments(arguments);
			try
			{
				ServiceLocator.ForSceneOf(source).Get<RpcRelay>(out var service);
				if (service != null)
				{
					service.ClientGlobalChatLocalizedRpc(text, text2, GetArgument(array, 0), GetArgument(array, 1), GetArgument(array, 2), GetArgument(array, 3), (byte)array.Length, (byte)type, (byte)tone);
					return;
				}
			}
			catch
			{
				UtilityMethods.Log("[GlobalChat] Localized broadcast failed. RpcRelay was not found.", LogType.Warning);
			}
			Singleton<GlobalChatManager>.Instance?.AddLocalizedMessage(type, text, text2, tone, ResolveLocalizedArguments(array));
		}

		public static string[] ResolveLocalizedArguments(string[] arguments)
		{
			if (arguments == null || arguments.Length == 0)
			{
				return Array.Empty<string>();
			}
			string[] array = new string[arguments.Length];
			for (int i = 0; i < arguments.Length; i++)
			{
				string text = arguments[i] ?? string.Empty;
				if (!text.StartsWith("@", StringComparison.Ordinal))
				{
					array[i] = text;
					continue;
				}
				int num = text.IndexOf('|');
				if (num <= 1)
				{
					array[i] = text;
					continue;
				}
				string text2 = text.Substring(1, num - 1);
				string fallback = text.Substring(num + 1);
				string tableName = (text2.StartsWith("item.", StringComparison.Ordinal) ? "ItemsTable" : "UiTable");
				array[i] = LocalizationDatabase.GetString(tableName, text2, fallback);
			}
			return array;
		}

		private static string[] PrepareLocalizedArguments(string[] arguments)
		{
			if (arguments == null || arguments.Length == 0)
			{
				return Array.Empty<string>();
			}
			int num = Mathf.Min(arguments.Length, 4);
			string[] array = new string[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = SanitizeText(arguments[i], 120);
			}
			return array;
		}

		private static string GetArgument(string[] arguments, int index)
		{
			if (index < 0 || index >= arguments.Length)
			{
				return string.Empty;
			}
			return arguments[index];
		}

		private static string GetSenderName(string key, string fallback)
		{
			return LocalizationDatabase.GetString("UiTable", key, fallback);
		}

		public void ClearSession()
		{
			_activeLobbyId = 0uL;
			_history.Clear();
			SessionCleared?.Invoke();
			UtilityMethods.Log("[GlobalChat] Session cleared.");
		}

		private bool TryHandleDevConsoleInput(string rawMessage, out string error)
		{
			error = string.Empty;
			if (!TryExtractDevConsoleCommand(rawMessage, out var commandInput))
			{
				return false;
			}
			if (!PaperPixelGames.DevConsole.DevConsole.IsEnabled)
			{
				error = "DevConsole is disabled.";
				AddDevConsoleMessage(error);
				return true;
			}
			if (PaperPixelGames.DevConsole.DevConsole.Instance == null)
			{
				error = "DevConsole is not available in this scene.";
				AddDevConsoleMessage(error);
				return true;
			}
			AddDevConsoleMessage("> " + commandInput);
			bool wroteFeedback = false;
			PaperPixelGames.DevConsole.DevConsole.Instance.TryExecuteCommand(commandInput, (string message) =>
			{
				if (!string.IsNullOrWhiteSpace(message))
				{
					wroteFeedback = true;
					AddDevConsoleMessage(message);
				}
			}, addToHistory: true);
			if (!wroteFeedback)
			{
				AddDevConsoleMessage("Command executed.");
			}
			return true;
		}

		private bool TryHandleRuntimeDebugToolsUnlock(string rawMessage, out string error)
		{
			error = string.Empty;
			if (string.IsNullOrWhiteSpace(rawMessage))
			{
				return false;
			}
			if (!string.Equals(rawMessage.Trim(), "irmikhelvasi", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			if (Singleton<GameController>.Instance == null)
			{
				error = "GameController is not available.";
				AddDevConsoleMessage(error);
				return true;
			}
			Singleton<GameController>.Instance.SetRuntimeDebugToolsEnabled(enabled: true);
			if (!Singleton<GameController>.Instance.RuntimeDebugToolsEnabled)
			{
				error = "Runtime debug tools are unavailable in this build.";
				AddDevConsoleMessage(error);
				return true;
			}
			AddDevConsoleMessage("Runtime debug tools enabled.");
			return true;
		}

		private static bool TryExtractDevConsoleCommand(string rawMessage, out string commandInput)
		{
			commandInput = string.Empty;
			if (string.IsNullOrWhiteSpace(rawMessage))
			{
				return false;
			}
			string text = rawMessage.Trim();
			if (!text.StartsWith("/", StringComparison.Ordinal))
			{
				return false;
			}
			commandInput = text.Substring(1).Trim();
			return !string.IsNullOrWhiteSpace(commandInput);
		}

		private void AddLocalMessage(ChatMessageType type, string senderName, string message)
		{
			AddLocalMessage(type, senderName, message, _maxMessageLength);
		}

		private void AddLocalMessage(ChatMessageType type, string senderName, string message, int maxLength, RestaurantChatTone restaurantTone = RestaurantChatTone.Info)
		{
			string text = NormalizeMessage(message, maxLength);
			if (!string.IsNullOrWhiteSpace(text))
			{
				ChatMessageData data = new ChatMessageData(ChatChannel.Global, type, 0uL, string.Empty, senderName, text, isLocal: true, restaurantTone);
				AddToHistory(data);
			}
		}

		private static bool TryPrepareServerMessage(NetworkBehaviour source, string message, out string safeMessage)
		{
			safeMessage = string.Empty;
			if (source == null || !source.IsServer)
			{
				return false;
			}
			int maxLength = ((Singleton<GlobalChatManager>.Instance != null) ? Singleton<GlobalChatManager>.Instance._maxMessageLength : 160);
			safeMessage = NormalizeMessage(message, maxLength);
			return !string.IsNullOrWhiteSpace(safeMessage);
		}

		private void OnLobbyEntered(Lobby lobby)
		{
			if ((ulong)lobby.Id != 0L)
			{
				ulong value = lobby.Id.Value;
				if (_activeLobbyId != value)
				{
					_activeLobbyId = value;
					_history.Clear();
					SessionCleared?.Invoke();
				}
				AddLocalizedMessage(ChatMessageType.System, "chat.system.connected", "Connected to lobby chat.", RestaurantChatTone.Info);
				if (_hasPendingNextLobbyFailure)
				{
					string pendingNextLobbyFailureKey = _pendingNextLobbyFailureKey;
					string pendingNextLobbyFailureFallback = _pendingNextLobbyFailureFallback;
					RestaurantChatTone pendingNextLobbyFailureTone = _pendingNextLobbyFailureTone;
					_hasPendingNextLobbyFailure = false;
					_pendingNextLobbyFailureKey = string.Empty;
					_pendingNextLobbyFailureFallback = string.Empty;
					_pendingNextLobbyFailureTone = RestaurantChatTone.Info;
					AddLocalizedMessage(ChatMessageType.System, pendingNextLobbyFailureKey, pendingNextLobbyFailureFallback, pendingNextLobbyFailureTone);
				}
				UtilityMethods.Log("[GlobalChat] Lobby entered" + $"\n   Lobby: {lobby.Id}" + $"\n   Members: {lobby.MemberCount}");
			}
		}

		private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
		{
			if ((ulong)lobby.Id != 0L && (ulong)friend.Id != (ulong)SteamClient.SteamId)
			{
				AddLocalizedMessage(ChatMessageType.System, "chat.system.player_joined", "{0} joined the lobby.", RestaurantChatTone.Info, SanitizeText(friend.Name, 32));
			}
		}

		private void OnLobbyMemberLeft(Lobby lobby, Friend friend)
		{
			if ((ulong)lobby.Id != 0L && (ulong)friend.Id != (ulong)SteamClient.SteamId)
			{
				AddLocalizedMessage(ChatMessageType.System, "chat.system.player_left", "{0} left the lobby.", RestaurantChatTone.Info, SanitizeText(friend.Name, 32));
			}
		}

		private void OnLobbyChatMessageReceived(Lobby lobby, Friend friend, string rawMessage)
		{
			if ((ulong)lobby.Id != 0L && (_activeLobbyId == 0L || lobby.Id.Value == _activeLobbyId))
			{
				Singleton<PlayersManager>.Instance.TryGetPlayerBySteamId(friend.Id, out var player);
				string zone = ResolvePlayerChatZone(player);
				_activeLobbyId = lobby.Id.Value;
				string text = SanitizeText(friend.Name, 32);
				string text2 = NormalizeMessage(rawMessage, _maxMessageLength);
				if (!string.IsNullOrWhiteSpace(text2))
				{
					ChatMessageData data = new ChatMessageData(ChatChannel.Global, ChatMessageType.Player, friend.Id.Value, zone, string.IsNullOrWhiteSpace(text) ? "Player" : text, text2, (ulong)friend.Id == (ulong)SteamClient.SteamId);
					AddToHistory(data);
				}
			}
		}

		private void OnLobbyHostRecommendationReceived(LobbyHostRecommendation recommendation)
		{
			if (_steamLobbyService == null || _steamLobbyService.CurrentLobby.Id.Value != recommendation.LobbyId)
			{
				return;
			}
			string text = "Player";
			foreach (Friend member in _steamLobbyService.CurrentLobby.Members)
			{
				if ((ulong)member.Id == (ulong)recommendation.RecommendedHostId)
				{
					text = SanitizeText(member.Name, 32);
					break;
				}
			}
			AddLocalizedMessage(ChatMessageType.System, "chat.system.host_recommendation", "Best host recommendation: {0} (worst ping {1} ms, average ping {2} ms, stable FPS {3}).", RestaurantChatTone.Info, text, recommendation.WorstPingMs.ToString(), recommendation.AveragePingMs.ToString(), recommendation.StableFps.ToString());
		}

		private void AddToHistory(ChatMessageData data)
		{
			_history.Add(data);
			if (_maxHistoryCount > 0)
			{
				while (_history.Count > _maxHistoryCount)
				{
					_history.RemoveAt(0);
				}
			}
			else
			{
				_history.Clear();
			}
			MessageReceived?.Invoke(data);
			if (_logMessages)
			{
				UtilityMethods.Log("[GlobalChat] Message received" + $"\n   Type: {data.Type}" + "\n   Sender: " + data.SenderName + $"\n   IsLocal: {data.IsLocal}" + "\n   Message: " + data.Message);
			}
		}

		private static string ResolvePlayerChatZone(PPG_Player player)
		{
			if (player == null)
			{
				return string.Empty;
			}
			StringBuilder stringBuilder = new StringBuilder();
			if (player.IsSwimming.Value)
			{
				stringBuilder.Append("OCEAN");
			}
			if (player.IsTrapped)
			{
				if (stringBuilder.Length > 0)
				{
					stringBuilder.Append("] [");
				}
				stringBuilder.Append("TRAPPED");
			}
			if (stringBuilder.Length > 0)
			{
				return stringBuilder.ToString();
			}
			return player.Zone;
		}

		private static string ResolvePlayerDisplayName(PPG_Player player)
		{
			if (player == null)
			{
				return "Player";
			}
			string value = player.PlayerName.Value.ToString();
			if (!string.IsNullOrWhiteSpace(value))
			{
				return SanitizeText(value, 32);
			}
			return "Player";
		}

		private static string NormalizeMessage(string value, int maxLength)
		{
			return SanitizeText(value, maxLength).Trim();
		}

		public static string SanitizeText(string value, int maxLength)
		{
			if (string.IsNullOrEmpty(value))
			{
				return string.Empty;
			}
			string text = value.Trim();
			if (maxLength > 0 && text.Length > maxLength)
			{
				text = text.Substring(0, maxLength);
			}
			StringBuilder stringBuilder = null;
			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];
				bool flag = char.IsControl(c) && c != '\n';
				char c2 = c;
				if (!flag)
				{
					switch (c)
					{
					case '<':
						c2 = '‹';
						break;
					case '>':
						c2 = '›';
						break;
					}
				}
				if (stringBuilder == null)
				{
					if (!flag && c2 == c)
					{
						continue;
					}
					stringBuilder = new StringBuilder(text.Length);
					if (i > 0)
					{
						stringBuilder.Append(text, 0, i);
					}
				}
				if (!flag)
				{
					stringBuilder.Append(c2);
				}
			}
			if (stringBuilder != null)
			{
				return stringBuilder.ToString();
			}
			return text;
		}
	}
}
