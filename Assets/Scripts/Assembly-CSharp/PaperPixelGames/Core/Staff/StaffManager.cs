using System;
using System.Collections.Generic;
using Bozo.ModularCharacters;
using DistantLands.Cozy;
using PaperPixelGames.Core.Economy;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Core.Settings;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Staff
{
	public sealed class StaffManager : NetworkBehaviour
	{
		private const int SushiChefLimit = 2;

		private const int PrepCookLimit = 2;

		private const int WaitressLimit = 3;

		private const int StockerLimit = 3;

		private const int PayrollHour = 21;

		[SerializeField]
		private StaffCatalogSO _catalog;

		private readonly NetworkList<StaffEmployeeEntry> _candidates = new NetworkList<StaffEmployeeEntry>();

		private readonly NetworkList<StaffEmployeeEntry> _team = new NetworkList<StaffEmployeeEntry>();

		private readonly NetworkVariable<int> _hiringLevel = new NetworkVariable<int>(1);

		private readonly NetworkVariable<bool> _protectKitchenShelf = new NetworkVariable<bool>(value: true);

		private readonly NetworkVariable<bool> _collectStationStorage = new NetworkVariable<bool>(value: false);

		private int _nextEmployeeId = 1;

		private int _nextHireSequence = 1;

		private int _lastPayrollDay = -1;

		private System.Random _random;

		private bool _eventsBound;

		private bool _staffChangedPending;

		private StaffRuntime _runtime;

		private readonly StaffNotificationPolicy _notificationPolicy = new StaffNotificationPolicy();

		public StaffCatalogSO Catalog => _catalog;

		public StaffReportLedger Ledger { get; } = new StaffReportLedger();

		public static StaffManager Instance { get; private set; }

		public int CandidateCount => _candidates.Count;

		public int TeamCount => _team.Count;

		public bool ProtectKitchenShelf => _protectKitchenShelf.Value;

		public bool CollectStationStorage => _collectStationStorage.Value;

		public bool HasActiveStocker
		{
			get
			{
				for (int i = 0; i < _team.Count; i++)
				{
					if (_team[i].Role == StaffRole.Stocker && _team[i].Departure == StaffDeparture.None)
					{
						return true;
					}
				}
				return false;
			}
		}

		public int HiringLevel
		{
			get
			{
				if (!base.IsServer)
				{
					return _hiringLevel.Value;
				}
				return GetProgressionLevel();
			}
		}

		public int CurrentDay => (Singleton<GameProgressManager>.Instance?.CozyWeatherManager?.CurrentDay).GetValueOrDefault();

		public static event Action<StaffManager> InstanceAvailable;

		public event Action StaffChanged;

		private bool SameAppearance(StaffRole firstRole, int firstId, StaffRole secondRole, int secondId)
		{
			if (firstId == secondId)
			{
				return true;
			}
			DataObject dataObject = _catalog?.ResolveCharacter(firstRole, firstId)?.ResidentData?.CharacterData;
			DataObject dataObject2 = _catalog?.ResolveCharacter(secondRole, secondId)?.ResidentData?.CharacterData;
			if (dataObject != null)
			{
				return dataObject == dataObject2;
			}
			return false;
		}

		private bool AppearanceInUse(StaffRole role, int customizationId, int ignoreEmployeeId = -1)
		{
			for (int i = 0; i < _team.Count; i++)
			{
				StaffEmployeeEntry staffEmployeeEntry = _team[i];
				if (staffEmployeeEntry.EmployeeId != ignoreEmployeeId && SameAppearance(role, customizationId, staffEmployeeEntry.Role, staffEmployeeEntry.CustomizationId))
				{
					return true;
				}
			}
			return false;
		}

		private StaffCustomizationOption FindUnusedAppearance(StaffRoleDefinition definition, int ignoreEmployeeId = -1)
		{
			if (definition.Customizations == null)
			{
				return null;
			}
			if (_random == null)
			{
				_random = new System.Random();
			}
			StaffCustomizationOption result = null;
			int num = 0;
			foreach (StaffCustomizationOption customization in definition.Customizations)
			{
				if (customization != null && customization.ResolveId() >= 0 && !AppearanceInUse(definition.Role, customization.ResolveId(), ignoreEmployeeId) && _random.Next(++num) == 0)
				{
					result = customization;
				}
			}
			return result;
		}

		private bool AssignUnusedAppearance(ref StaffEmployeeEntry employee)
		{
			StaffCustomizationOption staffCustomizationOption = FindUnusedAppearance(GetRoleDefinition(employee.Role), employee.EmployeeId);
			if (staffCustomizationOption == null)
			{
				return false;
			}
			employee.CustomizationId = staffCustomizationOption.ResolveId();
			employee.Gender = staffCustomizationOption.Gender;
			return true;
		}

		private void NormalizeTeamAppearances()
		{
			List<StaffEmployeeEntry> list = new List<StaffEmployeeEntry>();
			List<int> list2 = new List<int>();
			for (int i = 0; i < _team.Count; i++)
			{
				StaffEmployeeEntry item = _team[i];
				bool flag = false;
				foreach (StaffEmployeeEntry item2 in list)
				{
					if (SameAppearance(item.Role, item.CustomizationId, item2.Role, item2.CustomizationId))
					{
						flag = true;
						break;
					}
				}
				if (flag)
				{
					list2.Add(i);
				}
				else
				{
					list.Add(item);
				}
			}
			foreach (int item3 in list2)
			{
				StaffEmployeeEntry employee = _team[item3];
				if (AssignUnusedAppearance(ref employee))
				{
					_team[item3] = employee;
				}
			}
		}

		private void RefreshCandidateAppearances()
		{
			for (int i = 0; i < _candidates.Count; i++)
			{
				StaffEmployeeEntry employee = _candidates[i];
				if (AppearanceInUse(employee.Role, employee.CustomizationId) && AssignUnusedAppearance(ref employee))
				{
					_candidates[i] = employee;
				}
			}
		}

		public bool IsStackItemReserved(ulong itemId, int employee = 0)
		{
			return _runtime?.IsItemReserved(itemId, employee) ?? false;
		}

		public bool IsRoleUnlocked(StaffRole role)
		{
			if (StaffRules.IsRoleEnabled(role))
			{
				return HiringLevel >= GetRoleDefinition(role).UnlockLevel;
			}
			return false;
		}

		private void Awake()
		{
			Instance = this;
			ServiceLocator.ForSceneOf(this).Register(typeof(StaffManager), this);
			InstanceAvailable?.Invoke(this);
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			_random = new System.Random((Environment.TickCount * 397) ^ GetInstanceID());
			_candidates.OnListChanged += OnStaffListChanged;
			_team.OnListChanged += OnStaffListChanged;
			NetworkVariable<int> hiringLevel = _hiringLevel;
			hiringLevel.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Combine(hiringLevel.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnHiringLevelChanged));
			NetworkVariable<bool> protectKitchenShelf = _protectKitchenShelf;
			protectKitchenShelf.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(protectKitchenShelf.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnKitchenShelfProtectionChanged));
			NetworkVariable<bool> collectStationStorage = _collectStationStorage;
			collectStationStorage.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(collectStationStorage.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnStationStorageCollectionChanged));
			if (base.IsServer)
			{
				SynchronizeHiringLevel();
				_runtime = new StaffRuntime(this);
				ServerLoadFromCurrentSaveSlot();
				BindServerTimeEvents();
				ServerTryProcessPayroll();
			}
			RaiseStaffChanged();
		}

		public override void OnNetworkDespawn()
		{
			_candidates.OnListChanged -= OnStaffListChanged;
			_team.OnListChanged -= OnStaffListChanged;
			NetworkVariable<int> hiringLevel = _hiringLevel;
			hiringLevel.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Remove(hiringLevel.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnHiringLevelChanged));
			NetworkVariable<bool> protectKitchenShelf = _protectKitchenShelf;
			protectKitchenShelf.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(protectKitchenShelf.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnKitchenShelfProtectionChanged));
			NetworkVariable<bool> collectStationStorage = _collectStationStorage;
			collectStationStorage.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(collectStationStorage.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnStationStorageCollectionChanged));
			UnbindServerTimeEvents();
			_runtime?.Dispose();
			_runtime = null;
			_notificationPolicy.Clear();
			Ledger.Reset();
			base.OnNetworkDespawn();
		}

		public override void OnDestroy()
		{
			if (Instance == this)
			{
				Instance = null;
			}
			base.OnDestroy();
		}

		public StaffEmployeeEntry GetCandidate(int index)
		{
			return _candidates[index];
		}

		public StaffEmployeeEntry GetTeamMember(int index)
		{
			return _team[index];
		}

		public StaffRoleDefinition GetRoleDefinition(StaffRole role)
		{
			if (!(_catalog != null))
			{
				return StaffCatalogSO.CreateFallbackDefinition(role);
			}
			return _catalog.GetDefinition(role);
		}

		public int GetRoleLimit(StaffRole role)
		{
			return role switch
			{
				StaffRole.SushiChef => 2, 
				StaffRole.PrepCook => 2, 
				StaffRole.Waitress => 3, 
				StaffRole.Stocker => 3, 
				_ => 0, 
			};
		}

		public int GetHiredCount(StaffRole role)
		{
			int num = 0;
			for (int i = 0; i < _team.Count; i++)
			{
				if (_team[i].Role == role)
				{
					num++;
				}
			}
			return num;
		}

		public bool IsRoleFull(StaffRole role)
		{
			return GetHiredCount(role) >= GetRoleLimit(role);
		}

		public int GetDailyPayrollDollars()
		{
			int num = 0;
			for (int i = 0; i < _team.Count; i++)
			{
				if (StaffRules.IsRoleEnabled(_team[i].Role) && _team[i].Departure == StaffDeparture.None)
				{
					num += Mathf.Max(0, _team[i].DailyWageDollars);
				}
			}
			return num;
		}

		public void RequestRefreshCandidates()
		{
			RequestRefreshCandidatesRpc();
		}

		public void RequestHire(int employeeId)
		{
			RequestHireRpc(employeeId);
		}

		public void RequestFire(int employeeId)
		{
			RequestFireRpc(employeeId);
		}

		public void RequestStockerSetting(int employeeId, StockerSetting setting, bool enabled)
		{
			RequestStockerSettingRpc(employeeId, setting, enabled);
		}

		public void RequestKitchenShelfProtection(bool enabled)
		{
			if (base.IsSpawned)
			{
				RequestKitchenShelfProtectionRpc(enabled);
			}
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
		private void RequestKitchenShelfProtectionRpc(bool enabled)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Everyone
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1827642399u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in enabled, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1827642399u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (HasActiveStocker)
				{
					_protectKitchenShelf.Value = enabled;
				}
			}
		}

		private void OnKitchenShelfProtectionChanged(bool previous, bool current)
		{
			_staffChangedPending = true;
		}

		public void RequestStationStorageCollection(bool enabled)
		{
			if (base.IsSpawned)
			{
				RequestStationStorageCollectionRpc(enabled);
			}
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
		private void RequestStationStorageCollectionRpc(bool enabled)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Everyone
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3727330469u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in enabled, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 3727330469u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (HasActiveStocker)
				{
					_collectStationStorage.Value = enabled;
				}
			}
		}

		private void OnStationStorageCollectionChanged(bool previous, bool current)
		{
			_staffChangedPending = true;
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
		private void RequestRefreshCandidatesRpc(RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3212685478u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3212685478u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				EconomyHandler economyHandler = Singleton<GameProgressManager>.Instance?.EconomyHandler;
				if (economyHandler == null || !economyHandler.ServerTryStaffSpend(100, purchase: false, out var _))
				{
					ServerSendFeedbackToClient(StaffFeedback.InsufficientMoney, rpcParams.Receive.SenderClientId);
				}
				else
				{
					ServerRefreshAllCandidates();
				}
			}
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
		private void RequestHireRpc(int employeeId, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2895511264u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, employeeId);
				__endSendRpc(ref bufferWriter, 2895511264u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			int num = FindCandidateIndex(employeeId);
			if (num < 0)
			{
				ServerSendFeedbackToClient(StaffFeedback.CandidateUnavailable, rpcParams.Receive.SenderClientId);
				return;
			}
			StaffEmployeeEntry employee = _candidates[num];
			if (!StaffRules.IsRoleEnabled(employee.Role))
			{
				ServerSendFeedbackToClient(StaffFeedback.RoleUnavailable, rpcParams.Receive.SenderClientId, employee.Role);
				return;
			}
			SynchronizeHiringLevel();
			if (!IsRoleUnlocked(employee.Role))
			{
				ServerSendFeedbackToClient(StaffFeedback.RoleLevelLocked, rpcParams.Receive.SenderClientId, employee.Role);
				return;
			}
			if (IsRoleFull(employee.Role))
			{
				ServerSendFeedbackToClient(StaffFeedback.RoleLimitFull, rpcParams.Receive.SenderClientId, employee.Role);
				return;
			}
			if (_runtime == null || !_runtime.CanSpawn(out var _, employee.Role))
			{
				ServerSendFeedbackToClient(StaffFeedback.MissingWorkplace, rpcParams.Receive.SenderClientId);
				return;
			}
			if (AppearanceInUse(employee.Role, employee.CustomizationId) && !AssignUnusedAppearance(ref employee))
			{
				ServerSendFeedbackToClient(StaffFeedback.AppearanceUnavailable, rpcParams.Receive.SenderClientId);
				return;
			}
			employee.HireSequence = _nextHireSequence++;
			employee.LastPaidDay = -1;
			_team.Add(employee);
			Ledger.Register(employee);
			_runtime.Spawn(employee);
			_candidates[num] = GenerateCandidate(employee.Role);
			UtilityMethods.Log($"[StaffManager] Employee hired. EmployeeId={employee.EmployeeId}, Name={employee.Name}, Role={employee.Role}, Wage={employee.DailyWageDollars}");
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
		private void RequestFireRpc(int employeeId, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1418578379u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, employeeId);
				__endSendRpc(ref bufferWriter, 1418578379u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			int num = FindTeamIndex(employeeId);
			if (num < 0)
			{
				ServerSendFeedbackToClient(StaffFeedback.EmployeeUnavailable, rpcParams.Receive.SenderClientId);
				return;
			}
			StaffEmployeeEntry staffEmployeeEntry = _team[num];
			if (staffEmployeeEntry.Departure != StaffDeparture.None)
			{
				return;
			}
			if (StaffRules.IsRoleEnabled(staffEmployeeEntry.Role))
			{
				EconomyHandler economyHandler = Singleton<GameProgressManager>.Instance?.EconomyHandler;
				if (economyHandler == null)
				{
					return;
				}
				int num2 = StaffRules.FireCharge(staffEmployeeEntry.DailyWageDollars, staffEmployeeEntry.LastPaidDay, CurrentDay, economyHandler.ShopBalance.Value);
				if (num2 > 0 && !economyHandler.ServerTryStaffSpend(num2, purchase: false, out var _))
				{
					return;
				}
				Ledger.Wage(staffEmployeeEntry, num2);
				if (staffEmployeeEntry.LastPaidDay != CurrentDay)
				{
					Ledger.UnpaidWage(staffEmployeeEntry, staffEmployeeEntry.DailyWageDollars * 100 - num2);
				}
				staffEmployeeEntry.LastPaidDay = CurrentDay;
				_team[num] = staffEmployeeEntry;
			}
			ServerRequestDeparture(employeeId, StaffDeparture.Fired);
		}

		[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
		private void RequestStockerSettingRpc(int employeeId, StockerSetting setting, bool enabled)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = new RpcAttribute.RpcAttributeParams
				{
					InvokePermission = RpcInvokePermission.Everyone
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1201965852u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, employeeId);
				bufferWriter.WriteValueSafe(in setting, default(FastBufferWriter.ForEnums));
				bufferWriter.WriteValueSafe(in enabled, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 1201965852u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			int num = FindTeamIndex(employeeId);
			if (num < 0)
			{
				return;
			}
			StaffEmployeeEntry value = _team[num];
			if (StaffRules.IsRoleEnabled(value.Role) && value.Departure == StaffDeparture.None && (value.Role != StaffRole.PrepCook || setting == StockerSetting.StockingStopped || setting == StockerSetting.RiceCooker || setting == StockerSetting.FilletMachine) && (value.Role != StaffRole.SushiChef || setting == StockerSetting.StockingStopped || setting == StockerSetting.PrepareOrders || setting == StockerSetting.StockConveyor) && (value.Role != StaffRole.Stocker || (int)setting <= 3 || setting == StockerSetting.CollectFillets) && (value.Role != StaffRole.Waitress || setting == StockerSetting.StockingStopped || setting == StockerSetting.TakeOrders || setting == StockerSetting.ServeOrders))
			{
				switch (setting)
				{
				default:
					return;
				case StockerSetting.RiceCooker:
					value.RiceCookerEnabled = enabled;
					break;
				case StockerSetting.FilletMachine:
					value.FilletMachineEnabled = enabled;
					break;
				case StockerSetting.CollectFillets:
					value.CollectFilletsEnabled = enabled;
					break;
				case StockerSetting.PrepareOrders:
					value.PrepareOrdersEnabled = enabled;
					break;
				case StockerSetting.StockConveyor:
					value.StockConveyorEnabled = enabled;
					break;
				case StockerSetting.TakeOrders:
					value.TakeOrdersEnabled = enabled;
					break;
				case StockerSetting.ServeOrders:
					value.ServeOrdersEnabled = enabled;
					break;
				case StockerSetting.BuyMissingStock:
					value.BuyMissingStock = enabled;
					break;
				case StockerSetting.GreenMarket:
					value.GreenMarketEnabled = enabled;
					break;
				case StockerSetting.BlueMarket:
					value.BlueMarketEnabled = enabled;
					break;
				case StockerSetting.StockingStopped:
					value.StockingStopped = enabled;
					break;
				}
				_team[num] = value;
			}
		}

		public StaffData Save()
		{
			if (!base.IsServer)
			{
				return null;
			}
			StaffData staffData = new StaffData
			{
				Version = 1,
				NextEmployeeId = _nextEmployeeId,
				NextHireSequence = _nextHireSequence,
				LastPayrollDay = _lastPayrollDay,
				ProtectKitchenShelf = ProtectKitchenShelf,
				CollectStationStorage = CollectStationStorage,
				Candidates = new List<StaffEmployeeData>(),
				Team = new List<StaffEmployeeData>()
			};
			for (int i = 0; i < _candidates.Count; i++)
			{
				staffData.Candidates.Add(ToData(_candidates[i]));
			}
			for (int j = 0; j < _team.Count; j++)
			{
				if (_team[j].Departure == StaffDeparture.None)
				{
					staffData.Team.Add(ToData(_team[j]));
				}
			}
			return staffData;
		}

		private void ServerLoadFromCurrentSaveSlot()
		{
			if (base.IsServer)
			{
				StaffData staffData = null;
				if (Singleton<SaveManager>.Instance != null && Singleton<SaveManager>.Instance.TryGetCurrentSaveSlot(out var data) && data?.TabletData != null)
				{
					staffData = data.TabletData.StaffData;
				}
				ServerLoad(staffData ?? new StaffData());
			}
		}

		private void ServerLoad(StaffData data)
		{
			StaffRules.Migrate(data);
			_protectKitchenShelf.Value = data.ProtectKitchenShelf;
			_collectStationStorage.Value = data.CollectStationStorage;
			_candidates.Clear();
			_team.Clear();
			_nextEmployeeId = Mathf.Max(1, data.NextEmployeeId);
			_nextHireSequence = Mathf.Max(1, data.NextHireSequence);
			_lastPayrollDay = data.LastPayrollDay;
			HashSet<int> usedIds = new HashSet<int>();
			HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (data.Team != null)
			{
				for (int i = 0; i < data.Team.Count; i++)
				{
					if (TryRestoreEntry(data.Team[i], usedIds, usedNames, out var entry) && GetHiredCount(entry.Role) < GetRoleLimit(entry.Role))
					{
						_team.Add(entry);
						if (StaffRules.IsRoleEnabled(entry.Role))
						{
							_runtime?.QueueSpawn(entry.EmployeeId);
						}
					}
				}
			}
			NormalizeTeamAppearances();
			if (data.Candidates != null)
			{
				HashSet<StaffRole> hashSet = new HashSet<StaffRole>();
				for (int j = 0; j < data.Candidates.Count; j++)
				{
					if (TryRestoreEntry(data.Candidates[j], usedIds, usedNames, out var entry2) && hashSet.Add(entry2.Role))
					{
						entry2.HireSequence = 0;
						if (AppearanceInUse(entry2.Role, entry2.CustomizationId))
						{
							AssignUnusedAppearance(ref entry2);
						}
						_candidates.Add(entry2);
					}
				}
			}
			RecalculateNextIds();
			EnsureOneCandidatePerRole();
			RecalculateNextIds();
			UtilityMethods.Log($"[StaffManager] Staff data loaded. Candidates={_candidates.Count}, Team={_team.Count}, LastPayrollDay={_lastPayrollDay}");
		}

		private bool TryRestoreEntry(StaffEmployeeData data, HashSet<int> usedIds, HashSet<string> usedNames, out StaffEmployeeEntry entry)
		{
			entry = default;
			if (data == null || !Enum.IsDefined(typeof(StaffRole), data.Role))
			{
				return false;
			}
			string text = data.Name?.Trim();
			if (data.EmployeeId <= 0 || string.IsNullOrWhiteSpace(text) || !usedIds.Add(data.EmployeeId) || !usedNames.Add(text))
			{
				return false;
			}
			int num = ResolveSavedCustomization(data.Role, data.CustomizationId, data.EmployeeId);
			StaffCustomizationOption staffCustomizationOption = _catalog?.ResolveCharacter(data.Role, num);
			entry = new StaffEmployeeEntry
			{
				EmployeeId = data.EmployeeId,
				Name = text,
				Role = data.Role,
				Gender = (staffCustomizationOption?.Gender ?? data.Gender),
				CustomizationId = num,
				SkillLevel = StaffRules.ClampSkill(data.SkillLevel),
				LastPaidDay = data.LastPaidDay,
				Status = ((!StaffRules.IsRoleEnabled(data.Role)) ? StaffStatus.Unavailable : StaffStatus.Waiting),
				DailyWageDollars = Mathf.Max(1, data.DailyWageDollars),
				HireSequence = Mathf.Max(0, data.HireSequence),
				RiceCookerEnabled = data.RiceCookerEnabled,
				FilletMachineEnabled = data.FilletMachineEnabled,
				CollectFilletsEnabled = data.CollectFilletsEnabled,
				PrepareOrdersEnabled = data.PrepareOrdersEnabled,
				StockConveyorEnabled = data.StockConveyorEnabled,
				TakeOrdersEnabled = data.TakeOrdersEnabled,
				ServeOrdersEnabled = data.ServeOrdersEnabled,
				BuyMissingStock = data.BuyMissingStock,
				GreenMarketEnabled = data.GreenMarketEnabled,
				BlueMarketEnabled = data.BlueMarketEnabled,
				StockingStopped = data.StockingStopped
			};
			return true;
		}

		private int ResolveSavedCustomization(StaffRole role, int customizationId, int employeeId)
		{
			List<StaffCustomizationOption> customizations = GetRoleDefinition(role).Customizations;
			if (_catalog?.ResolveCharacter(role, customizationId) != null)
			{
				return customizationId;
			}
			if (customizations.Count <= 0)
			{
				return -1;
			}
			return customizations[Math.Abs(employeeId) % customizations.Count].ResolveId();
		}

		private void ServerRefreshAllCandidates()
		{
			_candidates.Clear();
			foreach (StaffRole value in Enum.GetValues(typeof(StaffRole)))
			{
				_candidates.Add(GenerateCandidate(value));
			}
			UtilityMethods.Log("[StaffManager] All candidates refreshed.");
		}

		private void EnsureOneCandidatePerRole()
		{
			foreach (StaffRole value in Enum.GetValues(typeof(StaffRole)))
			{
				bool flag = false;
				for (int i = 0; i < _candidates.Count; i++)
				{
					if (_candidates[i].Role == value)
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					_candidates.Add(GenerateCandidate(value));
				}
			}
			for (int num = _candidates.Count - 1; num >= 0; num--)
			{
				StaffRole role = _candidates[num].Role;
				int num2 = -1;
				for (int j = 0; j < _candidates.Count; j++)
				{
					if (_candidates[j].Role == role)
					{
						num2 = j;
						break;
					}
				}
				if (num2 != num)
				{
					_candidates.RemoveAt(num);
				}
			}
		}

		private StaffEmployeeEntry GenerateCandidate(StaffRole role)
		{
			if (_random == null)
			{
				_random = new System.Random();
			}
			StaffRoleDefinition roleDefinition = GetRoleDefinition(role);
			ResolveCustomization(roleDefinition, out var gender, out var customizationId);
			string text = GenerateUniqueName(gender);
			(int, int) tuple = StaffRules.SkillRange(GetProgressionLevel());
			byte b = (byte)_random.Next(tuple.Item1, tuple.Item2 + 1);
			int b2 = roleDefinition.BaseDailyWage + (b - 1) * roleDefinition.SecondaryStatWagePerPoint + roleDefinition.ProgressionPremium;
			return new StaffEmployeeEntry
			{
				EmployeeId = _nextEmployeeId++,
				Name = text,
				Role = role,
				Gender = gender,
				CustomizationId = customizationId,
				SkillLevel = b,
				LastPaidDay = -1,
				Status = ((!StaffRules.IsRoleEnabled(role)) ? StaffStatus.Unavailable : StaffStatus.Waiting),
				DailyWageDollars = Mathf.Max(1, b2),
				HireSequence = 0,
				RiceCookerEnabled = true,
				FilletMachineEnabled = true,
				CollectFilletsEnabled = false,
				PrepareOrdersEnabled = true,
				StockConveyorEnabled = true,
				TakeOrdersEnabled = true,
				ServeOrdersEnabled = true,
				BuyMissingStock = true,
				GreenMarketEnabled = true,
				BlueMarketEnabled = true,
				StockingStopped = false
			};
		}

		private void ResolveCustomization(StaffRoleDefinition definition, out StaffGender gender, out int customizationId)
		{
			StaffCustomizationOption staffCustomizationOption = FindUnusedAppearance(definition);
			if (staffCustomizationOption != null)
			{
				gender = staffCustomizationOption.Gender;
				customizationId = staffCustomizationOption.ResolveId();
				return;
			}
			if (definition.Customizations != null && definition.Customizations.Count > 0)
			{
				StaffCustomizationOption staffCustomizationOption2 = definition.Customizations[_random.Next(0, definition.Customizations.Count)];
				if (staffCustomizationOption2 != null)
				{
					gender = staffCustomizationOption2.Gender;
					customizationId = staffCustomizationOption2.ResolveId();
					return;
				}
			}
			gender = ((_random.Next(0, 2) != 0) ? StaffGender.Female : StaffGender.Male);
			customizationId = -1;
		}

		private string GenerateUniqueName(StaffGender gender)
		{
			IReadOnlyList<string> readOnlyList = ((gender != StaffGender.Male) ? ((_catalog != null && _catalog.FemaleNames.Count > 0) ? _catalog.FemaleNames : StaffNames.CanonicalNames(StaffGender.Female)) : ((_catalog != null && _catalog.MaleNames.Count > 0) ? _catalog.MaleNames : StaffNames.CanonicalNames(StaffGender.Male)));
			HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < _team.Count; i++)
			{
				hashSet.Add(_team[i].Name.ToString());
			}
			for (int j = 0; j < _candidates.Count; j++)
			{
				hashSet.Add(_candidates[j].Name.ToString());
			}
			List<string> list = new List<string>();
			for (int k = 0; k < readOnlyList.Count; k++)
			{
				string text = readOnlyList[k]?.Trim();
				if (!string.IsNullOrWhiteSpace(text) && !hashSet.Contains(text))
				{
					list.Add(text);
				}
			}
			if (list.Count > 0)
			{
				return list[_random.Next(0, list.Count)];
			}
			string text2 = ((gender == StaffGender.Male) ? "Employee" : "Staff");
			int num = 1;
			while (hashSet.Contains(text2 + num))
			{
				num++;
			}
			return text2 + num;
		}

		private int GetProgressionLevel()
		{
			return Mathf.Clamp(Singleton<GameProgressManager>.Instance?.ProgressionManager?.CurrentLevel ?? 1, 0, 30);
		}

		private int RollQualityTier(int progressionLevel)
		{
			int num = ((progressionLevel <= 19) ? ((progressionLevel <= 4) ? 1 : ((progressionLevel > 10) ? 3 : 2)) : ((progressionLevel > 24) ? 5 : 4));
			int num2 = num;
			int minValue = ((progressionLevel >= 25) ? 3 : ((progressionLevel < 15) ? 1 : 2));
			return _random.Next(minValue, num2 + 1);
		}

		private void BindServerTimeEvents()
		{
			if (!_eventsBound)
			{
				CozyWeather.Events.onNewHour += ServerOnNewHour;
				_eventsBound = true;
			}
		}

		private void UnbindServerTimeEvents()
		{
			if (_eventsBound)
			{
				CozyWeather.Events.onNewHour -= ServerOnNewHour;
				_eventsBound = false;
			}
		}

		private void ServerOnNewHour()
		{
			ServerTryProcessPayroll();
		}

		public void ServerStaffFeedback(int id, StaffStatus status, FixedString64Bytes itemId = default(FixedString64Bytes))
		{
			ServerSetStatus(id, status, itemId);
		}

		[Rpc(SendTo.Everyone)]
		private void ClientStaffFeedbackRpc(FixedString64Bytes employeeName, StaffGender gender, StaffStatus status, FixedString64Bytes itemId)
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
				FastBufferWriter bufferWriter = __beginSendRpc(1005107440u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in employeeName, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in gender, default(FastBufferWriter.ForEnums));
				bufferWriter.WriteValueSafe(in status, default(FastBufferWriter.ForEnums));
				bufferWriter.WriteValueSafe(in itemId, default(FastBufferWriter.ForFixedStrings));
				__endSendRpc(ref bufferWriter, 1005107440u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (RuntimeSettings.RestaurantChatLogsEnabled)
				{
					Singleton<NotifyManager>.Instance?.ShowRestaurantToast(StaffNames.DisplayName(employeeName.ToString(), gender) + ": " + StaffText.Status(status, itemId.ToString()), NotificationState.Info, 3f);
				}
			}
		}

		private void Update()
		{
			if (!base.IsServer || !base.IsSpawned)
			{
				return;
			}
			SynchronizeHiringLevel();
			_runtime?.Tick();
			for (int i = 0; i < _team.Count; i++)
			{
				StaffEmployeeEntry staffEmployeeEntry = _team[i];
				if (staffEmployeeEntry.Departure == StaffDeparture.None && !staffEmployeeEntry.StockingStopped && _notificationPolicy.TryNotify(staffEmployeeEntry.EmployeeId, staffEmployeeEntry.Status, Time.unscaledTimeAsDouble))
				{
					ClientStaffFeedbackRpc(staffEmployeeEntry.Name, staffEmployeeEntry.Gender, staffEmployeeEntry.Status, staffEmployeeEntry.StatusItemId);
				}
			}
		}

		private void SynchronizeHiringLevel()
		{
			int progressionLevel = GetProgressionLevel();
			if (_hiringLevel.Value != progressionLevel)
			{
				_hiringLevel.Value = progressionLevel;
			}
		}

		private void OnHiringLevelChanged(int previous, int current)
		{
			_staffChangedPending = true;
		}

		public bool TryGetEmployee(int id, out StaffEmployeeEntry employee)
		{
			int num = FindTeamIndex(id);
			employee = ((num < 0) ? default(StaffEmployeeEntry) : _team[num]);
			return num >= 0;
		}

		public void ServerSetStatus(int id, StaffStatus status, FixedString64Bytes itemId = default(FixedString64Bytes))
		{
			int num = FindTeamIndex(id);
			if (base.IsServer && num >= 0)
			{
				StaffEmployeeEntry value = _team[num];
				if (value.Departure != StaffDeparture.None)
				{
					status = StaffStatus.Leaving;
				}
				if (status != StaffStatus.WaitingForIngredients)
				{
					itemId = default;
				}
				if (value.Status != status || !value.StatusItemId.Equals(itemId))
				{
					value.Status = status;
					value.StatusItemId = itemId;
					_team[num] = value;
				}
			}
		}

		public void ServerRequestDeparture(int id, StaffDeparture reason)
		{
			int num = FindTeamIndex(id);
			if (base.IsServer && num >= 0 && _team[num].Departure == StaffDeparture.None)
			{
				StaffEmployeeEntry staffEmployeeEntry = _team[num];
				staffEmployeeEntry.Departure = reason;
				staffEmployeeEntry.Status = StaffStatus.Leaving;
				staffEmployeeEntry.StatusItemId = default;
				_team[num] = staffEmployeeEntry;
				Ledger.Depart(staffEmployeeEntry, reason);
				if (!StaffRules.IsRoleEnabled(staffEmployeeEntry.Role))
				{
					ServerCompleteDeparture(id);
				}
			}
		}

		public void ServerCompleteDeparture(int id)
		{
			int num = FindTeamIndex(id);
			if (base.IsServer && num >= 0 && _team[num].Departure != StaffDeparture.None)
			{
				Ledger.CompleteDeparture(_team[num]);
				_notificationPolicy.Remove(id);
				_team.RemoveAt(num);
				RefreshCandidateAppearances();
			}
		}

		public void ServerTryProcessPayroll()
		{
			CozyWeatherManager cozyWeatherManager = Singleton<GameProgressManager>.Instance?.CozyWeatherManager;
			if (cozyWeatherManager != null)
			{
				ServerProcessPayrollAt(CurrentDay, cozyWeatherManager.CurrentTime.hours);
			}
		}

		private void ServerProcessPayrollAt(int day, int hour)
		{
			if (!base.IsServer || !base.IsSpawned)
			{
				return;
			}
			EconomyHandler economyHandler = Singleton<GameProgressManager>.Instance?.EconomyHandler;
			if (economyHandler == null || hour < 21 || _lastPayrollDay == day)
			{
				return;
			}
			List<StaffEmployeeEntry> list = new List<StaffEmployeeEntry>();
			for (int i = 0; i < _team.Count; i++)
			{
				if (StaffRules.IsRoleEnabled(_team[i].Role) && _team[i].Departure == StaffDeparture.None && _team[i].LastPaidDay != day)
				{
					list.Add(_team[i]);
				}
			}
			list.Sort((StaffEmployeeEntry a, StaffEmployeeEntry b) => (a.DailyWageDollars != b.DailyWageDollars) ? a.DailyWageDollars.CompareTo(b.DailyWageDollars) : a.HireSequence.CompareTo(b.HireSequence));
			foreach (StaffEmployeeEntry item in list)
			{
				StaffEmployeeEntry current = item;
				if (!economyHandler.ServerTryStaffSpend(current.DailyWageDollars * 100, purchase: false, out var _))
				{
					Ledger.UnpaidWage(current, current.DailyWageDollars * 100);
					ServerRequestDeparture(current.EmployeeId, StaffDeparture.Unpaid);
				}
				else
				{
					current.LastPaidDay = day;
					_team[FindTeamIndex(current.EmployeeId)] = current;
					Ledger.Wage(current, current.DailyWageDollars * 100);
				}
			}
			_lastPayrollDay = day;
		}

		[Rpc(SendTo.Everyone)]
		private void ClientShowToastRpc(FixedString128Bytes message, byte state)
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
				FastBufferWriter bufferWriter = __beginSendRpc(451095696u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in message, default(FastBufferWriter.ForFixedStrings));
				bufferWriter.WriteValueSafe(in state, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 451095696u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (RuntimeSettings.RestaurantChatLogsEnabled)
				{
					Singleton<NotifyManager>.Instance?.ShowRestaurantToast(message.ToString(), (NotificationState)state, 3f);
				}
			}
		}

		[Rpc(SendTo.SpecifiedInParams)]
		private void ClientShowFeedbackTargetRpc(byte feedback, byte role, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2079904549u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in feedback, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in role, default(FastBufferWriter.ForPrimitives));
				__endSendRpc(ref bufferWriter, 2079904549u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (RuntimeSettings.RestaurantChatLogsEnabled)
				{
					Singleton<NotifyManager>.Instance?.ShowRestaurantToast(StaffText.Feedback((StaffFeedback)feedback, (StaffRole)role), NotificationState.Info, 2f);
				}
			}
		}

		private void ServerSendFeedbackToClient(StaffFeedback feedback, ulong clientId, StaffRole role = StaffRole.Stocker)
		{
			ClientShowFeedbackTargetRpc((byte)feedback, (byte)role, base.RpcTarget.Single(clientId, RpcTargetUse.Temp));
		}

		private int FindCandidateIndex(int employeeId)
		{
			for (int i = 0; i < _candidates.Count; i++)
			{
				if (_candidates[i].EmployeeId == employeeId)
				{
					return i;
				}
			}
			return -1;
		}

		private int FindTeamIndex(int employeeId)
		{
			for (int i = 0; i < _team.Count; i++)
			{
				if (_team[i].EmployeeId == employeeId)
				{
					return i;
				}
			}
			return -1;
		}

		private void RecalculateNextIds()
		{
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < _team.Count; i++)
			{
				num = Mathf.Max(num, _team[i].EmployeeId);
				num2 = Mathf.Max(num2, _team[i].HireSequence);
			}
			for (int j = 0; j < _candidates.Count; j++)
			{
				num = Mathf.Max(num, _candidates[j].EmployeeId);
			}
			_nextEmployeeId = Mathf.Max(_nextEmployeeId, num + 1);
			_nextHireSequence = Mathf.Max(_nextHireSequence, num2 + 1);
		}

		private static StaffEmployeeData ToData(StaffEmployeeEntry entry)
		{
			return new StaffEmployeeData
			{
				EmployeeId = entry.EmployeeId,
				Name = entry.Name.ToString(),
				Role = entry.Role,
				Gender = entry.Gender,
				CustomizationId = entry.CustomizationId,
				SkillLevel = entry.SkillLevel,
				LastPaidDay = entry.LastPaidDay,
				DailyWageDollars = entry.DailyWageDollars,
				HireSequence = entry.HireSequence,
				RiceCookerEnabled = entry.RiceCookerEnabled,
				FilletMachineEnabled = entry.FilletMachineEnabled,
				CollectFilletsEnabled = entry.CollectFilletsEnabled,
				PrepareOrdersEnabled = entry.PrepareOrdersEnabled,
				StockConveyorEnabled = entry.StockConveyorEnabled,
				TakeOrdersEnabled = entry.TakeOrdersEnabled,
				ServeOrdersEnabled = entry.ServeOrdersEnabled,
				BuyMissingStock = entry.BuyMissingStock,
				GreenMarketEnabled = entry.GreenMarketEnabled,
				BlueMarketEnabled = entry.BlueMarketEnabled,
				StockingStopped = entry.StockingStopped
			};
		}

		private void OnStaffListChanged(NetworkListEvent<StaffEmployeeEntry> change)
		{
			_staffChangedPending = true;
		}

		private void LateUpdate()
		{
			if (_staffChangedPending && base.IsSpawned)
			{
				_staffChangedPending = false;
				RaiseStaffChanged();
			}
		}

		private void RaiseStaffChanged()
		{
			StaffChanged?.Invoke();
		}

		protected override void __initializeVariables()
		{
			if (_candidates == null)
			{
				throw new Exception("StaffManager._candidates cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_candidates.Initialize(this);
			__nameNetworkVariable(_candidates, "_candidates");
			NetworkVariableFields.Add(_candidates);
			if (_team == null)
			{
				throw new Exception("StaffManager._team cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_team.Initialize(this);
			__nameNetworkVariable(_team, "_team");
			NetworkVariableFields.Add(_team);
			if (_hiringLevel == null)
			{
				throw new Exception("StaffManager._hiringLevel cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_hiringLevel.Initialize(this);
			__nameNetworkVariable(_hiringLevel, "_hiringLevel");
			NetworkVariableFields.Add(_hiringLevel);
			if (_protectKitchenShelf == null)
			{
				throw new Exception("StaffManager._protectKitchenShelf cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_protectKitchenShelf.Initialize(this);
			__nameNetworkVariable(_protectKitchenShelf, "_protectKitchenShelf");
			NetworkVariableFields.Add(_protectKitchenShelf);
			if (_collectStationStorage == null)
			{
				throw new Exception("StaffManager._collectStationStorage cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_collectStationStorage.Initialize(this);
			__nameNetworkVariable(_collectStationStorage, "_collectStationStorage");
			NetworkVariableFields.Add(_collectStationStorage);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(1827642399u, __rpc_handler_1827642399, "RequestKitchenShelfProtectionRpc", RpcInvokePermission.Everyone);
			__registerRpc(3727330469u, __rpc_handler_3727330469, "RequestStationStorageCollectionRpc", RpcInvokePermission.Everyone);
			__registerRpc(3212685478u, __rpc_handler_3212685478, "RequestRefreshCandidatesRpc", RpcInvokePermission.Everyone);
			__registerRpc(2895511264u, __rpc_handler_2895511264, "RequestHireRpc", RpcInvokePermission.Everyone);
			__registerRpc(1418578379u, __rpc_handler_1418578379, "RequestFireRpc", RpcInvokePermission.Everyone);
			__registerRpc(1201965852u, __rpc_handler_1201965852, "RequestStockerSettingRpc", RpcInvokePermission.Everyone);
			__registerRpc(1005107440u, __rpc_handler_1005107440, "ClientStaffFeedbackRpc", RpcInvokePermission.Everyone);
			__registerRpc(451095696u, __rpc_handler_451095696, "ClientShowToastRpc", RpcInvokePermission.Everyone);
			__registerRpc(2079904549u, __rpc_handler_2079904549, "ClientShowFeedbackTargetRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_1827642399(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((StaffManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((StaffManager)target).RequestKitchenShelfProtectionRpc(value);
				((StaffManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3727330469(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				((StaffManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((StaffManager)target).RequestStationStorageCollectionRpc(value);
				((StaffManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3212685478(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				RpcParams ext = rpcParams.Ext;
				((StaffManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((StaffManager)target).RequestRefreshCandidatesRpc(ext);
				((StaffManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2895511264(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				RpcParams ext = rpcParams.Ext;
				((StaffManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((StaffManager)target).RequestHireRpc(value, ext);
				((StaffManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1418578379(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				RpcParams ext = rpcParams.Ext;
				((StaffManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((StaffManager)target).RequestFireRpc(value, ext);
				((StaffManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1201965852(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				reader.ReadValueSafe(out StockerSetting value2, default(FastBufferWriter.ForEnums));
				reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
				((StaffManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((StaffManager)target).RequestStockerSettingRpc(value, value2, value3);
				((StaffManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1005107440(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out StaffGender value2, default(FastBufferWriter.ForEnums));
				reader.ReadValueSafe(out StaffStatus value3, default(FastBufferWriter.ForEnums));
				reader.ReadValueSafe(out FixedString64Bytes value4, default(FastBufferWriter.ForFixedStrings));
				((StaffManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((StaffManager)target).ClientStaffFeedbackRpc(value, value2, value3, value4);
				((StaffManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_451095696(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString128Bytes value, default(FastBufferWriter.ForFixedStrings));
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				((StaffManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((StaffManager)target).ClientShowToastRpc(value, value2);
				((StaffManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2079904549(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				RpcParams ext = rpcParams.Ext;
				((StaffManager)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((StaffManager)target).ClientShowFeedbackTargetRpc(value, value2, ext);
				((StaffManager)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "StaffManager";
		}
	}
}
