using System;
using System.Collections.Generic;
using System.Text;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.API
{
	public class PathfindingResult
	{
		private const string FAIL_TIMEOUT = "Pathfinding for points {0} failed;\nReason = TIMEOUT: {1} ms was taken, time limit is: {2} ms";

		private const string FAIL_NO_PATH = "Pathfinding for points {0} failed;\nReason = PATH DOES NOT EXIST: {1} ms was taken";

		private const string FAIL_CANCELLED = "Pathfinding for points {0} failed;\nReason = CANCELLED BY EXTERNAL CONTROLLER: {1} ms was taken";

		private const string FAIL_START_INSIDE_OBSTACLE = "Pathfinding for points {0} failed;\nReason = START POINT IS INSIDE THE OBSTACLE: {1} ms was taken";

		private const string FAIL_TARGET_INSIDE_OBSTACLE = "Pathfinding for points {0} failed;\nReason = ONE OF THE POINTS IS INSIDE THE OBSTACLE; Failed point info {1}: {2} ms was taken";

		private const string FAIL_UNKNOWN = "Pathfinding for points {0} canceled;\nReason = UNKNOWN: {1} ms was taken";

		private readonly List<(Vector3 Point, PathfindingResultCode Code)> m_TargetResultCodes;

		private readonly List<(Vector3 Point, TimeSpan Duration)> m_FragmentPathfindingDurations;

		public TimeSpan PathfindingDuration { get; private set; }

		public TimeSpan OptimizingDuration { get; private set; }

		public TimeSpan SmoothingDuration { get; private set; }

		public Vector3[] PathRaw { get; }

		public Vector3[] PathOptimized { get; }

		public Vector3[] PathFinal { get; }

		public int[] TargetIndices { get; }

		public PathfindingResultCode Result { get; }

		public bool Failed => Result != PathfindingResultCode.SUCCEEDED;

		public string ErrorMessage { get; private set; }

		public PathfindingResult(Vector3[] _PathRaw, Vector3[] _PathOptimized, Vector3[] _Smoothed, TimeSpan _PathfindingDuration, TimeSpan _OptimizingDuration, TimeSpan _SmoothingDuration, int[] _TargetIndices, List<(Vector3, PathfindingResultCode)> _TargetResultCodes, List<(Vector3, TimeSpan)> _FragmentPathfindingDurations)
		{
			PathRaw = _PathRaw;
			PathFinal = _Smoothed;
			PathOptimized = _PathOptimized;
			PathfindingDuration = _PathfindingDuration;
			OptimizingDuration = _OptimizingDuration;
			SmoothingDuration = _SmoothingDuration;
			TargetIndices = _TargetIndices;
			m_TargetResultCodes = _TargetResultCodes;
			m_FragmentPathfindingDurations = _FragmentPathfindingDurations;
			Result = PathfindingResultCode.SUCCEEDED;
		}

		public PathfindingResult(Vector3[] _Points, TimeSpan _PathfindingDuration, int _Timeout, List<(Vector3, PathfindingResultCode)> _TargetResultCodes, List<(Vector3, TimeSpan)> _FragmentPathfindingDurations, PathfindingResultCode _ResultCode)
		{
			m_TargetResultCodes = _TargetResultCodes;
			m_FragmentPathfindingDurations = _FragmentPathfindingDurations;
			Result = _ResultCode;
			ErrorMessage = _ResultCode switch
			{
				PathfindingResultCode.PATH_DOES_NOT_EXIST => $"Pathfinding for points {UtilsCommon.GetPointsString(_Points)} failed;\nReason = PATH DOES NOT EXIST: {_PathfindingDuration.TotalMilliseconds} ms was taken", 
				PathfindingResultCode.TIMEOUT => $"Pathfinding for points {UtilsCommon.GetPointsString(_Points)} failed;\nReason = TIMEOUT: {_PathfindingDuration.TotalMilliseconds} ms was taken, time limit is: {_Timeout} ms", 
				PathfindingResultCode.CANCELLED => $"Pathfinding for points {UtilsCommon.GetPointsString(_Points)} failed;\nReason = CANCELLED BY EXTERNAL CONTROLLER: {_PathfindingDuration.TotalMilliseconds} ms was taken", 
				PathfindingResultCode.START_POINT_INSIDE_OBSTACLE => $"Pathfinding for points {UtilsCommon.GetPointsString(_Points)} failed;\nReason = START POINT IS INSIDE THE OBSTACLE: {_PathfindingDuration.TotalMilliseconds} ms was taken", 
				PathfindingResultCode.TARGET_POINT_INSIDE_OBSTACLE => $"Pathfinding for points {UtilsCommon.GetPointsString(_Points)} failed;\nReason = ONE OF THE POINTS IS INSIDE THE OBSTACLE; Failed point info {GetFailedPointInfo()}: {_PathfindingDuration.TotalMilliseconds} ms was taken", 
				PathfindingResultCode.UNKNOWN => $"Pathfinding for points {UtilsCommon.GetPointsString(_Points)} canceled;\nReason = UNKNOWN: {_PathfindingDuration.TotalMilliseconds} ms was taken", 
			};
		}

		public string GetFailedPointInfo()
		{
			int num = m_TargetResultCodes.FindIndex(((Vector3 Point, PathfindingResultCode Code) _Pair) => _Pair.Code != PathfindingResultCode.SUCCEEDED);
			if (num == -1)
			{
				return string.Empty;
			}
			return $"Target index: {num}, Position: {m_TargetResultCodes[num].Point.ToStringExt()}, Code: {m_TargetResultCodes[num].Code};";
		}

		public string GetLastPathfindingStatsString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine(string.Format("{0}: {1}", "PathfindingResultCode", Result));
			if (Result != PathfindingResultCode.SUCCEEDED)
			{
				return stringBuilder.ToString();
			}
			stringBuilder.AppendLine("Pathfinding stages duration in ms:");
			stringBuilder.AppendLine(string.Format("{0}: {1}", "PathfindingDuration", PathfindingDuration.Milliseconds));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "OptimizingDuration", OptimizingDuration.Milliseconds));
			stringBuilder.AppendLine(string.Format("{0}: {1}", "SmoothingDuration", SmoothingDuration.Milliseconds));
			stringBuilder.AppendLine("Targets stats:");
			int num = 0;
			List<(Vector3, PathfindingResultCode)> list = m_TargetResultCodes.Copy();
			List<(Vector3, TimeSpan)> list2 = m_FragmentPathfindingDurations.Copy();
			for (int i = 0; i < TargetIndices.Length; i++)
			{
				int num2 = TargetIndices[i];
				Vector3 target = PathFinal[num2];
				int num3 = list.FindIndex(((Vector3, PathfindingResultCode) _Data) => _Data.Item1 == target);
				if (num3 < 0)
				{
					break;
				}
				(Vector3, PathfindingResultCode) tuple = list[num3];
				list.RemoveAt(num3);
				TimeSpan item = list2[num3].Item2;
				list2.RemoveAt(num3);
				stringBuilder.AppendLine($"[{i}]: [{target.ToStringExt()}], CODE: {tuple.Item2}, DURATION (ms): {item.TotalMilliseconds}");
				num++;
			}
			return stringBuilder.ToString();
		}
	}
}
