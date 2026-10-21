namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.AirFishLab.ScrollingList.Util;
using global::UnityEngine;

namespace AirFishLab.ScrollingList.ListStateProcessing.Linear
{
	internal class VelocityMovementCurve : IMovementCurve
	{
		private readonly DeltaTimeCurve _velocityFactorCurve;

		private float _baseVelocity;

		public float lastVelocity { get; private set; }

		public VelocityMovementCurve(AnimationCurve factorCurve)
		{
			_velocityFactorCurve = new DeltaTimeCurve(factorCurve);
		}

		public void SetMovement(float baseVelocity)
		{
			_velocityFactorCurve.Reset();
			_baseVelocity = baseVelocity;
			lastVelocity = _velocityFactorCurve.CurrentEvaluate() * _baseVelocity;
		}

		public bool IsMovementEnded()
		{
			return _velocityFactorCurve.IsTimeOut();
		}

		public void EndMovement()
		{
			_velocityFactorCurve.Evaluate(_velocityFactorCurve.TotalTime);
		}

		public float GetDistance(float deltaTime)
		{
			lastVelocity = _velocityFactorCurve.Evaluate(deltaTime) * _baseVelocity;
			return lastVelocity * deltaTime;
		}
	}
}

}
