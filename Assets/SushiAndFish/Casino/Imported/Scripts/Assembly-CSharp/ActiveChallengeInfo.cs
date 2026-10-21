namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

[Serializable]
public class ActiveChallengeInfo
{
	[Tooltip("The challenge being worked on")]
	public Challenge challenge;

	[Tooltip("Player name working on this challenge")]
	public string playerName;

	[Tooltip("Current progress (0-1)")]
	public float progress;

	[Tooltip("Progress text description")]
	public string progressText;

	[Tooltip("Whether the challenge is completed")]
	public bool isCompleted;

	[Tooltip("Whether the reward has been claimed")]
	public bool isClaimed;

	public ActiveChallengeInfo(Challenge challenge, string playerName, float progress, string progressText, bool isCompleted, bool isClaimed)
	{
		this.challenge = challenge;
		this.playerName = playerName;
		this.progress = progress;
		this.progressText = progressText;
		this.isCompleted = isCompleted;
		this.isClaimed = isClaimed;
	}
}

}
