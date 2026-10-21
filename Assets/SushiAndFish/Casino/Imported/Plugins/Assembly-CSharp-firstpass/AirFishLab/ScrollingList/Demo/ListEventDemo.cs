namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;
using global::UnityEngine.UI;

namespace AirFishLab.ScrollingList.Demo
{
	public class ListEventDemo : MonoBehaviour
	{
		[SerializeField]
		private CircularScrollingList _list;

		[SerializeField]
		private Text _selectedContentText;

		[SerializeField]
		private Text _requestedContentText;

		[SerializeField]
		private Text _autoUpdatedContentText;

		public void DisplayFocusingContent()
		{
			int focusingContentID = _list.GetFocusingContentID();
			IntListContent intListContent = (IntListContent)_list.ListBank.GetListContent(focusingContentID);
			_requestedContentText.text = $"Focusing content: {intListContent.Value}";
		}

		public void OnBoxSelected(ListBox listBox)
		{
			IntListContent intListContent = (IntListContent)_list.ListBank.GetListContent(listBox.ContentID);
			_selectedContentText.text = $"Selected content ID: {listBox.ContentID}, Content: {intListContent.Value}";
		}

		public void OnFocusingBoxChanged(ListBox prevFocusingBox, ListBox curFocusingBox)
		{
			_autoUpdatedContentText.text = "(Auto updated)\nFocusing content: " + $"{((IntListBox)curFocusingBox).Content}";
		}

		public void OnMovementEnd()
		{
			Debug.Log("Movement Ends");
		}
	}
}

}
