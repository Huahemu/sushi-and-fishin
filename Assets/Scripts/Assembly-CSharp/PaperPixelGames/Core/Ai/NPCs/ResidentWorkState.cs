namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentWorkState : ResidentBaseState
	{
		public ResidentWorkState(ResidentBrain brain)
			: base(brain)
		{
		}

		public override void Enter()
		{
			Runtime.SetActivity(ResidentActivityType.Work);
			Runtime.IsWorking = true;
			Runtime.IsSwimming = false;
			Runtime.IsAvailable = false;
			Runtime.HasArrived = true;
			Brain.Movement.Stop();
			Brain.SetModelVisible(visible: true);
		}

		public override void Exit()
		{
			Runtime.IsWorking = false;
		}

		public override string GetDebugName()
		{
			return "Work";
		}
	}
}
