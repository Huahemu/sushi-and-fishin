namespace SushiAndFish.Casino.Original
{
using global::UnityEngine;

namespace EpicToonFX
{
	public class ETFXRotation : MonoBehaviour
	{
		public enum spaceEnum
		{
			Local = 0,
			World = 1
		}

		[Header("Rotate axises by degrees per second")]
		public Vector3 rotateVector = Vector3.zero;

		public spaceEnum rotateSpace;

		private void Start()
		{
		}

		private void Update()
		{
			if (rotateSpace == spaceEnum.Local)
			{
				base.transform.Rotate(rotateVector * Time.deltaTime);
			}
			if (rotateSpace == spaceEnum.World)
			{
				base.transform.Rotate(rotateVector * Time.deltaTime, Space.World);
			}
		}
	}
}

}
