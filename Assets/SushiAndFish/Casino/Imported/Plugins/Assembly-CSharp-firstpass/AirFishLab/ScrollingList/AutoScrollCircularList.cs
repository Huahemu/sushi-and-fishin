namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::System.Threading.Tasks;
using global::UnityEngine;

namespace AirFishLab.ScrollingList
{
	public class AutoScrollCircularList : CircularScrollingList
	{
		[SerializeField]
		private float autoScrollSpeed = 5f;

		[SerializeField]
		private float delayBeforePrinting = 0.1f;

		private bool autoScrollActive;

		private void Start()
		{
			AwaitStart();
		}

		public async void AwaitStart()
		{
			await Task.Delay(Mathf.CeilToInt(autoScrollSpeed * 1000f));
			if (_listSetting.InitializeOnStart)
			{
				Initialize();
				StartAutoScroll();
			}
		}

		public void StartAutoScroll()
		{
			autoScrollActive = true;
			StartCoroutine(AutoScroll());
		}

		public void StopAutoScroll()
		{
			if (autoScrollActive)
			{
				autoScrollActive = false;
			}
		}

		private IEnumerator AutoScroll()
		{
			while (autoScrollActive)
			{
				MoveOneUnitDown();
				UpdateBoxOpacities();
				yield return new WaitForSeconds(autoScrollSpeed);
			}
		}
	}
}

}
