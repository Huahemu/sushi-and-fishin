using System;
using System.Collections;
using System.Collections.Generic;
using PaperPixelGames.Core.Player;
using UnityEngine;

namespace PaperPixelGames.Core.Boss
{
	public abstract class CrabBossLocalRockSlot : MonoBehaviour
	{
		[Header("Visual")]
		[SerializeField]
		private GameObject _rockVisualRoot;

		[SerializeField]
		private GameObject _flightVisualRoot;

		[SerializeField]
		private GameObject _playerImpactVisualRoot;

		[SerializeField]
		private GameObject _worldImpactVisualRoot;

		[Header("Impact Presentation")]
		[SerializeField]
		[Min(0.01f)]
		private float _impactVfxDuration = 0.75f;

		[SerializeField]
		[Min(0f)]
		private float _worldImpactSurfaceOffset = 0.02f;

		[SerializeField]
		private AudioClip _impactSfx;

		[SerializeField]
		[Range(0f, 1f)]
		private float _impactSfxVolume = 1f;

		private Coroutine _deactivateCoroutine;

		private Rigidbody _physicsBody;

		private Collider[] _physicsColliders;

		private bool _isInitialized;

		private bool _physicsArmed;

		private bool _impactReported;

		private Transform _ignoredCollisionRoot;

		private int _playerLayer = -1;

		private int _clientPlayerLayer = -1;

		private LayerMask _worldImpactLayers;

		private Vector3 _physicsVelocity;

		private readonly List<Collider> _ignoredWorldColliders = new List<Collider>();

		private Transform _worldImpactVisualTransform;

		private Vector3 _worldImpactInitialLocalPosition;

		private Quaternion _worldImpactInitialLocalRotation;

		private bool _alignWorldImpactToSurface;

		private ParticleSystem[] _flightParticles;

		private TrailRenderer[] _flightTrails;

		public float ImpactVfxDuration => _impactVfxDuration;

		public bool HasPlayerImpactVisual => _playerImpactVisualRoot != null;

		public bool HasWorldImpactVisual => _worldImpactVisualRoot != null;

		public bool HasImpactSfx => _impactSfx != null;

		public event Action<CrabBossLocalRockSlot, CrabBossRockImpactType, ulong, Vector3, Vector3, Vector3, Quaternion> LocalImpactDetected;

		public void SetIgnoredCollisionRoot(Transform ignoredCollisionRoot)
		{
			_ignoredCollisionRoot = ignoredCollisionRoot;
		}

		public void SetWorldImpactLayers(LayerMask worldImpactLayers)
		{
			_worldImpactLayers = worldImpactLayers;
		}

		public void SetWorldImpactSurfaceAlignment(bool enabled)
		{
			_alignWorldImpactToSurface = enabled;
		}

		protected virtual void Awake()
		{
			EnsureInitialized();
			DisarmPhysics();
			StopFlightVisual();
		}

		private void EnsureInitialized()
		{
			if (!_isInitialized)
			{
				_playerLayer = LayerMask.NameToLayer("Player");
				_clientPlayerLayer = LayerMask.NameToLayer("ClientPlayer");
				_physicsBody = GetComponent<Rigidbody>();
				_physicsColliders = GetComponentsInChildren<Collider>(includeInactive: true);
				CacheFlightVisuals();
				CacheWorldImpactVisualTransform();
				_isInitialized = true;
			}
		}

		public virtual void ResetSlot()
		{
			EnsureInitialized();
			if (_deactivateCoroutine != null)
			{
				StopCoroutine(_deactivateCoroutine);
				_deactivateCoroutine = null;
			}
			DisarmPhysics();
			StopFlightVisual();
			RestoreWorldImpactVisualTransform();
			SetActiveIfNeeded(_playerImpactVisualRoot, active: false);
			SetActiveIfNeeded(_worldImpactVisualRoot, active: false);
			SetActiveIfNeeded(_rockVisualRoot, active: true);
			SetActiveIfNeeded(base.gameObject, active: false);
		}

