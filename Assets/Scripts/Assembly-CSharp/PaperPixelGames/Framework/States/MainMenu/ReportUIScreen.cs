using System;
using PaperPixelGames.Core.Reporting;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Framework.States.MainMenu
{
	public class ReportUIScreen : BaseScreen
	{
		public enum FeedbackSentiment : byte
		{
			None = 0,
			VeryBad = 1,
			Bad = 2,
			Neutral = 3,
			Good = 4,
			Excellent = 5
		}

		[Serializable]
		public class ReportSubmitData
		{
			public FeedbackCategory Category;

			public FeedbackSentiment Sentiment;

			public string Details;

			public bool SendLogs;

			public bool SendScreenshot;
		}

		[Header("Category Toggles")]
		[SerializeField]
		private Toggle _generalToggle;

		[SerializeField]
		private Toggle _visualToggle;

		[SerializeField]
		private Toggle _gameplayToggle;

		[SerializeField]
		private Toggle _performanceToggle;

		[SerializeField]
		private Toggle _bugToggle;

		[Header("Rating Buttons")]
		[SerializeField]
		private BaseButton _veryBadButton;

		[SerializeField]
		private BaseButton _badButton;

		[SerializeField]
		private BaseButton _neutralButton;

		[SerializeField]
		private BaseButton _goodButton;

		[SerializeField]
		private BaseButton _excellentButton;

		[Header("Details Input")]
		[SerializeField]
		private TMP_InputField _detailsInputField;

		[SerializeField]
		private int _maxDetailLength = 500;

		[Header("Extra Options")]
		[SerializeField]
		private Button _sendLogsButton;

		[SerializeField]
		private Button _sendScreenshotButton;

		[Header("Bottom Buttons")]
		[SerializeField]
		private BaseButton _submitButton;

		[SerializeField]
		private BaseButton _closeButton;

		private FeedbackSentiment _selectedSentiment;

		private bool _isSendLogsSelected;

		private bool _isSendScreenshotSelected;

		private bool _canSendScreenshot;

		public bool IsGeneralSelected => IsToggleOn(_generalToggle);

		public bool IsVisualSelected => IsToggleOn(_visualToggle);

		public bool IsGameplaySelected => IsToggleOn(_gameplayToggle);

		public bool IsPerformanceSelected => IsToggleOn(_performanceToggle);

		public bool IsBugSelected => IsToggleOn(_bugToggle);

		private ReportManager _reportManager => Singleton<ReportManager>.Instance;

		protected override void OnInitialize()
		{
			BindButtons();
			SetupInputField();
			ResetAllFields();
		}

		protected override void OnShow()
		{
			ResetAllFields();
			ApplyScreenshotOptionState();
			SetSubmitInteractable(interactable: true);
		}

		protected override void OnHide()
		{
			ResetAllFields();
			SetSubmitInteractable(interactable: true);
		}

		public void ResetAllFields()
		{
			ResetCategorySelection();
			ResetSentimentSelection();
			ResetDetails();
			ResetExtras();
		}

		public void ResetDetails()
		{
			if (!(_detailsInputField == null))
			{
				_detailsInputField.SetTextWithoutNotify(string.Empty);
			}
		}

		public void ResetSentimentSelection()
		{
			_selectedSentiment = FeedbackSentiment.None;
		}

		public void SetScreenshotOptionAvailable(bool available)
		{
			_canSendScreenshot = available;
			if (!_canSendScreenshot)
			{
				_isSendScreenshotSelected = false;
			}
			ApplyScreenshotOptionState();
		}

		public string GetDetails()
		{
			if (_detailsInputField == null)
			{
				return string.Empty;
			}
			return _detailsInputField.text?.Trim() ?? string.Empty;
		}

		public bool ValidateBeforeSubmit(out string validationMessage)
		{
			if (!HasAnyCategorySelected())
			{
				validationMessage = "At least one category must be selected.";
				return false;
			}
			if (_selectedSentiment == FeedbackSentiment.None)
			{
				validationMessage = "Please select a rating.";
				return false;
			}
			if (_isSendScreenshotSelected && !IsBugSelected)
			{
				validationMessage = "Screenshot attachment requires the Bug category.";
				return false;
			}
			if (string.IsNullOrWhiteSpace(GetDetails()))
			{
				validationMessage = "Details field is empty.";
				return false;
			}
			validationMessage = string.Empty;
			return true;
		}

		public ReportSubmitData BuildSubmitData()
		{
			return new ReportSubmitData
			{
				Category = GetSelectedCategory(),
				Sentiment = _selectedSentiment,
				Details = GetDetails(),
				SendLogs = _isSendLogsSelected,
				SendScreenshot = _isSendScreenshotSelected
			};
		}

		public void Submit()
		{
			string validationMessage;
			if (_reportManager == null)
			{
				Singleton<NotifyManager>.Instance?.ShowToast("ServiceMissing", "ReportManager is missing.");
				UtilityMethods.Log("<b><color=#ff5555>[ReportUIScreen]</color></b> Submit failed: ReportManager is null.");
			}
			else if (!ValidateBeforeSubmit(out validationMessage))
			{
				Singleton<NotifyManager>.Instance?.ShowToast("ValidationFailed", validationMessage);
				UtilityMethods.Log("<b><color=#ff5555>[ReportUIScreen]</color></b> Submit failed: " + validationMessage);
			}
			else
			{
				ReportSubmitData data = BuildSubmitData();
				SetSubmitInteractable(interactable: false);
				OnSubmit(data);
			}
		}

		public void ClosePanel()
		{
			if (_reportManager != null)
			{
				_reportManager.HideReportScreen();
			}
			else
			{
				Hide();
			}
		}

		private void OnSubmit(ReportSubmitData data)
		{
			if (data.Category == FeedbackCategory.Bug)
			{
				_reportManager.SubmitBug(new BugReportData
				{
					Category = data.Category,
					Message = data.Details,
					Rating = (byte)data.Sentiment,
					IncludeScreenshot = data.SendScreenshot,
					IncludeLogs = false
				}, (ReportSubmitResult result) =>
				{
					SetSubmitInteractable(interactable: true);
					if (result.IsSuccess)
					{
						ResetAllFields();
						Hide();
					}
				});
				return;
			}
			_reportManager.SubmitFeedback(new FeedbackReportData
			{
				Category = data.Category,
				Message = data.Details,
				Rating = (byte)data.Sentiment
			}, (ReportSubmitResult result) =>
			{
				SetSubmitInteractable(interactable: true);
				if (result.IsSuccess)
				{
					ResetAllFields();
					Hide();
				}
			});
		}

		private void BindButtons()
		{
			BindBaseButton(_submitButton, Submit);
			BindBaseButton(_closeButton, ClosePanel);
			BindBaseButton(_veryBadButton, SelectVeryBad);
			BindBaseButton(_badButton, SelectBad);
			BindBaseButton(_neutralButton, SelectNeutral);
			BindBaseButton(_goodButton, SelectGood);
			BindBaseButton(_excellentButton, SelectExcellent);
			if (_sendLogsButton != null)
			{
				_sendLogsButton.onClick.RemoveAllListeners();
				_sendLogsButton.onClick.AddListener(ToggleSendLogs);
			}
			if (_sendScreenshotButton != null)
			{
				_sendScreenshotButton.onClick.RemoveAllListeners();
				_sendScreenshotButton.onClick.AddListener(ToggleSendScreenshot);
			}
		}

		private void BindBaseButton(BaseButton button, Action callback)
		{
			if (!(button == null) && callback != null)
			{
				button.Clicked -= callback;
				button.Clicked += callback;
			}
		}

		private void SetupInputField()
		{
			if (!(_detailsInputField == null))
			{
				_detailsInputField.characterLimit = _maxDetailLength;
			}
		}

		private void ResetCategorySelection()
		{
			SetToggleWithoutNotify(_generalToggle, value: false);
			SetToggleWithoutNotify(_visualToggle, value: false);
			SetToggleWithoutNotify(_gameplayToggle, value: false);
			SetToggleWithoutNotify(_performanceToggle, value: false);
			SetToggleWithoutNotify(_bugToggle, value: false);
		}

		private void ResetExtras()
		{
			_isSendLogsSelected = false;
			_isSendScreenshotSelected = false;
			ApplyScreenshotOptionState();
		}

		private void ToggleSendLogs()
		{
			_isSendLogsSelected = false;
			Singleton<NotifyManager>.Instance?.ShowToast("LogsUnavailable", "Log upload is not available yet.");
			UtilityMethods.Log("<b><color=#FFB020>[ReportUIScreen]</color></b> Log upload requested but no log attachment pipeline is available.", LogType.Warning);
		}

		private void ToggleSendScreenshot()
		{
			if (!_canSendScreenshot)
			{
				_isSendScreenshotSelected = false;
				ApplyScreenshotOptionState();
			}
			else
			{
				_isSendScreenshotSelected = !_isSendScreenshotSelected;
				ApplyScreenshotOptionState();
			}
		}

		private bool HasAnyCategorySelected()
		{
			if (!IsGeneralSelected && !IsVisualSelected && !IsGameplaySelected && !IsPerformanceSelected)
			{
				return IsBugSelected;
			}
			return true;
		}

		private FeedbackCategory GetSelectedCategory()
		{
			if (IsBugSelected)
			{
				return FeedbackCategory.Bug;
			}
			if (IsPerformanceSelected)
			{
				return FeedbackCategory.Performance;
			}
			if (IsGameplaySelected)
			{
				return FeedbackCategory.Gameplay;
			}
			if (IsVisualSelected)
			{
				return FeedbackCategory.Visual;
			}
			return FeedbackCategory.General;
		}

		private void SelectVeryBad()
		{
			SetSentiment(FeedbackSentiment.VeryBad);
			SetSelectedEmote(_veryBadButton);
		}

		private void SelectBad()
		{
			SetSentiment(FeedbackSentiment.Bad);
			SetSelectedEmote(_badButton);
		}

		private void SelectNeutral()
		{
			SetSentiment(FeedbackSentiment.Neutral);
			SetSelectedEmote(_neutralButton);
		}

		private void SelectGood()
		{
			SetSentiment(FeedbackSentiment.Good);
			SetSelectedEmote(_goodButton);
		}

		private void SelectExcellent()
		{
			SetSentiment(FeedbackSentiment.Excellent);
			SetSelectedEmote(_excellentButton);
		}

		private void SetSelectedEmote(BaseButton button, bool active = true)
		{
			if (!(button == null))
			{
				Image component = button.GetComponent<Image>();
				if (!(component == null))
				{
					Color color = component.color;
					color.a = (active ? 1 : 0);
					component.color = color;
				}
			}
		}

		private void SetSentiment(FeedbackSentiment sentiment)
		{
			SetSelectedEmote(_veryBadButton, active: false);
			SetSelectedEmote(_badButton, active: false);
			SetSelectedEmote(_neutralButton, active: false);
			SetSelectedEmote(_goodButton, active: false);
			SetSelectedEmote(_excellentButton, active: false);
			_selectedSentiment = sentiment;
		}

		private static bool IsToggleOn(Toggle toggle)
		{
			if (toggle != null)
			{
				return toggle.isOn;
			}
			return false;
		}

		private static void SetToggleWithoutNotify(Toggle toggle, bool value)
		{
			if (!(toggle == null))
			{
				toggle.SetIsOnWithoutNotify(value);
			}
		}

		private void SetSubmitInteractable(bool interactable)
		{
			if (!(_submitButton == null))
			{
				_submitButton.interactable = interactable && (_reportManager == null || !_reportManager.IsSubmitting);
			}
		}

		private void ApplyScreenshotOptionState()
		{
			if (!(_sendScreenshotButton == null))
			{
				_sendScreenshotButton.interactable = _canSendScreenshot;
				if (!_canSendScreenshot)
				{
					_isSendScreenshotSelected = false;
				}
			}
		}
	}
}
