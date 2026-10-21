using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Data.Ai.NPCs;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public class ResidentManager : Singleton<ResidentManager>
	{
		[Serializable]
		public struct HouseBinding
		{
			public HouseDataSO HouseData;

			public Transform HouseTransform;
		}

		[Header("Houses")]
		[SerializeField]
		private List<HouseBinding> _houseBindings = new List<HouseBinding>();

		[Header("Patrol")]
		[SerializeField]
		private List<Transform> _seaSwimPoints = new List<Transform>();

		[SerializeField]
		private float _seaSwimPointSampleRadius = 8f;

		[SerializeField]
		private float _seaSwimTransitionRadius = 2.5f;

		[SerializeField]
		private float _seaSwimHeightBlendDuration = 0.75f;

		[SerializeField]
		private List<Transform> _parkWalkPoints = new List<Transform>();

		[SerializeField]
		private List<Transform> _cityPatrolNearPoints = new List<Transform>();

		[Header("Restaurant Call Teleport")]
		[SerializeField]
		private bool _useRestaurantCallTeleport = true;

		[SerializeField]
		private float _restaurantCallTeleportDistanceThreshold = 80f;

		[SerializeField]
		private List<ResidentActivityType> _restaurantCallTeleportActivities = new List<ResidentActivityType>
		{
			ResidentActivityType.PierFishing,
			ResidentActivityType.LighthouseIdle
		};

		[SerializeField]
		private List<Transform> _restaurantCallEntryPoints = new List<Transform>();

		[Header("Runtime")]
		[SerializeField]
		private List<ResidentController> _residents = new List<ResidentController>();

		private readonly Dictionary<byte, ResidentController> _residentsById = new Dictionary<byte, ResidentController>();

		private readonly Dictionary<byte, ResidentStateSnapshotPacked> _lastSentStateSnapshots = new Dictionary<byte, ResidentStateSnapshotPacked>();

		private readonly Dictionary<byte, ResidentMovementSnapshotPacked> _lastSentMovementSnapshots = new Dictionary<byte, ResidentMovementSnapshotPacked>();

		private readonly Dictionary<byte, ResidentStateSnapshotPacked> _pendingStateSnapshots = new Dictionary<byte, ResidentStateSnapshotPacked>();

		private readonly Dictionary<byte, ResidentMovementSnapshotPacked> _pendingMovementSnapshots = new Dictionary<byte, ResidentMovementSnapshotPacked>();

		private readonly List<ResidentStateSnapshotPacked> _stateSnapshotBuffer = new List<ResidentStateSnapshotPacked>();

		private readonly List<ResidentMovementSnapshotPacked> _movementSnapshotBuffer = new List<ResidentMovementSnapshotPacked>();

		private readonly Queue<ResidentController> _restaurantQueue = new Queue<ResidentController>();

		private readonly HashSet<ResidentController> _restaurantQueuedResidents = new HashSet<ResidentController>();

		private bool _isInitDone;

		private int _currentHour = 8;

		private int _currentMinute;

		private float _nextMovementSyncTime;

		private const float MovementSyncInterval = 0.2f;

		private const float MovementPositionThreshold = 0.05f;

		private const float MovementYawThreshold = 2f;

		public override SingletonScope Scope => SingletonScope.Scene;

		public List<ResidentController> Residents => _residents;

		public event Action<ResidentController> RuntimeResidentRegistered;

		private void Update()
		{
			if (IsLiveNetworkAuthority())
			{
				TryBroadcastResidentStateChanges();
				TryBroadcastResidentMovementSnapshots();
			}
		}

		public void Initialize()
		{
			if (!_isInitDone)
			{
				_isInitDone = true;
				_restaurantQueue.Clear();
				_restaurantQueuedResidents.Clear();
				if (CanSpawnResidents())
				{
					SpawnHouseResidents();
				}
				for (int i = 0; i < _residents.Count; i++)
				{
					InitializeResident(_residents[i]);
				}
			}
		}

		public void RegisterRuntimeResident(ResidentController resident)
		{
			if (resident == null)
			{
				return;
			}
			if (!ConfigureResidentFromHouseData(resident))
			{
				UtilityMethods.Log("[ResidentManager] Could not configure runtime resident from house data. Resident=" + resident.name, LogType.Warning);
			}
			bool flag = false;
			if (!_residents.Contains(resident))
			{
				_residents.Add(resident);
				flag = true;
			}
			if (flag)
			{
				RuntimeResidentRegistered?.Invoke(resident);
			}
			if (resident.ResidentId != 0)
			{
				_residentsById[resident.ResidentId] = resident;
				if (_isInitDone || ShouldInitializeNetworkResidentOnRegister())
				{
					InitializeResident(resident);
				}
				ApplyPendingSnapshotsForResident(resident);
			}
		}

		public void UnregisterRuntimeResident(ResidentController resident)
		{
			if (!(resident == null))
			{
				_residents.Remove(resident);
				_restaurantQueuedResidents.Remove(resident);
				if (_residentsById.TryGetValue(resident.ResidentId, out var value) && value == resident)
				{
					_residentsById.Remove(resident.ResidentId);
				}
			}
		}

		public bool TryGetNextPatrolPoint(ResidentActivityType activity, ResidentController resident, int currentIndex, out int nextIndex, out Transform point)
		{
			point = null;
			nextIndex = -1;
			List<Transform> patrolPoints = GetPatrolPoints(activity);
			if (patrolPoints == null || patrolPoints.Count == 0)
			{
				return false;
			}
			int num = currentIndex;
			if (num < 0)
			{
				num = FindClosestPatrolPointIndex(patrolPoints, (resident != null) ? resident.transform.position : Vector3.zero);
			}
			for (int i = 1; i <= patrolPoints.Count; i++)
			{
				int num2 = (num + i) % patrolPoints.Count;
				Transform transform = patrolPoints[num2];
				if (!(transform == null))
				{
					nextIndex = num2;
					point = transform;
					return true;
				}
			}
			return false;
		}

		public float GetPatrolPointSampleRadius(ResidentActivityType activity)
		{
			if (activity != ResidentActivityType.SeaSwim)
			{
				return 1.5f;
			}
			return Mathf.Max(0.05f, _seaSwimPointSampleRadius);
		}

		public float GetSeaSwimTransitionRadius()
		{
			return Mathf.Max(0.05f, _seaSwimTransitionRadius);
		}

		public float GetSeaSwimHeightBlendDuration()
		{
			return Mathf.Max(0.01f, _seaSwimHeightBlendDuration);
		}

		public void SetScheduleTime(int hour, int minute)
		{
			_currentHour = Mathf.Clamp(hour, 0, 24);
			_currentMinute = Mathf.Clamp(minute, 0, 59);
			if (!IsSimulationAuthority())
			{
				return;
			}
			for (int i = 0; i < _residents.Count; i++)
			{
				if (_residents[i] != null)
				{
					_residents[i].SetScheduleTime(_currentHour, _currentMinute);
				}
			}
		}

		public void TeleportAllResidents()
		{
			for (int i = 0; i < _residents.Count; i++)
			{
				if (_residents[i] != null)
				{
					_residents[i].TeleportResident();
				}
			}
		}

		public int CallResidentsForRestaurant(int count, int tableId)
		{
			if (!IsSimulationAuthority())
			{
				return 0;
			}
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (restaurantManager == null)
			{
				return 0;
			}
			int num = 0;
			int count2 = _restaurantQueue.Count;
			ResidentController resident;
			while (num < count && count2-- > 0 && TryDequeueResident(out resident))
			{
				if (resident == null || !resident.CanBeCalledForRestaurant())
				{
					AddQueue(resident);
					continue;
				}
				Transform seatTransform = restaurantManager.GetSeatTransform(tableId, num);
				if (seatTransform == null)
				{
					AddQueue(resident);
					break;
				}
				if (!restaurantManager.CanAcceptDiningResident(tableId, num))
				{
					AddQueue(resident);
					UtilityMethods.Log($"[ResidentManager] Dining assignment skipped. Table is not waiting for this seat. Table={tableId}, Seat={num}", LogType.Warning);
					break;
				}
				PrepareResidentForRestaurantCall(resident, seatTransform);
				RestaurantAssignmentRequest request = new RestaurantAssignmentRequest(tableId, num, ResidentRestaurantTargetType.DiningTable, seatTransform);
				if (resident.TryAcceptRestaurantAssignment(request))
				{
					num++;
				}
				else
				{
					AddQueue(resident);
				}
			}
			if (num < count)
			{
				UtilityMethods.Log($"[ResidentManager] Not enough available residents. Requested={count}, Called={num}", LogType.Warning);
			}
			return num;
		}

		public int CallMissingResidentsForRestaurant(int expectedSeatCount, int tableId)
		{
			if (!IsSimulationAuthority() || expectedSeatCount <= 0)
			{
				return 0;
			}
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (restaurantManager == null)
			{
				return 0;
			}
			HashSet<int> hashSet = new HashSet<int>();
			for (int i = 0; i < _residents.Count; i++)
			{
				ResidentRuntimeState residentRuntimeState = ((_residents[i] != null) ? _residents[i].Runtime : null);
				if (residentRuntimeState != null && residentRuntimeState.RestaurantTargetType == ResidentRestaurantTargetType.DiningTable && residentRuntimeState.RestaurantTableId == tableId && residentRuntimeState.RestaurantSeatIndex >= 0 && residentRuntimeState.RestaurantSeatIndex < expectedSeatCount)
				{
					hashSet.Add(residentRuntimeState.RestaurantSeatIndex);
				}
			}
			int num = 0;
			for (int j = 0; j < expectedSeatCount; j++)
			{
				if (!hashSet.Contains(j) && TryCallResidentForDiningSeat(tableId, j, restaurantManager))
				{
					num++;
				}
			}
			return num;
		}

		public int RestartDiningTableMovements(int tableId)
		{
			if (!IsSimulationAuthority())
			{
				return 0;
			}
			int num = 0;
			for (int i = 0; i < _residents.Count; i++)
			{
				ResidentController residentController = _residents[i];
				ResidentRuntimeState residentRuntimeState = ((residentController != null) ? residentController.Runtime : null);
				if (residentRuntimeState != null && residentRuntimeState.RestaurantTargetType == ResidentRestaurantTargetType.DiningTable && residentRuntimeState.RestaurantTableId == tableId && !residentRuntimeState.HasArrived)
				{
					if (residentController.Brain != null && residentController.Brain.TryRestartRestaurantMovement())
					{
						num++;
						continue;
					}
					int restaurantSeatIndex = residentRuntimeState.RestaurantSeatIndex;
					string residentName = residentController.ResidentName;
					residentController.ForceLeaveRestaurantAssignment(tableId, ResidentRestaurantTargetType.DiningTable);
					UtilityMethods.Log($"[ResidentManager] Dining movement recovery removed unreachable assignment. Table={tableId}, Seat={restaurantSeatIndex}, Resident={residentName}", LogType.Warning);
				}
			}
			return num;
		}

		public int CompactArrivedDiningTableAssignments(int tableId, IReadOnlyList<Transform> seatingPoints, List<string> compactedResidentNames)
		{
			compactedResidentNames?.Clear();
			if (!IsSimulationAuthority() || seatingPoints == null || seatingPoints.Count == 0)
			{
				return 0;
			}
			List<ResidentController> list = new List<ResidentController>();
			List<ResidentController> list2 = new List<ResidentController>();
			for (int i = 0; i < _residents.Count; i++)
			{
				ResidentController residentController = _residents[i];
				ResidentRuntimeState residentRuntimeState = ((residentController != null) ? residentController.Runtime : null);
				if (residentRuntimeState != null && residentRuntimeState.RestaurantTargetType == ResidentRestaurantTargetType.DiningTable && residentRuntimeState.RestaurantTableId == tableId)
				{
					if (residentRuntimeState.HasArrived && residentRuntimeState.IsInRestaurant)
					{
						list.Add(residentController);
					}
					else
					{
						list2.Add(residentController);
					}
				}
			}
			list.Sort((ResidentController left, ResidentController right) => left.Runtime.RestaurantSeatIndex.CompareTo(right.Runtime.RestaurantSeatIndex));
			if (list.Count > seatingPoints.Count)
			{
				for (int num = seatingPoints.Count; num < list.Count; num++)
				{
					list2.Add(list[num]);
				}
				list.RemoveRange(seatingPoints.Count, list.Count - seatingPoints.Count);
			}
			for (int num2 = 0; num2 < list.Count; num2++)
			{
				if (!(seatingPoints[num2] != null))
				{
					list2.AddRange(list);
					list.Clear();
					break;
				}
			}
			for (int num3 = 0; num3 < list2.Count; num3++)
			{
				list2[num3]?.ForceLeaveRestaurantAssignment(tableId, ResidentRestaurantTargetType.DiningTable);
			}
			for (int num4 = 0; num4 < list.Count; num4++)
			{
				ResidentController residentController2 = list[num4];
				ResidentRuntimeState runtime = residentController2.Runtime;
				Transform transform = seatingPoints[num4];
				runtime.RestaurantSeatIndex = num4;
				runtime.TargetDestination = transform.position;
				runtime.TargetRotation = transform.rotation.eulerAngles;
				runtime.HasValidTarget = true;
				runtime.HasArrived = true;
				runtime.IsInRestaurant = true;
				runtime.RestaurantSubState = ResidentRestaurantSubState.ArrivedAtSeat;
				residentController2.Brain?.Movement.StopAtPose(transform.position, transform.rotation);
				compactedResidentNames?.Add(residentController2.ResidentName);
			}
			UtilityMethods.Log($"[ResidentManager] Compacted arrived dining group. Table={tableId}, Survivors={list.Count}, Released={list2.Count}");
			return list.Count;
		}

		private bool TryCallResidentForDiningSeat(int tableId, int seatIndex, RestaurantManager restaurantManager)
		{
			int count = _restaurantQueue.Count;
			ResidentController resident;
			while (count-- > 0 && TryDequeueResident(out resident))
			{
				if (resident == null || !resident.CanBeCalledForRestaurant())
				{
					AddQueue(resident);
					continue;
				}
				Transform seatTransform = restaurantManager.GetSeatTransform(tableId, seatIndex);
				if (seatTransform == null || !restaurantManager.CanAcceptDiningResident(tableId, seatIndex))
				{
					AddQueue(resident);
					return false;
				}
				PrepareResidentForRestaurantCall(resident, seatTransform);
				RestaurantAssignmentRequest request = new RestaurantAssignmentRequest(tableId, seatIndex, ResidentRestaurantTargetType.DiningTable, seatTransform);
				if (resident.TryAcceptRestaurantAssignment(request))
				{
					return true;
				}
				AddQueue(resident);
			}
			return false;
		}

		public void CallResidentForConveyorRestaurant(int tableId)
		{
			if (!IsSimulationAuthority())
			{
				return;
			}
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			if (restaurantManager == null || !restaurantManager.CanAcceptConveyorResident(tableId))
			{
				return;
			}
			int count = _restaurantQueue.Count;
			ResidentController resident;
			while (count-- > 0 && TryDequeueResident(out resident))
			{
				if (resident == null || !resident.CanBeCalledForRestaurant())
				{
					AddQueue(resident);
					continue;
				}
				Transform conveyorSeatTransform = restaurantManager.GetConveyorSeatTransform(tableId);
				if (conveyorSeatTransform == null)
				{
					AddQueue(resident);
					return;
				}
				if (!restaurantManager.CanAcceptConveyorResident(tableId))
				{
					AddQueue(resident);
					UtilityMethods.Log($"[ResidentManager] Conveyor assignment skipped. Table is not accepting a resident. Table={tableId}", LogType.Warning);
					return;
				}
				PrepareResidentForRestaurantCall(resident, conveyorSeatTransform);
				RestaurantAssignmentRequest request = new RestaurantAssignmentRequest(tableId, 0, ResidentRestaurantTargetType.ConveyorTable, conveyorSeatTransform);
				if (resident.TryAcceptRestaurantAssignment(request))
				{
					return;
				}
				AddQueue(resident);
			}
			UtilityMethods.Log($"[ResidentManager] No available resident found for conveyor table {tableId}.", LogType.Warning);
		}

		public void ForceRestaurantResidentsLeave(int tableId, ResidentRestaurantTargetType targetType)
		{
			if (IsSimulationAuthority())
			{
				for (int i = 0; i < _residents.Count; i++)
				{
					_residents[i]?.ForceLeaveRestaurantAssignment(tableId, targetType);
				}
			}
		}

		public int ReconcileDiningTableAssignments(int tableId, int expectedSeatCount, out int removedCount)
		{
			removedCount = 0;
			if (!IsSimulationAuthority())
			{
				return -1;
			}
			expectedSeatCount = Mathf.Max(0, expectedSeatCount);
			Dictionary<int, ResidentController> dictionary = new Dictionary<int, ResidentController>();
			List<ResidentController> list = new List<ResidentController>();
			for (int i = 0; i < _residents.Count; i++)
			{
				ResidentController residentController = _residents[i];
				ResidentRuntimeState residentRuntimeState = ((residentController != null) ? residentController.Runtime : null);
				if (residentRuntimeState != null && residentRuntimeState.RestaurantTargetType == ResidentRestaurantTargetType.DiningTable && residentRuntimeState.RestaurantTableId == tableId)
				{
					int restaurantSeatIndex = residentRuntimeState.RestaurantSeatIndex;
					ResidentController value;
					if (restaurantSeatIndex < 0 || restaurantSeatIndex >= expectedSeatCount)
					{
						list.Add(residentController);
					}
					else if (!dictionary.TryGetValue(restaurantSeatIndex, out value))
					{
						dictionary.Add(restaurantSeatIndex, residentController);
					}
					else if (GetDiningAssignmentKeeperScore(residentController) > GetDiningAssignmentKeeperScore(value))
					{
						dictionary[restaurantSeatIndex] = residentController;
						list.Add(value);
					}
					else
					{
						list.Add(residentController);
					}
				}
			}
			for (int j = 0; j < list.Count; j++)
			{
				ResidentController residentController2 = list[j];
				if (!(residentController2 == null))
				{
					residentController2.ForceLeaveRestaurantAssignment(tableId, ResidentRestaurantTargetType.DiningTable);
					removedCount++;
				}
			}
			if (removedCount > 0)
			{
				UtilityMethods.Log($"[ResidentManager] Reconciled dining assignments. TableId={tableId}, " + $"ExpectedSeats={expectedSeatCount}, ValidAssignments={dictionary.Count}, Removed={removedCount}", LogType.Warning);
			}
			return dictionary.Count;
		}

		private static int GetDiningAssignmentKeeperScore(ResidentController resident)
		{
			ResidentRuntimeState residentRuntimeState = ((resident != null) ? resident.Runtime : null);
			if (residentRuntimeState == null)
			{
				return 0;
			}
			int num = 0;
			if (residentRuntimeState.HasArrived)
			{
				num += 2;
			}
			if (residentRuntimeState.IsInRestaurant)
			{
				num++;
			}
			return num;
		}

		public bool TryGetRestaurantResident(int tableId, ResidentRestaurantTargetType targetType, out ResidentController resident)
		{
			for (int i = 0; i < _residents.Count; i++)
			{
				ResidentController residentController = _residents[i];
				ResidentRuntimeState residentRuntimeState = ((residentController != null) ? residentController.Runtime : null);
				if (residentRuntimeState != null && residentRuntimeState.RestaurantTableId == tableId && residentRuntimeState.RestaurantTargetType == targetType)
				{
					resident = residentController;
					return true;
				}
			}
			resident = null;
			return false;
		}

		public bool TryGetRestaurantResident(int tableId, ResidentRestaurantTargetType targetType, int seatIndex, out ResidentController resident)
		{
			for (int i = 0; i < _residents.Count; i++)
			{
				ResidentController residentController = _residents[i];
				ResidentRuntimeState residentRuntimeState = ((residentController != null) ? residentController.Runtime : null);
				if (residentRuntimeState != null && residentRuntimeState.RestaurantTableId == tableId && residentRuntimeState.RestaurantTargetType == targetType && residentRuntimeState.RestaurantSeatIndex == seatIndex)
				{
					resident = residentController;
					return true;
				}
			}
			resident = null;
			return false;
		}

		public bool PlayRestaurantFeedbackCue(int tableId, int speakerSeatIndex, RestaurantFeedbackCue cue)
		{
			return PlayRestaurantMoment(tableId, speakerSeatIndex, cue, RestaurantResidentGesture.None);
		}

		public bool PlayRestaurantMoment(int tableId, int speakerSeatIndex, RestaurantFeedbackCue cue, RestaurantResidentGesture gesture)
		{
			for (int i = 0; i < _residents.Count; i++)
			{
				ResidentController residentController = _residents[i];
				if (residentController != null && residentController.PlayRestaurantMoment(tableId, speakerSeatIndex, cue, gesture))
				{
					return true;
				}
			}
			return false;
		}

		public int GetClassicTableOrderSpeakerSeatIndex(int tableId)
		{
			ResidentController residentController = null;
			uint num = 0u;
			for (int i = 0; i < _residents.Count; i++)
			{
				ResidentController residentController2 = _residents[i];
				ResidentRuntimeState residentRuntimeState = ((residentController2 != null) ? residentController2.Runtime : null);
				if (residentRuntimeState != null && residentRuntimeState.RestaurantTargetType == ResidentRestaurantTargetType.DiningTable && residentRuntimeState.RestaurantTableId == tableId && residentRuntimeState.HasArrived && residentRuntimeState.IsInRestaurant)
				{
					uint classicTableOrderSpeakerScore = ResidentController.GetClassicTableOrderSpeakerScore(tableId, residentController2.ResidentId);
					if (residentController == null || classicTableOrderSpeakerScore > num || (classicTableOrderSpeakerScore == num && residentController2.ResidentId < residentController.ResidentId))
					{
						residentController = residentController2;
						num = classicTableOrderSpeakerScore;
					}
				}
			}
			if (!(residentController != null))
			{
				return 0;
			}
			return Mathf.Max(0, residentController.CurrentSeatIndex_ServerReadable);
		}

		private void PrepareResidentForRestaurantCall(ResidentController resident, Transform seat)
		{
			if (!_useRestaurantCallTeleport || !IsSimulationAuthority() || resident == null || seat == null || resident.Runtime == null || resident.Brain == null)
			{
				return;
			}
			ResidentActivityType currentActivity = resident.Runtime.CurrentActivity;
			if (!ShouldTeleportForRestaurantCallActivity(currentActivity))
			{
				return;
			}
			float num = Mathf.Max(0f, _restaurantCallTeleportDistanceThreshold);
			float num2 = Vector3.Distance(resident.transform.position, seat.position);
			if (!(num2 <= num))
			{
				if (_restaurantCallEntryPoints == null || _restaurantCallEntryPoints.Count == 0)
				{
					UtilityMethods.Log("[ResidentManager] Restaurant call teleport skipped. Entry points are missing.", LogType.Warning);
					return;
				}
				if (!TryGetClosestRestaurantCallEntryPoint(seat.position, out var entryPoint))
				{
					UtilityMethods.Log("[ResidentManager] Restaurant call teleport skipped. No valid entry point found.", LogType.Warning);
					return;
				}
				if (!resident.Brain.Movement.Warp(entryPoint.position, entryPoint.rotation))
				{
					UtilityMethods.Log($"[ResidentManager] Restaurant call teleport failed. Resident={resident.ResidentName}, Activity={currentActivity}, Entry={entryPoint.name}", LogType.Warning);
					return;
				}
				UtilityMethods.Log($"[ResidentManager] Restaurant call teleport. Resident={resident.ResidentName}, Activity={currentActivity}, Distance={num2:0.0}, Entry={entryPoint.name}");
			}
		}

		private bool ShouldTeleportForRestaurantCallActivity(ResidentActivityType activity)
		{
			if (_restaurantCallTeleportActivities != null)
			{
				return _restaurantCallTeleportActivities.Contains(activity);
			}
			return false;
		}

		private bool TryGetClosestRestaurantCallEntryPoint(Vector3 seatPosition, out Transform entryPoint)
		{
			entryPoint = null;
			float num = float.MaxValue;
			for (int i = 0; i < _restaurantCallEntryPoints.Count; i++)
			{
				Transform transform = _restaurantCallEntryPoints[i];
				if (!(transform == null))
				{
					float sqrMagnitude = (transform.position - seatPosition).sqrMagnitude;
					if (!(sqrMagnitude >= num))
					{
						num = sqrMagnitude;
						entryPoint = transform;
					}
				}
			}
			return entryPoint != null;
		}

		public bool TryGetResident(ushort residentId, out ResidentController resident)
		{
			if (_residentsById.TryGetValue((byte)residentId, out resident))
			{
				return resident != null;
			}
			return false;
		}

		public void BeginLateJoinApply()
		{
			foreach (object item in BeginLateJoinApplySteps())
			{
				_ = item;
			}
		}

		public IEnumerable<object> BeginLateJoinApplySteps()
		{
			_pendingStateSnapshots.Clear();
			_pendingMovementSnapshots.Clear();
			_restaurantQueue.Clear();
			_restaurantQueuedResidents.Clear();
			for (int i = 0; i < _residents.Count; i++)
			{
				if (!(_residents[i] == null))
				{
					if (IsSimulationAuthority())
					{
						_residents[i].SetSnapshotApplyMode(enabled: true);
					}
					else
					{
						_residents[i].SetLocalSimulationEnabled(enabled: false);
					}
					yield return null;
				}
			}
		}

		public void ApplySnapshotPacked(ResidentSnapshotPacked snapshot)
		{
			if (TryGetResident(snapshot.ResidentId, out var resident))
			{
				resident.ApplySnapshotPacked(snapshot, !IsSimulationAuthority());
			}
		}

		public void ApplyStateSnapshots(ResidentStateSnapshotPacked[] snapshots)
		{
			if (snapshots == null)
			{
				return;
			}
			for (int i = 0; i < snapshots.Length; i++)
			{
				ResidentStateSnapshotPacked snapshot = snapshots[i];
				if (TryGetResident(snapshot.ResidentId, out var resident))
				{
					resident.ApplyStateSnapshotPacked(snapshot, !IsSimulationAuthority());
				}
				else
				{
					QueuePendingStateSnapshot(snapshot);
				}
			}
		}

		public void ApplyMovementSnapshots(ResidentMovementSnapshotPacked[] snapshots)
		{
			if (snapshots == null || IsSimulationAuthority())
			{
				return;
			}
			for (int i = 0; i < snapshots.Length; i++)
			{
				ResidentMovementSnapshotPacked snapshot = snapshots[i];
				if (TryGetResident(snapshot.ResidentId, out var resident))
				{
					resident.ApplyMovementSnapshotPacked(snapshot);
				}
				else
				{
					QueuePendingMovementSnapshot(snapshot);
				}
			}
		}

		public void EndLateJoinApply()
		{
			foreach (object item in EndLateJoinApplySteps())
			{
				_ = item;
			}
		}

		public IEnumerable<object> EndLateJoinApplySteps()
		{
			for (int i = 0; i < _residents.Count; i++)
			{
				ResidentController residentController = _residents[i];
				if (!(residentController == null))
				{
					if (IsSimulationAuthority())
					{
						residentController.SetSnapshotApplyMode(enabled: false);
						AddQueue(residentController);
					}
					else
					{
						residentController.SetLocalSimulationEnabled(enabled: false);
					}
					yield return null;
				}
			}
		}

		public void AfterAllLateJoinModulesApplied()
		{
		}

		public void SetDebugSpeedMultiplier(float multiplier)
		{
			ResidentBrain.SetDebugSpeedMultiplier(multiplier);
			for (int i = 0; i < _residents.Count; i++)
			{
				_residents[i]?.Brain?.RefreshMovementSpeed();
			}
			UtilityMethods.Log($"[ResidentManager] Debug speed multiplier set to {ResidentBrain.DebugSpeedMultiplier:0.##}x");
		}

		private void QueuePendingStateSnapshot(ResidentStateSnapshotPacked snapshot)
		{
			if (snapshot.ResidentId != 0 && !IsSimulationAuthority())
			{
				_pendingStateSnapshots[snapshot.ResidentId] = snapshot;
			}
		}

		private void QueuePendingMovementSnapshot(ResidentMovementSnapshotPacked snapshot)
		{
			if (snapshot.ResidentId != 0 && !IsSimulationAuthority())
			{
				_pendingMovementSnapshots[snapshot.ResidentId] = snapshot;
			}
		}

		private void ApplyPendingSnapshotsForResident(ResidentController resident)
		{
			if (!(resident == null) && resident.ResidentId != 0 && !IsSimulationAuthority())
			{
				byte residentId = resident.ResidentId;
				if (_pendingStateSnapshots.Remove(residentId, out var value))
				{
					resident.ApplyStateSnapshotPacked(value, authoritativeProxy: true);
				}
				if (_pendingMovementSnapshots.Remove(residentId, out var value2))
				{
					resident.ApplyMovementSnapshotPacked(value2);
				}
			}
		}

		public void AddQueue(ResidentController resident)
		{
			if (IsSimulationAuthority() && !(resident == null) && !_restaurantQueuedResidents.Contains(resident))
			{
				_restaurantQueue.Enqueue(resident);
				_restaurantQueuedResidents.Add(resident);
			}
		}

		private bool CanSpawnResidents()
		{
			NetworkManager singleton = NetworkManager.Singleton;
			if (!(singleton == null) && singleton.IsListening)
			{
				return singleton.IsServer;
			}
			return true;
		}

		private void SpawnHouseResidents()
		{
			for (int i = 0; i < _houseBindings.Count; i++)
			{
				HouseDataSO houseData = _houseBindings[i].HouseData;
				if (!(houseData == null) && houseData.Residents != null)
				{
					for (int j = 0; j < houseData.Residents.Count; j++)
					{
						SpawnResident(houseData, houseData.Residents[j]);
					}
				}
			}
		}

		private void SpawnResident(HouseDataSO houseData, HouseDataSO.Resident entry)
		{
			if (entry.ResidentPrefab == null || entry.ResidentData == null)
			{
				UtilityMethods.Log("[ResidentManager] House resident entry is missing prefab or resident data.", LogType.Warning);
				return;
			}
			ResidentController residentController = UnityEngine.Object.Instantiate(entry.ResidentPrefab, houseData.ExitPoint, houseData.ExitRotation, base.transform);
			residentController.ConfigureResident(entry.ResidentData, houseData.ExitPoint, houseData.ExitRotation, this);
			RegisterRuntimeResident(residentController);
			if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer && residentController.TryGetComponent<NetworkObject>(out var component) && !component.IsSpawned)
			{
				component.Spawn(destroyWithScene: true);
			}
		}

		private void InitializeResident(ResidentController resident)
		{
			if (!(resident == null))
			{
				resident.Initialize();
				if (IsSimulationAuthority())
				{
					resident.SetScheduleTime(_currentHour, _currentMinute);
					AddQueue(resident);
				}
				else
				{
					resident.SetLocalSimulationEnabled(enabled: false);
				}
			}
		}

		private bool ConfigureResidentFromHouseData(ResidentController resident)
		{
			if (resident == null)
			{
				return false;
			}
			for (int i = 0; i < _houseBindings.Count; i++)
			{
				HouseDataSO houseData = _houseBindings[i].HouseData;
				if (houseData == null || houseData.Residents == null)
				{
					continue;
				}
				for (int j = 0; j < houseData.Residents.Count; j++)
				{
					HouseDataSO.Resident entry = houseData.Residents[j];
					if (IsResidentEntryMatch(resident, entry))
					{
						resident.ConfigureResident(entry.ResidentData, houseData.ExitPoint, houseData.ExitRotation, this);
						return true;
					}
				}
			}
			return false;
		}

		private static bool IsResidentEntryMatch(ResidentController resident, HouseDataSO.Resident entry)
		{
			if (resident == null || entry.ResidentData == null)
			{
				return false;
			}
			if (resident.ResidentData != null && entry.ResidentData == resident.ResidentData)
			{
				return true;
			}
			if (resident.NetworkResidentId != 0)
			{
				return entry.ResidentData.NpcID == resident.NetworkResidentId;
			}
			return false;
		}

		private static bool ShouldInitializeNetworkResidentOnRegister()
		{
			NetworkManager singleton = NetworkManager.Singleton;
			if (singleton != null && singleton.IsListening)
			{
				return !singleton.IsServer;
			}
			return false;
		}

		private static bool IsSimulationAuthority()
		{
			NetworkManager singleton = NetworkManager.Singleton;
			if (!(singleton == null) && singleton.IsListening)
			{
				return singleton.IsServer;
			}
			return true;
		}

		private List<Transform> GetPatrolPoints(ResidentActivityType activity)
		{
			return activity switch
			{
				ResidentActivityType.SeaSwim => _seaSwimPoints, 
				ResidentActivityType.ParkPatrol => _parkWalkPoints, 
				ResidentActivityType.CityPatrolNear => _cityPatrolNearPoints, 
				_ => null, 
			};
		}

		private int FindClosestPatrolPointIndex(List<Transform> points, Vector3 position)
		{
			int result = 0;
			float num = float.MaxValue;
			for (int i = 0; i < points.Count; i++)
			{
				Transform transform = points[i];
				if (!(transform == null))
				{
					float sqrMagnitude = (transform.position - position).sqrMagnitude;
					if (!(sqrMagnitude >= num))
					{
						num = sqrMagnitude;
						result = i;
					}
				}
			}
			return result;
		}

		private bool TryDequeueResident(out ResidentController resident)
		{
			resident = null;
			while (_restaurantQueue.Count > 0)
			{
				ResidentController residentController = _restaurantQueue.Dequeue();
				if (residentController != null)
				{
					_restaurantQueuedResidents.Remove(residentController);
				}
				if (!(residentController == null))
				{
					resident = residentController;
					return true;
				}
			}
			return false;
		}

		private void TryBroadcastResidentStateChanges()
		{
			if (!TryGetSyncRelay(out var relay))
			{
				return;
			}
			_stateSnapshotBuffer.Clear();
			for (int i = 0; i < _residents.Count; i++)
			{
				ResidentController resident = _residents[i];
				if (CanBuildSyncSnapshot(resident))
				{
					ResidentStateSnapshotPacked residentStateSnapshotPacked = ResidentSnapshotController.BuildState(resident);
					if (!_lastSentStateSnapshots.TryGetValue(residentStateSnapshotPacked.ResidentId, out var value) || !residentStateSnapshotPacked.EqualsSemantic(value))
					{
						_lastSentStateSnapshots[residentStateSnapshotPacked.ResidentId] = residentStateSnapshotPacked;
						_stateSnapshotBuffer.Add(residentStateSnapshotPacked);
					}
				}
			}
			if (_stateSnapshotBuffer.Count > 0)
			{
				relay.BroadcastResidentStateBatch(_stateSnapshotBuffer.ToArray());
			}
		}

		private void TryBroadcastResidentMovementSnapshots()
		{
			if (Time.time < _nextMovementSyncTime)
			{
				return;
			}
			_nextMovementSyncTime = Time.time + 0.2f;
			if (!TryGetSyncRelay(out var relay))
			{
				return;
			}
			_movementSnapshotBuffer.Clear();
			for (int i = 0; i < _residents.Count; i++)
			{
				ResidentController residentController = _residents[i];
				if (CanBuildSyncSnapshot(residentController))
				{
					ResidentMovementSnapshotPacked residentMovementSnapshotPacked = ResidentSnapshotController.BuildMovement(residentController);
					bool isMoving = IsResidentActivelyMoving(residentController.Runtime);
					if (ShouldSendMovementSnapshot(residentMovementSnapshotPacked, isMoving))
					{
						_lastSentMovementSnapshots[residentMovementSnapshotPacked.ResidentId] = residentMovementSnapshotPacked;
						_movementSnapshotBuffer.Add(residentMovementSnapshotPacked);
					}
				}
			}
			if (_movementSnapshotBuffer.Count > 0)
			{
				relay.BroadcastResidentMovementBatch(_movementSnapshotBuffer.ToArray());
			}
		}

		private bool ShouldSendMovementSnapshot(ResidentMovementSnapshotPacked snapshot, bool isMoving)
		{
			if (!isMoving)
			{
				return false;
			}
			if (!_lastSentMovementSnapshots.TryGetValue(snapshot.ResidentId, out var value))
			{
				return true;
			}
			if ((snapshot.Position - value.Position).sqrMagnitude > 0.0025000002f)
			{
				return true;
			}
			float current = ResidentYawQuantize.DecodeYaw(value.YawQ);
			float target = ResidentYawQuantize.DecodeYaw(snapshot.YawQ);
			return Mathf.Abs(Mathf.DeltaAngle(current, target)) > 2f;
		}

		private static bool IsResidentActivelyMoving(ResidentRuntimeState runtime)
		{
			if (runtime != null && runtime.HasValidTarget)
			{
				return !runtime.HasArrived;
			}
			return false;
		}

		private bool TryGetSyncRelay(out ResidentController relay)
		{
			relay = null;
			for (int i = 0; i < _residents.Count; i++)
			{
				ResidentController residentController = _residents[i];
				if (!(residentController == null) && residentController.IsSpawned && residentController.ResidentId != 0)
				{
					relay = residentController;
					return true;
				}
			}
			return false;
		}

		private static bool CanBuildSyncSnapshot(ResidentController resident)
		{
			if (resident != null && resident.IsSpawned && resident.ResidentId != 0)
			{
				return resident.Runtime != null;
			}
			return false;
		}

		private static bool IsLiveNetworkAuthority()
		{
			NetworkManager singleton = NetworkManager.Singleton;
			if (singleton != null && singleton.IsListening)
			{
				return singleton.IsServer;
			}
			return false;
		}
	}
}
