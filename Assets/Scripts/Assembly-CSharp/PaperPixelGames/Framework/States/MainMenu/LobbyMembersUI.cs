using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking;
using Steamworks;
using Steamworks.Data;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace PaperPixelGames.Framework.States.MainMenu
{
	public class LobbyMembersUI : MonoBehaviour
	{
		[Header("UI")]
		[SerializeField]
		private TextMeshProUGUI _lobbyCountText;

		[SerializeField]
		private BaseButton _lobbyInviteButton;

		[SerializeField]
		private LobbyMemberElement _lobbyMemberPrefab;

		[SerializeField]
		private Transform _memberContainer;

		[Tooltip("Optional. If empty, elements will be instantiated on-demand and stored here.")]
		[SerializeField]
		private List<LobbyMemberElement> _memberElements = new List<LobbyMemberElement>();

		[Header("Defaults")]
		[SerializeField]
		private Sprite _defaultAvatarSprite;

		private readonly Dictionary<SteamId, int> _steamIdToSlot = new Dictionary<SteamId, int>();

		private int _rebuildVersion;

		private int _lastMemberCount;

		private int _lastMaxSlots;

		public void Initialize()
		{
			if (_lobbyInviteButton != null)
			{
				_lobbyInviteButton.Clicked += OnLobbyInviteButtonClick;
			}
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
		}

		private void OnDestroy()
		{
			if (_lobbyInviteButton != null)
			{
				_lobbyInviteButton.Clicked -= OnLobbyInviteButtonClick;
			}
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
		}

		private void OnSelectedLocaleChanged(Locale locale)
		{
			UpdateLobbyCountText(_lastMemberCount, _lastMaxSlots);
		}

		private void OnLobbyInviteButtonClick()
		{
			if (SteamClient.IsValid)
			{
				Lobby lobby = ((Singleton<MultiplayerCoordinator>.Instance != null) ? Singleton<MultiplayerCoordinator>.Instance.LobbyService.CurrentLobby : default(Lobby));
				if (lobby.Id.Value != 0L)
				{
					SteamFriends.OpenGameInviteOverlay(lobby.Id);
				}
			}
		}

		public async UniTask RebuildFromLobbyAsync(Lobby lobby)
		{
			if (_memberElements == null)
			{
				_memberElements = new List<LobbyMemberElement>();
			}
			_steamIdToSlot.Clear();
			int version = ++_rebuildVersion;
			if (lobby.Id.Value == 0L)
			{
				UtilityMethods.Log("<b><color=#66D9EF>[LobbyMembersUI]</color></b>\n  <color=#F92672>Abort</color>\n    <b>Reason</b>: LobbyId is 0\n" + $"    <b>Version</b>: {version}");
				return;
			}
			UtilityMethods.Log("<b><color=#66D9EF>[LobbyMembersUI]</color></b>\n  <color=#A6E22E>Rebuild Start</color>\n" + $"    <b>Version</b>: {version}\n" + $"    <b>Members</b>: {lobby.MemberCount}\n" + $"    <b>Slots</b>: {_memberElements.Count}");
			ClearAllSlots();
			List<Friend> members = lobby.Members.ToList();
			List<Friend> orderedMembers = OrderMembersDeterministically(lobby);
			int maxSlots = _memberElements.Count;
			int shownCount = Math.Min(orderedMembers.Count, maxSlots);
			SteamId ownerId = GetLobbyOwnerIdSafe(lobby);
			RefreshLobbyHeader(members.Count, maxSlots);
			UtilityMethods.Log("<b><color=#66D9EF>[LobbyMembersUI]</color></b>\n  <color=#A6E22E>Prepared</color>\n" + $"    <b>OrderedMembers</b>: {orderedMembers.Count}\n" + $"    <b>ShownCount</b>: {shownCount}\n" + "    <b>FillDirection</b>: right-to-left (slot = max-1-i)");
			for (int i = 0; i < shownCount; i++)
			{
				if (this == null || version != _rebuildVersion)
				{
					UtilityMethods.Log("<b><color=#66D9EF>[LobbyMembersUI]</color></b>\n  <color=#F92672>Abort</color>\n    <b>Reason</b>: stale rebuild\n" + $"    <b>MyVersion</b>: {version}\n" + $"    <b>CurrentVersion</b>: {_rebuildVersion}");
					return;
				}
				int slotIndex = maxSlots - 1 - i;
				if (!TryGetUsableElement(slotIndex, out var element))
				{
					UtilityMethods.Log("<b><color=#66D9EF>[LobbyMembersUI]</color></b>\n  <color=#FD971F>Slot Skip</color>\n" + $"    <b>Index</b>: {i}\n" + $"    <b>Slot</b>: {slotIndex}\n" + "    <b>Reason</b>: unusable element (prefab/container/avatar missing)");
					continue;
				}
				ApplySlotBaseVisuals(slotIndex, element);
				Friend friend = orderedMembers[i];
				_steamIdToSlot[friend.Id] = slotIndex;
				bool isHost = (ulong)friend.Id == (ulong)ownerId;
				element.SetHostIconVisible(isHost);
				UtilityMethods.Log("<b><color=#66D9EF>[LobbyMembersUI]</color></b>\n  <color=#A6E22E>Avatar Fetch</color>\n" + $"    <b>Index</b>: {i}\n" + $"    <b>Slot</b>: {slotIndex}\n" + $"    <b>SteamId</b>: {(ulong)friend.Id}");
				Sprite sprite;
				try
				{
					sprite = await SteamAvatarCache.GetAvatarSpriteAsync(friend.Id);
				}
				catch (Exception ex)
				{
					sprite = null;
					UtilityMethods.Log("<b><color=#66D9EF>[LobbyMembersUI]</color></b>\n  <color=#F92672>Avatar Fetch Failed</color>\n" + $"    <b>Index</b>: {i}\n" + $"    <b>Slot</b>: {slotIndex}\n" + $"    <b>SteamId</b>: {(ulong)friend.Id}\n" + "    <b>Exception</b>: " + ex.GetType().Name + "\n    <b>Message</b>: " + ex.Message);
				}
				if (this == null || version != _rebuildVersion)
				{
					return;
				}
				await UniTask.SwitchToMainThread();
				if (TryGetUsableElement(slotIndex, out element))
				{
					element.gameObject.SetActive(value: true);
					bool num = sprite == null;
					element.Avatar.sprite = ((sprite != null) ? sprite : _defaultAvatarSprite);
					element.SetHostIconVisible(isHost);
					if (num)
					{
						ScheduleMissingAvatarRefresh(friend.Id, version);
					}
					UtilityMethods.Log("<b><color=#66D9EF>[LobbyMembersUI]</color></b>\n  <color=#A6E22E>Avatar Assigned</color>\n" + $"    <b>Index</b>: {i}\n" + $"    <b>Slot</b>: {slotIndex}\n" + $"    <b>SteamId</b>: {(ulong)friend.Id}\n" + "    <b>Sprite</b>: " + ((sprite != null) ? "Steam" : "Default"));
				}
			}
			if (!(this == null) && version == _rebuildVersion)
			{
				RefreshLobbyHeader(members.Count, maxSlots);
				UtilityMethods.Log("<b><color=#66D9EF>[LobbyMembersUI]</color></b>\n  <color=#A6E22E>Rebuild End</color>\n" + $"    <b>Version</b>: {version}\n" + $"    <b>Members</b>: {members.Count}\n" + $"    <b>Slots</b>: {maxSlots}\n" + $"    <b>Shown</b>: {shownCount}");
			}
		}

		public UniTask DelayedRebuildLobbyUI(Lobby lobby, int delayMs)
		{
			return DelayedRebuildLobbyUIInternal(lobby, delayMs);
		}

		private async UniTask DelayedRebuildLobbyUIInternal(Lobby lobby, int delayMs)
		{
			await UniTask.Delay(delayMs, ignoreTimeScale: true);
			if (!(this == null))
			{
				await RebuildFromLobbyAsync(lobby);
			}
		}

		private void ScheduleMissingAvatarRefresh(SteamId steamId, int rebuildVersion)
		{
			RefreshAvatarLaterAsync(steamId, rebuildVersion, 800).FireAndForget("[LobbyMembersUI]");
			RefreshAvatarLaterAsync(steamId, rebuildVersion, 1800).FireAndForget("[LobbyMembersUI]");
		}

		private async UniTask RefreshAvatarLaterAsync(SteamId steamId, int rebuildVersion, int delayMs)
		{
			await UniTask.Delay(delayMs, ignoreTimeScale: true);
			if (this == null || rebuildVersion != _rebuildVersion || !_steamIdToSlot.TryGetValue(steamId, out var slotIndex) || !TryGetUsableElement(slotIndex, out var element))
			{
				return;
			}
			Sprite sprite;
			try
			{
				sprite = await SteamAvatarCache.GetAvatarSpriteAsync(steamId);
			}
			catch
			{
				sprite = null;
			}
			if (!(sprite == null) && !(this == null) && rebuildVersion == _rebuildVersion)
			{
				await UniTask.SwitchToMainThread();
				if (TryGetUsableElement(slotIndex, out element))
				{
					element.Avatar.sprite = sprite;
				}
			}
		}

		private List<Friend> OrderMembersDeterministically(Lobby lobby)
		{
			try
			{
				SteamId ownerId = lobby.Owner.Id;
				return lobby.Members.OrderByDescending((Friend m) => (ulong)m.Id == (ulong)ownerId).ThenBy((Func<Friend, ulong>)((Friend m) => m.Id)).ToList();
			}
			catch
			{
				return lobby.Members.OrderBy((Func<Friend, ulong>)((Friend m) => m.Id)).ToList();
			}
		}

		private void RefreshLobbyHeader(int memberCount, int maxSlots)
		{
			UpdateLobbyCountText(memberCount, maxSlots);
			RefreshLobbyInviteButtonVisibility(memberCount, maxSlots);
		}

		private void UpdateLobbyCountText(int memberCount, int maxSlots)
		{
			_lastMemberCount = memberCount;
			_lastMaxSlots = maxSlots;
			int num = Math.Min(memberCount, maxSlots);
			if (_lobbyCountText != null)
			{
				_lobbyCountText.text = LocalizationDatabase.GetFormattedString("UiTable", "ui.main.lobby_count", "Lobby ({0}/{1})", num, maxSlots);
			}
		}

		private void RefreshLobbyInviteButtonVisibility(int memberCount, int maxSlots)
		{
			if (!(_lobbyInviteButton == null))
			{
				bool flag = maxSlots <= 0 || memberCount < maxSlots;
				if (_lobbyInviteButton.gameObject.activeSelf != flag)
				{
					_lobbyInviteButton.gameObject.SetActive(flag);
				}
			}
		}

		private void ApplySlotBaseVisuals(int slotIndex, LobbyMemberElement element)
		{
			if (!(element == null))
			{
				element.gameObject.SetActive(value: true);
				element.SetHostIconVisible(visible: false);
				if (_defaultAvatarSprite != null && element.Avatar != null)
				{
					element.Avatar.sprite = _defaultAvatarSprite;
				}
			}
		}

		private bool TryGetUsableElement(int slotIndex, out LobbyMemberElement element)
		{
			element = null;
			if (_lobbyMemberPrefab == null || _memberContainer == null)
			{
				return false;
			}
			if (_memberElements == null)
			{
				_memberElements = new List<LobbyMemberElement>();
			}
			while (_memberElements.Count <= slotIndex)
			{
				_memberElements.Add(null);
			}
			element = _memberElements[slotIndex];
			if (element == null || element.gameObject == null || element.Avatar == null)
			{
				element = UnityEngine.Object.Instantiate(_lobbyMemberPrefab, _memberContainer);
				_memberElements[slotIndex] = element;
			}
			if (element != null)
			{
				return element.Avatar != null;
			}
			return false;
		}

		private void ClearAllSlots()
		{
			if (_memberElements == null)
			{
				_memberElements = new List<LobbyMemberElement>();
			}
			for (int i = 0; i < _memberElements.Count; i++)
			{
				if (TryGetUsableElement(i, out var element))
				{
					element.gameObject.SetActive(value: false);
					element.SetHostIconVisible(visible: false);
					if (_defaultAvatarSprite != null && element.Avatar != null)
					{
						element.Avatar.sprite = _defaultAvatarSprite;
					}
				}
			}
			RefreshLobbyHeader(0, _memberElements.Count);
		}

		private void OnValidate()
		{
			if (_memberElements == null)
			{
				_memberElements = new List<LobbyMemberElement>();
			}
		}

		private bool TryGetCurrentLobby(out Lobby lobby)
		{
			lobby = ((Singleton<MultiplayerCoordinator>.Instance != null) ? Singleton<MultiplayerCoordinator>.Instance.LobbyService.CurrentLobby : default(Lobby));
			return lobby.Id.Value != 0;
		}

		public SteamId GetLobbyOwnerSteamIdOrLocal()
		{
			if (TryGetCurrentLobby(out var lobby) && lobby.Id.Value != 0L)
			{
				return lobby.Owner.Id;
			}
			return SteamClient.SteamId;
		}

		private SteamId GetLobbyOwnerIdSafe(Lobby lobby)
		{
			try
			{
				return lobby.Owner.Id;
			}
			catch
			{
				return default;
			}
		}
	}
}
