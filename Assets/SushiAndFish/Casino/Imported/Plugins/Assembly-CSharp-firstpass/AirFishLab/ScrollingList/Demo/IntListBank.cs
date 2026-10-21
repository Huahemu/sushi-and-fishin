namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;
using global::UnityEngine;

namespace AirFishLab.ScrollingList.Demo
{
	public class IntListBank : BaseListBank
	{
		[SerializeField]
		private int _numOfContents = 10;

		[SerializeReference]
		private readonly List<int> _contents = new List<int>();

		private readonly IntListContent _contentWrapper = new IntListContent();

		private void Awake()
		{
			for (int i = 0; i < _numOfContents; i++)
			{
				_contents.Add(i + 1);
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
