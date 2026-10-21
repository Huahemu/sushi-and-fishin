namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using SushiAndFish.Casino.Original.Extensions;
using global::SushiAndFish.Casino.Steamworks;
using global::UnityEngine;
using global::UnityEngine.EventSystems;
using global::UnityEngine.UI;

public class LobbyMemberInteraction : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler, IPointerClickHandler
{
	[Header("UI References")]
	[SerializeField]
	public GameObject leaveButton;

	[SerializeField]
	public GameObject kickButton;

	[SerializeField]
	public RawImage profileImage;

	[Header("Animation Settings")]
	[SerializeField]
	private float fadeDuration = 0.2f;

	[SerializeField]
	private float targetAlpha = 1f;

	private SteamIdComponent steamIdComponent;

	private CanvasGroup leaveButtonCanvasGroup;

	private CanvasGroup kickButtonCanvasGroup;

	private bool isLocalPlayer;

	private bool isHovering;

	private bool isLobbyOwner;

	private bool isLeaveButtonVisible;

	private bool isKickButtonVisible;

	private void Awake()
	{
		steamIdComponent = GetComponent<SteamIdComponent>();
		if (leaveButton != null)
		{
			leaveButtonCanvasGroup = leaveButton.GetComponent<CanvasGroup>();
		}
		if (kickButton != null)
		{
			kickButtonCanvasGroup = kickButton.GetComponent<CanvasGroup>();
		}
		if (leaveButton != null)
		{
			leaveButton.SetActive(value: false);
		}
		if (kickButton != null)
		{
			kickButton.SetActive(value: false);
		}
	}

	private void Start()
	{
		if (steamIdComponent != null)
		{
			ulong steamID = SteamUser.GetSteamID().m_SteamID;
			isLocalPlayer = steamIdComponent.SteamId == steamID;
			LobbySettings lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
			if (lobbySettings != null && lobbySettings.inALobby && lobbySettings.steamLobbyID != CSteamID.Nil)
			{
				CSteamID lobbyOwner = SteamMatchmaking.GetLobbyOwner(lobbySettings.steamLobbyID);
				isLobbyOwner = lobbyOwner == SteamUser.GetSteamID();
			}
		}
	}

	private void Update()
	{
		if (isHovering)
		{
			if (isLocalPlayer && !isLeaveButtonVisible)
			{
				ShowLeaveButton();
			}
			else if (!isLocalPlayer && isLobbyOwner && !isKickButtonVisible)
			{
				ShowKickButton();
			}
			return;
		}
		if (isLeaveButtonVisible)
		{
			HideLeaveButton();
		}
		if (isKickButtonVisible)
		{
			HideKickButton();
		}
	}

	private void ShowLeaveButton()
	{
		if (!(leaveButton == null) && !(leaveButtonCanvasGroup == null))
		{
			leaveButton.SetActive(value: true);
			isLeaveButtonVisible = true;
			StartCoroutine(FadeCanvasGroup(leaveButtonCanvasGroup, 0f, targetAlpha, fadeDuration));
		}
	}

	private void HideLeaveButton()
	{
		if (leaveButton == null || leaveButtonCanvasGroup == null)
		{
			return;
		}
		isLeaveButtonVisible = false;
		StartCoroutine(FadeCanvasGroup(leaveButtonCanvasGroup, targetAlpha, 0f, fadeDuration, () =>
		{
			if (leaveButton != null)
			{
				leaveButton.SetActive(value: false);
			}
		}));
	}

	private void ShowKickButton()
	{
		if (!(kickButton == null) && !(kickButtonCanvasGroup == null))
		{
			kickButton.SetActive(value: true);
			isKickButtonVisible = true;
			StartCoroutine(FadeCanvasGroup(kickButtonCanvasGroup, 0f, targetAlpha, fadeDuration));
		}
	}

	private void HideKickButton()
	{
		if (kickButton == null || kickButtonCanvasGroup == null)
		{
			return;
		}
		isKickButtonVisible = false;
		StartCoroutine(FadeCanvasGroup(kickButtonCanvasGroup, targetAlpha, 0f, fadeDuration, () =>
		{
			if (kickButton != null)
			{
				kickButton.SetActive(value: false);
			}
		}));
	}

	private IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float startAlpha, float endAlpha, float duration, Action onComplete = null)
	{
		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			float t = elapsed / duration;
			canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
			yield return null;
		}
		canvasGroup.alpha = endAlpha;
		onComplete?.Invoke();
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		isHovering = true;
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		isHovering = false;
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		if (isLocalPlayer && isHovering && leaveButton != null && leaveButton.activeSelf && isLeaveButtonVisible)
		{
			LeaveLobby();
		}
		else if (!isLocalPlayer && isLobbyOwner && isHovering && kickButton != null && kickButton.activeSelf && isKickButtonVisible)
		{
			KickPlayer();
		}
	}

	private void LeaveLobby()
	{
		Debug.Log("Local player clicked to leave lobby");
		if (MonoSingleton<LobbyManager>.Instance != null)
		{
			MonoSingleton<LobbyManager>.Instance.ClearRichPresence();
		}
		LobbySettings lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
		if (lobbySettings != null && lobbySettings.inALobby && lobbySettings.steamLobbyID != CSteamID.Nil)
		{
			if (SteamMatchmaking.GetLobbyOwner(lobbySettings.steamLobbyID) == SteamUser.GetSteamID())
			{
				Debug.Log("Host is leaving - disbanding lobby...");
				if (MonoSingleton<LobbyManager>.Instance != null)
				{
					MonoSingleton<LobbyManager>.Instance.DisbandLobby();
				}
				return;
			}
			SteamMatchmaking.LeaveLobby(lobbySettings.steamLobbyID);
			lobbySettings.inALobby = false;
			lobbySettings.steamLobbyID = CSteamID.Nil;
		}
		if (MonoSingleton<LobbyManager>.Instance != null)
		{
			MonoSingleton<LobbyManager>.Instance.CreateNewLobby();
		}
	}

	private void KickPlayer()
	{
		if (!(steamIdComponent == null))
		{
			Debug.Log($"Lobby owner clicked to kick player: {steamIdComponent.SteamId}");
			LobbySettings lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
			if (lobbySettings != null && lobbySettings.inALobby && lobbySettings.steamLobbyID != CSteamID.Nil)
			{
				CSteamID cSteamID = new CSteamID(steamIdComponent.SteamId);
				SteamMatchmaking.SetLobbyMemberData(lobbySettings.steamLobbyID, "Kicked", "1");
				SteamMatchmaking.SetLobbyMemberData(lobbySettings.steamLobbyID, "KickTarget", cSteamID.ToString());
				Debug.Log($"Player {steamIdComponent.SteamId} has been kicked from the lobby");
				StartCoroutine(CleanupKickData());
			}
		}
	}

	private IEnumerator CleanupKickData()
	{
		yield return new WaitForSeconds(1f);
		LobbySettings lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
		if (lobbySettings != null && lobbySettings.inALobby && lobbySettings.steamLobbyID != CSteamID.Nil)
		{
			SteamMatchmaking.SetLobbyMemberData(lobbySettings.steamLobbyID, "Kicked", "");
			SteamMatchmaking.SetLobbyMemberData(lobbySettings.steamLobbyID, "KickTarget", "");
		}
	}
}

}
