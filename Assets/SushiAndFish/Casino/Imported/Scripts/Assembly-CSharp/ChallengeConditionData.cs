namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

[Serializable]
public abstract class ChallengeConditionData
{
	[Header("Condition Settings")]
	[Tooltip("Description of what this condition checks")]
	[TextArea(2, 4)]
	public string description;

	[Tooltip("When enabled, this condition filters which results count toward other conditions.")]
	public bool useAsResultFilter;

	public abstract bool Evaluate(ChallengeContext context);

	public abstract float GetProgress(ChallengeContext context);

	public abstract string GetProgressText(ChallengeContext context);

	public virtual void ResetCondition()
	{
	}

	public virtual void OnGameResult(long bet, long payout, CasinoGameType gameType, Vector3 position)
	{
	}
}

}
