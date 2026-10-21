namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;
using global::UnityEngine;

namespace AirFishLab.ScrollingList.ListStateProcessing.Linear
{
	public class ListBoxController : IListBoxController
	{
		private ListSetting _setting;

		private readonly List<IListBox> _boxes = new List<IListBox>();

		private readonly List<IListBox> _boxesToBeUpdated = new List<IListBox>();

		private BoxTransformController _transformController;

		private FocusingBoxFinder _focusingBoxFinder;

		private ListContentProvider _contentProvider;

		private Action _updateFocusingBoxFunc;

		private Action<int> _recalculateAllBoxContentFunc;

		private IListBox _focusingBox;

		public ListFocusingState ListFocusingState { get; private set; } = ListFocusingState.Middle;

		public float FocusingDistanceOffset { get; private set; }

		public void Initialize(ListSetupData setupData)
		{
			_setting = setupData.ListSetting;
			_boxes.Clear();
			_boxes.AddRange(setupData.ListBoxes);
			_transformController = new BoxTransformController(setupData);
			_contentProvider = setupData.ListContentProvider;
			_focusingBoxFinder = new FocusingBoxFinder(_boxes, _setting, _transformController.TopBaseline, _transformController.MiddleBaseline, _transformController.BottomBaseline);
			switch (_setting.FocusingPosition)
			{
			case CircularScrollingList.FocusingPosition.Top:
				_updateFocusingBoxFunc = UpdateTopFocusingBox;
				_recalculateAllBoxContentFunc = RecalculateAllBoxContentForBothEnds;
				break;
			case CircularScrollingList.FocusingPosition.Center:
				_updateFocusingBoxFunc = UpdateCenterFocusingBox;
				_recalculateAllBoxContentFunc = RecalculateAllBoxContentFofMiddle;
				break;
			case CircularScrollingList.FocusingPosition.Bottom:
				_updateFocusingBoxFunc = UpdateBottomFocusingBox;
				_recalculateAllBoxContentFunc = RecalculateAllBoxContentForBothEnds;
				break;
			}
			InitializeBoxes(setupData);
		}

		private void InitializeBoxes(ListSetupData setupData)
		{
			int count = _boxes.Count;
			for (int i = 0; i < count; i++)
			{
				IListBox listBox = _boxes[i];
				IListBox lastListBox = _boxes[(int)Mathf.Repeat(i - 1, count)];
				IListBox nextListBox = _boxes[(int)Mathf.Repeat(i + 1, count)];
				listBox.Initialize(setupData.ScrollingList, i, lastListBox, nextListBox);
				listBox.OnBoxSelected.AddListener(_setting.OnBoxSelected.Invoke);
				_transformController.SetInitialLocalTransform(listBox, i);
				int initialContentID = _contentProvider.GetInitialContentID(i);
				UpdateBoxContent(listBox, initialContentID);
			}
			_updateFocusingBoxFunc();
			InitializeBoxLayerSorting();
		}

		private void InitializeBoxLayerSorting()
		{
			int i = 0;
			int count;
			for (count = _boxes.Count; i < count && _boxes[i] != _focusingBox; i++)
			{
			}
			for (int num = i - 1; num >= 0; num--)
			{
				_boxes[num].PushToBack();
			}
			for (int j = i + 1; j < count; j++)
			{
				_boxes[j].PushToBack();
			}
		}

		public void UpdateBoxes(float movementValue)
		{
			if (Mathf.Approximately(movementValue, 0f))
			{
				return;
			}
			BoxPositionState positionState = BoxPositionState.Nothing;
			foreach (IListBox box in _boxes)
			{
				BoxPositionState boxPositionState = _transformController.UpdateLocalTransform(box, movementValue);
				if (boxPositionState != BoxPositionState.Nothing)
				{
					_boxesToBeUpdated.Add(box);
					positionState = boxPositionState;
				}
			}
			UpdateBoxesInOrder(_boxesToBeUpdated, positionState);
			_boxesToBeUpdated.Clear();
			_updateFocusingBoxFunc();
		}

		public void RefreshBoxes(int focusingContentID = -1)
		{
			int contentID = _focusingBox.ContentID;
			int contentCount = _contentProvider.GetContentCount();
			if (focusingContentID >= contentCount)
			{
				throw new IndexOutOfRangeException("focusingContentID is larger than the number of contents");
			}
			if (focusingContentID < 0)
			{
				focusingContentID = ((contentID != int.MinValue) ? Mathf.Min(contentID, contentCount - 1) : 0);
			}
			_recalculateAllBoxContentFunc(focusingContentID);
		}

		public IListBox GetFocusingBox()
		{
			return _focusingBox;
		}

		private void UpdateBoxesInOrder(List<IListBox> boxes, BoxPositionState positionState)
		{
			switch (positionState)
			{
			case BoxPositionState.JumpToTop:
				boxes.Sort((IListBox a, IListBox b) => a.GetPositionFactor().CompareTo(b.GetPositionFactor()));
				{
					foreach (IListBox box in boxes)
					{
						int contentIDByNextBox = _contentProvider.GetContentIDByNextBox(box.NextListBox.ContentID);
						box.PushToBack();
						UpdateBoxContent(box, contentIDByNextBox);
					}
					break;
				}
			case BoxPositionState.JumpToBottom:
				boxes.Sort((IListBox a, IListBox b) => b.GetPositionFactor().CompareTo(a.GetPositionFactor()));
				{
					foreach (IListBox box2 in boxes)
					{
						int contentIDByLastBox = _contentProvider.GetContentIDByLastBox(box2.LastListBox.ContentID);
						box2.PushToBack();
						UpdateBoxContent(box2, contentIDByLastBox);
					}
					break;
				}
			}
		}

