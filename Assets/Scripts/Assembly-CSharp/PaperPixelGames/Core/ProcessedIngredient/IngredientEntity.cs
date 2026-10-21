using System;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Indicator;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.ProcessedIngredient
{
	public class IngredientEntity : BaseHoldable, ISaveable<IngredientData>
	{
		[SerializeField]
		public bool _isPlaceableOnChefDesk;

		private NetworkVariable<byte> _ingredientCount = new NetworkVariable<byte>(0);

		protected NetworkVariable<byte> IngredientCountState => _ingredientCount;

		public byte IngredientCount
		{
			get
			{
				return _ingredientCount.Value;
			}
			set
			{
				byte value2 = _ingredientCount.Value;
				if (value2 != value)
				{
					_ingredientCount.Value = value;
					if (base.IsServer)
					{
						OnServerIngredientCountChanged(value2, value);
					}
				}
			}
		}

		public bool IsPlaceableOnChefDesk => _isPlaceableOnChefDesk;

		public IngredientSO IngredientSO => base.ItemSO as IngredientSO;

		public override void EnableGazeHints()
		{
			if (!(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.ClearAllHints();
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_hold");
			}
		}

		public override void DisableGazeHints()
		{
			if (!(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
				Singleton<HintsManager>.Instance.RemoveHint("Interact");
			}
		}

		public override bool TryGetScanIndicatorVisualSettings(out OffscreenIndicatorVisualSettings settings)
		{
			if (IngredientSO == null)
			{
				return base.TryGetScanIndicatorVisualSettings(out settings);
			}
			settings = new OffscreenIndicatorVisualSettings
			{
				IconSprite = ((base.ItemSO != null) ? base.ItemSO.Sprite : null),
				ApplyDistanceVisible = true,
				DistanceVisible = false
			};
			return true;
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			if (base.IsServer)
			{
				Singleton<GameProgressManager>.Instance.RestaurantManager.RegisterIngredientData(this);
			}
			if (base.IsServer && TryResolveInitialIngredientCount(out var initialCount))
			{
				IngredientCount = initialCount;
			}
		}

		public override void OnNetworkDespawn()
		{
			if (base.IsServer)
			{
				Singleton<GameProgressManager>.Instance.RestaurantManager.DeRegisterIngredientData(this);
			}
			base.OnNetworkDespawn();
		}

		private ProcessedIngredientSO ResolveProcessedIngredientSO()
		{
			if (IngredientSO != null)
			{
				if (IngredientSO.ProcessedIngredients != null && IngredientSO.ProcessedIngredients.Count > 0)
				{
					return IngredientSO.ProcessedIngredients[0];
				}
				UtilityMethods.Log("[IngredientEntity] IngredientSO '" + IngredientSO.name + "' has an empty ProcessedIngredients list.");
				return null;
			}
			return base.ItemSO as ProcessedIngredientSO;
		}

		protected virtual bool TryResolveInitialIngredientCount(out byte initialCount)
		{
			initialCount = 0;
			ProcessedIngredientSO processedIngredientSO = ResolveProcessedIngredientSO();
			if (processedIngredientSO == null)
			{
				return false;
			}
			initialCount = (byte)Mathf.Clamp(processedIngredientSO.OutputYield, 0, 255);
			return true;
		}

		protected virtual void OnServerIngredientCountChanged(byte previousCount, byte currentCount)
		{
		}

		[Rpc(SendTo.Server)]
		public void ServerConsumeIngredientsRpc(byte amount)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1859673380u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in amount, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1859673380u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && amount != 0)
				{
					int ingredientCount = IngredientCount;
					int num = Mathf.Max(0, ingredientCount - amount);
					IngredientCount = (byte)num;
				}
			}
		}

		public IngredientData Save()
		{
			if (base.ItemSO == null || string.IsNullOrEmpty(base.ItemSO.Id))
			{
				UtilityMethods.Log("[IngredientEntity] Save skipped. ItemSO is missing on " + base.name + ".", LogType.Warning);
				return null;
			}
			return new IngredientData
			{
				IngredientId = base.ItemSO.Id,
				TransformData = GetTransformData(),
				Quantity = IngredientCount
			};
		}

		public void Load(IngredientData data)
		{
			if (base.IsServer)
			{
				IngredientCount = data.Quantity;
			}
		}

		public TransformData GetTransformData()
		{
			return new TransformData
			{
				Position = base.transform.position,
				Rotation = base.transform.rotation.eulerAngles
			};
		}

		public override void EnableHeldHints()
		{
			Singleton<HintsManager>.Instance.ClearAllHints();
			Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_place");
			AddHeldThrowHint();
		}

		public override void DisableHeldHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("RightMouseMechanic");
			RemoveHeldThrowHint();
		}

		protected override void __initializeVariables()
		{
			if (_ingredientCount == null)
			{
				throw new Exception("IngredientEntity._ingredientCount cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_ingredientCount.Initialize(this);
			__nameNetworkVariable(_ingredientCount, "_ingredientCount");
			NetworkVariableFields.Add(_ingredientCount);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(1859673380u, __rpc_handler_1859673380, "ServerConsumeIngredientsRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_1859673380(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				((IngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((IngredientEntity)target).ServerConsumeIngredientsRpc(value);
				((IngredientEntity)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "IngredientEntity";
		}
	}
}
