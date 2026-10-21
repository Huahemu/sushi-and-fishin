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
using global::Mirror.RemoteCalls;
using global::UnityEngine;
using global::UnityEngine.AI;

public class NPCSpawner : NetworkSingleton<NPCSpawner>
{
	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass9_0
	{
		public int floorIndex;

		internal bool _003CSpawnNPCsForFloor_003Eb__0(CasinoFloor f)
		{
			return f.floorIndex == floorIndex;
		}
	}

	[CompilerGenerated]
	private sealed class _003CSpawnAllCoroutine_003Ed__10 : IEnumerator<object>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public NPCSpawner _003C_003E4__this;

		private Dictionary<int, Transform> _003CnpcHoldersByFloor_003E5__2;

		private List<int> _003CfloorIndices_003E5__3;

		private int _003CspawnedThisFrame_003E5__4;

		private float _003CframeStartTime_003E5__5;

		private int _003CfloorIdx_003E5__6;

		private int _003Ci_003E5__7;

		private Vector3 _003Cpos_003E5__8;

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
		public _003CSpawnAllCoroutine_003Ed__10(int _003C_003E1__state)
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
			NPCSpawner nPCSpawner = _003C_003E4__this;
			Quaternion rotation;
			GameObject gameObject;
			NPC component;
			int num3;
			int cosmeticPresetIndexForFloor;
			Transform value;
			switch (num)
			{
			default:
				return false;
			case 0:
			{
				_003C_003E1__state = -1;
				nPCSpawner.gameSettings = SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
				if (nPCSpawner.gameSettings.npcCount <= 0)
				{
					_003C_003E2__current = null;
					_003C_003E1__state = 1;
					return true;
				}
				List<CasinoFloor> list = (from f in UnityEngine.Object.FindObjectsByType<CasinoFloor>(FindObjectsSortMode.None)
					where f.floorIndex >= 0 && f.floorIndex <= 3
					orderby f.floorIndex
					select f).ToList();
				_003CnpcHoldersByFloor_003E5__2 = new Dictionary<int, Transform>();
				foreach (CasinoFloor item in list)
				{
					NPCHolder componentInChildren = item.GetComponentInChildren<NPCHolder>();
					if (componentInChildren != null)
					{
						_003CnpcHoldersByFloor_003E5__2[item.floorIndex] = componentInChildren.transform;
					}
				}
				if (_003CnpcHoldersByFloor_003E5__2.Count == 0)
				{
					_003C_003E2__current = null;
					_003C_003E1__state = 2;
					return true;
				}
				_003CfloorIndices_003E5__3 = _003CnpcHoldersByFloor_003E5__2.Keys.OrderBy((int k) => k).ToList();
				_003CspawnedThisFrame_003E5__4 = 0;
				_003CframeStartTime_003E5__5 = Time.realtimeSinceStartup * 1000f;
				_003CfloorIdx_003E5__6 = 0;
				_003Ci_003E5__7 = 0;
				goto IL_0331;
			}
			case 1:
				_003C_003E1__state = -1;
				return false;
			case 2:
				_003C_003E1__state = -1;
				return false;
			case 3:
				_003C_003E1__state = -1;
				_003CspawnedThisFrame_003E5__4 = 0;
				_003CframeStartTime_003E5__5 = Time.realtimeSinceStartup * 1000f;
				goto IL_021e;
			case 4:
				_003C_003E1__state = -1;
				goto IL_0313;
			case 5:
				{
					_003C_003E1__state = -1;
					return false;
				}
				IL_031f:
				_003Ci_003E5__7++;
				goto IL_0331;
				IL_0331:
				if (_003Ci_003E5__7 < nPCSpawner.gameSettings.npcCount)
				{
					if (nPCSpawner.TryGetRandomNavMeshPosition(out _003Cpos_003E5__8))
					{
						float num2 = Time.realtimeSinceStartup * 1000f - _003CframeStartTime_003E5__5;
						if (_003CspawnedThisFrame_003E5__4 >= nPCSpawner.maxSpawnsPerFrame || num2 >= nPCSpawner.maxFrameTime)
						{
							_003C_003E2__current = null;
							_003C_003E1__state = 3;
							return true;
						}
						goto IL_021e;
					}
					goto IL_031f;
				}
				_003C_003E2__current = null;
				_003C_003E1__state = 5;
				return true;
				IL_0313:
				_003Cpos_003E5__8 = default;
				goto IL_031f;
				IL_021e:
				rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
				gameObject = UnityEngine.Object.Instantiate(nPCSpawner.npcPrefab, _003Cpos_003E5__8, rotation);
				component = gameObject.GetComponent<NPC>();
				num3 = _003CfloorIndices_003E5__3[_003CfloorIdx_003E5__6 % _003CfloorIndices_003E5__3.Count];
				cosmeticPresetIndexForFloor = nPCSpawner.GetCosmeticPresetIndexForFloor(num3);
				nPCSpawner.NPCs.Add(component);
				NetworkServer.Spawn(gameObject);
				if (NetworkSingleton<NPCController>.Instance != null)
				{
					NetworkSingleton<NPCController>.Instance.RegisterNPC(component);
				}
				_003CfloorIdx_003E5__6++;
				if (_003CnpcHoldersByFloor_003E5__2.TryGetValue(num3, out value))
				{
					nPCSpawner.SetupNPC(component, value, num3, cosmeticPresetIndexForFloor, delayClientSync: true);
				}
				_003CspawnedThisFrame_003E5__4++;
				if (_003Ci_003E5__7 % 2 == 0)
				{
					_003C_003E2__current = null;
					_003C_003E1__state = 4;
					return true;
				}
				goto IL_0313;
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
	private sealed class _003CSpawnNPCsForFloor_003Ed__9 : IEnumerator<object>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public int floorIndex;

		public NPCSpawner _003C_003E4__this;

		private _003C_003Ec__DisplayClass9_0 _003C_003E8__1;

		private NPCHolder _003CnpcHolder_003E5__2;

		private int _003Ccount_003E5__3;

		private int _003Ci_003E5__4;

		private Vector3 _003Cpos_003E5__5;

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
		public _003CSpawnNPCsForFloor_003Ed__9(int _003C_003E1__state)
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
			NPCSpawner nPCSpawner = _003C_003E4__this;
			int num2;
			float num3;
			if (num != 0)
			{
				if (num != 1)
				{
					return false;
				}
				_003C_003E1__state = -1;
				num2 = 0;
				num3 = Time.realtimeSinceStartup * 1000f;
				goto IL_0155;
			}
			_003C_003E1__state = -1;
			_003C_003E8__1 = new _003C_003Ec__DisplayClass9_0();
			_003C_003E8__1.floorIndex = floorIndex;
			nPCSpawner.gameSettings = ((nPCSpawner.gameSettings != null) ? nPCSpawner.gameSettings : SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings"));
			if (nPCSpawner.gameSettings.npcCount <= 0)
			{
				return false;
			}
			CasinoFloor casinoFloor = UnityEngine.Object.FindObjectsByType<CasinoFloor>(FindObjectsSortMode.None).FirstOrDefault((CasinoFloor f) => f.floorIndex == _003C_003E8__1.floorIndex);
			if (casinoFloor == null)
			{
				return false;
			}
			_003CnpcHolder_003E5__2 = casinoFloor.GetComponentInChildren<NPCHolder>(includeInactive: true);
			if (_003CnpcHolder_003E5__2 == null)
			{
				return false;
			}
			nPCSpawner.DestroyAllNPCs();
			_003Ccount_003E5__3 = nPCSpawner.gameSettings.npcCount;
			num2 = 0;
			num3 = Time.realtimeSinceStartup * 1000f;
			_003Ci_003E5__4 = 0;
			goto IL_0212;
			IL_0200:
			_003Ci_003E5__4++;
			goto IL_0212;
			IL_0212:
			if (_003Ci_003E5__4 < _003Ccount_003E5__3)
			{
				if (nPCSpawner.TryGetRandomNavMeshPosition(out _003Cpos_003E5__5, _003CnpcHolder_003E5__2.transform.position))
				{
					if (num2 >= nPCSpawner.maxSpawnsPerFrame || Time.realtimeSinceStartup * 1000f - num3 >= nPCSpawner.maxFrameTime)
					{
						_003C_003E2__current = null;
						_003C_003E1__state = 1;
						return true;
					}
					goto IL_0155;
				}
				goto IL_0200;
			}
			return false;
			IL_0155:
			Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
			GameObject gameObject = UnityEngine.Object.Instantiate(nPCSpawner.npcPrefab, _003Cpos_003E5__5, rotation);
			NPC component = gameObject.GetComponent<NPC>();
			int cosmeticPresetIndexForFloor = nPCSpawner.GetCosmeticPresetIndexForFloor(_003C_003E8__1.floorIndex);
			nPCSpawner.NPCs.Add(component);
			NetworkServer.Spawn(gameObject);
			if (NetworkSingleton<NPCController>.Instance != null)
			{
				NetworkSingleton<NPCController>.Instance.RegisterNPC(component);
			}
			nPCSpawner.SetupNPC(component, _003CnpcHolder_003E5__2.transform, _003C_003E8__1.floorIndex, cosmeticPresetIndexForFloor, delayClientSync: false);
			num2++;
			_003Cpos_003E5__5 = default;
			goto IL_0200;
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

	private GameSettings gameSettings;

	[Header("Spawn Settings")]
	public GameObject npcPrefab;

	[SerializeField]
	private readonly SyncList<NPC> NPCs = new SyncList<NPC>();

	[Header("Performance Settings")]
	[SerializeField]
	private float maxFrameTime = 16f;

	[SerializeField]
	private int maxSpawnsPerFrame = 3;

	[Server]
	private int GetCosmeticPresetIndexForFloor(int floorIndex)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Int32 NPCSpawner::GetCosmeticPresetIndexForFloor(System.Int32)' called when server was not active");
			return default;
		}
		return ((floorIndex >= 1 && floorIndex <= 4) ? floorIndex : (floorIndex + 1)) switch
		{
			1 => UnityEngine.Random.Range(6, 14), 
			2 => UnityEngine.Random.Range(10, 17), 
			3 => UnityEngine.Random.Range(18, 23), 
			4 => UnityEngine.Random.Range(24, 28), 
			_ => UnityEngine.Random.Range(6, 14), 
		};
	}

