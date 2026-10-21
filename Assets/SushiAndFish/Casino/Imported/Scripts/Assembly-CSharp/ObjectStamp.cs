namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::UnityEngine;

public class ObjectStamp : NetworkBehaviour
{
	[Header("Floor Reference")]
	[SerializeField]
	private CasinoFloor floor;

	[SerializeField]
	private bool requireFloorData = true;

	[Header("Preview")]
	[SerializeField]
	private GameObject currentPreview;

	[SerializeField]
	public bool previewEnabled = true;

	[Header("Spawn Settings")]
	[Tooltip("If true, spawns immediately on server start instead of waiting for StampManager initialization. Also automatically disables floor data requirement.")]
	[SerializeField]
	private bool spawnOnServerStart;

	[Header("Direct Spawn (Server Start Mode)")]
	[Tooltip("Prefab to spawn directly when spawnOnServerStart is enabled. If not set, will use floor-based loot system.")]
	[SerializeField]
	private GameObject directSpawnPrefab;

	public CasinoFloor Floor => floor;

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

	private void OnFloorChanged()
	{
		if (!Application.isPlaying && previewEnabled)
		{
			UpdatePreview();
		}
	}

	public void UpdatePreview()
	{
		if (Application.isPlaying || !previewEnabled)
		{
			return;
		}
		ClearPreview();
		if (!(floor == null))
		{
			GameObject previewPrefab = GetPreviewPrefab();
			if (previewPrefab != null)
			{
				currentPreview = Object.Instantiate(previewPrefab, base.transform.position, base.transform.rotation);
				currentPreview.transform.SetParent(base.transform);
				SetPreviewMode(currentPreview, isPreview: true);
			}
			else
			{
				Debug.LogWarning("No preview prefab found for this floor (check StampManager floor loot tables).");
			}
		}
	}

	private GameObject GetPreviewPrefab()
	{
		if (floor == null)
		{
			return null;
		}
		StampManager stampManager = Object.FindAnyObjectByType<StampManager>();
		if (stampManager == null)
		{
			return null;
		}
		return stampManager.GetRandomPreviewPrefabForFloor(floor);
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
		if (!base.isServer)
		{
			return;
		}
		GameObject gameObject = null;
		bool flag = requireFloorData && !spawnOnServerStart;
		if (directSpawnPrefab != null)
		{
			gameObject = directSpawnPrefab;
		}
		else if (floor != null && NetworkSingleton<StampManager>.Instance != null)
		{
			gameObject = NetworkSingleton<StampManager>.Instance.GetCappedLootForFloor(floor, base.transform.position);
		}
		else
		{
			if (flag && floor == null)
			{
				Debug.LogWarning("ObjectStamp on " + base.gameObject.name + ": Floor data is required but not set. Skipping initialization.");
				NetworkServer.Destroy(base.gameObject);
				return;
			}
			if (spawnOnServerStart && directSpawnPrefab == null)
			{
				Debug.Log("ObjectStamp on " + base.gameObject.name + ": spawnOnServerStart is enabled but no directSpawnPrefab is set. Stamp will remain inactive.");
				return;
			}
		}
		if (gameObject == null)
		{
			if (!spawnOnServerStart)
			{
				NetworkServer.Destroy(base.gameObject);
			}
		}
		else
		{
			SpawnObject(gameObject, base.transform.position, base.transform.rotation, base.transform.localScale);
		}
	}

	[Server]
	private void SpawnObject(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ObjectStamp::SpawnObject(UnityEngine.GameObject,UnityEngine.Vector3,UnityEngine.Quaternion,UnityEngine.Vector3)' called when server was not active");
			return;
		}
		if (prefab == null)
		{
			Debug.LogWarning("ObjectStamp: Tried to spawn null prefab on " + base.gameObject.name);
			NetworkServer.Destroy(base.gameObject);
			return;
		}
		GameObject gameObject = Object.Instantiate(prefab, position, rotation);
		gameObject.transform.localScale = scale;
		NetworkServer.Spawn(gameObject);
		foreach (Transform item in gameObject.transform)
		{
			foreach (Transform item2 in item)
			{
				if (item2.TryGetComponent<GameStamp>(out var component))
				{
					component.floor = floor;
					component.Initialize();
				}
			}
		}
		RpcSetParentHierarchy(gameObject);
	}

	[ClientRpc]
	private void RpcSetParentHierarchy(GameObject spawnedObject)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteGameObject(spawnedObject);
		SendRPCInternal("System.Void ObjectStamp::RpcSetParentHierarchy(UnityEngine.GameObject)", -2142009037, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	private void SetPreviewMode(GameObject target, bool isPreview)
	{
		if (!isPreview)
		{
			return;
		}
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
		MonoBehaviour[] componentsInChildren3 = target.GetComponentsInChildren<MonoBehaviour>();
		foreach (MonoBehaviour monoBehaviour in componentsInChildren3)
		{
			if (monoBehaviour is GameBase)
			{
				monoBehaviour.enabled = false;
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

	protected void UserCode_RpcSetParentHierarchy__GameObject(GameObject spawnedObject)
	{
		spawnedObject.transform.SetParent(base.gameObject.transform.parent);
		Object.Destroy(base.gameObject);
	}

	protected static void InvokeUserCode_RpcSetParentHierarchy__GameObject(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("RPC RpcSetParentHierarchy called on server.");
		}
		else
		{
			((ObjectStamp)obj).UserCode_RpcSetParentHierarchy__GameObject(reader.ReadGameObject());
		}
	}

	static ObjectStamp()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(ObjectStamp), "System.Void ObjectStamp::RpcSetParentHierarchy(UnityEngine.GameObject)", InvokeUserCode_RpcSetParentHierarchy__GameObject);
	}
}

}
