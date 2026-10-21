namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;
using global::UnityEngine;

namespace AirFishLab.ScrollingList
{
	public interface IListBox
	{
		int ListBoxID { get; }

		int ContentID { get; }

		IListBox LastListBox { get; }

		IListBox NextListBox { get; }

		ListBoxSelectedEvent OnBoxSelected { get; }

		bool IsActivated { get; set; }

		CircularScrollingList ScrollingList { get; }

		void Initialize(CircularScrollingList scrollingList, int listBoxID, IListBox lastListBox, IListBox nextListBox);

		Transform GetTransform();

		float GetPositionFactor();

		void OnBoxMoved(float positionRatio);

		void SetContentID(int contentID);

		void SetContent(IListContent content);

		void PopToFront();

		void PushToBack();
	}
}

}
