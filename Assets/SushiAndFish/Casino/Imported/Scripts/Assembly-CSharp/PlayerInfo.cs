namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

[Serializable]
public class PlayerInfo
{
	public string playerName;

	public ulong steamId;

	public Color playerColor;

	public PlayerInfo(string name, ulong steamId, Color color)
	{
		playerName = name;
		this.steamId = steamId;
		playerColor = color;
	}
}

}
