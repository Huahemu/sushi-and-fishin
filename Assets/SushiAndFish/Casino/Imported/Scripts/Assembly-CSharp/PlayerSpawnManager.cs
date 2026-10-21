namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Diagnostics;
using global::System.Runtime.CompilerServices;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class PlayerSpawnManager : NetworkSingleton<PlayerSpawnManager>
{
	[CompilerGenerated]
	private sealed class _003CInitialSpawnSequenceRoutine_003Ed__33 : IEnumerator<object>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public PlayerSpawnManager _003C_003E4__this;

		private List<Transform> _003Cstarts_003E5__2;

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
		public _003CInitialSpawnSequenceRoutine_003Ed__33(int _003C_003E1__state)
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
			PlayerSpawnManager playerSpawnManager = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				_003Cstarts_003E5__2 = NetworkManager.startPositions;
				if (_003Cstarts_003E5__2 == null || _003Cstarts_003E5__2.Count == 0)
				{
					UnityEngine.Debug.LogError("InitialSpawnSequenceRoutine: No spawn points available!");
					return false;
				}
				if (playerSpawnManager.emptyPlayerPrefab != null)
				{
					foreach (KeyValuePair<int, NetworkConnectionToClient> connection in NetworkServer.connections)
					{
						NetworkConnectionToClient value2 = connection.Value;
						if (value2 != null)
						{
							int connectionId2 = value2.connectionId;
							if (playerSpawnManager.registered.Contains(connectionId2) && playerSpawnManager.ready.Contains(connectionId2) && !(value2.identity != null))
							{
								GameObject player = UnityEngine.Object.Instantiate(playerSpawnManager.emptyPlayerPrefab);
								NetworkServer.AddPlayerForConnection(value2, player);
							}
						}
					}
				}
				else
				{
					UnityEngine.Debug.LogWarning("InitialSpawnSequenceRoutine: EmptyPlayer prefab missing, spawning real players directly.");
				}
				_003C_003E2__current = null;
				_003C_003E1__state = 1;
				return true;
			case 1:
				_003C_003E1__state = -1;
				foreach (KeyValuePair<int, NetworkConnectionToClient> connection2 in NetworkServer.connections)
				{
					NetworkConnectionToClient value = connection2.Value;
					if (value == null)
					{
						continue;
					}
					int connectionId = value.connectionId;
					if (playerSpawnManager.registered.Contains(connectionId) && playerSpawnManager.ready.Contains(connectionId))
					{
						int spawnIndexFor = playerSpawnManager.GetSpawnIndexFor(connectionId, _003Cstarts_003E5__2.Count);
						Transform transform = _003Cstarts_003E5__2[spawnIndexFor];
						GameObject gameObject = UnityEngine.Object.Instantiate(playerSpawnManager.playerPrefab, transform.position, transform.rotation);
						PlayerController component = gameObject.GetComponent<PlayerController>();
						if (value.identity == null)
						{
							NetworkServer.AddPlayerForConnection(value, gameObject);
						}
						else
						{
							NetworkServer.ReplacePlayerForConnection(value, gameObject, ReplacePlayerOptions.KeepAuthority);
						}
						if (component != null)
						{
							transform.SendMessageUpwards("AssignPlayer", component, SendMessageOptions.DontRequireReceiver);
						}
					}
				}
				playerSpawnManager.TryInitializeIfComplete();
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
	private sealed class _003CSpawnRoutine_003Ed__30 : IEnumerator<object>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public NetworkConnectionToClient conn;

		public PlayerSpawnManager _003C_003E4__this;

		public List<Transform> starts;

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
		public _003CSpawnRoutine_003Ed__30(int _003C_003E1__state)
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
			PlayerSpawnManager playerSpawnManager = _003C_003E4__this;
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
			{
				_003C_003E1__state = -1;
				if (conn == null)
				{
					return false;
				}
				int connectionId = conn.connectionId;
				if (!playerSpawnManager.registered.Contains(connectionId) || !playerSpawnManager.ready.Contains(connectionId))
				{
					playerSpawnManager.spawning.Remove(connectionId);
					return false;
				}
				if (conn.identity != null)
				{
					playerSpawnManager.spawning.Remove(connectionId);
					return false;
				}
				int spawnIndexFor = playerSpawnManager.GetSpawnIndexFor(connectionId, starts.Count);
				Transform transform = starts[spawnIndexFor];
				GameObject gameObject = UnityEngine.Object.Instantiate(playerSpawnManager.playerPrefab, transform.position, transform.rotation);
				PlayerController component = gameObject.GetComponent<PlayerController>();
				if (conn.identity == null)
				{
					NetworkServer.AddPlayerForConnection(conn, gameObject);
					UnityEngine.Debug.Log($"Successfully spawned player for connection {connectionId} at spawn point {spawnIndexFor}");
				}
				else
				{
					UnityEngine.Debug.LogWarning($"Connection {connectionId} already has identity, replacing player");
					NetworkServer.ReplacePlayerForConnection(conn, gameObject, ReplacePlayerOptions.KeepAuthority);
				}
				if (component != null)
				{
					transform.SendMessageUpwards("AssignPlayer", component, SendMessageOptions.DontRequireReceiver);
				}
				playerSpawnManager.spawning.Remove(connectionId);
				if (playerSpawnManager.initializedEpoch == playerSpawnManager.sceneEpoch && NetworkSingleton<GameManager>.Instance != null)
				{
					NetworkSingleton<GameManager>.Instance.RpcLoadAllPlayerCosmetics();
					playerSpawnManager.TargetLockPlayerInputs(conn);
					component.ServerLock(isLocked: true);
					playerSpawnManager.TargetSceneTransition(conn);
					playerSpawnManager.OnPlayerLateJoined?.Invoke();
				}
				playerSpawnManager.TryInitializeIfComplete();
				return false;
			}
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
	private sealed class _003CSpawnTimeoutRoutine_003Ed__35 : IEnumerator<object>, IEnumerator, IDisposable
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		public PlayerSpawnManager _003C_003E4__this;

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
		public _003CSpawnTimeoutRoutine_003Ed__35(int _003C_003E1__state)
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
			PlayerSpawnManager playerSpawnManager = _003C_003E4__this;
			switch (num)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				_003C_003E2__current = new WaitForSeconds(10f);
				_003C_003E1__state = 1;
				return true;
			case 1:
				_003C_003E1__state = -1;
				if (!playerSpawnManager.spawningEnabled && playerSpawnManager.sceneAcceptingSpawns)
				{
					UnityEngine.Debug.LogWarning("Spawn timeout reached. Spawning ready players and disconnecting non-ready connections.");
					playerSpawnManager.spawningEnabled = true;
					playerSpawnManager.StartInitialSpawnSequence();
					foreach (KeyValuePair<int, NetworkConnectionToClient> connection in NetworkServer.connections)
					{
						NetworkConnectionToClient value = connection.Value;
						if (value != null)
						{
							int connectionId = value.connectionId;
							if (playerSpawnManager.registered.Contains(connectionId) && !playerSpawnManager.ready.Contains(connectionId))
							{
								UnityEngine.Debug.LogWarning($"Disconnecting non-ready connection {connectionId} due to timeout");
								value.Disconnect();
							}
						}
					}
				}
				playerSpawnManager.timeoutCoroutine = null;
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

	[SerializeField]
	private GameObject playerPrefab;

	[SerializeField]
	private GameObject emptyPlayerPrefab;

	private LobbySettings lobbySettings;

	private readonly HashSet<int> registered = new HashSet<int>();

	private readonly HashSet<int> ready = new HashSet<int>();

	private readonly HashSet<int> spawning = new HashSet<int>();

	private readonly Dictionary<int, int> spawnIndexByConnId = new Dictionary<int, int>();

	private readonly HashSet<int> usedSpawnPoints = new HashSet<int>();

	private int sceneEpoch;

	private bool sceneAcceptingSpawns;

	private bool spawningEnabled;

	private bool initialSpawnSequenceStarted;

	private int initializedEpoch = -1;

	private Coroutine timeoutCoroutine;

	public int RegisteredCount => registered.Count;

	public event Action OnAllPlayersSpawned;

	public event Action OnPlayerLateJoined;

	protected override void OnAwake()
	{
		base.OnAwake();
		lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
		if (emptyPlayerPrefab == null)
		{
			emptyPlayerPrefab = SushiAndFish.Casino.Original.CasinoResources.Load<GameObject>("EmptyPlayer");
		}
	}

	[Server]
	public void ServerOnSceneChanged(string sceneName)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void PlayerSpawnManager::ServerOnSceneChanged(System.String)' called when server was not active");
			return;
		}
		sceneEpoch++;
		sceneAcceptingSpawns = false;
		spawningEnabled = false;
		initialSpawnSequenceStarted = false;
		initializedEpoch = -1;
		ready.Clear();
		spawning.Clear();
		usedSpawnPoints.Clear();
		spawnIndexByConnId.Clear();
		foreach (KeyValuePair<int, NetworkConnectionToClient> connection in NetworkServer.connections)
		{
			NetworkConnectionToClient value = connection.Value;
			if (value != null)
			{
				int connectionId = value.connectionId;
				registered.Add(connectionId);
				if (value.identity != null)
				{
					NetworkServer.RemovePlayerForConnection(value, RemovePlayerOptions.Destroy);
				}
			}
		}
		sceneAcceptingSpawns = true;
		if (timeoutCoroutine != null)
		{
			StopCoroutine(timeoutCoroutine);
		}
		timeoutCoroutine = StartCoroutine(SpawnTimeoutRoutine());
		foreach (KeyValuePair<int, NetworkConnectionToClient> connection2 in NetworkServer.connections)
		{
			NetworkConnectionToClient value2 = connection2.Value;
			if (value2 != null && value2.isReady)
			{
				TrySpawnForConnection(value2);
			}
		}
	}

	[Server]
	public void RegisterConnection(NetworkConnectionToClient conn)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void PlayerSpawnManager::RegisterConnection(Mirror.NetworkConnectionToClient)' called when server was not active");
		}
		else if (conn != null)
		{
			int connectionId = conn.connectionId;
			if (registered.Add(connectionId))
			{
				UnityEngine.Debug.Log($"Registered connection {connectionId}. Total registered: {registered.Count}");
			}
			CheckSpawningReady();
		}
	}

	[Server]
	public void OnClientSceneReady(NetworkConnectionToClient conn)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void PlayerSpawnManager::OnClientSceneReady(Mirror.NetworkConnectionToClient)' called when server was not active");
		}
		else if (conn != null)
		{
			int connectionId = conn.connectionId;
			if (ready.Add(connectionId))
			{
				UnityEngine.Debug.Log($"Connection {connectionId} is ready. Total ready: {ready.Count}");
			}
			CheckSpawningReady();
			if (!sceneAcceptingSpawns)
			{
				UnityEngine.Debug.LogWarning($"Connection {connectionId} ready but scene not accepting spawns yet");
			}
			else
			{
				TrySpawnForConnection(conn);
			}
		}
	}

	[Server]
	public void ServerOnDisconnected(NetworkConnectionToClient conn)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void PlayerSpawnManager::ServerOnDisconnected(Mirror.NetworkConnectionToClient)' called when server was not active");
		}
		else if (conn != null)
		{
			int connectionId = conn.connectionId;
			registered.Remove(connectionId);
			ready.Remove(connectionId);
			spawning.Remove(connectionId);
			if (spawnIndexByConnId.TryGetValue(connectionId, out var value))
			{
				usedSpawnPoints.Remove(value);
			}
			spawnIndexByConnId.Remove(connectionId);
		}
	}

	[Server]
	private void TryInitializeIfComplete()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void PlayerSpawnManager::TryInitializeIfComplete()' called when server was not active");
		}
		else
		{
			if (!sceneAcceptingSpawns)
			{
				return;
			}
			int num = Mathf.Max(1, registered.Count);
			if (registered.Count < num || ready.Count < num)
			{
				return;
			}
			int num2 = 0;
			foreach (KeyValuePair<int, NetworkConnectionToClient> connection in NetworkServer.connections)
			{
				NetworkConnectionToClient value = connection.Value;
				if (value != null)
				{
					int connectionId = value.connectionId;
					if (registered.Contains(connectionId) && ready.Contains(connectionId) && value.identity != null)
					{
						num2++;
					}
				}
			}
			if (num2 >= num && initializedEpoch != sceneEpoch)
			{
				initializedEpoch = sceneEpoch;
				OnAllPlayersSpawned?.Invoke();
			}
		}
	}

	[Server]
	private void CheckSpawningReady()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void PlayerSpawnManager::CheckSpawningReady()' called when server was not active");
		}
		else
		{
			if (spawningEnabled || !sceneAcceptingSpawns)
			{
				return;
			}
			int num = Mathf.Max(1, registered.Count);
			if (registered.Count >= num && ready.Count >= num)
			{
				spawningEnabled = true;
				if (timeoutCoroutine != null)
				{
					StopCoroutine(timeoutCoroutine);
					timeoutCoroutine = null;
				}
				StartInitialSpawnSequence();
			}
		}
	}

	[Server]
	private void TrySpawnForConnection(NetworkConnectionToClient conn)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void PlayerSpawnManager::TrySpawnForConnection(Mirror.NetworkConnectionToClient)' called when server was not active");
		}
		else
		{
			if (conn == null || !conn.isReady)
			{
				return;
			}
			if (!spawningEnabled)
			{
				UnityEngine.Debug.Log($"TrySpawnForConnection: Spawning not enabled yet for connection {conn.connectionId}");
				return;
			}
			if (!initialSpawnSequenceStarted)
			{
				UnityEngine.Debug.Log($"TrySpawnForConnection: Initial spawn sequence not started for connection {conn.connectionId}");
				return;
			}
			int connectionId = conn.connectionId;
			if (!registered.Contains(connectionId))
			{
				UnityEngine.Debug.LogWarning($"TrySpawnForConnection: Connection {connectionId} not registered");
				return;
			}
			if (!ready.Contains(connectionId))
			{
				UnityEngine.Debug.LogWarning($"TrySpawnForConnection: Connection {connectionId} not ready");
				return;
			}
			if (spawning.Contains(connectionId))
			{
				UnityEngine.Debug.LogWarning($"TrySpawnForConnection: Connection {connectionId} already spawning");
				return;
			}
			if (conn.identity != null)
			{
				UnityEngine.Debug.LogWarning($"TrySpawnForConnection: Connection {connectionId} already has identity");
				return;
			}
			List<Transform> startPositions = NetworkManager.startPositions;
			if (startPositions == null || startPositions.Count == 0)
			{
				UnityEngine.Debug.LogError($"TrySpawnForConnection: No spawn points available! Connection {connectionId} cannot spawn.");
				return;
			}
			UnityEngine.Debug.Log($"Attempting to spawn player for connection {connectionId}");
			spawning.Add(connectionId);
			StartCoroutine(SpawnRoutine(conn, startPositions));
		}
	}

	[IteratorStateMachine(typeof(_003CSpawnRoutine_003Ed__30))]
	[Server]
	private IEnumerator SpawnRoutine(NetworkConnectionToClient conn, List<Transform> starts)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.IEnumerator PlayerSpawnManager::SpawnRoutine(Mirror.NetworkConnectionToClient,System.Collections.Generic.List`1<UnityEngine.Transform>)' called when server was not active");
			return null;
		}
		return new _003CSpawnRoutine_003Ed__30(0)
		{
			_003C_003E4__this = this,
			conn = conn,
			starts = starts
		};
	}

	[TargetRpc]
	private void TargetLockPlayerInputs(NetworkConnection conn)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendTargetRPCInternal(conn, "System.Void PlayerSpawnManager::TargetLockPlayerInputs(Mirror.NetworkConnection)", 851145063, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	[Server]
	private void StartInitialSpawnSequence()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Void PlayerSpawnManager::StartInitialSpawnSequence()' called when server was not active");
		}
		else if (!initialSpawnSequenceStarted)
		{
			initialSpawnSequenceStarted = true;
			StartCoroutine(InitialSpawnSequenceRoutine());
		}
	}

	[IteratorStateMachine(typeof(_003CInitialSpawnSequenceRoutine_003Ed__33))]
	[Server]
	private IEnumerator InitialSpawnSequenceRoutine()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.IEnumerator PlayerSpawnManager::InitialSpawnSequenceRoutine()' called when server was not active");
			return null;
		}
		return new _003CInitialSpawnSequenceRoutine_003Ed__33(0)
		{
			_003C_003E4__this = this
		};
	}

	[TargetRpc]
	private void TargetSceneTransition(NetworkConnection conn)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		SendTargetRPCInternal(conn, "System.Void PlayerSpawnManager::TargetSceneTransition(Mirror.NetworkConnection)", -2064852653, writer, 0);
		NetworkWriterPool.Return(writer);
	}

	[IteratorStateMachine(typeof(_003CSpawnTimeoutRoutine_003Ed__35))]
	[Server]
	private IEnumerator SpawnTimeoutRoutine()
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Collections.IEnumerator PlayerSpawnManager::SpawnTimeoutRoutine()' called when server was not active");
			return null;
		}
		return new _003CSpawnTimeoutRoutine_003Ed__35(0)
		{
			_003C_003E4__this = this
		};
	}

	[Server]
	private int GetSpawnIndexFor(int connId, int total)
	{
		if (!NetworkServer.active)
		{
			UnityEngine.Debug.LogWarning("[Server] function 'System.Int32 PlayerSpawnManager::GetSpawnIndexFor(System.Int32,System.Int32)' called when server was not active");
			return default;
		}
		if (spawnIndexByConnId.TryGetValue(connId, out var value))
		{
			return value;
		}
		List<int> list = new List<int>();
		for (int i = 0; i < total; i++)
		{
			if (!usedSpawnPoints.Contains(i))
			{
				list.Add(i);
			}
		}
		int num = ((list.Count <= 0) ? UnityEngine.Random.Range(0, total) : list[UnityEngine.Random.Range(0, list.Count)]);
		usedSpawnPoints.Add(num);
		spawnIndexByConnId[connId] = num;
		return num;
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_TargetLockPlayerInputs__NetworkConnection(NetworkConnection conn)
	{
		InputEvents.ActiveLayer = InputLayer.SpawnBox;
	}

	protected static void InvokeUserCode_TargetLockPlayerInputs__NetworkConnection(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("TargetRPC TargetLockPlayerInputs called on server.");
		}
		else
		{
			((PlayerSpawnManager)obj).UserCode_TargetLockPlayerInputs__NetworkConnection(null);
		}
	}

	protected void UserCode_TargetSceneTransition__NetworkConnection(NetworkConnection conn)
	{
		MonoSingleton<SceneTransitioner>.Instance.SetLoadingScreen(isEnabled: false, 0.5f);
	}

	protected static void InvokeUserCode_TargetSceneTransition__NetworkConnection(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			UnityEngine.Debug.LogError("TargetRPC TargetSceneTransition called on server.");
		}
		else
		{
			((PlayerSpawnManager)obj).UserCode_TargetSceneTransition__NetworkConnection(null);
		}
	}

	static PlayerSpawnManager()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerSpawnManager), "System.Void PlayerSpawnManager::TargetLockPlayerInputs(Mirror.NetworkConnection)", InvokeUserCode_TargetLockPlayerInputs__NetworkConnection);
		RemoteProcedureCalls.RegisterRpc(typeof(PlayerSpawnManager), "System.Void PlayerSpawnManager::TargetSceneTransition(Mirror.NetworkConnection)", InvokeUserCode_TargetSceneTransition__NetworkConnection);
	}
}

}
