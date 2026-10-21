namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;
using global::UnityEngine.SceneManagement;

public class TransitionAnimator : MonoSingleton<TransitionAnimator>
{
	[SerializeField]
	private GameObject fadeImage;

	public Animator transition;

	public bool isTransitioning;

	public bool isBlackScreen;

	private void Start()
	{
		fadeImage.SetActive(value: true);
	}

	public void LoadGame(int levelIndex)
	{
		if (!isTransitioning)
		{
			isTransitioning = true;
			StartCoroutine(LoadLevel(levelIndex));
		}
	}

	private IEnumerator LoadLevel(int levelIndex)
	{
		transition.SetTrigger("START");
		yield return new WaitForSeconds(1f);
		AsyncOperation operation = SceneManager.LoadSceneAsync(levelIndex);
		while (!operation.isDone)
		{
			Mathf.Clamp01(operation.progress / 0.9f);
			yield return null;
		}
		isTransitioning = false;
		FadeIn();
	}

	public void FadeOut()
	{
		transition.SetTrigger("START");
		isBlackScreen = true;
	}

	public void FadeIn()
	{
		transition.SetTrigger("END");
		isBlackScreen = false;
	}
}

}
