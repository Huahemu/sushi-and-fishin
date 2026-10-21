using System;
using PaperPixelGames.Core.Data.Tools;
using PaperPixelGames.Core.Economy;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Tablet
{
	public class UpgradeApp : BaseApp
	{
		private enum ButtonMode : byte
		{
			None = 0,
			Locked = 1,
			Upgrade = 2,
			Equip = 3,
			Equipped = 4,
			Max = 5
		}

		[Header("Root")]
		[SerializeField]
		private GameObject _emptyUpgradePage;

		[SerializeField]
		private GameObject _contentRoot;

		[Header("Details")]
		[SerializeField]
		private Image _toolPreviewImage;

		[SerializeField]
		private BaseButton _toolUpgradeButton;

		[SerializeField]
		private TextMeshProUGUI _toolUpgradeButtonText;

		[SerializeField]
		private TextMeshProUGUI _toolUpgradePrice;

		[SerializeField]
		private TextMeshProUGUI _toolUpgradeScrapPrice;

		[SerializeField]
		private TextMeshProUGUI _toolName;

		[SerializeField]
		private TextMeshProUGUI _upgradeCostText;

		[SerializeField]
		private Image _upgradeCostMoneyIcon;

		[SerializeField]
		private Image _upgradeCostScrapIcon;

		[Header("Colors")]
		[SerializeField]
		private Color _normalColor = Color.white;

		[SerializeField]
		private Color _insufficientColor = Color.red;

		[Header("Tool Buttons")]
		[SerializeField]
		private BaseButton _divingSuitButton;

		[SerializeField]
		private BaseButton _harpoonButton;

		[SerializeField]
		private BaseButton _seaScooterButton;

		[SerializeField]
		private BaseButton _fishingRodButton;

		[Header("Level Buttons")]
		[SerializeField]
		private BaseButton _level1Button;

		[SerializeField]
		private BaseButton _level2Button;

		[SerializeField]
		private BaseButton _level3Button;

		[SerializeField]
		private BaseButton _level4Button;

		[SerializeField]
		private BaseButton _level5Button;

		[Header("Data")]
		[SerializeField]
		private DivingEquipmentData _divingEquipmentData;

		[SerializeField]
		private HarpoonData _harpoonData;

		[SerializeField]
		private SeaScooterData _seaScooterData;

		[SerializeField]
		private FishingRodData _fishingRodData;

		[Header("Specs")]
		[SerializeField]
		private TextMeshProUGUI _firstSpecsLine;

		[SerializeField]
		private TextMeshProUGUI _secondSpecsLine;

		[SerializeField]
		private TextMeshProUGUI _thirdSpecsLine;

		private const int MinLevelIndex = 0;

		private const int MaxLevelIndex = 4;

		private const int NoOwnedLevelIndex = -1;

		private ToolType _currentToolType;

		private int _selectedLevelIndex;

		private ButtonMode _currentButtonMode;

		private BaseButton[] _toolButtons;

		private BaseButton[] _levelButtons;

		private bool _eventsBound;

		private bool _economyEventsBound;

		private bool _progressionEventsBound;

		private EconomyHandler _economyHandler;

		private ProgressionManager _progressionManager;

		private int CurrentMoney
		{
			get
			{
				if (!(_economyHandler != null))
				{
					return 0;
				}
				return _economyHandler.ShopBalance.Value;
			}
		}

		private int CurrentScrap
		{
			get
			{
				if (!(_economyHandler != null))
				{
					return 0;
				}
				return _economyHandler.ScrapBalance.Value;
			}
		}

		public override void Initialize()
		{
			base.Initialize();
			if (_divingSuitButton != null)
			{
				_divingSuitButton.gameObject.SetActive(value: true);
			}
			if (_harpoonButton != null)
			{
				_harpoonButton.gameObject.SetActive(value: true);
			}
			if (_seaScooterButton != null)
			{
				_seaScooterButton.gameObject.SetActive(value: true);
			}
			if (_fishingRodButton != null)
			{
				_fishingRodButton.gameObject.SetActive(value: true);
			}
			if (_contentRoot != null)
			{
				_contentRoot.SetActive(value: true);
			}
			if (_emptyUpgradePage != null)
			{
				_emptyUpgradePage.SetActive(value: false);
			}
			CacheButtonGroups();
			BindEvents();
			RefreshApp();
		}

		private void OnDestroy()
		{
			UnbindProgressionEvents();
			UnbindEconomyEvents();
			UnbindEvents();
		}

		public override void Show()
		{
			base.Show();
			CacheReferences();
			BindEconomyEvents();
			BindProgressionEvents();
			RefreshApp();
		}

		private void CacheReferences()
		{
			if (_economyHandler != null)
			{
				return;
			}
			if (Singleton<GameProgressManager>.Instance != null && Singleton<GameProgressManager>.Instance.EconomyHandler != null)
			{
				_economyHandler = Singleton<GameProgressManager>.Instance.EconomyHandler;
				return;
			}
			try
			{
				ServiceLocator.ForSceneOf(this).Get<EconomyHandler>(out _economyHandler);
			}
			catch (ArgumentException)
			{
				_economyHandler = null;
			}
			if (_economyHandler == null)
			{
				UtilityMethods.Log("[UpgradeApp] EconomyHandler reference could not be found. Make sure EconomyHandler is assigned in GameProgressManager or registered to ServiceLocator before UpgradeApp.Initialize().", LogType.Error);
			}
		}

		private void BindEvents()
		{
			if (!_eventsBound)
			{
				_eventsBound = true;
				if (_divingSuitButton != null)
				{
					_divingSuitButton.Clicked += OnDivingSuitButtonClicked;
				}
				if (_harpoonButton != null)
				{
					_harpoonButton.Clicked += OnHarpoonButtonClicked;
				}
				if (_seaScooterButton != null)
				{
					_seaScooterButton.Clicked += OnSeaScooterButtonClicked;
				}
				if (_fishingRodButton != null)
				{
					_fishingRodButton.Clicked += OnFishingRodButtonClicked;
				}
				if (_level1Button != null)
				{
					_level1Button.Clicked += OnLevel1ButtonClicked;
				}
				if (_level2Button != null)
				{
					_level2Button.Clicked += OnLevel2ButtonClicked;
				}
				if (_level3Button != null)
				{
					_level3Button.Clicked += OnLevel3ButtonClicked;
				}
				if (_level4Button != null)
				{
					_level4Button.Clicked += OnLevel4ButtonClicked;
				}
				if (_level5Button != null)
				{
					_level5Button.Clicked += OnLevel5ButtonClicked;
				}
				if (_toolUpgradeButton != null)
				{
					_toolUpgradeButton.Clicked += OnToolUpgradeButtonClick;
				}
			}
		}

		private void UnbindEvents()
		{
			if (_eventsBound)
			{
				_eventsBound = false;
				if (_toolUpgradeButton != null)
				{
					_toolUpgradeButton.Clicked -= OnToolUpgradeButtonClick;
				}
				if (_divingSuitButton != null)
				{
					_divingSuitButton.Clicked -= OnDivingSuitButtonClicked;
				}
				if (_harpoonButton != null)
				{
					_harpoonButton.Clicked -= OnHarpoonButtonClicked;
				}
				if (_seaScooterButton != null)
				{
					_seaScooterButton.Clicked -= OnSeaScooterButtonClicked;
				}
				if (_fishingRodButton != null)
				{
					_fishingRodButton.Clicked -= OnFishingRodButtonClicked;
				}
				if (_level1Button != null)
				{
					_level1Button.Clicked -= OnLevel1ButtonClicked;
				}
				if (_level2Button != null)
				{
					_level2Button.Clicked -= OnLevel2ButtonClicked;
				}
				if (_level3Button != null)
				{
					_level3Button.Clicked -= OnLevel3ButtonClicked;
				}
				if (_level4Button != null)
				{
					_level4Button.Clicked -= OnLevel4ButtonClicked;
				}
				if (_level5Button != null)
				{
					_level5Button.Clicked -= OnLevel5ButtonClicked;
				}
			}
		}

		private void BindEconomyEvents()
		{
			CacheReferences();
			if (!_economyEventsBound && !(_economyHandler == null))
			{
				NetworkVariable<int> shopBalance = _economyHandler.ShopBalance;
				shopBalance.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Combine(shopBalance.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnShopBalanceChanged));
				NetworkVariable<int> scrapBalance = _economyHandler.ScrapBalance;
				scrapBalance.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Combine(scrapBalance.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnScrapBalanceChanged));
				_economyEventsBound = true;
			}
		}

		private void UnbindEconomyEvents()
		{
			if (_economyEventsBound && !(_economyHandler == null))
			{
				NetworkVariable<int> shopBalance = _economyHandler.ShopBalance;
				shopBalance.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Remove(shopBalance.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnShopBalanceChanged));
				NetworkVariable<int> scrapBalance = _economyHandler.ScrapBalance;
				scrapBalance.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Remove(scrapBalance.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnScrapBalanceChanged));
				_economyEventsBound = false;
			}
		}

		private void OnShopBalanceChanged(int previousValue, int newValue)
		{
			RefreshSelectedToolUI();
		}

		private void OnScrapBalanceChanged(int previousValue, int newValue)
		{
			RefreshSelectedToolUI();
		}

		private void BindProgressionEvents()
		{
			if (!_progressionEventsBound)
			{
				_progressionManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.ProgressionManager : null);
				if (!(_progressionManager == null))
				{
					_progressionManager.ProgressionChanged += OnProgressionChanged;
					_progressionEventsBound = true;
				}
			}
		}

		private void UnbindProgressionEvents()
		{
			if (_progressionEventsBound)
			{
				if (_progressionManager != null)
				{
					_progressionManager.ProgressionChanged -= OnProgressionChanged;
				}
				_progressionEventsBound = false;
				_progressionManager = null;
			}
		}

		private void OnProgressionChanged(int level, double currentXp, double nextLevelRequiredTotalXp)
		{
			RefreshSelectedToolUI();
		}

		private void OnDivingSuitButtonClicked()
		{
			SelectTool(ToolType.DivingEquipment);
		}

		private void OnHarpoonButtonClicked()
		{
			SelectTool(ToolType.Harpoon);
		}

		private void OnSeaScooterButtonClicked()
		{
			SelectTool(ToolType.SeaScooter);
		}

		private void OnFishingRodButtonClicked()
		{
			SelectTool(ToolType.FishingRod);
		}

		private void OnLevel1ButtonClicked()
		{
			OnLevelButtonClicked(1);
		}

		private void OnLevel2ButtonClicked()
		{
			OnLevelButtonClicked(2);
		}

		private void OnLevel3ButtonClicked()
		{
			OnLevelButtonClicked(3);
		}

		private void OnLevel4ButtonClicked()
		{
			OnLevelButtonClicked(4);
		}

		private void OnLevel5ButtonClicked()
		{
			OnLevelButtonClicked(5);
		}

		private void RefreshApp()
		{
			RefreshUnlockedTools();
			if (!(_contentRoot == null) && _contentRoot.activeSelf)
			{
				EnsureCurrentToolIsValid();
				RefreshSelectedToolUI();
			}
		}

		private void RefreshUnlockedTools()
		{
			if (Singleton<PlayersManager>.Instance?.LocalPlayer == null)
			{
				if (_contentRoot != null)
				{
					_contentRoot.SetActive(value: false);
				}
				if (_emptyUpgradePage != null)
				{
					_emptyUpgradePage.SetActive(value: true);
				}
				_currentToolType = ToolType.Empty;
				_selectedLevelIndex = 0;
				ClearAllSelectionHighlights();
				ClearDetails();
			}
			else
			{
				if (_contentRoot != null)
				{
					_contentRoot.SetActive(value: true);
				}
				if (_emptyUpgradePage != null)
				{
					_emptyUpgradePage.SetActive(value: false);
				}
				RefreshToolButtonVisibility();
				RefreshSelectionHighlights();
			}
		}

		private void EnsureCurrentToolIsValid()
		{
			if (!IsCurrentToolSelectionValid())
			{
				if (IsToolUnlocked(ToolType.DivingEquipment))
				{
					SelectTool(ToolType.DivingEquipment);
				}
				else if (IsToolUnlocked(ToolType.Harpoon))
				{
					SelectTool(ToolType.Harpoon);
				}
				else if (IsToolUnlocked(ToolType.SeaScooter))
				{
					SelectTool(ToolType.SeaScooter);
				}
				else if (IsToolUnlocked(ToolType.FishingRod))
				{
					SelectTool(ToolType.FishingRod);
				}
				else
				{
					SelectTool(ToolType.SeaScooter);
				}
			}
		}

		private void RefreshToolButtonVisibility()
		{
			if (_divingSuitButton != null)
			{
				_divingSuitButton.gameObject.SetActive(IsToolUnlocked(ToolType.DivingEquipment));
			}
			if (_harpoonButton != null)
			{
				_harpoonButton.gameObject.SetActive(IsToolUnlocked(ToolType.Harpoon));
			}
			if (_seaScooterButton != null)
			{
				_seaScooterButton.gameObject.SetActive(value: true);
			}
			if (_fishingRodButton != null)
			{
				_fishingRodButton.gameObject.SetActive(value: true);
			}
		}

		private bool IsCurrentToolSelectionValid()
		{
			if (_currentToolType == ToolType.DivingEquipment || _currentToolType == ToolType.Harpoon)
			{
				return IsToolUnlocked(_currentToolType);
			}
			return IsUpgradeableTool(_currentToolType);
		}

		private void SelectTool(ToolType toolType)
		{
			_currentToolType = toolType;
			int equippedLevel = GetEquippedLevel(toolType);
			int ownedLevel = GetOwnedLevel(toolType);
			_selectedLevelIndex = ((ownedLevel >= 0) ? Mathf.Clamp(equippedLevel, 0, ownedLevel) : 0);
			_selectedLevelIndex = ClampLevelIndex(_selectedLevelIndex);
			RefreshSelectedToolUI();
		}

		private void OnLevelButtonClicked(int displayLevel)
		{
			_selectedLevelIndex = ClampLevelIndex(displayLevel - 1);
			RefreshSelectedToolUI();
		}

		private void RefreshSelectedToolUI()
		{
			if (_currentToolType == ToolType.Empty)
			{
				ClearDetails();
				ClearAllSelectionHighlights();
			}
			else
			{
				RefreshPreview();
				RefreshActionButton();
				RefreshSelectionHighlights();
			}
		}

		private void ClearDetails()
		{
			if (_toolPreviewImage != null)
			{
				_toolPreviewImage.sprite = null;
			}
			if (_toolName != null)
			{
				_toolName.text = "";
			}
			if (_toolUpgradeButton != null)
			{
				_toolUpgradeButton.interactable = false;
			}
			if (_toolUpgradeButtonText != null)
			{
				_toolUpgradeButtonText.text = "";
			}
			if (_toolUpgradePrice != null)
			{
				_toolUpgradePrice.text = "";
				_toolUpgradePrice.color = _normalColor;
			}
			if (_toolUpgradeScrapPrice != null)
			{
				_toolUpgradeScrapPrice.text = "";
				_toolUpgradeScrapPrice.color = _normalColor;
			}
			if (_upgradeCostText != null)
			{
				_upgradeCostText.color = _normalColor;
				_upgradeCostText.gameObject.SetActive(value: false);
			}
			if (_upgradeCostMoneyIcon != null)
			{
				_upgradeCostMoneyIcon.gameObject.SetActive(value: false);
			}
			if (_upgradeCostScrapIcon != null)
			{
				_upgradeCostScrapIcon.gameObject.SetActive(value: false);
			}
			if (_firstSpecsLine != null)
			{
				_firstSpecsLine.text = "";
			}
			if (_secondSpecsLine != null)
			{
				_secondSpecsLine.text = "";
			}
			if (_thirdSpecsLine != null)
			{
				_thirdSpecsLine.text = "";
			}
		}

		private void RefreshPreview()
		{
			int selectedLevelIndex = GetSelectedLevelIndex();
			switch (_currentToolType)
			{
			case ToolType.DivingEquipment:
				if (IsValidLevelData(_divingEquipmentData, selectedLevelIndex))
				{
					if (_toolName != null)
					{
						_toolName.text = GetLocalizedToolLevelName(_currentToolType, selectedLevelIndex);
					}
					if (_toolPreviewImage != null)
					{
						_toolPreviewImage.sprite = _divingEquipmentData.Levels[selectedLevelIndex].Sprite;
					}
				}
				break;
			case ToolType.Harpoon:
				if (IsValidLevelData(_harpoonData, selectedLevelIndex))
				{
					if (_toolName != null)
					{
						_toolName.text = GetLocalizedToolLevelName(_currentToolType, selectedLevelIndex);
					}
					if (_toolPreviewImage != null)
					{
						_toolPreviewImage.sprite = _harpoonData.Levels[selectedLevelIndex].Sprite;
					}
				}
				break;
			case ToolType.SeaScooter:
				if (IsValidLevelData(_seaScooterData, selectedLevelIndex))
				{
					if (_toolName != null)
					{
						_toolName.text = GetLocalizedToolLevelName(_currentToolType, selectedLevelIndex);
					}
					if (_toolPreviewImage != null)
					{
						_toolPreviewImage.sprite = _seaScooterData.Levels[selectedLevelIndex].Sprite;
					}
				}
				break;
			case ToolType.FishingRod:
				if (IsValidLevelData(_fishingRodData, selectedLevelIndex))
				{
					if (_toolName != null)
					{
						_toolName.text = GetLocalizedToolLevelName(_currentToolType, selectedLevelIndex);
					}
					if (_toolPreviewImage != null)
					{
						_toolPreviewImage.sprite = _fishingRodData.Levels[selectedLevelIndex].Sprite;
					}
				}
				break;
			case ToolType.Mop:
				break;
			}
		}

		private void RefreshActionButton()
		{
			int selectedLevelIndex = GetSelectedLevelIndex();
			int ownedLevel = GetOwnedLevel(_currentToolType);
			int equippedLevel = GetEquippedLevel(_currentToolType);
			ResetActionButtonVisuals();
			RefreshSpecs(selectedLevelIndex);
			if (ownedLevel >= 4 && selectedLevelIndex >= 4)
			{
				if (selectedLevelIndex == equippedLevel)
				{
					SetEquippedState();
				}
				else
				{
					SetEquipState();
				}
			}
			else if (selectedLevelIndex <= ownedLevel)
			{
				if (selectedLevelIndex == equippedLevel)
				{
					SetEquippedState();
				}
				else
				{
					SetEquipState();
				}
			}
			else if (selectedLevelIndex == ownedLevel + 1)
			{
				SetUpgradeState(selectedLevelIndex);
			}
			else
			{
				SetLockedState();
			}
		}

		private void ResetActionButtonVisuals()
		{
			_currentButtonMode = ButtonMode.None;
			if (_toolUpgradeButton != null)
			{
				_toolUpgradeButton.interactable = false;
			}
			if (_toolUpgradePrice != null)
			{
				_toolUpgradePrice.text = "";
				_toolUpgradePrice.color = _normalColor;
			}
			if (_toolUpgradeScrapPrice != null)
			{
				_toolUpgradeScrapPrice.text = "";
				_toolUpgradeScrapPrice.color = _normalColor;
			}
			if (_toolUpgradeButtonText != null)
			{
				_toolUpgradeButtonText.text = "";
			}
			if (_upgradeCostText != null)
			{
				_upgradeCostText.color = _normalColor;
				_upgradeCostText.gameObject.SetActive(value: false);
			}
			if (_upgradeCostMoneyIcon != null)
			{
				_upgradeCostMoneyIcon.gameObject.SetActive(value: false);
			}
			if (_upgradeCostScrapIcon != null)
			{
				_upgradeCostScrapIcon.gameObject.SetActive(value: false);
			}
		}

		private void SetEquippedState()
		{
			_currentButtonMode = ButtonMode.Equipped;
			if (_toolUpgradeButtonText != null)
			{
				_toolUpgradeButtonText.text = TabletLocalization.Get("tablet.upgrade.equipped", "Equipped");
			}
			if (_toolUpgradeButton != null)
			{
				_toolUpgradeButton.interactable = false;
			}
		}

		private void SetEquipState()
		{
			_currentButtonMode = ButtonMode.Equip;
			if (_toolUpgradeButtonText != null)
			{
				_toolUpgradeButtonText.text = TabletLocalization.Get("tablet.upgrade.equip", "Equip");
			}
			if (_toolUpgradeButton != null)
			{
				_toolUpgradeButton.interactable = true;
			}
		}

		private void SetUpgradeState(int targetLevelIndex)
		{
			_currentButtonMode = ButtonMode.Upgrade;
			int upgradeMoneyCost = GetUpgradeMoneyCost(_currentToolType, targetLevelIndex);
			int upgradeScrapCost = GetUpgradeScrapCost(_currentToolType, targetLevelIndex);
			if (_toolUpgradePrice != null)
			{
				_toolUpgradePrice.text = upgradeMoneyCost.ToString();
			}
			if (_toolUpgradeScrapPrice != null)
			{
				_toolUpgradeScrapPrice.text = upgradeScrapCost.ToString();
			}
			if (_upgradeCostMoneyIcon != null)
			{
				_upgradeCostMoneyIcon.gameObject.SetActive(value: true);
			}
			if (_upgradeCostScrapIcon != null)
			{
				_upgradeCostScrapIcon.gameObject.SetActive(value: true);
			}
			if (_upgradeCostText != null)
			{
				_upgradeCostText.gameObject.SetActive(value: true);
			}
			if (!IsLevelConditionMet(_currentToolType, targetLevelIndex, out var _))
			{
				if (_toolUpgradeButtonText != null)
				{
					_toolUpgradeButtonText.text = TabletLocalization.Get("tablet.upgrade.locked", "Locked");
				}
				if (_toolUpgradeButton != null)
				{
					_toolUpgradeButton.interactable = false;
				}
				return;
			}
			bool interactable = EvaluateCostVisuals(upgradeMoneyCost, upgradeScrapCost);
			if (_toolUpgradeButtonText != null)
			{
				_toolUpgradeButtonText.text = (IsUnlockTarget(targetLevelIndex) ? TabletLocalization.Get("tablet.upgrade.unlock", "Unlock") : TabletLocalization.Get("tablet.upgrade.upgrade", "Upgrade"));
			}
			if (_toolUpgradeButton != null)
			{
				_toolUpgradeButton.interactable = interactable;
			}
		}

		private void SetLockedState()
		{
			_currentButtonMode = ButtonMode.Locked;
			if (_toolUpgradeButtonText != null)
			{
				_toolUpgradeButtonText.text = TabletLocalization.Get("tablet.upgrade.locked", "Locked");
			}
			if (_toolUpgradeButton != null)
			{
				_toolUpgradeButton.interactable = false;
			}
		}

		private bool EvaluateCostVisuals(int moneyCost, int scrapCost)
		{
			CacheReferences();
			int num = CurrentMoney / 100;
			int num2 = CurrentScrap / 100;
			bool flag = num >= moneyCost;
			bool flag2 = num2 >= scrapCost;
			bool flag3 = flag & flag2;
			if (_toolUpgradePrice != null)
			{
				_toolUpgradePrice.color = (flag ? _normalColor : _insufficientColor);
			}
			if (_toolUpgradeScrapPrice != null)
			{
				_toolUpgradeScrapPrice.color = (flag2 ? _normalColor : _insufficientColor);
			}
			if (_upgradeCostText != null)
			{
				_upgradeCostText.color = (flag3 ? _normalColor : _insufficientColor);
			}
			return flag3;
		}

		private void OnToolUpgradeButtonClick()
		{
			if (_currentToolType != ToolType.Empty)
			{
				switch (_currentButtonMode)
				{
				case ButtonMode.Equip:
					EquipSelectedLevel();
					break;
				case ButtonMode.Upgrade:
					UpgradeSelectedLevel();
					break;
				}
				RefreshSelectedToolUI();
			}
		}

		private void EquipSelectedLevel()
		{
			if (!IsToolUnlocked(_currentToolType))
			{
				Singleton<NotifyManager>.Instance.ShowToast(TabletLocalization.Get("tablet.upgrade.error.upgrade_first", "Upgrade this tool first."), NotificationState.Error);
				RefreshSelectedToolUI();
				return;
			}
			PPG_Player localPlayer = Singleton<PlayersManager>.Instance.LocalPlayer;
			if (localPlayer == null)
			{
				Singleton<NotifyManager>.Instance.ShowToast(TabletLocalization.Get("tablet.upgrade.error.player_not_found", "Player not found."), NotificationState.Error);
				return;
			}
			byte selectedLevelAsByte = GetSelectedLevelAsByte();
			localPlayer.EquipTool(_currentToolType, selectedLevelAsByte);
			Singleton<NotifyManager>.Instance.ShowToolEquipped(_currentToolType, selectedLevelAsByte + 1);
			UtilityMethods.Log("<b>[UpgradeApp]</b> Equip requested.\n" + $"    Tool: {_currentToolType}\n" + $"    LevelIndex: {selectedLevelAsByte}\n" + $"    DisplayLevel: {selectedLevelAsByte + 1}");
			_currentButtonMode = ButtonMode.Equipped;
			if (_toolUpgradeButtonText != null)
			{
				_toolUpgradeButtonText.text = TabletLocalization.Get("tablet.upgrade.equipped", "Equipped");
			}
			if (_toolUpgradeButton != null)
			{
				_toolUpgradeButton.interactable = false;
			}
		}

		private string GetToolDisplayName(ToolType toolType)
		{
			return toolType switch
			{
				ToolType.DivingEquipment => TabletLocalization.Get("tablet.upgrade.tool.diving_suit", "Diving Suit"), 
				ToolType.Harpoon => TabletLocalization.Get("tablet.upgrade.tool.harpoon", "Harpoon"), 
				ToolType.SeaScooter => TabletLocalization.Get("tablet.upgrade.tool.sea_scooter", "Sea Scooter"), 
				ToolType.FishingRod => TabletLocalization.Get("tablet.upgrade.tool.fishing_rod", "Fishing Rod"), 
				_ => TabletLocalization.Get("tablet.upgrade.tool.generic", "Tool"), 
			};
		}

		private string GetLocalizedToolLevelName(ToolType toolType, int levelIndex)
		{
			return TabletLocalization.Format("tablet.upgrade.tool_level", "{0} v{1}", GetToolDisplayName(toolType), levelIndex + 1);
		}

		private void UpgradeSelectedLevel()
		{
			CacheReferences();
			if (_economyHandler == null)
			{
				UtilityMethods.Log("<b>[UpgradeApp]</b> EconomyHandler is null.");
				return;
			}
			int selectedLevelIndex = GetSelectedLevelIndex();
			int ownedLevel = GetOwnedLevel(_currentToolType);
			if (selectedLevelIndex != ownedLevel + 1)
			{
				Singleton<NotifyManager>.Instance.ShowToast(TabletLocalization.Get("tablet.upgrade.error.previous_level", "Upgrade previous level first."), NotificationState.Error);
				UtilityMethods.Log("<b>[UpgradeApp]</b> Invalid upgrade target.\n" + $"    Tool: {_currentToolType}\n" + $"    OwnedLevelIndex: {ownedLevel}\n" + $"    TargetLevelIndex: {selectedLevelIndex}");
				RefreshSelectedToolUI();
				return;
			}
			if (!IsLevelConditionMet(_currentToolType, selectedLevelIndex, out var unmetCondition))
			{
				Singleton<NotifyManager>.Instance.ShowToast(unmetCondition, NotificationState.Error);
				UtilityMethods.Log("<b>[UpgradeApp]</b> Upgrade blocked by progression condition.\n" + $"    Tool: {_currentToolType}\n" + $"    TargetLevelIndex: {selectedLevelIndex}\n" + "    Condition: " + unmetCondition);
				RefreshSelectedToolUI();
				return;
			}
			int upgradeMoneyCost = GetUpgradeMoneyCost(_currentToolType, selectedLevelIndex);
			int upgradeScrapCost = GetUpgradeScrapCost(_currentToolType, selectedLevelIndex);
			if (!EvaluateCostVisuals(upgradeMoneyCost, upgradeScrapCost))
			{
				bool flag = CurrentMoney / 100 < upgradeMoneyCost;
				bool flag2 = CurrentScrap / 100 < upgradeScrapCost;
				if (flag & flag2)
				{
					Singleton<NotifyManager>.Instance.ShowToast(TabletLocalization.Get("tablet.upgrade.error.money_and_scrap", "Not enough money and scrap."), NotificationState.Error);
				}
				else if (flag)
				{
					Singleton<NotifyManager>.Instance.ShowToast(TabletLocalization.Get("tablet.upgrade.error.money", "Not enough money."), NotificationState.Error);
				}
				else if (flag2)
				{
					Singleton<NotifyManager>.Instance.ShowToast(TabletLocalization.Get("tablet.upgrade.error.scrap", "Not enough scrap."), NotificationState.Error);
				}
				return;
			}
			if (!_economyHandler.HasSufficientBalance(upgradeMoneyCost))
			{
				UtilityMethods.Log("<b>[UpgradeApp]</b> Not enough shop balance from EconomyHandler.\n" + $"    CurrentMoney: {CurrentMoney}\n" + $"    RequiredMoney: {upgradeMoneyCost}");
				EvaluateCostVisuals(upgradeMoneyCost, upgradeScrapCost);
				return;
			}
			if (!_economyHandler.HasSufficientScrap(upgradeScrapCost))
			{
				UtilityMethods.Log("<b>[UpgradeApp]</b> Not enough scrap from EconomyHandler.\n" + $"    CurrentScrap: {CurrentScrap}\n" + $"    RequiredScrap: {upgradeScrapCost}");
				EvaluateCostVisuals(upgradeMoneyCost, upgradeScrapCost);
				return;
			}
			_economyHandler.SpendMoney(upgradeMoneyCost);
			_economyHandler.SpendScrap(upgradeScrapCost);
			ServiceLocator.ForSceneOf(this).Get<RpcRelay>(out var service);
			if (service == null)
			{
				UtilityMethods.Log("<b>[UpgradeApp]</b> RpcRelay is null.");
				return;
			}
			service.ServerUpgradeRpc(_currentToolType, GetSelectedLevelAsByte());
			UtilityMethods.Log("<b>[UpgradeApp]</b> Upgrade requested.\n" + $"    Tool: {_currentToolType}\n" + $"    TargetLevelIndex: {selectedLevelIndex}\n" + $"    DisplayLevel: {selectedLevelIndex + 1}\n" + $"    MoneyCost: {upgradeMoneyCost}\n" + $"    ScrapCost: {upgradeScrapCost}");
			CompleteUpgradeQuestIfNeeded(_currentToolType, selectedLevelIndex);
		}

		private void CompleteUpgradeQuestIfNeeded(ToolType toolType, int targetLevelIndex)
		{
			switch (toolType)
			{
			case ToolType.DivingEquipment:
				switch (targetLevelIndex)
				{
				case 2:
					QuestReferenceHub.Instance.QuestComplete(QuestId.UpgradeDivingSuitToLevelThree);
					break;
				case 3:
					QuestReferenceHub.Instance.QuestComplete(QuestId.UpgradeDivingSuitToLevelFour);
					break;
				case 4:
					QuestReferenceHub.Instance.QuestComplete(QuestId.UpgradeDivingSuitToLevelFive);
					break;
				}
				break;
			case ToolType.Harpoon:
				switch (targetLevelIndex)
				{
				case 1:
					QuestReferenceHub.Instance.QuestComplete(QuestId.UpgradeHarpoonToLevelTwo);
					break;
				case 2:
					QuestReferenceHub.Instance.QuestComplete(QuestId.UpgradeHarpoonToLevelThree);
					break;
				}
				break;
			}
		}

		private int GetOwnedLevel(ToolType toolType)
		{
			PPG_Player pPG_Player = Singleton<PlayersManager>.Instance?.LocalPlayer;
			if (pPG_Player == null)
			{
				return -1;
			}
			ToolDataNet toolsData = pPG_Player.ToolsData;
			if (!IsToolUnlocked(toolType))
			{
				return -1;
			}
			return ClampLevelIndex(toolType switch
			{
				ToolType.DivingEquipment => toolsData.DivingEquipmentLevel, 
				ToolType.Harpoon => toolsData.HarpoonLevel, 
				ToolType.SeaScooter => toolsData.ScooterLevel, 
				ToolType.FishingRod => toolsData.FishingRodLevel, 
				_ => -1, 
			});
		}

		private int GetEquippedLevel(ToolType toolType)
		{
			PPG_Player pPG_Player = Singleton<PlayersManager>.Instance?.LocalPlayer;
			if (pPG_Player == null)
			{
				return 0;
			}
			return ClampLevelIndex(pPG_Player.GetEquipedToolLevel(toolType));
		}

		private int GetUpgradeMoneyCost(ToolType toolType, int levelIndex)
		{
			levelIndex = ClampLevelIndex(levelIndex);
			return toolType switch
			{
				ToolType.DivingEquipment => _divingEquipmentData.Levels[levelIndex].MoneyCost, 
				ToolType.Harpoon => _harpoonData.Levels[levelIndex].MoneyCost, 
				ToolType.SeaScooter => _seaScooterData.Levels[levelIndex].MoneyCost, 
				ToolType.FishingRod => _fishingRodData.Levels[levelIndex].MoneyCost, 
				_ => 0, 
			};
		}

		private int GetUpgradeScrapCost(ToolType toolType, int levelIndex)
		{
			levelIndex = ClampLevelIndex(levelIndex);
			return toolType switch
			{
				ToolType.DivingEquipment => _divingEquipmentData.Levels[levelIndex].ScrapCost, 
				ToolType.Harpoon => _harpoonData.Levels[levelIndex].ScrapCost, 
				ToolType.SeaScooter => _seaScooterData.Levels[levelIndex].ScrapCost, 
				ToolType.FishingRod => _fishingRodData.Levels[levelIndex].ScrapCost, 
				_ => 0, 
			};
		}

		private ProgressionUnlockKey GetLevelCondition(ToolType toolType, int levelIndex)
		{
			BaseLevelData levelData = GetLevelData(toolType, levelIndex);
			if (!(levelData != null))
			{
				return ProgressionUnlockKey.None;
			}
			return levelData.LevelCondition;
		}

		private bool IsLevelConditionMet(ToolType toolType, int levelIndex, out string unmetCondition)
		{
			unmetCondition = null;
			BaseLevelData levelData = GetLevelData(toolType, levelIndex);
			if (levelData == null)
			{
				return true;
			}
			int num = Mathf.Max(0, levelData.RequiredRestaurantLevel);
			if (num > 0)
			{
				ProgressionManager progressionManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.ProgressionManager : null);
				if (((progressionManager != null) ? progressionManager.CurrentLevel : num) < num)
				{
					unmetCondition = TabletLocalization.Format("tablet.upgrade.requires_restaurant_level", "Requires restaurant level {0}.", num);
					return false;
				}
			}
			ProgressionUnlockKey levelCondition = levelData.LevelCondition;
			if (levelCondition == ProgressionUnlockKey.None)
			{
				return true;
			}
			int num2;
			if (!(Singleton<GameProgressManager>.Instance == null))
			{
				num2 = (Singleton<GameProgressManager>.Instance.IsProgressionUnlocked(levelCondition) ? 1 : 0);
				if (num2 == 0)
				{
					unmetCondition = TabletLocalization.Get("tablet.upgrade.error.required_restaurant_level", "Reach the required restaurant level first.");
				}
			}
			else
			{
				num2 = 1;
			}
			return (byte)num2 != 0;
		}

		private BaseLevelData GetLevelData(ToolType toolType, int levelIndex)
		{
			levelIndex = ClampLevelIndex(levelIndex);
			return toolType switch
			{
				ToolType.DivingEquipment => IsValidLevelData(_divingEquipmentData, levelIndex) ? _divingEquipmentData.Levels[levelIndex] : null, 
				ToolType.Harpoon => IsValidLevelData(_harpoonData, levelIndex) ? _harpoonData.Levels[levelIndex] : null, 
				ToolType.SeaScooter => IsValidLevelData(_seaScooterData, levelIndex) ? _seaScooterData.Levels[levelIndex] : null, 
				ToolType.FishingRod => IsValidLevelData(_fishingRodData, levelIndex) ? _fishingRodData.Levels[levelIndex] : null, 
				_ => null, 
			};
		}

		private void RefreshSpecs(int levelIndex)
		{
			string[] specs = GetSpecs(_currentToolType, levelIndex);
			if (_firstSpecsLine != null)
			{
				_firstSpecsLine.text = specs[0];
			}
			if (_secondSpecsLine != null)
			{
				_secondSpecsLine.text = specs[1];
			}
			if (_thirdSpecsLine != null)
			{
				_thirdSpecsLine.text = specs[2];
			}
		}

		private string[] GetSpecs(ToolType toolType, int levelIndex)
		{
			levelIndex = ClampLevelIndex(levelIndex);
			return toolType switch
			{
				ToolType.DivingEquipment => new string[3]
				{
					TabletLocalization.Format("tablet.upgrade.spec.max_oxygen", "Max Oxygen: {0}", _divingEquipmentData.Levels[levelIndex].MaxOxygen),
					TabletLocalization.Format("tablet.upgrade.spec.oxygen_drain", "Base Drain Per Second: {0}", _divingEquipmentData.Levels[levelIndex].BaseDrainPerSec),
					TabletLocalization.Format("tablet.upgrade.spec.depth_border", "Depth Border: {0}", _divingEquipmentData.Levels[levelIndex].DepthPenaltyStart)
				}, 
				ToolType.Harpoon => new string[3]
				{
					TabletLocalization.Format("tablet.upgrade.spec.shoot_range", "Shoot Range: {0}", _harpoonData.Levels[levelIndex].ShootRange),
					TabletLocalization.Format("tablet.upgrade.spec.shoot_speed", "Shoot Speed: {0}", _harpoonData.Levels[levelIndex].ShootSpeed),
					TabletLocalization.Format("tablet.upgrade.spec.harpoon_force", "Harpoon Force: {0}", _harpoonData.Levels[levelIndex].HarpoonForce)
				}, 
				ToolType.SeaScooter => new string[3]
				{
					TabletLocalization.Format("tablet.upgrade.spec.speed", "Speed: {0}", _seaScooterData.Levels[levelIndex].MaxSeaScooterSpeed),
					TabletLocalization.Format("tablet.upgrade.spec.handling", "Handling: {0}", _seaScooterData.Levels[levelIndex].BrakingDecelerationSeaScooter),
					TabletLocalization.Format("tablet.upgrade.spec.stabilization", "Stabilization: {0}", _seaScooterData.Levels[levelIndex].SeaScooterFriction)
				}, 
				ToolType.FishingRod => GetFishingRodSpecs(levelIndex), 
				_ => new string[3]
				{
					string.Empty,
					string.Empty,
					string.Empty
				}, 
			};
		}

		private string[] GetFishingRodSpecs(int levelIndex)
		{
			FishingRodLevelData fishingRodLevelData = _fishingRodData.Levels[levelIndex];
			RodDifficultyMatchup matchup = fishingRodLevelData.GetMatchup(FishingRodDifficulty.Level5);
			int num = Mathf.RoundToInt(matchup.CatchProgressMultiplier * 100f);
			int num2 = Mathf.RoundToInt(100f / Mathf.Max(0.01f, matchup.TensionGainMultiplier));
			return new string[3]
			{
				TabletLocalization.Format("tablet.upgrade.spec.cast_distance", "Cast Distance: {0:0.#}m", fishingRodLevelData.MaxCastDistance),
				TabletLocalization.Format("tablet.upgrade.spec.catch_power", "Hard Fish Catch Power: {0}%", num),
				TabletLocalization.Format("tablet.upgrade.spec.tension_control", "Hard Fish Tension Control: {0}%", num2)
			};
		}

		private bool IsToolUnlocked(ToolType toolType)
		{
			PPG_Player pPG_Player = Singleton<PlayersManager>.Instance?.LocalPlayer;
			if (pPG_Player == null)
			{
				return false;
			}
			ToolDataNet toolsData = pPG_Player.ToolsData;
			return toolType switch
			{
				ToolType.DivingEquipment => toolsData.DivingEquipmentUnlocked || toolsData.DivingEquipmentLevel > 0, 
				ToolType.Harpoon => toolsData.HarpoonUnlocked, 
				ToolType.SeaScooter => toolsData.ScooterUnlocked, 
				ToolType.FishingRod => toolsData.FishingRodUnlocked, 
				_ => false, 
			};
		}

		private bool IsUnlockTarget(int targetLevelIndex)
		{
			if (targetLevelIndex == 0)
			{
				return GetOwnedLevel(_currentToolType) == -1;
			}
			return false;
		}

		private bool IsUpgradeableTool(ToolType toolType)
		{
			if (toolType != ToolType.DivingEquipment && toolType != ToolType.Harpoon && toolType != ToolType.SeaScooter)
			{
				return toolType == ToolType.FishingRod;
			}
			return true;
		}

		private int GetSelectedLevelIndex()
		{
			return ClampLevelIndex(_selectedLevelIndex);
		}

		private int GetSelectedLevelDisplayNumber()
		{
			return GetSelectedLevelIndex() + 1;
		}

		private byte GetSelectedLevelAsByte()
		{
			return (byte)GetSelectedLevelIndex();
		}

		private int ClampLevelIndex(int levelIndex)
		{
			return Mathf.Clamp(levelIndex, 0, 4);
		}

		private bool IsValidLevelData(DivingEquipmentData data, int levelIndex)
		{
			if (data != null && data.Levels != null)
			{
				return data.Levels.Count > levelIndex;
			}
			return false;
		}

		private bool IsValidLevelData(HarpoonData data, int levelIndex)
		{
			if (data != null && data.Levels != null)
			{
				return data.Levels.Count > levelIndex;
			}
			return false;
		}

		private bool IsValidLevelData(SeaScooterData data, int levelIndex)
		{
			if (data != null && data.Levels != null)
			{
				return data.Levels.Count > levelIndex;
			}
			return false;
		}

		private bool IsValidLevelData(FishingRodData data, int levelIndex)
		{
			if (data != null && data.Levels != null)
			{
				return data.Levels.Count > levelIndex;
			}
			return false;
		}

		private void CacheButtonGroups()
		{
			_toolButtons = new BaseButton[4] { _divingSuitButton, _harpoonButton, _seaScooterButton, _fishingRodButton };
			_levelButtons = new BaseButton[5] { _level1Button, _level2Button, _level3Button, _level4Button, _level5Button };
			RefreshLevelButtonLabels();
			RefreshSelectionHighlights();
		}

		private void RefreshLevelButtonLabels()
		{
			if (_levelButtons == null)
			{
				return;
			}
			for (int i = 0; i < _levelButtons.Length; i++)
			{
				if (!(_levelButtons[i] == null))
				{
					TextMeshProUGUI componentInChildren = _levelButtons[i].GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
					if (componentInChildren != null)
					{
						componentInChildren.text = (i + 1).ToString();
					}
				}
			}
		}

		private void RefreshSelectionHighlights()
		{
			RefreshToolSelectionHighlights();
			RefreshLevelSelectionHighlights();
		}

		private void RefreshToolSelectionHighlights()
		{
			SetButtonSelectedVisual(_divingSuitButton, _currentToolType == ToolType.DivingEquipment);
			SetButtonSelectedVisual(_harpoonButton, _currentToolType == ToolType.Harpoon);
			SetButtonSelectedVisual(_seaScooterButton, _currentToolType == ToolType.SeaScooter);
			SetButtonSelectedVisual(_fishingRodButton, _currentToolType == ToolType.FishingRod);
		}

		private void RefreshLevelSelectionHighlights()
		{
			if (_levelButtons != null)
			{
				int selectedLevelIndex = GetSelectedLevelIndex();
				for (int i = 0; i < _levelButtons.Length; i++)
				{
					SetButtonSelectedVisual(_levelButtons[i], i == selectedLevelIndex);
				}
			}
		}

		private void SetButtonSelectedVisual(BaseButton button, bool selected)
		{
			if (!(button == null) && button.transform.childCount > 0)
			{
				GameObject gameObject = button.transform.GetChild(0).gameObject;
				if (gameObject.activeSelf != selected)
				{
					gameObject.SetActive(selected);
				}
			}
		}

		private void ClearAllSelectionHighlights()
		{
			if (_toolButtons != null)
			{
				BaseButton[] toolButtons = _toolButtons;
				foreach (BaseButton button in toolButtons)
				{
					SetButtonSelectedVisual(button, selected: false);
				}
			}
			if (_levelButtons != null)
			{
				BaseButton[] toolButtons = _levelButtons;
				foreach (BaseButton button2 in toolButtons)
				{
					SetButtonSelectedVisual(button2, selected: false);
				}
			}
		}
	}
}
