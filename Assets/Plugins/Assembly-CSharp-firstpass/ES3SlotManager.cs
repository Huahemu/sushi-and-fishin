using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class ES3SlotManager : MonoBehaviour
{
	[Tooltip("Shows a confirmation if this slot already exists when we select it.")]
	public bool showConfirmationIfExists = true;

	[Tooltip("Whether the Create new slot button should be visible.")]
	public bool showCreateSlotButton = true;

	[Tooltip("Whether we should automatically create an empty save file when the user creates a new save slot. This will be created using the default settings, so you should set this to false if you are using ES3Settings objects.")]
	public bool autoCreateSaveFile;

	[Space(16f)]
	[Tooltip("The name of a scene to load after the user chooses a slot.")]
	public string loadSceneAfterSelectSlot;

	[Space(16f)]
	[Tooltip("An event called after a slot is selected, but before the scene specified by loadSceneAfterSelectSlot is loaded.")]
	public UnityEvent onAfterSelectSlot;

	[Space(16f)]
	[Tooltip("The subfolder we want to store our save files in. If this is a relative path, it will be relative to Application.persistentDataPath.")]
	public string slotDirectory = "slots/";

	[Tooltip("The extension we want to use for our save files.")]
	public string slotExtension = ".es3";

	[Space(16f)]
	[Tooltip("The template we'll instantiate to create our slots.")]
	public GameObject slotTemplate;

	[Tooltip("The dialog box for creating a new slot.")]
	public GameObject createDialog;

	[Tooltip("The dialog box for displaying an error to the user.")]
	public GameObject errorDialog;

	public static string selectedSlotPath = null;

	public List<GameObject> slots = new List<GameObject>();

	private static DateTime falseDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

	protected virtual void OnEnable()
	{
		slotTemplate.SetActive(value: false);
		DestroySlots();
		InstantiateSlots();
	}

	protected virtual void InstantiateSlots()
	{
		List<(string, DateTime)> list = new List<(string, DateTime)>();
		if (!ES3.DirectoryExists(slotDirectory))
		{
			return;
		}
		string[] files = ES3.GetFiles(slotDirectory);
		for (int i = 0; i < files.Length; i++)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(files[i]);
			DateTime timestamp = ES3.GetTimestamp(GetSlotPath(fileNameWithoutExtension));
			list.Add((fileNameWithoutExtension, timestamp));
		}
		list = list.OrderByDescending(((string Name, DateTime Timestamp) x) => x.Timestamp).ToList();
		foreach (var item in list)
		{
			InstantiateSlot(item.Item1, item.Item2);
		}
	}

	public virtual GameObject InstantiateSlot(string slotName, DateTime timestamp)
	{
		GameObject gameObject = UnityEngine.Object.Instantiate(slotTemplate, slotTemplate.transform.parent);
		slots.Add(gameObject);
		gameObject.SetActive(value: true);
		ES3Slot component = gameObject.GetComponent<ES3Slot>();
		component.nameLabel.text = slotName.Replace('_', ' ');
		if (timestamp == falseDateTime)
		{
			component.timestampLabel.text = "";
		}
		else
		{
			component.timestampLabel.text = timestamp.ToString("yyyy-MM-dd") + "\n" + timestamp.ToString("HH:mm:ss");
		}
		return gameObject;
	}

	public virtual void ShowErrorDialog(string errorMessage)
	{
		errorDialog.transform.Find("Dialog Box/Message").GetComponent<TMP_Text>().text = errorMessage;
		errorDialog.SetActive(value: true);
	}

	protected virtual void DestroySlots()
	{
		foreach (GameObject slot in slots)
		{
			UnityEngine.Object.Destroy(slot);
		}
	}

	public virtual string GetSlotPath(string slotName)
	{
		return slotDirectory + Regex.Replace(slotName, "\\s+", "_") + slotExtension;
	}
}
