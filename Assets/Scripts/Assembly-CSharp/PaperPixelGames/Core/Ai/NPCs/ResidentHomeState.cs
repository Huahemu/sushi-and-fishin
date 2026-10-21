namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentHomeState : ResidentBaseState
	{
		public ResidentHomeState(ResidentBrain brain)
			: base(brain)
		{
		}

		public override void Enter()
		{
			Runtime.SetActivity(ResidentActivityType.Home);
			Runtime.IsAvailable = false;
			Runtime.IsWorking = false;
			Runtime.IsSwimming = false;
			Runtime.HasArrived = true;
			Runtime.HasValidTarget = false;
			Brain.SetModelVisible(visible: false);
			Brain.Movement.Stop();
		}

		public override string GetDebugName()
		{
			return "Home";
		}
	}
}
