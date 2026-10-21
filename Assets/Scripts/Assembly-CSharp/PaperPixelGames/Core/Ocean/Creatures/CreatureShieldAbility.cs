using System;
using System.Collections;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Ocean.Creatures
{
	public sealed class CreatureShieldAbility : NetworkBehaviour
	{
		[Header("Shield")]
		[SerializeField]
		private bool _startShielded = true;

		[SerializeField]
		[Min(0f)]
		private float _resetCooldown = 5f;

		[Header("Visual")]
		[SerializeField]
		private GameObject _shieldVisualRoot;

		[Header("Debug")]
		[SerializeField]
		private bool _log;

		private readonly NetworkVariable<bool> _isShieldActive = new NetworkVariable<bool>(value: true);

		private Coroutine _resetCoroutine;

		public bool IsShieldActive => _isShieldActive.Value;

		public float ResetCooldown => _resetCooldown;

		public event Action<bool> ShieldChanged;

		public event Action ShieldBroken;

		public event Action ShieldReset;

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<bool> isShieldActive = _isShieldActive;
			isShieldActive.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isShieldActive.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnShieldValueChanged));
			if (base.IsServer)
			{
				_isShieldActive.Value = _startShielded;
			}
			ApplyShieldVisual(_isShieldActive.Value);
		}

		public override void OnNetworkDespawn()
		{
			NetworkVariable<bool> isShieldActive = _isShieldActive;
			isShieldActive.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isShieldActive.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnShieldValueChanged));
			StopResetRoutine();
			base.OnNetworkDespawn();
		}

		public bool CanBlockHit()
		{
			return _isShieldActive.Value;
		}

		public bool TryBreakShield(bool autoReset = true)
		{
			if (!base.IsServer)
			{
				Log("TryBreakShield ignored. Server only.", LogType.Warning);
				return false;
			}
			if (!_isShieldActive.Value)
			{
				return false;
			}
			_isShieldActive.Value = false;
			ShieldBroken?.Invoke();
			if (autoReset)
			{
				ScheduleReset();
			}
			Log("Shield broken.");
			return true;
		}

		public void ForceSetShield(bool active)
		{
			if (!base.IsServer)
			{
				Log("ForceSetShield ignored. Server only.", LogType.Warning);
				return;
			}
			StopResetRoutine();
			_isShieldActive.Value = active;
			Log($"Shield force set: {active}");
		}

		public void ScheduleReset()
		{
			if (base.IsServer)
			{
				StopResetRoutine();
				if (_resetCooldown <= 0f)
				{
					ResetShield();
				}
				else
				{
					_resetCoroutine = StartCoroutine(ResetAfterCooldownCoroutine());
				}
			}
		}

		public void ResetShield()
		{
			if (!base.IsServer)
			{
				Log("ResetShield ignored. Server only.", LogType.Warning);
				return;
			}
			StopResetRoutine();
			if (!_isShieldActive.Value)
			{
				_isShieldActive.Value = true;
				ShieldReset?.Invoke();
				Log("Shield reset.");
			}
		}

		private IEnumerator ResetAfterCooldownCoroutine()
		{
			yield return new WaitForSeconds(_resetCooldown);
			_resetCoroutine = null;
			if (base.IsServer)
			{
				ResetShield();
			}
		}

		private void StopResetRoutine()
		{
			if (_resetCoroutine != null)
			{
				StopCoroutine(_resetCoroutine);
				_resetCoroutine = null;
			}
		}

		private void OnShieldValueChanged(bool previous, bool current)
		{
			ApplyShieldVisual(current);
			ShieldChanged?.Invoke(current);
			Log($"Shield changed: {previous} -> {current}");
		}

		private void ApplyShieldVisual(bool active)
		{
			if (!(_shieldVisualRoot == null) && _shieldVisualRoot.activeSelf != active)
			{
				_shieldVisualRoot.SetActive(active);
			}
		}

		private void Log(string message, LogType type = LogType.Log)
		{
			if (_log)
			{
				UtilityMethods.Log("[CreatureShieldAbility] " + message, type);
			}
		}

		protected override void __initializeVariables()
		{
			if (_isShieldActive == null)
			{
				throw new Exception("CreatureShieldAbility._isShieldActive cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isShieldActive.Initialize(this);
			__nameNetworkVariable(_isShieldActive, "_isShieldActive");
			NetworkVariableFields.Add(_isShieldActive);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "CreatureShieldAbility";
		}
	}
}
