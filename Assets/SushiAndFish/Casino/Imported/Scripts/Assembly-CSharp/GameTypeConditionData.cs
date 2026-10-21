namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

[Serializable]
[global::UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "GameTypeConditionData")]
public class GameTypeConditionData : ChallengeConditionData
{
	[Header("Game Type Settings")]
	[Tooltip("The casino game type this condition checks for")]
	public CasinoGameType requiredGameType;

	public override bool Evaluate(ChallengeContext context)
	{
		return context.gameType == requiredGameType;
	}

	public override float GetProgress(ChallengeContext context)
	{
		if (!Evaluate(context))
		{
			return 0f;
		}
		return 1f;
	}

	public override string GetProgressText(ChallengeContext context)
	{
		if (context.gameType != requiredGameType)
		{
			return "Not playing " + requiredGameType;
		}
		return "Playing " + requiredGameType;
	}
}

}
