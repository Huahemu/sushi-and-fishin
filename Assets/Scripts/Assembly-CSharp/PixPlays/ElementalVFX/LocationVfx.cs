using UnityEngine;

namespace PixPlays.ElementalVFX
{
	public class LocationVfx : BaseVfx
	{
		[SerializeField]
		private VfxReference _LocationEffect;

		[SerializeField]
		private float _RadiusFactor;

		[SerializeField]
		private bool _IgnoreYDirection;

		public override void Play(VfxData data)
		{
			base.Play(data);
			_LocationEffect.transform.localScale = Vector3.one * _RadiusFactor * _data.Radius;
			_LocationEffect.transform.position = data.Source;
			Vector3 forward = _data.Target - _data.Source;
			if (_IgnoreYDirection)
			{
				forward.y = 0f;
			}
			_LocationEffect.transform.forward = forward;
			_LocationEffect.gameObject.SetActive(value: true);
			_LocationEffect.Play();
		}

		public override void Stop()
		{
			base.Stop();
			_LocationEffect.Stop();
		}
	}
}
