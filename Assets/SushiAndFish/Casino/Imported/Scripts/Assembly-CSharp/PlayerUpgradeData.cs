namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;

[Serializable]
public class PlayerUpgradeData
{
	public Dictionary<PlayerUpgradeType, float> Upgrades = new Dictionary<PlayerUpgradeType, float>();

	public PlayerUpgradeData()
	{
		Upgrades[PlayerUpgradeType.GamblersConfidence] = 1f;
		Upgrades[PlayerUpgradeType.Insurance] = 0f;
		Upgrades[PlayerUpgradeType.Stakeholder] = 1f;
		Upgrades[PlayerUpgradeType.BonusDraw] = 0f;
	}
}

}
