namespace SushiAndFish.Casino.Original
{
using global::TMPro;
using global::UnityEngine;

public class UpgradeEntryUI : MonoBehaviour
{
	[SerializeField]
	private TextMeshProUGUI labelText;

	[SerializeField]
	private TextMeshProUGUI descriptionText;

	[SerializeField]
	private TextMeshProUGUI valueText;

	public void SetUpgradeEntry(PlayerUpgradeType type, float value, float change)
	{
		string text = "";
		string text2 = "";
		switch (type)
		{
		case PlayerUpgradeType.GamblersConfidence:
			text = "Gambler's Confidence";
			text2 = "Increases profit";
			value--;
			break;
		case PlayerUpgradeType.Insurance:
			text = "Insurance";
			text2 = "Reduces loss";
			break;
		case PlayerUpgradeType.Stakeholder:
			text = "Stakeholder";
			text2 = "Empowers held items";
			value--;
			break;
		case PlayerUpgradeType.BonusDraw:
			text = "Bonus Draw";
			text2 = "Gives a chance to earn ticket on win";
			break;
		}
		labelText.text = text;
		descriptionText.text = text2;
		valueText.text = (value * 100f).ToString("0.#") + "%";
	}
}

}
