namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

[Serializable]
public class ItemAction
{
	[Tooltip("Display name for the action (e.g., 'Use', 'Throw', 'Consume')")]
	public string actionName;

	[Tooltip("Key to display for this action (e.g., 'E', 'F', 'G', 'Left Click')")]
	public string key;

	[Tooltip("If true, shows 'Hold' text. If false, no hold text is shown.")]
	public bool isHold;
}

}
