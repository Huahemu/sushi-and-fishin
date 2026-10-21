using System;
using System.Collections;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Kitchen.Wholasale;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Kitchen.RiceCookerScript
{
	public class RiceCooker : BaseInteractable, IHeldItemMouse1Interactable
	{
		public enum LedState
		{
			Off = 0,
			Empty = 1,
			HasRaw = 2,
			CookingBlink = 3,
			Cooked = 4
		}

		private enum RiceCookerFeedbackId : byte
		{
			PutRice = 1,
			TakeRice = 2
		}

		[Header("References")]
		public Renderer ledRenderer;

		private Color colorEmpty = Color.red;

		private Color colorHasRaw = Color.yellow;

		private Color colorCooked = Color.green;

		private Color colorCooking = Color.yellow;

		private Coroutine blinkRoutine;

		private readonly NetworkVariable<bool> _isLidOpen = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<bool> _hasRice = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<bool> _isCooked = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<bool> _isCooking = new NetworkVariable<bool>(value: false);

		private byte _cookingTimeRemaining;

		[SerializeField]
		private GameObject _objToolTipGO;

		[SerializeField]
		private IngredientSO _rawRiceSO;

		[SerializeField]
		private MachineType _machineType;

		[SerializeField]
		private Animator _animator;

		[SerializeField]
		private GameObject _riceInsideObj;

		[Header("VFX")]
		[SerializeField]
		private ParticleSystem _particleSystem;

		[Header("Sounds")]
		[SerializeField]
		private AudioSource _loopAudioSource;

		[SerializeField]
		private AudioSource _oneShotAudioSource;

		[SerializeField]
		private AudioClip _cookingFinshAudioClip;

		[SerializeField]
		private AudioClip _lidOpenAudioClip;

		[SerializeField]
		private AudioClip _lidOpenWithCookedRiceAudioClip;

		[SerializeField]
		private AudioClip _lidCloseAudioClip;

		[SerializeField]
		private AudioClip _cookingAudioClip;

		[Header("Interaction Sounds")]
		[SerializeField]
		private AudioClip _putRiceAudioClip;

		[SerializeField]
		private AudioClip _takeRiceAudioClip;

		private Coroutine _cookingRoutine;

		private byte _cookingTime = 7;

		private ulong _cookingClientId = ulong.MaxValue;

		private float _nextCookingRecoveryCheckAt;

		private readonly NetworkVariable<int> _staffEmployee = new NetworkVariable<int>(0);

		public bool StaffLocked => _staffEmployee.Value != 0;

		public bool ServerStaffAvailable
		{
			get
			{
				if (base.IsServer && base.IsSpawned && !StaffLocked && !_hasRice.Value)
				{
					return !_isCooking.Value;
				}
				return false;
			}
		}

		private void ApplyState(LedState state)
		{
			if (blinkRoutine != null)
			{
				StopCoroutine(blinkRoutine);
				blinkRoutine = null;
			}
			switch (state)
			{
			case LedState.Empty:
				SetSolid(colorEmpty);
				break;
			case LedState.HasRaw:
				SetSolid(colorHasRaw);
				break;
			case LedState.Cooked:
				SetSolid(colorCooked);
				break;
			case LedState.CookingBlink:
				blinkRoutine = StartCoroutine(Blink(colorCooking));
				break;
			default:
				SetSolid(Color.black);
				break;
			}
		}

		private void SetSolid(Color c)
		{
			ledRenderer.material.SetColor("_EmissionColor", c);
		}

		private IEnumerator Blink(Color baseColor, float speed = 4f)
		{
			while (true)
			{
				float num = Mathf.PingPong(Time.time * speed, 1f) + 0.1f;
				ledRenderer.material.SetColor("_EmissionColor", baseColor * num);
				yield return null;
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerSetLedStateRpc(LedState state)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3279785213u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in state, default(FastBufferWriter.ForEnums));
				__endSendRpc(ref bufferWriter, 3279785213u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ClientSetLedStateRpc(state);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientSetLedStateRpc(LedState state)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1627947148u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in state, default(FastBufferWriter.ForEnums));
				__endSendRpc(ref bufferWriter, 1627947148u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				ApplyState(state);
			}
		}

		private void ClientSetLedState(LedState state)
		{
			ApplyState(state);
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			if (_isLidOpen.Value)
			{
				_animator.SetTrigger("OpenLid");
			}
			_riceInsideObj.SetActive(_hasRice.Value);
			NetworkVariable<bool> isLidOpen = _isLidOpen;
			isLidOpen.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isLidOpen.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnLidOpenChanged));
			NetworkVariable<bool> hasRice = _hasRice;
			hasRice.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(hasRice.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnHasRiceChanged));
			NetworkVariable<bool> isCooked = _isCooked;
			isCooked.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isCooked.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnCookedChanged));
			NetworkVariable<bool> isCooking = _isCooking;
			isCooking.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isCooking.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnCookingChanged));
			_objToolTipGO.SetActive(value: false);
			ServerRecoverStuckCookingIfNeeded("OnNetworkSpawn");
		}

		public override void OnNetworkDespawn()
		{
			NetworkVariable<bool> isLidOpen = _isLidOpen;
			isLidOpen.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isLidOpen.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnLidOpenChanged));
			NetworkVariable<bool> hasRice = _hasRice;
			hasRice.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(hasRice.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnHasRiceChanged));
			NetworkVariable<bool> isCooked = _isCooked;
			isCooked.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isCooked.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnCookedChanged));
			NetworkVariable<bool> isCooking = _isCooking;
			isCooking.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isCooking.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnCookingChanged));
			base.OnNetworkDespawn();
		}

		private void Update()
		{
			if (base.IsServer && !(Time.time < _nextCookingRecoveryCheckAt))
			{
				_nextCookingRecoveryCheckAt = Time.time + 1f;
				ServerRecoverStuckCookingIfNeeded("Watchdog");
			}
		}

		private void ServerRecoverStuckCookingIfNeeded(string source)
		{
			if (base.IsServer && !StaffLocked && _isCooking.Value && _cookingRoutine == null)
			{
				if (!_hasRice.Value || _isCooked.Value)
				{
					_isCooking.Value = false;
					_cookingTimeRemaining = 0;
					UtilityMethods.Log("[RiceCooker] Invalid cooking state repaired. Source=" + source, LogType.Warning);
				}
				else
				{
					_cookingRoutine = StartCoroutine(ServerCookRoutine(consumeRawRiceStock: false));
					UtilityMethods.Log("[RiceCooker] Stuck cooking session resumed. Source=" + source, LogType.Warning);
				}
			}
		}

		protected override void ClientMouse1(Interactor interactor, InputActionPhase phase)
		{
			if (!StaffLocked && phase == InputActionPhase.Started && !(Singleton<QuickSlotHandler>.Instance?.SelectedItem != null))
			{
				if (_isCooking.Value)
				{
					BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(interactor.OwnerClientId, RpcTargetUse.Temp);
					ClientShowLocalizedNotifyRpc("notification.rice_cooker.already_cooking", "Already cooking", baseRpcTarget);
				}
				else if (_hasRice.Value && !_isCooked.Value)
				{
					ServerTryStartCookRpc(interactor.NetworkObject.ToReference());
				}
				else if (!_isLidOpen.Value)
				{
					UtilityMethods.Log("ClientMouse1: opening lid.");
					ServerSetLidStateRpc(value: true);
				}
				else
				{
					UtilityMethods.Log("ClientMouse1: closing lid.");
					ServerSetLidStateRpc(value: false);
				}
			}
		}

		protected override void ClientCrossMouse1(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			if (StaffLocked || phase != InputActionPhase.Started || interactor == null || interactor.NetworkObject == null || held == null || held.ItemSO == null)
			{
				return;
			}
			BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(interactor.OwnerClientId, RpcTargetUse.Temp);
			if (_isCooking.Value)
			{
				ClientShowLocalizedNotifyRpc("notification.rice_cooker.already_cooking", "Already cooking", baseRpcTarget);
			}
			else if (_hasRice.Value && _isCooked.Value)
			{
				ServerToggleLidStateRpc();
			}
			else if (_hasRice.Value)
			{
				ServerTryStartCookRpc(interactor.NetworkObject.ToReference());
			}
			else if (!_isLidOpen.Value)
			{
				UtilityMethods.Log("ClientCrossMouse1: lid was closed, opening lid.");
				ServerSetLidStateRpc(value: true);
			}
			else if (IsWholesaleRice(held))
			{
				if (!(held.NetworkObject == null))
				{
					ServerTryPutWholesaleRiceRpc(interactor.NetworkObject.ToReference(), held.NetworkObject.ToReference());
				}
			}
			else if (IsRawRiceIngredient(held))
			{
				if (!(held.NetworkObject == null))
				{
					ServerTryPutRawRiceRpc(interactor.NetworkObject.ToReference(), held.NetworkObject.ToReference());
				}
			}
			else
			{
				ServerSetLidStateRpc(value: false);
			}
		}

		protected override void ClientMouse2(Interactor interactor, InputActionPhase phase)
		{
			if (!StaffLocked && phase == InputActionPhase.Started)
			{
				BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(interactor.OwnerClientId, RpcTargetUse.Temp);
				if (_isCooking.Value)
				{
					ClientShowLocalizedNotifyRpc("notification.rice_cooker.already_cooking", "Already cooking", baseRpcTarget);
				}
				else if (!_hasRice.Value)
				{
					ClientShowLocalizedNotifyRpc("notification.rice_cooker.machine_empty", "Machine is empty", baseRpcTarget);
				}
				else if (!_isLidOpen.Value)
				{
					ClientShowLocalizedNotifyRpc("notification.rice_cooker.open_lid", "Open the lid", baseRpcTarget);
				}
				else
				{
					ServerGiveRpc(interactor.NetworkObject.ToReference());
				}
			}
		}

		protected override void ClientCrossMouse2(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			if (StaffLocked || phase != InputActionPhase.Started || interactor == null || interactor.NetworkObject == null || !IsRawRiceForCooker(held))
			{
				return;
			}
			BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(interactor.OwnerClientId, RpcTargetUse.Temp);
			if (_isCooking.Value)
			{
				ClientShowLocalizedNotifyRpc("notification.rice_cooker.already_cooking", "Already cooking", baseRpcTarget);
				return;
			}
			if (!_hasRice.Value)
			{
				ClientShowLocalizedNotifyRpc("notification.rice_cooker.machine_empty", "Machine is empty", baseRpcTarget);
				return;
			}
			if (!_isCooked.Value)
			{
				ClientShowLocalizedNotifyRpc("notification.rice_cooker.rice_not_cooked", "Rice is not cooked yet", baseRpcTarget);
				return;
			}
			if (!_isLidOpen.Value)
			{
				ClientShowLocalizedNotifyRpc("notification.rice_cooker.open_lid", "Open the lid", baseRpcTarget);
				return;
			}
			ProcessedIngredientSO cookedRiceSO = GetCookedRiceSO();
			if (cookedRiceSO == null)
			{
				ClientShowLocalizedNotifyRpc("notification.rice_cooker.cooked_rice_not_configured", "Cooked rice is not configured", baseRpcTarget);
			}
			else if (Singleton<QuickSlotHandler>.Instance == null || !Singleton<QuickSlotHandler>.Instance.CanHoldSlot(cookedRiceSO))
			{
				ClientShowLocalizedNotifyRpc("notification.rice_cooker.slots_full", "Slots are full", baseRpcTarget);
			}
			else
			{
				ServerGiveToQuickSlotWithoutSelectingRpc(interactor.NetworkObject.ToReference());
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerTryStartCookRpc(NetworkObjectReference interactorRef)
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
				FastBufferWriter bufferWriter = __beginSendRpc(703495045u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 703495045u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (StaffLocked)
			{
				return;
			}
			if (!interactorRef.TryGetComponent<Interactor>(out var component))
			{
				UtilityMethods.Log("[RiceCooker] ServerTryStartCookRpc failed.");
				return;
			}
			BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(component.OwnerClientId, RpcTargetUse.Temp);
			if (_isCooking.Value)
			{
				ClientShowLocalizedNotifyRpc("notification.rice_cooker.already_cooking", "Already cooking", baseRpcTarget);
				return;
			}
			if (!_hasRice.Value)
			{
				ClientShowLocalizedNotifyRpc("notification.rice_cooker.machine_empty", "Machine is empty", baseRpcTarget);
				return;
			}
			if (_isCooked.Value)
			{
				ClientShowLocalizedNotifyRpc("notification.rice_cooker.rice_already_cooked", "Rice is already cooked", baseRpcTarget);
				return;
			}
			if (_isLidOpen.Value)
			{
				_isLidOpen.Value = false;
			}
			ServerStartCookInternal(component.OwnerClientId);
		}

		private void ServerStartCookInternal(ulong cookingClientId)
		{
			if (_isCooking.Value || !_hasRice.Value || _isCooked.Value)
			{
				UtilityMethods.Log("[RiceCooker] Start blocked. " + (_isCooking.Value ? "Already cooking. " : "") + ((!_hasRice.Value) ? "Machine is empty. " : "") + (_isCooked.Value ? "Rice is already cooked." : ""));
				return;
			}
			_cookingClientId = cookingClientId;
			_cookingRoutine = StartCoroutine(ServerCookRoutine());
			QuestReferenceHub.Instance.QuestComplete(QuestId.StartTheRiceCooker);
		}

		[Rpc(SendTo.Server)]
		private void ServerGiveRpc(NetworkObjectReference interactorRef)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3732537284u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3732537284u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (StaffLocked)
			{
				return;
			}
			if (!interactorRef.TryGetComponent<Interactor>(out var component))
			{
				UtilityMethods.Log("[RiceCooker] ServerGiveRpc failed.");
				return;
			}
			if (_cookingRoutine != null)
			{
				StopCoroutine(_cookingRoutine);
				_cookingRoutine = null;
			}
			NetworkObject networkObject = UnityEngine.Object.Instantiate(_isCooked.Value ? _rawRiceSO.ProcessedIngredients[0].Prefab : _rawRiceSO.Prefab);
			networkObject.Spawn(destroyWithScene: true);
			component.ServerHoldRpc(networkObject.ToReference());
			_hasRice.Value = false;
			_isCooked.Value = false;
			ClientPlayRiceCookerFeedbackRpc(2);
			QuestReferenceHub.Instance.QuestComplete(QuestId.TakeCookedRice);
		}

		[Rpc(SendTo.Server)]
		private void ServerGiveToQuickSlotWithoutSelectingRpc(NetworkObjectReference interactorRef)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3889796143u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3889796143u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (StaffLocked)
			{
				return;
			}
			if (!interactorRef.TryGetComponent<Interactor>(out var component))
			{
				UtilityMethods.Log("[RiceCooker] ServerGiveToQuickSlotWithoutSelectingRpc failed.");
				return;
			}
			BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(component.OwnerClientId, RpcTargetUse.Temp);
			if (_isCooking.Value)
			{
				ClientShowLocalizedNotifyRpc("notification.rice_cooker.already_cooking", "Already cooking", baseRpcTarget);
			}
			else if (!_hasRice.Value)
			{
				ClientShowLocalizedNotifyRpc("notification.rice_cooker.machine_empty", "Machine is empty", baseRpcTarget);
			}
			else if (!_isCooked.Value)
			{
				ClientShowLocalizedNotifyRpc("notification.rice_cooker.rice_not_cooked", "Rice is not cooked yet", baseRpcTarget);
			}
			else if (!_isLidOpen.Value)
			{
				ClientShowLocalizedNotifyRpc("notification.rice_cooker.open_lid", "Open the lid", baseRpcTarget);
			}
			else
			{
				if (_isCooking.Value)
				{
					return;
				}
				ProcessedIngredientSO cookedRiceSO = GetCookedRiceSO();
				if (cookedRiceSO == null || cookedRiceSO.Prefab == null)
				{
					ClientShowLocalizedNotifyRpc("notification.rice_cooker.cooked_rice_not_configured", "Cooked rice is not configured", baseRpcTarget);
					return;
				}
				NetworkObject networkObject = UnityEngine.Object.Instantiate(cookedRiceSO.Prefab);
				networkObject.Spawn(destroyWithScene: true);
				if (networkObject.TryGetComponent<BaseHoldable>(out var component2))
				{
					component2.ServerLazyHold(component.OwnerClientId, component.NetworkObject.ToReference(), select: false, hold: true);
					_hasRice.Value = false;
					_isCooked.Value = false;
					ClientPlayRiceCookerFeedbackRpc(2);
					QuestReferenceHub.Instance.QuestComplete(QuestId.TakeCookedRice);
				}
				else
				{
					UtilityMethods.Log("[RiceCooker] Cooked rice prefab is missing BaseHoldable.", LogType.Warning);
					networkObject.Despawn();
				}
			}
		}

		protected override bool CanCrossTrigger(Interactor interactor, BaseHoldable held)
		{
			if (!base.CanCrossTrigger(interactor, held))
			{
				return false;
			}
			if (interactor == null)
			{
				return false;
			}
			if (held == null || held.ItemSO == null)
			{
				return false;
			}
			BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(interactor.OwnerClientId, RpcTargetUse.Temp);
			if (_isCooking.Value)
			{
				ClientShowLocalizedNotifyRpc("notification.rice_cooker.already_cooking", "Already cooking", baseRpcTarget);
				return false;
			}
			return true;
		}

		[Rpc(SendTo.Server)]
		private void ServerTryPutRawRiceRpc(NetworkObjectReference interactorRef, NetworkObjectReference heldRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2625608952u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in heldRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 2625608952u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!StaffLocked)
			{
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				RpcParams rpcParams3 = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
				if (!interactorRef.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != senderClientId || !heldRef.TryGetComponent<BaseHoldable>(out var component2) || component2.OwnerClientId != senderClientId || component2.CanHold.Value || (object)component.CurrentHeld != component2)
				{
					UtilityMethods.Log("[RiceCooker] Raw rice put failed. Expected held item could not be validated.", LogType.Warning);
					return;
				}
				if (_isCooking.Value)
				{
					ClientShowLocalizedNotifyRpc("notification.rice_cooker.already_cooking", "Already cooking", rpcParams3);
					return;
				}
				if (_hasRice.Value)
				{
					ClientShowLocalizedNotifyRpc(_isCooked.Value ? "notification.rice_cooker.cooked_rice_inside" : "notification.rice_cooker.rice_already_inside", _isCooked.Value ? "Cooked rice is inside" : "Rice is already inside", rpcParams3);
					return;
				}
				if (!_isLidOpen.Value)
				{
					ClientShowLocalizedNotifyRpc("notification.rice_cooker.open_lid", "Open the lid", rpcParams3);
					return;
				}
				if (!IsRawRiceIngredient(component2))
				{
					ClientShowLocalizedNotifyRpc("notification.rice_cooker.only_raw_rice", "Only raw rice", rpcParams3);
					return;
				}
				ServerSetRiceStateInternal(value: true);
				component.ClientDestroyHeldItemRpc(heldRef);
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerTryPutWholesaleRiceRpc(NetworkObjectReference interactorRef, NetworkObjectReference wholesaleRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3406068050u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in wholesaleRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3406068050u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!StaffLocked)
			{
				ulong senderClientId = rpcParams.Receive.SenderClientId;
				RpcParams rpcParams3 = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
				NetworkObject networkObject2;
				WholesaleEntity component2;
				byte consumedAmount;
				if (!interactorRef.TryGet(out var networkObject) || !networkObject.TryGetComponent<Interactor>(out var component))
				{
					UtilityMethods.Log("<color=yellow>[RiceCooker] Wholesale rice put failed. Interactor reference is invalid.</color>");
				}
				else if (component.OwnerClientId != senderClientId)
				{
					UtilityMethods.Log("<color=yellow>[RiceCooker] Wholesale rice put failed. Sender does not own the interactor.</color>");
				}
				else if (_isCooking.Value)
				{
					ClientShowLocalizedNotifyRpc("notification.rice_cooker.already_cooking", "Already cooking", rpcParams3);
				}
				else if (_hasRice.Value)
				{
					ClientShowLocalizedNotifyRpc(_isCooked.Value ? "notification.rice_cooker.cooked_rice_inside" : "notification.rice_cooker.rice_already_inside", _isCooked.Value ? "Cooked rice is inside" : "Rice is already inside", rpcParams3);
				}
				else if (!_isLidOpen.Value)
				{
					_isLidOpen.Value = true;
				}
				else if (!wholesaleRef.TryGet(out networkObject2) || !networkObject2.TryGetComponent<WholesaleEntity>(out component2))
				{
					UtilityMethods.Log("<color=yellow>[RiceCooker] Wholesale rice put failed. Wholesale reference is invalid.</color>");
				}
				else if (!IsWholesaleRice(component2))
				{
					ClientShowLocalizedNotifyRpc("notification.rice_cooker.only_raw_rice", "Only raw rice", rpcParams3);
				}
				else if (component2.CurrentCount <= 0)
				{
					ClientShowLocalizedNotifyRpc("notification.rice_cooker.pack_empty", "The pack is empty", rpcParams3);
				}
				else if (!component2.ServerTryConsumeAmount(1, out consumedAmount) || consumedAmount <= 0)
				{
					ClientShowLocalizedNotifyRpc("notification.rice_cooker.pack_empty", "The pack is empty", rpcParams3);
				}
				else
				{
					ServerSetRiceStateInternal(value: true);
				}
			}
		}

		private void ServerSetRiceStateInternal(bool value)
		{
			bool flag = _hasRice.Value != value;
			_hasRice.Value = value;
			if (value & flag)
			{
				ClientPlayRiceCookerFeedbackRpc(1);
				QuestReferenceHub.Instance.QuestComplete(QuestId.AddRiceToRiceCooker);
			}
		}

		private IEnumerator ServerCookRoutine(bool consumeRawRiceStock = true)
		{
			UtilityMethods.Log("RiceCooker: cooking started.");
			_isLidOpen.Value = false;
			_isCooking.Value = true;
			_cookingTimeRemaining = _cookingTime;
			RestaurantManager restaurantManager = Singleton<GameProgressManager>.Instance.RestaurantManager;
			if (consumeRawRiceStock)
			{
				restaurantManager.ServerRemoveStockRpc(_rawRiceSO.Id, 1);
			}
			ClientStartCookingSoundRpc();
			float startTime = Time.time;
			float timeout = (float)(int)_cookingTime + 5f;
			while ((float)(int)_cookingTimeRemaining > 0f)
			{
				yield return new WaitForSeconds(1f);
				_cookingTimeRemaining--;
				UtilityMethods.Log($"RiceCooker: cooking... {_cookingTimeRemaining} seconds remaining.");
				if (Time.time - startTime > timeout)
				{
					UtilityMethods.Log("RiceCooker: cooking timeout occurred. Forcing completion.");
					break;
				}
			}
			_isCooked.Value = true;
			_isCooking.Value = false;
			_cookingTimeRemaining = 0;
			_cookingRoutine = null;
			ClientStopCookingSoundRpc();
			restaurantManager.ServerAddStockRpc(_rawRiceSO.ProcessedIngredients[0].Id, 1);
			Singleton<GameProgressManager>.Instance.ServerGrantProgressionXp(ProgressionXpSource.RiceCooked, _rawRiceSO.ProcessedIngredients[0].Id, (_cookingClientId == ulong.MaxValue) ? ((ulong?)null) : new ulong?(_cookingClientId));
			UtilityMethods.Log("RiceCooker: cooking finished.");
		}

		[Rpc(SendTo.Server)]
		private void ServerSetLidStateRpc(bool value)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1430444641u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1430444641u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!StaffLocked && !_isCooking.Value)
				{
					_isLidOpen.Value = value;
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerToggleLidStateRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(602093955u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 602093955u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!StaffLocked && !_isCooking.Value)
				{
					_isLidOpen.Value = !_isLidOpen.Value;
				}
			}
		}

		private void OnLidOpenChanged(bool prev, bool current)
		{
			if (current)
			{
				if (_isCooked.Value)
				{
					_oneShotAudioSource.PlayOneShot(_lidOpenWithCookedRiceAudioClip);
					_particleSystem.Play();
				}
				else
				{
					_oneShotAudioSource.PlayOneShot(_lidOpenAudioClip);
				}
				_animator.SetTrigger("OpenLid");
			}
			else
			{
				_oneShotAudioSource.PlayOneShot(_lidCloseAudioClip);
				_animator.SetTrigger("CloseLid");
			}
			RefreshGazeFeedback();
		}

		private void OnHasRiceChanged(bool prev, bool current)
		{
			if (current)
			{
				_riceInsideObj.SetActive(value: true);
				ClientSetLedState(LedState.HasRaw);
			}
			else
			{
				_riceInsideObj.SetActive(value: false);
				ClientSetLedState(LedState.Empty);
			}
			RefreshGazeFeedback();
		}

		private void OnCookingChanged(bool prev, bool current)
		{
			RefreshGazeFeedback();
		}

		private void OnCookedChanged(bool prev, bool current)
		{
			RefreshGazeFeedback();
		}

		[Rpc(SendTo.Everyone)]
		private void ClientStartCookingSoundRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3597395618u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3597395618u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_loopAudioSource.clip = _cookingAudioClip;
				_loopAudioSource.loop = true;
				_loopAudioSource.Play();
				ClientSetLedState(LedState.CookingBlink);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientStopCookingSoundRpc(bool completed = true)
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
				FastBufferWriter bufferWriter = __beginSendRpc(2148858804u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in completed, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2148858804u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_loopAudioSource.Stop();
				if (completed)
				{
					_oneShotAudioSource.PlayOneShot(_cookingFinshAudioClip);
				}
				ClientSetLedState((!completed) ? LedState.Empty : LedState.Cooked);
			}
		}

		private static bool IsWholesaleRice(BaseHoldable held)
		{
			if (held == null || held.ItemSO == null)
			{
				return false;
			}
			string text = Singleton<GameProgressManager>.Instance?.ItemDatabaseSO?.WholesaleRice?.Id;
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}
			return held.ItemSO.Id == text;
		}

		private static bool IsRawRiceIngredient(BaseHoldable held)
		{
			if (held == null || held.ItemSO == null)
			{
				return false;
			}
			if (!(held.ItemSO is IngredientSO ingredientSO))
			{
				return false;
			}
			if (ingredientSO.ItemType == ItemType.RawIngredient)
			{
				return ingredientSO.IngredientType == IngredientType.Rice;
			}
			return false;
		}

		private static bool IsRawRiceForCooker(BaseHoldable held)
		{
			if (!IsRawRiceIngredient(held))
			{
				return IsWholesaleRice(held);
			}
			return true;
		}

		private ProcessedIngredientSO GetCookedRiceSO()
		{
			if (_rawRiceSO == null || _rawRiceSO.ProcessedIngredients == null || _rawRiceSO.ProcessedIngredients.Count == 0)
			{
				return null;
			}
			return _rawRiceSO.ProcessedIngredients[0];
		}

		public override void GazeEnter()
		{
			base.GazeEnter();
			_objToolTipGO.SetActive(value: true);
		}

		public override void GazeExit()
		{
			base.GazeExit();
			_objToolTipGO.SetActive(value: false);
		}

		public override void EnableGazeHints()
		{
			Singleton<HintsManager>.Instance.ClearAllHints();
			if (_isCooking.Value)
			{
				_objToolTipGO.SetActive(value: true);
			}
			else if (_hasRice.Value)
			{
				if (_isCooked.Value)
				{
					Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", GetLidHintKey());
					if (_isLidOpen.Value)
					{
						Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_take");
					}
				}
				else
				{
					Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_start");
					if (_isLidOpen.Value)
					{
						Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_take");
					}
				}
				_objToolTipGO.SetActive(value: true);
			}
			else if (!_isLidOpen.Value)
			{
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_openlid");
				_objToolTipGO.SetActive(value: true);
			}
			else if (IsHoldingRawRice())
			{
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_put");
				_objToolTipGO.SetActive(value: true);
			}
			else
			{
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_closelid");
				_objToolTipGO.SetActive(value: true);
			}
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance.ClearAllHints();
		}

		private bool IsHoldingRawRice()
		{
			BaseHoldable held = Singleton<QuickSlotHandler>.Instance?.SelectedItem;
			if (!IsRawRiceIngredient(held))
			{
				return IsWholesaleRice(held);
			}
			return true;
		}

		private string GetLidHintKey()
		{
			if (!_isLidOpen.Value)
			{
				return "key_openlid";
			}
			return "key_closelid";
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientShowLocalizedNotifyRpc(FixedString64Bytes localizationKey, FixedString128Bytes fallback, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(4037710042u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in localizationKey, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in fallback, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 4037710042u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!(Singleton<NotifyManager>.Instance == null))
				{
					string message = LocalizationDatabase.GetString("UiTable", localizationKey.ToString(), fallback.ToString());
					Singleton<NotifyManager>.Instance.ShowToast(message);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientPlayRiceCookerFeedbackRpc(byte feedbackId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(610179221u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in feedbackId, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 610179221u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				switch ((RiceCookerFeedbackId)feedbackId)
				{
				case RiceCookerFeedbackId.PutRice:
					_oneShotAudioSource.PlayOneShot(_putRiceAudioClip);
					break;
				case RiceCookerFeedbackId.TakeRice:
					_oneShotAudioSource.PlayOneShot(_takeRiceAudioClip);
					break;
				}
			}
		}

		public bool ServerStaffCanBegin(int id)
		{
			if (base.IsServer && base.IsSpawned && id > 0 && _staffEmployee.Value == id && !_hasRice.Value)
			{
				return !_isCooking.Value;
			}
			return false;
		}

		public bool ServerStaffClaim(int id)
		{
			if (id <= 0 || !ServerStaffAvailable)
			{
				return false;
			}
			_staffEmployee.Value = id;
			return true;
		}

		public void ServerStaffBegin(int id)
		{
			if (base.IsServer && _staffEmployee.Value == id && !_isCooking.Value)
			{
				_hasRice.Value = true;
				_isLidOpen.Value = false;
				_isCooking.Value = true;
				ClientStartCookingSoundRpc();
			}
		}

		public void ServerStaffRelease(int id, bool completed = false)
		{
			if (this.CanWriteServerState() && _staffEmployee.Value == id)
			{
				bool value = _isCooking.Value;
				_isCooking.Value = false;
				_isCooked.Value = false;
				_hasRice.Value = false;
				if (value)
				{
					_isLidOpen.Value = true;
				}
				_staffEmployee.Value = 0;
				if (value)
				{
					ClientStopCookingSoundRpc(completed);
				}
			}
		}

		protected override void __initializeVariables()
		{
			if (_isLidOpen == null)
			{
				throw new Exception("RiceCooker._isLidOpen cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isLidOpen.Initialize(this);
			__nameNetworkVariable(_isLidOpen, "_isLidOpen");
			NetworkVariableFields.Add(_isLidOpen);
			if (_hasRice == null)
			{
				throw new Exception("RiceCooker._hasRice cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_hasRice.Initialize(this);
			__nameNetworkVariable(_hasRice, "_hasRice");
			NetworkVariableFields.Add(_hasRice);
			if (_isCooked == null)
			{
				throw new Exception("RiceCooker._isCooked cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isCooked.Initialize(this);
			__nameNetworkVariable(_isCooked, "_isCooked");
			NetworkVariableFields.Add(_isCooked);
			if (_isCooking == null)
			{
				throw new Exception("RiceCooker._isCooking cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isCooking.Initialize(this);
			__nameNetworkVariable(_isCooking, "_isCooking");
			NetworkVariableFields.Add(_isCooking);
			if (_staffEmployee == null)
			{
				throw new Exception("RiceCooker._staffEmployee cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_staffEmployee.Initialize(this);
			__nameNetworkVariable(_staffEmployee, "_staffEmployee");
			NetworkVariableFields.Add(_staffEmployee);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3279785213u, __rpc_handler_3279785213, "ServerSetLedStateRpc", RpcInvokePermission.Everyone);
			__registerRpc(1627947148u, __rpc_handler_1627947148, "ClientSetLedStateRpc", RpcInvokePermission.Everyone);
			__registerRpc(703495045u, __rpc_handler_703495045, "ServerTryStartCookRpc", RpcInvokePermission.Everyone);
			__registerRpc(3732537284u, __rpc_handler_3732537284, "ServerGiveRpc", RpcInvokePermission.Everyone);
			__registerRpc(3889796143u, __rpc_handler_3889796143, "ServerGiveToQuickSlotWithoutSelectingRpc", RpcInvokePermission.Everyone);
			__registerRpc(2625608952u, __rpc_handler_2625608952, "ServerTryPutRawRiceRpc", RpcInvokePermission.Everyone);
			__registerRpc(3406068050u, __rpc_handler_3406068050, "ServerTryPutWholesaleRiceRpc", RpcInvokePermission.Everyone);
			__registerRpc(1430444641u, __rpc_handler_1430444641, "ServerSetLidStateRpc", RpcInvokePermission.Everyone);
			__registerRpc(602093955u, __rpc_handler_602093955, "ServerToggleLidStateRpc", RpcInvokePermission.Everyone);
			__registerRpc(3597395618u, __rpc_handler_3597395618, "ClientStartCookingSoundRpc", RpcInvokePermission.Everyone);
			__registerRpc(2148858804u, __rpc_handler_2148858804, "ClientStopCookingSoundRpc", RpcInvokePermission.Everyone);
			__registerRpc(4037710042u, __rpc_handler_4037710042, "ClientShowLocalizedNotifyRpc", RpcInvokePermission.Everyone);
			__registerRpc(610179221u, __rpc_handler_610179221, "ClientPlayRiceCookerFeedbackRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3279785213(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out LedState value, default(FastBufferWriter.ForEnums));
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RiceCooker)target).ServerSetLedStateRpc(value);
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1627947148(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out LedState value, default(FastBufferWriter.ForEnums));
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RiceCooker)target).ClientSetLedStateRpc(value);
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_703495045(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RiceCooker)target).ServerTryStartCookRpc(value);
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3732537284(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RiceCooker)target).ServerGiveRpc(value);
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3889796143(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RiceCooker)target).ServerGiveToQuickSlotWithoutSelectingRpc(value);
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2625608952(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RiceCooker)target).ServerTryPutRawRiceRpc(value, value2, ext);
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3406068050(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RiceCooker)target).ServerTryPutWholesaleRiceRpc(value, value2, ext);
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1430444641(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RiceCooker)target).ServerSetLidStateRpc(value);
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_602093955(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RiceCooker)target).ServerToggleLidStateRpc();
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3597395618(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RiceCooker)target).ClientStartCookingSoundRpc();
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2148858804(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RiceCooker)target).ClientStopCookingSoundRpc(value);
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4037710042(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out FixedString128Bytes value2, default(FastBufferWriter.ForFixedStrings));
				RpcParams ext = rpcParams.Ext;
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RiceCooker)target).ClientShowLocalizedNotifyRpc(value, value2, ext);
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_610179221(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RiceCooker)target).ClientPlayRiceCookerFeedbackRpc(value);
				((RiceCooker)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "RiceCooker";
		}
	}
}
