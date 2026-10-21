using System;
using DG.Tweening;
using PaperPixelGames.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Framework.UI.Notify
{
	public class Notification : MonoBehaviour
	{
		[Header("References")]
		[SerializeField]
		private RectTransform _root;

		[SerializeField]
		private CanvasGroup _canvasGroup;

		[SerializeField]
		private Image _backgroundImage;

		[SerializeField]
		private TMP_Text _descriptionText;

		[Header("Background Colors")]
		[SerializeField]
		private Sprite _infoBackgroundSprite;

		[SerializeField]
		private Sprite _successBackgroundSprite;

		[SerializeField]
		private Sprite _errorBackgroundSprite;

		[Header("Description Colors")]
		[SerializeField]
		private Color _infoDescriptionColor = Color.white;

		[SerializeField]
		private Color _successDescriptionColor = Color.white;

		[SerializeField]
		private Color _errorDescriptionColor = Color.white;

		[Header("Animation")]
		[SerializeField]
		private Vector2 _showFromOffset = new Vector2(500f, 0f);

		[SerializeField]
		private Vector2 _hideToOffset = new Vector2(500f, 0f);

		[SerializeField]
		private float _showDuration = 0.25f;

		[SerializeField]
		private float _hideDuration = 0.2f;

		[SerializeField]
		private Ease _showEase = Ease.OutCubic;

		[SerializeField]
		private Ease _hideEase = Ease.InCubic;

		[SerializeField]
		private bool _useUnscaledTime = true;

		private Sequence _sequence;

		private Tween _autoHideTween;

		private Vector2 _shownAnchoredPosition;

		private bool _isInitialized;

		public bool IsShowing { get; private set; }

		public event Action<Notification> Hidden;

		private void Awake()
		{
			Initialize();
		}

		private void OnDestroy()
		{
			KillTweens();
		}

		public void Initialize()
		{
			if (!_isInitialized)
			{
				if (_root == null)
				{
					_root = base.transform as RectTransform;
				}
				if (_canvasGroup == null)
				{
					_canvasGroup = GetComponent<CanvasGroup>();
				}
				if (_root != null)
				{
					_shownAnchoredPosition = _root.anchoredPosition;
				}
				SetVisibleImmediate(visible: false);
				_isInitialized = true;
			}
		}

		public void Show(string description, NotificationState state, float duration, bool autoClose = true)
		{
			Initialize();
			if (!(_root == null) && !(_canvasGroup == null))
			{
				ApplyVisualState(state);
				ApplyTexts(description);
				PlayShowAnimation();
				RestartLifetime(duration, autoClose);
			}
		}

		public void RestartLifetime(float duration, bool autoClose)
		{
			StopAutoHide();
			if (autoClose)
			{
				_autoHideTween = DOVirtual.DelayedCall(duration, Hide, _useUnscaledTime);
			}
		}

		public void Hide()
		{
			Initialize();
			StopAutoHide();
			PlayHideAnimation();
		}

		public void HideImmediate()
		{
			Initialize();
			StopAutoHide();
			KillSequence();
			IsShowing = false;
			SetVisibleImmediate(visible: false);
			Hidden?.Invoke(this);
		}

		private void ApplyTexts(string description)
		{
			if (_descriptionText != null)
			{
				ArabicTextPreprocessor.Install(_descriptionText);
				_descriptionText.text = description ?? string.Empty;
			}
		}

		private void ApplyVisualState(NotificationState state)
		{
			if (_backgroundImage != null)
			{
				_backgroundImage.sprite = GetBackgroundSprite(state);
			}
			if (_descriptionText != null)
			{
				_descriptionText.color = GetDescriptionColor(state);
			}
		}

		private Sprite GetBackgroundSprite(NotificationState state)
		{
			return state switch
			{
				NotificationState.Success => _successBackgroundSprite, 
				NotificationState.Error => _errorBackgroundSprite, 
				_ => _infoBackgroundSprite, 
			};
		}

		private Color GetDescriptionColor(NotificationState state)
		{
			return state switch
			{
				NotificationState.Success => _successDescriptionColor, 
				NotificationState.Error => _errorDescriptionColor, 
				_ => _infoDescriptionColor, 
			};
		}

		private void PlayShowAnimation()
		{
			KillSequence();
			if (!(_root == null) && !(_canvasGroup == null))
			{
				IsShowing = true;
				_root.gameObject.SetActive(value: true);
				_root.anchoredPosition = _shownAnchoredPosition + _showFromOffset;
				_canvasGroup.alpha = 0f;
				_sequence = DOTween.Sequence().SetUpdate(_useUnscaledTime).Append(_canvasGroup.DOFade(1f, _showDuration).SetEase(_showEase))
					.Join(_root.DOAnchorPos(_shownAnchoredPosition, _showDuration).SetEase(_showEase));
			}
		}

		private void PlayHideAnimation()
		{
			KillSequence();
			if (_root == null || _canvasGroup == null)
			{
				IsShowing = false;
				SetVisibleImmediate(visible: false);
				Hidden?.Invoke(this);
				return;
			}
			Vector2 endValue = _shownAnchoredPosition + _hideToOffset;
			_sequence = DOTween.Sequence().SetUpdate(_useUnscaledTime).Append(_canvasGroup.DOFade(0f, _hideDuration).SetEase(_hideEase))
				.Join(_root.DOAnchorPos(endValue, _hideDuration).SetEase(_hideEase))
				.OnComplete(() =>
				{
					IsShowing = false;
					SetVisibleImmediate(visible: false);
					Hidden?.Invoke(this);
				});
		}

		private void SetVisibleImmediate(bool visible)
		{
			if (!(_root == null) && !(_canvasGroup == null))
			{
				if (visible)
				{
					_root.gameObject.SetActive(value: true);
					_root.anchoredPosition = _shownAnchoredPosition;
					_canvasGroup.alpha = 1f;
				}
				else
				{
					_canvasGroup.alpha = 0f;
					_root.anchoredPosition = _shownAnchoredPosition;
					_root.gameObject.SetActive(value: false);
				}
			}
		}

		private void StopAutoHide()
		{
			if (_autoHideTween != null && _autoHideTween.IsActive())
			{
				_autoHideTween.Kill();
			}
			_autoHideTween = null;
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
			StopAutoHide();
			KillSequence();
		}
	}
}
