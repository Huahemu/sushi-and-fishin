using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ES3CreateSlot : MonoBehaviour
{
	[Tooltip("The button used to bring up the 'Create Slot' dialog.")]
	public Button createButton;

	[Tooltip("The ES3SlotDialog Component of the Create Slot dialog")]
	public ES3SlotDialog createDialog;

	[Tooltip("The TMP_Text input text field of the create slot dialog.")]
	public TMP_InputField inputField;

	[Tooltip("The ES3SlotManager this Create Slot Dialog belongs to.")]
	public ES3SlotManager mgr;

	protected virtual void OnEnable()
	{
		base.gameObject.SetActive(mgr.showCreateSlotButton);
		createButton.onClick.AddListener(ShowCreateSlotDialog);
		createDialog.confirmButton.onClick.AddListener(TryCreateNewSlot);
	}

	protected virtual void OnDisable()
	{
		inputField.text = string.Empty;
		createButton.onClick.RemoveAllListeners();
		createDialog.confirmButton.onClick.RemoveAllListeners();
	}

	protected void ShowCreateSlotDialog()
	{
		createDialog.gameObject.SetActive(value: true);
		inputField.Select();
		inputField.ActivateInputField();
	}

	public virtual void TryCreateNewSlot()
	{
		if (string.IsNullOrEmpty(inputField.text))
		{
			mgr.ShowErrorDialog("You must specify a name for your save slot");
			return;
		}
		string slotPath = mgr.GetSlotPath(inputField.text);
		if (ES3.FileExists(slotPath))
		{
			ES3Slot eS3Slot = mgr.slots.Select((GameObject go) => go.GetComponent<ES3Slot>()).FirstOrDefault((ES3Slot slot) => mgr.GetSlotPath(slot.nameLabel.text) == slotPath && slot.markedForDeletion);
			if (eS3Slot == null)
			{
				mgr.ShowErrorDialog("A slot already exists with this name. Please choose a different name.");
				return;
			}
			eS3Slot.DeleteSlot();
		}
		CreateNewSlot(inputField.text);
		inputField.text = "";
		createDialog.gameObject.SetActive(value: false);
	}

	protected virtual void CreateNewSlot(string slotName)
	{
		DateTime now = DateTime.Now;
		mgr.InstantiateSlot(slotName, now).transform.SetSiblingIndex(1);
		if (mgr.autoCreateSaveFile)
		{
			ES3.SaveRaw("{}", mgr.GetSlotPath(slotName));
		}
	}
}
