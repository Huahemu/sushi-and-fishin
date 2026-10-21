namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;
using global::UnityEngine.UI;

public class SimpleCursorManager : BaseCursorManager
{
	[Header("Sprite Cursor Settings")]
	[SerializeField]
	private Image cursorImage;

	[SerializeField]
	private RectTransform cursorRectTransform;

	private void Start()
	{
        if (CasinoRuntimeContext.Active) return;
		if (cursorImage == null)
		{
			cursorImage = GetComponent<Image>();
		}
		if (cursorRectTransform == null && cursorImage != null)
		{
			cursorRectTransform = cursorImage.rectTransform;
		}
		currentCursorType = CursorType.Default;
		CursorData cursorData = GetCursorData(CursorType.Default);
		if (cursorData != null)
		{
			ApplyCursorData(cursorData);
		}
		else
		{
			Debug.LogWarning("No default cursor data found in cursorDataMap!");
		}
	}

	private void Update()
	{
        if (CasinoRuntimeContext.Active) return;
		UpdateCursorPosition();
	}

	private void UpdateCursorPosition()
	{
		if (cursorRectTransform == null)
		{
			return;
		}
		Canvas componentInParent = cursorRectTransform.GetComponentInParent<Canvas>();
		if (!(componentInParent == null))
		{
			Camera cam = ((componentInParent.renderMode == RenderMode.ScreenSpaceCamera) ? componentInParent.worldCamera : null);
			if (RectTransformUtility.ScreenPointToLocalPointInRectangle(componentInParent.GetComponent<RectTransform>(), CursorPointerInput.ScreenPosition3D, cam, out var localPoint))
			{
				cursorRectTransform.anchoredPosition = localPoint;
			}
		}
	}

	public override void SetCursorType(CursorType type)
	{
        if (CasinoRuntimeContext.Active) return;
		if (currentCursorType == type)
		{
			return;
		}
		currentCursorType = type;
		CursorData cursorData = GetCursorData(type);
		if (cursorData == null)
		{
			Debug.LogWarning($"Cursor type {type} not found in SimpleCursorManager. Using default cursor.");
			if (cursorImage != null)
			{
				cursorImage.sprite = null;
			}
		}
		else if (cursorData.sprite == null && cursorData.texture == null)
		{
			Debug.LogWarning($"Cursor sprite/texture for type {type} is null. Using default cursor.");
			if (cursorImage != null)
			{
				cursorImage.sprite = null;
			}
		}
		else
		{
			ApplyCursorData(cursorData);
		}
	}

	private void ApplyCursorData(CursorData data)
	{
		if (cursorImage == null)
		{
			return;
		}
		if (data.sprite != null)
		{
			cursorImage.sprite = data.sprite;
		}
		else if (data.texture != null)
		{
			Vector2 pivot = data.hotspot;
			if (pivot.x < 0f || pivot.x > (float)data.texture.width || pivot.y < 0f || pivot.y > (float)data.texture.height)
			{
				Debug.LogWarning($"Hotspot for cursor type {data.type} is outside texture bounds. Using top-left alignment.");
				pivot = Vector2.zero;
			}
			data.sprite = Sprite.Create(data.texture, new Rect(0f, 0f, data.texture.width, data.texture.height), pivot);
			cursorImage.sprite = data.sprite;
		}
		cursorImage.enabled = true;
	}
}

}
