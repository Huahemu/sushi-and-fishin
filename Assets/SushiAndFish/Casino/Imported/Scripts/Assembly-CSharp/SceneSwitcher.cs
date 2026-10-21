namespace SushiAndFish.Casino.Original
{
using SushiAndFish.Casino.Original.Extensions;
using global::Mirror;
using global::UnityEngine;

public class SceneSwitcher : NetworkBehaviour
{
	[SerializeField]
	private GameState target;

	[SerializeField]
	private bool isInteractable = true;

	[Server]
	public void ServerProgressSceneWithoutInteraction()
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[Server] function 'System.Void SceneSwitcher::ServerProgressSceneWithoutInteraction()' called when server was not active");
		}
		else if (isInteractable)
		{
			isInteractable = false;
			if (NetworkSingleton<GameManager>.Instance.state == GameState.Game)
			{
				NetworkSingleton<GameManager>.Instance.ShowDayStats();
			}
			else if (NetworkSingleton<GameManager>.Instance.state == GameState.Lose)
			{
				NetworkSingleton<GameManager>.Instance.ShowGameOverStats();
			}
			else
			{
				NetworkSingleton<GameManager>.Instance.ProgressGame();
			}
		}
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
