using System.Collections.Generic;
using DistantLands.Cozy;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;
using UnityServiceLocator;

namespace PaperPixelGames.Core.ScrapSystem
{
	public class WorldPickupHandler : NetworkBehaviour
	{
		[Header("Points Parent (children = spawn points)")]
		[SerializeField]
		private Transform _scrapsParent;

		[SerializeField]
		private Transform _coinsParent;

		[Header("Prefabs")]
		[SerializeField]
		private NetworkObject _scrapNetworkPrefab;

		[SerializeField]
		private NetworkObject _coinNetworkPrefab;

		[Header("Limits")]
		[SerializeField]
		private int _maxScrapCount = 30;

		[Header("Spawn Timing")]
		[SerializeField]
		private int _scrapSpawnIntervalMinutes = 25;

		private int _minutesSinceLastScrapSpawn;

		[Header("Model Prefabs")]
		[SerializeField]
		private List<GameObject> _singleScrapModels = new List<GameObject>();

		[SerializeField]
		private List<GameObject> _groupScrapModels = new List<GameObject>();

		[Space]
		[SerializeField]
		private List<GameObject> _singleCoinModels = new List<GameObject>();

		[SerializeField]
		private List<GameObject> _groupCoinModels = new List<GameObject>();

		[Header("Ground Snap (Raycast)")]
		[SerializeField]
		private bool _snapToGround = true;

		[SerializeField]
		private float _raycastUpOffset = 0.5f;

		[SerializeField]
		private float _raycastDistance = 10f;

		[SerializeField]
		private LayerMask _groundMask = -1;

		[SerializeField]
		private float _groundYOffset = 0.02f;

		private readonly Dictionary<Transform, NetworkObject> _activeScrapPickupsBySpawnPoint = new Dictionary<Transform, NetworkObject>();

		[Header("Editor Gizmos")]
		[SerializeField]
		private bool _drawSpawnPointGizmos = true;

		[SerializeField]
		private float _gizmoSphereRadius = 0.12f;

		[SerializeField]
		private bool _drawRayPreview = true;

		[SerializeField]
		private bool _drawLabels;

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			ServiceLocator.ForSceneOf(this).Register(typeof(WorldPickupHandler), this);
			if (base.IsServer)
			{
				CozyWeather.Events.onNewMinute += OnNewMinute;
			}
		}

		public override void OnNetworkDespawn()
		{
			if (base.IsServer)
			{
				CozyWeather.Events.onNewMinute -= OnNewMinute;
			}
			base.OnNetworkDespawn();
		}

		private void OnNewMinute()
		{
			if (base.IsServer)
			{
				_minutesSinceLastScrapSpawn++;
				if (_minutesSinceLastScrapSpawn >= _scrapSpawnIntervalMinutes)
				{
					_minutesSinceLastScrapSpawn = 0;
					ServerSpawnOneScrap();
				}
			}
		}

		public void ServerSpawnOneScrap()
		{
			if (!base.IsServer || _scrapsParent == null || _scrapNetworkPrefab == null || GetCurrentScrapCount() >= _maxScrapCount)
			{
				return;
			}
			CleanupInactiveSpawnPointRecords();
			Transform transform = GetNearestAvailableSpawnPointToLocalPlayer();
			if (transform == null)
			{
				transform = GetRandomAvailableScrapSpawnPoint();
				if (transform == null)
				{
					return;
				}
			}
			transform.GetPositionAndRotation(out var position, out var rotation);
			if (_snapToGround && TryGetGroundSnappedPosition(transform.position, out var snappedPos))
			{
				position = snappedPos;
			}
			NetworkObject networkObject = Object.Instantiate(_scrapNetworkPrefab, position, rotation, _scrapsParent);
			networkObject.Spawn(destroyWithScene: true);
			_activeScrapPickupsBySpawnPoint[transform] = networkObject;
			if (networkObject.TryGetComponent<WorldPickup>(out var component))
			{
				component.Setup(PickupType.Scrap);
			}
		}

		public void ServerSpawnOneScrapAtPosition(Vector3 worldPos)
		{
			if (base.IsServer && !(_scrapsParent == null) && !(_scrapNetworkPrefab == null) && GetCurrentScrapCount() < _maxScrapCount)
			{
				Vector3 position = worldPos;
				Quaternion identity = Quaternion.identity;
				if (_snapToGround && TryGetGroundSnappedPosition(worldPos, out var snappedPos))
				{
					position = snappedPos;
				}
				NetworkObject networkObject = Object.Instantiate(_scrapNetworkPrefab, position, identity, _scrapsParent);
				networkObject.Spawn(destroyWithScene: true);
				if (networkObject.TryGetComponent<WorldPickup>(out var component))
				{
					component.Setup(PickupType.Scrap);
				}
			}
		}

		public void ServerSpawnOneCoinAtPosition(Vector3 worldPos)
		{
			if (base.IsServer && !(_coinsParent == null) && !(_coinNetworkPrefab == null))
			{
				Vector3 position = worldPos;
				Quaternion identity = Quaternion.identity;
				if (_snapToGround && TryGetGroundSnappedPosition(worldPos, out var snappedPos))
				{
					position = snappedPos;
				}
				NetworkObject networkObject = Object.Instantiate(_coinNetworkPrefab, position, identity, _coinsParent);
				networkObject.Spawn(destroyWithScene: true);
				if (networkObject.TryGetComponent<WorldPickup>(out var component))
				{
					component.Setup(PickupType.Coin);
				}
			}
		}

