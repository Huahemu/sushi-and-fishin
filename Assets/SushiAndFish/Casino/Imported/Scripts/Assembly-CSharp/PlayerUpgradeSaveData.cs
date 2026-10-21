namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;

[Serializable]
public class PlayerUpgradeSaveData
{
	public string steamId;

	public List<PlayerUpgradeValue> upgrades = new List<PlayerUpgradeValue>();
}

}
