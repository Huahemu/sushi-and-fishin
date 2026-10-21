using System.Collections;
using Nav3D.API;
using Nav3D.Common;
using UnityEngine;

namespace Nav3D.Demo
{
	public class EnemySpawner : MonoBehaviour
	{
		private const float PERIOD_MIN = 6f;

		private const float PERIOD_MAX = 8f;

		[SerializeField]
		private ContactPointsController m_TargetController;

		[SerializeField]
		private Nav3DAgentConfig m_EnemyConfig;

		private int m_Counter;

		private Transform m_EnemiesRoot;

		private float m_Period;

		private void DoStart()
		{
			m_Period = Random.Range(6f, 8f);
			m_EnemiesRoot = new GameObject(base.name + "_EnemiesRoot").transform;
			Nav3DManager.OnNav3DInit += InitSpawner;
		}

		private void InitSpawner()
		{
			StartCoroutine(SpawnRoutine());
		}

		private void Spawn()
		{
			Nav3DAgentConfig nav3DAgentConfig = m_EnemyConfig.Copy();
			GameObject gameObject = DemoHelper.InstantiateAgentConeBody($"{base.name}_Enemy_{m_Counter++}", nav3DAgentConfig.Radius, out var _, null, BlueTone());
			gameObject.transform.position = base.transform.position + UtilsMath.RandomNormal;
			gameObject.transform.SetParent(m_EnemiesRoot);
			Nav3DAgent nav3DAgent = gameObject.AddComponent<Nav3DAgent>();
			EnemyController enemyController = gameObject.AddComponent<EnemyController>();
			nav3DAgent.SetConfig(nav3DAgentConfig);
			enemyController.Init(m_TargetController.GetClosestTouchPoint(gameObject.transform.position));
		}

		private Color BlueTone()
		{
			return Random.ColorHSV(0.6f, 0.7f, 1f, 1f, 1f, 1f);
		}

		private IEnumerator SpawnRoutine()
		{
			while (true)
			{
				Spawn();
				yield return new WaitForSeconds(m_Period);
			}
		}

		private void Start()
		{
			DoStart();
		}
	}
}
