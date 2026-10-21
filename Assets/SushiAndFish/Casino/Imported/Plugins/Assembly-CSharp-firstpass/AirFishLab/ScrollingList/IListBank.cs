namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;

namespace AirFishLab.ScrollingList
{
	public interface IListBank
	{
		IListContent GetListContent(int index);

		int GetContentCount();
	}
}

}
