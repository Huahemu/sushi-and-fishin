namespace SushiAndFish.Casino.Original
{
using global::SushiAndFish.Casino.Steamworks;
using global::UnityEngine;

[CreateAssetMenu(fileName = "ACH_", menuName = "Game Data/Steam/Achievement")]
public class SteamAchievement_SteamworksNET : StringVariable
{
	public void AssignValueByAssetName()
	{
		Value = base.name;
		Debug.Log("Assigned achievement ID '" + Value + "' from asset name.");
	}

	public bool CheckAchievementState()
	{
		if (!SteamManager.Initialized)
		{
			Debug.LogWarning("Steam not initialized.");
			return false;
		}
		string value = Value;
		bool pbAchieved = false;
		if (!SteamUserStats.GetAchievement(value, out pbAchieved))
		{
			Debug.LogError("Failed to get achievement '" + value + "'.");
			return false;
		}
		Debug.Log($"Achievement {value} status: {pbAchieved}");
		return pbAchieved;
	}

	public void UnlockAchievement()
	{
		if (!SteamManager.Initialized)
		{
			Debug.LogWarning("Steam not initialized.");
			return;
		}
		string value = Value;
		if (!SteamUserStats.SetAchievement(value))
		{
			Debug.LogError("Failed to set achievement '" + value + "'.");
			return;
		}
		SteamUserStats.StoreStats();
		Debug.Log("Achievement " + value + " unlocked.");
	}

	public void ClearAchievement()
	{
		if (!SteamManager.Initialized)
		{
			Debug.LogWarning("Steam not initialized.");
			return;
		}
		string value = Value;
		if (!SteamUserStats.ClearAchievement(value))
		{
			Debug.LogError("Failed to clear achievement '" + value + "'.");
			return;
		}
		SteamUserStats.StoreStats();
		Debug.Log("Achievement " + value + " cleared.");
	}
}

}
