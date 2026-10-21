namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.Extensions;
using global::SushiAndFish.Casino.Steamworks;
using global::UnityEngine;
using global::UnityEngine.SceneManagement;

public class SteamRichPresenceManager : MonoSingleton<SteamRichPresenceManager>
{
	private LobbySettings lobbySettings;

	[SerializeField]
	private bool pendingMoneyPresenceUpdate;

	[SerializeField]
	private float moneyPresenceCooldownDuration = 60f;

	[SerializeField]
	private bool isMoneyPresenceUpdateOnCooldown;

	private float moneyPresenceCooldownEndTime;

	protected override void OnAwake()
	{
		base.OnAwake();
		lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private bool HasMoneyManager()
	{
		return UnityEngine.Object.FindFirstObjectByType<MoneyManager>() != null;
	}

	private bool HasGameManager()
	{
		return UnityEngine.Object.FindFirstObjectByType<GameManager>() != null;
	}

	private void SubscribeToMoneyChanges()
	{
		if (HasMoneyManager())
		{
			MoneyManager instance = NetworkSingleton<MoneyManager>.Instance;
			instance.OnBalanceChanged = (Action<BalanceChangeData>)Delegate.Combine(instance.OnBalanceChanged, new Action<BalanceChangeData>(OnMoneyChanged));
		}
	}

	private void UnsubscribeFromMoneyChanges()
	{
		if (HasMoneyManager())
		{
			MoneyManager instance = NetworkSingleton<MoneyManager>.Instance;
			instance.OnBalanceChanged = (Action<BalanceChangeData>)Delegate.Remove(instance.OnBalanceChanged, new Action<BalanceChangeData>(OnMoneyChanged));
		}
	}

	private void OnDestroy()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
		UnsubscribeFromMoneyChanges();
	}

	private void UpdateGroupPresence(int playerCount)
	{
		if (lobbySettings != null && lobbySettings.steamLobbyID != CSteamID.Nil)
		{
			SteamFriends.SetRichPresence("steam_player_group", lobbySettings.steamLobbyID.m_SteamID.ToString());
			SteamFriends.SetRichPresence("steam_player_group_size", playerCount.ToString());
		}
	}

	private string BuildQuotaBar(long balance, long quota)
	{
		if (quota <= 0)
		{
			return "";
		}
		int num = Mathf.RoundToInt(Mathf.Clamp01((float)balance / (float)quota) * 10f);
		return "[" + new string('#', num) + new string('-', 10 - num) + "]";
	}

	private void OnMoneyChanged(BalanceChangeData data)
	{
		if (!isMoneyPresenceUpdateOnCooldown)
		{
			pendingMoneyPresenceUpdate = true;
		}
	}

	public void Update()
	{
		if (isMoneyPresenceUpdateOnCooldown && Time.unscaledTime >= moneyPresenceCooldownEndTime)
		{
			isMoneyPresenceUpdateOnCooldown = false;
		}
		if (pendingMoneyPresenceUpdate && !isMoneyPresenceUpdateOnCooldown)
		{
			TryUpdateMoneyDrivenPresence();
		}
	}

	private void TryUpdateMoneyDrivenPresence()
	{
		if (!HasGameManager())
		{
			return;
		}
		if (NetworkSingleton<GameManager>.Instance.state == GameState.Lobby)
		{
			SetInHomePresence();
		}
		else
		{
			if (NetworkSingleton<GameManager>.Instance.state != GameState.Game)
			{
				return;
			}
			SetInGamePresence();
		}
		pendingMoneyPresenceUpdate = false;
		isMoneyPresenceUpdateOnCooldown = true;
		moneyPresenceCooldownEndTime = Time.unscaledTime + moneyPresenceCooldownDuration;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		if (SteamManager.Initialized)
		{
			switch (scene.name)
			{
			case "MainMenuScene":
				SetMainMenuPresence();
				break;
			case "CasinoScene":
				UnsubscribeFromMoneyChanges();
				SubscribeToMoneyChanges();
				break;
			case "LoseStateScene":
				SetLoseScenePresence();
				break;
			case "WinStateScene":
				SetWinScenePresence();
				break;
			}
		}
	}

	public void SetLoseScenePresence()
	{
		try
		{
			SteamFriends.SetRichPresence("status", "At the end of the line.");
			SteamFriends.SetRichPresence("steam_display", "#Status_Lose");
			SetConnectString(connectable: false);
			Debug.Log("Rich presence set to: Lose Scene");
		}
		catch (Exception ex)
		{
			Debug.LogWarning("Failed to set lose scene rich presence: " + ex.Message);
		}
	}

	public void SetWinScenePresence()
	{
		try
		{
			SteamFriends.SetRichPresence("status", "Deciding on something very important...");
			SteamFriends.SetRichPresence("steam_display", "#Status_Win");
			SetConnectString(connectable: false);
			Debug.Log("Rich presence set to: Win Scene");
		}
		catch (Exception ex)
		{
			Debug.LogWarning("Failed to set win scene rich presence: " + ex.Message);
		}
	}

