using UnityEngine;

namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentFallbackIdleState : ResidentBaseState
	{
		private readonly string _reason;

		public ResidentFallbackIdleState(ResidentBrain brain, string reason)
			: base(brain)
		{
			_reason = reason;
		}

		public override void Enter()
		{
			Runtime.SetActivity(ResidentActivityType.Idle);
			Runtime.IsAvailable = false;
			Runtime.IsWorking = false;
			Runtime.IsSwimming = false;
			Runtime.HasArrived = true;
			Runtime.HasValidTarget = false;
			Brain.Movement.Stop();
			Brain.SetModelVisible(visible: true);
			ResidentDebugLogger.Log("Fallback idle: " + _reason, LogType.Warning);
			_retryAt = Brain.TalkActivityTime + RetrySeconds;
		}

		// Fallback used to last until the next schedule entry (hours of game
		// time standing still). Re-evaluate the same schedule periodically and,
		// if it still cannot start, walk the original near-city patrol.
		private const float RetrySeconds = 15f;
		private float _retryAt;

		public override void Tick(float deltaTime)
		{
			if (Brain.TalkActivityTime < _retryAt) return;
			_retryAt = Brain.TalkActivityTime + RetrySeconds;
			Brain.RequestScheduleChange();
			if (Brain.StateMachine.Current is ResidentFallbackIdleState)
				Brain.BeginAuthoredActivityFallback(ResidentActivityType.Idle, _reason);
		}

		public override string GetDebugName()
		{
			return "FallbackIdle";
		}
	}
}
