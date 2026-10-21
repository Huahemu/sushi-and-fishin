namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

public class SaveSlotManager : MonoBehaviour
{
	[Header("UI References")]
	[SerializeField]
	private GameObject saveSlotPanel;

	[SerializeField]
	private SaveSlotUI[] saveSlots;

	[SerializeField]
	private int maxSlots = 6;

	private void Awake()
	{
		if (saveSlotPanel != null)
		{
			saveSlotPanel.SetActive(value: false);
		}
	}

	public void OpenSaveSelection()
	{
		if (MonoSingleton<ConfirmationDialogManager>.Instance != null)
		{
			MonoSingleton<ConfirmationDialogManager>.Instance.ShowConfirmation("Are you the friend with the best computer and network connection?", () =>
			{
				if (saveSlotPanel != null)
				{
					saveSlotPanel.SetActive(value: true);
				}
				PopulateSlots();
			}, () =>
			{
				Debug.Log("[SaveSlotManager] User cancelled hosting game.");
			}, "Yes, I am", "No, cancel");
		}
		else
		{
			Debug.LogWarning("[SaveSlotManager] ConfirmationDialogManager not found. Opening save selection without confirmation.");
			if (saveSlotPanel != null)
			{
				saveSlotPanel.SetActive(value: true);
			}
			PopulateSlots();
		}
	}

	public void CloseSaveSelection()
	{
		if (saveSlotPanel != null)
		{
			saveSlotPanel.SetActive(value: false);
		}
	}

	private void PopulateSlots()
	{
		if (saveSlots == null || saveSlots.Length == 0)
		{
			Debug.LogWarning("[SaveSlotManager] No save slots assigned!");
			return;
		}
		List<string> list = ((MonoSingleton<LocalSaveManager>.Instance != null) ? MonoSingleton<LocalSaveManager>.Instance.GetAvailableSaves() : new List<string>());
		for (int i = 0; i < saveSlots.Length && i < maxSlots; i++)
		{
			if (!(saveSlots[i] == null))
			{
				if (i < list.Count)
				{
					string saveName = list[i];
					SaveData saveData = MonoSingleton<LocalSaveManager>.Instance.LoadSaveData(saveName);
					saveSlots[i].OnSlotSelected = OnSlotSelected;
					saveSlots[i].PopulateSlot(saveName, saveData);
				}
				else
				{
					string saveName2 = $"Slot_{i + 1}";
					saveSlots[i].OnSlotSelected = OnSlotSelected;
					saveSlots[i].PopulateSlot(saveName2);
				}
			}
		}
	}

	private void OnSlotSelected(string saveName)
	{
		if (MonoSingleton<LocalSaveManager>.Instance != null)
		{
			MonoSingleton<LocalSaveManager>.Instance.SelectSave(saveName);
		}
		CloseSaveSelection();
		StartCoroutine(StartGameCoroutine());
		if (MonoSingleton<LobbyManager>.Instance != null)
		{
			MonoSingleton<LobbyManager>.Instance.StartGame();
		}
		else
		{
			Debug.LogError("[SaveSlotManager] LobbyManager not found! Cannot start game.");
		}
	}

	private IEnumerator StartGameCoroutine()
	{
		MonoSingleton<SceneTransitioner>.Instance.SetLoadingScreen(isEnabled: false, 0.5f);
		yield return new WaitForSeconds(1f);
		if (MonoSingleton<LobbyManager>.Instance != null)
		{
			MonoSingleton<LobbyManager>.Instance.StartGame();
		}
	}
}

}
