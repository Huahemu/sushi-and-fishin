namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::UnityEngine;

public class BossGame : MonoBehaviour
{
	public int requiredConsecutiveWins = 3;

	public int currentConsecutiveWins;

	public List<Renderer> winIndicators;

	private void Start()
	{
		UpdateWinIndicators();
	}

	public void AddToConsecutiveWins()
	{
		currentConsecutiveWins++;
		if (currentConsecutiveWins >= requiredConsecutiveWins)
		{
			Debug.Log("Player has won the Boss Game!");
		}
		UpdateWinIndicators();
	}

	public void ResetConsecutiveWins()
	{
	}

	private void UpdateWinIndicators()
	{
		for (int i = 0; i < winIndicators.Count; i++)
		{
			if (i < currentConsecutiveWins)
			{
				winIndicators[i].material.color = Color.green;
			}
			else
			{
				winIndicators[i].material.color = Color.red;
			}
		}
	}
}

}
