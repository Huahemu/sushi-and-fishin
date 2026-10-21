namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;
using global::UnityEngine.UI;

public class CursorManager : MonoSingleton<CursorManager>
{
	[Serializable]
	public class CursorData
	{
		public CursorType type;

		public Texture2D texture;

		public Sprite sprite;

		[Tooltip("Leave at 0,0 for top-left alignment. Set to texture dimensions/2 for center alignment")]
		public Vector2 hotspot = Vector2.zero;

		[Tooltip("If true, hotspot will be set to center of texture")]
		public bool useCenterAlignment;
	}

	public enum CursorType
	{
		Default = 0,
		Interact = 1,
		Forbidden = 2,
		Speak = 3,
		Play = 4,
		Custom = 5,
		PointUI = 6,
		Lock = 7,
		Unlock = 8
	}

	[Header("Cursor Settings")]
	[SerializeField]
	private List<CursorData> cursorDataList = new List<CursorData>();

	[Header("Sprite Cursor Settings")]
	[SerializeField]
	private Image cursorImage;

	private CursorType currentCursorType;

	private bool isLocked;

	private bool isVisible;

	private Dictionary<CursorType, CursorData> cursorDataMap;

	public bool IsCursorLocked => isLocked;

	public bool IsCursorVisible => isVisible;

	public CursorType CurrentCursorType => currentCursorType;

	protected override void OnAwake()
	{
		base.OnAwake();
		InitializeCursorData();
	}

	private void Start()
	{
        if (CasinoRuntimeContext.Active) return;
		cursorImage = GetComponent<Image>();
		currentCursorType = CursorType.Default;
		if (cursorDataMap.TryGetValue(CursorType.Default, out var value))
		{
			if (value.sprite != null && cursorImage != null)
			{
				cursorImage.sprite = value.sprite;
			}
			else if (value.texture != null && cursorImage != null)
			{
				value.sprite = Sprite.Create(value.texture, new Rect(0f, 0f, value.texture.width, value.texture.height), value.hotspot);
				cursorImage.sprite = value.sprite;
			}
			else
			{
				Debug.LogWarning("Default cursor sprite/texture is null or SpriteRenderer not assigned!");
			}
		}
		else
		{
			Debug.LogWarning("No default cursor data found in cursorDataMap!");
		}
	}

	private void InitializeCursorData()
	{
		cursorDataMap = new Dictionary<CursorType, CursorData>();
		foreach (CursorData cursorData in cursorDataList)
		{
			if (!cursorDataMap.ContainsKey(cursorData.type))
			{
				if (cursorData.useCenterAlignment && cursorData.texture != null)
				{
					cursorData.hotspot = new Vector2((float)cursorData.texture.width / 2f, (float)cursorData.texture.height / 2f);
				}
				if (cursorData.sprite == null && cursorData.texture != null)
				{
					cursorData.sprite = Sprite.Create(cursorData.texture, new Rect(0f, 0f, cursorData.texture.width, cursorData.texture.height), cursorData.hotspot);
				}
				cursorDataMap.Add(cursorData.type, cursorData);
			}
			else
			{
				Debug.LogWarning($"Duplicate cursor type {cursorData.type} found in CursorManager. Only the first entry will be used.");
			}
		}
	}

	public void SetCursorType(CursorType type)
	{
        if (CasinoRuntimeContext.Active) return;
		if (currentCursorType == type)
		{
			return;
		}
		currentCursorType = type;
		if (cursorDataMap.TryGetValue(type, out var value))
		{
			if (value.sprite == null && value.texture == null)
			{
				Debug.LogWarning($"Cursor sprite/texture for type {type} is null. Using default cursor.");
				if (cursorImage != null)
				{
					cursorImage.sprite = null;
				}
			}
			else if (cursorImage != null)
			{
				if (value.sprite != null)
				{
					cursorImage.sprite = value.sprite;
				}
				else if (value.texture != null)
				{
					Vector2 pivot = value.hotspot;
					if (pivot.x < 0f || pivot.x > (float)value.texture.width || pivot.y < 0f || pivot.y > (float)value.texture.height)
					{
						Debug.LogWarning($"Hotspot for cursor type {type} is outside texture bounds. Using top-left alignment.");
						pivot = Vector2.zero;
					}
					value.sprite = Sprite.Create(value.texture, new Rect(0f, 0f, value.texture.width, value.texture.height), pivot);
					cursorImage.sprite = value.sprite;
				}
				cursorImage.transform.DOScale(1.2f, 0.1f).OnComplete(() =>
				{
					cursorImage.transform.DOScale(1f, 0.1f);
				});
			}
			else
			{
				Debug.LogWarning("SpriteRenderer not assigned to CursorManager!");
			}
		}
		else
		{
			Debug.LogWarning($"Cursor type {type} not found in CursorManager. Using default cursor.");
			if (cursorImage != null)
			{
				cursorImage.sprite = null;
			}
		}
	}

	public void LockCursor(bool isLocked)
	{
        if (CasinoRuntimeContext.Active) return;
		this.isLocked = isLocked;
		Cursor.lockState = (isLocked ? CursorLockMode.Locked : CursorLockMode.None);
	}

	public void ShowCursor(bool isVisible)
	{
        if (CasinoRuntimeContext.Active) return;
		this.isVisible = isVisible;
		Cursor.visible = isVisible;
	}
}

}
