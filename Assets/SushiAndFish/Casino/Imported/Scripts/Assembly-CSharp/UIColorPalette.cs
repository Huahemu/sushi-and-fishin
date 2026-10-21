namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::Mirror;
using global::SushiAndFish.Casino.Steamworks;
using global::UnityEngine;

[CreateAssetMenu(menuName = "Game Settings/Color Settings", fileName = "ColorSettings")]
public class UIColorPalette : ScriptableObject
{
	public Color profitGreen = Color.green;

	public Color lossRed = Color.red;

	public Color ticketYellow = Color.yellow;

	public Color white = Color.white;

	public Color black = Color.black;

	public Color playerColor = Color.blue;

	public Color gwyfMainColor = Color.gray;

	public Color gwyfSecondaryColor = Color.gray;

	[Header("Player Colors")]
	[Tooltip("List of colors that will be assigned to players")]
	public Color[] playerColors = new Color[4]
	{
		new Color(0.282f, 0.784f, 0.424f),
		new Color(0.694f, 0.282f, 0.784f),
		new Color(0.282f, 0.541f, 0.784f),
		new Color(0.784f, 0.282f, 0.282f)
	};

	private Dictionary<NetworkIdentity, Color> playerColorMap = new Dictionary<NetworkIdentity, Color>();

	public Color NPCColor => playerColors[UnityEngine.Random.Range(0, playerColors.Length)];

	public static event Action<UIColorPalette> PaletteChanged;

	public void NotifyChanged()
	{
		PaletteChanged?.Invoke(this);
	}

	public Color GetPlayerColor(NetworkIdentity playerId)
	{
		if (playerColorMap.TryGetValue(playerId, out var value))
		{
			return value;
		}
		return playerColor;
	}

	public void SetPlayerColor(NetworkIdentity playerId, Color color)
	{
		playerColorMap[playerId] = color;
		NotifyChanged();
	}

	public void RemovePlayerColor(NetworkIdentity playerId)
	{
		if (playerColorMap.Remove(playerId))
		{
			NotifyChanged();
		}
	}

	public void UpdateLocalPlayerColor(LobbySettings lobbySettings)
	{
		if (!(lobbySettings == null))
		{
			ulong steamID = SteamUser.GetSteamID().m_SteamID;
			if (lobbySettings.GetPlayerBySteamId(steamID) != null)
			{
				NotifyChanged();
			}
		}
	}
}

}
