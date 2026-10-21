namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.Util;
using global::UnityEngine;

namespace AirFishLab.ScrollingList.ListStateProcessing.Linear
{
	public class ListMovementProcessor : IListMovementProcessor
	{
		private Func<Vector2, float> _getFactorFunc;

		private float _unitPos;

		private FreeMovementCtrl _freeMovementCtrl;

		private UnitMovementCtrl _unitMovementCtrl;

		private ListBoxController _listBoxController;

		private int _scrollingFactor;

		private int _selectionDistanceFactor;

		private bool _alignAtFocusingPosition;

		public void Initialize(ListSetupData setupData)
		{
			ListSetting listSetting = setupData.ListSetting;
			InitializePositionVars(setupData.RectTransform.rect, listSetting.Direction, listSetting.BoxDensity, setupData.ListBoxes.Count);
			InitializeComponents(listSetting);
			_alignAtFocusingPosition = listSetting.AlignAtFocusingPosition;
		}

		public void SetMovement(InputInfo inputInfo)
		{
			switch (inputInfo.Phase)
			{
			case InputPhase.Began:
				if (!_freeMovementCtrl.IsMovementEnded())
				{
					_freeMovementCtrl.EndMovement();
				}
				if (!_unitMovementCtrl.IsMovementEnded())
				{
					_unitMovementCtrl.EndMovement();
				}
				break;
			case InputPhase.Moved:
			{
				float num = _getFactorFunc(inputInfo.DeltaLocalPos);
				_freeMovementCtrl.SetMovement(num, isDragging: true);
				break;
			}
			case InputPhase.Ended:
			{
				float num = _getFactorFunc(inputInfo.DeltaLocalPos);
				float value = num / inputInfo.DeltaTime;
				_freeMovementCtrl.SetMovement(value, isDragging: false);
				break;
			}
			case InputPhase.Scrolled:
				SetUnitMovement((int)inputInfo.DeltaLocalPos.y * _scrollingFactor);
				break;
			}
		}

		public void SetUnitMovement(int unit)
		{
			if (!_freeMovementCtrl.IsMovementEnded())
			{
				_freeMovementCtrl.EndMovement();
			}
			float distanceAdded = (float)unit * _unitPos;
			_unitMovementCtrl.SetMovement(distanceAdded, flag: false);
		}

		public void SetSelectionMovement(int units)
		{
			EndMovement(toAlign: false);
			float distanceAdded = (float)units * _unitPos * (float)_selectionDistanceFactor;
			_unitMovementCtrl.SetMovement(distanceAdded, flag: false);
		}

		public float GetMovement(float detailTime)
		{
			if (!_freeMovementCtrl.IsMovementEnded())
			{
				return _freeMovementCtrl.GetDistance(detailTime);
			}
			if (!_unitMovementCtrl.IsMovementEnded())
			{
				return _unitMovementCtrl.GetDistance(detailTime);
			}
			return 0f;
		}

		public bool IsMovementEnded()
		{
			if (_freeMovementCtrl.IsMovementEnded())
			{
				return _unitMovementCtrl.IsMovementEnded();
			}
			return false;
		}

		public bool NeedToAlign()
		{
			if (!_alignAtFocusingPosition || _freeMovementCtrl.IsMovementEnded())
			{
				return !_unitMovementCtrl.IsMovementEnded();
			}
			return true;
		}

		public void EndMovement(bool toAlign)
		{
			_freeMovementCtrl.EndMovement();
			_unitMovementCtrl.EndMovement();
			if (toAlign)
			{
				_unitMovementCtrl.SetMovement(0f, flag: false);
			}
		}

		private void InitializePositionVars(Rect parentRect, CircularScrollingList.Direction direction, float boxDensity, int numOfBoxes)
		{
			float num = ((direction == CircularScrollingList.Direction.Vertical) ? parentRect.height : parentRect.width);
			_unitPos = num / (float)(numOfBoxes - 1) / boxDensity;
			if (direction == CircularScrollingList.Direction.Vertical)
			{
				_getFactorFunc = FactorUtility.GetVector2Y;
			}
			else
			{
				_getFactorFunc = FactorUtility.GetVector2X;
			}
		}

		private void InitializeComponents(ListSetting setting)
		{
			float exceedingDistanceLimit = _unitPos * 0.3f;
			_freeMovementCtrl = new FreeMovementCtrl(setting.BoxVelocityCurve, setting.AlignAtFocusingPosition, _unitPos * 1.2f, exceedingDistanceLimit, GetFocusingDistanceOffset, GetListFocusingState);
			_unitMovementCtrl = new UnitMovementCtrl(setting.BoxMovementCurve, exceedingDistanceLimit, GetFocusingDistanceOffset, GetListFocusingState);
			_scrollingFactor = ((!setting.ReverseScrollingDirection) ? 1 : (-1));
			_selectionDistanceFactor = ((!setting.ReverseContentOrder) ? 1 : (-1));
		}

		public void SetListBoxController(ListBoxController listBoxController)
		{
			_listBoxController = listBoxController;
		}

		private ListFocusingState GetListFocusingState()
		{
			return _listBoxController.ListFocusingState;
		}

		private float GetFocusingDistanceOffset()
		{
			return _listBoxController.FocusingDistanceOffset;
		}
	}
}

}
