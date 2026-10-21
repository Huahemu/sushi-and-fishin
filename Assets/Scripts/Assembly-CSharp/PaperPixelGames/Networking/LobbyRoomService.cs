using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PaperPixelGames.Core;
using PaperPixelGames.Core.Localization;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace PaperPixelGames.Networking
{
	[DisallowMultipleComponent]
	public sealed class LobbyRoomService : MonoBehaviour
	{
		private sealed class PendingRequest
		{
			public ulong LobbyId;

			public ulong OwnerId;

			public TaskCompletionSource<LobbyRoomResult> Completion;
		}

		[Serializable]
		private sealed class Packet
		{
			public string protocol = "sushi-room-1";

			public string kind;

			public string request;

			public ulong lobby;

			public string password;

			public bool success;

			public string message;
		}

		public const string ProtocolKey = "room_protocol";

		public const string VisibilityKey = "room_visibility";

		public const string PasswordKey = "room_password_required";

		public const string LockedKey = "room_locked";

		public const string ReadyRequiredKey = "room_ready_required";

		public const string AdmittedKey = "room_admitted";

		public const string ReadyMemberKey = "room_ready";

		public const string OwnerKey = "room_owner";

		public const string OwnerNameKey = "room_owner_name";

		public const string ReadyRevisionKey = "room_ready_revision";

		public const string ReadyRevisionMemberKey = "room_ready_revision_ack";

		private const int Channel = 73;

		private const int MaxPacketBytes = 2048;

		private const string Protocol = "sushi-room-1";

		private SteamLobbyService _steam;

		private MultiplayerCoordinator _coordinator;

		private readonly LobbyAdmissionPolicy _admission = new LobbyAdmissionPolicy();

		private readonly List<LobbyBrowserEntry> _rooms = new List<LobbyBrowserEntry>();

		private readonly List<LobbyBannedMember> _bans = new List<LobbyBannedMember>();

		private readonly Dictionary<ulong, double> _pendingAdmissionExpiry = new Dictionary<ulong, double>();

		private readonly Dictionary<ulong, double> _memberArrivals = new Dictionary<ulong, double>();

		private readonly Dictionary<ulong, double> _packetRateLimit = new Dictionary<ulong, double>();

		private readonly Dictionary<string, PendingRequest> _requests = new Dictionary<string, PendingRequest>();

		private readonly HashSet<ulong> _peers = new HashSet<ulong>();

		private bool _initialized;

		private ulong _observedLobby;

		private ulong _observedOwner;

		private ulong _authenticatedLobby;

		private ulong _authenticatedOwner;

		private ulong _joinedAdmissionLobby;

		private ulong _joinedAdmissionOwner;

		private double _nextRefresh;

		private bool _refreshing;

		private LobbyRoomSettings _desiredSettings;

		private CancellationTokenSource _joinCancellation;

		private ulong _loadedBanOwner;

		private readonly Dictionary<ulong, double> _peerLastSeen = new Dictionary<ulong, double>();

		private readonly byte[] _discardBuffer = new byte[1048576];

		public LobbyRoomSnapshot Snapshot { get; private set; } = new LobbyRoomSnapshot();

		public IReadOnlyList<LobbyBrowserEntry> Rooms => _rooms;

		public bool IsBusy { get; private set; }

		public string LastAdmissionError { get; private set; } = "";

		public bool IsLocalAdmitted => Snapshot.IsAdmitted;

		public bool CanLaunch
		{
			get
			{
				RefreshSnapshot();
				if (HasRoom)
				{
					if (Snapshot.IsOwner)
					{
						if (Snapshot.RequireReady)
						{
							return Snapshot.AllReady;
						}
						return true;
					}
					return false;
				}
				return true;
			}
		}

		public bool HasRoom
		{
			get
			{
				if (_steam != null)
				{
					return (ulong)_steam.CurrentLobby.Id != 0;
				}
				return false;
			}
		}

		public IReadOnlyCollection<ulong> BannedIds => _admission.BannedIds;

		private string BanPreferenceKey => "sushi.lobby.bans." + (SteamClient.IsValid ? SteamClient.SteamId.Value : 0);

		public event Action Changed;

		public event Action<string> Notice;

		public event Action<ulong> PasswordRequested;

		public void Initialize(SteamLobbyService steam, MultiplayerCoordinator coordinator)
		{
			if (!_initialized)
			{
				_steam = steam;
				_coordinator = coordinator;
				_initialized = true;
				_steam.LobbyCreated += OnCreated;
				_steam.LobbyEntered += OnEntered;
				_steam.LobbyMemberJoined += OnMemberJoined;
				_steam.LobbyMemberLeft += OnMemberLeft;
				_steam.LobbyDataChanged += OnDataChanged;
				_steam.LobbyMemberDataChanged += OnMemberDataChanged;
				SteamNetworking.OnP2PSessionRequest = (Action<SteamId>)Delegate.Combine(SteamNetworking.OnP2PSessionRequest, new Action<SteamId>(OnPeerRequest));
				LoadBans();
				RefreshSnapshot();
			}
		}

		private void OnDestroy()
		{
			if (!_initialized)
			{
				return;
			}
			SteamNetworking.OnP2PSessionRequest = (Action<SteamId>)Delegate.Remove(SteamNetworking.OnP2PSessionRequest, new Action<SteamId>(OnPeerRequest));
			if (_steam != null)
			{
				_steam.LobbyCreated -= OnCreated;
				_steam.LobbyEntered -= OnEntered;
				_steam.LobbyMemberJoined -= OnMemberJoined;
				_steam.LobbyMemberLeft -= OnMemberLeft;
				_steam.LobbyDataChanged -= OnDataChanged;
				_steam.LobbyMemberDataChanged -= OnMemberDataChanged;
			}
			_joinCancellation?.Cancel();
			PendingRequest[] array = _requests.Values.ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				array[i].Completion.TrySetCanceled();
			}
			_requests.Clear();
			_admission.Reset();
			if (!SteamClient.IsValid)
			{
				return;
			}
			foreach (ulong peer in _peers)
			{
				SteamNetworking.CloseP2PSessionWithUser(peer);
			}
		}

		public async Task<LobbyRoomResult> CreateAsync(LobbyRoomSettings settings)
		{
			if (IsBusy)
			{
				return LobbyRoomResult.Fail("A lobby operation is already in progress.");
			}
			LobbyRoomResult result = LobbyRoomValidation.Validate(settings, 1, 5);
			if (!result.Success)
			{
				return result;
			}
			if (!_coordinator.IsSteamConnectionAvailable)
			{
				return LobbyRoomResult.Fail("Connect to Steam to create a lobby.");
			}
			if (HasRoom && Snapshot.IsOwner)
			{
				return ApplySettings(settings);
			}
			if (_coordinator.SessionPhase != MultiplayerSessionPhase.MenuWaiting)
			{
				return LobbyRoomResult.Fail("Return to the main menu before creating a lobby.");
			}
			IsBusy = true;
			Changed?.Invoke();
			_desiredSettings = settings;
			try
			{
				if (HasRoom)
				{
					_coordinator.LeaveForeignLobbyFromMainMenu();
				}
				else
				{
					_coordinator.CreateLobby();
				}
				double deadline = Time.realtimeSinceStartupAsDouble + 30.0;
				while (this != null && Time.realtimeSinceStartupAsDouble < deadline)
				{
					if (Snapshot.IsOwner && HasRoom)
					{
						return LobbyRoomResult.Ok("Lobby created.");
					}
					await Task.Delay(100);
				}
				return LobbyRoomResult.Fail("Steam could not create a lobby. Please try again.");
			}
			finally
			{
				_desiredSettings = null;
				IsBusy = false;
				Changed?.Invoke();
			}
		}

		public Task<LobbyRoomResult> JoinByCodeAsync(string code, string password)
		{
			if (!LobbyJoinCode.TryDecode(code, out var lobbyId))
			{
				return Task.FromResult(LobbyRoomResult.Fail("Enter a valid SUSHI lobby code."));
			}
			return JoinAsync(lobbyId, password);
		}

		public async Task<LobbyRoomResult> JoinAsync(ulong lobbyId, string password)
		{
			if (IsBusy)
			{
				return LobbyRoomResult.Fail("A lobby operation is already in progress.");
			}
			if (!_coordinator.IsSteamConnectionAvailable)
			{
				return LobbyRoomResult.Fail("Connect to Steam before joining a lobby.");
			}
			if (lobbyId == 0L)
			{
				return LobbyRoomResult.Fail("This lobby code is invalid.");
			}
			if (password != null && password.Length > 32)
			{
				return LobbyRoomResult.Fail("The password is too long.");
			}
			if (HasRoom && _steam.CurrentLobby.Id.Value == lobbyId && Snapshot.IsAdmitted)
			{
				return LobbyRoomResult.Ok("You are already in this lobby.");
			}
			IsBusy = true;
			LastAdmissionError = "";
			Changed?.Invoke();
			CancellationTokenSource cancellation = (_joinCancellation = new CancellationTokenSource());
			try
			{
				Lobby lobby = new Lobby(lobbyId);
				LobbyRoomResult result = await FetchMetadataAsync(lobby, cancellation.Token);
				if (!result.Success)
				{
					return result;
				}
				LobbyRoomResult result2 = await RequestAdmissionAsync(lobby, password, cancellation.Token);
				if (!result2.Success)
				{
					return result2;
				}
				cancellation.Token.ThrowIfCancellationRequested();
				LobbyJoinOutcome lobbyJoinOutcome = await _coordinator.RequestRoomJoinAsync(lobby);
				return lobbyJoinOutcome.Succeeded ? LobbyRoomResult.Ok("Joined the lobby.") : LobbyRoomResult.Fail((!string.IsNullOrEmpty(LastAdmissionError)) ? LastAdmissionError : ((lobbyJoinOutcome.RoomEnter == RoomEnter.Full) ? "This lobby is full." : "Steam could not join this lobby. Check the code and try again."));
			}
			catch (OperationCanceledException)
			{
				return LobbyRoomResult.Fail("Lobby join cancelled.");
			}
			catch (Exception)
			{
				return LobbyRoomResult.Fail("Steam could not complete the lobby request.");
			}
			finally
			{
				password = null;
				if (_joinCancellation == cancellation)
				{
					_joinCancellation = null;
				}
				cancellation.Dispose();
				IsBusy = false;
				Changed?.Invoke();
			}
		}

		public void CancelPendingJoin()
		{
			_joinCancellation?.Cancel();
			PendingRequest[] array = _requests.Values.ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				array[i].Completion.TrySetCanceled();
			}
			_coordinator?.CancelRoomJoinRequest();
		}

		public async Task<LobbyRoomResult> RefreshAsync()
		{
			if (_refreshing)
			{
				return LobbyRoomResult.Fail("The lobby list is already refreshing.");
			}
			if (!_coordinator.IsSteamConnectionAvailable)
			{
				return LobbyRoomResult.Fail("Connect to Steam to browse lobbies.");
			}
			_refreshing = true;
			try
			{
				if (!(await _coordinator.RefreshLobbiesAsync(50)))
				{
					return LobbyRoomResult.Fail("Steam could not refresh the lobby list.");
				}
				Dictionary<ulong, Lobby> candidates = new Dictionary<ulong, Lobby>();
				foreach (Lobby lobby in _steam.Lobbies)
				{
					candidates[lobby.Id.Value] = lobby;
				}
				foreach (Friend friend in SteamFriends.GetFriends())
				{
					if (friend.IsPlayingThisGame)
					{
						Friend.FriendGameInfo? gameInfo = friend.GameInfo;
						if (gameInfo.HasValue && gameInfo.Value.Lobby.HasValue)
						{
							Lobby value = gameInfo.Value.Lobby.Value;
							candidates[value.Id.Value] = value;
							value.Refresh();
						}
					}
				}
				await Task.Delay(600);
				_rooms.Clear();
				foreach (Lobby value2 in candidates.Values)
				{
					if ((ulong)value2.Id == 0L || value2.GetData("room_protocol") != "sushi-room-1" || (ulong)value2.Id == (ulong)_steam.CurrentLobby.Id)
					{
						continue;
					}
					LobbyVisibility lobbyVisibility = ReadVisibility(value2);
					if (lobbyVisibility != LobbyVisibility.Private)
					{
						ulong num = ReadOwnerId(value2);
						bool flag = num != 0L && new Friend(num).IsFriend;
						if (lobbyVisibility != LobbyVisibility.FriendsOnly || flag)
						{
							_rooms.Add(new LobbyBrowserEntry
							{
								LobbyId = value2.Id.Value,
								Name = ReadName(value2),
								OwnerName = LobbyRoomValidation.CleanName(value2.GetData("room_owner_name")),
								JoinCode = LobbyJoinCode.Encode(value2.Id.Value),
								State = ReadState(value2),
								Visibility = lobbyVisibility,
								MemberCount = value2.MemberCount,
								MaxMembers = value2.MaxMembers,
								HasPassword = (value2.GetData("room_password_required") == "1"),
								JoinLocked = (value2.GetData("room_locked") == "1"),
								IsFriend = flag,
								IsCompatible = (value2.GetData("release_version") == ReleaseVersion.Value)
							});
						}
					}
				}
				_rooms.Sort((LobbyBrowserEntry a, LobbyBrowserEntry b) =>
				{
					int num2 = b.IsFriend.CompareTo(a.IsFriend);
					return (num2 == 0) ? string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase) : num2;
				});
				Changed?.Invoke();
				return LobbyRoomResult.Ok();
			}
			catch (Exception)
			{
				return LobbyRoomResult.Fail("Steam could not refresh the lobby list.");
			}
			finally
			{
				_refreshing = false;
			}
		}

		public LobbyRoomResult ApplySettings(LobbyRoomSettings settings)
		{
			if (!Snapshot.IsOwner || !Snapshot.CanEdit)
			{
				return LobbyRoomResult.Fail("Only the host can change settings while the lobby is waiting.");
			}
			LobbyRoomResult result = LobbyRoomValidation.Validate(settings, _steam.CurrentLobby.MemberCount, 5);
			if (!result.Success)
			{
				return result;
			}
			Lobby currentLobby = _steam.CurrentLobby;
			if (!((settings.Visibility == LobbyVisibility.Private) ? currentLobby.SetPrivate() : ((settings.Visibility == LobbyVisibility.FriendsOnly) ? currentLobby.SetFriendsOnly() : currentLobby.SetPublic())))
			{
				return LobbyRoomResult.Fail("Steam could not update lobby visibility.");
			}
			currentLobby.MaxMembers = settings.MaxMembers;
			if (!currentLobby.SetJoinable(!settings.JoinLocked))
			{
				return LobbyRoomResult.Fail("Steam could not update the lobby lock.");
			}
			if (settings.ClearPassword || !string.IsNullOrEmpty(settings.Password))
			{
				_admission.SetPassword(settings.ClearPassword ? "" : settings.Password);
			}
			_steam.SetLobbyName(LobbyRoomValidation.CleanName(settings.Name));
			currentLobby.SetData("room_visibility", settings.Visibility.ToString());
			currentLobby.SetData("room_password_required", _admission.HasPassword ? "1" : "0");
			currentLobby.SetData("room_locked", settings.JoinLocked ? "1" : "0");
			currentLobby.SetData("room_ready_required", settings.RequireReady ? "1" : "0");
			currentLobby.SetData("room_ready_revision", Guid.NewGuid().ToString("N"));
			_coordinator.SetMaxMembers(settings.MaxMembers);
			_coordinator.SetFriendsOnly(settings.Visibility == LobbyVisibility.FriendsOnly);
			_steam.SetFriendsOnlyFlag(settings.Visibility == LobbyVisibility.FriendsOnly);
			RefreshSnapshot();
			return LobbyRoomResult.Ok("Lobby settings saved.");
		}

		public LobbyRoomResult SetReady(bool ready)
		{
			if (!HasRoom || !Snapshot.IsAdmitted || Snapshot.State != "waiting")
			{
				return LobbyRoomResult.Fail("Join a waiting lobby before changing ready status.");
			}
			_steam.CurrentLobby.SetMemberData("room_ready_revision_ack", _steam.CurrentLobby.GetData("room_ready_revision"));
			_steam.CurrentLobby.SetMemberData("room_ready", ready ? "1" : "0");
			RefreshSnapshot();
			return LobbyRoomResult.Ok();
		}

		public LobbyRoomResult Kick(ulong id)
		{
			return RemoveMember(id, ban: false);
		}

		public LobbyRoomResult Ban(ulong id)
		{
			return RemoveMember(id, ban: true);
		}

		internal LobbyRoomResult KickConnectedMember(ulong id)
		{
			return RemoveMember(id, ban: false, allowMissingMember: true);
		}

		private LobbyRoomResult RemoveMember(ulong id, bool ban, bool allowMissingMember = false)
		{
			if (!HasRoom || !Snapshot.IsOwner)
			{
				return LobbyRoomResult.Fail("Only the lobby host can remove players.");
			}
			if (id == 0L || id == SteamClient.SteamId.Value)
			{
				return LobbyRoomResult.Fail("The lobby host cannot be removed.");
			}
			LobbyRoomMember lobbyRoomMember = Snapshot.Members.FirstOrDefault((LobbyRoomMember m) => m.SteamId == id);
			if (lobbyRoomMember == null && !allowMissingMember)
			{
				return LobbyRoomResult.Fail("This player has already left.");
			}
			if (ban)
			{
				_admission.Ban(id);
				if (!_bans.Any((LobbyBannedMember b) => b.SteamId == id))
				{
					_bans.Add(new LobbyBannedMember
					{
						SteamId = id,
						Name = (lobbyRoomMember?.Name ?? LobbyRoomValidation.CleanName(new Friend(id).Name))
					});
				}
				SaveBans();
			}
			else
			{
				_admission.Kick(id, Time.realtimeSinceStartupAsDouble);
			}
			_pendingAdmissionExpiry.Remove(id);
			PublishAdmissions();
			Send(id, new Packet
			{
				kind = "remove",
				lobby = _steam.CurrentLobby.Id.Value,
				message = (ban ? "You were banned by the lobby host." : "You were removed by the lobby host.")
			});
			_coordinator.RemoveRoomMember(id, ban);
			RefreshSnapshot();
			return LobbyRoomResult.Ok(ban ? "Player banned. The ban remains until you remove it." : "Player removed. They can rejoin after 30 seconds.");
		}

		public LobbyRoomResult Unban(ulong id)
		{
			if (!Snapshot.IsOwner)
			{
				return LobbyRoomResult.Fail("Only the host can remove bans.");
			}
			_admission.Unban(id);
			_bans.RemoveAll((LobbyBannedMember b) => b.SteamId == id);
			SaveBans();
			RefreshSnapshot();
			return LobbyRoomResult.Ok("Ban removed.");
		}

		public void Leave()
		{
			if (HasRoom)
			{
				_coordinator.Disconnect();
			}
		}

		public void OpenInviteOverlay()
		{
			if (HasRoom && Snapshot.IsAdmitted)
			{
				SteamFriends.OpenGameInviteOverlay(_steam.CurrentLobby.Id);
			}
		}

		public void CopyJoinCode()
		{
			if (HasRoom)
			{
				GUIUtility.systemCopyBuffer = Snapshot.JoinCode;
				Notice?.Invoke("Lobby code copied.");
			}
		}

		public bool IsMemberAdmitted(ulong steamId)
		{
			if (!Snapshot.IsOwner)
			{
				return Snapshot.Members.Any((LobbyRoomMember m) => m.SteamId == steamId && m.IsAdmitted);
			}
			return _admission.IsAdmitted(steamId);
		}

		internal async Task<bool> AuthenticateJoinedLobbyAsync(Lobby lobby, CancellationToken cancellationToken)
		{
			if ((ulong)lobby.Owner.Id == (ulong)SteamClient.SteamId || (ReadOwnerId(lobby) != 0L && lobby.Owner.Id.Value != ReadOwnerId(lobby)))
			{
				LastAdmissionError = "The lobby host left or changed during the join. Please use a new lobby code.";
				Notice?.Invoke(LastAdmissionError);
				return false;
			}
			if (_authenticatedLobby == lobby.Id.Value && _authenticatedOwner == lobby.Owner.Id.Value)
			{
				await Task.Delay(600, cancellationToken);
			}
			LobbyRoomResult lobbyRoomResult = await FetchMetadataAsync(lobby, cancellationToken);
			if (!lobbyRoomResult.Success)
			{
				LastAdmissionError = lobbyRoomResult.Message;
				Notice?.Invoke(lobbyRoomResult.Message);
				return false;
			}
			LobbyRoomResult lobbyRoomResult2 = await RequestAdmissionAsync(lobby, "", cancellationToken);
			if (lobbyRoomResult2.Success && lobby.Owner.Id.Value != _authenticatedOwner)
			{
				_authenticatedLobby = 0uL;
				_authenticatedOwner = 0uL;
				lobbyRoomResult2 = LobbyRoomResult.Fail("The lobby host changed during the join. Please try again.");
			}
			if (!lobbyRoomResult2.Success)
			{
				LastAdmissionError = lobbyRoomResult2.Message;
				Notice?.Invoke(lobbyRoomResult2.Message);
				if (lobby.GetData("room_password_required") == "1")
				{
					PasswordRequested?.Invoke(lobby.Id.Value);
				}
			}
			else
			{
				_joinedAdmissionLobby = lobby.Id.Value;
				_joinedAdmissionOwner = lobby.Owner.Id.Value;
			}
			return lobbyRoomResult2.Success;
		}

		internal bool HasPrivateAdmissionFor(Lobby lobby)
		{
			if (ReadVisibility(lobby) == LobbyVisibility.Private && _authenticatedLobby == lobby.Id.Value && _authenticatedOwner != 0L)
			{
				return _authenticatedOwner == ReadOwnerId(lobby);
			}
			return false;
		}

		internal void OnLobbyLeft()
		{
			_joinedAdmissionLobby = 0uL;
			_joinedAdmissionOwner = 0uL;
			if (_authenticatedLobby == _observedLobby)
			{
				_authenticatedLobby = 0uL;
				_authenticatedOwner = 0uL;
			}
			_observedLobby = 0uL;
			_observedOwner = 0uL;
			_memberArrivals.Clear();
			_pendingAdmissionExpiry.Clear();
			_admission.Reset(_bans.Select((LobbyBannedMember b) => b.SteamId));
			Snapshot = new LobbyRoomSnapshot();
			Changed?.Invoke();
		}

		private async Task<LobbyRoomResult> FetchMetadataAsync(Lobby lobby, CancellationToken cancellationToken)
		{
			lobby.Refresh();
			double deadline = Time.realtimeSinceStartupAsDouble + 8.0;
			while (this != null && Time.realtimeSinceStartupAsDouble < deadline)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (ReadOwnerId(lobby) != 0L && !string.IsNullOrEmpty(lobby.GetData("room_protocol")))
				{
					if (lobby.GetData("room_protocol") != "sushi-room-1")
					{
						return LobbyRoomResult.Fail("This lobby uses an incompatible lobby version.");
					}
					if (lobby.GetData("release_version") != ReleaseVersion.Value)
					{
						return LobbyRoomResult.Fail("The host is running a different game version.");
					}
					return LobbyRoomResult.Ok();
				}
				await Task.Delay(100, cancellationToken);
			}
			return LobbyRoomResult.Fail("This lobby is unavailable or uses an older game version.");
		}

		private async Task<LobbyRoomResult> RequestAdmissionAsync(Lobby lobby, string password, CancellationToken cancellationToken)
		{
			ulong owner = ReadOwnerId(lobby);
			if (owner == 0L)
			{
				return LobbyRoomResult.Fail("The lobby host is unavailable.");
			}
			string requestId = Guid.NewGuid().ToString("N");
			PendingRequest pending = new PendingRequest
			{
				LobbyId = lobby.Id.Value,
				OwnerId = owner,
				Completion = new TaskCompletionSource<LobbyRoomResult>(TaskCreationOptions.RunContinuationsAsynchronously)
			};
			_requests.Add(requestId, pending);
			try
			{
				if (!Send(owner, new Packet
				{
					kind = "request",
					request = requestId,
					lobby = lobby.Id.Value,
					password = (password ?? "")
				}))
				{
					return LobbyRoomResult.Fail("Steam could not contact the lobby host.");
				}
				Task obj = await Task.WhenAny(pending.Completion.Task, Task.Delay(15000, cancellationToken));
				cancellationToken.ThrowIfCancellationRequested();
				if (obj != pending.Completion.Task)
				{
					return LobbyRoomResult.Fail("The lobby host did not respond. Please try again.");
				}
				LobbyRoomResult result = await pending.Completion.Task;
				if (result.Success)
				{
					_authenticatedLobby = lobby.Id.Value;
					_authenticatedOwner = owner;
				}
				return result;
			}
			finally
			{
				_requests.Remove(requestId);
				password = null;
			}
		}

		private void OnPeerRequest(SteamId peer)
		{
			if (SteamClient.IsValid && (ulong)peer != 0L)
			{
				bool flag = _requests.Values.Any((PendingRequest p) => p.OwnerId == peer.Value);
				if (((HasRoom && Snapshot.IsOwner) || flag || peer.Value == _observedOwner) && (_peers.Count < 64 || _peers.Contains(peer.Value)))
				{
					SteamNetworking.AcceptP2PSessionWithUser(peer);
					_peers.Add(peer.Value);
					_peerLastSeen[peer.Value] = Time.realtimeSinceStartupAsDouble;
				}
			}
		}

		private bool Send(ulong target, Packet packet)
		{
			if (!SteamClient.IsValid || target == 0L)
			{
				return false;
			}
			byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(packet));
			try
			{
				return bytes.Length <= 2048 && SteamNetworking.SendP2PPacket(target, bytes, bytes.Length, 73);
			}
			finally
			{
				Array.Clear(bytes, 0, bytes.Length);
				packet.password = null;
			}
		}

		private void Update()
		{
			if (!_initialized || !SteamClient.IsValid)
			{
				return;
			}
			for (int i = 0; i < 12; i++)
			{
				if (!SteamNetworking.IsP2PPacketAvailable(out var msgSize, 73))
				{
					break;
				}
				if (msgSize > 2048)
				{
					uint size = 0u;
					SteamId steamid = 0uL;
					SteamNetworking.ReadP2PPacket(_discardBuffer, ref size, ref steamid, 73);
					continue;
				}
				P2Packet? p2Packet = SteamNetworking.ReadP2PPacket(73);
				if (!p2Packet.HasValue)
				{
					break;
				}
				P2Packet value = p2Packet.Value;
				_peerLastSeen[value.SteamId.Value] = Time.realtimeSinceStartupAsDouble;
				try
				{
					Packet packet = JsonConvert.DeserializeObject<Packet>(Encoding.UTF8.GetString(value.Data), new JsonSerializerSettings
					{
						MaxDepth = 4,
						TypeNameHandling = TypeNameHandling.None
					});
					if (packet != null && packet.protocol == "sushi-room-1")
					{
						HandlePacket(value.SteamId.Value, packet);
					}
				}
				catch (JsonException)
				{
				}
				finally
				{
					Array.Clear(value.Data, 0, value.Data.Length);
				}
			}
			if (Time.realtimeSinceStartupAsDouble < _nextRefresh)
			{
				return;
			}
			_nextRefresh = Time.realtimeSinceStartupAsDouble + 0.5;
			KeyValuePair<ulong, double>[] array = _peerLastSeen.ToArray();
			for (int j = 0; j < array.Length; j++)
			{
				KeyValuePair<ulong, double> peer = array[j];
				if (!(Time.realtimeSinceStartupAsDouble - peer.Value < 60.0) && !_requests.Values.Any((PendingRequest p) => p.OwnerId == peer.Key))
				{
					SteamNetworking.CloseP2PSessionWithUser(peer.Key);
					_peerLastSeen.Remove(peer.Key);
					_peers.Remove(peer.Key);
				}
			}
			RefreshSnapshot();
			if (!HasRoom || !Snapshot.IsOwner)
			{
				return;
			}
			double realtimeSinceStartupAsDouble = Time.realtimeSinceStartupAsDouble;
			bool flag = false;
			array = _pendingAdmissionExpiry.ToArray();
			for (int j = 0; j < array.Length; j++)
			{
				KeyValuePair<ulong, double> entry = array[j];
				if (Snapshot.Members.Any((LobbyRoomMember m) => m.SteamId == entry.Key))
				{
					_pendingAdmissionExpiry.Remove(entry.Key);
				}
				else if (!(realtimeSinceStartupAsDouble < entry.Value))
				{
					_pendingAdmissionExpiry.Remove(entry.Key);
					_admission.Depart(entry.Key);
					flag = true;
				}
			}
			foreach (LobbyRoomMember member in Snapshot.Members)
			{
				if (!member.IsAdmitted && !member.IsOwner)
				{
					if (!_memberArrivals.TryGetValue(member.SteamId, out var value2))
					{
						_memberArrivals[member.SteamId] = realtimeSinceStartupAsDouble;
					}
					else if (realtimeSinceStartupAsDouble - value2 > 20.0)
					{
						Send(member.SteamId, new Packet
						{
							kind = "remove",
							lobby = Snapshot.LobbyId,
							message = "Lobby access was not approved. Join again with the lobby code and password."
						});
						_memberArrivals[member.SteamId] = realtimeSinceStartupAsDouble;
					}
				}
			}
			if (flag)
			{
				PublishAdmissions();
			}
		}

		private void HandlePacket(ulong authenticatedSender, Packet packet)
		{
			if (packet.kind == "result")
			{
				if (packet.request != null && _requests.TryGetValue(packet.request, out var value) && value.OwnerId == authenticatedSender && value.LobbyId == packet.lobby)
				{
					value.Completion.TrySetResult(new LobbyRoomResult(packet.success, packet.message));
				}
			}
			else if (packet.kind == "remove")
			{
				if (HasRoom && packet.lobby == _steam.CurrentLobby.Id.Value && authenticatedSender == _steam.CurrentLobby.Owner.Id.Value && authenticatedSender != SteamClient.SteamId.Value)
				{
					_authenticatedLobby = 0uL;
					_authenticatedOwner = 0uL;
					LastAdmissionError = packet.message ?? "You were removed from the lobby.";
					Notice?.Invoke(LastAdmissionError);
					_coordinator.Disconnect();
				}
			}
			else
			{
				if (packet.kind != "request" || !HasRoom || (ulong)_steam.CurrentLobby.Owner.Id != (ulong)SteamClient.SteamId || packet.lobby != _steam.CurrentLobby.Id.Value || string.IsNullOrEmpty(packet.request) || packet.request.Length > 40)
				{
					return;
				}
				double realtimeSinceStartupAsDouble = Time.realtimeSinceStartupAsDouble;
				if (_packetRateLimit.TryGetValue(authenticatedSender, out var value2) && realtimeSinceStartupAsDouble < value2)
				{
					return;
				}
				if (_packetRateLimit.Count > 256)
				{
					_packetRateLimit.Clear();
				}
				_packetRateLimit[authenticatedSender] = realtimeSinceStartupAsDouble + 0.5;
				Lobby currentLobby = _steam.CurrentLobby;
				bool flag = currentLobby.Members.Any((Friend m) => m.Id.Value == authenticatedSender);
				bool flag2 = _admission.IsAdmitted(authenticatedSender);
				LobbyRoomResult lobbyRoomResult = ((packet.password != null && packet.password.Length > 32) ? LobbyRoomResult.Fail("The password is too long.") : ((!flag2 && !flag && currentLobby.MemberCount + _pendingAdmissionExpiry.Count >= currentLobby.MaxMembers) ? LobbyRoomResult.Fail("This lobby is full.") : ((!(ReadState(currentLobby) != "waiting") || !(ReadState(currentLobby) != "ingame") || flag2) ? _admission.Evaluate(authenticatedSender, packet.password, ReadVisibility(currentLobby) == LobbyVisibility.FriendsOnly, new Friend(authenticatedSender).IsFriend, currentLobby.GetData("room_locked") == "1", realtimeSinceStartupAsDouble) : LobbyRoomResult.Fail("The host is starting the game. Try again shortly."))));
				packet.password = null;
				if (lobbyRoomResult.Success && !flag)
				{
					if (ReadVisibility(currentLobby) == LobbyVisibility.Private && !currentLobby.InviteFriend(authenticatedSender))
					{
						_admission.Depart(authenticatedSender);
						lobbyRoomResult = LobbyRoomResult.Fail("Steam could not authorize the private lobby invitation.");
					}
					else
					{
						_pendingAdmissionExpiry[authenticatedSender] = realtimeSinceStartupAsDouble + 30.0;
					}
				}
				PublishAdmissions();
				Send(authenticatedSender, new Packet
				{
					kind = "result",
					request = packet.request,
					lobby = packet.lobby,
					success = lobbyRoomResult.Success,
					message = lobbyRoomResult.Message
				});
				RefreshSnapshot();
			}
		}

		private void OnCreated(Lobby lobby)
		{
			if (_loadedBanOwner != SteamClient.SteamId.Value)
			{
				LoadBans();
			}
			_observedLobby = lobby.Id.Value;
			_observedOwner = lobby.Owner.Id.Value;
			_authenticatedLobby = lobby.Id.Value;
			_authenticatedOwner = lobby.Owner.Id.Value;
			_admission.Reset(_bans.Select((LobbyBannedMember b) => b.SteamId));
			_admission.AdmitOwner(SteamClient.SteamId.Value);
			lobby.SetData("room_protocol", "sushi-room-1");
			lobby.SetData("room_owner", SteamClient.SteamId.Value.ToString());
			lobby.SetData("room_owner_name", LobbyRoomValidation.CleanName(SteamClient.Name));
			lobby.SetData("release_version", ReleaseVersion.Value);
			lobby.SetMemberData("room_ready", "1");
			PublishAdmissions();
			RefreshSnapshot();
			LobbyRoomSettings lobbyRoomSettings = _desiredSettings;
			if (lobbyRoomSettings == null)
			{
				LobbyRoomSettings lobbyRoomSettings2 = new LobbyRoomSettings();
				lobbyRoomSettings2.Name = LobbyRoomValidation.CleanName(LocalizationDatabase.GetFormattedString("UiTable", "ui.lobby.owner_lobby", "{0}'s Lobby", SteamClient.Name));
				lobbyRoomSettings2.MaxMembers = lobby.MaxMembers;
				lobbyRoomSettings2.Visibility = (_steam.IsFriendsOnly ? LobbyVisibility.FriendsOnly : LobbyVisibility.Public);
				lobbyRoomSettings = lobbyRoomSettings2;
			}
			LobbyRoomSettings settings = lobbyRoomSettings;
			ApplySettings(settings);
			_desiredSettings = null;
		}

		private void OnEntered(Lobby lobby)
		{
			_observedLobby = lobby.Id.Value;
			_observedOwner = lobby.Owner.Id.Value;
			lobby.SetMemberData("room_ready", "0");
			RefreshSnapshot();
		}

		private void OnMemberJoined(Lobby lobby, Friend member)
		{
			if (HasRoom && (ulong)lobby.Id == (ulong)_steam.CurrentLobby.Id)
			{
				_memberArrivals[member.Id.Value] = Time.realtimeSinceStartupAsDouble;
				RefreshSnapshot();
			}
		}

		private void OnMemberLeft(Lobby lobby, Friend member)
		{
			if (HasRoom && (ulong)lobby.Id == (ulong)_steam.CurrentLobby.Id)
			{
				_memberArrivals.Remove(member.Id.Value);
				_pendingAdmissionExpiry.Remove(member.Id.Value);
				if (Snapshot.IsOwner)
				{
					_admission.Depart(member.Id.Value);
					PublishAdmissions();
				}
				RefreshSnapshot();
			}
		}

		private void OnDataChanged(Lobby lobby)
		{
			RefreshSnapshot();
		}

		private void OnMemberDataChanged(Lobby lobby, Friend member)
		{
			RefreshSnapshot();
		}

		private void PublishAdmissions()
		{
			if (HasRoom && (ulong)_steam.CurrentLobby.Owner.Id == (ulong)SteamClient.SteamId)
			{
				_steam.CurrentLobby.SetData("room_admitted", LobbySessionMetadata.SerializeCohort(_admission.AdmittedIds));
			}
		}

		private void RefreshSnapshot()
		{
			if (!_initialized || !SteamClient.IsValid || !HasRoom)
			{
				if (Snapshot.LobbyId != 0L)
				{
					Snapshot = new LobbyRoomSnapshot();
					Changed?.Invoke();
				}
				return;
			}
			Lobby currentLobby = _steam.CurrentLobby;
			ulong value = currentLobby.Owner.Id.Value;
			if (_observedLobby == currentLobby.Id.Value && _observedOwner != 0L && value != _observedOwner)
			{
				_observedOwner = value;
				_authenticatedLobby = 0uL;
				_authenticatedOwner = 0uL;
				Notice?.Invoke("The lobby host left. Returning to your own lobby.");
				_coordinator.Disconnect();
				return;
			}
			bool flag = value == SteamClient.SteamId.Value;
			IReadOnlyCollection<ulong> source = (flag ? _admission.AdmittedIds : LobbySessionMetadata.ParseCohort(currentLobby.GetData("room_admitted")));
			LobbyRoomSnapshot lobbyRoomSnapshot = new LobbyRoomSnapshot
			{
				LobbyId = currentLobby.Id.Value,
				OwnerSteamId = value,
				Name = ReadName(currentLobby),
				JoinCode = LobbyJoinCode.Encode(currentLobby.Id.Value),
				State = ReadState(currentLobby),
				Visibility = ReadVisibility(currentLobby),
				MaxMembers = currentLobby.MaxMembers,
				HasPassword = (currentLobby.GetData("room_password_required") == "1"),
				JoinLocked = (currentLobby.GetData("room_locked") == "1"),
				RequireReady = (currentLobby.GetData("room_ready_required") != "0"),
				IsOwner = flag,
				IsAdmitted = (flag || (_joinedAdmissionLobby == currentLobby.Id.Value && _joinedAdmissionOwner == value && source.Contains(SteamClient.SteamId.Value)))
			};
			foreach (Friend member in currentLobby.Members)
			{
				bool flag2 = member.Id.Value == value;
				lobbyRoomSnapshot.Members.Add(new LobbyRoomMember
				{
					SteamId = member.Id.Value,
					Name = LobbyRoomValidation.CleanName(member.Name),
					IsOwner = flag2,
					IsLocal = ((ulong)member.Id == (ulong)SteamClient.SteamId),
					IsFriend = member.IsFriend,
					IsAdmitted = (flag2 || source.Contains(member.Id.Value)),
					IsReady = (flag2 || (currentLobby.GetMemberData(member, "room_ready") == "1" && currentLobby.GetMemberData(member, "room_ready_revision_ack") == currentLobby.GetData("room_ready_revision")))
				});
			}
			lobbyRoomSnapshot.Members.Sort((LobbyRoomMember a, LobbyRoomMember b) => b.IsOwner.CompareTo(a.IsOwner));
			lobbyRoomSnapshot.LocalReady = lobbyRoomSnapshot.Members.Any((LobbyRoomMember m) => m.IsLocal && m.IsReady);
			lobbyRoomSnapshot.AllReady = lobbyRoomSnapshot.Members.Where((LobbyRoomMember m) => m.IsAdmitted).All((LobbyRoomMember m) => m.IsReady);
			lobbyRoomSnapshot.CanEdit = flag && lobbyRoomSnapshot.State == "waiting" && _coordinator.SessionPhase == MultiplayerSessionPhase.MenuWaiting;
			if (flag)
			{
				lobbyRoomSnapshot.Bans = _bans.Select((LobbyBannedMember b) => new LobbyBannedMember
				{
					SteamId = b.SteamId,
					Name = b.Name
				}).ToList();
			}
			if (!flag && Snapshot.LobbyId == lobbyRoomSnapshot.LobbyId && Snapshot.IsAdmitted && !lobbyRoomSnapshot.IsAdmitted)
			{
				Snapshot = lobbyRoomSnapshot;
				_authenticatedLobby = 0uL;
				_authenticatedOwner = 0uL;
				LastAdmissionError = "The lobby host removed your access.";
				Notice?.Invoke(LastAdmissionError);
				_coordinator.Disconnect();
			}
			else
			{
				PublishSnapshot(lobbyRoomSnapshot);
			}
		}

		private void PublishSnapshot(LobbyRoomSnapshot snapshot)
		{
			if (!Snapshot.HasSamePresentation(snapshot))
			{
				Snapshot = snapshot;
				Changed?.Invoke();
			}
		}

		private static string ReadName(Lobby lobby)
		{
			string text = LobbyRoomValidation.CleanName(lobby.GetData("name"));
			if (!string.IsNullOrEmpty(text))
			{
				return text;
			}
			return LocalizationDatabase.GetStringOrFallback("UiTable", "ui.lobby.default_name", "Sushi Lobby");
		}

		private static string ReadState(Lobby lobby)
		{
			string data = lobby.GetData("session_state");
			if (!string.IsNullOrEmpty(data))
			{
				return data;
			}
			return "waiting";
		}

		private static LobbyVisibility ReadVisibility(Lobby lobby)
		{
			if (!Enum.TryParse<LobbyVisibility>(lobby.GetData("room_visibility"), out var result))
			{
				return LobbyVisibility.Public;
			}
			return result;
		}

		private static ulong ReadOwnerId(Lobby lobby)
		{
			if (!ulong.TryParse(lobby.GetData("room_owner"), out var result))
			{
				return 0uL;
			}
			return result;
		}

		private void LoadBans()
		{
			_bans.Clear();
			if (SteamClient.IsValid)
			{
				_loadedBanOwner = SteamClient.SteamId.Value;
				try
				{
					_bans.AddRange(JsonConvert.DeserializeObject<List<LobbyBannedMember>>(PlayerPrefs.GetString(BanPreferenceKey, "[]")) ?? new List<LobbyBannedMember>());
				}
				catch (JsonException)
				{
					_bans.Clear();
				}
				_bans.RemoveAll((LobbyBannedMember b) => b == null || b.SteamId == 0L || (SteamClient.IsValid && b.SteamId == SteamClient.SteamId.Value));
				_admission.Reset(_bans.Select((LobbyBannedMember b) => b.SteamId));
			}
		}

		private void SaveBans()
		{
			PlayerPrefs.SetString(BanPreferenceKey, JsonConvert.SerializeObject(_bans));
			PlayerPrefs.Save();
		}
	}
}
