using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.AI;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentWaypointResolver
	{
		private readonly Transform _startPoint;

		private readonly Vector3 _homePosition;

		private readonly Quaternion _homeRotation;

		private readonly bool _useExplicitHome;

		private readonly float _sampleRadius;

		private readonly bool _allowExplicitZero;
        private ResidentController _actor;
        private NavMeshAgent _agent;
        public void BindActor(ResidentController actor){_actor=actor;_agent=actor?actor.GetComponent<NavMeshAgent>():null;}
        private NavMeshQueryFilter QueryFilter=>new NavMeshQueryFilter{agentTypeID=_agent?_agent.agentTypeID:0,areaMask=_agent?_agent.areaMask:-1};

		public ResidentWaypointResolver(Transform startPoint, float sampleRadius = 2f, bool allowExplicitZero = false)
		{
			_startPoint = startPoint;
			_sampleRadius = Mathf.Max(0.05f, sampleRadius);
			_allowExplicitZero = allowExplicitZero;
		}

		public ResidentWaypointResolver(Vector3 homePosition, Quaternion homeRotation, float sampleRadius = 2f, bool allowExplicitZero = false)
		{
			_homePosition = homePosition;
			_homeRotation = homeRotation;
			_useExplicitHome = true;
			_sampleRadius = Mathf.Max(0.05f, sampleRadius);
			_allowExplicitZero = allowExplicitZero;
		}

		public bool ResolveHome(out Vector3 position, out Vector3 rotation)
		{
			position = Vector3.zero;
			rotation = Vector3.zero;
            // Housing updates the existing binding; do not cache a cloned
            // template's immutable home across moves, upgrades or co-living.
            var owned=_actor?_actor.GetComponent<SushiAndFish.FriendsLife.FriendResidentBindingState>():null;
            if(owned&&owned.hasHome){position=owned.homePosition;rotation=owned.homeRotation.eulerAngles;return ValidateTarget(position,out position);}
			if (_useExplicitHome)
			{
				position = _homePosition;
				rotation = _homeRotation.eulerAngles;
				return ValidateTarget(position, out position);
			}
			if (_startPoint == null)
			{
				return false;
			}
			position = _startPoint.position;
			rotation = _startPoint.rotation.eulerAngles;
			return ValidateTarget(position, out position);
		}

		public bool ResolveScheduleTarget(ResidentActivityType activity, WayPointIndex index, out Vector3 position, out Vector3 rotation)
		{
			position = Vector3.zero;
			rotation = Vector3.zero;
			if (activity == ResidentActivityType.Home)
			{
				return ResolveHome(out position, out rotation);
			}
			if (WaypointDirectory.TryGetAnchor(activity, index, out var anchor) && anchor != null)
			{
				rotation = anchor.transform.rotation.eulerAngles;
                if (_agent ? anchor.TryGetActivityApproach(_agent,out position,out var authored) : anchor.TryGetNavMeshPosition(out position))
				{
					if (anchor.UseAuthoredPose)
					{
						position = anchor.transform.position;
					}
					return true;
				}
				return false;
			}
			if (!WaypointDirectory.TryGet(activity, index, out var target) || target == null)
			{
				UtilityMethods.Log($"[Resident] Missing waypoint. Activity={activity}, Index={index}", LogType.Warning);
				return false;
			}
			position = target.position;
			rotation = target.rotation.eulerAngles;
			return ValidateTarget(position, out position);
		}

		public bool ValidateTarget(Vector3 candidate, out Vector3 navMeshPosition)
		{
			navMeshPosition = candidate;
			if (float.IsNaN(candidate.x) || float.IsNaN(candidate.y) || float.IsNaN(candidate.z)||float.IsInfinity(candidate.x)||float.IsInfinity(candidate.y)||float.IsInfinity(candidate.z))
			{
				return false;
			}
			if (!_allowExplicitZero && candidate == Vector3.zero)
			{
				return false;
			}
            NavMeshHit hit;
            bool found=_agent?NavMesh.SamplePosition(candidate,out hit,_sampleRadius,QueryFilter):NavMesh.SamplePosition(candidate,out hit,_sampleRadius,-1);
			if (found)
			{
				navMeshPosition = hit.position;
				return true;
			}
			return false;
		}
	}
}
