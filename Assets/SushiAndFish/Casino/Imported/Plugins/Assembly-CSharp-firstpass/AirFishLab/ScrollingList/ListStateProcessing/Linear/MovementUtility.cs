namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

namespace AirFishLab.ScrollingList.ListStateProcessing.Linear
{
	public static class MovementUtility
	{
		public static bool IsGoingToFar(ListFocusingState focusingState, float distanceLimit, float targetDistance)
		{
			switch (focusingState)
			{
			case ListFocusingState.Middle:
				return false;
			case ListFocusingState.TopAndBottom:
				return Mathf.Abs(targetDistance) > distanceLimit;
			default:
				if ((focusingState.HasFlag(ListFocusingState.Bottom) && targetDistance < 0f) || (focusingState.HasFlag(ListFocusingState.Top) && targetDistance > 0f))
				{
					return false;
				}
				return Mathf.Abs(targetDistance) > distanceLimit;
			}
		}
	}
}

}
