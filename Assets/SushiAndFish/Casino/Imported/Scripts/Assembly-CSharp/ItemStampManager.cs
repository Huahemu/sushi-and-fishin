namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Diagnostics;
using global::System.Linq;
using global::System.Reflection;
using global::System.Runtime.CompilerServices;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::MoreMountains.Tools;
using global::UnityEngine;

public class ItemStampManager : NetworkSingleton<ItemStampManager>
{
	[CompilerGenerated]
	private sealed class _003CCoRespawnStampAfterDeferredDestroy_003Ed__33 : IEnumerator<object>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public ItemStamp stamp;

		public ItemStampManager _003C_003E4__this;

		object IEnumerator<object>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CCoRespawnStampAfterDeferredDestroy_003Ed__33(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
		}

		private bool MoveNext()
		{
			int num = _003C_003E1__state;
			ItemStampManager itemStampManager = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				_003C_003E2__current = null;
				_003C_003E1__state = 1;
				return true;
			case 1:
				_003C_003E1__state = -1;
				_003C_003E2__current = null;
				_003C_003E1__state = 2;
				return true;
			case 2:
				_003C_003E1__state = -1;
				if (stamp != null && stamp.gameObject.activeInHierarchy && !itemStampManager._purchasedStamps.Contains(stamp))
				{
					stamp.Initialize();
				}
				return false;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}
	}

	[CompilerGenerated]
	private sealed class _003CCoRetrieveAndRespawnItemStamp_003Ed__32 : IEnumerator<object>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public ItemStampManager _003C_003E4__this;

		public ItemStamp stamp;

		object IEnumerator<object>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CCoRetrieveAndRespawnItemStamp_003Ed__32(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
		}

		private bool MoveNext()
		{
			int num = _003C_003E1__state;
			ItemStampManager itemStampManager = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				itemStampManager.DestroySpawnedInstancesForStamp(stamp);
				_003C_003E2__current = null;
				_003C_003E1__state = 1;
				return true;
			case 1:
				_003C_003E1__state = -1;
				_003C_003E2__current = null;
				_003C_003E1__state = 2;
				return true;
			case 2:
				_003C_003E1__state = -1;
				if (stamp != null && stamp.gameObject.activeInHierarchy && !itemStampManager._purchasedStamps.Contains(stamp))
				{
					stamp.Initialize();
				}
				return false;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}
	}

	[CompilerGenerated]
	private sealed class _003CInitializeAllItemStampsCoroutine_003Ed__41 : IEnumerator<object>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public ItemStampManager _003C_003E4__this;

		public List<ItemStamp> itemStamps;

		private int _003CprocessedThisFrame_003E5__2;

		private float _003CframeStartTime_003E5__3;

		private int _003CsuccessfulInitializations_003E5__4;

		private int _003CfailedInitializations_003E5__5;

		private int _003Ci_003E5__6;

		object IEnumerator<object>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CInitializeAllItemStampsCoroutine_003Ed__41(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
		}

		private bool MoveNext()
		{
			int num = _003C_003E1__state;
			ItemStampManager itemStampManager = _003C_003E4__this;
			ItemStamp itemStamp;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				_003CprocessedThisFrame_003E5__2 = 0;
				_003CframeStartTime_003E5__3 = Time.realtimeSinceStartup * 1000f;
				_003CsuccessfulInitializations_003E5__4 = 0;
				_003CfailedInitializations_003E5__5 = 0;
				_003Ci_003E5__6 = 0;
				goto IL_01a5;
			case 1:
				_003C_003E1__state = -1;
				_003CprocessedThisFrame_003E5__2 = 0;
				_003CframeStartTime_003E5__3 = Time.realtimeSinceStartup * 1000f;
				goto IL_00b4;
			case 2:
				{
					_003C_003E1__state = -1;
					goto IL_0193;
				}
				IL_01a5:
				if (_003Ci_003E5__6 < itemStamps.Count)
				{
					float num2 = Time.realtimeSinceStartup * 1000f - _003CframeStartTime_003E5__3;
					if (_003CprocessedThisFrame_003E5__2 >= itemStampManager.maxStampsPerFrame || num2 >= itemStampManager.maxFrameTime)
					{
						_003C_003E2__current = null;
						_003C_003E1__state = 1;
						return true;
					}
					goto IL_00b4;
				}
				UnityEngine.Debug.Log($"[ItemStampManager] Initialization complete. Success: {_003CsuccessfulInitializations_003E5__4}, Failed: {_003CfailedInitializations_003E5__5}");
				return false;
				IL_00b4:
				itemStamp = itemStamps[_003Ci_003E5__6];
				if (itemStamp != null && itemStamp.gameObject.activeInHierarchy)
				{
					try
					{
						itemStamp.Initialize();
						_003CsuccessfulInitializations_003E5__4++;
					}
					catch (Exception ex)
					{
						UnityEngine.Debug.LogError("[ItemStampManager] Failed to initialize ItemStamp " + itemStamp.gameObject.name + ": " + ex.Message);
						_003CfailedInitializations_003E5__5++;
					}
					_003CprocessedThisFrame_003E5__2++;
				}
				else
				{
					UnityEngine.Debug.LogWarning($"[ItemStampManager] Found null ItemStamp at index {_003Ci_003E5__6}");
					_003CfailedInitializations_003E5__5++;
				}
				if (_003Ci_003E5__6 % 2 == 0)
				{
					_003C_003E2__current = null;
					_003C_003E1__state = 2;
					return true;
				}
				goto IL_0193;
				IL_0193:
				_003Ci_003E5__6++;
				goto IL_01a5;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}
	}

	[CompilerGenerated]
	private sealed class _003CInitializeManager_003Ed__38 : IEnumerator<object>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public ItemStampManager _003C_003E4__this;

		object IEnumerator<object>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CInitializeManager_003Ed__38(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
		}

		private bool MoveNext()
		{
			int num = _003C_003E1__state;
			ItemStampManager itemStampManager = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
			{
				_003C_003E1__state = -1;
				if (!itemStampManager.isServer)
				{
					return false;
				}
				itemStampManager._spawnedItems.Clear();
				itemStampManager._preAssignedItems.Clear();
				itemStampManager._spawnedItemInstances.Clear();
				itemStampManager._instanceToStamp.Clear();
				itemStampManager._purchasedStamps.Clear();
				List<ItemStamp> itemStamps = UnityEngine.Object.FindObjectsByType<ItemStamp>(FindObjectsSortMode.None).ToList();
				itemStampManager.PreAssignItemsToStamps(itemStamps);
				_003C_003E2__current = itemStampManager.StartCoroutine(itemStampManager.InitializeAllItemStampsCoroutine(itemStamps));
				_003C_003E1__state = 1;
				return true;
			}
			case 1:
				_003C_003E1__state = -1;
				return false;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}
	}

	private readonly HashSet<GameObject> _spawnedItems = new HashSet<GameObject>();

	private readonly List<GameObject> _spawnedItemInstances = new List<GameObject>();

	private readonly Dictionary<GameObject, ItemStamp> _instanceToStamp = new Dictionary<GameObject, ItemStamp>();

	private readonly HashSet<ItemStamp> _purchasedStamps = new HashSet<ItemStamp>();

	private int _rerollCount;

	private readonly Dictionary<string, GameObject> _preAssignedItems = new Dictionary<string, GameObject>();

	[Header("Performance Settings")]
	[Tooltip("Maximum number of ItemStamps to process per frame")]
	[SerializeField]
	private int maxStampsPerFrame = 5;

	[Tooltip("Maximum frame time in milliseconds before yielding")]
	[SerializeField]
	private float maxFrameTime = 16f;

	[Header("Uniqueness Settings")]
	[Tooltip("Maximum number of times each item prefab from a loot table can be assigned across its ItemStamps before duplicates are allowed again. 1 = each item at most once.")]
	[SerializeField]
	[Min(1f)]
	private int maxAssignmentsPerItem = 1;

	private float _lastRetrieveTime;

	protected override void OnAwake()
	{
		base.OnAwake();
		_spawnedItems.Clear();
		_spawnedItemInstances.Clear();
		_instanceToStamp.Clear();
		_purchasedStamps.Clear();
		_rerollCount = 0;
	}

	[Server]
	public GameObject GetUniqueLoot(MMLootTableGameObjectSO lootTable)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'UnityEngine.GameObject ItemStampManager::GetUniqueLoot(MoreMountains.Tools.MMLootTableGameObjectSO)' called when server was not active");
			return null;
		}
		return GetUniqueLoot(lootTable, Vector3.zero);
	}

	[Server]
	public GameObject GetUniqueLoot(MMLootTableGameObjectSO lootTable, Vector3 stampPosition)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'UnityEngine.GameObject ItemStampManager::GetUniqueLoot(MoreMountains.Tools.MMLootTableGameObjectSO,UnityEngine.Vector3)' called when server was not active");
			return null;
		}
		if (lootTable == null)
		{
			UnityEngine.Debug.LogError("ItemStampManager: Loot table is null.");
			return null;
		}
		string positionKey = GetPositionKey(stampPosition, lootTable);
		if (_preAssignedItems.TryGetValue(positionKey, out var value))
		{
			return value;
		}
		UnityEngine.Debug.LogWarning($"ItemStampManager: NO pre-assigned item for position ({stampPosition.x:F2}, {stampPosition.y:F2}, {stampPosition.z:F2}) with loot table {lootTable.name}. Key: {positionKey}. Using fallback selection.");
		return GetUniqueLootFallback(lootTable, stampPosition);
	}

	[Server]
	private GameObject GetUniqueLootFallback(MMLootTableGameObjectSO lootTable, Vector3 stampPosition)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'UnityEngine.GameObject ItemStampManager::GetUniqueLootFallback(MoreMountains.Tools.MMLootTableGameObjectSO,UnityEngine.Vector3)' called when server was not active");
			return null;
		}
		if (lootTable.LootTable == null || lootTable.LootTable.ObjectsToLoot == null || lootTable.LootTable.ObjectsToLoot.Count == 0)
		{
			UnityEngine.Debug.LogError("ItemStampManager: Loot table has no items.");
			return null;
		}
		List<GameObject> list = (from x in lootTable.LootTable.ObjectsToLoot
			where x != null && x.Loot != null
			select x.Loot).ToList();
		if (list.Count == 0)
		{
			UnityEngine.Debug.LogError("ItemStampManager: No valid prefabs in loot table.");
			return null;
		}
		int successfulQuota = NetworkSingleton<GameManager>.Instance.successfulQuota;
		global::System.Random random = new global::System.Random(GetDeterministicHash(stampPosition, NetworkSingleton<SeededRandomManager>.Instance.CurrentSeed, successfulQuota));
		List<GameObject> list2 = list.ToList();
		for (int num = list2.Count - 1; num > 0; num--)
		{
			int index = random.Next(0, num + 1);
			GameObject value = list2[num];
			list2[num] = list2[index];
			list2[index] = value;
		}
		GameObject gameObject = null;
		for (int num2 = 0; num2 < list2.Count; num2++)
		{
			GameObject gameObject2 = list2[num2];
			if (!_spawnedItems.Contains(gameObject2))
			{
				gameObject = gameObject2;
				_spawnedItems.Add(gameObject2);
				break;
			}
		}
		if (gameObject == null)
		{
			gameObject = list2[0];
		}
		return gameObject;
	}

	private string GetPositionKey(Vector3 position, MMLootTableGameObjectSO lootTable)
	{
		int num = Mathf.RoundToInt(position.x * 100f);
		int num2 = Mathf.RoundToInt(position.y * 100f);
		int num3 = Mathf.RoundToInt(position.z * 100f);
		int num4 = ((lootTable != null) ? lootTable.GetInstanceID() : (-1));
		return $"{num},{num2},{num3},{num4}";
	}

	private int GetDeterministicHash(Vector3 position, int seed, int quotaIndex)
	{
		int num = seed * 31 + quotaIndex;
		int num2 = Mathf.RoundToInt(position.x * 100f);
		int num3 = Mathf.RoundToInt(position.y * 100f);
		int num4 = Mathf.RoundToInt(position.z * 100f);
		return ((num * 31 + num2) * 31 + num3) * 31 + num4;
	}

	[Server]
	public void MarkItemAsSpawned(GameObject itemPrefab)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void ItemStampManager::MarkItemAsSpawned(UnityEngine.GameObject)' called when server was not active");
		}
		else if (itemPrefab != null)
		{
			_spawnedItems.Add(itemPrefab);
		}
	}

	[Server]
	public void UnmarkItemAsSpawned(GameObject itemPrefab)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void ItemStampManager::UnmarkItemAsSpawned(UnityEngine.GameObject)' called when server was not active");
		}
		else if (itemPrefab != null)
		{
			_spawnedItems.Remove(itemPrefab);
		}
	}

	[Server]
	public void ResetTracking()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void ItemStampManager::ResetTracking()' called when server was not active");
			return;
		}
		_spawnedItems.Clear();
		_spawnedItemInstances.Clear();
		_instanceToStamp.Clear();
		_purchasedStamps.Clear();
		_rerollCount = 0;
		UnityEngine.Debug.Log("ItemStampManager: Spawned items tracking has been reset.");
	}

	[Server]
	public int GetSpawnedItemCount()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Int32 ItemStampManager::GetSpawnedItemCount()' called when server was not active");
			return default;
		}
		return _spawnedItems.Count;
	}

	[Server]
	public bool HasItemBeenSpawned(GameObject itemPrefab)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Boolean ItemStampManager::HasItemBeenSpawned(UnityEngine.GameObject)' called when server was not active");
			return default;
		}
		if (itemPrefab != null)
		{
			return _spawnedItems.Contains(itemPrefab);
		}
		return false;
	}

	[Server]
	public void RegisterSpawnedInstance(GameObject instance, ItemStamp sourceStamp)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void ItemStampManager::RegisterSpawnedInstance(UnityEngine.GameObject,ItemStamp)' called when server was not active");
		}
		else if (!(instance == null) && !(sourceStamp == null))
		{
			if (!_spawnedItemInstances.Contains(instance))
			{
				_spawnedItemInstances.Add(instance);
			}
			_instanceToStamp[instance] = sourceStamp;
		}
	}

	[Server]
	public ItemStamp GetStampFromInstance(GameObject instance)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'ItemStamp ItemStampManager::GetStampFromInstance(UnityEngine.GameObject)' called when server was not active");
			return null;
		}
		if (instance == null)
		{
			return null;
		}
		if (_instanceToStamp.TryGetValue(instance, out var value))
		{
			return value;
		}
		return null;
	}

	[Server]
	public void UnregisterSpawnedInstance(GameObject instance)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void ItemStampManager::UnregisterSpawnedInstance(UnityEngine.GameObject)' called when server was not active");
		}
		else if (!(instance == null))
		{
			_spawnedItemInstances.Remove(instance);
			_instanceToStamp.Remove(instance);
		}
	}

	[Server]
	public void MarkInstancePurchased(GameObject instance)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void ItemStampManager::MarkInstancePurchased(UnityEngine.GameObject)' called when server was not active");
		}
		else if (!(instance == null))
		{
			if (_instanceToStamp.TryGetValue(instance, out var value) && value != null)
			{
				_purchasedStamps.Add(value);
			}
			UnregisterSpawnedInstance(instance);
		}
	}

	[Server]
	private static void ServerDestroySpawnedLootObject(GameObject go)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void ItemStampManager::ServerDestroySpawnedLootObject(UnityEngine.GameObject)' called when server was not active");
		}
		else if (!(go == null))
		{
			Item component2;
			if (go.TryGetComponent<ConsumableItem>(out var component))
			{
				component.DestroyItem();
			}
			else if (go.TryGetComponent<Item>(out component2))
			{
				component2.ServerDrop();
				NetworkServer.Destroy(go);
			}
			else
			{
				NetworkServer.Destroy(go);
			}
		}
	}

	[Server]
	private List<GameObject> CollectInstancesForStamp(ItemStamp stamp)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.Generic.List`1<UnityEngine.GameObject> ItemStampManager::CollectInstancesForStamp(ItemStamp)' called when server was not active");
			return null;
		}
		List<GameObject> list = new List<GameObject>();
		if (stamp == null)
		{
			return list;
		}
		foreach (KeyValuePair<GameObject, ItemStamp> item in _instanceToStamp)
		{
			if (item.Value == stamp && item.Key != null)
			{
				list.Add(item.Key);
			}
		}
		return list;
	}

	[Server]
	private void DestroySpawnedInstancesForStamp(ItemStamp stamp)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void ItemStampManager::DestroySpawnedInstancesForStamp(ItemStamp)' called when server was not active");
			return;
		}
		foreach (GameObject item in CollectInstancesForStamp(stamp))
		{
			UnregisterSpawnedInstance(item);
			ServerDestroySpawnedLootObject(item);
		}
	}

	[Server]
	private void DestroyAllSpawnedInstances()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void ItemStampManager::DestroyAllSpawnedInstances()' called when server was not active");
			return;
		}
		List<GameObject> list = new List<GameObject>(_spawnedItemInstances);
		_spawnedItemInstances.Clear();
		_instanceToStamp.Clear();
		for (int i = 0; i < list.Count; i++)
		{
			GameObject gameObject = list[i];
			if (gameObject != null)
			{
				ServerDestroySpawnedLootObject(gameObject);
			}
		}
	}

	[Server]
	public void RetrieveAndRespawnItemStamp(ItemStamp stamp)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void ItemStampManager::RetrieveAndRespawnItemStamp(ItemStamp)' called when server was not active");
		}
		else if (base.isServer && !(stamp == null) && stamp.gameObject.activeInHierarchy && !_purchasedStamps.Contains(stamp))
		{
			StartCoroutine(CoRetrieveAndRespawnItemStamp(stamp));
		}
	}

	[Server]
	public void RetrieveAndRespawnItemStampForInstance(GameObject spawnedInstance)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void ItemStampManager::RetrieveAndRespawnItemStampForInstance(UnityEngine.GameObject)' called when server was not active");
		}
		else if (base.isServer)
		{
			RetrieveAndRespawnItemStamp(GetStampFromInstance(spawnedInstance));
		}
	}

	[Server]
	public void OnLobbyStampItemConsumed(GameObject instance)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void ItemStampManager::OnLobbyStampItemConsumed(UnityEngine.GameObject)' called when server was not active");
		}
		else if (base.isServer && !(instance == null))
		{
			ItemStamp stampFromInstance = GetStampFromInstance(instance);
			if (!(stampFromInstance == null) && !_purchasedStamps.Contains(stampFromInstance))
			{
				UnregisterSpawnedInstance(instance);
				StartCoroutine(CoRespawnStampAfterDeferredDestroy(stampFromInstance));
			}
		}
	}

	[IteratorStateMachine(typeof(_003CCoRetrieveAndRespawnItemStamp_003Ed__32))]
	[Server]
	private IEnumerator CoRetrieveAndRespawnItemStamp(ItemStamp stamp)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.IEnumerator ItemStampManager::CoRetrieveAndRespawnItemStamp(ItemStamp)' called when server was not active");
			return null;
		}
		return new _003CCoRetrieveAndRespawnItemStamp_003Ed__32(0)
		{
			_003C_003E4__this = this,
			stamp = stamp
		};
	}

	[IteratorStateMachine(typeof(_003CCoRespawnStampAfterDeferredDestroy_003Ed__33))]
	[Server]
	private IEnumerator CoRespawnStampAfterDeferredDestroy(ItemStamp stamp)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.IEnumerator ItemStampManager::CoRespawnStampAfterDeferredDestroy(ItemStamp)' called when server was not active");
			return null;
		}
		return new _003CCoRespawnStampAfterDeferredDestroy_003Ed__33(0)
		{
			_003C_003E4__this = this,
			stamp = stamp
		};
	}

	[Server]
	public void RerollAllItemStamps()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void ItemStampManager::RerollAllItemStamps()' called when server was not active");
		}
		else if (base.isServer)
		{
			DestroyAllSpawnedInstances();
			int seed = NetworkSingleton<SeededRandomManager>.Instance.CurrentSeed + 1;
			NetworkSingleton<SeededRandomManager>.Instance.InitializeSeed(seed);
			_spawnedItems.Clear();
			_preAssignedItems.Clear();
			List<ItemStamp> itemStamps = (from s in UnityEngine.Object.FindObjectsByType<ItemStamp>(FindObjectsSortMode.None).ToList()
				where s != null && !_purchasedStamps.Contains(s)
				select s).ToList();
			PreAssignItemsToStamps(itemStamps);
			StartCoroutine(InitializeAllItemStampsCoroutine(itemStamps));
		}
	}

	[Server]
	public void TryRerollAllItemStampsWithCost()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void ItemStampManager::TryRerollAllItemStampsWithCost()' called when server was not active");
		}
		else
		{
			if (!base.isServer)
			{
				return;
			}
			GameSettings gameSettings = SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
			if (gameSettings == null)
			{
				UnityEngine.Debug.LogWarning("[ItemStampManager] GameSettings not found in SushiAndFish.Casino.Original.CasinoResources. Cannot charge reroll cost.");
				return;
			}
			GameSettings.CasinoFloorData currentFloorData = gameSettings.GetCurrentFloorData();
			if (currentFloorData == null)
			{
				UnityEngine.Debug.LogWarning("[ItemStampManager] Current floor data is null. Cannot charge reroll cost.");
				return;
			}
			int num = currentFloorData.rerollCost;
			if (num < 0)
			{
				num = 0;
			}
			int num2 = (_rerollCount + 1) * num;
			if (num2 > 0)
			{
				if (NetworkSingleton<MoneyManager>.Instance == null)
				{
					UnityEngine.Debug.LogWarning("[ItemStampManager] MoneyManager not found. Cannot charge reroll cost.");
					return;
				}
				if (!NetworkSingleton<MoneyManager>.Instance.TryChangeTicketBalance(-num2))
				{
					UnityEngine.Debug.Log($"[ItemStampManager] Not enough tickets to reroll items. Need {num2}, have {NetworkSingleton<MoneyManager>.Instance.ticketBalance}.");
					return;
				}
			}
			RerollAllItemStamps();
			_rerollCount++;
		}
	}

	[Server]
	public int GetCurrentRerollCost()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Int32 ItemStampManager::GetCurrentRerollCost()' called when server was not active");
			return default;
		}
		int num = 0;
		GameSettings gameSettings = SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
		if (gameSettings != null)
		{
			GameSettings.CasinoFloorData currentFloorData = gameSettings.GetCurrentFloorData();
			if (currentFloorData != null)
			{
				num = currentFloorData.rerollCost;
				if (num < 0)
				{
					num = 0;
				}
			}
		}
		return (_rerollCount + 1) * num;
	}

	[Server]
	public void RetrieveAndRespawnAllItemStamps()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void ItemStampManager::RetrieveAndRespawnAllItemStamps()' called when server was not active");
		}
		else if (base.isServer && !(Time.time - _lastRetrieveTime < 1f))
		{
			_lastRetrieveTime = Time.time;
			DestroyAllSpawnedInstances();
			_spawnedItems.Clear();
			_preAssignedItems.Clear();
			List<ItemStamp> itemStamps = (from s in UnityEngine.Object.FindObjectsByType<ItemStamp>(FindObjectsSortMode.None).ToList()
				where s != null && !_purchasedStamps.Contains(s)
				select s).ToList();
			PreAssignItemsToStamps(itemStamps);
			StartCoroutine(InitializeAllItemStampsCoroutine(itemStamps));
		}
	}

	[IteratorStateMachine(typeof(_003CInitializeManager_003Ed__38))]
	[Server]
	public IEnumerator InitializeManager()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.IEnumerator ItemStampManager::InitializeManager()' called when server was not active");
			return null;
		}
		return new _003CInitializeManager_003Ed__38(0)
		{
			_003C_003E4__this = this
		};
	}

	[Server]
	private void PreAssignItemsToStamps(List<ItemStamp> itemStamps)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void ItemStampManager::PreAssignItemsToStamps(System.Collections.Generic.List`1<ItemStamp>)' called when server was not active");
			return;
		}
		int successfulQuota = NetworkSingleton<GameManager>.Instance.successfulQuota;
		int currentSeed = NetworkSingleton<SeededRandomManager>.Instance.CurrentSeed;
		Dictionary<MMLootTableGameObjectSO, List<ItemStamp>> dictionary = new Dictionary<MMLootTableGameObjectSO, List<ItemStamp>>();
		foreach (ItemStamp itemStamp in itemStamps)
		{
			if (itemStamp == null)
			{
				continue;
			}
			MMLootTableGameObjectSO lootTableFromStamp = GetLootTableFromStamp(itemStamp);
			if (!(lootTableFromStamp == null))
			{
				if (!dictionary.ContainsKey(lootTableFromStamp))
				{
					dictionary[lootTableFromStamp] = new List<ItemStamp>();
				}
				dictionary[lootTableFromStamp].Add(itemStamp);
			}
		}
		foreach (KeyValuePair<MMLootTableGameObjectSO, List<ItemStamp>> item in dictionary.OrderBy((KeyValuePair<MMLootTableGameObjectSO, List<ItemStamp>> x) => x.Key.GetInstanceID()))
		{
			MMLootTableGameObjectSO key = item.Key;
			List<ItemStamp> value = item.Value;
			if (key.LootTable == null || key.LootTable.ObjectsToLoot == null)
			{
				continue;
			}
			List<GameObject> list = (from x in key.LootTable.ObjectsToLoot
				where x != null && x.Loot != null
				select x.Loot).ToList();
			if (list.Count == 0)
			{
				continue;
			}
			global::System.Random random = new global::System.Random(GetDeterministicHash(Vector3.zero, currentSeed, successfulQuota) * 31 + key.GetInstanceID());
			List<GameObject> list2 = list.ToList();
			for (int num = list2.Count - 1; num > 0; num--)
			{
				int index = random.Next(0, num + 1);
				GameObject value2 = list2[num];
				list2[num] = list2[index];
				list2[index] = value2;
			}
			List<ItemStamp> list3 = (from s in value
				orderby s.transform.position.x, s.transform.position.y, s.transform.position.z
				select s).ToList();
			Dictionary<GameObject, int> dictionary2 = new Dictionary<GameObject, int>();
			foreach (ItemStamp item2 in list3)
			{
				Vector3 position = item2.transform.position;
				int num2 = new global::System.Random(GetDeterministicHash(position, currentSeed, successfulQuota) * 31 + key.GetInstanceID()).Next(0, list2.Count);
				GameObject gameObject = null;
				for (int num3 = 0; num3 < list2.Count; num3++)
				{
					int index2 = (num2 + num3) % list2.Count;
					GameObject gameObject2 = list2[index2];
					if (!dictionary2.TryGetValue(gameObject2, out var value3) || value3 < maxAssignmentsPerItem)
					{
						gameObject = gameObject2;
						dictionary2[gameObject2] = value3 + 1;
						break;
					}
				}
				if (gameObject == null)
				{
					gameObject = list2[num2];
				}
				string positionKey = GetPositionKey(position, key);
				_preAssignedItems[positionKey] = gameObject;
			}
		}
	}

	private MMLootTableGameObjectSO GetLootTableFromStamp(ItemStamp stamp)
	{
		if (stamp == null)
		{
			return null;
		}
		try
		{
			FieldInfo field = typeof(ItemStamp).GetField("lootTable", BindingFlags.Instance | BindingFlags.NonPublic);
			if (field != null)
			{
				return field.GetValue(stamp) as MMLootTableGameObjectSO;
			}
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogWarning("ItemStampManager: Failed to get loot table from ItemStamp " + stamp.gameObject.name + ": " + ex.Message);
		}
		return null;
	}

	[IteratorStateMachine(typeof(_003CInitializeAllItemStampsCoroutine_003Ed__41))]
	[Server]
	private IEnumerator InitializeAllItemStampsCoroutine(List<ItemStamp> itemStamps)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.IEnumerator ItemStampManager::InitializeAllItemStampsCoroutine(System.Collections.Generic.List`1<ItemStamp>)' called when server was not active");
			return null;
		}
		return new _003CInitializeAllItemStampsCoroutine_003Ed__41(0)
		{
			_003C_003E4__this = this,
			itemStamps = itemStamps
		};
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
