using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace PaperPixelGames.Framework.States.MainMenu
{
	public sealed class LobbyRoomScreen : BaseScreen
	{
		[SerializeField]
		private LobbyRoomSkin _skin;

		private MainMenuManager _menu;

		private LobbyRoomService _service;

		private readonly Dictionary<string, Transform> _elements = new Dictionary<string, Transform>(StringComparer.Ordinal);

		private readonly List<Action> _unbind = new List<Action>();

		private readonly Dictionary<string, Func<string>> _localizedOverrides = new Dictionary<string, Func<string>>(StringComparer.Ordinal);

		private LobbyVisibility _createVisibility = LobbyVisibility.FriendsOnly;

		private LobbyVisibility _editVisibility;

		private int _editCapacity = 5;

		private bool _editLocked;

		private bool _editReady = true;

		private bool _clearPassword;

		private bool _friendsFilter;

		private bool _showingRoom;

		private bool _working;

		private bool _wired;

		private bool _released;

		private string _modal;

		private ulong _joinLobbyId;

		private ulong _deferredPasswordLobbyId;

		private LobbyRoomService _deferredPasswordService;

		private string _deferredPasswordError;

		private Action _confirm;

		private GameObject _previousSelection;

		private float _nextRefreshAt;

		private int _viewVersion;

		private bool _departureDismissal;

		private LobbyRoomSnapshot _previewSnapshot;

		private IReadOnlyList<LobbyBrowserEntry> _previewRooms;

		private LobbyRoomSnapshot _renderedSnapshot;

		private ulong _presentedGuestLobbyId;

		private ulong _observedHostLobbyId;

		private readonly HashSet<ulong> _presentedHostMembers = new HashSet<ulong>();

		private static readonly Dictionary<string, (string Key, string Turkish)> ResultMessages = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
		{
			["A lobby operation is already in progress."] = ("busy", "Bir lobi işlemi zaten sürüyor."),
			["Connect to Steam to create a lobby."] = ("connect_create", "Lobi oluşturmak için Steam'e bağlan."),
			["Return to the main menu before creating a lobby."] = ("return_menu", "Lobi oluşturmadan önce ana menüye dön."),
			["Lobby created."] = ("created", "Lobi oluşturuldu."),
			["Steam could not create a lobby. Please try again."] = ("create_failed", "Steam lobi oluşturamadı. Tekrar dene."),
			["Enter a valid SUSHI lobby code."] = ("valid_code", "Geçerli bir SUSHI lobi kodu gir."),
			["Connect to Steam before joining a lobby."] = ("connect_join", "Lobiye katılmadan önce Steam'e bağlan."),
			["This lobby code is invalid."] = ("invalid_code", "Bu lobi kodu geçersiz."),
			["The password is too long."] = ("password_long", "Şifre çok uzun."),
			["You are already in this lobby."] = ("already_joined", "Zaten bu lobidesin."),
			["Joined the lobby."] = ("joined", "Lobiye katıldın."),
			["This lobby is full."] = ("room_full", "Bu lobi dolu."),
			["Steam could not join this lobby. Check the code and try again."] = ("join_failed", "Steam bu lobiye katılamadı. Kodu kontrol edip tekrar dene."),
			["Lobby join cancelled."] = ("join_cancelled", "Lobiye katılım iptal edildi."),
			["Steam could not complete the lobby request."] = ("request_failed", "Steam lobi isteğini tamamlayamadı."),
			["The lobby list is already refreshing."] = ("refreshing", "Lobi listesi zaten yenileniyor."),
			["Connect to Steam to browse lobbies."] = ("connect_browse", "Lobileri görmek için Steam'e bağlan."),
			["Steam could not refresh the lobby list."] = ("refresh_failed", "Steam lobi listesini yenileyemedi."),
			["Only the host can change settings while the lobby is waiting."] = ("settings_host", "Lobi beklemedeyken ayarları yalnızca ev sahibi değiştirebilir."),
			["Steam could not update lobby visibility."] = ("visibility_failed", "Steam lobi görünürlüğünü güncelleyemedi."),
			["Steam could not update the lobby lock."] = ("lock_failed", "Steam lobi kilidini güncelleyemedi."),
			["Lobby settings saved."] = ("settings_saved", "Lobi ayarları kaydedildi."),
			["Join a waiting lobby before changing ready status."] = ("ready_waiting", "Hazır durumunu değiştirmek için bekleyen bir lobiye katıl."),
			["Only the lobby host can remove players."] = ("remove_host", "Oyuncuları yalnızca ev sahibi çıkarabilir."),
			["The lobby host cannot be removed."] = ("cannot_remove_host", "Lobi ev sahibi çıkarılamaz."),
			["This player has already left."] = ("player_left", "Bu oyuncu zaten ayrıldı."),
			["Player banned. The ban remains until you remove it."] = ("player_banned", "Oyuncu yasaklandı. Yasak, sen kaldırana kadar sürer."),
			["Player removed. They can rejoin after 30 seconds."] = ("player_kicked", "Oyuncu çıkarıldı. 30 saniye sonra yeniden katılabilir."),
			["Only the host can remove bans."] = ("unban_host", "Yasakları yalnızca ev sahibi kaldırabilir."),
			["Ban removed."] = ("ban_removed", "Yasak kaldırıldı."),
			["Lobby code copied."] = ("copied", "Lobi kodu kopyalandı."),
			["This lobby uses an incompatible lobby version."] = ("incompatible_lobby", "Bu lobinin sürümü uyumsuz."),
			["The host is running a different game version."] = ("different_version", "Ev sahibi farklı bir oyun sürümü kullanıyor."),
			["This lobby is unavailable or uses an older game version."] = ("unavailable_lobby", "Bu lobi kullanılamıyor veya eski bir oyun sürümü kullanıyor."),
			["The lobby host is unavailable."] = ("host_unavailable", "Lobi ev sahibine ulaşılamıyor."),
			["Steam could not contact the lobby host."] = ("host_contact_failed", "Steam lobi ev sahibine ulaşamadı."),
			["The lobby host did not respond. Please try again."] = ("host_timeout", "Lobi ev sahibi yanıt vermedi. Tekrar dene."),
			["The host is starting the game. Try again shortly."] = ("host_starting", "Ev sahibi oyunu başlatıyor. Birazdan tekrar dene."),
			["Steam could not authorize the private lobby invitation."] = ("private_invite_failed", "Steam özel lobi davetini onaylayamadı."),
			["The lobby host left. Returning to your own lobby."] = ("host_left", "Lobi ev sahibi ayrıldı. Kendi lobine dönülüyor."),
			["Lobby settings are missing."] = ("settings_missing", "Lobi ayarları eksik."),
			["Enter a lobby name."] = ("name_missing", "Bir lobi adı gir."),
			["Lobby names can contain up to 32 characters."] = ("name_limit", "Lobi adı en fazla 32 karakter olabilir."),
			["Choose a valid lobby visibility."] = ("visibility_invalid", "Geçerli bir lobi görünürlüğü seç."),
			["Choose between 1 and 5 players."] = ("capacity_invalid", "1 ile 5 arasında oyuncu kapasitesi seç."),
			["The player limit cannot be smaller than the current party."] = ("capacity_small", "Oyuncu sınırı mevcut ekipten küçük olamaz."),
			["Passwords must contain 4–32 characters."] = ("password_length", "Şifre 4–32 karakter içermeli."),
			["Passwords cannot contain control characters."] = ("password_invalid", "Şifre kontrol karakterleri içeremez."),
			["Steam identity is unavailable."] = ("identity_unavailable", "Steam kimliği kullanılamıyor."),
			["You are banned from this host's lobbies."] = ("you_banned", "Bu ev sahibinin lobilerinden yasaklandın."),
			["You were removed. Wait 30 seconds before rejoining."] = ("you_kicked", "Lobiden çıkarıldın. Yeniden katılmadan önce 30 saniye bekle."),
			["This lobby is locked."] = ("room_locked", "Bu lobi kilitli."),
			["This lobby is for the host's Steam friends."] = ("friends_required", "Bu lobi ev sahibinin Steam arkadaşlarına açık."),
			["Too many attempts. Please wait a moment."] = ("rate_limit", "Çok fazla deneme yaptın. Biraz bekle."),
			["This lobby requires a password."] = ("password_required", "Bu lobi için şifre gerekiyor."),
			["Incorrect lobby password."] = ("wrong_password", "Lobi şifresi yanlış."),
			["You were banned by the lobby host."] = ("you_banned_by_host", "Lobi ev sahibi seni yasakladı."),
			["You were removed by the lobby host."] = ("you_removed_by_host", "Lobi ev sahibi seni çıkardı."),
			["The lobby host left or changed during the join. Please use a new lobby code."] = ("owner_changed_code", "Katılırken lobi ev sahibi ayrıldı veya değişti. Yeni bir lobi kodu kullan."),
			["The lobby host changed during the join. Please try again."] = ("owner_changed_retry", "Katılırken lobi ev sahibi değişti. Tekrar dene."),
			["Lobby access was not approved. Join again with the lobby code and password."] = ("admission_not_approved", "Lobiye erişimin onaylanmadı. Lobi kodu ve şifreyle yeniden katıl."),
			["You were removed from the lobby."] = ("you_removed", "Lobiden çıkarıldın."),
			["The lobby host removed your access."] = ("access_revoked", "Lobi ev sahibi erişimini kaldırdı."),
			["Left the lobby."] = ("left_lobby", "Lobiden ayrıldın.")
		};

		private LobbyRoomSnapshot Snapshot
		{
			get
			{
				LobbyRoomSnapshot lobbyRoomSnapshot = _previewSnapshot;
				if (lobbyRoomSnapshot == null)
				{
					LobbyRoomService service = _service;
					if ((object)service == null)
					{
						return null;
					}
					lobbyRoomSnapshot = service.Snapshot;
				}
				return lobbyRoomSnapshot;
			}
		}

		private bool Busy
		{
			get
			{
				if (!_working)
				{
					return _service?.IsBusy ?? false;
				}
				return true;
			}
		}

		private bool SteamReady
		{
			get
			{
				if (_service != null && Singleton<MultiplayerCoordinator>.TryGetInstance(out var instance))
				{
					return instance.IsSteamConnectionAvailable;
				}
				return false;
			}
		}

		private static string T(string key, string english, string turkish)
		{
			return LobbyRoomLabel.Text(key, english, turkish);
		}

		public void Configure(MainMenuManager menu)
		{
			if (!_released)
			{
				_menu = menu;
				Initialize();
				AttachService();
			}
		}

		protected override void OnInitialize()
		{
			if (_wired || _released)
			{
				return;
			}
			if (_skin == null)
			{
				_skin = Resources.Load<LobbyRoomSkin>("LobbyRoomSkin");
			}
			if (base.transform.Find("Board") == null && _skin != null)
			{
				Build(_skin);
			}
			if (_skin == null || base.transform.Find("Board") == null)
			{
				Debug.LogError("[LobbyRoomScreen] LobbyRoomSkin is missing. Rebuild the lobby UI prefab.");
				return;
			}
			Transform[] componentsInChildren = GetComponentsInChildren<Transform>(includeInactive: true);
			foreach (Transform transform in componentsInChildren)
			{
				_elements[transform.name] = transform;
			}
			Click("Close", Hide);
			Click("BrowserTab", ShowBrowser);
			Click("RoomTab", ShowRoom);
			Click("PublicFilter", () =>
			{
				SetFilter(friends: false);
			});
			Click("FriendsFilter", () =>
			{
				SetFilter(friends: true);
			});
			Click("Refresh", () =>
			{
				Run(RefreshService);
			});
			Click("CreateVisibility", CycleCreateVisibility);
			Click("CreateLobby", CreateLobby);
			Click("JoinCode", () =>
			{
				OpenJoin(0uL);
			});
			Click("JoinCancel", CloseModal);
			Click("JoinSubmit", JoinLobby);
			Click("Ready", ToggleReady);
			Click("Start", ChooseSave);
			Click("Leave", ConfirmLeave);
			Click("CopyCode", CopyCode);
			Click("Invite", InviteFriends);
			Click("Settings", OpenSettings);
			Click("Banned", OpenBans);
			Click("SettingsCancel", CloseModal);
			Click("SettingsSave", SaveSettings);
			Click("SettingsVisibility", () =>
			{
				_editVisibility = NextVisibility(_editVisibility);
				RefreshSettingsLabels();
			});
			Click("SettingsCapacity", CycleCapacity);
			Click("SettingsLocked", () =>
			{
				_editLocked = !_editLocked;
				RefreshSettingsLabels();
			});
			Click("SettingsReady", () =>
			{
				_editReady = !_editReady;
				RefreshSettingsLabels();
			});
			Click("RemovePassword", () =>
			{
				_clearPassword = !_clearPassword;
				RefreshSettingsLabels();
			});
			Click("ShowPassword", TogglePasswordVisibility);
			Click("ConfirmCancel", CloseModal);
			Click("ConfirmSubmit", () =>
			{
				Action confirm = _confirm;
				CloseModal();
				confirm?.Invoke();
			});
			Click("BansClose", CloseModal);
			TMP_InputField search = Field("Search");
			search.onValueChanged.AddListener(OnSearchChanged);
			_unbind.Add(() =>
			{
				if (search != null)
				{
					search.onValueChanged.RemoveListener(OnSearchChanged);
				}
			});
			LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
			_unbind.Add(() =>
			{
				LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
			});
			_wired = true;
			AttachService();
			if (!Application.isPlaying)
			{
				return;
			}
			AsyncOperationHandle<LocalizationSettings> initialization = LocalizationSettings.InitializationOperation;
			if (initialization.IsDone)
			{
				return;
			}
			Action<AsyncOperationHandle<LocalizationSettings>> initialized = (AsyncOperationHandle<LocalizationSettings> _) =>
			{
				if (this != null && _wired)
				{
					OnLocaleChanged(LocalizationSettings.SelectedLocale);
				}
			};
			initialization.Completed += initialized;
			_unbind.Add(() =>
			{
				initialization.Completed -= initialized;
			});
		}

		protected override void OnShow()
		{
			if (_wired)
			{
				_previousSelection = ((EventSystem.current != null) ? EventSystem.current.currentSelectedGameObject : null);
				_viewVersion++;
				AttachService();
				RefreshLabels();
				Render();
				Focus("BrowserTab");
			}
		}

		protected override void OnHide()
		{
			ClearDeferredPasswordRequest();
			if (_wired)
			{
				_viewVersion++;
				if (!_departureDismissal)
				{
					_service?.CancelPendingJoin();
				}
				CloseModal();
				ClearPasswords();
				if (EventSystem.current != null && _previousSelection != null && _previousSelection.activeInHierarchy)
				{
					EventSystem.current.SetSelectedGameObject(_previousSelection);
				}
			}
		}

		private void OnDestroy()
		{
			Release();
		}

		public void Release()
		{
			if (_released)
			{
				return;
			}
			_released = true;
			_wired = false;
			_viewVersion++;
			DetachService();
			foreach (Action item in _unbind)
			{
				item();
			}
			_unbind.Clear();
			_menu = null;
		}

		public void ShowBrowser()
		{
			if (!base.IsVisible)
			{
				Show();
			}
			_viewVersion++;
			_showingRoom = false;
			Render();
			if (_service != null && !Busy && Time.unscaledTime >= _nextRefreshAt)
			{
				Run(RefreshService);
			}
			Focus("PublicFilter");
		}

		public void DismissForDeparture()
		{
			_departureDismissal = true;
			try
			{
				Hide();
			}
			finally
			{
				_departureDismissal = false;
			}
		}

		public void ShowRoom()
		{
			if (!base.IsVisible)
			{
				Show();
			}
			_viewVersion++;
			_showingRoom = true;
			Render();
			Focus("Ready");
		}

		public override bool TryHandleBack()
		{
			if (!base.IsVisible)
			{
				return false;
			}
			if (_modal != null)
			{
				CloseModal();
				return true;
			}
			Hide();
			return true;
		}

		private void Update()
		{
			if (_wired)
			{
				if (_service == null)
				{
					AttachService();
				}
				if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
				{
					MoveTab((!Keyboard.current.shiftKey.isPressed) ? 1 : (-1));
				}
				KeepSelectedRowVisible();
			}
		}

		public void RefreshServiceState()
		{
			if (!_wired || _menu == null || !_menu.CanPresentLobby)
			{
				return;
			}
			AttachService();
			LobbyRoomSnapshot lobbyRoomSnapshot = _service?.Snapshot;
			if (lobbyRoomSnapshot == null || lobbyRoomSnapshot.LobbyId == 0L || lobbyRoomSnapshot.IsOwner)
			{
				_presentedGuestLobbyId = 0uL;
			}
			else if (lobbyRoomSnapshot.IsAdmitted && lobbyRoomSnapshot.State == "waiting" && !Busy && _presentedGuestLobbyId != lobbyRoomSnapshot.LobbyId)
			{
				_presentedGuestLobbyId = lobbyRoomSnapshot.LobbyId;
				_menu.OpenLobbyRoom();
			}
			if (lobbyRoomSnapshot == null || lobbyRoomSnapshot.LobbyId == 0L || !lobbyRoomSnapshot.IsOwner)
			{
				_observedHostLobbyId = 0uL;
				_presentedHostMembers.Clear();
			}
			else if (lobbyRoomSnapshot.IsAdmitted && lobbyRoomSnapshot.State == "waiting" && !Busy)
			{
				if (_observedHostLobbyId != lobbyRoomSnapshot.LobbyId)
				{
					_observedHostLobbyId = lobbyRoomSnapshot.LobbyId;
					_presentedHostMembers.Clear();
				}
				bool flag = lobbyRoomSnapshot.Members.Count > 1 && lobbyRoomSnapshot.Members.Any((LobbyRoomMember member) => !member.IsOwner && !_presentedHostMembers.Contains(member.SteamId));
				_presentedHostMembers.Clear();
				foreach (LobbyRoomMember member in lobbyRoomSnapshot.Members)
				{
					_presentedHostMembers.Add(member.SteamId);
				}
				if (flag && !base.IsVisible)
				{
					_menu.OpenLobbyRoom();
				}
			}
			if (base.IsVisible && _renderedSnapshot != Snapshot)
			{
				OnServiceChanged();
			}
		}

		private void AttachService()
		{
			if (this == null || _released)
			{
				return;
			}
			LobbyRoomService lobbyRoomService = (Singleton<MultiplayerCoordinator>.TryGetInstance(out var instance) ? instance.RoomService : null);
			if (!(lobbyRoomService == _service))
			{
				DetachService();
				_presentedGuestLobbyId = 0uL;
				_observedHostLobbyId = 0uL;
				_presentedHostMembers.Clear();
				_service = lobbyRoomService;
				if (!(_service == null))
				{
					_service.Changed += OnServiceChanged;
					_service.Notice += OnNotice;
					_service.PasswordRequested += OnPasswordRequested;
					Render();
				}
			}
		}

		private void DetachService()
		{
			ClearDeferredPasswordRequest();
			if ((object)_service != null)
			{
				_service.Changed -= OnServiceChanged;
				_service.Notice -= OnNotice;
				_service.PasswordRequested -= OnPasswordRequested;
				_service = null;
			}
		}

		private void OnServiceChanged()
		{
			if (this == null || _released)
			{
				return;
			}
			if (_deferredPasswordLobbyId != 0L && !DeferredPasswordRequestIsCurrent())
			{
				ClearDeferredPasswordRequest();
			}
			if (!_wired || !base.IsVisible)
			{
				return;
			}
			string text = EventSystem.current?.currentSelectedGameObject?.name;
			if (_modal == "SettingsModal")
			{
				LobbyRoomSnapshot snapshot = Snapshot;
				if (snapshot == null || !snapshot.CanEdit)
				{
					CloseModal();
				}
			}
			Render();
			RestoreSelection(text);
		}

		private void OnNotice(string message)
		{
			if (!(this == null) && !_released && _wired)
			{
				Status(message, error: true);
			}
		}

		private void OnPasswordRequested(ulong lobbyId)
		{
			if (!(this == null) && !_released && lobbyId != 0L)
			{
				if (_menu != null && !_menu.isActiveAndEnabled)
				{
					_deferredPasswordLobbyId = lobbyId;
					_deferredPasswordService = _service;
					_deferredPasswordError = _service?.LastAdmissionError;
				}
				else
				{
					ClearDeferredPasswordRequest();
					ShowPasswordRequest(lobbyId);
				}
			}
		}

		public void ResumeDeferredPasswordRequest()
		{
			if (_wired && _deferredPasswordLobbyId != 0L && !(_menu == null) && _menu.isActiveAndEnabled)
			{
				AttachService();
				ulong deferredPasswordLobbyId = _deferredPasswordLobbyId;
				bool num = deferredPasswordLobbyId != 0L && SteamReady && DeferredPasswordRequestIsCurrent();
				ClearDeferredPasswordRequest();
				if (num)
				{
					ShowPasswordRequest(deferredPasswordLobbyId);
				}
			}
		}

		private bool DeferredPasswordRequestIsCurrent()
		{
			LobbyRoomSnapshot lobbyRoomSnapshot = _service?.Snapshot;
			if (_service != null && _service == _deferredPasswordService && _service.LastAdmissionError == _deferredPasswordError)
			{
				if (lobbyRoomSnapshot != null && lobbyRoomSnapshot.IsAdmitted)
				{
					if (lobbyRoomSnapshot.IsOwner)
					{
						return lobbyRoomSnapshot.LobbyId != _deferredPasswordLobbyId;
					}
					return false;
				}
				return true;
			}
			return false;
		}

		private void ClearDeferredPasswordRequest()
		{
			_deferredPasswordLobbyId = 0uL;
			_deferredPasswordService = null;
			_deferredPasswordError = null;
		}

		private void ShowPasswordRequest(ulong lobbyId)
		{
			if (_menu != null)
			{
				_menu.OpenLobbyRoom();
			}
			else
			{
				Show();
			}
			OpenJoin(lobbyId, LobbyJoinCode.Encode(lobbyId));
		}

		private void OnLocaleChanged(Locale locale)
		{
			if (this == null || _released)
			{
				return;
			}
			RefreshLabels();
			Render();
			if (_modal == "SettingsModal")
			{
				RefreshSettingsLabels();
			}
			if (_modal == "BansModal")
			{
				RenderBans();
			}
			foreach (KeyValuePair<string, Func<string>> localizedOverride in _localizedOverrides)
			{
				Text(localizedOverride.Key).text = localizedOverride.Value();
			}
		}

		private void RefreshLabels()
		{
			LobbyRoomLabel[] componentsInChildren = GetComponentsInChildren<LobbyRoomLabel>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].Refresh();
			}
			SetButton("CreateVisibility", VisibilityName(_createVisibility));
		}

		private void Render()
		{
			if (_wired)
			{
				Element("BrowserPage").gameObject.SetActive(!_showingRoom);
				Element("RoomPage").gameObject.SetActive(_showingRoom);
				SetButton("BrowserTab", T("find_crew", "FIND A CREW", "EKİP BUL"));
				SetButton("RoomTab", T("your_lobby", "YOUR LOBBY", "LOBİN"));
				Text("Connection").text = (Busy ? T("working", "Connecting to Steam...", "Steam'e bağlanılıyor...") : ((!SteamReady && _previewSnapshot == null && _previewRooms == null) ? T("unavailable", "Steam connection unavailable", "Steam bağlantısı kullanılamıyor") : T("steam_connected", "STEAM  •  Crew lobby", "STEAM  •  Ekip lobisi")));
				TintTab("BrowserTab", !_showingRoom);
				TintTab("RoomTab", _showingRoom);
				RenderBrowser();
				RenderRoom();
				UpdateInteractability();
				RefreshNavigation();
				_renderedSnapshot = Snapshot;
			}
		}

		private void SetFilter(bool friends)
		{
			_friendsFilter = friends;
			RenderBrowser();
			RefreshNavigation();
		}

		private void OnSearchChanged(string value)
		{
			RenderBrowser();
			RefreshNavigation();
		}

		private void RenderBrowser()
		{
			RectTransform content = Element("BrowserListContent") as RectTransform;
			ClearChildren(content);
			IReadOnlyList<LobbyBrowserEntry> readOnlyList = _previewRooms ?? _service?.Rooms;
			string search = Field("Search").text.Trim();
			List<LobbyBrowserEntry> list = ((readOnlyList == null) ? new List<LobbyBrowserEntry>() : (from room in readOnlyList
				where room.IsCompatible && (_friendsFilter ? room.IsFriend : (room.Visibility == LobbyVisibility.Public)) && (search.Length == 0 || (room.Name ?? string.Empty).IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0)
				orderby room.JoinLocked || room.MemberCount >= room.MaxMembers, room.IsFriend descending
				select room).ThenBy((LobbyBrowserEntry room) => room.Name, StringComparer.CurrentCultureIgnoreCase).ToList());
			foreach (LobbyBrowserEntry item in list)
			{
				AddBrowserRow(content, item);
			}
			Element("BrowserEmpty").gameObject.SetActive(list.Count == 0);
			Text("BrowserEmpty").text = (Busy ? T("searching", "Finding crews...", "Ekipler aranıyor...") : (_friendsFilter ? T("no_friends", "No joinable friend lobbies.\nAsk a friend for a code or invite,\nor create your own lobby.", "Katılabileceğin arkadaş lobisi yok.\nArkadaşından kod veya davet iste\nveya kendi lobini oluştur.") : ((search.Length > 0) ? T("no_search", "No lobbies match your search.\nTry a different lobby name.", "Aramana uyan lobi yok.\nBaşka bir lobi adı dene.") : T("empty", "No lobbies found.\nTry refreshing, check Friends,\nor create a lobby for your crew.", "Lobi bulunamadı.\nYenile, Arkadaşlar'a bak\nveya ekibin için bir lobi oluştur."))));
			Text("BrowserCount").text = string.Format(T("lobby_count", "{0} lobbies • Availability can change", "{0} lobi • Yer durumu değişebilir"), list.Count);
			TintTab("PublicFilter", !_friendsFilter);
			TintTab("FriendsFilter", _friendsFilter);
		}

		private void AddBrowserRow(RectTransform content, LobbyBrowserEntry room)
		{
			RectTransform parent = Rect("Lobby_" + room.LobbyId, content, 0f, 0f, 694f, 82f);
			Paint(parent, _skin.Row, new Color32(byte.MaxValue, 250, 235, byte.MaxValue));
			DynamicLabel("LobbyName", parent, 15f, 8f, 340f, 29f, 21f, heading: true).text = room.Name;
			DynamicLabel("LobbyDetails", parent, 15f, 44f, 425f, 25f, 14f).text = string.Join("  •  ", new string[3]
			{
				VisibilityName(room.Visibility),
				room.HasPassword ? T("protected", "Password", "Şifreli") : T("open", "No password", "Şifresiz"),
				room.IsFriend ? T("friend_badge", "Friend's crew", "Arkadaş ekibi") : string.Empty
			}.Where((string value) => !string.IsNullOrEmpty(value)));
			DynamicLabel("LobbyCapacity", parent, 439f, 23f, 76f, 34f, 21f, heading: true).text = room.MemberCount + "/" + room.MaxMembers;
			bool flag = room.JoinLocked || room.MemberCount >= room.MaxMembers || !room.IsCompatible;
			BaseButton baseButton = Button("Join_" + room.LobbyId, parent, 527f, 18f, 151f, 45f, "join", "Join", "Katıl", danger: false, primary: true);
			baseButton.interactable = !flag && !Busy;
			baseButton.SetText(room.JoinLocked ? T("locked", "Locked", "Kilitli") : ((room.MemberCount >= room.MaxMembers) ? T("full", "Full", "Dolu") : T("join", "Join", "Katıl")));
			baseButton.Clicked += () =>
			{
				if (room.HasPassword)
				{
					OpenJoin(room.LobbyId, room.JoinCode, room.Name);
				}
				else
				{
					Run(async () => await JoinRoom(room.LobbyId, string.Empty));
				}
			};
		}

		private void RenderRoom()
		{
			LobbyRoomSnapshot snapshot = Snapshot;
			bool flag = snapshot != null && snapshot.LobbyId != 0;
			Text("RoomName").text = (flag ? snapshot.Name : T("no_current_lobby", "NO ACTIVE LOBBY", "AKTİF LOBİ YOK"));
			Text("RoomCode").text = (flag ? snapshot.JoinCode : "—");
			Text("CrewCount").text = (flag ? string.Format(T("crew_count", "CREW  {0}/{1}  •  {2}", "EKİP  {0}/{1}  •  {2}"), snapshot.Members.Count, snapshot.MaxMembers, snapshot.IsOwner ? T("you_host", "You are the host", "Ev sahibi sensin") : T("guest", "Guest", "Misafir")) : T("create_first", "Find a crew or create a lobby to start.", "Başlamak için ekip bul veya lobi oluştur."));
			RectTransform content = Element("MemberListContent") as RectTransform;
			ClearChildren(content);
			if (flag)
			{
				foreach (LobbyRoomMember item in from value in snapshot.Members
					orderby value.IsOwner descending, value.SteamId
					select value)
				{
					AddMemberRow(content, item, snapshot);
				}
				for (int num = snapshot.Members.Count; num < snapshot.MaxMembers; num++)
				{
					AddEmptyMemberRow(content, num + 1, snapshot.JoinLocked);
				}
			}
			Text("RoomAccess").text = (flag ? string.Join("\n", VisibilityName(snapshot.Visibility), string.Format(T("capacity_count", "Capacity: {0} players", "Kapasite: {0} oyuncu"), snapshot.MaxMembers), snapshot.HasPassword ? T("protected_room", "Password protected", "Şifre korumalı") : T("open", "No password", "Şifresiz"), snapshot.JoinLocked ? T("joins_locked", "New joins locked", "Yeni katılımlar kilitli") : T("joins_open", "Accepting new crew", "Yeni üyelere açık")) : T("no_room_help", "Create a lobby or join another crew.", "Lobi oluştur veya başka bir ekibe katıl."));
			SetButton("Ready", (snapshot != null && snapshot.IsOwner) ? T("host_ready", "HOST READY", "EV SAHİBİ HAZIR") : ((snapshot != null && snapshot.LocalReady) ? T("unready", "NOT READY", "HAZIR DEĞİLİM") : T("ready", "READY", "HAZIRIM")));
			Text("RoomReadiness").text = ((!flag) ? string.Empty : ((!snapshot.IsAdmitted) ? T("admitting", "Waiting for host admission...", "Ev sahibinin onayı bekleniyor...") : ((snapshot.RequireReady && !snapshot.AllReady) ? T("waiting_ready", "Waiting for everyone to be ready. Settings changes reset readiness.", "Herkesin hazır olması bekleniyor. Ayar değişiklikleri hazır durumunu sıfırlar.") : (snapshot.IsOwner ? T("ready_to_sail", "Crew is ready. Choose a save and set sail together.", "Ekip hazır. Bir kayıt seç ve birlikte yola çık.") : T("waiting_host", "Ready to go. Waiting for the host to choose a save.", "Yola çıkmaya hazır. Ev sahibinin kayıt seçmesi bekleniyor.")))));
			SetButton("Banned", string.Format(T("bans_count", "Banned players ({0})", "Yasaklanan oyuncular ({0})"), snapshot?.Bans.Count ?? 0));
		}

		private void AddMemberRow(RectTransform content, LobbyRoomMember member, LobbyRoomSnapshot snapshot)
		{
			RectTransform parent = Rect("Member_" + member.SteamId, content, 0f, 0f, 694f, 62f);
			Paint(parent, _skin.Row, member.IsLocal ? new Color32(232, 243, 226, byte.MaxValue) : new Color32(byte.MaxValue, 250, 235, byte.MaxValue));
			DynamicLabel("MemberName", parent, 15f, 5f, 330f, 29f, 20f, heading: true).text = member.Name + (member.IsLocal ? T("you_suffix", " (you)", " (sen)") : string.Empty);
			DynamicLabel("MemberRole", parent, 15f, 35f, 330f, 22f, 13f).text = (member.IsOwner ? T("host", "HOST", "EV SAHİBİ") : (member.IsFriend ? T("friend", "FRIEND", "ARKADAŞ") : T("crew", "CREW", "EKİP")));
			TMP_Text tMP_Text = DynamicLabel("MemberReady", parent, 352f, 16f, 121f, 29f, 14f);
			tMP_Text.text = ((!member.IsAdmitted) ? T("pending", "Joining...", "Katılıyor...") : (member.IsReady ? T("ready_badge", "READY", "HAZIR") : T("not_ready", "Not ready", "Hazır değil")));
			tMP_Text.color = (member.IsReady ? ((Color)new Color32(45, 123, 72, byte.MaxValue)) : _skin.Muted);
			if (snapshot.CanEdit && !member.IsOwner && !member.IsLocal)
			{
				BaseButton baseButton = Button("Kick_" + member.SteamId, parent, 475f, 12f, 96f, 38f, "kick", "Kick", "Çıkar", danger: true);
				BaseButton baseButton2 = Button("Ban_" + member.SteamId, parent, 579f, 12f, 99f, 38f, "ban", "Ban", "Yasakla", danger: true);
				bool interactable = (baseButton2.interactable = !Busy);
				baseButton.interactable = interactable;
				baseButton.Clicked += () =>
				{
					ConfirmModeration(member, ban: false);
				};
				baseButton2.Clicked += () =>
				{
					ConfirmModeration(member, ban: true);
				};
			}
		}

		private void AddEmptyMemberRow(RectTransform content, int number, bool locked)
		{
			RectTransform parent = Rect("EmptySeat_" + number, content, 0f, 0f, 694f, 62f);
			Paint(parent, _skin.Row, new Color32(229, 224, 207, byte.MaxValue));
			TMP_Text tMP_Text = DynamicLabel("EmptySeatLabel", parent, 15f, 10f, 663f, 41f, 16f);
			tMP_Text.color = _skin.Muted;
			tMP_Text.text = (locked ? T("seat_locked", "Empty seat • Lobby locked", "Boş yer • Lobi kilitli") : T("empty_seat", "Empty seat • Invite a friend to join the crew", "Boş yer • Ekibe bir arkadaşını davet et"));
		}

		private async Task<LobbyRoomResult> RefreshService()
		{
			_nextRefreshAt = Time.unscaledTime + 3f;
			return await _service.RefreshAsync();
		}

		private async void Run(Func<Task<LobbyRoomResult>> operation)
		{
			if (Busy || _service == null)
			{
				return;
			}
			_working = true;
			int viewVersion = _viewVersion;
			Status(() => T("working", "Connecting to Steam...", "Steam'e bağlanılıyor..."));
			Render();
			try
			{
				LobbyRoomResult result = await operation();
				if (!IsCurrentView(viewVersion))
				{
					return;
				}
				Status(() => (!result.Success || !string.IsNullOrEmpty(result.Message)) ? LocalizeResult(result.Message) : T("updated", "Lobby information updated.", "Lobi bilgileri güncellendi."), !result.Success);
				if (!result.Success && _modal == "JoinModal")
				{
					SetLocalizedText("JoinFeedback", () => LocalizeResult(result.Message));
				}
			}
			catch (Exception ex)
			{
				if (IsCurrentView(viewVersion))
				{
					Status(() => T("operation_error", "Steam could not complete the request. Please try again.", "Steam isteği tamamlayamadı. Tekrar dene."), error: true);
				}
				Debug.LogWarning("[LobbyRoomScreen] Lobby operation failed: " + ex.GetType().Name);
			}
			finally
			{
				if (this != null)
				{
					_working = false;
					if (base.IsVisible)
					{
						Render();
					}
				}
			}
		}

		private void CreateLobby()
		{
			string name = Field("CreateName").text.Trim();
			if (string.IsNullOrWhiteSpace(name))
			{
				Status(() => T("name_required", "Give your lobby a name first.", "Önce lobine bir ad ver."), error: true);
				Focus("CreateName");
				return;
			}
			Action action = () =>
			{
				Run(async () =>
				{
					int viewVersion = _viewVersion;
					LobbyRoomResult result = await _service.CreateAsync(new LobbyRoomSettings
					{
						Name = name,
						Visibility = _createVisibility,
						MaxMembers = 5,
						RequireReady = true
					});
					if (result.Success && IsCurrentView(viewVersion))
					{
						ShowRoom();
						Status(result.Message);
					}
					return result;
				});
			};
			LobbyRoomSnapshot snapshot = Snapshot;
			if (snapshot != null && snapshot.Members.Count > 1 && !Snapshot.IsOwner)
			{
				Confirm(() => T("switch_lobby", "LEAVE YOUR CURRENT CREW?", "MEVCUT EKİBİNDEN AYRIL?"), () => T("switch_create", "Creating a lobby leaves your current crew and returns you to your own lobby.", "Lobi oluşturmak mevcut ekibinden ayrılmana ve kendi lobine dönmene neden olur."), action);
			}
			else
			{
				action();
			}
		}

		private void CycleCreateVisibility()
		{
			_createVisibility = NextVisibility(_createVisibility);
			SetButton("CreateVisibility", VisibilityName(_createVisibility));
		}

		private void OpenJoin(ulong lobbyId, string code = null, string lobbyName = null)
		{
			_joinLobbyId = lobbyId;
			Field("JoinCodeInput").SetTextWithoutNotify(code ?? string.Empty);
			Field("JoinCodeInput").interactable = lobbyId == 0;
			Field("JoinPassword").SetTextWithoutNotify(string.Empty);
			SetLocalizedText("JoinHelp", () => (!string.IsNullOrEmpty(lobbyName)) ? string.Format(T("join_named", "Join {0}", "{0} lobisine katıl"), lobbyName) : T("join_help", "Enter the lobby code your host shared with you.", "Ev sahibinin seninle paylaştığı lobi kodunu gir."));
			SetLocalizedText("JoinFeedback", () => T("password_privacy", "Your password is never shown in the lobby list.", "Şifren lobi listesinde gösterilmez."));
			OpenModal("JoinModal", (lobbyId == 0L) ? "JoinCodeInput" : "JoinPassword");
		}

		private void JoinLobby()
		{
			string code = Field("JoinCodeInput").text.Trim();
			string password = Field("JoinPassword").text;
			if (_joinLobbyId == 0L && code.Length == 0)
			{
				SetLocalizedText("JoinFeedback", () => T("code_required", "Enter a lobby join code.", "Lobi katılım kodunu gir."));
				Focus("JoinCodeInput");
			}
			else
			{
				Run(async () => (_joinLobbyId == 0L) ? (await JoinCode(code, password)) : (await JoinRoom(_joinLobbyId, password)));
			}
		}

		private async Task<LobbyRoomResult> JoinRoom(ulong id, string password)
		{
			int viewVersion = _viewVersion;
			LobbyRoomResult result = await _service.JoinAsync(id, password);
			if (result.Success && IsCurrentView(viewVersion))
			{
				CloseModal();
				ShowRoom();
				Status(result.Message);
			}
			return result;
		}

		private async Task<LobbyRoomResult> JoinCode(string code, string password)
		{
			int viewVersion = _viewVersion;
			LobbyRoomResult result = await _service.JoinByCodeAsync(code, password);
			if (result.Success && IsCurrentView(viewVersion))
			{
				CloseModal();
				ShowRoom();
				Status(result.Message);
			}
			return result;
		}

		private void ToggleReady()
		{
			if (_service != null && Snapshot != null && !Busy)
			{
				Present(_service.SetReady(!Snapshot.LocalReady));
			}
		}

		private void ChooseSave()
		{
			LobbyRoomSnapshot snapshot = Snapshot;
			if (Busy || snapshot == null || !snapshot.IsOwner || !snapshot.IsAdmitted)
			{
				return;
			}
			if (snapshot.RequireReady && !snapshot.AllReady)
			{
				Status(() => T("wait_ready", "Everyone must be ready before choosing a save.", "Kayıt seçmeden önce herkes hazır olmalı."), error: true);
			}
			else
			{
				_menu?.OpenLobbySaveSelection();
			}
		}

		private void ConfirmLeave()
		{
			Confirm(() => T("leave_title", "LEAVE THIS CREW?", "BU EKİPTEN AYRIL?"), () =>
			{
				LobbyRoomSnapshot snapshot = Snapshot;
				return (snapshot == null || !snapshot.IsOwner) ? T("leave_body", "You will return to your own lobby. You can rejoin this crew with an invite or code.", "Kendi lobine döneceksin. Bu ekibe davet veya kod ile yeniden katılabilirsin.") : T("leave_host_body", "You will leave this lobby. Your crew will return to their own lobbies. Your saves are kept.", "Bu lobiden ayrılacaksın. Ekibin kendi lobilerine dönecek. Kayıtların korunur.");
			}, () =>
			{
				_service?.Leave();
				_showingRoom = false;
				Render();
			});
		}

		private void CopyCode()
		{
			_service?.CopyJoinCode();
			Status(() => T("code_copied", "Join code copied. Share it with your crew.", "Katılım kodu kopyalandı. Ekibinle paylaş."));
		}

		private void InviteFriends()
		{
			_service?.OpenInviteOverlay();
			Status(() => T("invite_opened", "Use the Steam overlay to invite friends. You can also share the join code.", "Steam arayüzünden arkadaşlarını davet et. Katılım kodunu da paylaşabilirsin."));
		}

		private void OpenSettings()
		{
			LobbyRoomSnapshot snapshot = Snapshot;
			if (snapshot != null && snapshot.CanEdit)
			{
				Field("SettingsName").SetTextWithoutNotify(snapshot.Name);
				Field("SettingsPassword").SetTextWithoutNotify(string.Empty);
				Field("SettingsPassword").contentType = TMP_InputField.ContentType.Password;
				_editVisibility = snapshot.Visibility;
				_editCapacity = snapshot.MaxMembers;
				_editLocked = snapshot.JoinLocked;
				_editReady = snapshot.RequireReady;
				_clearPassword = false;
				_localizedOverrides.Remove("SettingsFeedback");
				Text("SettingsFeedback").text = string.Empty;
				RefreshSettingsLabels();
				OpenModal("SettingsModal", "SettingsName");
			}
		}

		private void RefreshSettingsLabels()
		{
			SetButton("SettingsVisibility", VisibilityName(_editVisibility));
			SetButton("SettingsCapacity", string.Format(T("players_count", "{0} players", "{0} oyuncu"), _editCapacity));
			SetButton("SettingsLocked", string.Format(T("lock_option", "Lock new joins: {0}", "Yeni katılımı kilitle: {0}"), OnOff(_editLocked)));
			SetButton("SettingsReady", string.Format(T("ready_option", "Require everyone ready: {0}", "Herkes hazır olmalı: {0}"), OnOff(_editReady)));
			SetButton("RemovePassword", _clearPassword ? T("remove_yes", "Remove password: Yes", "Şifreyi kaldır: Evet") : T("remove_no", "Remove password: No", "Şifreyi kaldır: Hayır"));
			Field("SettingsPassword").interactable = !_clearPassword;
			SetButton("ShowPassword", (Field("SettingsPassword").contentType == TMP_InputField.ContentType.Password) ? T("show_password", "Show password", "Şifreyi göster") : T("hide_password", "Hide password", "Şifreyi gizle"));
		}

		private void CycleCapacity()
		{
			int num = 5;
			int num2 = Mathf.Clamp(Snapshot?.Members.Count ?? 1, 1, num);
			_editCapacity = ((_editCapacity >= num) ? num2 : (_editCapacity + 1));
			RefreshSettingsLabels();
		}

		private void TogglePasswordVisibility()
		{
			TMP_InputField tMP_InputField = Field("SettingsPassword");
			tMP_InputField.contentType = ((tMP_InputField.contentType != TMP_InputField.ContentType.Password) ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard);
			tMP_InputField.ForceLabelUpdate();
			RefreshSettingsLabels();
		}

		private void SaveSettings()
		{
			if (_service == null || Busy)
			{
				return;
			}
			LobbyRoomResult result = _service.ApplySettings(new LobbyRoomSettings
			{
				Name = Field("SettingsName").text.Trim(),
				Password = (_clearPassword ? string.Empty : Field("SettingsPassword").text),
				ClearPassword = _clearPassword,
				Visibility = _editVisibility,
				MaxMembers = _editCapacity,
				JoinLocked = _editLocked,
				RequireReady = _editReady
			});
			if (result.Success)
			{
				CloseModal();
			}
			else
			{
				SetLocalizedText("SettingsFeedback", () => LocalizeResult(result.Message));
			}
			Present(result);
		}

		private void ConfirmModeration(LobbyRoomMember member, bool ban)
		{
			Confirm(() => (!ban) ? T("kick_title", "KICK PLAYER?", "OYUNCUYU ÇIKAR?") : T("ban_title", "BAN PLAYER?", "OYUNCUYU YASAKLA?"), () => string.Format(ban ? T("ban_body", "{0} will be removed and banned from your lobbies until you unban their Steam account. The ban stays after this lobby closes.", "{0} çıkarılacak ve Steam hesabının yasağını kaldırana kadar lobilerine katılamayacak. Yasak, bu lobi kapandıktan sonra da sürer.") : T("kick_body", "{0} will be removed from this lobby. They can join again after 30 seconds with access or an invite.", "{0} bu lobiden çıkarılacak. Erişimi veya daveti varsa 30 saniye sonra yeniden katılabilir."), member.Name), () =>
			{
				if (_service != null)
				{
					Present(ban ? _service.Ban(member.SteamId) : _service.Kick(member.SteamId));
				}
			});
		}

		private void OpenBans()
		{
			RenderBans();
			OpenModal("BansModal", "BansClose");
		}

		private void RenderBans()
		{
			RectTransform rectTransform = Element("BansListContent") as RectTransform;
			ClearChildren(rectTransform);
			List<LobbyBannedMember> list = Snapshot?.Bans;
			Element("BansEmpty").gameObject.SetActive(list == null || list.Count == 0);
			if (list == null)
			{
				return;
			}
			foreach (LobbyBannedMember banned in list)
			{
				RectTransform parent = Rect("Banned_" + banned.SteamId, rectTransform, 0f, 0f, 574f, 70f);
				Paint(parent, _skin.Row, new Color32(byte.MaxValue, 250, 235, byte.MaxValue));
				DynamicLabel("BannedName", parent, 14f, 6f, 367f, 31f, 20f, heading: true).text = banned.Name;
				DynamicLabel("BannedId", parent, 14f, 37f, 367f, 22f, 12f).text = banned.SteamId.ToString();
				BaseButton baseButton = Button("Unban_" + banned.SteamId, parent, 392f, 13f, 162f, 43f, "unban", "Unban", "Yasağı kaldır");
				LobbyRoomSnapshot snapshot = Snapshot;
				baseButton.interactable = snapshot != null && snapshot.CanEdit && !Busy;
				baseButton.Clicked += () =>
				{
					Confirm(() => T("unban_title", "UNBAN PLAYER?", "YASAĞI KALDIR?"), () => string.Format(T("unban_body", "{0} will be allowed to join this lobby again.", "{0} bu lobiye tekrar katılabilecek."), banned.Name), () =>
					{
						if (_service != null)
						{
							Present(_service.Unban(banned.SteamId));
						}
						OpenBans();
					});
				};
			}
		}

		private void Confirm(Func<string> title, Func<string> body, Action action)
		{
			_confirm = action;
			SetLocalizedText("ConfirmTitle", title);
			SetLocalizedText("ConfirmBody", body);
			OpenModal("ConfirmModal", "ConfirmCancel");
		}

		private void OpenModal(string id, string focus)
		{
			_viewVersion++;
			string[] array = new string[4] { "JoinModal", "SettingsModal", "ConfirmModal", "BansModal" };
			foreach (string text in array)
			{
				Element(text).gameObject.SetActive(text == id);
			}
			_modal = id;
			Element("ModalRoot").gameObject.SetActive(value: true);
			Element("Board").GetComponent<CanvasGroup>().interactable = false;
			UpdateInteractability();
			RefreshNavigation();
			Focus(focus);
		}

		private void CloseModal()
		{
			if (_wired)
			{
				_viewVersion++;
				if (_modal == "JoinModal" && !_departureDismissal)
				{
					_service?.CancelPendingJoin();
				}
				_modal = null;
				_confirm = null;
				string[] array = new string[5] { "ConfirmTitle", "ConfirmBody", "JoinHelp", "JoinFeedback", "SettingsFeedback" };
				foreach (string key in array)
				{
					_localizedOverrides.Remove(key);
				}
				Element("ModalRoot").gameObject.SetActive(value: false);
				Element("Board").GetComponent<CanvasGroup>().interactable = true;
				ClearPasswords();
				RefreshNavigation();
				Focus(_showingRoom ? "RoomTab" : "BrowserTab");
			}
		}

		private void ClearPasswords()
		{
			Field("JoinPassword").SetTextWithoutNotify(string.Empty);
			Field("SettingsPassword").SetTextWithoutNotify(string.Empty);
			Field("SettingsPassword").contentType = TMP_InputField.ContentType.Password;
		}

		private void Present(LobbyRoomResult result)
		{
			Status(result.Message, !result.Success);
			Render();
		}

		private void Status(string message, bool error = false)
		{
			if (!string.IsNullOrEmpty(message) && _wired)
			{
				Status(() => LocalizeResult(message), error);
			}
		}

		private void Status(Func<string> message, bool error = false)
		{
			if (_wired)
			{
				SetLocalizedText("Status", message);
				Text("Status").color = (error ? _skin.Danger : _skin.Ink);
			}
		}

		private void SetLocalizedText(string id, Func<string> resolve)
		{
			_localizedOverrides[id] = resolve;
			Text(id).text = resolve();
		}

		private void UpdateInteractability()
		{
			bool flag = SteamReady || _previewSnapshot != null || _previewRooms != null;
			LobbyRoomSnapshot snapshot = Snapshot;
			bool flag2 = snapshot != null && snapshot.LobbyId != 0;
			bool flag3 = flag2 && snapshot.IsAdmitted;
			bool flag4 = flag3 && snapshot.CanEdit;
			string[] array = new string[4] { "Refresh", "CreateLobby", "JoinCode", "JoinSubmit" };
			foreach (string id in array)
			{
				Control(id).interactable = flag && !Busy;
			}
			Control("RoomTab").interactable = flag2;
			Control("Ready").interactable = flag3 && !snapshot.IsOwner && !Busy && snapshot.State == "waiting";
			Control("Start").interactable = flag4 && !Busy && (!snapshot.RequireReady || snapshot.AllReady);
			Control("Leave").interactable = flag2 && !Busy;
			Control("CopyCode").interactable = flag3 && !Busy && !string.IsNullOrEmpty(snapshot.JoinCode);
			Control("Invite").interactable = flag3 && !Busy && !snapshot.JoinLocked && snapshot.Members.Count < snapshot.MaxMembers;
			Control("Settings").interactable = flag4 && !Busy;
			Control("Banned").interactable = flag4 && !Busy;
			Control("SettingsSave").interactable = flag4 && !Busy;
			Control("ConfirmSubmit").interactable = flag && !Busy;
		}

		private Transform Element(string id)
		{
			return _elements[id];
		}

		private bool IsCurrentView(int version)
		{
			if (this != null && !_released && base.IsVisible)
			{
				return _viewVersion == version;
			}
			return false;
		}

		private TMP_Text Text(string id)
		{
			return Element(id).GetComponent<TMP_Text>();
		}

		private TMP_InputField Field(string id)
		{
			return Element(id).GetComponent<TMP_InputField>();
		}

		private Selectable Control(string id)
		{
			return Element(id).GetComponent<Selectable>();
		}

		private void SetButton(string id, string value)
		{
			Element(id).GetComponent<BaseButton>().SetText(value);
		}

		private void Click(string id, Action callback)
		{
			BaseButton button = Element(id).GetComponent<BaseButton>();
			button.Clicked += callback;
			_unbind.Add(() =>
			{
				if (button != null)
				{
					button.Clicked -= callback;
				}
			});
		}

		private void TintTab(string id, bool selected)
		{
			Image component = Element(id).GetComponent<Image>();
			component.sprite = (selected ? _skin.Button : _skin.SecondaryButton);
			component.color = Color.white;
			Element(id).GetComponentInChildren<TMP_Text>().color = (selected ? Color.white : _skin.Ink);
		}

		private static LobbyVisibility NextVisibility(LobbyVisibility visibility)
		{
			return visibility switch
			{
				LobbyVisibility.Public => LobbyVisibility.FriendsOnly, 
				LobbyVisibility.FriendsOnly => LobbyVisibility.Private, 
				_ => LobbyVisibility.Public, 
			};
		}

		private static string VisibilityName(LobbyVisibility visibility)
		{
			return visibility switch
			{
				LobbyVisibility.Public => T("public", "Public", "Herkese açık"), 
				LobbyVisibility.FriendsOnly => T("friends_only", "Friends only", "Yalnızca arkadaşlar"), 
				_ => T("private", "Private / invite only", "Özel / davet ile"), 
			};
		}

		private static string OnOff(bool value)
		{
			if (!value)
			{
				return T("off", "Off", "Kapalı");
			}
			return T("on", "On", "Açık");
		}

		private static void ClearChildren(Transform content)
		{
			for (int num = content.childCount - 1; num >= 0; num--)
			{
				GameObject gameObject = content.GetChild(num).gameObject;
				gameObject.SetActive(value: false);
				if (Application.isPlaying)
				{
					UnityEngine.Object.Destroy(gameObject);
				}
				else
				{
					UnityEngine.Object.DestroyImmediate(gameObject);
				}
			}
		}

		private Selectable[] ActiveControls()
		{
			return (from control in Element(_modal ?? "Board").GetComponentsInChildren<Selectable>(includeInactive: false)
				where control.IsActive() && control.IsInteractable()
				select control).ToArray();
		}

		private void RefreshNavigation()
		{
			if (_wired)
			{
				Canvas.ForceUpdateCanvases();
				Selectable[] array = ActiveControls();
				Selectable[] array2 = array;
				foreach (Selectable selectable in array2)
				{
					Navigation navigation = selectable.navigation;
					navigation.mode = Navigation.Mode.Explicit;
					navigation.selectOnUp = Nearest(selectable, array, Vector2.up);
					navigation.selectOnDown = Nearest(selectable, array, Vector2.down);
					navigation.selectOnLeft = Nearest(selectable, array, Vector2.left);
					navigation.selectOnRight = Nearest(selectable, array, Vector2.right);
					selectable.navigation = navigation;
				}
			}
		}

		private static Selectable Nearest(Selectable origin, IEnumerable<Selectable> controls, Vector2 direction)
		{
			Vector2 vector = ((RectTransform)origin.transform).TransformPoint(((RectTransform)origin.transform).rect.center);
			Selectable result = origin;
			float num = float.PositiveInfinity;
			foreach (Selectable control in controls)
			{
				if (control == origin)
				{
					continue;
				}
				RectTransform obj = control.transform as RectTransform;
				Vector2 lhs = (Vector2)obj.TransformPoint(obj.rect.center) - vector;
				float num2 = Vector2.Dot(lhs, direction);
				if (!(num2 <= 1f))
				{
					float num3 = Mathf.Abs(Vector2.Dot(lhs, new Vector2(0f - direction.y, direction.x)));
					float num4 = num2 + num3 * 2.5f;
					if (num4 < num)
					{
						num = num4;
						result = control;
					}
				}
			}
			return result;
		}

		private void Focus(string id)
		{
			if (!(EventSystem.current == null) && _elements.TryGetValue(id, out var value))
			{
				Selectable component = value.GetComponent<Selectable>();
				if (component != null && component.IsActive() && component.IsInteractable())
				{
					EventSystem.current.SetSelectedGameObject(value.gameObject);
				}
				else
				{
					EventSystem.current.SetSelectedGameObject(ActiveControls().FirstOrDefault()?.gameObject);
				}
			}
		}

		private void RestoreSelection(string name)
		{
			if (!(EventSystem.current == null))
			{
				Selectable selectable = ActiveControls().FirstOrDefault((Selectable control) => control.name == name);
				if (selectable != null)
				{
					EventSystem.current.SetSelectedGameObject(selectable.gameObject);
				}
				else if (EventSystem.current.currentSelectedGameObject == null || !EventSystem.current.currentSelectedGameObject.activeInHierarchy)
				{
					Focus((_modal != null) ? "ConfirmCancel" : (_showingRoom ? "RoomTab" : "BrowserTab"));
				}
			}
		}

		private void MoveTab(int direction)
		{
			if (EventSystem.current == null)
			{
				return;
			}
			Selectable[] array = ActiveControls();
			if (array.Length != 0)
			{
				int num = (Array.FindIndex(array, (Selectable control) => control.gameObject == EventSystem.current.currentSelectedGameObject) + direction + array.Length) % array.Length;
				EventSystem.current.SetSelectedGameObject(array[num].gameObject);
			}
		}

		private static void KeepSelectedRowVisible()
		{
			GameObject gameObject = EventSystem.current?.currentSelectedGameObject;
			if (gameObject == null)
			{
				return;
			}
			ScrollRect componentInParent = gameObject.GetComponentInParent<ScrollRect>();
			if (!(componentInParent == null) && !(componentInParent.content == null) && !(componentInParent.viewport == null))
			{
				RectTransform child = gameObject.transform as RectTransform;
				Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(componentInParent.viewport, child);
				float yMax = componentInParent.viewport.rect.yMax;
				float yMin = componentInParent.viewport.rect.yMin;
				float num = ((bounds.max.y > yMax) ? (yMax - bounds.max.y) : ((bounds.min.y < yMin) ? (yMin - bounds.min.y) : 0f));
				if (Mathf.Abs(num) > 0.5f)
				{
					componentInParent.StopMovement();
					componentInParent.content.anchoredPosition += new Vector2(0f, num);
				}
			}
		}

		public void Build(LobbyRoomSkin skin)
		{
			_skin = skin;
			if (!(base.transform.Find("Board") != null))
			{
				RectTransform obj = (RectTransform)base.transform;
				obj.localScale = Vector3.one;
				obj.localRotation = Quaternion.identity;
				Vector2 vector = (obj.pivot = new Vector2(0.5f, 0.5f));
				Vector2 anchorMin = (obj.anchorMax = vector);
				obj.anchorMin = anchorMin;
				obj.anchoredPosition3D = Vector3.zero;
				obj.sizeDelta = new Vector2(1600f, 1000f);
				Canvas canvas = base.gameObject.GetComponent<Canvas>();
				if (canvas == null)
				{
					canvas = base.gameObject.AddComponent<Canvas>();
				}
				canvas.renderMode = RenderMode.ScreenSpaceOverlay;
				canvas.sortingOrder = 100;
				CanvasScaler canvasScaler = base.gameObject.GetComponent<CanvasScaler>();
				if (canvasScaler == null)
				{
					canvasScaler = base.gameObject.AddComponent<CanvasScaler>();
				}
				canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
				canvasScaler.referenceResolution = new Vector2(1600f, 1000f);
				canvasScaler.matchWidthOrHeight = 1f;
				if (base.gameObject.GetComponent<GraphicRaycaster>() == null)
				{
					base.gameObject.AddComponent<GraphicRaycaster>();
				}
				RectTransform rectTransform = Rect("Shade", base.transform, 0f, 0f, 0f, 0f);
				Stretch(rectTransform);
				Paint(rectTransform, null, new Color(0.035f, 0.09f, 0.12f, 0.65f));
				RectTransform rectTransform2 = Rect("Board", base.transform, 0f, 0f, 1240f, 814f);
				vector = (rectTransform2.pivot = new Vector2(0.5f, 0.5f));
				anchorMin = (rectTransform2.anchorMax = vector);
				rectTransform2.anchorMin = anchorMin;
				rectTransform2.anchoredPosition = Vector2.zero;
				Paint(rectTransform2, _skin.Window, Color.white);
				AddShadow(rectTransform2, 8f);
				rectTransform2.gameObject.AddComponent<CanvasGroup>();
				Label("Title", rectTransform2, 116f, 27f, 970f, 52f, 42f, "title", "CREW LOBBY", "EKİP LOBİSİ", heading: true);
				Label("Subtitle", rectTransform2, 44f, 81f, 1040f, 28f, 17f, "subtitle", "A good shift starts with a great crew.", "Güzel bir vardiya, iyi bir ekiple başlar.").color = _skin.Muted;
				CloseButton(rectTransform2);
				Button("BrowserTab", rectTransform2, 40f, 119f, 240f, 54f, "find_crew", "FIND A CREW", "EKİP BUL");
				Button("RoomTab", rectTransform2, 294f, 119f, 240f, 54f, "your_lobby", "YOUR LOBBY", "LOBİN");
				Label("Connection", rectTransform2, 734f, 132f, 460f, 28f, 16f, "connection", "Steam lobby", "Steam lobisi").alignment = TextAlignmentOptions.Right;
				Divider("HeaderRule", rectTransform2, 44f, 182f, 1152f);
				RectTransform page = Rect("BrowserPage", rectTransform2, 40f, 191f, 1160f, 503f);
				BuildBrowser(page);
				RectTransform rectTransform3 = Rect("RoomPage", rectTransform2, 40f, 191f, 1160f, 503f);
				BuildRoom(rectTransform3);
				rectTransform3.gameObject.SetActive(value: false);
				Divider("FooterRule", rectTransform2, 44f, 705f, 1152f);
				Label("Status", rectTransform2, 45f, 718f, 1064f, 40f, 17f, "initial_status", "Choose a lobby, join with a code, or gather your own crew.", "Bir lobi seç, kodla katıl veya kendi ekibini topla.");
				RectTransform rectTransform4 = Rect("ModalRoot", base.transform, 0f, 0f, 0f, 0f);
				Stretch(rectTransform4);
				Paint(rectTransform4, null, new Color(0.02f, 0.06f, 0.08f, 0.65f));
				BuildJoinModal(rectTransform4);
				BuildSettingsModal(rectTransform4);
				BuildConfirmModal(rectTransform4);
				BuildBansModal(rectTransform4);
				rectTransform4.gameObject.SetActive(value: false);
			}
		}

		private void BuildBrowser(RectTransform page)
		{
			Button("PublicFilter", page, 0f, 0f, 138f, 44f, "public", "Public", "Herkese açık");
			Button("FriendsFilter", page, 148f, 0f, 138f, 44f, "friends", "Friends", "Arkadaşlar");
			Input("Search", page, 298f, 0f, 272f, 44f, "search", "Search lobby name...", "Lobi adı ara...", 48);
			Button("Refresh", page, 580f, 0f, 146f, 44f, "refresh", "Refresh", "Yenile");
			Label("ListCaption", page, 10f, 63f, 700f, 26f, 15f, "list_caption", "LOBBY  /  CREW  /  ACCESS", "LOBİ  /  EKİP  /  ERİŞİM");
			Scroll("BrowserList", page, 0f, 101f, 728f, 346f);
			Label("BrowserEmpty", page, 44f, 154f, 640f, 220f, 22f, "empty", "No lobbies found.\nTry refreshing, check Friends,\nor create a lobby for your crew.", "Lobi bulunamadı.\nYenile, Arkadaşlar'a bak\nveya ekibin için bir lobi oluştur.").alignment = TextAlignmentOptions.Center;
			Label("BrowserCount", page, 10f, 463f, 704f, 26f, 15f, "list_hint", "Only compatible lobbies appear. Availability can change.", "Yalnızca uyumlu lobiler gösterilir. Yer durumu değişebilir.");
			RectTransform rectTransform = Rect("CreateCard", page, 754f, 0f, 406f, 503f);
			Paint(rectTransform, _skin.Panel, Color.white);
			CardHeader("CreateHeader", rectTransform, 406f, 66f);
			Label("CreateTitle", rectTransform, 24f, 20f, 358f, 38f, 28f, "make_room", "GATHER YOUR CREW", "EKİBİNİ TOPLA", heading: true);
			rectTransform.Find("CreateTitle").GetComponent<TMP_Text>().color = Color.white;
			Label("CreateHelp", rectTransform, 24f, 82f, 352f, 52f, 16f, "create_help", "Name your lobby and set who can join. You can change its settings later.", "Lobini adlandır, kimlerin katılacağını seç. Ayarları sonra değiştirebilirsin.");
			Label("CreateNameLabel", rectTransform, 24f, 144f, 350f, 24f, 16f, "lobby_name", "Lobby name", "Lobi adı");
			Input("CreateName", rectTransform, 24f, 177f, 358f, 47f, "name_placeholder", "My sushi crew", "Suşi ekibim", 32);
			Label("CreateVisibilityLabel", rectTransform, 24f, 239f, 350f, 24f, 16f, "visibility", "Who can join?", "Kimler katılabilir?");
			Button("CreateVisibility", rectTransform, 24f, 273f, 358f, 45f, "friends_only", "Friends only", "Yalnızca arkadaşlar");
			Label("CreateDefaults", rectTransform, 24f, 333f, 352f, 57f, 16f, "create_defaults", $"Up to {5} players. Password and ready checks are in Lobby settings.", $"En fazla {5} oyuncu. Şifre ve hazır kontrolü Lobi ayarlarında.");
			Button("CreateLobby", rectTransform, 24f, 424f, 358f, 55f, "create_lobby", "CREATE LOBBY", "LOBİ OLUŞTUR", danger: false, primary: true);
			Button("JoinCode", page, 0f, 452f, 272f, 51f, "join_code", "JOIN WITH CODE", "KODLA KATIL");
			page.Find("BrowserCount").GetComponent<RectTransform>().anchoredPosition = new Vector2(294f, -464f);
			page.Find("BrowserCount").GetComponent<RectTransform>().sizeDelta = new Vector2(432f, 35f);
		}

		private void BuildRoom(RectTransform page)
		{
			Label("RoomName", page, 0f, 0f, 740f, 47f, 32f, "your_crew", "YOUR CREW", "EKİBİN", heading: true);
			Label("CrewCount", page, 0f, 54f, 720f, 28f, 16f, "crew_hint", "Get ready before the host chooses a save.", "Ev sahibi kayıt seçmeden önce hazır ol.");
			Scroll("MemberList", page, 0f, 101f, 728f, 289f);
			Label("RoomReadiness", page, 0f, 404f, 728f, 40f, 16f, "ready_hint", "The host chooses the save. Everyone travels together.", "Kaydı ev sahibi seçer. Herkes birlikte yola çıkar.");
			Button("Ready", page, 0f, 450f, 220f, 53f, "ready", "READY", "HAZIRIM", danger: false, primary: true);
			Button("Start", page, 236f, 450f, 280f, 53f, "choose_save", "START GAME", "OYUNU BAŞLAT", danger: false, primary: true);
			Button("Leave", page, 534f, 450f, 194f, 53f, "leave", "Leave lobby", "Lobiden ayrıl", danger: true);
			RectTransform parent = Rect("RoomCard", page, 754f, 0f, 406f, 503f);
			Paint(parent, _skin.Panel, Color.white);
			CardHeader("CodeHeader", parent, 406f, 112f);
			Label("CodeTitle", parent, 24f, 24f, 358f, 25f, 16f, "join_code_label", "LOBBY JOIN CODE", "LOBİ KATILIM KODU").color = Color.white;
			Label("RoomCode", parent, 24f, 63f, 358f, 42f, 30f, "no_code", "—", "—", heading: true).color = Color.white;
			Button("CopyCode", parent, 24f, 126f, 171f, 44f, "copy", "Copy code", "Kodu kopyala");
			Button("Invite", parent, 207f, 126f, 175f, 44f, "invite", "Invite friends", "Arkadaş davet et");
			Label("RoomAccess", parent, 24f, 193f, 358f, 120f, 19f, "room_access", "Create or join a lobby to see its settings.", "Ayarlarını görmek için lobi oluştur veya katıl.");
			Button("Settings", parent, 24f, 332f, 358f, 49f, "settings", "LOBBY SETTINGS", "LOBİ AYARLARI");
			Button("Banned", parent, 24f, 394f, 358f, 44f, "banned", "Banned players", "Yasaklanan oyuncular");
			Label("RoomFootnote", parent, 24f, 452f, 358f, 31f, 14f, "room_footnote", "Host bans stay until the host removes them.", "Yasaklar ev sahibi kaldırana kadar sürer.");
		}

		private void BuildJoinModal(RectTransform parent)
		{
			RectTransform parent2 = Modal("JoinModal", parent, 620f, 496f);
			Label("JoinTitle", parent2, 35f, 28f, 550f, 50f, 31f, "join_title", "JOIN A CREW", "BİR EKİBE KATIL", heading: true);
			Label("JoinHelp", parent2, 35f, 88f, 550f, 45f, 17f, "join_help", "Enter the lobby code your host shared with you.", "Ev sahibinin seninle paylaştığı lobi kodunu gir.");
			Label("JoinCodeLabel", parent2, 35f, 150f, 550f, 25f, 16f, "code", "Join code", "Katılım kodu");
			Input("JoinCodeInput", parent2, 35f, 185f, 550f, 51f, "code_placeholder", "Paste lobby code", "Lobi kodunu yapıştır", 96);
			Label("JoinPasswordLabel", parent2, 35f, 259f, 550f, 25f, 16f, "password_optional", "Password (if required)", "Şifre (gerekiyorsa)");
			Input("JoinPassword", parent2, 35f, 295f, 550f, 51f, "password_placeholder", "Enter password", "Şifreyi gir", 32, password: true);
			Label("JoinFeedback", parent2, 35f, 358f, 550f, 44f, 15f, "password_privacy", "Your password is never shown in the lobby list.", "Şifren lobi listesinde gösterilmez.");
			Button("JoinCancel", parent2, 35f, 420f, 210f, 50f, "cancel", "Cancel", "Vazgeç");
			Button("JoinSubmit", parent2, 260f, 420f, 325f, 50f, "join_submit", "JOIN LOBBY", "LOBİYE KATIL", danger: false, primary: true);
		}

		private void BuildSettingsModal(RectTransform parent)
		{
			RectTransform parent2 = Modal("SettingsModal", parent, 700f, 796f);
			Label("SettingsTitle", parent2, 35f, 25f, 630f, 46f, 31f, "settings", "LOBBY SETTINGS", "LOBİ AYARLARI", heading: true);
			Label("SettingsNameLabel", parent2, 35f, 96f, 625f, 23f, 16f, "lobby_name", "Lobby name", "Lobi adı");
			Input("SettingsName", parent2, 35f, 129f, 630f, 47f, "name_placeholder", "My sushi crew", "Suşi ekibim", 32);
			Label("VisibilityLabel", parent2, 35f, 193f, 300f, 24f, 16f, "visibility", "Who can join?", "Kimler katılabilir?");
			Label("CapacityLabel", parent2, 368f, 193f, 285f, 24f, 16f, "capacity", "Player capacity", "Oyuncu kapasitesi");
			Button("SettingsVisibility", parent2, 35f, 227f, 309f, 47f, "public", "Public", "Herkese açık");
			Button("SettingsCapacity", parent2, 368f, 227f, 297f, 47f, "capacity_default", $"{5} players", $"{5} oyuncu");
			Label("VisibilityHelp", parent2, 35f, 286f, 630f, 47f, 15f, "visibility_help", "Public: discoverable. Friends: host's friends. Private: invitation or code.", "Açık: listede görünür. Arkadaşlar: ev sahibinin arkadaşları. Özel: davet veya kod.");
			Label("SettingsPasswordLabel", parent2, 35f, 348f, 630f, 24f, 16f, "password", "Lobby password", "Lobi şifresi");
			Input("SettingsPassword", parent2, 35f, 383f, 630f, 47f, "keep_password", "4–32 characters; blank keeps the current password", "4–32 karakter; boş bırakırsan mevcut şifre korunur", 32, password: true);
			Button("RemovePassword", parent2, 35f, 443f, 309f, 43f, "remove_password", "Remove password: No", "Şifreyi kaldır: Hayır");
			Button("ShowPassword", parent2, 368f, 443f, 297f, 43f, "show_password", "Show password", "Şifreyi göster");
			Button("SettingsLocked", parent2, 35f, 508f, 630f, 48f, "lock_off", "Lock new joins: Off", "Yeni katılımı kilitle: Kapalı");
			Button("SettingsReady", parent2, 35f, 570f, 630f, 48f, "require_ready", "Require everyone ready: On", "Herkes hazır olmalı: Açık");
			Label("SettingsFeedback", parent2, 35f, 634f, 630f, 61f, 15f, "settings_help", "Only the host can edit these settings. Changing them clears crew readiness.", "Bu ayarları yalnızca ev sahibi değiştirebilir. Değişiklikler ekibin hazır durumunu sıfırlar.");
			Button("SettingsCancel", parent2, 35f, 718f, 220f, 49f, "cancel", "Cancel", "Vazgeç");
			Button("SettingsSave", parent2, 273f, 718f, 392f, 49f, "save_settings", "SAVE SETTINGS", "AYARLARI KAYDET", danger: false, primary: true);
		}

		private void BuildConfirmModal(RectTransform parent)
		{
			RectTransform parent2 = Modal("ConfirmModal", parent, 640f, 355f);
			Label("ConfirmTitle", parent2, 35f, 28f, 570f, 50f, 30f, "confirm", "ARE YOU SURE?", "EMİN MİSİN?", heading: true);
			Label("ConfirmBody", parent2, 35f, 99f, 570f, 142f, 19f, "confirm_body", "This action affects the current lobby.", "Bu işlem mevcut lobiyi etkiler.");
			Button("ConfirmCancel", parent2, 35f, 275f, 258f, 50f, "cancel", "Cancel", "Vazgeç");
			Button("ConfirmSubmit", parent2, 308f, 275f, 297f, 50f, "confirm_action", "Confirm", "Onayla", danger: true);
		}

		private void BuildBansModal(RectTransform parent)
		{
			RectTransform parent2 = Modal("BansModal", parent, 670f, 572f);
			Label("BansTitle", parent2, 35f, 28f, 600f, 50f, 31f, "banned_title", "BANNED PLAYERS", "YASAKLANAN OYUNCULAR", heading: true);
			Label("BansHelp", parent2, 35f, 89f, 600f, 60f, 16f, "bans_help", "Bans apply to this host's lobbies until removed. Unbanning lets the Steam account join again.", "Yasaklar kaldırılana kadar bu ev sahibinin lobilerinde geçerlidir. Kaldırırsan Steam hesabı yeniden katılabilir.");
			Scroll("BansList", parent2, 35f, 165f, 600f, 304f);
			Label("BansEmpty", parent2, 35f, 234f, 600f, 88f, 20f, "no_bans", "No banned players.", "Yasaklanan oyuncu yok.").alignment = TextAlignmentOptions.Center;
			Button("BansClose", parent2, 35f, 496f, 600f, 48f, "back", "Back", "Geri");
		}

		private RectTransform Modal(string id, RectTransform parent, float width, float height)
		{
			RectTransform rectTransform = Rect(id, parent, 0f, 0f, width, height);
			Vector2 vector = (rectTransform.pivot = new Vector2(0.5f, 0.5f));
			Vector2 anchorMin = (rectTransform.anchorMax = vector);
			rectTransform.anchorMin = anchorMin;
			rectTransform.anchoredPosition = Vector2.zero;
			Paint(rectTransform, _skin.Modal, Color.white);
			AddShadow(rectTransform, 8f);
			Divider(id + "HeaderRule", rectTransform, 35f, 82f, width - 70f);
			rectTransform.gameObject.SetActive(value: false);
			return rectTransform;
		}

		private static RectTransform Rect(string id, Transform parent, float x, float y, float width, float height)
		{
			RectTransform component = new GameObject(id, typeof(RectTransform))
			{
				layer = 5
			}.GetComponent<RectTransform>();
			component.SetParent(parent, worldPositionStays: false);
			Vector2 vector = (component.pivot = new Vector2(0f, 1f));
			Vector2 anchorMin = (component.anchorMax = vector);
			component.anchorMin = anchorMin;
			component.anchoredPosition = new Vector2(x, 0f - y);
			component.sizeDelta = new Vector2(width, height);
			return component;
		}

		private static void Stretch(RectTransform rect)
		{
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			Vector2 offsetMin = (rect.offsetMax = Vector2.zero);
			rect.offsetMin = offsetMin;
		}

		private Image Paint(RectTransform parent, Sprite sprite, Color color)
		{
			Image image = parent.gameObject.AddComponent<Image>();
			image.sprite = sprite;
			image.type = ((sprite != null && sprite.border.sqrMagnitude > 0f) ? Image.Type.Tiled : Image.Type.Simple);
			image.preserveAspect = image.type == Image.Type.Simple && sprite != null;
			image.pixelsPerUnitMultiplier = _skin.PixelsPerUnitMultiplier(sprite);
			image.color = color;
			return image;
		}

		private void Divider(string id, Transform parent, float x, float y, float width)
		{
			Paint(Rect(id, parent, x, y, width, 1.5f), null, new Color32(146, 113, 74, 70)).raycastTarget = false;
		}

		private void CardHeader(string id, Transform parent, float width, float height)
		{
			Paint(Rect(id, parent, 12f, 12f, width - 24f, height - 12f), _skin.Button, Color.white).raycastTarget = false;
		}

		private static void AddShadow(RectTransform rect, float distance)
		{
			Shadow shadow = rect.gameObject.AddComponent<Shadow>();
			shadow.effectColor = new Color(0.02f, 0.08f, 0.1f, 0.24f);
			shadow.effectDistance = new Vector2(0f, 0f - distance);
		}

		private void CloseButton(RectTransform parent)
		{
			RectTransform rectTransform = Rect("Close", parent, 1138f, 28f, 62f, 62f);
			CenterButtonOrigin(rectTransform);
			Image targetGraphic = Paint(rectTransform, _skin.CloseIcon, Color.white);
			BaseButton baseButton = rectTransform.gameObject.AddComponent<BaseButton>();
			baseButton.targetGraphic = targetGraphic;
			baseButton.transition = Selectable.Transition.ColorTint;
		}

		private TMP_Text Label(string id, Transform parent, float x, float y, float width, float height, float size, string key, string english, string turkish, bool heading = false)
		{
			TMP_Text tMP_Text = DynamicLabel(id, parent, x, y, width, height, size, heading);
			tMP_Text.gameObject.AddComponent<LobbyRoomLabel>().Set(key, english, turkish);
			return tMP_Text;
		}

		private TMP_Text DynamicLabel(string id, Transform parent, float x, float y, float width, float height, float size, bool heading = false)
		{
			LobbyLocalizedText lobbyLocalizedText = Rect(id, parent, x, y, width, height).gameObject.AddComponent<LobbyLocalizedText>();
			lobbyLocalizedText.font = ((heading && _skin.HeadingFont != null) ? _skin.HeadingFont : _skin.BodyFont);
			lobbyLocalizedText.fontSize = size;
			lobbyLocalizedText.fontSizeMin = size * 0.76f;
			lobbyLocalizedText.fontSizeMax = size;
			lobbyLocalizedText.enableAutoSizing = true;
			lobbyLocalizedText.color = _skin.Ink;
			lobbyLocalizedText.richText = false;
			lobbyLocalizedText.raycastTarget = false;
			lobbyLocalizedText.overflowMode = TextOverflowModes.Ellipsis;
			lobbyLocalizedText.textWrappingMode = TextWrappingModes.Normal;
			lobbyLocalizedText.alignment = TextAlignmentOptions.MidlineLeft;
			return lobbyLocalizedText;
		}

		private BaseButton Button(string id, Transform parent, float x, float y, float width, float height, string key, string english, string turkish, bool danger = false, bool primary = false)
		{
			RectTransform rectTransform = Rect(id, parent, x, y, width, height);
			CenterButtonOrigin(rectTransform);
			Image targetGraphic = Paint(rectTransform, danger ? _skin.DangerButton : (primary ? _skin.Button : _skin.SecondaryButton), Color.white);
			AddShadow(rectTransform, 2f);
			TMP_Text tMP_Text = Label(id + "Label", rectTransform, 12f, 3f, width - 24f, height - 6f, (height < 48f) ? 16 : 19, key, english, turkish);
			tMP_Text.color = ((danger | primary) ? Color.white : _skin.Ink);
			tMP_Text.alignment = TextAlignmentOptions.Center;
			BaseButton baseButton = rectTransform.gameObject.AddComponent<BaseButton>();
			baseButton.targetGraphic = targetGraphic;
			baseButton.transition = Selectable.Transition.ColorTint;
			ColorBlock colors = baseButton.colors;
			colors.highlightedColor = new Color32(229, 246, byte.MaxValue, byte.MaxValue);
			colors.selectedColor = new Color32(216, 241, byte.MaxValue, byte.MaxValue);
			colors.pressedColor = new Color32(181, 214, 224, byte.MaxValue);
			colors.disabledColor = new Color(0.72f, 0.72f, 0.69f, 0.6f);
			colors.fadeDuration = 0.12f;
			baseButton.colors = colors;
			return baseButton;
		}

		private static void CenterButtonOrigin(RectTransform rect)
		{
			Vector3 position = rect.TransformPoint(rect.rect.center);
			Vector2 vector = (rect.pivot = new Vector2(0.5f, 0.5f));
			Vector2 anchorMin = (rect.anchorMax = vector);
			rect.anchorMin = anchorMin;
			rect.position = position;
		}

		private TMP_InputField Input(string id, Transform parent, float x, float y, float width, float height, string key, string english, string turkish, int max, bool password = false)
		{
			RectTransform rectTransform = Rect(id, parent, x, y, width, height);
			Image targetGraphic = Paint(rectTransform, _skin.Input, new Color32(231, 230, 214, byte.MaxValue));
			TMP_Text textComponent = DynamicLabel(id + "Text", rectTransform, 14f, 5f, width - 28f, height - 10f, 18f);
			TMP_Text tMP_Text = Label(id + "Placeholder", rectTransform, 14f, 5f, width - 28f, height - 10f, 16f, key, english, turkish);
			tMP_Text.color = _skin.Muted;
			TMP_InputField tMP_InputField = rectTransform.gameObject.AddComponent<TMP_InputField>();
			tMP_InputField.textViewport = rectTransform;
			tMP_InputField.textComponent = textComponent;
			tMP_InputField.placeholder = tMP_Text;
			tMP_InputField.targetGraphic = targetGraphic;
			tMP_InputField.characterLimit = max;
			tMP_InputField.lineType = TMP_InputField.LineType.SingleLine;
			tMP_InputField.contentType = (password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard);
			tMP_InputField.asteriskChar = '•';
			tMP_InputField.richText = false;
			tMP_InputField.caretColor = _skin.Ink;
			tMP_InputField.customCaretColor = true;
			return tMP_InputField;
		}

		private void Scroll(string id, Transform parent, float x, float y, float width, float height)
		{
			RectTransform rectTransform = Rect(id, parent, x, y, width, height);
			Paint(rectTransform, _skin.Panel, new Color32(231, 230, 215, byte.MaxValue));
			RectTransform rectTransform2 = Rect(id + "Viewport", rectTransform, 8f, 7f, width - 16f, height - 14f);
			rectTransform2.gameObject.AddComponent<RectMask2D>();
			RectTransform rectTransform3 = Rect(id + "Content", rectTransform2, 0f, 0f, width - 16f, 0f);
			VerticalLayoutGroup verticalLayoutGroup = rectTransform3.gameObject.AddComponent<VerticalLayoutGroup>();
			verticalLayoutGroup.spacing = 9f;
			verticalLayoutGroup.padding = new RectOffset(5, 5, 5, 5);
			verticalLayoutGroup.childForceExpandHeight = false;
			verticalLayoutGroup.childControlHeight = false;
			verticalLayoutGroup.childForceExpandWidth = true;
			rectTransform3.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			ScrollRect scrollRect = rectTransform.gameObject.AddComponent<ScrollRect>();
			scrollRect.viewport = rectTransform2;
			scrollRect.content = rectTransform3;
			scrollRect.horizontal = false;
			scrollRect.vertical = true;
			scrollRect.movementType = ScrollRect.MovementType.Clamped;
			scrollRect.scrollSensitivity = 35f;
		}

		private static string LocalizeResult(string message)
		{
			if (string.IsNullOrEmpty(message))
			{
				return message;
			}
			if (!ResultMessages.TryGetValue(message, out (string, string) value))
			{
				return message;
			}
			return T("result." + value.Item1, message, value.Item2);
		}
	}
}
