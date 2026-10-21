using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nav3D.API;
using Nav3D.Common;
using Nav3D.Obstacles;
using Nav3D.Pathfinding;
using UnityEngine;

namespace Nav3D.Agents
{
	public class Nav3DAgentMoverGlobal : Nav3DAgentMover
	{
		private readonly string LOG_BEGIN_MOVE_TO_POINT = "Nav3DAgentMoverGlobal.BeginMoveTo: {0}";

		private readonly string LOG_BEGIN_MOVE_TO_POINTS = "Nav3DAgentMoverGlobal.BeginMoveToPoints: {0}";

		private readonly string LOG_BEGIN_MOVE_TO_WITH_EXISTING_PATH = "Nav3DAgentMoverGlobal.MoveToUsingExistingPath: {0}";

		private readonly string LOG_RESTART_FOLLOWING = "Nav3DAgentMoverGlobal.ReinitPathFollowingData";

		private readonly string LOG_REINIT_PATH = "Nav3DAgentMoverGlobal.RestartFollowingForCurrentPath";

		private readonly string LOG_PATH_UPDATE_SUCCESS = "Nav3DAgentMoverGlobal.BeginPathFollowing";

		private readonly string LOG_PATH_UPDATE_FAIL = "Nav3DAgentMoverGlobal.OnPathfindingFailed: Path.UpdatePath: Fail: {0}";

		private Vector3[] m_LastSucceededFoundPath;

		protected Nav3DPath m_Path;

		protected PathFollowData m_CurrFollowData;

		protected Vector3 m_PathNextPoint;

		protected Action<PathfindingResult> m_OnPathfindingFail;

		public override bool Avoiding => false;

		public override Vector3[] CurrentPath => m_Path?.Trajectory;

		public Nav3DAgentMoverGlobal(Vector3 _Position, Nav3DAgentConfig _Config, Nav3DAgent _Agent, Log _Log)
			: base(_Position, _Config, _Agent, _Log)
		{
		}

		public override void BeginMoveTo(PathfindingRequest _Request, Nav3DPath _Path, Action<PathfindingResult> _OnPathUpdated, Action<PathfindingResult> _OnPathfindingFail = null)
		{
			m_Log?.WriteFormat(LOG_BEGIN_MOVE_TO_POINT, UtilsCommon.GetPointsString(_Request.Points));
			m_Request = _Request;
			m_Path = _Path;
			m_OnPathUpdated = _OnPathUpdated;
			m_OnPathfindingFail = _OnPathfindingFail;
			m_Path.OnPathfindingSuccess += BeginPathFollowing;
			BeginPathFollowing(_Path.PathfindingResult);
		}

		public override void BeginMoveToPoints(PathfindingRequest _Request, Nav3DPath _Path, Action<PathfindingResult> _OnPathUpdated, Action<Vector3> _OnTargetPointPassed = null, Action _OnLastPointReached = null, Action<PathfindingResult> _OnPathfindingFail = null)
		{
			m_Log?.WriteFormat(LOG_BEGIN_MOVE_TO_POINTS, UtilsCommon.GetPointsString(_Request.Points));
			m_Request = _Request;
			m_Path = _Path;
			m_OnTargetPassed = _OnTargetPointPassed;
			m_OnLastTargetReached = _OnLastPointReached;
			m_OnPathUpdated = _OnPathUpdated;
			m_OnPathfindingFail = _OnPathfindingFail;
			m_Path.OnPathfindingSuccess += BeginPathFollowing;
			BeginPathFollowing(_Path.PathfindingResult);
		}

		public override void MoveToUsingExistingPath(PathfindingRequest _Request, Action<PathfindingResult> _OnPathUpdated, Action<PathfindingResult> _OnPathfindingFail = null)
		{
			m_Log?.WriteFormat(LOG_BEGIN_MOVE_TO_WITH_EXISTING_PATH, UtilsCommon.GetPointsString(_Request.Points));
			m_Path?.Find(_Request, _OnPathUpdated, _OnPathfindingFail, _UpdatePathSilently: true);
		}

		public override void ReinitPathFollowingData()
		{
			m_Log?.Write(LOG_RESTART_FOLLOWING);
			m_CurrFollowData.RestartFollowing(base.CurrentPosition, m_Config.TargetReachDistance);
		}

		public override void RestartFollowingForCurrentPath()
		{
			m_Log?.Write(LOG_REINIT_PATH);
			RestartPathFollowing(null, _SkipCurrentPositionAddition: true);
		}

		public override void DisposePath()
		{
			if (m_Path != null)
			{
				m_Path.Dispose();
				m_Path = null;
			}
		}

