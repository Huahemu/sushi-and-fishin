using System.Collections.Generic;
using Kamgam.SettingsGenerator;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Core.Settings;
using PaperPixelGames.Core.StationTutorial;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Core.UI;
using PaperPixelGames.Core.UI.DailyReportUI;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.States.Game;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace PaperPixelGames.Core.Managers
{
	public class GameUIManager : Singleton<GameUIManager>
	{
		[Header("Game State")]
		[SerializeField]
		private EventSystem _eventSystem;

		[SerializeField]
		private PauseScreen _pauseScreen;

		[SerializeField]
		private DailyLogScreen _dailyLogScreen;

		[SerializeField]
		private InputSystemUIInputModule _uiInputModule;

		[Header("Shops")]
		[SerializeField]
		private ShopApp _greenMarketShop;

		[SerializeField]
		private ShopApp _redMarketShop;

		[SerializeField]
		private ShopApp _blueMarketShop;

		[Header("GlobalStationTutorialPopUpUI")]
		[SerializeField]
		private StationTutorialPopupUI _stationTutorialPopupUI;

		[Header("UI Modules")]
		[SerializeField]
		private TimeUI _timeUI;

		[SerializeField]
		private ProgressionUI _progressionUI;

		[Header("Dialogue Data")]
		[SerializeField]
		private DialoguePanelController _dialoguePanelController;

		[Header("Settings Connections")]
		[SerializeField]
		private ShowHudConnectionSO _showHudConnectionSO;

		[Space]
		[SerializeField]
		private RectTransform _uiGogglesTarget;

		private ShowHudConnection _showHudConnection;

		private bool _showHud = true;

		private bool _bossRoomHudSuppressed;

		private bool _isShopUIActive;

		public override SingletonScope Scope => SingletonScope.Scene;

		public RectTransform UIGogglesTarget => _uiGogglesTarget;

		public TimeUI TimeUI => _timeUI;

		public InputSystemUIInputModule UIInputModule => _uiInputModule;

		public PauseScreen PauseScreen => _pauseScreen;

		public DialoguePanelController DialoguePanelController => _dialoguePanelController;

		public DailyLogScreen DailyLogScreen => _dailyLogScreen;

		public StationTutorialPopupUI StationTutorialPopupUI => _stationTutorialPopupUI;

		public bool IsHudEnabled => _showHud;

		public bool IsShopUIActive
		{
			get
			{
				return _isShopUIActive;
			}
			set
			{
				_isShopUIActive = value;
			}
		}

		public void Initialize()
		{
			BindSettingsListeners();
			_pauseScreen.Initialize();
			_greenMarketShop.Initialize();
			_blueMarketShop.Initialize();
			_dialoguePanelController.Initialize();
		}

		private void OnDestroy()
		{
			UnbindSettingsListeners();
		}

		private void BindSettingsListeners()
		{
			BindShowHudListener();
		}

		private void UnbindSettingsListeners()
		{
			UnbindShowHudListener();
		}

		private void BindShowHudListener()
		{
			_showHud = RuntimeSettings.ShowHudEnabled;
			if (_showHudConnectionSO == null)
			{
				UtilityMethods.Log("[GameUIManager] ShowHudConnectionSO is null.");
				return;
			}
			_showHudConnection = _showHudConnectionSO.GetConnection() as ShowHudConnection;
			if (_showHudConnection == null)
			{
				UtilityMethods.Log("[GameUIManager] Failed to resolve ShowHudConnection.");
				return;
			}
			_showHudConnection.RemoveChangeListener(OnShowHudChanged);
			_showHudConnection.AddChangeListener(OnShowHudChanged);
			_showHud = _showHudConnection.Get();
			UtilityMethods.Log("[GameUIManager] ShowHud listener bound.");
		}

		private void UnbindShowHudListener()
		{
			if (_showHudConnection != null)
			{
				_showHudConnection.RemoveChangeListener(OnShowHudChanged);
				_showHudConnection = null;
			}
		}

		private void OnShowHudChanged(bool showHud)
		{
			_showHud = showHud;
			ApplyShowHudSetting(showHud);
		}

		private void ApplyShowHudSetting(bool showHud)
		{
			_showHud = showHud;
			UtilityMethods.Log($"[GameUIManager] ShowHud applied: {showHud}");
			ApplyHudVisibility();
		}

		public void SetBossRoomHudSuppressed(bool suppressed)
		{
			if (_bossRoomHudSuppressed != suppressed)
			{
				_bossRoomHudSuppressed = suppressed;
				ApplyHudVisibility();
			}
		}

		private void ApplyHudVisibility()
		{
			bool showHud = _showHud;
			bool visibility = _showHud && !_bossRoomHudSuppressed;
			_timeUI?.SetVisibility(visibility);
			ResolveEconomyUI()?.SetVisibility(visibility);
			_progressionUI?.SetVisibility(visibility);
			Singleton<GameProgressManager>.Instance?.QuestUI?.SetVisibility(showHud);
			Singleton<QuickSlotHandler>.Instance?.SetVisibility(showHud);
			Singleton<PlayersManager>.Instance?.LocalPlayer?.SetVisibility(showHud);
		}

		private static EconomyUI ResolveEconomyUI()
		{
			if (!(Singleton<GameProgressManager>.Instance != null) || !(Singleton<GameProgressManager>.Instance.EconomyHandler != null))
			{
				return null;
			}
			return Singleton<GameProgressManager>.Instance.EconomyHandler.EconomyUI;
		}

		public void ShowWholesaleUI(ActorType actorType)
		{
			_isShopUIActive = true;
			switch (actorType)
			{
			case ActorType.GreenMarketNPC:
				_greenMarketShop.Show();
				break;
			case ActorType.BlueMarketNPC:
				_blueMarketShop.Show();
				break;
			case ActorType.RedMarketNPC:
				break;
			}
		}

		public void ShowDialoguePanel(ActorType actorType, ResidentBillboardText billboard, List<string> relatedQuestIds, ResidentController interactedResident)
		{
			_isShopUIActive = true;
			_dialoguePanelController.ShowDialoguePanel(actorType, billboard, relatedQuestIds, interactedResident);
		}

		public bool TryFastForwardResidentDialogue(ResidentController resident)
		{
			if (_dialoguePanelController == null)
			{
				return false;
			}
			return _dialoguePanelController.TryFastForwardForResident(resident);
		}

		public void NotifyResidentBillboardTypingFinished(ResidentController resident)
		{
			if (!(_dialoguePanelController == null))
			{
				_dialoguePanelController.NotifyBillboardTypingFinished(resident);
			}
		}
	}
}
