using System;
using Steamworks;
using Unity.Netcode;

namespace PaperPixelGames.Networking
{
	public interface INgoNetworkGateway
	{
		ClientJoinStage JoinStage { get; }

		long JoinAttempt { get; }

		double JoinStageStartedAt { get; }

		double JoinLastProgressAt { get; }

		double JoinStageLimitSeconds { get; }

		double ApprovalTimeoutSeconds { get; }

		double SceneTimeoutSeconds { get; }

		bool IsStopped { get; }

		bool SceneOperationsSettled { get; }

		event Action<ClientJoinStage> ClientJoinStageChanged;

		event Action ServerStarted;

		event Action<ulong> ClientConnected;

		event Action<ulong> ClientDisconnected;

		event Action TransportFailure;

		event Action<bool> ServerStopped;

		event Action<bool> ClientStopped;

		event Action<NetworkManager.ConnectionApprovalRequest, NetworkManager.ConnectionApprovalResponse> ApprovalRequested;

		void Initialize();

		void ConfigureJoinTimeouts(float transportTimeoutSeconds);

		JoinWaitStatus CheckJoinDeadline(long attempt, double now);

		bool StartHost(byte[] connectionData);

		bool StartClient(SteamId target, byte[] connectionData, bool exitCurrentState = true);

		bool TryGetAuthenticatedSteamId(ulong networkClientId, out ulong steamId);

		void Shutdown(bool kickRemotesIfHost = true);
	}
}
