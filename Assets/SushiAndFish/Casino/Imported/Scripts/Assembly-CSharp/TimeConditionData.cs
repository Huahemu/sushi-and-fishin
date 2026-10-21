namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

[Serializable]
public class TimeConditionData : ChallengeConditionData
{
	[Header("Time Settings")]
	[Tooltip("The time limit in seconds")]
	public float timeLimit = 60f;

	[Tooltip("Whether to check time since challenge started or time since last game")]
	public bool checkSinceStart = true;

	public override bool Evaluate(ChallengeContext context)
	{
		ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState == null)
		{
			return false;
		}
		return (checkSinceStart ? (Time.time - conditionState.startTime) : (Time.time - conditionState.lastGameTime)) <= timeLimit;
	}

	public override float GetProgress(ChallengeContext context)
	{
		ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState == null)
		{
			return 0f;
		}
		float num = (checkSinceStart ? (Time.time - conditionState.startTime) : (Time.time - conditionState.lastGameTime));
		return Mathf.Clamp01(1f - num / timeLimit);
	}

	public override string GetProgressText(ChallengeContext context)
	{
		ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState == null)
		{
			return "Time limit expired";
		}
		float num = (checkSinceStart ? (Time.time - conditionState.startTime) : (Time.time - conditionState.lastGameTime));
		float num2 = Mathf.Max(0f, timeLimit - num);
		return $"{num2:F1}s remaining";
	}

	public override void OnGameResult(long bet, long payout, CasinoGameType gameType, Vector3 position)
	{
		if (!checkSinceStart)
		{
			ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
			if (conditionState != null)
			{
				conditionState.lastGameTime = Time.time;
			}
		}
	}

	public override void ResetCondition()
	{
		ConditionState conditionState = NetworkSingleton<ChallengeManager>.Instance?.GetConditionState(this);
		if (conditionState != null)
		{
			conditionState.startTime = Time.time;
			conditionState.lastGameTime = Time.time;
		}
	}
}

}
