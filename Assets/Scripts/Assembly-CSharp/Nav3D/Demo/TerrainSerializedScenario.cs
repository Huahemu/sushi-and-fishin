using System;
using Nav3D.API;
using UnityEngine;

namespace Nav3D.Demo
{
	public class TerrainSerializedScenario : MonoBehaviour
	{
		private const int AGENTS_COUNT = 100;

		[SerializeField]
		private Nav3DObstacleLoader m_ObstaclesLoader;

		[SerializeField]
		private Nav3DAgentConfig m_Config;

		private void Start()
		{
			m_ObstaclesLoader.OnLoadingFinished += () =>
			{
				Debug.Log(m_ObstaclesLoader.DeserializingProgress.GetResultStats());
				Nav3DPathfindingManager.MaxPathfindingTasks = (int)((float)Environment.ProcessorCount * 0.6f);
				SpawnAgents();
			};
		}

		private void SpawnAgents()
		{
			GameObject gameObject = new GameObject("AgentsRoot");
			for (int i = 0; i < 100; i++)
			{
				Nav3DAgentConfig nav3DAgentConfig = m_Config.Copy();
				GameObject obj = DemoHelper.InstantiateAgentConeBody($"agent_{i}", nav3DAgentConfig.Radius, out var _, null, Color.blue);
				_ = 500 * (i + 1) / 100;
				obj.transform.position = new Vector3(500 * (i + 1) / 100, 25f, 0f);
				obj.transform.SetParent(gameObject.transform);
				obj.AddComponent<Nav3DAgent>().SetConfig(nav3DAgentConfig);
				SquareWanderingAgent squareWanderingAgent = obj.AddComponent<SquareWanderingAgent>();
				squareWanderingAgent.Init(0f, 25f, 0f, 500f, 25f, 500f, 15f);
				squareWanderingAgent.StartMovement();
			}
		}
	}
}
