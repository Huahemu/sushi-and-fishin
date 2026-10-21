using System;
using UnityEngine;

namespace PaperPixelGames.Core.Reporting
{
	public class ReportValidationService : MonoBehaviour
	{
		[Header("Feedback Limits")]
		[SerializeField]
		private int _minFeedbackMessageLength = 3;

		[SerializeField]
		private int _maxFeedbackMessageLength = 500;

		[Header("Bug Limits")]
		[SerializeField]
		private int _minBugDescriptionLength = 5;

		[SerializeField]
		private int _maxBugDescriptionLength = 500;

		public bool ValidateFeedback(FeedbackReportData data)
		{
			string message;
			return ValidateFeedback(data, out message);
		}

		public bool ValidateFeedback(FeedbackReportData data, out string message)
		{
			if (data == null)
			{
				message = "Feedback data is null.";
				Debug.LogError("[ReportValidationService] Feedback data is null.");
				return false;
			}
			return ValidateSubmission(new ReportSubmissionData
			{
				Type = ReportSubmissionType.Feedback,
				Category = data.Category,
				Message = data.Message,
				Rating = data.Rating
			}, out message);
		}

		public bool ValidateBug(BugReportData data)
		{
			string message;
			return ValidateBug(data, out message);
		}

		public bool ValidateBug(BugReportData data, out string message)
		{
			if (data == null)
			{
				message = "Bug data is null.";
				Debug.LogError("[ReportValidationService] Bug data is null.");
				return false;
			}
			return ValidateSubmission(new ReportSubmissionData
			{
				Type = ReportSubmissionType.Bug,
				Category = data.Category,
				Message = data.Message,
				Rating = data.Rating,
				IncludeScreenshot = data.IncludeScreenshot
			}, out message);
		}

		public bool ValidateSubmission(ReportSubmissionData data, out string message)
		{
			if (data == null)
			{
				message = "Report data is null.";
				Debug.LogError("[ReportValidationService] Report data is null.");
				return false;
			}
			if (!Enum.IsDefined(typeof(ReportSubmissionType), data.Type))
			{
				message = "Report type is invalid.";
				Debug.LogWarning("[ReportValidationService] Report type is invalid.");
				return false;
			}
			if (!Enum.IsDefined(typeof(FeedbackCategory), data.Category))
			{
				message = "Report category is invalid.";
				Debug.LogWarning("[ReportValidationService] Report category is invalid.");
				return false;
			}
			if (data.Rating < 1 || data.Rating > 5)
			{
				message = "Report rating is invalid.";
				Debug.LogWarning("[ReportValidationService] Report rating is invalid.");
				return false;
			}
			if (string.IsNullOrWhiteSpace(data.Message))
			{
				message = ((data.Type == ReportSubmissionType.Bug) ? "Bug description is empty." : "Feedback message is empty.");
				Debug.LogWarning("[ReportValidationService] Report message is empty.");
				return false;
			}
			string text = data.Message.Trim();
			int num = ((data.Type == ReportSubmissionType.Bug) ? _minBugDescriptionLength : _minFeedbackMessageLength);
			int num2 = ((data.Type == ReportSubmissionType.Bug) ? _maxBugDescriptionLength : _maxFeedbackMessageLength);
			if (text.Length < num)
			{
				message = $"Report message is too short. Minimum {num} characters required.";
				Debug.LogWarning("[ReportValidationService] Report message is too short.");
				return false;
			}
			if (text.Length > num2)
			{
				message = $"Report message is too long. Maximum {num2} characters allowed.";
				Debug.LogWarning("[ReportValidationService] Report message is too long.");
				return false;
			}
			message = string.Empty;
			return true;
		}
	}
}
