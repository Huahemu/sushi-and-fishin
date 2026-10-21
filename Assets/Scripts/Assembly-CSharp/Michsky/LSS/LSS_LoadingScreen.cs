using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Michsky.LSS
{
	[RequireComponent(typeof(CanvasGroup))]
	[AddComponentMenu("Loading Screen Studio/LSS Loading Screen")]
	public class LSS_LoadingScreen : MonoBehaviour
	{
		public static LSS_LoadingScreen instance = null;

		public float titleSize = 50f;

		public float descriptionSize = 28f;

		public float hintSize = 32f;

		public float statusSize = 24f;

		public float pakSize = 35f;

		public TMP_FontAsset titleFont;

		public TMP_FontAsset descriptionFont;

		public TMP_FontAsset hintFont;

		public TMP_FontAsset statusFont;

		public TMP_FontAsset pakFont;

		public Color titleColor = Color.white;

		public Color descriptionColor = Color.white;

		public Color hintColor = Color.white;

		public Color spinnerColor = Color.white;

		public Color statusColor = Color.white;

		public Color pakColor = Color.white;

		public string titleObjText = "Title";

		[TextArea(2, 5)]
		public string titleObjDescText = "Description";

		[TextArea(2, 5)]
		public string pakText = "Press {KEY} to continue";

		public CanvasGroup canvasGroup;

		public CanvasGroup backgroundCanvasGroup;

		public CanvasGroup contentCanvasGroup;

		public CanvasGroup pakCanvasGroup;

		public TextMeshProUGUI statusObj;

		public TextMeshProUGUI titleObj;

		public TextMeshProUGUI descriptionObj;

		public Slider progressBar;

		public Sprite backgroundImage;

		public Transform spinnerParent;

		[SerializeField]
		private bool enableRandomHints = true;

		public TextMeshProUGUI hintsText;

		public bool changeHintWithTimer;

		[Range(1f, 60f)]
		public float hintTimerValue = 5f;

		[TextArea]
		public List<string> hintList = new List<string>();

		private int currentHintIndex;

		[SerializeField]
		private bool enableRandomImages = true;

		public Image imageObject;

		public List<Sprite> imageList = new List<Sprite>();

		public bool changeImageWithTimer;

		public float imageTimerValue = 5f;

		[Range(0.1f, 10f)]
		public float imageFadingSpeed = 4f;

		private int currentImageIndex;

		public TextMeshProUGUI pakTextObj;

		public TextMeshProUGUI pakCountdownLabel;

		public Slider pakCountdownSlider;

		public bool useSpecificKey;

		public bool useCountdown = true;

		public bool waitForPlayerInput;

		[Range(1f, 30f)]
		public int pakCountdownTimer = 5;

		public KeyCode keyCode = KeyCode.Space;

		public bool enableVirtualLoading;

		public bool enableCustomLoading;

		public float virtualLoadingTimer = 5f;

		public float currentVirtualTime;

		[SerializeField]
		private bool setTimeScale = true;

		[SerializeField]
		private bool enablePressAnyKey = true;

		[SerializeField]
		private bool customSceneActivation;

		[Range(0.25f, 10f)]
		public float fadeSpeed = 4f;

		[Range(0.25f, 10f)]
		public float backgroundFadeSpeed = 2f;

		[Range(0.25f, 10f)]
		public float contentFadeSpeed = 2f;

		[SerializeField]
		private Camera projectorCamera;

		private RenderTexture projectorRT;

		private RawImage projectorImage;

		private CanvasGroup projectorCG;

		public UnityEvent onLoadingStart = new UnityEvent();

		public UnityEvent onTransitionCompleted = new UnityEvent();

		public UnityEvent onLoadingEnd = new UnityEvent();

		public UnityEvent onLoadingDestroy = new UnityEvent();

		[HideInInspector]
		public bool isFadeInRunning;

		[HideInInspector]
		public bool isFadeInCompleted;

		[HideInInspector]
		public bool isFadeOutRunning;

		[HideInInspector]
		public bool isFadeOutCompleted;

		[HideInInspector]
		public bool isBGFadeInRunning;

		[HideInInspector]
		public bool isBGFadeInCompleted;

		[HideInInspector]
		public bool isBGFadeOutRunning;

		[HideInInspector]
		public bool isBGFadeOutCompleted;

		[HideInInspector]
		public bool isContentFadeInRunning;

		[HideInInspector]
		public bool isContentFadeInCompleted;

		[HideInInspector]
		public bool isContentFadeOutRunning;

		[HideInInspector]
		public bool isContentFadeOutCompleted;

		[HideInInspector]
		public bool isPAKFadeInRunning;

		[HideInInspector]
		public bool isPAKFadeInCompleted;

		[HideInInspector]
		public bool isPAKFadeOutRunning;

		[HideInInspector]
		public bool isPAKFadeOutCompleted;

		public static string presetName = "Default";

		public int spinnerHelper;

		public bool updateHelper;

		private bool processLoading;

		public AsyncOperation loadingProcess = new AsyncOperation();

		private void Awake()
		{
			if (canvasGroup == null)
			{
				canvasGroup = base.gameObject.GetComponent<CanvasGroup>();
			}
			if (backgroundCanvasGroup == null)
			{
				backgroundCanvasGroup = new GameObject().AddComponent<CanvasGroup>();
				backgroundCanvasGroup.transform.SetParent(base.transform);
				backgroundCanvasGroup.gameObject.name = "[B]";
			}
			if (contentCanvasGroup == null)
			{
				contentCanvasGroup = new GameObject().AddComponent<CanvasGroup>();
				contentCanvasGroup.transform.SetParent(base.transform);
				contentCanvasGroup.gameObject.name = "[C]";
			}
			if (pakCanvasGroup == null)
			{
				pakCanvasGroup = new GameObject().AddComponent<CanvasGroup>();
				pakCanvasGroup.transform.SetParent(base.transform);
				pakCanvasGroup.gameObject.name = "[P]";
			}
			if (projectorCamera != null)
			{
				projectorRT = new RenderTexture(Screen.currentResolution.width, Screen.currentResolution.height, 24, RenderTextureFormat.RGB111110Float);
				projectorCamera.targetTexture = projectorRT;
				GameObject obj = new GameObject();
				obj.transform.SetParent(contentCanvasGroup.transform);
				RectTransform rectTransform = obj.AddComponent<RectTransform>();
				rectTransform.anchorMin = new Vector2(0f, 0f);
				rectTransform.anchorMax = new Vector2(1f, 1f);
				rectTransform.anchoredPosition = new Vector2(0f, 0f);
				projectorImage = rectTransform.gameObject.AddComponent<RawImage>();
				projectorImage.texture = projectorRT;
				projectorImage.transform.SetAsFirstSibling();
				projectorCG = projectorImage.gameObject.AddComponent<CanvasGroup>();
			}
			canvasGroup.alpha = 0f;
			backgroundCanvasGroup.alpha = 0f;
			contentCanvasGroup.alpha = 0f;
			pakCanvasGroup.alpha = 0f;
		}

		private void Start()
		{
			if (enableRandomHints && hintsText != null && hintList.Count > 0)
			{
				hintsText.text = GetRandomHint();
				StartCoroutine("ProcessRandomHint");
			}
			else if (!enableRandomHints && hintsText != null && hintList.Count > 0)
			{
				hintsText.text = hintList[currentHintIndex];
			}
			if (enableRandomImages && imageList.Count > 0)
			{
				ProcessRandomImages();
			}
			else if (imageObject != null)
			{
				imageObject.sprite = backgroundImage;
			}
			if (useCountdown && pakCountdownSlider != null && pakCountdownLabel != null)
			{
				pakCountdownSlider.maxValue = pakCountdownTimer;
				pakCountdownSlider.value = pakCountdownTimer;
				pakCountdownLabel.text = Mathf.Round(pakCountdownSlider.value * 1f).ToString();
			}
			else if (!useCountdown && pakCountdownSlider != null)
			{
				pakCountdownSlider.gameObject.SetActive(value: false);
			}
			if (statusObj != null)
			{
				statusObj.text = "0%";
			}
			if (progressBar != null)
			{
				progressBar.value = 0f;
			}
		}

		public static void LoadScene(string targetScene, string targetPrefab)
		{
			presetName = targetPrefab;
		}

		public static void SetupLoadingScreen(LSS_LoadingScreen targetPrefab, AsyncOperation loadOp = null)
		{
			try
			{
				if (instance != null)
				{
					instance.StopAllCoroutines();
					Object.Destroy(instance.gameObject);
				}
				instance = Object.Instantiate(targetPrefab);
				instance.gameObject.SetActive(value: true);
				Object.DontDestroyOnLoad(instance.gameObject);
				if (instance.setTimeScale)
				{
					Time.timeScale = 1f;
				}
				if (instance.customSceneActivation)
				{
					instance.enablePressAnyKey = false;
					instance.waitForPlayerInput = false;
				}
				instance.processLoading = true;
				instance.loadingProcess = loadOp;
				if (instance.loadingProcess != null)
				{
					instance.loadingProcess.allowSceneActivation = false;
					instance.onLoadingStart.Invoke();
				}
			}
			catch
			{
				instance.processLoading = false;
				if (instance != null)
				{
					Object.Destroy(instance.gameObject);
				}
			}
		}

		public static void TryToSetLoadingOp(AsyncOperation loadOp)
		{
			if (!(instance == null) && loadOp != null)
			{
				instance.loadingProcess = loadOp;
				instance.loadingProcess.allowSceneActivation = false;
				instance.onLoadingStart.Invoke();
			}
		}

		public static void SetupCustomLoadingScreen(LSS_LoadingScreen targetPrefab)
		{
			try
			{
				instance = Object.Instantiate(targetPrefab);
				instance.enableCustomLoading = true;
				instance.gameObject.SetActive(value: true);
				Object.DontDestroyOnLoad(instance.gameObject);
				instance.processLoading = true;
				instance.onLoadingStart.Invoke();
			}
			catch
			{
				instance.processLoading = false;
				if (instance != null)
				{
					Object.Destroy(instance.gameObject);
				}
			}
		}

		public static void LoadSceneAdditive(string targetScene, string targetPrefab)
		{
			presetName = targetPrefab;
			LoadSceneAdditive(targetScene);
		}

		public static void CustomAdditiveLoadScene(AsyncOperation loadOp)
		{
			LoadSceneAdditive("", loadOp);
		}

		public static void LoadSceneAdditive(string targetScene, AsyncOperation loadOp = null)
		{
			try
			{
				instance = Object.Instantiate(Resources.Load<GameObject>("Loading Screens/" + presetName).GetComponent<LSS_LoadingScreen>());
				instance.gameObject.SetActive(value: true);
				Object.DontDestroyOnLoad(instance.gameObject);
				if (instance.setTimeScale)
				{
					Time.timeScale = 1f;
				}
				instance.canvasGroup.alpha = 0f;
				instance.backgroundCanvasGroup.alpha = 0f;
				instance.contentCanvasGroup.alpha = 0f;
				instance.pakCanvasGroup.alpha = 0f;
				instance.processLoading = true;
				instance.loadingProcess = loadOp;
				instance.loadingProcess.allowSceneActivation = false;
				instance.onLoadingStart.Invoke();
			}
			catch
			{
				Debug.LogError("<b><color=orange>[LSS]</color></b> Cannot initalize the loading screen because either <b><color=orange>'" + targetScene + "'</color></b> scene has not been added to the build window, or <b><color=orange>'" + presetName + "'</color></b> prefab cannot be found in <b>Resources/Loading Screens</b>.");
				instance.processLoading = false;
				if (instance != null)
				{
					Object.Destroy(instance.gameObject);
				}
			}
		}

		public static void PerformVirtualTransition(float transitionDuration)
		{
			instance = Object.Instantiate(Resources.Load<GameObject>("Loading Screens/" + presetName).GetComponent<LSS_LoadingScreen>());
			instance.gameObject.SetActive(value: true);
			Object.DontDestroyOnLoad(instance.gameObject);
			if (instance.setTimeScale)
			{
				Time.timeScale = 1f;
			}
			instance.StopCoroutine("FadeInLoadingScreen");
			instance.StartCoroutine("FadeInLoadingScreen");
			instance.StartCoroutine("HandleVirtualTransition", transitionDuration);
		}

		public static void EndVirtualTransition()
		{
			instance.StopCoroutine("HandleVirtualTransition");
			instance.StartCoroutine("FadeOutContentScreen", true);
		}

		private void Update()
		{
			if (!enableVirtualLoading)
			{
				ProcessLoading();
			}
			else if (enableCustomLoading)
			{
				ProcessCustomLoading();
			}
			else
			{
				ProcessVirtualLoading();
			}
		}

		private void ProcessLoading()
		{
			if (!processLoading)
			{
				return;
			}
			if (!customSceneActivation && !loadingProcess.allowSceneActivation && isContentFadeInCompleted && !enablePressAnyKey)
			{
				loadingProcess.allowSceneActivation = true;
			}
			else if (!loadingProcess.allowSceneActivation && isContentFadeInCompleted && enablePressAnyKey && !waitForPlayerInput)
			{
				loadingProcess.allowSceneActivation = true;
			}
			if (progressBar != null)
			{
				progressBar.value = Mathf.Lerp(progressBar.value, loadingProcess.progress, 0.1f);
			}
			if (statusObj != null && progressBar != null)
			{
				statusObj.text = Mathf.Round(progressBar.value * 100f) + "%";
			}
			if (canvasGroup.alpha == 0f && !isFadeInRunning && !isFadeInCompleted)
			{
				StopCoroutine("FadeInLoadingScreen");
				StartCoroutine("FadeInLoadingScreen");
			}
			else if (customSceneActivation && loadingProcess.progress == 0.9f && isContentFadeInCompleted && !isContentFadeOutRunning && !isContentFadeOutCompleted)
			{
				canvasGroup.interactable = false;
				canvasGroup.blocksRaycasts = false;
				StopCoroutine("FadeOutBackgroundScreen");
				StartCoroutine("FadeOutBackgroundScreen");
				StopCoroutine("FadeOutContentScreen");
				StartCoroutine("FadeOutContentScreen", true);
			}
			else if (!enablePressAnyKey && loadingProcess.isDone && loadingProcess.allowSceneActivation && isContentFadeInCompleted && !isContentFadeOutRunning && !isContentFadeOutCompleted)
			{
				canvasGroup.interactable = false;
				canvasGroup.blocksRaycasts = false;
				StopCoroutine("FadeOutBackgroundScreen");
				StartCoroutine("FadeOutBackgroundScreen");
				StopCoroutine("FadeOutContentScreen");
				StartCoroutine("FadeOutContentScreen", true);
			}
			else if (enablePressAnyKey && waitForPlayerInput && loadingProcess.progress == 0.9f)
			{
				if (!isPAKFadeInRunning && !isPAKFadeInCompleted && isContentFadeInCompleted && !isContentFadeOutRunning && !isContentFadeOutCompleted)
				{
					StopCoroutine("FadeOutContentScreen");
					StartCoroutine("FadeOutContentScreen", false);
					StopCoroutine("FadeInPAKScreen");
					StartCoroutine("FadeInPAKScreen");
				}
				else if (isPAKFadeInCompleted && useCountdown)
				{
					pakCountdownSlider.value -= Time.unscaledDeltaTime;
					pakCountdownLabel.text = Mathf.Round(pakCountdownSlider.value * 1f).ToString();
					if (pakCountdownSlider.value == 0f)
					{
						loadingProcess.allowSceneActivation = true;
						canvasGroup.interactable = false;
						canvasGroup.blocksRaycasts = false;
						StopCoroutine("FadeOutBackgroundScreen");
						StartCoroutine("FadeOutBackgroundScreen");
						StopCoroutine("FadeOutPAKScreen");
						StartCoroutine("FadeOutPAKScreen", true);
					}
				}
				if (!useSpecificKey && Input.anyKeyDown && !isPAKFadeInRunning && isPAKFadeInCompleted)
				{
					loadingProcess.allowSceneActivation = true;
					canvasGroup.interactable = false;
					canvasGroup.blocksRaycasts = false;
					StopCoroutine("FadeOutBackgroundScreen");
					StartCoroutine("FadeOutBackgroundScreen");
					StopCoroutine("FadeOutPAKScreen");
					StartCoroutine("FadeOutPAKScreen", true);
				}
				else if (useSpecificKey && Input.GetKeyDown(keyCode) && !isPAKFadeInRunning && isPAKFadeInCompleted)
				{
					loadingProcess.allowSceneActivation = true;
					canvasGroup.interactable = false;
					canvasGroup.blocksRaycasts = false;
					StopCoroutine("FadeOutBackgroundScreen");
					StartCoroutine("FadeOutBackgroundScreen");
					StopCoroutine("FadeOutPAKScreen");
					StartCoroutine("FadeOutPAKScreen", true);
				}
			}
			else
			{
				if (!enablePressAnyKey || waitForPlayerInput || !loadingProcess.isDone)
				{
					return;
				}
				if (!isPAKFadeInRunning && !isPAKFadeInCompleted)
				{
					StopCoroutine("FadeInPAKScreen");
					StartCoroutine("FadeInPAKScreen");
				}
				else if (useCountdown)
				{
					pakCountdownSlider.value -= Time.unscaledDeltaTime;
					pakCountdownLabel.text = Mathf.Round(pakCountdownSlider.value * 1f).ToString();
					if (pakCountdownSlider.value == 0f)
					{
						canvasGroup.interactable = false;
						canvasGroup.blocksRaycasts = false;
						StopCoroutine("FadeOutBackgroundScreen");
						StartCoroutine("FadeOutBackgroundScreen");
						StopCoroutine("FadeOutPAKScreen");
						StartCoroutine("FadeOutPAKScreen", true);
					}
				}
				if (!useSpecificKey && Input.anyKeyDown && !isPAKFadeInRunning && isPAKFadeInCompleted)
				{
					canvasGroup.interactable = false;
					canvasGroup.blocksRaycasts = false;
					StopCoroutine("FadeOutBackgroundScreen");
					StartCoroutine("FadeOutBackgroundScreen");
					StopCoroutine("FadeOutPAKScreen");
					StartCoroutine("FadeOutPAKScreen", true);
				}
				else if (useSpecificKey && Input.GetKeyDown(keyCode) && !isPAKFadeInRunning && isPAKFadeInCompleted)
				{
					canvasGroup.interactable = false;
					canvasGroup.blocksRaycasts = false;
					StopCoroutine("FadeOutBackgroundScreen");
					StartCoroutine("FadeOutBackgroundScreen");
					StopCoroutine("FadeOutPAKScreen");
					StartCoroutine("FadeOutPAKScreen", true);
				}
			}
		}

		private void ProcessVirtualLoading()
		{
			if (!processLoading)
			{
				return;
			}
			if (!loadingProcess.allowSceneActivation && loadingProcess.progress == 0.9f)
			{
				loadingProcess.allowSceneActivation = true;
			}
			if (progressBar != null)
			{
				progressBar.value += 1f / virtualLoadingTimer * Time.unscaledDeltaTime;
			}
			if (statusObj != null && progressBar != null)
			{
				statusObj.text = Mathf.Round(progressBar.value * 100f) + "%";
			}
			currentVirtualTime += Time.unscaledDeltaTime;
			if (canvasGroup.alpha == 0f && !isFadeInRunning && !isFadeInCompleted)
			{
				StopCoroutine("FadeInLoadingScreen");
				StartCoroutine("FadeInLoadingScreen");
			}
			if (!(currentVirtualTime >= virtualLoadingTimer))
			{
				return;
			}
			if (!customSceneActivation && !loadingProcess.allowSceneActivation && isContentFadeInCompleted && !enablePressAnyKey)
			{
				loadingProcess.allowSceneActivation = true;
			}
			else if (!loadingProcess.allowSceneActivation && isContentFadeInCompleted && enablePressAnyKey && !waitForPlayerInput)
			{
				loadingProcess.allowSceneActivation = true;
			}
			if (customSceneActivation && loadingProcess.progress == 0.9f && isContentFadeInCompleted && !isContentFadeOutRunning && !isContentFadeOutCompleted)
			{
				canvasGroup.interactable = false;
				canvasGroup.blocksRaycasts = false;
				StopCoroutine("FadeOutBackgroundScreen");
				StartCoroutine("FadeOutBackgroundScreen");
				StopCoroutine("FadeOutContentScreen");
				StartCoroutine("FadeOutContentScreen", true);
			}
			else if (!enablePressAnyKey && loadingProcess.isDone && isContentFadeInCompleted && !isContentFadeOutRunning && !isContentFadeOutCompleted)
			{
				canvasGroup.interactable = false;
				canvasGroup.blocksRaycasts = false;
				StopCoroutine("FadeOutBackgroundScreen");
				StartCoroutine("FadeOutBackgroundScreen");
				StopCoroutine("FadeOutContentScreen");
				StartCoroutine("FadeOutContentScreen", true);
			}
			else if (enablePressAnyKey && waitForPlayerInput && loadingProcess.progress == 0.9f)
			{
				if (!isPAKFadeInRunning && !isPAKFadeInCompleted && isContentFadeInCompleted && !isContentFadeOutRunning && !isContentFadeOutCompleted)
				{
					StopCoroutine("FadeOutContentScreen");
					StartCoroutine("FadeOutContentScreen", false);
					StopCoroutine("FadeInPAKScreen");
					StartCoroutine("FadeInPAKScreen");
				}
				else if (isPAKFadeInCompleted && useCountdown)
				{
					pakCountdownSlider.value -= Time.unscaledDeltaTime;
					pakCountdownLabel.text = Mathf.Round(pakCountdownSlider.value * 1f).ToString();
					if (pakCountdownSlider.value == 0f)
					{
						canvasGroup.interactable = false;
						canvasGroup.blocksRaycasts = false;
						StopCoroutine("FadeOutBackgroundScreen");
						StartCoroutine("FadeOutBackgroundScreen");
						StopCoroutine("FadeOutPAKScreen");
						StartCoroutine("FadeOutPAKScreen", true);
					}
				}
				if (!useSpecificKey && Input.anyKeyDown && !isPAKFadeInRunning && isPAKFadeInCompleted)
				{
					canvasGroup.interactable = false;
					canvasGroup.blocksRaycasts = false;
					StopCoroutine("FadeOutBackgroundScreen");
					StartCoroutine("FadeOutBackgroundScreen");
					StopCoroutine("FadeOutPAKScreen");
					StartCoroutine("FadeOutPAKScreen", true);
				}
				else if (useSpecificKey && Input.GetKeyDown(keyCode) && !isPAKFadeInRunning && isPAKFadeInCompleted)
				{
					canvasGroup.interactable = false;
					canvasGroup.blocksRaycasts = false;
					StopCoroutine("FadeOutBackgroundScreen");
					StartCoroutine("FadeOutBackgroundScreen");
					StopCoroutine("FadeOutPAKScreen");
					StartCoroutine("FadeOutPAKScreen", true);
				}
			}
			else
			{
				if (!enablePressAnyKey || waitForPlayerInput)
				{
					return;
				}
				if (!isPAKFadeInRunning && !isPAKFadeInCompleted)
				{
					StopCoroutine("FadeInPAKScreen");
					StartCoroutine("FadeInPAKScreen");
				}
				else if (useCountdown)
				{
					pakCountdownSlider.value -= Time.unscaledDeltaTime;
					pakCountdownLabel.text = Mathf.Round(pakCountdownSlider.value * 1f).ToString();
					if (pakCountdownSlider.value == 0f)
					{
						canvasGroup.interactable = false;
						canvasGroup.blocksRaycasts = false;
						StopCoroutine("FadeOutBackgroundScreen");
						StartCoroutine("FadeOutBackgroundScreen");
						StopCoroutine("FadeOutPAKScreen");
						StartCoroutine("FadeOutPAKScreen", true);
					}
				}
				if (!useSpecificKey && Input.anyKeyDown && !isPAKFadeInRunning && isPAKFadeInCompleted)
				{
					canvasGroup.interactable = false;
					canvasGroup.blocksRaycasts = false;
					StopCoroutine("FadeOutBackgroundScreen");
					StartCoroutine("FadeOutBackgroundScreen");
					StopCoroutine("FadeOutPAKScreen");
					StartCoroutine("FadeOutPAKScreen", true);
				}
				else if (useSpecificKey && Input.GetKeyDown(keyCode) && !isPAKFadeInRunning && isPAKFadeInCompleted)
				{
					canvasGroup.interactable = false;
					canvasGroup.blocksRaycasts = false;
					StopCoroutine("FadeOutBackgroundScreen");
					StartCoroutine("FadeOutBackgroundScreen");
					StopCoroutine("FadeOutPAKScreen");
					StartCoroutine("FadeOutPAKScreen", true);
				}
			}
		}

		private void ProcessCustomLoading()
		{
			if (processLoading)
			{
				if (progressBar != null)
				{
					progressBar.value += 1f / virtualLoadingTimer * Time.unscaledDeltaTime;
				}
				if (statusObj != null && progressBar != null)
				{
					statusObj.text = Mathf.Round(progressBar.value * 100f) + "%";
				}
				currentVirtualTime += Time.unscaledDeltaTime;
				if (canvasGroup.alpha == 0f && !isFadeInRunning && !isFadeInCompleted)
				{
					StopCoroutine("FadeInLoadingScreen");
					StartCoroutine("FadeInLoadingScreen");
				}
				if (currentVirtualTime >= virtualLoadingTimer && customSceneActivation && isContentFadeInCompleted && !isContentFadeOutRunning && !isContentFadeOutCompleted)
				{
					canvasGroup.interactable = false;
					canvasGroup.blocksRaycasts = false;
					StopCoroutine("FadeOutBackgroundScreen");
					StartCoroutine("FadeOutBackgroundScreen");
					StopCoroutine("FadeOutContentScreen");
					StartCoroutine("FadeOutContentScreen", true);
				}
			}
		}

		private void ProcessRandomImages()
		{
			if (!(imageObject == null))
			{
				imageObject.sprite = GetRandomImage();
				if (changeImageWithTimer)
				{
					StartCoroutine("DoImageTransition");
				}
				else
				{
					enableRandomImages = false;
				}
			}
		}

		private Sprite GetRandomImage()
		{
			if (imageList.Count == 0)
			{
				return null;
			}
			if (currentImageIndex >= imageList.Count)
			{
				currentImageIndex = 0;
			}
			return imageList[currentImageIndex++];
		}

		private string GetRandomHint()
		{
			if (hintList.Count == 0)
			{
				return null;
			}
			currentHintIndex = GetRandomUniqueValue(currentHintIndex, 0, hintList.Count);
			return hintList[currentHintIndex];
		}

		private IEnumerator ProcessRandomHint()
		{
			yield return new WaitForSecondsRealtime(hintTimerValue);
			currentHintIndex = GetRandomUniqueValue(currentHintIndex, 0, hintList.Count);
			hintsText.text = hintList[currentHintIndex];
			StartCoroutine("ProcessRandomHint");
		}

		private IEnumerator FadeInLoadingScreen()
		{
			isFadeInRunning = true;
			canvasGroup.alpha = 0f;
			while (canvasGroup.alpha < 0.99f)
			{
				canvasGroup.alpha += Time.unscaledDeltaTime * fadeSpeed;
				yield return null;
			}
			isFadeInRunning = false;
			isFadeInCompleted = true;
			canvasGroup.alpha = 1f;
			StopCoroutine("FadeInBackgroundScreen");
			StartCoroutine("FadeInBackgroundScreen");
			StopCoroutine("FadeInContentScreen");
			StartCoroutine("FadeInContentScreen");
		}

		private IEnumerator FadeOutLoadingScreen()
		{
			backgroundCanvasGroup.gameObject.SetActive(value: false);
			contentCanvasGroup.gameObject.SetActive(value: false);
			pakCanvasGroup.gameObject.SetActive(value: false);
			isFadeOutRunning = true;
			onLoadingEnd.Invoke();
			while (canvasGroup.alpha > 0.01f)
			{
				canvasGroup.alpha -= Time.unscaledDeltaTime * fadeSpeed;
				yield return null;
			}
			isFadeOutRunning = false;
			isFadeOutCompleted = true;
			onLoadingDestroy.Invoke();
			Object.Destroy(instance.gameObject);
		}

		private IEnumerator FadeInContentScreen()
		{
			isContentFadeInRunning = true;
			while (contentCanvasGroup.alpha < 0.99f)
			{
				contentCanvasGroup.alpha += Time.unscaledDeltaTime * contentFadeSpeed;
				yield return null;
			}
			isContentFadeInRunning = false;
			isContentFadeInCompleted = true;
			contentCanvasGroup.alpha = 1f;
			onTransitionCompleted.Invoke();
		}

		private IEnumerator FadeOutContentScreen(bool fadeOutScreenAfter)
		{
			isContentFadeOutRunning = true;
			while (contentCanvasGroup.alpha > 0.01f)
			{
				contentCanvasGroup.alpha -= Time.unscaledDeltaTime * contentFadeSpeed;
				yield return null;
			}
			isContentFadeOutRunning = false;
			isContentFadeOutCompleted = true;
			contentCanvasGroup.alpha = 0f;
			if (projectorCamera != null)
			{
				projectorImage.gameObject.SetActive(value: false);
				projectorCamera.gameObject.SetActive(value: false);
			}
			if (fadeOutScreenAfter)
			{
				StopCoroutine("FadeOutLoadingScreen");
				StartCoroutine("FadeOutLoadingScreen");
			}
			if (loadingProcess != null && customSceneActivation)
			{
				loadingProcess.allowSceneActivation = true;
			}
		}

		private IEnumerator FadeInBackgroundScreen()
		{
			isBGFadeInRunning = true;
			while (backgroundCanvasGroup.alpha < 0.99f)
			{
				backgroundCanvasGroup.alpha += Time.unscaledDeltaTime * backgroundFadeSpeed;
				yield return null;
			}
			isBGFadeInRunning = false;
			isBGFadeInCompleted = true;
			backgroundCanvasGroup.alpha = 1f;
		}

		private IEnumerator FadeOutBackgroundScreen()
		{
			isBGFadeOutRunning = true;
			while (backgroundCanvasGroup.alpha > 0.01f)
			{
				backgroundCanvasGroup.alpha -= Time.unscaledDeltaTime * backgroundFadeSpeed;
				yield return null;
			}
			isBGFadeOutRunning = false;
			isBGFadeOutCompleted = true;
			backgroundCanvasGroup.alpha = 0f;
		}

		private IEnumerator FadeInPAKScreen()
		{
			isPAKFadeInRunning = true;
			pakCanvasGroup.alpha = 0f;
			StopCoroutine("FadeOutContentScreen");
			StartCoroutine("FadeOutContentScreen", false);
			while (pakCanvasGroup.alpha < 0.99f)
			{
				pakCanvasGroup.alpha += Time.unscaledDeltaTime * contentFadeSpeed;
				yield return null;
			}
			isPAKFadeInRunning = false;
			isPAKFadeInCompleted = true;
			pakCanvasGroup.alpha = 1f;
		}

		private IEnumerator FadeOutPAKScreen(bool fadeOutScreenAfter)
		{
			isPAKFadeOutRunning = true;
			while (pakCanvasGroup.alpha > 0.01f)
			{
				pakCanvasGroup.alpha -= Time.unscaledDeltaTime * contentFadeSpeed;
				yield return null;
			}
			isPAKFadeOutRunning = false;
			isPAKFadeOutCompleted = true;
			pakCanvasGroup.alpha = 0f;
			if (fadeOutScreenAfter)
			{
				StopCoroutine("FadeOutLoadingScreen");
				StartCoroutine("FadeOutLoadingScreen");
			}
		}

		private IEnumerator DoImageTransition()
		{
			yield return new WaitForSecondsRealtime(imageTimerValue);
			while (imageObject.color.a > 0.01f)
			{
				imageObject.color = Color.Lerp(imageObject.color, new Color(imageObject.color.r, imageObject.color.g, imageObject.color.b, 0f), imageFadingSpeed / 30f);
				yield return new WaitForFixedUpdate();
			}
			imageObject.color = new Color(imageObject.color.r, imageObject.color.g, imageObject.color.b, 0f);
			imageObject.sprite = GetRandomImage();
			StartCoroutine("DoImageTransitionHelper");
		}

		private IEnumerator DoImageTransitionHelper()
		{
			while (imageObject.color.a < 0.99f)
			{
				imageObject.color = Color.Lerp(imageObject.color, new Color(imageObject.color.r, imageObject.color.g, imageObject.color.b, 1f), imageFadingSpeed / 30f);
				yield return new WaitForFixedUpdate();
			}
			imageObject.color = new Color(imageObject.color.r, imageObject.color.g, imageObject.color.b, 1f);
			StartCoroutine("DoImageTransition");
		}

		private IEnumerator HandleVirtualTransition(float timer)
		{
			yield return new WaitForSeconds(timer);
			StartCoroutine("FadeOutContentScreen", true);
		}

		public static int GetRandomUniqueValue(int currentValue, int minValue, int maxValue)
		{
			if (maxValue == 1)
			{
				return currentValue;
			}
			int num = Random.Range(minValue, maxValue);
			while (currentValue == num)
			{
				num = Random.Range(minValue, maxValue);
			}
			return num;
		}
	}
}
