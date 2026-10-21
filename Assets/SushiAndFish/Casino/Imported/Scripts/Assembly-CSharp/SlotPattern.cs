namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::UnityEngine;

[CreateAssetMenu(fileName = "Slot Pattern", menuName = "Slot Pattern")]
public class SlotPattern : ScriptableObject
{
	public float multiplier = 1f;

	public BoolGrid3x5 grid;

	public List<int> GetPatternIndexes()
	{
		List<int> list = new List<int>();
		for (int i = 0; i < grid.values.Length; i++)
		{
			if (grid.values[i])
			{
				list.Add(i);
			}
		}
		return list;
	}

	public void Debug()
	{
		foreach (int patternIndex in GetPatternIndexes())
		{
			UnityEngine.Debug.LogWarning(patternIndex);
		}
	}
}

}
