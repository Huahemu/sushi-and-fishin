using UnityEngine;
using UnityEngine.AI;

namespace SbbUtutuya
{
	[RequireComponent(typeof(NavMeshAgent))]
	public class SushiController : MonoBehaviour
	{
		[SerializeField]
		private Transform[] m_wayPoints;

		[SerializeField]
		private Transform m_firstWayPoint;

		private NavMeshAgent m_navMeshAgent;

		private int m_nextWayPointId;

		private void Start()
		{
			m_navMeshAgent = GetComponent<NavMeshAgent>();
			if (m_wayPoints.Length == 0)
			{
				return;
			}
			if ((bool)m_firstWayPoint)
			{
				for (int i = 0; i < m_wayPoints.Length; i++)
				{
					if (m_firstWayPoint == m_wayPoints[i])
					{
						m_nextWayPointId = i;
						break;
					}
				}
			}
			else
			{
				m_nextWayPointId = 0;
			}
			m_navMeshAgent.SetDestination(m_wayPoints[m_nextWayPointId].position);
		}

		private void Update()
		{
			if (m_wayPoints.Length != 0 && Vector3.Distance(base.transform.position, m_wayPoints[m_nextWayPointId].position) < 0.5f)
			{
				m_nextWayPointId++;
				if (m_nextWayPointId >= m_wayPoints.Length)
				{
					m_nextWayPointId = 0;
				}
				m_navMeshAgent.SetDestination(m_wayPoints[m_nextWayPointId].position);
			}
		}
	}
}
