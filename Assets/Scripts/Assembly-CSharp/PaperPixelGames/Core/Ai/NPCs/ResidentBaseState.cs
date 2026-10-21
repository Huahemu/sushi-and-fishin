namespace PaperPixelGames.Core.Ai.NPCs
{
	public abstract class ResidentBaseState : IResidentState
	{
		protected readonly ResidentBrain Brain;

		protected readonly ResidentRuntimeState Runtime;

		protected ResidentBaseState(ResidentBrain brain)
		{
			Brain = brain;
			Runtime = brain.Runtime;
		}

		public abstract void Enter();

		public virtual void Tick(float deltaTime)
		{
		}

		public virtual void Exit()
		{
		}

		public virtual bool CanInterrupt(ResidentRequestPriority priority)
		{
			return true;
		}

		public abstract string GetDebugName();
	}
}
