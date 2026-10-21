using System.Collections;
using UnityEngine;

namespace PixPlays.ElementalVFX
{
	public class ProjectileVfx : BaseVfx
	{
		[SerializeField]
		private ParticleSystem _CastEffect;

		[SerializeField]
		private ParticleSystem _HitEffect;

		[SerializeField]
		private ParticleSystem _ProjectileEffect;

		[SerializeField]
		private float _FlySpeed = 1f;

		[SerializeField]
		private AnimationCurve _FlyCurve;

		[SerializeField]
		private Vector2 _FlyCurveDirection;

		[SerializeField]
		private bool _RandomizeFlyCurveDirection;

		[SerializeField]
		private float _FlyCurveStrength;

		[SerializeField]
		private float _ProjectileFlyDelay;

		[SerializeField]
		private float _ProjectileDeactivateDelay;

		public override void Play(VfxData data)
		{
			base.Play(data);
			StartCoroutine(Coroutine_Projectile());
		}

		private IEnumerator Coroutine_Projectile()
		{
			_CastEffect.gameObject.SetActive(value: true);
			_CastEffect.transform.position = _data.Source;
			_CastEffect.transform.forward = _data.Target - _data.Source;
			_CastEffect.Play();
			yield return new WaitForSeconds(_ProjectileFlyDelay);
			_ProjectileEffect.gameObject.SetActive(value: true);
			_ProjectileEffect.transform.position = _CastEffect.transform.position;
			_ProjectileEffect.Play();
			_FlyCurveDirection = _FlyCurveDirection.normalized;
			if (_RandomizeFlyCurveDirection)
			{
				_FlyCurveDirection = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
			}
			float lerp = 0f;
			Vector3 startPos = _ProjectileEffect.transform.position;
			while (lerp < 1f)
			{
				Vector3 vector = Vector3.Lerp(startPos, _data.Target, lerp);
				vector += (Vector3)_FlyCurveDirection * _FlyCurve.Evaluate(lerp) * _FlyCurveStrength;
				if (lerp > 0f)
				{
					_ProjectileEffect.transform.forward = vector - _ProjectileEffect.transform.position;
				}
				_ProjectileEffect.transform.position = vector;
				lerp += Time.deltaTime / _FlySpeed;
				yield return null;
			}
			_HitEffect.transform.forward = _ProjectileEffect.transform.position - _data.Target;
			_ProjectileEffect.transform.position = _data.Target;
			_ProjectileEffect.Stop();
			_HitEffect.transform.position = _data.Target;
			_HitEffect.gameObject.SetActive(value: true);
			_HitEffect.Play();
			yield return new WaitForSeconds(_ProjectileDeactivateDelay);
			_ProjectileEffect.gameObject.SetActive(value: false);
		}

		public override void Stop()
		{
			base.Stop();
			if (base.gameObject != null)
			{
				_HitEffect.Stop();
				_ProjectileEffect.Stop();
				_CastEffect.Stop();
			}
		}
	}
}
