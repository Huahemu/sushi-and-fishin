using System;
using System.Collections.Generic;
using DG.Tweening;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace PaperPixelGames.Core.Ocean
{
	[RequireComponent(typeof(Collider))]
	public class PressureTile : NetworkBehaviour
	{
		[Header("References")]
		[SerializeField]
		private PressureTileHandler _handler;

		[Header("Visual")]
		[SerializeField]
		private Transform _visualRoot;

		[SerializeField]
		private Vector3 _pressedLocalOffset = new Vector3(0f, -0.05f, 0f);

		[SerializeField]
		private float _pressAnimDuration = 0.15f;

		[SerializeField]
		private Ease _pressAnimEase = Ease.OutQuad;

		[SerializeField]
		private bool _playVisualOnSpawnSync = true;

		[Header("VFX")]
		[SerializeField]
		private ParticleSystem _pressedVfx;

		[SerializeField]
		private ParticleSystem _releasedVfx;

		[Header("SFX")]
		[SerializeField]
		private AudioSource _audioSource;

		[SerializeField]
		private AudioClip _pressedSfx;

		[SerializeField]
		private AudioClip _releasedSfx;

		[SerializeField]
		private float _sfxVolume = 1f;

		[Header("Debug")]
		[SerializeField]
		private bool _logDebug;

		[Header("Events")]
		[SerializeField]
		private UnityEvent _onActivated;

		[SerializeField]
		private UnityEvent _onDeactivated;

		[SerializeField]
		private UnityEvent _onLockedResolved;

		private readonly HashSet<ulong> _occupants = new HashSet<ulong>();

		private readonly NetworkVariable<bool> _isActive = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<bool> _isLockedPressed = new NetworkVariable<bool>(value: false);

		private Vector3 _visualInitialLocalPosition;

		private Tween _visualTween;

		private bool _hasCachedVisualPose;

		public bool IsActive => _isActive.Value;

		public bool IsLockedPressed => _isLockedPressed.Value;

		public bool HasHandler => _handler != null;

		private void Awake()
		{
			CacheVisualPose();
		}

		private void Reset()
		{
			GetComponent<Collider>().isTrigger = true;
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			CacheVisualPose();
			NetworkVariable<bool> isActive = _isActive;
			isActive.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isActive.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnActiveStateChanged));
			NetworkVariable<bool> isLockedPressed = _isLockedPressed;
			isLockedPressed.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isLockedPressed.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnLockedPressedChanged));
			if (_playVisualOnSpawnSync)
			{
				ApplyVisualStateImmediate(_isLockedPressed.Value || _isActive.Value);
			}
			if (base.IsServer && _handler != null)
			{
				_handler.RegisterTile(this);
			}
		}

		public override void OnNetworkDespawn()
		{
			NetworkVariable<bool> isActive = _isActive;
			isActive.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isActive.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnActiveStateChanged));
			NetworkVariable<bool> isLockedPressed = _isLockedPressed;
			isLockedPressed.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isLockedPressed.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnLockedPressedChanged));
			if (base.IsServer && _handler != null)
			{
				_handler.UnregisterTile(this);
			}
			_occupants.Clear();
			KillVisualTween();
			base.OnNetworkDespawn();
		}

		private void OnTriggerEnter(Collider other)
		{
			if (!base.IsServer || _isLockedPressed.Value)
			{
				return;
			}
			if (_logDebug)
			{
				UtilityMethods.Log("<b><color=#7ED957>[PressureTile]</color></b> Try to Enter -> " + other.name);
			}
			if (TryGetValidOccupant(other, out var networkObject) && _occupants.Add(networkObject.NetworkObjectId))
			{
				if (_logDebug)
				{
					UtilityMethods.Log("<b><color=#7ED957>[PressureTile]</color></b> Enter -> " + other.name);
				}
				RefreshActiveState();
			}
		}

		private void OnTriggerExit(Collider other)
		{
			if (base.IsServer && !_isLockedPressed.Value && TryGetValidOccupant(other, out var networkObject) && _occupants.Remove(networkObject.NetworkObjectId))
			{
				if (_logDebug)
				{
					UtilityMethods.Log("<b><color=#FF8C42>[PressureTile]</color></b> Exit  -> " + other.name);
				}
				RefreshActiveState();
			}
		}

		private bool TryGetValidOccupant(Collider other, out NetworkObject networkObject)
		{
			networkObject = null;
			if (other.TryGetComponent<PullableRock>(out var component))
			{
				networkObject = component.NetworkObject;
				return networkObject != null;
			}
			return false;
		}

		private void RefreshActiveState()
		{
			if (_isLockedPressed.Value)
			{
				return;
			}
			bool flag = _occupants.Count > 0;
			if (_isActive.Value != flag)
			{
				_isActive.Value = flag;
				if (_logDebug)
				{
					UtilityMethods.Log("<b><color=#57C7FF>[PressureTile]</color></b> Tile: <b>" + base.name + "</b>\n" + $"    Active        : {_isActive.Value}\n" + $"    OccupantCount : {_occupants.Count}\n" + $"    Locked        : {_isLockedPressed.Value}");
				}
				_handler?.HandleTileStateChanged(this);
			}
		}

		private void OnActiveStateChanged(bool previousValue, bool newValue)
		{
			if (!_isLockedPressed.Value)
			{
				AnimateVisualState(newValue);
				PlayFeedback(newValue);
				if (newValue)
				{
					_onActivated?.Invoke();
				}
				else
				{
					_onDeactivated?.Invoke();
				}
			}
		}

		private void OnLockedPressedChanged(bool previousValue, bool newValue)
		{
			if (newValue)
			{
				AnimateVisualState(isPressed: true);
				PlayFeedback(isPressed: true);
				_onLockedResolved?.Invoke();
			}
		}

		private void CacheVisualPose()
		{
			if (!(_visualRoot == null) && !_hasCachedVisualPose)
			{
				_visualInitialLocalPosition = _visualRoot.localPosition;
				_hasCachedVisualPose = true;
			}
		}

		private void AnimateVisualState(bool isPressed)
		{
			if (!(_visualRoot == null))
			{
				CacheVisualPose();
				Vector3 endValue = (isPressed ? (_visualInitialLocalPosition + _pressedLocalOffset) : _visualInitialLocalPosition);
				KillVisualTween();
				_visualTween = _visualRoot.DOLocalMove(endValue, _pressAnimDuration).SetEase(_pressAnimEase).SetLink(base.gameObject);
			}
		}

		private void ApplyVisualStateImmediate(bool isPressed)
		{
			if (!(_visualRoot == null))
			{
				CacheVisualPose();
				KillVisualTween();
				_visualRoot.localPosition = (isPressed ? (_visualInitialLocalPosition + _pressedLocalOffset) : _visualInitialLocalPosition);
			}
		}

		private void PlayFeedback(bool isPressed)
		{
			if (isPressed)
			{
				if (_pressedVfx != null)
				{
					_pressedVfx.Play();
				}
				PlayClip(_pressedSfx);
			}
			else
			{
				if (_releasedVfx != null)
				{
					_releasedVfx.Play();
				}
				PlayClip(_releasedSfx);
			}
		}

		private void PlayClip(AudioClip clip)
		{
			if (!(_audioSource == null) && !(clip == null))
			{
				_audioSource.PlayOneShot(clip, _sfxVolume);
			}
		}

		private void KillVisualTween()
		{
			if (_visualTween != null)
			{
				if (_visualTween.IsActive())
				{
					_visualTween.Kill();
				}
				_visualTween = null;
			}
		}

		public void SetHandler(PressureTileHandler handler)
		{
			if (base.IsServer)
			{
				_handler = handler;
			}
		}

		public void ForceLockPressedResolvedState()
		{
			if (base.IsServer && !_isLockedPressed.Value)
			{
				_occupants.Clear();
				_isLockedPressed.Value = true;
				_isActive.Value = true;
				Collider component = GetComponent<Collider>();
				if (component != null)
				{
					component.enabled = false;
				}
				if (_logDebug)
				{
					UtilityMethods.Log("<b><color=#FFD166>[PressureTile]</color></b> Tile locked resolved: <b>" + base.name + "</b>");
				}
			}
		}

		private void ForceRefreshActiveState()
		{
			if (base.IsServer)
			{
				RefreshActiveState();
			}
		}

		private void PreviewPressed()
		{
			ApplyVisualStateImmediate(isPressed: true);
		}

		private void PreviewReleased()
		{
			ApplyVisualStateImmediate(isPressed: false);
		}

		protected override void __initializeVariables()
		{
			if (_isActive == null)
			{
				throw new Exception("PressureTile._isActive cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isActive.Initialize(this);
			__nameNetworkVariable(_isActive, "_isActive");
			NetworkVariableFields.Add(_isActive);
			if (_isLockedPressed == null)
			{
				throw new Exception("PressureTile._isLockedPressed cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isLockedPressed.Initialize(this);
			__nameNetworkVariable(_isLockedPressed, "_isLockedPressed");
			NetworkVariableFields.Add(_isLockedPressed);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "PressureTile";
		}
	}
}
