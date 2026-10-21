namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::TMPro;
using global::UnityEngine;

public class ItemPriceDisplayManager : MonoBehaviour
{
	[Header("References")]
	[Tooltip("The canvas prefab to instantiate above items. Should be a world space canvas.")]
	[SerializeField]
	private GameObject priceCanvasPrefab;

	[Tooltip("Item price settings. If null, will load from SushiAndFish.Casino.Original.CasinoResources.")]
	[SerializeField]
	private ItemPriceSettings priceSettings;

	[SerializeField]
	private GameSettings gameSettings;

	[Header("Display Settings")]
	[Tooltip("Height offset above the item's bounding box where the canvas should be positioned.")]
	[SerializeField]
	private float heightOffset = 0.15f;

	[Tooltip("Scale for the canvas (typically 0.01 for world space).")]
	[SerializeField]
	private Vector3 canvasScale = new Vector3(0.01f, 0.01f, 0.01f);

	private readonly Dictionary<Item, GameObject> _activePriceDisplays = new Dictionary<Item, GameObject>();

	private void Awake()
	{
		if (priceSettings == null)
		{
			priceSettings = SushiAndFish.Casino.Original.CasinoResources.Load<ItemPriceSettings>("ItemPriceSettings");
		}
		if (!gameSettings)
		{
			gameSettings = SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
		}
	}

	private void Update()
	{
		List<Item> list = new List<Item>();
		foreach (KeyValuePair<Item, GameObject> activePriceDisplay in _activePriceDisplays)
		{
			Item key = activePriceDisplay.Key;
			GameObject value = activePriceDisplay.Value;
			if (key == null || key.gameObject == null || value == null)
			{
				list.Add(key);
				continue;
			}
			Vector3 position = CalculateCanvasPosition(key);
			value.transform.position = position;
		}
		foreach (Item item in list)
		{
			if (_activePriceDisplays.TryGetValue(item, out var value2) && value2 != null)
			{
				Object.Destroy(value2);
			}
			_activePriceDisplays.Remove(item);
		}
	}

	public void ShowPriceForItem(Item item)
	{
		if (!(item == null) && (!(item.GetComponent<GachaSphere>() == null) || !(item.spawnableSo == null)) && !(priceCanvasPrefab == null) && !_activePriceDisplays.ContainsKey(item) && CalculatePrice(item) != 0)
		{
			Vector3 position = CalculateCanvasPosition(item);
			GameObject gameObject = Object.Instantiate(priceCanvasPrefab, position, Quaternion.identity);
			gameObject.transform.localScale = canvasScale;
			UpdatePriceText(gameObject, item);
			_activePriceDisplays[item] = gameObject;
		}
	}

	public void HidePriceForItem(Item item)
	{
		if (!(item == null) && _activePriceDisplays.TryGetValue(item, out var value))
		{
			if (value != null)
			{
				Object.Destroy(value);
			}
			_activePriceDisplays.Remove(item);
		}
	}

	private Vector3 CalculateCanvasPosition(Item item)
	{
		if (item == null)
		{
			return Vector3.zero;
		}
		Bounds bounds = default;
		bool flag = false;
		Renderer[] components = item.GetComponents<Renderer>();
		Renderer[] componentsInChildren = item.GetComponentsInChildren<Renderer>();
		Renderer[] array = components;
		foreach (Renderer renderer in array)
		{
			if (!(renderer == null) && renderer.enabled && !(renderer is ParticleSystemRenderer) && !(renderer is LineRenderer))
			{
				if (!flag)
				{
					bounds = renderer.bounds;
					flag = true;
				}
				else
				{
					bounds.Encapsulate(renderer.bounds);
				}
			}
		}
		array = componentsInChildren;
		foreach (Renderer renderer2 in array)
		{
			if (!(renderer2 == null) && renderer2.enabled && !(renderer2 is ParticleSystemRenderer) && !(renderer2 is LineRenderer))
			{
				if (!flag)
				{
					bounds = renderer2.bounds;
					flag = true;
				}
				else
				{
					bounds.Encapsulate(renderer2.bounds);
				}
			}
		}
		Collider[] components2 = item.GetComponents<Collider>();
		Collider[] componentsInChildren2 = item.GetComponentsInChildren<Collider>();
		Collider[] array2 = components2;
		foreach (Collider collider in array2)
		{
			if (!(collider == null) && collider.enabled)
			{
				if (!flag)
				{
					bounds = collider.bounds;
					flag = true;
				}
				else
				{
					bounds.Encapsulate(collider.bounds);
				}
			}
		}
		array2 = componentsInChildren2;
		foreach (Collider collider2 in array2)
		{
			if (!(collider2 == null) && collider2.enabled)
			{
				if (!flag)
				{
					bounds = collider2.bounds;
					flag = true;
				}
				else
				{
					bounds.Encapsulate(collider2.bounds);
				}
			}
		}
		if (!flag)
		{
			return item.transform.position + Vector3.up * heightOffset;
		}
		return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) + Vector3.up * heightOffset;
	}

	private void UpdatePriceText(GameObject canvas, Item item)
	{
		if (!(canvas == null) && !(item == null) && !(priceSettings == null))
		{
			TextMeshProUGUI componentInChildren = canvas.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
			if (componentInChildren != null)
			{
				componentInChildren.text = CalculatePrice(item).ToString();
			}
		}
	}

	private int CalculatePrice(Item item)
	{
		if (priceSettings == null || item == null)
		{
			return 0;
		}
		int floorIndex = 0;
		if (NetworkSingleton<GameManager>.Instance != null)
		{
			floorIndex = gameSettings.DayToFloor(NetworkSingleton<GameManager>.Instance.daysPassed - 1);
		}
		GachaSphere component = item.GetComponent<GachaSphere>();
		if (component != null)
		{
			CosmeticRarity rarity = CosmeticRarity.Common;
			CosmeticData cosmeticById = CosmeticDataManager.GetCosmeticById(component.CosmeticId);
			if (cosmeticById != null)
			{
				rarity = cosmeticById.rarity;
			}
			return priceSettings.CalculateCosmeticPrice(rarity, floorIndex);
		}
		if (item.spawnableSo == null)
		{
			return 0;
		}
		return priceSettings.CalculatePrice(item.spawnableSo, floorIndex);
	}

	private void OnDestroy()
	{
		foreach (GameObject value in _activePriceDisplays.Values)
		{
			if (value != null)
			{
				Object.Destroy(value);
			}
		}
		_activePriceDisplays.Clear();
	}
}

}
