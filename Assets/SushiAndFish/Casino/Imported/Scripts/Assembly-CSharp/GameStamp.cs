namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class GameStamp : NetworkBehaviour
{
	[Header("Game Prefab")]
	[Tooltip("The casino game prefab to spawn. Must have a GameBase component.")]
	[SerializeField]
	private GameObject gamePrefab;

	[Header("Floor Reference")]
	[Tooltip("The casino floor this game belongs to. Used to set the casinoLevel on spawned games.")]
	[SerializeField]
	public CasinoFloor floor;

	[Header("Spawn Settings")]
	[Tooltip("If true, spawns immediately on server start. If false, waits for Initialize() to be called.")]
	[SerializeField]
	private bool spawnOnServerStart = true;

	[Header("Preview")]
	[SerializeField]
	private GameObject currentPreview;

	[SerializeField]
	public bool previewEnabled = true;

	private string PreviewStatus
	{
		get
		{
			if (!previewEnabled)
			{
				return "Preview Disabled";
			}
			if (!(currentPreview != null))
			{
				return "Preview Disabled";
			}
			return "Preview Active";
		}
	}

	private void OnGamePrefabChanged()
	{
		if (!Application.isPlaying && previewEnabled)
		{
			UpdatePreview();
		}
	}

	private void OnFloorChanged()
	{
		if (!Application.isPlaying && previewEnabled)
		{
			UpdatePreview();
		}
	}

	public override void OnStartServer()
	{
		foreach (Transform item in base.transform)
		{
			NetworkServer.Destroy(item.gameObject);
		}
		if (spawnOnServerStart)
		{
			Initialize();
		}
	}

	public void Initialize()
	{
		if (base.isServer)
		{
			if (gamePrefab == null)
			{
				Debug.LogWarning("GameStamp on " + base.gameObject.name + ": Game prefab is not assigned. Skipping initialization.");
				NetworkServer.Destroy(base.gameObject);
			}
			else if (gamePrefab.GetComponentInChildren<GameBase>() == null)
			{
				Debug.LogWarning("GameStamp on " + base.gameObject.name + ": Prefab " + gamePrefab.name + " does not have a GameBase component. Skipping initialization.");
				NetworkServer.Destroy(base.gameObject);
			}
			else
			{
				SpawnGame(gamePrefab, base.transform.position, base.transform.rotation, base.transform.localScale);
			}
		}
	}

	[Server]
	private void SpawnGame(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void GameStamp::SpawnGame(UnityEngine.GameObject,UnityEngine.Vector3,UnityEngine.Quaternion,UnityEngine.Vector3)' called when server was not active");
			return;
		}
		if (prefab == null)
		{
			Debug.LogWarning("GameStamp: Tried to spawn null prefab on " + base.gameObject.name);
			NetworkServer.Destroy(base.gameObject);
			return;
		}
		GameObject gameObject = Object.Instantiate(prefab, position, rotation);
		gameObject.transform.localScale = scale;
		NetworkServer.Spawn(gameObject);
		if (floor != null)
		{
			GameBase componentInChildren = gameObject.GetComponentInChildren<GameBase>();
			if (componentInChildren != null)
			{
				componentInChildren.NetworkcasinoLevel = floor.floorIndex;
			}
		}
		StartCoroutine(SetParentHierarchyAfterDelay(gameObject));
	}

	private IEnumerator SetParentHierarchyAfterDelay(GameObject spawnedGame)
	{
		yield return new WaitForSeconds(0.2f);
		RpcSetParentHierarchy(spawnedGame);
	}

	[ClientRpc]
	private void RpcSetParentHierarchy(GameObject spawnedGame)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteGameObject(spawnedGame);
		SendRPCInternal("System.Void GameStamp::RpcSetParentHierarchy(UnityEngine.GameObject)", -745019018, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public void UpdatePreview()
	{
		if (!Application.isPlaying && previewEnabled)
		{
			ClearPreview();
			if (!(gamePrefab == null))
			{
				currentPreview = Object.Instantiate(gamePrefab, base.transform.position, base.transform.rotation);
				currentPreview.transform.SetParent(base.transform);
				SetPreviewMode(currentPreview, isPreview: true);
			}
		}
	}

	private void SetPreviewMode(GameObject target, bool isPreview)
	{
		if (isPreview)
		{
			Collider[] componentsInChildren = target.GetComponentsInChildren<Collider>();
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].enabled = false;
			}
			Rigidbody[] componentsInChildren2 = target.GetComponentsInChildren<Rigidbody>();
			foreach (Rigidbody obj in componentsInChildren2)
			{
				obj.isKinematic = true;
				obj.detectCollisions = false;
			}
			GameBase componentInChildren = target.GetComponentInChildren<GameBase>();
			if (componentInChildren != null)
			{
				componentInChildren.enabled = false;
			}
		}
	}

	public void ClearPreview()
	{
		if (Application.isPlaying)
		{
			return;
		}
		while (base.transform.childCount > 0)
		{
			Transform child = base.transform.GetChild(0);
			if (child != null && child.gameObject != null)
			{
				Object.DestroyImmediate(child.gameObject);
			}
		}
		currentPreview = null;
	}

	private void OnDrawGizmosSelected()
	{
		if (!Application.isPlaying && currentPreview != null && currentPreview.transform != null)
		{
			currentPreview.transform.position = base.transform.position;
			currentPreview.transform.rotation = base.transform.rotation;
		}
	}

	public override bool Weaved()
	{
		return true;
	}

	protected void UserCode_RpcSetParentHierarchy__GameObject(GameObject spawnedGame)
	{
		if (spawnedGame != null && this != null && base.transform != null && base.transform.parent != null)
		{
			spawnedGame.transform.SetParent(base.transform.parent);
		}
		if (this != null)
		{
			Object.Destroy(base.gameObject);
		}
	}

	protected static void InvokeUserCode_RpcSetParentHierarchy__GameObject(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetParentHierarchy called on server.");
		}
		else
		{
			((GameStamp)obj).UserCode_RpcSetParentHierarchy__GameObject(reader.ReadGameObject());
		}
	}

	static GameStamp()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(GameStamp), "System.Void GameStamp::RpcSetParentHierarchy(UnityEngine.GameObject)", InvokeUserCode_RpcSetParentHierarchy__GameObject);
	}
}

}
