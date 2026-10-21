namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;
using global::UnityEngine;

namespace AirFishLab.ScrollingList.Demo
{
	public class SuffixListBank : BaseListBank
	{
		[Serializable]
		private class JsonStringArray
		{
			public List<string> array;
		}

		private List<string> _contents = new List<string>();

		private readonly StringListContent _contentWrapper = new StringListContent();

		private void Awake()
		{
			LoadSuffixNames();
		}

		private void LoadSuffixNames()
		{
			TextAsset textAsset = SushiAndFish.Casino.Original.CasinoResources.Load<TextAsset>("suffix");
			if (textAsset != null)
			{
				_contents.AddRange(JsonUtility.FromJson<JsonStringArray>(textAsset.text).array);
			}
			else
			{
				Debug.LogWarning("JSON file 'Suffix' not found in Resources folder.");
			}
		}

		public override IListContent GetListContent(int index)
		{
			_contentWrapper.Value = _contents[index];
			return _contentWrapper;
		}

		public override int GetContentCount()
		{
			return _contents.Count;
		}
	}
}

}
