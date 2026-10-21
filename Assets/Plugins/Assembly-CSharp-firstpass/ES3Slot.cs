using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ES3Slot : MonoBehaviour
{
	[Tooltip("The text label containing the slot name.")]
	public TMP_Text nameLabel;

	[Tooltip("The text label containing the last updated timestamp for the slot.")]
	public TMP_Text timestampLabel;

	[Tooltip("The confirmation dialog to show if showConfirmationIfExists is true.")]
	public GameObject confirmationDialog;

	public ES3SlotManager mgr;

	[Tooltip("The button for selecting this slot.")]
	public Button selectButton;

	[Tooltip("The button for deleting this slot.")]
	public Button deleteButton;

	[Tooltip("The button for undoing the deletion of this slot.")]
	public Button undoButton;

	public bool markedForDeletion;

	public virtual void OnEnable()
	{
		selectButton.onClick.AddListener(TrySelectSlot);
		deleteButton.onClick.AddListener(MarkSlotForDeletion);
		undoButton.onClick.AddListener(UnmarkSlotForDeletion);
	}

	public virtual void OnDisable()
	{
		selectButton.onClick.RemoveAllListeners();
		deleteButton.onClick.RemoveAllListeners();
		undoButton.onClick.RemoveAllListeners();
		if (markedForDeletion)
		{
			DeleteSlot();
		}
	}

	protected virtual void TrySelectSlot()
	{
		if (mgr.showConfirmationIfExists)
		{
			if (confirmationDialog == null)
			{
				Debug.LogError("The confirmationDialog field of this ES3SelectSlot Component hasn't been set in the inspector.", this);
			}
			if (ES3.FileExists(GetSlotPath()))
			{
				confirmationDialog.SetActive(value: true);
				confirmationDialog.GetComponent<ES3SlotDialog>().confirmButton.onClick.AddListener(DeleteThenSelectSlot);
				return;
			}
		}
		SelectSlot();
	}

	protected virtual void DeleteThenSelectSlot()
	{
		DeleteSlot();
		SelectSlot();
	}

	protected virtual void SelectSlot()
	{
		confirmationDialog?.SetActive(value: false);
		ES3SlotManager.selectedSlotPath = GetSlotPath();
		ES3Settings.defaultSettings.path = ES3SlotManager.selectedSlotPath;
		mgr.onAfterSelectSlot?.Invoke();
		if (!string.IsNullOrEmpty(mgr.loadSceneAfterSelectSlot))
		{
			SceneManager.LoadScene(mgr.loadSceneAfterSelectSlot);
		}
	}

	protected virtual void MarkSlotForDeletion()
	{
		markedForDeletion = true;
		undoButton.gameObject.SetActive(value: true);
		deleteButton.gameObject.SetActive(value: false);
	}

	protected virtual void UnmarkSlotForDeletion()
	{
		markedForDeletion = false;
		undoButton.gameObject.SetActive(value: false);
		deleteButton.gameObject.SetActive(value: true);
	}

	public virtual void DeleteSlot()
	{
		ES3.DeleteFile(GetSlotPath(), new ES3Settings(ES3.Location.Cache));
		ES3.DeleteFile(GetSlotPath(), new ES3Settings(ES3.Location.File));
		Object.Destroy(base.gameObject);
	}

	public virtual string GetSlotPath()
	{
		return mgr.GetSlotPath(nameLabel.text);
	}
}
