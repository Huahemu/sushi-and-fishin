namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Diagnostics;
using global::System.Linq;
using global::System.Runtime.CompilerServices;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::MoreMountains.Tools;
using global::UnityEngine;

public class StampManager : NetworkSingleton<StampManager>
{
	[Serializable]
	private class FloorLootTable
	{
		[SerializeField]
		public CasinoFloor floor;

		[SerializeField]
		public MMLootTableGameObjectSO lootTable;
	}

	[CompilerGenerated]
	private sealed class _003CInitializeAllStampsCoroutine_003Ed__24 : IEnumerator<object>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public StampManager _003C_003E4__this;

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
		public _003CInitializeAllStampsCoroutine_003Ed__24(int _003C_003E1__state)
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
			StampManager stampManager = _003C_003E4__this;
			ObjectStamp objectStamp;
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
				UnityEngine.Debug.Log($"StampManager: Starting initialization of {stampManager.allStamps.Count} ObjectStamps...");
				_003Ci_003E5__6 = 0;
				goto IL_01c4;
			case 1:
				_003C_003E1__state = -1;
				_003CprocessedThisFrame_003E5__2 = 0;
				_003CframeStartTime_003E5__3 = Time.realtimeSinceStartup * 1000f;
				goto IL_00d3;
			case 2:
				{
					_003C_003E1__state = -1;
					goto IL_01b2;
				}
				IL_01c4:
				if (_003Ci_003E5__6 < stampManager.allStamps.Count)
				{
					float num2 = Time.realtimeSinceStartup * 1000f - _003CframeStartTime_003E5__3;
					if (_003CprocessedThisFrame_003E5__2 >= stampManager.maxStampsPerFrame || num2 >= stampManager.maxFrameTime)
					{
						_003C_003E2__current = null;
						_003C_003E1__state = 1;
						return true;
					}
					goto IL_00d3;
				}
				UnityEngine.Debug.Log($"StampManager: Initialization complete. Success: {_003CsuccessfulInitializations_003E5__4}, Failed: {_003CfailedInitializations_003E5__5}");
				return false;
				IL_00d3:
				objectStamp = stampManager.allStamps[_003Ci_003E5__6];
				if (objectStamp != null && objectStamp.gameObject.activeInHierarchy)
				{
					try
					{
						objectStamp.Initialize();
						_003CsuccessfulInitializations_003E5__4++;
					}
					catch (Exception ex)
					{
						UnityEngine.Debug.LogError("StampManager: Failed to initialize ObjectStamp " + objectStamp.gameObject.name + ": " + ex.Message);
						_003CfailedInitializations_003E5__5++;
					}
					_003CprocessedThisFrame_003E5__2++;
				}
				else
				{
					UnityEngine.Debug.LogWarning($"StampManager: Found null ObjectStamp at index {_003Ci_003E5__6}");
					_003CfailedInitializations_003E5__5++;
				}
				if (_003Ci_003E5__6 % 2 == 0)
				{
					_003C_003E2__current = null;
					_003C_003E1__state = 2;
					return true;
				}
				goto IL_01b2;
				IL_01b2:
				_003Ci_003E5__6++;
				goto IL_01c4;
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
	private sealed class _003CInitializeManager_003Ed__14 : IEnumerator<object>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public StampManager _003C_003E4__this;

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
		public _003CInitializeManager_003Ed__14(int _003C_003E1__state)
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
			StampManager stampManager = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				if (!stampManager.isServer)
				{
					return false;
				}
				stampManager.RebuildFloorLookup();
				stampManager._spawnCountsByTable.Clear();
				stampManager._preAssignedGames.Clear();
				stampManager.allStamps = UnityEngine.Object.FindObjectsByType<ObjectStamp>(FindObjectsSortMode.None).ToList();
				stampManager.PreAssignGamesToStamps();
				_003C_003E2__current = stampManager.StartCoroutine(stampManager.InitializeAllStampsCoroutine());
				_003C_003E1__state = 1;
				return true;
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

	[Header("Performance Settings")]
	[SerializeField]
	private float maxFrameTime = 16f;

	[SerializeField]
	private int maxStampsPerFrame = 5;

	[Header("Floor Loot Tables")]
	[Tooltip("Assign as many floors as you want and their corresponding loot tables.")]
	[SerializeField]
	private List<FloorLootTable> floorLootTables = new List<FloorLootTable>();

	[Header("Loot Spawn Limits (Server)")]
	[Tooltip("How many times the same prefab can be spawned from a single loot table (MMLootTableGameObjectSO) during this scene. Set to 0 or less for unlimited.")]
	[SerializeField]
	private int defaultMaxSpawnsPerLootPrefab = 2;

	[Tooltip("How many times we'll re-roll the loot table to try to find an item that hasn't hit its cap yet.")]
	[SerializeField]
	private int maxLootRollAttempts = 25;

