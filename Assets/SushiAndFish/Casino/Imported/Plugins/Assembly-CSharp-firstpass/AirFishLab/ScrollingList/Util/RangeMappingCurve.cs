namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

namespace AirFishLab.ScrollingList.Util
{
	public class RangeMappingCurve
	{
		private readonly AnimationCurve _curve;

		private readonly float _curveXMin;

		private readonly float _curveXMax;

		private readonly float _customXMin;

		private readonly float _customXMax;

		public RangeMappingCurve(AnimationCurve curve, float curveXMin, float curveXMax, float customXMin, float customXMax)
		{
			_curve = curve;
			_curveXMin = curveXMin;
			_curveXMax = curveXMax;
			_customXMin = customXMin;
			_customXMax = customXMax;
		}

		public float Evaluate(float value)
		{
			float t = Mathf.InverseLerp(_customXMin, _customXMax, value);
			return _curve.Evaluate(Mathf.Lerp(_curveXMin, _curveXMax, t));
		}
	}
}

}
