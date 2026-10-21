using System;
using System.Threading;
using System.Threading.Tasks;
using Nav3D.API;
using Nav3D.Common;

namespace Nav3D.Pathfinding
{
	public class PathfindingOrder : IExecutable
	{
		private readonly string LOG_CTOR = "PathfindingOrder.ctor (ID:{0}): Points {1}, Loop: {2}";

		private readonly string LOG_EXECUTION_STARTED = "PathfindingOrder.Execute: for points {0}";

		private readonly string LOG_PATHFINDING_STARTED = "PathfindingOrder: Pathfinding task started: for points {0}";

		private readonly string LOG_PATHFINDING_FINISHED = "PathfindingOrder: Pathfinding task finished: for points {0}";

		private readonly PathfindingRequest m_Request;

		private readonly CancellationToken m_CancellationToken;

		private readonly Func<PathfindingRequest, CancellationToken, CancellationToken, Action<string>, Log, Task<PathfindingResult>> m_PathfindingMethod;

		private string m_Status;

		private readonly Log m_Log;

		private readonly TaskCompletionSource<PathfindingResult> m_TCS;

		public Task<PathfindingResult> Task => m_TCS.Task;

		public CancellationToken CancellationToken => m_CancellationToken;

		public PathfindingOrder(PathfindingRequest _Request, CancellationToken _CancellationToken, Func<PathfindingRequest, CancellationToken, CancellationToken, Action<string>, Log, Task<PathfindingResult>> _PathfindingMethod, Log _Log = null)
		{
			m_Request = _Request;
			m_CancellationToken = _CancellationToken;
			m_PathfindingMethod = _PathfindingMethod;
			m_TCS = new TaskCompletionSource<PathfindingResult>(m_CancellationToken);
			(m_Log = _Log)?.WriteFormat(LOG_CTOR, GetHashCode(), UtilsCommon.GetPointsString(m_Request.Points), m_Request.Loop);
		}

		public string GetExecutingStatus()
		{
			return m_Request.RequesterID + ": " + m_Status;
		}

		public void Execute(Action _OnStart, Action _OnResolve)
		{
			_OnStart?.Invoke();
			m_Status = "Execute method called";
			m_Log?.WriteFormat(LOG_EXECUTION_STARTED, UtilsCommon.GetPointsString(m_Request.Points));
			CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
			CancellationToken token = cancellationTokenSource.Token;
			m_Status = "Pathfinding task started";
			m_Log?.WriteFormat(LOG_PATHFINDING_STARTED, UtilsCommon.GetPointsString(m_Request.Points));
			cancellationTokenSource.CancelAfter(m_Request.Timeout);
			PathfindingResult result = m_PathfindingMethod(m_Request, m_CancellationToken, token, (string _Status) =>
			{
				m_Status = _Status;
			}, m_Log).GetAwaiter().GetResult();
			if (m_CancellationToken.IsCancellationRequested)
			{
				m_TCS.SetCanceled();
				_OnResolve();
				m_Status = "Pathfinding task cancelled";
			}
			else
			{
				m_TCS.SetResult(result);
				_OnResolve();
				m_Status = "Pathfinding task finished";
			}
			m_Log?.WriteFormat(LOG_PATHFINDING_FINISHED, UtilsCommon.GetPointsString(m_Request.Points));
		}
	}
}
