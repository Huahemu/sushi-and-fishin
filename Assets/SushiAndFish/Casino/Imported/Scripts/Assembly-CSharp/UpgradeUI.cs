namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

public class UpgradeUI : MonoSingleton<UpgradeUI>
{
	[SerializeField]
	private Transform entryParent;

	[SerializeField]
	private UpgradeEntryUI upgradeEntryUI;

	private Dictionary<PlayerUpgradeType, UpgradeEntryUI> _upgradeEntries = new Dictionary<PlayerUpgradeType, UpgradeEntryUI>();

	public void UpdateUpgradeUI(PlayerUpgradeType type, float value, float change)
	{
		float num = new PlayerUpgradeData().Upgrades[type];
		if (_upgradeEntries.TryGetValue(type, out var value2))
		{
			if (value == num)
			{
				Object.Destroy(value2.gameObject);
				_upgradeEntries.Remove(type);
			}
			else
			{
				value2.SetUpgradeEntry(type, value, change);
			}
		}
		else if (value != num)
		{
			UpgradeEntryUI upgradeEntryUI = Object.Instantiate(this.upgradeEntryUI, entryParent);
			_upgradeEntries.Add(type, upgradeEntryUI);
			upgradeEntryUI.SetUpgradeEntry(type, value, change);
		}
	}

	public void ClearUpgradeUI()
	{
		foreach (KeyValuePair<PlayerUpgradeType, UpgradeEntryUI> upgradeEntry in _upgradeEntries)
		{
			Object.Destroy(upgradeEntry.Value.gameObject);
		}
		_upgradeEntries.Clear();
	}
}

}
