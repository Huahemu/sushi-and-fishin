using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Framework.UI.Notify
{
	public sealed class ProgressFeedbackItem : MonoBehaviour
	{
		[Header("References")]
		[SerializeField]
		private RectTransform _animatedRoot;

		[SerializeField]
		private CanvasGroup _canvasGroup;

		[SerializeField]
		private Image _iconImage;

		[SerializeField]
		private TMP_Text _primaryText;

		[SerializeField]
		private Image _accentImage;

		[SerializeField]
		private Image _badgeImage;

		[Header("Animation")]
		[SerializeField]
		private Vector2 _showFromOffset = new Vector2(28f, 0f);

		[SerializeField]
		[Min(0.01f)]
		private float _showDuration = 0.2f;

		[SerializeField]
		[Min(0.01f)]
		private float _hideDuration = 0.16f;

		[SerializeField]
		private float _showStartScale = 0.94f;

		[SerializeField]
		private Vector3 _mergePunchScale = new Vector3(0.06f, 0.06f, 0f);

		[SerializeField]
		[Min(0.01f)]
		private float _mergePunchDuration = 0.16f;

		[SerializeField]
		private Ease _showEase = Ease.OutCubic;

		[SerializeField]
		private Ease _hideEase = Ease.InCubic;

		[SerializeField]
		private bool _useUnscaledTime = true;

		private Sequence _sequence;

		private Tween _lifetimeTween;

		private Tween _mergePunchTween;

		private Vector2 _shownAnchoredPosition;

		private bool _isInitialized;

		public string MergeKey { get; private set; }

		public int Amount { get; private set; }

		public float LastUpdateTime { get; private set; }

		public event Action<ProgressFeedbackItem> Hidden;

		private void Awake()
		{
			Initialize();
		}

		private void OnDestroy()
		{
			KillTweens();
		}

		public void Show(string mergeKey, int amount, string primaryText, string secondaryText, Sprite icon, Sprite badge, Color accentColor, float duration)
		{
			Initialize();
			MergeKey = mergeKey ?? string.Empty;
			Amount = amount;
			LastUpdateTime = Time.unscaledTime;
			ApplyText(primaryText, secondaryText);
			ApplyIcon(icon);
			ApplyBadge(badge);
			if (_accentImage != null)
			{
				_accentImage.color = accentColor;
			}
			base.gameObject.SetActive(value: true);
			PlayShowAnimation();
			RestartLifetime(duration);
		}

		public void MergeAmount(int amount, string primaryText, float duration)
		{
			Amount = amount;
			LastUpdateTime = Time.unscaledTime;
			if (_primaryText != null)
			{
				_primaryText.text = primaryText ?? string.Empty;
			}
			RestartLifetime(duration);
			PlayMergePulse();
		}

		public void ReleaseImmediate()
		{
			Initialize();
			KillTweens();
			MergeKey = string.Empty;
			Amount = 0;
			LastUpdateTime = 0f;
			if (_canvasGroup != null)
			{
				_canvasGroup.alpha = 0f;
			}
			if (_animatedRoot != null)
			{
				_animatedRoot.anchoredPosition = _shownAnchoredPosition;
				_animatedRoot.localScale = Vector3.one;
			}
			base.gameObject.SetActive(value: false);
		}

		private void Initialize()
		{
			if (!_isInitialized)
			{
				if (_animatedRoot == null)
				{
					_animatedRoot = base.transform as RectTransform;
				}
				if (_canvasGroup == null)
				{
					_canvasGroup = GetComponent<CanvasGroup>();
				}
				if (_animatedRoot != null)
				{
					_shownAnchoredPosition = _animatedRoot.anchoredPosition;
				}
				_isInitialized = true;
			}
		}

		private void ApplyText(string primaryText, string secondaryText)
		{
			bool flag = !string.IsNullOrWhiteSpace(secondaryText);
			if (_primaryText != null)
			{
				_primaryText.text = (flag ? ((primaryText ?? string.Empty) + "\n" + secondaryText) : (primaryText ?? string.Empty));
			}
		}

		private void ApplyIcon(Sprite icon)
		{
			bool active = icon != null;
			if (_iconImage != null)
			{
				_iconImage.gameObject.SetActive(active);
				_iconImage.sprite = icon;
			}
		}

		private void ApplyBadge(Sprite badge)
		{
			if (!(_badgeImage == null))
			{
				_badgeImage.gameObject.SetActive(badge != null);
				_badgeImage.sprite = badge;
			}
		}

		private void PlayShowAnimation()
		{
			KillSequence();
			if (!(_animatedRoot == null) && !(_canvasGroup == null))
			{
				_animatedRoot.anchoredPosition = _shownAnchoredPosition + _showFromOffset;
				_animatedRoot.localScale = Vector3.one * _showStartScale;
				_canvasGroup.alpha = 0f;
				_sequence = DOTween.Sequence().SetUpdate(_useUnscaledTime).Append(_canvasGroup.DOFade(1f, _showDuration).SetEase(_showEase))
					.Join(_animatedRoot.DOAnchorPos(_shownAnchoredPosition, _showDuration).SetEase(_showEase))
					.Join(_animatedRoot.DOScale(1f, _showDuration).SetEase(Ease.OutBack));
			}
		}

		private void RestartLifetime(float duration)
		{
			if (_lifetimeTween != null && _lifetimeTween.IsActive())
			{
				_lifetimeTween.Kill();
			}
			_lifetimeTween = DOVirtual.DelayedCall(Mathf.Max(0.01f, duration), Hide, _useUnscaledTime);
		}

		private void PlayMergePulse()
		{
			if (!(_animatedRoot == null))
			{
				if (_mergePunchTween != null && _mergePunchTween.IsActive())
				{
					_mergePunchTween.Kill();
				}
				_animatedRoot.localScale = Vector3.one;
				_mergePunchTween = _animatedRoot.DOPunchScale(_mergePunchScale, _mergePunchDuration, 5, 0.7f).SetUpdate(_useUnscaledTime);
			}
		}

		private void Hide()
		{
			KillSequence();
			if (_animatedRoot == null || _canvasGroup == null)
			{
				CompleteHide();
			}
			else
			{
				_sequence = DOTween.Sequence().SetUpdate(_useUnscaledTime).Append(_canvasGroup.DOFade(0f, _hideDuration).SetEase(_hideEase))
					.Join(_animatedRoot.DOAnchorPos(_shownAnchoredPosition - _showFromOffset, _hideDuration).SetEase(_hideEase))
					.OnComplete(CompleteHide);
			}
		}

		private void CompleteHide()
		{
			Hidden?.Invoke(this);
		}

		private void KillSequence()
		{
			if (_sequence != null && _sequence.IsActive())
			{
				_sequence.Kill();
			}
			_sequence = null;
		}

		private void KillTweens()
		{
			KillSequence();
			if (_lifetimeTween != null && _lifetimeTween.IsActive())
			{
				_lifetimeTween.Kill();
			}
			if (_mergePunchTween != null && _mergePunchTween.IsActive())
			{
				_mergePunchTween.Kill();
			}
			_lifetimeTween = null;
			_mergePunchTween = null;
		}
	}
}
