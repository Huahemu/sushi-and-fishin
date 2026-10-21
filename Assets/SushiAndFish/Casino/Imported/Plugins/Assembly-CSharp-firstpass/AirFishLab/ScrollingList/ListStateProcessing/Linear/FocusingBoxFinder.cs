namespace SushiAndFish.Casino.Original
{
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.ContentManagement;
using global::UnityEngine;

namespace AirFishLab.ScrollingList.ListStateProcessing.Linear
{
	public class FocusingBoxFinder
	{
		public struct FocusingBox
		{
			public IListBox Box;

			public float DistanceOffset;

			public void Deconstruct(out IListBox box, out float distanceOffset)
			{
				box = Box;
				distanceOffset = DistanceOffset;
			}
		}

		public struct MiddleResult
		{
			public ListFocusingState ListFocusingState;

			public FocusingBox MiddleFocusing;
		}

		public struct BothEndsResult
		{
			public ListFocusingState ListFocusingState;

			public FocusingBox TopFocusing;

			public FocusingBox BottomFocusing;
		}

		private readonly List<IListBox> _boxes;

		private readonly ListSetting _setting;

		private readonly float _topBaseline;

		private readonly float _middleBaseline;

		private readonly float _bottomBaseline;

		public FocusingBoxFinder(List<IListBox> boxes, ListSetting setting, float topBaseline, float middleBaseline, float bottomBaseline)
		{
			_boxes = boxes;
			_setting = setting;
			_topBaseline = topBaseline;
			_middleBaseline = middleBaseline;
			_bottomBaseline = bottomBaseline;
		}

		public MiddleResult FindForMiddle(int contentCount)
		{
			float num = float.PositiveInfinity;
			IListBox box = null;
			foreach (IListBox box2 in _boxes)
			{
				ContentIDState iDState = ListContentProvider.GetIDState(box2.ContentID, contentCount);
				if (iDState != ContentIDState.Overflow && iDState != ContentIDState.Underflow)
				{
					float num2 = box2.GetPositionFactor() - _middleBaseline;
					if (!(Mathf.Abs(num2) >= Mathf.Abs(num)))
					{
						num = num2;
						box = box2;
					}
				}
			}
			FocusingBox focusingBox = new FocusingBox
			{
				Box = box,
				DistanceOffset = num
			};
			ListFocusingState listFocusingState = FindFocusingStateForMiddle(focusingBox, contentCount);
			return new MiddleResult
			{
				ListFocusingState = listFocusingState,
				MiddleFocusing = focusingBox
			};
		}

		private ListFocusingState FindFocusingStateForMiddle(FocusingBox focusingBox, int contentCount)
		{
			if (_setting.ListType == CircularScrollingList.ListType.Linear)
			{
				return FindFocusingState(focusingBox, contentCount);
			}
			return ListFocusingState.Middle;
		}

		public BothEndsResult FindForBothEnds(int contentCount)
		{
			float num = float.PositiveInfinity;
			IListBox box = null;
			float num2 = float.PositiveInfinity;
			IListBox box2 = null;
			foreach (IListBox box3 in _boxes)
			{
				ContentIDState iDState = ListContentProvider.GetIDState(box3.ContentID, contentCount);
				if (iDState != ContentIDState.Overflow && iDState != ContentIDState.Underflow)
				{
					float positionFactor = box3.GetPositionFactor();
					float num3 = positionFactor - _topBaseline;
					float num4 = positionFactor - _bottomBaseline;
					if (Mathf.Abs(num3) < Mathf.Abs(num))
					{
						num = num3;
						box = box3;
					}
					if (Mathf.Abs(num4) < Mathf.Abs(num2))
					{
						num2 = num4;
						box2 = box3;
					}
				}
			}
			FocusingBox focusingBox = new FocusingBox
			{
				Box = box,
				DistanceOffset = num
			};
			FocusingBox focusingBox2 = new FocusingBox
			{
				Box = box2,
				DistanceOffset = num2
			};
			ListFocusingState listFocusingState = FindFocusingStateForBothEnds(focusingBox, focusingBox2, contentCount);
			return new BothEndsResult
			{
				ListFocusingState = listFocusingState,
				TopFocusing = focusingBox,
				BottomFocusing = focusingBox2
			};
		}

		private ListFocusingState FindFocusingStateForBothEnds(FocusingBox topFocusingBox, FocusingBox bottomFocusingBox, int contentCount)
		{
			if (_setting.ListType == CircularScrollingList.ListType.Circular)
			{
				return ListFocusingState.Middle;
			}
			ListFocusingState listFocusingState = FindFocusingState(topFocusingBox, contentCount);
			ListFocusingState listFocusingState2 = FindFocusingState(bottomFocusingBox, contentCount);
			ListFocusingState listFocusingState3 = ListFocusingState.None;
			if (listFocusingState != ListFocusingState.Middle)
			{
				listFocusingState3 |= listFocusingState;
			}
			if (listFocusingState2 != ListFocusingState.Middle)
			{
				listFocusingState3 |= listFocusingState2;
			}
			if (listFocusingState3 != ListFocusingState.None)
			{
				return listFocusingState3;
			}
			return ListFocusingState.Middle;
		}

		private ListFocusingState FindFocusingState(FocusingBox focusingBox, int contentCount)
		{
			FocusingBox focusingBox2 = focusingBox;
			focusingBox2.Deconstruct(out var box, out var _);
			int contentID = box.ContentID;
			bool reverseContentOrder = _setting.ReverseContentOrder;
			bool flag = contentID == 0;
			bool flag2 = contentID == contentCount - 1;
			if (!(flag | flag2))
			{
				return ListFocusingState.Middle;
			}
			if (flag & flag2)
			{
				return ListFocusingState.TopAndBottom;
			}
			if (!(reverseContentOrder ^ flag))
			{
				return ListFocusingState.Bottom;
			}
			return ListFocusingState.Top;
		}
	}
}

}
