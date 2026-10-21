using System;
using System.Collections;
using DG.Tweening;
using Deform;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking.Chat;
using Unity.Netcode;
using UnityEngine;

public class JellyfishTrap : NetworkBehaviour, IPlayerTrapRecoveryOwner
{
	[SerializeField]
	private GameObject _jellyFishGraphic;

	[SerializeField]
	private GameObject _jellyFishGraphicPrefab;

	[Header("Settings")]
	[SerializeField]
	private float _disableDuration = 5f;

	[SerializeField]
	private byte _consumeOxygenAmount = 5;

	[SerializeField]
	private float _deactiveDuration = 15f;

	[SerializeField]
	private float _deadUpMovement = 2f;

	[SerializeField]
	private float _tweenAnimDuration = 2f;

	[SerializeField]
	private float _fpsModelOffSet = 0.2f;

	[Header("SFX")]
	[SerializeField]
	private AudioSource _audioSource;

	[SerializeField]
	private AudioSource _audioSourceLoop;

	[SerializeField]
	private AudioClip _deadSfx;

	[SerializeField]
	private AudioClip _enterSfx;

	[SerializeField]
	private AudioClip _exitSfx;

	[Header("Deformers")]
	[SerializeField]
	private RippleDeformer _upAndDownDeformer;

	[SerializeField]
	private RippleDeformer _rightAndLeftDeformer;

	private NetworkVariable<bool> _isTriggered = new NetworkVariable<bool>(value: false);

	private NetworkVariable<bool> _canBeKilledByHarpoon = new NetworkVariable<bool>(value: false);

	private PPG_Player _trappedPlayer;

	private GameObject _fpsJellyFishGraphic;

	private GameObject _tpsJellyFishGraphic;

	private Coroutine _serverTrapRoutine;

	private ulong _serverTrappedClientId = ulong.MaxValue;

	public bool CanBeKilledByHarpoon => _canBeKilledByHarpoon.Value;

	public void ServerSetCanBeKilledByHarpoon(bool canBeKilled)
	{
		if (base.IsServer)
		{
			_canBeKilledByHarpoon.Value = canBeKilled;
		}
	}

	public void ServerReceiveHarpoonHit(PPG_Player player)
	{
		if (base.IsServer && CanBeKilledByHarpoon)
		{
			ServerResetTrapRuntime();
			Singleton<SeaCreatureManager>.Instance?.UnregisterJellyfishTrap(this);
			if (base.NetworkObject != null && base.NetworkObject.IsSpawned)
			{
				base.NetworkObject.Despawn();
			}
		}
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();
		Singleton<DelayHelper>.Instance.DelayAction(UnityEngine.Random.Range(0, 10), () =>
		{
			_upAndDownDeformer.update = true;
			_rightAndLeftDeformer.update = true;
		});
		if (base.IsServer && base.NetworkManager != null)
		{
			base.NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
		}
	}

	public override void OnNetworkDespawn()
	{
		if (base.IsServer)
		{
			ServerSetTrappedState(_serverTrappedClientId, isTrapped: false);
		}
		if (base.IsServer && base.NetworkManager != null)
		{
			base.NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
		}
		StopServerTrapRoutine();
		ClearLocalTrapRuntime(showWorldGraphic: true);
		base.OnNetworkDespawn();
	}

	private void OnTriggerEnter(Collider other)
	{
		if (!_isTriggered.Value && other.CompareTag("Player") && !Singleton<PlayersManager>.Instance.LocalPlayer.IsTrapped)
		{
			if (!other.TryGetComponent<PPG_Player>(out _trappedPlayer) || !_trappedPlayer.IsLocalPlayer)
			{
				UtilityMethods.Log("[JellyfishTrap] OnTriggerEnter - not a player or not a local player");
			}
			else
			{
				ServerTriggeredRpc(_trappedPlayer.OwnerClientId);
			}
		}
	}

