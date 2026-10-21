using System;
using System.Threading;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;

namespace PaperPixelGames.Networking
{
	public interface ISteamLobbyGateway
	{
		bool IsInitializedSuccessfully { get; }

		bool IsSteamAvailable { get; }

		bool IsFriendsOnly { get; }

		Lobby CurrentLobby { get; }

		event Action<Lobby> LobbyCreated;

		event Action<Lobby> LobbyEntered;

		event Action<Lobby, Friend> LobbyMemberJoined;

		event Action<Lobby, Friend> LobbyMemberLeft;

		event Action<Friend, Lobby> LobbyInvite;

		event Action<Lobby, SteamId> GameLobbyJoinRequested;

		event Action<Lobby> LobbyDataChanged;

		event Action<Lobby, Friend, string> LobbyInternalMessageReceived;

		void Initialize();

		Task<LobbyCreationOutcome> CreateLobbyAsync(int maxMembers, bool friendsOnly, CancellationToken cancellationToken, int timeoutSeconds);

		Task<LobbyJoinOutcome> JoinLobbyAsync(Lobby targetLobby, CancellationToken cancellationToken, int timeoutSeconds = 15);

		void LeaveCurrentLobby();

		bool SendLobbyInternalMessage(string payload);

		bool TryGetLobbyMenuData(Lobby lobby, out LobbyMenuData data);

		Task<bool> RefreshLobbiesAsync(int maxResults = 20);
	}
}
