namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::UnityEngine;

[CreateAssetMenu(fileName = "WordBank", menuName = "Gamble With Your Friends/Word Bank", order = 0)]
public class WordBankSO : ScriptableObject
{
	[Header("Pools (each item counts as ONE slot no matter how many words)")]
	public List<Pool> pools = new List<Pool>();

	private Dictionary<SlotType, List<string>> _map;

	private static readonly List<string> _empty = new List<string>();

	public List<string> GetPool(SlotType slot)
	{
		if (_map == null)
		{
			_map = new Dictionary<SlotType, List<string>>();
			foreach (Pool pool in pools)
			{
				if (!_map.ContainsKey(pool.slot))
				{
					_map[pool.slot] = new List<string>();
				}
				_map[pool.slot].AddRange(pool.items);
			}
		}
		if (!_map.TryGetValue(slot, out var value))
		{
			return _empty;
		}
		return value;
	}
}

}
