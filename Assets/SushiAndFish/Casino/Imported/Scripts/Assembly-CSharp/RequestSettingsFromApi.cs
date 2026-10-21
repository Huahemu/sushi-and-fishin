namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Globalization;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;
using global::UnityEngine.Networking;

public class RequestSettingsFromApi : NetworkSingleton<RequestSettingsFromApi>
{
	private class GameSettings
	{
		public float? estimatedValue;

		public int? baseMinBet;

		public int? baseMaxBet;
	}

	[Serializable]
	public class GameDataResponse
	{
		public string game_id;

		public string game_name;

		public SubGameResponse[] sub_games;
	}

	[Serializable]
	public class SubGameResponse
	{
		public string sub_game_name;

		public SettingResponse[] settings;
	}

	[Serializable]
	public class SettingResponse
	{
		public string setting_key;

		public string setting_value;

		public string setting_type;
	}

	[Header("API Settings")]
	[SerializeField]
	private string gameId = "";

	[SerializeField]
	private string apiBaseUrl = "";

	private Dictionary<string, GameSettings> _gameSettings = new Dictionary<string, GameSettings>();

	public void ReloadSettings()
	{
		if (base.isServer)
		{
			StartCoroutine(LoadAndApplySettingsCoroutine());
		}
	}

	private IEnumerator LoadAndApplySettingsCoroutine()
	{
		yield return StartCoroutine(FetchAllGameSettingsCoroutine());
		ApplySettingsToGames();
	}

	private IEnumerator FetchAllGameSettingsCoroutine()
	{
		long num = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		string uri = $"{apiBaseUrl}/{gameId}/all?t={num}";
		using UnityWebRequest request = UnityWebRequest.Get(uri);
		request.SetRequestHeader("Cache-Control", "no-cache, no-store, must-revalidate");
		request.SetRequestHeader("Pragma", "no-cache");
		request.SetRequestHeader("Expires", "0");
		yield return request.SendWebRequest();
		if (request.result != UnityWebRequest.Result.Success)
		{
			Debug.LogError("Failed to fetch game settings: " + request.error);
			yield break;
		}
		try
		{
			GameDataResponse gameDataResponse = JsonUtility.FromJson<GameDataResponse>(request.downloadHandler.text);
			if (gameDataResponse?.sub_games != null)
			{
				ProcessGameSettings(gameDataResponse);
			}
		}
		catch (Exception ex)
		{
			Debug.LogError("Error parsing game settings: " + ex.Message);
		}
	}

	private void ProcessGameSettings(GameDataResponse gameData)
	{
		_gameSettings.Clear();
		SubGameResponse[] sub_games = gameData.sub_games;
		foreach (SubGameResponse subGameResponse in sub_games)
		{
			if (subGameResponse.settings == null)
			{
				continue;
			}
			string text = subGameResponse.sub_game_name?.ToLower() ?? "";
			if (string.IsNullOrEmpty(text))
			{
				continue;
			}
			GameSettings gameSettings = new GameSettings();
			SettingResponse[] settings = subGameResponse.settings;
			foreach (SettingResponse settingResponse in settings)
			{
				if (settingResponse.setting_type != "number")
				{
					continue;
				}
				switch (settingResponse.setting_key)
				{
				case "estimatedValue":
				{
					if (float.TryParse(settingResponse.setting_value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
					{
						gameSettings.estimatedValue = result2;
					}
					break;
				}
				case "baseMinBet":
				{
					if (int.TryParse(settingResponse.setting_value, out var result3))
					{
						gameSettings.baseMinBet = result3;
					}
					break;
				}
				case "baseMaxBet":
				{
					if (int.TryParse(settingResponse.setting_value, out var result))
					{
						gameSettings.baseMaxBet = result;
					}
					break;
				}
				}
			}
			if (gameSettings.estimatedValue.HasValue || gameSettings.baseMinBet.HasValue || gameSettings.baseMaxBet.HasValue)
			{
				_gameSettings[text] = gameSettings;
			}
		}
	}

	private void ApplySettingsToGames()
	{
		GameBase[] array = UnityEngine.Object.FindObjectsByType<GameBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		List<GameObject> list = new List<GameObject>();
		GameBase[] array2 = array;
		foreach (GameBase gameBase in array2)
		{
			string text = gameBase.GameName?.ToLower() ?? "";
			string text2 = gameBase.GameType.ToString().ToLower();
			GameSettings value = null;
			if (!string.IsNullOrEmpty(text) && _gameSettings.TryGetValue(text, out value))
			{
				ApplySettingsToGame(gameBase, value);
				continue;
			}
			if (_gameSettings.TryGetValue(text2, out value))
			{
				ApplySettingsToGame(gameBase, value);
				continue;
			}
			bool flag = false;
			string[] array3 = new string[3]
			{
				text.Replace(" ", ""),
				text.Replace(" ", "_"),
				(text2 == "slotmachine") ? "slots" : null
			};
			foreach (string text3 in array3)
			{
				if (!string.IsNullOrEmpty(text3) && _gameSettings.TryGetValue(text3, out value))
				{
					ApplySettingsToGame(gameBase, value);
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				list.Add(gameBase.gameObject);
			}
		}
		if (list.Count > 0)
		{
			Debug.Log($"[RequestSettingsFromApi] No API settings applied for {list.Count} GameObject(s): " + string.Join(", ", list.ConvertAll((GameObject g) => g.name)));
		}
	}

	private void ApplySettingsToGame(GameBase game, GameSettings settings)
	{
		if (settings.estimatedValue.HasValue)
		{
			game.SetEstimatedValue(settings.estimatedValue.Value);
		}
		if (settings.baseMinBet.HasValue)
		{
			game.BaseMinBet = settings.baseMinBet.Value;
		}
		if (settings.baseMaxBet.HasValue)
		{
			game.BaseMaxBet = settings.baseMaxBet.Value;
		}
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
