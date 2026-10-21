namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;
using global::UnityEngine;

namespace AirFishLab.ScrollingList
{
	public abstract class BaseListBank : MonoBehaviour, IListBank
	{
		public abstract IListContent GetListContent(int index);

		public abstract int GetContentCount();
	}
}

}
