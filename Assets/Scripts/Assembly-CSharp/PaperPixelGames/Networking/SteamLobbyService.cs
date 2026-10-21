using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Netcode.Transports.Facepunch;
using Newtonsoft.Json;
using PaperPixelGames.Core;
using PaperPixelGames.Framework.Utility;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace PaperPixelGames.Networking
{
	public class SteamLobbyService : MonoBehaviour, ISteamLobbyGateway
	{
		public FacepunchTransport FacepunchTransport;

		public readonly List<Lobby> Lobbies = new List<Lobby>();

		private bool _initialized;

		private bool _initializedSuccessfully;

		private LobbyHostRecommendationService _hostRecommendationService;

		private bool _lobbyCreationInProgress;

		private readonly SemaphoreSlim _joinGate = new SemaphoreSlim(1, 1);

		private readonly SemaphoreSlim _listGate = new SemaphoreSlim(1, 1);

		private const string LobbyMenuDataKey = "lobby_menu_data";

		private const string ReleaseVersionMemberDataKey = "release_version";

		private const string InternalMessagePrefix = "__PPG_INTERNAL__";

		private const int DefaultOperationTimeoutSeconds = 15;

		private readonly JsonSerializerSettings _lobbyMenuJsonSettings = new JsonSerializerSettings();

		public bool IsFriendsOnly { get; private set; }

		public LobbyRoomService RoomService { get; internal set; }

		public Lobby CurrentLobby { get; private set; }

		public bool IsInitializedSuccessfully => _initializedSuccessfully;

		public bool IsSteamAvailable
		{
			get
			{
				if (SteamClient.IsValid && SteamClient.IsLoggedOn && _initializedSuccessfully && FacepunchTransport != null)
				{
					return FacepunchTransport.IsInitialized;
				}
				return false;
			}
		}

		public event Action<Lobby> LobbyCreated;

		public event Action<Lobby> LobbyEntered;

		public event Action<Lobby, Friend> LobbyMemberJoined;

		public event Action<Lobby, Friend> LobbyMemberLeft;

		public event Action<Friend, Lobby> LobbyInvite;

		public event Action<Lobby, SteamId> GameLobbyJoinRequested;

		public event Action<List<Lobby>> LobbyListUpdated;

		public event Action<Lobby> LobbyDataChanged;

		public event Action<Lobby, Friend> LobbyMemberDataChanged;

		public event Action<Lobby, Friend, string> LobbyChatMessageReceived;

		public event Action<Lobby, Friend, string> LobbyInternalMessageReceived;

		public event Action<LobbyHostRecommendation> LobbyHostRecommendationReceived;

		public void Initialize()
		{
			if (_initialized)
			{
				return;
			}
			if (!SteamClient.IsValid)
			{
				UtilityMethods.Log("[SteamLobbyService] Initialize blocked. Steam client is not valid.", LogType.Error);
				return;
			}
			if (FacepunchTransport == null)
			{
				FacepunchTransport = UnityEngine.Object.FindFirstObjectByType<FacepunchTransport>();
			}
			_initialized = true;
			_initializedSuccessfully = true;
			SteamMatchmaking.OnLobbyCreated += OnLobbyCreatedInternal;
			SteamMatchmaking.OnLobbyEntered += OnLobbyEnteredInternal;
			SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoinedInternal;
			SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeftInternal;
			SteamMatchmaking.OnLobbyInvite += OnLobbyInviteInternal;
			SteamMatchmaking.OnLobbyDataChanged += OnLobbyDataChanged;
			SteamMatchmaking.OnLobbyMemberDataChanged += OnLobbyMemberDataChanged;
			SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequestedInternal;
			SteamMatchmaking.OnChatMessage += OnLobbyChatMessageInternal;
			_hostRecommendationService = GetComponent<LobbyHostRecommendationService>();
			_hostRecommendationService?.Initialize(this);
			Debug.Log("[SteamLobbyService] Initialized.");
		}

		private void OnDestroy()
		{
			_hostRecommendationService?.Shutdown();
			SteamMatchmaking.OnLobbyCreated -= OnLobbyCreatedInternal;
			SteamMatchmaking.OnLobbyEntered -= OnLobbyEnteredInternal;
			SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoinedInternal;
			SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeftInternal;
			SteamMatchmaking.OnLobbyInvite -= OnLobbyInviteInternal;
			SteamMatchmaking.OnLobbyDataChanged -= OnLobbyDataChanged;
			SteamMatchmaking.OnLobbyMemberDataChanged -= OnLobbyMemberDataChanged;
			SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequestedInternal;
			SteamMatchmaking.OnChatMessage -= OnLobbyChatMessageInternal;
		}

		public async Task<LobbyCreationOutcome> CreateLobbyAsync(int maxMembers, bool friendsOnly)
		{
			return await CreateLobbyAsync(maxMembers, friendsOnly, CancellationToken.None, 15);
		}

		public async Task<LobbyCreationOutcome> CreateLobbyAsync(int maxMembers, bool friendsOnly, CancellationToken cancellationToken, int timeoutSeconds)
		{
			if (!SteamClient.IsValid || !SteamClient.IsLoggedOn)
			{
				UtilityMethods.Log("[SteamLobbyService] CreateLobbyAsync blocked. Steam backend is unavailable." + $"\n   SteamClientValid: {SteamClient.IsValid}" + $"\n   SteamLoggedOn: {SteamClient.IsValid && SteamClient.IsLoggedOn}", LogType.Warning);
				return new LobbyCreationOutcome(null, Result.NoConnection);
			}
			if (_lobbyCreationInProgress)
			{
				throw new InvalidOperationException("A Steam lobby creation request is already in progress.");
			}
			if ((ulong)CurrentLobby.Id != 0L)
			{
				LeaveCurrentLobby();
			}
			IsFriendsOnly = friendsOnly;
			Task<Lobby?> createLobbyTask = null;
			bool adopted = false;
			_lobbyCreationInProgress = true;
			try
			{
				int steamDelayMilliseconds = MultiplayerFaultInjection.SteamDelayMilliseconds;
				if (steamDelayMilliseconds > 0)
				{
					await Task.Delay(steamDelayMilliseconds, cancellationToken);
				}
				createLobbyTask = SteamMatchmaking.CreateLobbyAsync(maxMembers);
				Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)), cancellationToken);
				if (MultiplayerFaultInjection.ConsumeDroppedSteamCompletion())
				{
					await timeoutTask;
					cancellationToken.ThrowIfCancellationRequested();
					return new LobbyCreationOutcome(null, Result.Timeout);
				}
				Task obj = await Task.WhenAny(createLobbyTask, timeoutTask);
				cancellationToken.ThrowIfCancellationRequested();
				LobbyCreationOutcome result;
				if (obj == timeoutTask)
				{
					result = new LobbyCreationOutcome(null, Result.Timeout);
				}
				else
				{
					Lobby? lobby = await createLobbyTask;
					result = (lobby.HasValue ? new LobbyCreationOutcome(lobby, Result.OK) : new LobbyCreationOutcome(null, Result.Fail));
				}
				if (result.Succeeded)
				{
					Lobby value = result.Lobby.Value;
					AdoptLobby(value);
					adopted = true;
					ConfigureCreatedLobby(value, friendsOnly);
					InvokeLobbyEventSafely(LobbyCreated, value, "LobbyCreated");
				}
				return result;
			}
			finally
			{
				if (!adopted && createLobbyTask != null)
				{
					LeaveAbandonedCreatedLobbyAsync(createLobbyTask);
				}
				_lobbyCreationInProgress = false;
			}
		}

		public async Task<LobbyJoinOutcome> JoinLobbyAsync(Lobby targetLobby, CancellationToken cancellationToken, int timeoutSeconds = 15)
		{
			if (!IsSteamAvailable || (ulong)targetLobby.Id == 0L)
			{
				return new LobbyJoinOutcome(null, RoomEnter.Error);
			}
			int steamDelayMilliseconds = MultiplayerFaultInjection.SteamDelayMilliseconds;
			if (steamDelayMilliseconds > 0)
			{
				await Task.Delay(steamDelayMilliseconds, cancellationToken);
			}
			if (_joinGate.CurrentCount == 0)
			{
				MultiplayerDiagnostics.Record("lobby_join_drain_wait", $"lobby={targetLobby.Id}", ulong.MaxValue);
			}
			if (!(await _joinGate.WaitAsync(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)), cancellationToken)))
			{
				return new LobbyJoinOutcome(null, RoomEnter.Error, timedOut: true);
			}
			Task<RoomEnter> joinTask = null;
			bool adopted = false;
			try
			{
				cancellationToken.ThrowIfCancellationRequested();
				joinTask = targetLobby.Join();
				Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)), cancellationToken);
				if (MultiplayerFaultInjection.ConsumeDroppedSteamCompletion())
				{
					await timeoutTask;
					cancellationToken.ThrowIfCancellationRequested();
					return new LobbyJoinOutcome(null, RoomEnter.Error, timedOut: true);
				}
				Task obj = await Task.WhenAny(joinTask, timeoutTask);
				cancellationToken.ThrowIfCancellationRequested();
				if (obj == timeoutTask)
				{
					return new LobbyJoinOutcome(null, RoomEnter.Error, timedOut: true);
				}
				RoomEnter enter = await joinTask;
				int retry = 0;
				while (enter == RoomEnter.NotAllowed && retry < 2 && RoomService != null && RoomService.HasPrivateAdmissionFor(targetLobby))
				{
					await Task.Delay((retry == 0) ? 200 : 500, cancellationToken);
					joinTask = targetLobby.Join();
					Task obj2 = await Task.WhenAny(joinTask, timeoutTask);
					cancellationToken.ThrowIfCancellationRequested();
					if (obj2 == timeoutTask)
					{
						return new LobbyJoinOutcome(null, RoomEnter.Error, timedOut: true);
					}
					enter = await joinTask;
					retry++;
				}
				if (enter != RoomEnter.Success)
				{
					return new LobbyJoinOutcome(null, enter);
				}
				AdoptLobby(targetLobby);
				bool flag = RoomService != null;
				if (flag)
				{
					flag = !(await RoomService.AuthenticateJoinedLobbyAsync(targetLobby, cancellationToken));
				}
				if (flag)
				{
					LeaveCurrentLobby();
					return new LobbyJoinOutcome(null, RoomEnter.NotAllowed);
				}
				cancellationToken.ThrowIfCancellationRequested();
				adopted = true;
				PublishLocalReleaseVersion(targetLobby);
				InvokeLobbyEventSafely(LobbyEntered, targetLobby, "LobbyEntered");
				return new LobbyJoinOutcome(targetLobby, enter);
			}
			finally
			{
				if (!adopted && (ulong)CurrentLobby.Id == (ulong)targetLobby.Id)
				{
					LeaveCurrentLobby();
				}
				if (!adopted && joinTask != null)
				{
					LeaveAbandonedJoinedLobbyAsync(targetLobby, joinTask);
				}
				else
				{
					_joinGate.Release();
				}
			}
		}

		private async void LeaveAbandonedCreatedLobbyAsync(Task<Lobby?> createTask)
		{
			try
			{
				Lobby? lobby = await createTask;
				if (lobby.HasValue && ((ulong)CurrentLobby.Id == 0L || (ulong)CurrentLobby.Id != (ulong)lobby.Value.Id))
				{
					lobby.Value.Leave();
				}
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[SteamLobbyService] Abandoned create cleanup completed with an error: " + ex.Message, LogType.Warning);
			}
		}

		private static void InvokeLobbyEventSafely(Action<Lobby> lobbyEvent, Lobby lobby, string eventName)
		{
			if (lobbyEvent == null)
			{
				return;
			}
			Delegate[] invocationList = lobbyEvent.GetInvocationList();
			foreach (Delegate obj in invocationList)
			{
				try
				{
					((Action<Lobby>)obj)(lobby);
				}
				catch (Exception arg)
				{
					UtilityMethods.Log("[SteamLobbyService] " + eventName + " subscriber failed without invalidating the Steam operation." + $"\n   LobbyId: {lobby.Id}" + "\n   Subscriber: " + obj.Method.DeclaringType?.FullName + "." + obj.Method.Name + $"\n   Exception: {arg}", LogType.Error);
				}
			}
		}

		private async void LeaveAbandonedJoinedLobbyAsync(Lobby targetLobby, Task<RoomEnter> joinTask)
		{
			try
			{
				if (await joinTask == RoomEnter.Success && ((ulong)CurrentLobby.Id == 0L || (ulong)CurrentLobby.Id != (ulong)targetLobby.Id))
				{
					targetLobby.Leave();
				}
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[SteamLobbyService] Abandoned join cleanup completed with an error: " + ex.Message, LogType.Warning);
			}
			finally
			{
				_joinGate.Release();
				MultiplayerDiagnostics.Record("lobby_join_drain_complete", $"lobby={targetLobby.Id}", ulong.MaxValue);
			}
		}

		public void LeaveCurrentLobby()
		{
			if ((ulong)CurrentLobby.Id != 0L)
			{
				CurrentLobby.Leave();
			}
			CurrentLobby = default;
			RoomService?.OnLobbyLeft();
		}

		public bool SendLobbyChatMessage(string message)
		{
			if (RoomService != null && !RoomService.IsLocalAdmitted)
			{
				return false;
			}
			if (!SteamClient.IsValid)
			{
				UtilityMethods.Log("[SteamLobbyService] Cannot send chat message. Steam client is not valid.", LogType.Warning);
				return false;
			}
			if ((ulong)CurrentLobby.Id == 0L)
			{
				UtilityMethods.Log("[SteamLobbyService] Cannot send chat message. CurrentLobby is invalid.", LogType.Warning);
				return false;
			}
			if (string.IsNullOrWhiteSpace(message))
			{
				return false;
			}
			bool flag = CurrentLobby.SendChatString(message);
			UtilityMethods.Log("[SteamLobbyService] SendLobbyChatMessage" + $"\n   Lobby: {CurrentLobby.Id}" + $"\n   Sent: {flag}" + $"\n   MessageLength: {message.Length}");
			return flag;
		}

		private void AdoptLobby(Lobby lobby)
		{
			CurrentLobby = lobby;
		}

		private void ConfigureCreatedLobby(Lobby lobby, bool friendsOnly)
		{
			lobby.SetData("name", SteamClient.Name + "'s Lobby");
			if (friendsOnly)
			{
				lobby.SetFriendsOnly();
			}
			else
			{
				lobby.SetPublic();
			}
			PublishLobbyMenuData(lobby);
			PublishLocalReleaseVersion(lobby);
		}

		internal void SetFriendsOnlyFlag(bool value)
		{
			IsFriendsOnly = value;
		}

		public bool SetLobbyName(string name)
		{
			if (!SteamClient.IsValid || (ulong)CurrentLobby.Id == 0L || (ulong)CurrentLobby.Owner.Id != (ulong)SteamClient.SteamId)
			{
				return false;
			}
			string text = LobbyRoomValidation.CleanName(name);
			if (string.IsNullOrEmpty(text) || !CurrentLobby.SetData("name", text))
			{
				return false;
			}
			SetLobbyMenuData(CurrentLobby, new LobbyMenuData("")
			{
				LobbyName = text
			});
			return true;
		}

		public bool SendLobbyInternalMessage(string payload)
		{
			if (!SteamClient.IsValid || (ulong)CurrentLobby.Id == 0L || string.IsNullOrEmpty(payload))
			{
				return false;
			}
			return CurrentLobby.SendChatString("__PPG_INTERNAL__" + payload);
		}

		public bool PublishLocalReleaseVersion()
		{
			return PublishLocalReleaseVersion(CurrentLobby);
		}

		public bool TryGetMemberReleaseVersion(Lobby lobby, Friend member, out string releaseVersion)
		{
			releaseVersion = string.Empty;
			if ((ulong)lobby.Id == 0L || member.Id.Value == 0L)
			{
				return false;
			}
			string memberData = lobby.GetMemberData(member, "release_version");
			if (string.IsNullOrWhiteSpace(memberData))
			{
				return false;
			}
			releaseVersion = memberData.Trim();
			return true;
		}

		internal void NotifyLobbyHostRecommendationReceived(LobbyHostRecommendation recommendation)
		{
			LobbyHostRecommendationReceived?.Invoke(recommendation);
		}

		public async Task<bool> RefreshLobbiesAsync(int maxResults = 20)
		{
			if (!SteamClient.IsValid)
			{
				UtilityMethods.Log("[SteamLobbyService] RefreshLobbiesAsync blocked. Steam client is not valid.", LogType.Warning);
				return false;
			}
			if (!(await _listGate.WaitAsync(0)))
			{
				return false;
			}
			try
			{
				Lobby[] array = await SteamMatchmaking.LobbyList.FilterDistanceWorldwide().WithSlotsAvailable(1).WithMaxResults(maxResults)
					.RequestAsync();
				Lobbies.Clear();
				if (array != null)
				{
					Lobbies.AddRange(array);
				}
				LobbyListUpdated?.Invoke(Lobbies);
				return true;
			}
			catch (Exception exception)
			{
				Debug.LogException(exception);
				return false;
			}
			finally
			{
				_listGate.Release();
			}
		}

		private void OnLobbyCreatedInternal(Result result, Lobby lobby)
		{
			if (result != Result.OK)
			{
				UtilityMethods.Log("[SteamLobbyService] Lobby creation request failed." + $"\n   Result: {result}" + $"\n   AppId: {SteamClient.AppId}" + $"\n   SteamLoggedOn: {SteamClient.IsValid && SteamClient.IsLoggedOn}", LogType.Warning);
			}
		}

		private void OnLobbyEnteredInternal(Lobby lobby)
		{
			UtilityMethods.Log("[SteamLobbyService] OnLobbyEntered");
		}

		private void OnLobbyMemberJoinedInternal(Lobby lobby, Friend friend)
		{
			UtilityMethods.Log($"[SteamLobbyService] OnLobbyMemberJoined {friend.Id}");
			PublishLobbyMenuData(lobby);
			LobbyMemberJoined?.Invoke(lobby, friend);
		}

		private void OnLobbyMemberLeftInternal(Lobby lobby, Friend friend)
		{
			UtilityMethods.Log($"[SteamLobbyService] OnLobbyMemberLeft {friend.Id}");
			PublishLobbyMenuData(lobby);
			LobbyMemberLeft?.Invoke(lobby, friend);
		}

		private void OnLobbyInviteInternal(Friend friend, Lobby lobby)
		{
			UtilityMethods.Log("[SteamLobbyService] OnLobbyInvite from=" + friend.Name);
			LobbyInvite?.Invoke(friend, lobby);
		}

		private void OnGameLobbyJoinRequestedInternal(Lobby lobby, SteamId id)
		{
			UtilityMethods.Log($"[SteamLobbyService] OnGameLobbyJoinRequested id={id}");
			GameLobbyJoinRequested?.Invoke(lobby, id);
		}

		private void OnLobbyDataChanged(Lobby lobby)
		{
			if ((ulong)lobby.Id == (ulong)CurrentLobby.Id)
			{
				LobbyDataChanged?.Invoke(lobby);
			}
		}

		private void OnLobbyMemberDataChanged(Lobby lobby, Friend member)
		{
			if ((ulong)lobby.Id != 0L && (ulong)lobby.Id == (ulong)CurrentLobby.Id)
			{
				LobbyMemberDataChanged?.Invoke(lobby, member);
			}
		}

		private void OnLobbyChatMessageInternal(Lobby lobby, Friend friend, string message)
		{
			if ((ulong)CurrentLobby.Id == 0L || (ulong)lobby.Id != (ulong)CurrentLobby.Id || (RoomService != null && (ulong)friend.Id != (ulong)lobby.Owner.Id && !RoomService.IsMemberAdmitted(friend.Id.Value)) || string.IsNullOrWhiteSpace(message))
			{
				return;
			}
			if (message.StartsWith("__PPG_INTERNAL__", StringComparison.Ordinal))
			{
				string text = message.Substring("__PPG_INTERNAL__".Length);
				if (!string.IsNullOrEmpty(text))
				{
					LobbyInternalMessageReceived?.Invoke(lobby, friend, text);
				}
			}
			else
			{
				UtilityMethods.Log("[SteamLobbyService] OnLobbyChatMessage" + $"\n   Lobby: {lobby.Id}" + $"\n   Sender: {friend.Name} ({friend.Id})" + $"\n   MessageLength: {message.Length}");
				LobbyChatMessageReceived?.Invoke(lobby, friend, message);
			}
		}

		private string SerializeLobbyMenuData(LobbyMenuData data)
		{
			return JsonConvert.SerializeObject(data, _lobbyMenuJsonSettings);
		}

		private bool TryDeserializeLobbyMenuData(string json, out LobbyMenuData data)
		{
			data = null;
			if (string.IsNullOrEmpty(json))
			{
				return false;
			}
			try
			{
				data = JsonConvert.DeserializeObject<LobbyMenuData>(json, _lobbyMenuJsonSettings);
				return data != null;
			}
			catch
			{
				return false;
			}
		}

		private void SetLobbyMenuData(Lobby lobby, LobbyMenuData data)
		{
			if ((ulong)lobby.Id != 0L && (ulong)lobby.Owner.Id == (ulong)SteamClient.SteamId)
			{
				lobby.SetData("lobby_menu_data", SerializeLobbyMenuData(data));
			}
		}

		public bool TryGetLobbyMenuData(Lobby lobby, out LobbyMenuData data)
		{
			data = null;
			if ((ulong)lobby.Id == 0L)
			{
				return false;
			}
			string data2 = lobby.GetData("lobby_menu_data");
			return TryDeserializeLobbyMenuData(data2, out data);
		}

		private void PublishLobbyMenuData(Lobby lobby)
		{
			if ((ulong)lobby.Id != 0L && (ulong)lobby.Owner.Id == (ulong)SteamClient.SteamId)
			{
				string data = lobby.GetData("name");
				SetLobbyMenuData(lobby, string.IsNullOrEmpty(data) ? new LobbyMenuData(SteamClient.Name) : new LobbyMenuData("")
				{
					LobbyName = data
				});
			}
		}

		private bool PublishLocalReleaseVersion(Lobby lobby)
		{
			if (!SteamClient.IsValid || (ulong)lobby.Id == 0L)
			{
				return false;
			}
			string value = ReleaseVersion.Value;
			if (string.IsNullOrWhiteSpace(value))
			{
				UtilityMethods.Log("[SteamLobbyService] Local release version was not published because Application.version is empty.", LogType.Warning);
				return false;
			}
			lobby.SetMemberData("release_version", value);
			UtilityMethods.Log("[SteamLobbyService] Published local release version" + $"\n   Lobby: {lobby.Id}" + $"\n   SteamId: {SteamClient.SteamId}" + "\n   Version: " + value);
			return true;
		}
	}
}
