namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

[CreateAssetMenu(fileName = "PlayerMoneyTier", menuName = "Player Money/Player Money Tier")]
public class PlayerMoneyTier : ScriptableObject
{
	[Header("Tier Settings")]
	[Tooltip("The tier number (1-4)")]
	public int tierNumber = 1;

	[Header("Money Limits")]
	[Tooltip("Maximum amount of money the player can hold in this tier")]
	public int maxMoney = 1000;
}

}
