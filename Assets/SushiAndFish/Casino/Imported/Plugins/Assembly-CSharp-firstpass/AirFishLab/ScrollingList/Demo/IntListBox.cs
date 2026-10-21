namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;
using global::UnityEngine;
using global::UnityEngine.UI;

namespace AirFishLab.ScrollingList.Demo
{
	public class IntListBox : ListBox
	{
		[SerializeField]
		private Text _contentText;

		public int Content { get; private set; }

		protected override void UpdateDisplayContent(IListContent listContent)
		{
			Content = ((IntListContent)listContent).Value;
			_contentText.text = Content.ToString();
		}
	}
}

}
