using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Splines;

namespace PaperPixelGames.Core.WaterSurface
{
	public class ShipTrafficManager : NetworkBehaviour
	{
		[Header("Routes")]
		[SerializeField]
		private List<SplineContainer> _routes = new List<SplineContainer>();

		[Header("Ship Prefabs (Random Pick)")]
		[SerializeField]
		private List<SplineRouteFollower> _shipPrefabs = new List<SplineRouteFollower>();

		[Header("Pooling")]
		[SerializeField]
		[Min(1f)]
		private int _poolPerPrefab = 2;

		[Header("Spawn Timing (Minutes)")]
		[SerializeField]
		private Vector2 _spawnIntervalMinutes = new Vector2(2f, 6f);

		[Header("Ship Speed (Units/sec)")]
		[SerializeField]
		private Vector2 _speedRange = new Vector2(4f, 9f);

		[Header("Start From End Chance")]
		[Range(0f, 1f)]
		[SerializeField]
		private float _startFromEndChance = 0.5f;

		private readonly Dictionary<SplineRouteFollower, List<SplineRouteFollower>> _poolsByPrefab = new Dictionary<SplineRouteFollower, List<SplineRouteFollower>>();

		private float _nextSpawnTime;

		private void Awake()
		{
			if (_shipPrefabs == null || _shipPrefabs.Count == 0)
			{
				Debug.LogError("[ShipTrafficManager] No ship prefabs assigned.");
				base.enabled = false;
				return;
			}
			foreach (SplineRouteFollower shipPrefab in _shipPrefabs)
			{
				if (!(shipPrefab == null))
				{
					List<SplineRouteFollower> list = new List<SplineRouteFollower>(_poolPerPrefab);
					for (int i = 0; i < _poolPerPrefab; i++)
					{
						SplineRouteFollower splineRouteFollower = Object.Instantiate(shipPrefab, base.transform);
						splineRouteFollower.gameObject.SetActive(value: false);
						splineRouteFollower.Completed += OnShipCompleted;
						list.Add(splineRouteFollower);
					}
					_poolsByPrefab[shipPrefab] = list;
				}
			}
			ScheduleNextSpawn();
		}

		private void FixedUpdate()
		{
			if (!(Time.time < _nextSpawnTime))
			{
				TrySpawnShip();
				ScheduleNextSpawn();
			}
		}

		private void TrySpawnShip()
		{
			if (_routes == null || _routes.Count == 0)
			{
				return;
			}
			SplineContainer splineContainer = _routes[Random.Range(0, _routes.Count)];
			if (splineContainer == null || splineContainer.Splines == null || splineContainer.Splines.Count == 0)
			{
				return;
			}
			SplineRouteFollower splineRouteFollower = PickRandomPrefab();
			if (!(splineRouteFollower == null))
			{
				SplineRouteFollower availableShip = GetAvailableShip(splineRouteFollower);
				if (!(availableShip == null))
				{
					int splineIndex = 0;
					float speedUnitsPerSec = Random.Range(_speedRange.x, _speedRange.y);
					bool startFromEnd = Random.value < _startFromEndChance;
					availableShip.Begin(splineContainer, splineIndex, speedUnitsPerSec, startFromEnd);
				}
			}
		}

		private SplineRouteFollower PickRandomPrefab()
		{
			for (int i = 0; i < 8; i++)
			{
				SplineRouteFollower splineRouteFollower = _shipPrefabs[Random.Range(0, _shipPrefabs.Count)];
				if (splineRouteFollower != null)
				{
					return splineRouteFollower;
				}
			}
			return null;
		}

		private SplineRouteFollower GetAvailableShip(SplineRouteFollower prefab)
		{
			if (!_poolsByPrefab.TryGetValue(prefab, out var value) || value == null)
			{
				return null;
			}
			for (int i = 0; i < value.Count; i++)
			{
				SplineRouteFollower splineRouteFollower = value[i];
				if (splineRouteFollower != null && !splineRouteFollower.IsRunning)
				{
					return splineRouteFollower;
				}
			}
			return null;
		}

		private void OnShipCompleted(SplineRouteFollower ship)
		{
			if (!(ship == null))
			{
				ship.StopAndHide();
			}
		}

		private void ScheduleNextSpawn()
		{
			float minInclusive = Mathf.Min(_spawnIntervalMinutes.x, _spawnIntervalMinutes.y);
			float maxInclusive = Mathf.Max(_spawnIntervalMinutes.x, _spawnIntervalMinutes.y);
			float num = Random.Range(minInclusive, maxInclusive) * 60f;
			_nextSpawnTime = Time.time + num;
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "ShipTrafficManager";
		}
	}
}
