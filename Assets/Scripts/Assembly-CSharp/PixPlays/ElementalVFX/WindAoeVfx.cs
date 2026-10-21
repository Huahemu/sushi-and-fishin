using UnityEngine;

namespace PixPlays.ElementalVFX
{
	public class WindAoeVfx : LocationVfx
	{
		[SerializeField]
		private Transform _GroundEffect;

		public override void Play(VfxData data)
		{
			base.Play(data);
			_GroundEffect.transform.position = _data.Ground;
		}
	}
}
