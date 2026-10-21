namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;

public class QuitGameComponent : MonoBehaviour
{
	public void QuitGame()
	{
		Application.Quit();
	}

	public void QuitGameWithConfirmation()
	{
		if (MonoSingleton<ConfirmationDialogManager>.Instance != null)
		{
			MonoSingleton<ConfirmationDialogManager>.Instance.ShowConfirmation("Are you sure you want to quit?", () =>
			{
				StartCoroutine(QuitGameCoroutine());
			}, () =>
			{
				Debug.Log("[QuitGameComponent] User cancelled quitting game.");
			}, "Yes, quit", "No, stay");
		}
		else
		{
			Debug.LogWarning("[QuitGameComponent] ConfirmationDialogManager not found. Quitting without confirmation.");
			QuitGame();
		}
	}

	private IEnumerator QuitGameCoroutine()
	{
		MonoSingleton<SceneTransitioner>.Instance.SetLoadingScreen(isEnabled: true, 0.5f, loadingScreen: false);
		yield return new WaitForSeconds(1f);
		QuitGame();
	}
}

}
