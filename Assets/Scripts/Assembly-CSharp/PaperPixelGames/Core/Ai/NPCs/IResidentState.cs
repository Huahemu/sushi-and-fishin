namespace PaperPixelGames.Core.Ai.NPCs
{
	public interface IResidentState
	{
		void Enter();

		void Tick(float deltaTime);

		void Exit();

		bool CanInterrupt(ResidentRequestPriority priority);

		string GetDebugName();
	}
}
