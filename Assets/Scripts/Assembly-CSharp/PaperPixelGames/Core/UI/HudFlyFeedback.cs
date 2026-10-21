using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace PaperPixelGames.Core.UI
{
	public class HudFlyFeedback : MonoBehaviour
	{
		[Header("References")]
		[SerializeField]
		private RectTransform _flyRoot;

		[SerializeField]
		private TextMeshProUGUI _textPrefab;

		[Header("Motion")]
		[SerializeField]
		[Min(0.01f)]
		private float _duration = 0.55f;

		[SerializeField]
		private Ease _moveEase = Ease.OutCubic;

		[SerializeField]
		private Ease _scaleEase = Ease.OutBack;

		[SerializeField]
		private Vector2 _startJitter = new Vector2(20f, 10f);

		[SerializeField]
		private float _startScale = 0.85f;

		[SerializeField]
		private float _endScale = 0.55f;

		[SerializeField]
		private float _fadeOutStart = 0.65f;

		public void SetRuntimeDefaults(RectTransform flyRoot, TextMeshProUGUI textPrefab)
		{
			if (_flyRoot == null)
			{
				_flyRoot = flyRoot;
			}
			if (_textPrefab == null)
			{
				_textPrefab = textPrefab;
			}
		}

		public void Play(string text, RectTransform startAnchor, RectTransform target, Color color, Action onArrived = null)
		{
			RectTransform rectTransform = ((_flyRoot != null) ? _flyRoot : (base.transform as RectTransform));
			TextMeshProUGUI textPrefab = _textPrefab;
			if (rectTransform == null || textPrefab == null || target == null)
			{
				onArrived?.Invoke();
				return;
			}
			TextMeshProUGUI instance = UnityEngine.Object.Instantiate(textPrefab, rectTransform);
			instance.gameObject.SetActive(value: true);
			instance.text = text;
			instance.color = color;
			instance.alpha = 1f;
			RectTransform rectTransform2 = instance.rectTransform;
			rectTransform2.localScale = Vector3.one * _startScale;
			rectTransform2.anchoredPosition = ResolveAnchoredPosition(startAnchor, rectTransform);
			rectTransform2.anchoredPosition += new Vector2(UnityEngine.Random.Range(0f - _startJitter.x, _startJitter.x), UnityEngine.Random.Range(0f - _startJitter.y, _startJitter.y));
			Vector2 endValue = ResolveAnchoredPosition(target, rectTransform);
			float num = Mathf.Clamp01(_fadeOutStart) * _duration;
			float duration = Mathf.Max(0.01f, _duration - num);
			Sequence sequence = DOTween.Sequence();
			sequence.Join(rectTransform2.DOAnchorPos(endValue, _duration).SetEase(_moveEase));
			sequence.Join(rectTransform2.DOScale(_endScale, _duration).SetEase(_scaleEase));
			sequence.Insert(num, instance.DOFade(0f, duration).SetEase(Ease.InQuad));
			sequence.OnComplete(() =>
			{
				if (instance != null)
				{
					UnityEngine.Object.Destroy(instance.gameObject);
				}
				onArrived?.Invoke();
			});
		}

		private static Vector2 ResolveAnchoredPosition(RectTransform source, RectTransform root)
		{
			if (source == null)
			{
				return Vector2.zero;
			}
			Canvas componentInParent = root.GetComponentInParent<Canvas>();
			Camera cam = ((componentInParent != null && componentInParent.renderMode != RenderMode.ScreenSpaceOverlay) ? componentInParent.worldCamera : null);
			Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, source.position);
			if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPoint, cam, out var localPoint))
			{
				return Vector2.zero;
			}
			return localPoint;
		}
	}
}
