using System.Collections.Generic;
using System.Linq;
using Nav3D.API;
using UnityEngine;

namespace Nav3D.Tutorials
{
	public class WaypointAgent : MonoBehaviour
	{
		[SerializeField]
		private List<Transform> m_TargetPoints;

		private Nav3DAgent m_Agent;

		private void Awake()
		{
			Nav3DManager.OnNav3DInit += Init;
		}

		private void Init()
		{
			m_Agent = GetComponent<Nav3DAgent>();
			m_Agent.MoveToPoints(m_TargetPoints.Select((Transform _Point) => _Point.position).ToArray(), _Loop: true, _StartFromClosest: false, _SkipUnpassableTargets: false, null, null, () =>
			{
				Object.Destroy(base.gameObject);
			});
		}
	}
}
