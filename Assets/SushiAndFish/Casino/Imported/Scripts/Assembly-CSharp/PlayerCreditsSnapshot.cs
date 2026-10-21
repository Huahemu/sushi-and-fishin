namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;

[Serializable]
public class PlayerCreditsSnapshot
{
	[Serializable]
	public struct CosmeticEntry
	{
		public CosmeticType type;

		public int cosmeticId;
	}

	public ulong steamId;

	public string displayName;

	public List<CosmeticEntry> cosmetics = new List<CosmeticEntry>();
}

}