	[Rpc(SendTo.Server)]
	private void ServerTriggeredRpc(ulong id)
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
			FastBufferWriter bufferWriter = __beginSendRpc(3395371814u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			BytePacker.WriteValueBitPacked(bufferWriter, id);
			__endSendRpc(ref bufferWriter, 3395371814u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		if (!_isTriggered.Value)
		{
			_serverTrappedClientId = id;
			_isTriggered.Value = true;
			if (Singleton<PlayersManager>.Instance != null && Singleton<PlayersManager>.Instance.TryGetPlayer(id, out var player))
			{
				player.IsTrapped = true;
				player.ServerSetTrapRecoveryOwner(this);
				GlobalChatManager.BroadcastPlayerTrappedFromServer(this, player, "chat.trap.jellyfish", "a jellyfish trap");
			}
			if (base.OwnerClientId != id)
			{
				base.NetworkObject.ChangeOwnership(id);
			}
			ClientStartTrapRpc(id);
			StartServerTrapTimer();
		}
	}

	[Rpc(SendTo.Everyone)]
	private void ClientStartTrapRpc(ulong trappedClientId)
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
			FastBufferWriter bufferWriter = __beginSendRpc(1456719119u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			BytePacker.WriteValueBitPacked(bufferWriter, trappedClientId);
			__endSendRpc(ref bufferWriter, 1456719119u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		Singleton<PlayersManager>.Instance.TryGetPlayer(trappedClientId, out var player);
		if (player == null)
		{
			UtilityMethods.Log("[JellyfishTrap] ClientAnimationsEffectsAndSoundsPlayRpc - trappedPlayer is null!", LogType.Error);
			return;
		}
		EventBus<JellyFishEvent>.Raise(new JellyFishEvent
		{
			Status = true,
			Player = player
		});
		if (player.IsLocalPlayer)
		{
			_trappedPlayer = player;
			_trappedPlayer.SetJellyfishTrapScreenVfx(active: true);
			_fpsJellyFishGraphic = UnityEngine.Object.Instantiate(_jellyFishGraphicPrefab, _trappedPlayer.CameraParent.transform);
			_fpsJellyFishGraphic.transform.localPosition = Vector3.zero;
			_fpsJellyFishGraphic.transform.localPosition += Vector3.down * _fpsModelOffSet;
			UtilityMethods.SetLayerRecursive(_fpsJellyFishGraphic, "ViewModel", includeChildren: true);
			_trappedPlayer.IsTrapped = true;
			if (_trappedPlayer.IsToolActive)
			{
				_trappedPlayer.DisableActiveTool();
			}
			Singleton<QuickSlotHandler>.Instance?.SuspendCurrentSelectionForMode(HeldSlotSuspensionReason.JellyfishTrap);
			_trappedPlayer.ReverseMovement();
			_trappedPlayer.DivingEquipment.ConsumeOxygen(_consumeOxygenAmount, withFeedback: true);
			_audioSource.PlayOneShot(_enterSfx, 2f);
			_audioSourceLoop.Play(2uL);
			_jellyFishGraphic.SetActive(value: false);
		}
		else
		{
			_tpsJellyFishGraphic = UnityEngine.Object.Instantiate(_jellyFishGraphicPrefab, player.DivingEquipment.TpsHead.transform);
			_tpsJellyFishGraphic.transform.localPosition = Vector3.zero;
			_jellyFishGraphic.SetActive(value: false);
			_audioSource.PlayOneShot(_enterSfx);
		}
	}

	private void StartServerTrapTimer()
	{
		if (base.IsServer)
		{
			StopServerTrapRoutine();
			_serverTrapRoutine = StartCoroutine(ServerTrapTimerCoroutine());
		}
	}

	private IEnumerator ServerTrapTimerCoroutine()
	{
		yield return new WaitForSeconds(_disableDuration);
		if (_isTriggered.Value && _serverTrappedClientId != ulong.MaxValue)
		{
			ServerSetTrappedState(_serverTrappedClientId, isTrapped: false);
			ClientStopTrapRpc(_serverTrappedClientId);
			yield return new WaitForSeconds(_deactiveDuration);
			ServerCompleteTrapCooldown();
		}
	}

	[Rpc(SendTo.Everyone)]
	private void ClientStopTrapRpc(ulong trappedClientId)
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
			FastBufferWriter bufferWriter = __beginSendRpc(499649954u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			BytePacker.WriteValueBitPacked(bufferWriter, trappedClientId);
			__endSendRpc(ref bufferWriter, 499649954u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		Singleton<PlayersManager>.Instance.TryGetPlayer(trappedClientId, out var player);
		if (player == null)
		{
			return;
		}
		EventBus<JellyFishEvent>.Raise(new JellyFishEvent
		{
			Status = false,
			Player = player
		});
		if (player.IsLocalPlayer)
		{
			ReleaseLocalTrappedPlayer();
			_audioSourceLoop.Stop();
			_audioSource.PlayOneShot(_exitSfx);
			if (_fpsJellyFishGraphic != null)
			{
				GameObject graphic = _fpsJellyFishGraphic;
				_fpsJellyFishGraphic = null;
				graphic.transform.DOBlendableMoveBy(Vector3.up * _deadUpMovement, _tweenAnimDuration);
				graphic.transform.DOScale(Vector3.zero, _tweenAnimDuration).OnComplete(() =>
				{
					_audioSource.PlayOneShot(_deadSfx);
					DestroyGraphicObject(graphic);
				});
			}
			return;
		}
		_audioSource.PlayOneShot(_exitSfx);
		if (_tpsJellyFishGraphic != null)
		{
			GameObject graphic2 = _tpsJellyFishGraphic;
			_tpsJellyFishGraphic = null;
			graphic2.transform.DOBlendableMoveBy(Vector3.up * _deadUpMovement, _tweenAnimDuration);
			graphic2.transform.DOScale(Vector3.zero, _tweenAnimDuration).OnComplete(() =>
			{
				_audioSource.PlayOneShot(_deadSfx);
				DestroyGraphicObject(graphic2);
			});
		}
	}

	private void ServerCompleteTrapCooldown()
	{
		if (base.IsServer)
		{
			_serverTrapRoutine = null;
			_isTriggered.Value = false;
			_serverTrappedClientId = ulong.MaxValue;
			RemovePlayerOwnership();
			ClientResetTrapRpc();
		}
	}

	[Rpc(SendTo.Everyone)]
	private void ClientResetTrapRpc()
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
			FastBufferWriter bufferWriter = __beginSendRpc(4192741911u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 4192741911u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			_jellyFishGraphic.SetActive(value: true);
		}
	}

	public void ServerEndDayReset()
	{
		if (base.IsServer)
		{
			ServerResetTrapRuntime();
		}
	}

	public bool ServerTryReleasePlayerForRecovery(PPG_Player player, string reason)
	{
		if (!base.IsServer || player == null || player.OwnerClientId != _serverTrappedClientId)
		{
			return false;
		}
		UtilityMethods.Log($"[JellyfishTrap] Player released by recovery. ClientId={player.OwnerClientId} Reason={reason}");
		ServerResetTrapRuntime();
		return true;
	}

	private void ServerResetTrapRuntime()
	{
		ulong serverTrappedClientId = _serverTrappedClientId;
		StopServerTrapRoutine();
		_isTriggered.Value = false;
		_serverTrappedClientId = ulong.MaxValue;
		ServerSetTrappedState(serverTrappedClientId, isTrapped: false);
		ClientForceResetTrapRpc(serverTrappedClientId);
		RemovePlayerOwnership();
	}

	private void ServerSetTrappedState(ulong clientId, bool isTrapped)
	{
		if (base.IsServer && clientId != ulong.MaxValue && !(Singleton<PlayersManager>.Instance == null) && Singleton<PlayersManager>.Instance.TryGetPlayer(clientId, out var player) && !(player == null))
		{
			player.IsTrapped = isTrapped;
			if (!isTrapped)
			{
				player.ServerClearTrapRecoveryOwner(this);
			}
		}
	}

	private void RemovePlayerOwnership()
	{
		if (base.NetworkObject != null && base.NetworkObject.IsSpawned && base.OwnerClientId != 0L)
		{
			base.NetworkObject.RemoveOwnership();
		}
	}

	private void StopServerTrapRoutine()
	{
		if (_serverTrapRoutine != null)
		{
			StopCoroutine(_serverTrapRoutine);
			_serverTrapRoutine = null;
		}
	}

	private void OnClientDisconnected(ulong clientId)
	{
		if (base.IsServer && clientId == _serverTrappedClientId)
		{
			ServerResetTrapRuntime();
		}
	}

	[Rpc(SendTo.Everyone)]
	private void ClientForceResetTrapRpc(ulong trappedClientId)
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
			FastBufferWriter bufferWriter = __beginSendRpc(3401257303u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			BytePacker.WriteValueBitPacked(bufferWriter, trappedClientId);
			__endSendRpc(ref bufferWriter, 3401257303u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (Singleton<PlayersManager>.Instance != null && Singleton<PlayersManager>.Instance.TryGetPlayer(trappedClientId, out var player) && player != null)
			{
				EventBus<JellyFishEvent>.Raise(new JellyFishEvent
				{
					Status = false,
					Player = player
				});
			}
			ClearLocalTrapRuntime(showWorldGraphic: true);
		}
	}

	private void ClearLocalTrapRuntime(bool showWorldGraphic)
	{
		ReleaseLocalTrappedPlayer();
		_audioSourceLoop.Stop();
		DestroyGraphic(ref _fpsJellyFishGraphic);
		DestroyGraphic(ref _tpsJellyFishGraphic);
		if (showWorldGraphic && _jellyFishGraphic != null)
		{
			_jellyFishGraphic.SetActive(value: true);
		}
	}

	private void ReleaseLocalTrappedPlayer()
	{
		if (!(_trappedPlayer == null))
		{
			_trappedPlayer.SetJellyfishTrapScreenVfx(active: false);
			_trappedPlayer.IsTrapped = false;
			_trappedPlayer.RestoreMovement();
			Singleton<QuickSlotHandler>.Instance?.ResumeCurrentSelectionForMode(HeldSlotSuspensionReason.JellyfishTrap);
			_trappedPlayer = null;
		}
	}

	private static void DestroyGraphic(ref GameObject graphic)
	{
		if (!(graphic == null))
		{
			DestroyGraphicObject(graphic);
			graphic = null;
		}
	}

	private static void DestroyGraphicObject(GameObject graphic)
	{
		if (!(graphic == null))
		{
			graphic.transform.DOKill();
			UnregisterDeformables(graphic);
			UnityEngine.Object.Destroy(graphic);
			DeformableManagerResetUtility.PruneInvalidDefaultManagerDeformables();
		}
	}

	private static void UnregisterDeformables(GameObject graphic)
	{
		if (graphic == null)
		{
			return;
		}
		Deformable[] componentsInChildren = graphic.GetComponentsInChildren<Deformable>(includeInactive: true);
		foreach (Deformable deformable in componentsInChildren)
		{
			if (!(deformable == null))
			{
				try
				{
					deformable.Manager = null;
					deformable.enabled = false;
				}
				catch
				{
				}
			}
		}
	}

	protected override void __initializeVariables()
	{
		if (_isTriggered == null)
		{
			throw new Exception("JellyfishTrap._isTriggered cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		_isTriggered.Initialize(this);
		__nameNetworkVariable(_isTriggered, "_isTriggered");
		NetworkVariableFields.Add(_isTriggered);
		if (_canBeKilledByHarpoon == null)
		{
			throw new Exception("JellyfishTrap._canBeKilledByHarpoon cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		_canBeKilledByHarpoon.Initialize(this);
		__nameNetworkVariable(_canBeKilledByHarpoon, "_canBeKilledByHarpoon");
		NetworkVariableFields.Add(_canBeKilledByHarpoon);
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		__registerRpc(3395371814u, __rpc_handler_3395371814, "ServerTriggeredRpc", RpcInvokePermission.Everyone);
		__registerRpc(1456719119u, __rpc_handler_1456719119, "ClientStartTrapRpc", RpcInvokePermission.Everyone);
		__registerRpc(499649954u, __rpc_handler_499649954, "ClientStopTrapRpc", RpcInvokePermission.Everyone);
		__registerRpc(4192741911u, __rpc_handler_4192741911, "ClientResetTrapRpc", RpcInvokePermission.Everyone);
		__registerRpc(3401257303u, __rpc_handler_3401257303, "ClientForceResetTrapRpc", RpcInvokePermission.Everyone);
		base.__initializeRpcs();
	}

	private static void __rpc_handler_3395371814(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
			((JellyfishTrap)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((JellyfishTrap)target).ServerTriggeredRpc(value);
			((JellyfishTrap)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1456719119(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
			((JellyfishTrap)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((JellyfishTrap)target).ClientStartTrapRpc(value);
			((JellyfishTrap)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_499649954(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
			((JellyfishTrap)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((JellyfishTrap)target).ClientStopTrapRpc(value);
			((JellyfishTrap)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_4192741911(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((JellyfishTrap)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((JellyfishTrap)target).ClientResetTrapRpc();
			((JellyfishTrap)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3401257303(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
			((JellyfishTrap)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((JellyfishTrap)target).ClientForceResetTrapRpc(value);
			((JellyfishTrap)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	protected override string __getTypeName()
	{
		return "JellyfishTrap";
	}
}
