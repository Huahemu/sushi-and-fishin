using System.Linq;
using Nav3D.API;
using UnityEngine;

namespace Nav3D.Agents
{
	public record AgentPathfindingRequest : PathfindingRequest
	{
		private Vector3 AgentPosition { get; }

		public AgentPathfindingRequest(Vector3[] _Points, Vector3 _AgentPosition, bool _Loop = false, bool _SkipUnpassableTargets = false, bool _TryRepositionStartIfOccupied = false, bool _TryRepositionTargetIfOccupied = false, int _Timeout = 2000, bool _Smooth = false, int _SmoothRatio = 3, string _RequesterID = null)
			: base(_Points, _Loop, _SkipUnpassableTargets, _TryRepositionStartIfOccupied, _TryRepositionTargetIfOccupied, _Timeout, _Smooth, _SmoothRatio, _RequesterID)
		{
			AgentPosition = _AgentPosition;
		}

		public override Vector3[] GetExtendedPoints()
		{
			return new Vector3[1] { AgentPosition }.Concat(base.Points).ToArray();
		}
	}
}
