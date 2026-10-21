using Deform;
using UnityEngine;

namespace PaperPixelGames.Core.Restaurant
{
	public sealed class FishTrophyGraphic : MonoBehaviour
	{
		[Header("Playback Bend")]
		[SerializeField]
		private BendDeformer _bendDeformer;

		[SerializeField]
		[Min(0f)]
		private float _minimumPlaybackBendAngle = 6f;

		[SerializeField]
		[Min(0f)]
		private float _maximumPlaybackBendAngle = 15f;

		public BendDeformer BendDeformer => _bendDeformer;

		public void SetPlaybackAmplitude(float normalizedAmplitude, bool hasAudiblePlayback)
		{
			if (!(_bendDeformer == null))
			{
				_bendDeformer.Angle = (hasAudiblePlayback ? Mathf.Lerp(_minimumPlaybackBendAngle, Mathf.Max(_minimumPlaybackBendAngle, _maximumPlaybackBendAngle), Mathf.Clamp01(normalizedAmplitude)) : 0f);
			}
		}

		private void OnDisable()
		{
			if (_bendDeformer != null)
			{
				_bendDeformer.Angle = 0f;
			}
		}
	}
}
