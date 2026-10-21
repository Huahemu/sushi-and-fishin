using DG.Tweening;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Quests
{
	public class QuestUIQuestRow : MonoBehaviour
	{
		[Header("Text")]
		[SerializeField]
		private TextMeshProUGUI _text;

		[Header("Progress Optional")]
		[SerializeField]
		private Slider _progressSlider;

		[SerializeField]
		private bool _showProgressInText = true;

		[Header("Visual")]
		[SerializeField]
		private GameObject _completeIcon;

		[SerializeField]
		private CanvasGroup _canvasGroup;

		[Header("Feedback")]
		[SerializeField]
		private RectTransform _animRoot;

		[SerializeField]
		private AudioClip _completeSfx;

		[SerializeField]
		private float _completeSfxVolume = 0.55f;

		[Header("State Visual")]
		[SerializeField]
		[Range(0f, 1f)]
		private float _completedAlpha = 0.8f;

		private QuestSO _quest;

		private bool _isCompleted;

		private int _currentProgress;

		private int _targetProgress;

		public QuestSO Quest => _quest;

		public bool IsCompleted => _isCompleted;

		public void Setup(QuestSO quest, bool isCompleted, int currentProgress, int targetProgress)
		{
			_quest = quest;
			_isCompleted = isCompleted;
			_currentProgress = Mathf.Max(0, currentProgress);
			_targetProgress = Mathf.Max(1, targetProgress);
			UpdateText();
			UpdateProgressVisual();
			ApplyCompletedState(playFeedback: false);
		}

		public void SetProgress(int current, int target)
		{
			_currentProgress = Mathf.Max(0, current);
			_targetProgress = Mathf.Max(1, target);
			UpdateText();
			UpdateProgressVisual();
			PlayProgressFeedback();
		}

		public void SetCompleted(bool isCompleted, bool playFeedback)
		{
			if (_isCompleted != isCompleted)
			{
				_isCompleted = isCompleted;
				if (_isCompleted && _quest != null)
				{
					_currentProgress = _quest.TargetAmount;
					_targetProgress = _quest.TargetAmount;
				}
				UpdateText();
				UpdateProgressVisual();
				ApplyCompletedState(playFeedback);
			}
		}

		private void UpdateText()
		{
			if (!(_text == null))
			{
				string text = ((_quest != null) ? _quest.name : "Quest");
				string text2 = ((_quest != null && !string.IsNullOrWhiteSpace(_quest.LocalizedTitle)) ? _quest.LocalizedTitle : text);
				if (_quest != null && _quest.IsCountable && _showProgressInText)
				{
					_text.text = $"{text2} ({_currentProgress}/{_targetProgress})";
				}
				else
				{
					_text.text = text2;
				}
			}
		}

		private void UpdateProgressVisual()
		{
			if (!(_progressSlider == null))
			{
				bool flag = _quest != null && _quest.IsCountable;
				_progressSlider.gameObject.SetActive(flag);
				if (flag)
				{
					_progressSlider.minValue = 0f;
					_progressSlider.maxValue = _targetProgress;
					_progressSlider.value = Mathf.Clamp(_currentProgress, 0, _targetProgress);
				}
			}
		}

		private void ApplyCompletedState(bool playFeedback)
		{
			if (_text != null)
			{
				if (_isCompleted)
				{
					_text.fontStyle |= FontStyles.Strikethrough;
				}
				else
				{
					_text.fontStyle &= ~FontStyles.Strikethrough;
				}
			}
			if (_completeIcon != null)
			{
				_completeIcon.SetActive(_isCompleted);
			}
			if (_canvasGroup != null)
			{
				_canvasGroup.alpha = (_isCompleted ? _completedAlpha : 1f);
			}
			if (playFeedback && _isCompleted)
			{
				PlayCompleteFeedback();
			}
		}

		private void PlayProgressFeedback()
		{
			if (!(_animRoot == null))
			{
				_animRoot.DOKill();
				_animRoot.DOPunchScale(new Vector3(0.04f, 0.04f, 0f), 0.14f, 7, 0.85f);
			}
		}

		private void PlayCompleteFeedback()
		{
			if (_animRoot != null)
			{
				_animRoot.DOKill();
				_animRoot.DOPunchScale(new Vector3(0.09f, 0.09f, 0f), 0.18f, 9, 0.95f);
			}
			if (_completeIcon != null && _completeIcon.transform is RectTransform rectTransform)
			{
				rectTransform.DOKill();
				rectTransform.localScale = Vector3.zero;
				rectTransform.DOScale(1f, 0.18f).SetEase(Ease.OutBack);
			}
			if (_completeSfx != null && base.gameObject.activeInHierarchy)
			{
				Camera main = Camera.main;
				if (!(main != null))
				{
					_ = Vector3.zero;
				}
				else
				{
					_ = main.transform.position;
				}
				Singleton<SoundManager>.Instance.Play2DClip(_completeSfx, _completeSfxVolume, 1f);
			}
		}

		private void OnDestroy()
		{
			if (_animRoot != null)
			{
				_animRoot.DOKill();
			}
			if (_completeIcon != null && _completeIcon.transform is RectTransform target)
			{
				target.DOKill();
			}
		}
	}
}
