using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Ai.NPCs;
using PaperPixelGames.Core.DailyReport;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Data.Ai.NPCs;
using PaperPixelGames.Core.Economy;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Interaction.ToolTip;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Progress;
using PaperPixelGames.Core.Progression;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Core.Restaurant.LateJoin;
using PaperPixelGames.Core.Staff;
using PaperPixelGames.Core.Struct;
using PaperPixelGames.Core.Tablet;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityServiceLocator;

namespace PaperPixelGames.Core.Restaurant
{
	public class RestaurantTable : BaseInteractable
	{
		private sealed class DeliveredTrayRecord
		{
			public Tray Tray;

			public int SeatIndex;

			public CustomerOrderData Order;

			public int OrderId;

			public int EarnedPayment;

			public ulong WaitressSteamId;

			public bool HadPreviousServingSteamId;

			public ulong PreviousServingSteamId;

			public CustomerOrderFoodPayload DeliveredPayload;
		}

		private sealed class DeliveredSeatVisualRecord
		{
			public Tray Tray;

			public int SeatIndex;
		}

		private const double LastCallThresholdSeconds = 3.0;

		private const double ConsistencyCheckIntervalSeconds = 2.0;

		[SerializeField]
		private int _seatCount = 4;

		[SerializeField]
		private int _maxEatingTime = 100;

		[SerializeField]
		private int _minEatingTime = 20;

		[SerializeField]
		private TextMeshProUGUI _debugText;

		[SerializeField]
		private Transform _trayPointsParent;

		[SerializeField]
		private ItemSO _traySO;

		[SerializeField]
		private int _id;

		[SerializeField]
		private GameObject _moneyGraphPrefab;

		[SerializeField]
		private List<GameObject> _seatAvailableObjects;

		[SerializeField]
		private List<TextMeshPro> _numberList;

		[SerializeField]
		private PhysicalStateController _physicalStateController;

		[SerializeField]
		private ItemDatabaseSO _itemDatabase;

		[Header("Restaurant Audio")]
		[SerializeField]
		private AudioSource _restaurantAudioSource;

		[SerializeField]
		private AudioClipSO[] _restaurantHiBanks;

		[SerializeField]
		private AudioClipSO[] _restaurantHappyBanks;

		[SerializeField]
		private AudioClipSO _restaurantTalkingBank;

		[SerializeField]
		private AudioClipSO _restaurantEatingBank;

		[SerializeField]
		private AudioClipSO _restaurantOrderReceivedSfx;

		[SerializeField]
		private AudioClipSO _restaurantPaymentRequestBank;

		[SerializeField]
		private AudioClipSO _restaurantFarewellBank;

		[SerializeField]
		private AudioClipSO _restaurantSadLeaveBank;

		[SerializeField]
		[Range(0.05f, 2f)]
		private float _restaurantAmbientFadeOutSeconds = 0.4f;

		[SerializeField]
		[Min(0f)]
		private float _restaurantTalkingReplayMinDelay = 3f;

		[SerializeField]
		[Min(0f)]
		private float _restaurantTalkingReplayMaxDelay = 4f;

		[Header("Customer Timeout")]
		[SerializeField]
		private float _takeOrderTimeoutSeconds = 120f;

		[SerializeField]
		private float _deliverOrderTimeoutSeconds = 120f;

		[SerializeField]
		[Min(0f)]
		private float _deliverOrderGraceSeconds = 2f;

		[SerializeField]
		[Min(1f)]
		private float _customerTimeoutWarningSeconds = 30f;

		[SerializeField]
		[Min(1f)]
		private float _waitingCustomerRetrySeconds = 8f;

		[SerializeField]
		[Min(10f)]
		private float _waitingCustomerTimeoutSeconds = 90f;

		private bool _takeOrderTimerActive;

		private bool _deliverOrderTimerActive;

		private bool _takeOrderWarningShown;

		private bool _deliverOrderWarningShown;

		private double _takeOrderExpireServerTime;

		private double _deliverOrderExpireServerTime;

		private readonly HashSet<int> _groupServiceGraceSeats = new HashSet<int>();

		private double _nextConsistencyCheckServerTime;

		private double _waitingCustomersStartedServerTime;

		private double _nextWaitingCustomerRetryServerTime;

		private bool _cleaningBecauseCustomersLeft;

		private bool _customersLeavingBecauseOfTimeout;

		private NetworkVariable<RestaurantTableState> _currentState = new NetworkVariable<RestaurantTableState>(RestaurantTableState.Empty);

		private NetworkVariable<int> _seatedCount = new NetworkVariable<int>(-1);

		private NetworkVariable<double> _activeOrderExpireServerTime = new NetworkVariable<double>(0.0);

		private readonly NetworkVariable<int> _takeBackableTrayCount = new NetworkVariable<int>(0);

		private List<CustomerSeatOrder> _seatOrders = new List<CustomerSeatOrder>();

		private readonly List<string> _residentNames = new List<string>();

		private int _completedOrderCount;

		private int _completedCustomerCount;

		private int _cleaningRequiredTrayCount;

		private int _cleanedTrayCount;

		private int _totalPayment;

		private int _totalPaymentFinal;

		private bool _isTableNeedsToClean;

		private bool _restaurantHiPlayedForGroup;

		private bool _restaurantAmbientPlaying;

		private float _restaurantOneShotEndTime;

		private Coroutine _restaurantPendingAmbientRoutine;

		private Coroutine _restaurantAmbientFadeRoutine;

		private Coroutine _restaurantTalkingReplayRoutine;

		private int _restaurantAmbientPlaybackVersion;

		private AudioClip _lastRestaurantTalkingClip;

		private bool _preserveTalkingAcrossOrderTransition;

		private readonly List<Tray> _trays = new List<Tray>();

		private readonly List<DeliveredTrayRecord> _deliveredTrayRecords = new List<DeliveredTrayRecord>();

		private readonly Dictionary<int, ulong> _servingSteamIdBySeat = new Dictionary<int, ulong>();

		private readonly Dictionary<ulong, int> _customerCreditsBuffer = new Dictionary<ulong, int>();

		private RestaurantTableFeedback _feedback;

		private RestaurantManager _boundOwnershipManager;

		public List<Transform> SeatingPoints = new List<Transform>();

		private int _lateJoinTakingOrderSeed;

		private int _lateJoinLastSeatedCount;

		private int _stateVersion;

		private bool _isChangingState;

		private readonly List<DeliveredSeatVisualRecord> _deliveredSeatVisualRecords = new List<DeliveredSeatVisualRecord>();

		[SerializeField]
		private int _debugDiningCustomerCount = 1;

		[SerializeField]
		private bool _debugAutoArriveAllCustomers = true;

		[SerializeField]
		private bool _debugRepairIfNeeded = true;

		[SerializeField]
		private RestaurantTableState _debugTargetState;

		public int Id => _id;

		public PhysicalStateController PhysicalStateController => _physicalStateController;

		public RestaurantTableState CurrentState => _currentState.Value;

		public int SeatCount => _seatCount;

		public int ExpectedSeatedCount => _seatedCount.Value;

		public int ArrivedCustomerCount => _completedCustomerCount;

		public int TotalPaymentFinal => _totalPaymentFinal;

		public bool HasPendingIncomingCustomers
		{
			get
			{
				if (_currentState.Value == RestaurantTableState.WaitingOtherCustomers)
				{
					return _completedCustomerCount < _seatedCount.Value;
				}
				return false;
			}
		}

		public bool HasTakeBackableDeliveredTray => CanClientTryTakeBackDeliveredTray();

		public double ActiveOrderExpireServerTime => _activeOrderExpireServerTime.Value;

		public float ActiveOrderTimerDurationSeconds
		{
			get
			{
				if (_currentState.Value != RestaurantTableState.TakingOrder)
				{
					if (_currentState.Value != RestaurantTableState.DeliveringOrder)
					{
						return 0f;
					}
					return _deliverOrderTimeoutSeconds;
				}
				return _takeOrderTimeoutSeconds;
			}
		}

		private RestaurantTableFeedback Feedback
		{
			get
			{
				if (_feedback == null && !TryGetComponent<RestaurantTableFeedback>(out _feedback))
				{
					_feedback = base.gameObject.AddComponent<RestaurantTableFeedback>();
				}
				return _feedback;
			}
		}

		public IEnumerable<CustomerSeatOrder> ServerStaffOrders
		{
			get
			{
				if (!base.IsServer || CurrentState != RestaurantTableState.DeliveringOrder || HasDeliverOrderDeadlineExpired())
				{
					return Enumerable.Empty<CustomerSeatOrder>();
				}
				return _seatOrders.Where((CustomerSeatOrder s) => s != null && s.Order?.HasMainDish == true && s.OrderId >= 0);
			}
		}

		public bool ServerHasOrderForRecipe(string recipeId)
		{
			if (!base.IsServer || string.IsNullOrWhiteSpace(recipeId) || _seatOrders == null)
			{
				return false;
			}
			for (int i = 0; i < _seatOrders.Count; i++)
			{
				CustomerOrderData customerOrderData = _seatOrders[i]?.Order;
				if (customerOrderData != null && string.Equals(customerOrderData.MainDishIdentityId, recipeId, StringComparison.Ordinal))
				{
					return true;
				}
			}
			return false;
		}

		public bool TryGetSeatOrder(int seatIndex, out CustomerOrderData order)
		{
			order = null;
			if (_seatOrders == null || seatIndex < 0 || seatIndex >= _seatOrders.Count)
			{
				return false;
			}
			CustomerSeatOrder customerSeatOrder = _seatOrders[seatIndex];
			if (customerSeatOrder == null || customerSeatOrder.Order == null)
			{
				return false;
			}
			order = customerSeatOrder.Order;
			return true;
		}

		private void Start()
		{
			_ = Feedback;
			RefreshTableNumberTexts();
			if (_debugText != null)
			{
				_debugText.text = "State: Empty";
			}
			_completedOrderCount = 0;
			_completedCustomerCount = 0;
			_totalPayment = 0;
			_totalPaymentFinal = 0;
			_residentNames.Clear();
			SetAllSeatAvailableObjectsActive(isActive: true);
		}

		private void Update()
		{
			if (base.IsServer)
			{
				ServerTickCustomerTimeouts();
				ServerTickWaitingCustomerRecovery();
				ServerTickTableConsistency();
				Feedback.ServerTickReminder(this, _id, _currentState.Value, GetServerTimeSafe());
				if (Feedback.ServerTryGetAmbientMoment(_currentState.Value, Mathf.Clamp(_seatedCount.Value, 0, _seatCount), GetServerTimeSafe(), out var speakerSeatIndex, out var cue, out var gesture))
				{
					ServerSendClientPlayRestaurantMoment(speakerSeatIndex, (byte)cue, (byte)gesture);
				}
			}
		}

		private void ServerTickCustomerTimeouts()
		{
			double serverTimeSafe = GetServerTimeSafe();
			if (_takeOrderTimerActive)
			{
				if (_currentState.Value != RestaurantTableState.TakingOrder)
				{
					ServerStopTakeOrderTimer();
				}
				else if (!_takeOrderWarningShown && serverTimeSafe < _takeOrderExpireServerTime && _takeOrderExpireServerTime - serverTimeSafe <= (double)_customerTimeoutWarningSeconds)
				{
					_takeOrderWarningShown = true;
					ServerSendClientShowTableToastToEveryone($"Table {_id}: {Mathf.CeilToInt((float)(_takeOrderExpireServerTime - serverTimeSafe))} seconds left to take the order.");
				}
				else if (serverTimeSafe >= _takeOrderExpireServerTime)
				{
					ServerStopTakeOrderTimer();
					ServerHandleTakeOrderTimeout();
				}
			}
			if (_deliverOrderTimerActive)
			{
				if (_currentState.Value != RestaurantTableState.DeliveringOrder)
				{
					ServerStopDeliverOrderTimer();
				}
				else if (!_deliverOrderWarningShown && serverTimeSafe < _deliverOrderExpireServerTime && _deliverOrderExpireServerTime - serverTimeSafe <= (double)_customerTimeoutWarningSeconds)
				{
					_deliverOrderWarningShown = true;
					ServerSendClientShowTableToastToEveryone($"Table {_id}: {Mathf.CeilToInt((float)(_deliverOrderExpireServerTime - serverTimeSafe))} seconds left to deliver the order.");
				}
				else if (serverTimeSafe >= GetDeliverOrderEffectiveExpireServerTime())
				{
					ServerStopDeliverOrderTimer();
					ServerHandleDeliverOrderTimeout();
				}
			}
		}

		private void OnValidate()
		{
			RefreshTableNumberTexts();
		}

		private void RefreshTableNumberTexts()
		{
			if (_numberList == null)
			{
				return;
			}
			string text = _id.ToString();
			for (int i = 0; i < _numberList.Count; i++)
			{
				if (!(_numberList[i] == null))
				{
					_numberList[i].text = text;
				}
			}
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<int> takeBackableTrayCount = _takeBackableTrayCount;
			takeBackableTrayCount.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Combine(takeBackableTrayCount.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnTakeBackableTrayCountChanged));
			if (_physicalStateController != null)
			{
				_physicalStateController.OnPhysicallyUsableChanged += HandlePhysicalRepair;
			}
			else
			{
				UtilityMethods.Log("[RestaurantTable] PhysicalStateController is missing.", LogType.Warning);
			}
			BindDiningTableOwnershipEvents();
			HandlePhysicalRepair(IsPhysicallyUsableSafe());
		}

		private void ServerTickWaitingCustomerRecovery()
		{
			if (_currentState.Value != RestaurantTableState.WaitingOtherCustomers)
			{
				return;
			}
			int num = Mathf.Clamp(_seatedCount.Value, 0, _seatCount);
			if (num <= 0)
			{
				ServerChangeState(RestaurantTableState.Empty);
				return;
			}
			double serverTimeSafe = GetServerTimeSafe();
			if (serverTimeSafe - _waitingCustomersStartedServerTime >= (double)Mathf.Max(10f, _waitingCustomerTimeoutSeconds))
			{
				ResidentManager instance = Singleton<ResidentManager>.Instance;
				int num2 = ((instance != null) ? instance.CompactArrivedDiningTableAssignments(_id, SeatingPoints, _residentNames) : 0);
				if (num2 <= 0)
				{
					UtilityMethods.Log($"[RestaurantTable] Waiting customer timeout with no arrived residents. Releasing group. TableId={_id}, Expected={num}", LogType.Warning);
					ServerChangeState(RestaurantTableState.Empty);
					return;
				}
				_seatedCount.Value = num2;
				_lateJoinLastSeatedCount = num2;
				_completedCustomerCount = num2;
				SendCustomerMenuWaitingForArrivedSeats(isWaiting: false);
				UtilityMethods.Log($"[RestaurantTable] Waiting customer timeout recovered with partial group. TableId={_id}, Continuing={num2}, OriginalExpected={num}", LogType.Warning);
				ServerChangeState(RestaurantTableState.TakingOrder);
				_completedCustomerCount = 0;
			}
			else
			{
				if (serverTimeSafe < _nextWaitingCustomerRetryServerTime)
				{
					return;
				}
				_nextWaitingCustomerRetryServerTime = serverTimeSafe + (double)Mathf.Max(1f, _waitingCustomerRetrySeconds);
				ResidentManager instance2 = Singleton<ResidentManager>.Instance;
				if (!(instance2 == null))
				{
					int num3 = instance2.RestartDiningTableMovements(_id);
					int num4 = ServerReconcileDiningAssignments(num, out var removedCount);
					int num5 = ((num4 < num) ? instance2.CallMissingResidentsForRestaurant(num, _id) : 0);
					if (removedCount > 0 || num5 > 0)
					{
						UtilityMethods.Log($"[RestaurantTable] Waiting customer recovery. TableId={_id}, Expected={num}, Arrived={_completedCustomerCount}, ValidAssignments={num4}, Restarted={num3}, Removed={removedCount}, Replacements={num5}", LogType.Warning);
					}
				}
			}
		}

		public override void OnNetworkDespawn()
		{
			StopRestaurantTableAudio(includeOneShots: true);
			NetworkVariable<int> takeBackableTrayCount = _takeBackableTrayCount;
			takeBackableTrayCount.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Remove(takeBackableTrayCount.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(OnTakeBackableTrayCountChanged));
			if (_physicalStateController != null)
			{
				_physicalStateController.OnPhysicallyUsableChanged -= HandlePhysicalRepair;
			}
			UnbindDiningTableOwnershipEvents();
			base.OnNetworkDespawn();
		}

		private void OnTakeBackableTrayCountChanged(int oldValue, int newValue)
		{
			RefreshGazeFeedback();
		}

		private void HandlePhysicalRepair(bool value)
		{
			if (_interactableTooltip != null)
			{
				if (value)
				{
					_interactableTooltip.ClearRuntimeTooltip();
					_interactableTooltip.IsEnabled = false;
					if (Singleton<ToolTipHandler>.Instance != null)
					{
						Singleton<ToolTipHandler>.Instance.OnGazeExit(_interactableTooltip);
					}
				}
				else
				{
					_interactableTooltip.IsEnabled = true;
					RefreshUnlockTooltipLocal();
				}
			}
			if (_inGaze)
			{
				DisableGazeHints();
				EnableGazeHints();
			}
		}

