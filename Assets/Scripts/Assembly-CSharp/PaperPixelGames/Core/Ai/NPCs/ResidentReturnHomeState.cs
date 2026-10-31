using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentReturnHomeState : ResidentBaseState
	{
		private readonly float _speed;

		private bool _moveStarted;
        private float _nextPathRetry;
        // Pre-merge original: a rejected walk home went straight to Home.
        // Keep the complete-path walk first, then the original partial walk,
        // and finally the original Home fallback instead of standing forever.
        private const float PathFailureGraceSeconds=6f,HomeFallbackSeconds=20f;
        private float _pathFailingSince=-1f;
        private bool _partialPath;
        private bool TryStartMove()
        {
            float radius=Brain.GetDestinationSampleRadius(ResidentActivityType.Home);_partialPath=false;
            bool ok=Runtime.HasValidTarget&&Brain.Movement.MoveTo(Runtime.TargetDestination,_speed,radius,true);
            if(!ok&&Runtime.HasValidTarget&&_pathFailingSince>=0&&Brain.TalkActivityTime-_pathFailingSince>=PathFailureGraceSeconds)
            {
                Brain.Movement.TryReattachDetachedBody(1.5f);
                ok=Brain.Movement.MoveTo(Runtime.TargetDestination,_speed,radius,false);_partialPath=ok;
            }
            if(ok)_pathFailingSince=-1f;else if(_pathFailingSince<0)_pathFailingSince=Brain.TalkActivityTime;
            return ok;
        }

		public ResidentReturnHomeState(ResidentBrain brain, float speed)
			: base(brain)
		{
			_speed = speed;
		}

		public override void Enter()
		{
			Runtime.SetActivity(ResidentActivityType.Idle);
			Runtime.IsAvailable = false;
			Runtime.IsWorking = false;
			Runtime.IsSwimming = false;
			Runtime.HasArrived = false;
			Brain.SetModelVisible(visible: true);
			_moveStarted = TryStartMove();
            _nextPathRetry=Brain.TalkActivityTime+.5f;
		}

		public override void Tick(float deltaTime)
		{
            var agent=Brain.Owner?Brain.Owner.GetComponent<UnityEngine.AI.NavMeshAgent>():null;
            bool arrived=_moveStarted&&(_partialPath?Brain.Movement.HasReachedPathEnd():Brain.Movement.HasArrived());
            bool lost=_moveStarted&&!_partialPath&&agent&&agent.enabled&&agent.isOnNavMesh&&!agent.pathPending&&
                (agent.pathStatus!=UnityEngine.AI.NavMeshPathStatus.PathComplete||!agent.hasPath&&!arrived);
            if((!_moveStarted||lost)&&Brain.TalkActivityTime>=_nextPathRetry)
            {
                _nextPathRetry=Brain.TalkActivityTime+.5f;
                _moveStarted=TryStartMove();
                if(!_moveStarted&&_pathFailingSince>=0&&Brain.TalkActivityTime-_pathFailingSince>=HomeFallbackSeconds)
                {Brain.StateMachine.ChangeState(new ResidentHomeState(Brain),ResidentRequestPriority.ScheduledActivity);return;}
                arrived=_moveStarted&&(_partialPath?Brain.Movement.HasReachedPathEnd():Brain.Movement.HasArrived());
            }
			if (arrived)
			{
				Runtime.HasArrived = true;
				Brain.Movement.StopAtRotation(Quaternion.Euler(Runtime.TargetRotation));
				Brain.StateMachine.ChangeState(new ResidentHomeState(Brain), ResidentRequestPriority.ScheduledActivity);
			}
		}

		public override string GetDebugName()
		{
			return _moveStarted?"ReturnHome":"ReturnHome:WaitingForOriginalPath";
		}
	}
}
