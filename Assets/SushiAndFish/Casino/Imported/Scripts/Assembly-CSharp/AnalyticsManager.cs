namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Text;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::UnityEngine;
using global::UnityEngine.Networking;

public class AnalyticsManager : NetworkSingleton<AnalyticsManager>
{
	[Serializable]
	private class AnalyticsData
	{
		public string game_id;

		public string sub_game_name;

		public string player_name;

		public float bet_amount;

		public float bet_multiplier;

		public float win_amount;

		public string round_id;

		public string session_id;

		public string bet_type;
	}

	[Header("API Settings")]
	[SerializeField]
	private string apiKey = "";

	[SerializeField]
	private string apiEndpoint = "";

	[SerializeField]
	private string gameId = "";

	private string sessionId;

	private int roundCounter;

	public override void OnStartServer()
	{
		sessionId = $"session_{UnityEngine.Random.Range(100000, 999999)}";
		roundCounter = 0;
	}

	[Server]
	public void SendAnalytics(GameBase game, string playerName, long betAmount, long payout)
	{
		if (CasinoLab.CasinoLabMode.Active) return;
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void AnalyticsManager::SendAnalytics(GameBase,System.String,System.Int64,System.Int64)' called when server was not active");
			return;
		}
		if (string.IsNullOrEmpty(apiKey))
		{
			Debug.LogWarning("[AnalyticsManager] API key is not set. Skipping analytics.");
			return;
		}
		roundCounter++;
		string round_id = $"round_{roundCounter}";
		string subGameName = GetSubGameName(game);
		float bet_multiplier = ((betAmount > 0) ? ((float)payout / (float)betAmount) : 0f);
		string bet_type = "standard";
		AnalyticsData data = new AnalyticsData
		{
			game_id = gameId,
			sub_game_name = subGameName,
			player_name = playerName,
			bet_amount = betAmount,
			bet_multiplier = bet_multiplier,
			win_amount = payout,
			round_id = round_id,
			session_id = sessionId,
			bet_type = bet_type
		};
		StartCoroutine(SendAnalyticsCoroutine(data));
	}

	private string GetSubGameName(GameBase game)
	{
		return game.GameType.ToString().ToLower();
	}

	private IEnumerator SendAnalyticsCoroutine(AnalyticsData data)
	{
		string s = JsonUtility.ToJson(data);
		byte[] bytes = Encoding.UTF8.GetBytes(s);
		using UnityWebRequest request = new UnityWebRequest(apiEndpoint, "POST");
		request.uploadHandler = new UploadHandlerRaw(bytes);
		request.downloadHandler = new DownloadHandlerBuffer();
		request.SetRequestHeader("x-api-key", apiKey);
		request.SetRequestHeader("Content-Type", "application/json");
		yield return request.SendWebRequest();
		if (request.result != UnityWebRequest.Result.Success)
		{
			Debug.LogError("[AnalyticsManager] Failed to send analytics: " + request.error);
			Debug.LogError("[AnalyticsManager] Response: " + request.downloadHandler?.text);
		}
		else
		{
			Debug.Log("[AnalyticsManager] Analytics sent successfully for round " + data.round_id);
		}
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
