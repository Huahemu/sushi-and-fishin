namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

namespace AirFishLab.ScrollingList.ListStateProcessing.Linear
{
	public class FreeMovementCtrl : IMovementCtrl
	{
		private readonly VelocityMovementCurve _releasingMovementCurve;

		private readonly DistanceMovementCurve _aligningMovementCurve;

		private bool _isDragging;

		private float _draggingDistance;

		private readonly bool _toAlign;

		private readonly float _maxMovingDistance;

		private readonly float _exceedingDistanceLimit;

		private const float _stopVelocityThreshold = 200f;

		private readonly Func<float> _getFocusingPositionOffset;

		private readonly Func<ListFocusingState> _getFocusingStateFunc;

		public FreeMovementCtrl(AnimationCurve releasingCurve, bool toAlign, float maxMovingDistance, float exceedingDistanceLimit, Func<float> getFocusingPositionOffset, Func<ListFocusingState> getFocusingStateFunc)
		{
			_releasingMovementCurve = new VelocityMovementCurve(releasingCurve);
			_aligningMovementCurve = new DistanceMovementCurve(new AnimationCurve(new Keyframe(0f, 0f, 0f, 8f), new Keyframe(0.25f, 1f, 0f, 0f)));
			_toAlign = toAlign;
			_maxMovingDistance = maxMovingDistance;
			_exceedingDistanceLimit = exceedingDistanceLimit;
			_getFocusingPositionOffset = getFocusingPositionOffset;
			_getFocusingStateFunc = getFocusingStateFunc;
		}

		public void SetMovement(float value, bool isDragging)
		{
			_isDragging = isDragging;
			if (isDragging)
			{
				_draggingDistance = value;
				_aligningMovementCurve.EndMovement();
				_releasingMovementCurve.EndMovement();
			}
			else if (_getFocusingStateFunc() != ListFocusingState.Middle)
			{
				_aligningMovementCurve.SetMovement(0f - _getFocusingPositionOffset());
			}
			else
			{
				_releasingMovementCurve.SetMovement(value);
			}
		}

		public bool IsMovementEnded()
		{
			if (!_isDragging && _aligningMovementCurve.IsMovementEnded())
			{
				return _releasingMovementCurve.IsMovementEnded();
			}
			return false;
		}

		public float GetDistance(float deltaTime)
		{
			float result = 0f;
			float num = _getFocusingPositionOffset();
			ListFocusingState focusingState = _getFocusingStateFunc();
			if (_isDragging)
			{
				if (Mathf.Approximately(_draggingDistance, 0f))
				{
					return 0f;
				}
				result = LimitMovingDistance(_draggingDistance);
				_draggingDistance = 0f;
				if (!MovementUtility.IsGoingToFar(focusingState, _exceedingDistanceLimit, num + result))
				{
					return result;
				}
				result = _exceedingDistanceLimit * Mathf.Sign(result) - num;
			}
			else if (!_aligningMovementCurve.IsMovementEnded())
			{
				result = _aligningMovementCurve.GetDistance(deltaTime);
			}
			else if (!_releasingMovementCurve.IsMovementEnded())
			{
				result = LimitMovingDistance(_releasingMovementCurve.GetDistance(deltaTime));
				if (!MovementUtility.IsGoingToFar(focusingState, _exceedingDistanceLimit, num + result) && !IsTooSlow())
				{
					return result;
				}
				_releasingMovementCurve.EndMovement();
				_aligningMovementCurve.SetMovement(0f - _getFocusingPositionOffset());
				result = _aligningMovementCurve.GetDistance(deltaTime);
			}
			return result;
		}

		public void EndMovement()
		{
			_isDragging = false;
			_releasingMovementCurve.EndMovement();
			_aligningMovementCurve.EndMovement();
		}

		private float LimitMovingDistance(float value)
		{
			return Mathf.Min(Mathf.Abs(value), _maxMovingDistance) * Mathf.Sign(value);
		}

		private bool IsTooSlow()
		{
			if (_toAlign)
			{
				return Mathf.Abs(_releasingMovementCurve.lastVelocity) < 200f;
			}
			return false;
		}
	}
}

}
