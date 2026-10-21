using System;
using System.Collections;
using System.Collections.Generic;
using Nav3D.API;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.Demo
{
	public class EvadersScenario : MonoBehaviour
	{
		private const int EVADERS_COUNT_LIMIT = 200;

		private const float SPAWN_DELAY = 0.3f;

		[Header("Game entities")]
		[SerializeField]
		private Nav3DObstacle m_Obstacle;

		[Space]
		[Header("Agent configs")]
		[SerializeField]
		private Nav3DAgentConfig m_AgentConfig;

		[Space]
		[Header("Prefabs")]
		[SerializeField]
		private GameObject m_EvaderPrefab;

		[Space]
		[Header("Spawn X boundaries")]
		[SerializeField]
		private Transform m_LeftSpawnBorder;

		[SerializeField]
		private Transform m_RightSpawnBorder;

		[Space]
		[Header("Spawn Y and Z constraints")]
		[SerializeField]
		private float m_YMin;

		[SerializeField]
		private float m_YMax;

		[SerializeField]
		private float m_ZMin;

		[SerializeField]
		private float m_ZMax;

		[Space]
		[Header("Debug draw")]
		[SerializeField]
		private bool m_VisualizeAgents;

		private readonly List<Nav3DAgent> m_Agents = new List<Nav3DAgent>();

		private int m_EvadersCountCurrent;

		private Transform m_EvadersRoot;

		private void Start()
		{
			Nav3DManager.OnNav3DInit += () =>
			{
				m_EvadersRoot = new GameObject("EvadersRoot").transform;
				StartCoroutine(SpawnAgents(0.3f));
				StartCoroutine(CheckEvadersCount());
			};
			m_Obstacle.OnObstacleAdded += () =>
			{
				Debug.Log(m_Obstacle.AdditionProgress.GetResultStats());
			};
		}

		private void SpawnEvader()
		{
			GameObject evaderGO = UnityEngine.Object.Instantiate(m_EvaderPrefab, GetEvadersPosition(), Quaternion.identity, m_EvadersRoot);
			Nav3DEvader component = evaderGO.GetComponent<Nav3DEvader>();
			component.OnPositionChanged = (Action<Vector3>)Delegate.Combine(component.OnPositionChanged, (Action<Vector3>)((Vector3 _Position) =>
			{
				if (_Position.x > m_RightSpawnBorder.position.x || _Position.x < m_LeftSpawnBorder.position.x || _Position.y > m_YMax || _Position.y < m_YMin || _Position.z > m_ZMax || _Position.z < m_ZMin)
				{
					m_EvadersCountCurrent--;
					UnityEngine.Object.Destroy(evaderGO);
				}
			}));
			float randomUniformValue = UtilsMath.GetRandomUniformValue(0.1f, 0.5f);
			float num = randomUniformValue * 2f;
			component.Radius = randomUniformValue;
			evaderGO.transform.localScale = new Vector3(num, num, num);
			m_EvadersCountCurrent++;
		}

		private Vector3 GetEvadersPosition()
		{
			Vector3 vector;
			do
			{
				vector = new Vector3(UnityEngine.Random.Range(m_LeftSpawnBorder.position.x, m_RightSpawnBorder.position.x), UnityEngine.Random.Range(m_YMin, m_YMax), UnityEngine.Random.Range(m_ZMin, m_ZMax));
			}
			while (Nav3DManager.IsPointInsideOccupiedVolume(vector));
			return vector;
		}

		private IEnumerator CheckEvadersCount()
		{
			while (true)
			{
				if (m_EvadersCountCurrent < 200)
				{
					SpawnEvader();
				}
				else
				{
					yield return new WaitForSeconds(2f);
				}
			}
		}

		private IEnumerator SpawnAgents(float _Delay)
		{
			GameObject agentsRoot = new GameObject("AgentsRoot");
			int i = 0;
			while (true)
			{
				Nav3DAgentConfig nav3DAgentConfig = m_AgentConfig.Copy();
				nav3DAgentConfig.Radius = UtilsMath.GetRandomNormalValue(0.15f, 0.35f);
				GameObject agentGO = DemoHelper.InstantiateAgentConeBody($"agent_{i}", nav3DAgentConfig.Radius, out var _);
				agentGO.transform.SetParent(agentsRoot.transform);
				Nav3DAgent nav3DAgent = agentGO.AddComponent<Nav3DAgent>();
				m_Agents.Add(nav3DAgent);
				nav3DAgent.SetConfig(nav3DAgentConfig);
				Vector3 agentPosition = GetAgentPosition(out var _Left);
				nav3DAgent.transform.position = agentPosition;
				nav3DAgent.MoveTo(new Vector3(_Left ? m_RightSpawnBorder.position.x : m_LeftSpawnBorder.position.x, agentPosition.y, agentPosition.z), () =>
				{
					UnityEngine.Object.Destroy(agentGO);
				}, null, (PathfindingResult _) =>
				{
					UnityEngine.Object.Destroy(agentGO);
				});
				i++;
				yield return new WaitForSeconds(_Delay);
			}
		}

		private Vector3 GetAgentPosition(out bool _Left)
		{
			_Left = UnityEngine.Random.Range(0, 2) == 0;
			return new Vector3(_Left ? m_LeftSpawnBorder.position.x : m_RightSpawnBorder.position.x, UnityEngine.Random.Range(m_YMin, m_YMax), UnityEngine.Random.Range(m_ZMin, m_ZMax));
		}
	}
}
