namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::SushiAndFish.Casino.Steamworks;
using global::UnityEngine;

public class PublicLobbyListUI : MonoBehaviour
{
	[Header("UI")]
	[SerializeField]
	private GameObject rootPanel;

	[SerializeField]
	private Transform contentRoot;

	[SerializeField]
	private GameObject lobbyEntryPrefab;

	private readonly List<GameObject> _entries = new List<GameObject>();

	private CallResult<LobbyMatchList_t> _lobbyMatchListResult;

	private void Awake()
	{
		if (!SteamManager.Initialized)
		{
			if (rootPanel != null)
			{
				rootPanel.SetActive(value: false);
			}
			base.enabled = false;
		}
		else
		{
			_lobbyMatchListResult = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);
		}
	}

	public void Show()
	{
		if (!SteamManager.Initialized)
		{
			Debug.LogWarning("[PublicLobbyListUI] Show: Steam not initialized.");
			return;
		}
		if (rootPanel != null && !rootPanel.activeSelf)
		{
			rootPanel.SetActive(value: true);
		}
		Refresh();
	}

	public void Hide()
	{
		if (rootPanel != null && rootPanel.activeSelf)
		{
			rootPanel.SetActive(value: false);
		}
	}

	public void Refresh()
	{
		if (SteamManager.Initialized)
		{
			ClearEntries();
			SteamMatchmaking.AddRequestLobbyListStringFilter("GameStarted", "1", ELobbyComparison.k_ELobbyComparisonEqual);
			SteamMatchmaking.AddRequestLobbyListResultCountFilter(50);
			SteamAPICall_t steamAPICall_t = SteamMatchmaking.RequestLobbyList();
			if (steamAPICall_t == SteamAPICall_t.Invalid)
			{
				Debug.LogWarning("[PublicLobbyListUI] RequestLobbyList returned invalid handle.");
				return;
			}
			_lobbyMatchListResult.Set(steamAPICall_t);
			Debug.Log("[PublicLobbyListUI] Lobby list requested, waiting for Steam callback...");
		}
	}

	private void OnLobbyMatchList(LobbyMatchList_t result, bool failure)
	{
		if (failure)
		{
			Debug.Log("[PublicLobbyListUI] Lobby list request failed (IO failure).");
			return;
		}
		if (result.m_nLobbiesMatching == 0)
		{
			Debug.Log("[PublicLobbyListUI] Lobby list: 0 public lobbies for this game.");
			return;
		}
		Debug.Log($"[PublicLobbyListUI] Lobby list: {result.m_nLobbiesMatching} lobbies.");
		if (contentRoot == null || lobbyEntryPrefab == null)
		{
			Debug.LogWarning("[PublicLobbyListUI] contentRoot or lobbyEntryPrefab is not set in the inspector - no entries will be created.");
			return;
		}
		for (int i = 0; i < result.m_nLobbiesMatching; i++)
		{
			CSteamID lobbyByIndex = SteamMatchmaking.GetLobbyByIndex(i);
			AddEntry(lobbyByIndex);
		}
	}

	private void AddEntry(CSteamID lobbyId)
	{
		if (!(contentRoot == null) && !(lobbyEntryPrefab == null))
		{
			GameObject gameObject = Object.Instantiate(lobbyEntryPrefab, contentRoot);
			_entries.Add(gameObject);
			string text = SteamMatchmaking.GetLobbyData(lobbyId, "name");
			if (string.IsNullOrEmpty(text))
			{
				text = "Lobby " + lobbyId.m_SteamID;
			}
			int numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
			int lobbyMemberLimit = SteamMatchmaking.GetLobbyMemberLimit(lobbyId);
			string labelText = ((lobbyMemberLimit > 0) ? $"{text} ({numLobbyMembers}/{lobbyMemberLimit})" : $"{text} ({numLobbyMembers})");
			PublicLobbyListEntry component = gameObject.GetComponent<PublicLobbyListEntry>();
			if (component != null)
			{
				component.Initialize(this, lobbyId, labelText);
			}
		}
	}

	private void ClearEntries()
	{
		for (int i = 0; i < _entries.Count; i++)
		{
			if (_entries[i] != null)
			{
				Object.Destroy(_entries[i]);
			}
		}
		_entries.Clear();
	}

	public void JoinLobby(CSteamID lobbyId)
	{
		if (!SteamManager.Initialized)
		{
			Debug.LogWarning("[PublicLobbyListUI] JoinLobby: Steam not initialized.");
			return;
		}
		Debug.Log($"[PublicLobbyListUI] JoinLobby: joining lobby {lobbyId.m_SteamID}");
		LobbyManager instance = MonoSingleton<LobbyManager>.Instance;
		if (instance != null)
		{
			instance.CleanupCurrentLobby();
		}
		SteamMatchmaking.JoinLobby(lobbyId);
	}
}

}
