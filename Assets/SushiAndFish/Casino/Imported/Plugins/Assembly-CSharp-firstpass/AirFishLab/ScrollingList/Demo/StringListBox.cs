namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;
using global::UnityEngine;
using global::UnityEngine.UI;

namespace AirFishLab.ScrollingList.Demo
{
	public class StringListBox : ListBox
	{
		[SerializeField]
		private Text _text;

		protected override void UpdateDisplayContent(IListContent content)
		{
			AdjustableStringListBank.DataWrapper dataWrapper = (AdjustableStringListBank.DataWrapper)content;
			_text.text = dataWrapper.Data;
		}
	}
}

}
