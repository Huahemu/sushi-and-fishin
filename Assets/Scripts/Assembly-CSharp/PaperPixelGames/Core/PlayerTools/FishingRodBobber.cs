using System;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.PlayerTools
{
	public sealed class FishingRodBobber : BaseInteractable, IToolActiveInteractable
	{
		[SerializeField]
		private Transform _lineAttachment;

		[SerializeField]
		private Transform _presentationOffsetTarget;

		private readonly NetworkVariable<ulong> _rodOwnerClientId = new NetworkVariable<ulong>(ulong.MaxValue);

		private readonly NetworkVariable<uint> _sessionId = new NetworkVariable<uint>(0u);

		private FishingRod _rod;

		private FishingRod _presentationRod;

		private Vector3 _presentationOffset;

		private Vector3 _presentationOffsetRestLocalPosition;

		public Vector3 LineAttachmentPosition
		{
			get
			{
				if (!(_lineAttachment != null))
				{
					return base.transform.position + _presentationOffset;
				}
				return _lineAttachment.position + _presentationOffset;
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			if (_presentationOffsetTarget != null)
			{
				_presentationOffsetRestLocalPosition = _presentationOffsetTarget.localPosition;
			}
			TryBindPresentationRod();
		}

		public override void OnNetworkDespawn()
		{
			UnbindPresentationRod();
			if (_inGaze)
			{
				DisableGazeHints();
			}
			base.OnNetworkDespawn();
		}

		internal void InitializeServer(FishingRod rod, ulong rodOwnerClientId, uint sessionId)
		{
			_rod = rod;
			_rodOwnerClientId.Value = rodOwnerClientId;
			_sessionId.Value = sessionId;
		}

		private void LateUpdate()
		{
			if (base.IsSpawned && _presentationRod == null)
			{
				TryBindPresentationRod();
			}
			ApplyPresentationOffset();
		}

		internal void SetPresentationOffset(Vector3 worldOffset)
		{
			_presentationOffset = worldOffset;
		}

		private void ApplyPresentationOffset()
		{
			if (!(_presentationOffsetTarget == null))
			{
				Transform parent = _presentationOffsetTarget.parent;
				Vector3 vector = ((parent != null) ? parent.InverseTransformVector(_presentationOffset) : _presentationOffset);
				_presentationOffsetTarget.localPosition = _presentationOffsetRestLocalPosition + vector;
			}
		}

		private void TryBindPresentationRod()
		{
			FishingRod fishingRod = _rod;
			if (fishingRod == null && Singleton<PlayersManager>.Instance != null && Singleton<PlayersManager>.Instance.TryGetPlayer(_rodOwnerClientId.Value, out var player))
			{
				fishingRod = player.FishingRod;
			}
			if (!(fishingRod == null) && fishingRod.TryRegisterPresentationBobber(this, _rodOwnerClientId.Value, _sessionId.Value))
			{
				_presentationRod = fishingRod;
			}
		}

		private void UnbindPresentationRod()
		{
			if (!(_presentationRod == null))
			{
				_presentationRod.UnregisterPresentationBobber(this, _sessionId.Value);
				_presentationRod = null;
			}
		}

		protected override bool CanTrigger(Interactor interactor)
		{
			if (base.CanTrigger(interactor) && interactor != null && interactor.IsLocalPlayer)
			{
				return !IsLocalRodOwner();
			}
			return false;
		}

		protected override void ClientInteract(Interactor interactor, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && !(interactor == null) && interactor.IsLocalPlayer && !IsLocalRodOwner())
			{
				ServerPullBobberRpc(_sessionId.Value);
			}
		}

		private bool IsLocalRodOwner()
		{
			if (base.NetworkManager != null)
			{
				return base.NetworkManager.LocalClientId == _rodOwnerClientId.Value;
			}
			return false;
		}

		protected override void ClientCrossInteract(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			ClientInteract(interactor, phase);
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
		private void ServerPullBobberRpc(uint sessionId, RpcParams rpcParams = default(RpcParams))
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
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Everyone
				};
				FastBufferWriter bufferWriter = __beginSendRpc(1996610233u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, sessionId);
				__endSendRpc(ref bufferWriter, 1996610233u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (_rod == null)
				{
					UtilityMethods.Log("[FishingRod.Bobber] Pull rejected. " + $"Reason=RodUnavailable RodOwner={_rodOwnerClientId.Value} " + $"Interactor={rpcParams.Receive.SenderClientId} " + $"Session={sessionId}", LogType.Warning);
				}
				else
				{
					_rod.ServerTryPullBobber(this, sessionId, rpcParams.Receive.SenderClientId);
				}
			}
		}

		public override void EnableGazeHints()
		{
			if (!(Singleton<HintsManager>.Instance == null) && !(base.NetworkManager == null) && base.NetworkManager.LocalClientId != _rodOwnerClientId.Value)
			{
				Singleton<HintsManager>.Instance.AddHint("Interact", "key_pull_rod");
			}
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance?.RemoveHint("Interact");
		}

		protected override void __initializeVariables()
		{
			if (_rodOwnerClientId == null)
			{
				throw new Exception("FishingRodBobber._rodOwnerClientId cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_rodOwnerClientId.Initialize(this);
			__nameNetworkVariable(_rodOwnerClientId, "_rodOwnerClientId");
			NetworkVariableFields.Add(_rodOwnerClientId);
			if (_sessionId == null)
			{
				throw new Exception("FishingRodBobber._sessionId cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_sessionId.Initialize(this);
			__nameNetworkVariable(_sessionId, "_sessionId");
			NetworkVariableFields.Add(_sessionId);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(1996610233u, __rpc_handler_1996610233, "ServerPullBobberRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_1996610233(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out uint value);
				RpcParams ext = rpcParams.Ext;
				((FishingRodBobber)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((FishingRodBobber)target).ServerPullBobberRpc(value, ext);
				((FishingRodBobber)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "FishingRodBobber";
		}
	}
}
