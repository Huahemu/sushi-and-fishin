using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Framework.States.MainMenu;
using Bozo.ModularCharacters;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using PaperPixelGames.Core;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Player.Visuals;
using PaperPixelGames.Core.Reporting;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking;
using Steamworks;
using Steamworks.Data;
using StylizedWater3;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PaperPixelGames.Framework.States.MainMenu
{
	[DefaultExecutionOrder(-2000)]
	public class MainMenuManager : Singleton<MainMenuManager>
	{
		private const string SteamUrl = "https://store.steampowered.com/developer/paperpixelgames";

		private const string DiscordUrl = "https://discord.gg/paperpixelgames";

		private const string YoutubeUrl = "https://www.youtube.com/@PaperPixelGames";

		private const string FirstTimeLaunchKey = "FirstTimeLaunch";

		[Header("Menus")]
		[SerializeField]
		private SaveSlotScreen _saveSlotScreen;

		[SerializeField]
		private SettingsScreen _settingsScreen;

		[SerializeField]
		private CreditsScreen _creditsScreen;

		[SerializeField]
		private LobbyMembersUI _lobbyMembersUI;

		[Header("Main Buttons")]
		[SerializeField]
		private BaseButton _singleplayerButton;

		[SerializeField]
		private BaseButton _playButton;

		[SerializeField]
		private BaseButton _customizationButton;

		[SerializeField]
		private BaseButton _settingsButton;

		[SerializeField]
		private BaseButton _creditsButton;

		[SerializeField]
		private BaseButton _fedbackButton;

		[SerializeField]
		private BaseButton _quitButton;

		[Header("External Buttons")]
		[SerializeField]
		private BaseButton _discordButton;

		[SerializeField]
		private BaseButton _steamButton;

		[SerializeField]
		private BaseButton _youtubeButton;

		[Header("Misc UI")]
		[SerializeField]
		private PatchNotePopup _patchNotePopup;

		[SerializeField]
		private TextMeshProUGUI _versionText;

		[SerializeField]
		private GameObject _earlyAccessPopUp;

		[SerializeField]
		private BaseButton _okButton;

		[SerializeField]
		private MotionSicknessPopup _motionSicknessPopup;

		[SerializeField]
		private RapidPressBindingPopup _rapidPressBindingPopup;

		[Header("Steam Connection Blocker")]
		[SerializeField]
		private GameObject _steamConnectionBlocker;

		[SerializeField]
		private TextMeshProUGUI _steamConnectionFeedbackText;

		[SerializeField]
		private BaseButton _steamConnectionQuitButton;

		[Space]
		[Header("Scene Refs")]
		[SerializeField]
		private List<GameObject> _charModels;

		[SerializeField]
		private GameObject _mainCamera;

		[Header("Animation")]
		[SerializeField]
		private RectTransform _gameName;

		[SerializeField]
		private RectTransform _buttons;

		[SerializeField]
		private RectTransform _socials;

		[SerializeField]
		private RectTransform _members;

		[SerializeField]
		private float _textTweenDurationGame = 3f;

		[SerializeField]
		private float _textTweenDurationButton = 3f;

		[SerializeField]
		private float _textTweenDurationSocial = 3f;

		[SerializeField]
		private float _textTweenDurationMember = 3f;

		[SerializeField]
		private Vector2 _gameNameMoveOffset = new Vector2(0f, 150f);

		[SerializeField]
		private Vector2 _buttonsMoveOffset = new Vector2(-550f, 0f);

		[SerializeField]
		private Vector2 _socialsMoveOffset = new Vector2(0f, -150f);

		[SerializeField]
		private Vector2 _membersMoveOffset = new Vector2(850f, 0f);

		[SerializeField]
		private Ease _textTweenEase = Ease.InQuad;

		[Space]
		[Header("Boat")]
		[SerializeField]
		private Transform _boat;

		[SerializeField]
		private GameObject _boatMemberChars;

		[SerializeField]
		private AlignToWater _boatAlignment;

		[SerializeField]
		private float _boatMoveDistance = 2f;

		[SerializeField]
		private float _boatMoveDuration = 1.25f;

		[SerializeField]
		private Ease _boatMoveEase = Ease.InOutSine;

		[SerializeField]
		private AudioSource _boatSFX;

		[SerializeField]
		private float _boatIdlePitch = 0.92f;

		[SerializeField]
		private float _boatMovePitch = 1.1f;

		[SerializeField]
		private float _boatIdleVolume = 0.15f;

		[SerializeField]
		private float _boatMoveVolume = 0.65f;

		[SerializeField]
		private float _boatSfxTweenDuration = 1f;

		[SerializeField]
		private Ease _boatSfxTweenEase = Ease.OutSine;

		private const float MaximumSafeBoatIdleVolume = 0.25f;

		private const float MaximumSafeBoatMoveVolume = 0.7f;

		[Header("Lobby Button State")]
		[SerializeField]
		private TextMeshProUGUI _playButtonText;

		[SerializeField]
		private string _playButtonDefaultText = "Play";

		[SerializeField]
		private string _leaveLobbyButtonText = "Leave Lobby";

		private bool _playButtonActsAsLeaveLobby;

		private bool _boatTransitionInProgress;

		private Coroutine _boatDepartureRoutine;

		private long _boatDepartureVersion;

		private readonly Dictionary<Selectable, bool> _departureInputStates = new Dictionary<Selectable, bool>();

		private Tween _boatPitchTween;

		private Tween _boatVolumeTween;

		private Vector3 _boatStartPosition;

		private Quaternion _boatStartRotation;

		private Tween _boatTween;

		private Vector2 _gameNameStartAnchoredPos;

		private Vector2 _buttonsStartAnchoredPos;

		private Vector2 _socialsStartAnchoredPos;

		private Vector2 _membersStartAnchoredPos;

		private Tween _gameNameTween;

		private Tween _buttonsTween;

		private Tween _socialsTween;

		private Tween _membersTween;

		private bool _initialized;

		private bool _eventsBound;

		private BaseScreen _currentOpenedScreen;

		private MainMenuCustomizationSceneHost _customizationSceneHost;

		[SerializeField]
		private SushiAndFish.FrontEnd.IslandFrontEndFlow _islandFrontEnd;

		public MainMenuCustomizationSceneHost CustomizationSceneHost
		{
			get { EnsureCustomizationSceneHost(); return _customizationSceneHost; }
		}

		private LobbyCharacterCustomizationSync _lobbyCustomizationSync;

		private LobbyRoomScreen _lobbyRoomScreen;

		private PatchInfoPopup _patchInfoPopup;

		public override SingletonScope Scope => SingletonScope.Scene;

		public bool IsBoatDepartureInProgress => _boatTransitionInProgress;

		public LobbyMembersUI LobbyMembersUI => _lobbyMembersUI;

		public bool CanPresentLobby
		{
			get
			{
				if (base.isActiveAndEnabled && !_boatTransitionInProgress)
				{
					if (!(_patchInfoPopup == null))
					{
						return !_patchInfoPopup.IsVisible;
					}
					return true;
				}
				return false;
			}
		}

		public int CharModelCount
		{
			get
			{
				if (_charModels == null)
				{
					return 0;
				}
				return _charModels.Count;
			}
		}

		private float SafeBoatIdleVolume => Mathf.Clamp(_boatIdleVolume, 0f, 0.25f);

		private float SafeBoatMoveVolume => Mathf.Clamp(_boatMoveVolume, 0f, 0.7f);

		protected override void Awake()
		{
			base.Awake();
			for (int i = 0; i < _charModels.Count; i++)
			{
				GameObject gameObject = _charModels[i];
				if (!(gameObject == null))
				{
					OutfitSystem[] componentsInChildren = gameObject.GetComponentsInChildren<OutfitSystem>(includeInactive: true);
					for (int j = 0; j < componentsInChildren.Length; j++)
					{
						componentsInChildren[j].loadMode = OutfitSystem.LoadMode.Manual;
					}
					gameObject.SetActive(i == 0);
				}
			}
			CacheBoatStartPose();
			CacheRectStartPoses();
			if (_boatSFX != null)
			{
				_boatSFX.pitch = _boatIdlePitch;
				_boatSFX.volume = 0f;
			}
		}

		public void Initialize()
		{
			if (!_initialized)
			{
				_initialized = true;
				EnsureCustomizationSceneHost();
				EnsureLobbyCustomizationSync();
				InitializeCustomizationAsync().Forget();
				UpdateVersionText();
				RegisterEvents();
				ConfigureLocalizedMainMenuTexts();
				_saveSlotScreen?.Initialize();
				_settingsScreen?.Initialize();
				_creditsScreen?.Initialize();
				_lobbyMembersUI?.Initialize();
				EnsureLobbyRoomScreen();
				InitializeFirstLaunchPopups();
				ApplySteamConnectionBlockerState();
				Singleton<MultiplayerCoordinator>.Instance?.RefreshCurrentLobbyMenuUi();
				FadeInBoatIdleSfx();
			}
		}

		private async UniTask InitializeCustomizationAsync()
		{
			if (!PlayerCustomizationSession.EnsureInitialCharacterData())
			{
				UtilityMethods.Log("[MainMenuManager] Initial character preset could not be loaded.", LogType.Error);
			}
			else
			{
				await _customizationSceneHost.RestoreSavedCustomizationToMenuPuppetsAsync();
			}
		}

		public void SwitchMenuStatus(MainMenuType menuType)
		{
			if (!_boatTransitionInProgress && (!(_patchInfoPopup != null) || !_patchInfoPopup.IsVisible))
			{
				CloseCurrentScreen();
				BaseScreen baseScreen = menuType switch
				{
					MainMenuType.Play => _saveSlotScreen, 
					MainMenuType.Settings => _settingsScreen, 
					MainMenuType.Credits => _creditsScreen, 
					_ => null, 
				};
				if (!(baseScreen == null))
				{
					_currentOpenedScreen = baseScreen;
					_currentOpenedScreen.Show();
				}
			}
		}

		private void CloseCurrentScreen()
		{
			if (!(_currentOpenedScreen == null))
			{
				_currentOpenedScreen.Hide();
				_currentOpenedScreen = null;
			}
		}

		private void RegisterEvents()
		{
			if (_eventsBound)
			{
				return;
			}
			_eventsBound = true;
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
			if (_quitButton != null)
			{
				_quitButton.Clicked += OnClickQuitButton;
			}
			if (_steamConnectionQuitButton != null)
			{
				_steamConnectionQuitButton.Clicked += OnClickQuitButton;
			}
			if (_discordButton != null)
			{
				_discordButton.Clicked += OnClickDiscord;
			}
			if (_steamButton != null)
			{
				_steamButton.Clicked += OnClickSteam;
			}
			if (_youtubeButton != null)
			{
				_youtubeButton.Clicked += OnClickYoutube;
			}
			if (_okButton != null)
			{
				_okButton.Clicked += OnClickOkButton;
			}
			if (_motionSicknessPopup != null)
			{
				_motionSicknessPopup.Completed -= OnMotionSicknessPromptCompleted;
				_motionSicknessPopup.Completed += OnMotionSicknessPromptCompleted;
			}
			if (_rapidPressBindingPopup != null)
			{
				_rapidPressBindingPopup.Completed -= OnRapidPressPromptCompleted;
				_rapidPressBindingPopup.Completed += OnRapidPressPromptCompleted;
			}
			if (_playButton != null)
			{
				_playButton.Clicked += OnClickPlay;
			}
			if (_singleplayerButton != null)
			{
				_singleplayerButton.Clicked += OnClickSingleplayer;
			}
			if (_customizationButton != null)
			{
				_customizationButton.Clicked += OnClickCustomization;
			}
			if (_settingsButton != null)
			{
				_settingsButton.Clicked += OnClickSettings;
			}
			if (_creditsButton != null)
			{
				_creditsButton.Clicked += OnClickCredits;
			}
			if (_fedbackButton != null)
			{
				_fedbackButton.Clicked += OnClickFeedback;
			}
			InputManager instance = Singleton<InputManager>.Instance;
			if (instance == null || instance.InputHandler == null)
			{
				UtilityMethods.Log("Input Manager/Input Handler is null, couldn't register Cancel event");
				return;
			}
			instance.InputHandler.Cancel -= OnCloseSettings;
			instance.InputHandler.Cancel += OnCloseSettings;
			MultiplayerCoordinator instance2 = Singleton<MultiplayerCoordinator>.Instance;
			if (instance2 != null)
			{
				instance2.ForeignLobbyStateChanged -= OnForeignLobbyStateChanged;
				instance2.ForeignLobbyStateChanged += OnForeignLobbyStateChanged;
				instance2.SteamConnectionAvailabilityChanged -= OnSteamConnectionAvailabilityChanged;
				instance2.SteamConnectionAvailabilityChanged += OnSteamConnectionAvailabilityChanged;
				ApplyForeignLobbyUiState(instance2.IsInForeignLobby);
			}
		}

		private void OnEnable()
		{
			_lobbyRoomScreen?.ResumeDeferredPasswordRequest();
		}

		private void Update()
		{
			if (_singleplayerButton != null)
			{
				GameObject obj = _singleplayerButton.gameObject;
				MultiplayerCoordinator instance = Singleton<MultiplayerCoordinator>.Instance;
				obj.SetActive((object)instance == null || !instance.LobbyBlocksSingleplayer);
			}
			_lobbyRoomScreen?.RefreshServiceState();
		}

		private void OnDisable()
		{
			if (_patchInfoPopup != null)
			{
				_patchInfoPopup.Suspend();
			}
			if (_lobbyRoomScreen != null && _lobbyRoomScreen.IsVisible)
			{
				_lobbyRoomScreen.Hide();
			}
		}

		private void OnDestroy()
		{
			CancelBoatDeparture();
			_boatTween?.Kill();
			_gameNameTween?.Kill();
			_buttonsTween?.Kill();
			_socialsTween?.Kill();
			_membersTween?.Kill();
			_boatPitchTween?.Kill();
			_boatVolumeTween?.Kill();
			DeregisterEvents();
			if (_patchInfoPopup != null)
			{
				GameObject gameObject = _patchInfoPopup.gameObject;
				_patchInfoPopup = null;
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
			if ((object)_lobbyRoomScreen != null)
			{
				_lobbyRoomScreen.Release();
			}
			if (_lobbyRoomScreen != null)
			{
				GameObject gameObject2 = _lobbyRoomScreen.gameObject;
				_lobbyRoomScreen = null;
				gameObject2.SetActive(value: false);
				if (Application.isPlaying)
				{
					UnityEngine.Object.Destroy(gameObject2);
				}
				else
				{
					UnityEngine.Object.DestroyImmediate(gameObject2);
				}
			}
		}

		public void DeregisterEvents()
		{
			if (_eventsBound)
			{
				_eventsBound = false;
				LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
				if (_quitButton != null)
				{
					_quitButton.Clicked -= OnClickQuitButton;
				}
				if (_steamConnectionQuitButton != null)
				{
					_steamConnectionQuitButton.Clicked -= OnClickQuitButton;
				}
				if (_discordButton != null)
				{
					_discordButton.Clicked -= OnClickDiscord;
				}
				if (_steamButton != null)
				{
					_steamButton.Clicked -= OnClickSteam;
				}
				if (_youtubeButton != null)
				{
					_youtubeButton.Clicked -= OnClickYoutube;
				}
				if (_okButton != null)
				{
					_okButton.Clicked -= OnClickOkButton;
				}
				if (_motionSicknessPopup != null)
				{
					_motionSicknessPopup.Completed -= OnMotionSicknessPromptCompleted;
				}
				if (_rapidPressBindingPopup != null)
				{
					_rapidPressBindingPopup.Completed -= OnRapidPressPromptCompleted;
				}
				if (_playButton != null)
				{
					_playButton.Clicked -= OnClickPlay;
				}
				if (_singleplayerButton != null)
				{
					_singleplayerButton.Clicked -= OnClickSingleplayer;
				}
				if (_customizationButton != null)
				{
					_customizationButton.Clicked -= OnClickCustomization;
				}
				if (_settingsButton != null)
				{
					_settingsButton.Clicked -= OnClickSettings;
				}
				if (_creditsButton != null)
				{
					_creditsButton.Clicked -= OnClickCredits;
				}
				if (_fedbackButton != null)
				{
					_fedbackButton.Clicked -= OnClickFeedback;
				}
				InputManager instance = Singleton<InputManager>.Instance;
				if (instance != null && instance.InputHandler != null)
				{
					instance.InputHandler.Cancel -= OnCloseSettings;
				}
				MultiplayerCoordinator instance2 = Singleton<MultiplayerCoordinator>.Instance;
				if (instance2 != null)
				{
					instance2.ForeignLobbyStateChanged -= OnForeignLobbyStateChanged;
					instance2.SteamConnectionAvailabilityChanged -= OnSteamConnectionAvailabilityChanged;
				}
			}
		}

		private void OnSteamConnectionAvailabilityChanged(bool isAvailable)
		{
			ApplySteamConnectionBlockerState();
		}

		private void ApplySteamConnectionBlockerState()
		{
			MultiplayerCoordinator instance = Singleton<MultiplayerCoordinator>.Instance;
			bool flag = instance != null && instance.IsSteamConnectionBlocked;
			if (_steamConnectionBlocker != null)
			{
				_steamConnectionBlocker.SetActive(flag);
			}
			if (!flag)
			{
				TryShowPatchInfo();
				return;
			}
			if (_patchInfoPopup != null)
			{
				_patchInfoPopup.Suspend();
			}
			string text = ((instance != null) ? instance.SteamConnectionBlockMessage : "Steam connection is required to play. Please start the game through Steam.");
			if (_steamConnectionFeedbackText != null)
			{
				_steamConnectionFeedbackText.text = text;
			}
			CloseCurrentScreen();
			UtilityMethods.Log("<b><color=#FFB020>[MainMenu]</color></b> Steam connection blocker enabled.\n   Message: " + text, LogType.Warning);
		}

		private void OnClickSingleplayer()
		{
			if (_islandFrontEnd != null) { _islandFrontEnd.BeginNewGame(); return; }
			if (!_boatTransitionInProgress)
			{
				MultiplayerCoordinator instance = Singleton<MultiplayerCoordinator>.Instance;
				if (!(instance == null) && instance.TrySelectSingleplayer())
				{
					SwitchMenuStatus(MainMenuType.Play);
				}
			}
		}

		private void OnClickPlay()
		{
			if (_islandFrontEnd != null) { _islandFrontEnd.ShowLoadGames(); return; }
			if (!_boatTransitionInProgress)
			{
				OpenLobbyRoom();
			}
		}

		private void EnsureLobbyRoomScreen()
		{
			if (!(_lobbyRoomScreen != null))
			{
				LobbyRoomScreen lobbyRoomScreen = Resources.Load<LobbyRoomScreen>("LobbyRoomScreen");
				if (lobbyRoomScreen != null)
				{
					_lobbyRoomScreen = UnityEngine.Object.Instantiate(lobbyRoomScreen);
				}
				else
				{
					GameObject gameObject = new GameObject("LobbyRoomScreen", typeof(RectTransform));
					gameObject.SetActive(value: false);
					_lobbyRoomScreen = gameObject.AddComponent<LobbyRoomScreen>();
				}
				SceneManager.MoveGameObjectToScene(_lobbyRoomScreen.gameObject, base.gameObject.scene);
				_lobbyRoomScreen.Configure(this);
				_lobbyRoomScreen.gameObject.SetActive(value: false);
			}
		}

		public void OpenLobbyRoom()
		{
			if (!_boatTransitionInProgress && (!(_patchInfoPopup != null) || !_patchInfoPopup.IsVisible))
			{
				Singleton<MultiplayerCoordinator>.Instance?.SelectMultiplayer();
				EnsureLobbyRoomScreen();
				CloseCurrentScreen();
				_currentOpenedScreen = _lobbyRoomScreen;
				_lobbyRoomScreen.Show();
				MultiplayerCoordinator instance = Singleton<MultiplayerCoordinator>.Instance;
				if (instance != null && instance.RoomService != null && instance.RoomService.Snapshot.LobbyId != 0L)
				{
					_lobbyRoomScreen.ShowRoom();
				}
				else
				{
					_lobbyRoomScreen.ShowBrowser();
				}
			}
		}

		public void OpenLobbySaveSelection()
		{
			MultiplayerCoordinator instance = Singleton<MultiplayerCoordinator>.Instance;
			if (!(instance == null) && !(instance.RoomService == null) && instance.RoomService.CanLaunch)
			{
				SwitchMenuStatus(MainMenuType.Play);
			}
		}

		private void OnClickSettings()
		{
			SwitchMenuStatus(MainMenuType.Settings);
		}

		private void OnClickCredits()
		{
			SwitchMenuStatus(MainMenuType.Credits);
		}

		private void OnClickFeedback()
		{
			if (!_boatTransitionInProgress && (!(_patchInfoPopup != null) || !_patchInfoPopup.IsVisible))
			{
				Singleton<ReportManager>.Instance.ShowReportScreen(allowScreenshot: false);
			}
		}

		private void CheckLaunchPopUp()
		{
			if (_earlyAccessPopUp == null)
			{
				TryShowPatchInfo();
				return;
			}
			bool flag = !PlayerPrefs.HasKey("FirstTimeLaunch");
			_earlyAccessPopUp.SetActive(flag);
			if (flag)
			{
				PlayerPrefs.SetInt("FirstTimeLaunch", 1);
				PlayerPrefs.Save();
			}
			else
			{
				TryShowPatchInfo();
			}
		}

		private void TryShowPatchInfo()
		{
			if (_islandFrontEnd != null) return;
			if (!_initialized || _boatTransitionInProgress || (_motionSicknessPopup != null && _motionSicknessPopup.IsVisible) || (_rapidPressBindingPopup != null && _rapidPressBindingPopup.IsVisible) || (_earlyAccessPopUp != null && _earlyAccessPopUp.activeSelf) || (Singleton<MultiplayerCoordinator>.Instance != null && Singleton<MultiplayerCoordinator>.Instance.IsSteamConnectionBlocked) || PlayerPrefs.GetInt("PatchInfo.Seen.lobby-staff-boss-v1", 0) == 1)
			{
				return;
			}
			if (_patchInfoPopup == null)
			{
				PatchInfoPopup patchInfoPopup = Resources.Load<PatchInfoPopup>("PatchInfoPopup");
				if (patchInfoPopup == null)
				{
					Debug.LogError("[MainMenuManager] PatchInfoPopup prefab is missing.", this);
					return;
				}
				_patchInfoPopup = UnityEngine.Object.Instantiate(patchInfoPopup);
				SceneManager.MoveGameObjectToScene(_patchInfoPopup.gameObject, base.gameObject.scene);
			}
			_patchInfoPopup.TryShow((_playButton != null) ? _playButton.gameObject : null);
		}

		private void InitializeFirstLaunchPopups()
		{
			if (_earlyAccessPopUp != null)
			{
				_earlyAccessPopUp.SetActive(value: false);
			}
			if ((!(_motionSicknessPopup != null) || !_motionSicknessPopup.TryShow()) && (!(_rapidPressBindingPopup != null) || !_rapidPressBindingPopup.TryShow()))
			{
				CheckLaunchPopUp();
			}
		}

		private void OnMotionSicknessPromptCompleted()
		{
			if (!(_rapidPressBindingPopup != null) || !_rapidPressBindingPopup.TryShow())
			{
				CheckLaunchPopUp();
			}
		}

		private void OnRapidPressPromptCompleted()
		{
			CheckLaunchPopUp();
		}

		private void UpdateVersionText()
		{
			if (!(_versionText == null))
			{
				_versionText.text = string.Empty;
				_versionText.gameObject.SetActive(false);
			}
			if (_fedbackButton != null) _fedbackButton.gameObject.SetActive(false);
		}

		public void CustomizationScreenClose()
		{
			if (_boatTransitionInProgress)
			{
				return;
			}
			if (_patchInfoPopup != null && _patchInfoPopup.IsVisible)
			{
				_patchInfoPopup.Dismiss();
			}
			else if ((!(_motionSicknessPopup != null) || !_motionSicknessPopup.IsVisible) && (!(_rapidPressBindingPopup != null) || !_rapidPressBindingPopup.IsVisible))
			{
				if (_earlyAccessPopUp != null && _earlyAccessPopUp.activeSelf)
				{
					OnClickOkButton();
				}
				else if (_customizationSceneHost != null && _customizationSceneHost.IsOpen)
				{
					_customizationSceneHost.HandleBack();
				}
				else if (!(_currentOpenedScreen != null) || !_currentOpenedScreen.TryHandleBack())
				{
					CloseCurrentScreen();
				}
			}
		}

		private void OnCloseSettings(InputActionPhase phase)
		{
			if (_boatTransitionInProgress || phase != InputActionPhase.Started)
			{
				return;
			}
			if (_patchInfoPopup != null && _patchInfoPopup.IsVisible)
			{
				_patchInfoPopup.Dismiss();
			}
			else if ((!(_motionSicknessPopup != null) || !_motionSicknessPopup.IsVisible) && (!(_rapidPressBindingPopup != null) || !_rapidPressBindingPopup.IsVisible))
			{
				if (_earlyAccessPopUp != null && _earlyAccessPopUp.activeSelf)
				{
					OnClickOkButton();
				}
				else if (_customizationSceneHost != null && _customizationSceneHost.IsOpen)
				{
					_customizationSceneHost.HandleBack();
				}
				else if (!(_currentOpenedScreen != null) || !_currentOpenedScreen.TryHandleBack())
				{
					CloseCurrentScreen();
				}
			}
		}

		private void OnClickQuitButton()
		{
			if (!_boatTransitionInProgress)
			{
				Application.Quit();
			}
		}

		private void OnClickSteam()
		{
			OpenUrl("https://store.steampowered.com/developer/paperpixelgames");
		}

		private void OnClickDiscord()
		{
			OpenUrl("https://discord.gg/paperpixelgames");
		}

		private void OnClickYoutube()
		{
			OpenUrl("https://www.youtube.com/@PaperPixelGames");
		}

		private void OnClickOkButton()
		{
			if (!_boatTransitionInProgress)
			{
				if (_earlyAccessPopUp != null)
				{
					_earlyAccessPopUp.SetActive(value: false);
				}
				TryShowPatchInfo();
			}
		}

		private void OpenUrl(string url)
		{
			if (!_boatTransitionInProgress && !string.IsNullOrWhiteSpace(url))
			{
				Application.OpenURL(url);
			}
		}

		private void OnClickCustomization()
		{
			if (!_boatTransitionInProgress && (!(_patchInfoPopup != null) || !_patchInfoPopup.IsVisible))
			{
				OpenCustomizationWithLoading().Forget();
			}
		}

		private async UniTaskVoid OpenCustomizationWithLoading()
		{
			EnsureCustomizationSceneHost();
			Canvas.ForceUpdateCanvases();
			await UniTask.Yield();
			CloseCurrentScreen();
			await _customizationSceneHost.OpenAsync();
		}

		public GameObject GetCharModelByIndex(int index)
		{
			if (_charModels == null)
			{
				return null;
			}
			if (index < 0 || index >= _charModels.Count)
			{
				return null;
			}
			return _charModels[index];
		}

		public GameObject GetLocalCharModel()
		{
			if (!TryGetLocalSteamId(out var steamId))
			{
				return GetCharModelByIndex(0);
			}
			if (!TryGetCharModelForSteamId(steamId, out var charModel))
			{
				return GetCharModelByIndex(0);
			}
			return charModel;
		}

		public bool TryGetCharModelForSteamId(SteamId steamId, out GameObject charModel)
		{
			charModel = null;
			if (_charModels == null || _charModels.Count == 0)
			{
				return false;
			}
			MultiplayerCoordinator instance = Singleton<MultiplayerCoordinator>.Instance;
			Lobby lobby = ((instance != null && instance.LobbyService != null) ? instance.LobbyService.CurrentLobby : default(Lobby));
			if ((ulong)lobby.Id == 0L)
			{
				if (!TryGetLocalSteamId(out var steamId2) || (ulong)steamId != (ulong)steamId2)
				{
					return false;
				}
				charModel = _charModels[0];
				return charModel != null;
			}
			int num = GetOrderedLobbyMembers(lobby).FindIndex((Friend member) => (ulong)member.Id == (ulong)steamId);
			if (num < 0 || num >= _charModels.Count)
			{
				return false;
			}
			charModel = _charModels[num];
			return charModel != null;
		}

		public void RefreshLobbyPuppetNameTags(Lobby lobby)
		{
			if (_charModels == null)
			{
				return;
			}
			for (int i = 0; i < _charModels.Count; i++)
			{
				GameObject gameObject = _charModels[i];
				((gameObject != null) ? gameObject.GetComponentInChildren<LobbyPuppetNameTag>(includeInactive: true) : null)?.SetName(string.Empty);
			}
			if ((ulong)lobby.Id == 0L)
			{
				return;
			}
			List<Friend> orderedLobbyMembers = GetOrderedLobbyMembers(lobby);
			int num = Math.Min(orderedLobbyMembers.Count, _charModels.Count);
			for (int j = 0; j < num; j++)
			{
				GameObject gameObject2 = _charModels[j];
				LobbyPuppetNameTag lobbyPuppetNameTag = ((gameObject2 != null) ? gameObject2.GetComponentInChildren<LobbyPuppetNameTag>(includeInactive: true) : null);
				if (!(lobbyPuppetNameTag == null))
				{
					Friend friend = orderedLobbyMembers[j];
					string text = ((TryGetLocalSteamId(out var steamId) && (ulong)friend.Id == (ulong)steamId) ? LocalizationDatabase.GetString("UiTable", "ui.main.you", "YOU") : SanitizePuppetName(friend.Name));
					lobbyPuppetNameTag.SetName(text);
				}
			}
		}

		private static List<Friend> GetOrderedLobbyMembers(Lobby lobby)
		{
			try
			{
				SteamId ownerId = lobby.Owner.Id;
				bool hasLocalSteamId = TryGetLocalSteamId(out var localId);
				return (from member in lobby.Members
					orderby hasLocalSteamId && (ulong)member.Id == (ulong)localId descending, (ulong)member.Id == (ulong)ownerId descending
					select member).ThenBy((Func<Friend, ulong>)((Friend member) => member.Id)).ToList();
			}
			catch
			{
				return lobby.Members.OrderBy((Func<Friend, ulong>)((Friend member) => member.Id)).ToList();
			}
		}

		internal static bool TryGetLocalSteamId(out SteamId steamId)
		{
			steamId = default;
			if (!SteamClient.IsValid)
			{
				return false;
			}
			steamId = SteamClient.SteamId;
			return steamId.Value != 0;
		}

		private static string SanitizePuppetName(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return "PLAYER";
			}
			string text = value.Replace("<", string.Empty).Replace(">", string.Empty).Trim();
			if (text.Length > 32)
			{
				return text.Substring(0, 32);
			}
			return text;
		}

		public void SetCharModelByCount(int count)
		{
			if (_charModels == null || _charModels.Count == 0)
			{
				return;
			}
			int num = Mathf.Clamp(count, 1, _charModels.Count);
			for (int i = 0; i < _charModels.Count; i++)
			{
				GameObject gameObject = _charModels[i];
				if (!(gameObject == null))
				{
					gameObject.SetActive(i < num);
				}
			}
			MultiplayerCoordinator instance = Singleton<MultiplayerCoordinator>.Instance;
			if (instance != null && instance.LobbyService != null)
			{
				RefreshLobbyPuppetNameTags(instance.LobbyService.CurrentLobby);
			}
		}

		private void OnApplicationQuit()
		{
		}

		private void EnsureCustomizationSceneHost()
		{
			if (_customizationSceneHost == null)
			{
				GameObject gameObject = new GameObject("[MainMenuCustomizationSceneHost]");
				SceneManager.MoveGameObjectToScene(gameObject, base.gameObject.scene);
				_customizationSceneHost = gameObject.AddComponent<MainMenuCustomizationSceneHost>();
				_customizationSceneHost.Initialize();
			}
			_customizationSceneHost.ConfigureMainMenuPresentation(base.gameObject, _mainCamera, _boatMemberChars, _boatAlignment);
			_customizationSceneHost.SetMenuPuppetRoots(_charModels);
			_customizationSceneHost.SetLocalMenuPuppetResolver(GetLocalCharModel);
		}

		private void EnsureLobbyCustomizationSync()
		{
			if (_lobbyCustomizationSync == null)
			{
				_lobbyCustomizationSync = base.gameObject.GetComponent<LobbyCharacterCustomizationSync>();
			}
			if (_lobbyCustomizationSync == null)
			{
				_lobbyCustomizationSync = base.gameObject.AddComponent<LobbyCharacterCustomizationSync>();
			}
			MultiplayerCoordinator instance = Singleton<MultiplayerCoordinator>.Instance;
			_lobbyCustomizationSync.Initialize(this, _customizationSceneHost, (instance != null) ? instance.LobbyService : null);
		}

		private void CacheBoatStartPose()
		{
			if (_boat == null)
			{
				UtilityMethods.Log("[MainMenuManager] CacheBoatStartPose failed: _boat is null.");
				return;
			}
			_boatStartPosition = _boat.position;
			_boatStartRotation = _boat.rotation;
		}

		public void ResetBoatToStartPose()
		{
			if (_boat == null)
			{
				UtilityMethods.Log("[MainMenuManager] ResetBoatToStartPose failed: _boat is null.");
				return;
			}
			_boat.DOKill();
			_boat.position = _boatStartPosition;
			_boat.rotation = _boatStartRotation;
		}

		private void CacheRectStartPoses()
		{
			if (_gameName != null)
			{
				_gameNameStartAnchoredPos = _gameName.anchoredPosition;
			}
			if (_buttons != null)
			{
				_buttonsStartAnchoredPos = _buttons.anchoredPosition;
			}
			if (_socials != null)
			{
				_socialsStartAnchoredPos = _socials.anchoredPosition;
			}
			if (_members != null)
			{
				_membersStartAnchoredPos = _members.anchoredPosition;
			}
		}

		public void ResetRectsToStartPose()
		{
			_gameNameTween?.Kill();
			_buttonsTween?.Kill();
			_socialsTween?.Kill();
			_membersTween?.Kill();
			_gameName?.DOKill();
			_buttons?.DOKill();
			_socials?.DOKill();
			_members?.DOKill();
			if (_gameName != null)
			{
				_gameName.anchoredPosition = _gameNameStartAnchoredPos;
			}
			if (_buttons != null)
			{
				_buttons.anchoredPosition = _buttonsStartAnchoredPos;
			}
			if (_socials != null)
			{
				_socials.anchoredPosition = _socialsStartAnchoredPos;
			}
			if (_members != null)
			{
				_members.anchoredPosition = _membersStartAnchoredPos;
			}
		}

		public void AnimateBoat()
		{
			if (_islandFrontEnd != null)
			{
				if (!_boatTransitionInProgress && Singleton<MultiplayerCoordinator>.TryGetInstance(out var singleplayer) && singleplayer.CanContinueMenuDeparture(singleplayer.ClientCancellationVersion))
				{
					_boatTransitionInProgress = true;
					_islandFrontEnd.MarkLaunching();
					SetDepartureInputLocked(true);
					_saveSlotScreen?.Hide();
					LoadingScreen.ShowLocalized("loading.title.preparing_world", "Preparing World", "loading.status.loading_world", "Loading the world...");
					Singleton<StateManager>.Instance.SwitchState<LoadingState>();
				}
				return;
			}
			if (!_boatTransitionInProgress && Singleton<MultiplayerCoordinator>.TryGetInstance(out var instance) && instance.CanContinueMenuDeparture(instance.ClientCancellationVersion))
			{
				_boatTransitionInProgress = true;
				long num = ++_boatDepartureVersion;
				SetDepartureInputLocked(locked: true);
				CursorHelper.CursorVisible = false;
				MultiplayerDiagnostics.Record("menu_departure_begin", $"version={num}", ulong.MaxValue);
				_saveSlotScreen?.Hide();
				_lobbyRoomScreen?.DismissForDeparture();
				_gameNameTween?.Kill();
				_buttonsTween?.Kill();
				_socialsTween?.Kill();
				_membersTween?.Kill();
				_gameName?.DOKill();
				_buttons?.DOKill();
				_socials?.DOKill();
				_members?.DOKill();
				if (_gameName != null)
				{
					_gameNameTween = _gameName.DOAnchorPos(_gameNameStartAnchoredPos + _gameNameMoveOffset, _textTweenDurationGame).SetEase(_textTweenEase);
				}
				if (_buttons != null)
				{
					_buttonsTween = _buttons.DOAnchorPos(_buttonsStartAnchoredPos + _buttonsMoveOffset, _textTweenDurationButton).SetEase(_textTweenEase);
				}
				if (_socials != null)
				{
					_socialsTween = _socials.DOAnchorPos(_socialsStartAnchoredPos + _socialsMoveOffset, _textTweenDurationSocial).SetEase(_textTweenEase);
				}
				if (_members != null)
				{
					_membersTween = _members.DOAnchorPos(_membersStartAnchoredPos + _membersMoveOffset, _textTweenDurationMember).SetEase(_textTweenEase);
				}
				_boatTween?.Kill();
				_boat?.DOKill();
				PlayBoatMoveSfx();
				Vector3 endValue = _boatStartPosition + _boatStartRotation * Vector3.forward * _boatMoveDistance;
				_boatTween = _boat?.DOMove(endValue, _boatMoveDuration).SetEase(_boatMoveEase).OnComplete(() =>
				{
					_boatTween = null;
				});
				_boatDepartureRoutine = StartCoroutine(CoBoatDeparture(num, instance.ClientCancellationVersion));
			}
		}

		private IEnumerator CoBoatDeparture(long departureVersion, long sessionVersion)
		{
			yield return new WaitForSecondsRealtime(2.7f);
			if (IsBoatDepartureCurrent(departureVersion, sessionVersion))
			{
				if (Singleton<MultiplayerCoordinator>.Instance.IsSingleplayerSession)
				{
					LoadingScreen.ShowLocalized("loading.title.preparing_world", "Preparing World", "loading.status.loading_world", "Loading the world...");
				}
				else if (Singleton<MultiplayerCoordinator>.Instance.IsHostStarted)
				{
					LoadingScreen.ShowLocalized("loading.title.hosting_session", "Hosting Session", "loading.status.creating_session", "Creating the game session...");
				}
				else
				{
					LoadingScreen.ShowLocalized("loading.title.joining_session", "Joining Session", "loading.status.waiting_for_host", "Waiting for host create the game session...");
				}
				yield return new WaitForSecondsRealtime(1.3f);
				if (IsBoatDepartureCurrent(departureVersion, sessionVersion))
				{
					_boatDepartureRoutine = null;
					MultiplayerDiagnostics.Record("menu_departure_complete", $"version={departureVersion}", ulong.MaxValue);
					Singleton<StateManager>.Instance.SwitchState<LoadingState>();
				}
			}
		}

		private bool IsBoatDepartureCurrent(long departureVersion, long sessionVersion)
		{
			if (_boatTransitionInProgress && departureVersion == _boatDepartureVersion && Singleton<MultiplayerCoordinator>.TryGetInstance(out var instance))
			{
				return instance.CanContinueMenuDeparture(sessionVersion);
			}
			return false;
		}

		private void SetDepartureInputLocked(bool locked)
		{
			if (locked)
			{
				GameObject[] rootGameObjects = base.gameObject.scene.GetRootGameObjects();
				for (int i = 0; i < rootGameObjects.Length; i++)
				{
					Selectable[] componentsInChildren = rootGameObjects[i].GetComponentsInChildren<Selectable>(includeInactive: true);
					foreach (Selectable selectable in componentsInChildren)
					{
						if (!_departureInputStates.ContainsKey(selectable))
						{
							_departureInputStates.Add(selectable, selectable.interactable);
							selectable.interactable = false;
						}
					}
				}
				return;
			}
			foreach (KeyValuePair<Selectable, bool> departureInputState in _departureInputStates)
			{
				if (departureInputState.Key != null)
				{
					departureInputState.Key.interactable = departureInputState.Value;
				}
			}
			_departureInputStates.Clear();
		}

		public void CancelBoatDeparture()
		{
			if (_boatTransitionInProgress)
			{
				_boatTransitionInProgress = false;
				_boatDepartureVersion++;
				if (_boatDepartureRoutine != null)
				{
					StopCoroutine(_boatDepartureRoutine);
				}
				_boatDepartureRoutine = null;
				_boatTween?.Kill();
				_boatTween = null;
				ResetRectsToStartPose();
				if (_boat != null)
				{
					ResetBoatToStartPose();
				}
				ResetBoatSfxToIdle();
				SetDepartureInputLocked(locked: false);
				if (Singleton<StateManager>.TryGetInstance(out var instance) && instance.CurrentState is MainMenuState)
				{
					CursorHelper.CursorVisible = true;
				}
				MultiplayerDiagnostics.Record("menu_departure_cancelled", "", ulong.MaxValue);
			}
		}

		private void PlayBoatMoveSfx()
		{
			if (_boatSFX == null)
			{
				UtilityMethods.Log("[MainMenuManager] PlayBoatMoveSfx failed: _boatSFX is null.");
				return;
			}
			_boatPitchTween?.Kill();
			_boatVolumeTween?.Kill();
			if (!_boatSFX.isPlaying)
			{
				_boatSFX.Play();
			}
			_boatSFX.pitch = _boatIdlePitch;
			_boatSFX.volume = SafeBoatIdleVolume;
			_boatPitchTween = DOTween.To(() => _boatSFX.pitch, (float x) =>
			{
				_boatSFX.pitch = x;
			}, _boatMovePitch, _boatSfxTweenDuration).SetEase(_boatSfxTweenEase);
			_boatVolumeTween = DOTween.To(() => _boatSFX.volume, (float x) =>
			{
				_boatSFX.volume = x;
			}, SafeBoatMoveVolume, _boatSfxTweenDuration).SetEase(_boatSfxTweenEase);
		}

		private void FadeInBoatIdleSfx()
		{
			if (!(_boatSFX == null))
			{
				_boatPitchTween?.Kill();
				_boatVolumeTween?.Kill();
				if (!_boatSFX.isPlaying)
				{
					_boatSFX.Play();
				}
				_boatSFX.pitch = _boatIdlePitch;
				_boatVolumeTween = DOTween.To(() => _boatSFX.volume, (float x) =>
				{
					_boatSFX.volume = x;
				}, SafeBoatIdleVolume, _boatSfxTweenDuration).SetEase(_boatSfxTweenEase);
			}
		}

		public void ResetBoatSfxToIdle(float duration = 0.75f)
		{
			if (!(_boatSFX == null))
			{
				_boatPitchTween?.Kill();
				_boatVolumeTween?.Kill();
				_boatPitchTween = DOTween.To(() => _boatSFX.pitch, (float x) =>
				{
					_boatSFX.pitch = x;
				}, _boatIdlePitch, duration).SetEase(Ease.OutSine);
				_boatVolumeTween = DOTween.To(() => _boatSFX.volume, (float x) =>
				{
					_boatSFX.volume = x;
				}, SafeBoatIdleVolume, duration).SetEase(Ease.OutSine);
			}
		}

		private void OnForeignLobbyStateChanged(bool isInForeignLobby)
		{
			ApplyForeignLobbyUiState(isInForeignLobby);
		}

		private void OnSelectedLocaleChanged(Locale locale)
		{
			ConfigureLocalizedMainMenuTexts();
			ApplyForeignLobbyUiState(_playButtonActsAsLeaveLobby);
			MultiplayerCoordinator instance = Singleton<MultiplayerCoordinator>.Instance;
			if (instance != null && instance.LobbyService != null)
			{
				RefreshLobbyPuppetNameTags(instance.LobbyService.CurrentLobby);
			}
		}

		private void ConfigureLocalizedMainMenuTexts()
		{
			if (_islandFrontEnd != null) { _islandFrontEnd.RefreshLabels(); return; }
			_singleplayerButton?.SetLocalizedText("UiTable", "ui.main.singleplayer", "SINGLEPLAYER");
			_playButton?.SetLocalizedText("UiTable", "ui.main.multiplayer", "MULTIPLAYER");
			_customizationButton?.SetLocalizedText("UiTable", "ui.main.customization", "CUSTOMIZATION");
			_settingsButton?.SetLocalizedText("UiTable", "ui.main.settings", "SETTINGS");
			_creditsButton?.SetLocalizedText("UiTable", "ui.main.credits", "CREDITS");
			_fedbackButton?.SetLocalizedText("UiTable", "ui.main.feedback_bug", "Feedback & Bug");
			_quitButton?.SetLocalizedText("UiTable", "ui.main.quit", "QUIT");
		}

		private void ApplyForeignLobbyUiState(bool isInForeignLobby)
		{
			if (_islandFrontEnd != null) { _playButtonActsAsLeaveLobby = false; _islandFrontEnd.RefreshLabels(); return; }
			_playButtonActsAsLeaveLobby = isInForeignLobby;
			_playButton?.SetLocalizedText("UiTable", "ui.main.multiplayer", "MULTIPLAYER");
			if (isInForeignLobby)
			{
				CloseSaveSlotScreenIfOpen();
			}
		}

		private void CloseSaveSlotScreenIfOpen()
		{
			if (!(_saveSlotScreen == null))
			{
				if (_currentOpenedScreen == _saveSlotScreen)
				{
					_saveSlotScreen.Hide();
					_currentOpenedScreen = null;
				}
				else
				{
					_saveSlotScreen.Hide();
				}
			}
		}
	}
}
