namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::System.IO;
using global::UnityEngine;

public static class PlayerVoiceVolumePersistence
{
	[Serializable]
	private class SaveData
	{
		public List<PlayerVolumeEntry> volumes = new List<PlayerVolumeEntry>();
	}

	[Serializable]
	public class PlayerVolumeEntry
	{
		public string steamId;

		public float volumePercent;
	}

	private const string FileName = "playervoicevolumes.json";

	private static string SavePath => Path.Combine(SushiAndFish.Casino.Original.CasinoPaths.PersistentDataPath, "playervoicevolumes.json");

	public static Dictionary<string, float> Load()
	{
		Dictionary<string, float> dictionary = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
		if (!File.Exists(SavePath))
		{
			return dictionary;
		}
		try
		{
			SaveData saveData = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
			if (saveData?.volumes == null)
			{
				return dictionary;
			}
			foreach (PlayerVolumeEntry volume in saveData.volumes)
			{
				if (!string.IsNullOrEmpty(volume.steamId))
				{
					dictionary[volume.steamId] = Mathf.Clamp01(volume.volumePercent);
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[PlayerVoiceVolumePersistence] Load failed: " + ex.Message);
		}
		return dictionary;
	}

	public static void Save(Dictionary<string, float> volumesBySteamId)
	{
		if (volumesBySteamId == null)
		{
			return;
		}
		try
		{
			SaveData saveData = new SaveData();
			foreach (KeyValuePair<string, float> item in volumesBySteamId)
			{
				if (!string.IsNullOrEmpty(item.Key))
				{
					saveData.volumes.Add(new PlayerVolumeEntry
					{
						steamId = item.Key,
						volumePercent = Mathf.Clamp01(item.Value)
					});
				}
			}
			File.WriteAllText(SavePath, JsonUtility.ToJson(saveData, prettyPrint: true));
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[PlayerVoiceVolumePersistence] Save failed: " + ex.Message);
		}
	}
}

}
