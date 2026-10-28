using PaperPixelGames.Core.Data.Ai.NPCs;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentBrain
	{
		private readonly ResidentController _owner;

		private readonly ResidentDataSO _residentData;

		private GameObject _model;

		// Runtime registration/customization may replace the baked model after
		// the brain already exists. Visibility must follow the replacement.
		public void RebindModel(GameObject model)
		{
			_model = model;
			if (_model != null)
				_model.SetActive(_modelVisibilityHeld || (Runtime != null ? Runtime.CurrentActivity != ResidentActivityType.Home : _deferredModelVisible));
		}

		private readonly float _moveSpeed;

		private ResidentScheduleController _schedule;

		private ResidentWaypointResolver _waypoints;

		private ResidentStateMachine _stateMachine;

		private bool _paused;

		private bool _modelVisibilityHeld;

		private bool _deferredModelVisible = true;

		private int _lastHour = 8;

		private int _lastMinute;

		private static float _debugSpeedMultiplier = 1f;

		public ResidentRuntimeState Runtime { get; private set; }

		public ResidentMovementController Movement { get; private set; }

		public ResidentStateMachine StateMachine => _stateMachine;

		public float EffectiveMoveSpeed => _moveSpeed * _debugSpeedMultiplier;

		public static float DebugSpeedMultiplier => _debugSpeedMultiplier;

		public ResidentBrain(ResidentController owner, ResidentDataSO residentData, GameObject model, ResidentMovementController movement, ResidentWaypointResolver waypoints)
		{
			_owner = owner;
			_residentData = residentData;
			_model = model;
			_moveSpeed = ((residentData != null && residentData.MoveSpeed > 0f) ? residentData.MoveSpeed : 3.5f);
			Movement = movement;
			_waypoints = waypoints;
		}

		public void Initialize(byte residentId, int hour, int minute)
		{
			Runtime = new ResidentRuntimeState
			{
				ResidentId = residentId,
				CurrentActivity = ResidentActivityType.Home,
				PreviousActivity = ResidentActivityType.Home,
				LastStateChangeTime = Time.time
			};
			_lastHour = hour;
			_lastMinute = minute;
			_schedule = new ResidentScheduleController(_residentData);
			_stateMachine = new ResidentStateMachine();
			ForceResetToStart(evaluateSchedule: false);
			EvaluateSchedule(hour, minute, force: true);
		}

		public void Tick(float deltaTime)
		{
			if (!_paused && Runtime != null && !Runtime.IsSnapshotApplying)
			{
				_stateMachine.Tick(deltaTime);
			}
		}

		public void SetScheduleTime(int hour, int minute)
		{
			_lastHour = hour;
			_lastMinute = minute;
			EvaluateSchedule(hour, minute, force: false);
		}

		public static void SetDebugSpeedMultiplier(float multiplier)
		{
			_debugSpeedMultiplier = Mathf.Max(0.05f, multiplier);
		}

		public void RefreshMovementSpeed()
		{
			Movement?.SetSpeed(EffectiveMoveSpeed);
		}

		public void RequestScheduleChange()
		{
			EvaluateSchedule(_lastHour, _lastMinute, force: true);
		}

		public void ResumeScheduleAfterInteraction()
		{
			if (Runtime != null && !Runtime.IsInteracting)
			{
				Runtime.PendingScheduleEntryIndex = -1;
				EvaluateSchedule(_lastHour, _lastMinute, force: false);
			}
		}

		public void ForceResetToStart(bool evaluateSchedule = true)
		{
			if (Runtime != null)
			{
				if (_waypoints.ResolveHome(out var position, out var rotation))
				{
					Movement.Warp(position, Quaternion.Euler(rotation));
					Runtime.TargetDestination = position;
					Runtime.TargetRotation = rotation;
					Runtime.HasValidTarget = true;
				}
				Runtime.CurrentScheduleEntryIndex = -1;
				Runtime.PendingScheduleEntryIndex = -1;
				Runtime.CurrentPatrolEntryPointIndex = -1;
				Runtime.ClearRestaurantAssignment();
				_stateMachine.ChangeState(new ResidentHomeState(this), ResidentRequestPriority.HardReset);
				if (evaluateSchedule)
				{
					EvaluateSchedule(_lastHour, _lastMinute, force: true);
				}
			}
		}

		public void Pause()
		{
			_paused = true;
			Runtime.IsSnapshotApplying = true;
			Runtime.SystemState = ResidentSystemState.SnapshotApplying;
			_stateMachine.Suspend();
			Movement.Stop();
		}

		public void Resume()
		{
			Runtime.IsSnapshotApplying = false;
			Runtime.SystemState = ResidentSystemState.Normal;
			_stateMachine.Resume();
			_paused = false;
		}

		public void SetLocalSimulationEnabled(bool enabled)
		{
			if (Runtime != null)
			{
				Runtime.IsSnapshotApplying = false;
				Runtime.SystemState = ResidentSystemState.Normal;
				_paused = !enabled;
				if (enabled)
				{
					_stateMachine.Resume();
					return;
				}
				_stateMachine.Suspend();
				Movement.Stop();
			}
		}

		public void SetModelVisible(bool visible)
		{
			_deferredModelVisible = visible;
			if (_modelVisibilityHeld)
			{
				if (_model != null && !_model.activeSelf)
				{
					_model.SetActive(value: true);
				}
			}
			else if (_model != null && _model.activeSelf != visible)
			{
				_model.SetActive(visible);
			}
		}

		public void BeginModelVisibilityHold()
		{
			if (_modelVisibilityHeld)
			{
				if (_model != null && !_model.activeSelf)
				{
					_model.SetActive(value: true);
				}
				return;
			}
			_modelVisibilityHeld = true;
			if (_model != null)
			{
				_deferredModelVisible = _model.activeSelf;
				if (!_model.activeSelf)
				{
					_model.SetActive(value: true);
				}
			}
		}

		public void ReleaseModelVisibilityHold()
		{
			if (_modelVisibilityHeld)
			{
				_modelVisibilityHeld = false;
				SetModelVisible(_deferredModelVisible);
			}
		}

		public void ChangeToFallback(string reason)
		{
			_stateMachine.ChangeState(new ResidentFallbackIdleState(this, reason), ResidentRequestPriority.FallbackIdle);
		}

		public bool TryAcceptRestaurantAssignment(RestaurantAssignmentRequest request)
		{
			if (!CanAcceptRestaurantAssignment())
			{
				return false;
			}
			if (request.TargetType == ResidentRestaurantTargetType.None || request.SeatTransform == null)
			{
				ResidentDebugLogger.Log("Restaurant assignment rejected. Invalid request target or seat transform.", LogType.Warning);
				return false;
			}
			ResidentActivityType currentActivity = Runtime.CurrentActivity;
			ResidentActivityType previousActivity = Runtime.PreviousActivity;
			Vector3 targetDestination = Runtime.TargetDestination;
			Vector3 targetRotation = Runtime.TargetRotation;
			bool hasValidTarget = Runtime.HasValidTarget;
			bool hasArrived = Runtime.HasArrived;
			bool isAvailable = Runtime.IsAvailable;
			bool isWorking = Runtime.IsWorking;
			bool isSwimming = Runtime.IsSwimming;
			Runtime.SetActivity(ResidentActivityType.Restaurant);
			Runtime.RestaurantTableId = request.TableId;
			Runtime.RestaurantSeatIndex = request.SeatIndex;
			Runtime.RestaurantTargetType = request.TargetType;
			Runtime.RestaurantSubState = ResidentRestaurantSubState.MovingToSeat;
			Runtime.RestaurantServiceState = RestaurantActivityType.Idle;
			Runtime.TargetDestination = request.TargetDestination;
			Runtime.TargetRotation = request.TargetRotation.eulerAngles;
			Runtime.HasValidTarget = true;
			Runtime.IsAvailable = false;
			Runtime.IsWorking = false;
			Runtime.IsSwimming = false;
			Runtime.HasArrived = false;
			Runtime.IsInRestaurant = false;
			SetModelVisible(visible: true);
			if (!Movement.MoveTo(Runtime.TargetDestination, EffectiveMoveSpeed, 1.5f, requireCompletePath: true))
			{
				Runtime.ClearRestaurantAssignment();
				Runtime.CurrentActivity = currentActivity;
				Runtime.PreviousActivity = previousActivity;
				Runtime.TargetDestination = targetDestination;
				Runtime.TargetRotation = targetRotation;
				Runtime.HasValidTarget = hasValidTarget;
				Runtime.HasArrived = hasArrived;
				Runtime.IsAvailable = isAvailable;
				Runtime.IsWorking = isWorking;
				Runtime.IsSwimming = isSwimming;
				SetModelVisible(currentActivity != ResidentActivityType.Home);
				if (hasValidTarget && !hasArrived)
				{
					Movement.MoveTo(targetDestination, EffectiveMoveSpeed, GetDestinationSampleRadius(currentActivity));
				}
				ResidentDebugLogger.Log("Restaurant assignment rejected. Destination is not reachable. Table=" + request.TableId + ", Seat=" + request.SeatIndex, LogType.Warning);
				return false;
			}
			_stateMachine.ChangeState(new ResidentRestaurantMoveState(this), ResidentRequestPriority.RestaurantAssignment);
			ResidentDebugLogger.Log("Restaurant assignment accepted. Table=" + request.TableId + ", Seat=" + request.SeatIndex);
			return true;
		}

		public bool TryRestartRestaurantMovement()
		{
			if (Runtime == null || !Runtime.HasRestaurantAssignment || Runtime.CurrentActivity != ResidentActivityType.Restaurant || Runtime.HasArrived || !Runtime.HasValidTarget)
			{
				return false;
			}
			return Movement.MoveTo(Runtime.TargetDestination, EffectiveMoveSpeed, 1.5f, requireCompletePath: true);
		}

		public bool CanAcceptRestaurantAssignment()
		{
			if (Runtime == null)
			{
				return false;
			}
			if (Runtime.SystemState == ResidentSystemState.Normal && Runtime.IsAvailable && !Runtime.IsWorking && !Runtime.IsSwimming && !Runtime.IsInteracting && !Runtime.IsInRestaurant)
			{
				return !Runtime.HasRestaurantAssignment;
			}
			return false;
		}

		public void CompleteRestaurantAssignment()
		{
			if (Runtime != null)
			{
				ResidentActivityType activity = ((Runtime.PreviousActivity != ResidentActivityType.Restaurant) ? Runtime.PreviousActivity : ResidentActivityType.Home);
				Runtime.RestaurantServiceState = RestaurantActivityType.Leave;
				Runtime.RestaurantSubState = ResidentRestaurantSubState.Leaving;
				Runtime.ClearRestaurantAssignment();
				Runtime.SetActivity(activity);
				Runtime.IsAvailable = false;
				Runtime.HasArrived = false;
				Movement.ClearPath();
				if (!EvaluateSchedule(_lastHour, _lastMinute, force: true, allowHomeFallbackOnMissingTarget: true))
				{
					MoveToHomeFallbackAfterRestaurant();
				}
			}
		}

		private bool EvaluateSchedule(int hour, int minute, bool force, bool allowHomeFallbackOnMissingTarget = false)
		{
			if (Runtime == null || Runtime.SystemState != ResidentSystemState.Normal)
			{
				return false;
			}
			if (Runtime.CurrentActivity == ResidentActivityType.Restaurant || Runtime.IsInteracting)
			{
				Runtime.PendingScheduleEntryIndex = -2;
				return false;
			}
			if (!_schedule.TryFindActiveEntry(hour, minute, out var entryIndex, out var entry))
			{
				if (Runtime.CurrentScheduleEntryIndex == -1 && !force)
				{
					return true;
				}
				Runtime.CurrentScheduleEntryIndex = -1;
				_stateMachine.ChangeState(new ResidentHomeState(this), ResidentRequestPriority.ScheduledActivity);
				return true;
			}
			if (!force && entryIndex == Runtime.CurrentScheduleEntryIndex)
			{
				return true;
			}
			Runtime.CurrentScheduleEntryIndex = entryIndex;
			Runtime.HasNewEntry = true;
			Runtime.CurrentWaypointIndex = entry.ActivityIndex;
			bool flag;
			if (IsPatrolActivity(entry.Activity))
			{
				if (Runtime.CurrentActivity != entry.Activity)
				{
					Runtime.CurrentPatrolEntryPointIndex = -1;
				}
				flag = TryAdvancePatrolTarget(entry.Activity);
			}
			else
			{
				flag = _waypoints.ResolveScheduleTarget(entry.Activity, entry.ActivityIndex, out var position, out var rotation);
				if (flag)
				{
					Runtime.TargetDestination = position;
					Runtime.TargetRotation = rotation;
					Runtime.HasValidTarget = true;
					Runtime.HasArrived = false;
				}
			}
			if (!flag)
			{
				Runtime.HasValidTarget = false;
				if (allowHomeFallbackOnMissingTarget)
				{
					return false;
				}
				ChangeToFallback("Missing or invalid schedule target. Activity=" + entry.Activity.ToString() + ", Index=" + entry.ActivityIndex);
				return false;
			}
			if (entry.Activity == ResidentActivityType.Home)
			{
				if (Runtime.IsWorking)
				{
					_stateMachine.ChangeState(new ResidentReturnHomeState(this, EffectiveMoveSpeed), ResidentRequestPriority.ScheduledActivity);
				}
				else
				{
					_stateMachine.ChangeState(new ResidentHomeState(this), ResidentRequestPriority.ScheduledActivity);
				}
				return true;
			}
			_stateMachine.ChangeState(new ResidentMoveToScheduleTargetState(this, entry.Activity, EffectiveMoveSpeed), ResidentRequestPriority.ScheduledActivity);
			ResidentDebugLogger.Log("Schedule changed. Activity=" + entry.Activity.ToString() + ", Index=" + entry.ActivityIndex);
			return true;
		}

		private void MoveToHomeFallbackAfterRestaurant()
		{
			if (!_waypoints.ResolveHome(out var position, out var rotation))
			{
				ChangeToFallback("Restaurant leave fallback failed. Home target is invalid.");
				return;
			}
			Runtime.TargetDestination = position;
			Runtime.TargetRotation = rotation;
			Runtime.HasValidTarget = true;
			Runtime.HasArrived = false;
			Runtime.IsAvailable = false;
			Runtime.IsWorking = false;
			Runtime.IsSwimming = false;
			Runtime.CurrentPatrolEntryPointIndex = -1;
			_stateMachine.ChangeState(new ResidentMoveToScheduleTargetState(this, ResidentActivityType.Idle, EffectiveMoveSpeed), ResidentRequestPriority.ScheduledActivity);
			ResidentDebugLogger.Log("Restaurant leave fell back to home because active schedule target could not be resolved.", LogType.Warning);
		}

		public bool TryAdvancePatrolTarget(ResidentActivityType activity)
		{
			if (Runtime == null)
			{
				return false;
			}
			ResidentManager instance = Singleton<ResidentManager>.Instance;
			if (instance == null || !instance.TryGetNextPatrolPoint(activity, _owner, Runtime.CurrentPatrolEntryPointIndex, out var nextIndex, out var point))
			{
				UtilityMethods.Log("[Resident] Patrol points are missing on ResidentManager. Activity=" + activity, LogType.Warning);
				return false;
			}
			Vector3 position = point.position;
			float destinationSampleRadius = GetDestinationSampleRadius(activity);
			if (!Movement.TryResolveDestination(position, destinationSampleRadius, out var resolvedDestination))
			{
				return false;
			}
			Runtime.CurrentPatrolEntryPointIndex = nextIndex;
			Runtime.TargetDestination = ((activity == ResidentActivityType.SeaSwim) ? point.position : resolvedDestination);
			Runtime.TargetRotation = point.rotation.eulerAngles;
			Runtime.HasValidTarget = true;
			Runtime.HasArrived = false;
			return true;
		}

		public float GetDestinationSampleRadius(ResidentActivityType activity)
		{
			ResidentManager instance = Singleton<ResidentManager>.Instance;
			if (!(instance != null))
			{
				return 1.5f;
			}
			return instance.GetPatrolPointSampleRadius(activity);
		}

		public float GetSeaSwimTransitionRadius()
		{
			ResidentManager instance = Singleton<ResidentManager>.Instance;
			if (!(instance != null))
			{
				return 2.5f;
			}
			return instance.GetSeaSwimTransitionRadius();
		}

		public float GetSeaSwimHeightBlendDuration()
		{
			ResidentManager instance = Singleton<ResidentManager>.Instance;
			if (!(instance != null))
			{
				return 0.75f;
			}
			return instance.GetSeaSwimHeightBlendDuration();
		}

		public static bool IsPatrolActivity(ResidentActivityType activity)
		{
			if (activity != ResidentActivityType.SeaSwim && activity != ResidentActivityType.CityPatrolNear)
			{
				return activity == ResidentActivityType.ParkPatrol;
			}
			return true;
		}
	}
}
