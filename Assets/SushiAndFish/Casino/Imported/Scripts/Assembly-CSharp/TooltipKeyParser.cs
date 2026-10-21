namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using global::System.Text.RegularExpressions;
using global::UnityEngine;

public static class TooltipKeyParser
{
	private static readonly string KeyBracketPattern = "\\[([^\\]]+)\\]";

	private const string InteractFallbackKey = "E";

	public static List<TooltipElement> ParseTooltip(string tooltipText)
	{
		List<TooltipElement> list = new List<TooltipElement>();
		if (string.IsNullOrEmpty(tooltipText))
		{
			Debug.Log("TooltipKeyParser: Tooltip text is null or empty");
			return list;
		}
		int num = 0;
		foreach (Match item in Regex.Matches(tooltipText, KeyBracketPattern, RegexOptions.IgnoreCase))
		{
			if (item.Index > num)
			{
				string text = tooltipText.Substring(num, item.Index - num);
				if (!string.IsNullOrEmpty(text))
				{
					list.Add(new TooltipElement
					{
						Type = TooltipElementType.Text,
						Content = text
					});
				}
			}
			string value = item.Groups[1].Value;
			value = ResolveDynamicKey(value);
			list.Add(new TooltipElement
			{
				Type = TooltipElementType.Key,
				Content = value
			});
			num = item.Index + item.Length;
		}
		if (num < tooltipText.Length)
		{
			string text2 = tooltipText.Substring(num);
			if (!string.IsNullOrEmpty(text2))
			{
				list.Add(new TooltipElement
				{
					Type = TooltipElementType.Text,
					Content = text2
				});
			}
		}
		if (list.Count == 0)
		{
			list.Add(new TooltipElement
			{
				Type = TooltipElementType.Text,
				Content = tooltipText
			});
		}
		return list;
	}

	public static bool HasKeys(string tooltipText)
	{
		if (string.IsNullOrEmpty(tooltipText))
		{
			return false;
		}
		return Regex.IsMatch(tooltipText, KeyBracketPattern, RegexOptions.IgnoreCase);
	}

	private static string ResolveDynamicKey(string keyText)
	{
		if (string.IsNullOrWhiteSpace(keyText))
		{
			return keyText;
		}
		if (!string.Equals(keyText.Trim(), "E", StringComparison.OrdinalIgnoreCase))
		{
			return keyText;
		}
		if (InputReader.Instance == null)
		{
			return "E";
		}
		string bindingDisplayName = InputReader.Instance.GetBindingDisplayName("Interact", 0);
		if (!string.IsNullOrWhiteSpace(bindingDisplayName))
		{
			return bindingDisplayName;
		}
		return "E";
	}
}

}