	[Server]
	private void SetupNPC(NPC npc, Transform npcHolder, int floorIndex, int presetIndex, bool delayClientSync)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void NPCSpawner::SetupNPC(NPC,UnityEngine.Transform,System.Int32,System.Int32,System.Boolean)' called when server was not active");
			return;
		}
		npc.transform.SetParent(npcHolder);
		NPCCosmeticSelector component = npc.GetComponent<NPCCosmeticSelector>();
		if (component != null)
		{
			component.SetSelectedPresetIndex(presetIndex);
		}
		if (delayClientSync)
		{
			StartCoroutine(SyncNPCSetupAfterDelay(npc.netId, floorIndex, presetIndex));
		}
		else
		{
			RpcSetupNPC(npc.netId, floorIndex, presetIndex);
		}
	}

	[Server]
	private bool TryGetRandomNavMeshPosition(out Vector3 position, Vector3? fallbackCenter = null)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Boolean NPCSpawner::TryGetRandomNavMeshPosition(UnityEngine.Vector3&,System.Nullable`1<UnityEngine.Vector3>)' called when server was not active");
			position = default;
			return default;
		}
		position = Vector3.zero;
		NavMeshTriangulation navMeshTriangulation = NavMesh.CalculateTriangulation();
		if (navMeshTriangulation.vertices != null && navMeshTriangulation.vertices.Length >= 3 && navMeshTriangulation.indices != null && navMeshTriangulation.indices.Length >= 3)
		{
			int maxExclusive = navMeshTriangulation.indices.Length / 3;
			int num = UnityEngine.Random.Range(0, maxExclusive);
			int num2 = navMeshTriangulation.indices[num * 3];
			int num3 = navMeshTriangulation.indices[num * 3 + 1];
			int num4 = navMeshTriangulation.indices[num * 3 + 2];
			float num5 = UnityEngine.Random.value;
			float num6 = UnityEngine.Random.value;
			if (num5 + num6 > 1f)
			{
				num5 = 1f - num5;
				num6 = 1f - num6;
			}
			position = num5 * navMeshTriangulation.vertices[num2] + num6 * navMeshTriangulation.vertices[num3] + (1f - num5 - num6) * navMeshTriangulation.vertices[num4];
			return true;
		}
		Vector3 vector = fallbackCenter ?? Vector3.zero;
		for (int i = 0; i < 30; i++)
		{
			Vector2 vector2 = UnityEngine.Random.insideUnitCircle * 25f;
			if (NavMesh.SamplePosition(vector + new Vector3(vector2.x, 0f, vector2.y), out var hit, 15f, -1))
			{
				position = hit.position;
				return true;
			}
		}
		return false;
	}

	[Server]
	public void DestroyAllNPCs()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void NPCSpawner::DestroyAllNPCs()' called when server was not active");
			return;
		}
		NPCHolder[] array = UnityEngine.Object.FindObjectsByType<NPCHolder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		List<NPC> list = new List<NPC>();
		NPCHolder[] array2 = array;
		foreach (NPCHolder nPCHolder in array2)
		{
			list.AddRange(nPCHolder.GetComponentsInChildren<NPC>(includeInactive: true));
		}
		foreach (NPC item in list)
		{
			if (NetworkSingleton<NPCController>.Instance != null)
			{
				NetworkSingleton<NPCController>.Instance.UnregisterNPC(item);
			}
			NPCs.Remove(item);
			NetworkServer.Destroy(item.gameObject);
		}
	}

	[IteratorStateMachine(typeof(_003CSpawnNPCsForFloor_003Ed__9))]
	[Server]
	public IEnumerator SpawnNPCsForFloor(int floorIndex)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.IEnumerator NPCSpawner::SpawnNPCsForFloor(System.Int32)' called when server was not active");
			return null;
		}
		return new _003CSpawnNPCsForFloor_003Ed__9(0)
		{
			_003C_003E4__this = this,
			floorIndex = floorIndex
		};
	}

	[IteratorStateMachine(typeof(_003CSpawnAllCoroutine_003Ed__10))]
	[Server]
	public IEnumerator SpawnAllCoroutine()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.IEnumerator NPCSpawner::SpawnAllCoroutine()' called when server was not active");
			return null;
		}
		return new _003CSpawnAllCoroutine_003Ed__10(0)
		{
			_003C_003E4__this = this
		};
	}

	private IEnumerator SyncNPCSetupAfterDelay(uint npcNetId, int floorIndex, int presetIndex)
	{
		yield return new WaitForSeconds(0.1f);
		RpcSetupNPC(npcNetId, floorIndex, presetIndex);
	}

	[ClientRpc]
	private void RpcSetupNPC(uint npcNetId, int floorIndex, int presetIndex)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteVarUInt(npcNetId);
		writer.WriteVarInt(floorIndex);
		writer.WriteVarInt(presetIndex);
		SendRPCInternal("System.Void NPCSpawner::RpcSetupNPC(System.UInt32,System.Int32,System.Int32)", 646469297, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public NPCSpawner()
	{
		InitSyncObject(NPCs);
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSetupNPC__UInt32__Int32__Int32(uint npcNetId, int floorIndex, int presetIndex)
	{
		if (!NetworkClient.spawned.TryGetValue(npcNetId, out var value))
		{
			return;
		}
		NPC component = value.GetComponent<NPC>();
		if (!(component != null))
		{
			return;
		}
		foreach (CasinoFloor item in from f in UnityEngine.Object.FindObjectsByType<CasinoFloor>(FindObjectsSortMode.None)
			where f.floorIndex == floorIndex
			select f)
		{
			NPCHolder componentInChildren = item.GetComponentInChildren<NPCHolder>();
			if (componentInChildren != null)
			{
				component.transform.SetParent(componentInChildren.transform);
				NPCCosmeticSelector component2 = component.GetComponent<NPCCosmeticSelector>();
				if (component2 != null)
				{
					component2.SetSelectedPresetIndex(presetIndex);
				}
				break;
			}
		}
	}

	protected static void InvokeUserCode_RpcSetupNPC__UInt32__Int32__Int32(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("RPC RpcSetupNPC called on server.");
		}
		else
		{
			((NPCSpawner)obj).UserCode_RpcSetupNPC__UInt32__Int32__Int32(reader.ReadVarUInt(), reader.ReadVarInt(), reader.ReadVarInt());
		}
	}

	static NPCSpawner()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(NPCSpawner), "System.Void NPCSpawner::RpcSetupNPC(System.UInt32,System.Int32,System.Int32)", InvokeUserCode_RpcSetupNPC__UInt32__Int32__Int32);
	}
}

}
