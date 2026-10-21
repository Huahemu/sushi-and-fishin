namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

[CreateAssetMenu(fileName = "BankTier", menuName = "Bank/Bank Tier")]
public class BankTier : ScriptableObject
{
	[Header("Tier Settings")]
	[Tooltip("The tier number (1-4)")]
	public int tierNumber = 1;

	[Header("Market Fluctuation")]
	[Tooltip("Maximum positive percentage change (e.g., 0.25 = 25% gain, 0.90 = 90% gain)")]
	[Range(0f, 1f)]
	public float maxGainPercent = 0.25f;

	[Tooltip("Maximum negative percentage change (e.g., 0.25 = 25% loss, 1.0 = 100% loss)")]
	[Range(0f, 1f)]
	public float maxLossPercent = 0.25f;

	[Header("Time Settings")]
	[Tooltip("Time interval between market modifications in seconds (60 = 1 minute)")]
	public float modificationInterval = 60f;

	[Header("Deposit Limits")]
	[Tooltip("Minimum amount that can be deposited in this tier")]
	public long minDepositAmount = 1L;

	[Tooltip("Maximum amount that can be deposited in this tier")]
	public long maxDepositAmount = 10000L;

	public float GetRandomModification()
	{
		float minInclusive = 1f - maxLossPercent;
		float maxInclusive = 1f + maxGainPercent;
		return Random.Range(minInclusive, maxInclusive);
	}

	public string GetFluctuationDisplay()
	{
		return $"+{maxGainPercent * 100f:F0}% / -{maxLossPercent * 100f:F0}%";
	}
}

}
