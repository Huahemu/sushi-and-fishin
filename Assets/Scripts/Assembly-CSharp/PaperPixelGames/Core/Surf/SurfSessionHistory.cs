using System.Collections.Generic;

namespace PaperPixelGames.Core.Surf
{
	public sealed class SurfSessionHistory
	{
		private readonly Dictionary<ulong, uint> _endedSessions = new Dictionary<ulong, uint>();

		public bool CanEnter(ulong boardId, uint session)
		{
			if (_endedSessions.TryGetValue(boardId, out var value))
			{
				return session > value;
			}
			return true;
		}

		public void End(ulong boardId, uint session)
		{
			if (!_endedSessions.TryGetValue(boardId, out var value) || session > value)
			{
				_endedSessions[boardId] = session;
			}
		}

		public void Clear()
		{
			_endedSessions.Clear();
		}
	}
}
