namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.Extensions;
using global::SushiAndFish.Casino.Steamworks;
using global::UnityEngine;
using global::UnityEngine.UI;

public class DisableIfNotLobbyOwner : MonoBehaviour
{
	[Header("Settings")]
	[Tooltip("Alpha value to set when player is not the lobby owner (0-1)")]
	[SerializeField]
	private float disabledAlpha = 0.5f;

	private Button button;

	private CanvasGroup canvasGroup;

	private bool isLobbyOwner;

	private bool hasVersionMismatch;

	private void Awake()
	{
		button = GetComponent<Button>();
		if (button == null)
		{
			Debug.LogWarning("[DisableIfNotLobbyOwner] No Button component found on " + base.gameObject.name);
		}
		canvasGroup = GetComponent<CanvasGroup>();
		if (canvasGroup == null)
		{
			Debug.LogWarning("[DisableIfNotLobbyOwner] No CanvasGroup component found on " + base.gameObject.name);
		}
	}

	private void OnEnable()
	{
		LobbyManager.OnLobbyOwnerStatusChanged += OnLobbyOwnerStatusChanged;
		LobbyManager.OnLobbyEnteredEvent += OnLobbyEntered;
		LobbyManager.OnLobbyLeftEvent += OnLobbyLeft;
		VersionMismatchManager.OnVersionMismatchChanged += OnVersionMismatchChanged;
		if (MonoSingleton<VersionMismatchManager>.Instance != null)
		{
			hasVersionMismatch = MonoSingleton<VersionMismatchManager>.Instance.HasVersionMismatch();
		}
	}

	private void OnDisable()
	{
		LobbyManager.OnLobbyOwnerStatusChanged -= OnLobbyOwnerStatusChanged;
		LobbyManager.OnLobbyEnteredEvent -= OnLobbyEntered;
		LobbyManager.OnLobbyLeftEvent -= OnLobbyLeft;
		VersionMismatchManager.OnVersionMismatchChanged -= OnVersionMismatchChanged;
	}

	private void Start()
	{
		CheckLobbyOwnerStatus();
		ApplyDisableState();
	}

	private void OnLobbyOwnerStatusChanged(bool isOwner)
	{
		isLobbyOwner = isOwner;
		ApplyDisableState();
	}

	private void OnLobbyEntered()
	{
		CheckLobbyOwnerStatus();
		ApplyDisableState();
	}

	private void OnLobbyLeft()
	{
		isLobbyOwner = false;
		hasVersionMismatch = false;
		ApplyDisableState();
	}

	private void OnVersionMismatchChanged(bool hasMismatch)
	{
		hasVersionMismatch = hasMismatch;
		ApplyDisableState();
	}

	private void CheckLobbyOwnerStatus()
	{
		if (!SteamManager.Initialized)
		{
			isLobbyOwner = false;
			return;
		}
		LobbySettings lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
		if (lobbySettings == null || !lobbySettings.inALobby || lobbySettings.steamLobbyID == CSteamID.Nil)
		{
			isLobbyOwner = false;
			return;
		}
		try
		{
			CSteamID lobbyOwner = SteamMatchmaking.GetLobbyOwner(lobbySettings.steamLobbyID);
			isLobbyOwner = lobbyOwner == SteamUser.GetSteamID();
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[DisableIfNotLobbyOwner] Failed to check lobby owner status: " + ex.Message);
			isLobbyOwner = false;
		}
	}

	private void ApplyDisableState()
	{
		if (!isLobbyOwner || hasVersionMismatch)
		{
			if (canvasGroup != null)
			{
				canvasGroup.alpha = disabledAlpha;
				canvasGroup.blocksRaycasts = false;
				canvasGroup.interactable = false;
			}
			if (button != null)
			{
				button.interactable = false;
			}
		}
		else
		{
			if (canvasGroup != null)
			{
				canvasGroup.alpha = 1f;
				canvasGroup.blocksRaycasts = true;
				canvasGroup.interactable = true;
			}
			if (button != null)
			{
				button.interactable = true;
			}
		}
	}
}

}
