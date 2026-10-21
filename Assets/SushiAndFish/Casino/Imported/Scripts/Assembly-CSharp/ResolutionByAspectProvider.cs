namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using global::UnityEngine;

[CreateAssetMenu(menuName = "Game Settings/Dropdown Provider/Resolutions By Aspect", fileName = "ResolutionByAspectProvider")]
public class ResolutionByAspectProvider : ScriptableObject, IDropdownOptionsProvider
{
	[Range(0f, 0.1f)]
	public float aspectTolerance = 0.01f;

	public List<string> GetOptions()
	{
		List<string> list = new List<string>();
		Resolution[] resolutions = Screen.resolutions;
		if (resolutions == null || resolutions.Length == 0)
		{
			list.Add($"{Screen.currentResolution.width}x{Screen.currentResolution.height}");
			return list;
		}
		float num = ((Screen.width > 0 && Screen.height > 0) ? ((float)Screen.width / (float)Screen.height) : ((float)Screen.currentResolution.width / (float)Screen.currentResolution.height));
		HashSet<string> hashSet = new HashSet<string>();
		List<Resolution> list2 = new List<Resolution>();
		for (int i = 0; i < resolutions.Length; i++)
		{
			Resolution item = resolutions[i];
			if (!(Mathf.Abs((float)item.width / (float)item.height - num) > aspectTolerance))
			{
				string item2 = $"{item.width}x{item.height}";
				if (hashSet.Add(item2))
				{
					list2.Add(item);
				}
			}
		}
		if (list2.Count == 0)
		{
			list.Add($"{Screen.currentResolution.width}x{Screen.currentResolution.height}");
			return list;
		}
		list2.Sort((Resolution a, Resolution b) =>
		{
			int value = a.width * a.height;
			return (b.width * b.height).CompareTo(value);
		});
		for (int num2 = 0; num2 < list2.Count; num2++)
		{
			list.Add($"{list2[num2].width}x{list2[num2].height}");
		}
		return list;
	}

	public int GetDefaultIndex(List<string> options)
	{
		if (options == null || options.Count == 0)
		{
			return 0;
		}
		string text = $"{Screen.currentResolution.width}x{Screen.currentResolution.height}";
		for (int i = 0; i < options.Count; i++)
		{
			if (options[i] == text)
			{
				return i;
			}
		}
		return 0;
	}
}

}
