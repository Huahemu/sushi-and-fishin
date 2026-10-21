namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;
using global::UnityEngine;

namespace AirFishLab.ScrollingList.Demo
{
	public class ColorStrListBank : BaseListBank
	{
		[SerializeField]
		private ColorString[] _contents;

		public override IListContent GetListContent(int index)
		{
			return _contents[index];
		}

		public override int GetContentCount()
		{
			return _contents.Length;
		}
	}
}

}
