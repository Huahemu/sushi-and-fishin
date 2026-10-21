namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.Extensions;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.UI;

public class SaveSlotUI : MonoBehaviour
{
	[Header("UI References")]
	[SerializeField]
	private TextMeshProUGUI titleText;

	[SerializeField]
	private Button slotButton;

	[SerializeField]
	private TextMeshProUGUI buttonText;

	[SerializeField]
	private Button deleteButton;

	[Header("Save Data Display")]
	[SerializeField]
	private TextMeshProUGUI moneyText;

	[SerializeField]
	private TextMeshProUGUI ticketsText;

	[SerializeField]
	private TextMeshProUGUI quotaText;

	[SerializeField]
	private TextMeshProUGUI seedText;

	[SerializeField]
	private TextMeshProUGUI daysText;

	[Header("Empty Slot Settings")]
	[SerializeField]
	private string emptySlotTitle = "Empty Slot";

	[SerializeField]
	private string emptySlotButtonText = "New Game";

	private string saveName;

	private bool isEmpty;

	private SaveData currentSaveData;

	public Action<string> OnSlotSelected;

	public Action<string> OnSaveDeleted;

	private void Awake()
	{
		if (slotButton != null)
		{
			slotButton.onClick.AddListener(OnSlotClicked);
		}
		if (deleteButton != null)
		{
			deleteButton.onClick.AddListener(OnDeleteClicked);
		}
	}

	public void PopulateSlot(string saveName, SaveData saveData = null)
	{
		this.saveName = saveName;
		isEmpty = saveData == null;
		currentSaveData = saveData;
		if (isEmpty)
		{
			if (titleText != null)
			{
				titleText.text = emptySlotTitle;
			}
			if (buttonText != null)
			{
				buttonText.text = emptySlotButtonText;
			}
			if (moneyText != null)
			{
				moneyText.text = "";
			}
			if (ticketsText != null)
			{
				ticketsText.text = "";
			}
			if (quotaText != null)
			{
				quotaText.text = "";
			}
			if (seedText != null)
			{
				seedText.text = "";
			}
			if (daysText != null)
			{
				daysText.text = "";
			}
			if (deleteButton != null)
			{
				deleteButton.gameObject.SetActive(value: false);
			}
			return;
		}
		if (titleText != null)
		{
			string text = FormatSaveDate(saveData.saveTime);
			titleText.text = text;
		}
		if (buttonText != null)
		{
			buttonText.text = "SELECT";
		}
		if (moneyText != null)
		{
			moneyText.text = MoneyFormatter.FormatWithDollar(saveData.money);
		}
		if (ticketsText != null)
		{
			ticketsText.text = saveData.tickets.ToString();
		}
		if (quotaText != null)
		{
			quotaText.text = MoneyFormatter.FormatWithDollar(saveData.currentQuota);
		}
		if (seedText != null)
		{
			seedText.text = saveData.seed.ToString();
		}
		if (daysText != null)
		{
			daysText.text = $"{saveData.daysPassed + 1}";
		}
		if (deleteButton != null)
		{
			deleteButton.gameObject.SetActive(value: true);
		}
	}

	private void OnSlotClicked()
	{
		if (isEmpty)
		{
			if (MonoSingleton<LocalSaveManager>.Instance != null)
			{
				string obj = $"Save_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
				MonoSingleton<LocalSaveManager>.Instance.CreateNewSave(obj);
				OnSlotSelected?.Invoke(obj);
			}
		}
		else
		{
			OnSlotSelected?.Invoke(saveName);
		}
	}

	private void OnDeleteClicked()
	{
		if (isEmpty || string.IsNullOrEmpty(saveName))
		{
			return;
		}
		string saveToDelete = saveName;
		string text = "this save";
		if (currentSaveData != null)
		{
			text = FormatSaveDate(currentSaveData.saveTime);
		}
		if (MonoSingleton<ConfirmationDialogManager>.Instance != null)
		{
			MonoSingleton<ConfirmationDialogManager>.Instance.ShowConfirmation("Are you sure you want to delete the save from " + text + "? This action cannot be undone.", () =>
			{
				if (MonoSingleton<LocalSaveManager>.Instance != null)
				{
					MonoSingleton<LocalSaveManager>.Instance.DeleteSave(saveToDelete);
				}
				PopulateSlot(saveToDelete);
				OnSaveDeleted?.Invoke(saveToDelete);
			}, () =>
			{
			}, "Yes, delete", "Cancel");
		}
		else
		{
			Debug.LogWarning("[SaveSlotUI] ConfirmationDialogManager not found. Deleting save without confirmation.");
			if (MonoSingleton<LocalSaveManager>.Instance != null)
			{
				MonoSingleton<LocalSaveManager>.Instance.DeleteSave(saveToDelete);
			}
			PopulateSlot(saveToDelete);
			OnSaveDeleted?.Invoke(saveToDelete);
		}
	}

	private string FormatSaveDate(long unixTimestamp)
	{
		if (unixTimestamp <= 0)
		{
			return "Unknown Date";
		}
		try
		{
			return DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).ToString("dd/MM/yyyy");
		}
		catch
		{
			return "Invalid Date";
		}
	}
}

}
