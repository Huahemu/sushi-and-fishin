namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::System.Linq;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

[CreateAssetMenu(menuName = "Game Settings/Challenge Settings", fileName = "ChallengeSettings")]
public class ChallengeSettings : ScriptableObject
{
	public int dailyAvailableChallengeCount = 1;

	[Tooltip("Cost in tickets to reroll a challenge")]
	[Min(0f)]
	public int challengeRerollPrice = 1;

	public int[] ticketRewards = new int[4];

	[Header("Challenges")]
	[Tooltip("List of all challenges in the game. These will be available in the ChallengeBooth.")]
	public List<Challenge> challenges = new List<Challenge>();

	[Header("Challenge Lists by Difficulty")]
	[Tooltip("First floor challenges")]
	public List<Challenge> firstFloorChallenges = new List<Challenge>();

	[Tooltip("Medium difficulty challenges")]
	public List<Challenge> secondFloorChallenges = new List<Challenge>();

	[Tooltip("Third floor challenges")]
	public List<Challenge> thirdFloorChallenges = new List<Challenge>();

	[Tooltip("Fourth floor challenges")]
	public List<Challenge> fourthFloorChallenges = new List<Challenge>();

	[Header("Runtime: Currently Active Challenges")]
	[Tooltip("Challenges that players have purchased and are currently working on (runtime only - updates automatically)")]
	public List<ActiveChallengeInfo> activeChallenges = new List<ActiveChallengeInfo>();

	public static event Action<ChallengeSettings> SettingsChanged;

	public void NotifyChanged()
	{
		SettingsChanged?.Invoke(this);
	}

	public void UpdateActiveChallenges()
	{
		activeChallenges.Clear();
		if (NetworkSingleton<ChallengeManager>.Instance == null)
		{
			return;
		}
		foreach (ChallengeProgress activeChallenge in NetworkSingleton<ChallengeManager>.Instance.GetActiveChallenges())
		{
			if (activeChallenge != null && !(activeChallenge.challenge == null) && !activeChallenge.isClaimed)
			{
				activeChallenges.Add(new ActiveChallengeInfo(activeChallenge.challenge, "Server", activeChallenge.progress, activeChallenge.progressText, activeChallenge.isCompleted, activeChallenge.isClaimed));
			}
		}
	}

	public List<Challenge> GetChallengesByFloorIndex(int floorIndex)
	{
		return floorIndex switch
		{
			0 => firstFloorChallenges, 
			1 => secondFloorChallenges, 
			2 => thirdFloorChallenges, 
			3 => fourthFloorChallenges, 
			_ => new List<Challenge>(), 
		};
	}

	public Challenge GetRandomChallenge(int floorIndex)
	{
		List<Challenge> challengesByFloorIndex = GetChallengesByFloorIndex(floorIndex);
		if (challengesByFloorIndex == null || challengesByFloorIndex.Count == 0)
		{
			return null;
		}
		int floorIndex2 = floorIndex + 1;
		HashSet<CasinoGameType> availableGameTypes = NextCasinoPredicter.GetAvailableGameTypesForFloor(floorIndex2);
		List<Challenge> list = challengesByFloorIndex.Where((Challenge c) => c != null).Where((Challenge c) =>
		{
			HashSet<CasinoGameType> requiredGameTypes = c.GetRequiredGameTypes();
			return requiredGameTypes == null || requiredGameTypes.Count == 0 || requiredGameTypes.Overlaps(availableGameTypes);
		}).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		return list.GetRandomElement();
	}

	public int GetTicketReward(int floorIndex)
	{
		return ticketRewards[floorIndex];
	}
}

}
