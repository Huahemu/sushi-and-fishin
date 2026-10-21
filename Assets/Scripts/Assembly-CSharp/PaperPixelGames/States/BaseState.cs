using PaperPixelGames.Framework.States;

namespace PaperPixelGames.States
{
	public abstract class BaseState : IState
	{
		public abstract void LoadScene();

		public virtual void EnterState()
		{
		}

		public virtual void UpdateState()
		{
		}

		public virtual void LateUpdateState()
		{
		}

		public virtual void ExitState()
		{
		}
	}
}
