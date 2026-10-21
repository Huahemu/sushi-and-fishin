namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

public class WheelOfFortuneLocal : MonoBehaviour
{
	[Header("References")]
	[SerializeField]
	private WheelLocal wheel;

	[SerializeField]
	private CasinoGameFeedbacks gameFeedbacks;

	public void Play()
	{
		if (wheel != null)
		{
			wheel.SpinTheWheel();
		}
	}
}

}
