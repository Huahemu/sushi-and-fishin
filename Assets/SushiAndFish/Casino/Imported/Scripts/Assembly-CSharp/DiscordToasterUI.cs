namespace SushiAndFish.Casino.Original
{
using global::DG.Tweening;
using global::UnityEngine;

public class DiscordToasterUI : MonoBehaviour
{
	[Header("Toaster")]
	[SerializeField]
	private RectTransform toasterRoot;

	[SerializeField]
	private float visibleAnchorY = 80f;

	[SerializeField]
	private float hiddenAnchorY = -400f;

	[SerializeField]
	private float animateInDuration = 0.4f;

	[SerializeField]
	private float animateOutDuration = 0.3f;

	[SerializeField]
	private Ease easeIn = Ease.OutBack;

	[SerializeField]
	private Ease easeOut = Ease.InBack;

	private Tween _activeTween;

	private void Awake()
	{
		if ((bool)toasterRoot)
		{
			Vector2 anchoredPosition = toasterRoot.anchoredPosition;
			anchoredPosition.y = hiddenAnchorY;
			toasterRoot.anchoredPosition = anchoredPosition;
		}
	}

	private void OnDisable()
	{
		_activeTween?.Kill();
	}

	public void SlideIn()
	{
		if ((bool)toasterRoot)
		{
			_activeTween?.Kill();
			base.gameObject.SetActive(value: true);
			toasterRoot.anchoredPosition = new Vector2(toasterRoot.anchoredPosition.x, hiddenAnchorY);
			_activeTween = toasterRoot.DOAnchorPosY(visibleAnchorY, animateInDuration).SetEase(easeIn).SetTarget(toasterRoot);
		}
	}

	public void SlideOut()
	{
		if ((bool)toasterRoot)
		{
			_activeTween?.Kill();
			_activeTween = toasterRoot.DOAnchorPosY(hiddenAnchorY, animateOutDuration).SetEase(easeOut).SetTarget(toasterRoot)
				.OnComplete(() =>
				{
					base.gameObject.SetActive(value: false);
				});
		}
	}
}

}