		private bool TryGetGroundSnappedPosition(Vector3 originalPos, out Vector3 snappedPos)
		{
			if (Physics.Raycast(originalPos + Vector3.up * _raycastUpOffset, Vector3.down, out var hitInfo, _raycastDistance, _groundMask))
			{
				snappedPos = hitInfo.point + Vector3.up * _groundYOffset;
				return true;
			}
			snappedPos = originalPos;
			return false;
		}

		private Transform GetNearestAvailableSpawnPointToLocalPlayer()
		{
			if (_scrapsParent == null)
			{
				return null;
			}
			PlayersManager instance = Singleton<PlayersManager>.Instance;
			if (instance == null || instance.LocalPlayer == null)
			{
				return null;
			}
			Vector3 position = instance.LocalPlayer.transform.position;
			int childCount = _scrapsParent.childCount;
			if (childCount <= 0)
			{
				return null;
			}
			Transform result = null;
			float num = float.MaxValue;
			for (int i = 0; i < childCount; i++)
			{
				Transform child = _scrapsParent.GetChild(i);
				if (!(child == null) && !child.TryGetComponent<WorldPickup>(out var _) && !IsScrapSpawnPointOccupied(child))
				{
					float sqrMagnitude = (child.position - position).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						result = child;
					}
				}
			}
			return result;
		}

		private Transform GetRandomAvailableScrapSpawnPoint()
		{
			if (_scrapsParent == null)
			{
				return null;
			}
			int childCount = _scrapsParent.childCount;
			if (childCount <= 0)
			{
				return null;
			}
			List<Transform> list = new List<Transform>();
			for (int i = 0; i < childCount; i++)
			{
				Transform child = _scrapsParent.GetChild(i);
				if (!(child == null) && !child.TryGetComponent<WorldPickup>(out var _) && !IsScrapSpawnPointOccupied(child))
				{
					list.Add(child);
				}
			}
			if (list.Count == 0)
			{
				return null;
			}
			return list[Random.Range(0, list.Count)];
		}

		private bool IsScrapSpawnPointOccupied(Transform point)
		{
			if (point == null)
			{
				return true;
			}
			if (!_activeScrapPickupsBySpawnPoint.TryGetValue(point, out var value))
			{
				return false;
			}
			if (value != null && value.IsSpawned)
			{
				return true;
			}
			_activeScrapPickupsBySpawnPoint.Remove(point);
			return false;
		}

		private void CleanupInactiveSpawnPointRecords()
		{
			if (_activeScrapPickupsBySpawnPoint.Count == 0)
			{
				return;
			}
			List<Transform> list = null;
			foreach (KeyValuePair<Transform, NetworkObject> item in _activeScrapPickupsBySpawnPoint)
			{
				if (!(item.Key != null) || !(item.Value != null) || !item.Value.IsSpawned)
				{
					if (list == null)
					{
						list = new List<Transform>();
					}
					list.Add(item.Key);
				}
			}
			if (list == null)
			{
				return;
			}
			foreach (Transform item2 in list)
			{
				_activeScrapPickupsBySpawnPoint.Remove(item2);
			}
		}

		private int GetCurrentScrapCount()
		{
			if (_scrapsParent == null)
			{
				return 0;
			}
			int num = 0;
			int childCount = _scrapsParent.childCount;
			for (int i = 0; i < childCount; i++)
			{
				Transform child = _scrapsParent.GetChild(i);
				if (!(child == null) && child.TryGetComponent<WorldPickup>(out var _))
				{
					num++;
				}
			}
			return num;
		}

		private GameObject GetSingleModel(PickupType pickupType, int index)
		{
			List<GameObject> list = ((pickupType == PickupType.Coin) ? _singleCoinModels : _singleScrapModels);
			if (index < 0 || list == null || list.Count <= index)
			{
				return null;
			}
			return list[index];
		}

		private GameObject GetGroupModel(PickupType pickupType, int index)
		{
			List<GameObject> list = ((pickupType == PickupType.Coin) ? _groupCoinModels : _groupScrapModels);
			if (index < 0 || list == null || list.Count <= index)
			{
				return null;
			}
			return list[index];
		}

		public GameObject GetPickupModel(PickupType pickupType, bool isSingle, int index)
		{
			if (!isSingle)
			{
				return GetGroupModel(pickupType, index);
			}
			return GetSingleModel(pickupType, index);
		}

		public int GetModelCount(PickupType pickupType, bool isSingle)
		{
			return ((pickupType != PickupType.Coin) ? (isSingle ? _singleScrapModels : _groupScrapModels) : (isSingle ? _singleCoinModels : _groupCoinModels))?.Count ?? 0;
		}

		public bool TryGetRandomModelIndex(PickupType pickupType, bool isSingle, out byte modelIndex)
		{
			modelIndex = 0;
			int modelCount = GetModelCount(pickupType, isSingle);
			if (modelCount <= 0)
			{
				return false;
			}
			modelIndex = (byte)Random.Range(0, modelCount);
			return true;
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
			return "WorldPickupHandler";
		}
	}
}
