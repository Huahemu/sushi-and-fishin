using System;
using System.Threading;
using System.Threading.Tasks;
using Nav3D.API;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.Pathfinding
{
	public class Path : IDisposable, ICurve
	{
		private readonly string LOG_PATH_CTOR = "Path.ctor";

		private readonly string LOG_PATH_SUCCESS = "Path.DoOnSuccess: Targets: {0}";

		private readonly string LOG_PATH_FAIL = "Path.DoOnFail: Targets: {0}";

		private readonly string LOG_PATH_DISPOSE = "Path.Dispose";

		private readonly string LOG_FIND_PATH = "Path.Find: Targets: {0}";

		private readonly string LOG_PATH_UPDATE = "Path.Update";

		private PathfindingRequest m_Request;

		private int[] m_TargetIndices;

		private readonly Log m_Log;

		private CancellationTokenSource m_UpdateTokenSource;

		private PathFollowData m_LastGivenFollowData;

		public Vector3[] TrajectoryFinal { get; private set; }

		public Vector3[] TrajectoryOriginal { get; private set; }

		public Vector3[] TrajectoryOptimized { get; private set; }

		public Vector3[] TrajectorySmoothed => TrajectoryFinal;

		public Bounds Bounds { get; private set; }

		public bool IsValid { get; set; }

		public Segment3[] Segments { get; private set; }

		public PathfindingResult LastPathfindingResult { get; private set; }

		public event Action<PathfindingResult> OnPathfindingFinished;

		public Path(Log _Log = null)
		{
			m_Log = _Log;
			m_Log?.Write(LOG_PATH_CTOR);
		}

		~Path()
		{
			Dispose();
		}

		public bool Intersects(Bounds _Bounds)
		{
			if (!IsValid)
			{
				return false;
			}
			Segment3[] segments = Segments;
			foreach (Segment3 segment in segments)
			{
				if (Bounds.IntersectSegment(segment))
				{
					return true;
				}
			}
			return false;
		}

		public Task<PathfindingResult> Find(PathfindingRequest _Request, bool _SilentUpdate = false)
		{
			m_Log?.WriteFormat(LOG_FIND_PATH, UtilsCommon.GetPointsString(_Request.Points));
			m_UpdateTokenSource?.Cancel();
			m_UpdateTokenSource = new CancellationTokenSource();
			m_Request = _Request;
			if (!_SilentUpdate)
			{
				IsValid = false;
			}
			return RequestPathfinding();
		}

		public void Update()
		{
			m_Log?.Write(LOG_PATH_UPDATE);
			m_UpdateTokenSource?.Cancel();
			m_UpdateTokenSource = new CancellationTokenSource();
			RequestPathfinding();
		}

		public bool GetFollowData(Action<Vector3> _OnTargetPassed, Action _OnLastTargetReached, Vector3 _FollowerPosition, float _ReachDist, out PathFollowData _FollowData)
		{
			if (!IsValid || TrajectoryFinal == null || TrajectoryFinal.Length == 0 || m_TargetIndices == null || m_TargetIndices.Length == 0)
			{
				_FollowData = null;
				return false;
			}
			int num = -1;
			for (int i = 0; i < m_TargetIndices.Length; i++)
			{
				int num2 = m_TargetIndices[i];
				if (num2 < 0 || num2 >= TrajectoryFinal.Length || num2 < num)
				{
					_FollowData = null;
					return false;
				}
				num = num2;
			}
			_FollowData = (m_LastGivenFollowData = new PathFollowData(TrajectoryFinal, m_TargetIndices, _FollowerPosition, _ReachDist, _OnTargetPassed, _OnLastTargetReached, m_Log));
			return true;
		}

		public void Dispose()
		{
			m_LastGivenFollowData?.Invalidate();
			m_UpdateTokenSource.Cancel();
			if (!PathfindingManager.Doomed)
			{
				PathfindingManager.Instance.DisposePath(this);
			}
			m_Log?.Write(LOG_PATH_DISPOSE);
		}

		private Task<PathfindingResult> RequestPathfinding()
		{
			if (m_Request == null)
			{
				return null;
			}
			return PathfindingManager.Instance.RequestPathfinding(this, m_Request, m_UpdateTokenSource.Token, m_Log).ContinueWith<PathfindingResult>(OnPathfindingFinishedLocal, TaskContinuationOptions.OnlyOnRanToCompletion);
			PathfindingResult OnPathfindingFinishedLocal(Task<PathfindingResult> _PathfindingTask)
			{
				if (_PathfindingTask.Result.Failed)
				{
					DoOnFail(_PathfindingTask.Result);
				}
				else
				{
					DoOnSuccess(_PathfindingTask.Result);
				}
				OnPathfindingFinished?.Invoke(_PathfindingTask.Result);
				return _PathfindingTask.Result;
			}
		}

		protected virtual void DoOnSuccess(PathfindingResult _Result)
		{
			m_Log?.WriteFormat(LOG_PATH_SUCCESS, UtilsCommon.GetPointsString(m_Request.Points));
			m_LastGivenFollowData?.Invalidate();
			LastPathfindingResult = _Result;
			TrajectoryOriginal = _Result.PathRaw;
			TrajectoryOptimized = _Result.PathOptimized;
			TrajectoryFinal = _Result.PathFinal;
			m_TargetIndices = _Result.TargetIndices;
			Bounds = ExtensionBounds.PointBounds(TrajectoryFinal);
			Segments = UtilsMath.PointSequenceToSegments(TrajectoryFinal);
			IsValid = true;
			PathfindingManager.Instance.UpdatePathInStorage(this);
		}

		protected void DoOnFail(PathfindingResult _Result)
		{
			m_Log?.WriteFormat(LOG_PATH_FAIL, UtilsCommon.GetPointsString(m_Request.Points));
			IsValid = false;
			PathfindingManager.Instance.RemovePathFromStorage(this);
		}
	}
}
