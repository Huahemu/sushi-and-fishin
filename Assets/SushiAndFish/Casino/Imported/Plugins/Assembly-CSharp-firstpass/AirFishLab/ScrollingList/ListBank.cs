namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;

namespace AirFishLab.ScrollingList
{
	public class ListBank : BaseListBank
	{
		public class Content : IListContent
		{
			public int Value;
		}

		private int[] contents = new int[10] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

		private readonly Content _contentWrapper = new Content();

		public override IListContent GetListContent(int index)
		{
			_contentWrapper.Value = contents[index];
			return _contentWrapper;
		}

		public override int GetContentCount()
		{
			return contents.Length;
		}
	}
}

}
