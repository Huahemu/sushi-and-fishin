namespace PaperPixelGames.Framework.States
{
	public interface IState
	{
		void LoadScene();

		void EnterState();

		void UpdateState();

		void LateUpdateState();

		void ExitState();
	}
}
