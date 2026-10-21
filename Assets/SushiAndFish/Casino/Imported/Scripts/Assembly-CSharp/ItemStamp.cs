namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Linq;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::Mirror.RemoteCalls;
using global::MoreMountains.Tools;
using global::UnityEngine;

public class ItemStamp : NetworkBehaviour
{
	[Header("Loot Table")]
	[Tooltip("The loot table containing item prefabs to spawn. Items should be SpawnableSO prefabs.")]
	[SerializeField]
	private MMLootTableGameObjectSO lootTable;

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

	public override void OnStartServer()
	{
		foreach (Transform item in base.transform)
		{
			NetworkServer.Destroy(item.gameObject);
		}
	}

	public void Initialize()
	{
		if (!base.isServer)
		{
			return;
		}
		if (lootTable == null)
		{
			Debug.LogWarning("ItemStamp on " + base.gameObject.name + ": Loot table is not assigned. Skipping initialization.");
			NetworkServer.Destroy(base.gameObject);
			return;
		}
		if (NetworkSingleton<ItemStampManager>.Instance == null)
		{
			Debug.LogError("ItemStamp on " + base.gameObject.name + ": ItemStampManager.Instance is null! Cannot spawn item deterministically.");
			NetworkServer.Destroy(base.gameObject);
			return;
		}
		GameObject uniqueLoot = NetworkSingleton<ItemStampManager>.Instance.GetUniqueLoot(lootTable, base.transform.position);
		if (uniqueLoot == null)
		{
			Debug.LogWarning("ItemStamp on " + base.gameObject.name + ": Could not get item prefab. Skipping initialization.");
			NetworkServer.Destroy(base.gameObject);
		}
		else if (uniqueLoot.GetComponent<Item>() == null)
		{
			Debug.LogWarning("ItemStamp on " + base.gameObject.name + ": Prefab " + uniqueLoot.name + " does not have an Item component. Skipping initialization.");
			NetworkServer.Destroy(base.gameObject);
		}
		else
		{
			SpawnItem(uniqueLoot, base.transform.position, base.transform.rotation, base.transform.localScale);
		}
	}

	[Server]
	private void SpawnItem(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ItemStamp::SpawnItem(UnityEngine.GameObject,UnityEngine.Vector3,UnityEngine.Quaternion,UnityEngine.Vector3)' called when server was not active");
			return;
		}
		if (prefab == null)
		{
			Debug.LogWarning("ItemStamp: Tried to spawn null prefab on " + base.gameObject.name);
			return;
		}
		GameObject gameObject = Object.Instantiate(prefab, position, rotation);
		gameObject.transform.localScale = scale;
		NetworkServer.Spawn(gameObject);
		if (NetworkSingleton<ItemStampManager>.Instance != null)
		{
			NetworkSingleton<ItemStampManager>.Instance.RegisterSpawnedInstance(gameObject, this);
		}
		StartCoroutine(SetParentHierarchyAfterDelay(gameObject));
	}

	private IEnumerator SetParentHierarchyAfterDelay(GameObject spawnedItem)
	{
		yield return new WaitForSeconds(0.2f);
		RpcSetParentHierarchy(spawnedItem);
	}

	[ClientRpc]
	private void RpcSetParentHierarchy(GameObject spawnedItem)
	{
		NetworkWriterPooled writer = NetworkWriterPool.Get();
		writer.WriteGameObject(spawnedItem);
		SendRPCInternal("System.Void ItemStamp::RpcSetParentHierarchy(UnityEngine.GameObject)", 1388244683, writer, 0, includeOwner: true);
		NetworkWriterPool.Return(writer);
	}

	public void UpdatePreview()
	{
		if (Application.isPlaying || !previewEnabled)
		{
			return;
		}
		ClearPreview();
		if (!(lootTable == null))
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
				Debug.LogWarning("ItemStamp on " + base.gameObject.name + ": No preview prefab found from loot table.");
			}
		}
	}

	private GameObject GetPreviewPrefab()
	{
		if (lootTable == null || lootTable.LootTable == null || lootTable.LootTable.ObjectsToLoot == null)
		{
			return null;
		}
		List<MMLootGameObject> objectsToLoot = lootTable.LootTable.ObjectsToLoot;
		if (objectsToLoot == null || objectsToLoot.Count == 0)
		{
			return null;
		}
		List<GameObject> list = (from x in objectsToLoot
			where x != null && x.Loot != null
			select x.Loot).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		return list[NetworkSingleton<SeededRandomManager>.Instance.Range(0, list.Count)];
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
			Item component = target.GetComponent<Item>();
			if (component != null)
			{
				component.enabled = false;
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

	protected void UserCode_RpcSetParentHierarchy__GameObject(GameObject spawnedItem)
	{
		if (spawnedItem != null && this != null && base.transform != null && base.transform.parent != null)
		{
			spawnedItem.transform.SetParent(base.transform.parent);
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
			((ItemStamp)obj).UserCode_RpcSetParentHierarchy__GameObject(reader.ReadGameObject());
		}
	}

	static ItemStamp()
	{
		RemoteProcedureCalls.RegisterRpc(typeof(ItemStamp), "System.Void ItemStamp::RpcSetParentHierarchy(UnityEngine.GameObject)", InvokeUserCode_RpcSetParentHierarchy__GameObject);
	}
}

}
