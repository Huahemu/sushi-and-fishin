using System;
using System.Collections.Generic;
using Nav3D.API;
using Nav3D.Obstacles;
using UnityEngine;

namespace Nav3D.Dev
{
	public class PathfindingResultDebug : PathfindingResult
	{
		public List<Leaf> PathfindingHistory { get; private set; }

		public List<Leaf> PathNodes { get; private set; }

		public PathfindingResultDebug(Vector3[] _Path, Vector3[] _PathOptimized, Vector3[] _Smoothed, TimeSpan _PathfindingDuration, TimeSpan _OptimizingDuration, TimeSpan _SmoothingDuration, List<Leaf> _PathfindingHistory, List<Leaf> _PathNodes)
			: base(_Path, _PathOptimized, _Smoothed, _PathfindingDuration, _OptimizingDuration, _SmoothingDuration, new int[0], new List<(Vector3, PathfindingResultCode)>(), new List<(Vector3, TimeSpan)>())
		{
			PathfindingHistory = _PathfindingHistory;
			PathNodes = _PathNodes;
		}

		public PathfindingResultDebug(PathfindingResultCode _ResultCode)
			: base(new Vector3[0], TimeSpan.Zero, 0, new List<(Vector3, PathfindingResultCode)>(), new List<(Vector3, TimeSpan)>(), _ResultCode)
		{
		}
	}
}
