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
		}

		public override string GetDebugName()
		{
			return "FallbackIdle";
		}
	}
}
