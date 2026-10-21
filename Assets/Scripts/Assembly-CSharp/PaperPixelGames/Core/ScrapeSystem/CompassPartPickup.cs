using System;
using System.Collections.Generic;
using EPOOutline;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.ScrapeSystem
{
	public class CompassPartPickup : NetworkBehaviour, IBasicInteractable
	{
		[SerializeField]
		private Outlinable _outlinable;

		[SerializeField]
		[Min(1f)]
		private int _unlocksCompassLevel = 1;

		[Header("Gaze VFX")]
		[SerializeField]
		private List<ParticleSystem> _gazeVfx = new List<ParticleSystem>();

		private readonly NetworkVariable<bool> _isCollected = new NetworkVariable<bool>(value: false);

		private EventBinding<ToolDataEvent> _toolDataBinding;

		private bool _gazeVfxActivated;

		public bool CanGaze => !_isCollected.Value;

		public bool CanInteractable => !_isCollected.Value;

		private void Awake()
		{
			SetGazeVfxActive(active: false);
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<bool> isCollected = _isCollected;
			isCollected.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isCollected.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnCollectedChanged));
			if (base.IsServer)
			{
				_toolDataBinding = new EventBinding<ToolDataEvent>(OnToolDataChanged);
				EventBus<ToolDataEvent>.Register(_toolDataBinding);
				ServerRefreshCollectedState();
			}
			_outlinable.enabled = false;
			SetGazeVfxActive(active: false);
			ApplyCollectedState(_isCollected.Value);
		}

		public override void OnNetworkDespawn()
		{
			NetworkVariable<bool> isCollected = _isCollected;
			isCollected.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isCollected.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnCollectedChanged));
			if (base.IsServer && _toolDataBinding != null)
			{
				EventBus<ToolDataEvent>.Deregister(_toolDataBinding);
				_toolDataBinding = null;
			}
			SafeClearHintAndHighlight();
			base.OnNetworkDespawn();
		}

		private void OnToolDataChanged(ToolDataEvent @event)
		{
			ServerRefreshCollectedState(@event.ToolDataNet);
		}

		private void ServerRefreshCollectedState()
		{
			RpcRelay rpcRelay = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RpcRelay : null);
			if (!(rpcRelay == null))
			{
				ServerRefreshCollectedState(rpcRelay.ToolDataNV.Value);
			}
		}

		private void ServerRefreshCollectedState(ToolDataNet toolData)
		{
			if (base.IsServer)
			{
				bool flag = toolData.CompassUnlocked && toolData.CompassLevel >= _unlocksCompassLevel;
				if (_isCollected.Value != flag)
				{
					_isCollected.Value = flag;
				}
			}
		}

		private void OnCollectedChanged(bool wasCollected, bool isCollected)
		{
			ApplyCollectedState(isCollected);
		}

		private void ApplyCollectedState(bool isCollected)
		{
			bool flag = !isCollected;
			Renderer[] componentsInChildren = GetComponentsInChildren<Renderer>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].enabled = flag;
			}
			Collider[] componentsInChildren2 = GetComponentsInChildren<Collider>(includeInactive: true);
			for (int i = 0; i < componentsInChildren2.Length; i++)
			{
				componentsInChildren2[i].enabled = flag;
			}
			if (isCollected)
			{
				SetGazeVfxActive(active: false);
				_gazeVfxActivated = false;
				SafeClearHintAndHighlight();
			}
		}

		private void SafeClearHintAndHighlight()
		{
			if (Singleton<HintsManager>.Instance != null)
			{
				Singleton<HintsManager>.Instance.RemoveHint("Interact");
			}
			_outlinable.enabled = false;
		}

		public void GazeEnter()
		{
			if (!_isCollected.Value)
			{
				ActivateGazeVfx();
				Singleton<HintsManager>.Instance.AddHint("Interact", "key_take");
				if (_outlinable != null)
				{
					Singleton<HighlightManager>.Instance.SetHighlight(_outlinable);
				}
			}
		}

		private void ActivateGazeVfx()
		{
			if (!_gazeVfxActivated)
			{
				_gazeVfxActivated = true;
				SetGazeVfxActive(active: true);
			}
		}

		private void SetGazeVfxActive(bool active)
		{
			for (int i = 0; i < _gazeVfx.Count; i++)
			{
				ParticleSystem particleSystem = _gazeVfx[i];
				if (!(particleSystem == null))
				{
					particleSystem.gameObject.SetActive(active);
					if (active)
					{
						particleSystem.Play(withChildren: true);
					}
					else
					{
						particleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
					}
				}
			}
		}

		public void GazeExit()
		{
			Singleton<HintsManager>.Instance.RemoveHint("Interact");
			Singleton<HighlightManager>.Instance.ClearCurrentHighlight();
		}

		public void TriggerCrossInput(Interactor interactor, BaseHoldable held, InputKind inputKind, InputActionPhase phase)
		{
			TriggerInput(interactor, inputKind, phase);
		}

		public void TriggerInput(Interactor interactor, InputKind inputKind, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && base.IsSpawned && !_isCollected.Value && !(interactor == null) && !(interactor.NetworkObject == null))
			{
				ServerCollectCompassPartRpc(interactor.NetworkObject.ToReference());
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerCollectCompassPartRpc(NetworkObjectReference interactorRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2076519870u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 2076519870u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (_isCollected.Value)
			{
				return;
			}
			if (!interactorRef.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != rpcParams.Receive.SenderClientId)
			{
				UtilityMethods.Log($"[CompassPartPickup] Collect rejected. Invalid interactor. SenderClientId={rpcParams.Receive.SenderClientId}", LogType.Warning);
				return;
			}
			RpcRelay rpcRelay = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RpcRelay : null);
			if (!(rpcRelay == null))
			{
				if (!rpcRelay.ServerTryUpgradeCompassToLevel((byte)_unlocksCompassLevel))
				{
					UtilityMethods.Log($"[CompassPartPickup] Collect rejected. unlocksCompassLevel={_unlocksCompassLevel}", LogType.Warning);
					return;
				}
				CompleteCompassPartQuestIfNeeded();
				base.NetworkObject.Despawn();
			}
		}

		private void CompleteCompassPartQuestIfNeeded()
		{
			if (!(QuestReferenceHub.Instance == null))
			{
				switch (_unlocksCompassLevel)
				{
				case 2:
					QuestReferenceHub.Instance.QuestComplete(QuestId.FallowTheCompassSignal);
					break;
				case 3:
					QuestReferenceHub.Instance.QuestComplete(QuestId.FindTheNewCompassPiece);
					break;
				case 4:
					QuestReferenceHub.Instance.QuestComplete(QuestId.FollowTheCompassSignalAndFindTheFinalPiece);
					break;
				}
			}
		}

		protected override void __initializeVariables()
		{
			if (_isCollected == null)
			{
				throw new Exception("CompassPartPickup._isCollected cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isCollected.Initialize(this);
			__nameNetworkVariable(_isCollected, "_isCollected");
			NetworkVariableFields.Add(_isCollected);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(2076519870u, __rpc_handler_2076519870, "ServerCollectCompassPartRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_2076519870(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((CompassPartPickup)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((CompassPartPickup)target).ServerCollectCompassPartRpc(value, ext);
				((CompassPartPickup)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "CompassPartPickup";
		}
	}
}
