using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PaperPixelGames.Framework.Analytics
{
	internal static class AnalyticsEventUtility
	{
		private const int MaxEventSegments = 5;

		private const int MaxSegmentLength = 64;

		private const int MaxCustomFieldKeyLength = 64;

		private const int MaxCustomFieldStringLength = 256;

		public static bool TryBuildEventName(object[] categories, out string eventName)
		{
			eventName = string.Empty;
			if (categories == null || categories.Length == 0)
			{
				return false;
			}
			List<string> list = new List<string>(Math.Min(categories.Length, 5));
			for (int i = 0; i < categories.Length; i++)
			{
				if (list.Count >= 5)
				{
					break;
				}
				if (categories[i] != null)
				{
					string text = SanitizeSegment(Convert.ToString(categories[i], CultureInfo.InvariantCulture));
					if (!string.IsNullOrEmpty(text))
					{
						list.Add(text);
					}
				}
			}
			if (list.Count == 0)
			{
				return false;
			}
			eventName = string.Join(":", list);
			return true;
		}

		public static IDictionary<string, object> SanitizeCustomFields(IDictionary<string, object> customFields)
		{
			if (customFields == null || customFields.Count == 0)
			{
				return null;
			}
			Dictionary<string, object> dictionary = new Dictionary<string, object>();
			foreach (KeyValuePair<string, object> customField in customFields)
			{
				string text = SanitizeSegment(customField.Key);
				if (!string.IsNullOrEmpty(text) && TrySanitizeCustomFieldValue(customField.Value, out var sanitizedValue))
				{
					dictionary[text] = sanitizedValue;
				}
			}
			if (dictionary.Count <= 0)
			{
				return null;
			}
			return dictionary;
		}

		private static bool TrySanitizeCustomFieldValue(object value, out object sanitizedValue)
		{
			sanitizedValue = null;
			if (value == null)
			{
				return false;
			}
			if (!(value is string value2))
			{
				if (!(value is bool flag))
				{
					if (value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong || value is float || value is double || value is decimal)
					{
						sanitizedValue = value;
						return true;
					}
					return false;
				}
				sanitizedValue = (flag ? 1 : 0);
				return true;
			}
			sanitizedValue = TrimToLength(value2, 256);
			return !string.IsNullOrEmpty((string)sanitizedValue);
		}

		private static string SanitizeSegment(string value, int maxLength = 64)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return string.Empty;
			}
			StringBuilder stringBuilder = new StringBuilder(value.Length);
			for (int i = 0; i < value.Length; i++)
			{
				if (stringBuilder.Length >= maxLength)
				{
					break;
				}
				char c = value[i];
				if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '-' || c == '_' || c == '.' || c == '(' || c == ')' || c == '!' || c == '?')
				{
					stringBuilder.Append(c);
				}
				else
				{
					stringBuilder.Append('_');
				}
			}
			return stringBuilder.ToString().Trim();
		}

		private static string TrimToLength(string value, int maxLength)
		{
			if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
			{
				return value;
			}
			return value.Substring(0, maxLength);
		}
	}
}
