namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

namespace EpicToonFX
{
	public class ETFXLightFade : MonoBehaviour
	{
		public enum OnLifeEnd
		{
			DoNothing = 0,
			Disable = 1,
			Destroy = 2
		}

		[Header("Seconds to dim the light")]
		public float life = 0.2f;

		public OnLifeEnd onLifeEnd = OnLifeEnd.Destroy;

		private Light li;

		private float initIntensity;

		private void Start()
		{
			li = GetComponent<Light>();
			if (li != null)
			{
				initIntensity = li.intensity;
			}
		}

		private void Update()
		{
			if (!(li != null))
			{
				return;
			}
			li.intensity -= initIntensity * (Time.deltaTime / life);
			if (li.intensity <= 0f)
			{
				switch (onLifeEnd)
				{
				case OnLifeEnd.Disable:
					li.enabled = false;
					break;
				case OnLifeEnd.Destroy:
					Object.Destroy(li);
					break;
				case OnLifeEnd.DoNothing:
					break;
				}
			}
		}
	}
}

}
