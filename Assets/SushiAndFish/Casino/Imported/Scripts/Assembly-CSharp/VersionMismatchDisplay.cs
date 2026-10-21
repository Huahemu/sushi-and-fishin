namespace SushiAndFish.Casino.Original
{
using global::TMPro;
using global::UnityEngine;

public class VersionMismatchDisplay : MonoBehaviour
{
	[Header("UI References")]
	[SerializeField]
	private GameObject versionMismatchPanel;

	[SerializeField]
	private TextMeshProUGUI versionText;

	private void Awake()
	{
		if (versionMismatchPanel != null)
		{
			versionMismatchPanel.SetActive(value: false);
		}
	}

	public void ShowVersionMismatch(string playerVersion)
	{
		if (versionMismatchPanel != null)
		{
			versionMismatchPanel.SetActive(value: true);
			if (versionText != null && !string.IsNullOrEmpty(playerVersion))
			{
				versionText.text = "Version: " + playerVersion;
			}
		}
	}

	public void HideVersionMismatch()
	{
		if (versionMismatchPanel != null)
		{
			versionMismatchPanel.SetActive(value: false);
		}
	}
}

}
