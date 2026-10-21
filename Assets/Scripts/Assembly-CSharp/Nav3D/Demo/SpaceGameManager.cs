using System;
using System.Collections;
using System.Collections.Generic;
using Nav3D.API;
using UnityEngine;
using UnityEngine.UI;

namespace Nav3D.Demo
{
	public class SpaceGameManager : MonoBehaviour
	{
		private const int MINERS_COUNT_DEFAULT = 140;

		private const int DEFENDERS_COUNT_DEFAULT = 15;

		[Space]
		[Header("Agents configs")]
		[SerializeField]
		private Nav3DAgentConfig m_MinerConfig;

		[SerializeField]
		private Nav3DAgentConfig m_DefenderConfig;

		[Space]
		[Header("Agents prefabs")]
		[SerializeField]
		private GameObject m_DefenderPrefab;

		[Space]
		[Header("Scene objects")]
		[SerializeField]
		private Nav3DObstacle m_ObstaclesRoot;

		[SerializeField]
		private ContactPointsController m_Planet;

		[SerializeField]
		private List<ContactPointsController> m_Mines;

		[SerializeField]
		private List<Transform> m_GuardWaypoints;

		[Space]
		[Header("UI")]
		[SerializeField]
		private Text m_LoaderText;

		[SerializeField]
		private bool m_VisualizeMinersPaths;

		private bool m_Inited;

		private List<Nav3DAgent> m_MinerAgents = new List<Nav3DAgent>(140);

		private void Start()
		{
			Nav3DManager.OnNav3DInit += () =>
			{
				Nav3DPathfindingManager.MaxPathfindingTasks = (int)((float)Environment.ProcessorCount * 0.6f);
			};
			m_ObstaclesRoot.OnObstacleAdded += () =>
			{
				Debug.Log(m_ObstaclesRoot.AdditionProgress.GetResultStats());
			};
			Nav3DManager.OnNav3DInit += InitGame;
		}

		private void InitGame()
		{
			if (!m_Inited)
			{
				m_LoaderText.gameObject.SetActive(value: false);
				StartCoroutine(SpawnMiners(null));
				SpawnDefenders(null);
				m_Inited = true;
			}
		}

		private IEnumerator SpawnMiners(GameObject _Prefab)
		{
			GameObject agentsRoot = new GameObject("MinersRoot");
			for (int i = 0; i < 140; i++)
			{
				yield return new WaitForSeconds(0.125f);
				Nav3DAgentConfig nav3DAgentConfig = m_MinerConfig.Copy();
				GameObject obj = DemoHelper.InstantiateAgentConeBody($"miner_{i}", nav3DAgentConfig.Radius, out var _, null, OrangeTone());
				obj.transform.SetParent(agentsRoot.transform);
				Nav3DAgent nav3DAgent = obj.AddComponent<Nav3DAgent>();
				MinerController minerController = obj.AddComponent<MinerController>();
				nav3DAgent.SetConfig(nav3DAgentConfig);
				minerController.Init(m_Planet, m_Mines[UnityEngine.Random.Range(0, m_Mines.Count)]);
				m_MinerAgents.Add(nav3DAgent);
			}
		}

		private void SpawnDefenders(GameObject _Prefab)
		{
			GameObject gameObject = new GameObject("DefendersRoot");
			for (int i = 0; i < 15; i++)
			{
				Nav3DAgentConfig config = m_DefenderConfig.Copy();
				GameObject obj = UnityEngine.Object.Instantiate(m_DefenderPrefab, Vector3.zero, Quaternion.identity);
				obj.transform.SetParent(gameObject.transform);
				Nav3DAgent nav3DAgent = obj.AddComponent<Nav3DAgent>();
				DefenderController defenderController = obj.AddComponent<DefenderController>();
				obj.name = $"Defender_{nav3DAgent.GetInstanceID()}";
				nav3DAgent.SetConfig(config);
				int startIndex = (int)((float)m_GuardWaypoints.Count * ((float)i / 15f));
				defenderController.Init(m_GuardWaypoints.ToArray(), startIndex);
			}
		}

		private Color OrangeTone()
		{
			return UnityEngine.Random.ColorHSV(0.1127f, 0.1805f, 1f, 1f, 1f, 1f);
		}
	}
}
