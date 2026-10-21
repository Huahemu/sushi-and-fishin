namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::FMODUnity;
using global::UnityEngine;
using global::UnityEngine.EventSystems;

public class UICursor : BaseCursor
{
	private static UICursor _instance;

	[Header("Tag-Based Hover Detection")]
	[SerializeField]
	private List<TagCursorMapping> tagMappings = new List<TagCursorMapping>();

	[SerializeField]
	private float raycastDistance = 100f;

	[SerializeField]
	private Camera mainCamera;

	[Header("Click Feedback")]
	[SerializeField]
	private ParticleSystem defaultClickParticle;

	[Tooltip("If true, will instantiate a new particle system for each click. Otherwise, moves the existing one.")]
	[SerializeField]
	private bool instantiateClickParticles = true;

	[Header("SFX")]
	[SerializeField]
	private EventReference gunSFX;

	private Dictionary<string, CursorType> tagMap;

	private bool isInputModeEnabled = true;

	public static UICursor Instance
	{
		get
		{
			if (_instance == null)
			{
				_instance = Object.FindAnyObjectByType<UICursor>();
				if (_instance == null)
				{
					_instance = new GameObject("UICursor").AddComponent<UICursor>();
				}
			}
			return _instance;
		}
	}

	public void SetInputModeEnabled(bool enabled)
	{
        if (CasinoRuntimeContext.Active) return;
		isInputModeEnabled = enabled;
		if (enabled)
		{
			ShowCursor(isVisible: true);
			if (currentCursorType == CursorType.Default)
			{
				currentCursorType = CursorType.Play;
			}
			SetCursorType(CursorType.Default);
		}
		else
		{
			ShowCursor(isVisible: false);
			if (useUiCursor && uiCursorImage != null)
			{
				uiCursorImage.enabled = false;
			}
			else
			{
				Cursor.visible = false;
			}
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
		base.Start();
		if (mainCamera == null)
		{
			mainCamera = Camera.main;
		}
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

	protected override void Update()
	{
        if (CasinoRuntimeContext.Active) return;
		base.Update();
		if (!isInputModeEnabled)
		{
			return;
		}
		CursorType cursorType = CursorType.Default;
		GameObject gameObject = null;
		RaycastHit? raycastHit = null;
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
		if (gameObject == null && mainCamera != null && Physics.Raycast(mainCamera.ScreenPointToRay(CursorPointerInput.ScreenPosition3D), out var hitInfo, raycastDistance))
		{
			gameObject = hitInfo.collider.gameObject;
			raycastHit = hitInfo;
			string tagFromHierarchy2 = GetTagFromHierarchy(gameObject);
			if (tagFromHierarchy2 != null && tagMap.TryGetValue(tagFromHierarchy2, out var value2))
			{
				cursorType = value2;
			}
		}
		SetCursorType(cursorType);
		if (!CursorPointerInput.LeftClickPressedThisFrame || !(gameObject != null))
		{
			return;
		}
		string tagFromHierarchy3 = GetTagFromHierarchy(gameObject);
		if (tagFromHierarchy3 == "menu_play")
		{
			MonoBehaviour[] components = gameObject.transform.root.GetComponents<MonoBehaviour>();
			foreach (MonoBehaviour monoBehaviour in components)
			{
				if (monoBehaviour != null && monoBehaviour.GetType().GetMethod("Play") != null)
				{
					SFXManager.SFXOneShot(gunSFX);
					monoBehaviour.GetType().GetMethod("Play").Invoke(monoBehaviour, null);
					break;
				}
			}
		}
		else if (tagFromHierarchy3 == "menu_hit" && raycastHit.HasValue)
		{
			FloatingObjectPhysics floatingObjectPhysics = gameObject.GetComponent<FloatingObjectPhysics>();
			if (floatingObjectPhysics == null)
			{
				floatingObjectPhysics = gameObject.GetComponentInParent<FloatingObjectPhysics>();
			}
			if (floatingObjectPhysics != null)
			{
				SFXManager.SFXOneShot(gunSFX);
				floatingObjectPhysics.HandleClick(raycastHit.Value, mainCamera);
			}
		}
		else if (tagFromHierarchy3 == null && raycastHit.HasValue && raycastHit.Value.collider != null)
		{
			SFXManager.SFXOneShot(gunSFX);
			PlayClickParticle(raycastHit.Value.point, raycastHit.Value.normal);
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

	private void PlayClickParticle(Vector3 position, Vector3 normal)
	{
		if (defaultClickParticle == null)
		{
			return;
		}
		if (instantiateClickParticles)
		{
			ParticleSystem particleSystem = Object.Instantiate(defaultClickParticle, position, Quaternion.LookRotation(normal));
			particleSystem.gameObject.SetActive(value: true);
			ParticleSystem.MainModule main = particleSystem.main;
			main.playOnAwake = false;
			particleSystem.Play();
			if (main.duration > 0f)
			{
				float num = ((main.startLifetime.constantMax > 0f) ? main.startLifetime.constantMax : main.startLifetime.constant);
				Object.Destroy(particleSystem.gameObject, main.duration + num + 1f);
			}
			else
			{
				Object.Destroy(particleSystem.gameObject, 5f);
			}
			return;
		}
		if (!defaultClickParticle.gameObject.activeInHierarchy)
		{
			defaultClickParticle.gameObject.SetActive(value: true);
		}
		if (defaultClickParticle.isPlaying)
		{
			defaultClickParticle.Stop();
			defaultClickParticle.Clear();
		}
		defaultClickParticle.transform.position = position;
		defaultClickParticle.transform.rotation = Quaternion.LookRotation(normal);
		defaultClickParticle.Play();
	}
}

}
