namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::TMPro;
using global::UnityEngine;

public class ItemDescriptionDisplayManager : MonoBehaviour
{
	[Header("References")]
	[Tooltip("The canvas prefab to instantiate next to items. Should be a world space canvas.")]
	[SerializeField]
	private GameObject descriptionCanvasPrefab;

	[Tooltip("Item description settings. If null, will load from SushiAndFish.Casino.Original.CasinoResources.")]
	[SerializeField]
	private ItemDescriptionSettings descriptionSettings;

	[Header("Display Settings")]
	[Tooltip("Horizontal offset to the right of the item where the canvas should be positioned (camera-relative).")]
	[SerializeField]
	private float horizontalOffset = 0.2f;

	[Tooltip("Vertical offset from the item's transform position.")]
	[SerializeField]
	private float verticalOffset;

	[Tooltip("Forward/backward offset along the camera's view direction. Positive values move toward camera, negative away.")]
	[SerializeField]
	private float forwardOffset;

	[Tooltip("Scale for the canvas (typically 0.01 for world space).")]
	[SerializeField]
	private Vector3 canvasScale = new Vector3(0.01f, 0.01f, 0.01f);

	private readonly Dictionary<Item, GameObject> _activeDescriptionDisplays = new Dictionary<Item, GameObject>();

	private Camera _camera;

	private void Awake()
	{
		if (descriptionSettings == null)
		{
			descriptionSettings = SushiAndFish.Casino.Original.CasinoResources.Load<ItemDescriptionSettings>("ItemDescriptionSettings");
		}
	}

	private void Start()
	{
		if (MonoSingleton<LocalManager>.Instance != null)
		{
			_camera = MonoSingleton<LocalManager>.Instance.mainCamera;
		}
	}

	private void LateUpdate()
	{
		List<Item> list = new List<Item>();
		foreach (KeyValuePair<Item, GameObject> activeDescriptionDisplay in _activeDescriptionDisplays)
		{
			Item key = activeDescriptionDisplay.Key;
			GameObject value = activeDescriptionDisplay.Value;
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
			if (_activeDescriptionDisplays.TryGetValue(item, out var value2) && value2 != null)
			{
				Object.Destroy(value2);
			}
			_activeDescriptionDisplays.Remove(item);
		}
	}

	public void ShowDescriptionForItem(Item item)
	{
		if (!(item == null) && !(item.spawnableSo == null) && !(descriptionCanvasPrefab == null) && !_activeDescriptionDisplays.ContainsKey(item))
		{
			Vector3 position = CalculateCanvasPosition(item);
			GameObject gameObject = Object.Instantiate(descriptionCanvasPrefab, position, Quaternion.identity);
			gameObject.transform.localScale = canvasScale;
			UpdateDescriptionText(gameObject, item);
			_activeDescriptionDisplays[item] = gameObject;
		}
	}

	public void HideDescriptionForItem(Item item)
	{
		if (!(item == null) && _activeDescriptionDisplays.TryGetValue(item, out var value))
		{
			if (value != null)
			{
				Object.Destroy(value);
			}
			_activeDescriptionDisplays.Remove(item);
		}
	}

	private Vector3 CalculateCanvasPosition(Item item)
	{
		if (item == null)
		{
			return Vector3.zero;
		}
		Camera camera = _camera;
		if (camera == null)
		{
			if (MonoSingleton<LocalManager>.Instance != null)
			{
				camera = MonoSingleton<LocalManager>.Instance.mainCamera;
			}
			if (camera == null)
			{
				camera = Camera.main;
			}
		}
		Vector3 vector = Vector3.right;
		Vector3 vector2 = Vector3.forward;
		if (camera != null)
		{
			Vector3 normalized = (item.transform.position - camera.transform.position).normalized;
			vector = Vector3.Cross(Vector3.up, normalized).normalized;
			if (vector.sqrMagnitude < 0.1f)
			{
				vector = camera.transform.right;
			}
			vector2 = -normalized;
		}
		return item.transform.position + vector * horizontalOffset + Vector3.up * verticalOffset + vector2 * forwardOffset;
	}

	private void UpdateDescriptionText(GameObject canvas, Item item)
	{
		if (!(canvas == null) && !(item == null) && !(item.spawnableSo == null) && !(descriptionSettings == null))
		{
			TextMeshProUGUI componentInChildren = canvas.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
			if (componentInChildren != null)
			{
				string description = descriptionSettings.GetDescription(item.spawnableSo);
				componentInChildren.text = description;
			}
		}
	}

	private void OnDestroy()
	{
		foreach (GameObject value in _activeDescriptionDisplays.Values)
		{
			if (value != null)
			{
				Object.Destroy(value);
			}
		}
		_activeDescriptionDisplays.Clear();
	}
}

}