		public virtual void PlayImpact(CrabBossRockImpactType impactType, Vector3 position, Vector3 normal)
		{
			if (impactType == CrabBossRockImpactType.None)
			{
				ResetSlot();
				return;
			}
			DisarmPhysics();
			StopFlightVisual();
			SetActiveIfNeeded(base.gameObject, active: true);
			base.transform.position = position;
			if (normal.sqrMagnitude > Mathf.Epsilon)
			{
				base.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal.normalized);
			}
			SetActiveIfNeeded(_rockVisualRoot, active: false);
			PlayImpactVisual((impactType == CrabBossRockImpactType.Player) ? _playerImpactVisualRoot : _worldImpactVisualRoot);
			if (_impactSfx != null && _impactSfxVolume > 0f)
			{
				AudioSource.PlayClipAtPoint(_impactSfx, position, _impactSfxVolume);
			}
			if (_deactivateCoroutine != null)
			{
				StopCoroutine(_deactivateCoroutine);
			}
			_deactivateCoroutine = StartCoroutine(DeactivateAfterImpact());
		}

		public virtual void PlayPhysicsImpact(CrabBossRockImpactType impactType, Vector3 impactPosition, Vector3 impactNormal, Vector3 physicsPosition, Quaternion physicsRotation)
		{
			if (impactType == CrabBossRockImpactType.None)
			{
				ResetSlot();
				return;
			}
			DisarmPhysics();
			StopFlightVisual();
			SetActiveIfNeeded(base.gameObject, active: true);
			base.transform.SetPositionAndRotation(physicsPosition, physicsRotation);
			SetActiveIfNeeded(_rockVisualRoot, active: false);
			if (impactType == CrabBossRockImpactType.World && _alignWorldImpactToSurface)
			{
				PlaceWorldImpactOnSurface(impactPosition, impactNormal);
			}
			PlayImpactVisual((impactType == CrabBossRockImpactType.Player) ? _playerImpactVisualRoot : _worldImpactVisualRoot);
			if (_impactSfx != null && _impactSfxVolume > 0f)
			{
				AudioSource.PlayClipAtPoint(_impactSfx, impactPosition, _impactSfxVolume);
			}
			if (_deactivateCoroutine != null)
			{
				StopCoroutine(_deactivateCoroutine);
			}
			_deactivateCoroutine = StartCoroutine(DeactivateAfterImpact());
		}

		public bool ValidatePresentation(out string error)
		{
			EnsureInitialized();
			if (_rockVisualRoot == null)
			{
				error = base.name + " rock visual root is missing.";
				return false;
			}
			if (_physicsBody == null)
			{
				error = base.name + " requires a Rigidbody for local physics impacts.";
				return false;
			}
			if (_physicsColliders == null || _physicsColliders.Length == 0)
			{
				error = base.name + " requires at least one Collider for local physics impacts.";
				return false;
			}
			if (_impactVfxDuration <= 0f || float.IsNaN(_impactVfxDuration) || float.IsInfinity(_impactVfxDuration))
			{
				error = base.name + " impact VFX duration must be greater than zero and finite.";
				return false;
			}
			error = null;
			return true;
		}

		protected void PrepareVisual(Vector3 position, Quaternion rotation)
		{
			EnsureInitialized();
			if (_deactivateCoroutine != null)
			{
				StopCoroutine(_deactivateCoroutine);
				_deactivateCoroutine = null;
			}
			DisarmPhysics();
			RestoreWorldImpactVisualTransform();
			base.transform.SetPositionAndRotation(position, rotation);
			SetActiveIfNeeded(_playerImpactVisualRoot, active: false);
			SetActiveIfNeeded(_worldImpactVisualRoot, active: false);
			SetActiveIfNeeded(_rockVisualRoot, active: true);
			SetActiveIfNeeded(base.gameObject, active: true);
			PlayFlightVisual();
		}

		protected static void SetActiveIfNeeded(GameObject target, bool active)
		{
			if (target != null && target.activeSelf != active)
			{
				target.SetActive(active);
			}
		}

		private IEnumerator DeactivateAfterImpact()
		{
			yield return new WaitForSeconds(_impactVfxDuration);
			_deactivateCoroutine = null;
			ResetSlot();
		}

