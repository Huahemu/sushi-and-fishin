using PaperPixelGames.Core;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Reporting;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States.MainMenu;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityServiceLocator;

namespace PaperPixelGames.Framework.States.Game
{
	public class PauseScreen : BaseScreen
	{
		[Header("Buttons")]
		[SerializeField]
		private BaseButton _resumeButton;

		[SerializeField]
		private BaseButton _playersButton;

		[SerializeField]
		private BaseButton _imStuckButton;

		[SerializeField]
		private BaseButton _settingsButton;

		[SerializeField]
		private BaseButton _saveButton;

		[SerializeField]
		private BaseButton _mainMenuButton;

		[SerializeField]
		private BaseButton _debugButton;

		[Header("Sub Screens")]
		[SerializeField]
		private PlayersScreen _playersScreen;

		[SerializeField]
		private SettingsScreen _settingsScreen;

		[Header("Misc UI")]
		[SerializeField]
		private TextMeshProUGUI _versionText;

		[SerializeField]
		private CanvasGroup _captureVisibilityGroup;

		private BaseScreen _currentSubScreen;

		private InputHandler _cachedInputHandler;

		private bool _inputSubscribed;

		protected override void OnInitialize()
		{
			if (_playersScreen != null)
			{
				_playersScreen.Initialize();
			}
			if (_settingsScreen != null)
			{
				_settingsScreen.Initialize();
			}
			if (_saveButton != null)
			{
				_saveButton.gameObject.SetActive(NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost);
			}
			UpdateVersionText();
			CacheInputHandler();
		}

		private void UpdateVersionText()
		{
			if (!(_versionText == null))
			{
				_versionText.text = string.Empty;
				_versionText.gameObject.SetActive(false);
			}
			if (_debugButton != null) _debugButton.gameObject.SetActive(false);
		}

		private void CacheInputHandler()
		{
			_cachedInputHandler = ((Singleton<InputManager>.Instance != null) ? Singleton<InputManager>.Instance.InputHandler : null);
			if (_cachedInputHandler == null)
			{
				UtilityMethods.Log("[PauseScreen] InputHandler is null.", LogType.Warning);
			}
		}

		private void OnEnable()
		{
			SubscribeInputSafe();
		}

		private void OnDisable()
		{
			UnsubscribeInputSafe();
		}

		private void SubscribeInputSafe()
		{
			if (!_inputSubscribed)
			{
				if (_cachedInputHandler == null)
				{
					CacheInputHandler();
				}
				if (!(_cachedInputHandler == null))
				{
					_cachedInputHandler.Cancel += OnCancelInput;
					_inputSubscribed = true;
				}
			}
		}

		private void UnsubscribeInputSafe()
		{
			if (_inputSubscribed)
			{
				if (_cachedInputHandler != null)
				{
					_cachedInputHandler.Cancel -= OnCancelInput;
				}
				_inputSubscribed = false;
			}
		}

		private void RegisterEvents()
		{
			if (_resumeButton != null)
			{
				_resumeButton.Clicked += OnResumeButtonClick;
			}
			if (_playersButton != null)
			{
				_playersButton.Clicked += OnPlayersButtonClick;
			}
			if (_imStuckButton != null)
			{
				_imStuckButton.Clicked += OnImStuckButtonClick;
			}
			if (_settingsButton != null)
			{
				_settingsButton.Clicked += OnSettingsButtonClick;
			}
			if (_saveButton != null)
			{
				_saveButton.Clicked += OnSaveButtonClick;
			}
			if (_mainMenuButton != null)
			{
				_mainMenuButton.Clicked += OnMainMenuButtonClick;
			}
			if (_playersScreen != null)
			{
				_playersScreen.RequestedClose += HideCurrentSubScreen;
			}
			if (_debugButton != null)
			{
				_debugButton.Clicked += OnDebugButton;
			}
		}

		private void UnRegisterEvents()
		{
			if (_resumeButton != null)
			{
				_resumeButton.Clicked -= OnResumeButtonClick;
			}
			if (_playersButton != null)
			{
				_playersButton.Clicked -= OnPlayersButtonClick;
			}
			if (_imStuckButton != null)
			{
				_imStuckButton.Clicked -= OnImStuckButtonClick;
			}
			if (_settingsButton != null)
			{
				_settingsButton.Clicked -= OnSettingsButtonClick;
			}
			if (_saveButton != null)
			{
				_saveButton.Clicked -= OnSaveButtonClick;
			}
			if (_mainMenuButton != null)
			{
				_mainMenuButton.Clicked -= OnMainMenuButtonClick;
			}
			if (_debugButton != null)
			{
				_debugButton.Clicked -= OnDebugButton;
			}
			if (_playersScreen != null)
			{
				_playersScreen.RequestedClose -= HideCurrentSubScreen;
			}
		}

		public void OnOpenSettings(InputActionPhase phase)
		{
			if (phase != InputActionPhase.Started)
			{
				return;
			}
			PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
			if (pPG_Player != null && (pPG_Player.IsUsingEmoteArea || pPG_Player.DidExitEmoteAreaThisFrame))
			{
				return;
			}
			if (Singleton<GameUIManager>.Instance != null && Singleton<GameUIManager>.Instance.StationTutorialPopupUI != null && Singleton<GameUIManager>.Instance.StationTutorialPopupUI.isActiveAndEnabled)
			{
				Singleton<GameUIManager>.Instance.StationTutorialPopupUI.Close();
				Cursor.visible = false;
			}
			else if (Singleton<GameUIManager>.Instance != null && Singleton<GameUIManager>.Instance.DialoguePanelController != null && Singleton<GameUIManager>.Instance.DialoguePanelController.IsVisible)
			{
				Singleton<GameUIManager>.Instance.DialoguePanelController.Hide();
				Cursor.visible = false;
			}
			else if (!(Singleton<GameUIManager>.Instance != null) || !Singleton<GameUIManager>.Instance.IsShopUIActive)
			{
				if (base.IsVisible)
				{
					Hide();
				}
				else
				{
					Show();
				}
			}
		}

