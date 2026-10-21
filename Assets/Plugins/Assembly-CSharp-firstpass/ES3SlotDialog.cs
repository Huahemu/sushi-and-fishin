using UnityEngine;
using UnityEngine.UI;

public class ES3SlotDialog : MonoBehaviour
{
	[Tooltip("The UnityEngine.UI.Button Component for the Confirm button.")]
	public Button confirmButton;

	[Tooltip("The UnityEngine.UI.Button Component for the Cancel button.")]
	public Button cancelButton;

	protected virtual void OnEnable()
	{
		cancelButton.onClick.AddListener(() =>
		{
			base.gameObject.SetActive(value: false);
		});
	}

	protected virtual void OnDisable()
	{
		cancelButton.onClick.RemoveAllListeners();
	}
}
