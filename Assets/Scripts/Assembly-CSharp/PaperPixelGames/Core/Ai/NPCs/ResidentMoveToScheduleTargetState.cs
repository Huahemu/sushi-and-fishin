using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentMoveToScheduleTargetState : ResidentBaseState
	{
		private readonly ResidentActivityType _activity;

		private readonly float _speed;

		private bool _moveStarted;

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
			_moveStarted = Runtime.HasValidTarget && Brain.Movement.MoveTo(Runtime.TargetDestination, _speed, Brain.GetDestinationSampleRadius(_activity));
			if (!_moveStarted)
			{
				Brain.ChangeToFallback("MoveTo failed for " + _activity);
			}
		}

		public override void Tick(float deltaTime)
		{
			if (!_moveStarted)
			{
				return;
			}
			bool flag = UpdateSeaSwimTransition(deltaTime);
			if (!Brain.Movement.HasArrived() || (_activity == ResidentActivityType.SeaSwim && !flag))
			{
				return;
			}
			Runtime.HasArrived = true;
			Brain.Movement.StopAtPose(Runtime.TargetDestination, Quaternion.Euler(Runtime.TargetRotation));
			if (ResidentBrain.IsPatrolActivity(_activity))
			{
				if (Brain.TryAdvancePatrolTarget(_activity))
				{
					Runtime.HasArrived = false;
					if (_activity == ResidentActivityType.SeaSwim)
					{
						BeginHeightBlendIfNeeded();
					}
					_moveStarted = Brain.Movement.MoveTo(Runtime.TargetDestination, _speed, Brain.GetDestinationSampleRadius(_activity));
					if (!_moveStarted)
					{
						Brain.ChangeToFallback("Patrol MoveTo failed for " + _activity);
					}
				}
				else
				{
					Brain.ChangeToFallback("Missing patrol target for " + _activity);
				}
			}
			else if (_activity == ResidentActivityType.Work)
			{
				Brain.StateMachine.ChangeState(new ResidentWorkState(Brain), ResidentRequestPriority.ScheduledActivity);
			}
		}

		public override string GetDebugName()
		{
			return "MoveTo:" + _activity;
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