		private void OnCancelInput(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && base.IsVisible && (!(Singleton<GameUIManager>.Instance != null) || !Singleton<GameUIManager>.Instance.IsShopUIActive))
			{
				if (_currentSubScreen != null && _currentSubScreen.IsVisible)
				{
					HideCurrentSubScreen();
				}
				else
				{
					Hide();
				}
			}
		}

		protected override void OnShow()
		{
			RegisterEvents();
			HideGameplayHud();
			SwitchLocalPlayerToUI();
		}

		protected override void OnHide()
		{
			UnRegisterEvents();
			HideCurrentSubScreen();
			RestoreGameplayHud();
			SwitchLocalPlayerToPlayer();
		}

		private void HideGameplayHud()
		{
			if (Singleton<HintsManager>.Instance != null)
			{
				Singleton<HintsManager>.Instance.SetVisibilitySuppressed(suppressed: true);
			}
			PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
			if (!(pPG_Player == null))
			{
				pPG_Player.SetVisibility(v: false);
				pPG_Player.DivingEquipment?.SetUIVisibilitySuppressed(suppressed: true);
			}
		}

		private void RestoreGameplayHud()
		{
			if (Singleton<HintsManager>.Instance != null)
			{
				Singleton<HintsManager>.Instance.SetVisibilitySuppressed(suppressed: false);
			}
			PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
			if (!(pPG_Player == null))
			{
				bool visibility = Singleton<GameUIManager>.Instance == null || Singleton<GameUIManager>.Instance.IsHudEnabled;
				pPG_Player.SetVisibility(visibility);
				pPG_Player.DivingEquipment?.SetUIVisibilitySuppressed(suppressed: false);
			}
		}

		private void OnResumeButtonClick()
		{
			Hide();
		}

		private void OnPlayersButtonClick()
		{
			if (!(_playersScreen == null))
			{
				_playersScreen.LoadCurrentPlayers();
				ShowSubScreen(_playersScreen);
			}
		}

		private void OnSettingsButtonClick()
		{
			if (!(_settingsScreen == null))
			{
				ShowSubScreen(_settingsScreen);
			}
		}

		private void OnImStuckButtonClick()
		{
			HideCurrentSubScreen();
			PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
			if (pPG_Player == null)
			{
				UtilityMethods.Log("LocalPlayer is null, cannot run stuck recovery");
				Hide();
			}
			else
			{
				pPG_Player.RecoverFromStuck();
				pPG_Player.StuckSave();
				Hide();
			}
		}

		private void OnSaveButtonClick()
		{
			if (!(NetworkManager.Singleton == null) && NetworkManager.Singleton.IsHost)
			{
				ServiceLocator serviceLocator = ServiceLocator.ForSceneOf(this);
				RpcRelay service;
				if (serviceLocator == null)
				{
					UtilityMethods.Log("ServiceLocator is null, cannot save");
				}
				else if (!serviceLocator.Get<RpcRelay>(out service) || service == null)
				{
					UtilityMethods.Log("RpcRelay not found, cannot save");
				}
				else
				{
					service.ClientSaveRpc();
				}
			}
		}

		private void OnMainMenuButtonClick()
		{
			if (Singleton<GameSessionLifecycle>.Instance == null)
			{
				UtilityMethods.Log("[PauseScreen] GameSessionLifecycle is null, cannot return to main menu.", LogType.Warning);
			}
			else
			{
				Singleton<GameSessionLifecycle>.Instance.ReturnToMainMenu(ReturnToMainMenuRequest.UserRequested());
			}
		}

		public void OnDebugButton()
		{
			if (Singleton<ReportManager>.Instance == null)
			{
				UtilityMethods.Log("[PauseScreen] ReportManager is null, cannot open report screen.", LogType.Warning);
			}
			else
			{
				Singleton<ReportManager>.Instance.ShowReportScreen(allowScreenshot: true, _captureVisibilityGroup);
			}
		}

		private void ShowSubScreen(BaseScreen screen)
		{
			if (screen == null)
			{
				return;
			}
			if (_currentSubScreen == screen)
			{
				if (!screen.IsVisible)
				{
					screen.Show();
				}
				return;
			}
			if (_currentSubScreen != null && _currentSubScreen.IsVisible)
			{
				_currentSubScreen.Hide();
			}
			_currentSubScreen = screen;
			_currentSubScreen.Show();
		}

		private void HideCurrentSubScreen()
		{
			if (!(_currentSubScreen == null))
			{
				if (_currentSubScreen.IsVisible)
				{
					_currentSubScreen.Hide();
				}
				_currentSubScreen = null;
			}
		}

		private void SwitchLocalPlayerToUI()
		{
			PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
			if (!(pPG_Player == null))
			{
				pPG_Player.AcquireUIInput(PlayerUIInputOwner.PauseScreen);
			}
		}

		private void SwitchLocalPlayerToPlayer()
		{
			PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
			if (!(pPG_Player == null))
			{
				pPG_Player.ReleaseUIInput(PlayerUIInputOwner.PauseScreen);
			}
		}
	}
}
