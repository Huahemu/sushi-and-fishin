namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

namespace AirFishLab.ScrollingList.Util
{
	public class DeltaTimeCurve
	{
		private readonly AnimationCurve _curve;

		private float _timePassed;

		public readonly float TotalTime;

		public DeltaTimeCurve(AnimationCurve curve)
		{
			_curve = curve;
			TotalTime = _curve[_curve.length - 1].time;
			_timePassed = TotalTime + 1f;
		}

		public void Reset()
		{
			_timePassed = 0f;
		}

		public bool IsTimeOut()
		{
			return _timePassed > TotalTime;
		}

		public float Evaluate(float deltaTime)
		{
			_timePassed += deltaTime;
			return _curve.Evaluate(_timePassed);
		}

		public float CurrentEvaluate()
		{
			return _curve.Evaluate(_timePassed);
		}
	}
}

}
