namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;
using global::UnityEngine.InputSystem;
using global::UnityEngine.SceneManagement;

public class SceneSkipper : MonoBehaviour
{
	[SerializeField]
	private string mainMenuSceneName = "MainMenuScene";

	[SerializeField]
	private float transitionDuration = 0.5f;

	public bool ignoreInput;

	private bool _isSkipping;

	private void OnEnable()
	{
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	private void OnDisable()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private void Start()
	{
		StartCoroutine(SkipSceneRoutine());
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		_isSkipping = false;
		if (MonoSingleton<SceneTransitioner>.Instance != null)
		{
			MonoSingleton<SceneTransitioner>.Instance.SetLoadingScreen(isEnabled: false, transitionDuration, loadingScreen: false);
		}
		if (!string.IsNullOrEmpty(mainMenuSceneName) && scene.name == mainMenuSceneName)
		{
			Object.Destroy(base.gameObject);
		}
		StartCoroutine(SkipSceneRoutine());
	}

	private IEnumerator SkipSceneRoutine()
	{
		yield return new WaitForSeconds(3f);
		if (!ignoreInput)
		{
			SkipScene();
		}
	}

	private void Update()
	{
		if (!ignoreInput && IsAnyInputPressedThisFrame())
		{
			SkipScene();
		}
	}

	private static bool IsAnyInputPressedThisFrame()
	{
		if ((Keyboard.current == null || !Keyboard.current.anyKey.wasPressedThisFrame) && (Mouse.current == null || (!Mouse.current.leftButton.wasPressedThisFrame && !Mouse.current.rightButton.wasPressedThisFrame && !Mouse.current.middleButton.wasPressedThisFrame)))
		{
			if (Gamepad.current != null)
			{
				if (!Gamepad.current.buttonSouth.wasPressedThisFrame && !Gamepad.current.buttonNorth.wasPressedThisFrame && !Gamepad.current.buttonEast.wasPressedThisFrame && !Gamepad.current.buttonWest.wasPressedThisFrame)
				{
					return Gamepad.current.startButton.wasPressedThisFrame;
				}
				return true;
			}
			return false;
		}
		return true;
	}

	public void SkipSceneFromInspector()
	{
		SkipScene();
	}

	private void SkipScene()
	{
		if (_isSkipping)
		{
			return;
		}
		if (!string.IsNullOrEmpty(mainMenuSceneName) && SceneManager.GetActiveScene().name != mainMenuSceneName)
		{
			int num = SceneManager.GetActiveScene().buildIndex + 1;
			if (num < SceneManager.sceneCountInBuildSettings)
			{
				_isSkipping = true;
				StartCoroutine(TransitionAndLoadRoutine(num));
			}
			else
			{
				Debug.LogWarning("No more scenes to load. Check your Build Settings.");
			}
		}
		else
		{
			Object.Destroy(base.gameObject);
		}
	}

	private IEnumerator TransitionAndLoadRoutine(int nextSceneIndex)
	{
		if (MonoSingleton<SceneTransitioner>.Instance != null)
		{
			MonoSingleton<SceneTransitioner>.Instance.SetLoadingScreen(isEnabled: true, transitionDuration, loadingScreen: false);
			yield return new WaitForSeconds(transitionDuration);
		}
		SceneManager.LoadScene(nextSceneIndex);
	}
}

}
