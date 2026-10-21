namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.Util;
using global::UnityEngine;

namespace AirFishLab.ScrollingList.ListStateProcessing.Linear
{
	public class BoxTransformController : IBoxTransformController
	{
		private int _numOfBoxes;

		private float _unitPos;

		private float _minPos;

		private float _maxPos;

		private float _sideChangingMinPos;

		private float _sideChangingMaxPos;

		private RangeMappingCurve _positionCurve;

		private RangeMappingCurve _scaleCurve;

		private Func<Vector2, float> _getMajorFactorFunc;

		private Func<float, float, float, Vector3> _getLocalPositionFunc;

		public float TopBaseline { get; private set; }

		public float MiddleBaseline { get; private set; }

		public float BottomBaseline { get; private set; }

		public BoxTransformController(ListSetupData setupData)
		{
			_numOfBoxes = setupData.ListBoxes.Count;
			InitializePositionVars(setupData.RectTransform, setupData.ListSetting, _numOfBoxes);
			InitializeFactorGetter(setupData.ListSetting);
			InitializeCurves(setupData.ListSetting);
		}

		private void InitializePositionVars(RectTransform rectTransform, ListSetting listSetting, int numOfBoxes)
		{
			Rect rect = rectTransform.rect;
			float num = ((listSetting.Direction == CircularScrollingList.Direction.Vertical) ? rect.height : rect.width);
			_unitPos = num / (float)(numOfBoxes - 1) / listSetting.BoxDensity;
			float num2 = (((numOfBoxes & 1) == 0) ? 0.5f : 0f);
			TopBaseline = _unitPos * ((float)(numOfBoxes / 2) - num2);
			BottomBaseline = 0f - TopBaseline;
			MiddleBaseline = _unitPos * num2;
			_maxPos = TopBaseline + _unitPos;
			_minPos = 0f - _maxPos;
			_sideChangingMinPos = _minPos + _unitPos * 0.5f;
			_sideChangingMaxPos = _maxPos - _unitPos * 0.5f;
		}

		private void InitializeFactorGetter(ListSetting setting)
		{
			if (setting.Direction == CircularScrollingList.Direction.Vertical)
			{
				_getMajorFactorFunc = FactorUtility.GetVector2Y;
				_getLocalPositionFunc = GetPositionYMajor;
			}
			else
			{
				_getMajorFactorFunc = FactorUtility.GetVector2X;
				_getLocalPositionFunc = GetPositionXMajor;
			}
		}

		private void InitializeCurves(ListSetting setting)
		{
			_positionCurve = new RangeMappingCurve(setting.BoxPositionCurve, -1f, 1f, _sideChangingMinPos, _sideChangingMaxPos);
			_scaleCurve = new RangeMappingCurve(setting.BoxScaleCurve, -1f, 1f, _sideChangingMinPos, _sideChangingMaxPos);
		}

		public void SetInitialLocalTransform(IListBox box, int boxID)
		{
			float num = _unitPos * (float)(boxID * -1 + _numOfBoxes / 2);
			if ((_numOfBoxes & 1) == 0)
			{
				num = _unitPos * (float)(boxID * -1 + _numOfBoxes / 2) - _unitPos / 2f;
			}
			Transform transform = box.GetTransform();
			float minorPosition = GetMinorPosition(num);
			float z = transform.localPosition.z;
			float scaleValue = GetScaleValue(num);
			float z2 = transform.localScale.z;
			transform.localPosition = _getLocalPositionFunc(num, minorPosition, z);
			transform.localScale = new Vector3(scaleValue, scaleValue, z2);
			if (Application.isPlaying)
			{
				box.OnBoxMoved(GetPositionRatio(num));
			}
		}

		public BoxPositionState UpdateLocalTransform(IListBox box, float deltaPos)
		{
			Transform transform = box.GetTransform();
			Vector3 localPosition = transform.localPosition;
			float num = _getMajorFactorFunc(localPosition);
			float majorPosition = GetMajorPosition(num + deltaPos, out var isJumpingToTop, out var isJumpingToBottom);
			float minorPosition = GetMinorPosition(majorPosition);
			Vector3 localScale = transform.localScale;
			float scaleValue = GetScaleValue(majorPosition);
			transform.localPosition = _getLocalPositionFunc(majorPosition, minorPosition, localPosition.z);
			transform.localScale = new Vector3(scaleValue, scaleValue, localScale.z);
			box.OnBoxMoved(GetPositionRatio(num));
			if (!isJumpingToTop)
			{
				if (!isJumpingToBottom)
				{
					return BoxPositionState.Nothing;
				}
				return BoxPositionState.JumpToBottom;
			}
			return BoxPositionState.JumpToTop;
		}

		private float GetPositionRatio(float majorPosition)
		{
			return Mathf.InverseLerp(_sideChangingMinPos, _sideChangingMaxPos, majorPosition) * 2f - 1f;
		}

		private float GetMajorPosition(float positionValue, out bool isJumpingToTop, out bool isJumpingToBottom)
		{
			isJumpingToTop = false;
			isJumpingToBottom = false;
			float num = 0f;
			float result = positionValue;
			if (positionValue < _sideChangingMinPos)
			{
				num = positionValue - _minPos;
				result = _maxPos - _unitPos + num;
				isJumpingToTop = true;
			}
			else if (positionValue > _sideChangingMaxPos)
			{
				num = positionValue - _maxPos;
				result = _minPos + _unitPos + num;
				isJumpingToBottom = true;
			}
			return result;
		}

		private float GetMinorPosition(float majorPosition)
		{
			float num = _positionCurve.Evaluate(majorPosition);
			return _sideChangingMaxPos * num;
		}

		private Vector3 GetPositionXMajor(float majorPos, float minorPos, float z)
		{
			return new Vector3(majorPos, minorPos, z);
		}

		private Vector3 GetPositionYMajor(float majorPos, float minorPos, float z)
		{
			return new Vector3(minorPos, majorPos, z);
		}

		private float GetScaleValue(float majorPosition)
		{
			return _scaleCurve.Evaluate(majorPosition);
		}
	}
}

}
