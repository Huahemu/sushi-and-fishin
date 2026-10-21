namespace SushiAndFish.Casino.Original
{
using global::System;

[Serializable]
public class PlayerResultDebugEntry
{
	public PlayerProfile player;

	public long totalBets;

	public long totalPayouts;

	public long netProfit;
}

}
