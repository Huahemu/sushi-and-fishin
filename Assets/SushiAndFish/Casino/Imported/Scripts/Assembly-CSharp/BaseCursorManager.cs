namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

public abstract class BaseCursorManager : MonoSingleton<BaseCursorManager>
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
	protected List<CursorData> cursorDataList = new List<CursorData>();

	protected CursorType currentCursorType;

	protected bool isLocked;

	protected bool isVisible;

	protected Dictionary<CursorType, CursorData> cursorDataMap;

	public bool IsCursorLocked => isLocked;

	public bool IsCursorVisible => isVisible;

	public CursorType CurrentCursorType => currentCursorType;

	protected override void OnAwake()
	{
		base.OnAwake();
		InitializeCursorData();
	}

	protected virtual void InitializeCursorData()
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

	public abstract void SetCursorType(CursorType type);

	public virtual void LockCursor(bool isLocked)
	{
        if (CasinoRuntimeContext.Active) return;
		this.isLocked = isLocked;
		Cursor.lockState = (isLocked ? CursorLockMode.Locked : CursorLockMode.None);
	}

	public virtual void ShowCursor(bool isVisible)
	{
        if (CasinoRuntimeContext.Active) return;
		this.isVisible = isVisible;
		Cursor.visible = isVisible;
	}

	protected CursorData GetCursorData(CursorType type)
	{
		cursorDataMap.TryGetValue(type, out var value);
		return value;
	}
}

}
