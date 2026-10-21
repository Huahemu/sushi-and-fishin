namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

namespace AirFishLab.ScrollingList
{
	public struct InputInfo
	{
		public InputPhase Phase;

		public Vector2 DeltaLocalPos;

		public Vector2 DeltaLocalPosNormalized;

		public float DeltaTime;

		public override string ToString()
		{
			return $"Phase: {Phase}, " + $"DeltaLocalPos: {DeltaLocalPos}, " + $"DeltaLocalPosNormalized: {DeltaLocalPosNormalized}, " + $"DeltaTime: {DeltaTime}";
		}
	}
}

}
