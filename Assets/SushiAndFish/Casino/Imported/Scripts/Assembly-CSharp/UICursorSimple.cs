namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::UnityEngine;
using global::UnityEngine.EventSystems;

public class UICursorSimple : BaseCursor
{
	private static UICursorSimple _instance;

	[Header("Tag-Based Hover Detection")]
	[SerializeField]
	private List<TagCursorMapping> tagMappings = new List<TagCursorMapping>();

	private Dictionary<string, CursorType> tagMap;

	private bool _cursorShouldBeVisible;

	public static UICursorSimple Instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = Object.FindAnyObjectByType<UICursorSimple>();
				if (_instance == null)
				{
					_instance = new GameObject("UICursorSimple").AddComponent<UICursorSimple>();
				}
			}
			return _instance;
		}
	}

	protected override void Awake()
	{
		if (_instance != null && _instance != this)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		_instance = this;
		base.Awake();
		BuildTagMap();
	}

	protected override void Start()
	{
        if (CasinoRuntimeContext.Active) return;
        if (CasinoRuntimeContext.Active) return;
        if (CasinoRuntimeContext.IsHostIntegration) return;
		base.Start();
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
		ShowCursor(isVisible: false);
	}

	protected override void Update()
	{
        if (CasinoRuntimeContext.Active) return;
        if (CasinoRuntimeContext.Active) return;
        if (CasinoRuntimeContext.IsHostIntegration) return;
		base.Update();
		if (!_cursorShouldBeVisible)
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}
		CursorType cursorType = CursorType.Default;
		GameObject gameObject = null;
		if (EventSystem.current != null)
		{
			PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
			pointerEventData.position = CursorPointerInput.ScreenPosition;
			List<RaycastResult> list = new List<RaycastResult>();
			EventSystem.current.RaycastAll(pointerEventData, list);
			if (list.Count > 0)
			{
				gameObject = list[0].gameObject;
				string tagFromHierarchy = GetTagFromHierarchy(gameObject);
				if (tagFromHierarchy != null && tagMap.TryGetValue(tagFromHierarchy, out var value))
				{
					cursorType = value;
				}
			}
		}
		SetCursorType(cursorType);
	}

	public void ShowCursor()
	{
        if (CasinoRuntimeContext.Active) return;
        if (CasinoRuntimeContext.Active) return;
        if (CasinoRuntimeContext.IsHostIntegration) return;
		_cursorShouldBeVisible = true;
		ShowCursor(isVisible: true);
		Cursor.lockState = CursorLockMode.Confined;
		Cursor.visible = false;
	}

	public void HideCursor()
	{
        if (CasinoRuntimeContext.Active) return;
        if (CasinoRuntimeContext.Active) return;
        if (CasinoRuntimeContext.IsHostIntegration) return;
		_cursorShouldBeVisible = false;
		ShowCursor(isVisible: false);
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

	private void BuildTagMap()
	{
		tagMap = new Dictionary<string, CursorType>();
		foreach (TagCursorMapping tagMapping in tagMappings)
		{
			if (!string.IsNullOrEmpty(tagMapping.tag))
			{
				tagMap[tagMapping.tag] = tagMapping.cursorType;
			}
		}
	}

	private string GetTagFromHierarchy(GameObject obj)
	{
		Transform parent = obj.transform;
		while (parent != null)
		{
			if (tagMap.ContainsKey(parent.tag))
			{
				return parent.tag;
			}
			parent = parent.parent;
		}
		return null;
	}
}

}
