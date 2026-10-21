namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.Util;
using global::UnityEngine;
using global::UnityEngine.UI;

namespace AirFishLab.ScrollingList
{
	public class ListBox : MonoBehaviour, IListBox
	{
		private GameObject _gameObject;

		private Transform _transform;

		private Func<Vector2, float> _factorFunc;

		public int ListBoxID { get; private set; }

		public int ContentID { get; private set; }

		public IListBox LastListBox { get; private set; }

		public IListBox NextListBox { get; private set; }

		public ListBoxSelectedEvent OnBoxSelected { get; } = new ListBoxSelectedEvent();

		public CircularScrollingList ScrollingList { get; private set; }

		public bool IsActivated
		{
			get
			{
				return _gameObject.activeSelf;
			}
			set
			{
				_gameObject.SetActive(value);
			}
		}

		public void Initialize(CircularScrollingList scrollingList, int listBoxID, IListBox lastListBox, IListBox nextListBox)
		{
			ScrollingList = scrollingList;
			ListBoxID = listBoxID;
			LastListBox = lastListBox;
			NextListBox = nextListBox;
			_gameObject = base.gameObject;
			_transform = base.transform;
			if (scrollingList.ListSetting.Direction == CircularScrollingList.Direction.Horizontal)
			{
				_factorFunc = FactorUtility.GetVector2X;
			}
			else
			{
				_factorFunc = FactorUtility.GetVector2Y;
			}
			RegisterClickEvent();
			OnInitialized();
		}

		public Transform GetTransform()
		{
			return _transform;
		}

		public float GetPositionFactor()
		{
			return _factorFunc(_transform.localPosition);
		}

		public virtual void OnBoxMoved(float positionRatio)
		{
		}

		public void SetContentID(int contentID)
		{
			ContentID = contentID;
		}

		public void SetContent(IListContent content)
		{
			UpdateDisplayContent(content);
		}

		public void PopToFront()
		{
			base.transform.SetAsLastSibling();
		}

		public void PushToBack()
		{
			base.transform.SetAsFirstSibling();
		}

		private void RegisterClickEvent()
		{
			if (TryGetComponent<Button>(out var component))
			{
				component.onClick.AddListener(OnButtonClick);
			}
		}

		private void OnButtonClick()
		{
			OnBoxSelected?.Invoke(this);
		}

		protected virtual void OnInitialized()
		{
		}

		protected virtual void UpdateDisplayContent(IListContent content)
		{
			Debug.Log(content);
		}
	}
}

}
