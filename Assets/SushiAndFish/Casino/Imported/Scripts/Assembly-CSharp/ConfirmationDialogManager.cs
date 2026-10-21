namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

public class ConfirmationDialogManager : MonoSingleton<ConfirmationDialogManager>
{
	[Header("Dialog Reference")]
	[SerializeField]
	private ConfirmationDialog confirmationDialog;

	protected override void OnAwake()
	{
		base.OnAwake();
		if (confirmationDialog == null)
		{
			confirmationDialog = UnityEngine.Object.FindFirstObjectByType<ConfirmationDialog>();
			if (confirmationDialog == null)
			{
				Debug.LogWarning("[ConfirmationDialogManager] No ConfirmationDialog found in scene. Please assign one in the inspector or add it to the scene.");
			}
		}
	}

	public void ShowConfirmation(string question, Action onConfirm, Action onCancel = null, string confirmLabel = "Yes", string cancelLabel = "No")
	{
		if (confirmationDialog == null)
		{
			Debug.LogError("[ConfirmationDialogManager] Cannot show confirmation dialog - ConfirmationDialog is not assigned!");
		}
		else
		{
			confirmationDialog.Show(question, onConfirm, onCancel, confirmLabel, cancelLabel);
		}
	}

	public void HideConfirmation()
	{
		if (confirmationDialog != null)
		{
			confirmationDialog.Hide();
		}
	}

	public bool IsDialogVisible()
	{
		if (confirmationDialog != null)
		{
			return confirmationDialog.gameObject.activeSelf;
		}
		return false;
	}
}

}
