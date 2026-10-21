namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;
using global::UnityEngine;
using global::UnityEngine.UI;

namespace AirFishLab.ScrollingList.Demo
{
	public class NameListBox : ListBox
	{
		[SerializeField]
		private Text _contentText;

		public string Content { get; private set; }

		protected override void UpdateDisplayContent(IListContent listContent)
		{
			Content = ((StringListContent)listContent).Value;
			_contentText.text = Content;
		}
	}
}

}
