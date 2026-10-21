namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::UnityEngine;

public class ItemManager : NetworkSingleton<ItemManager>
{
	public List<SpawnableSO> currentItems = new List<SpawnableSO>();

	public List<ConsumableItem> spawnedItemInstances = new List<ConsumableItem>();

	public void ServerAddItem(SpawnableSO spawnableSo)
	{
		currentItems.Add(spawnableSo);
	}

	[Server]
	public void ServerRemoveItem(SpawnableSO spawnableSo, ConsumableItem itemInstance)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ItemManager::ServerRemoveItem(SpawnableSO,ConsumableItem)' called when server was not active");
			return;
		}
		if (currentItems.Contains(spawnableSo))
		{
			currentItems.Remove(spawnableSo);
		}
		if (spawnedItemInstances.Contains(itemInstance))
		{
			spawnedItemInstances.Remove(itemInstance);
		}
	}

	[Server]
	public List<int> GetCurrentItemIds()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Collections.Generic.List`1<System.Int32> ItemManager::GetCurrentItemIds()' called when server was not active");
			return null;
		}
		List<int> list = new List<int>();
		foreach (SpawnableSO currentItem in currentItems)
		{
			if ((bool)currentItem)
			{
				list.Add(currentItem.spawnableID);
			}
		}
		return list;
	}

	[Server]
	public void SetCurrentItems(List<int> itemIds)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ItemManager::SetCurrentItems(System.Collections.Generic.List`1<System.Int32>)' called when server was not active");
			return;
		}
		currentItems.Clear();
		foreach (int itemId in itemIds)
		{
			SpawnableSO spawnableSoById = SpawnableSettings.GetSpawnableSoById(itemId);
			if ((bool)spawnableSoById)
			{
				currentItems.Add(spawnableSoById);
			}
		}
	}

	[Server]
	public void ServerResetItems()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void ItemManager::ServerResetItems()' called when server was not active");
			return;
		}
		currentItems.Clear();
		spawnedItemInstances.Clear();
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
