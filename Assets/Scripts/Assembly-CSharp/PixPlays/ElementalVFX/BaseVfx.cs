using UnityEngine;

namespace PixPlays.ElementalVFX
{
	public class BaseVfx : MonoBehaviour
	{
		[SerializeField]
		private float _SafetyDestroy;

		[SerializeField]
		private float _DestoyDelay;

		protected VfxData _data;

		public virtual void Play(VfxData data)
		{
			_data = data;
			if (_data.Duration > _SafetyDestroy)
			{
				_SafetyDestroy += _data.Duration;
			}
			Object.Destroy(base.gameObject, _SafetyDestroy);
			Invoke("Stop", _data.Duration);
			StopAllCoroutines();
		}

		public virtual void Stop()
		{
			StopAllCoroutines();
			Object.Destroy(base.gameObject, _DestoyDelay);
		}
	}
}
