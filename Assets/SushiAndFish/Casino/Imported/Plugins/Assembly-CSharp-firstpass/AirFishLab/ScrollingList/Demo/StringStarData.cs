namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;
using global::UnityEngine;

namespace AirFishLab.ScrollingList.Demo
{
	[Serializable]
	public class StringStarData : IListContent
	{
		[SerializeField]
		private string _title;

		[SerializeField]
		private int _numOfStars;

		public string Title => _title;

		public int NumOfStars => _numOfStars;
	}
}

}
