namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;
using global::UnityEngine;
using global::UnityEngine.UI;

namespace AirFishLab.ScrollingList.Demo
{
	public class ImageListBox : ListBox
	{
		[SerializeField]
		private Image _image;

		public Sprite Content { get; private set; }

		protected override void UpdateDisplayContent(IListContent listContent)
		{
			Content = ((SpriteListContent)listContent).Value;
			_image.sprite = Content;
		}
	}
}

}
