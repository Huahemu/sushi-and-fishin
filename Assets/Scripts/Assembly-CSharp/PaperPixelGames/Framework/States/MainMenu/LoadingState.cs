using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.SceneManagement;
using PaperPixelGames.Framework.Utility;
using PaperPixelGames.Networking;
using PaperPixelGames.States;

namespace PaperPixelGames.Framework.States.MainMenu
{
	public class LoadingState : BaseState
	{
		public override void LoadScene()
		{
			Singleton<PPGSceneManager>.Instance.LoadScene("LoadingState");
		}

		public override void EnterState()
		{
			UtilityMethods.Log("[LoadingState] Entered Loading State");
			Singleton<MultiplayerCoordinator>.Instance.ContinuePendingLaunchFromLoadingState();
		}

		public override void ExitState()
		{
		}
	}
}
