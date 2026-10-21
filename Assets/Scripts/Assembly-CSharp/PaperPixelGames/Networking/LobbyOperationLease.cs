using System.Threading;

namespace PaperPixelGames.Networking
{
	public readonly struct LobbyOperationLease
	{
		public long OperationId { get; }

		public LobbyOperationKind Kind { get; }

		public ulong TargetLobbyId { get; }

		public CancellationToken CancellationToken { get; }

		public LobbyOperationLease(long operationId, LobbyOperationKind kind, ulong targetLobbyId, CancellationToken cancellationToken)
		{
			OperationId = operationId;
			Kind = kind;
			TargetLobbyId = targetLobbyId;
			CancellationToken = cancellationToken;
		}
	}
}
