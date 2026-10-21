using PaperPixelGames.Core.DailyReport;
using PaperPixelGames.Core.Economy;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Ocean;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentDeadFishBuyer : NetworkBehaviour
	{
		[SerializeField]
		private bool _canBuyDeadFish = true;

		[SerializeField]
		private bool _logSales;

		public bool CanBuyDeadFish => _canBuyDeadFish;

		public bool ServerTrySellDeadFish(ResidentController resident, Interactor interactor, DeadFish deadFish, ulong requesterClientId)
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (!_canBuyDeadFish)
			{
				UtilityMethods.Log("[ResidentDeadFishBuyer] Sale rejected. Buyer is disabled.", LogType.Warning);
				return false;
			}
			if (resident == null || interactor == null || deadFish == null)
			{
				UtilityMethods.Log("[ResidentDeadFishBuyer] Sale rejected. Missing resident, interactor, or fish.", LogType.Warning);
				return false;
			}
			if (interactor.OwnerClientId != requesterClientId)
			{
				UtilityMethods.Log("[ResidentDeadFishBuyer] Sale rejected. Sender does not own interactor.", LogType.Warning);
				return false;
			}
			if (deadFish.NetworkObject == null || !deadFish.NetworkObject.IsSpawned)
			{
				UtilityMethods.Log("[ResidentDeadFishBuyer] Sale rejected. DeadFish is not spawned.", LogType.Warning);
				return false;
			}
			if (deadFish.OwnerClientId != requesterClientId)
			{
				UtilityMethods.Log("[ResidentDeadFishBuyer] Sale rejected. Sender does not own DeadFish.", LogType.Warning);
				return false;
			}
			int num = CalculateSalePrice(deadFish);
			if (num <= 0)
			{
				ClientShowSaleRejectedRpc("This fish has no sale value.", base.RpcTarget.Single(requesterClientId, RpcTargetUse.Temp));
				return false;
			}
			if (!ServiceLocator.ForSceneOf(resident).TryGet<EconomyHandler>(out var service) || service == null)
			{
				UtilityMethods.Log("[ResidentDeadFishBuyer] Sale rejected. EconomyHandler not found.", LogType.Warning);
				return false;
			}
			service.EarnMoney(num, requesterClientId);
			if (ServiceLocator.ForSceneOf(resident).TryGet<DailyReportBroadcaster>(out var service2) && service2 != null)
			{
				ulong steamId = ((interactor.Player != null) ? interactor.Player.SteamID.Value : 0);
				service2.ServerRecordMoneyEarned(steamId, num);
			}
			NetworkObjectReference fishRef = deadFish.NetworkObject.ToReference();
			ClientFinalizeSaleRpc(fishRef, base.RpcTarget.Single(requesterClientId, RpcTargetUse.Temp));
			deadFish.NetworkObject.Despawn();
			resident.ServerPlayPurchaseExpression();
			resident.ServerPlayPurchaseWave();
			if (_logSales)
			{
				string arg = ((deadFish.FishSO != null) ? deadFish.FishSO.Name : deadFish.name);
				UtilityMethods.Log($"[ResidentDeadFishBuyer] Sold {arg}. Weight:{deadFish.Weight.Value:F2}kg Earned:${num}");
			}
			return true;
		}

		public int CalculateSalePrice(DeadFish deadFish)
		{
			if (deadFish == null || deadFish.FishSO == null)
			{
				return 0;
			}
			float num = Mathf.Max(0f, deadFish.Weight.Value);
			float num2 = Mathf.Max(0f, deadFish.FishSO.DeadFishSalePrice);
			return Mathf.Max(0, Mathf.RoundToInt(num * num2));
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientFinalizeSaleRpc(NetworkObjectReference fishRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2437981582u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in fishRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 2437981582u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (fishRef.TryGetComponent<DeadFish>(out var component) && Singleton<QuickSlotHandler>.Instance != null)
				{
					Singleton<QuickSlotHandler>.Instance.RemoveSpecificItem(component);
				}
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientShowSaleRejectedRpc(string message, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2664098201u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bool value = message != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(message);
				}
				__endSendRpc(ref bufferWriter, 2664098201u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Singleton<NotifyManager>.Instance?.ShowToast(message, NotificationState.Info, 1.5f);
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(2437981582u, __rpc_handler_2437981582, "ClientFinalizeSaleRpc", RpcInvokePermission.Everyone);
			__registerRpc(2664098201u, __rpc_handler_2664098201, "ClientShowSaleRejectedRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_2437981582(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((ResidentDeadFishBuyer)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentDeadFishBuyer)target).ClientFinalizeSaleRpc(value, ext);
				((ResidentDeadFishBuyer)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2664098201(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				string s = null;
				if (value)
				{
					reader.ReadValueSafe(out s, false);
				}
				RpcParams ext = rpcParams.Ext;
				((ResidentDeadFishBuyer)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ResidentDeadFishBuyer)target).ClientShowSaleRejectedRpc(s, ext);
				((ResidentDeadFishBuyer)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "ResidentDeadFishBuyer";
		}
	}
}
