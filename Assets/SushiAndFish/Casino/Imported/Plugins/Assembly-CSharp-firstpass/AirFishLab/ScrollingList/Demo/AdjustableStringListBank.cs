namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;
using global::UnityEngine;
using global::UnityEngine.UI;

namespace AirFishLab.ScrollingList.Demo
{
	public class AdjustableStringListBank : BaseListBank
	{
		public class DataWrapper : IListContent
		{
			public string Data;
		}

		[SerializeField]
		private InputField _contentInputField;

		[SerializeField]
		private string[] _contents = new string[5] { "a", "b", "c", "d", "e" };

		[SerializeField]
		private CircularScrollingList _circularList;

		[SerializeField]
		private CircularScrollingList _linearList;

		private readonly DataWrapper _dataWrapper = new DataWrapper();

		public void ChangeContents()
		{
			_contents = _contentInputField.text.Split(new char[2] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			_circularList.Refresh();
			_linearList.Refresh();
		}

		public override IListContent GetListContent(int index)
		{
			_dataWrapper.Data = _contents[index];
			return _dataWrapper;
		}

		public override int GetContentCount()
		{
			return _contents.Length;
		}
	}
}

}
