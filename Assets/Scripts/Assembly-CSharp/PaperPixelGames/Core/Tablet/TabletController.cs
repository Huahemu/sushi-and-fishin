using System.Collections.Generic;
using DistantLands.Cozy;
using PaperPixelGames.Core.Interaction.ToolTip;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Indicator;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Tablet
{
	public class TabletController : MonoBehaviour
	{
		[SerializeField]
		private GameObject _tabletUI;

		[SerializeField]
		private TextMeshProUGUI _timeText;

		[SerializeField]
		private TextMeshProUGUI _dayText;

		[Space]
		[Header("App Buttons")]
		[SerializeField]
		private BaseButton _inventoryAppButton;

		[SerializeField]
		private BaseButton _mapAppButton;

		[SerializeField]
		private BaseButton _staffAppbutton;

		[SerializeField]
		private BaseButton _menuAppbutton;

		[SerializeField]
		private BaseButton _wallpaperAppButton;

		[SerializeField]
		private BaseButton _fishBookAppButton;

		[SerializeField]
		private BaseButton _ordersAppButton;

		[SerializeField]
		private BaseButton _upgradeAppButton;

		[SerializeField]
		private BaseButton _runnerGameAppButton;

		[Header("Apps")]
		[SerializeField]
		private DeliveryApp _deliveryApp;

		[SerializeField]
		private MapApp _mapApp;

		[SerializeField]
		private StaffApp _staffApp;

		[SerializeField]
		private MenuApp _menuApp;

		[SerializeField]
		private WallpaperApp _wallpaperApp;

		[SerializeField]
		private FishBookApp _fishBookApp;

		[SerializeField]
		private OrdersApp _ordersApp;

		[SerializeField]
		private InventoryApp _inventoryApp;

		[SerializeField]
		private UpgradeApp _upgradeApp;

		[SerializeField]
		private RunnerGameApp _runnerGameApp;

		private Dictionary<TabletAppType, BaseApp> _appsByType;

		private BaseApp _currentActiveApp;

		private PPG_Player _localPlayer;

		private bool _isTabletActive;

		private bool _initialized;

		public bool IsTabletActive => _isTabletActive;

		public bool IsInitialized => _initialized;

		public MenuApp MenuApp => _menuApp;

		public FishBookApp FishBookApp => _fishBookApp;

		public OrdersApp OrdersApp => _ordersApp;

		public UpgradeApp UpgradeApp => _upgradeApp;

		public MapApp MapApplication => _mapApp;

		public void Initialize(PPG_Player localPlayer)
		{
			if (!_initialized)
			{
				UtilityMethods.Log("Tablet Initialized");
				_localPlayer = localPlayer;
				_inventoryAppButton.Clicked += OnInventoryAppButtonClick;
				_mapAppButton.Clicked += OnMapAppButtonClick;
				_staffAppbutton.Clicked += OnStaffAppButtonClick;
				_menuAppbutton.Clicked += OnMenuAppButtonClick;
				_wallpaperAppButton.Clicked += OnWallpaperAppButtonClick;
				_fishBookAppButton.Clicked += OnFishBookAppButtonClick;
				_ordersAppButton.Clicked += OnOrdersAppButtonClick;
				_upgradeAppButton.Clicked += OnUpgradeAppButtonClick;
				if (_runnerGameAppButton != null) _runnerGameAppButton.transform.parent.gameObject.SetActive(false);
				if (_runnerGameApp != null) _runnerGameApp.gameObject.SetActive(false);
				ConfigureLocalizedAppLabels();
				_mapApp.Initialize();
				_staffApp.Initialize();
				_wallpaperApp.Initialize();
				_fishBookApp.Initialize();
				_inventoryApp.Initialize();
				_ordersApp.Initialize();
				_upgradeApp.Initialize();
				_menuApp.Initialize();
				Singleton<GameUIManager>.Instance.DailyLogScreen.Initialize();
				_appsByType = new Dictionary<TabletAppType, BaseApp>
				{
					{
						TabletAppType.InventoryApp,
						_inventoryApp
					},
					{
						TabletAppType.MapApp,
						_mapApp
					},
					{
						TabletAppType.StaffApp,
						_staffApp
					},
					{
						TabletAppType.MenuApp,
						_menuApp
					},
					{
						TabletAppType.WallpaperApp,
						_wallpaperApp
					},
					{
						TabletAppType.FishBookApp,
						_fishBookApp
					},
					{
						TabletAppType.OrdersApp,
						_ordersApp
					},
					{
						TabletAppType.UpgradeApp,
						_upgradeApp
					}
				};
				_initialized = true;
				NotifyTabletNetworkHandlerUiReady();
			}
		}

		public void RegisterEvents()
		{
			UtilityMethods.Log("[TabletController] RegisterEvents");
			InputHandler inputHandler = Singleton<InputManager>.Instance.InputHandler;
			if (inputHandler != null)
			{
				inputHandler.Map += OnMap;
				inputHandler.Phone += OnPhone;
				inputHandler.CloseTablet += OnCloseTablet;
				inputHandler.CloseMap += OnCloseMap;
				inputHandler.BackTablet += OnBackTablet;
			}
			CozyWeather.Events.onNewMinute += OnNewMinute;
			CozyWeather.Events.onNewDay += OnNewDay;
			OnNewMinute();
			OnNewDay();
		}

		public void UnregisterEvents()
		{
			UtilityMethods.Log("[TabletController] UnregisterEvents");
			_inventoryAppButton.Clicked -= OnInventoryAppButtonClick;
			_mapAppButton.Clicked -= OnMapAppButtonClick;
			_staffAppbutton.Clicked -= OnStaffAppButtonClick;
			_menuAppbutton.Clicked -= OnMenuAppButtonClick;
			_wallpaperAppButton.Clicked -= OnWallpaperAppButtonClick;
			_fishBookAppButton.Clicked -= OnFishBookAppButtonClick;
			_ordersAppButton.Clicked -= OnOrdersAppButtonClick;
			_upgradeAppButton.Clicked -= OnUpgradeAppButtonClick;
			InputHandler inputHandler = Singleton<InputManager>.Instance.InputHandler;
			if (inputHandler != null)
			{
				inputHandler.Map -= OnMap;
				inputHandler.Phone -= OnPhone;
				inputHandler.CloseTablet -= OnCloseTablet;
				inputHandler.CloseMap -= OnCloseMap;
				inputHandler.BackTablet -= OnBackTablet;
			}
			CozyWeather.Events.onNewMinute -= OnNewMinute;
			CozyWeather.Events.onNewDay -= OnNewDay;
		}

		private void OnInventoryAppButtonClick()
		{
			SwitchMenuStatus(isActive: true, TabletAppType.InventoryApp);
		}

		private void OnMapAppButtonClick()
		{
			SwitchMenuStatus(isActive: true, TabletAppType.MapApp);
		}

		private void OnStaffAppButtonClick()
		{
			SwitchMenuStatus(isActive: true, TabletAppType.StaffApp);
		}

		private void OnMenuAppButtonClick()
		{
			SwitchMenuStatus(isActive: true, TabletAppType.MenuApp);
		}

		private void OnWallpaperAppButtonClick()
		{
			SwitchMenuStatus(isActive: true, TabletAppType.WallpaperApp);
		}

		private void OnFishBookAppButtonClick()
		{
			SwitchMenuStatus(isActive: true, TabletAppType.FishBookApp);
		}

		private void OnOrdersAppButtonClick()
		{
			SwitchMenuStatus(isActive: true, TabletAppType.OrdersApp);
		}

		private void OnUpgradeAppButtonClick()
		{
			SwitchMenuStatus(isActive: true, TabletAppType.UpgradeApp);
		}

		private void OnRunnerGameAppButtonClick()
		{
			SwitchMenuStatus(isActive: true, TabletAppType.RunnerGameApp);
		}

		public void SwitchMenuStatus(bool isActive, TabletAppType appType)
		{
			BaseApp value;
			if (!_initialized || _appsByType == null)
			{
				UtilityMethods.Log($"[TabletController] Cannot switch app before initialization. AppType={appType}", LogType.Warning);
			}
			else if (_appsByType.TryGetValue(appType, out value))
			{
				if (_currentActiveApp != null && _currentActiveApp != value)
				{
					_currentActiveApp.Hide();
				}
				value.Show();
				_currentActiveApp = value;
				RefreshHints();
			}
		}

		private void CloseApps()
		{
			if (_appsByType == null)
			{
				return;
			}
			foreach (BaseApp value in _appsByType.Values)
			{
				if (value != null && value.gameObject.activeSelf)
				{
					value.Hide();
				}
			}
		}

		private void OpenTablet()
		{
			if (!_isTabletActive)
			{
				_isTabletActive = true;
				WorldInteractionOverlay.SetStackLabelsVisible(visible: false);
				Singleton<HintsManager>.Instance.ClearAllHints();
				_tabletUI.gameObject.SetActive(value: true);
				_localPlayer.SwitchTo("Tablet", cursorVisible: true);
				_localPlayer.SetTabletFpsStatus(status: true);
				Singleton<GameUIManager>.Instance.TimeUI.SetVisibility(status: false);
				Singleton<QuickSlotHandler>.Instance.SetVisibility(status: false);
				Singleton<GameProgressManager>.Instance.QuestUI.SetVisibility(isVisible: false);
				Singleton<OffscreenIndicatorHandler>.Instance.SetVisibility(s: false);
				_localPlayer.DivingEquipment.SetDepthUIVisibilitySuppressed(suppressed: true);
				EnableHints();
				QuestReferenceHub.Instance.QuestComplete(QuestId.OpenTablet);
			}
		}

		public void CloseTablet()
		{
			if (_isTabletActive)
			{
				_isTabletActive = false;
				WorldInteractionOverlay.SetStackLabelsVisible(visible: true);
				CloseApps();
				_currentActiveApp = null;
				_tabletUI.gameObject.SetActive(value: false);
				_localPlayer.SetTabletFpsStatus(status: false);
				_localPlayer.SwitchTo("Player", cursorVisible: false);
				Singleton<GameUIManager>.Instance.TimeUI.SetVisibility(status: true);
				Singleton<QuickSlotHandler>.Instance.SetVisibility(status: true);
				Singleton<GameProgressManager>.Instance.QuestUI.SetVisibility(isVisible: true);
				Singleton<OffscreenIndicatorHandler>.Instance.SetVisibility(s: true);
				_localPlayer.DivingEquipment.SetDepthUIVisibilitySuppressed(suppressed: false);
				DisableHints();
				_localPlayer.Interactor?.RefreshCurrentHints();
			}
		}

		public void EnableHints()
		{
			Singleton<HintsManager>.Instance.AddHint("CloseTablet", "key_close");
			if (_currentActiveApp is MapApp)
			{
				Singleton<HintsManager>.Instance.AddHint("Zoom", "key_zoom");
			}
		}

		public void DisableHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("BackTablet");
			Singleton<HintsManager>.Instance.RemoveHint("CloseTablet");
			Singleton<HintsManager>.Instance.RemoveHint("Zoom");
		}

		private void RefreshHints()
		{
			if (_isTabletActive)
			{
				DisableHints();
				EnableHints();
			}
		}

		private void NotifyTabletNetworkHandlerUiReady()
		{
			if (ServiceLocator.ForSceneOf(this).TryGet<TabletNetworkHandler>(out var service))
			{
				service.NotifyLocalTabletUiReady();
			}
		}

		private void OnCloseTablet(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				if (_currentActiveApp != null)
				{
					_currentActiveApp.Hide();
					_currentActiveApp = null;
					RefreshHints();
				}
				else
				{
					CloseTablet();
				}
			}
		}

		private void OnCloseMap(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && _currentActiveApp != null && _currentActiveApp is MapApp)
			{
				CloseTablet();
				_currentActiveApp = null;
			}
		}

		private void OnBackTablet(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				if (_currentActiveApp != null)
				{
					_currentActiveApp = null;
				}
				CloseTablet();
			}
		}

		private void OnMap(InputActionPhase phase)
		{
			UtilityMethods.Log("[TabletController] OnMap:" + phase);
			if (TryPrepareTabletOpenFromPlayerInput(phase))
			{
				SwitchMenuStatus(isActive: true, TabletAppType.MapApp);
				OpenTablet();
			}
		}

		private void OnPhone(InputActionPhase phase)
		{
			UtilityMethods.Log("[TabletController] OnPhone:" + phase);
			if (TryPrepareTabletOpenFromPlayerInput(phase))
			{
				OpenTablet();
			}
		}

		private bool TryPrepareTabletOpenFromPlayerInput(InputActionPhase phase)
		{
			if (phase != InputActionPhase.Started || _localPlayer == null || !_localPlayer.CanStartControlAction(PlayerControlAction.OpenTablet))
			{
				return false;
			}
			QuickSlotHandler instance = Singleton<QuickSlotHandler>.Instance;
			if (instance == null)
			{
				return false;
			}
			if (!instance.HeldSlotAvailable && (!_localPlayer.IsToolActive || !(instance.HeldSlotElement != null) || !(instance.HeldSlotElement.HeldItem == null)))
			{
				return false;
			}
			if (_localPlayer.TryDisableActiveToolForModeChange())
			{
				return instance.HeldSlotAvailable;
			}
			return false;
		}

		private void OnNewMinute()
		{
			CozyWeather instance = CozyWeather.instance;
			_timeText.text = $"{instance.timeModule.currentTime.hours:D2}:{instance.timeModule.currentTime.minutes:D2}";
		}

		private void OnNewDay()
		{
			_dayText.text = TabletLocalization.Format("tablet.top_bar.day", "Day {0}", GetDisplayDay());
		}

		private void ConfigureLocalizedAppLabels()
		{
			_inventoryAppButton?.SetLocalizedText("UiTable", "tablet.app.inventory", "Inventory");
			_mapAppButton?.SetLocalizedText("UiTable", "tablet.app.map", "Map");
			_staffAppbutton?.SetLocalizedText("UiTable", "tablet.app.staff", "Staff");
			_menuAppbutton?.SetLocalizedText("UiTable", "tablet.app.menu", "Menu");
			_wallpaperAppButton?.SetLocalizedText("UiTable", "tablet.app.wallpaper", "Wallpaper");
			_fishBookAppButton?.SetLocalizedText("UiTable", "tablet.app.fish_book", "Fish Book");
			_ordersAppButton?.SetLocalizedText("UiTable", "tablet.app.orders", "Orders");
			_upgradeAppButton?.SetLocalizedText("UiTable", "tablet.app.upgrades", "Upgrades");
		}

		private int GetDisplayDay()
		{
			int num = 0;
			CozyWeather instance = CozyWeather.instance;
			if (instance != null && instance.timeModule != null)
			{
				num = instance.timeModule.currentDay;
			}
			int num2 = 0;
			if (Singleton<SaveManager>.Instance != null && Singleton<SaveManager>.Instance.TryGetCurrentSaveSlot(out var data) && data?.GameProgress != null && data.GameProgress.HasSave)
			{
				num2 = data.GameProgress.CurrentDay;
			}
			return Mathf.Max(1, num, num2);
		}
	}
}
