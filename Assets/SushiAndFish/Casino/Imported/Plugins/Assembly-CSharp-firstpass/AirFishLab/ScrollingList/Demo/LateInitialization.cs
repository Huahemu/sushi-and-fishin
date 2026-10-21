namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

namespace AirFishLab.ScrollingList.Demo
{
	public class LateInitialization : MonoBehaviour
	{
		[SerializeField]
		private CircularScrollingList _list;

		[SerializeField]
		private BaseListBank _listBankSource;

		[SerializeField]
		private ListBox _listBoxSource;

		[SerializeField]
		private int _numOfBoxes;

		public void InitializeTheList()
		{
			_list.SetListBank(_listBankSource);
			ListBoxSetting boxSetting = _list.BoxSetting;
			boxSetting.SetBoxPrefab(_listBoxSource);
			boxSetting.SetNumOfBoxes(_numOfBoxes);
			ListSetting listSetting = _list.ListSetting;
			listSetting.SetListType(CircularScrollingList.ListType.Linear);
			listSetting.SetAlignAtFocusingPosition(toAlign: true);
			listSetting.SetFocusSelectedBox(toFocus: true);
			listSetting.AddOnBoxSelectedCallback(OnBoxSelected);
			_list.Initialize();
		}

		private void OnBoxSelected(ListBox box)
		{
			IntListBox intListBox = (IntListBox)box;
			Debug.Log($"The selected content: {intListBox.Content}");
		}
	}
}

}