	public void SetMainMenuPresence()
	{
		try
		{
			int playerCount = 0;
			int num = 0;
			if (lobbySettings != null && lobbySettings.steamLobbyID != CSteamID.Nil)
			{
				playerCount = SteamMatchmaking.GetNumLobbyMembers(lobbySettings.steamLobbyID);
				num = SteamMatchmaking.GetLobbyMemberLimit(lobbySettings.steamLobbyID);
			}
			else if (lobbySettings != null)
			{
				playerCount = lobbySettings.currentPlayerCount;
				num = lobbySettings.maxPlayers;
			}
			SetConnectString(connectable: true);
			UpdateGroupPresence(playerCount);
			SteamFriends.SetRichPresence("status", "Getting the gang together for an adventure");
			SteamFriends.SetRichPresence("steam_display", "#Status_MainMenu");
			SteamFriends.SetRichPresence("player_count", playerCount.ToString());
			SteamFriends.SetRichPresence("max_players", num.ToString());
			Debug.Log("Rich presence set to: Main Menu");
		}
		catch (Exception ex)
		{
			Debug.LogWarning("Failed to set main menu rich presence: " + ex.Message);
		}
	}

	public void SetInHomePresence()
	{
		try
		{
			long amount = (HasMoneyManager() ? NetworkSingleton<MoneyManager>.Instance.balance : 0);
			int num = (HasGameManager() ? NetworkSingleton<GameManager>.Instance.successfulQuota : 0);
			if (HasGameManager())
			{
				_ = NetworkSingleton<GameManager>.Instance.currentQuota;
			}
			string text = MoneyFormatter.FormatWithDollar(amount);
			int playerCount = 0;
			if (lobbySettings != null && lobbySettings.steamLobbyID != CSteamID.Nil)
			{
				playerCount = SteamMatchmaking.GetNumLobbyMembers(lobbySettings.steamLobbyID);
			}
			SetConnectString(connectable: true);
			UpdateGroupPresence(playerCount);
			int num2 = NetworkSingleton<GameManager>.Instance.daysPassed + 1;
			SteamFriends.SetRichPresence("status", $"Home - Balance: {text} | Day {num2}");
			SteamFriends.SetRichPresence("steam_display", "#Status_InLobby");
			SteamFriends.SetRichPresence("money", text);
			SteamFriends.SetRichPresence("quota_number", num.ToString());
			SteamFriends.SetRichPresence("day_number", num2.ToString());
			UpdatePlayerCount();
			Debug.Log("Rich presence set to: In Lobby");
		}
		catch (Exception ex)
		{
			Debug.LogWarning("Failed to set lobby rich presence: " + ex.Message);
		}
	}

	public void SetInGamePresence()
	{
		if (!SteamManager.Initialized || !HasGameManager() || NetworkSingleton<GameManager>.Instance.state != GameState.Game)
		{
			return;
		}
		try
		{
			long num = (HasMoneyManager() ? NetworkSingleton<MoneyManager>.Instance.balance : 0);
			long num2 = (HasGameManager() ? NetworkSingleton<GameManager>.Instance.currentQuota : 0);
			string text = MoneyFormatter.FormatWithDollar(num);
			string text2 = MoneyFormatter.FormatWithDollar(num2);
			int playerCount = 0;
			if (lobbySettings != null && lobbySettings.steamLobbyID != CSteamID.Nil)
			{
				playerCount = SteamMatchmaking.GetNumLobbyMembers(lobbySettings.steamLobbyID);
			}
			UpdateGroupPresence(playerCount);
			int num3 = Mathf.RoundToInt((num2 > 0) ? (Mathf.Clamp01((float)num / (float)num2) * 100f) : 0f);
			string text3 = BuildQuotaBar(num, num2);
			SteamFriends.SetRichPresence("status", $"Letting it ride - {text}/{text2} ({num3}%) {text3}");
			SteamFriends.SetRichPresence("steam_display", "#Status_Casino");
			SteamFriends.SetRichPresence("money", text);
			SteamFriends.SetRichPresence("quota_amount", text2);
			SteamFriends.SetRichPresence("quota_percent", num3.ToString());
			SteamFriends.SetRichPresence("quota_progress_bar", text3);
			SteamFriends.SetRichPresence("player_count", "");
			SteamFriends.SetRichPresence("max_players", "");
			SetConnectString(connectable: false);
			if (HasGameManager())
			{
				_ = NetworkSingleton<GameManager>.Instance.state == GameState.Game;
			}
			else
				_ = 0;
			_ = SceneManager.GetActiveScene().name == "CasinoScene";
			Debug.Log("Rich presence set to: In Game");
		}
		catch (Exception ex)
		{
			Debug.LogWarning("Failed to set in-game rich presence: " + ex.Message);
		}
	}

	public void UpdatePlayerCount()
	{
		if (!SteamManager.Initialized || lobbySettings == null || lobbySettings.steamLobbyID == CSteamID.Nil)
		{
			return;
		}
		try
		{
			int numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(lobbySettings.steamLobbyID);
			int lobbyMemberLimit = SteamMatchmaking.GetLobbyMemberLimit(lobbySettings.steamLobbyID);
			SteamFriends.SetRichPresence("player_count", numLobbyMembers.ToString());
			SteamFriends.SetRichPresence("max_players", lobbyMemberLimit.ToString());
		}
		catch (Exception ex)
		{
			Debug.LogWarning("Failed to update player count in rich presence: " + ex.Message);
		}
	}

	public void SetConnectString(bool connectable)
	{
		if (!connectable || lobbySettings == null || lobbySettings.steamLobbyID == CSteamID.Nil)
		{
			SteamFriends.SetRichPresence("connect", null);
		}
		else
		{
			SteamFriends.SetRichPresence("connect", $"connect_lobby {lobbySettings.steamLobbyID}");
		}
	}

	public void ClearRichPresence()
	{
		if (!SteamManager.Initialized)
		{
			return;
		}
		try
		{
			SteamFriends.ClearRichPresence();
			Debug.Log("Rich presence cleared");
		}
		catch (Exception ex)
		{
			Debug.LogWarning("Failed to clear rich presence: " + ex.Message);
		}
	}
}

}
