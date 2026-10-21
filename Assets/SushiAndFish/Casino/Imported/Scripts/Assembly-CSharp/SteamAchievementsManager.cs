namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

public class SteamAchievementsManager : NetworkSingleton<SteamAchievementsManager>
{
	[Header("Money-Related Achievements")]
	public List<SteamAchievement_SteamworksNET> moneyAchievements;

	public List<bool> unlockStatus;

	private void OnEnable()
	{
		MoneyManager instance = NetworkSingleton<MoneyManager>.Instance;
		instance.OnBalanceChanged = (Action<BalanceChangeData>)Delegate.Combine(instance.OnBalanceChanged, new Action<BalanceChangeData>(CheckBalanceChangeAchievements));
	}

	private void OnDisable()
	{
		MoneyManager instance = NetworkSingleton<MoneyManager>.Instance;
		instance.OnBalanceChanged = (Action<BalanceChangeData>)Delegate.Remove(instance.OnBalanceChanged, new Action<BalanceChangeData>(CheckBalanceChangeAchievements));
	}

	private void Start()
	{
		if (unlockStatus.Count != moneyAchievements.Count)
		{
			unlockStatus = new List<bool>(new bool[moneyAchievements.Count]);
		}
		CheckAndUpdateUnlockStatus();
	}

	private void CheckAndUpdateUnlockStatus()
	{
		foreach (SteamAchievement_SteamworksNET moneyAchievement in moneyAchievements)
		{
			if (moneyAchievement.CheckAchievementState())
			{
				int num = moneyAchievements.IndexOf(moneyAchievement);
				if (num >= 0 && num < unlockStatus.Count)
				{
					unlockStatus[num] = true;
				}
			}
		}
	}

	private void CheckBalanceChangeAchievements(BalanceChangeData data)
	{
		if (data.changeType == ChangeType.Save)
		{
			return;
		}
		foreach (SteamAchievement_SteamworksNET moneyAchievement in moneyAchievements)
		{
			if (moneyAchievement.Value == "ACH_MONEY_10M" && NetworkSingleton<MoneyManager>.Instance.balance >= 10000000 && !unlockStatus[moneyAchievements.IndexOf(moneyAchievement)])
			{
				moneyAchievement.UnlockAchievement();
				unlockStatus[moneyAchievements.IndexOf(moneyAchievement)] = true;
			}
			else if (moneyAchievement.Value == "ACH_MONEY_100M" && NetworkSingleton<MoneyManager>.Instance.balance >= 100000000 && !unlockStatus[moneyAchievements.IndexOf(moneyAchievement)])
			{
				moneyAchievement.UnlockAchievement();
				unlockStatus[moneyAchievements.IndexOf(moneyAchievement)] = true;
			}
			else if (moneyAchievement.Value == "ACH_MONEY_1B" && NetworkSingleton<MoneyManager>.Instance.balance >= 1000000000 && !unlockStatus[moneyAchievements.IndexOf(moneyAchievement)])
			{
				moneyAchievement.UnlockAchievement();
				unlockStatus[moneyAchievements.IndexOf(moneyAchievement)] = true;
			}
			else if (moneyAchievement.Value == "ACH_MONEY_10B" && NetworkSingleton<MoneyManager>.Instance.balance >= 10000000000L && !unlockStatus[moneyAchievements.IndexOf(moneyAchievement)])
			{
				moneyAchievement.UnlockAchievement();
				unlockStatus[moneyAchievements.IndexOf(moneyAchievement)] = true;
			}
			else if (moneyAchievement.Value == "ACH_MONEY_100B" && NetworkSingleton<MoneyManager>.Instance.balance >= 100000000000L && !unlockStatus[moneyAchievements.IndexOf(moneyAchievement)])
			{
				moneyAchievement.UnlockAchievement();
				unlockStatus[moneyAchievements.IndexOf(moneyAchievement)] = true;
			}
		}
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
