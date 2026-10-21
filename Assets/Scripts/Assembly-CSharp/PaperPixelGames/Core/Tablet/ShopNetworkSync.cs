using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Tablet
{
	public class ShopNetworkSync : NetworkBehaviour
	{
		[SerializeField]
		private ItemDatabaseSO _itemDatabase;

		private EventBinding<SpawnWholesalerItemEvent> _spawnWholesalerItemEventBinding;

		public override void OnNetworkSpawn()
		{
			_spawnWholesalerItemEventBinding = new EventBinding<SpawnWholesalerItemEvent>(OnSpawnWholesalerItem);
			EventBus<SpawnWholesalerItemEvent>.Register(_spawnWholesalerItemEventBinding);
		}

		public override void OnNetworkDespawn()
		{
			EventBus<SpawnWholesalerItemEvent>.Deregister(_spawnWholesalerItemEventBinding);
		}

		private void OnSpawnWholesalerItem(SpawnWholesalerItemEvent @event)
		{
			ServerSpawnWholesalerItemRpc(@event.ItemId, @event.Position);
		}

		[Rpc(SendTo.Server)]
		private void ServerSpawnWholesalerItemRpc(string itemId, Vector3 position)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3806062636u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bool value = itemId != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(itemId);
				}
				bufferWriter.WriteValueSafe(in position);
				__endSendRpc(ref bufferWriter, 3806062636u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			ItemSO itemSO = _itemDatabase.FindById(itemId);
			if (!(itemSO == null) && itemSO is WholesaleSO wholesaleSO)
			{
				if (Singleton<GameProgressManager>.Instance != null && !Singleton<GameProgressManager>.Instance.IsWholesaleProgressionUnlocked(wholesaleSO))
				{
					UtilityMethods.Log("[ShopNetworkSync][Progression] Wholesale spawn rejected. WholesaleId=" + wholesaleSO.Id, LogType.Warning);
				}
				else
				{
					wholesaleSO.Prefab.TryInstantiateAndSpawn(out var _, position, Quaternion.identity);
				}
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3806062636u, __rpc_handler_3806062636, "ServerSpawnWholesalerItemRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3806062636(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
				reader.ReadValueSafe(out Vector3 value2);
				((ShopNetworkSync)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((ShopNetworkSync)target).ServerSpawnWholesalerItemRpc(s, value2);
				((ShopNetworkSync)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "ShopNetworkSync";
		}
	}
}
