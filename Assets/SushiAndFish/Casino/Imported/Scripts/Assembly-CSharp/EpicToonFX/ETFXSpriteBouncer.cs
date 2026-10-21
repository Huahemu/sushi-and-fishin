namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

namespace EpicToonFX
{
	public class ETFXSpriteBouncer : MonoBehaviour
	{
		public float scaleAmount = 1.1f;

		public float scaleDuration = 1f;

		private Vector3 startScale;

		private float scaleTimer;

		private void Start()
		{
			startScale = base.transform.localScale;
			if (startScale.y != 1f)
			{
				float y = startScale.y / scaleAmount;
				startScale = new Vector3(startScale.x, y, startScale.z);
			}
		}

		private void Update()
		{
			scaleTimer += Time.deltaTime;
			float t = Mathf.Clamp01(scaleTimer / scaleDuration);
			float y = Mathf.Lerp(startScale.y, startScale.y * scaleAmount, t) + Mathf.PingPong(scaleTimer / scaleDuration, 0.1f);
			Vector3 localScale = new Vector3(startScale.x, y, startScale.z);
			base.transform.localScale = localScale;
		}
	}
}

}