		protected override void ClientCrossInteract(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				if (IsPhysicallyBrokenSafe())
				{
					TryUnlockTableClient(interactor);
				}
				else if (CanClientTryDisableTable())
				{
					TryDisableTableClient(interactor);
				}
			}
		}

		protected override void ClientInteract(Interactor interactor, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				if (IsPhysicallyBrokenSafe())
				{
					TryUnlockTableClient(interactor);
				}
				else if (CanClientTryDisableTable())
				{
					TryDisableTableClient(interactor);
				}
			}
		}

		private void TryUnlockTableClient(Interactor interactor)
		{
			if (IsPhysicallyBrokenSafe())
			{
				ServerTryUnlockTableRpc(interactor.NetworkObject.ToReference());
			}
		}

		private void TryDisableTableClient(Interactor interactor)
		{
			if (CanClientTryDisableTable())
			{
				ServerTryDisableTableRpc(interactor.NetworkObject.ToReference());
			}
		}

		private bool CanClientTryDisableTable()
		{
			if (IsPhysicallyUsableSafe() && _currentState.Value == RestaurantTableState.Empty && _seatedCount.Value <= 0)
			{
				return _completedCustomerCount == 0;
			}
			return false;
		}

		public override void GazeEnter()
		{
			BindDiningTableOwnershipEvents();
			base.GazeEnter();
			if (IsPhysicallyBrokenSafe())
			{
				RefreshUnlockTooltipLocal();
				if (_interactableTooltip != null && _interactableTooltip.IsEnabled && Singleton<ToolTipHandler>.Instance != null)
				{
					Singleton<ToolTipHandler>.Instance.OnGazeEnter(_interactableTooltip, _interactableTooltip.GetWorldPosition());
				}
			}
		}

		public override void GazeExit()
		{
			base.GazeExit();
			ClearTooltip();
		}

		private void ClearTooltip()
		{
			if (_interactableTooltip != null && _interactableTooltip.IsEnabled && Singleton<ToolTipHandler>.Instance != null)
			{
				Singleton<ToolTipHandler>.Instance.OnGazeExit(_interactableTooltip);
			}
		}

		protected override void ClientMouse1(Interactor interactor, InputActionPhase phase)
		{
			if (phase != InputActionPhase.Started)
			{
				return;
			}
			switch (_currentState.Value)
			{
			case RestaurantTableState.TakingOrder:
				ServerConfirmOrderRpc(interactor.NetworkObject.ToReference());
				break;
			case RestaurantTableState.DeliveringOrder:
				ServerDeliveringOrderInteractRpc(interactor.NetworkObject.ToReference());
				break;
			case RestaurantTableState.AwaitingPayment:
				ServerAwaitingPaymentInteractRpc(interactor.NetworkObject.ToReference());
				break;
			case RestaurantTableState.Cleaning:
			{
				if (!CanClientTryPickUpTray(interactor))
				{
					Singleton<NotifyManager>.Instance.ShowToast("No free quick slot for the dirty tray.", NotificationState.Info, 2f);
					break;
				}
				NetworkObjectReference expectedHeldRef = ((interactor.CurrentHeld != null && interactor.CurrentHeld.NetworkObject != null) ? interactor.CurrentHeld.NetworkObject.ToReference() : default(NetworkObjectReference));
				ServerCleaningInteractRpc(interactor.NetworkObject.ToReference(), expectedHeldRef);
				break;
			}
			case RestaurantTableState.WaitingOtherCustomers:
				Singleton<NotifyManager>.Instance.ShowToast("Waiting for other customers to arrive.", NotificationState.Info, 2f);
				break;
			case RestaurantTableState.Empty:
			case RestaurantTableState.Eating:
				break;
			}
		}

		protected override void ClientCrossMouse1(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			ClientMouse1(interactor, phase);
		}

		protected override void ClientMouse2(Interactor interactor, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && CanClientTryTakeBackDeliveredTray())
			{
				if (!CanClientTryPickUpTray(interactor))
				{
					Singleton<NotifyManager>.Instance.ShowToast("No free quick slot for the tray.", NotificationState.Info, 2f);
				}
				else
				{
					ServerTakeBackDeliveredTrayRpc(interactor.NetworkObject.ToReference());
				}
			}
		}

		protected override void ClientCrossMouse2(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			ClientMouse2(interactor, phase);
		}

		private bool CanClientTryTakeBackDeliveredTray()
		{
			if (_currentState.Value == RestaurantTableState.DeliveringOrder)
			{
				return _takeBackableTrayCount.Value > 0;
			}
			return false;
		}

		private static bool CanClientTryPickUpTray(Interactor interactor)
		{
			if (interactor != null && Singleton<QuickSlotHandler>.Instance != null)
			{
				return Singleton<QuickSlotHandler>.Instance.AnySlotAvailable;
			}
			return false;
		}

		private void ServerTrackDeliveredSeatVisual(Tray tray, int seatIndex)
		{
			if (!base.IsServer || tray == null || !IsValidSeatIndex(seatIndex))
			{
				return;
			}
			for (int num = _deliveredSeatVisualRecords.Count - 1; num >= 0; num--)
			{
				DeliveredSeatVisualRecord deliveredSeatVisualRecord = _deliveredSeatVisualRecords[num];
				if (deliveredSeatVisualRecord == null || deliveredSeatVisualRecord.Tray == null)
				{
					_deliveredSeatVisualRecords.RemoveAt(num);
				}
				else if (deliveredSeatVisualRecord.Tray == tray || deliveredSeatVisualRecord.SeatIndex == seatIndex)
				{
					_deliveredSeatVisualRecords.RemoveAt(num);
				}
			}
			_deliveredSeatVisualRecords.Add(new DeliveredSeatVisualRecord
			{
				Tray = tray,
				SeatIndex = seatIndex
			});
		}

		private void ServerRestoreDeliveredSeatVisualForTray(Tray tray)
		{
			if (!base.IsServer || tray == null)
			{
				return;
			}
			for (int num = _deliveredSeatVisualRecords.Count - 1; num >= 0; num--)
			{
				DeliveredSeatVisualRecord deliveredSeatVisualRecord = _deliveredSeatVisualRecords[num];
				if (deliveredSeatVisualRecord == null || deliveredSeatVisualRecord.Tray == null)
				{
					_deliveredSeatVisualRecords.RemoveAt(num);
				}
				else if (!(deliveredSeatVisualRecord.Tray != tray))
				{
					int seatIndex = deliveredSeatVisualRecord.SeatIndex;
					_deliveredSeatVisualRecords.RemoveAt(num);
					ServerSendClientSetSeatAvailableObjectActive(seatIndex, isActive: true);
					break;
				}
			}
		}

		private void ServerTickTableConsistency()
		{
			double serverTimeSafe = GetServerTimeSafe();
			if (serverTimeSafe < _nextConsistencyCheckServerTime)
			{
				return;
			}
			_nextConsistencyCheckServerTime = serverTimeSafe + 2.0;
			RestaurantTableState value = _currentState.Value;
			if (value == RestaurantTableState.Empty || value == RestaurantTableState.WaitingOtherCustomers || value == RestaurantTableState.Cleaning)
			{
				return;
			}
			int num = Mathf.Clamp(_seatedCount.Value, 0, _seatCount);
			int num2 = ServerReconcileDiningAssignments(num, out var _);
			if (value != RestaurantTableState.TakingOrder || num <= 0)
			{
				return;
			}
			int activeSeatOrderCount = GetActiveSeatOrderCount();
			if (HasCompleteSeatOrderSet(num, activeSeatOrderCount))
			{
				return;
			}
			if (num2 >= 0 && num2 != num)
			{
				UtilityMethods.Log("[RestaurantTable] TakingOrder consistency check is waiting for valid resident assignments. " + $"TableId={_id}, Expected={num}, ValidAssignments={num2}, ActiveOrders={activeSeatOrderCount}", LogType.Warning);
			}
			else
			{
				UtilityMethods.Log("[RestaurantTable] Repairing incomplete seat orders before confirmation. " + $"TableId={_id}, Expected={num}, ActiveOrders={activeSeatOrderCount}", LogType.Warning);
				if (ServerGenerateSeatOrders() && !_takeOrderTimerActive)
				{
					ServerStartTakeOrderTimer();
				}
			}
		}

		private int ServerReconcileDiningAssignments(int expectedSeatCount, out int removedCount)
		{
			removedCount = 0;
			ResidentManager instance = Singleton<ResidentManager>.Instance;
			if (instance == null)
			{
				return -1;
			}
			return instance.ReconcileDiningTableAssignments(_id, expectedSeatCount, out removedCount);
		}

		private static bool HasCompleteSeatOrderSet(int expectedSeatCount, int activeOrderCount)
		{
			if (expectedSeatCount > 0)
			{
				return activeOrderCount == expectedSeatCount;
			}
			return false;
		}

		private void ServerRestoreAllDeliveredSeatVisuals()
		{
			if (base.IsServer)
			{
				_deliveredSeatVisualRecords.Clear();
				ServerSendClientSetAllSeatAvailableObjectsActive(isActive: true);
			}
		}

		private byte ServerBuildDeliveredSeatMask()
		{
			byte b = 0;
			for (int num = _deliveredSeatVisualRecords.Count - 1; num >= 0; num--)
			{
				DeliveredSeatVisualRecord deliveredSeatVisualRecord = _deliveredSeatVisualRecords[num];
				if (deliveredSeatVisualRecord == null || deliveredSeatVisualRecord.Tray == null || !IsValidSeatIndex(deliveredSeatVisualRecord.SeatIndex))
				{
					_deliveredSeatVisualRecords.RemoveAt(num);
				}
				else
				{
					b |= (byte)(1 << deliveredSeatVisualRecord.SeatIndex);
				}
			}
			return b;
		}

		private bool IsValidSeatIndex(int seatIndex)
		{
			if (seatIndex >= 0)
			{
				return seatIndex < _seatCount;
			}
			return false;
		}

		private void ServerSendClientSetSeatAvailableObjectActive(int seatIndex, bool isActive)
		{
			if (base.IsServer)
			{
				ClientSetSeatAvailableObjectActiveRevisionRpc(seatIndex, isActive, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		private void ApplyClientSetSeatAvailableObjectActive(int seatIndex, bool isActive)
		{
			SetSeatAvailableObjectActive(seatIndex, isActive);
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientSetSeatAvailableObjectActiveRevisionRpc(int seatIndex, bool isActive, ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3513808002u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, seatIndex);
				bufferWriter.WriteValueSafe(in isActive, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 3513808002u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, (seatIndex, isActive), (RestaurantTable target, (int seatIndex, bool isActive) data) =>
				{
					target.ApplyClientSetSeatAvailableObjectActive(data.seatIndex, data.isActive);
				}, 4096, worldRevision))
				{
					ApplyClientSetSeatAvailableObjectActive(seatIndex, isActive);
				}
			}
		}

		private void ApplyClientSetAllSeatAvailableObjectsActive(bool isActive)
		{
			SetAllSeatAvailableObjectsActive(isActive);
		}

		private void ServerSendClientSetAllSeatAvailableObjectsActive(bool isActive)
		{
			if (base.IsServer)
			{
				ClientSetAllSeatAvailableObjectsActiveRevisionRpc(isActive, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientSetAllSeatAvailableObjectsActiveRevisionRpc(bool isActive, ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(148841439u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in isActive, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 148841439u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, isActive, (RestaurantTable target, bool data) =>
				{
					target.ApplyClientSetAllSeatAvailableObjectsActive(data);
				}, 4096, worldRevision))
				{
					ApplyClientSetAllSeatAvailableObjectsActive(isActive);
				}
			}
		}

		private void ApplySeatAvailableObjectMask(byte deliveredSeatMask)
		{
			for (int i = 0; i < _seatCount; i++)
			{
				SetSeatAvailableObjectActive(i, (deliveredSeatMask & (1 << i)) == 0);
			}
		}

		private void SetAllSeatAvailableObjectsActive(bool isActive)
		{
			if (_seatAvailableObjects != null)
			{
				for (int i = 0; i < _seatAvailableObjects.Count; i++)
				{
					SetSeatAvailableObjectActive(i, isActive);
				}
			}
		}

		private void SetSeatAvailableObjectActive(int seatIndex, bool isActive)
		{
			if (_seatAvailableObjects != null && seatIndex >= 0 && seatIndex < _seatAvailableObjects.Count)
			{
				GameObject gameObject = _seatAvailableObjects[seatIndex];
				if (!(gameObject == null) && gameObject.activeSelf != isActive)
				{
					gameObject.SetActive(isActive);
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerTryUnlockTableRpc(NetworkObjectReference interactorRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(49826291u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 49826291u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsServer)
			{
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!interactorRef.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != senderClientId)
			{
				return;
			}
			RestaurantManager restaurantManager = Singleton<GameProgressManager>.Instance.RestaurantManager;
			if (restaurantManager == null)
			{
				UtilityMethods.Log("[RestaurantTable] Unlock failed. RestaurantManager is null.", LogType.Warning);
				return;
			}
			if (restaurantManager.IsDiningTablePermanentlyUnlocked(_id))
			{
				TryRepairPhysicalState();
				restaurantManager.ServerRecordDiningTableOpenState(_id, isOpen: true);
				restaurantManager.ServerAddWavesForNewUnlockedTable();
				return;
			}
			int permanentlyUnlockedDiningTableCount = restaurantManager.GetPermanentlyUnlockedDiningTableCount();
			if (!Singleton<GameProgressManager>.Instance.CanUnlockMoreDiningTables(permanentlyUnlockedDiningTableCount))
			{
				BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(component.OwnerClientId, RpcTargetUse.Temp);
				ServerSendClientShowProgressionLockedNotify(baseRpcTarget);
				return;
			}
			int nextDiningTableUnlockPrice = restaurantManager.GetNextDiningTableUnlockPrice();
			ServiceLocator.ForSceneOf(this).Get<EconomyHandler>(out var service);
			if (!service.HasSufficientBalance(nextDiningTableUnlockPrice))
			{
				BaseRpcTarget baseRpcTarget2 = base.RpcTarget.Single(component.OwnerClientId, RpcTargetUse.Temp);
				ServerSendClientShowNotify(baseRpcTarget2);
				return;
			}
			service.SpendMoney(nextDiningTableUnlockPrice);
			bool isAnyRestaurantTableUnlocked = restaurantManager.IsAnyRestaurantTableUnlocked;
			restaurantManager.ServerMarkDiningTableUnlocked(_id);
			TryRepairPhysicalState();
			restaurantManager.ServerRecordDiningTableOpenState(_id, isOpen: true);
			restaurantManager.ServerAddWavesForNewUnlockedTable();
			if (!isAnyRestaurantTableUnlocked)
			{
				QuestReferenceHub.Instance.QuestComplete(QuestId.RepairTheDiningTable);
			}
			else
			{
				QuestReferenceHub.Instance.QuestComplete(QuestId.UnlockAnotherTable);
			}
		}

		private void TryRepairPhysicalState()
		{
			if (!(_physicalStateController == null) && !_physicalStateController.IsPhysicallyUsable)
			{
				_physicalStateController.Repair();
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerTryDisableTableRpc(NetworkObjectReference interactorRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(163596466u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 163596466u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (base.IsServer && interactorRef.TryGetComponent<Interactor>(out var component) && component.OwnerClientId == rpcParams.Receive.SenderClientId && CanServerDisableTable())
				{
					RestaurantManager obj = Singleton<GameProgressManager>.Instance?.RestaurantManager;
					obj?.ServerMarkDiningTableUnlocked(_id);
					_physicalStateController.Break();
					obj?.ServerRecordDiningTableOpenState(_id, isOpen: false);
				}
			}
		}

		private bool CanServerDisableTable()
		{
			if (_physicalStateController != null && _physicalStateController.IsPhysicallyUsable && _currentState.Value == RestaurantTableState.Empty && _seatedCount.Value <= 0)
			{
				return _completedCustomerCount == 0;
			}
			return false;
		}

		private void ApplyClientShowProgressionLockedNotify()
		{
			Singleton<NotifyManager>.Instance?.ShowToast("Reach the required restaurant level first.", NotificationState.Info, 2f);
		}

		private void ServerSendClientShowProgressionLockedNotify(RpcParams rpcParams = default(RpcParams))
		{
			if (base.IsServer)
			{
				ClientShowProgressionLockedNotifyRevisionRpc(BootstrapLiveUpdateBuffer.NextServerRevision(), rpcParams);
			}
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientShowProgressionLockedNotifyRevisionRpc(ulong worldRevision, RpcParams rpcParams = default(RpcParams))
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
					InvokePermission = RpcInvokePermission.Server
				};
				FastBufferWriter bufferWriter = __beginSendRpc(2403997508u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 2403997508u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, 0, (RestaurantTable target, int data) =>
				{
					target.ApplyClientShowProgressionLockedNotify();
				}, 4096, worldRevision))
				{
					ApplyClientShowProgressionLockedNotify();
				}
			}
		}

		public int GetCurrentUnlockPrice()
		{
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (restaurantManager == null)
			{
				return 100;
			}
			return restaurantManager.GetNextDiningTableUnlockPrice();
		}

		private void RefreshUnlockTooltipLocal()
		{
			if (!(_interactableTooltip == null))
			{
				TooltipData baseTooltip = _interactableTooltip.GetBaseTooltip();
				if (IsPermanentlyUnlockedLocal())
				{
					baseTooltip.Title = LocalizationDatabase.GetString("UiTable", "progression.unlock.name.dining_table", "Dining Table");
				}
				else
				{
					baseTooltip.Title = ProgressionTooltipLocalization.GetTitle("progression.unlock.title.broken_table", string.IsNullOrWhiteSpace(baseTooltip.Title) ? "Broken Table" : baseTooltip.Title);
				}
				baseTooltip.Body = GetUnlockTooltipBody();
				baseTooltip.OnlyHeader = false;
				_interactableTooltip.SetRuntimeTooltip(baseTooltip);
			}
		}

		private string GetUnlockTooltipBody()
		{
			GameProgressManager instance = Singleton<GameProgressManager>.Instance;
			RestaurantManager restaurantManager = ((instance != null) ? instance.RestaurantManager : null);
			if (restaurantManager != null && restaurantManager.IsDiningTablePermanentlyUnlocked(_id))
			{
				return LocalizationDatabase.GetString("GameStateTable", "key_open", "Open");
			}
			int currentlyUnlockedCount = ((restaurantManager != null) ? restaurantManager.GetPermanentlyUnlockedDiningTableCount() : 0);
			if (instance != null && !instance.CanUnlockMoreDiningTables(currentlyUnlockedCount))
			{
				int requiredLevelForNextDiningTableUnlock = instance.GetRequiredLevelForNextDiningTableUnlock(currentlyUnlockedCount);
				if (requiredLevelForNextDiningTableUnlock <= 0)
				{
					return ProgressionTooltipLocalization.GetRequiredLevelText();
				}
				return ProgressionTooltipLocalization.GetReachLevelText(requiredLevelForNextDiningTableUnlock);
			}
			return ProgressionTooltipLocalization.GetRepairCostText(GetCurrentUnlockPrice());
		}

		private bool IsPermanentlyUnlockedLocal()
		{
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (restaurantManager != null)
			{
				return restaurantManager.IsDiningTablePermanentlyUnlocked(_id);
			}
			return false;
		}

		private void BindDiningTableOwnershipEvents()
		{
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (!(_boundOwnershipManager == restaurantManager))
			{
				UnbindDiningTableOwnershipEvents();
				_boundOwnershipManager = restaurantManager;
				if (_boundOwnershipManager != null)
				{
					_boundOwnershipManager.DiningTableOwnershipChanged += HandleDiningTableOwnershipChanged;
				}
			}
		}

		private void UnbindDiningTableOwnershipEvents()
		{
			if (_boundOwnershipManager != null)
			{
				_boundOwnershipManager.DiningTableOwnershipChanged -= HandleDiningTableOwnershipChanged;
			}
			_boundOwnershipManager = null;
		}

		private void HandleDiningTableOwnershipChanged()
		{
			if (!IsPhysicallyBrokenSafe())
			{
				return;
			}
			if (_interactableTooltip != null)
			{
				_interactableTooltip.IsEnabled = true;
				RefreshUnlockTooltipLocal();
				if (_inGaze && Singleton<ToolTipHandler>.Instance != null)
				{
					Singleton<ToolTipHandler>.Instance.OnGazeEnter(_interactableTooltip, _interactableTooltip.GetWorldPosition());
				}
			}
			RefreshGazeFeedback();
		}

		private bool IsPhysicallyBrokenSafe()
		{
			if (_physicalStateController != null)
			{
				return !_physicalStateController.IsPhysicallyUsable;
			}
			return false;
		}

		private bool IsPhysicallyUsableSafe()
		{
			if (!(_physicalStateController == null))
			{
				return _physicalStateController.IsPhysicallyUsable;
			}
			return true;
		}

		private bool CanRunServerDebug(string actionName)
		{
			if (!Application.isPlaying)
			{
				UtilityMethods.Log("<b><color=orange>[RestaurantTable DEBUG]</color></b> " + actionName + " blocked. Application is not playing.", LogType.Warning);
				return false;
			}
			if (!base.IsServer)
			{
				UtilityMethods.Log("<b><color=orange>[RestaurantTable DEBUG]</color></b> " + actionName + " blocked. Only server/host can run this debug action.", LogType.Warning);
				return false;
			}
			return true;
		}

		private void ServerInvalidateDelayedActions()
		{
			_stateVersion++;
		}

		private double GetServerTimeSafe()
		{
			if (NetworkManager.Singleton == null)
			{
				return Time.timeAsDouble;
			}
			return NetworkManager.Singleton.ServerTime.Time;
		}

		private void ServerStartTakeOrderTimer()
		{
			if (base.IsServer)
			{
				_takeOrderExpireServerTime = GetServerTimeSafe() + (double)Mathf.Max(1f, _takeOrderTimeoutSeconds);
				_takeOrderTimerActive = true;
				_takeOrderWarningShown = false;
				_activeOrderExpireServerTime.Value = _takeOrderExpireServerTime;
				UtilityMethods.Log($"[RestaurantTable] Take order timer started. TableId={_id}, ExpiresAt={_takeOrderExpireServerTime:0.00}");
			}
		}

		private void ServerStopTakeOrderTimer()
		{
			if (base.IsServer)
			{
				_takeOrderTimerActive = false;
				_takeOrderExpireServerTime = 0.0;
				_activeOrderExpireServerTime.Value = 0.0;
			}
		}

		private void ServerStartDeliverOrderTimer()
		{
			if (base.IsServer)
			{
				double serverTimeSafe = GetServerTimeSafe();
				if (_deliverOrderExpireServerTime <= serverTimeSafe)
				{
					_deliverOrderExpireServerTime = serverTimeSafe + (double)Mathf.Max(1f, _deliverOrderTimeoutSeconds);
				}
				_deliverOrderTimerActive = true;
				_groupServiceGraceSeats.Clear();
				_deliverOrderWarningShown = false;
				_activeOrderExpireServerTime.Value = _deliverOrderExpireServerTime;
				UtilityMethods.Log($"[RestaurantTable] Deliver order timer started. TableId={_id}, Remaining={Mathf.Max(0f, (float)(_deliverOrderExpireServerTime - serverTimeSafe)):0.00}");
			}
		}

		private void ServerStopDeliverOrderTimer()
		{
			if (base.IsServer)
			{
				_deliverOrderTimerActive = false;
				_deliverOrderExpireServerTime = 0.0;
				_activeOrderExpireServerTime.Value = 0.0;
			}
		}

		private void ServerGrantGroupServiceGrace(int seatIndex)
		{
			int num = Mathf.Clamp(_seatedCount.Value, 0, _seatCount);
			if (base.IsServer && _deliverOrderTimerActive && num >= 2 && _seatOrders.Any((CustomerSeatOrder s) => s?.Order != null) && _groupServiceGraceSeats.Count < num - 1 && _groupServiceGraceSeats.Add(seatIndex))
			{
				_deliverOrderExpireServerTime += 10.0;
				_activeOrderExpireServerTime.Value = _deliverOrderExpireServerTime;
				ServiceLocator.ForSceneOf(this).Get<TabletNetworkHandler>(out var service);
				service?.ServerExtendTableOrderDeadline(_id.ToString(), _deliverOrderExpireServerTime);
			}
		}

		private double GetDeliverOrderEffectiveExpireServerTime()
		{
			return _deliverOrderExpireServerTime + (double)Mathf.Max(0f, _deliverOrderGraceSeconds);
		}

		private bool HasTakeOrderDeadlineExpired()
		{
			if (_takeOrderTimerActive)
			{
				return GetServerTimeSafe() >= _takeOrderExpireServerTime;
			}
			return false;
		}

		private bool HasDeliverOrderDeadlineExpired()
		{
			if (_deliverOrderTimerActive)
			{
				return GetServerTimeSafe() >= GetDeliverOrderEffectiveExpireServerTime();
			}
			return false;
		}

		private void ServerHandleTakeOrderTimeout()
		{
			if (base.IsServer && _currentState.Value == RestaurantTableState.TakingOrder)
			{
				ServerSendClientShowTableToastToEveryone($"Table {_id}: Customer left because their order was not taken in time.", NotificationState.Error);
				ServerSendClientPlayRestaurantAudio(6);
				string text = ((_residentNames.Count == 0) ? "-" : string.Join(", ", _residentNames));
				UtilityMethods.Log("<b><color=#FFAA00>[RestaurantTable Timeout]</color></b>\n  Reason          : TakeOrderTimeout\n" + $"  TableId         : {_id}\n" + "  GameObject      : " + base.name + "\n" + $"  NetworkObjectId : {base.NetworkObjectId}\n" + $"  State           : {_currentState.Value}\n" + $"  SeatedCount     : {_seatedCount.Value}\n" + "  Residents       : " + text);
				ServerInvalidateDelayedActions();
				ServerStopTakeOrderTimer();
				ServerStopDeliverOrderTimer();
				ServiceLocator.ForSceneOf(this).Get<TabletNetworkHandler>(out var service);
				if (service != null)
				{
					service.ServerRemoveOrdersForTable(_id.ToString());
				}
				ClearSeatOrdersRuntime();
				_completedOrderCount = 0;
				_completedCustomerCount = 0;
				_cleaningRequiredTrayCount = 0;
				_cleanedTrayCount = 0;
				_totalPayment = 0;
				_totalPaymentFinal = 0;
				_isTableNeedsToClean = false;
				_cleaningBecauseCustomersLeft = false;
				ServerStartCustomersLeaveBecauseOfTimeout("TakeOrderTimeout");
				_residentNames.Clear();
				_customersLeavingBecauseOfTimeout = false;
				ServerChangeState(RestaurantTableState.Empty);
			}
		}

		private void ServerHandleDeliverOrderTimeout()
		{
			if (base.IsServer && _currentState.Value == RestaurantTableState.DeliveringOrder)
			{
				ServerSendClientShowTableToastToEveryone($"Table {_id}: Customer left because their order was not delivered in time.", NotificationState.Error);
				ServerSendClientPlayRestaurantAudio(6);
				UtilityMethods.Log($"[RestaurantTable] Deliver order timeout. Customers left. TableId={_id}, TrayCount={_trays.Count}");
				ServerForceCustomersLeaveAfterDeliveryTimeout();
			}
		}

		private void ServerForceCustomersLeaveAfterDeliveryTimeout()
		{
			if (base.IsServer)
			{
				string text = ((_residentNames.Count == 0) ? "-" : string.Join(", ", _residentNames));
				UtilityMethods.Log("<b><color=#FFAA00>[RestaurantTable Timeout]</color></b>\n  Reason          : DeliverOrderTimeout\n" + $"  TableId         : {_id}\n" + "  GameObject      : " + base.name + "\n" + $"  NetworkObjectId : {base.NetworkObjectId}\n" + $"  State           : {_currentState.Value}\n" + $"  SeatedCount     : {_seatedCount.Value}\n" + $"  TrayCount       : {_trays.Count}\n" + "  Residents       : " + text);
				ServerInvalidateDelayedActions();
				ServerStopTakeOrderTimer();
				ServerStopDeliverOrderTimer();
				ServiceLocator.ForSceneOf(this).Get<TabletNetworkHandler>(out var service);
				if (service != null)
				{
					service.ServerRemoveOrdersForTable(_id.ToString());
				}
				ClearSeatOrdersRuntime();
				_completedOrderCount = 0;
				_completedCustomerCount = 0;
				_totalPayment = 0;
				_totalPaymentFinal = 0;
				_isTableNeedsToClean = false;
				_cleanedTrayCount = 0;
				_cleaningRequiredTrayCount = Mathf.Max(0, _trays.Count);
				ServerStartCustomersLeaveBecauseOfTimeout("DeliverOrderTimeout");
				_residentNames.Clear();
				if (_trays.Count > 0)
				{
					_cleaningBecauseCustomersLeft = true;
					ServerChangeState(RestaurantTableState.Cleaning);
				}
				else
				{
					_cleaningBecauseCustomersLeft = false;
					_customersLeavingBecauseOfTimeout = false;
					ServerChangeState(RestaurantTableState.Empty);
				}
			}
		}

		private void ServerStartCustomersLeaveBecauseOfTimeout(string reason)
		{
			if (base.IsServer)
			{
				_customersLeavingBecauseOfTimeout = true;
				Singleton<ResidentManager>.Instance?.ForceRestaurantResidentsLeave(_id, ResidentRestaurantTargetType.DiningTable);
				UtilityMethods.Log("<b><color=#FFAA00>[RestaurantTable Timeout Leave]</color></b>\n  Reason          : " + reason + "\n" + $"  TableId         : {_id}\n" + "  GameObject      : " + base.name + "\n" + $"  NetworkObjectId : {base.NetworkObjectId}\n" + $"  State           : {_currentState.Value}\n" + $"  SeatedCount     : {_seatedCount.Value}\n" + $"  TrayCount       : {_trays.Count}");
				ServerSendClientRaiseVisualState(5);
			}
		}

		private void ClearSeatOrdersRuntime()
		{
			if (_seatOrders == null)
			{
				return;
			}
			for (int i = 0; i < _seatOrders.Count; i++)
			{
				CustomerSeatOrder customerSeatOrder = _seatOrders[i];
				if (customerSeatOrder == null)
				{
					customerSeatOrder = new CustomerSeatOrder
					{
						SeatIndex = i,
						Order = null,
						OrderId = -1
					};
				}
				else
				{
					customerSeatOrder.SeatIndex = i;
					customerSeatOrder.Order = null;
					customerSeatOrder.OrderId = -1;
				}
				_seatOrders[i] = customerSeatOrder;
			}
		}

		private void ServerRemoveActiveInvoices()
		{
			if (!base.IsServer || _seatOrders == null || _seatOrders.Count == 0)
			{
				return;
			}
			ServiceLocator.ForSceneOf(this).Get<TabletNetworkHandler>(out var service);
			if (service == null)
			{
				return;
			}
			for (int i = 0; i < _seatOrders.Count; i++)
			{
				CustomerSeatOrder customerSeatOrder = _seatOrders[i];
				if (customerSeatOrder != null && customerSeatOrder.OrderId >= 0)
				{
					service.ServerRemoveOrder(customerSeatOrder.OrderId);
					customerSeatOrder.OrderId = -1;
					_seatOrders[i] = customerSeatOrder;
				}
			}
		}

		private void ApplyClientShowNotify()
		{
			Singleton<NotifyManager>.Instance.ShowToast("Not enough money to repair the table!");
		}

		public void ServerSendClientShowNotify(RpcParams rpcParams = default(RpcParams))
		{
			if (base.IsServer)
			{
				ClientShowNotifyRevisionRpc(BootstrapLiveUpdateBuffer.NextServerRevision(), rpcParams);
			}
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientShowNotifyRevisionRpc(ulong worldRevision, RpcParams rpcParams = default(RpcParams))
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
					InvokePermission = RpcInvokePermission.Server
				};
				FastBufferWriter bufferWriter = __beginSendRpc(964473992u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 964473992u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, 0, (RestaurantTable target, int data) =>
				{
					target.ApplyClientShowNotify();
				}, 4096, worldRevision))
				{
					ApplyClientShowNotify();
				}
			}
		}

		public bool CanAcceptResidentArrival(int seatIndex)
		{
			if (_currentState.Value != RestaurantTableState.WaitingOtherCustomers)
			{
				return false;
			}
			if (seatIndex < 0 || seatIndex >= _seatedCount.Value)
			{
				return false;
			}
			if (seatIndex < _residentNames.Count && !string.IsNullOrEmpty(_residentNames[seatIndex]))
			{
				return false;
			}
			return _completedCustomerCount < _seatedCount.Value;
		}

		public bool OnServerCustomerArrived(int seatIndex, string residentName)
		{
			if (!CanAcceptResidentArrival(seatIndex))
			{
				UtilityMethods.Log($"[RestaurantTable] Resident arrival rejected. TableId={_id}, Seat={seatIndex}, State={_currentState.Value}, Arrived={_completedCustomerCount}, Expected={_seatedCount.Value}", LogType.Warning);
				return false;
			}
			if (seatIndex >= _residentNames.Count)
			{
				for (int i = _residentNames.Count; i <= seatIndex; i++)
				{
					_residentNames.Add(null);
				}
			}
			_residentNames[seatIndex] = residentName;
			_completedCustomerCount++;
			if (_seatedCount.Value <= _completedCustomerCount)
			{
				SendCustomerMenuWaitingForArrivedSeats(isWaiting: false);
				UtilityMethods.Log("Table- OnServerCustomerArrived CompletedCustomer: " + _completedCustomerCount);
				ServerChangeState(RestaurantTableState.TakingOrder);
				_completedCustomerCount = 0;
			}
			else
			{
				SendCustomerMenuWaitingVisual(seatIndex, isWaiting: true);
			}
			return true;
		}

		public void ServerCancelPendingArrivalsForRestaurantClose()
		{
			if (base.IsServer && _currentState.Value == RestaurantTableState.WaitingOtherCustomers)
			{
				UtilityMethods.Log($"[RestaurantTable] Pending arrivals cancelled because restaurant closed. TableId={_id}, Arrived={_completedCustomerCount}, Expected={_seatedCount.Value}");
				ServerInvalidateDelayedActions();
				_seatOrders.Clear();
				_residentNames.Clear();
				_servingSteamIdBySeat.Clear();
				_completedCustomerCount = 0;
				_completedOrderCount = 0;
				_seatedCount.Value = 0;
				_lateJoinLastSeatedCount = 0;
				ServerChangeState(RestaurantTableState.Empty);
			}
		}

		public bool CheckIsTableEmpty()
		{
			if (_completedCustomerCount > 0)
			{
				return false;
			}
			if (base.IsServer)
			{
				_seatedCount.Value--;
				UtilityMethods.Log("[Server - RestaurantTable] CheckIsAnyoneOnTable:False SeatedCount decremented to " + _seatedCount.Value);
				if (_seatedCount.Value <= 0)
				{
					ServerChangeState(RestaurantTableState.Empty);
				}
			}
			return true;
		}

		public void ServerChangeState(RestaurantTableState newState)
		{
			if (!base.IsServer)
			{
				UtilityMethods.Log($"[RestaurantTable] ServerChangeState blocked on client. RequestedState={newState}", LogType.Warning);
			}
			else if (_isChangingState)
			{
				UtilityMethods.Log($"[RestaurantTable] ServerChangeState blocked because another state transition is already running. RequestedState={newState}", LogType.Warning);
			}
			else if (_currentState.Value != newState)
			{
				_stateVersion++;
				_isChangingState = true;
				try
				{
					RestaurantTableState value = _currentState.Value;
					_preserveTalkingAcrossOrderTransition = value == RestaurantTableState.TakingOrder && newState == RestaurantTableState.DeliveringOrder;
					UtilityMethods.Log($"[RestaurantTable] State change. TableId={_id}, {value} -> {newState}");
					ServerExitState();
					_currentState.Value = newState;
					ServerEnterState();
					ServerSendClientChangeState((byte)newState);
				}
				finally
				{
					_preserveTalkingAcrossOrderTransition = false;
					_isChangingState = false;
				}
			}
		}

		private void ServerEnterState()
		{
			switch (_currentState.Value)
			{
			case RestaurantTableState.Empty:
				ServerEmptyStateEnter();
				ServerSendClientEmptyStateEnter();
				break;
			case RestaurantTableState.TakingOrder:
				ServerTakingOrderStateEnter();
				ServerSendClientTakingOrderEnter();
				break;
			case RestaurantTableState.DeliveringOrder:
				ServerDeliveringOrderStateEnter();
				ServerSendClientDeliveringOrderEnter();
				break;
			case RestaurantTableState.Eating:
				ServerEatingStateEnter();
				ServerSendClientEatingEnter();
				break;
			case RestaurantTableState.AwaitingPayment:
				ServerAwaitingPaymentEnter();
				ServerSendClientAwaitingPaymentEnter();
				break;
			case RestaurantTableState.Cleaning:
				ServerCleaningStateEnter();
				ServerSendClientCleaningEnter();
				break;
			case RestaurantTableState.WaitingOtherCustomers:
				ServerWaitingOtherCustomersEnter();
				ServerSendClientWaitingOtherCustomersEnter();
				break;
			}
		}

		private void ServerExitState()
		{
			switch (_currentState.Value)
			{
			case RestaurantTableState.Empty:
				ServerEmptyStateExit();
				ClientEmptyStateExitRpc();
				break;
			case RestaurantTableState.TakingOrder:
				ServerTakingOrderStateExit();
				ClientTakingOrderExitRpc();
				break;
			case RestaurantTableState.DeliveringOrder:
				ServerDeliveringOrderStateExit();
				ServerSendClientDeliveringOrderExit();
				break;
			case RestaurantTableState.Eating:
				ServerEatingStateExit();
				ClientEatingExitRpc();
				break;
			case RestaurantTableState.AwaitingPayment:
				ServerAwaitingPaymentExit();
				ServerSendClientAwaitingPaymentExit();
				break;
			case RestaurantTableState.Cleaning:
				ServerCleaningStateExit();
				ClientCleaningExitRpc();
				break;
			case RestaurantTableState.WaitingOtherCustomers:
				ServerWaitingOtherCustomersExit();
				ClientWaitingOtherCustomersExitRpc();
				break;
			}
		}

		private void ServerEmptyStateEnter()
		{
			_restaurantHiPlayedForGroup = false;
			ServerSendClientStopRestaurantAmbient();
			Singleton<ResidentManager>.Instance?.ForceRestaurantResidentsLeave(_id, ResidentRestaurantTargetType.DiningTable);
			ServerStopTakeOrderTimer();
			ServerStopDeliverOrderTimer();
			ServerClearDeliveredTrayRecords();
			ServerRestoreAllDeliveredSeatVisuals();
			_servingSteamIdBySeat.Clear();
			_customersLeavingBecauseOfTimeout = false;
			_cleaningBecauseCustomersLeft = false;
			_seatedCount.Value = 0;
			Singleton<GameProgressManager>.Instance.RestaurantManager.ServerNotifyTableEmptied(Id);
		}

		private void ServerEmptyStateExit()
		{
		}

		private void ApplyClientEmptyStateEnter()
		{
			if (_debugText != null)
			{
				_debugText.text = "State: Empty";
			}
			_seatOrders.Clear();
			_completedOrderCount = 0;
			_completedCustomerCount = 0;
			_cleaningRequiredTrayCount = 0;
			_cleanedTrayCount = 0;
			_totalPayment = 0;
			_totalPaymentFinal = 0;
			_isTableNeedsToClean = false;
			_residentNames.Clear();
			if (_moneyGraphPrefab != null)
			{
				_moneyGraphPrefab.SetActive(value: false);
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientEmptyStateExitRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(78961431u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 78961431u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private void ServerSendClientEmptyStateEnter()
		{
			if (base.IsServer)
			{
				ClientEmptyStateEnterRevisionRpc(BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientEmptyStateEnterRevisionRpc(ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2062433882u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 2062433882u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, 0, (RestaurantTable target, int data) =>
				{
					target.ApplyClientEmptyStateEnter();
				}, 4096, worldRevision))
				{
					ApplyClientEmptyStateEnter();
				}
			}
		}

		private void ServerWaitingOtherCustomersEnter()
		{
			_nextWaitingCustomerRetryServerTime = (_waitingCustomersStartedServerTime = GetServerTimeSafe()) + (double)Mathf.Max(1f, _waitingCustomerRetrySeconds);
			Feedback.ServerCustomersAssigned(this, _id, Mathf.Max(0, _seatedCount.Value));
		}

		private void ServerWaitingOtherCustomersExit()
		{
			_waitingCustomersStartedServerTime = 0.0;
			_nextWaitingCustomerRetryServerTime = 0.0;
			QuestReferenceHub.Instance.QuestComplete(QuestId.WaitForYourFirstCustomer);
		}

		private void ApplyClientWaitingOtherCustomersEnter()
		{
			if (_debugText != null)
			{
				_debugText.text = "State: WaitingOtherCustomers";
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientWaitingOtherCustomersExitRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(944480883u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 944480883u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private void ServerTakingOrderStateEnter()
		{
			_completedOrderCount = 0;
			int num = Mathf.Clamp(_seatedCount.Value, 0, _seatCount);
			int num2 = ServerReconcileDiningAssignments(num, out var removedCount);
			if (removedCount > 0 || (num2 >= 0 && num2 != num))
			{
				UtilityMethods.Log("[RestaurantTable] Dining assignments were inconsistent on TakingOrder entry. " + $"TableId={_id}, Expected={num}, ValidAssignments={num2}, Removed={removedCount}", LogType.Warning);
			}
			if (!ServerGenerateSeatOrders())
			{
				UtilityMethods.Log($"[RestaurantTable] TakingOrder entered but server could not generate orders. TableId={_id}, SeatedCount={_seatedCount.Value}", LogType.Warning);
				ServerStopTakeOrderTimer();
				return;
			}
			ServerStartTakeOrderTimer();
			Feedback.ServerReadyToOrder(this, _id);
			Feedback.ServerBeginReminder(RestaurantTableState.TakingOrder, GetServerTimeSafe());
			int speakerSeatIndex = ((Singleton<ResidentManager>.Instance != null) ? Singleton<ResidentManager>.Instance.GetClassicTableOrderSpeakerSeatIndex(_id) : 0);
			ServerSendClientPlayRestaurantMoment(speakerSeatIndex, 0, 1);
			int num3 = Mathf.Clamp(_seatedCount.Value, 0, _seatCount);
			if (!_restaurantHiPlayedForGroup)
			{
				_restaurantHiPlayedForGroup = true;
				if (num3 > 1)
				{
					ServerSendClientPlayRestaurantHiThenStartAmbient(speakerSeatIndex);
				}
				else
				{
					ServerSendClientPlayRestaurantAudio(0, speakerSeatIndex);
				}
			}
			else if (num3 > 1)
			{
				ServerSendClientStartRestaurantAmbient(1);
			}
		}

		private void ServerSendClientWaitingOtherCustomersEnter()
		{
			if (base.IsServer)
			{
				ClientWaitingOtherCustomersEnterRevisionRpc(BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientWaitingOtherCustomersEnterRevisionRpc(ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3639903054u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 3639903054u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, 0, (RestaurantTable target, int data) =>
				{
					target.ApplyClientWaitingOtherCustomersEnter();
				}, 4096, worldRevision))
				{
					ApplyClientWaitingOtherCustomersEnter();
				}
			}
		}

		private void ServerTakingOrderStateExit()
		{
			if (!_preserveTalkingAcrossOrderTransition)
			{
				ServerSendClientStopRestaurantAmbient();
			}
			Feedback.ServerStopReminder(RestaurantTableState.TakingOrder);
			ServerStopTakeOrderTimer();
		}

		private bool ServerGenerateSeatOrders()
		{
			if (!base.IsServer)
			{
				return false;
			}
			int num = Mathf.Clamp(_seatedCount.Value, 0, _seatCount);
			if (num <= 0)
			{
				UtilityMethods.Log($"[RestaurantTable] Cannot generate orders. No seated customers. TableId={_id}", LogType.Warning);
				return false;
			}
			ServiceLocator.ForSceneOf(this).Get<TabletNetworkHandler>(out var service);
			if (service == null)
			{
				UtilityMethods.Log($"[RestaurantTable] Cannot generate orders. TabletNetworkHandler not found. TableId={_id}", LogType.Warning);
				return false;
			}
			int seed = (_lateJoinTakingOrderSeed = UnityEngine.Random.Range(0, int.MaxValue));
			_lateJoinLastSeatedCount = num;
			System.Random rng = new System.Random(seed);
			_totalPayment = 0;
			_totalPaymentFinal = 0;
			EnsureSeatOrdersCapacity(_seatCount);
			for (int i = 0; i < _seatOrders.Count; i++)
			{
				CustomerSeatOrder customerSeatOrder = _seatOrders[i];
				if (customerSeatOrder == null)
				{
					customerSeatOrder = new CustomerSeatOrder();
				}
				customerSeatOrder.SeatIndex = i;
				customerSeatOrder.Order = null;
				customerSeatOrder.OrderId = -1;
				_seatOrders[i] = customerSeatOrder;
			}
			bool result = false;
			SeatOrderPacked seat = default;
			SeatOrderPacked seat2 = default;
			SeatOrderPacked seat3 = default;
			SeatOrderPacked seat4 = default;
			for (int j = 0; j < num; j++)
			{
				if (!service.ServerTryGetRandomCustomerSelection(rng, out var mainDish, out var sideDish, out var beverage))
				{
					UtilityMethods.Log($"[RestaurantTable] Failed to generate order for seat {j}. TableId={_id}", LogType.Warning);
					continue;
				}
				if (mainDish == null)
				{
					UtilityMethods.Log($"[RestaurantTable] Generated order has no main dish. Seat={j}, TableId={_id}", LogType.Warning);
					continue;
				}
				CustomerOrderData order = new CustomerOrderData
				{
					MainDishSO = mainDish.BuiltInSushi,
					MainDishRecipe = mainDish,
					MainDishRecipeId = mainDish.RecipeId,
					SideDishSO = sideDish,
					BeverageSO = beverage
				};
				CustomerSeatOrder customerSeatOrder2 = _seatOrders[j];
				if (customerSeatOrder2 == null)
				{
					customerSeatOrder2 = new CustomerSeatOrder();
				}
				customerSeatOrder2.SeatIndex = j;
				customerSeatOrder2.Order = order;
				customerSeatOrder2.OrderId = -1;
				_seatOrders[j] = customerSeatOrder2;
				result = true;
				SeatOrderPacked seatOrderPacked = new SeatOrderPacked
				{
					MainId = PackId64(mainDish.RecipeId),
					SideId = PackId64((sideDish != null) ? sideDish.Id : null),
					BevId = PackId64((beverage != null) ? beverage.Id : null)
				};
				switch (j)
				{
				case 0:
					seat = seatOrderPacked;
					break;
				case 1:
					seat2 = seatOrderPacked;
					break;
				case 2:
					seat3 = seatOrderPacked;
					break;
				case 3:
					seat4 = seatOrderPacked;
					break;
				}
			}
			int activeSeatOrderCount = GetActiveSeatOrderCount();
			if (!HasCompleteSeatOrderSet(num, activeSeatOrderCount))
			{
				UtilityMethods.Log("[RestaurantTable] Discarding incomplete seat-order generation. " + $"TableId={_id}, Expected={num}, Generated={activeSeatOrderCount}", LogType.Warning);
				ClearSeatOrdersRuntime();
				return false;
			}
			ServerSendClientSyncSeatOrders((byte)num, seat, seat2, seat3, seat4);
			UtilityMethods.Log($"[RestaurantTable] Server generated seat orders. TableId={_id}, Seated={num}, Generated={GetActiveSeatOrderCount()}, TotalPayment={_totalPayment}, FinalPayment={_totalPaymentFinal}");
			return result;
		}

		private int GetActiveSeatOrderCount()
		{
			if (_seatOrders == null)
			{
				return 0;
			}
			int num = 0;
			for (int i = 0; i < _seatOrders.Count; i++)
			{
				CustomerSeatOrder customerSeatOrder = _seatOrders[i];
				if (customerSeatOrder != null && customerSeatOrder.Order != null && customerSeatOrder.Order.HasMainDish)
				{
					num++;
				}
			}
			return num;
		}

		private void ApplyClientSyncSeatOrders(byte seatedCount, SeatOrderPacked seat0, SeatOrderPacked seat1, SeatOrderPacked seat2, SeatOrderPacked seat3)
		{
			ClearSeatOrdersForClientSync();
			ApplySeatToRuntime(0, in seat0);
			ApplySeatToRuntime(1, in seat1);
			ApplySeatToRuntime(2, in seat2);
			ApplySeatToRuntime(3, in seat3);
			_lateJoinLastSeatedCount = seatedCount;
			if (_debugText != null)
			{
				_debugText.text = "State: Taking Order";
			}
		}

		private void ClearSeatOrdersForClientSync()
		{
			EnsureSeatOrdersCapacity(_seatCount);
			for (int i = 0; i < _seatOrders.Count; i++)
			{
				CustomerSeatOrder customerSeatOrder = _seatOrders[i];
				if (customerSeatOrder == null)
				{
					customerSeatOrder = new CustomerSeatOrder();
				}
				customerSeatOrder.SeatIndex = i;
				customerSeatOrder.Order = null;
				customerSeatOrder.OrderId = -1;
				_seatOrders[i] = customerSeatOrder;
			}
		}

		private static FixedString64Bytes PackId64(string id)
		{
			if (!string.IsNullOrEmpty(id))
			{
				return new FixedString64Bytes(id);
			}
			return default;
		}

		private void ApplyClientTakingOrderEnter()
		{
			if (_debugText != null)
			{
				_debugText.text = "State: Taking Order";
			}
			_lateJoinLastSeatedCount = _seatedCount.Value;
		}

		[Rpc(SendTo.Everyone)]
		private void ClientTakingOrderExitRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1074990379u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1074990379u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerConfirmOrderRpc(NetworkObjectReference interactorRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(2266663196u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 2266663196u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsServer)
			{
				return;
			}
			if (_currentState.Value != RestaurantTableState.TakingOrder)
			{
				UtilityMethods.Log($"[RestaurantTable] Confirm order ignored. Table is not TakingOrder. TableId={_id}, State={_currentState.Value}");
				return;
			}
			if (HasTakeOrderDeadlineExpired())
			{
				ServerStopTakeOrderTimer();
				ServerHandleTakeOrderTimeout();
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!interactorRef.TryGetComponent<Interactor>(out var component) || component.OwnerClientId != senderClientId)
			{
				return;
			}
			int num = Mathf.Clamp(_seatedCount.Value, 0, _seatCount);
			int activeSeatOrderCount = GetActiveSeatOrderCount();
			if (!HasCompleteSeatOrderSet(num, activeSeatOrderCount))
			{
				int num2 = ServerReconcileDiningAssignments(num, out var _);
				if ((num2 >= 0 && num2 != num) || !ServerGenerateSeatOrders())
				{
					UtilityMethods.Log("[RestaurantTable] Confirm order blocked because the table could not repair its seat orders. " + $"TableId={_id}, Expected={num}, ActiveOrders={activeSeatOrderCount}, " + $"ValidAssignments={num2}", LogType.Warning);
					BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
					ServerSendClientShowTableToast("The table is still preparing the customer orders. Please try again.", NotificationState.Info, baseRpcTarget);
					return;
				}
			}
			string text = component.Player.PlayerName.Value.ToString();
			_deliverOrderExpireServerTime = GetServerTimeSafe() + (double)Mathf.Max(1f, _deliverOrderTimeoutSeconds);
			if (!ServerCreateInvoicesForAllCustomers(text, _deliverOrderExpireServerTime))
			{
				UtilityMethods.Log($"[RestaurantTable] Confirm order failed. No invoices created. TableId={_id}", LogType.Warning);
				_deliverOrderExpireServerTime = 0.0;
				_deliverOrderTimerActive = false;
				BaseRpcTarget baseRpcTarget2 = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
				ServerSendClientShowTableToast("No valid orders found for this table.", NotificationState.Error, baseRpcTarget2);
			}
			else
			{
				QuestReferenceHub.Instance.QuestComplete(QuestId.TakeTheCustomersOrder);
				Feedback.ServerOrderTaken(this, _id, text);
				BaseRpcTarget baseRpcTarget3 = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
				ServerSendClientShowOrderReceivedFeedback(_id, baseRpcTarget3);
				int maxExclusive = Mathf.Clamp(_seatedCount.Value, 1, _seatCount);
				ServerSendClientPlayRestaurantMoment(UnityEngine.Random.Range(0, maxExclusive), 1, (byte)UnityEngine.Random.Range(4, 7));
				ServerChangeState(RestaurantTableState.DeliveringOrder);
			}
		}

		private void ServerSendClientSyncSeatOrders(byte seatedCount, SeatOrderPacked seat0, SeatOrderPacked seat1, SeatOrderPacked seat2, SeatOrderPacked seat3)
		{
			if (base.IsServer)
			{
				ClientSyncSeatOrdersRevisionRpc(seatedCount, seat0, seat1, seat2, seat3, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientSyncSeatOrdersRevisionRpc(byte seatedCount, SeatOrderPacked seat0, SeatOrderPacked seat1, SeatOrderPacked seat2, SeatOrderPacked seat3, ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1247925531u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in seatedCount, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in seat0, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in seat1, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in seat2, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in seat3, default(FastBufferWriter.ForNetworkSerializable));
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 1247925531u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, (seatedCount, seat0, seat1, seat2, seat3), (RestaurantTable target, (byte seatedCount, SeatOrderPacked seat0, SeatOrderPacked seat1, SeatOrderPacked seat2, SeatOrderPacked seat3) data) =>
				{
					target.ApplyClientSyncSeatOrders(data.seatedCount, data.seat0, data.seat1, data.seat2, data.seat3);
				}, 4096, worldRevision))
				{
					ApplyClientSyncSeatOrders(seatedCount, seat0, seat1, seat2, seat3);
				}
			}
		}

		private void ApplyClientShowOrderReceivedFeedback(int tableId)
		{
			Singleton<NotifyManager>.Instance?.ShowToast($"Table {tableId}: Order received.", NotificationState.Success, 2f);
			if (Singleton<SoundManager>.Instance != null && _restaurantOrderReceivedSfx != null)
			{
				Singleton<SoundManager>.Instance.PlaySFX(_restaurantOrderReceivedSfx, base.transform.position, base.transform);
			}
		}

		private void ApplyClientShowTableToast(string message, NotificationState state = NotificationState.Info)
		{
			if (!(Singleton<NotifyManager>.Instance == null))
			{
				Singleton<NotifyManager>.Instance.ShowToast(message, state, 2f);
			}
		}

		private void ApplyClientShowDeliveryRejectedToast(string message)
		{
			if (!(Singleton<NotifyManager>.Instance == null))
			{
				Singleton<NotifyManager>.Instance.ShowToast(message, NotificationState.Error, 3f);
			}
		}

		private void ApplyClientShowTableToastToEveryone(string message, NotificationState state = NotificationState.Info)
		{
			if (!(Singleton<NotifyManager>.Instance == null))
			{
				Singleton<NotifyManager>.Instance.ShowToast(message, state, 3f);
			}
		}

		private void ServerSendClientTakingOrderEnter()
		{
			if (base.IsServer)
			{
				ClientTakingOrderEnterRevisionRpc(BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientTakingOrderEnterRevisionRpc(ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1078604966u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 1078604966u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, 0, (RestaurantTable target, int data) =>
				{
					target.ApplyClientTakingOrderEnter();
				}, 4096, worldRevision))
				{
					ApplyClientTakingOrderEnter();
				}
			}
		}

		private bool ServerCreateInvoicesForAllCustomers(string waitressName, double expireServerTime, bool isStaffWaitress = false, StaffGender staffWaitressGender = StaffGender.Male)
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (_seatOrders == null || _seatOrders.Count == 0)
			{
				return false;
			}
			ServiceLocator.ForSceneOf(this).Get<TabletNetworkHandler>(out var service);
			if (service == null)
			{
				UtilityMethods.Log($"[RestaurantTable] Cannot create invoices. TabletNetworkHandler not found. TableId={_id}", LogType.Warning);
				return false;
			}
			string tableNo = _id.ToString();
			int num = 0;
			for (int i = 0; i < _seatOrders.Count; i++)
			{
				CustomerSeatOrder customerSeatOrder = _seatOrders[i];
				if (customerSeatOrder == null || customerSeatOrder.Order == null || !customerSeatOrder.Order.HasMainDish)
				{
					continue;
				}
				if (customerSeatOrder.OrderId >= 0)
				{
					num++;
					continue;
				}
				CustomerOrderFoodPayload foodPayload = default;
				CustomerOrderData order = customerSeatOrder.Order;
				if (order.HasMainDish && foodPayload.FoodIds.Length < foodPayload.FoodIds.Capacity)
				{
					foodPayload.FoodIds.Add((FixedString32Bytes)order.MainDishIdentityId);
				}
				if (order.SideDishSO != null && foodPayload.FoodIds.Length < foodPayload.FoodIds.Capacity)
				{
					foodPayload.FoodIds.Add((FixedString32Bytes)order.SideDishSO.Id);
				}
				if (order.BeverageSO != null && foodPayload.FoodIds.Length < foodPayload.FoodIds.Capacity)
				{
					foodPayload.FoodIds.Add((FixedString32Bytes)order.BeverageSO.Id);
				}
				int seatIndex = customerSeatOrder.SeatIndex;
				string customerName = ((seatIndex < 0 || seatIndex >= _residentNames.Count || string.IsNullOrEmpty(_residentNames[seatIndex])) ? $"Table_{_id}_Seat_{seatIndex}" : _residentNames[seatIndex]);
				int num2 = service.ServerAddOrder(tableNo, customerName, waitressName, foodPayload, expireServerTime, seatIndex, isStaffWaitress, staffWaitressGender);
				if (num2 >= 0)
				{
					customerSeatOrder.OrderId = num2;
					_seatOrders[i] = customerSeatOrder;
					num++;
				}
			}
			UtilityMethods.Log($"[RestaurantTable] Created invoices. TableId={_id}, Count={num}");
			return num > 0;
		}

		private void ServerDeliveringOrderStateEnter()
		{
			ServerClearDeliveredTrayRecords();
			ServerStartDeliverOrderTimer();
			Feedback.ServerBeginReminder(RestaurantTableState.DeliveringOrder, GetServerTimeSafe());
			if (Mathf.Clamp(_seatedCount.Value, 0, _seatCount) > 1)
			{
				ServerSendClientStartRestaurantAmbient(1);
			}
		}

		private void ServerSendClientShowOrderReceivedFeedback(int tableId, RpcParams rpcParams = default(RpcParams))
		{
			if (base.IsServer)
			{
				ClientShowOrderReceivedFeedbackRevisionRpc(tableId, BootstrapLiveUpdateBuffer.NextServerRevision(), rpcParams);
			}
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientShowOrderReceivedFeedbackRevisionRpc(int tableId, ulong worldRevision, RpcParams rpcParams = default(RpcParams))
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
					InvokePermission = RpcInvokePermission.Server
				};
				FastBufferWriter bufferWriter = __beginSendRpc(1806025271u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, tableId);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 1806025271u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, tableId, (RestaurantTable target, int data) =>
				{
					target.ApplyClientShowOrderReceivedFeedback(data);
				}, 4096, worldRevision))
				{
					ApplyClientShowOrderReceivedFeedback(tableId);
				}
			}
		}

		private void ServerSendClientShowTableToast(string message, NotificationState state = NotificationState.Info, RpcParams rpcParams = default(RpcParams))
		{
			if (base.IsServer)
			{
				ClientShowTableToastRevisionRpc(message, state, BootstrapLiveUpdateBuffer.NextServerRevision(), rpcParams);
			}
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientShowTableToastRevisionRpc(string message, NotificationState state, ulong worldRevision, RpcParams rpcParams = default(RpcParams))
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
					InvokePermission = RpcInvokePermission.Server
				};
				FastBufferWriter bufferWriter = __beginSendRpc(3923445107u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bool value = message != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(message);
				}
				bufferWriter.WriteValueSafe(in state, default(FastBufferWriter.ForEnums));
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 3923445107u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, (message, state), (RestaurantTable target, (string message, NotificationState state) data) =>
				{
					target.ApplyClientShowTableToast(data.message, data.state);
				}, 4096 + (message?.Length ?? 0) * 2, worldRevision))
				{
					ApplyClientShowTableToast(message, state);
				}
			}
		}

		private void ServerSendClientShowDeliveryRejectedToast(string message, RpcParams rpcParams = default(RpcParams))
		{
			if (base.IsServer)
			{
				ClientShowDeliveryRejectedToastRevisionRpc(message, BootstrapLiveUpdateBuffer.NextServerRevision(), rpcParams);
			}
		}

		private void ServerDeliveringOrderStateExit()
		{
			ServerSendClientStopRestaurantAmbient();
			Feedback.ServerStopReminder(RestaurantTableState.DeliveringOrder);
			ServerStopDeliverOrderTimer();
			ServerClearDeliveredTrayRecords();
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientShowDeliveryRejectedToastRevisionRpc(string message, ulong worldRevision, RpcParams rpcParams = default(RpcParams))
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
					InvokePermission = RpcInvokePermission.Server
				};
				FastBufferWriter bufferWriter = __beginSendRpc(1314408236u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				bool value = message != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(message);
				}
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 1314408236u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, message, (RestaurantTable target, string data) =>
				{
					target.ApplyClientShowDeliveryRejectedToast(data);
				}, 4096 + (message?.Length ?? 0) * 2, worldRevision))
				{
					ApplyClientShowDeliveryRejectedToast(message);
				}
			}
		}

		private void ServerSendClientShowTableToastToEveryone(string message, NotificationState state = NotificationState.Info)
		{
			if (base.IsServer)
			{
				ClientShowTableToastToEveryoneRevisionRpc(message, state, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientShowTableToastToEveryoneRevisionRpc(string message, NotificationState state, ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3864854892u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bool value = message != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(message);
				}
				bufferWriter.WriteValueSafe(in state, default(FastBufferWriter.ForEnums));
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 3864854892u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, (message, state), (RestaurantTable target, (string message, NotificationState state) data) =>
				{
					target.ApplyClientShowTableToastToEveryone(data.message, data.state);
				}, 4096 + (message?.Length ?? 0) * 2, worldRevision))
				{
					ApplyClientShowTableToastToEveryone(message, state);
				}
			}
		}

		private void ApplyClientDeliveringOrderEnter()
		{
			if (_debugText != null)
			{
				_debugText.text = "State: DeliveringOrder";
			}
		}

		private void ApplyClientDeliveringOrderExit()
		{
			_completedOrderCount = 0;
		}

		[Rpc(SendTo.Server)]
		private void ServerDeliveringOrderInteractRpc(NetworkObjectReference interactRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1801723160u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 1801723160u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsServer)
			{
				return;
			}
			if (_currentState.Value != RestaurantTableState.DeliveringOrder)
			{
				UtilityMethods.Log($"[RestaurantTable] Deliver order ignored. Table is not DeliveringOrder. TableId={_id}, State={_currentState.Value}");
				return;
			}
			if (HasDeliverOrderDeadlineExpired())
			{
				ServerStopDeliverOrderTimer();
				ServerHandleDeliverOrderTimeout();
				return;
			}
			if (!interactRef.TryGetComponent<Interactor>(out var component))
			{
				UtilityMethods.Log("[RestaurantTable] DeliverInteract: FAIL - Interactor component not found from NetworkObjectReference.", LogType.Warning);
				return;
			}
			if (component.OwnerClientId != rpcParams.Receive.SenderClientId)
			{
				UtilityMethods.Log($"[RestaurantTable] DeliverInteract: FAIL - Interactor owner mismatch. Sender={rpcParams.Receive.SenderClientId}, Owner={component.OwnerClientId}", LogType.Warning);
				return;
			}
			if (component.CurrentHeld == null)
			{
				UtilityMethods.Log("[RestaurantTable] DeliverInteract: IGNORE - " + component.name + " is not holding anything.");
				BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(component.OwnerClientId, RpcTargetUse.Temp);
				ServerSendClientShowTableToast("You need to hold a tray to deliver an order.", NotificationState.Info, baseRpcTarget);
				return;
			}
			if (!component.CurrentHeld.TryGetComponent<Tray>(out var component2))
			{
				UtilityMethods.Log("[RestaurantTable] DeliverInteract: IGNORE - Held object is not a Tray. Held=" + component.CurrentHeld.name);
				BaseRpcTarget baseRpcTarget2 = base.RpcTarget.Single(component.OwnerClientId, RpcTargetUse.Temp);
				ServerSendClientShowTableToast("This table only accepts trays.", NotificationState.Info, baseRpcTarget2);
				return;
			}
			if (component2.IsDirty.Value)
			{
				UtilityMethods.Log("[RestaurantTable] DeliverInteract: IGNORE - Tray is dirty. Tray=" + component2.name);
				BaseRpcTarget baseRpcTarget3 = base.RpcTarget.Single(component.OwnerClientId, RpcTargetUse.Temp);
				ServerSendClientShowTableToast("This tray is dirty. Clean it before serving.", NotificationState.Error, baseRpcTarget3);
				return;
			}
			if (!TryFindBestSeatMatch(component2, out var bestIndex, out var bestEval, out var rejectIssues))
			{
				UtilityMethods.Log("[RestaurantTable] DeliverInteract: FAIL - No valid seat match for this tray. Tray=" + component2.name, LogType.Warning);
				BaseRpcTarget baseRpcTarget4 = base.RpcTarget.Single(component.OwnerClientId, RpcTargetUse.Temp);
				ServerSendClientShowDeliveryRejectedToast(GetDeliveryRejectMessage(rejectIssues), baseRpcTarget4);
				return;
			}
			if (bestIndex < 0 || bestIndex >= _seatOrders.Count)
			{
				UtilityMethods.Log($"[RestaurantTable] DeliverInteract: FAIL - Matched index out of range. Index={bestIndex}", LogType.Warning);
				return;
			}
			CustomerSeatOrder customerSeatOrder = _seatOrders[bestIndex];
			if (customerSeatOrder == null || customerSeatOrder.Order == null)
			{
				UtilityMethods.Log($"[RestaurantTable] DeliverInteract: FAIL - Matched seat order is null. Index={bestIndex}", LogType.Warning);
				return;
			}
			Transform child = _trayPointsParent.GetChild(bestIndex);
			if (!component2.ServerPlaceOnRestaurantTable(child.position, child.rotation.eulerAngles))
			{
				UtilityMethods.Log($"[RestaurantTable] DeliverInteract: FAIL - Tray could not be placed on table. TableId={_id}, Tray={component2.name}, Owner={component2.OwnerClientId}, CanHold={component2.CanHold.Value}", LogType.Warning);
				return;
			}
			_trays.Add(component2);
			ServerTrackDeliveredSeatVisual(component2, bestIndex);
			UtilityMethods.Log("[RestaurantTable] DeliverInteracted: SUCCESS");
			ServiceLocator.ForSceneOf(this).Get<TabletNetworkHandler>(out var service);
			CustomerOrderFoodPayload deliveredPayload = BuildDeliveredPayloadFromSeatOrder(customerSeatOrder.Order, bestEval);
			bool flag = false;
			if (service != null && customerSeatOrder.OrderId >= 0)
			{
				flag = service.ServerRegisterDeliveredFoods(customerSeatOrder.OrderId, deliveredPayload);
			}
			ulong value = component.Player.SteamID.Value;
			bool hadPreviousServingSteamId = _servingSteamIdBySeat.TryGetValue(bestIndex, out var value2);
			_servingSteamIdBySeat[bestIndex] = value;
			ServerTryUnlockLastCall(component.OwnerClientId);
			_totalPayment += Mathf.Max(0, bestEval.Earned);
			_totalPaymentFinal = _totalPayment;
			if (flag)
			{
				_deliveredTrayRecords.Add(new DeliveredTrayRecord
				{
					Tray = component2,
					SeatIndex = bestIndex,
					Order = customerSeatOrder.Order,
					OrderId = customerSeatOrder.OrderId,
					EarnedPayment = Mathf.Max(0, bestEval.Earned),
					WaitressSteamId = value,
					HadPreviousServingSteamId = hadPreviousServingSteamId,
					PreviousServingSteamId = value2,
					DeliveredPayload = deliveredPayload
				});
				ServerRefreshTakeBackableTrayCount();
			}
			FixedString64Bytes value3 = component.Player.PlayerName.Value;
			Feedback.ServerOrderServed(this, _id, bestIndex, bestEval.Issues);
			bool flag2 = (bestEval.Issues & (OrderIssueFlags.SideMissing | OrderIssueFlags.BeverageMissing)) != 0;
			ServerSendClientPlayRestaurantMoment(bestIndex, (byte)(flag2 ? 4 : 2), (byte)UnityEngine.Random.Range(4, 7));
			ServerSendClientPlayRestaurantAudio(3, bestIndex);
			ServerSendClientDeliveryFeedback(value3, bestIndex, (byte)bestEval.Result, (ushort)bestEval.Issues, bestEval.Penalty, bestEval.Earned, _totalPaymentFinal);
			HandleDeliveredMainDishQuest(component2);
			HandleDeliveredSideDishQuest(customerSeatOrder.Order, bestEval);
			customerSeatOrder.Order = null;
			customerSeatOrder.OrderId = -1;
			_seatOrders[bestIndex] = customerSeatOrder;
			ServerGrantGroupServiceGrace(bestIndex);
			_completedOrderCount++;
			if (_seatOrders.All((CustomerSeatOrder s) => s == null || s.Order == null))
			{
				_completedOrderCount = 0;
				if (service != null)
				{
					service.ServerRemoveOrdersForTable(_id.ToString());
				}
				ServerChangeState(RestaurantTableState.Eating);
			}
		}

		private static CustomerOrderFoodPayload BuildDeliveredPayloadFromSeatOrder(CustomerOrderData order, DeliveryEvaluation eval)
		{
			CustomerOrderFoodPayload result = default;
			if (order == null)
			{
				return result;
			}
			if (order.HasMainDish && result.FoodIds.Length < result.FoodIds.Capacity)
			{
				result.FoodIds.Add((FixedString32Bytes)order.MainDishIdentityId);
			}
			if (order.SideDishSO != null && (eval.Issues & OrderIssueFlags.SideMissing) == 0 && result.FoodIds.Length < result.FoodIds.Capacity)
			{
				result.FoodIds.Add((FixedString32Bytes)order.SideDishSO.Id);
			}
			if (order.BeverageSO != null && (eval.Issues & OrderIssueFlags.BeverageMissing) == 0 && result.FoodIds.Length < result.FoodIds.Capacity)
			{
				result.FoodIds.Add((FixedString32Bytes)order.BeverageSO.Id);
			}
			return result;
		}

		private void ServerTryUnlockLastCall(ulong clientId)
		{
			if (base.IsServer && _deliverOrderTimerActive)
			{
				double num = _deliverOrderExpireServerTime - GetServerTimeSafe();
				if (!(num <= 0.0) && !(num >= 3.0))
				{
					Singleton<GameProgressManager>.Instance?.RpcRelay?.ServerUnlockSteamAchievementForClient("ACH_LAST_CALL", clientId);
				}
			}
		}

		private void ServerSendClientDeliveringOrderEnter()
		{
			if (base.IsServer)
			{
				ClientDeliveringOrderEnterRevisionRpc(BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientDeliveringOrderEnterRevisionRpc(ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1525086334u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 1525086334u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, 0, (RestaurantTable target, int data) =>
				{
					target.ApplyClientDeliveringOrderEnter();
				}, 4096, worldRevision))
				{
					ApplyClientDeliveringOrderEnter();
				}
			}
		}

		private void ServerSendClientDeliveringOrderExit()
		{
			if (base.IsServer)
			{
				ClientDeliveringOrderExitRevisionRpc(BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientDeliveringOrderExitRevisionRpc(ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(992866641u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 992866641u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, 0, (RestaurantTable target, int data) =>
				{
					target.ApplyClientDeliveringOrderExit();
				}, 4096, worldRevision))
				{
					ApplyClientDeliveringOrderExit();
				}
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerTakeBackDeliveredTrayRpc(NetworkObjectReference interactRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1656241749u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 1656241749u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsServer)
			{
				return;
			}
			if (_currentState.Value != RestaurantTableState.DeliveringOrder)
			{
				UtilityMethods.Log($"[RestaurantTable] TakeBack ignored. Table is not DeliveringOrder. TableId={_id}, State={_currentState.Value}");
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!interactRef.TryGetComponent<Interactor>(out var component))
			{
				UtilityMethods.Log("[RestaurantTable] TakeBack failed. Interactor component not found.", LogType.Warning);
			}
			else
			{
				if (!ServerCanInteractorPickUpTray(component, senderClientId, "TakeBack"))
				{
					return;
				}
				if (!TryFindNearestDeliveredTrayRecord(component.transform.position, out var recordIndex))
				{
					UtilityMethods.Log($"[RestaurantTable] TakeBack failed. No take-backable tray found. TableId={_id}");
					ServerRefreshTakeBackableTrayCount();
					return;
				}
				DeliveredTrayRecord deliveredTrayRecord = _deliveredTrayRecords[recordIndex];
				if (deliveredTrayRecord == null || deliveredTrayRecord.Tray == null || deliveredTrayRecord.Tray.NetworkObject == null || !deliveredTrayRecord.Tray.NetworkObject.IsSpawned)
				{
					UtilityMethods.Log("[RestaurantTable] TakeBack failed. Tray reference is invalid.", LogType.Warning);
					_deliveredTrayRecords.RemoveAt(recordIndex);
					ServerRefreshTakeBackableTrayCount();
					return;
				}
				if (deliveredTrayRecord.SeatIndex < 0 || deliveredTrayRecord.SeatIndex >= _seatOrders.Count)
				{
					UtilityMethods.Log($"[RestaurantTable] TakeBack failed. Seat index is invalid. Seat={deliveredTrayRecord.SeatIndex}", LogType.Warning);
					return;
				}
				CustomerSeatOrder customerSeatOrder = _seatOrders[deliveredTrayRecord.SeatIndex];
				if (customerSeatOrder == null)
				{
					customerSeatOrder = new CustomerSeatOrder
					{
						SeatIndex = deliveredTrayRecord.SeatIndex
					};
				}
				if (customerSeatOrder.Order != null)
				{
					UtilityMethods.Log($"[RestaurantTable] TakeBack blocked. Seat already has an active order. Seat={deliveredTrayRecord.SeatIndex}", LogType.Warning);
					return;
				}
				ServiceLocator.ForSceneOf(this).Get<TabletNetworkHandler>(out var service);
				if (service == null || deliveredTrayRecord.OrderId < 0 || !service.ServerUnregisterDeliveredFoods(deliveredTrayRecord.OrderId, deliveredTrayRecord.DeliveredPayload))
				{
					UtilityMethods.Log($"[RestaurantTable] TakeBack failed. OrdersApp rollback failed. TableId={_id}, OrderId={deliveredTrayRecord.OrderId}", LogType.Warning);
				}
				else if (component.ServerPrepareToHoldAnotherItem(senderClientId, "TakeBack"))
				{
					_deliveredTrayRecords.RemoveAt(recordIndex);
					RemoveTrackedTray(deliveredTrayRecord.Tray);
					customerSeatOrder.SeatIndex = deliveredTrayRecord.SeatIndex;
					customerSeatOrder.Order = deliveredTrayRecord.Order;
					customerSeatOrder.OrderId = deliveredTrayRecord.OrderId;
					_seatOrders[deliveredTrayRecord.SeatIndex] = customerSeatOrder;
					_completedOrderCount = Mathf.Max(0, _completedOrderCount - 1);
					_totalPayment = Mathf.Max(0, _totalPayment - Mathf.Max(0, deliveredTrayRecord.EarnedPayment));
					_totalPaymentFinal = _totalPayment;
					RestoreServingAttributionAfterTakeBack(deliveredTrayRecord);
					deliveredTrayRecord.Tray.CanHold.Value = true;
					deliveredTrayRecord.Tray.CanGaze.Value = true;
					component.ServerHoldRpc(deliveredTrayRecord.Tray.NetworkObject.ToReference());
					ServerRestoreDeliveredSeatVisualForTray(deliveredTrayRecord.Tray);
					ServerRefreshTakeBackableTrayCount();
					ServerSendClientTakeBackFeedback(deliveredTrayRecord.SeatIndex, _totalPaymentFinal);
				}
			}
		}

		private bool TryFindNearestDeliveredTrayRecord(Vector3 worldPosition, out int recordIndex)
		{
			recordIndex = -1;
			float num = float.MaxValue;
			for (int num2 = _deliveredTrayRecords.Count - 1; num2 >= 0; num2--)
			{
				DeliveredTrayRecord deliveredTrayRecord = _deliveredTrayRecords[num2];
				if (!IsDeliveredTrayRecordValid(deliveredTrayRecord))
				{
					_deliveredTrayRecords.RemoveAt(num2);
				}
				else
				{
					float sqrMagnitude = (deliveredTrayRecord.Tray.transform.position - worldPosition).sqrMagnitude;
					if (!(sqrMagnitude >= num))
					{
						num = sqrMagnitude;
						recordIndex = num2;
					}
				}
			}
			return recordIndex >= 0;
		}

		private bool IsDeliveredTrayRecordValid(DeliveredTrayRecord record)
		{
			if (record != null && record.Tray != null && record.Tray.NetworkObject != null && record.Tray.NetworkObject.IsSpawned && record.Order != null)
			{
				return record.OrderId >= 0;
			}
			return false;
		}

		private void ServerRefreshTakeBackableTrayCount()
		{
			if (!base.IsServer)
			{
				return;
			}
			for (int num = _deliveredTrayRecords.Count - 1; num >= 0; num--)
			{
				if (!IsDeliveredTrayRecordValid(_deliveredTrayRecords[num]))
				{
					_deliveredTrayRecords.RemoveAt(num);
				}
			}
			_takeBackableTrayCount.Value = ((_currentState.Value == RestaurantTableState.DeliveringOrder) ? _deliveredTrayRecords.Count : 0);
		}

		private void ServerClearDeliveredTrayRecords()
		{
			if (base.IsServer)
			{
				_deliveredTrayRecords.Clear();
				_takeBackableTrayCount.Value = 0;
			}
		}

		private void RestoreServingAttributionAfterTakeBack(DeliveredTrayRecord record)
		{
			if (record != null && _servingSteamIdBySeat.TryGetValue(record.SeatIndex, out var value) && value == record.WaitressSteamId)
			{
				if (record.HadPreviousServingSteamId)
				{
					_servingSteamIdBySeat[record.SeatIndex] = record.PreviousServingSteamId;
				}
				else
				{
					_servingSteamIdBySeat.Remove(record.SeatIndex);
				}
			}
		}

		private void ServerRecordCompletedCustomers(DailyReportBroadcaster dailyReport)
		{
			int num = Mathf.Max(0, _seatedCount.Value);
			if (num <= 0)
			{
				return;
			}
			_customerCreditsBuffer.Clear();
			int num2 = 0;
			for (int i = 0; i < num; i++)
			{
				if (_servingSteamIdBySeat.TryGetValue(i, out var value))
				{
					switch (value)
					{
					case 0uL:
						num2++;
						break;
					default:
					{
						_customerCreditsBuffer[value] = ((!_customerCreditsBuffer.TryGetValue(value, out var value2)) ? 1 : (value2 + 1));
						break;
					}
					case ulong.MaxValue:
						break;
					}
				}
			}
			dailyReport.ServerRecordCustomersServed(num, _customerCreditsBuffer, num2);
		}

		private bool RemoveTrackedTray(Tray tray)
		{
			if (tray == null)
			{
				return false;
			}
			for (int num = _trays.Count - 1; num >= 0; num--)
			{
				if (!(_trays[num] != tray))
				{
					_trays.RemoveAt(num);
					return true;
				}
			}
			return false;
		}

		private bool ServerCanInteractorPickUpTray(Interactor interactor, ulong senderClientId, string context)
		{
			if (interactor == null)
			{
				return false;
			}
			if (interactor.OwnerClientId != senderClientId)
			{
				return false;
			}
			BaseHoldable currentHeld = interactor.CurrentHeld;
			if (currentHeld == null)
			{
				return true;
			}
			if (currentHeld.OwnerClientId == senderClientId && !currentHeld.CanHold.Value)
			{
				return true;
			}
			UtilityMethods.Log($"[RestaurantTable] {context} blocked. Current held item is invalid. TableId={_id}", LogType.Warning);
			return false;
		}

		private bool ServerDoesInteractorHeldStateMatch(Interactor interactor, NetworkObjectReference expectedHeldRef, string context)
		{
			if (interactor == null)
			{
				return false;
			}
			if (((interactor.CurrentHeld != null && interactor.CurrentHeld.NetworkObject != null) ? interactor.CurrentHeld.NetworkObject.ToReference() : default(NetworkObjectReference)).Equals(expectedHeldRef))
			{
				return true;
			}
			UtilityMethods.Log($"[RestaurantTable] {context} ignored because the client has not acknowledged its previous held item yet. TableId={_id}");
			return false;
		}

		private void ApplyClientTakeBackFeedback(int seatIndex, int totalPaymentFinal)
		{
			ApplyTotalPaymentFinal(totalPaymentFinal);
			SetSeatAvailableObjectActive(seatIndex, isActive: true);
			UtilityMethods.Log($"[Restaurant Table] Took back delivered tray. Seat:{seatIndex} FinalTotal:{totalPaymentFinal}");
		}

		private bool TryFindBestSeatMatch(Tray tray, out int bestIndex, out DeliveryEvaluation bestEval, out OrderIssueFlags rejectIssues)
		{
			bestIndex = -1;
			bestEval = default;
			rejectIssues = OrderIssueFlags.None;
			if (tray == null)
			{
				UtilityMethods.Log("[RestaurantTable] TryFindBestSeatMatch: FAIL - Tray is null.", LogType.Warning);
				return false;
			}
			if (_seatOrders == null)
			{
				UtilityMethods.Log("[RestaurantTable] TryFindBestSeatMatch: FAIL - _seatOrders is null.", LogType.Warning);
				return false;
			}
			if (_seatOrders.Count == 0)
			{
				UtilityMethods.Log("[RestaurantTable] TryFindBestSeatMatch: FAIL - No seat orders available (_seatOrders.Count == 0).");
				return false;
			}
			bool flag = false;
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			for (int i = 0; i < _seatOrders.Count; i++)
			{
				CustomerSeatOrder customerSeatOrder = _seatOrders[i];
				if (customerSeatOrder.Order == null)
				{
					num++;
					continue;
				}
				DeliveryEvaluation deliveryEvaluation = tray.EvaluateDelivery(customerSeatOrder.Order);
				if (deliveryEvaluation.Result == OrderMatchResult.None)
				{
					num2++;
					continue;
				}
				OrderIssueFlags blockingDeliveryIssues = GetBlockingDeliveryIssues(deliveryEvaluation.Issues);
				if (blockingDeliveryIssues != OrderIssueFlags.None)
				{
					num3++;
					rejectIssues |= blockingDeliveryIssues;
					UtilityMethods.Log($"[RestaurantTable] TryFindBestSeatMatch: Candidate blocked by optional issue. SeatIndex={i} Issues={blockingDeliveryIssues} Tray={tray.name}");
				}
				else if (!flag)
				{
					flag = true;
					bestIndex = i;
					bestEval = deliveryEvaluation;
					UtilityMethods.Log($"[RestaurantTable] TryFindBestSeatMatch: Candidate found. SeatIndex={i} Result={bestEval.Result} Penalty={bestEval.Penalty} Tray={tray.name}");
					if (IsPerfectDelivery(bestEval))
					{
						UtilityMethods.Log($"[RestaurantTable] TryFindBestSeatMatch: Perfect match. Early-exit. SeatIndex={i} Tray={tray.name}");
						break;
					}
				}
				else if ((int)deliveryEvaluation.Result > (int)bestEval.Result)
				{
					UtilityMethods.Log($"[RestaurantTable] TryFindBestSeatMatch: Better match found. PrevSeat={bestIndex} PrevResult={bestEval.Result} PrevPenalty={bestEval.Penalty} -> NewSeat={i} NewResult={deliveryEvaluation.Result} NewPenalty={deliveryEvaluation.Penalty} Tray={tray.name}");
					bestIndex = i;
					bestEval = deliveryEvaluation;
				}
				else if (deliveryEvaluation.Result == bestEval.Result && deliveryEvaluation.Earned > bestEval.Earned)
				{
					UtilityMethods.Log($"[RestaurantTable] TryFindBestSeatMatch: Same result, higher earned. PrevSeat={bestIndex} PrevEarned={bestEval.Earned} -> NewSeat={i} NewEarned={deliveryEvaluation.Earned} Result={deliveryEvaluation.Result} Tray={tray.name}");
					bestIndex = i;
					bestEval = deliveryEvaluation;
				}
			}
			if (!flag || bestIndex == -1)
			{
				UtilityMethods.Log($"[RestaurantTable] TryFindBestSeatMatch: FAIL - No valid seat match. SeatOrders={_seatOrders.Count} SkippedNullOrder={num} SkippedNoMatch={num2} SkippedBlockedOptional={num3} RejectIssues={rejectIssues} Tray={tray.name}", LogType.Warning);
				return false;
			}
			UtilityMethods.Log($"[RestaurantTable] TryFindBestSeatMatch: SUCCESS - BestSeat={bestIndex} Result={bestEval.Result} Penalty={bestEval.Penalty} Tray={tray.name}");
			return true;
		}

		private static bool IsPerfectDelivery(DeliveryEvaluation eval)
		{
			if (eval.Result == OrderMatchResult.Full && eval.Issues == OrderIssueFlags.None)
			{
				return eval.Penalty == 0;
			}
			return false;
		}

		private static OrderIssueFlags GetBlockingDeliveryIssues(OrderIssueFlags issues)
		{
			return issues & (OrderIssueFlags.SideWrong | OrderIssueFlags.SideExtra | OrderIssueFlags.BeverageWrong | OrderIssueFlags.BeverageExtra);
		}

		private static string GetDeliveryRejectMessage(OrderIssueFlags issues)
		{
			if ((issues & OrderIssueFlags.SideWrong) != OrderIssueFlags.None && (issues & OrderIssueFlags.BeverageWrong) != OrderIssueFlags.None)
			{
				return "Wrong side dish and beverage.";
			}
			if ((issues & OrderIssueFlags.SideExtra) != OrderIssueFlags.None && (issues & OrderIssueFlags.BeverageExtra) != OrderIssueFlags.None)
			{
				return "This customer did not order a side dish or beverage.";
			}
			if ((issues & OrderIssueFlags.SideWrong) != OrderIssueFlags.None)
			{
				return "Wrong side dish for this customer.";
			}
			if ((issues & OrderIssueFlags.BeverageWrong) != OrderIssueFlags.None)
			{
				return "Wrong beverage for this customer.";
			}
			if ((issues & OrderIssueFlags.SideExtra) != OrderIssueFlags.None)
			{
				return "This customer did not order a side dish.";
			}
			if ((issues & OrderIssueFlags.BeverageExtra) != OrderIssueFlags.None)
			{
				return "This customer did not order a beverage.";
			}
			return "Wrong main dish for this table.";
		}

		private void ApplyClientDeliveryFeedback(FixedString64Bytes waitressName, int seatIndex, byte matchResult, ushort issues, int penalty, int earned, int totalPaymentFinal)
		{
			ApplyTotalPaymentFinal(totalPaymentFinal);
			SetSeatAvailableObjectActive(seatIndex, isActive: false);
			OrderMatchResult orderMatchResult = (OrderMatchResult)matchResult;
			OrderIssueFlags orderIssueFlags = (OrderIssueFlags)issues;
			UtilityMethods.Log("[Restaurant Table] " + $"Waitress:{waitressName} " + $"Delivered Seat:{seatIndex} " + $"Result:{orderMatchResult} " + $"Issues:{orderIssueFlags} " + $"Penalty:{penalty} " + $"Earned:{earned} " + $"FinalTotal:{totalPaymentFinal}");
		}

		private void ServerEatingStateEnter()
		{
			_isTableNeedsToClean = true;
			int num = Mathf.Clamp(_seatedCount.Value, 0, _seatCount);
			if (num > 0)
			{
				ServerSendClientStartRestaurantAmbient((byte)((num != 1) ? 1 : 2));
			}
			int stateVersion = _stateVersion;
			int num2 = UnityEngine.Random.Range(_minEatingTime, _maxEatingTime + 1);
			Singleton<DelayHelper>.Instance.DelayAction(num2, () =>
			{
				if (base.IsServer)
				{
					if (stateVersion != _stateVersion)
					{
						UtilityMethods.Log($"[RestaurantTable] Eating delay ignored because state version changed. TableId={_id}");
					}
					else if (_currentState.Value != RestaurantTableState.Eating)
					{
						UtilityMethods.Log($"[RestaurantTable] Eating delay ignored because table is no longer Eating. TableId={_id}, CurrentState={_currentState.Value}");
					}
					else
					{
						ServerChangeState(RestaurantTableState.AwaitingPayment);
					}
				}
			});
		}

		private void ServerEatingStateExit()
		{
			ServerSendClientStopRestaurantAmbient();
			foreach (Tray tray in _trays)
			{
				tray.ServerEatingDone();
			}
		}

		private void ApplyClientEatingEnter()
		{
			if (_debugText != null)
			{
				_debugText.text = "State: Eating";
			}
		}

		private void ServerSendClientTakeBackFeedback(int seatIndex, int totalPaymentFinal)
		{
			if (base.IsServer)
			{
				ClientTakeBackFeedbackRevisionRpc(seatIndex, totalPaymentFinal, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientEatingExitRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(3526569147u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 3526569147u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientTakeBackFeedbackRevisionRpc(int seatIndex, int totalPaymentFinal, ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(948352604u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, seatIndex);
				BytePacker.WriteValueBitPacked(bufferWriter, totalPaymentFinal);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 948352604u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, (seatIndex, totalPaymentFinal), (RestaurantTable target, (int seatIndex, int totalPaymentFinal) data) =>
				{
					target.ApplyClientTakeBackFeedback(data.seatIndex, data.totalPaymentFinal);
				}, 4096, worldRevision))
				{
					ApplyClientTakeBackFeedback(seatIndex, totalPaymentFinal);
				}
			}
		}

		private void ServerAwaitingPaymentEnter()
		{
			Feedback.ServerReadyForPayment(this, _id);
			Feedback.ServerBeginReminder(RestaurantTableState.AwaitingPayment, GetServerTimeSafe());
			ServerSendClientPlayRestaurantMoment(0, 3, 0);
		}

		private void ServerAwaitingPaymentExit()
		{
			Feedback.ServerStopReminder(RestaurantTableState.AwaitingPayment);
		}

		[Rpc(SendTo.Server)]
		private void ServerAwaitingPaymentInteractRpc(NetworkObjectReference interactorRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(3704567097u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 3704567097u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsServer)
			{
				return;
			}
			if (_currentState.Value != RestaurantTableState.AwaitingPayment)
			{
				UtilityMethods.Log($"[RestaurantTable] Payment ignored. Table is not AwaitingPayment. TableId={_id}, State={_currentState.Value}");
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (interactorRef.TryGetComponent<Interactor>(out var component) && component.OwnerClientId == senderClientId)
			{
				int num = Mathf.Max(0, _totalPaymentFinal);
				string playerName = ((component.Player != null) ? component.Player.PlayerName.Value.ToString() : $"Client {senderClientId}");
				if (num > 0)
				{
					ServiceLocator.ForSceneOf(this).Get<EconomyHandler>(out var service);
					service.EarnMoney(num, senderClientId);
					ServiceLocator.ForSceneOf(this).Get<DailyReportBroadcaster>(out var service2);
					ulong steamId = ((component.Player != null) ? component.Player.SteamID.Value : 0);
					service2.ServerRecordMoneyEarned(steamId, num);
					BaseRpcTarget baseRpcTarget = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
					ServerSendClientPlayMoneyEarnedSfx(baseRpcTarget);
				}
				else
				{
					UtilityMethods.Log($"[RestaurantTable] Payment completed with zero money. TableId={_id}", LogType.Warning);
					BaseRpcTarget baseRpcTarget2 = base.RpcTarget.Single(senderClientId, RpcTargetUse.Temp);
					ServerSendClientShowTableToast("Payment completed, but no money was earned.", NotificationState.Info, baseRpcTarget2);
				}
				Feedback.ServerPaymentCollected(this, _id, playerName, num);
				ServerSendClientPlayRestaurantMoment(0, 5, 0);
				_totalPayment = 0;
				_totalPaymentFinal = 0;
				Singleton<GameProgressManager>.Instance.ServerGrantProgressionXp(ProgressionXpSource.CustomerServed, _id.ToString(), senderClientId);
				QuestReferenceHub.Instance.QuestComplete(QuestId.CollectPayment);
				ServerChangeState(RestaurantTableState.Cleaning);
			}
		}

		private void ApplyClientAwaitingPaymentEnter()
		{
			if (_debugText != null)
			{
				_debugText.text = "State: Awaiting Payment";
			}
			if (_moneyGraphPrefab != null)
			{
				_moneyGraphPrefab.SetActive(value: true);
			}
		}

		private void ApplyClientAwaitingPaymentExit()
		{
			if (_moneyGraphPrefab != null)
			{
				_moneyGraphPrefab.SetActive(value: false);
			}
		}

		private void ApplyClientPlayMoneyEarnedSfx()
		{
			Singleton<SoundManager>.Instance?.PlaySFX(SoundType.Money_Earned);
		}

		private void ServerCleaningStateEnter()
		{
			_cleanedTrayCount = 0;
			_cleaningRequiredTrayCount = Mathf.Max(0, _trays.Count);
			if (!_cleaningBecauseCustomersLeft)
			{
				ServiceLocator.ForSceneOf(this).Get<DailyReportBroadcaster>(out var service);
				ServerRecordCompletedCustomers(service);
			}
			if (_cleaningRequiredTrayCount > 0)
			{
				return;
			}
			UtilityMethods.Log($"[RestaurantTable] Cleaning entered with no trays. TableId={_id}", LogType.Warning);
			Singleton<DelayHelper>.Instance.DelayActionUntilEndOfFrame(() =>
			{
				if (base.IsServer && _currentState.Value == RestaurantTableState.Cleaning)
				{
					ServerCompleteCleaning();
				}
			});
		}

		private void ServerCompleteCleaning()
		{
			_cleanedTrayCount = 0;
			_cleaningRequiredTrayCount = 0;
			_cleaningBecauseCustomersLeft = false;
			ServerChangeState(RestaurantTableState.Empty);
		}

		private void ServerCleaningStateExit()
		{
		}

		private void ApplyClientCleaningEnter()
		{
			if (_debugText != null)
			{
				_debugText.text = (_cleaningBecauseCustomersLeft ? "State: Cleaning After Customers Left" : "State: Cleaning");
			}
		}

		private void ServerSendClientDeliveryFeedback(FixedString64Bytes waitressName, int seatIndex, byte matchResult, ushort issues, int penalty, int earned, int totalPaymentFinal)
		{
			if (base.IsServer)
			{
				ClientDeliveryFeedbackRevisionRpc(waitressName, seatIndex, matchResult, issues, penalty, earned, totalPaymentFinal, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientDeliveryFeedbackRevisionRpc(FixedString64Bytes waitressName, int seatIndex, byte matchResult, ushort issues, int penalty, int earned, int totalPaymentFinal, ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1892617651u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in waitressName, default(FastBufferWriter.ForFixedStrings));
				BytePacker.WriteValueBitPacked(bufferWriter, seatIndex);
				bufferWriter.WriteValueSafe(in matchResult, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, issues);
				BytePacker.WriteValueBitPacked(bufferWriter, penalty);
				BytePacker.WriteValueBitPacked(bufferWriter, earned);
				BytePacker.WriteValueBitPacked(bufferWriter, totalPaymentFinal);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 1892617651u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, (waitressName, seatIndex, matchResult, issues, penalty, earned, totalPaymentFinal), (RestaurantTable target, (FixedString64Bytes waitressName, int seatIndex, byte matchResult, ushort issues, int penalty, int earned, int totalPaymentFinal) data) =>
				{
					target.ApplyClientDeliveryFeedback(data.waitressName, data.seatIndex, data.matchResult, data.issues, data.penalty, data.earned, data.totalPaymentFinal);
				}, 4096, worldRevision))
				{
					ApplyClientDeliveryFeedback(waitressName, seatIndex, matchResult, issues, penalty, earned, totalPaymentFinal);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		private void ClientCleaningExitRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(969474723u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 969474723u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		[Rpc(SendTo.Server)]
		private void ServerCleaningInteractRpc(NetworkObjectReference interactRef, NetworkObjectReference expectedHeldRef, RpcParams rpcParams = default(RpcParams))
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
				FastBufferWriter bufferWriter = __beginSendRpc(1490711507u, rpcParams2, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactRef, default(FastBufferWriter.ForNetworkSerializable));
				bufferWriter.WriteValueSafe(in expectedHeldRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 1490711507u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				return;
			}
			__rpc_exec_stage = __RpcExecStage.Send;
			if (!base.IsServer)
			{
				return;
			}
			if (_currentState.Value != RestaurantTableState.Cleaning)
			{
				UtilityMethods.Log($"[RestaurantTable] Cleaning ignored. Table is not Cleaning. TableId={_id}, State={_currentState.Value}");
				return;
			}
			ulong senderClientId = rpcParams.Receive.SenderClientId;
			if (!interactRef.TryGetComponent<Interactor>(out var component))
			{
				UtilityMethods.Log("[RestaurantTable] Cleaning failed. Interactor component not found.", LogType.Warning);
			}
			else
			{
				if (!ServerCanInteractorPickUpTray(component, senderClientId, "Cleaning") || !ServerDoesInteractorHeldStateMatch(component, expectedHeldRef, "Cleaning"))
				{
					return;
				}
				if (_trays.Count == 0)
				{
					UtilityMethods.Log("[RestaurantTable] Cleaning found no trays. Completing cleaning flow.", LogType.Warning);
					ServerCompleteCleaning();
				}
				else
				{
					if (!component.ServerPrepareToHoldAnotherItem(senderClientId, "Cleaning"))
					{
						return;
					}
					int index = _trays.Count - 1;
					Tray tray = _trays[index];
					if (tray == null || tray.NetworkObject == null || !tray.NetworkObject.IsSpawned)
					{
						UtilityMethods.Log("[RestaurantTable] Cleaning skipped invalid tray reference.", LogType.Warning);
						_trays.RemoveAt(index);
						if (_trays.Count == 0)
						{
							ServerChangeState(RestaurantTableState.Empty);
						}
						return;
					}
					tray.CanHold.Value = true;
					tray.CanGaze.Value = true;
					if (!component.ServerTryHoldItem(tray.NetworkObject.ToReference()))
					{
						tray.CanHold.Value = false;
						tray.CanGaze.Value = false;
						UtilityMethods.Log($"[RestaurantTable] Cleaning tray pickup failed before commit. TableId={_id}, Tray={tray.name}", LogType.Warning);
						return;
					}
					_trays.RemoveAt(index);
					ServerRestoreDeliveredSeatVisualForTray(tray);
					_cleanedTrayCount++;
					QuestReferenceHub.Instance.QuestComplete(QuestId.PickUpTheDirtyTray);
					int num = Mathf.Max(1, _cleaningRequiredTrayCount);
					if (_cleanedTrayCount >= num || _trays.Count == 0)
					{
						ServerCompleteCleaning();
					}
				}
			}
		}

		private void ServerSendClientEatingEnter()
		{
			if (base.IsServer)
			{
				ClientEatingEnterRevisionRpc(BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientEatingEnterRevisionRpc(ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1088754815u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 1088754815u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, 0, (RestaurantTable target, int data) =>
				{
					target.ApplyClientEatingEnter();
				}, 4096, worldRevision))
				{
					ApplyClientEatingEnter();
				}
			}
		}

		[Rpc(SendTo.Server)]
		public void ServerChangeSeatedCountRpc(int value)
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
				FastBufferWriter bufferWriter = __beginSendRpc(240508425u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, value);
				__endSendRpc(ref bufferWriter, 240508425u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_seatedCount.Value = value;
				_lateJoinLastSeatedCount = value;
			}
		}

		private void SendCustomerMenuWaitingForArrivedSeats(bool isWaiting)
		{
			int num = Mathf.Clamp(_completedCustomerCount, 0, _seatCount);
			for (int i = 0; i < num; i++)
			{
				SendCustomerMenuWaitingVisual(i, isWaiting);
			}
		}

		private void SendCustomerMenuWaitingVisual(int seatIndex, bool isWaiting)
		{
			RaiseCustomerMenuWaitingVisual(seatIndex, isWaiting);
			ServerSendClientCustomerMenuWaitingVisual(seatIndex, isWaiting);
		}

		private void ApplyClientCustomerMenuWaitingVisual(int seatIndex, bool isWaiting)
		{
			RaiseCustomerMenuWaitingVisual(seatIndex, isWaiting);
		}

		private void ServerSendClientAwaitingPaymentEnter()
		{
			if (base.IsServer)
			{
				ClientAwaitingPaymentEnterRevisionRpc(BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientAwaitingPaymentEnterRevisionRpc(ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3686861657u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 3686861657u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, 0, (RestaurantTable target, int data) =>
				{
					target.ApplyClientAwaitingPaymentEnter();
				}, 4096, worldRevision))
				{
					ApplyClientAwaitingPaymentEnter();
				}
			}
		}

		private void ServerSendClientAwaitingPaymentExit()
		{
			if (base.IsServer)
			{
				ClientAwaitingPaymentExitRevisionRpc(BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientAwaitingPaymentExitRevisionRpc(ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2687671101u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 2687671101u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, 0, (RestaurantTable target, int data) =>
				{
					target.ApplyClientAwaitingPaymentExit();
				}, 4096, worldRevision))
				{
					ApplyClientAwaitingPaymentExit();
				}
			}
		}

		private void ServerSendClientPlayMoneyEarnedSfx(RpcParams rpcParams = default(RpcParams))
		{
			if (base.IsServer)
			{
				ClientPlayMoneyEarnedSfxRevisionRpc(BootstrapLiveUpdateBuffer.NextServerRevision(), rpcParams);
			}
		}

		[Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
		private void ClientPlayMoneyEarnedSfxRevisionRpc(ulong worldRevision, RpcParams rpcParams = default(RpcParams))
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
					InvokePermission = RpcInvokePermission.Server
				};
				FastBufferWriter bufferWriter = __beginSendRpc(3379710945u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 3379710945u, rpcParams, attributeParams, SendTo.SpecifiedInParams, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, 0, (RestaurantTable target, int data) =>
				{
					target.ApplyClientPlayMoneyEarnedSfx();
				}, 4096, worldRevision))
				{
					ApplyClientPlayMoneyEarnedSfx();
				}
			}
		}

		private void RaiseCustomerMenuWaitingVisual(int seatIndex, bool isWaiting)
		{
			EventBus<RestaurantCustomerMenuWaitingEvent>.Raise(new RestaurantCustomerMenuWaitingEvent
			{
				TableId = _id,
				SeatIndex = seatIndex,
				IsWaiting = isWaiting
			});
		}

		private void ApplyClientChangeState(byte newState)
		{
			RestaurantTableState restaurantTableState = (RestaurantTableState)newState;
			if (ShouldResetPaymentForState(restaurantTableState))
			{
				ApplyTotalPaymentFinal(0);
			}
			EventBus<RestaurantTableStateChangeEvent>.Raise(new RestaurantTableStateChangeEvent
			{
				NewState = restaurantTableState,
				TableId = _id
			});
		}

		private void ApplyClientRaiseVisualState(byte visualState)
		{
			EventBus<RestaurantTableStateChangeEvent>.Raise(new RestaurantTableStateChangeEvent
			{
				NewState = (RestaurantTableState)visualState,
				TableId = _id
			});
		}

		private void ServerSendClientCleaningEnter()
		{
			if (base.IsServer)
			{
				ClientCleaningEnterRevisionRpc(BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientCleaningEnterRevisionRpc(ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(757846584u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 757846584u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, 0, (RestaurantTable target, int data) =>
				{
					target.ApplyClientCleaningEnter();
				}, 4096, worldRevision))
				{
					ApplyClientCleaningEnter();
				}
			}
		}

		private void ApplyClientPlayRestaurantFeedbackCue(byte cue)
		{
			if (Enum.IsDefined(typeof(RestaurantFeedbackCue), cue))
			{
				Feedback.PlayClientCue(_id, (RestaurantFeedbackCue)cue);
			}
		}

		private void ApplyClientPlayRestaurantMoment(int speakerSeatIndex, byte cue, byte gesture)
		{
			if (Enum.IsDefined(typeof(RestaurantFeedbackCue), cue) && Enum.IsDefined(typeof(RestaurantResidentGesture), gesture))
			{
				RestaurantFeedbackCue cue2 = (RestaurantFeedbackCue)cue;
				Feedback.PlayClientMoment(_id, speakerSeatIndex, cue2, (RestaurantResidentGesture)gesture);
				PlayRestaurantFeedbackCueAudio(cue2);
			}
		}

		private void ApplyClientPlayRestaurantAudio(byte cue, int speakerSeatIndex = -1)
		{
			if (Enum.IsDefined(typeof(RestaurantAudioCue), cue))
			{
				RestaurantAudioCue restaurantAudioCue = (RestaurantAudioCue)cue;
				AudioClipSO audioClipSO = restaurantAudioCue switch
				{
					RestaurantAudioCue.Happy => ChooseRestaurantAudioBank(_restaurantHappyBanks, speakerSeatIndex), 
					RestaurantAudioCue.PaymentRequest => _restaurantPaymentRequestBank, 
					RestaurantAudioCue.Farewell => _restaurantFarewellBank, 
					RestaurantAudioCue.SadLeave => _restaurantSadLeaveBank, 
					_ => null, 
				};
				if (audioClipSO != null)
				{
					PlayRestaurantSpatialOneShot(audioClipSO);
				}
				else
				{
					PlayRestaurantTableOneShot(restaurantAudioCue, speakerSeatIndex);
				}
			}
		}

		private void ApplyClientPlayRestaurantHiThenStartAmbient(int speakerSeatIndex)
		{
			PlayRestaurantTableHiThenStartAmbient(speakerSeatIndex);
		}

		private void ApplyClientStartRestaurantAmbient(byte cue)
		{
			if (Enum.IsDefined(typeof(RestaurantAudioCue), cue))
			{
				RestaurantAudioCue restaurantAudioCue = (RestaurantAudioCue)cue;
				if (restaurantAudioCue == RestaurantAudioCue.Talking || restaurantAudioCue == RestaurantAudioCue.Eating)
				{
					StartRestaurantTableAmbient(restaurantAudioCue);
				}
			}
		}

		private void ApplyClientStopRestaurantAmbient()
		{
			StopRestaurantTableAudio(includeOneShots: false);
		}

		private void ServerSendClientCustomerMenuWaitingVisual(int seatIndex, bool isWaiting)
		{
			if (base.IsServer)
			{
				ClientCustomerMenuWaitingVisualRevisionRpc(seatIndex, isWaiting, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientCustomerMenuWaitingVisualRevisionRpc(int seatIndex, bool isWaiting, ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(866448535u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, seatIndex);
				bufferWriter.WriteValueSafe(in isWaiting, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 866448535u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, (seatIndex, isWaiting), (RestaurantTable target, (int seatIndex, bool isWaiting) data) =>
				{
					target.ApplyClientCustomerMenuWaitingVisual(data.seatIndex, data.isWaiting);
				}, 4096, worldRevision))
				{
					ApplyClientCustomerMenuWaitingVisual(seatIndex, isWaiting);
				}
			}
		}

		private void PlayRestaurantFeedbackCueAudio(RestaurantFeedbackCue cue)
		{
			switch (cue)
			{
			case RestaurantFeedbackCue.WaitingChatter:
			case RestaurantFeedbackCue.WaitingChatterReply:
				if (_currentState.Value == RestaurantTableState.DeliveringOrder && Mathf.Clamp(_seatedCount.Value, 0, _seatCount) > 1)
				{
					EnsureRestaurantAmbientPlaying(RestaurantAudioCue.Talking);
				}
				break;
			case RestaurantFeedbackCue.EatingChatter:
				if (_currentState.Value == RestaurantTableState.Eating)
				{
					int num = Mathf.Clamp(_seatedCount.Value, 0, _seatCount);
					if (num > 0)
					{
						EnsureRestaurantAmbientPlaying((num != 1) ? RestaurantAudioCue.Talking : RestaurantAudioCue.Eating);
					}
				}
				break;
			case RestaurantFeedbackCue.RequestPayment:
				PlayRestaurantSpatialOneShot(_restaurantPaymentRequestBank);
				break;
			case RestaurantFeedbackCue.PaymentCollectedFarewell:
				PlayRestaurantSpatialOneShot(_restaurantFarewellBank);
				break;
			case RestaurantFeedbackCue.OrderServedIncomplete:
				break;
			}
		}

		private void ServerSendClientChangeState(byte newState)
		{
			if (base.IsServer)
			{
				ClientChangeStateRevisionRpc(newState, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientChangeStateRevisionRpc(byte newState, ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(792787721u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in newState, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 792787721u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, newState, (RestaurantTable target, byte data) =>
				{
					target.ApplyClientChangeState(data);
				}, 4096, worldRevision))
				{
					ApplyClientChangeState(newState);
				}
			}
		}

		private void ServerSendClientRaiseVisualState(byte visualState)
		{
			if (base.IsServer)
			{
				ClientRaiseVisualStateRevisionRpc(visualState, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientRaiseVisualStateRevisionRpc(byte visualState, ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1823057485u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in visualState, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 1823057485u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, visualState, (RestaurantTable target, byte data) =>
				{
					target.ApplyClientRaiseVisualState(data);
				}, 4096, worldRevision))
				{
					ApplyClientRaiseVisualState(visualState);
				}
			}
		}

		private void ServerSendClientPlayRestaurantFeedbackCue(byte cue)
		{
			if (base.IsServer)
			{
				ClientPlayRestaurantFeedbackCueRevisionRpc(cue, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientPlayRestaurantFeedbackCueRevisionRpc(byte cue, ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1977094708u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in cue, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 1977094708u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, cue, (RestaurantTable target, byte data) =>
				{
					target.ApplyClientPlayRestaurantFeedbackCue(data);
				}, 4096, worldRevision))
				{
					ApplyClientPlayRestaurantFeedbackCue(cue);
				}
			}
		}

		private void ServerSendClientPlayRestaurantMoment(int speakerSeatIndex, byte cue, byte gesture)
		{
			if (base.IsServer)
			{
				ClientPlayRestaurantMomentRevisionRpc(speakerSeatIndex, cue, gesture, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientPlayRestaurantMomentRevisionRpc(int speakerSeatIndex, byte cue, byte gesture, ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(1585204484u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, speakerSeatIndex);
				bufferWriter.WriteValueSafe(in cue, default(FastBufferWriter.ForPrimitives));
				bufferWriter.WriteValueSafe(in gesture, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 1585204484u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, (speakerSeatIndex, cue, gesture), (RestaurantTable target, (int speakerSeatIndex, byte cue, byte gesture) data) =>
				{
					target.ApplyClientPlayRestaurantMoment(data.speakerSeatIndex, data.cue, data.gesture);
				}, 4096, worldRevision))
				{
					ApplyClientPlayRestaurantMoment(speakerSeatIndex, cue, gesture);
				}
			}
		}

		private void ServerSendClientPlayRestaurantAudio(byte cue, int speakerSeatIndex = -1)
		{
			if (base.IsServer)
			{
				ClientPlayRestaurantAudioRevisionRpc(cue, speakerSeatIndex, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientPlayRestaurantAudioRevisionRpc(byte cue, int speakerSeatIndex, ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3959231580u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in cue, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, speakerSeatIndex);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 3959231580u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, (cue, speakerSeatIndex), (RestaurantTable target, (byte cue, int speakerSeatIndex) data) =>
				{
					target.ApplyClientPlayRestaurantAudio(data.cue, data.speakerSeatIndex);
				}, 4096, worldRevision))
				{
					ApplyClientPlayRestaurantAudio(cue, speakerSeatIndex);
				}
			}
		}

		private void ServerSendClientPlayRestaurantHiThenStartAmbient(int speakerSeatIndex)
		{
			if (base.IsServer)
			{
				ClientPlayRestaurantHiThenStartAmbientRevisionRpc(speakerSeatIndex, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientPlayRestaurantHiThenStartAmbientRevisionRpc(int speakerSeatIndex, ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(682076219u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, speakerSeatIndex);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 682076219u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, speakerSeatIndex, (RestaurantTable target, int data) =>
				{
					target.ApplyClientPlayRestaurantHiThenStartAmbient(data);
				}, 4096, worldRevision))
				{
					ApplyClientPlayRestaurantHiThenStartAmbient(speakerSeatIndex);
				}
			}
		}

		private void ServerSendClientStartRestaurantAmbient(byte cue)
		{
			if (base.IsServer)
			{
				ClientStartRestaurantAmbientRevisionRpc(cue, BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientStartRestaurantAmbientRevisionRpc(byte cue, ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(2649236778u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in cue, default(FastBufferWriter.ForPrimitives));
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 2649236778u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, cue, (RestaurantTable target, byte data) =>
				{
					target.ApplyClientStartRestaurantAmbient(data);
				}, 4096, worldRevision))
				{
					ApplyClientStartRestaurantAmbient(cue);
				}
			}
		}

		private void ServerSendClientStopRestaurantAmbient()
		{
			if (base.IsServer)
			{
				ClientStopRestaurantAmbientRevisionRpc(BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientStopRestaurantAmbientRevisionRpc(ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(3848177965u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 3848177965u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, 0, (RestaurantTable target, int data) =>
				{
					target.ApplyClientStopRestaurantAmbient();
				}, 4096, worldRevision))
				{
					ApplyClientStopRestaurantAmbient();
				}
			}
		}

		private void EnsureRestaurantAmbientPlaying(RestaurantAudioCue cue)
		{
			if (!_restaurantAmbientPlaying && _restaurantPendingAmbientRoutine == null && _restaurantTalkingReplayRoutine == null)
			{
				StartRestaurantTableAmbient(cue);
			}
		}

		private void PlayRestaurantSpatialOneShot(AudioClipSO bank)
		{
			if (!(bank == null) && !(Singleton<SoundManager>.Instance == null))
			{
				Singleton<SoundManager>.Instance.PlaySFX(bank, base.transform.position, base.transform);
			}
		}

		private void PlayRestaurantTableHiThenStartAmbient(int speakerSeatIndex)
		{
			StopRestaurantTableAudio(includeOneShots: false);
			float delay = PlayRestaurantTableOneShot(RestaurantAudioCue.Hi, speakerSeatIndex);
			ScheduleRestaurantAmbient(RestaurantAudioCue.Talking, delay);
		}

		private float PlayRestaurantTableOneShot(RestaurantAudioCue cue, int speakerSeatIndex = -1)
		{
			AudioClipSO audioClipSO = cue switch
			{
				RestaurantAudioCue.Hi => ChooseRestaurantAudioBank(_restaurantHiBanks, speakerSeatIndex), 
				RestaurantAudioCue.Happy => ChooseRestaurantAudioBank(_restaurantHappyBanks, speakerSeatIndex), 
				_ => null, 
			};
			AudioSource restaurantAudioSource = GetRestaurantAudioSource();
			AudioClip audioClip = ResolveRestaurantAudioClip(audioClipSO);
			if (restaurantAudioSource == null || audioClipSO == null || audioClip == null)
			{
				return 0f;
			}
			if (!_restaurantAmbientPlaying)
			{
				ConfigureRestaurantAudioSource(restaurantAudioSource, audioClipSO);
				restaurantAudioSource.volume = 1f;
			}
			float volumeScale = ResolveRestaurantAudioVolume(audioClipSO) / Mathf.Max(0.0001f, restaurantAudioSource.volume);
			restaurantAudioSource.PlayOneShot(audioClip, volumeScale);
			float num = audioClip.length / Mathf.Max(0.01f, Mathf.Abs(restaurantAudioSource.pitch));
			_restaurantOneShotEndTime = Mathf.Max(_restaurantOneShotEndTime, Time.unscaledTime + num);
			return num;
		}

		private void StartRestaurantTableAmbient(RestaurantAudioCue cue)
		{
			if ((cue == RestaurantAudioCue.Talking || cue == RestaurantAudioCue.Eating) && (cue != RestaurantAudioCue.Talking || (!_restaurantAmbientPlaying && _restaurantPendingAmbientRoutine == null && _restaurantTalkingReplayRoutine == null)))
			{
				CancelPendingRestaurantAmbient();
				float num = Mathf.Max(0f, _restaurantOneShotEndTime - Time.unscaledTime);
				if (num > 0f)
				{
					ScheduleRestaurantAmbient(cue, num);
				}
				else
				{
					StartRestaurantTableAmbientNow(cue);
				}
			}
		}

		private void ScheduleRestaurantAmbient(RestaurantAudioCue cue, float delay)
		{
			CancelPendingRestaurantAmbient();
			_restaurantPendingAmbientRoutine = StartCoroutine(StartRestaurantAmbientAfterDelay(cue, Mathf.Max(0f, delay)));
		}

		private IEnumerator StartRestaurantAmbientAfterDelay(RestaurantAudioCue cue, float delay)
		{
			if (delay > 0f)
			{
				yield return new WaitForSecondsRealtime(delay);
			}
			_restaurantPendingAmbientRoutine = null;
			StartRestaurantTableAmbientNow(cue);
		}

		private void StartRestaurantTableAmbientNow(RestaurantAudioCue cue)
		{
			AudioClipSO audioClipSO = ((cue == RestaurantAudioCue.Eating) ? _restaurantEatingBank : _restaurantTalkingBank);
			AudioSource restaurantAudioSource = GetRestaurantAudioSource();
			AudioClip audioClip = ((cue == RestaurantAudioCue.Talking) ? ResolveRestaurantTalkingClip(audioClipSO) : ResolveRestaurantAudioClip(audioClipSO));
			if (!(restaurantAudioSource == null) && !(audioClipSO == null) && !(audioClip == null))
			{
				CancelRestaurantTalkingReplay();
				CancelRestaurantAmbientFade(restaurantAudioSource, stopSource: true);
				if (_restaurantAmbientPlaying)
				{
					restaurantAudioSource.Stop();
				}
				ConfigureRestaurantAudioSource(restaurantAudioSource, audioClipSO);
				restaurantAudioSource.clip = audioClip;
				restaurantAudioSource.volume = ResolveRestaurantAudioVolume(audioClipSO);
				restaurantAudioSource.loop = false;
				restaurantAudioSource.Play();
				_restaurantAmbientPlaying = true;
				if (cue == RestaurantAudioCue.Talking)
				{
					int playbackVersion = ++_restaurantAmbientPlaybackVersion;
					float clipDuration = audioClip.length / Mathf.Max(0.01f, Mathf.Abs(restaurantAudioSource.pitch));
					_restaurantTalkingReplayRoutine = StartCoroutine(ReplayRestaurantTalkingAfterDelay(restaurantAudioSource, audioClip, playbackVersion, clipDuration));
				}
			}
		}

		private void StopRestaurantTableAudio(bool includeOneShots)
		{
			CancelPendingRestaurantAmbient();
			CancelRestaurantTalkingReplay();
			_restaurantAmbientPlaybackVersion++;
			AudioSource restaurantAudioSource = GetRestaurantAudioSource();
			if (!(restaurantAudioSource == null))
			{
				if (includeOneShots)
				{
					CancelRestaurantAmbientFade(restaurantAudioSource, stopSource: false);
					restaurantAudioSource.Stop();
					restaurantAudioSource.clip = null;
					restaurantAudioSource.volume = 1f;
					_restaurantAmbientPlaying = false;
					_restaurantOneShotEndTime = 0f;
					_lastRestaurantTalkingClip = null;
				}
				else if (_restaurantAmbientPlaying)
				{
					_restaurantAmbientPlaying = false;
					StartRestaurantAmbientFadeOut(restaurantAudioSource);
				}
			}
		}

		private void CancelPendingRestaurantAmbient()
		{
			if (_restaurantPendingAmbientRoutine != null)
			{
				StopCoroutine(_restaurantPendingAmbientRoutine);
				_restaurantPendingAmbientRoutine = null;
			}
		}

		private IEnumerator ReplayRestaurantTalkingAfterDelay(AudioSource source, AudioClip playedClip, int playbackVersion, float clipDuration)
		{
			yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, clipDuration));
			if (!(source == null) && playbackVersion == _restaurantAmbientPlaybackVersion && _restaurantAmbientPlaying && !(source.clip != playedClip))
			{
				source.Stop();
				source.clip = null;
				source.volume = 1f;
				_restaurantAmbientPlaying = false;
				float num = Mathf.Max(0f, _restaurantTalkingReplayMinDelay);
				float maxInclusive = Mathf.Max(num, _restaurantTalkingReplayMaxDelay);
				yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(num, maxInclusive));
				if (playbackVersion == _restaurantAmbientPlaybackVersion && !_restaurantAmbientPlaying)
				{
					_restaurantTalkingReplayRoutine = null;
					StartRestaurantTableAmbientNow(RestaurantAudioCue.Talking);
				}
			}
		}

		private void CancelRestaurantTalkingReplay()
		{
			if (_restaurantTalkingReplayRoutine != null)
			{
				StopCoroutine(_restaurantTalkingReplayRoutine);
				_restaurantTalkingReplayRoutine = null;
			}
		}

		private AudioSource GetRestaurantAudioSource()
		{
			if (_restaurantAudioSource != null)
			{
				return _restaurantAudioSource;
			}
			if (!TryGetComponent<AudioSource>(out _restaurantAudioSource))
			{
				_restaurantAudioSource = base.gameObject.AddComponent<AudioSource>();
			}
			return _restaurantAudioSource;
		}

		private void StartRestaurantAmbientFadeOut(AudioSource source)
		{
			if (!(source == null))
			{
				CancelRestaurantAmbientFade(source, stopSource: false);
				if (!source.isPlaying || source.clip == null)
				{
					source.Stop();
					source.clip = null;
					source.volume = 1f;
				}
				else
				{
					_restaurantAmbientFadeRoutine = StartCoroutine(FadeOutRestaurantAmbient(source, source.clip, source.volume));
				}
			}
		}

		private IEnumerator FadeOutRestaurantAmbient(AudioSource source, AudioClip fadingClip, float startVolume)
		{
			float duration = Mathf.Max(0.05f, _restaurantAmbientFadeOutSeconds);
			float elapsed = 0f;
			while (elapsed < duration && source != null && source.isPlaying && source.clip == fadingClip && !_restaurantAmbientPlaying)
			{
				elapsed += Time.unscaledDeltaTime;
				source.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / duration));
				yield return null;
			}
			if (source != null && source.clip == fadingClip && !_restaurantAmbientPlaying)
			{
				source.Stop();
				source.clip = null;
				source.volume = 1f;
			}
			_restaurantAmbientFadeRoutine = null;
		}

		private void CancelRestaurantAmbientFade(AudioSource source, bool stopSource)
		{
			if (_restaurantAmbientFadeRoutine != null)
			{
				StopCoroutine(_restaurantAmbientFadeRoutine);
				_restaurantAmbientFadeRoutine = null;
				if (stopSource && !(source == null))
				{
					source.Stop();
					source.clip = null;
					source.volume = 1f;
				}
			}
		}

		private AudioClipSO ChooseRestaurantAudioBank(AudioClipSO[] banks, int speakerSeatIndex = -1)
		{
			if (banks == null || banks.Length == 0)
			{
				return null;
			}
			if (speakerSeatIndex >= 0)
			{
				ResidentManager instance = Singleton<ResidentManager>.Instance;
				if (instance != null && instance.TryGetRestaurantResident(_id, ResidentRestaurantTargetType.DiningTable, speakerSeatIndex, out var resident))
				{
					int num = ((resident.VoiceGender == ResidentVoiceGender.Female) ? 1 : 0);
					if (num < banks.Length && banks[num] != null)
					{
						return banks[num];
					}
				}
			}
			int num2 = UnityEngine.Random.Range(0, banks.Length);
			for (int i = 0; i < banks.Length; i++)
			{
				AudioClipSO audioClipSO = banks[(num2 + i) % banks.Length];
				if (audioClipSO != null)
				{
					return audioClipSO;
				}
			}
			return null;
		}

		private static AudioClip ResolveRestaurantAudioClip(AudioClipSO bank)
		{
			if (bank == null)
			{
				return null;
			}
			if (bank.Variants != null && bank.Variants.Length != 0)
			{
				int num = UnityEngine.Random.Range(0, bank.Variants.Length);
				for (int i = 0; i < bank.Variants.Length; i++)
				{
					AudioClip audioClip = bank.Variants[(num + i) % bank.Variants.Length];
					if (audioClip != null)
					{
						return audioClip;
					}
				}
			}
			return bank.Clip;
		}

		private AudioClip ResolveRestaurantTalkingClip(AudioClipSO bank)
		{
			if (bank == null)
			{
				return null;
			}
			AudioClip[] variants = bank.Variants;
			if (variants != null && variants.Length != 0)
			{
				int num = UnityEngine.Random.Range(0, variants.Length);
				for (int i = 0; i < variants.Length; i++)
				{
					AudioClip audioClip = variants[(num + i) % variants.Length];
					if (!(audioClip == null) && (!(audioClip == _lastRestaurantTalkingClip) || variants.Length <= 1))
					{
						_lastRestaurantTalkingClip = audioClip;
						return audioClip;
					}
				}
			}
			if (bank.Clip != null)
			{
				_lastRestaurantTalkingClip = bank.Clip;
			}
			return bank.Clip;
		}

		private static float ResolveRestaurantAudioVolume(AudioClipSO bank)
		{
			if (bank == null)
			{
				return 1f;
			}
			if (!bank.UseRandomVolume)
			{
				return Mathf.Clamp01(bank.Volume);
			}
			float num = Mathf.Clamp01(bank.RandomVolumeRange.x);
			float num2 = Mathf.Clamp01(bank.RandomVolumeRange.y);
			if (num2 < num)
			{
				float num3 = num2;
				float num4 = num;
				num = num3;
				num2 = num4;
			}
			return Mathf.Clamp01(bank.Volume * UnityEngine.Random.Range(num, num2));
		}

		private static void ConfigureRestaurantAudioSource(AudioSource source, AudioClipSO bank)
		{
			source.enabled = true;
			source.outputAudioMixerGroup = bank.MixerGroup;
			source.playOnAwake = false;
			source.loop = false;
			source.spatialBlend = (bank.Is3D ? Mathf.Clamp01(bank.SpatialBlend) : 0f);
			source.minDistance = Mathf.Max(0f, bank.MinDistance);
			source.maxDistance = Mathf.Max(source.minDistance, bank.HearDistance);
			source.rolloffMode = bank.RolloffMode;
			source.dopplerLevel = Mathf.Max(0f, bank.DopplerLevel);
			if (bank.UseRandomPitch)
			{
				float num = bank.RandomPitchRange.x;
				float num2 = bank.RandomPitchRange.y;
				if (num2 < num)
				{
					float num3 = num2;
					float num4 = num;
					num = num3;
					num2 = num4;
				}
				source.pitch = UnityEngine.Random.Range(num, num2);
			}
			else
			{
				source.pitch = 1f;
			}
		}

		public void ServerResetTable()
		{
			ServerResetForEndDay();
		}

		public void ServerResetForEndDay()
		{
			if (base.IsServer)
			{
				ServerInvalidateDelayedActions();
				ServerRemoveActiveInvoices();
				ServerReleaseTrackedTraysForReset();
				ServerClearDeliveredTrayRecords();
				ServerRestoreAllDeliveredSeatVisuals();
				_seatOrders.Clear();
				_residentNames.Clear();
				_servingSteamIdBySeat.Clear();
				_completedOrderCount = 0;
				_completedCustomerCount = 0;
				_cleaningRequiredTrayCount = 0;
				_cleanedTrayCount = 0;
				_totalPayment = 0;
				_totalPaymentFinal = 0;
				_restaurantHiPlayedForGroup = false;
				_isTableNeedsToClean = false;
				_cleaningBecauseCustomersLeft = false;
				_customersLeavingBecauseOfTimeout = false;
				_trays.Clear();
				if (_currentState.Value != RestaurantTableState.Empty)
				{
					ServerChangeState(RestaurantTableState.Empty);
				}
				ServerSendClientResetTable();
			}
		}

		private void ServerReleaseTrackedTraysForReset()
		{
			int num = 0;
			foreach (Tray tray in _trays)
			{
				if (!(tray == null) && !(tray.NetworkObject == null) && tray.NetworkObject.IsSpawned)
				{
					tray.ServerResetAfterRestaurantTableRelease();
					num++;
				}
			}
			if (num > 0)
			{
				UtilityMethods.Log($"[RestaurantTable] End-day reset released tracked trays. TableId={_id}, Count={num}");
			}
		}

		private void ApplyClientResetTable()
		{
			StopRestaurantTableAudio(includeOneShots: true);
			_seatOrders.Clear();
			SetAllSeatAvailableObjectsActive(isActive: true);
			_completedOrderCount = 0;
			_completedCustomerCount = 0;
			_cleaningRequiredTrayCount = 0;
			_cleanedTrayCount = 0;
			_totalPayment = 0;
			_totalPaymentFinal = 0;
			_isTableNeedsToClean = false;
			_cleaningBecauseCustomersLeft = false;
			_customersLeavingBecauseOfTimeout = false;
			if (_debugText != null)
			{
				_debugText.text = "State: Empty";
			}
			if (_moneyGraphPrefab != null)
			{
				_moneyGraphPrefab.SetActive(value: false);
			}
			RefreshGazeFeedback();
		}

		public void ServerSendClientResetTable()
		{
			if (base.IsServer)
			{
				ClientResetTableRevisionRpc(BootstrapLiveUpdateBuffer.NextServerRevision());
			}
		}

		[Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
		private void ClientResetTableRevisionRpc(ulong worldRevision)
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
					InvokePermission = RpcInvokePermission.Server
				};
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(831261193u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				BytePacker.WriteValueBitPacked(bufferWriter, worldRevision);
				__endSendRpc(ref bufferWriter, 831261193u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (!BootstrapLiveUpdateBuffer.TryDefer(this, 0, (RestaurantTable target, int data) =>
				{
					target.ApplyClientResetTable();
				}, 4096, worldRevision))
				{
					ApplyClientResetTable();
				}
			}
		}

		public override void EnableGazeHints()
		{
			Singleton<HintsManager>.Instance.ClearAllHints();
			if (IsPhysicallyBrokenSafe())
			{
				Singleton<HintsManager>.Instance.AddHint("Interact", IsPermanentlyUnlockedLocal() ? "key_open" : "key_repair");
				return;
			}
			if (CanClientTryDisableTable())
			{
				Singleton<HintsManager>.Instance.AddHint("Interact", "key_close");
				return;
			}
			switch (_currentState.Value)
			{
			case RestaurantTableState.TakingOrder:
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_take_order");
				break;
			case RestaurantTableState.DeliveringOrder:
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_deliver_order");
				if (_takeBackableTrayCount.Value > 0)
				{
					Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_take");
				}
				break;
			case RestaurantTableState.AwaitingPayment:
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_collect_payment");
				break;
			case RestaurantTableState.Cleaning:
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_clean_table");
				break;
			case RestaurantTableState.Empty:
			case RestaurantTableState.Eating:
				break;
			}
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("Interact");
			Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
			Singleton<HintsManager>.Instance.RemoveHint("RightMouseMechanic");
		}

		private void HandleDeliveredMainDishQuest(Tray tray)
		{
			if (!base.IsServer || tray == null)
			{
				return;
			}
			if (!tray.TryGetMainDishId(out var mainDishId))
			{
				UtilityMethods.Log("[RestaurantTable] Delivered tray has no valid main dish id.", LogType.Warning);
				return;
			}
			ItemDatabaseSO itemDatabaseSO = Singleton<GameProgressManager>.Instance.ItemDatabaseSO;
			if (!(itemDatabaseSO == null))
			{
				if (mainDishId == itemDatabaseSO.CucumberMakiServeredDishSO.Id)
				{
					QuestReferenceHub.Instance.AddProgress(QuestId.ServeCucumberMaki);
				}
				else if (mainDishId == itemDatabaseSO.MackarelNigiriServeredDishSO.Id)
				{
					QuestReferenceHub.Instance.AddProgress(QuestId.ServeMackerelNigiri);
				}
				else if (mainDishId == itemDatabaseSO.ChinookSalmonTemakiServeredDishSO.Id)
				{
					QuestReferenceHub.Instance.AddProgress(QuestId.ServeChinookSalmonDish);
				}
				else if (mainDishId == itemDatabaseSO.SquidNigiriServeredDishSO.Id)
				{
					QuestReferenceHub.Instance.AddProgress(QuestId.ServeSquidNigiri);
				}
			}
		}

		private void HandleDeliveredSideDishQuest(CustomerOrderData order, DeliveryEvaluation eval)
		{
			if (base.IsServer && order != null && !(order.SideDishSO == null) && (eval.Issues & OrderIssueFlags.SideMissing) == 0)
			{
				QuestReferenceHub.Instance.AddProgress(QuestId.ServeSideDish);
			}
		}

		public TableSnapshotPacked ServerBuildSnapshotPacked()
		{
			TableSnapshotPacked result = new TableSnapshotPacked
			{
				TableId = (byte)_id,
				CurrentState = (byte)_currentState.Value
			};
			int num = _seatedCount.Value;
			if (num < 0)
			{
				num = _lateJoinLastSeatedCount;
			}
			num = Mathf.Clamp(num, 0, _seatCount);
			result.SeatedCount = (byte)num;
			result.ArrivedCustomerCount = (byte)Mathf.Clamp(_completedCustomerCount, 0, num);
			TableSnapFlags tableSnapFlags = TableSnapFlags.None;
			if (_isTableNeedsToClean)
			{
				tableSnapFlags |= TableSnapFlags.NeedsClean;
			}
			result.Flags = tableSnapFlags;
			result.TakingOrderSeed = _lateJoinTakingOrderSeed;
			result.TotalPaymentFinal = _totalPaymentFinal;
			result.DeliveredSeatMask = ServerBuildDeliveredSeatMask();
			result.Seat0 = BuildSeatPacked(0);
			result.Seat1 = BuildSeatPacked(1);
			result.Seat2 = BuildSeatPacked(2);
			result.Seat3 = BuildSeatPacked(3);
			return result;
		}

		private SeatOrderPacked BuildSeatPacked(int seatIndex)
		{
			SeatOrderPacked result = default;
			if (_seatOrders == null || seatIndex < 0 || seatIndex >= _seatOrders.Count)
			{
				return result;
			}
			CustomerSeatOrder customerSeatOrder = _seatOrders[seatIndex];
			if (customerSeatOrder.Order == null)
			{
				return result;
			}
			result.MainId = PackId64(customerSeatOrder.Order.MainDishIdentityId);
			result.SideId = PackId64(GetUniqueId(customerSeatOrder.Order.SideDishSO));
			result.BevId = PackId64(GetUniqueId(customerSeatOrder.Order.BeverageSO));
			return result;
		}

		private static string GetUniqueId(ServedDishSO so)
		{
			if (so == null)
			{
				return null;
			}
			return so.Id;
		}

		public void ClientResetForBootstrap()
		{
			if (!base.IsServer)
			{
				ApplyClientResetTable();
				_lateJoinTakingOrderSeed = 0;
				_lateJoinLastSeatedCount = 0;
				_residentNames.Clear();
			}
		}

		public void ClientApplySnapshotPacked(in TableSnapshotPacked s)
		{
			_lateJoinTakingOrderSeed = s.TakingOrderSeed;
			_lateJoinLastSeatedCount = s.SeatedCount;
			_totalPaymentFinal = s.TotalPaymentFinal;
			_totalPayment = _totalPaymentFinal;
			_isTableNeedsToClean = (s.Flags & TableSnapFlags.NeedsClean) != 0;
			_completedOrderCount = 0;
			_completedCustomerCount = Mathf.Clamp(s.ArrivedCustomerCount, 0, _seatCount);
			_residentNames.Clear();
			EnsureSeatOrdersCapacity(_seatCount);
			ApplySeatToRuntime(0, in s.Seat0);
			ApplySeatToRuntime(1, in s.Seat1);
			ApplySeatToRuntime(2, in s.Seat2);
			ApplySeatToRuntime(3, in s.Seat3);
			RestaurantTableState currentState = (RestaurantTableState)s.CurrentState;
			int num = Mathf.Clamp(s.SeatedCount, 0, _seatCount);
			int arrivedCustomerCount = Mathf.Clamp(s.ArrivedCustomerCount, 0, num);
			ClientLateJoinApplyVisualState(currentState, num, arrivedCustomerCount);
			ApplySeatAvailableObjectMask(s.DeliveredSeatMask);
		}

		private void ApplyTotalPaymentFinal(int totalPaymentFinal)
		{
			_totalPaymentFinal = (_totalPayment = Mathf.Max(0, totalPaymentFinal));
			EventBus<RestaurantTablePaymentChangedEvent>.Raise(new RestaurantTablePaymentChangedEvent
			{
				TableId = _id,
				TotalPaymentFinal = _totalPaymentFinal
			});
		}

		private static bool ShouldResetPaymentForState(RestaurantTableState state)
		{
			if (state != RestaurantTableState.Empty && state != RestaurantTableState.WaitingOtherCustomers)
			{
				return state == RestaurantTableState.Cleaning;
			}
			return true;
		}

		private void EnsureSeatOrdersCapacity(int capacity)
		{
			if (_seatOrders == null)
			{
				_seatOrders = new List<CustomerSeatOrder>(capacity);
			}
			while (_seatOrders.Count < capacity)
			{
				_seatOrders.Add(new CustomerSeatOrder
				{
					SeatIndex = _seatOrders.Count,
					Order = null,
					OrderId = -1
				});
			}
		}

		private void ApplySeatToRuntime(int seatIndex, in SeatOrderPacked packed)
		{
			ApplySeatToRuntime(seatIndex, packed.MainId, packed.SideId, packed.BevId);
		}

		private void ApplySeatToRuntime(int seatIndex, FixedString64Bytes mainId, FixedString64Bytes sideId, FixedString64Bytes beverageId)
		{
			if (_seatOrders == null)
			{
				_seatOrders = new List<CustomerSeatOrder>(_seatCount);
			}
			EnsureSeatOrdersCapacity(_seatCount);
			if (seatIndex < 0 || seatIndex >= _seatOrders.Count)
			{
				return;
			}
			if (mainId.Length <= 0)
			{
				CustomerSeatOrder customerSeatOrder = _seatOrders[seatIndex];
				customerSeatOrder.SeatIndex = seatIndex;
				customerSeatOrder.Order = null;
				customerSeatOrder.OrderId = -1;
				_seatOrders[seatIndex] = customerSeatOrder;
				return;
			}
			CustomSushiRecipeCatalog customCatalog = Singleton<GameProgressManager>.Instance?.RestaurantManager?.CustomSushiRecipeCatalog;
			new SushiRecipeResolver(_itemDatabase, customCatalog).TryResolveById(mainId.ToString(), out var definition);
			ServedDishSO sideDishSO = ((sideId.Length > 0) ? ResolveAs<ServedDishSO>(_itemDatabase, sideId) : null);
			ServedDishSO beverageSO = ((beverageId.Length > 0) ? ResolveAs<ServedDishSO>(_itemDatabase, beverageId) : null);
			if (definition == null)
			{
				UtilityMethods.Log($"[RestaurantTable] ApplySeatToRuntime failed. Main dish not found. Seat={seatIndex}, MainId={mainId}", LogType.Warning);
				CustomerSeatOrder customerSeatOrder2 = _seatOrders[seatIndex];
				customerSeatOrder2.SeatIndex = seatIndex;
				customerSeatOrder2.Order = null;
				customerSeatOrder2.OrderId = -1;
				_seatOrders[seatIndex] = customerSeatOrder2;
			}
			else
			{
				CustomerOrderData order = new CustomerOrderData
				{
					MainDishSO = definition.BuiltInSushi,
					MainDishRecipe = definition,
					MainDishRecipeId = definition.RecipeId,
					SideDishSO = sideDishSO,
					BeverageSO = beverageSO
				};
				CustomerSeatOrder customerSeatOrder3 = _seatOrders[seatIndex];
				customerSeatOrder3.SeatIndex = seatIndex;
				customerSeatOrder3.Order = order;
				customerSeatOrder3.OrderId = -1;
				_seatOrders[seatIndex] = customerSeatOrder3;
			}
		}

		private static T ResolveAs<T>(ItemDatabaseSO db, FixedString64Bytes id) where T : ItemSO
		{
			if (db == null || id.Length == 0)
			{
				return null;
			}
			ItemSO itemSO = db.FindById(id.ToString());
			if (itemSO == null)
			{
				return null;
			}
			return itemSO as T;
		}

		private void ClientLateJoinApplyVisualState(RestaurantTableState state, int seatedCount, int arrivedCustomerCount)
		{
			seatedCount = Mathf.Clamp(seatedCount, 0, _seatCount);
			arrivedCustomerCount = Mathf.Clamp(arrivedCustomerCount, 0, seatedCount);
			if ((bool)_moneyGraphPrefab)
			{
				_moneyGraphPrefab.SetActive(value: false);
			}
			SetAllSeatAvailableObjectsActive(isActive: true);
			switch (state)
			{
			case RestaurantTableState.Empty:
				if ((bool)_debugText)
				{
					_debugText.text = "State: Empty";
				}
				_seatOrders.Clear();
				_totalPayment = 0;
				_totalPaymentFinal = 0;
				_isTableNeedsToClean = false;
				break;
			case RestaurantTableState.WaitingOtherCustomers:
				if ((bool)_debugText)
				{
					_debugText.text = "State: WaitingOtherCustomers";
				}
				break;
			case RestaurantTableState.TakingOrder:
				if ((bool)_debugText)
				{
					_debugText.text = "State: Taking Order";
				}
				break;
			case RestaurantTableState.DeliveringOrder:
				if ((bool)_debugText)
				{
					_debugText.text = "State: DeliveringOrder";
				}
				break;
			case RestaurantTableState.Eating:
				if ((bool)_debugText)
				{
					_debugText.text = "State: Eating";
				}
				break;
			case RestaurantTableState.AwaitingPayment:
				if ((bool)_debugText)
				{
					_debugText.text = "State: Awaiting Payment";
				}
				if ((bool)_moneyGraphPrefab)
				{
					_moneyGraphPrefab.SetActive(value: true);
				}
				break;
			case RestaurantTableState.Cleaning:
				if ((bool)_debugText)
				{
					_debugText.text = "State: Cleaning";
				}
				break;
			default:
				if ((bool)_debugText)
				{
					_debugText.text = $"State: {state}";
				}
				break;
			}
			EventBus<RestaurantTableStateChangeEvent>.Raise(new RestaurantTableStateChangeEvent
			{
				NewState = state,
				TableId = _id
			});
			if (state == RestaurantTableState.WaitingOtherCustomers)
			{
				for (int i = 0; i < arrivedCustomerCount; i++)
				{
					RaiseCustomerMenuWaitingVisual(i, isWaiting: true);
				}
			}
		}

		private void DEBUG_RepairTable()
		{
			if (CanRunServerDebug("DEBUG_RepairTable"))
			{
				TryRepairPhysicalState();
				DEBUG_PrintRuntimeState();
			}
		}

		private void DEBUG_ResetTableFull()
		{
			if (CanRunServerDebug("DEBUG_ResetTableFull"))
			{
				ServerResetTable();
				DEBUG_PrintRuntimeState();
			}
		}

		private void DEBUG_StartFakeDiningGroup()
		{
			if (CanRunServerDebug("DEBUG_StartFakeDiningGroup"))
			{
				if (_debugRepairIfNeeded && IsPhysicallyBrokenSafe())
				{
					TryRepairPhysicalState();
				}
				ServerInvalidateDelayedActions();
				_seatOrders.Clear();
				_residentNames.Clear();
				_trays.Clear();
				_servingSteamIdBySeat.Clear();
				_completedOrderCount = 0;
				_completedCustomerCount = 0;
				_totalPayment = 0;
				_totalPaymentFinal = 0;
				_isTableNeedsToClean = false;
				int num = Mathf.Clamp(_debugDiningCustomerCount, 1, Mathf.Max(1, _seatCount));
				_seatedCount.Value = num;
				_lateJoinLastSeatedCount = num;
				ServerChangeState(RestaurantTableState.WaitingOtherCustomers);
				if (_debugAutoArriveAllCustomers)
				{
					DEBUG_ArriveAllFakeCustomers();
				}
				DEBUG_PrintRuntimeState();
			}
		}

		private void DEBUG_ArriveFakeCustomer()
		{
			if (CanRunServerDebug("DEBUG_ArriveFakeCustomer"))
			{
				if (_seatedCount.Value <= 0)
				{
					int num = Mathf.Clamp(_debugDiningCustomerCount, 1, Mathf.Max(1, _seatCount));
					_seatedCount.Value = num;
					_lateJoinLastSeatedCount = num;
				}
				if (_currentState.Value != RestaurantTableState.WaitingOtherCustomers)
				{
					ServerChangeState(RestaurantTableState.WaitingOtherCustomers);
				}
				int num2 = Mathf.Clamp(_completedCustomerCount, 0, Mathf.Max(0, _seatedCount.Value - 1));
				string residentName = $"DEBUG_Customer_{_id}_{num2 + 1}";
				OnServerCustomerArrived(num2, residentName);
				DEBUG_PrintRuntimeState();
			}
		}

		private void DEBUG_ArriveAllFakeCustomers()
		{
			if (CanRunServerDebug("DEBUG_ArriveAllFakeCustomers"))
			{
				if (_seatedCount.Value <= 0)
				{
					int num = Mathf.Clamp(_debugDiningCustomerCount, 1, Mathf.Max(1, _seatCount));
					_seatedCount.Value = num;
					_lateJoinLastSeatedCount = num;
				}
				if (_currentState.Value != RestaurantTableState.WaitingOtherCustomers)
				{
					ServerChangeState(RestaurantTableState.WaitingOtherCustomers);
				}
				int num2 = Mathf.Clamp(_seatedCount.Value, 1, Mathf.Max(1, _seatCount));
				int num3 = 0;
				while (_currentState.Value == RestaurantTableState.WaitingOtherCustomers && num3 < num2)
				{
					int num4 = Mathf.Clamp(_completedCustomerCount, 0, num2 - 1);
					string residentName = $"DEBUG_Customer_{_id}_{num4 + 1}";
					OnServerCustomerArrived(num4, residentName);
					num3++;
				}
				DEBUG_PrintRuntimeState();
			}
		}

		private void DEBUG_RunRealPartialArrivalRecovery()
		{
			if (!CanRunServerDebug("DEBUG_RunRealPartialArrivalRecovery"))
			{
				return;
			}
			bool flag = _currentState.Value == RestaurantTableState.WaitingOtherCustomers;
			if (_currentState.Value != RestaurantTableState.Empty && !flag)
			{
				UtilityMethods.Log($"[RestaurantTable DEBUG] Partial recovery scenario requires an Empty or WaitingOtherCustomers table. TableId={_id}, State={_currentState.Value}", LogType.Warning);
				return;
			}
			ResidentManager instance = Singleton<ResidentManager>.Instance;
			if (instance == null)
			{
				UtilityMethods.Log("[RestaurantTable DEBUG] ResidentManager not found.", LogType.Error);
				return;
			}
			int num2;
			int num;
			if (flag)
			{
				num = Mathf.Clamp(_seatedCount.Value, 0, _seatCount);
				num2 = 0;
				List<ResidentController> residents = instance.Residents;
				for (int i = 0; i < residents.Count; i++)
				{
					ResidentRuntimeState residentRuntimeState = ((residents[i] != null) ? residents[i].Runtime : null);
					if (residentRuntimeState != null && residentRuntimeState.RestaurantTargetType == ResidentRestaurantTargetType.DiningTable && residentRuntimeState.RestaurantTableId == _id)
					{
						num2++;
					}
				}
			}
			else
			{
				RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
				if (restaurantManager == null || !restaurantManager.IsRestaurantOpen)
				{
					UtilityMethods.Log("[RestaurantTable DEBUG] Open the restaurant before starting the partial recovery scenario.", LogType.Error);
					return;
				}
				num = Mathf.Clamp(3, 1, Mathf.Max(1, _seatCount));
				_residentNames.Clear();
				_completedCustomerCount = 0;
				_seatedCount.Value = num;
				_lateJoinLastSeatedCount = num;
				ServerChangeState(RestaurantTableState.WaitingOtherCustomers);
				num2 = instance.CallResidentsForRestaurant(num, _id);
				_seatedCount.Value = num2;
				_lateJoinLastSeatedCount = num2;
				num = num2;
			}
			if (num2 < 3 || num2 != num)
			{
				UtilityMethods.Log($"[RestaurantTable DEBUG] Partial recovery scenario needs a complete pending group of at least three residents. Expected={num}, Assigned={num2}", LogType.Error);
				if (!flag)
				{
					ServerChangeState(RestaurantTableState.Empty);
				}
				return;
			}
			ResidentController residentController = null;
			int num3 = -1;
			List<ResidentController> residents2 = instance.Residents;
			for (int j = 0; j < residents2.Count; j++)
			{
				ResidentController residentController2 = residents2[j];
				ResidentRuntimeState residentRuntimeState2 = ((residentController2 != null) ? residentController2.Runtime : null);
				if (residentRuntimeState2 != null && residentRuntimeState2.RestaurantTargetType == ResidentRestaurantTargetType.DiningTable && residentRuntimeState2.RestaurantTableId == _id && !residentRuntimeState2.HasArrived && residentRuntimeState2.RestaurantSeatIndex > num3)
				{
					num3 = residentRuntimeState2.RestaurantSeatIndex;
					residentController = residentController2;
				}
			}
			if (residentController == null)
			{
				UtilityMethods.Log("[RestaurantTable DEBUG] Could not select the simulated missing resident.", LogType.Error);
				if (!flag)
				{
					ServerChangeState(RestaurantTableState.Empty);
				}
				return;
			}
			string residentName = residentController.ResidentName;
			residentController.ForceLeaveRestaurantAssignment(_id, ResidentRestaurantTargetType.DiningTable);
			_nextWaitingCustomerRetryServerTime = double.MaxValue;
			UtilityMethods.Log($"[RestaurantTable DEBUG] Partial recovery scenario started. TableId={_id}, Assigned={num2}, SimulatedMissing={residentName}, Seat={num3}");
			StartCoroutine(DEBUG_WaitForPartialArrivalsAndForceTimeout(num - 1));
		}

		private IEnumerator DEBUG_WaitForPartialArrivalsAndForceTimeout(int expectedSurvivorCount)
		{
			float deadline = Time.realtimeSinceStartup + 60f;
			while (_currentState.Value == RestaurantTableState.WaitingOtherCustomers && _completedCustomerCount < expectedSurvivorCount && Time.realtimeSinceStartup < deadline)
			{
				yield return null;
			}
			if (_currentState.Value != RestaurantTableState.WaitingOtherCustomers)
			{
				UtilityMethods.Log($"[RestaurantTable DEBUG] Partial recovery scenario stopped because table state changed to {_currentState.Value}.", LogType.Warning);
				yield break;
			}
			int completedCustomerCount = _completedCustomerCount;
			_waitingCustomersStartedServerTime = GetServerTimeSafe() - (double)Mathf.Max(10f, _waitingCustomerTimeoutSeconds) - 1.0;
			ServerTickWaitingCustomerRecovery();
			bool flag = completedCustomerCount > 0 && _currentState.Value == RestaurantTableState.TakingOrder && _seatedCount.Value == completedCustomerCount && GetActiveSeatOrderCount() == completedCustomerCount;
			UtilityMethods.Log("[RestaurantTable DEBUG] Partial recovery scenario " + (flag ? "PASSED" : "FAILED") + ". " + $"TableId={_id}, ExpectedSurvivors={expectedSurvivorCount}, Arrived={completedCustomerCount}, " + $"State={_currentState.Value}, Seated={_seatedCount.Value}, Orders={GetActiveSeatOrderCount()}", flag ? LogType.Log : LogType.Error);
			DEBUG_PrintRuntimeState();
		}

		private void DEBUG_ForceTargetState()
		{
			if (CanRunServerDebug("DEBUG_ForceTargetState"))
			{
				ServerChangeState(_debugTargetState);
				DEBUG_PrintRuntimeState();
			}
		}

		private void DEBUG_SkipEatingToPayment()
		{
			if (CanRunServerDebug("DEBUG_SkipEatingToPayment"))
			{
				ServerChangeState(RestaurantTableState.AwaitingPayment);
				DEBUG_PrintRuntimeState();
			}
		}

		private void DEBUG_PrintRuntimeState()
		{
			int num = 0;
			if (_seatOrders != null)
			{
				for (int i = 0; i < _seatOrders.Count; i++)
				{
					if (_seatOrders[i] != null && _seatOrders[i].Order != null)
					{
						num++;
					}
				}
			}
			string text = ((_residentNames == null || _residentNames.Count == 0) ? "-" : string.Join(", ", _residentNames));
			UtilityMethods.Log("<b><color=#7BDFF2>[RestaurantTable DEBUG]</color></b>\n" + $"  TableId              : {_id}\n" + $"  State                : {_currentState.Value}\n" + $"  StateVersion         : {_stateVersion}\n" + $"  SeatCount            : {_seatCount}\n" + $"  SeatedCount          : {_seatedCount.Value}\n" + $"  ArrivedCustomers     : {_completedCustomerCount}\n" + $"  CompletedOrders      : {_completedOrderCount}\n" + $"  CleaningRequired     : {_cleaningRequiredTrayCount}\n" + $"  CleanedTrays         : {_cleanedTrayCount}\n" + $"  ActiveOrders         : {num}\n" + $"  TrayStackCount       : {_trays.Count}\n" + $"  TotalPayment         : {_totalPayment}\n" + $"  TotalPaymentFinal    : {_totalPaymentFinal}\n" + $"  NeedsClean           : {_isTableNeedsToClean}\n" + $"  PhysicalUsable       : {IsPhysicallyUsableSafe()}\n" + "  Residents            : " + text);
		}

		public bool ServerStaffOrderValid(int orderId)
		{
			return ServerStaffOrders.Any((CustomerSeatOrder s) => s.OrderId == orderId);
		}

		public bool ServerStaffTakeOrder(string employeeName)
		{
			return ServerStaffTakeOrderInternal(employeeName, StaffGender.Male);
		}

		public bool ServerStaffTakeOrder(StaffEmployeeEntry employee)
		{
			return ServerStaffTakeOrderInternal(employee.Name.ToString(), employee.Gender);
		}

		private bool ServerStaffTakeOrderInternal(string employeeName, StaffGender gender)
		{
			if (!base.IsServer || !base.IsSpawned || CurrentState != RestaurantTableState.TakingOrder || HasTakeOrderDeadlineExpired())
			{
				return false;
			}
			if (!HasCompleteSeatOrderSet(Mathf.Clamp(_seatedCount.Value, 0, _seatCount), GetActiveSeatOrderCount()) && !ServerGenerateSeatOrders())
			{
				return false;
			}
			_deliverOrderExpireServerTime = GetServerTimeSafe() + (double)Mathf.Max(1f, _deliverOrderTimeoutSeconds);
			if (!ServerCreateInvoicesForAllCustomers(employeeName, _deliverOrderExpireServerTime, isStaffWaitress: true, gender))
			{
				_deliverOrderExpireServerTime = 0.0;
				return false;
			}
			Feedback.ServerOrderTaken(this, _id, StaffNames.LocalizedArgument(employeeName, gender));
			ServerSendClientPlayRestaurantMoment(0, 1, 0);
			ServerChangeState(RestaurantTableState.DeliveringOrder);
			StaffQuestProgress.Complete(QuestId.TakeTheCustomersOrder);
			return true;
		}

		public bool ServerStaffDeliver(int employeeId, string employeeName, int orderId, Tray tray)
		{
			if (!base.IsServer || !base.IsSpawned || tray == null || !tray.ServerStaffCarriedBy(employeeId) || tray.IsDirty.Value)
			{
				return false;
			}
			int num = _seatOrders.FindIndex((CustomerSeatOrder s) => s != null && s.OrderId == orderId && s.Order != null);
			if (!ServerStaffOrderValid(orderId) || num < 0 || _trayPointsParent == null || num >= _trayPointsParent.childCount)
			{
				return false;
			}
			CustomerSeatOrder customerSeatOrder = _seatOrders[num];
			if (!tray.ServerStaffMatches(customerSeatOrder.Order))
			{
				return false;
			}
			DeliveryEvaluation eval = tray.EvaluateDelivery(customerSeatOrder.Order);
			Transform child = _trayPointsParent.GetChild(num);
			if (!tray.ServerStaffPlace(employeeId, child.position, child.rotation, onTable: true))
			{
				return false;
			}
			if (QuestReferenceHub.Instance != null)
			{
				HandleDeliveredMainDishQuest(tray);
				HandleDeliveredSideDishQuest(customerSeatOrder.Order, eval);
			}
			_trays.Add(tray);
			ServerTrackDeliveredSeatVisual(tray, num);
			ServiceLocator.ForSceneOf(this).Get<TabletNetworkHandler>(out var service);
			CustomerOrderFoodPayload deliveredPayload = BuildDeliveredPayloadFromSeatOrder(customerSeatOrder.Order, eval);
			bool num2 = service != null && service.ServerRegisterDeliveredFoods(orderId, deliveredPayload);
			_servingSteamIdBySeat[num] = 0uL;
			_totalPayment += Mathf.Max(0, eval.Earned);
			_totalPaymentFinal = _totalPayment;
			if (num2)
			{
				_deliveredTrayRecords.Add(new DeliveredTrayRecord
				{
					Tray = tray,
					SeatIndex = num,
					Order = customerSeatOrder.Order,
					OrderId = orderId,
					EarnedPayment = Mathf.Max(0, eval.Earned),
					DeliveredPayload = deliveredPayload
				});
				ServerRefreshTakeBackableTrayCount();
			}
			Feedback.ServerOrderServed(this, _id, num, eval.Issues);
			ServerSendClientPlayRestaurantMoment(num, 2, 0);
			ServerSendClientDeliveryFeedback(employeeName, num, (byte)eval.Result, (ushort)eval.Issues, eval.Penalty, eval.Earned, _totalPaymentFinal);
			customerSeatOrder.Order = null;
			customerSeatOrder.OrderId = -1;
			ServerGrantGroupServiceGrace(num);
			_completedOrderCount++;
			if (_seatOrders.All((CustomerSeatOrder s) => s == null || s.Order == null))
			{
				_completedOrderCount = 0;
				service?.ServerRemoveOrdersForTable(_id.ToString());
				ServerChangeState(RestaurantTableState.Eating);
			}
			return true;
		}

		public bool ServerStaffCollectPayment(StaffEmployeeEntry employee)
		{
			if (!base.IsServer || !base.IsSpawned || CurrentState != RestaurantTableState.AwaitingPayment)
			{
				return false;
			}
			int num = Mathf.Max(0, _totalPaymentFinal);
			if (!StaffServiceGameplayApi.Earn(this, num))
			{
				return false;
			}
			_totalPayment = 0;
			_totalPaymentFinal = 0;
			Feedback.ServerPaymentCollected(this, _id, StaffNames.LocalizedArgument(employee), num);
			ServerSendClientPlayRestaurantMoment(0, 5, 0);
			ServerChangeState(RestaurantTableState.Cleaning);
			StaffQuestProgress.Complete(QuestId.CollectPayment);
			StaffServiceGameplayApi.Record(employee, 0, 1, 0, _id.ToString());
			return true;
		}

		public Tray ServerStaffTakeDirtyTray(int employeeId)
		{
			if (!base.IsServer || !base.IsSpawned || CurrentState != RestaurantTableState.Cleaning)
			{
				return null;
			}
			_trays.RemoveAll((Tray t) => t == null || !t.IsSpawned);
			Tray tray = _trays.LastOrDefault();
			if (tray == null)
			{
				ServerCompleteCleaning();
				return null;
			}
			tray.CanHold.Value = true;
			tray.CanGaze.Value = true;
			if (!tray.ServerStaffTryPickup(employeeId))
			{
				tray.CanHold.Value = false;
				tray.CanGaze.Value = false;
				return null;
			}
			_trays.Remove(tray);
			ServerRestoreDeliveredSeatVisualForTray(tray);
			_cleanedTrayCount++;
			StaffQuestProgress.Complete(QuestId.PickUpTheDirtyTray);
			if (_cleanedTrayCount >= Mathf.Max(1, _cleaningRequiredTrayCount) || _trays.Count == 0)
			{
				ServerCompleteCleaning();
			}
			return tray;
		}

		protected override void __initializeVariables()
		{
			if (_currentState == null)
			{
				throw new Exception("RestaurantTable._currentState cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_currentState.Initialize(this);
			__nameNetworkVariable(_currentState, "_currentState");
			NetworkVariableFields.Add(_currentState);
			if (_seatedCount == null)
			{
				throw new Exception("RestaurantTable._seatedCount cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_seatedCount.Initialize(this);
			__nameNetworkVariable(_seatedCount, "_seatedCount");
			NetworkVariableFields.Add(_seatedCount);
			if (_activeOrderExpireServerTime == null)
			{
				throw new Exception("RestaurantTable._activeOrderExpireServerTime cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_activeOrderExpireServerTime.Initialize(this);
			__nameNetworkVariable(_activeOrderExpireServerTime, "_activeOrderExpireServerTime");
			NetworkVariableFields.Add(_activeOrderExpireServerTime);
			if (_takeBackableTrayCount == null)
			{
				throw new Exception("RestaurantTable._takeBackableTrayCount cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_takeBackableTrayCount.Initialize(this);
			__nameNetworkVariable(_takeBackableTrayCount, "_takeBackableTrayCount");
			NetworkVariableFields.Add(_takeBackableTrayCount);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(3513808002u, __rpc_handler_3513808002, "ClientSetSeatAvailableObjectActiveRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(148841439u, __rpc_handler_148841439, "ClientSetAllSeatAvailableObjectsActiveRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(49826291u, __rpc_handler_49826291, "ServerTryUnlockTableRpc", RpcInvokePermission.Everyone);
			__registerRpc(163596466u, __rpc_handler_163596466, "ServerTryDisableTableRpc", RpcInvokePermission.Everyone);
			__registerRpc(2403997508u, __rpc_handler_2403997508, "ClientShowProgressionLockedNotifyRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(964473992u, __rpc_handler_964473992, "ClientShowNotifyRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(78961431u, __rpc_handler_78961431, "ClientEmptyStateExitRpc", RpcInvokePermission.Everyone);
			__registerRpc(2062433882u, __rpc_handler_2062433882, "ClientEmptyStateEnterRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(944480883u, __rpc_handler_944480883, "ClientWaitingOtherCustomersExitRpc", RpcInvokePermission.Everyone);
			__registerRpc(3639903054u, __rpc_handler_3639903054, "ClientWaitingOtherCustomersEnterRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(1074990379u, __rpc_handler_1074990379, "ClientTakingOrderExitRpc", RpcInvokePermission.Everyone);
			__registerRpc(2266663196u, __rpc_handler_2266663196, "ServerConfirmOrderRpc", RpcInvokePermission.Everyone);
			__registerRpc(1247925531u, __rpc_handler_1247925531, "ClientSyncSeatOrdersRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(1078604966u, __rpc_handler_1078604966, "ClientTakingOrderEnterRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(1806025271u, __rpc_handler_1806025271, "ClientShowOrderReceivedFeedbackRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(3923445107u, __rpc_handler_3923445107, "ClientShowTableToastRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(1314408236u, __rpc_handler_1314408236, "ClientShowDeliveryRejectedToastRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(3864854892u, __rpc_handler_3864854892, "ClientShowTableToastToEveryoneRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(1801723160u, __rpc_handler_1801723160, "ServerDeliveringOrderInteractRpc", RpcInvokePermission.Everyone);
			__registerRpc(1525086334u, __rpc_handler_1525086334, "ClientDeliveringOrderEnterRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(992866641u, __rpc_handler_992866641, "ClientDeliveringOrderExitRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(1656241749u, __rpc_handler_1656241749, "ServerTakeBackDeliveredTrayRpc", RpcInvokePermission.Everyone);
			__registerRpc(3526569147u, __rpc_handler_3526569147, "ClientEatingExitRpc", RpcInvokePermission.Everyone);
			__registerRpc(948352604u, __rpc_handler_948352604, "ClientTakeBackFeedbackRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(3704567097u, __rpc_handler_3704567097, "ServerAwaitingPaymentInteractRpc", RpcInvokePermission.Everyone);
			__registerRpc(1892617651u, __rpc_handler_1892617651, "ClientDeliveryFeedbackRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(969474723u, __rpc_handler_969474723, "ClientCleaningExitRpc", RpcInvokePermission.Everyone);
			__registerRpc(1490711507u, __rpc_handler_1490711507, "ServerCleaningInteractRpc", RpcInvokePermission.Everyone);
			__registerRpc(1088754815u, __rpc_handler_1088754815, "ClientEatingEnterRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(240508425u, __rpc_handler_240508425, "ServerChangeSeatedCountRpc", RpcInvokePermission.Everyone);
			__registerRpc(3686861657u, __rpc_handler_3686861657, "ClientAwaitingPaymentEnterRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(2687671101u, __rpc_handler_2687671101, "ClientAwaitingPaymentExitRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(3379710945u, __rpc_handler_3379710945, "ClientPlayMoneyEarnedSfxRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(757846584u, __rpc_handler_757846584, "ClientCleaningEnterRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(866448535u, __rpc_handler_866448535, "ClientCustomerMenuWaitingVisualRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(792787721u, __rpc_handler_792787721, "ClientChangeStateRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(1823057485u, __rpc_handler_1823057485, "ClientRaiseVisualStateRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(1977094708u, __rpc_handler_1977094708, "ClientPlayRestaurantFeedbackCueRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(1585204484u, __rpc_handler_1585204484, "ClientPlayRestaurantMomentRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(3959231580u, __rpc_handler_3959231580, "ClientPlayRestaurantAudioRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(682076219u, __rpc_handler_682076219, "ClientPlayRestaurantHiThenStartAmbientRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(2649236778u, __rpc_handler_2649236778, "ClientStartRestaurantAmbientRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(3848177965u, __rpc_handler_3848177965, "ClientStopRestaurantAmbientRevisionRpc", RpcInvokePermission.Server);
			__registerRpc(831261193u, __rpc_handler_831261193, "ClientResetTableRevisionRpc", RpcInvokePermission.Server);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_3513808002(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientSetSeatAvailableObjectActiveRevisionRpc(value, value2, value3);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_148841439(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientSetAllSeatAvailableObjectsActiveRevisionRpc(value, value2);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_49826291(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ServerTryUnlockTableRpc(value, ext);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_163596466(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ServerTryDisableTableRpc(value, ext);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2403997508(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				RpcParams ext = rpcParams.Ext;
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientShowProgressionLockedNotifyRevisionRpc(value, ext);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_964473992(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				RpcParams ext = rpcParams.Ext;
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientShowNotifyRevisionRpc(value, ext);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_78961431(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientEmptyStateExitRpc();
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2062433882(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientEmptyStateEnterRevisionRpc(value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_944480883(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientWaitingOtherCustomersExitRpc();
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3639903054(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientWaitingOtherCustomersEnterRevisionRpc(value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1074990379(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientTakingOrderExitRpc();
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2266663196(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ServerConfirmOrderRpc(value, ext);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1247925531(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out SeatOrderPacked value2, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out SeatOrderPacked value3, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out SeatOrderPacked value4, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out SeatOrderPacked value5, default(FastBufferWriter.ForNetworkSerializable));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value6);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientSyncSeatOrdersRevisionRpc(value, value2, value3, value4, value5, value6);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1078604966(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientTakingOrderEnterRevisionRpc(value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1806025271(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
				RpcParams ext = rpcParams.Ext;
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientShowOrderReceivedFeedbackRevisionRpc(value, value2, ext);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3923445107(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
				reader.ReadValueSafe(out NotificationState value2, default(FastBufferWriter.ForEnums));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
				RpcParams ext = rpcParams.Ext;
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientShowTableToastRevisionRpc(s, value2, value3, ext);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1314408236(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
				RpcParams ext = rpcParams.Ext;
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientShowDeliveryRejectedToastRevisionRpc(s, value2, ext);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3864854892(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
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
				reader.ReadValueSafe(out NotificationState value2, default(FastBufferWriter.ForEnums));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientShowTableToastToEveryoneRevisionRpc(s, value2, value3);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1801723160(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ServerDeliveringOrderInteractRpc(value, ext);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1525086334(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientDeliveringOrderEnterRevisionRpc(value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_992866641(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientDeliveringOrderExitRevisionRpc(value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1656241749(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ServerTakeBackDeliveredTrayRpc(value, ext);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3526569147(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientEatingExitRpc();
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_948352604(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientTakeBackFeedbackRevisionRpc(value, value2, value3);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3704567097(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ServerAwaitingPaymentInteractRpc(value, ext);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1892617651(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out FixedString64Bytes value, default(FastBufferWriter.ForFixedStrings));
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				reader.ReadValueSafe(out byte value3, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out ushort value4);
				ByteUnpacker.ReadValueBitPacked(reader, out int value5);
				ByteUnpacker.ReadValueBitPacked(reader, out int value6);
				ByteUnpacker.ReadValueBitPacked(reader, out int value7);
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value8);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientDeliveryFeedbackRevisionRpc(value, value2, value3, value4, value5, value6, value7, value8);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_969474723(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientCleaningExitRpc();
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1490711507(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				reader.ReadValueSafe(out NetworkObjectReference value2, default(FastBufferWriter.ForNetworkSerializable));
				RpcParams ext = rpcParams.Ext;
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ServerCleaningInteractRpc(value, value2, ext);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1088754815(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientEatingEnterRevisionRpc(value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_240508425(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ServerChangeSeatedCountRpc(value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3686861657(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientAwaitingPaymentEnterRevisionRpc(value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2687671101(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientAwaitingPaymentExitRevisionRpc(value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3379710945(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				RpcParams ext = rpcParams.Ext;
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientPlayMoneyEarnedSfxRevisionRpc(value, ext);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_757846584(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientCleaningEnterRevisionRpc(value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_866448535(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				reader.ReadValueSafe(out bool value2, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientCustomerMenuWaitingVisualRevisionRpc(value, value2, value3);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_792787721(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientChangeStateRevisionRpc(value, value2);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1823057485(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientRaiseVisualStateRevisionRpc(value, value2);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1977094708(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientPlayRestaurantFeedbackCueRevisionRpc(value, value2);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1585204484(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				reader.ReadValueSafe(out byte value2, default(FastBufferWriter.ForPrimitives));
				reader.ReadValueSafe(out byte value3, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value4);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientPlayRestaurantMomentRevisionRpc(value, value2, value3, value4);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3959231580(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out int value2);
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value3);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientPlayRestaurantAudioRevisionRpc(value, value2, value3);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_682076219(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out int value);
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientPlayRestaurantHiThenStartAmbientRevisionRpc(value, value2);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_2649236778(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out byte value, default(FastBufferWriter.ForPrimitives));
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value2);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientStartRestaurantAmbientRevisionRpc(value, value2);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_3848177965(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientStopRestaurantAmbientRevisionRpc(value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_831261193(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				ByteUnpacker.ReadValueBitPacked(reader, out ulong value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((RestaurantTable)target).ClientResetTableRevisionRpc(value);
				((RestaurantTable)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "RestaurantTable";
		}
	}
}
