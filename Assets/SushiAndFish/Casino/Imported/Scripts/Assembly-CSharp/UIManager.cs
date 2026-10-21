namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::System.Linq;
using global::Mirror;
using global::UnityEngine;

public class UIManager : MonoBehaviour
{
	[Header("UI References")]
	private readonly List<IUIManager> registeredUIs = new List<IUIManager>();

	private IUIManager currentlyActiveUI;

	public static UIManager Instance { get; private set; }

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		}
		else
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	private void OnEnable()
	{
		InputEvents.OnEscapeMenuEvent = (Action)Delegate.Combine(InputEvents.OnEscapeMenuEvent, new Action(HandleEscapeKey));
	}

	private void OnDisable()
	{
		InputEvents.OnEscapeMenuEvent = (Action)Delegate.Remove(InputEvents.OnEscapeMenuEvent, new Action(HandleEscapeKey));
	}

	public void RegisterUI(IUIManager ui)
	{
		if (ui != null && !registeredUIs.Contains(ui))
		{
			registeredUIs.Add(ui);
			registeredUIs.Sort((IUIManager a, IUIManager b) => b.Priority.CompareTo(a.Priority));
		}
	}

	public void UnregisterUI(IUIManager ui)
	{
		if (ui != null)
		{
			registeredUIs.Remove(ui);
			if (currentlyActiveUI == ui)
			{
				currentlyActiveUI = null;
			}
		}
	}

	private void HandleEscapeKey()
	{
        if (CasinoRuntimeContext.Active) return;
		IUIManager iUIManager = registeredUIs.FirstOrDefault((IUIManager ui) => ui.IsActive);
		if (iUIManager == null)
		{
			return;
		}
		iUIManager.CloseUI();
		currentlyActiveUI = null;
		Cursor.lockState = CursorLockMode.Locked;
		UICursorSimple.Instance?.HideCursor();
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer != null)
		{
			PlayerController component = localPlayer.GetComponent<PlayerController>();
			if (component != null && component.head != null)
			{
				component.head.isLocked = false;
			}
		}
	}

	public void SetActiveUI(IUIManager ui)
	{
		if (currentlyActiveUI != null && currentlyActiveUI != ui)
		{
			currentlyActiveUI.CloseUI();
		}
		currentlyActiveUI = ui;
	}

	public void ClearActiveUI(IUIManager ui)
	{
		if (currentlyActiveUI == ui)
		{
			currentlyActiveUI = null;
		}
	}
}

}
