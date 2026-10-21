namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public class SteamIdComponent : MonoBehaviour
{
	[SerializeField]
	private ulong steamId;

	public ulong SteamId => steamId;

	public SteamIdComponent SetSteamID(ulong id)
	{
		steamId = id;
		if (TryGetComponent<UIColorManager>(out var component))
		{
			component.ApplyColors();
		}
		return this;
	}
}

}
