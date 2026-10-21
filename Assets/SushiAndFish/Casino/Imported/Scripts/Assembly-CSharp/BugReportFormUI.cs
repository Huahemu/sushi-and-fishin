namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.UI;

public class BugReportFormUI : MonoBehaviour
{
	[Header("Panel")]
	[SerializeField]
	private GameObject formPanel;

	[Header("Required fields")]
	[SerializeField]
	private TMP_InputField titleField;

	[SerializeField]
	private TMP_Dropdown severityDropdown;

	[Header("Optional fields")]
	[SerializeField]
	private TMP_Dropdown categoryDropdown;

	[SerializeField]
	private TMP_InputField whatHappenedField;

	[SerializeField]
	private TMP_InputField expectedField;

	[SerializeField]
	private TMP_InputField reproStepsField;

	[SerializeField]
	private TMP_Dropdown frequencyDropdown;

	[SerializeField]
	private Toggle canReproduceToggle;

	[Header("Actions")]
	[SerializeField]
	private Button submitButton;

	[SerializeField]
	private Button cancelButton;

	[Header("Feedback")]
	[SerializeField]
	private GameObject loadingIndicator;

	[SerializeField]
	private TextMeshProUGUI messageText;

	[Header("API")]
	[SerializeField]
	private BugReportAPIClient apiClient;

	private static readonly string[] Categories = new string[8] { "Other", "Crash", "Multiplayer", "UI", "Performance", "Gameplay", "Visual", "Audio" };

	private static readonly string[] Severities = new string[3] { "Minor", "Major", "Blocker" };

	private static readonly string[] Frequencies = new string[3] { "Once", "Sometimes", "Always" };

	private void Awake()
	{
		if (apiClient == null)
		{
			apiClient = MonoSingleton<BugReportAPIClient>.Instance;
		}
		if (formPanel != null)
		{
			formPanel.SetActive(value: false);
		}
		if (severityDropdown != null)
		{
			severityDropdown.ClearOptions();
			severityDropdown.AddOptions(new List<string>(Severities));
		}
		if (categoryDropdown != null)
		{
			categoryDropdown.ClearOptions();
			categoryDropdown.AddOptions(new List<string>(Categories));
		}
		if (frequencyDropdown != null)
		{
			frequencyDropdown.ClearOptions();
			frequencyDropdown.AddOptions(new List<string>(Frequencies));
		}
		if (submitButton != null)
		{
			submitButton.onClick.AddListener(OnSubmit);
		}
		if (cancelButton != null)
		{
			cancelButton.onClick.AddListener(CloseForm);
		}
	}

	public void OpenForm()
	{
		if (formPanel != null)
		{
			formPanel.SetActive(value: true);
		}
		SetMessage("");
		if (loadingIndicator != null)
		{
			loadingIndicator.SetActive(value: false);
		}
	}

	public void CloseForm()
	{
		if (formPanel != null)
		{
			formPanel.SetActive(value: false);
		}
	}

	private void SetMessage(string msg)
	{
		if (messageText != null)
		{
			messageText.text = msg;
		}
	}

	private void OnSubmit()
	{
		string text = ((titleField != null) ? titleField.text.Trim() : "");
		if (string.IsNullOrEmpty(text))
		{
			SetMessage("Please enter a title.");
			return;
		}
		BugReportPayload bugReportPayload = new BugReportPayload
		{
			title = text,
			severity = ((severityDropdown != null && severityDropdown.options.Count > 0) ? Severities[Mathf.Clamp(severityDropdown.value, 0, Severities.Length - 1)] : "Minor"),
			category = ((categoryDropdown != null && categoryDropdown.options.Count > 0) ? Categories[Mathf.Clamp(categoryDropdown.value, 0, Categories.Length - 1)] : "Other"),
			whatHappened = ((whatHappenedField != null) ? whatHappenedField.text.Trim() : ""),
			expected = ((expectedField != null) ? expectedField.text.Trim() : ""),
			reproSteps = ParseReproSteps((reproStepsField != null) ? reproStepsField.text : ""),
			frequency = ((frequencyDropdown != null && frequencyDropdown.options.Count > 0) ? Frequencies[Mathf.Clamp(frequencyDropdown.value, 0, Frequencies.Length - 1)] : "Once"),
			canReproduceNow = (canReproduceToggle != null && canReproduceToggle.isOn)
		};
		if (apiClient == null)
		{
			SetMessage("Bug report client not configured.");
			return;
		}
		apiClient.FillContext(bugReportPayload);
		if (loadingIndicator != null)
		{
			loadingIndicator.SetActive(value: true);
		}
		if (submitButton != null)
		{
			submitButton.interactable = false;
		}
		SetMessage("Sending…");
		apiClient.SendReport(bugReportPayload, (bool success, string errorMsg, string trelloUrl) =>
		{
			if (loadingIndicator != null)
			{
				loadingIndicator.SetActive(value: false);
			}
			if (submitButton != null)
			{
				submitButton.interactable = true;
			}
			if (success)
			{
				SetMessage(string.IsNullOrEmpty(trelloUrl) ? "Report submitted." : ("Report submitted. Card: " + trelloUrl));
				ClearForm();
			}
			else
			{
				SetMessage("Failed: " + (string.IsNullOrEmpty(errorMsg) ? "Unknown error" : errorMsg));
			}
		});
	}

	private static string[] ParseReproSteps(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		string[] array = text.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length == 0)
		{
			return null;
		}
		return array;
	}

	private void ClearForm()
	{
		if (titleField != null)
		{
			titleField.text = "";
		}
		if (whatHappenedField != null)
		{
			whatHappenedField.text = "";
		}
		if (expectedField != null)
		{
			expectedField.text = "";
		}
		if (reproStepsField != null)
		{
			reproStepsField.text = "";
		}
		if (canReproduceToggle != null)
		{
			canReproduceToggle.isOn = false;
		}
	}
}

}
