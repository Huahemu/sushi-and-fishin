namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::UnityEngine;
using global::UnityEngine.SceneManagement;

public class LocalManager : MonoSingleton<LocalManager>
{
	public Camera mainCamera;

	public PlayerEyesUI playerEyesUI;

	public PlayerBuffUI playerBuffUI;

	public InteractionUIPanel interactionUIPanel;

	public HeldItemActionPanel heldItemActionPanel;

	public BaseCursor baseCursor;

	public GameObject crosshair;

	public GameObject itemInputsUI;

	public List<PlayerReferences> players;

	public Action<PlayerReferences> OnNewPlayerRegistered;

	protected override void OnAwake()
	{
		base.OnAwake();
        if (CasinoRuntimeContext.IsHostIntegration) mainCamera = CasinoRuntimeContext.PlayerCamera;
		UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
		if (!playerEyesUI)
		{
			playerEyesUI = GetComponentInChildren<PlayerEyesUI>();
		}
		if (!playerBuffUI)
		{
			playerBuffUI = GetComponentInChildren<PlayerBuffUI>();
		}
		if (!interactionUIPanel)
		{
			interactionUIPanel = GetComponentInChildren<InteractionUIPanel>();
		}
		if (!heldItemActionPanel)
		{
			heldItemActionPanel = GetComponentInChildren<HeldItemActionPanel>();
		}
	}

	private void OnEnable()
	{
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private void OnDisable()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
        if (CasinoRuntimeContext.IsHostIntegration) { mainCamera = CasinoRuntimeContext.PlayerCamera; return; }
		if (!this || !base.gameObject)
		{
			return;
		}
		string text = scene.name;
		if (text == "NetworkSetupScene" || text == "MainMenuScene")
		{
			GameSettings gameSettings = SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
			if (gameSettings != null)
			{
				gameSettings.gameHasStarted = false;
			}
			base.enabled = false;
			UnityEngine.Object.Destroy(base.gameObject, 0.01f);
			return;
		}
		if (scene.name == "HomeScene")
		{
			UICursorSimple.Instance?.HideCursor();
		}
		if ((bool)mainCamera)
		{
			UnityEngine.Object.Destroy(mainCamera.gameObject);
		}
		mainCamera = UnityEngine.Object.Instantiate(SushiAndFish.Casino.Original.CasinoResources.Load<Camera>("Camera"), base.transform);
		players.Clear();
	}

	private void Start()
	{
        if (CasinoRuntimeContext.IsHostIntegration) { mainCamera = CasinoRuntimeContext.PlayerCamera; return; }
		if ((bool)mainCamera)
		{
			UnityEngine.Object.Destroy(mainCamera.gameObject);
		}
		mainCamera = UnityEngine.Object.Instantiate(SushiAndFish.Casino.Original.CasinoResources.Load<Camera>("Camera"), base.transform);
	}

	public void SetCrosshair(bool isEnabled)
	{
		crosshair.SetActive(isEnabled);
	}

	public void RegisterPlayer(NetworkIdentity identity)
	{
		PlayerReferences playerReferences = new PlayerReferences(identity);
		players.Add(playerReferences);
		OnNewPlayerRegistered?.Invoke(playerReferences);
	}

	public void UnregisterPlayer(NetworkIdentity identity)
	{
		players.Remove(players.Find((PlayerReferences player) => player.identity == identity));
	}
}

}
