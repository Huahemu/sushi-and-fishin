using System;
using System.IO;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Analytics;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace PaperPixelGames.Framework.States.MainMenu
{
	public class SaveSlot : MonoBehaviour
	{
		[Header("Config")]
		[SerializeField]
		private int _saveSlotIndex;

		[SerializeField]
		private bool _requireDeleteConfirmation = true;

		[SerializeField]
		private ProgressionLevelDataSO _levelData;

		[Header("UI - Text")]
		[SerializeField]
		private TextMeshProUGUI _shopNameText;

		[SerializeField]
		private TextMeshProUGUI _dayText;

		[SerializeField]
		private TextMeshProUGUI _lastSaveDateText;

		[SerializeField]
		private TextMeshProUGUI _moneyText;

		[SerializeField]
		private TextMeshProUGUI _scrapText;

		[SerializeField]
		private TextMeshProUGUI _levelText;

		[SerializeField]
		private TextMeshProUGUI _levelProgressText;

		[Header("UI - States")]
		[SerializeField]
		private GameObject _emptySaveSlot;

		[SerializeField]
		private GameObject _dataSaveSlot;

		[Header("UI - Buttons")]
		[SerializeField]
		private BaseButton _saveSlotButton;

		[SerializeField]
		private BaseButton _deleteSaveButton;

		private bool _uiBound;

		private bool _localeChangeBound;

		public int SlotIndex => _saveSlotIndex;
		public bool ContainsSave => HasSave;

		private SaveSlotData SlotData
		{
			get
			{
				if (!(Singleton<SaveManager>.Instance != null) || Singleton<SaveManager>.Instance.SaveSlotsData == null || _saveSlotIndex < 0 || _saveSlotIndex >= Singleton<SaveManager>.Instance.SaveSlotsData.Count)
				{
					return null;
				}
				return Singleton<SaveManager>.Instance.SaveSlotsData[_saveSlotIndex];
			}
		}

		private bool HasSave
		{
			get
			{
				SaveSlotData slotData = SlotData;
				if (slotData == null)
				{
					return false;
				}
				if (slotData.GameProgress != null)
				{
					return slotData.GameProgress.HasSave;
				}
				return false;
			}
		}

		private bool IsCorrupted
		{
			get
			{
				if (Singleton<SaveManager>.Instance != null)
				{
					return Singleton<SaveManager>.Instance.IsSaveSlotCorrupted(_saveSlotIndex);
				}
				return false;
			}
		}

		private void BindUI()
		{
			if (!_uiBound)
			{
				_uiBound = true;
				if (_saveSlotButton != null)
				{
					_saveSlotButton.Clicked += OnSaveSlotButtonClick;
				}
				if (_deleteSaveButton != null)
				{
					_deleteSaveButton.Clicked += OnDeleteSaveButtonClick;
				}
				if (Singleton<MultiplayerCoordinator>.Instance != null)
				{
					Singleton<MultiplayerCoordinator>.Instance.ForeignLobbyStateChanged += OnForeignLobbyStateChanged;
				}
			}
		}

		private void UnbindUI()
		{
			if (_uiBound)
			{
				_uiBound = false;
				if (_saveSlotButton != null)
				{
					_saveSlotButton.Clicked -= OnSaveSlotButtonClick;
				}
				if (_deleteSaveButton != null)
				{
					_deleteSaveButton.Clicked -= OnDeleteSaveButtonClick;
				}
				if (Singleton<MultiplayerCoordinator>.Instance != null)
				{
					Singleton<MultiplayerCoordinator>.Instance.ForeignLobbyStateChanged -= OnForeignLobbyStateChanged;
				}
			}
		}

		private void OnForeignLobbyStateChanged(bool isInForeignLobby)
		{
			if (_saveSlotButton != null)
			{
				_saveSlotButton.interactable = !isInForeignLobby && !IsCorrupted;
			}
		}

		private void OnDestroy()
		{
			UnbindUI();
			UnbindLocaleChange();
		}

		private void OnSaveSlotButtonClick()
		{
			var frontEnd = SushiAndFish.FrontEnd.IslandFrontEndFlow.Active;
			if (frontEnd != null && frontEnd.HandleSaveSlotClick(this)) return;
			LaunchOriginalSlot();
		}

		public void LaunchOriginalSlot()
		{
			if (Singleton<SaveManager>.Instance == null)
			{
				return;
			}
			if (IsCorrupted)
			{
				Singleton<NotifyManager>.Instance.ShowToast(LocalizationDatabase.GetString("UiTable", "ui.save_slot.corrupted_blocked", "This save is damaged and could not be recovered. Delete it to reuse this slot."), NotificationState.Error);
				return;
			}
			bool hasSave = HasSave;
			MultiplayerCoordinator instance = Singleton<MultiplayerCoordinator>.Instance;
			if (instance == null)
			{
				return;
			}
			LobbyRoomService roomService = instance.RoomService;
			if (roomService != null && roomService.HasRoom && roomService.Snapshot.IsOwner && !roomService.CanLaunch)
			{
				if (Singleton<NotifyManager>.TryGetInstance(out var instance2))
				{
					instance2.ShowToast(LobbyRoomLabel.Text("crew_not_ready", "Wait until everyone in your crew is ready.", "Ekibindeki herkesin hazır olmasını bekle."), NotificationState.Error);
				}
				return;
			}
			if (!instance.CanStartGameFromSaveSlot)
			{
				Singleton<NotifyManager>.Instance.ShowToast(LocalizationDatabase.GetString("UiTable", "ui.save_slot.leave_lobby_first", "Leave the current lobby before starting your own save."), NotificationState.Error);
				return;
			}
			Singleton<SaveManager>.Instance.SetCurrentSaveSlot(_saveSlotIndex);
			if (instance.TryBeginHostLaunch())
			{
				AnalyticsEvents.Fields("has_save", hasSave ? 1 : 0, "slot_index", _saveSlotIndex);
				Singleton<MainMenuManager>.Instance.AnimateBoat();
			}
		}

		public void Initialize()
		{
			BindUI();
			BindLocaleChange();
			RefreshUI();
		}

		private void BindLocaleChange()
		{
			if (!_localeChangeBound)
			{
				LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
				_localeChangeBound = true;
			}
		}

		private void UnbindLocaleChange()
		{
			if (_localeChangeBound)
			{
				LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
				_localeChangeBound = false;
			}
		}

		private void OnSelectedLocaleChanged(Locale locale)
		{
			RefreshUI();
		}

		private void OnDeleteSaveButtonClick()
		{
			if (HasSave || IsCorrupted)
			{
				Singleton<SaveManager>.Instance.SetCurrentSaveSlot(_saveSlotIndex);
				if (!_requireDeleteConfirmation || !TryShowDeleteConfirmation(OnDeleteSave))
				{
					OnDeleteSave();
				}
			}
		}

		private void RefreshUI()
		{
			bool hasSave = HasSave;
			bool isCorrupted = IsCorrupted;
			bool flag = Singleton<MultiplayerCoordinator>.Instance != null && Singleton<MultiplayerCoordinator>.Instance.IsInForeignLobby;
			if (_saveSlotButton != null)
			{
				_saveSlotButton.interactable = !isCorrupted && !flag;
			}
			if (_emptySaveSlot != null)
			{
				_emptySaveSlot.SetActive(!hasSave | isCorrupted);
			}
			if (_dataSaveSlot != null)
			{
				_dataSaveSlot.SetActive(hasSave && !isCorrupted);
			}
			if (isCorrupted)
			{
				SetTextSafe(_shopNameText, LocalizationDatabase.GetString("UiTable", "ui.save_slot.corrupted", "Corrupted Save"));
				SetTextSafe(_dayText, string.Empty);
				SetTextSafe(_moneyText, string.Empty);
				SetTextSafe(_scrapText, string.Empty);
				SetTextSafe(_levelText, string.Empty);
				SetTextSafe(_levelProgressText, string.Empty);
				SetTextSafe(_lastSaveDateText, string.Empty);
				if (_deleteSaveButton != null)
				{
					_deleteSaveButton.gameObject.SetActive(value: true);
				}
			}
			else if (!hasSave)
			{
				SetTextSafe(_shopNameText, LocalizationDatabase.GetFormattedString("UiTable", "ui.save_slot.empty", "Empty Slot {0}", _saveSlotIndex + 1));
				SetTextSafe(_dayText, string.Empty);
				SetTextSafe(_moneyText, string.Empty);
				SetTextSafe(_scrapText, string.Empty);
				SetTextSafe(_levelText, string.Empty);
				SetTextSafe(_levelProgressText, string.Empty);
				SetTextSafe(_lastSaveDateText, string.Empty);
				if (_deleteSaveButton != null)
				{
					_deleteSaveButton.gameObject.SetActive(value: false);
				}
			}
			else
			{
				if (_deleteSaveButton != null)
				{
					_deleteSaveButton.gameObject.SetActive(value: true);
				}
				LoadSaveDataIntoUI();
			}
		}

		private void OnDeleteSave()
		{
			if (!(Singleton<SaveManager>.Instance == null))
			{
				DeleteWallpapersFolderForSlot();
				Singleton<SaveManager>.Instance.Clear(_saveSlotIndex);
				UtilityMethods.Log($"[SaveSlot] Deleted slot: {_saveSlotIndex}");
				RefreshUI();
			}
		}

		private void LoadSaveDataIntoUI()
		{
			SaveSlotData slotData = SlotData;
			if (slotData == null)
			{
				UtilityMethods.Log($"[SaveSlot] Missing slot data at index: {_saveSlotIndex}");
				return;
			}
			string text = TryGetShopName(slotData);
			SetTextSafe(_shopNameText, string.IsNullOrWhiteSpace(text) ? LocalizationDatabase.GetString("UiTable", "ui.save_slot.default_shop", "Shop") : text);
			ushort num = (ushort)((slotData.GameProgress != null) ? ((ushort)slotData.GameProgress.CurrentDay) : 0);
			SetTextSafe(_dayText, (num > 0) ? LocalizationDatabase.GetFormattedString("UiTable", "ui.save_slot.day", "Day {0}", num) : string.Empty);
			string value = ((slotData.GameProgress != null) ? LocalizationDatabase.GetFormattedString("UiTable", "ui.save_slot.last_play_time", "Last Play Time: {0}", slotData.GameProgress.LastSaveTime) : string.Empty);
			SetTextSafe(_lastSaveDateText, value);
			string formattedString = LocalizationDatabase.GetFormattedString("UiTable", "ui.save_slot.balance", "Balance: {0}", TryGetMoneyText(slotData));
			SetTextSafe(_moneyText, formattedString);
			string formattedString2 = LocalizationDatabase.GetFormattedString("UiTable", "ui.save_slot.scrap", "Scrap: {0}", TryGetScrapText(slotData));
			SetTextSafe(_scrapText, formattedString2);
			SetTextSafe(_levelText, TryGetLevelText(slotData));
			SetTextSafe(_levelProgressText, TryGetLevelProgressText(slotData));
		}

		private void DeleteWallpapersFolderForSlot()
		{
			if (_saveSlotIndex < 0)
			{
				UtilityMethods.Log($"[SaveSlot] Invalid slot index: {_saveSlotIndex}");
				return;
			}
			string text = Path.Combine(Application.persistentDataPath, "Wallpapers");
			if (!Directory.Exists(text))
			{
				return;
			}
			string text2 = Path.Combine(text, $"SaveSlot_{_saveSlotIndex}");
			if (!Directory.Exists(text2))
			{
				return;
			}
			try
			{
				Directory.Delete(text2, recursive: true);
				UtilityMethods.Log("[SaveSlot] Deleted wallpapers folder: " + text2);
			}
			catch (Exception ex)
			{
				UtilityMethods.Log("[SaveSlot] Failed deleting wallpapers folder: " + ex.Message);
			}
		}

		private void SetTextSafe(TextMeshProUGUI text, string value)
		{
			if (!(text == null))
			{
				text.text = value ?? string.Empty;
			}
		}

		private string TryGetShopName(SaveSlotData slot)
		{
			return slot?.RestaurantData?.RestaurantSignCustomizationData?.RestaurantName;
		}

		private string TryGetMoneyText(SaveSlotData slot)
		{
			if (slot?.GameProgress == null)
			{
				return "0";
			}
			return slot.GameProgress.ShopBalance.ConvertMoneyText();
		}

		private string TryGetScrapText(SaveSlotData slot)
		{
			if (slot?.GameProgress == null)
			{
				return "0";
			}
			return slot.GameProgress.ScrapBalance.ConvertMoneyText(includeDollarSign: false);
		}

		private string TryGetLevelText(SaveSlotData slot)
		{
			GameProgressData gameProgressData = slot?.GameProgress;
			if (gameProgressData == null)
			{
				return string.Empty;
			}
			return LocalizationDatabase.GetFormattedString("UiTable", "ui.save_slot.level", "Level: {0}", ResolveRestaurantLevel(gameProgressData));
		}

		private string TryGetLevelProgressText(SaveSlotData slot)
		{
			GameProgressData gameProgressData = slot?.GameProgress;
			if (gameProgressData == null)
			{
				return string.Empty;
			}
			int level = ResolveRestaurantLevel(gameProgressData);
			double value = ResolveCurrentLevelXp(gameProgressData, level);
			double num = ResolveNextLevelRequiredXp(level);
			if (!(num < 0.0))
			{
				return FormatXp(value) + " / " + FormatXp(num) + " XP";
			}
			return FormatXp(value) + " XP";
		}

		private int ResolveRestaurantLevel(GameProgressData progress)
		{
			if (progress == null)
			{
				return 1;
			}
			int num = Mathf.Max(1, progress.RestaurantLevel);
			if (_levelData == null)
			{
				return num;
			}
			return Mathf.Max(num, _levelData.GetLevelForTotalXp(Math.Max(0.0, progress.TotalXp)));
		}

		private double ResolveCurrentLevelXp(GameProgressData progress, int level)
		{
			if (progress == null)
			{
				return 0.0;
			}
			if (_levelData == null)
			{
				return Math.Max(0.0, progress.CurrentXp);
			}
			double num = Math.Max(0.0, progress.TotalXp);
			return Math.Max(0.0, num - _levelData.GetRequiredTotalXpForLevel(level));
		}

		private double ResolveNextLevelRequiredXp(int level)
		{
			if (_levelData == null)
			{
				return -1.0;
			}
			double requiredTotalXpForNextLevel = _levelData.GetRequiredTotalXpForNextLevel(level);
			if (requiredTotalXpForNextLevel < 0.0)
			{
				return -1.0;
			}
			double requiredTotalXpForLevel = _levelData.GetRequiredTotalXpForLevel(level);
			return Math.Max(1.0, requiredTotalXpForNextLevel - requiredTotalXpForLevel);
		}

		private static string FormatXp(double value)
		{
			return value.ToString("0");
		}

		private bool TryShowDeleteConfirmation(Action onConfirm)
		{
			if (Singleton<NotifyManager>.Instance == null || onConfirm == null)
			{
				return false;
			}
			Singleton<NotifyManager>.Instance.ShowPopup(LocalizationDatabase.GetString("UiTable", "ui.save_slot.delete_title", "Delete save?"), LocalizationDatabase.GetString("UiTable", "ui.save_slot.delete_message", "This save file will be permanently deleted. Are you sure you want to delete it?"), () =>
			{
				onConfirm();
			}, () =>
			{
			}, "Delete", useDescriptionBackground: false, 0f, closeOnEscape: true, "ui.popup.delete");
			return true;
		}
	}
}