	[SerializeField]
	private List<ObjectStamp> allStamps = new List<ObjectStamp>();

	[Header("Debug")]
	[SerializeField]
	private bool logStampAssignments;

	private readonly Dictionary<MMLootTableGameObjectSO, Dictionary<GameObject, int>> _spawnCountsByTable = new Dictionary<MMLootTableGameObjectSO, Dictionary<GameObject, int>>();

	private readonly Dictionary<string, GameObject> _preAssignedGames = new Dictionary<string, GameObject>();

	private readonly Dictionary<CasinoFloor, MMLootTableGameObjectSO> _lootTableByFloor = new Dictionary<CasinoFloor, MMLootTableGameObjectSO>();

	protected override void OnAwake()
	{
		base.OnAwake();
		RebuildFloorLookup();
	}

	protected override void OnValidate()
	{
		base.OnValidate();
		RebuildFloorLookup();
	}

	private void RebuildFloorLookup()
	{
		_lootTableByFloor.Clear();
		if (floorLootTables == null)
		{
			return;
		}
		foreach (FloorLootTable floorLootTable in floorLootTables)
		{
			if (floorLootTable != null && !(floorLootTable.floor == null) && !(floorLootTable.lootTable == null))
			{
				_lootTableByFloor[floorLootTable.floor] = floorLootTable.lootTable;
			}
		}
	}

