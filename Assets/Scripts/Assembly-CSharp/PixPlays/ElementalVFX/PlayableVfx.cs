using UnityEngine;
using UnityEngine.Playables;

namespace PixPlays.ElementalVFX
{
	public class PlayableVfx : VfxReference
	{
		[SerializeField]
		private PlayableDirector _Vfx;

		public override void Play()
		{
			_Vfx.Play();
		}

		public override void Stop()
		{
			_Vfx.Stop();
		}
	}
}
