namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;

public class DaySummaryRuntime : MonoSingleton<DaySummaryRuntime>
{
	[Serializable]
	public struct ChallengeReward
	{
		public string challengeName;

		public int tickets;
	}

	private readonly List<ChallengeReward> _completedChallenges = new List<ChallengeReward>();

	public IReadOnlyList<ChallengeReward> CompletedChallenges => _completedChallenges;

	public void Clear()
	{
		_completedChallenges.Clear();
	}

	public void Add(string challengeName, int tickets)
	{
		if (tickets > 0)
		{
			_completedChallenges.Add(new ChallengeReward
			{
				challengeName = challengeName,
				tickets = tickets
			});
		}
	}
}

}
