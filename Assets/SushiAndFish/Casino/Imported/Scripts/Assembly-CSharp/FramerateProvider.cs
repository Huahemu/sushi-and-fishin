namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::UnityEngine;

[CreateAssetMenu(menuName = "Game Settings/Dropdown Provider/Framerate", fileName = "FramerateProvider")]
public class FramerateProvider : ScriptableObject, IDropdownOptionsProvider
{
	[Tooltip("Common framerate options to include. Set to -1 for unlimited.")]
	public List<int> framerateOptions = new List<int> { 30, 60, 120, 144, 240, -1 };

	public List<string> GetOptions()
	{
		List<string> list = new List<string>();
		foreach (int framerateOption in framerateOptions)
		{
			if (framerateOption == -1)
			{
				list.Add("Unlimited");
			}
			else if (framerateOption > 0)
			{
				list.Add(framerateOption.ToString());
			}
		}
		if (list.Count == 0)
		{
			list.AddRange(new string[6] { "30", "60", "120", "144", "240", "Unlimited" });
		}
		return list;
	}

	public int GetDefaultIndex(List<string> options)
	{
		if (options == null || options.Count == 0)
		{
			return 0;
		}
		int targetFrameRate = Application.targetFrameRate;
		if (targetFrameRate == -1)
		{
			for (int i = 0; i < options.Count; i++)
			{
				switch (options[i].Trim().ToLowerInvariant())
				{
				case "unlimited":
				case "uncapped":
				case "off":
					return i;
				}
			}
			return 0;
		}
		for (int j = 0; j < options.Count; j++)
		{
			string text = options[j];
			switch (text.Trim().ToLowerInvariant())
			{
			case "unlimited":
			case "uncapped":
			case "off":
				continue;
			}
			if (int.TryParse(text, out var result) && result == targetFrameRate)
			{
				return j;
			}
		}
		int result2 = 0;
		int num = int.MaxValue;
		for (int k = 0; k < options.Count; k++)
		{
			string text2 = options[k];
			switch (text2.Trim().ToLowerInvariant())
			{
			case "unlimited":
			case "uncapped":
			case "off":
				continue;
			}
			if (int.TryParse(text2, out var result3) && result3 > 0)
			{
				int num2 = Mathf.Abs(result3 - targetFrameRate);
				if (num2 < num)
				{
					num = num2;
					result2 = k;
				}
			}
		}
		return result2;
	}
}

}
