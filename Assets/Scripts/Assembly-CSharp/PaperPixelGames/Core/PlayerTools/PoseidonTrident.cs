using System;
using System.Collections;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Data.Tools;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Wheel;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PaperPixelGames.Core.PlayerTools
{
	public sealed class PoseidonTrident : BaseTool
	{
		private const float ProjectileSpawnClearance = 0.05f;

		private readonly RaycastHit[] _spawnHitBuffer = new RaycastHit[32];

		[SerializeField]
		private PoseidonTridentData _data;

		[Header("Charge UI")]
		[SerializeField]
		private GameObject _chargeUiRoot;

		[SerializeField]
		private Image _chargeFillImage;

		private readonly NetworkVariable<PoseidonTridentState> _state = new NetworkVariable<PoseidonTridentState>(PoseidonTridentState.Held);

		private readonly NetworkVariable<double> _serverChargeStartedAt = new NetworkVariable<double>(0.0);

		private PoseidonTridentProjectile _activeProjectile;

		private Coroutine _delayedThrowRoutine;

		private PoseidonTridentChargeKind _localChargeKind;

		private float _localChargeStartedAt;

		private float _nextDashTime;

		private bool _lastUnderwaterState;

		private Vector3 _fpsRestPosition;

		private Vector3 _tpsRestPosition;

		private Quaternion _fpsRestRotation;

		private Quaternion _tpsRestRotation;

		private Vector3 _fpsRestScale;

		private Vector3 _tpsRestScale;

		private bool _hasFpsRestPosition;

		private bool _hasTpsRestPosition;

		private float _cachedSwimSpeed;

		private bool _hasSwimSpeedOverride;

		public override byte Level => 0;

		public override bool IsUnlocked
		{
			get
			{
				if (_player != null)
				{
					return _player.ToolsData.PoseidonTridentUnlocked;
				}
				return false;
			}
		}

		public override string Name
		{
			get
			{
				if (!(_data != null))
				{
					return "Poseidon's Trident";
				}
				return _data.Name;
			}
		}

		public override Sprite Icon
		{
			get
			{
				if (!(_data != null))
				{
					return null;
				}
				return _data.Sprite;
			}
		}

		public override bool CapturesInteractInput => IsProjectileActive;

		public bool ProvidesOxygenProtection => base.ToolStatus;

		public PoseidonTridentState State => _state.Value;

		public bool IsProjectileActive => IsProjectileState(_state.Value);

		private void Start()
		{
			HideChargeUI();
			if (_tpsGraphicParent != null)
			{
				_tpsGraphicParent.gameObject.SetActive(value: false);
			}
			if (_fpsGraphicParent != null)
			{
				_fpsGraphicParent.gameObject.SetActive(value: false);
			}
			if (!(_player == null))
			{
				if (_tpsGraphicParent != null && _player.RigBones?.TpsRig?.RightHand != null)
				{
					_tpsGraphicParent.SetParent(_player.RigBones.TpsRig.RightHand);
					_tpsGraphicParent.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
				}
				if (base.IsOwner && _fpsGraphicParent != null && _player.RigBones?.FpsRig?.RightHand != null)
				{
					_fpsGraphicParent.SetParent(_player.RigBones.FpsRig.RightHand);
					_fpsGraphicParent.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
				}
				_lastUnderwaterState = _player.IsUnderWater;
				if (base.IsOwner)
				{
					ApplySwimSpeedOverride(base.ToolStatus);
				}
				ApplyAnimatorState(_state.Value);
			}
		}

		private void Update()
		{
			UpdateChargeFeedback();
			if (base.IsOwner && !(_player == null) && base.ToolStatus && _lastUnderwaterState != _player.IsUnderWater)
			{
				_lastUnderwaterState = _player.IsUnderWater;
				if (_localChargeKind == PoseidonTridentChargeKind.Dash && !_lastUnderwaterState)
				{
					CancelLocalCharge(notifyServer: true);
				}
				RefreshActionHints();
			}
		}

		protected override void NetworkSpawn()
		{
			NetworkVariable<PoseidonTridentState> state = _state;
			state.OnValueChanged = (NetworkVariable<PoseidonTridentState>.OnValueChangedDelegate)Delegate.Combine(state.OnValueChanged, new NetworkVariable<PoseidonTridentState>.OnValueChangedDelegate(OnStateChanged));
			ApplyStateVisuals(_state.Value);
			ApplyAnimatorState(_state.Value);
		}

		protected override void NetworkDespawn()
		{
			NetworkVariable<PoseidonTridentState> state = _state;
			state.OnValueChanged = (NetworkVariable<PoseidonTridentState>.OnValueChangedDelegate)Delegate.Remove(state.OnValueChanged, new NetworkVariable<PoseidonTridentState>.OnValueChangedDelegate(OnStateChanged));
			CancelLocalCharge(notifyServer: false);
			HideChargeUI();
			ApplySwimSpeedOverride(active: false);
			ClearAnimatorState();
			if (base.NetworkManager != null && base.NetworkManager.IsServer)
			{
				ServerCleanupOnDespawn();
			}
		}

		protected override void ApplyToolStatus(bool status)
		{
			base.ApplyToolStatus(status);
			ApplySwimSpeedOverride(status);
			if (!status)
			{
				CancelLocalCharge(notifyServer: false);
				HideChargeUI();
				_nextDashTime = 0f;
				if (base.IsServer)
				{
					ServerResetRuntime();
				}
			}
			ApplyStateVisuals(_state.Value);
			ApplyAnimatorState(_state.Value);
			if (!status)
			{
				_player?.AnimResetTrigger(PPG_Player.TRIDENT_THROW);
			}
		}

		protected override void SpawnGraphics()
		{
			base.SpawnGraphics();
			if (_data != null)
			{
				ApplyHeldGraphicOffset(_tpsGraphic, _data.OffsetTps);
				if (base.IsOwner)
				{
					ApplyHeldGraphicOffset(_fpsGraphic, _data.OffsetFps);
				}
			}
			CacheGraphicRestPositions();
		}

		public override void Mouse1(InputActionPhase phase)
		{
			if (!base.IsOwner || !base.ToolStatus || !IsUnlocked)
			{
				return;
			}
			switch (phase)
			{
			case InputActionPhase.Started:
				BeginLocalCharge(PoseidonTridentChargeKind.Throw);
				break;
			case InputActionPhase.Canceled:
				if (_localChargeKind == PoseidonTridentChargeKind.Throw)
				{
					ReleaseThrow();
				}
				break;
			}
		}

		public override void Mouse2(InputActionPhase phase)
		{
			if (!base.IsOwner || !base.ToolStatus || !IsUnlocked)
			{
				return;
			}
			switch (phase)
			{
			case InputActionPhase.Started:
				BeginLocalCharge(PoseidonTridentChargeKind.Dash);
				break;
			case InputActionPhase.Canceled:
				if (_localChargeKind == PoseidonTridentChargeKind.Dash)
				{
					ReleaseDash();
				}
				break;
			}
		}

		public override void Interact(InputActionPhase phase)
		{
			if (base.IsOwner && phase == InputActionPhase.Started && IsProjectileActive)
			{
				ServerRecallProjectileRpc();
			}
		}

		public override bool DisableIf()
		{
			return true;
		}

		public override void LoadLevel(ToolDataNet toolData)
		{
			ApplyLevel(0);
			if (base.IsOwner)
			{
				SetUnlock(toolData.PoseidonTridentUnlocked);
			}
		}

		public Vector3 GetServerReturnTargetPosition()
		{
			if (_player != null && _player.RigBones?.TpsRig?.RightHand != null)
			{
				return _player.RigBones.TpsRig.RightHand.position;
			}
			if (!(_player != null))
			{
				return base.transform.position;
			}
			return _player.transform.position + Vector3.up;
		}

		public void ServerSetProjectileState(PoseidonTridentProjectile projectile, PoseidonTridentState state)
		{
			if (base.IsServer && !(projectile == null) && !(projectile != _activeProjectile))
			{
				_state.Value = state;
			}
		}

		public void ServerProjectileReturned(PoseidonTridentProjectile projectile)
		{
			if (base.IsServer && !(projectile == null) && !(projectile != _activeProjectile))
			{
				_activeProjectile = null;
				_state.Value = PoseidonTridentState.Held;
				if (projectile.NetworkObject != null && projectile.NetworkObject.IsSpawned)
				{
					projectile.NetworkObject.Despawn();
				}
			}
		}

		public void ServerProjectileDespawned(PoseidonTridentProjectile projectile)
		{
			if (base.IsServer && !(projectile == null) && !(projectile != _activeProjectile))
			{
				_activeProjectile = null;
				_state.Value = PoseidonTridentState.Held;
			}
		}

		protected override GameObject GetGraphicByLevel()
		{
			if (!(_data != null))
			{
				return null;
			}
			return _data.GraphicPrefab;
		}

		protected override int GetLevelCount()
		{
			return 1;
		}

		protected override void SetUnlock(bool newValue)
		{
			if (Singleton<PPG_WheelManager>.Instance != null)
			{
				Singleton<PPG_WheelManager>.Instance.UnlockPoseidonTrident(newValue);
			}
		}

		protected override void EnableHints()
		{
			RefreshActionHints();
		}

		protected override void DisableHints()
		{
			ClearActionHints();
		}

		private void BeginLocalCharge(PoseidonTridentChargeKind kind)
		{
			if (_localChargeKind == PoseidonTridentChargeKind.None && _state.Value == PoseidonTridentState.Held && (kind != PoseidonTridentChargeKind.Dash || (_player.IsUnderWater && !(Time.time < _nextDashTime))))
			{
				_localChargeKind = kind;
				_localChargeStartedAt = Time.time;
				ShowChargeUI();
				RefreshActionHints();
				ServerBeginChargeRpc(kind);
			}
		}

		private void ReleaseThrow()
		{
			float localCharge = GetLocalCharge01();
			Vector3 direction = ((_player.CameraTransform != null) ? _player.CameraTransform.forward : _player.transform.forward);
			FinishLocalCharge();
			HideHeldGraphicsLocally();
			ServerReleaseThrowRpc(direction, localCharge);
		}

		private void ReleaseDash()
		{
			if (!_player.IsUnderWater || _state.Value == PoseidonTridentState.Returning)
			{
				CancelLocalCharge(notifyServer: true);
				return;
			}
			float localCharge = GetLocalCharge01();
			float impulse = Mathf.Lerp(GetMinDashImpulse(), GetMaxDashImpulse(), localCharge);
			Vector3 direction = ((_player.CameraTransform != null) ? _player.CameraTransform.forward : _player.transform.forward);
			FinishLocalCharge();
			_player.FirstPersonCharacter?.ImpulseInDirection(direction, impulse);
			_nextDashTime = Time.time + GetDashCooldown();
			ServerFinishDashChargeRpc();
		}

		private void FinishLocalCharge()
		{
			_localChargeKind = PoseidonTridentChargeKind.None;
			_localChargeStartedAt = 0f;
			HideChargeUI();
			RestoreGraphicPositions();
		}

		private void CancelLocalCharge(bool notifyServer)
		{
			if (_localChargeKind != PoseidonTridentChargeKind.None)
			{
				FinishLocalCharge();
				if (notifyServer && base.IsSpawned && base.IsOwner)
				{
					ServerCancelChargeRpc();
				}
			}
		}

		private float GetLocalCharge01()
		{
			float num = ((_data != null) ? Mathf.Max(0.01f, _data.ChargeDuration) : 1f);
			return Mathf.Clamp01((Time.time - _localChargeStartedAt) / num);
		}

		private float GetServerCharge01()
		{
			float num = ((_data != null) ? Mathf.Max(0.01f, _data.ChargeDuration) : 1f);
			return Mathf.Clamp01((float)((base.NetworkManager.ServerTime.Time - _serverChargeStartedAt.Value) / (double)num));
		}

		[Rpc(SendTo.Server)]
		private void ServerBeginChargeRpc(PoseidonTridentChargeKind kind, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(345058579u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in kind, default(FastBufferWriter.ForEnums));
				__endSendRpc(ref bufferWriter, 345058579u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsServer || rpcParams.Receive.SenderClientId != base.OwnerClientId || !base.ToolStatus || !IsUnlocked || _state.Value != PoseidonTridentState.Held)
			{
				return;
			}
			switch (kind)
			{
			case PoseidonTridentChargeKind.None:
				return;
			case PoseidonTridentChargeKind.Dash:
				if (!_player.IsUnderWater)
				{
					return;
				}
				break;
			}
			_serverChargeStartedAt.Value = base.NetworkManager.ServerTime.Time;
			_state.Value = ((kind == PoseidonTridentChargeKind.Throw) ? PoseidonTridentState.ChargingThrow : PoseidonTridentState.ChargingDash);
		}

		[Rpc(SendTo.Server)]
		private void ServerReleaseThrowRpc(Vector3 direction, float clientCharge, RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1557472562u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in direction);
				bufferWriter.WriteValueSafe(in clientCharge, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1557472562u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (base.IsServer && rpcParams.Receive.SenderClientId == base.OwnerClientId)
			{
				if (!base.ToolStatus || _state.Value != PoseidonTridentState.ChargingThrow || !float.IsFinite(clientCharge) || !IsFinite(direction) || direction.sqrMagnitude < 0.0001f)
				{
					ServerCancelCharge();
					return;
				}
				float charge = Mathf.Min(GetServerCharge01(), Mathf.Clamp01(clientCharge) + 0.1f);
				BeginDelayedThrow(direction.normalized, charge);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerFinishDashChargeRpc(RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2799840417u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2799840417u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && rpcParams.Receive.SenderClientId == base.OwnerClientId && _state.Value == PoseidonTridentState.ChargingDash)
				{
					PlayDashSfxOwnerRpc();
					_state.Value = PoseidonTridentState.Held;
					QuestReferenceHub.Instance?.CompleteIfActive(QuestId.DashWithTheTrident);
				}
			}
		}

		[Rpc(SendTo.Owner)]
		private void PlayDashSfxOwnerRpc()
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2004373304u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2004373304u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				PlayTridentSfx((_data != null) ? _data.DashSfx : null, (_data != null) ? _data.DashSfxDelay : 0f);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerCancelChargeRpc(RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(642157879u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 642157879u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && rpcParams.Receive.SenderClientId == base.OwnerClientId)
				{
					ServerCancelCharge();
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerRecallProjectileRpc(RpcParams rpcParams = default(RpcParams))
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcParams rpcParams2 = rpcParams;
				RpcAttribute.RpcAttributeParams attributeParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2669908092u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 2669908092u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && rpcParams.Receive.SenderClientId == base.OwnerClientId && base.ToolStatus && !(_activeProjectile == null))
				{
					_activeProjectile.ServerBeginReturn();
				}
			}
		}

		private void SpawnProjectile(Vector3 direction, float charge)
		{
			if (_data == null || _data.ProjectilePrefab == null || _activeProjectile != null)
			{
				_state.Value = PoseidonTridentState.Held;
				return;
			}
			Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
			Vector3 projectileSpawnPosition = GetProjectileSpawnPosition(direction, rotation);
			GameObject gameObject = UnityEngine.Object.Instantiate(_data.ProjectilePrefab, projectileSpawnPosition, rotation);
			if (!gameObject.TryGetComponent<PoseidonTridentProjectile>(out var component) || !gameObject.TryGetComponent<NetworkObject>(out var component2))
			{
				UnityEngine.Object.Destroy(gameObject);
				_state.Value = PoseidonTridentState.Held;
				return;
			}
			_activeProjectile = component;
			component2.Spawn(destroyWithScene: true);
			component.ServerInitialize(this, _player, direction, charge, _data);
			_state.Value = PoseidonTridentState.Outbound;
		}

		private Vector3 GetProjectileSpawnPosition(Vector3 direction, Quaternion rotation)
		{
			Transform transform = ((_player != null) ? _player.CameraTransform : null);
			Vector3 vector = ((transform != null) ? transform.position : ((!(_player != null)) ? GetServerReturnTargetPosition() : (_player.transform.position + Vector3.up)));
			Vector3 vector2 = vector + rotation * _data.ProjectileSpawnOffset;
			float num = Mathf.Max(0.01f, _data.CollisionRadius);
			float num2 = Vector3.Dot(vector2 - vector, direction);
			vector2 += direction * Mathf.Max(0f, num + 0.05f - num2);
			Vector3 vector3 = vector2 - vector;
			float magnitude = vector3.magnitude;
			if (magnitude <= 0.0001f)
			{
				return vector2;
			}
			Vector3 vector4 = vector3 / magnitude;
			int num3 = Physics.SphereCastNonAlloc(vector, num, vector4, _spawnHitBuffer, magnitude, _data.ProjectileHitMask, QueryTriggerInteraction.Collide);
			float num4 = magnitude;
			for (int i = 0; i < num3; i++)
			{
				Collider collider = _spawnHitBuffer[i].collider;
				if (!(collider == null) && !(collider.GetComponentInParent<PPG_Player>() == _player))
				{
					num4 = Mathf.Min(num4, _spawnHitBuffer[i].distance);
				}
			}
			if (!(num4 >= magnitude))
			{
				return vector + vector4 * Mathf.Max(0f, num4 - 0.05f);
			}
			return vector2;
		}

		private void BeginDelayedThrow(Vector3 direction, float charge)
		{
			CancelDelayedThrow();
			_state.Value = PoseidonTridentState.Throwing;
			_delayedThrowRoutine = StartCoroutine(CoSpawnProjectileAfterDelay(direction, charge));
		}

		private IEnumerator CoSpawnProjectileAfterDelay(Vector3 direction, float charge)
		{
			float num = ((_data != null) ? Mathf.Max(0f, _data.ThrowReleaseDelay) : 1f);
			if (num > 0f)
			{
				yield return new WaitForSeconds(num);
			}
			_delayedThrowRoutine = null;
			if (!base.IsServer || !base.ToolStatus || _state.Value != PoseidonTridentState.Throwing)
			{
				if (base.IsServer && _state.Value == PoseidonTridentState.Throwing)
				{
					_state.Value = PoseidonTridentState.Held;
				}
			}
			else
			{
				SpawnProjectile(direction, charge);
			}
		}

		private void CancelDelayedThrow()
		{
			if (_delayedThrowRoutine != null)
			{
				StopCoroutine(_delayedThrowRoutine);
				_delayedThrowRoutine = null;
			}
		}

		private void ServerCancelCharge()
		{
			PoseidonTridentState value = _state.Value;
			if (value == PoseidonTridentState.ChargingThrow || value == PoseidonTridentState.ChargingDash)
			{
				_state.Value = PoseidonTridentState.Held;
			}
		}

		private void ServerResetRuntime()
		{
			if (base.IsServer)
			{
				CancelDelayedThrow();
				PoseidonTridentProjectile activeProjectile = _activeProjectile;
				_activeProjectile = null;
				_state.Value = PoseidonTridentState.Held;
				if (activeProjectile != null && activeProjectile.NetworkObject != null && activeProjectile.NetworkObject.IsSpawned)
				{
					activeProjectile.NetworkObject.Despawn();
				}
			}
		}

		private void ServerCleanupOnDespawn()
		{
			CancelDelayedThrow();
			PoseidonTridentProjectile activeProjectile = _activeProjectile;
			_activeProjectile = null;
			if (activeProjectile != null && activeProjectile.NetworkObject != null && activeProjectile.NetworkObject.IsSpawned)
			{
				activeProjectile.NetworkObject.Despawn();
			}
		}

		private void OnStateChanged(PoseidonTridentState previous, PoseidonTridentState current)
		{
			ApplyStateVisuals(current);
			ApplyAnimatorState(current);
			if (current == PoseidonTridentState.Throwing && previous != PoseidonTridentState.Throwing)
			{
				PlayThrowAnimation();
				if (base.IsOwner)
				{
					PlayTridentSfx((_data != null) ? _data.ThrowSfx : null, (_data != null) ? _data.ThrowSfxDelay : 0f);
				}
			}
			if (base.IsOwner && base.ToolStatus && previous == PoseidonTridentState.Returning && current == PoseidonTridentState.Held)
			{
				PlayTridentSfx((_data != null) ? _data.ReturnSfx : null, (_data != null) ? _data.ReturnSfxDelay : 0f);
			}
			if (base.IsOwner)
			{
				RefreshActionHints();
			}
		}

		private void ApplyStateVisuals(PoseidonTridentState state)
		{
			bool flag = base.ToolStatus && state != PoseidonTridentState.Throwing && !IsProjectileState(state);
			if (state != PoseidonTridentState.ChargingThrow && state != PoseidonTridentState.ChargingDash)
			{
				RestoreGraphicPositions();
			}
			if (_fpsGraphic != null)
			{
				_fpsGraphic.SetActive(base.IsOwner & flag);
			}
			if (_tpsGraphic != null)
			{
				_tpsGraphic.SetActive(flag);
			}
		}

		private void UpdateChargeFeedback()
		{
			float num = 0f;
			bool flag = false;
			if (base.IsOwner && _localChargeKind != PoseidonTridentChargeKind.None)
			{
				num = GetLocalCharge01();
				flag = true;
				SetChargeProgress(num);
			}
			else if (!base.IsOwner && base.IsSpawned)
			{
				PoseidonTridentState value = _state.Value;
				if (value == PoseidonTridentState.ChargingThrow || value == PoseidonTridentState.ChargingDash)
				{
					num = GetServerCharge01();
					flag = true;
				}
			}
			if (flag)
			{
				ApplyChargeGraphicPose();
			}
		}

		private void ShowChargeUI()
		{
			SetChargeProgress(0f);
			if (_chargeUiRoot != null)
			{
				_chargeUiRoot.SetActive(value: true);
			}
		}

		private void SetChargeProgress(float progress)
		{
			if (_chargeFillImage != null)
			{
				_chargeFillImage.fillAmount = Mathf.Clamp01(progress);
			}
		}

		private void HideChargeUI()
		{
			SetChargeProgress(0f);
			if (_chargeUiRoot != null)
			{
				_chargeUiRoot.SetActive(value: false);
			}
		}

		private void CacheGraphicRestPositions()
		{
			_hasFpsRestPosition = false;
			_hasTpsRestPosition = false;
			if (_fpsGraphic != null)
			{
				_fpsRestPosition = _fpsGraphic.transform.localPosition;
				_fpsRestRotation = _fpsGraphic.transform.localRotation;
				_fpsRestScale = _fpsGraphic.transform.localScale;
				_hasFpsRestPosition = true;
			}
			if (_tpsGraphic != null)
			{
				_tpsRestPosition = _tpsGraphic.transform.localPosition;
				_tpsRestRotation = _tpsGraphic.transform.localRotation;
				_tpsRestScale = _tpsGraphic.transform.localScale;
				_hasTpsRestPosition = true;
			}
		}

		private static void ApplyHeldGraphicOffset(GameObject graphic, ItemOffset offset)
		{
			if (!(graphic == null) && offset != null)
			{
				graphic.transform.SetLocalPositionAndRotation(offset.PositionOffset, Quaternion.Euler(offset.RotationOffset));
			}
		}

		private void ApplyChargeGraphicPose()
		{
			bool flag = _localChargeKind == PoseidonTridentChargeKind.Dash || (!base.IsOwner && _state.Value == PoseidonTridentState.ChargingDash);
			ItemOffset offset = ((_data == null) ? null : (flag ? _data.DashOffsetFps : _data.ThrowOffsetFps));
			ItemOffset offset2 = ((_data == null) ? null : (flag ? _data.DashOffsetTps : _data.ThrowOffsetTps));
			if (_fpsGraphic != null && _hasFpsRestPosition)
			{
				ApplyGraphicOffset(_fpsGraphic.transform, _fpsRestPosition, _fpsRestRotation, _fpsRestScale, offset);
			}
			if (_tpsGraphic != null && _hasTpsRestPosition)
			{
				ApplyGraphicOffset(_tpsGraphic.transform, _tpsRestPosition, _tpsRestRotation, _tpsRestScale, offset2);
			}
		}

		private void RestoreGraphicPositions()
		{
			if (_fpsGraphic != null && _hasFpsRestPosition)
			{
				_fpsGraphic.transform.SetLocalPositionAndRotation(_fpsRestPosition, _fpsRestRotation);
			}
			if (_tpsGraphic != null && _hasTpsRestPosition)
			{
				_tpsGraphic.transform.SetLocalPositionAndRotation(_tpsRestPosition, _tpsRestRotation);
			}
			if (_fpsGraphic != null && _hasFpsRestPosition)
			{
				_fpsGraphic.transform.localScale = _fpsRestScale;
			}
			if (_tpsGraphic != null && _hasTpsRestPosition)
			{
				_tpsGraphic.transform.localScale = _tpsRestScale;
			}
		}

		private void HideHeldGraphicsLocally()
		{
			if (_fpsGraphic != null)
			{
				_fpsGraphic.SetActive(value: false);
			}
			if (_tpsGraphic != null)
			{
				_tpsGraphic.SetActive(value: false);
			}
		}

		private static void ApplyGraphicOffset(Transform graphic, Vector3 restPosition, Quaternion restRotation, Vector3 restScale, ItemOffset offset)
		{
			if (offset == null)
			{
				graphic.SetLocalPositionAndRotation(restPosition, restRotation);
				graphic.localScale = restScale;
			}
			else
			{
				graphic.SetLocalPositionAndRotation(offset.PositionOffset, Quaternion.Euler(offset.RotationOffset));
				graphic.localScale = offset.ScaleOffset;
			}
		}

		private void ApplyAnimatorState(PoseidonTridentState state)
		{
			if (!(_player == null))
			{
				bool flag = base.ToolStatus && !IsProjectileState(state);
				bool value = flag && (state == PoseidonTridentState.ChargingThrow || state == PoseidonTridentState.ChargingDash);
				_player.AnimSetBool(PPG_Player.TRIDENT, flag);
				_player.AnimSetBool(PPG_Player.TRIDENT_AIM, value);
			}
		}

		private void PlayThrowAnimation()
		{
			if (!(_player == null))
			{
				_player.AnimResetTrigger(PPG_Player.TRIDENT_THROW);
				_player.AnimSetTrigger(PPG_Player.TRIDENT_THROW);
			}
		}

		private void PlayTridentSfx(AudioClipSO sfx, float delay)
		{
			if (!(sfx == null) && !(Singleton<SoundManager>.Instance == null))
			{
				if (delay > 0f)
				{
					StartCoroutine(CoPlayTridentSfx(sfx, delay));
				}
				else
				{
					Singleton<SoundManager>.Instance.PlaySFX(sfx, GetServerReturnTargetPosition());
				}
			}
		}

		private IEnumerator CoPlayTridentSfx(AudioClipSO sfx, float delay)
		{
			yield return new WaitForSeconds(delay);
			if (sfx != null && Singleton<SoundManager>.Instance != null)
			{
				Singleton<SoundManager>.Instance.PlaySFX(sfx, GetServerReturnTargetPosition());
			}
		}

		private void ClearAnimatorState()
		{
			if (!(_player == null))
			{
				_player.AnimSetBool(PPG_Player.TRIDENT, value: false);
				_player.AnimSetBool(PPG_Player.TRIDENT_AIM, value: false);
				_player.AnimResetTrigger(PPG_Player.TRIDENT_THROW);
			}
		}

		private void ApplySwimSpeedOverride(bool active)
		{
			if (base.IsOwner && !(_player?.FirstPersonCharacter == null))
			{
				if (active && !_hasSwimSpeedOverride)
				{
					_cachedSwimSpeed = _player.FirstPersonCharacter.maxSwimSpeed;
					float value = ((_data != null) ? _data.SwimSpeedMultiplier : 0.65f);
					_player.FirstPersonCharacter.maxSwimSpeed = _cachedSwimSpeed * Mathf.Clamp(value, 0.1f, 1f);
					_hasSwimSpeedOverride = true;
				}
				else if (!active && _hasSwimSpeedOverride)
				{
					_player.FirstPersonCharacter.maxSwimSpeed = _cachedSwimSpeed;
					_cachedSwimSpeed = 0f;
					_hasSwimSpeedOverride = false;
				}
			}
		}

		private void RefreshActionHints()
		{
			if (!base.IsOwner || Singleton<HintsManager>.Instance == null)
			{
				return;
			}
			ClearActionHints();
			if (!base.ToolStatus)
			{
				return;
			}
			switch (_state.Value)
			{
			case PoseidonTridentState.Held:
			case PoseidonTridentState.ChargingThrow:
			case PoseidonTridentState.ChargingDash:
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_hold_throw");
				if (_player != null && _player.IsUnderWater)
				{
					Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_hold_dash");
				}
				break;
			case PoseidonTridentState.Outbound:
			case PoseidonTridentState.Stopped:
				Singleton<HintsManager>.Instance.AddHint("Interact", "key_recall");
				break;
			case PoseidonTridentState.Throwing:
				break;
			}
		}

		private static void ClearActionHints()
		{
			if (!(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
				Singleton<HintsManager>.Instance.RemoveHint("RightMouseMechanic");
				Singleton<HintsManager>.Instance.RemoveHint("Interact");
			}
		}

		private float GetMinDashImpulse()
		{
			if (!(_data != null))
			{
				return 3f;
			}
			return _data.MinDashImpulse;
		}

		private float GetMaxDashImpulse()
		{
			if (!(_data != null))
			{
				return 10f;
			}
			return _data.MaxDashImpulse;
		}

		private float GetDashCooldown()
		{
			if (!(_data != null))
			{
				return 1.5f;
			}
			return _data.DashCooldown;
		}

		private static bool IsProjectileState(PoseidonTridentState state)
		{
			if (state != PoseidonTridentState.Outbound && state != PoseidonTridentState.Stopped)
			{
				return state == PoseidonTridentState.Returning;
			}
			return true;
		}

		private static bool IsFinite(Vector3 value)
		{
			if (float.IsFinite(value.x) && float.IsFinite(value.y))
			{
				return float.IsFinite(value.z);
			}
			return false;
		}

		protected override void __initializeVariables()
		{
			if (_state == null)
			{
				throw new Exception("PoseidonTrident._state cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_state.Initialize(this);
			__nameNetworkVariable(_state, "_state");
			NetworkVariableFields.Add(_state);
			if (_serverChargeStartedAt == null)
			{
				throw new Exception("PoseidonTrident._serverChargeStartedAt cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_serverChargeStartedAt.Initialize(this);
			__nameNetworkVariable(_serverChargeStartedAt, "_serverChargeStartedAt");
			NetworkVariableFields.Add(_serverChargeStartedAt);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(345058579u, __rpc_handler_345058579, "ServerBeginChargeRpc", RpcInvokePermission.Everyone);
			__registerRpc(1557472562u, __rpc_handler_1557472562, "ServerReleaseThrowRpc", RpcInvokePermission.Everyone);
			__registerRpc(2799840417u, __rpc_handler_2799840417, "ServerFinishDashChargeRpc", RpcInvokePermission.Everyone);
			__registerRpc(2004373304u, __rpc_handler_2004373304, "PlayDashSfxOwnerRpc", RpcInvokePermission.Everyone);
			__registerRpc(642157879u, __rpc_handler_642157879, "ServerCancelChargeRpc", RpcInvokePermission.Everyone);
			__registerRpc(2669908092u, __rpc_handler_2669908092, "ServerRecallProjectileRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_345058579(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out PoseidonTridentChargeKind value, default(FastBufferWriter.ForEnums));
				RpcParams ext = rpcParams.Ext;
				((PoseidonTrident)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PoseidonTrident)target).ServerBeginChargeRpc(value, ext);
				((PoseidonTrident)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1557472562(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Vector3 value);
				reader.ReadValueSafe(out float value2, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((PoseidonTrident)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PoseidonTrident)target).ServerReleaseThrowRpc(value, value2, ext);
				((PoseidonTrident)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2799840417(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((PoseidonTrident)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PoseidonTrident)target).ServerFinishDashChargeRpc(ext);
				((PoseidonTrident)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2004373304(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((PoseidonTrident)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PoseidonTrident)target).PlayDashSfxOwnerRpc();
				((PoseidonTrident)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_642157879(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((PoseidonTrident)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PoseidonTrident)target).ServerCancelChargeRpc(ext);
				((PoseidonTrident)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2669908092(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((PoseidonTrident)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PoseidonTrident)target).ServerRecallProjectileRpc(ext);
				((PoseidonTrident)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "PoseidonTrident";
		}
	}
}
