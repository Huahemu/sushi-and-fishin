namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;

public class ConditionStateTracker
{
	private Dictionary<ChallengeConditionData, ConditionState> conditionStates = new Dictionary<ChallengeConditionData, ConditionState>();

	public ConditionState GetOrCreateState(ChallengeConditionData condition)
	{
		if (condition == null)
		{
			return null;
		}
		if (!conditionStates.ContainsKey(condition))
		{
			conditionStates[condition] = new ConditionState();
		}
		return conditionStates[condition];
	}

	public void ResetAll()
	{
		foreach (ConditionState value in conditionStates.Values)
		{
			value?.Reset();
		}
	}

	public void ResetCondition(ChallengeConditionData condition)
	{
		if (condition != null && conditionStates.ContainsKey(condition))
		{
			conditionStates[condition]?.Reset();
		}
	}
}

}
