using System.Linq;
using Nav3D.Agents;
using Nav3D.Common;
using Nav3D.LocalAvoidance;
using UnityEngine;

namespace Nav3D.API
{
	internal static class Nav3DAgentManager
	{
		public static Nav3DAgent[] GetAgentsInBounds(Bounds _Bounds)
		{
			if (!Nav3DManager.Inited)
			{
				throw new Nav3DManager.Nav3DManagerNotInitializedException();
			}
			return (from _Movable in AgentManager.Instance.GetMovablesInBounds(_Bounds)
				where _Movable is Nav3DAgentMover
				select ((Nav3DAgentMover)_Movable).Agent).ToArray();
		}

		public static Nav3DAgent[] GetAgentsInSphere(Vector3 _Center, float _Radius)
		{
			if (!Nav3DManager.Inited)
			{
				throw new Nav3DManager.Nav3DManagerNotInitializedException();
			}
			float num = _Radius * 2f;
			float sqrRadius = _Radius * _Radius;
			return (from _Movable in AgentManager.Instance.GetMovablesInBounds(new Bounds(_Center, new Vector3(num, num, num)))
				where _Movable is Nav3DAgentMover && (_Movable.GetPosition() - _Center).sqrMagnitude <= sqrRadius
				select ((Nav3DAgentMover)_Movable).Agent).ToArray();
		}
	}
}
