using System;
using System.Threading;

namespace PaperPixelGames.Networking
{
	public sealed class SteamLobbyOperationController : IDisposable
	{
		private long _nextOperationId;

		private long _activeOperationId;

		private CancellationTokenSource _activeCancellation;

		public long ActiveOperationId => _activeOperationId;

		public LobbyOperationLease Begin(LobbyOperationKind kind, ulong targetLobbyId = 0uL)
		{
			CancelActive();
			_activeCancellation = new CancellationTokenSource();
			_activeOperationId = Interlocked.Increment(ref _nextOperationId);
			return new LobbyOperationLease(_activeOperationId, kind, targetLobbyId, _activeCancellation.Token);
		}

		public bool IsCurrent(LobbyOperationLease lease)
		{
			if (lease.OperationId != 0L && lease.OperationId == _activeOperationId)
			{
				return !lease.CancellationToken.IsCancellationRequested;
			}
			return false;
		}

		public void Complete(LobbyOperationLease lease)
		{
			if (IsCurrent(lease))
			{
				_activeCancellation?.Dispose();
				_activeCancellation = null;
				_activeOperationId = 0L;
			}
		}

		public void CancelActive()
		{
			if (_activeCancellation != null)
			{
				_activeCancellation.Cancel();
				_activeCancellation.Dispose();
				_activeCancellation = null;
			}
			_activeOperationId = 0L;
		}

		public void Dispose()
		{
			CancelActive();
		}
	}
}
