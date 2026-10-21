namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;
using global::UnityEngine.Networking;

public class GameSettingsAPIManager : NetworkSingleton<GameSettingsAPIManager>
{
	[Serializable]
	private class MainSettingsResponse
	{
		public string game_id;

		public string game_name;

		public SettingResponse[] settings;
	}

	[Header("API Settings")]
	[SerializeField]
	private string gameId = "gamblelite";

	[SerializeField]
	private string apiBaseUrl = "https://api.diabolical.studio/rest-api/gameSettings";

	[SerializeField]
	private GameSettings gameSettings;

	public override void OnStartServer()
	{
		base.OnStartServer();
		if (CasinoLab.CasinoLabMode.Active) return;
		StartCoroutine(FetchAndApplyMainSettingsCoroutine());
	}

	public void ReloadMainSettings()
	{
		if (!base.isServer)
		{
			Debug.LogWarning("[GameSettingsAPIManager] Not on server - cannot reload settings");
		}
		else
		{
			StartCoroutine(FetchAndApplyMainSettingsCoroutine());
		}
	}

	public IEnumerator FetchAndApplyMainSettingsCoroutine()
	{
		gameSettings = SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
		long num = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		string uri = $"{apiBaseUrl}/{gameId}/main-settings?t={num}";
		using UnityWebRequest request = UnityWebRequest.Get(uri);
		request.SetRequestHeader("Content-Type", "application/json");
		request.SetRequestHeader("Cache-Control", "no-cache, no-store, must-revalidate");
		request.SetRequestHeader("Pragma", "no-cache");
		request.SetRequestHeader("Expires", "0");
		yield return request.SendWebRequest();
		if (request.result != UnityWebRequest.Result.Success)
		{
			Debug.LogError("[GameSettingsAPIManager] Failed to fetch main settings: " + request.error);
			if (request.downloadHandler != null)
			{
				Debug.LogError("[GameSettingsAPIManager] Response: " + request.downloadHandler.text);
			}
			yield break;
		}
		try
		{
			MainSettingsResponse mainSettingsResponse = JsonUtility.FromJson<MainSettingsResponse>(request.downloadHandler.text);
			if (mainSettingsResponse?.settings != null)
			{
				DynamicSettingsApplier.ApplySettings(mainSettingsResponse.settings);
			}
			else
			{
				Debug.LogWarning("[GameSettingsAPIManager] ⚠\ufe0f Response did not contain settings array");
			}
		}
		catch (Exception ex)
		{
			Debug.LogError("[GameSettingsAPIManager] ❌ Error parsing main settings: " + ex.Message + "\n" + ex.StackTrace);
		}
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
