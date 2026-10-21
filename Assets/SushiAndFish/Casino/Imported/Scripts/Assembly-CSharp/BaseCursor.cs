namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::UnityEngine;
using global::UnityEngine.UI;

public abstract class BaseCursor : MonoBehaviour
{
	private static readonly Dictionary<Texture2D,Texture2D> labReadableCursors = new Dictionary<Texture2D,Texture2D>();
	[Header("Cursor Visuals")]
	[SerializeField]
	protected List<CursorData> cursorDataList = new List<CursorData>();

	[Header("UI Cursor (Software)")]
	[SerializeField]
	protected bool useUiCursor;

	[Tooltip("Canvas hosting the cursor image. Must be Screen Space - Camera.")]
	[SerializeField]
	protected Canvas uiCursorCanvas;

	[Tooltip("UI Image used as the cursor.")]
	[SerializeField]
	protected Image uiCursorImage;

	[SerializeField]
	protected float uiCursorScale = 1f;

	protected CursorType currentCursorType;

	protected Dictionary<CursorType, CursorData> cursorDataMap;

	protected Dictionary<CursorType, Sprite> cursorSpriteCache;

	protected RectTransform uiCursorCanvasRect;

	public CursorType CurrentCursorType => currentCursorType;

	protected virtual void Awake()
	{
		InitializeCursorData();
	}

	protected virtual void Start()
	{
        if (CasinoRuntimeContext.Active) return;
		if (useUiCursor && uiCursorCanvas != null)
		{
			uiCursorCanvasRect = uiCursorCanvas.GetComponent<RectTransform>();
		}
		if (useUiCursor)
		{
			Cursor.visible = false;
			Cursor.lockState = CursorLockMode.None;
		}
	}

	protected virtual void Update()
	{
        if (CasinoRuntimeContext.Active) return;
		if (useUiCursor)
		{
			UpdateUiCursorPosition();
		}
	}

	protected virtual void InitializeCursorData()
	{
		cursorDataMap = new Dictionary<CursorType, CursorData>();
		cursorSpriteCache = new Dictionary<CursorType, Sprite>();
		foreach (CursorData cursorData in cursorDataList)
		{
			if (!cursorDataMap.ContainsKey(cursorData.type))
			{
				if (cursorData.useCenterAlignment && cursorData.texture != null)
				{
					cursorData.hotspot = new Vector2((float)cursorData.texture.width / 2f, (float)cursorData.texture.height / 2f);
				}
				cursorDataMap.Add(cursorData.type, cursorData);
			}
		}
	}

	public virtual void SetCursorType(CursorType type)
	{
        if (CasinoRuntimeContext.Active) return;
		if (currentCursorType == type)
		{
			return;
		}
		currentCursorType = type;
		if (!cursorDataMap.TryGetValue(type, out var value))
		{
			return;
		}
		if (value.texture != null)
		{
			if (useUiCursor && uiCursorImage != null && uiCursorCanvas != null)
			{
				ApplyUiCursor(value);
				return;
			}
			Vector2 hotspot = value.hotspot;
			if (hotspot.x < 0f || hotspot.x > (float)value.texture.width || hotspot.y < 0f || hotspot.y > (float)value.texture.height)
			{
				hotspot = Vector2.zero;
			}
			var texture=value.texture;
			if (CasinoLab.CasinoLabMode.Active && !texture.isReadable)
			{
				if (!labReadableCursors.TryGetValue(texture,out var readable))
				{
					var target=RenderTexture.GetTemporary(texture.width,texture.height,0);
					var previous=RenderTexture.active;Graphics.Blit(texture,target);RenderTexture.active=target;
					readable=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
					readable.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);readable.Apply();
					RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);labReadableCursors[texture]=readable;
				}
				texture=readable;
			}
			Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
		}
		else if (useUiCursor && uiCursorImage != null)
		{
			uiCursorImage.enabled = false;
		}
		else
		{
			Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
		}
	}

	public virtual void LockCursor(bool isLocked)
	{
        if (CasinoRuntimeContext.Active) return;
		Cursor.lockState = (isLocked ? CursorLockMode.Locked : CursorLockMode.None);
	}

	public virtual void ShowCursor(bool isVisible)
	{
        if (CasinoRuntimeContext.Active) return;
		if (useUiCursor && uiCursorImage != null)
		{
			uiCursorImage.enabled = isVisible;
			Cursor.visible = false;
		}
		else
		{
			Cursor.visible = isVisible;
		}
	}

	protected virtual void ApplyUiCursor(CursorData data)
	{
        if (CasinoRuntimeContext.Active) return;
		if (!(uiCursorImage == null) && !(uiCursorCanvas == null))
		{
			uiCursorImage.enabled = true;
			Cursor.visible = false;
			Sprite cursorSprite = GetCursorSprite(data);
			uiCursorImage.sprite = cursorSprite;
			Vector2 sizeDelta = new Vector2(data.texture.width, data.texture.height) * uiCursorScale;
			RectTransform rectTransform = uiCursorImage.rectTransform;
			rectTransform.sizeDelta = sizeDelta;
			Vector2 vector = data.hotspot;
			if (vector.x < 0f || vector.x > (float)data.texture.width || vector.y < 0f || vector.y > (float)data.texture.height)
			{
				vector = Vector2.zero;
			}
			rectTransform.pivot = new Vector2((data.texture.width > 0) ? (vector.x / (float)data.texture.width) : 0f, (data.texture.height > 0) ? (1f - vector.y / (float)data.texture.height) : 1f);
		}
	}

	protected virtual Sprite GetCursorSprite(CursorData data)
	{
		if (cursorSpriteCache.TryGetValue(data.type, out var value) && value != null)
		{
			return value;
		}
		Sprite sprite = Sprite.Create(rect: new Rect(0f, 0f, data.texture.width, data.texture.height), texture: data.texture, pivot: new Vector2(0.5f, 0.5f), pixelsPerUnit: 100f);
		cursorSpriteCache[data.type] = sprite;
		return sprite;
	}

	protected virtual void UpdateUiCursorPosition()
	{
		if (!(uiCursorImage == null) && !(uiCursorCanvas == null))
		{
			if (uiCursorCanvasRect == null)
			{
				uiCursorCanvasRect = uiCursorCanvas.GetComponent<RectTransform>();
			}
			Camera cam = ((uiCursorCanvas.renderMode == RenderMode.ScreenSpaceCamera) ? uiCursorCanvas.worldCamera : null);
			if (RectTransformUtility.ScreenPointToLocalPointInRectangle(uiCursorCanvasRect, CursorPointerInput.ScreenPosition3D, cam, out var localPoint))
			{
				uiCursorImage.rectTransform.anchoredPosition = localPoint;
			}
		}
	}
}

}
