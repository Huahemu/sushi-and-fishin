using System;
using DG.Tweening;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace IndieKit
{
	public class DestructibleObject : MonoBehaviour, IDamageable
	{
		[Serializable]
		private struct GroundExplosionSettings
		{
			public float Force;

			public float Radius;

			public float UpwardsModifier;

			public ForceMode ForceMode;

			public static GroundExplosionSettings Default => new GroundExplosionSettings
			{
				Force = 4f,
				Radius = 1.5f,
				UpwardsModifier = 0f,
				ForceMode = ForceMode.Impulse
			};
		}

		[Serializable]
		private struct UnderwaterExplosionSettings
		{
			public float RadialForce;

			[Tooltip("Extra upward push to mimic buoyant turbulence.")]
			public float UpwardBias;

			[Tooltip("Higher damping makes debris slow down quickly, like moving through water.")]
			public float LinearDamping;

			public float AngularDamping;

			[Tooltip("Optional random spin amount.")]
			public float RandomTorque;

			public ForceMode ForceMode;

			public static UnderwaterExplosionSettings Default => new UnderwaterExplosionSettings
			{
				RadialForce = 2.5f,
				UpwardBias = 1.2f,
				LinearDamping = 2.5f,
				AngularDamping = 2f,
				RandomTorque = 0.75f,
				ForceMode = ForceMode.Impulse
			};
		}

		[SerializeField]
		private int _id;

		[SerializeField]
		private int _prefabType;

		[SerializeField]
		private GameObject _debrisPrefab;

		[SerializeField]
		private float _debrisDestroyDelay = 1f;

		[SerializeField]
		private bool _explodeUnderwater;

		[SerializeField]
		private UnderwaterExplosionSettings _underwaterSettings = UnderwaterExplosionSettings.Default;

		[SerializeField]
		private GroundExplosionSettings _groundSettings = GroundExplosionSettings.Default;

		private bool _destroyed;

		public int Id => _id;

		public int PrefabType => _prefabType;

		public void SetId(int id)
		{
			_id = id;
		}

		public void SetPrefabType(int type)
		{
			_prefabType = type;
		}

		private void OnDestroy()
		{
			if (DestructibleManager.HasInstance)
			{
				Singleton<DestructibleManager>.Instance.Unregister(this);
			}
		}

		public void ApplyDamage(float damage, Vector3 hitPoint)
		{
			if (!_destroyed)
			{
				_destroyed = true;
				if (DestructibleManager.HasInstance)
				{
					Singleton<DestructibleManager>.Instance.Unregister(this);
				}
				SpawnAndExplodeDebris(hitPoint);
				UnityEngine.Object.Destroy(base.gameObject);
			}
		}

		private void SpawnAndExplodeDebris(Vector3 hitPoint)
		{
			if (_debrisPrefab == null)
			{
				return;
			}
			GameObject debris = UnityEngine.Object.Instantiate(_debrisPrefab, base.transform.position, base.transform.rotation);
			debris.transform.localScale = base.transform.localScale;
			Rigidbody[] componentsInChildren = debris.GetComponentsInChildren<Rigidbody>();
			foreach (Rigidbody rb in componentsInChildren)
			{
				if (_explodeUnderwater)
				{
					ExplodeUnderwater(rb, hitPoint);
				}
				else
				{
					ExplodeOnGround(rb, hitPoint);
				}
			}
			Singleton<DelayHelper>.Instance.DelayAction(_debrisDestroyDelay, () =>
			{
				if (debris != null)
				{
					DebrisDespawn(debris.transform);
				}
			});
		}

		private void ExplodeOnGround(Rigidbody rb, Vector3 hitPoint)
		{
			rb.AddExplosionForce(_groundSettings.Force, hitPoint, _groundSettings.Radius, _groundSettings.UpwardsModifier, _groundSettings.ForceMode);
		}

		private void ExplodeUnderwater(Rigidbody rb, Vector3 hitPoint)
		{
			rb.linearDamping = _underwaterSettings.LinearDamping;
			rb.angularDamping = _underwaterSettings.AngularDamping;
			Vector3 vector = rb.worldCenterOfMass - hitPoint;
			if (vector.sqrMagnitude < 0.0001f)
			{
				vector = UnityEngine.Random.onUnitSphere * 0.05f;
			}
			Vector3 normalized = vector.normalized;
			rb.AddForce(normalized * _underwaterSettings.RadialForce, _underwaterSettings.ForceMode);
			rb.AddForce(Vector3.up * _underwaterSettings.UpwardBias, _underwaterSettings.ForceMode);
			if (!(_underwaterSettings.RandomTorque <= 0f))
			{
				Vector3 torque = UnityEngine.Random.insideUnitSphere * _underwaterSettings.RandomTorque;
				rb.AddTorque(torque, _underwaterSettings.ForceMode);
			}
		}

		private void DebrisDespawn(Transform debrisRoot)
		{
			if (debrisRoot == null)
			{
				return;
			}
			DOTween.Kill(debrisRoot);
			Rigidbody[] componentsInChildren = debrisRoot.GetComponentsInChildren<Rigidbody>(includeInactive: true);
			foreach (Rigidbody rigidbody in componentsInChildren)
			{
				if (!(rigidbody == null))
				{
					rigidbody.isKinematic = true;
				}
			}
			DOTween.Sequence().SetTarget(debrisRoot).Join(debrisRoot.DOScale(Vector3.zero, 0.6f).SetEase(Ease.InSine))
				.OnComplete(() =>
				{
					if (debrisRoot != null)
					{
						UnityEngine.Object.Destroy(debrisRoot.gameObject);
					}
				})
				.SetLink(debrisRoot.gameObject, LinkBehaviour.KillOnDestroy);
		}
	}
}
