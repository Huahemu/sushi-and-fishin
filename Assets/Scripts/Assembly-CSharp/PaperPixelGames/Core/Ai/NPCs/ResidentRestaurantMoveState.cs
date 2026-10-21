namespace PaperPixelGames.Core.Ai.NPCs
{
	public sealed class ResidentRestaurantMoveState : ResidentBaseState
	{
		public ResidentRestaurantMoveState(ResidentBrain brain)
			: base(brain)
		{
		}

		public override void Enter()
		{
			Runtime.SetActivity(ResidentActivityType.Restaurant);
			Runtime.IsAvailable = false;
			Runtime.IsWorking = false;
			Runtime.IsSwimming = false;
			Runtime.HasArrived = false;
			Runtime.IsInRestaurant = false;
			Brain.SetModelVisible(visible: true);
		}

		public override string GetDebugName()
		{
			return "RestaurantMove";
		}
	}
}
