using UnityEngine;

namespace PaperPixelGames.Core.Kitchen
{
	public class SushiSurfaceMarker : MonoBehaviour
	{
		[SerializeField]
		private SeasoningSurfaceType _surfaceType = SeasoningSurfaceType.Sushi;

		public SeasoningSurfaceType SurfaceType => _surfaceType;
	}
}
