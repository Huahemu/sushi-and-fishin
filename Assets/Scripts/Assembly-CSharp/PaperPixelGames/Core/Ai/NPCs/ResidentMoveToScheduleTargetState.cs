using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentMoveToScheduleTargetState : ResidentBaseState
	{
		private readonly ResidentActivityType _activity;

		private readonly float _speed;

		private bool _moveStarted;
        private bool _groundExitRequested,_groundExitBlending;
        private float _groundExitElapsed,_groundExitStartY;
        private float _nextOriginalPathRetry;
        // A rejected complete path must not freeze the resident for the whole
        // schedule slot. After a grace period restore the pre-merge original
        // behaviour: walk the nearest reachable part of the path.
        private const float PathFailureGraceSeconds=6f;
        private float _pathFailingSince=-1f;
        private bool _partialPath;
        private Vector3[] _workHousingTargets;
        private int _workHousingIndex;
        private float _nextWorkHousingPlan;

        bool RestoreCurrentOriginalWorkTarget()
        {
            if(!Brain.IsOriginalScheduleRequestCurrent(ResidentActivityType.Work,Runtime.CurrentWaypointIndex,"schedule/"+Runtime.CurrentScheduleEntryIndex))return false;
            if(!WaypointDirectory.TryGetAnchor(ResidentActivityType.Work,Runtime.CurrentWaypointIndex,out var anchor)||!anchor)return false;
            var agent=Brain.Owner?Brain.Owner.GetComponent<UnityEngine.AI.NavMeshAgent>():null;
            if(!agent||!anchor.TryGetActivityApproach(agent,out var approach,out _))return false;
            Runtime.TargetDestination=anchor.UseAuthoredPose?anchor.transform.position:approach;
            Runtime.TargetRotation=anchor.transform.eulerAngles;Runtime.HasValidTarget=true;Runtime.HasArrived=false;
            _workHousingTargets=null;_workHousingIndex=0;return true;
        }
        bool RejectWorkHousingPlan()
        {
            _workHousingTargets=null;_workHousingIndex=0;
            _nextWorkHousingPlan=Brain.TalkActivityTime+2f;
            if(_pathFailingSince<0)_pathFailingSince=Brain.TalkActivityTime;
            var agent=Brain.Owner?Brain.Owner.GetComponent<UnityEngine.AI.NavMeshAgent>():null;
            if(agent&&agent.enabled&&agent.isOnNavMesh)Brain.Movement.Stop();
            return false;
        }
        bool StartWorkHousingLeg()
        {
            if(_workHousingTargets==null||_workHousingIndex<0||_workHousingIndex>=_workHousingTargets.Length||
                !SushiAndFish.FriendsLife.FriendWorkHousingRoute.HasCurrentBodyFoot(Brain.Owner))return RejectWorkHousingPlan();
            var target=_workHousingTargets[_workHousingIndex];
            if(!SushiAndFish.FriendsLife.FriendWorkHousingRoute.LegStillPublic(Brain.Owner,target))return RejectWorkHousingPlan();
            _partialPath=false;
            if(!Brain.Movement.MoveTo(target,_speed,Brain.GetDestinationSampleRadius(_activity),true,false))return RejectWorkHousingPlan();
            _pathFailingSince=-1f;_nextWorkHousingPlan=0;
            return true;
        }


        private bool TryStartMove()
        {
            bool swim=_activity==ResidentActivityType.SeaSwim;
            _partialPath=false;
            if(_activity==ResidentActivityType.Work&&!SushiAndFish.FriendsLife.FriendResidentBindings.IsOwned(Brain.Owner))
            {
                if(Brain.TalkActivityTime<_nextWorkHousingPlan)return false;
                if(SushiAndFish.FriendsLife.FriendWorkHousingRoute.HasPrivateVolumes(Brain.Owner))
                {
                    // Active private volumes always require complete public
                    // legs. No planner failure can reach the legacy partial TP.
                    if(!Runtime.HasValidTarget&&!RestoreCurrentOriginalWorkTarget())return RejectWorkHousingPlan();
                    if(!SushiAndFish.FriendsLife.FriendWorkHousingRoute.HasCurrentBodyFoot(Brain.Owner))return RejectWorkHousingPlan();
                    if(_workHousingTargets!=null)return StartWorkHousingLeg();
                    bool planned=SushiAndFish.FriendsLife.FriendWorkHousingRoute.TryPlan(Brain.Owner,Runtime.TargetDestination,out var targets,out bool guarded);
                    if(!planned||!guarded||targets==null||targets.Length==0)return RejectWorkHousingPlan();
                    _workHousingTargets=targets;_workHousingIndex=0;
                    return StartWorkHousingLeg();
                }
                _workHousingTargets=null;_workHousingIndex=0; // No privacy: existing behaviour below.
            }
            bool ok=Runtime.HasValidTarget&&Brain.Movement.MoveTo(Runtime.TargetDestination,_speed,Brain.GetDestinationSampleRadius(_activity),true,swim);
            if(!ok&&!swim&&Runtime.HasValidTarget&&_pathFailingSince>=0&&Brain.TalkActivityTime-_pathFailingSince>=PathFailureGraceSeconds)
            {
                Brain.Movement.TryReattachDetachedBody(1.5f);
                ok=Brain.Movement.MoveTo(Runtime.TargetDestination,_speed,Brain.GetDestinationSampleRadius(_activity),false,false);
                _partialPath=ok;
            }
            if(ok)_pathFailingSince=-1f;else if(_pathFailingSince<0)_pathFailingSince=Brain.TalkActivityTime;
            return ok;
        }

		private bool _heightBlendActive;

		private float _heightBlendElapsed;

		private float _heightBlendStartY;

		private float _heightBlendTargetY;

		public ResidentMoveToScheduleTargetState(ResidentBrain brain, ResidentActivityType activity, float speed)
			: base(brain)
		{
			_activity = activity;
			_speed = speed;
		}

		public override void Enter()
		{
			Runtime.SetActivity(_activity);
			Runtime.IsWorking = false;
			Runtime.IsSwimming = false;
			Runtime.IsAvailable = !Runtime.IsWorking && !Runtime.IsSwimming && _activity != ResidentActivityType.Home && _activity != ResidentActivityType.Restaurant;
			Runtime.HasArrived = false;
			ResetHeightBlend();
			Brain.SetModelVisible(_activity != ResidentActivityType.Home);
			_moveStarted = TryStartMove();
            // Tick re-plans; an immediate FallbackIdle stood the body still
            // until the next schedule entry.
            if (!_moveStarted) _nextOriginalPathRetry=Brain.TalkActivityTime+.5f;
		}

		public override void Tick(float deltaTime)
		{
            if(UpdateOriginalGroundExit(deltaTime))return;
            var actualNav=Brain.Owner?Brain.Owner.GetComponent<UnityEngine.AI.NavMeshAgent>():null;
            if(_workHousingTargets!=null&&(!actualNav||!actualNav.enabled||!actualNav.isOnNavMesh||
                Vector3.Distance(Brain.Owner.transform.position,actualNav.nextPosition)>.25f))
            {
                _moveStarted=false;RejectWorkHousingPlan();_nextOriginalPathRetry=_nextWorkHousingPlan;return;
            }
            if(_moveStarted&&!_partialPath&&actualNav&&actualNav.enabled&&actualNav.isOnNavMesh&&!actualNav.pathPending&&
                (actualNav.pathStatus!=UnityEngine.AI.NavMeshPathStatus.PathComplete||!actualNav.hasPath&&!Brain.Movement.HasArrived(_activity==ResidentActivityType.SeaSwim)))_moveStarted=false;
            if(!_moveStarted)
            {
                // Swimming cannot be replaced by fallback while the visual
                // height is detached. Re-plan THIS SAME original patrol goal,
                // without advancing the source patrol index or clearing Swim.
                if(_activity==ResidentActivityType.SeaSwim&&Runtime.IsSwimming)UpdateSeaSwimTransition(deltaTime);
                if(Brain.TalkActivityTime>=_nextOriginalPathRetry)
                {
                    _nextOriginalPathRetry=Brain.TalkActivityTime+.5f;
                    // An unreachable land patrol point is skipped (same original
                    // patrol list) instead of being retried forever.
                    if(_activity!=ResidentActivityType.SeaSwim&&ResidentBrain.IsPatrolActivity(_activity)&&(!Runtime.HasValidTarget||_pathFailingSince>=0))
                        Brain.TryAdvancePatrolTarget(_activity);
                    _moveStarted=TryStartMove();
                }
                if(!_moveStarted)return;
            }
			bool flag = UpdateSeaSwimTransition(deltaTime);
			bool arrived=_partialPath?Brain.Movement.HasReachedPathEnd():Brain.Movement.HasArrived(_activity==ResidentActivityType.SeaSwim);
			if (!arrived || (_activity == ResidentActivityType.SeaSwim && !flag))
			{
				return;
			}
            if(_workHousingTargets!=null)
            {
                if(_workHousingIndex+1<_workHousingTargets.Length)
                {
                    _workHousingIndex++;_moveStarted=StartWorkHousingLeg();
                    if(!_moveStarted)_nextOriginalPathRetry=Brain.TalkActivityTime+.5f;
                    return; // Intermediate public turns are not arrival at Work.
                }
                _workHousingTargets=null;_workHousingIndex=0;
            }
            bool reachedPartialEnd=_partialPath;
            _partialPath=false;
			Runtime.HasArrived = true;
            // Pre-merge original placed a Work arrival at its authored post
            // (StopAtPose); keep that for a partial path so a shopkeeper whose
            // route is cut still staffs the shop.
            if(reachedPartialEnd&&_activity==ResidentActivityType.Work)
                Brain.Movement.StopAtPose(Runtime.TargetDestination,Quaternion.Euler(Runtime.TargetRotation));
            else
            Brain.Movement.StopAtRotation(Quaternion.Euler(Runtime.TargetRotation));
            if(_activity==ResidentActivityType.SeaSwim)
                Brain.Movement.MatchAgentPositionAtHeight(Runtime.TargetDestination.y);
			if (ResidentBrain.IsPatrolActivity(_activity))
			{
				if (Brain.TryAdvancePatrolTarget(_activity))
				{
					Runtime.HasArrived = false;
					if (_activity == ResidentActivityType.SeaSwim)
					{
						BeginHeightBlendIfNeeded();
					}
					_moveStarted = TryStartMove();
					if (!_moveStarted) _nextOriginalPathRetry=Brain.TalkActivityTime+.5f;
				}
				else
				{
					// Next patrol point could not be sampled this frame; Tick
					// retries the same patrol list instead of FallbackIdle.
					_moveStarted=false;_nextOriginalPathRetry=Brain.TalkActivityTime+.5f;
				}
			}
			else if (_activity == ResidentActivityType.Work)
			{
				Brain.StateMachine.ChangeState(new ResidentWorkState(Brain), ResidentRequestPriority.ScheduledActivity);
			}
		}

        public void RequestOriginalGroundExit()
        {if(_activity==ResidentActivityType.SeaSwim&&Runtime.IsSwimming)_groundExitRequested=true;}

        bool UpdateOriginalGroundExit(float deltaTime)
        {
            if(!_groundExitRequested)return false;
            var nav=Brain.Owner?Brain.Owner.GetComponent<UnityEngine.AI.NavMeshAgent>():null;
            if(!nav||!nav.enabled||!nav.isOnNavMesh)return true;
            Vector3 body=Brain.Movement.Position,ground=nav.nextPosition;
            if(Vector2.Distance(new Vector2(body.x,body.z),new Vector2(ground.x,ground.z))>.25f)return true;
            // Continue the original patrol until its actual navigation foot is
            // supported and the real capsule fits; never relocate to the shore.
            if(!_groundExitBlending)
            {
                if(!OriginalNpcActivityPort.FootFits(Brain.Owner,ground))return false;
                Brain.Movement.Stop();_groundExitStartY=body.y;_groundExitElapsed=0;_groundExitBlending=true;Runtime.HasArrived=false;
            }
            if(!OriginalNpcActivityPort.FootFits(Brain.Owner,ground))return true;
            _groundExitElapsed+=deltaTime;
            float fraction=Mathf.Clamp01(_groundExitElapsed/Brain.GetSeaSwimHeightBlendDuration());
            Brain.Movement.MatchAgentPositionAtHeight(Mathf.Lerp(_groundExitStartY,ground.y,Mathf.SmoothStep(0,1,fraction)));
            if(fraction<1||Vector3.Distance(Brain.Movement.Position,nav.nextPosition)>.15f)return true;
            Runtime.IsSwimming=false;Runtime.HasArrived=false;_groundExitRequested=false;_groundExitBlending=false;
            Brain.RequestScheduleChange();return true;
        }

        public override bool CanInterrupt(ResidentRequestPriority priority)
            =>!Runtime.IsSwimming||priority==ResidentRequestPriority.HardReset||priority==ResidentRequestPriority.SnapshotApplying;

		public override string GetDebugName()
		{
			return "MoveTo:"+_activity+(!_moveStarted?":WaitingForOriginalPath":"");
		}

		private bool UpdateSeaSwimTransition(float deltaTime)
		{
			if (_activity != ResidentActivityType.SeaSwim)
			{
				return true;
			}
			if (!Runtime.IsSwimming && IsInsideSeaSwimTransitionRadius())
			{
				Runtime.IsSwimming = true;
				BeginHeightBlendIfNeeded();
			}
			if (!Runtime.IsSwimming)
			{
				return false;
			}
			if (_heightBlendActive)
			{
				_heightBlendElapsed += deltaTime;
				float seaSwimHeightBlendDuration = Brain.GetSeaSwimHeightBlendDuration();
				float num = Mathf.Clamp01(_heightBlendElapsed / seaSwimHeightBlendDuration);
				float t = Mathf.SmoothStep(0f, 1f, num);
				float height = Mathf.Lerp(_heightBlendStartY, _heightBlendTargetY, t);
				Brain.Movement.MatchAgentPositionAtHeight(height);
				if (num < 1f)
				{
					return false;
				}
				_heightBlendActive = false;
			}
			Brain.Movement.MatchAgentPositionAtHeight(Runtime.TargetDestination.y);
			return true;
		}

		private bool IsInsideSeaSwimTransitionRadius()
		{
			Vector3 position = Brain.Movement.Position;
			Vector3 targetDestination = Runtime.TargetDestination;
			Vector2 a = new Vector2(position.x, position.z);
			Vector2 b = new Vector2(targetDestination.x, targetDestination.z);
			return Vector2.Distance(a, b) <= Brain.GetSeaSwimTransitionRadius();
		}

		private void BeginHeightBlendIfNeeded()
		{
			float y = Brain.Movement.Position.y;
			float y2 = Runtime.TargetDestination.y;
			if (Mathf.Approximately(y, y2))
			{
				ResetHeightBlend();
				return;
			}
			_heightBlendActive = true;
			_heightBlendElapsed = 0f;
			_heightBlendStartY = y;
			_heightBlendTargetY = y2;
		}

		private void ResetHeightBlend()
		{
			_heightBlendActive = false;
			_heightBlendElapsed = 0f;
			_heightBlendStartY = 0f;
			_heightBlendTargetY = 0f;
		}
	}
}
