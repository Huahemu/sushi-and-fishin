namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::SushiAndFish.Casino.Steamworks;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.UI;

public class LobbyMemberList : MonoBehaviour
{
	[Header("UI")]
	[SerializeField]
	private Transform memberListContainer;

	[SerializeField]
	private GameObject memberEntryPrefab;

	private LobbySettings lobbySettings;

	private UIColorPalette colorPalette;

	private readonly Dictionary<CSteamID, GameObject> memberEntries = new Dictionary<CSteamID, GameObject>();

	private readonly Dictionary<CSteamID, Texture2D> avatarTextures = new Dictionary<CSteamID, Texture2D>();

	private readonly Dictionary<CSteamID, RawImage> pendingAvatars = new Dictionary<CSteamID, RawImage>();

	private Callback<LobbyChatUpdate_t> lobbyChatUpdateCallback;

	private Callback<PersonaStateChange_t> personaStateCallback;

	private Callback<AvatarImageLoaded_t> avatarLoadedCallback;

	private Callback<LobbyDataUpdate_t> lobbyDataUpdateCallback;

	private void Awake()
	{
		if (!SteamManager.Initialized)
		{
			base.enabled = false;
			return;
		}
		lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
		colorPalette = SushiAndFish.Casino.Original.CasinoResources.Load<UIColorPalette>("ColorSettings");
		lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyMemberUpdate);
		personaStateCallback = Callback<PersonaStateChange_t>.Create(OnPersonaStateChange);
		avatarLoadedCallback = Callback<AvatarImageLoaded_t>.Create(OnAvatarImageLoaded);
		lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
	}

	private void OnEnable()
	{
		VersionMismatchManager.OnVersionMismatchChanged += OnVersionMismatchChanged;
		LobbyManager.OnLobbyEnteredEvent += OnLobbyEntered;
	}

	private void OnDisable()
	{
		VersionMismatchManager.OnVersionMismatchChanged -= OnVersionMismatchChanged;
		LobbyManager.OnLobbyEnteredEvent -= OnLobbyEntered;
	}

	private void OnVersionMismatchChanged(bool hasMismatch)
	{
		RefreshAllVersionMismatches();
	}

	private void RefreshAllVersionMismatches()
	{
		if (lobbySettings == null || lobbySettings.steamLobbyID == CSteamID.Nil)
		{
			return;
		}
		CSteamID steamLobbyID = lobbySettings.steamLobbyID;
		CSteamID lobbyOwner = SteamMatchmaking.GetLobbyOwner(steamLobbyID);
		if (lobbyOwner == CSteamID.Nil || string.IsNullOrEmpty(SteamMatchmaking.GetLobbyMemberData(steamLobbyID, lobbyOwner, "GameVersion")))
		{
			return;
		}
		foreach (KeyValuePair<CSteamID, GameObject> memberEntry in memberEntries)
		{
			CheckAndUpdateVersionMismatch(memberEntry.Key, memberEntry.Value);
		}
	}

	private void Start()
	{
		if (lobbySettings != null && lobbySettings.inALobby && lobbySettings.steamLobbyID != CSteamID.Nil)
		{
			SyncLocalPlayerColorToSteamLobby();
			StartCoroutine(DelayedInitialRefresh());
		}
	}

	private IEnumerator DelayedInitialRefresh()
	{
		yield return new WaitForSeconds(0.5f);
		RefreshLobbyMembers();
	}

	private void OnDestroy()
	{
		lobbyChatUpdateCallback?.Dispose();
		personaStateCallback?.Dispose();
		avatarLoadedCallback?.Dispose();
		lobbyDataUpdateCallback?.Dispose();
		foreach (Texture2D value in avatarTextures.Values)
		{
			Object.Destroy(value);
		}
		avatarTextures.Clear();
	}

	private void RefreshLobbyMembers()
	{
		CSteamID currentLobbyID = MonoSingleton<LobbyManager>.Instance.GetCurrentLobbyID();
		if (currentLobbyID == CSteamID.Nil)
		{
			ClearAllEntries();
			return;
		}
		EnsureLocalPlayerVersionSet(currentLobbyID);
		int numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(currentLobbyID);
		HashSet<CSteamID> hashSet = new HashSet<CSteamID>();
		for (int i = 0; i < numLobbyMembers; i++)
		{
			CSteamID lobbyMemberByIndex = SteamMatchmaking.GetLobbyMemberByIndex(currentLobbyID, i);
			if (lobbyMemberByIndex == CSteamID.Nil)
			{
				continue;
			}
			hashSet.Add(lobbyMemberByIndex);
			string lobbyMemberData = SteamMatchmaking.GetLobbyMemberData(currentLobbyID, lobbyMemberByIndex, "PlayerColor");
			string lobbyMemberData2 = SteamMatchmaking.GetLobbyMemberData(currentLobbyID, lobbyMemberByIndex, "GameVersion");
			if (!string.IsNullOrEmpty(lobbyMemberData) && !string.IsNullOrEmpty(lobbyMemberData2))
			{
				if (!memberEntries.ContainsKey(lobbyMemberByIndex))
				{
					AddMemberEntry(lobbyMemberByIndex);
				}
				else
				{
					UpdateMemberEntry(lobbyMemberByIndex);
				}
			}
		}
		List<CSteamID> list = new List<CSteamID>();
		foreach (CSteamID key in memberEntries.Keys)
		{
			if (!hashSet.Contains(key))
			{
				list.Add(key);
			}
		}
		foreach (CSteamID item in list)
		{
			RemoveMemberEntry(item);
		}
	}

	private void ClearAllEntries()
	{
		foreach (CSteamID item in new List<CSteamID>(memberEntries.Keys))
		{
			RemoveMemberEntry(item);
		}
	}

	private Transform FindChildRecursively(Transform parent, string name)
	{
		foreach (Transform item in parent)
		{
			if (item.name == name)
			{
				return item;
			}
			Transform transform2 = FindChildRecursively(item, name);
			if (transform2 != null)
			{
				return transform2;
			}
		}
		return null;
	}

	private void AddMemberEntry(CSteamID id)
	{
		if (!memberEntries.ContainsKey(id) && !(lobbySettings == null) && !(lobbySettings.steamLobbyID == CSteamID.Nil))
		{
			CSteamID steamLobbyID = lobbySettings.steamLobbyID;
			string lobbyMemberData = SteamMatchmaking.GetLobbyMemberData(steamLobbyID, id, "PlayerColor");
			string lobbyMemberData2 = SteamMatchmaking.GetLobbyMemberData(steamLobbyID, id, "GameVersion");
			if (!string.IsNullOrEmpty(lobbyMemberData) && !string.IsNullOrEmpty(lobbyMemberData2))
			{
				GameObject gameObject = Object.Instantiate(memberEntryPrefab, memberListContainer);
				memberEntries[id] = gameObject;
				UpdateMemberEntry(id, gameObject);
			}
		}
	}

	private void UpdateMemberEntry(CSteamID id, GameObject entry = null)
	{
		if (!(entry == null) || memberEntries.TryGetValue(id, out entry))
		{
			TextMeshProUGUI componentInChildren = entry.GetComponentInChildren<TextMeshProUGUI>();
			if (componentInChildren != null)
			{
				componentInChildren.text = SteamFriends.GetFriendPersonaName(id);
			}
			RawImage rawImage = FindChildRecursively(entry.transform, "Avatar")?.GetComponent<RawImage>();
			if (rawImage == null)
			{
				rawImage = entry.GetComponentInChildren<RawImage>();
			}
			if (rawImage != null)
			{
				RequestOrAssignAvatar(id, rawImage);
			}
			SteamIdComponent component = entry.GetComponent<SteamIdComponent>();
			if (component != null)
			{
				component.SetSteamID(id.m_SteamID);
			}
			CheckAndUpdateVersionMismatch(id, entry);
		}
	}

	private void CheckAndUpdateVersionMismatch(CSteamID memberID, GameObject entry)
	{
		if (lobbySettings == null || lobbySettings.steamLobbyID == CSteamID.Nil)
		{
			return;
		}
		CSteamID steamLobbyID = lobbySettings.steamLobbyID;
		CSteamID lobbyOwner = SteamMatchmaking.GetLobbyOwner(steamLobbyID);
		if (lobbyOwner == CSteamID.Nil)
		{
			return;
		}
		string lobbyMemberData = SteamMatchmaking.GetLobbyMemberData(steamLobbyID, lobbyOwner, "GameVersion");
		if (string.IsNullOrEmpty(lobbyMemberData))
		{
			return;
		}
		string lobbyMemberData2 = SteamMatchmaking.GetLobbyMemberData(steamLobbyID, memberID, "GameVersion");
		if (memberID == lobbyOwner)
		{
			return;
		}
		VersionMismatchDisplay component = entry.GetComponent<VersionMismatchDisplay>();
		if (component != null)
		{
			if (string.IsNullOrEmpty(lobbyMemberData2) || lobbyMemberData2 != lobbyMemberData)
			{
				component.ShowVersionMismatch(lobbyMemberData2 ?? "unknown");
			}
			else
			{
				component.HideVersionMismatch();
			}
		}
	}

	public void UpdatePlayerVersionMismatch(CSteamID playerID, string playerVersion, bool hasMismatch)
	{
		if (!memberEntries.TryGetValue(playerID, out var value))
		{
			return;
		}
		VersionMismatchDisplay component = value.GetComponent<VersionMismatchDisplay>();
		if (component != null)
		{
			if (hasMismatch)
			{
				component.ShowVersionMismatch(playerVersion);
			}
			else
			{
				component.HideVersionMismatch();
			}
		}
	}

	private void RemoveMemberEntry(CSteamID id)
	{
		if (memberEntries.TryGetValue(id, out var value))
		{
			Object.Destroy(value);
			memberEntries.Remove(id);
			pendingAvatars.Remove(id);
		}
	}

	private void RequestOrAssignAvatar(CSteamID id, RawImage target)
	{
		if (avatarTextures.TryGetValue(id, out var value))
		{
			target.texture = value;
			return;
		}
		int largeFriendAvatar = SteamFriends.GetLargeFriendAvatar(id);
		if (largeFriendAvatar == -1)
		{
			pendingAvatars[id] = target;
		}
		else
		{
			LoadTextureFromHandle(largeFriendAvatar, id, target);
		}
	}

	private void OnAvatarImageLoaded(AvatarImageLoaded_t cb)
	{
		CSteamID steamID = cb.m_steamID;
		if (pendingAvatars.TryGetValue(steamID, out var value))
		{
			LoadTextureFromHandle(cb.m_iImage, steamID, value);
			pendingAvatars.Remove(steamID);
		}
	}

	private void LoadTextureFromHandle(int handle, CSteamID id, RawImage target)
	{
		if (handle > 0 && SteamUtils.GetImageSize(handle, out var pnWidth, out var pnHeight))
		{
			byte[] array = new byte[pnWidth * pnHeight * 4];
			if (SteamUtils.GetImageRGBA(handle, array, array.Length))
			{
				Texture2D texture2D = new Texture2D((int)pnWidth, (int)pnHeight, TextureFormat.RGBA32, mipChain: false);
				texture2D.LoadRawTextureData(array);
				texture2D.Apply();
				avatarTextures[id] = texture2D;
				target.texture = texture2D;
			}
		}
	}

	private void OnLobbyMemberUpdate(LobbyChatUpdate_t cb)
	{
		if (!((CSteamID)cb.m_ulSteamIDLobby != lobbySettings.steamLobbyID))
		{
			CSteamID id = new CSteamID(cb.m_ulSteamIDUserChanged);
			uint rgfChatMemberStateChange = cb.m_rgfChatMemberStateChange;
			if ((rgfChatMemberStateChange & 1) != 0)
			{
				StartCoroutine(DelayedRefresh());
			}
			else if ((rgfChatMemberStateChange & 0x1E) != 0)
			{
				RemoveMemberEntry(id);
			}
		}
	}

	private IEnumerator DelayedRefresh()
	{
		yield return new WaitForSeconds(0.5f);
		RefreshLobbyMembers();
	}

	private void OnPersonaStateChange(PersonaStateChange_t cb)
	{
		CSteamID cSteamID = new CSteamID(cb.m_ulSteamID);
		if (memberEntries.TryGetValue(cSteamID, out var value))
		{
			RawImage rawImage = FindChildRecursively(value.transform, "Avatar")?.GetComponent<RawImage>();
			if (rawImage == null)
			{
				rawImage = value.GetComponentInChildren<RawImage>();
			}
			if (rawImage != null)
			{
				RequestOrAssignAvatar(cSteamID, rawImage);
			}
		}
	}

	public void OnLobbyEntered()
	{
		SyncLocalPlayerColorToSteamLobby();
		StartCoroutine(DelayedInitialRefresh());
	}

	private void EnsureLocalPlayerVersionSet(CSteamID lobbyId)
	{
		if (SteamManager.Initialized && !(lobbyId == CSteamID.Nil))
		{
			CSteamID steamID = SteamUser.GetSteamID();
			if (string.IsNullOrEmpty(SteamMatchmaking.GetLobbyMemberData(lobbyId, steamID, "GameVersion")))
			{
				SteamMatchmaking.SetLobbyMemberData(lobbyId, "GameVersion", Application.version);
			}
		}
	}

	private void SyncLocalPlayerColorToSteamLobby()
	{
		if (SteamManager.Initialized && !(lobbySettings == null) && !(lobbySettings.steamLobbyID == CSteamID.Nil) && !(MonoSingleton<CosmeticsUnlockManager>.Instance == null))
		{
			Color? playerColor = MonoSingleton<CosmeticsUnlockManager>.Instance.GetPlayerColor();
			Color color = (playerColor.HasValue ? playerColor.Value : colorPalette.playerColors[Random.Range(0, colorPalette.playerColors.Length)]);
			if (!playerColor.HasValue)
			{
				MonoSingleton<CosmeticsUnlockManager>.Instance.SetPlayerColor(color);
			}
			string pchValue = ColorHexUtility.ColorToHex(color);
			SteamMatchmaking.SetLobbyMemberData(lobbySettings.steamLobbyID, "PlayerColor", pchValue);
			SteamMatchmaking.SetLobbyMemberData(lobbySettings.steamLobbyID, "GameVersion", Application.version);
		}
	}

	private void OnLobbyDataUpdate(LobbyDataUpdate_t cb)
	{
		if (cb.m_ulSteamIDMember == 0L || lobbySettings == null || lobbySettings.steamLobbyID == CSteamID.Nil || (CSteamID)cb.m_ulSteamIDLobby != lobbySettings.steamLobbyID)
		{
			return;
		}
		CSteamID cSteamID = new CSteamID(cb.m_ulSteamIDMember);
		CSteamID steamLobbyID = lobbySettings.steamLobbyID;
		string lobbyMemberData = SteamMatchmaking.GetLobbyMemberData(steamLobbyID, cSteamID, "PlayerColor");
		string lobbyMemberData2 = SteamMatchmaking.GetLobbyMemberData(steamLobbyID, cSteamID, "GameVersion");
		if (!string.IsNullOrEmpty(lobbyMemberData) && !string.IsNullOrEmpty(lobbyMemberData2))
		{
			if (!memberEntries.ContainsKey(cSteamID))
			{
				AddMemberEntry(cSteamID);
			}
			else
			{
				UpdateMemberEntry(cSteamID);
			}
		}
	}

	private void OnLobbyEnteredEvent()
	{
		OnLobbyEntered();
	}
}

}
