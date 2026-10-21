using System.Collections.Generic;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Wheel;
using Unity.Netcode;
using UnityEngine;
using UnityServiceLocator;

[DefaultExecutionOrder(10000)]
public class TestSceneHandler : MonoBehaviour
{
	public NetworkObject PlayerPrefab;

	public Transform CompassTarget;

	private readonly Dictionary<ulong, int> _spawnSlotByClientId = new Dictionary<ulong, int>();

	private readonly HashSet<int> _occupiedSpawnSlots = new HashSet<int>();

	private void Awake()
	{
		NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedCallback;
		NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
		NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectCallback;
		NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;
		Singleton<HintsManager>.Instance.Initialize();
		Singleton<PPG_WheelManager>.Instance.Initialize();
		ServiceLocator.Global.Register(this);
	}

	private void Start()
	{
		Singleton<GameUIManager>.Instance.Initialize();
	}

	private void OnDestroy()
	{
		if (!(NetworkManager.Singleton == null))
		{
			NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedCallback;
			NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectCallback;
		}
	}

	private void OnClientConnectedCallback(ulong id)
	{
		if (NetworkManager.Singleton.IsServer)
		{
			PlayersManager instance = Singleton<PlayersManager>.Instance;
			if (!(instance == null) && TryGetOrAssignSpawnSlot(id, out var slotIndex))
			{
				Transform playerSpawnPointForSlot = instance.GetPlayerSpawnPointForSlot(slotIndex);
				Vector3 position = ((playerSpawnPointForSlot != null) ? playerSpawnPointForSlot.position : instance.GetFallbackSpawnPosition(slotIndex));
				Quaternion rotation = ((playerSpawnPointForSlot != null) ? playerSpawnPointForSlot.rotation : instance.GetFallbackSpawnRotation());
				NetworkManager.Singleton.SpawnManager.InstantiateAndSpawn(PlayerPrefab, id, destroyWithScene: true, isPlayerObject: true, forceOverride: false, position, rotation);
			}
		}
	}

	private void OnClientDisconnectCallback(ulong id)
	{
		if (_spawnSlotByClientId.TryGetValue(id, out var value))
		{
			_spawnSlotByClientId.Remove(id);
			_occupiedSpawnSlots.Remove(value);
		}
	}

	private bool TryGetOrAssignSpawnSlot(ulong id, out int slotIndex)
	{
		if (_spawnSlotByClientId.TryGetValue(id, out slotIndex))
		{
			return true;
		}
		for (int i = ((id != 0L) ? 1 : 0); i < 5; i++)
		{
			if (!_occupiedSpawnSlots.Contains(i))
			{
				slotIndex = i;
				_spawnSlotByClientId[id] = slotIndex;
				_occupiedSpawnSlots.Add(slotIndex);
				return true;
			}
		}
		slotIndex = -1;
		return false;
	}
}
