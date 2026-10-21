namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;
using global::UnityEngine;

namespace AirFishLab.ScrollingList.Demo
{
	[Serializable]
	public class SpriteStringData : IListContent
	{
		[SerializeField]
		private Sprite _sprite;

		[SerializeField]
		private string _title;

		public Sprite sprite => _sprite;

		public string title => _title;
	}
}

}
