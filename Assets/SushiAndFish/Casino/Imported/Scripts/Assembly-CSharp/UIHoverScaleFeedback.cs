namespace SushiAndFish.Casino.Original
{
using global::DG.Tweening;
using global::UnityEngine;
using global::UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class UIHoverScaleFeedback : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler
{
	[Header("Scale Settings")]
	[Tooltip("Target scale when hovered")]
	public Vector3 hoverScale = new Vector3(1.1f, 1.1f, 1f);

	[Tooltip("Duration of the scale tween")]
	public float tweenDuration = 0.2f;

	[Tooltip("Ease type used for scaling")]
	public Ease tweenEase = Ease.OutBack;

	private bool _hasClicked;

	private RectTransform rectTransform;

	private Vector3 originalScale;

	private Tween currentTween;

	private void Awake()
	{
		rectTransform = GetComponent<RectTransform>();
		originalScale = rectTransform.localScale;
	}

	public void SetHasClicked(bool value)
	{
		_hasClicked = value;
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		if (!_hasClicked)
		{
			currentTween = rectTransform.DOScale(hoverScale, tweenDuration).SetEase(tweenEase);
		}
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		if (!_hasClicked)
		{
			currentTween = rectTransform.DOScale(originalScale, tweenDuration).SetEase(tweenEase);
		}
	}

	private void OnDisable()
	{
		rectTransform.localScale = originalScale;
	}
}

}
