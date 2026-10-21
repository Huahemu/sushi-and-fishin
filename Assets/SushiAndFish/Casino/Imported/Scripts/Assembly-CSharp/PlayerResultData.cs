namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;

[Serializable]
public class PlayerResultData
{
	public long totalBets;

	public long totalPayouts;

	public Dictionary<CasinoGameType, GameResultBreakdown> ByGameType = new Dictionary<CasinoGameType, GameResultBreakdown>();

	public long NetProfit => totalPayouts - totalBets;
}

}
