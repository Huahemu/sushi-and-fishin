namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::System.IO;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

public class LocalSaveManager : MonoSingleton<LocalSaveManager>
{
	private const string SAVE_DIRECTORY = "Saves";

	private const string SELECTED_SAVE_KEY = "SelectedSaveName";

	private const string SAVE_DATA_KEY = "SelectedSaveData";

	private string SaveDirectoryPath => Path.Combine(SushiAndFish.Casino.Original.CasinoPaths.PersistentDataPath, "Saves");

	public string SelectedSaveName { get; private set; }

	protected override void OnAwake()
	{
		base.OnAwake();
		if (!Directory.Exists(SaveDirectoryPath))
		{
			Directory.CreateDirectory(SaveDirectoryPath);
		}
		SelectedSaveName = PlayerPrefs.GetString("SelectedSaveName", "");
	}

	public List<string> GetAvailableSaves()
	{
		List<string> list = new List<string>();
		if (!Directory.Exists(SaveDirectoryPath))
		{
			return list;
		}
		string[] files = Directory.GetFiles(SaveDirectoryPath, "*.json");
		foreach (string path in files)
		{
			list.Add(Path.GetFileNameWithoutExtension(path));
		}
		return list;
	}

	public void CreateNewSave(string saveName)
	{
		if (string.IsNullOrEmpty(saveName))
		{
			Debug.LogError("[LocalSaveManager] Cannot create save with empty name");
			return;
		}
		SelectedSaveName = saveName;
		PlayerPrefs.SetString("SelectedSaveName", saveName);
		PlayerPrefs.Save();
		GameSettings gameSettings = SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
		SaveData obj = new SaveData
		{
			saveName = saveName,
			saveTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			successfulQuota = 0,
			daysLeft = gameSettings.daysBeforeQuota,
			daysPassed = 0,
			currentQuota = gameSettings.startingQuota,
			currentFloor = 0,
			requiredQuotaToNextFloor = gameSettings.floorData[1].requiredQuotaToAccess,
			money = gameSettings.startingMoney,
			tickets = gameSettings.startingTicket,
			seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue)
		};
		string path = Path.Combine(SaveDirectoryPath, saveName + ".json");
		try
		{
			string contents = JsonUtility.ToJson(obj, prettyPrint: true);
			File.WriteAllText(path, contents);
			PlayerPrefs.SetString("SelectedSaveData", JsonUtility.ToJson(obj));
			PlayerPrefs.Save();
		}
		catch (Exception ex)
		{
			Debug.LogError("[LocalSaveManager] Failed to create save: " + ex.Message);
		}
	}

	public void SelectSave(string saveName)
	{
		if (string.IsNullOrEmpty(saveName))
		{
			Debug.LogError("[LocalSaveManager] Cannot select save with empty name");
			return;
		}
		string text = Path.Combine(SaveDirectoryPath, saveName + ".json");
		if (!File.Exists(text))
		{
			Debug.LogError("[LocalSaveManager] Save file not found: " + text);
			return;
		}
		SelectedSaveName = saveName;
		PlayerPrefs.SetString("SelectedSaveName", saveName);
		SaveData saveData = LoadSaveData(saveName);
		if (saveData != null)
		{
			string value = JsonUtility.ToJson(saveData);
			PlayerPrefs.SetString("SelectedSaveData", value);
		}
		PlayerPrefs.Save();
		Debug.Log("[LocalSaveManager] Active save: " + saveName);
	}

	public SaveData LoadSaveData(string saveName)
	{
		if (string.IsNullOrEmpty(saveName))
		{
			Debug.LogError("[LocalSaveManager] Cannot load save with empty name");
			return null;
		}
		string text = Path.Combine(SaveDirectoryPath, saveName + ".json");
		if (!File.Exists(text))
		{
			Debug.LogError("[LocalSaveManager] Save file not found: " + text);
			return null;
		}
		try
		{
			return JsonUtility.FromJson<SaveData>(File.ReadAllText(text));
		}
		catch (Exception ex)
		{
			Debug.LogError("[LocalSaveManager] Failed to load save: " + ex.Message);
			return null;
		}
	}

	public void SaveGameData(SaveData saveData)
	{
		if (saveData == null || string.IsNullOrEmpty(saveData.saveName))
		{
			Debug.LogWarning("[LocalSaveManager] No save data to save");
			return;
		}
		string path = Path.Combine(SaveDirectoryPath, saveData.saveName + ".json");
		try
		{
			saveData.saveTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			string contents = JsonUtility.ToJson(saveData, prettyPrint: true);
			File.WriteAllText(path, contents);
			Debug.Log("[LocalSaveManager] Saved game to: " + saveData.saveName);
		}
		catch (Exception ex)
		{
			Debug.LogError("[LocalSaveManager] Failed to save game: " + ex.Message);
		}
	}

	public void DeleteSave(string saveName)
	{
		if (string.IsNullOrEmpty(saveName))
		{
			Debug.LogError("[LocalSaveManager] Cannot delete save with empty name");
			return;
		}
		string path = Path.Combine(SaveDirectoryPath, saveName + ".json");
		if (!File.Exists(path))
		{
			return;
		}
		try
		{
			File.Delete(path);
			Debug.Log("[LocalSaveManager] Deleted save: " + saveName);
			if (SelectedSaveName == saveName)
			{
				SelectedSaveName = "";
				PlayerPrefs.DeleteKey("SelectedSaveName");
				PlayerPrefs.Save();
			}
		}
		catch (Exception ex)
		{
			Debug.LogError("[LocalSaveManager] Failed to delete save: " + ex.Message);
		}
	}
}

}
