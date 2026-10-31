using PaperPixelGames.Core.Data.Ai.NPCs;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.AI;

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
			// The caller copied the outgoing model's active state. Keep that state
			// and any deferred visibility/hold unchanged while rebinding.
			_model = model;
		}

		private readonly float _moveSpeed;

		private ResidentScheduleController _schedule;

		private ResidentWaypointResolver _waypoints;

		private ResidentStateMachine _stateMachine;

		private bool _paused;

        // Non-persisted actor-local hold. Authority owns the token, world,
        // request and lifecycle; this class owns only the exact nav stop it sets.
        private object _talkOwner,_resumeOwner;
        private IResidentState _talkState;
        private NavMeshAgent _talkAgent;
        private bool _talkChangedAgent,_talkHadPath,_talkGoalValid,_talkUpdatePosition,_talkUpdateRotation,_talkSeat,_insideOriginalTick,_talkInheritedGoal;
        private Vector3 _talkGoal,_talkDestination;
        private float _talkStarted,_talkPausedTotal;
        private int _talkScheduleHour,_talkScheduleMinute;
        private string _talkSeatPhase;
        private string _conversationHoldStatus="none",_conversationGoalStatus="none";
        private IResidentState _resumeGoalState;
        private Vector3 _resumeGoal,_resumeProgressPosition;
        private float _resumeStarted,_resumeProgressAt;
        public string ConversationHoldStatus => _conversationHoldStatus;
        public string ConversationGoalStatus => _conversationGoalStatus;
        public float TalkActivityTime => Time.unscaledTime - _talkPausedTotal - (_talkOwner != null ? Time.unscaledTime - _talkStarted : 0f);
        public bool HasConversationHold(object token)
        {
            if(token==null||!ReferenceEquals(_talkOwner,token))return false;
            try { if(ConversationHoldIntact())return true;_conversationHoldStatus="conflict";return false; }
            catch(System.Exception){_conversationHoldStatus="validation-failed";return false;}
        }
        // Token ownership is distinct from whether Unity's path has naturally
        // advanced/replanned. Known mutating owners transfer via the hooks.
        private bool ConversationHoldIntact()
        {
            if (_talkOwner == null || !_owner || _owner.Brain != this || Runtime == null || _paused || Runtime.IsSnapshotApplying ||
                Runtime.SystemState != ResidentSystemState.Normal || _stateMachine == null || _stateMachine.IsSuspended ||
                !ReferenceEquals(_stateMachine.Current,_talkState) || !_talkAgent || !_talkAgent.enabled || !_talkAgent.isOnNavMesh ||
                _talkAgent.isOnOffMeshLink || !_talkAgent.isStopped) return false;
            if (_talkSeat)
            {
                if(!(_talkState is ResidentSeatActivityState seat)||seat.Phase!=_talkSeatPhase)return false;
                if(_talkSeatPhase=="Approaching"&&!seat.ConversationStableApproach&&!seat.ConversationOwnedWaitingApproach||
                   _talkSeatPhase=="Seated"&&!seat.ConversationStableSeat||
                   _talkSeatPhase=="Active"&&!seat.ConversationStableStanding&&!seat.ConversationStableRest)return false;
            }
            if (_talkAgent.updatePosition != _talkUpdatePosition || _talkAgent.updateRotation != _talkUpdateRotation ||
                Movement.HasMoveGoal != _talkGoalValid) return false;
            if (_talkGoalValid && Vector3.Distance(Movement.RequestedMoveGoal,_talkGoal)>.001f) return false;
            // The original path may have new/fewer corners, be temporarily
            // pending or have completed while braking. Only a changed actual
            // destination is evidence of an uncoordinated other movement.
            if (_talkChangedAgent && (_talkAgent.hasPath || _talkAgent.pathPending) &&
                Vector3.Distance(_talkAgent.destination,_talkDestination)>.15f) return false;
            return true;
        }
        public bool TryHoldConversation(object token,out string reason)
        {
            reason=null;
            if (token == null || _talkOwner != null || !_owner || _owner.Brain != this || Runtime == null || _paused ||
                Runtime.IsSnapshotApplying || Runtime.SystemState != ResidentSystemState.Normal || _stateMachine == null ||
                _stateMachine.IsSuspended || _stateMachine.Current == null || Movement == null)
            {reason="AlreadyHeldOrUnavailable";return false;}
            // Complete or invalidate an existing handoff before considering a
            // fresh interaction. This does not select or force a schedule.
            if(_resumeGoalState!=null)UpdateConversationGoalBoundary();
            var seat=_stateMachine.Current as ResidentSeatActivityState;
            bool seating=seat!=null&&(seat.ConversationStableRest||seat.ConversationStableStanding);
            bool walking=_stateMachine.Current is ResidentMoveToScheduleTargetState||_stateMachine.Current is ResidentReturnHomeState||
                seat!=null&&seat.ConversationStableApproach;
            bool ownedWaitingApproach=seat!=null&&seat.ConversationOwnedWaitingApproach;
            bool idle=_stateMachine.Current is ResidentIdleState||_stateMachine.Current is ResidentFallbackIdleState;
            if(!seating&&!walking&&!idle&&!ownedWaitingApproach){reason="UnsupportedOrTransitioningActivity";return false;}
            if(Runtime.IsWorking||Runtime.IsSwimming||Runtime.IsInRestaurant||Runtime.HasRestaurantAssignment||Runtime.IsInteracting)
            {reason="NonAmbientOwnerState";return false;}
            var agent=_owner.GetComponent<NavMeshAgent>();
            if (!agent || !agent.enabled || !agent.isOnNavMesh || agent.isOnOffMeshLink || agent.pathPending)
            {reason="NavigationUnavailable";return false;}
            // No path means no accepted moving route. A stationary native
            // navigation wait is held in place, never restarted at a stale goal.
            bool waitingNavigation=(walking||ownedWaitingApproach)&&!agent.hasPath&&agent.updatePosition&&
                agent.velocity.sqrMagnitude<=.0025f&&Vector3.Distance(_owner.transform.position,agent.nextPosition)<=.25f;
            bool completeWalkingPath=agent.updatePosition&&!agent.isStopped&&agent.hasPath&&agent.pathStatus==NavMeshPathStatus.PathComplete&&
                Movement.HasMoveGoal&&Vector3.Distance(_owner.transform.position,agent.nextPosition)<=.25f&&
                Vector3.Distance(Movement.RequestedMoveGoal,agent.destination)<=.15f;
            if((walking&&!completeWalkingPath&&!waitingNavigation)||(ownedWaitingApproach&&!walking&&!waitingNavigation))
            {reason="WalkingPathNotStable";return false;}
            if((seating||idle)&&(!agent.isStopped||agent.hasPath)){reason="StationaryNavigationNotStable";return false;}
            bool inherit=_resumeGoalState!=null;
            if(inherit && (!walking || !ReferenceEquals(_stateMachine.Current,_resumeGoalState) ||
                _resumeOwner==null || ReferenceEquals(token,_resumeOwner) || !Movement.HasMoveGoal ||
                Vector3.Distance(Movement.RequestedMoveGoal,_resumeGoal)>.001f ||
                Vector3.Distance(agent.destination,_resumeGoal)>.15f))
            {reason="PreviousGoalCannotBeInherited";return false;}
            bool changedAgent=false;
            try
            {
                bool hadPath=agent.hasPath,goalValid=Movement.HasMoveGoal;
                Vector3 goal=goalValid?Movement.RequestedMoveGoal:Vector3.zero;
                Vector3 destination=goalValid?agent.destination:Vector3.zero;
                bool updatePosition=agent.updatePosition,updateRotation=agent.updateRotation;
                if(!agent.isStopped){changedAgent=true;agent.isStopped=true;}
                _talkState=_stateMachine.Current;_talkAgent=agent;_talkSeat=seat!=null;_talkSeatPhase=seat?.Phase;
                _talkChangedAgent=changedAgent;_talkHadPath=hadPath;_talkGoalValid=goalValid;
                _talkGoal=goal;_talkDestination=destination;
                _talkUpdatePosition=updatePosition;_talkUpdateRotation=updateRotation;
                if(!inherit){_talkScheduleHour=_lastHour;_talkScheduleMinute=_lastMinute;}
                _talkInheritedGoal=inherit;_talkStarted=Time.unscaledTime;_talkOwner=token;
                // Transfer only after all acquisition operations succeed. A
                // stale prior token can no longer release this goal gate.
                if(inherit)_resumeOwner=token;
                _conversationHoldStatus="held";
                if(!inherit)_conversationGoalStatus="none";
                return true;
            }
            catch (System.Exception e)
            {
                try { if(changedAgent&&agent&&agent.enabled&&agent.isOnNavMesh&&agent.isStopped)agent.isStopped=false; }
                catch(System.Exception){_conversationHoldStatus="acquisition-cleanup-unresolved";reason="AcquisitionCleanupUnresolved";return false;}
                reason="HoldAcquisitionException:"+e.GetType().Name;return false;
            }
        }
        public bool ReleaseConversationHold(object token)
        {
            if(token==null||!ReferenceEquals(_talkOwner,token))return false;
            bool intact=false;var state=_talkState;var agent=_talkAgent;
            bool wasMoving=_talkChangedAgent&&_talkHadPath&&_talkGoalValid;var goal=_talkGoal;
            try
            {
                intact=ConversationHoldIntact();
                if(intact&&_talkChangedAgent&&agent&&agent.enabled&&agent.isOnNavMesh&&agent.isStopped)agent.isStopped=false;
                if(intact&&wasMoving)
                {
                    if(Movement.HasArrived())FinishConversationGoal("original-goal-arrived");
                    else if(!agent.pathPending&&!agent.hasPath)FinishConversationGoal("original-path-unavailable");
                    else if(!agent.pathPending&&agent.pathStatus!=NavMeshPathStatus.PathComplete)FinishConversationGoal("original-path-unavailable");
                    else
                    {
                        // Repeated conversations retain the original schedule,
                        // goal and elapsed active movement budget.
                        if(!_talkInheritedGoal)
                        {
                            _resumeGoalState=state;_resumeOwner=token;_resumeGoal=goal;
                            _resumeStarted=_resumeProgressAt=TalkActivityTime;
                            _resumeProgressPosition=_owner.transform.position;
                        }
                        _conversationGoalStatus="awaiting-original-goal";
                    }
                }
                else if(!intact&&_talkInheritedGoal)FinishConversationGoal("hold-conflict-unresolved");
                _conversationHoldStatus=intact?"released":"conflict-unresolved";
                return intact;
            }
            catch(System.Exception)
            {if(_talkInheritedGoal)FinishConversationGoal("hold-release-exception");_conversationHoldStatus="release-exception-unresolved";return false;}
            finally
            {
                _talkPausedTotal+=Mathf.Max(0f,Time.unscaledTime-_talkStarted);
                _talkOwner=null;_talkState=null;_talkAgent=null;_talkSeatPhase=null;_talkInheritedGoal=false;
            }
        }
        private void ConversationMovementControl()
        {
            // The resumed original state may retry its same target in Tick;
            // validate the goal afterwards instead of treating it as a takeover.
            if(_talkOwner!=null || !_insideOriginalTick)ConversationExternalTransfer();
        }
        private void ConversationExternalTransfer()
        {
            // Called before another known owner changes state/nav/suspension.
            if (_talkOwner != null) ReleaseConversationHold(_talkOwner);
            if(_resumeGoalState!=null)_conversationGoalStatus="external-control";
            _resumeGoalState=null;_resumeOwner=null;
        }
        private void FinishConversationGoal(string status)
        {_resumeGoalState=null;_resumeOwner=null;_conversationGoalStatus=status;}
        // Only the same authority token can abandon a deferred goal after a
        // source identity transition. This never requests a schedule change.
        public bool CancelConversationGoalHandoff(object token)
        {
            if(token==null||_talkOwner!=null||_resumeGoalState==null||!ReferenceEquals(_resumeOwner,token))return false;
            FinishConversationGoal("cancelled-by-authority");return true;
        }
        private void UpdateConversationGoalBoundary()
        {
            if (_resumeGoalState == null) return;
            if (!_owner || !ReferenceEquals(_resumeGoalState,_stateMachine.Current)) {FinishConversationGoal("owner-changed");return;}
            var agent=_owner.GetComponent<NavMeshAgent>();
            if(!agent||!agent.enabled||!agent.isOnNavMesh||agent.isOnOffMeshLink||Movement==null||!Movement.HasMoveGoal||
                Vector3.Distance(Movement.RequestedMoveGoal,_resumeGoal)>.001f)
            {FinishConversationGoal("original-goal-invalidated");return;}
            if(Movement.HasArrived()){FinishConversationGoal("original-goal-arrived");return;}
            if(!agent.pathPending&&(!agent.hasPath||agent.pathStatus!=NavMeshPathStatus.PathComplete))
            {FinishConversationGoal("original-path-unavailable");return;}
            float now=TalkActivityTime;
            if(Vector3.Distance(_owner.transform.position,_resumeProgressPosition)>=.1f)
            {_resumeProgressPosition=_owner.transform.position;_resumeProgressAt=now;}
            if(now-_resumeProgressAt>=10f){FinishConversationGoal("original-goal-no-progress");return;}
            if(now-_resumeStarted>=60f){FinishConversationGoal("original-goal-time-limit");return;}
        }

		private bool _modelVisibilityHeld;

		private bool _deferredModelVisible = true;

		private int _lastHour = 8;

		private int _lastMinute;

		private static float _debugSpeedMultiplier = 1f;

		public ResidentRuntimeState Runtime { get; private set; }

		public ResidentMovementController Movement { get; private set; }

		public ResidentStateMachine StateMachine => _stateMachine;
        public ResidentController Owner => _owner;
        public bool UsesLiveHomeBinding => true;

		public float EffectiveMoveSpeed => _moveSpeed * _debugSpeedMultiplier;

		public static float DebugSpeedMultiplier => _debugSpeedMultiplier;

		public ResidentBrain(ResidentController owner, ResidentDataSO residentData, GameObject model, ResidentMovementController movement, ResidentWaypointResolver waypoints)
		{
			_owner = owner;
			_residentData = residentData;
			_model = model;
			_moveSpeed = ((residentData != null && residentData.MoveSpeed > 0f) ? residentData.MoveSpeed : 3.5f);
			Movement = movement;
            if(Movement!=null)Movement.BeforeExternalControl += ConversationMovementControl;
			_waypoints = waypoints;
            _waypoints.BindActor(owner);
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
            _stateMachine.BeforeExternalControl += ConversationExternalTransfer;
			ForceResetToStart(evaluateSchedule: false);
			EvaluateSchedule(hour, minute, force: true);
		}

		public void Tick(float deltaTime)
		{
			if (!_paused && Runtime != null && !Runtime.IsSnapshotApplying && _talkOwner == null)
			{
                UpdateConversationGoalBoundary();
                _insideOriginalTick=true;
                try { _stateMachine.Tick(deltaTime); }
                finally { _insideOriginalTick=false; }
                UpdateConversationGoalBoundary();
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
            if (_talkOwner != null || _resumeGoalState != null) return;
			if (Runtime != null && !Runtime.IsInteracting)
			{
				Runtime.PendingScheduleEntryIndex = -1;
				EvaluateSchedule(_lastHour, _lastMinute, force: false);
			}
		}

		public void ForceResetToStart(bool evaluateSchedule = true)
		{
            ConversationExternalTransfer();
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
            ConversationExternalTransfer();
			_paused = true;
			Runtime.IsSnapshotApplying = true;
			Runtime.SystemState = ResidentSystemState.SnapshotApplying;
			_stateMachine.Suspend();
			Movement.Stop();
		}

		public void Resume()
		{
            ConversationExternalTransfer();
			Runtime.IsSnapshotApplying = false;
			Runtime.SystemState = ResidentSystemState.Normal;
			_stateMachine.Resume();
			_paused = false;
		}

		public void SetLocalSimulationEnabled(bool enabled)
		{
            ConversationExternalTransfer();
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
            // The original ambient restaurant must not bypass an added
            // friend's existing source body owner or finite activity request.
            if(SushiAndFish.FriendsLife.FriendResidentBindings.IsOwned(_owner))
            {
                var identity=_owner.GetComponent<SushiAndFish.FriendsLife.FriendWorldActor>();
                var runtime=SushiAndFish.FriendsLife.FriendsLifeRuntime.Instance;
                if(!identity||!runtime||!runtime.Ready||runtime.ActorOf(identity.personId)!=_owner||!runtime.IsWorldAvailable(identity.personId))return false;
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
            if (_talkOwner != null || _resumeGoalState != null) return false;
            if (SushiAndFish.FriendsLife.FriendsOriginalActivityTransit.SuppressScheduleDuringRelease(_owner)) return false;
			if (Runtime == null || Runtime.SystemState != ResidentSystemState.Normal)
			{
				return false;
			}
            // An explicit original-activity command (QA/player request) owns
            // its finite seat cycle until RequestSafeExit. A minute tick must
            // not replace it before the first original Sit transition occurs.
            if (_stateMachine.Current is ResidentSeatActivityState explicitSeat && !explicitSeat.SafeBoundary &&
                !explicitSeat.RequestOwner.StartsWith("schedule/",System.StringComparison.Ordinal)) return true;
			if (Runtime.CurrentActivity == ResidentActivityType.Restaurant || Runtime.IsInteracting)
			{
				Runtime.PendingScheduleEntryIndex = -2;
				return false;
			}
			if (!_schedule.TryFindActiveEntry(hour, minute, out var entryIndex, out var entry))
			{
                if(Runtime.IsSwimming&&_stateMachine.Current is ResidentMoveToScheduleTargetState endingSwim)
                {Runtime.PendingScheduleEntryIndex=-1;endingSwim.RequestOriginalGroundExit();return false;}
                if (_stateMachine.Current is ResidentSeatActivityState endingSeat && !endingSeat.SafeBoundary)
                { Runtime.PendingScheduleEntryIndex=-1;endingSeat.RequestExit(true);return false; }

				if (Runtime.CurrentScheduleEntryIndex == -1 && !force)
				{
					return true;
				}
				Runtime.CurrentScheduleEntryIndex = -1;
                return BeginReturnHome();
			}
            if(entry.Activity!=ResidentActivityType.SeaSwim&&Runtime.IsSwimming&&_stateMachine.Current is ResidentMoveToScheduleTargetState swimming)
            {Runtime.PendingScheduleEntryIndex=entryIndex;swimming.RequestOriginalGroundExit();return false;}
            if (_stateMachine.Current is ResidentSeatActivityState seated && !seated.SafeBoundary)
            {
                if (entryIndex == Runtime.CurrentScheduleEntryIndex && seated.Activity == entry.Activity) return true;
                Runtime.PendingScheduleEntryIndex=entryIndex;seated.RequestExit(true);return false;
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
            if(entry.Activity==ResidentActivityType.Home)return BeginReturnHome();
            if (OriginalNpcActivityPort.IsAuthoredActivity(entry.Activity))
            {
                if (!WaypointDirectory.TryGetAnchor(entry.Activity,entry.ActivityIndex,out var seatAnchor)||!seatAnchor)
                { ChangeToFallback("Original activity has no authored resource anchor");return false; }
                if (SushiAndFish.FriendsLife.FriendsOriginalActivityTransit.TryRoute(_owner,entry.Activity,entry.ActivityIndex,seatAnchor,"schedule/"+entryIndex,out bool transitAccepted,out _))return transitAccepted;
                _stateMachine.ChangeState(new ResidentSeatActivityState(this,_owner,entry.Activity,entry.ActivityIndex,seatAnchor,"schedule/"+entryIndex),ResidentRequestPriority.ScheduledActivity);
            }
            else
            {
                if(SushiAndFish.FriendsLife.FriendsOriginalActivityTransit.TryRouteResolvedSchedule(_owner,entry.Activity,entry.ActivityIndex,"schedule/"+entryIndex,out bool transitAccepted,out _))return transitAccepted;
			    _stateMachine.ChangeState(new ResidentMoveToScheduleTargetState(this, entry.Activity, EffectiveMoveSpeed), ResidentRequestPriority.ScheduledActivity);
            }
			ResidentDebugLogger.Log("Schedule changed. Activity=" + entry.Activity.ToString() + ", Index=" + entry.ActivityIndex);
			return true;
		}

        public bool IsOriginalScheduleRequestCurrent(ResidentActivityType activity,PaperPixelGames.Framework.Enums.WayPointIndex index,string requestOwner)
        {
            if(string.IsNullOrEmpty(requestOwner))return false;
            if(!requestOwner.StartsWith("schedule/",System.StringComparison.Ordinal))return true;
            int hour=(_talkOwner!=null||_resumeGoalState!=null)?_talkScheduleHour:_lastHour;
            int minute=(_talkOwner!=null||_resumeGoalState!=null)?_talkScheduleMinute:_lastMinute;
            return int.TryParse(requestOwner.Substring(9),out int expected)&&_schedule.TryFindActiveEntry(hour,minute,out int actual,out var current)&&
                actual==expected&&current.Activity==activity&&current.ActivityIndex==index;
        }

        public bool ContinueResolvedScheduleAfterPublicTransit(ResidentActivityType activity,PaperPixelGames.Framework.Enums.WayPointIndex index,string requestOwner,
            Vector3 authoredDestination,Vector3 rotation,int patrolIndex)
        {
            if(Runtime==null||Runtime.SystemState!=ResidentSystemState.Normal||!OriginalNpcActivityPort.CanAmbientOwn(_owner))return false;
            if(requestOwner.StartsWith("schedule/",System.StringComparison.Ordinal))
            {
                int hour=(_talkOwner!=null||_resumeGoalState!=null)?_talkScheduleHour:_lastHour;
                int minute=(_talkOwner!=null||_resumeGoalState!=null)?_talkScheduleMinute:_lastMinute;
                if(!int.TryParse(requestOwner.Substring(9),out int expected)||!_schedule.TryFindActiveEntry(hour,minute,out int actual,out var current)||
                    actual!=expected||current.Activity!=activity||current.ActivityIndex!=index)
                {RequestScheduleChange();return true;}
                Runtime.CurrentScheduleEntryIndex=expected;
            }
            Runtime.TargetDestination=authoredDestination;Runtime.TargetRotation=rotation;Runtime.CurrentWaypointIndex=index;
            Runtime.CurrentPatrolEntryPointIndex=patrolIndex;Runtime.HasValidTarget=true;Runtime.HasArrived=false;
            if(OriginalNpcActivityPort.IsAuthoredActivity(activity))return OriginalNpcActivityPort.TryBeginActivity(_owner,activity,index,requestOwner,out _);
            return _stateMachine.ChangeState(new ResidentMoveToScheduleTargetState(this,activity,EffectiveMoveSpeed),ResidentRequestPriority.ScheduledActivity);
        }

        // The authored seat/spot of this schedule slot is unavailable (owned
        // by another body, unreachable, or failed). Keep the resident walking
        // the original near-city patrol for the remainder of the slot instead
        // of standing still; the next schedule entry resumes normally.
        public bool BeginAuthoredActivityFallback(ResidentActivityType activity,string reason)
        {
            if(Runtime==null||Runtime.SystemState!=ResidentSystemState.Normal||_paused||Runtime.IsSnapshotApplying||Runtime.IsInteracting||
                Runtime.IsInRestaurant||Runtime.HasRestaurantAssignment||Runtime.IsSwimming||Runtime.IsWorking||
                SushiAndFish.FriendsLife.FriendsOriginalActivityTransit.SuppressScheduleDuringRelease(_owner)||
                !OriginalNpcActivityPort.CanAmbientOwn(_owner))return false;
            Runtime.CurrentPatrolEntryPointIndex=-1;
            if(!TryAdvancePatrolTarget(ResidentActivityType.CityPatrolNear))return false;
            bool changed=_stateMachine.ChangeState(new ResidentMoveToScheduleTargetState(this,ResidentActivityType.CityPatrolNear,EffectiveMoveSpeed),ResidentRequestPriority.ScheduledActivity);
            if(changed)ResidentDebugLogger.Log("Authored activity "+activity+" unavailable, patrolling instead: "+reason,LogType.Warning);
            return changed;
        }

        bool BeginReturnHome()
        {
            if(!_waypoints.ResolveHome(out var home,out var rotation))
            {Runtime.HasValidTarget=false;ChangeToFallback("The current actual home approach is unavailable");return false;}
            var identity=_owner?_owner.GetComponent<SushiAndFish.FriendsLife.FriendWorldActor>():null;
            var runtime=SushiAndFish.FriendsLife.FriendsLifeRuntime.Instance;
            if(identity&&runtime&&SushiAndFish.FriendsLife.FriendsPrivateCommonTransit.IsInOwnPrivateRoom(runtime,identity.personId))
            {
                var nav=_owner.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if(nav&&nav.enabled&&nav.isOnNavMesh&&Vector3.Distance(_owner.transform.position,nav.nextPosition)<=.25f&&OriginalNpcActivityPort.FootFits(_owner,nav.nextPosition))
                {home=nav.nextPosition;rotation=_owner.transform.eulerAngles;}
            }
            Runtime.TargetDestination=home;Runtime.TargetRotation=rotation;Runtime.HasValidTarget=true;Runtime.HasArrived=false;
            return _stateMachine.ChangeState(new ResidentReturnHomeState(this,EffectiveMoveSpeed),ResidentRequestPriority.ScheduledActivity);
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
			_stateMachine.ChangeState(new ResidentReturnHomeState(this, EffectiveMoveSpeed), ResidentRequestPriority.ScheduledActivity);
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
