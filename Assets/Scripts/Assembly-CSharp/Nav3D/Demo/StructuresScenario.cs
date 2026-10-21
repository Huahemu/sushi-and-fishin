using System;
using System.Collections;
using System.Collections.Generic;
using Nav3D.API;
using Nav3D.Common;
using UnityEngine;
using UnityEngine.UI;

namespace Nav3D.Demo
{
	public class StructuresScenario : MonoBehaviour
	{
		private const int AGENTS_PREFETCH_NUMBER = 150;

		[Header("Obstacles setup")]
		[SerializeField]
		private Nav3DObstacle m_Obstacle;

		[Header("Obstacles drawing")]
		[SerializeField]
		private bool m_DrawOccupiedLeaves;

		[SerializeField]
		private bool m_DrawFreeLeaves;

		[SerializeField]
		private bool m_DrawGraph;

		[Header("Agents setup")]
		[SerializeField]
		private Nav3DAgentConfig m_AgentConfig;

		[Header("Scenario setup")]
		[SerializeField]
		private int m_AgentsCount;

		[SerializeField]
		private float m_SpawnDelay;

		[SerializeField]
		private List<Transform> m_Targets = new List<Transform>();

		[SerializeField]
		private Transform m_Spawn;

		[Header("Agents drawing")]
		[SerializeField]
		private bool m_DrawPaths;

		[SerializeField]
		private bool m_DrawAgents;

		[Header("UI")]
		[SerializeField]
		private Text m_LoaderText;

		private Queue<Nav3DAgent> m_AgentsPrefetched = new Queue<Nav3DAgent>();

		private List<Nav3DAgent> m_AgentsOperating = new List<Nav3DAgent>(150);

		private void Start()
		{
			Nav3DManager.OnNav3DInit += () =>
			{
				Nav3DPathfindingManager.MaxPathfindingTasks = (int)((float)Environment.ProcessorCount * 0.8f);
				Debug.Log(m_Obstacle.AdditionProgress.GetResultStats());
				PrefetchAgents((m_AgentsCount == 0) ? 150 : m_AgentsCount);
				m_LoaderText.gameObject.SetActive(value: false);
				StartCoroutine(AgentProductionRoutine());
			};
		}

		private void PrefetchAgents(int _Number)
		{
			GameObject gameObject = new GameObject("AgentsRoot");
			using (UtilsCommon.RandomSeedPermanence)
			{
				for (int i = 0; i < _Number; i++)
				{
					Nav3DAgentConfig nav3DAgentConfig = m_AgentConfig.Copy();
					float num = (nav3DAgentConfig.Radius = UtilsMath.GetRandomNormalValue(0.05f, 0.075f));
					nav3DAgentConfig.Speed *= num + 1.1f;
					GameObject obj = DemoHelper.InstantiateAgentConeBody($"agent_{i}", num, out var _);
					obj.transform.SetParent(gameObject.transform);
					Nav3DAgent nav3DAgent = obj.AddComponent<Nav3DAgent>();
					nav3DAgent.SetConfig(nav3DAgentConfig);
					m_AgentsPrefetched.Enqueue(nav3DAgent);
					nav3DAgent.gameObject.SetActive(value: false);
				}
			}
		}

		private IEnumerator AgentProductionRoutine()
		{
			while (true)
			{
				if (m_AgentsPrefetched.Count == 0)
				{
					yield return null;
					continue;
				}
				Nav3DAgent nav3DAgent = m_AgentsPrefetched.Dequeue();
				nav3DAgent.transform.position = m_Spawn.position + UtilsMath.GetRandomVector(0.4f);
				nav3DAgent.gameObject.SetActive(value: true);
				MoveToRandomTarget(nav3DAgent);
				m_AgentsOperating.Add(nav3DAgent);
				yield return new WaitForSeconds((m_SpawnDelay == 0f) ? 0.5f : m_SpawnDelay);
			}
		}

		private void MoveToRandomTarget(Nav3DAgent _Agent)
		{
			Transform transform = m_Targets[UnityEngine.Random.Range(0, m_Targets.Count)];
			_Agent.MoveTo(transform.position, () =>
			{
				EnqueueAgent(_Agent);
			}, null, (PathfindingResult _Result) =>
			{
				Debug.LogWarning(_Agent.name + ": " + _Result.ErrorMessage);
				if (_Result.Result == PathfindingResultCode.TIMEOUT)
				{
					MoveToRandomTarget(_Agent);
				}
				else
				{
					EnqueueAgent(_Agent);
				}
			});
		}

		private void EnqueueAgent(Nav3DAgent _Agent)
		{
			_Agent.gameObject.SetActive(value: false);
			m_AgentsPrefetched.Enqueue(_Agent);
			m_AgentsOperating.Remove(_Agent);
		}
	}
}