	[IteratorStateMachine(typeof(_003CInitializeManager_003Ed__14))]
	[Server]
	public IEnumerator InitializeManager()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.IEnumerator StampManager::InitializeManager()' called when server was not active");
			return null;
		}
		return new _003CInitializeManager_003Ed__14(0)
		{
			_003C_003E4__this = this
		};
	}

	private string GetPositionKey(Vector3 position, CasinoFloor floor)
	{
		int num = Mathf.RoundToInt(position.x * 100f);
		int num2 = Mathf.RoundToInt(position.y * 100f);
		int num3 = Mathf.RoundToInt(position.z * 100f);
		int num4 = ((floor != null) ? floor.floorIndex : (-1));
		return $"{num},{num2},{num3},{num4}";
	}

	[Server]
	private void PreAssignGamesToStamps()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void StampManager::PreAssignGamesToStamps()' called when server was not active");
			return;
		}
		int successfulQuota = NetworkSingleton<GameManager>.Instance.successfulQuota;
		int currentSeed = NetworkSingleton<SeededRandomManager>.Instance.CurrentSeed;
		Dictionary<CasinoFloor, List<ObjectStamp>> dictionary = new Dictionary<CasinoFloor, List<ObjectStamp>>();
		foreach (ObjectStamp allStamp in allStamps)
		{
			if (!(allStamp == null) && !(allStamp.Floor == null))
			{
				if (!dictionary.ContainsKey(allStamp.Floor))
				{
					dictionary[allStamp.Floor] = new List<ObjectStamp>();
				}
				dictionary[allStamp.Floor].Add(allStamp);
			}
		}
		foreach (KeyValuePair<CasinoFloor, List<ObjectStamp>> item in dictionary.OrderBy((KeyValuePair<CasinoFloor, List<ObjectStamp>> kvp) => kvp.Key.floorIndex).ToList())
		{
			CasinoFloor key = item.Key;
			List<ObjectStamp> value = item.Value;
			MMLootTableGameObjectSO lootTableForFloor = GetLootTableForFloor(key);
			if (lootTableForFloor == null || lootTableForFloor.LootTable == null)
			{
				UnityEngine.Debug.LogWarning("[CasinoGame] Floor " + key.name + " has no loot table, skipping");
				continue;
			}
			List<GameObject> list = (from x in lootTableForFloor.LootTable.ObjectsToLoot
				where x != null && x.Loot != null
				select x.Loot).ToList();
			if (list.Count == 0)
			{
				UnityEngine.Debug.LogWarning("[CasinoGame] Floor " + key.name + " has no valid prefabs, skipping");
				continue;
			}
			global::System.Random random = new global::System.Random(GetDeterministicHash(Vector3.zero, currentSeed, successfulQuota) * 31 + key.floorIndex);
			List<GameObject> list2 = list.ToList();
			for (int num = list2.Count - 1; num > 0; num--)
			{
				int index = random.Next(0, num + 1);
				GameObject value2 = list2[num];
				list2[num] = list2[index];
				list2[index] = value2;
			}
			List<ObjectStamp> list3 = (from s in value
				orderby s.transform.position.x, s.transform.position.y, s.transform.position.z
				select s).ToList();
			for (int num2 = 0; num2 < list3.Count; num2++)
			{
				Vector3 position = list3[num2].transform.position;
				int index2 = num2 % list2.Count;
				GameObject gameObject = list2[index2];
				string positionKey = GetPositionKey(position, key);
				_preAssignedGames[positionKey] = gameObject;
				if (logStampAssignments)
				{
					UnityEngine.Debug.Log($"[StampManager] Floor {key.floorIndex} stamp at {position} -> {gameObject.name} (key: {positionKey}, index: {num2})");
				}
			}
		}
	}

	public MMLootTableGameObjectSO GetLootTableForFloor(CasinoFloor floor)
	{
		if (floor == null)
		{
			return null;
		}
		if (_lootTableByFloor.Count == 0)
		{
			RebuildFloorLookup();
		}
		_lootTableByFloor.TryGetValue(floor, out var value);
		return value;
	}

	public GameObject GetRandomPreviewPrefabForFloor(CasinoFloor floor)
	{
		MMLootTableGameObjectSO lootTableForFloor = GetLootTableForFloor(floor);
		if (lootTableForFloor?.LootTable?.ObjectsToLoot == null || lootTableForFloor.LootTable.ObjectsToLoot.Count == 0)
		{
			return null;
		}
		List<GameObject> list = (from x in lootTableForFloor.LootTable.ObjectsToLoot
			where x != null && x.Loot != null
			select x.Loot).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		return list[NetworkSingleton<SeededRandomManager>.Instance.Range(0, list.Count)];
	}

	[Server]
	public GameObject GetCappedLoot(MMLootTableGameObjectSO lootFilter)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'UnityEngine.GameObject StampManager::GetCappedLoot(MoreMountains.Tools.MMLootTableGameObjectSO)' called when server was not active");
			return null;
		}
		return GetCappedLoot(lootFilter, Vector3.zero);
	}

	[Server]
	public GameObject GetCappedLoot(MMLootTableGameObjectSO lootFilter, Vector3 stampPosition)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'UnityEngine.GameObject StampManager::GetCappedLoot(MoreMountains.Tools.MMLootTableGameObjectSO,UnityEngine.Vector3)' called when server was not active");
			return null;
		}
		if (lootFilter == null)
		{
			UnityEngine.Debug.LogError("StampManager: Loot filter is null.");
			return null;
		}
		if (lootFilter.LootTable == null || lootFilter.LootTable.ObjectsToLoot == null || lootFilter.LootTable.ObjectsToLoot.Count == 0)
		{
			UnityEngine.Debug.LogError("StampManager: Loot table has no items.");
			return null;
		}
		List<GameObject> list = (from x in lootFilter.LootTable.ObjectsToLoot
			where x != null && x.Loot != null
			select x.Loot).ToList();
		if (list.Count == 0)
		{
			UnityEngine.Debug.LogError("StampManager: No valid prefabs in loot table.");
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
		return list2[0];
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
	public GameObject GetCappedLootForFloor(CasinoFloor floor)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'UnityEngine.GameObject StampManager::GetCappedLootForFloor(CasinoFloor)' called when server was not active");
			return null;
		}
		return GetCappedLootForFloor(floor, Vector3.zero);
	}

	[Server]
	public GameObject GetCappedLootForFloor(CasinoFloor floor, Vector3 stampPosition)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'UnityEngine.GameObject StampManager::GetCappedLootForFloor(CasinoFloor,UnityEngine.Vector3)' called when server was not active");
			return null;
		}
		if (floor == null)
		{
			UnityEngine.Debug.LogError("[CasinoGame] StampManager: Floor is null. Cannot get loot.");
			return null;
		}
		string positionKey = GetPositionKey(stampPosition, floor);
		if (_preAssignedGames.TryGetValue(positionKey, out var value))
		{
			return value;
		}
		UnityEngine.Debug.LogWarning($"[CasinoGame] NO pre-assigned game for position ({stampPosition.x:F2}, {stampPosition.y:F2}, {stampPosition.z:F2}) on floor {floor.name}. Key: {positionKey}. Total assignments: {_preAssignedGames.Count}. Using fallback selection.");
		MMLootTableGameObjectSO lootTableForFloor = GetLootTableForFloor(floor);
		return GetCappedLoot(lootTableForFloor, stampPosition);
	}

	[IteratorStateMachine(typeof(_003CInitializeAllStampsCoroutine_003Ed__24))]
	[Server]
	private IEnumerator InitializeAllStampsCoroutine()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.IEnumerator StampManager::InitializeAllStampsCoroutine()' called when server was not active");
			return null;
		}
		return new _003CInitializeAllStampsCoroutine_003Ed__24(0)
		{
			_003C_003E4__this = this
		};
	}

	[Server]
	public void ResetStampManager()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void StampManager::ResetStampManager()' called when server was not active");
			return;
		}
		allStamps.Clear();
		_spawnCountsByTable.Clear();
		_preAssignedGames.Clear();
		_lootTableByFloor.Clear();
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