		private void UpdateTopFocusingBox()
		{
			FocusingBoxFinder.BothEndsResult bothEndsResult = _focusingBoxFinder.FindForBothEnds(_contentProvider.GetContentCount());
			FocusingBoxFinder.FocusingBox topFocusing = bothEndsResult.TopFocusing;
			ContentIDState iDState = _contentProvider.GetIDState(topFocusing.Box.ContentID);
			FocusingBoxFinder.FocusingBox bottomFocusing = bothEndsResult.BottomFocusing;
			ContentIDState iDState2 = _contentProvider.GetIDState(bottomFocusing.Box.ContentID);
			float distanceOffset = ((iDState2.HasFlag(ContentIDState.Last) && !iDState.HasFlag(ContentIDState.First)) ? bottomFocusing.DistanceOffset : topFocusing.DistanceOffset);
			UpdateFocusingBox(topFocusing.Box, distanceOffset, bothEndsResult.ListFocusingState);
		}

		private void UpdateCenterFocusingBox()
		{
			FocusingBoxFinder.MiddleResult middleResult = _focusingBoxFinder.FindForMiddle(_contentProvider.GetContentCount());
			var (focusingBox2, distanceOffset) = middleResult.MiddleFocusing;
			UpdateFocusingBox(focusingBox2, distanceOffset, middleResult.ListFocusingState);
		}

		private void UpdateBottomFocusingBox()
		{
			FocusingBoxFinder.BothEndsResult bothEndsResult = _focusingBoxFinder.FindForBothEnds(_contentProvider.GetContentCount());
			FocusingBoxFinder.FocusingBox topFocusing = bothEndsResult.TopFocusing;
			ContentIDState iDState = _contentProvider.GetIDState(topFocusing.Box.ContentID);
			FocusingBoxFinder.FocusingBox bottomFocusing = bothEndsResult.BottomFocusing;
			ContentIDState iDState2 = _contentProvider.GetIDState(bottomFocusing.Box.ContentID);
			float distanceOffset = ((iDState.HasFlag(ContentIDState.Last) && !iDState2.HasFlag(ContentIDState.First)) ? topFocusing.DistanceOffset : bottomFocusing.DistanceOffset);
			UpdateFocusingBox(bottomFocusing.Box, distanceOffset, bothEndsResult.ListFocusingState);
		}

		private void UpdateFocusingBox(IListBox focusingBox, float distanceOffset, ListFocusingState listFocusingState)
		{
			ListFocusingState = listFocusingState;
			FocusingDistanceOffset = distanceOffset;
			if (focusingBox != _focusingBox)
			{
				focusingBox.PopToFront();
				_setting.OnFocusingBoxChanged.Invoke((ListBox)_focusingBox, (ListBox)focusingBox);
				_focusingBox = focusingBox;
			}
		}

		private void RecalculateAllBoxContentFofMiddle(int newFocusingContentID)
		{
			int count = _boxes.Count;
			int listBoxID = _focusingBox.ListBoxID;
			int num = ((!_setting.ReverseContentOrder) ? 1 : (-1));
			foreach (IListBox box in _boxes)
			{
				float positionFactor = box.GetPositionFactor();
				int num2 = box.ListBoxID;
				if (num2 > listBoxID && positionFactor > 0f)
				{
					num2 -= count;
				}
				else if (num2 < listBoxID && positionFactor < 0f)
				{
					num2 += count;
				}
				int origContentID = newFocusingContentID + (num2 - listBoxID) * num;
				int refreshedContentID = _contentProvider.GetRefreshedContentID(origContentID);
				UpdateBoxContent(box, refreshedContentID);
			}
			_updateFocusingBoxFunc();
		}

		private void RecalculateAllBoxContentForBothEnds(int newFocusingContentID)
		{
			List<IListBox> list = new List<IListBox>(_boxes);
			if (!_setting.ReverseContentOrder)
			{
				list.Sort((IListBox a, IListBox b) => b.GetPositionFactor().CompareTo(a.GetPositionFactor()));
			}
			else
			{
				list.Sort((IListBox a, IListBox b) => a.GetPositionFactor().CompareTo(b.GetPositionFactor()));
			}
			int count = list.Count;
			int contentCount = _contentProvider.GetContentCount();
			if (_setting.ListType != CircularScrollingList.ListType.Circular)
			{
				if (contentCount <= count)
				{
					newFocusingContentID = 0;
				}
				else
				{
					int num = contentCount - newFocusingContentID - count;
					if (num < 0)
					{
						newFocusingContentID += num;
					}
				}
			}
			foreach (IListBox item in list)
			{
				int refreshedContentID = _contentProvider.GetRefreshedContentID(newFocusingContentID);
				UpdateBoxContent(item, refreshedContentID);
				newFocusingContentID++;
			}
			_updateFocusingBoxFunc();
		}

		private void UpdateBoxContent(IListBox box, int contentID)
		{
			ContentIDState iDState = _contentProvider.GetIDState(contentID);
			ToggleBoxActivation(box, iDState);
			box.SetContentID(contentID);
			if (_contentProvider.TryGetContent(contentID, out var content))
			{
				box.SetContent(content);
			}
		}

		private void ToggleBoxActivation(IListBox box, ContentIDState idState)
		{
			if (idState == ContentIDState.NoContent)
			{
				box.IsActivated = false;
				return;
			}
			bool isActivated = box.IsActivated;
			bool flag = idState.HasFlag(ContentIDState.Valid);
			if (!flag & isActivated)
			{
				box.IsActivated = false;
			}
			else if (flag && !isActivated)
			{
				box.IsActivated = true;
			}
		}
	}
}

}
