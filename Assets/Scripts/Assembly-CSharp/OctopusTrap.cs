using System;
using System.Collections;
using DG.Tweening;
using EPOOutline;
using PaperPixelGames.Core;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Input;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking.Chat;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class OctopusTrap : NetworkBehaviour, IBasicInteractable, IPlayerTrapRecoveryOwner
{
	private const string HoldToEscapeLocalizationKey = "gameplay.octopus_trap.hold_to_escape";

	private const string HoldToRescueLocalizationKey = "gameplay.octopus_trap.hold_to_rescue";

	[Header("Visuals")]
	[SerializeField]
	private Transform _octopusGraph;

	[SerializeField]
	private Outlinable _octopusOutlinable;

	[SerializeField]
	private Animator _octopusAnimator;

	[SerializeField]
	private BoxCollider _octopusCollider;

	[SerializeField]
	private ParticleSystem _sandVfx;

	[SerializeField]
	private ParticleSystem _sandVfxTwo;

	[SerializeField]
	private AudioClipSO _trapSound;

	[Header("Settings")]
	[SerializeField]
	private float _requiredHoldDuration = 2f;

	[FormerlySerializedAs("escapeChance")]
	[SerializeField]
	[Range(0f, 1f)]
	private float _escapeChance = 0.05f;

	[SerializeField]
	[Range(0f, 1f)]
	private float _singlePlayerEscapeChance = 0.5f;

	[SerializeField]
	[Min(0.05f)]
	private float _damageTickInterval = 1f;

	[SerializeField]
	[Min(1f)]
	private byte _damagePerTick = 1;

	[FormerlySerializedAs("disableDuration")]
	[SerializeField]
	private float _disableDuration = 5f;

	[Header("UI Elements")]
	[SerializeField]
	private GameObject _canvas;

	[SerializeField]
	private Image _escapeProgressBarImage;

	[SerializeField]
	private TMP_Text _actionPromptText;

	private Vector3 _defaultPosition;

	private Tweener _progressTween;

	private PPG_Player _localTrappedPlayer;

	private PPG_Player _serverTrappedPlayer;

	private bool _isLocalPlayerTrapped;

	private bool _isHelpingRelease;

	private int _visualRuntimeVersion;

	private ulong _serverTrappedClientId = ulong.MaxValue;

	private Coroutine _serverDamageRoutine;

	private readonly NetworkVariable<OctopusTrapState> _currentTrapState = new NetworkVariable<OctopusTrapState>(OctopusTrapState.Active);

	private readonly NetworkVariable<ulong> _trappedPlayerNetworkId = new NetworkVariable<ulong>(0uL);

	private EventBinding<PlayerDrownedEvent> _playerDrownedBinding;

	public bool CanGaze => true;

	public bool CanInteractable => true;

	private void Awake()
	{
		CacheOctopusOutlinable();
	}

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();
		_defaultPosition = base.transform.position;
		ResetLocalVisualState();
		NetworkVariable<ulong> trappedPlayerNetworkId = _trappedPlayerNetworkId;
		trappedPlayerNetworkId.OnValueChanged = (NetworkVariable<ulong>.OnValueChangedDelegate)Delegate.Combine(trappedPlayerNetworkId.OnValueChanged, new NetworkVariable<ulong>.OnValueChangedDelegate(OnTrappedPlayerIdChanged));
		LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
		if (base.IsServer)
		{
			_currentTrapState.Value = OctopusTrapState.Active;
			_trappedPlayerNetworkId.Value = 0uL;
			_serverTrappedPlayer = null;
			_serverTrappedClientId = ulong.MaxValue;
			if (base.NetworkManager != null)
			{
				base.NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
			}
		}
		_playerDrownedBinding = new EventBinding<PlayerDrownedEvent>(OnTrappedPlayerDrowned);
		EventBus<PlayerDrownedEvent>.Register(_playerDrownedBinding);
		if (_trappedPlayerNetworkId.Value != 0L)
		{
			OnTrappedPlayerIdChanged(0uL, _trappedPlayerNetworkId.Value);
		}
	}

	public override void OnNetworkDespawn()
	{
		if (base.IsServer)
		{
			StopServerDamageRoutine();
			if (_serverTrappedPlayer != null)
			{
				_serverTrappedPlayer.IsTrapped = false;
				_serverTrappedPlayer.ServerClearTrapRecoveryOwner(this);
				_serverTrappedPlayer.ClientSetOctoTrapAnimRpc(isActive: false);
			}
		}
		if (base.IsServer && base.NetworkManager != null)
		{
			base.NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
		}
		if (_playerDrownedBinding != null)
		{
			EventBus<PlayerDrownedEvent>.Deregister(_playerDrownedBinding);
		}
		NetworkVariable<ulong> trappedPlayerNetworkId = _trappedPlayerNetworkId;
		trappedPlayerNetworkId.OnValueChanged = (NetworkVariable<ulong>.OnValueChangedDelegate)Delegate.Remove(trappedPlayerNetworkId.OnValueChanged, new NetworkVariable<ulong>.OnValueChangedDelegate(OnTrappedPlayerIdChanged));
		LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
		ClearLocalTrappedState();
		ClearOctopusHighlight();
		if (_octopusGraph != null)
		{
			_octopusGraph.DOKill();
		}
		base.OnNetworkDespawn();
	}

	private void ResetLocalVisualState()
	{
		_progressTween?.Kill();
		_progressTween = null;
		_isHelpingRelease = false;
		RefreshActionPrompt(isHelperRelease: false);
		ClearOctopusHighlight();
		if (_escapeProgressBarImage != null)
		{
			_escapeProgressBarImage.fillAmount = 0f;
		}
		if (_canvas != null)
		{
			_canvas.SetActive(value: false);
		}
		if (_octopusGraph != null)
		{
			_octopusGraph.gameObject.SetActive(value: false);
		}
	}

	private void OnTriggerEnter(Collider other)
	{
		if (_currentTrapState.Value == OctopusTrapState.Active && other.CompareTag("Player"))
		{
			if (!other.TryGetComponent<PPG_Player>(out var component))
			{
				UtilityMethods.Log("[OctopusTrap] OnTriggerEnter - Player component not found.", LogType.Warning);
			}
			else if (base.IsServer)
			{
				ServerTriggerTrap(component, component.transform.position);
			}
			else if (component.IsLocalPlayer)
			{
				RequestTriggerTrapServerRpc(component.NetworkObject.ToReference(), component.transform.position);
			}
		}
	}

	private void OnTriggerExit(Collider other)
	{
		if (_isHelpingRelease)
		{
			StopEscapeProgressUI(hideCanvas: true);
		}
	}

	[Rpc(SendTo.Server)]
	private void RequestTriggerTrapServerRpc(NetworkObjectReference playerReference, Vector3 reportedPlayerPosition)
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
			FastBufferWriter bufferWriter = __beginSendRpc(3014300824u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in playerReference, default(FastBufferWriter.ForNetworkSerializable));
			bufferWriter.WriteValueSafe(in reportedPlayerPosition);
			__endSendRpc(ref bufferWriter, 3014300824u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		if (_currentTrapState.Value == OctopusTrapState.Active)
		{
			PPG_Player component;
			if (!playerReference.TryGet(out var networkObject))
			{
				UtilityMethods.Log("[OctopusTrap] RequestTriggerTrapServerRpc - Failed to resolve NetworkObject.", LogType.Warning);
			}
			else if (!networkObject.TryGetComponent<PPG_Player>(out component))
			{
				UtilityMethods.Log("[OctopusTrap] RequestTriggerTrapServerRpc - Failed to resolve PPG_Player.", LogType.Warning);
			}
			else
			{
				ServerTriggerTrap(component, component.transform.position);
			}
		}
	}

	private void ServerTriggerTrap(PPG_Player player, Vector3 trappedPosition)
	{
		if (base.IsServer && _currentTrapState.Value == OctopusTrapState.Active && !(player == null) && !(player.NetworkObject == null))
		{
			_serverTrappedPlayer = player;
			_serverTrappedClientId = player.OwnerClientId;
			_currentTrapState.Value = OctopusTrapState.Triggered;
			_trappedPlayerNetworkId.Value = player.NetworkObject.NetworkObjectId;
			player.IsTrapped = true;
			player.ServerSetTrapRecoveryOwner(this);
			player.ClientSetOctoTrapAnimRpc(isActive: true);
			GlobalChatManager.BroadcastPlayerTrappedFromServer(this, player, "chat.trap.octopus", "an octopus trap");
			ClientPlayRpc(trappedPosition);
			StartServerDamageRoutine();
		}
	}

	private void StartServerDamageRoutine()
	{
		if (base.IsServer)
		{
			StopServerDamageRoutine();
			_serverDamageRoutine = StartCoroutine(ServerDamageRoutine());
		}
	}

	private IEnumerator ServerDamageRoutine()
	{
		WaitForSeconds damageTickWait = new WaitForSeconds(Mathf.Max(0.05f, _damageTickInterval));
		while (base.IsServer && base.IsSpawned && _currentTrapState.Value == OctopusTrapState.Triggered)
		{
			yield return damageTickWait;
			if (!base.IsServer || !base.IsSpawned || _currentTrapState.Value != OctopusTrapState.Triggered)
			{
				break;
			}
			if (!(_serverTrappedPlayer == null) && _serverTrappedPlayer.IsSpawned && !(_serverTrappedPlayer.DivingEquipment == null))
			{
				_serverTrappedPlayer.DivingEquipment.ServerConsumeOxygenRpc(_damagePerTick, withFeedback: true);
			}
		}
		_serverDamageRoutine = null;
	}

	private void StopServerDamageRoutine()
	{
		if (_serverDamageRoutine != null)
		{
			StopCoroutine(_serverDamageRoutine);
			_serverDamageRoutine = null;
		}
	}

	private void OnTrappedPlayerIdChanged(ulong previousId, ulong newId)
	{
		PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
		if (!(pPG_Player == null) && !(pPG_Player.NetworkObject == null))
		{
			ulong networkObjectId = pPG_Player.NetworkObject.NetworkObjectId;
			if (newId != 0L && newId == networkObjectId)
			{
				ApplyLocalTrappedState(pPG_Player);
			}
			else if (previousId != 0L && previousId == networkObjectId && newId != previousId)
			{
				ClearLocalTrappedState();
			}
			else if (newId == 0L && !_isLocalPlayerTrapped)
			{
				StopEscapeProgressUI(hideCanvas: true);
			}
		}
	}

	private void ApplyLocalTrappedState(PPG_Player localPlayer)
	{
		if (!_isLocalPlayerTrapped || !(_localTrappedPlayer == localPlayer))
		{
			_localTrappedPlayer = localPlayer;
			_isLocalPlayerTrapped = true;
			_isHelpingRelease = false;
			RefreshActionPrompt(isHelperRelease: false);
			ClearOctopusHighlight();
			SubscribeTrappedInput();
			if (_canvas != null)
			{
				_canvas.SetActive(value: true);
			}
			if (_escapeProgressBarImage != null)
			{
				_escapeProgressBarImage.fillAmount = 0f;
			}
			_localTrappedPlayer.SwitchToTpsCam();
			_localTrappedPlayer.SetOctopusTrapScreenVfx(active: true);
			_localTrappedPlayer.PauseMovement();
			_localTrappedPlayer.IsTrapped = true;
			if (_localTrappedPlayer.IsToolActive)
			{
				_localTrappedPlayer.DisableActiveTool();
			}
			Singleton<QuickSlotHandler>.Instance?.SuspendCurrentSelectionForMode(HeldSlotSuspensionReason.OctopusTrap);
		}
	}

	private void ClearLocalTrappedState()
	{
		UnsubscribeTrappedInput();
		StopEscapeProgressUI(hideCanvas: false);
		if (_localTrappedPlayer != null)
		{
			_localTrappedPlayer.SetOctopusTrapScreenVfx(active: false);
			_localTrappedPlayer.SwitchToFpsCam();
			_localTrappedPlayer.ResumeMovement();
			_localTrappedPlayer.IsTrapped = false;
			Singleton<QuickSlotHandler>.Instance?.ResumeCurrentSelectionForMode(HeldSlotSuspensionReason.OctopusTrap);
		}
		_localTrappedPlayer = null;
		_isLocalPlayerTrapped = false;
		_isHelpingRelease = false;
		if (_canvas != null)
		{
			_canvas.SetActive(value: false);
		}
	}

	private void SubscribeTrappedInput()
	{
		if (!(Singleton<InputManager>.Instance == null) && !(Singleton<InputManager>.Instance.InputHandler == null))
		{
			Singleton<InputManager>.Instance.InputHandler.Mouse1 -= OnTrappedPlayerMouse1;
			Singleton<InputManager>.Instance.InputHandler.Mouse1 += OnTrappedPlayerMouse1;
		}
	}

	private void UnsubscribeTrappedInput()
	{
		if (!(Singleton<InputManager>.Instance == null) && !(Singleton<InputManager>.Instance.InputHandler == null))
		{
			Singleton<InputManager>.Instance.InputHandler.Mouse1 -= OnTrappedPlayerMouse1;
		}
	}

	private void OnTrappedPlayerMouse1(InputActionPhase phase)
	{
		if (_isLocalPlayerTrapped && _currentTrapState.Value == OctopusTrapState.Triggered)
		{
			switch (phase)
			{
			case InputActionPhase.Started:
			case InputActionPhase.Performed:
				StartEscapeProgressUI(_requiredHoldDuration, isHelperRelease: false);
				break;
			case InputActionPhase.Canceled:
				StopEscapeProgressUI(hideCanvas: false);
				break;
			}
		}
	}

	private void StartEscapeProgressUI(float duration, bool isHelperRelease)
	{
		if (_progressTween != null && _progressTween.IsActive() && _progressTween.IsPlaying())
		{
			return;
		}
		_isHelpingRelease = isHelperRelease;
		RefreshActionPrompt(isHelperRelease);
		if (_canvas != null)
		{
			_canvas.SetActive(value: true);
		}
		if (!(_escapeProgressBarImage == null))
		{
			_progressTween?.Kill();
			_escapeProgressBarImage.fillAmount = 0f;
			_progressTween = _escapeProgressBarImage.DOFillAmount(1f, duration).SetEase(Ease.Linear).OnComplete(() =>
			{
				_progressTween = null;
				RequestEscapeServerRpc(isHelperRelease);
			});
		}
	}

	private void StopEscapeProgressUI(bool hideCanvas)
	{
		_progressTween?.Kill();
		_progressTween = null;
		if (_escapeProgressBarImage != null)
		{
			_escapeProgressBarImage.fillAmount = 0f;
		}
		if (hideCanvas && _canvas != null)
		{
			_canvas.SetActive(value: false);
		}
		if (hideCanvas)
		{
			_isHelpingRelease = false;
		}
	}

	[Rpc(SendTo.Server)]
	private void RequestEscapeServerRpc(bool isHelperRelease)
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
			FastBufferWriter bufferWriter = __beginSendRpc(525664987u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in isHelperRelease, default(FastBufferWriter.ForPrimitives));
			__endSendRpc(ref bufferWriter, 525664987u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		if (_currentTrapState.Value == OctopusTrapState.Triggered)
		{
			if (isHelperRelease)
			{
				ServerReleaseTrap();
			}
			else if (UnityEngine.Random.value < GetCurrentEscapeChance())
			{
				ServerReleaseTrap();
			}
			else
			{
				ClientEscapeFailedRpc();
			}
		}
	}

	private float GetCurrentEscapeChance()
	{
		if (IsSinglePlayerSession())
		{
			return _singlePlayerEscapeChance;
		}
		return _escapeChance;
	}

	private bool IsSinglePlayerSession()
	{
		if (!(base.NetworkManager == null))
		{
			return base.NetworkManager.ConnectedClientsIds.Count <= 1;
		}
		return true;
	}

	[Rpc(SendTo.Everyone)]
	private void ClientEscapeFailedRpc()
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
			FastBufferWriter bufferWriter = __beginSendRpc(1128803355u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 1128803355u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_isLocalPlayerTrapped)
			{
				StopEscapeProgressUI(hideCanvas: false);
			}
			else if (_isHelpingRelease)
			{
				StopEscapeProgressUI(hideCanvas: true);
			}
		}
	}

	private void ServerReleaseTrap()
	{
		if (base.IsServer && _currentTrapState.Value == OctopusTrapState.Triggered)
		{
			StopServerDamageRoutine();
			if (_serverTrappedPlayer != null)
			{
				_serverTrappedPlayer.IsTrapped = false;
				_serverTrappedPlayer.ServerClearTrapRecoveryOwner(this);
				_serverTrappedPlayer.ClientSetOctoTrapAnimRpc(isActive: false);
			}
			_currentTrapState.Value = OctopusTrapState.Inactive;
			_trappedPlayerNetworkId.Value = 0uL;
			_serverTrappedPlayer = null;
			_serverTrappedClientId = ulong.MaxValue;
			ClientStopRpc();
			if (Singleton<DelayHelper>.Instance != null)
			{
				Singleton<DelayHelper>.Instance.DelayAction(_disableDuration, ServerReactivateTrap);
			}
			else
			{
				Invoke("ServerReactivateTrap", _disableDuration);
			}
		}
	}

	public bool ServerTryReleasePlayerForRecovery(PPG_Player player, string reason)
	{
		if (!base.IsServer || player == null || _serverTrappedPlayer != player)
		{
			return false;
		}
		UtilityMethods.Log($"[OctopusTrap] Player released by recovery. ClientId={player.OwnerClientId} Reason={reason}");
		ServerReleaseTrap();
		return true;
	}

	private void ServerReactivateTrap()
	{
		if (base.IsServer && base.IsSpawned)
		{
			_currentTrapState.Value = OctopusTrapState.Active;
			_trappedPlayerNetworkId.Value = 0uL;
			_serverTrappedPlayer = null;
			_serverTrappedClientId = ulong.MaxValue;
		}
	}

	[Rpc(SendTo.Everyone)]
	private void ClientPlayRpc(Vector3 trappedPosition)
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
			FastBufferWriter bufferWriter = __beginSendRpc(647339439u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in trappedPosition);
			__endSendRpc(ref bufferWriter, 647339439u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		if (!(_octopusGraph == null))
		{
			_visualRuntimeVersion++;
			_octopusGraph.DOKill();
			_octopusGraph.gameObject.SetActive(value: true);
			base.transform.position = new Vector3(trappedPosition.x, _defaultPosition.y, trappedPosition.z);
			if (_sandVfx != null)
			{
				_sandVfx.Play();
			}
			if (_sandVfxTwo != null)
			{
				_sandVfxTwo.Play();
			}
			if (_octopusAnimator != null)
			{
				_octopusAnimator.SetTrigger("TrapActive");
			}
			_octopusGraph.DOLocalRotate(new Vector3(225f, 0f, 0f), 2f);
			_octopusGraph.DOLocalMove(new Vector3(0f, 0.8f, 1f), 1f);
			Singleton<SoundManager>.Instance.PlaySFX(_trapSound, base.transform.position);
			if (_octopusCollider != null)
			{
				_octopusCollider.center = new Vector3(0f, 0.5f, 0f);
			}
		}
	}

	[Rpc(SendTo.Everyone)]
	private void ClientStopRpc()
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
			FastBufferWriter bufferWriter = __beginSendRpc(3658805178u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 3658805178u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage != __RpcExecStage.Execute)
		{
			return;
		}
		__rpc_exec_stage = __RpcExecStage.Send;
		int runtimeVersion = _visualRuntimeVersion;
		StopEscapeProgressUI(hideCanvas: true);
		ClearOctopusHighlight();
		if (_octopusAnimator != null)
		{
			_octopusAnimator.SetTrigger("TrapDeactive");
		}
		if (Singleton<DelayHelper>.Instance == null)
		{
			ResetOctopusVisual();
			return;
		}
		Singleton<DelayHelper>.Instance.DelayAction(2f, () =>
		{
			if (runtimeVersion == _visualRuntimeVersion && !(_octopusGraph == null))
			{
				_octopusGraph.DOKill();
				_octopusGraph.DOLocalRotate(new Vector3(180f, 0f, 0f), 1f);
				if (_octopusCollider != null)
				{
					_octopusCollider.center = Vector3.zero;
				}
				Singleton<SoundManager>.Instance.PlaySFX(_trapSound, base.transform.position);
				_octopusGraph.DOLocalMoveY(-1f, 2f).OnComplete(ResetOctopusVisual);
			}
		});
	}

	public void ServerEndDayReset()
	{
		if (base.IsServer)
		{
			StopServerDamageRoutine();
			CancelInvoke("ServerReactivateTrap");
			if (_serverTrappedPlayer != null)
			{
				_serverTrappedPlayer.IsTrapped = false;
				_serverTrappedPlayer.ServerClearTrapRecoveryOwner(this);
				_serverTrappedPlayer.ClientSetOctoTrapAnimRpc(isActive: false);
			}
			_currentTrapState.Value = OctopusTrapState.Active;
			_trappedPlayerNetworkId.Value = 0uL;
			_serverTrappedPlayer = null;
			_serverTrappedClientId = ulong.MaxValue;
			ClientEndDayResetRpc();
		}
	}

	private void OnClientDisconnected(ulong clientId)
	{
		if (base.IsServer && clientId == _serverTrappedClientId)
		{
			ServerReleaseTrap();
		}
	}

	[Rpc(SendTo.Everyone)]
	private void ClientEndDayResetRpc()
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
			FastBufferWriter bufferWriter = __beginSendRpc(1789788044u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 1789788044u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			_visualRuntimeVersion++;
			ClearLocalTrappedState();
			if (_octopusGraph != null)
			{
				_octopusGraph.DOKill();
			}
			ResetOctopusVisual();
			ResetLocalVisualState();
		}
	}

	private void ResetOctopusVisual()
	{
		ClearOctopusHighlight();
		if (_octopusGraph != null)
		{
			_octopusGraph.gameObject.SetActive(value: false);
			_octopusGraph.localPosition = Vector3.up * -1f;
		}
		base.transform.position = _defaultPosition;
		if (_sandVfx != null)
		{
			_sandVfx.Stop();
		}
		if (_sandVfxTwo != null)
		{
			_sandVfxTwo.Stop();
		}
		if (_octopusCollider != null)
		{
			_octopusCollider.center = Vector3.zero;
		}
	}

	private void OnTrappedPlayerDrowned(PlayerDrownedEvent @event)
	{
		if (!(@event._drownedPlayer == null))
		{
			if (_localTrappedPlayer != null && @event._drownedPlayer == _localTrappedPlayer)
			{
				RequestForceReleaseServerRpc();
			}
			else if (base.IsServer && _serverTrappedPlayer != null && @event._drownedPlayer == _serverTrappedPlayer)
			{
				ServerReleaseTrap();
			}
		}
	}

	[Rpc(SendTo.Server)]
	private void RequestForceReleaseServerRpc()
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
			FastBufferWriter bufferWriter = __beginSendRpc(3725374480u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			__endSendRpc(ref bufferWriter, 3725374480u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Execute)
		{
			__rpc_exec_stage = __RpcExecStage.Send;
			ServerReleaseTrap();
		}
	}

	public void GazeEnter()
	{
		if (_currentTrapState.Value == OctopusTrapState.Triggered && !_isLocalPlayerTrapped)
		{
			RefreshActionPrompt(isHelperRelease: true);
			SetOctopusHighlight();
			if (_canvas != null)
			{
				_canvas.SetActive(value: true);
			}
		}
	}

	public void GazeExit()
	{
		if (!_isLocalPlayerTrapped)
		{
			ClearOctopusHighlight();
			if (!_isHelpingRelease && _canvas != null)
			{
				_canvas.SetActive(value: false);
			}
		}
	}

	private void CacheOctopusOutlinable()
	{
		if (_octopusGraph == null)
		{
			return;
		}
		if (_octopusOutlinable == null)
		{
			_octopusOutlinable = _octopusGraph.GetComponent<Outlinable>();
		}
		if (_octopusOutlinable == null)
		{
			_octopusOutlinable = _octopusGraph.gameObject.AddComponent<Outlinable>();
		}
		while (_octopusOutlinable.OutlineTargetsCount > 0)
		{
			_octopusOutlinable.RemoveTarget(_octopusOutlinable[0]);
		}
		Renderer[] componentsInChildren = _octopusGraph.GetComponentsInChildren<Renderer>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			if (componentsInChildren[i] != null)
			{
				_octopusOutlinable.AddRenderer(componentsInChildren[i]);
			}
		}
		_octopusOutlinable.enabled = false;
	}

	private void SetOctopusHighlight()
	{
		if (!(_octopusOutlinable == null))
		{
			Singleton<HighlightManager>.Instance?.SetHighlight(_octopusOutlinable);
		}
	}

	private void ClearOctopusHighlight()
	{
		if (!(_octopusOutlinable == null))
		{
			HighlightManager instance = Singleton<HighlightManager>.Instance;
			if (instance != null)
			{
				instance.ClearCurrentHighlight(_octopusOutlinable);
			}
			else
			{
				_octopusOutlinable.enabled = false;
			}
		}
	}

	public void TriggerInput(Interactor interactor, InputKind inputKind, InputActionPhase phase)
	{
		if (inputKind == InputKind.Mouse1 && _currentTrapState.Value == OctopusTrapState.Triggered && !_isLocalPlayerTrapped)
		{
			switch (phase)
			{
			case InputActionPhase.Started:
			case InputActionPhase.Performed:
				StartEscapeProgressUI(_requiredHoldDuration, isHelperRelease: true);
				break;
			case InputActionPhase.Canceled:
				StopEscapeProgressUI(hideCanvas: true);
				break;
			}
		}
	}

	public void TriggerCrossInput(Interactor interactor, BaseHoldable held, InputKind inputKind, InputActionPhase phase)
	{
	}

	private void OnSelectedLocaleChanged(Locale locale)
	{
		if (!(_canvas == null) && _canvas.activeSelf)
		{
			RefreshActionPrompt(!_isLocalPlayerTrapped);
		}
	}

	private void RefreshActionPrompt(bool isHelperRelease)
	{
		if (!(_actionPromptText == null))
		{
			_actionPromptText.text = (isHelperRelease ? LocalizationDatabase.GetString("UiTable", "gameplay.octopus_trap.hold_to_rescue", "HOLD TO RESCUE") : LocalizationDatabase.GetString("UiTable", "gameplay.octopus_trap.hold_to_escape", "HOLD TO ESCAPE"));
		}
	}

	protected override void __initializeVariables()
	{
		if (_currentTrapState == null)
		{
			throw new Exception("OctopusTrap._currentTrapState cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		_currentTrapState.Initialize(this);
		__nameNetworkVariable(_currentTrapState, "_currentTrapState");
		NetworkVariableFields.Add(_currentTrapState);
		if (_trappedPlayerNetworkId == null)
		{
			throw new Exception("OctopusTrap._trappedPlayerNetworkId cannot be null. All NetworkVariableBase instances must be initialized.");
		}
		_trappedPlayerNetworkId.Initialize(this);
		__nameNetworkVariable(_trappedPlayerNetworkId, "_trappedPlayerNetworkId");
		NetworkVariableFields.Add(_trappedPlayerNetworkId);
		base.__initializeVariables();
	}

	protected override void __initializeRpcs()
	{
		__registerRpc(3014300824u, __rpc_handler_3014300824, "RequestTriggerTrapServerRpc", RpcInvokePermission.Everyone);
		__registerRpc(525664987u, __rpc_handler_525664987, "RequestEscapeServerRpc", RpcInvokePermission.Everyone);
		__registerRpc(1128803355u, __rpc_handler_1128803355, "ClientEscapeFailedRpc", RpcInvokePermission.Everyone);
		__registerRpc(647339439u, __rpc_handler_647339439, "ClientPlayRpc", RpcInvokePermission.Everyone);
		__registerRpc(3658805178u, __rpc_handler_3658805178, "ClientStopRpc", RpcInvokePermission.Everyone);
		__registerRpc(1789788044u, __rpc_handler_1789788044, "ClientEndDayResetRpc", RpcInvokePermission.Everyone);
		__registerRpc(3725374480u, __rpc_handler_3725374480, "RequestForceReleaseServerRpc", RpcInvokePermission.Everyone);
		base.__initializeRpcs();
	}

	private static void __rpc_handler_3014300824(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
			reader.ReadValueSafe(out Vector3 value2);
			((OctopusTrap)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((OctopusTrap)target).RequestTriggerTrapServerRpc(value, value2);
			((OctopusTrap)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_525664987(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
			((OctopusTrap)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((OctopusTrap)target).RequestEscapeServerRpc(value);
			((OctopusTrap)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1128803355(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((OctopusTrap)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((OctopusTrap)target).ClientEscapeFailedRpc();
			((OctopusTrap)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_647339439(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			reader.ReadValueSafe(out Vector3 value);
			((OctopusTrap)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((OctopusTrap)target).ClientPlayRpc(value);
			((OctopusTrap)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3658805178(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((OctopusTrap)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((OctopusTrap)target).ClientStopRpc();
			((OctopusTrap)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_1789788044(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((OctopusTrap)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((OctopusTrap)target).ClientEndDayResetRpc();
			((OctopusTrap)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	private static void __rpc_handler_3725374480(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
	{
		NetworkManager networkManager = target.NetworkManager;
		if ((object)networkManager != null && networkManager.IsListening)
		{
			((OctopusTrap)target).__rpc_exec_stage = __RpcExecStage.Execute;
			((OctopusTrap)target).RequestForceReleaseServerRpc();
			((OctopusTrap)target).__rpc_exec_stage = __RpcExecStage.Send;
		}
	}

	protected override string __getTypeName()
	{
		return "OctopusTrap";
	}
}
