using System;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Restaurant.Sign
{
	public class RestaurantSignManager : BaseUsable, ISaveable<RestaurantSignCustomizationData>
	{
		[SerializeField]
		private PhysicalStateController _physicalStateController;

		[SerializeField]
		private RestaurantSignCustomization _customization;

		[SerializeField]
		private Animator _animator;

		private NetworkVariable<FixedString128Bytes> _tableName = new NetworkVariable<FixedString128Bytes>();

		private NetworkVariable<Color> _tableNameColor = new NetworkVariable<Color>(Color.white);

		public string RestaurantName => _tableName.Value.ToString();

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			_customization.Initialize(this);
			NetworkVariable<FixedString128Bytes> tableName = _tableName;
			tableName.OnValueChanged = (NetworkVariable<FixedString128Bytes>.OnValueChangedDelegate)Delegate.Combine(tableName.OnValueChanged, new NetworkVariable<FixedString128Bytes>.OnValueChangedDelegate(OnTableNameChanged));
			NetworkVariable<Color> tableNameColor = _tableNameColor;
			tableNameColor.OnValueChanged = (NetworkVariable<Color>.OnValueChangedDelegate)Delegate.Combine(tableNameColor.OnValueChanged, new NetworkVariable<Color>.OnValueChangedDelegate(OnTableNameColorChanged));
			_physicalStateController.OnPhysicallyUsableChanged += HandlePhysicalRepair;
			ApplyAllInitialState();
		}

		public override void OnNetworkDespawn()
		{
			base.OnNetworkDespawn();
			NetworkVariable<FixedString128Bytes> tableName = _tableName;
			tableName.OnValueChanged = (NetworkVariable<FixedString128Bytes>.OnValueChangedDelegate)Delegate.Remove(tableName.OnValueChanged, new NetworkVariable<FixedString128Bytes>.OnValueChangedDelegate(OnTableNameChanged));
			NetworkVariable<Color> tableNameColor = _tableNameColor;
			tableNameColor.OnValueChanged = (NetworkVariable<Color>.OnValueChangedDelegate)Delegate.Remove(tableNameColor.OnValueChanged, new NetworkVariable<Color>.OnValueChangedDelegate(OnTableNameColorChanged));
			_physicalStateController.OnPhysicallyUsableChanged -= HandlePhysicalRepair;
		}

		private void ApplyAllInitialState()
		{
			ApplyPhysicalUsable(_physicalStateController.IsPhysicallyUsable);
			ApplyTableNameSafe(_tableName.Value);
			ApplyTableColorSafe(_tableNameColor.Value);
		}

		private void ApplyPhysicalUsable(bool usable)
		{
			if (_animator != null)
			{
				_animator.SetBool("Repaired", usable);
			}
		}

		private void ApplyTableNameSafe(FixedString128Bytes value)
		{
			if (_customization != null)
			{
				_customization.TableNameChanged(value);
			}
		}

		private void ApplyTableColorSafe(Color value)
		{
			if (_customization != null)
			{
				_customization.TableNameColorChanged(ForceOpaque(value));
			}
		}

		public override bool IsAvailable()
		{
			if (base.IsAvailable() && _physicalStateController != null)
			{
				return _physicalStateController.IsPhysicallyUsable;
			}
			return false;
		}

		public override void ServerUseEnter(Interactor interactor)
		{
			if (_physicalStateController.IsPhysicallyUsable)
			{
				base.ServerUseEnter(interactor);
				if (base.NetworkObject.OwnerClientId != interactor.OwnerClientId)
				{
					base.NetworkObject.ChangeOwnership(interactor.OwnerClientId);
				}
			}
		}

		public override void ServerUseExit(Interactor interactor)
		{
			base.ServerUseExit(interactor);
			base.NetworkObject.RemoveOwnership();
		}

		public override void ClientUseEnter(Interactor interactor)
		{
			if (_physicalStateController.IsPhysicallyUsable)
			{
				base.ClientUseEnter(interactor);
				interactor.Player.SwitchTo("PrepStation", cursorVisible: true);
				_customization.Active();
			}
		}

		public override void ClientUseExit(Interactor interactor)
		{
			base.ClientUseExit(interactor);
			interactor.Player.SwitchTo("Player", cursorVisible: false);
			_customization.Deactive();
		}

		protected override void ClientInteract(Interactor interactor, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && !TryRepairPhysicalState())
			{
				base.ClientInteract(interactor, phase);
			}
		}

		[Rpc(SendTo.Owner)]
		public void ClientShowNotifyRpc(FixedString64Bytes header, FixedString64Bytes message)
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
				FastBufferWriter bufferWriter = __beginSendRpc(380921659u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in header, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in message, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 380921659u, rpcParams, attributeParams, SendTo.Owner, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				Singleton<NotifyManager>.Instance.ShowToast(message.ToString());
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerSetTableNameRpc(FixedString64Bytes name)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3358569033u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in name, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 3358569033u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_tableName.Value = name;
				if (!string.IsNullOrWhiteSpace(name.ToString()))
				{
					QuestReferenceHub.Instance.QuestComplete(QuestId.NameYourRestaurant);
				}
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerSetTableNameColorRpc(Color color)
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
				FastBufferWriter bufferWriter = __beginSendRpc(3140548756u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in color);
				__endSendRpc(ref bufferWriter, 3140548756u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_tableNameColor.Value = ForceOpaque(color);
			}
		}

		private void OnTableNameChanged(FixedString128Bytes oldValue, FixedString128Bytes newValue)
		{
			ApplyTableNameSafe(newValue);
		}

		private void OnTableNameColorChanged(Color oldValue, Color newValue)
		{
			ApplyTableColorSafe(newValue);
		}

		private void HandlePhysicalRepair(bool value)
		{
			ApplyPhysicalUsable(value);
		}

		public RestaurantSignCustomizationData Save()
		{
			return new RestaurantSignCustomizationData
			{
				Repaired = _physicalStateController.IsPhysicallyUsable,
				RestaurantName = _tableName.Value.ToString(),
				RestaurantNameColor = new Color32Data(_customization.TablegSignColor)
			};
		}

		public void Load(RestaurantSignCustomizationData data)
		{
			Color value = ForceOpaque(data.RestaurantNameColor.ToColor());
			if (base.IsServer)
			{
				_physicalStateController.ServerSetUsable(data.Repaired);
				_tableName.Value = data.RestaurantName;
				_tableNameColor.Value = value;
			}
			ApplyPhysicalUsable(data.Repaired);
			ApplyTableNameSafe(data.RestaurantName);
			ApplyTableColorSafe(value);
		}

		private static Color ForceOpaque(Color color)
		{
			color.a = 1f;
			return color;
		}

		private bool TryRepairPhysicalState()
		{
			if (_physicalStateController == null)
			{
				return false;
			}
			if (_physicalStateController.IsPhysicallyUsable)
			{
				return false;
			}
			QuestReferenceHub.Instance.QuestComplete(QuestId.RepairTheSign);
			_physicalStateController.Repair();
			return true;
		}

		protected override void __initializeVariables()
		{
			if (_tableName == null)
			{
				throw new Exception("RestaurantSignManager._tableName cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_tableName.Initialize(this);
			__nameNetworkVariable(_tableName, "_tableName");
			NetworkVariableFields.Add(_tableName);
			if (_tableNameColor == null)
			{
				throw new Exception("RestaurantSignManager._tableNameColor cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_tableNameColor.Initialize(this);
			__nameNetworkVariable(_tableNameColor, "_tableNameColor");
			NetworkVariableFields.Add(_tableNameColor);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(380921659u, __rpc_handler_380921659, "ClientShowNotifyRpc", RpcInvokePermission.Everyone);
			__registerRpc(3358569033u, __rpc_handler_3358569033, "ServerSetTableNameRpc", RpcInvokePermission.Everyone);
			__registerRpc(3140548756u, __rpc_handler_3140548756, "ServerSetTableNameColorRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_380921659(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out FixedString64Bytes value2, default(FastBufferWriter.ForFixedStrings));
				((RestaurantSignManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantSignManager)target).ClientShowNotifyRpc(value, value2);
				((RestaurantSignManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3358569033(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				((RestaurantSignManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantSignManager)target).ServerSetTableNameRpc(value);
				((RestaurantSignManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3140548756(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out Color value);
				((RestaurantSignManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantSignManager)target).ServerSetTableNameColorRpc(value);
				((RestaurantSignManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "RestaurantSignManager";
		}
	}
}
