namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::UnityEngine;

[Serializable]
public class ConditionState
{
	public int currentWinCount;

	public int consecutiveWinCount;

	public int currentLossCount;

	public int consecutiveLossCount;

	public long totalBetAmount;

	public long totalPayoutAmount;

	public long totalProfit;

	public float startTime;

	public float lastGameTime;

	private Dictionary<string, object> customData = new Dictionary<string, object>();

	public ConditionState()
	{
		startTime = Time.time;
		lastGameTime = Time.time;
	}

	public void Reset()
	{
		currentWinCount = 0;
		consecutiveWinCount = 0;
		currentLossCount = 0;
		consecutiveLossCount = 0;
		totalBetAmount = 0L;
		totalPayoutAmount = 0L;
		totalProfit = 0L;
		startTime = Time.time;
		lastGameTime = Time.time;
		customData.Clear();
	}

	public T GetCustom<T>(string key, T defaultValue = default(T))
	{
		if (customData == null || !customData.TryGetValue(key, out var value))
		{
			return defaultValue;
		}
		if (value is T)
		{
			return (T)value;
		}
		return defaultValue;
	}

	public void SetCustom<T>(string key, T value)
	{
		if (customData == null)
		{
			customData = new Dictionary<string, object>();
		}
		customData[key] = value;
	}

	public int GetCustomInt(string key, int defaultValue = 0)
	{
		return GetCustom(key, defaultValue);
	}

	public void SetCustomInt(string key, int value)
	{
		SetCustom(key, value);
	}
}

}
