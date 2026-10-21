namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;
using global::UnityEngine;
using global::UnityEngine.UI;

namespace AirFishLab.ScrollingList.Demo
{
	public class ColorStrListBox : ListBox
	{
		[SerializeField]
		private Image _contentImage;

		[SerializeField]
		private Text _contentText;

		protected override void UpdateDisplayContent(IListContent content)
		{
			ColorString colorString = (ColorString)content;
			_contentImage.color = colorString.color;
			_contentText.text = colorString.name;
		}
	}
}

}
