using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.PlayerTools;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Player
{
	public class PlayerInteraction : BaseInteractable
	{
		private const byte OxygenTransferAmount = 10;

		private const float ServerOxygenTransferMaxDistance = 4f;

		[SerializeField]
		private Collider _interactorCollider;

		public PPG_Player OwnerPlayer { get; set; }

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			_interactorCollider.enabled = !base.IsOwner;
		}

		public override void OnNetworkDespawn()
		{
			base.OnNetworkDespawn();
			base.gameObject.SetActive(!base.IsOwner);
		}

		public override void EnableGazeHints()
		{
			OwnerPlayer.SetContextPlayerNameVisible(visible: true);
			if (CanReceiveOxygen())
			{
				Singleton<HintsManager>.Instance.AddHint("Interact", "key_give_oxygen");
			}
		}

		public override void DisableGazeHints()
		{
			OwnerPlayer.SetContextPlayerNameVisible(visible: false);
			if (OwnerPlayer.IsUnderWater)
			{
				Singleton<HintsManager>.Instance.RemoveHint("Interact");
			}
		}

		protected override void ClientInteract(Interactor interactor, InputActionPhase phase)
		{
			base.ClientInteract(interactor, phase);
			if (phase == InputActionPhase.Started && CanReceiveOxygen())
			{
				ServerTryGiveOxygenRpc();
			}
		}

		private bool CanReceiveOxygen()
		{
			if (OwnerPlayer != null && OwnerPlayer.IsUnderWater && OwnerPlayer.DivingEquipment != null)
			{
				return !OwnerPlayer.DivingEquipment.HasLatchedPiranhas;
			}
			return false;
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
		private void ServerTryGiveOxygenRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1154586935u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1154586935u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsServer || OwnerPlayer == null || !OwnerPlayer.IsUnderWater)
			{
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (senderClientId == OwnerPlayer.OwnerClientId)
			{
				return;
			}
			DivingEquipment divingEquipment = OwnerPlayer.DivingEquipment;
			if (!(divingEquipment == null) && !divingEquipment.HasLatchedPiranhas && !(Singleton<PlayersManager>.Instance == null) && Singleton<PlayersManager>.Instance.TryGetPlayer(senderClientId, out var player) && !(player == null) && player.IsUnderWater && !(player.DivingEquipment == null))
			{
				float num = 16f;
				if (!((player.transform.position - OwnerPlayer.transform.position).sqrMagnitude > num))
				{
					player.DivingEquipment.ServerConsumeOxygenForPlayerTransfer(10);
					divingEquipment.ServerApplyOxygenFromPlayerTransfer(10);
				}
			}
		}

		protected override void ClientMouse1(Interactor interactor, InputActionPhase phase)
		{
			interactor.EmptyInputActions(InputKind.Mouse1, phase);
		}

		protected override void ClientMouse2(Interactor interactor, InputActionPhase phase)
		{
			interactor.EmptyInputActions(InputKind.Mouse2, phase);
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(1154586935u, __rpc_handler_1154586935, "ServerTryGiveOxygenRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_1154586935(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((PlayerInteraction)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((PlayerInteraction)target).ServerTryGiveOxygenRpc(ext);
				((PlayerInteraction)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "PlayerInteraction";
		}
	}
}
