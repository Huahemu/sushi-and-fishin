using UnityEngine;

namespace PixPlays.ElementalVFX
{
	public abstract class Shield : BaseVfx, IHittable
	{
		[SerializeField]
		private float _RadiusFactor;

		public override void Play(VfxData data)
		{
			base.Play(data);
			base.transform.position = data.Source;
			base.transform.localScale = Vector3.one * _RadiusFactor * _data.Radius;
			base.gameObject.SetActive(value: true);
			PlayImplementation();
		}

		public override void Stop()
		{
			base.Stop();
			StopImplemenation();
		}

		protected abstract void PlayImplementation();

		protected abstract void StopImplemenation();

		protected abstract void HitImplementation(Vector3 point, Vector3 normal);

		public void OnHit(Vector3 hitPoint, Vector3 normal)
		{
			HitImplementation(hitPoint, normal);
		}
	}
}
