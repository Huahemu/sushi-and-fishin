using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace PaperPixelGames.Core.Boss.Attacks
{
	[RequireComponent(typeof(NetworkObject))]
	[RequireComponent(typeof(Rigidbody))]
	[MovedFrom(true, "PaperPixelGames.Core.Boss.Attacks", "Assembly-CSharp", "CrabBossShockwave")]
	public sealed class CrabBossExpandingPulse : NetworkBehaviour
	{
		[Header("Wave Shape")]
		[SerializeField]
		private SphereCollider _hitTrigger;

		[SerializeField]
		private Transform _visualRoot;

		[SerializeField]
		private bool _scaleVisualWithRadius;

		[SerializeField]
		private GameObject _expandingVfxPrefab;

		[SerializeField]
		private float _startRadius;

		[SerializeField]
		private float _endRadius;

		[Header("Timing")]
		[SerializeField]
		private float _expandDuration;

		[SerializeField]
		private float _shrinkDuration = 2f;

		[Tooltip("How long the pulse remains at its maximum radius before shrinking.")]
		[SerializeField]
		private float _lifeDuration;

		[Header("Player Knockback")]
		[SerializeField]
		private float _knockbackImpulse;

		[SerializeField]
		private float _upwardForce;

		[Header("Player Oxygen Damage")]
		[SerializeField]
		private byte _oxygenDamagePerTick;

		[SerializeField]
		[Min(0.01f)]
		private float _oxygenDamageInterval = 1f;

		[Header("Debug")]
		[SerializeField]
		private bool _log;

		private readonly HashSet<ulong> _hitClientIds = new HashSet<ulong>();

		private readonly Dictionary<ulong, float> _nextOxygenDamageTimeByClientId = new Dictionary<ulong, float>();

		private Action<PPG_Player> _playerHitHandler;

		private Action<PPG_Player> _playerOxygenDamageHandler;

		private GameObject _expandingVfxInstance;

		private Vector3 _expandingVfxBaseScale = Vector3.one;

		private Rigidbody _gameplayRigidbody;

		private SphereCollider _gameplayHitTrigger;

		private float _elapsed;

		private bool _isRunning;

		private bool _handlesGameplay;

		public float LifeDuration => _expandDuration + _lifeDuration + _shrinkDuration;

		public float KnockbackImpulse => _knockbackImpulse;

		public float UpwardForce => _upwardForce;

		public byte OxygenDamagePerTick => _oxygenDamagePerTick;

		public float Elapsed => _elapsed;

		public bool IsRunning => _isRunning;

		public override void OnNetworkSpawn()
		{
			BeginRuntime(base.IsServer, null);
			base.OnNetworkSpawn();
		}

		private void Awake()
		{
			EnsureGameplayRigidbody();
			EnsureGameplayHitTrigger();
		}

		public override void OnNetworkDespawn()
		{
			StopRuntime();
			base.OnNetworkDespawn();
		}

		public void BeginRuntime(bool handlesGameplay, Action<PPG_Player> playerHitHandler, Action<PPG_Player> playerOxygenDamageHandler = null, float initialElapsed = 0f)
		{
			_elapsed = (IsNonNegativeFinite(initialElapsed) ? initialElapsed : 0f);
			_handlesGameplay = handlesGameplay;
			_playerHitHandler = playerHitHandler;
			_playerOxygenDamageHandler = playerOxygenDamageHandler;
			_isRunning = true;
			_hitClientIds.Clear();
			_nextOxygenDamageTimeByClientId.Clear();
			if (!base.gameObject.activeSelf)
			{
				base.gameObject.SetActive(value: true);
			}
			EnsureGameplayRigidbody();
			EnsureGameplayHitTrigger();
			if (_gameplayHitTrigger != null)
			{
				_gameplayHitTrigger.enabled = handlesGameplay;
			}
			PlayExpandingVfx();
			ApplyElapsedState();
		}

		public void StopRuntime()
		{
			_isRunning = false;
			_handlesGameplay = false;
			_playerHitHandler = null;
			_playerOxygenDamageHandler = null;
			_hitClientIds.Clear();
			_nextOxygenDamageTimeByClientId.Clear();
			_elapsed = 0f;
			if (_hitTrigger != null)
			{
				_hitTrigger.enabled = false;
			}
			if (_gameplayHitTrigger != null)
			{
				_gameplayHitTrigger.enabled = false;
			}
			ApplyRadius(_startRadius);
			StopExpandingVfx();
			if (base.gameObject.activeSelf)
			{
				base.gameObject.SetActive(value: false);
			}
		}

		private void Update()
		{
			if (_isRunning)
			{
				_elapsed += Time.deltaTime;
				ApplyElapsedState();
			}
		}

		private void ApplyElapsedState()
		{
			float num = _expandDuration + _lifeDuration;
			float num2 = num + _shrinkDuration;
			if (_elapsed < _expandDuration)
			{
				float t = Mathf.Clamp01(_elapsed / _expandDuration);
				ApplyRadius(Mathf.Lerp(_startRadius, _endRadius, t));
			}
			else if (_elapsed < num)
			{
				ApplyRadius(_endRadius);
			}
			else
			{
				float t2 = Mathf.Clamp01((_elapsed - num) / _shrinkDuration);
				ApplyRadius(Mathf.Lerp(_endRadius, _startRadius, t2));
			}
			if (_elapsed >= num2)
			{
				StopRuntime();
			}
		}

		private void OnTriggerEnter(Collider other)
		{
			if (!_handlesGameplay || other == null)
			{
				return;
			}
			PPG_Player componentInParent = other.GetComponentInParent<PPG_Player>();
			if (!(componentInParent == null) && componentInParent.IsSpawned && IsPlayerBodyCollider(componentInParent, other))
			{
				ulong ownerClientId = componentInParent.OwnerClientId;
				if (_hitClientIds.Add(ownerClientId))
				{
					_playerHitHandler?.Invoke(componentInParent);
					Log($"Player hit. ClientId={ownerClientId}.");
				}
			}
		}

		private void OnTriggerStay(Collider other)
		{
			if (!_handlesGameplay || other == null || _oxygenDamagePerTick == 0)
			{
				return;
			}
			PPG_Player componentInParent = other.GetComponentInParent<PPG_Player>();
			if (!(componentInParent == null) && componentInParent.IsSpawned && IsPlayerBodyCollider(componentInParent, other))
			{
				ulong ownerClientId = componentInParent.OwnerClientId;
				float time = Time.time;
				if (!_nextOxygenDamageTimeByClientId.TryGetValue(ownerClientId, out var value) || !(time < value))
				{
					_nextOxygenDamageTimeByClientId[ownerClientId] = time + _oxygenDamageInterval;
					_playerOxygenDamageHandler?.Invoke(componentInParent);
					Log($"Oxygen tick. ClientId={ownerClientId} Damage={_oxygenDamagePerTick}.");
				}
			}
		}

		internal static bool IsPlayerBodyCollider(PPG_Player player, Collider collider)
		{
			if (player != null && player.FirstPersonCharacter != null && collider != null && !collider.isTrigger)
			{
				return collider == player.FirstPersonCharacter.GetComponent<CapsuleCollider>();
			}
			return false;
		}

		public bool ValidateConfiguration(out string error)
		{
			if (_hitTrigger == null)
			{
				error = "Expanding Pulse hit trigger is missing.";
				return false;
			}
			if (!_hitTrigger.isTrigger)
			{
				error = "Expanding Pulse SphereCollider must be marked as Is Trigger.";
				return false;
			}
			if (!IsNonNegativeFinite(_startRadius))
			{
				error = "Expanding Pulse start radius must be zero or greater and finite.";
				return false;
			}
			if (!IsPositiveFinite(_endRadius) || _endRadius <= _startRadius)
			{
				error = "Expanding Pulse end radius must be finite and greater than start radius.";
				return false;
			}
			if (!IsPositiveFinite(_expandDuration))
			{
				error = "Expanding Pulse expand duration must be greater than zero and finite.";
				return false;
			}
			if (!IsPositiveFinite(_lifeDuration))
			{
				error = "Expanding Pulse hold duration must be greater than zero and finite.";
				return false;
			}
			if (!IsPositiveFinite(_shrinkDuration))
			{
				error = "Expanding Pulse shrink duration must be greater than zero and finite.";
				return false;
			}
			if (!IsPositiveFinite(_knockbackImpulse))
			{
				error = "Expanding Pulse knockback impulse must be greater than zero and finite.";
				return false;
			}
			if (!IsNonNegativeFinite(_upwardForce))
			{
				error = "Expanding Pulse upward force must be zero or greater and finite.";
				return false;
			}
			if (_oxygenDamagePerTick > 0 && !IsPositiveFinite(_oxygenDamageInterval))
			{
				error = "Oxygen damage interval must be greater than zero and finite when oxygen damage is enabled.";
				return false;
			}
			error = null;
			return true;
		}

		private void ApplyRadius(float radius)
		{
			Vector3 lossyScale = base.transform.lossyScale;
			float num = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
			float num2 = ((num > Mathf.Epsilon) ? (radius / num) : 0f);
			if (_gameplayHitTrigger != null)
			{
				Vector3 lossyScale2 = _gameplayHitTrigger.transform.lossyScale;
				float num3 = Mathf.Max(Mathf.Abs(lossyScale2.x), Mathf.Abs(lossyScale2.y), Mathf.Abs(lossyScale2.z));
				_gameplayHitTrigger.center = Vector3.zero;
				_gameplayHitTrigger.radius = ((num3 > Mathf.Epsilon) ? (radius / num3) : 0f);
			}
			if (_scaleVisualWithRadius && _visualRoot != null)
			{
				float num4 = num2 * 2f;
				_visualRoot.localScale = Vector3.one * num4;
			}
			if (_expandingVfxInstance != null && (_visualRoot == null || !_scaleVisualWithRadius))
			{
				_expandingVfxInstance.transform.localScale = _expandingVfxBaseScale * (num2 * 2f);
			}
		}

		private void PlayExpandingVfx()
		{
			if (_expandingVfxPrefab == null)
			{
				return;
			}
			if (_expandingVfxInstance == null)
			{
				if (_expandingVfxPrefab.transform.IsChildOf(base.transform))
				{
					_expandingVfxInstance = _expandingVfxPrefab;
				}
				else
				{
					Transform parent = ((_visualRoot != null) ? _visualRoot : base.transform);
					_expandingVfxInstance = UnityEngine.Object.Instantiate(_expandingVfxPrefab, parent);
					_expandingVfxInstance.name = _expandingVfxPrefab.name + "_Runtime";
					_expandingVfxInstance.transform.localPosition = Vector3.zero;
					_expandingVfxInstance.transform.localRotation = Quaternion.identity;
				}
				_expandingVfxBaseScale = _expandingVfxInstance.transform.localScale;
			}
			if (!_expandingVfxInstance.activeSelf)
			{
				_expandingVfxInstance.SetActive(value: true);
			}
			ParticleSystem[] componentsInChildren = _expandingVfxInstance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].Play(withChildren: true);
			}
		}

		private void StopExpandingVfx()
		{
			if (!(_expandingVfxInstance == null))
			{
				ParticleSystem[] componentsInChildren = _expandingVfxInstance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					componentsInChildren[i].Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
				}
				if (_expandingVfxInstance.activeSelf)
				{
					_expandingVfxInstance.SetActive(value: false);
				}
			}
		}

		private void EnsureGameplayRigidbody()
		{
			if (_gameplayRigidbody == null)
			{
				_gameplayRigidbody = GetComponent<Rigidbody>();
			}
			if (_gameplayRigidbody == null)
			{
				_gameplayRigidbody = base.gameObject.AddComponent<Rigidbody>();
			}
			_gameplayRigidbody.isKinematic = true;
			_gameplayRigidbody.useGravity = false;
			_gameplayRigidbody.detectCollisions = true;
		}

		private void EnsureGameplayHitTrigger()
		{
			if (!(_hitTrigger == null) && !(_gameplayHitTrigger != null))
			{
				if (_hitTrigger.transform == base.transform)
				{
					_gameplayHitTrigger = _hitTrigger;
				}
				else
				{
					_hitTrigger.enabled = false;
					_gameplayHitTrigger = base.gameObject.AddComponent<SphereCollider>();
				}
				_gameplayHitTrigger.isTrigger = true;
				_gameplayHitTrigger.center = Vector3.zero;
				_gameplayHitTrigger.enabled = false;
			}
		}

		private static bool IsPositiveFinite(float value)
		{
			if (value > 0f && !float.IsNaN(value))
			{
				return !float.IsInfinity(value);
			}
			return false;
		}

		private static bool IsNonNegativeFinite(float value)
		{
			if (value >= 0f && !float.IsNaN(value))
			{
				return !float.IsInfinity(value);
			}
			return false;
		}

		private void Log(string message)
		{
			if (_log)
			{
				UtilityMethods.Log("[CrabBoss][ExpandingPulse] " + message);
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "CrabBossExpandingPulse";
		}
	}
}
