namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;
using global::UnityEngine;
using global::UnityEngine.UI;

namespace AirFishLab.ScrollingList.Demo
{
	public class SpriteStringListBox : ListBox
	{
		[SerializeField]
		private Image _image;

		[SerializeField]
		private Text _title;

		protected override void UpdateDisplayContent(IListContent content)
		{
			SpriteStringData spriteStringData = (SpriteStringData)content;
			_image.sprite = spriteStringData.sprite;
			_title.text = spriteStringData.title;
		}
	}
}

}
