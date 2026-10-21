using System;
using System.Linq;
using System.Threading.Tasks;
using Nav3D.Common;
using Nav3D.Pathfinding;
using UnityEngine;

namespace Nav3D.API
{
	public class Nav3DPath : IDisposable
	{
		private const string TARGETS_LIST_IS_EMPTY_ERROR = "Error. Targets array is empty!";

		private Path m_Path;

		private readonly Log m_Log;

		private Action<PathfindingResult> m_OnSuccess;

		private Action<PathfindingResult> m_OnFail;

		public bool IsValid { get; private set; }

		public bool IsPathfindingInProgress { get; private set; }

		public Vector3[] Trajectory => m_Path?.TrajectoryFinal;

		public Bounds? Bounds => m_Path?.Bounds;

		public PathfindingResult PathfindingResult => m_Path?.LastPathfindingResult;

		public event Action<PathfindingResult> OnPathfindingSuccess;

		public event Action<PathfindingResult> OnPathfindingFail;

		public event Action<PathfindingResult> OnPathfindingComplete;

		public Nav3DPath(Log _Log = null)
		{
			m_Log = _Log;
		}

		public Task<PathfindingResult> Find(Vector3 _Start, Vector3 _End, bool _Smooth = false, int _SmoothRatio = 3, Action<PathfindingResult> _OnSuccess = null, Action<PathfindingResult> _OnFail = null)
		{
			if (!Nav3DManager.Inited)
			{
				throw new Nav3DManager.Nav3DManagerNotInitializedException();
			}
			return RequestPathfinding(new Vector3[2] { _Start, _End }, _Loop: false, _SkipUnpassableTargets: false, _Smooth, _SmoothRatio, _OnSuccess, _OnFail);
		}

		public Task<PathfindingResult> Find(Vector3[] _Targets, bool _Loop, bool _SkipUnpassableTargets = false, bool _Smooth = false, int _SmoothRatio = 3, Action<PathfindingResult> _OnSuccess = null, Action<PathfindingResult> _OnFail = null, bool _UpdatePathSilently = false)
		{
			if (!Nav3DManager.Inited)
			{
				throw new Nav3DManager.Nav3DManagerNotInitializedException();
			}
			if (_Targets == null || !_Targets.Any())
			{
				throw new ArgumentException("Error. Targets array is empty!");
			}
			return RequestPathfinding(_Targets, _Loop, _SkipUnpassableTargets, _Smooth, _SmoothRatio, _OnSuccess, _OnFail, _UpdatePathSilently);
		}

		public Task<PathfindingResult> Find(PathfindingRequest _Request, Action<PathfindingResult> _OnSuccess = null, Action<PathfindingResult> _OnFail = null, bool _UpdatePathSilently = false)
		{
			if (!Nav3DManager.Inited)
			{
				throw new Nav3DManager.Nav3DManagerNotInitializedException();
			}
			if (_Request.Points == null || !_Request.Points.Any())
			{
				throw new ArgumentException("Error. Targets array is empty!");
			}
			return RequestPathfinding(_Request, _OnSuccess, _OnFail, _UpdatePathSilently);
		}

		public void Update()
		{
			m_Path?.Update();
		}

		public void Dispose()
		{
			IsValid = false;
			if (m_Path != null)
			{
				m_Path.OnPathfindingFinished -= OnPathfindingFinished;
				m_Path.Dispose();
				m_Path = null;
			}
		}

		public bool TryGetFollowData(Action<Vector3> _OnTargetPassed, Action _OnLastTargetReached, Vector3 _FollowerPosition, float _ReachDist, out PathFollowData _FollowData)
		{
			if (!IsValid)
			{
				_FollowData = null;
				return false;
			}
			return m_Path.GetFollowData(_OnTargetPassed, _OnLastTargetReached, _FollowerPosition, _ReachDist, out _FollowData);
		}

		private void InvokeOnPathfindingSuccess(PathfindingResult _Result)
		{
			OnPathfindingSuccess?.Invoke(_Result);
		}

		private void InvokeOnPathfindingFail(PathfindingResult _Result)
		{
			OnPathfindingFail?.Invoke(_Result);
		}

		private void InvokeOnPathfindingComplete(PathfindingResult _Result)
		{
			OnPathfindingComplete?.Invoke(_Result);
		}

		private Task<PathfindingResult> RequestPathfinding(Vector3[] _Targets, bool _Loop, bool _SkipUnpassableTargets, bool _Smooth = false, int _SmoothRatio = 3, Action<PathfindingResult> _OnSuccess = null, Action<PathfindingResult> _OnFail = null, bool _SilentUpdate = false)
		{
			IsPathfindingInProgress = true;
			m_OnSuccess = _OnSuccess;
			m_OnFail = _OnFail;
			if (m_Path == null)
			{
				m_Path = new Path(m_Log);
				m_Path.OnPathfindingFinished += OnPathfindingFinished;
			}
			return m_Path.Find(new PathfindingRequest(_Targets, _Loop, _SkipUnpassableTargets, _TryRepositionStartIfOccupied: false, _TryRepositionTargetIfOccupied: false, 2000, _Smooth, _SmoothRatio), _SilentUpdate);
		}

		private Task<PathfindingResult> RequestPathfinding(PathfindingRequest _Request, Action<PathfindingResult> _OnSuccess = null, Action<PathfindingResult> _OnFail = null, bool _UpdatePathSilently = false)
		{
			IsPathfindingInProgress = true;
			m_OnSuccess = _OnSuccess;
			m_OnFail = _OnFail;
			if (m_Path == null)
			{
				m_Path = new Path(m_Log);
				m_Path.OnPathfindingFinished += OnPathfindingFinished;
			}
			return m_Path.Find(_Request, _UpdatePathSilently);
		}

		private void CleanCallbacks()
		{
			m_OnSuccess = null;
			m_OnFail = null;
		}

		private void OnPathfindingFinished(PathfindingResult _Result)
		{
			IsPathfindingInProgress = false;
			if (_Result.Failed)
			{
				IsValid = false;
				if (m_OnFail != null)
				{
					ThreadDispatcher.BeginInvoke(m_OnFail, _Result);
					CleanCallbacks();
				}
				if (OnPathfindingFail != null)
				{
					ThreadDispatcher.BeginInvoke(InvokeOnPathfindingFail, _Result);
				}
			}
			else
			{
				IsValid = true;
				if (m_OnSuccess != null)
				{
					ThreadDispatcher.BeginInvoke(m_OnSuccess, _Result);
					CleanCallbacks();
				}
				if (OnPathfindingSuccess != null)
				{
					ThreadDispatcher.BeginInvoke(InvokeOnPathfindingSuccess, _Result);
				}
			}
			if (OnPathfindingComplete != null)
			{
				ThreadDispatcher.BeginInvoke(InvokeOnPathfindingComplete, _Result);
			}
		}
	}
}