		public override void Uninitialize()
		{
			base.Uninitialize();
			DisposePath();
		}

		public override string GetInnerStatusString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("-------MOVER STATUS-------");
			stringBuilder.AppendLine("Mover type: Nav3DAgentMoverGlobal");
			stringBuilder.AppendLine("-------FOLLOW DATA STATUS-------");
			if (m_CurrFollowData == null)
			{
				stringBuilder.AppendLine("m_CurrFollowData: null");
			}
			else
			{
				stringBuilder.AppendLine($"m_CurrFollowData: {m_CurrFollowData.GetHashCode()}, IsValid: {m_CurrFollowData.IsValid}");
				stringBuilder.AppendLine(m_CurrFollowData.GetNextTargetInfo());
			}
			stringBuilder.AppendLine("-------PATHFINDING STATUS-------");
			string value = ((m_Path == null) ? "m_Path: null" : $"m_Path:\n\t{m_Path.GetHashCode()},\n\tIsValid: {m_Path.IsValid},\n\tIsPathfindingInProgress: {m_Path.IsPathfindingInProgress}");
			stringBuilder.AppendLine(value);
			return stringBuilder.ToString();
		}

		public override string GetLastPathfindingStatsString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			if (m_Path == null)
			{
				stringBuilder.AppendLine("m_Path: null");
				return stringBuilder.ToString();
			}
			if (m_Path.PathfindingResult == null)
			{
				stringBuilder.AppendLine("PathfindingResult: null");
				return stringBuilder.ToString();
			}
			stringBuilder.Append(m_Path.PathfindingResult.GetLastPathfindingStatsString());
			return stringBuilder.ToString();
		}

		private void BeginPathFollowing(PathfindingResult _Result)
		{
			m_Log?.Write(LOG_PATH_UPDATE_SUCCESS);
			Nav3DPath path = m_Path;
			if (path != null && path.IsValid)
			{
				m_LastSucceededFoundPath = _Result.PathFinal;
				m_OnPathUpdated(_Result);
				if (m_Path.TryGetFollowData(OnTargetPointPassed, m_OnLastTargetReached, m_CurrentPosition, m_Config.TargetReachDistance, out var _FollowData))
				{
					m_CurrFollowData = _FollowData;
				}
			}
		}

		private void OnPathfindingFailed(PathfindingResult _Result)
		{
			m_Log?.WriteFormat(LOG_PATH_UPDATE_FAIL, _Result.ErrorMessage);
			if (_Result.Result == PathfindingResultCode.START_POINT_INSIDE_OBSTACLE && m_LastSucceededFoundPath.Any())
			{
				Vector3 closestPointOnCurve = UtilsMath.GetClosestPointOnCurve(m_LastSucceededFoundPath, m_CurrentPosition);
				if (!ObstacleManager.Instance.IsPointInsideObstacle(closestPointOnCurve))
				{
					RestartPathFollowing(closestPointOnCurve, _SkipCurrentPositionAddition: true);
					return;
				}
			}
			m_OnPathfindingFail?.Invoke(_Result);
		}

		protected void RestartPathFollowing(Vector3? _StartPoint = null, bool _SkipCurrentPositionAddition = false)
		{
			List<Vector3> list = new List<Vector3>(m_Request.GetInitialPoints());
			List<Vector3> list2 = m_CurrFollowData.GetUnpassedTargets(m_Request.Loop).ToList();
			for (int i = 0; i < list2.Count; i++)
			{
				int num = list.IndexOf(list2[i]);
				if (num > -1)
				{
					list.RemoveAt(num);
				}
				else
				{
					list2.RemoveAt(i);
				}
			}
			if (_StartPoint.HasValue)
			{
				list2.Insert(0, _StartPoint.Value);
			}
			if (list2.Count == 1 || !_SkipCurrentPositionAddition)
			{
				list2.Insert(0, m_CurrentPosition);
			}
			m_Path.Find(m_Request with
			{
				Points = list2.ToArray()
			}, null, null, _UpdatePathSilently: true);
		}

		protected virtual void OnTargetPointPassed(Vector3 _Point)
		{
			m_OnTargetPassed?.Invoke(_Point);
		}

		protected override Vector3 GetVelocity()
		{
			if (m_Path == null || !m_Path.IsValid || m_CurrFollowData == null || !m_CurrFollowData.IsValid)
			{
				return base.LastVelocity = Vector3.zero;
			}
			m_PathNextPoint = m_CurrFollowData.GetMovePoint(m_Config.Speed, m_Config.Radius, m_CurrentPosition);
			return base.LastVelocity = (m_PathNextPoint - m_CurrentPosition).normalized * m_Config.Speed;
		}
	}
}