		private static void PlayImpactVisual(GameObject impactRoot)
		{
			if (!(impactRoot == null))
			{
				SetActiveIfNeeded(impactRoot, active: true);
				ParticleSystem[] componentsInChildren = impactRoot.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					componentsInChildren[i].Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
					componentsInChildren[i].Play(withChildren: true);
				}
			}
		}

		private void CacheWorldImpactVisualTransform()
		{
			if (!(_worldImpactVisualRoot == null))
			{
				_worldImpactVisualTransform = _worldImpactVisualRoot.transform;
				_worldImpactInitialLocalPosition = _worldImpactVisualTransform.localPosition;
				_worldImpactInitialLocalRotation = _worldImpactVisualTransform.localRotation;
			}
		}

		private void PlaceWorldImpactOnSurface(Vector3 impactPosition, Vector3 impactNormal)
		{
			if (!(_worldImpactVisualTransform == null))
			{
				Vector3 vector = ((impactNormal.sqrMagnitude > Mathf.Epsilon) ? impactNormal.normalized : Vector3.up);
				_worldImpactVisualTransform.SetPositionAndRotation(impactPosition + vector * _worldImpactSurfaceOffset, Quaternion.FromToRotation(Vector3.up, vector) * _worldImpactInitialLocalRotation);
			}
		}

		private void RestoreWorldImpactVisualTransform()
		{
			if (!(_worldImpactVisualTransform == null))
			{
				_worldImpactVisualTransform.localPosition = _worldImpactInitialLocalPosition;
				_worldImpactVisualTransform.localRotation = _worldImpactInitialLocalRotation;
			}
		}

		protected void ArmPhysics(Vector3 velocity)
		{
			if (!(_physicsBody == null))
			{
				_impactReported = false;
				_physicsArmed = true;
				_physicsVelocity = velocity;
				for (int i = 0; i < _physicsColliders.Length; i++)
				{
					_physicsColliders[i].enabled = true;
				}
				_physicsBody.isKinematic = false;
				_physicsBody.useGravity = false;
				_physicsBody.linearVelocity = velocity;
				_physicsBody.angularVelocity = Vector3.zero;
			}
		}

		protected void DisarmPhysics()
		{
			_physicsArmed = false;
			_impactReported = false;
			_physicsVelocity = Vector3.zero;
			RestoreIgnoredWorldCollisions();
			if (_physicsColliders != null)
			{
				for (int i = 0; i < _physicsColliders.Length; i++)
				{
					_physicsColliders[i].enabled = false;
				}
			}
			if (_physicsBody != null)
			{
				if (!_physicsBody.isKinematic)
				{
					_physicsBody.linearVelocity = Vector3.zero;
					_physicsBody.angularVelocity = Vector3.zero;
				}
				_physicsBody.useGravity = false;
				_physicsBody.isKinematic = true;
			}
		}

		private void OnCollisionEnter(Collision collision)
		{
			if (!_physicsArmed || _impactReported || collision.contactCount <= 0)
			{
				return;
			}
			if (_ignoredCollisionRoot != null && collision.collider.transform.IsChildOf(_ignoredCollisionRoot))
			{
				IgnoreCollisionForCurrentFlight(collision.collider);
				if (_physicsBody != null)
				{
					_physicsBody.linearVelocity = _physicsVelocity;
				}
				return;
			}
			ContactPoint contact = collision.GetContact(0);
			PPG_Player componentInParent = collision.collider.GetComponentInParent<PPG_Player>();
			bool flag = componentInParent != null && componentInParent.IsSpawned && (collision.collider.gameObject.layer == _playerLayer || collision.collider.gameObject.layer == _clientPlayerLayer);
			if (!flag && !IsAllowedWorldImpactLayer(collision.collider.gameObject.layer))
			{
				IgnoreCollisionForCurrentFlight(collision.collider);
				if (_physicsBody != null)
				{
					_physicsBody.linearVelocity = _physicsVelocity;
				}
				return;
			}
			CrabBossRockImpactType arg = (flag ? CrabBossRockImpactType.Player : CrabBossRockImpactType.World);
			ulong arg2 = ((componentInParent != null) ? componentInParent.OwnerClientId : ulong.MaxValue);
			Vector3 arg3 = contact.normal;
			if (arg3.sqrMagnitude <= Mathf.Epsilon)
			{
				arg3 = base.transform.position - contact.point;
				if (arg3.sqrMagnitude <= Mathf.Epsilon)
				{
					arg3 = Vector3.up;
				}
				else
				{
					arg3.Normalize();
				}
			}
			_impactReported = true;
			_physicsArmed = false;
			if (_physicsBody != null)
			{
				_physicsBody.linearVelocity = Vector3.zero;
				_physicsBody.angularVelocity = Vector3.zero;
				_physicsBody.isKinematic = true;
			}
			for (int i = 0; i < _physicsColliders.Length; i++)
			{
				_physicsColliders[i].enabled = false;
			}
			StopFlightVisual();
			SetActiveIfNeeded(_rockVisualRoot, active: false);
			LocalImpactDetected?.Invoke(this, arg, arg2, contact.point, arg3, base.transform.position, base.transform.rotation);
		}

		private bool IsAllowedWorldImpactLayer(int layer)
		{
			if (layer >= 0)
			{
				return (_worldImpactLayers.value & (1 << layer)) != 0;
			}
			return false;
		}

		private void IgnoreCollisionForCurrentFlight(Collider other)
		{
			if (other == null || _physicsColliders == null)
			{
				return;
			}
			if (!_ignoredWorldColliders.Contains(other))
			{
				_ignoredWorldColliders.Add(other);
			}
			for (int i = 0; i < _physicsColliders.Length; i++)
			{
				Collider collider = _physicsColliders[i];
				if (collider != null)
				{
					Physics.IgnoreCollision(collider, other, ignore: true);
				}
			}
		}

		private void RestoreIgnoredWorldCollisions()
		{
			if (_physicsColliders == null)
			{
				_ignoredWorldColliders.Clear();
				return;
			}
			for (int i = 0; i < _ignoredWorldColliders.Count; i++)
			{
				Collider collider = _ignoredWorldColliders[i];
				if (collider == null)
				{
					continue;
				}
				for (int j = 0; j < _physicsColliders.Length; j++)
				{
					Collider collider2 = _physicsColliders[j];
					if (collider2 != null)
					{
						Physics.IgnoreCollision(collider2, collider, ignore: false);
					}
				}
			}
			_ignoredWorldColliders.Clear();
		}

		private void CacheFlightVisuals()
		{
			if (_flightVisualRoot == null)
			{
				_flightParticles = Array.Empty<ParticleSystem>();
				_flightTrails = Array.Empty<TrailRenderer>();
			}
			else
			{
				_flightParticles = _flightVisualRoot.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
				_flightTrails = _flightVisualRoot.GetComponentsInChildren<TrailRenderer>(includeInactive: true);
			}
		}

		private void PlayFlightVisual()
		{
			if (_flightVisualRoot == null)
			{
				return;
			}
			EnsureFlightVisualCache();
			SetActiveIfNeeded(_flightVisualRoot, active: true);
			for (int i = 0; i < _flightTrails.Length; i++)
			{
				TrailRenderer trailRenderer = _flightTrails[i];
				if (!(trailRenderer == null))
				{
					trailRenderer.Clear();
					trailRenderer.emitting = true;
				}
			}
			for (int j = 0; j < _flightParticles.Length; j++)
			{
				ParticleSystem particleSystem = _flightParticles[j];
				if (!(particleSystem == null))
				{
					particleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
					particleSystem.Play(withChildren: true);
				}
			}
		}

		private void StopFlightVisual()
		{
			if (_flightVisualRoot == null)
			{
				return;
			}
			EnsureFlightVisualCache();
			for (int i = 0; i < _flightTrails.Length; i++)
			{
				TrailRenderer trailRenderer = _flightTrails[i];
				if (!(trailRenderer == null))
				{
					trailRenderer.emitting = false;
					trailRenderer.Clear();
				}
			}
			for (int j = 0; j < _flightParticles.Length; j++)
			{
				ParticleSystem particleSystem = _flightParticles[j];
				if (particleSystem != null)
				{
					particleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
				}
			}
			SetActiveIfNeeded(_flightVisualRoot, active: false);
		}

		private void EnsureFlightVisualCache()
		{
			if (_flightParticles == null || _flightTrails == null)
			{
				CacheFlightVisuals();
			}
		}
	}
}
