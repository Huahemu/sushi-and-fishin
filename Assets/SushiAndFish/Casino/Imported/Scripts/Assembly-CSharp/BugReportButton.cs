namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;
using global::UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class BugReportButton : MonoBehaviour
{
	[SerializeField]
	private BugReportFormUI formUI;

	private void Awake()
	{
		Button component = GetComponent<Button>();
		if (component != null)
		{
			component.onClick.RemoveAllListeners();
			component.onClick.AddListener(OpenForm);
		}
	}

	private void OpenForm()
	{
		if (formUI != null)
		{
			formUI.OpenForm();
			return;
		}
		BugReportFormUI bugReportFormUI = Object.FindFirstObjectByType<BugReportFormUI>();
		if (bugReportFormUI != null)
		{
			bugReportFormUI.OpenForm();
		}
	}
}

}
