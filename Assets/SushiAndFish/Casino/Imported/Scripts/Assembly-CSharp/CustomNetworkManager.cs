namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::UnityEngine;
using global::UnityEngine.SceneManagement;

public class CustomNetworkManager : NetworkManager
{
	private string lastSceneReadySent;

	private bool sentJoin;

    public override void ServerChangeScene(string newSceneName)
    {
        if(CasinoLab.CasinoLabMode.Active && !CasinoRuntimeContext.IsHostIntegration)DG.Tweening.DOTween.KillAll(false);
        base.ServerChangeScene(newSceneName);
    }

	[SerializeField]
	private GameObject managersPrefab;

	public override void OnStartServer()
	{
		base.OnStartServer();
		NetworkServer.ReplaceHandler<JoinGameMessage>(OnJoinGameMessage);
		NetworkServer.ReplaceHandler<SceneReadyMessage>(OnSceneReadyMessage);
		NetworkServer.ReplaceHandler<ClientScenePlayReadyMessage>(OnClientScenePlayReadyMessage);
		SpawnManagersPrefab();
		if (NetworkSingleton<PlayerSpawnManager>.Instance != null)
		{
			NetworkSingleton<PlayerSpawnManager>.Instance.OnAllPlayersSpawned -= OnAllPlayersSpawned;
			NetworkSingleton<PlayerSpawnManager>.Instance.OnAllPlayersSpawned += OnAllPlayersSpawned;
			NetworkSingleton<PlayerSpawnManager>.Instance.ServerOnSceneChanged(SceneManager.GetActiveScene().name);
		}
	}

	public override void OnStopServer()
	{
		base.OnStopServer();
		NetworkServer.UnregisterHandler<JoinGameMessage>();
		NetworkServer.UnregisterHandler<SceneReadyMessage>();
		NetworkServer.UnregisterHandler<ClientScenePlayReadyMessage>();
		if (NetworkSingleton<PlayerSpawnManager>.Instance != null)
		{
			NetworkSingleton<PlayerSpawnManager>.Instance.OnAllPlayersSpawned -= OnAllPlayersSpawned;
		}
	}

	public override void OnClientConnect()
	{
		base.OnClientConnect();
		if (!NetworkClient.ready)
		{
			NetworkClient.Ready();
		}
		if (!sentJoin)
		{
			sentJoin = true;
			NetworkClient.Send<JoinGameMessage>(default);
		}
		string text = SceneManager.GetActiveScene().name;
		if (!string.IsNullOrEmpty(text) && text != lastSceneReadySent)
		{
			lastSceneReadySent = text;
			StartCoroutine(SendSceneReadyNextFrame());
		}
	}

	public override void OnStopClient()
	{
		base.OnStopClient();
		sentJoin = false;
		lastSceneReadySent = null;
	}

	public override void OnClientSceneChanged()
	{
		base.OnClientSceneChanged();
		if (NetworkClient.isConnected)
		{
			if (!NetworkClient.ready)
			{
				NetworkClient.Ready();
			}
			string text = SceneManager.GetActiveScene().name;
			if (!string.IsNullOrEmpty(text) && !(text == lastSceneReadySent))
			{
				lastSceneReadySent = text;
				StartCoroutine(SendSceneReadyNextFrame());
			}
		}
	}

	private IEnumerator SendSceneReadyNextFrame()
	{
		yield return null;
		if (NetworkClient.isConnected)
		{
			NetworkClient.Send<SceneReadyMessage>(default);
		}
	}

	public override void OnServerSceneChanged(string sceneName)
	{
		base.OnServerSceneChanged(sceneName);
		if (NetworkSingleton<PlayerSpawnManager>.Instance != null)
		{
			NetworkSingleton<PlayerSpawnManager>.Instance.OnAllPlayersSpawned -= OnAllPlayersSpawned;
			NetworkSingleton<PlayerSpawnManager>.Instance.OnAllPlayersSpawned += OnAllPlayersSpawned;
			NetworkSingleton<PlayerSpawnManager>.Instance.ServerOnSceneChanged(sceneName);
		}
	}

	private void OnAllPlayersSpawned()
	{
		if (NetworkServer.active && !(NetworkSingleton<GameManager>.Instance == null))
		{
			string sceneName = SceneManager.GetActiveScene().name;
			if (CasinoLab.CasinoLabMode.Active) CasinoLab.CasinoLabBootstrap.Instance.OnOriginalPlayersSpawned();
			else NetworkSingleton<GameManager>.Instance.InitializeScene(sceneName);
		}
	}

	private void OnJoinGameMessage(NetworkConnectionToClient conn, JoinGameMessage message)
	{
		NetworkSingleton<PlayerSpawnManager>.Instance?.RegisterConnection(conn);
	}

	private void OnSceneReadyMessage(NetworkConnectionToClient conn, SceneReadyMessage message)
	{
		NetworkSingleton<PlayerSpawnManager>.Instance?.OnClientSceneReady(conn);
	}

	private void OnClientScenePlayReadyMessage(NetworkConnectionToClient conn, ClientScenePlayReadyMessage message)
	{
		NetworkSingleton<GameManager>.Instance?.ServerOnClientScenePlayReady(conn, message.epoch);
	}

	public override void OnServerDisconnect(NetworkConnectionToClient conn)
	{
		if (conn?.identity != null)
		{
			if (conn.identity.TryGetComponent<PlayerCarry>(out var component) && component.TryGetHolderInventory(out var holderInventory))
			{
				holderInventory.ServerDropHoldingItem();
			}
			if (conn.identity.TryGetComponent<PlayerInventory>(out var component2))
			{
				component2.ServerDropHoldingItem();
			}
		}
		NetworkSingleton<PlayerSpawnManager>.Instance?.ServerOnDisconnected(conn);
		SkipUI[] array = Object.FindObjectsByType<SkipUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
		for (int i = 0; i < array.Length; i++)
		{
			array[i].ServerRegisterSkipFromConnection(conn);
		}
		base.OnServerDisconnect(conn);
	}

	public override void OnServerAddPlayer(NetworkConnectionToClient conn)
	{
	}

	[Server]
	private void SpawnManagersPrefab()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void CustomNetworkManager::SpawnManagersPrefab()' called when server was not active");
		}
		else
		{
			NetworkServer.Spawn(Object.Instantiate(managersPrefab));
		}
	}
}

}
