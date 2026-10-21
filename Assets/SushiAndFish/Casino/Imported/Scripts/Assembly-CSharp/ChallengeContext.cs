namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::UnityEngine;

public class ChallengeContext
{
	public long bet;

	public long payout;

	public CasinoGameType gameType;

	public Vector3 gamePosition;

	public long quotaAtActivation;

	public bool hadTipsyFortuneBuff;

	public bool hadInspiringMelodyBuff;

	public bool hadImmunityBuff;

	public Dictionary<string, object> gameSpecificData = new Dictionary<string, object>();

	public bool isWin => payout > bet;

	public bool isLoss => payout < bet;

	public long profit => payout - bet;

	public T GetGameData<T>(string key, T defaultValue = default(T))
	{
		if (gameSpecificData == null || !gameSpecificData.TryGetValue(key, out var value))
		{
			return defaultValue;
		}
		if (value is T)
		{
			return (T)value;
		}
		return defaultValue;
	}

	public bool HadBuff(PlayerBuffType buffType)
	{
		return buffType switch
		{
			PlayerBuffType.TipsyFortune => hadTipsyFortuneBuff, 
			PlayerBuffType.InspiringMelody => hadInspiringMelodyBuff, 
			PlayerBuffType.Immunity => hadImmunityBuff, 
			_ => false, 
		};
	}
}

}
