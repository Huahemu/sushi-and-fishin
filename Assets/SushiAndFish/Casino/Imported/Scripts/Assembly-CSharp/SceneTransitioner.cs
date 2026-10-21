namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::System.Collections.Generic;
using global::DG.Tweening;
using SushiAndFish.Casino.Original.Extensions;
using global::UnityEngine;
using global::UnityEngine.UI;

public class SceneTransitioner : MonoSingleton<SceneTransitioner>
{
	private struct FadeRequest
	{
		public bool enable;

		public float duration;

		public bool loadingScreen;
	}

	[SerializeField]
	private CanvasGroup canvasGroup;

	[SerializeField]
	private Image fadeImage;

	[SerializeField]
	private GameObject loadingEffects;

	[SerializeField]
	private LoadingQuotes loadingQuotes;

	private readonly Queue<FadeRequest> _requestQueue = new Queue<FadeRequest>();

	private bool _isProcessing;

	private Sequence _fadeSequence;

	protected override void OnAwake()
	{
		base.OnAwake();
		Object.DontDestroyOnLoad(base.gameObject);
		if ((bool)canvasGroup)
		{
			canvasGroup.alpha = 0f;
			canvasGroup.blocksRaycasts = false;
		}
		if ((bool)loadingEffects)
		{
			loadingEffects.gameObject.SetActive(value: false);
		}
	}

	public void SetLoadingScreen(bool isEnabled, float duration, bool loadingScreen = true)
	{
		if (_requestQueue.Count <= 0 || _requestQueue.Peek().enable != isEnabled)
		{
			_requestQueue.Enqueue(new FadeRequest
			{
				enable = isEnabled,
				duration = duration,
				loadingScreen = loadingScreen
			});
			if (!_isProcessing)
			{
				StartCoroutine(ProcessQueue());
			}
		}
	}

	private IEnumerator ProcessQueue()
	{
		_isProcessing = true;
		while (_requestQueue.Count > 0)
		{
			FadeRequest request = _requestQueue.Dequeue();
			yield return PlayFade(request);
		}
		_isProcessing = false;
	}

	private IEnumerator PlayFade(FadeRequest request)
	{
		float endValue = (request.enable ? 1f : 0f);
		if ((bool)canvasGroup)
		{
			canvasGroup.blocksRaycasts = request.enable;
		}
		bool flag = request.enable && request.loadingScreen;
		if ((bool)loadingEffects)
		{
			loadingEffects.gameObject.SetActive(flag);
			if (flag && (bool)loadingQuotes)
			{
				loadingQuotes.ShowRandomQuote();
			}
		}
		_fadeSequence?.Kill();
		_fadeSequence = DOTween.Sequence().SetUpdate(isIndependentUpdate: true);
		if ((bool)canvasGroup)
		{
			_fadeSequence.Append(canvasGroup.DOFade(endValue, request.duration).SetEase(Ease.Linear));
		}
		yield return _fadeSequence.WaitForCompletion();
	}

	public void ForceSet(bool isEnabled)
	{
		_requestQueue.Clear();
		_fadeSequence?.Kill();
		float alpha = (isEnabled ? 1f : 0f);
		if ((bool)canvasGroup)
		{
			canvasGroup.alpha = alpha;
			canvasGroup.blocksRaycasts = isEnabled;
		}
		if ((bool)loadingEffects)
		{
			loadingEffects.gameObject.SetActive(isEnabled);
			if (isEnabled && (bool)loadingQuotes)
			{
				loadingQuotes.ShowRandomQuote();
			}
		}
	}
}

}
