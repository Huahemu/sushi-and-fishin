namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::UnityEngine;

[CreateAssetMenu(menuName = "Game Settings/Dropdown Provider/Refresh Rate", fileName = "RefreshRateProvider")]
public class RefreshRateProvider : ScriptableObject, IDropdownOptionsProvider
{
	[Tooltip("Common refresh rate values to check for. These will be filtered to only show available rates up to the user's maximum.")]
	public List<int> commonRefreshRates = new List<int> { 60, 75, 120, 144, 165, 240, 360 };

	public List<string> GetOptions()
	{
		List<string> list = new List<string>();
		int maxAvailableRefreshRate = GetMaxAvailableRefreshRate();
		List<float> availableRefreshRates = GetAvailableRefreshRates();
		List<int> obj = ((commonRefreshRates != null && commonRefreshRates.Count > 0) ? commonRefreshRates : new List<int> { 60, 75, 120, 144, 165, 240, 360 });
		List<int> list2 = new List<int>();
		foreach (int item in obj)
		{
			if (item > maxAvailableRefreshRate)
			{
				continue;
			}
			bool flag = false;
			foreach (float item2 in availableRefreshRates)
			{
				if (Mathf.Abs(item2 - (float)item) <= 2f)
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				list2.Add(item);
			}
		}
		if (list2.Count == 0)
		{
			foreach (float item3 in availableRefreshRates)
			{
				if (item3 <= (float)maxAvailableRefreshRate)
				{
					list2.Add(Mathf.RoundToInt(item3));
				}
			}
		}
		list2.Sort();
		for (int i = 0; i < list2.Count; i++)
		{
			list.Add($"{list2[i]} Hz");
		}
		if (list.Count == 0)
		{
			float f = (float)Screen.currentResolution.refreshRateRatio.numerator / (float)Screen.currentResolution.refreshRateRatio.denominator;
			list.Add($"{Mathf.RoundToInt(f)} Hz");
		}
		return list;
	}

	private int GetMaxAvailableRefreshRate()
	{
		Resolution[] resolutions = Screen.resolutions;
		if (resolutions == null || resolutions.Length == 0)
		{
			return Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.numerator / (float)Screen.currentResolution.refreshRateRatio.denominator);
		}
		int num = 0;
		for (int i = 0; i < resolutions.Length; i++)
		{
			Resolution resolution = resolutions[i];
			int num2 = Mathf.RoundToInt((float)resolution.refreshRateRatio.numerator / (float)resolution.refreshRateRatio.denominator);
			if (num2 > num)
			{
				num = num2;
			}
		}
		if (num <= 0)
		{
			return 60;
		}
		return num;
	}

	private List<float> GetAvailableRefreshRates()
	{
		List<float> list = new List<float>();
		Resolution[] resolutions = Screen.resolutions;
		if (resolutions == null || resolutions.Length == 0)
		{
			float item = (float)Screen.currentResolution.refreshRateRatio.numerator / (float)Screen.currentResolution.refreshRateRatio.denominator;
			list.Add(item);
			return list;
		}
		HashSet<int> hashSet = new HashSet<int>();
		for (int i = 0; i < resolutions.Length; i++)
		{
			Resolution resolution = resolutions[i];
			float f = (float)resolution.refreshRateRatio.numerator / (float)resolution.refreshRateRatio.denominator;
			hashSet.Add(Mathf.RoundToInt(f));
		}
		foreach (int item2 in hashSet)
		{
			list.Add(item2);
		}
		return list;
	}

	public int GetDefaultIndex(List<string> options)
	{
		if (options == null || options.Count == 0)
		{
			return 0;
		}
		RefreshRate refreshRateRatio = Screen.currentResolution.refreshRateRatio;
		int num = Mathf.RoundToInt((float)refreshRateRatio.numerator / (float)refreshRateRatio.denominator);
		for (int i = 0; i < options.Count; i++)
		{
			if (int.TryParse(options[i].Trim().ToLowerInvariant().Replace("hz", "")
				.Trim(), out var result) && result == num)
			{
				return i;
			}
		}
		return 0;
	}
}

}
