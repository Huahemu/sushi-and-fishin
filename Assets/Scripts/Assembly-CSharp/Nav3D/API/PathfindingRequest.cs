using System;
using UnityEngine;

namespace Nav3D.API
{
	public record PathfindingRequest
	{
		public Vector3[] Points { get; set; }

		public bool Loop { get; set; }

		public bool SkipUnpassableTargets { get; set; }

		public bool TryRepositionStartIfOccupied { get; set; }

		public bool TryRepositionTargetIfOccupied { get; set; }

		public int Timeout { get; set; }

		public bool Smooth { get; set; }

		public int SmoothRatio { get; set; }

		public string RequesterID { get; set; }

		protected const int DEFAULT_SEARCH_TIMEOUT = 2000;

		protected const int DEFAULT_SMOOTH_RATIO = 3;

		public PathfindingRequest(Vector3[] _Points, bool _Loop = false, bool _SkipUnpassableTargets = false, bool _TryRepositionStartIfOccupied = false, bool _TryRepositionTargetIfOccupied = false, int _Timeout = 2000, bool _Smooth = false, int _SmoothRatio = 3, string _RequesterID = null)
		{
			Points = _Points;
			Loop = _Loop;
			SkipUnpassableTargets = _SkipUnpassableTargets;
			TryRepositionStartIfOccupied = _TryRepositionStartIfOccupied;
			TryRepositionTargetIfOccupied = _TryRepositionTargetIfOccupied;
			Timeout = _Timeout;
			Smooth = _Smooth;
			SmoothRatio = _SmoothRatio;
			RequesterID = _RequesterID ?? Guid.NewGuid().ToString();
		}

		public Vector3[] GetInitialPoints()
		{
			return Points;
		}

		public virtual Vector3[] GetExtendedPoints()
		{
			return Points;
		}
	}
}
