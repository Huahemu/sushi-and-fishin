namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.SceneManagement;

public class OfflineSplashCoinFlip : MonoBehaviour
{
	[Header("Optional References")]
	[SerializeField]
	private Rigidbody coin;

	[SerializeField]
	private SceneSkipper sceneSkipperToDisable;

	[SerializeField]
	private CanvasGroup questionCanvasGroup;

	[SerializeField]
	private CanvasGroup resultCanvasGroup;

	[SerializeField]
	private TMP_Text resultText;

	[Header("Throw Settings")]
	[SerializeField]
	private float minFlipForce = 10f;

	[SerializeField]
	private float maxFlipForce = 20f;

	[SerializeField]
	private float minFlipTorque = 2f;

	[SerializeField]
	private float maxFlipTorque = 4f;

	[Header("Stop Check Settings")]
	[SerializeField]
	private float firstCheckDelay = 1f;

	[SerializeField]
	private float checkInterval = 0.1f;

	[SerializeField]
	private float maxDuration = 10f;

	[SerializeField]
	private float stopVelocityThreshold = 0.1f;

	[SerializeField]
	private float stopAngularVelocityThreshold = 0.1f;

	[SerializeField]
	private float coinHeightThreshold = 1.25f;

	[Header("Result Settings")]
	[SerializeField]
	private bool headsQuitsGame = true;

	[SerializeField]
	private bool disableInputAfterChoice = true;

	[SerializeField]
	private float transitionDuration = 0.5f;

	[SerializeField]
	private float resultFadeDuration = 0.35f;

	[SerializeField]
	private float resultDisplayDuration = 1.2f;

	[SerializeField]
	private string loseMessage = "Uh oh, you lost sucka.";

	[SerializeField]
	private string winMessage = "You win this time.";

	[Header("Achievements")]
	public SteamAchievement_SteamworksNET onChooseNo;

	public SteamAchievement_SteamworksNET onChooseYesWin;

	public SteamAchievement_SteamworksNET onChooseYesLose;

	[Header("SFX")]
	[SerializeField]
	private SFXLocalLoopComponent coinFlipSfx;

	[SerializeField]
	private SFXLocalPlayer gameWinSfx;

	[SerializeField]
	private SFXLocalPlayer gameLoseSfx;

	[SerializeField]
	private SFXLocalPlayer boomSfx;

	[SerializeField]
	private SFXLocalLoopComponent heartLoop;

	private bool _choiceLocked;

	private bool _isTransitioning;

	private Coroutine _checkStopCoroutine;

	private void Awake()
	{
		if (sceneSkipperToDisable != null)
		{
			sceneSkipperToDisable.ignoreInput = true;
		}
	}

	public void OnChooseNo()
	{
		if (!_choiceLocked)
		{
			_choiceLocked = disableInputAfterChoice;
			onChooseNo?.UnlockAchievement();
			LoadNextScene();
			FadeOutText();
		}
	}

	public void OnChooseYes()
	{
		if (_choiceLocked)
		{
			return;
		}
		_choiceLocked = disableInputAfterChoice;
		if (coin == null)
		{
			ResolveResult(UnityEngine.Random.value > 0.5f);
			return;
		}
		if (_checkStopCoroutine != null)
		{
			StopCoroutine(_checkStopCoroutine);
		}
		FlipCoin();
		_checkStopCoroutine = StartCoroutine(CheckCoinStoppedRoutine());
		FadeOutText();
	}

	private void FadeOutText()
	{
		questionCanvasGroup.DOFade(0f, 0.5f);
	}

	private void FlipCoin()
	{
		coinFlipSfx.LoopSFX(play: true);
		heartLoop.LoopSFX(play: true);
		coin.linearVelocity = Vector3.zero;
		coin.angularVelocity = Vector3.zero;
		float num = UnityEngine.Random.Range(minFlipForce, maxFlipForce);
		coin.AddForce(Vector3.up * num, ForceMode.VelocityChange);
		float f = UnityEngine.Random.value * MathF.PI * 2f;
		Vector3 normalized = new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f)).normalized;
		float num2 = UnityEngine.Random.Range(minFlipTorque, maxFlipTorque);
		coin.AddTorque(normalized * num2, ForceMode.VelocityChange);
	}

	private IEnumerator CheckCoinStoppedRoutine()
	{
		yield return new WaitForSeconds(firstCheckDelay);
		float startTime = Time.time;
		while (Time.time - startTime < maxDuration)
		{
			yield return new WaitForSeconds(checkInterval);
			if (!(coin.transform.localPosition.y > coinHeightThreshold) && !(coin.linearVelocity.sqrMagnitude > stopVelocityThreshold * stopVelocityThreshold) && !(coin.angularVelocity.sqrMagnitude > stopAngularVelocityThreshold * stopAngularVelocityThreshold))
			{
				break;
			}
		}
		bool isHeads = Vector3.Angle(coin.transform.up, Vector3.up) <= 90f;
		ResolveResult(isHeads);
		coinFlipSfx.LoopSFX(play: false);
		heartLoop.LoopSFX(play: false);
	}

	private void ResolveResult(bool isHeads)
	{
		bool flag = isHeads == headsQuitsGame;
		string message = (flag ? loseMessage : winMessage);
		if (flag)
		{
			onChooseYesLose?.UnlockAchievement();
			gameLoseSfx.PlayOneShotWith3DPos();
			boomSfx.PlayOneShotWith3DPos();
		}
		else
		{
			onChooseYesWin?.UnlockAchievement();
			gameWinSfx.PlayOneShotOverrideParams();
		}
		StartCoroutine(ShowResultAndContinueRoutine(message, flag));
	}

	private IEnumerator ShowResultAndContinueRoutine(string message, bool shouldQuit)
	{
		if (resultText != null)
		{
			resultText.text = message;
		}
		if (resultCanvasGroup != null)
		{
			resultCanvasGroup.DOFade(1f, resultFadeDuration);
		}
		yield return new WaitForSeconds(resultDisplayDuration);
		if (shouldQuit)
		{
			QuitGame();
		}
		else
		{
			LoadNextScene();
		}
	}

	private void LoadNextScene()
	{
		if (!_isTransitioning)
		{
			int num = SceneManager.GetActiveScene().buildIndex + 1;
			if (num < SceneManager.sceneCountInBuildSettings)
			{
				_isTransitioning = true;
				StartCoroutine(TransitionAndLoadRoutine(num));
			}
			else
			{
				Debug.LogWarning("No more scenes to load. Check your Build Settings.");
			}
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

	private void QuitGame()
	{
		Application.Quit();
	}
}

}
