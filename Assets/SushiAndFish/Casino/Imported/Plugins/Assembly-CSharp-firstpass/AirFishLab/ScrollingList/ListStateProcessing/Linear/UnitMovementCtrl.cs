namespace SushiAndFish.Casino.Original
{
using global::System;
using global::UnityEngine;

namespace AirFishLab.ScrollingList.ListStateProcessing.Linear
{
	public class UnitMovementCtrl : IMovementCtrl
	{
		private readonly DistanceMovementCurve _unitMovementCurve;

		private readonly DistanceMovementCurve _bouncingOffCurve;

		private readonly DistanceMovementCurve _bouncingBackCurve;

		private const float BOUNCING_INTERVAL = 0.125f;

		private readonly float _exceedingDistanceLimit;

		private readonly Func<float> _getFocusingDistanceOffset;

		private readonly Func<ListFocusingState> _getFocusingStateFunc;

		public UnitMovementCtrl(AnimationCurve movementCurve, float exceedingDistanceLimit, Func<float> getFocusingDistanceOffset, Func<ListFocusingState> getFocusingStateFunc)
		{
			AnimationCurve factorCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 5f), new Keyframe(0.125f, 1f, 0f, 0f));
			AnimationCurve factorCurve2 = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(0.125f, 1f, -5f, 0f));
			_unitMovementCurve = new DistanceMovementCurve(movementCurve);
			_bouncingOffCurve = new DistanceMovementCurve(factorCurve);
			_bouncingBackCurve = new DistanceMovementCurve(factorCurve2);
			_exceedingDistanceLimit = exceedingDistanceLimit;
			_getFocusingDistanceOffset = getFocusingDistanceOffset;
			_getFocusingStateFunc = getFocusingStateFunc;
		}

		public void SetMovement(float distanceAdded, bool flag)
		{
			if (_bouncingOffCurve.IsMovementEnded() && _bouncingBackCurve.IsMovementEnded())
			{
				float num = _getFocusingDistanceOffset();
				ListFocusingState listFocusingState = _getFocusingStateFunc();
				float num2 = Mathf.Sign(distanceAdded);
				if ((listFocusingState.HasFlag(ListFocusingState.Top) && num2 < 0f) || (listFocusingState.HasFlag(ListFocusingState.Bottom) && num2 > 0f))
				{
					_bouncingOffCurve.SetMovement(num2 * _exceedingDistanceLimit - num);
					_unitMovementCurve.EndMovement();
				}
				else
				{
					float distanceRemaining = _unitMovementCurve.distanceRemaining;
					distanceAdded = ((Mathf.Approximately(distanceRemaining, 0f) || !Mathf.Approximately(Mathf.Sign(distanceRemaining), Math.Sign(distanceAdded))) ? (distanceAdded - num) : (distanceAdded + _unitMovementCurve.distanceRemaining));
					_unitMovementCurve.SetMovement(distanceAdded);
				}
			}
		}

		public bool IsMovementEnded()
		{
			if (_bouncingOffCurve.IsMovementEnded() && _bouncingBackCurve.IsMovementEnded())
			{
				return _unitMovementCurve.IsMovementEnded();
			}
			return false;
		}

		public float GetDistance(float deltaTime)
		{
			float num = _getFocusingDistanceOffset();
			float num2 = 0f;
			if (!_bouncingOffCurve.IsMovementEnded())
			{
				num2 = _bouncingOffCurve.GetDistance(deltaTime);
				if (_bouncingOffCurve.IsMovementEnded())
				{
					_bouncingBackCurve.SetMovement(0f - (num + num2));
				}
				return num2;
			}
			if (!_bouncingBackCurve.IsMovementEnded())
			{
				return _bouncingBackCurve.GetDistance(deltaTime);
			}
			ListFocusingState focusingState = _getFocusingStateFunc();
			num2 = _unitMovementCurve.GetDistance(deltaTime);
			if (!MovementUtility.IsGoingToFar(focusingState, _exceedingDistanceLimit, num + num2))
			{
				return num2;
			}
			_unitMovementCurve.EndMovement();
			_bouncingBackCurve.SetMovement(0f - num);
			return _bouncingBackCurve.GetDistance(deltaTime);
		}

		public void EndMovement()
		{
			_unitMovementCurve.EndMovement();
			_bouncingOffCurve.EndMovement();
			_bouncingBackCurve.EndMovement();
		}
	}
}

}
