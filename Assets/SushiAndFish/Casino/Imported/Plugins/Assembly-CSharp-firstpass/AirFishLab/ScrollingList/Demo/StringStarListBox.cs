namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;
using global::UnityEngine;
using global::UnityEngine.UI;

namespace AirFishLab.ScrollingList.Demo
{
	public class StringStarListBox : ListBox
	{
		[SerializeField]
		private Text _text;

		[SerializeField]
		private GameObject[] _stars;

		protected override void UpdateDisplayContent(IListContent content)
		{
			StringStarData stringStarData = (StringStarData)content;
			_text.text = stringStarData.Title;
			for (int i = 0; i < _stars.Length; i++)
			{
				_stars[i].SetActive(i < stringStarData.NumOfStars);
			}
		}
	}
}

}
