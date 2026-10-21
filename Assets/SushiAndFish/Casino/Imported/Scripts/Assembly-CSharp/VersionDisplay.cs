namespace SushiAndFish.Casino.Original
{
using global::TMPro;
using global::UnityEngine;

public class VersionDisplay : MonoBehaviour
{
	private TextMeshProUGUI versionText;

	private void Start()
	{
		versionText = GetComponent<TextMeshProUGUI>();
		versionText.text = Application.version;
	}
}

}
