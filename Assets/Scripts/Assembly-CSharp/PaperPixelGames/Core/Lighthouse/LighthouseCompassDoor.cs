using System;
using System.Collections.Generic;
using DG.Tweening;
using EPOOutline;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.PlayerTools;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Lighthouse
{
	public class LighthouseCompassDoor : NetworkBehaviour, IBasicInteractable, IToolActiveInteractable
	{
		[Header("Compass Gate")]
		[SerializeField]
		private byte _requiredCompassLevel = 4;

		[SerializeField]
		private Transform _compassTarget;

		[Header("Door")]
		[SerializeField]
		private Animator _doorAnimator;

		[SerializeField]
		private string _openBoolName = "IsOpen";

		[SerializeField]
		private string _closeBoolName = "IsCLose";

		[Header("Boss Defeated Compass Teleport")]
		[SerializeField]
		private List<Portal> _lighthousePortals = new List<Portal>();

		[Header("Boss Defeated Cleanup")]
		[SerializeField]
		private GameObject _topDoorToDisableAfterBossDefeat;

		[Header("Feedback")]
		[SerializeField]
		private Outlinable _outlinable;

		[SerializeField]
		private AudioClipSO _openSfx;

		[SerializeField]
		private AudioClipSO _closeSfx;

		[SerializeField]
		private AudioClipSO _lockedSfx;

		[SerializeField]
		private AudioClipSO _lockedAttemptRoarSfx;

		[SerializeField]
		private string _lockedToastMessage = "Door is locked.";

		[Header("Locked Attempt Feedback")]
		[SerializeField]
		[Min(1f)]
		private int _lockedAttemptRoarThreshold = 10;

		[SerializeField]
		[Min(0f)]
		private float _lockedAttemptResetDelay = 2f;

		[Header("Compass Proximity FX")]
		[SerializeField]
		private ParticleSystem _requiredCompassProximityFx;

		[SerializeField]
		[Min(0f)]
		private float _requiredCompassFxDistance = 8f;

		[SerializeField]
		[Min(0.05f)]
		private float _requiredCompassFxCheckInterval = 0.2f;

		[SerializeField]
		[Min(0f)]
		private float _requiredCompassFxTransitionDuration = 0.35f;

		[SerializeField]
		private Ease _requiredCompassFxEase = Ease.OutCubic;

		private readonly NetworkVariable<bool> _isOpen = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<bool> _isTeleportEnabled = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<bool> _arePortalVisualsEnabled = new NetworkVariable<bool>(value: false);

		private bool _isGazing;

		private bool _lastLocalCompassHeldState;

		private Transform _requiredCompassProximityFxRoot;

		private Vector3 _requiredCompassProximityFxScale = Vector3.one;

		private bool _isRequiredCompassProximityFxVisible;

		private float _nextRequiredCompassFxCheckTime;

		private int _consecutiveLockedAttempts;

		private float _lastLockedAttemptTime = float.NegativeInfinity;

		public bool CanGaze => true;

		public bool CanInteractable => true;

		public Transform CompassTarget
		{
			get
			{
				if (!(_compassTarget != null))
				{
					return base.transform;
				}
				return _compassTarget;
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<bool> isOpen = _isOpen;
			isOpen.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isOpen.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnOpenChanged));
			NetworkVariable<bool> isTeleportEnabled = _isTeleportEnabled;
			isTeleportEnabled.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isTeleportEnabled.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnTeleportEnabledChanged));
			NetworkVariable<bool> arePortalVisualsEnabled = _arePortalVisualsEnabled;
			arePortalVisualsEnabled.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(arePortalVisualsEnabled.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnPortalVisualsEnabledChanged));
			ApplyState(_isOpen.Value, playSfx: false);
			ApplyPortalState();
		}

		public override void OnNetworkDespawn()
		{
			NetworkVariable<bool> isOpen = _isOpen;
			isOpen.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isOpen.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnOpenChanged));
			NetworkVariable<bool> isTeleportEnabled = _isTeleportEnabled;
			isTeleportEnabled.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isTeleportEnabled.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnTeleportEnabledChanged));
			NetworkVariable<bool> arePortalVisualsEnabled = _arePortalVisualsEnabled;
			arePortalVisualsEnabled.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(arePortalVisualsEnabled.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnPortalVisualsEnabledChanged));
			SetRequiredCompassProximityFxVisible(visible: false, instant: true);
			ClearHintsAndHighlight();
			base.OnNetworkDespawn();
		}

		private void Awake()
		{
			if (_outlinable == null)
			{
				TryGetComponent<Outlinable>(out _outlinable);
			}
			if (_outlinable != null)
			{
				_outlinable.enabled = false;
			}
			if (_lighthousePortals.Count == 0)
			{
				GetComponentsInChildren(includeInactive: true, _lighthousePortals);
			}
			ConfigureMovingDoorInteractionColliders();
			InitializeRequiredCompassProximityFx();
			ApplyPortalState();
		}

		private void Update()
		{
			RefreshTopDoorAfterBossDefeat();
			if (!(Time.time < _nextRequiredCompassFxCheckTime))
			{
				_nextRequiredCompassFxCheckTime = Time.time + _requiredCompassFxCheckInterval;
				RefreshRequiredCompassProximityFx();
			}
		}

		public void RefreshForLocalPlayer()
		{
			RefreshHintsAndHighlight();
		}

		public void GazeEnter()
		{
			_isGazing = true;
			RefreshHintsAndHighlight();
		}

		public void GazeExit()
		{
			_isGazing = false;
			ClearHintsAndHighlight();
		}

		public void TriggerInput(Interactor interactor, InputKind inputKind, InputActionPhase phase)
		{
			if (inputKind == InputKind.Interact && phase == InputActionPhase.Started)
			{
				TryUseDoor(interactor);
			}
		}

		public void TriggerCrossInput(Interactor interactor, BaseHoldable held, InputKind inputKind, InputActionPhase phase)
		{
			TriggerInput(interactor, inputKind, phase);
		}

		private void TryUseDoor(Interactor interactor)
		{
			if (CanInteractable)
			{
				if (!IsDiscoverLighthouseQuestActive() && !IsLighthouseDiscovered())
				{
					PlayLockedFeedback();
				}
				else if (!IsRedKingCrabDefeated() && !HasRequiredCompassHeld(interactor))
				{
					PlayLockedFeedback();
				}
				else
				{
					ServerToggleLighthouseDoorRpc(interactor.NetworkObject.ToReference());
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerToggleLighthouseDoorRpc(NetworkObjectReference interactorRef)
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
				FastBufferWriter bufferWriter = __beginSendRpc(32776571u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 32776571u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			bool flag = IsDiscoverLighthouseQuestActive();
			if (!flag && !IsLighthouseDiscovered())
			{
				return;
			}
			bool flag2 = interactorRef.TryGetComponent<Interactor>(out var component) && HasRequiredCompassHeld(component);
			if (!IsRedKingCrabDefeated() && !flag2)
			{
				UtilityMethods.Log("[LighthouseCompassDoor] Use rejected. Final compass is not held.", LogType.Warning);
				return;
			}
			bool flag3 = !_isOpen.Value;
			_isTeleportEnabled.Value = flag3 & flag2;
			_arePortalVisualsEnabled.Value = flag3 & flag2;
			_isOpen.Value = flag3;
			if (flag3 & flag)
			{
				QuestReferenceHub.Instance?.QuestComplete(QuestId.DiscoverTheLighthouse);
			}
		}

		private void OnOpenChanged(bool wasOpen, bool isOpen)
		{
			ApplyState(isOpen, playSfx: true);
		}

		private void OnTeleportEnabledChanged(bool wasEnabled, bool isEnabled)
		{
			ApplyPortalState();
		}

		private void OnPortalVisualsEnabledChanged(bool wereEnabled, bool areEnabled)
		{
			ApplyPortalState();
		}

		private void ApplyState(bool isOpen, bool playSfx)
		{
			if (_doorAnimator != null)
			{
				_doorAnimator.SetBool(_openBoolName, isOpen);
				_doorAnimator.SetBool(_closeBoolName, !isOpen);
			}
			if (playSfx)
			{
				PlayDoorSfx(isOpen);
			}
			RefreshHintsAndHighlight();
			RefreshRequiredCompassProximityFx();
		}

		private void PlayDoorSfx(bool isOpen)
		{
			if (!(Singleton<SoundManager>.Instance == null))
			{
				AudioClipSO audioClipSO = (isOpen ? _openSfx : _closeSfx);
				if (!(audioClipSO == null))
				{
					Singleton<SoundManager>.Instance.PlaySFX(audioClipSO, base.transform.position, base.transform);
				}
			}
		}

		private void PlayLockedFeedback()
		{
			Singleton<NotifyManager>.Instance?.ShowToast(_lockedToastMessage, NotificationState.Info, 1.5f);
			if (Singleton<SoundManager>.Instance != null && _lockedSfx != null)
			{
				Singleton<SoundManager>.Instance.PlaySFX(_lockedSfx, base.transform.position, base.transform);
			}
			RegisterLockedAttempt();
		}

		private void RegisterLockedAttempt()
		{
			if (Time.unscaledTime - _lastLockedAttemptTime > _lockedAttemptResetDelay)
			{
				_consecutiveLockedAttempts = 0;
			}
			_lastLockedAttemptTime = Time.unscaledTime;
			_consecutiveLockedAttempts++;
			if (_consecutiveLockedAttempts >= _lockedAttemptRoarThreshold)
			{
				_consecutiveLockedAttempts = 0;
				if (Singleton<SoundManager>.Instance != null && _lockedAttemptRoarSfx != null)
				{
					Singleton<SoundManager>.Instance.PlaySFX(_lockedAttemptRoarSfx, base.transform.position, base.transform);
				}
			}
		}

		private void ConfigureMovingDoorInteractionColliders()
		{
			Collider[] componentsInChildren = GetComponentsInChildren<Collider>(includeInactive: true);
			bool flag = false;
			Collider[] array = componentsInChildren;
			foreach (Collider collider in array)
			{
				if (!(collider == null) && !(collider.transform == base.transform))
				{
					collider.gameObject.layer = base.gameObject.layer;
					flag = true;
				}
			}
			if (!flag)
			{
				return;
			}
			array = GetComponents<Collider>();
			foreach (Collider collider2 in array)
			{
				if (collider2 != null && collider2.isTrigger)
				{
					collider2.enabled = false;
				}
			}
		}

		private static bool IsDiscoverLighthouseQuestActive()
		{
			if (QuestReferenceHub.Instance != null)
			{
				return QuestReferenceHub.Instance.IsActive(QuestId.DiscoverTheLighthouse);
			}
			return false;
		}

		private static bool IsLighthouseDiscovered()
		{
			if (QuestReferenceHub.Instance != null)
			{
				return QuestReferenceHub.Instance.IsCompleted(QuestId.DiscoverTheLighthouse);
			}
			return false;
		}

		private static bool IsRedKingCrabDefeated()
		{
			if (QuestReferenceHub.Instance != null)
			{
				return QuestReferenceHub.Instance.IsCompleted(QuestId.DefeatTheRedKingCrab);
			}
			return false;
		}

		private void ApplyPortalState()
		{
			foreach (Portal lighthousePortal in _lighthousePortals)
			{
				if (lighthousePortal != null)
				{
					lighthousePortal.SetRuntimeState(_isTeleportEnabled.Value, _arePortalVisualsEnabled.Value);
				}
			}
		}

		private void RefreshTopDoorAfterBossDefeat()
		{
			if (!(_topDoorToDisableAfterBossDefeat == null))
			{
				bool flag = !IsRedKingCrabDefeated();
				if (_topDoorToDisableAfterBossDefeat.activeSelf != flag)
				{
					_topDoorToDisableAfterBossDefeat.SetActive(flag);
				}
			}
		}

		private void InitializeRequiredCompassProximityFx()
		{
			if (!(_requiredCompassProximityFx == null))
			{
				_requiredCompassProximityFxRoot = _requiredCompassProximityFx.transform;
				Transform transform = ((_doorAnimator != null) ? _doorAnimator.transform : base.transform);
				if (_requiredCompassProximityFxRoot.parent != transform)
				{
					_requiredCompassProximityFxRoot.SetParent(transform, worldPositionStays: true);
				}
				_requiredCompassProximityFxScale = _requiredCompassProximityFxRoot.localScale;
				SetRequiredCompassProximityFxVisible(visible: false, instant: true);
			}
		}

		private void RefreshRequiredCompassProximityFx()
		{
			if (!(_requiredCompassProximityFx == null))
			{
				bool visible = !_isOpen.Value && (IsDiscoverLighthouseQuestActive() || IsLighthouseDiscovered()) && CanInteractable && IsAnyRequiredCompassHeldPlayerInRange();
				SetRequiredCompassProximityFxVisible(visible, instant: false);
			}
		}

		private bool IsAnyRequiredCompassHeldPlayerInRange()
		{
			if (Singleton<PlayersManager>.Instance == null)
			{
				return false;
			}
			float num = _requiredCompassFxDistance * _requiredCompassFxDistance;
			foreach (PPG_Player value in Singleton<PlayersManager>.Instance.Players.Values)
			{
				if (!(value == null) && HasRequiredCompassHeld(value.Interactor) && (value.transform.position - base.transform.position).sqrMagnitude <= num)
				{
					return true;
				}
			}
			return false;
		}

		private void SetRequiredCompassProximityFxVisible(bool visible, bool instant)
		{
			if (_requiredCompassProximityFx == null || (_isRequiredCompassProximityFxVisible == visible && !instant))
			{
				return;
			}
			_isRequiredCompassProximityFxVisible = visible;
			Transform fxRoot = ((_requiredCompassProximityFxRoot != null) ? _requiredCompassProximityFxRoot : _requiredCompassProximityFx.transform);
			fxRoot.DOKill();
			if (visible)
			{
				if (!fxRoot.gameObject.activeSelf)
				{
					fxRoot.gameObject.SetActive(value: true);
				}
				if (!_requiredCompassProximityFx.isPlaying)
				{
					_requiredCompassProximityFx.Play(withChildren: true);
				}
				if (instant || _requiredCompassFxTransitionDuration <= 0f)
				{
					fxRoot.localScale = _requiredCompassProximityFxScale;
				}
				else
				{
					fxRoot.DOScale(_requiredCompassProximityFxScale, _requiredCompassFxTransitionDuration).From(Vector3.zero).SetEase(_requiredCompassFxEase);
				}
				return;
			}
			if (instant || _requiredCompassFxTransitionDuration <= 0f)
			{
				_requiredCompassProximityFx.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
				fxRoot.localScale = Vector3.zero;
				fxRoot.gameObject.SetActive(value: false);
				return;
			}
			fxRoot.DOScale(Vector3.zero, _requiredCompassFxTransitionDuration).SetEase(_requiredCompassFxEase).OnComplete(() =>
			{
				if (!_isRequiredCompassProximityFxVisible && !(_requiredCompassProximityFx == null))
				{
					_requiredCompassProximityFx.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
					if (fxRoot != null)
					{
						fxRoot.gameObject.SetActive(value: false);
					}
				}
			});
		}

		private void RefreshHintsAndHighlight()
		{
			if (!_isGazing)
			{
				return;
			}
			ClearHintsAndHighlight();
			if (CanInteractable)
			{
				Singleton<HintsManager>.Instance?.AddHint("Interact", _isOpen.Value ? "key_close" : "key_open");
				if (_outlinable != null)
				{
					_outlinable.enabled = true;
					Singleton<HighlightManager>.Instance?.SetHighlight(_outlinable);
				}
			}
		}

		private void ClearHintsAndHighlight()
		{
			Singleton<HintsManager>.Instance?.RemoveHint("Interact");
			if (_outlinable != null)
			{
				Singleton<HighlightManager>.Instance?.ClearCurrentHighlight();
				_outlinable.enabled = false;
			}
		}

		private bool IsLocalRequiredCompassHeld()
		{
			PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
			if (pPG_Player == null || !pPG_Player.IsOwner)
			{
				return false;
			}
			return HasRequiredCompassHeld(pPG_Player.Interactor);
		}

		private bool HasRequiredCompassHeld(Interactor interactor)
		{
			if (interactor == null || interactor.Player == null)
			{
				return false;
			}
			AlienCompass compass = interactor.Player.Compass;
			if (compass != null && compass.IsUnlocked && compass.Level >= _requiredCompassLevel)
			{
				return compass.ToolStatus;
			}
			return false;
		}

		protected override void __initializeVariables()
		{
			if (_isOpen == null)
			{
				throw new Exception("LighthouseCompassDoor._isOpen cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isOpen.Initialize(this);
			__nameNetworkVariable(_isOpen, "_isOpen");
			NetworkVariableFields.Add(_isOpen);
			if (_isTeleportEnabled == null)
			{
				throw new Exception("LighthouseCompassDoor._isTeleportEnabled cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isTeleportEnabled.Initialize(this);
			__nameNetworkVariable(_isTeleportEnabled, "_isTeleportEnabled");
			NetworkVariableFields.Add(_isTeleportEnabled);
			if (_arePortalVisualsEnabled == null)
			{
				throw new Exception("LighthouseCompassDoor._arePortalVisualsEnabled cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_arePortalVisualsEnabled.Initialize(this);
			__nameNetworkVariable(_arePortalVisualsEnabled, "_arePortalVisualsEnabled");
			NetworkVariableFields.Add(_arePortalVisualsEnabled);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(32776571u, __rpc_handler_32776571, "ServerToggleLighthouseDoorRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_32776571(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((LighthouseCompassDoor)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((LighthouseCompassDoor)target).ServerToggleLighthouseDoorRpc(value);
				((LighthouseCompassDoor)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "LighthouseCompassDoor";
		}
	}
}
