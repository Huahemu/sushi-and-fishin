using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Staff
{
	public sealed class StaffReportToggle : MonoBehaviour
	{
		public GameObject Panel;

		private void OnEnable()
		{
			GetComponent<Button>().onClick.AddListener(Toggle);
			TextMeshProUGUI componentInChildren = GetComponentInChildren<TextMeshProUGUI>();
			if (componentInChildren != null)
			{
				componentInChildren.text = StaffText.Get("report");
			}
			if (Panel != null)
			{
				Panel.SetActive(value: false);
			}
		}

		private void OnDisable()
		{
			GetComponent<Button>().onClick.RemoveListener(Toggle);
		}

		private void Toggle()
		{
			if (Panel != null)
			{
				Panel.SetActive(!Panel.activeSelf);
			}
		}
	}
}
