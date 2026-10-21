namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentIdleState : ResidentBaseState
	{
		public ResidentIdleState(ResidentBrain brain)
			: base(brain)
		{
		}

		public override void Enter()
		{
			Runtime.SetActivity(ResidentActivityType.Idle);
			Runtime.IsAvailable = true;
			Runtime.IsWorking = false;
			Runtime.IsSwimming = false;
			Runtime.HasArrived = true;
			Brain.SetModelVisible(visible: true);
			Brain.Movement.Stop();
		}

		public override string GetDebugName()
		{
			return "Idle";
		}
	}
}
