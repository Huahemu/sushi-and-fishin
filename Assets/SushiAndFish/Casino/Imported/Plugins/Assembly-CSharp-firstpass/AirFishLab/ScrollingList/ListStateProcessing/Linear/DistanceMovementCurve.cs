namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.Util;
using global::UnityEngine;

namespace AirFishLab.ScrollingList.ListStateProcessing.Linear
{
	internal class DistanceMovementCurve : IMovementCurve
	{
		private readonly DeltaTimeCurve _distanceFactorCurve;

		private float _distanceTotal;

		private float _lastDistance;

		public float distanceRemaining => _distanceTotal - _lastDistance;

		public DistanceMovementCurve(AnimationCurve factorCurve)
		{
			_distanceFactorCurve = new DeltaTimeCurve(factorCurve);
		}

		public void SetMovement(float totalDistance)
		{
			_distanceFactorCurve.Reset();
			_distanceTotal = totalDistance;
			_lastDistance = 0f;
		}

		public bool IsMovementEnded()
		{
			return _distanceFactorCurve.IsTimeOut();
		}

		public void EndMovement()
		{
			_distanceFactorCurve.Evaluate(_distanceFactorCurve.TotalTime);
			_lastDistance = _distanceTotal;
		}

		public float GetDistance(float deltaTime)
		{
			float num = _distanceTotal * _distanceFactorCurve.Evaluate(deltaTime);
			float result = num - _lastDistance;
			_lastDistance = num;
			return result;
		}
	}
}

}
