namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::UnityEngine;

[CreateAssetMenu(menuName = "Game Settings/Item Description Settings", fileName = "ItemDescriptionSettings")]
public class ItemDescriptionSettings : ScriptableObject
{
	[Serializable]
	public class ItemDescriptionData
	{
		[Tooltip("The SpawnableSO this description applies to.")]
		public SpawnableSO spawnableSO;

		[Tooltip("Short description explaining how this item works.")]
		[TextArea(2, 5)]
		public string description = "";
	}

	[Header("Item Descriptions")]
	[Tooltip("List of items and their descriptions.")]
	[SerializeField]
	private List<ItemDescriptionData> itemDescriptions = new List<ItemDescriptionData>();

	[Header("Global Settings")]
	[Tooltip("Default description for items not in the list.")]
	[TextArea(2, 5)]
	[SerializeField]
	private string defaultDescription = "";

	private Dictionary<int, ItemDescriptionData> _descriptionCache;

	public string GetDescription(SpawnableSO spawnableSO)
	{
		if (spawnableSO == null)
		{
			return defaultDescription;
		}
		BuildCacheIfNeeded();
		if (_descriptionCache.TryGetValue(spawnableSO.spawnableID, out var value))
		{
			if (!string.IsNullOrEmpty(value.description))
			{
				return value.description;
			}
			return defaultDescription;
		}
		return defaultDescription;
	}

	private void BuildCacheIfNeeded()
	{
		if (_descriptionCache != null)
		{
			return;
		}
		_descriptionCache = new Dictionary<int, ItemDescriptionData>();
		foreach (ItemDescriptionData itemDescription in itemDescriptions)
		{
			if (itemDescription.spawnableSO != null)
			{
				_descriptionCache[itemDescription.spawnableSO.spawnableID] = itemDescription;
			}
		}
	}

	private void OnValidate()
	{
		_descriptionCache = null;
	}
}

}
