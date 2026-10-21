namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.SceneManagement;
using global::UnityEngine.UI;

public class HeldItemActionPanel : MonoBehaviour
{
	[Serializable]
	private class KeyButtonImageData
	{
		public string keyName;

		public Sprite sprite;
	}

	[Header("References")]
	[Tooltip("Transform where action buttons will be created")]
	[SerializeField]
	private Transform actionParent;

	[Header("Item Action Prefab")]
	[Tooltip("Prefab that contains both a key button and text element as children")]
	[SerializeField]
	private GameObject itemActionPrefab;

	[Header("Key Button Images")]
	[Tooltip("Dictionary of key names to their corresponding sprites (e.g., 'Left Click' -> sprite)")]
	[SerializeField]
	private List<KeyButtonImageData> keyButtonImages = new List<KeyButtonImageData>();

	[SerializeField]
	private PlayerInventory playerInventory;

	[SerializeField]
	private Item _currentHeldItem;

	[SerializeField]
	private List<GameObject> _activeActionElements = new List<GameObject>();

	private void OnEnable()
	{
		SceneManager.sceneLoaded += OnSceneLoaded;
		if ((bool)playerInventory)
		{
			playerInventory.OnLocalInventoryUpdated += OnInventoryUpdated;
		}
	}

	private void OnDisable()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
		if ((bool)playerInventory)
		{
			playerInventory.OnLocalInventoryUpdated -= OnInventoryUpdated;
		}
	}

	private void Start()
	{
		ClearActionParent();
	}

	public void SetPlayerInventory(PlayerInventory inventory)
	{
		if ((bool)playerInventory)
		{
			playerInventory.OnLocalInventoryUpdated -= OnInventoryUpdated;
		}
		playerInventory = inventory;
		playerInventory.OnLocalInventoryUpdated += OnInventoryUpdated;
		RefreshActions();
	}

	private void ClearActionParent()
	{
		if (actionParent != null)
		{
			for (int num = actionParent.childCount - 1; num >= 0; num--)
			{
				UnityEngine.Object.Destroy(actionParent.GetChild(num).gameObject);
			}
			_activeActionElements.Clear();
		}
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		if ((bool)playerInventory)
		{
			playerInventory.OnLocalInventoryUpdated -= OnInventoryUpdated;
		}
		playerInventory = null;
		_currentHeldItem = null;
		ClearActionParent();
	}

	private void OnInventoryUpdated()
	{
		RefreshActions();
	}

	private void RefreshActions()
	{
		if (playerInventory == null)
		{
			return;
		}
		Item networkholdingItem = playerInventory.NetworkholdingItem;
		if (networkholdingItem != null && !networkholdingItem.isInPocket)
		{
			if (networkholdingItem != _currentHeldItem)
			{
				_currentHeldItem = networkholdingItem;
				UpdateHeldItemActions();
			}
		}
		else
		{
			_currentHeldItem = null;
			ShowDefaultActions();
		}
	}

	private void UpdateHeldItemActions()
	{
		ClearActionParent();
		if (!(playerInventory == null) && !(playerInventory.NetworkholdingItem == null))
		{
			Item networkholdingItem = playerInventory.NetworkholdingItem;
			if (!(networkholdingItem == null) && !networkholdingItem.isInPocket)
			{
				CreateActionElements(networkholdingItem);
			}
		}
	}

	private void ShowDefaultActions()
	{
		ClearActionParent();
		CreateActionElement("Middle Click", "Ping", isHold: false);
		if (!CasinoLab.CasinoLabMode.Active) CreateActionElement("R", "Emote Wheel", isHold: true);
	}

	private void CreateActionElements(Item item)
	{
		if (itemActionPrefab == null)
		{
			return;
		}
		List<ItemAction> itemActions = item.itemActions;
		if (itemActions == null || itemActions.Count == 0)
		{
			return;
		}
		foreach (ItemAction item2 in itemActions)
		{
			if (!string.IsNullOrEmpty(item2.actionName) && !string.IsNullOrEmpty(item2.key))
			{
				CreateActionElement(item2.key, item2.actionName, item2.isHold);
			}
		}
	}

	private void CreateActionElement(string key, string actionText, bool isHold)
	{
		if (!(itemActionPrefab == null))
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(itemActionPrefab, actionParent);
			gameObject.name = "ItemAction_" + actionText;
			TextMeshProUGUI componentInChildren = gameObject.GetComponentInChildren<TextMeshProUGUI>();
			if (componentInChildren != null && componentInChildren.transform.parent == gameObject.transform)
			{
				componentInChildren.text = " " + actionText;
			}
			Transform transform = gameObject.transform.Find("KeyButton");
			if (transform != null)
			{
				SetupKeyButton(transform.gameObject, key, isHold);
			}
			_activeActionElements.Add(gameObject);
		}
	}

	private void SetupKeyButton(GameObject keyButton, string key, bool isHold)
	{
		bool flag = key.Length == 1 && char.IsLetter(key[0]);
		TextMeshProUGUI[] componentsInChildren = keyButton.GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
		TextMeshProUGUI textMeshProUGUI = null;
		TextMeshProUGUI textMeshProUGUI2 = null;
		TextMeshProUGUI[] array = componentsInChildren;
		foreach (TextMeshProUGUI textMeshProUGUI3 in array)
		{
			if (textMeshProUGUI3.gameObject.name.ToLower().Contains("hold"))
			{
				textMeshProUGUI2 = textMeshProUGUI3;
			}
			else
			{
				textMeshProUGUI = textMeshProUGUI3;
			}
		}
		Image component = keyButton.GetComponent<Image>();
		if (textMeshProUGUI2 != null)
		{
			textMeshProUGUI2.gameObject.SetActive(isHold);
		}
		if (flag)
		{
			if (textMeshProUGUI != null)
			{
				textMeshProUGUI.text = key.ToUpper();
				textMeshProUGUI.gameObject.SetActive(value: true);
			}
			return;
		}
		if (textMeshProUGUI != null)
		{
			textMeshProUGUI.gameObject.SetActive(value: false);
		}
		if (component != null)
		{
			component.gameObject.SetActive(value: true);
			Sprite keySprite = GetKeySprite(key);
			if (keySprite != null)
			{
				component.sprite = keySprite;
			}
			component.type = Image.Type.Simple;
			component.preserveAspect = true;
		}
	}

	private Sprite GetKeySprite(string keyName)
	{
		foreach (KeyButtonImageData keyButtonImage in keyButtonImages)
		{
			if (keyButtonImage.keyName.Equals(keyName, StringComparison.OrdinalIgnoreCase))
			{
				return keyButtonImage.sprite;
			}
		}
		return null;
	}
}

}
