using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentReturnHomeState : ResidentBaseState
	{
		private readonly float _speed;

		private bool _moveStarted;

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
			_moveStarted = Runtime.HasValidTarget && Brain.Movement.MoveTo(Runtime.TargetDestination, _speed, Brain.GetDestinationSampleRadius(ResidentActivityType.Home));
			if (!_moveStarted)
			{
				Brain.StateMachine.ChangeState(new ResidentHomeState(Brain), ResidentRequestPriority.ScheduledActivity);
			}
		}

		public override void Tick(float deltaTime)
		{
			if (_moveStarted && Brain.Movement.HasArrived())
			{
				Runtime.HasArrived = true;
				Brain.Movement.StopAtPose(Runtime.TargetDestination, Quaternion.Euler(Runtime.TargetRotation));
				Brain.StateMachine.ChangeState(new ResidentHomeState(Brain), ResidentRequestPriority.ScheduledActivity);
			}
		}

		public override string GetDebugName()
		{
			return "ReturnHome";
		}
	}
}
