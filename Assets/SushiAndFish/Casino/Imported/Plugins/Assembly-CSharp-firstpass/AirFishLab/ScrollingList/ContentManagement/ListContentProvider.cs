namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

namespace AirFishLab.ScrollingList.ContentManagement
{
	public class ListContentProvider
	{
		public const int NO_CONTENT_ID = int.MinValue;

		private readonly ListSetting _listSetting;

		private readonly IListBank _listBank;

		private readonly int _numOfBoxes;

		private readonly int _idFactor;

		private readonly Func<int, int> _idCalculationFunc;

		public ListContentProvider(ListSetting listSetting, IListBank listBank, int numOfBoxes)
		{
			_listSetting = listSetting;
			_listBank = listBank;
			_numOfBoxes = numOfBoxes;
			_idFactor = ((!_listSetting.ReverseContentOrder) ? 1 : (-1));
			if (_listSetting.ListType == CircularScrollingList.ListType.Circular)
			{
				_idCalculationFunc = GetLoopedContentID;
			}
			else
			{
				_idCalculationFunc = GetNonLoopedContentID;
			}
		}

		public int GetInitialContentID(int listBoxID)
		{
			int contentCount = GetContentCount();
			if (contentCount == 0)
			{
				return int.MinValue;
			}
			int arg = 0;
			int num = _listSetting.InitFocusingContentID;
			switch (_listSetting.FocusingPosition)
			{
			case CircularScrollingList.FocusingPosition.Top:
			case CircularScrollingList.FocusingPosition.Bottom:
				if (_listSetting.ListType == CircularScrollingList.ListType.Circular)
				{
					arg = 0;
				}
				else if (contentCount <= _numOfBoxes)
				{
					num = 0;
				}
				else
				{
					int num2 = contentCount - num - _numOfBoxes;
					if (num2 < 0)
					{
						num += num2;
					}
				}
				arg = (_listSetting.ReverseContentOrder ? (_numOfBoxes - 1 - listBoxID + num) : (listBoxID + num));
				break;
			case CircularScrollingList.FocusingPosition.Center:
				arg = (_listSetting.ReverseContentOrder ? (_numOfBoxes / 2 - listBoxID) : (listBoxID - _numOfBoxes / 2));
				arg += num;
				break;
			}
			return _idCalculationFunc(arg);
		}

		public int GetRefreshedContentID(int origContentID)
		{
			if (_listBank.GetContentCount() != 0)
			{
				return _idCalculationFunc(origContentID);
			}
			return int.MinValue;
		}

		public int GetContentIDByNextBox(int nextBoxContentID)
		{
			return _idCalculationFunc(nextBoxContentID - _idFactor);
		}

		public int GetContentIDByLastBox(int lastBoxContentID)
		{
			return _idCalculationFunc(lastBoxContentID + _idFactor);
		}

		public ContentIDState GetIDState(int contentID)
		{
			return GetIDState(contentID, _listBank.GetContentCount());
		}

		public static ContentIDState GetIDState(int contentID, int contentCount)
		{
			if (contentID == int.MinValue)
			{
				return ContentIDState.NoContent;
			}
			ContentIDState contentIDState = ((contentID < 0) ? ContentIDState.Underflow : ((contentID < contentCount) ? ContentIDState.Valid : ContentIDState.Overflow));
			if (contentIDState != ContentIDState.Valid)
			{
				return contentIDState;
			}
			if (contentID == 0)
			{
				contentIDState |= ContentIDState.First;
			}
			if (contentID == contentCount - 1)
			{
				contentIDState |= ContentIDState.Last;
			}
			return contentIDState;
		}

		public bool IsIDValid(int contentID)
		{
			if (contentID >= 0)
			{
				return contentID < _listBank.GetContentCount();
			}
			return false;
		}

		public int GetShortestIDDiff(int fromContentID, int toContentID)
		{
			if (!IsIDValid(fromContentID))
			{
				throw new IndexOutOfRangeException("fromContentID");
			}
			if (!IsIDValid(toContentID))
			{
				throw new IndexOutOfRangeException("toContentID");
			}
			int num = toContentID - fromContentID;
			if (_listSetting.ListType == CircularScrollingList.ListType.Linear)
			{
				return num;
			}
			int contentCount = _listBank.GetContentCount();
			int num2 = contentCount / 2;
			if (Mathf.Abs(num) > num2)
			{
				num -= (int)Mathf.Sign(num) * contentCount;
			}
			return num;
		}

		private int GetLoopedContentID(int contentID)
		{
			return (int)Mathf.Repeat(contentID, _listBank.GetContentCount());
		}

		private int GetNonLoopedContentID(int contentID)
		{
			return contentID;
		}

		public int GetContentCount()
		{
			return _listBank.GetContentCount();
		}

		public bool TryGetContent(int contentID, out IListContent content)
		{
			bool flag = IsIDValid(contentID);
			content = (flag ? _listBank.GetListContent(contentID) : null);
			return flag;
		}
	}
}

}
