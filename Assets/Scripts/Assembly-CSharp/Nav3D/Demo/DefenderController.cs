using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Nav3D.API;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.Demo
{
	[RequireComponent(typeof(Nav3DAgent))]
	public class DefenderController : MonoBehaviour
	{
		private const float ENEMY_DETECTION_DISTANCE = 3f;

		private const float ENEMIES_CHECK_PERIOD = 0.4f;

		private Nav3DAgent m_Agent;

		private int m_CurWaypointIndex;

		private int m_WaypointsCount;

		private List<Transform> m_Waypoints;

		private Coroutine m_EnemiesCheckRoutine;

		public void Init(Transform[] _Waypoints, int _StartIndex)
		{
			m_Waypoints = _Waypoints.ToList();
			m_CurWaypointIndex = _StartIndex;
			m_WaypointsCount = m_Waypoints.Count;
			int index = ((m_CurWaypointIndex - 1 < 0) ? (m_Waypoints.Count - 1) : (m_CurWaypointIndex - 1));
			base.transform.position = m_Waypoints[index].position;
			MoveToNextWaypoint();
			StartCheckEnemiesRoutine();
		}

		private void MoveToNextWaypoint()
		{
			if (m_CurWaypointIndex < m_WaypointsCount)
			{
				m_Agent.MoveTo(m_Waypoints[m_CurWaypointIndex].position, MoveToNextWaypoint, null, (PathfindingResult _Error) =>
				{
					ContinueWaypointsFollowing(_Error);
				});
				m_CurWaypointIndex++;
			}
			else
			{
				m_CurWaypointIndex = 0;
				MoveToNextWaypoint();
			}
		}

		private void StartCheckEnemiesRoutine()
		{
			if (m_EnemiesCheckRoutine != null)
			{
				StopCoroutine(m_EnemiesCheckRoutine);
			}
			m_EnemiesCheckRoutine = StartCoroutine(CheckEnemiesRoutine());
		}

		private void ContinueWaypointsFollowing(PathfindingResult? _Result = null)
		{
			if ((_Result != null && _Result.Result == PathfindingResultCode.START_POINT_INSIDE_OBSTACLE) || (_Result != null && _Result.Result == PathfindingResultCode.TARGET_POINT_INSIDE_OBSTACLE))
			{
				base.transform.position = m_Waypoints[Random.Range(0, m_Waypoints.Count)].position;
			}
			Transform transform = m_Waypoints.MinBy((Transform _Waypoint) => (_Waypoint.position - m_Agent.Transform.position).sqrMagnitude);
			m_CurWaypointIndex = m_Waypoints.IndexOf(transform);
			m_Agent.MoveTo(transform.position, MoveToNextWaypoint, null, (PathfindingResult _) =>
			{
				MoveToNextWaypoint();
			});
			StartCheckEnemiesRoutine();
		}

		private void FollowRandomWaypointSet()
		{
			m_Agent.MoveToPoints(new Vector3[3]
			{
				m_Waypoints[Random.Range(0, m_Waypoints.Count)].position,
				m_Waypoints[Random.Range(0, m_Waypoints.Count)].position,
				m_Waypoints[Random.Range(0, m_Waypoints.Count)].position
			}, _Loop: false, _StartFromClosest: false, _SkipUnpassableTargets: false, null, null, () =>
			{
				ContinueWaypointsFollowing();
			});
			StartCheckEnemiesRoutine();
		}

		private IEnumerator CheckEnemiesRoutine()
		{
			List<EnemyController> list;
			while (true)
			{
				Nav3DAgent[] agentsInRadius = m_Agent.GetAgentsInRadius(3f);
				list = new List<EnemyController>();
				Nav3DAgent[] array = agentsInRadius;
				for (int i = 0; i < array.Length; i++)
				{
					EnemyController component = array[i].GetComponent<EnemyController>();
					if (component != null && !component.IsVictim)
					{
						list.Add(component);
					}
				}
				if (list.Any())
				{
					break;
				}
				yield return new WaitForSeconds(0.4f);
			}
			EnemyController enemy = list.MinBy((EnemyController _Enemy) => (_Enemy.transform.position - m_Agent.Transform.position).sqrMagnitude);
			enemy.MarkAsVictim();
			m_Agent.FollowTarget(enemy.transform, _FollowContinuously: false, 0.125f, m_Agent.Config.Radius * 1.5f, () =>
			{
				if (enemy != null)
				{
					enemy.Destroy();
				}
				if (Random.Range(0, 2) == 0)
				{
					ContinueWaypointsFollowing();
				}
				else
				{
					FollowRandomWaypointSet();
				}
			}, () =>
			{
				ContinueWaypointsFollowing();
			}, null, ContinueWaypointsFollowing);
		}

		private void Awake()
		{
			m_Agent = GetComponent<Nav3DAgent>();
		}
	}
}
