namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;
using global::UnityEngine;

namespace AirFishLab.ScrollingList
{
	public class ListSetupData
	{
		public readonly CircularScrollingList ScrollingList;

		public readonly ListSetting ListSetting;

		public readonly RectTransform RectTransform;

		public readonly Camera CanvasRefCamera;

		public readonly List<IListBox> ListBoxes;

		public readonly ListContentProvider ListContentProvider;

		public ListSetupData(CircularScrollingList scrollingList, ListSetting listSetting, RectTransform rectTransform, Camera canvasRefCamera, List<IListBox> listBoxes, ListContentProvider listContentProvider)
		{
			ScrollingList = scrollingList;
			ListSetting = listSetting;
			RectTransform = rectTransform;
			CanvasRefCamera = canvasRefCamera;
			ListBoxes = listBoxes;
			ListContentProvider = listContentProvider;
		}
	}
}

}
