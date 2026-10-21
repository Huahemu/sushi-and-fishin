namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Text;
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::UnityEngine;
using global::UnityEngine.Networking;
using global::UnityEngine.SceneManagement;

public class BugReportAPIClient : MonoSingleton<BugReportAPIClient>
{
	[Serializable]
	private class DiscordWebhookBody
	{
		public string username;

		public DiscordEmbed[] embeds;
	}

	[Serializable]
	private class DiscordEmbed
	{
		public string title;

		public string description;

		public int color;

		public DiscordEmbedFooter footer;
	}

	[Serializable]
	private class DiscordEmbedFooter
	{
		public string text;
	}

	[Header("Discord webhook")]
	[Tooltip("Full URL: https://discord.com/api/webhooks/...")]
	[SerializeField]
	private string discordWebhookUrl = "";

	[SerializeField]
	private string gameId = "gamblelite";

	private static string _sessionId;

	public static string SessionId
	{
		get
		{
			if (string.IsNullOrEmpty(_sessionId))
			{
				_sessionId = Guid.NewGuid().ToString();
			}
			return _sessionId;
		}
	}

	public void FillContext(BugReportPayload p)
	{
		p.version = Application.version;
		p.build = Application.version;
		p.platform = Application.platform.ToString();
		p.os = SystemInfo.operatingSystem;
		p.gpu = SystemInfo.graphicsDeviceName;
		p.cpu = SystemInfo.processorType;
		p.ram = SystemInfo.systemMemorySize;
		p.driverVersion = SystemInfo.graphicsDeviceVersion;
		p.scene = SceneManager.GetActiveScene().name;
		p.timestampUtc = DateTime.UtcNow.ToString("o");
		p.gameId = gameId;
		p.sessionId = SessionId;
		p.branch = "";
		if (NetworkClient.active)
		{
			p.role = (NetworkServer.active ? "Host" : "Client");
			p.networkBackend = "Mirror";
			if (NetworkManager.singleton != null && NetworkManager.singleton.numPlayers > 0)
			{
				p.playerCount = NetworkManager.singleton.numPlayers;
			}
		}
	}

	public void SendReport(BugReportPayload payload, Action<bool, string, string> onComplete)
	{
		if (string.IsNullOrWhiteSpace(discordWebhookUrl))
		{
			onComplete?.Invoke(arg1: false, "Discord webhook URL not set.", null);
		}
		else
		{
			StartCoroutine(SendReportCoroutine(payload, onComplete));
		}
	}

	private IEnumerator SendReportCoroutine(BugReportPayload payload, Action<bool, string, string> onComplete)
	{
		string s = BuildDiscordWebhookJson(payload);
		byte[] bytes = Encoding.UTF8.GetBytes(s);
		using UnityWebRequest request = new UnityWebRequest(discordWebhookUrl, "POST");
		request.uploadHandler = new UploadHandlerRaw(bytes);
		request.downloadHandler = new DownloadHandlerBuffer();
		request.SetRequestHeader("Content-Type", "application/json");
		yield return request.SendWebRequest();
		bool flag = request.result == UnityWebRequest.Result.Success;
		string text = null;
		if (!flag)
		{
			text = ((request.downloadHandler != null && request.downloadHandler.data != null && request.downloadHandler.data.Length != 0) ? request.downloadHandler.text : request.error);
			if (string.IsNullOrEmpty(text))
			{
				text = "Request failed.";
			}
		}
		onComplete?.Invoke(flag, text ?? "", null);
	}

	private static string BuildDiscordWebhookJson(BugReportPayload p)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("**Severity:** " + p.severity + "  **Category:** " + p.category + "  **Frequency:** " + p.frequency);
		stringBuilder.AppendLine($"**Can reproduce now:** {p.canReproduceNow}");
		if (!string.IsNullOrEmpty(p.whatHappened))
		{
			stringBuilder.AppendLine().AppendLine("**What happened**").AppendLine(p.whatHappened);
		}
		if (!string.IsNullOrEmpty(p.expected))
		{
			stringBuilder.AppendLine().AppendLine("**Expected**").AppendLine(p.expected);
		}
		if (p.reproSteps != null && p.reproSteps.Length != 0)
		{
			stringBuilder.AppendLine().AppendLine("**Repro**");
			for (int i = 0; i < p.reproSteps.Length; i++)
			{
				stringBuilder.AppendLine($"{i + 1}. {p.reproSteps[i]}");
			}
		}
		stringBuilder.AppendLine().AppendLine("**Context**");
		stringBuilder.AppendLine("Game: `" + p.gameId + "`  Version: `" + p.version + "`  Scene: `" + p.scene + "`");
		stringBuilder.AppendLine("Platform: `" + p.platform + "`  OS: `" + Trunc(p.os, 120) + "`");
		stringBuilder.AppendLine($"GPU: `{Trunc(p.gpu, 80)}`  CPU: `{Trunc(p.cpu, 80)}`  RAM MB: `{p.ram}`");
		if (!string.IsNullOrEmpty(p.role))
		{
			stringBuilder.AppendLine($"Network: `{p.networkBackend}`  Role: `{p.role}`  Players: `{p.playerCount}`");
		}
		stringBuilder.AppendLine("Session: `" + p.sessionId + "`  UTC: `" + p.timestampUtc + "`");
		string description = Trunc(stringBuilder.ToString(), 3900);
		string title = Trunc(string.IsNullOrEmpty(p.title) ? "Bug report" : p.title, 250);
		DiscordWebhookBody discordWebhookBody = new DiscordWebhookBody();
		discordWebhookBody.username = "Bug report";
		discordWebhookBody.embeds = new DiscordEmbed[1]
		{
			new DiscordEmbed
			{
				title = title,
				description = description,
				color = 15158332,
				footer = new DiscordEmbedFooter
				{
					text = p.gameId + " · " + p.sessionId
				}
			}
		};
		return JsonUtility.ToJson(discordWebhookBody);
	}

	private static string Trunc(string s, int max)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		if (s.Length <= max)
		{
			return s;
		}
		return s.Substring(0, max - 1) + "…";
	}
}

}
