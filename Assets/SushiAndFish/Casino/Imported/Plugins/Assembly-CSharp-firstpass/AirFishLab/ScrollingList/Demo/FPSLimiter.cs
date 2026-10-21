namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

namespace AirFishLab.ScrollingList.Demo
{
	public class FPSLimiter : MonoBehaviour
	{
		[SerializeField]
		private int _targetFPS = 60;

		private void Start()
		{
			Application.targetFrameRate = _targetFPS;
		}
	}
}

}
