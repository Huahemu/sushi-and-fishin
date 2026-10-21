using DG.Tweening;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.UI;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Progression
{
	public class ProgressionUI : MonoBehaviour
	{
		[SerializeField]
		private TextMeshProUGUI _levelText;

		[SerializeField]
		private TextMeshProUGUI _xpText;

		[SerializeField]
		private Image _xpSlider;

		[Header("Feedback")]
		[SerializeField]
		private HudFlyFeedback _xpFlyFeedback;

		[SerializeField]
		private RectTransform _xpFeedbackRoot;

		[SerializeField]
		private TextMeshProUGUI _xpFeedbackTextPrefab;

		[SerializeField]
		private RectTransform _xpFeedbackStartAnchor;

		[SerializeField]
		private RectTransform _xpFeedbackTarget;

		[SerializeField]
		private RectTransform _xpSliderPunchTarget;

		[SerializeField]
		private Color _xpFeedbackColor = new Color(0.45f, 0.9f, 1f);

		[SerializeField]
		[Min(0.01f)]
		private float _xpSliderTweenDuration = 0.45f;

		[SerializeField]
		private Ease _xpSliderTweenEase = Ease.OutCubic;

		[SerializeField]
		private Vector3 _xpSliderPunchScale = new Vector3(0.06f, 0.06f, 0f);

		[SerializeField]
		[Min(0.01f)]
		private float _xpSliderPunchDuration = 0.16f;

		private ProgressionManager _progressionManager;

		private Tweener _xpSliderTween;

		private Tweener _xpSliderPunchTween;

		public void SetVisibility(bool visible)
		{
			if (base.gameObject.activeSelf != visible)
			{
				base.gameObject.SetActive(visible);
			}
		}

		private void OnEnable()
		{
			TryBind();
			Refresh();
		}

		private void OnDisable()
		{
			if (_progressionManager != null)
			{
				_progressionManager.ProgressionChanged -= OnProgressionChanged;
				_progressionManager.XpFeedbackRequested -= OnXpFeedbackRequested;
			}
			KillFeedbackTweens();
		}

		private void OnDestroy()
		{
			KillFeedbackTweens();
		}

		private void TryBind()
		{
			ProgressionManager progressionManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.ProgressionManager : null);
			if (!(_progressionManager == progressionManager))
			{
				if (_progressionManager != null)
				{
					_progressionManager.ProgressionChanged -= OnProgressionChanged;
					_progressionManager.XpFeedbackRequested -= OnXpFeedbackRequested;
				}
				_progressionManager = progressionManager;
				if (_progressionManager != null)
				{
					_progressionManager.ProgressionChanged += OnProgressionChanged;
					_progressionManager.XpFeedbackRequested += OnXpFeedbackRequested;
				}
			}
		}

		private void OnProgressionChanged(int level, double currentXp, double nextLevelRequiredTotalXp)
		{
			Refresh(level, currentXp, nextLevelRequiredTotalXp);
		}

		private void OnXpFeedbackRequested(int amount, int previousLevel, double previousCurrentXp, double previousNextLevelRequiredXp, int level, double currentXp, double nextLevelRequiredXp)
		{
			if (amount > 0)
			{
				PlayXpGainFeedback(amount, previousLevel, previousCurrentXp, previousNextLevelRequiredXp, level, currentXp, nextLevelRequiredXp);
			}
		}

		private void Refresh()
		{
			TryBind();
			if (_progressionManager == null)
			{
				Refresh(1, 0.0, -1.0);
			}
			else
			{
				Refresh(_progressionManager.CurrentLevel, _progressionManager.CurrentXp, _progressionManager.NextLevelRequiredTotalXp);
			}
		}

		private void Refresh(int level, double currentXp, double nextLevelRequiredTotalXp)
		{
			if (_levelText != null)
			{
				_levelText.text = $"Lv. {level}";
			}
			double num = ((_progressionManager != null) ? _progressionManager.NextLevelRequiredXp : nextLevelRequiredTotalXp);
			if (_xpText != null)
			{
				_xpText.text = ((num < 0.0) ? (FormatXp(currentXp) + " XP") : (FormatXp(currentXp) + " / " + FormatXp(num) + " XP"));
			}
			if (!(_xpSlider == null))
			{
				_xpSlider.fillAmount = CalculateFillAmount(currentXp, num);
			}
		}

		private void PlayXpGainFeedback(int amount, int previousLevel, double previousCurrentXp, double previousNextLevelRequiredXp, int level, double currentXp, double nextLevelRequiredXp)
		{
			RefreshXpText(level, currentXp, nextLevelRequiredXp);
			if (_xpSlider != null)
			{
				_xpSliderTween?.Kill();
				_xpSlider.fillAmount = CalculateFillAmount(previousCurrentXp, previousNextLevelRequiredXp);
				_xpSliderTween = _xpSlider.DOFillAmount(CalculateFillAmount(currentXp, nextLevelRequiredXp), _xpSliderTweenDuration).SetEase(_xpSliderTweenEase);
			}
			Singleton<NotifyManager>.Instance?.ShowXpGain(amount);
			CompleteXpFeedbackArrival();
		}

		private void CompleteXpFeedbackArrival()
		{
			RectTransform rectTransform = ((_xpSliderPunchTarget != null) ? _xpSliderPunchTarget : ResolveXpFeedbackTarget());
			if (rectTransform != null)
			{
				_xpSliderPunchTween?.Kill();
				rectTransform.localScale = Vector3.one;
				_xpSliderPunchTween = rectTransform.DOPunchScale(_xpSliderPunchScale, _xpSliderPunchDuration, 6, 0.75f);
			}
			_progressionManager?.PlayXpGainSfxLocal();
		}

		private void RefreshXpText(int level, double currentXp, double nextLevelRequiredXp)
		{
			if (_levelText != null)
			{
				_levelText.text = $"Lv. {level}";
			}
			if (!(_xpText == null))
			{
				_xpText.text = ((nextLevelRequiredXp < 0.0) ? (FormatXp(currentXp) + " XP") : (FormatXp(currentXp) + " / " + FormatXp(nextLevelRequiredXp) + " XP"));
			}
		}

		private RectTransform ResolveXpFeedbackTarget()
		{
			if (_xpFeedbackTarget != null)
			{
				return _xpFeedbackTarget;
			}
			if (!(_xpSlider != null))
			{
				if (!(_xpText != null))
				{
					return base.transform as RectTransform;
				}
				return _xpText.rectTransform;
			}
			return _xpSlider.rectTransform;
		}

		private static float CalculateFillAmount(double currentXp, double nextLevelRequiredXp)
		{
			if (nextLevelRequiredXp < 0.0)
			{
				return 1f;
			}
			return Mathf.Clamp01((float)(currentXp / nextLevelRequiredXp));
		}

		private void KillFeedbackTweens()
		{
			_xpSliderTween?.Kill();
			_xpSliderTween = null;
			_xpSliderPunchTween?.Kill();
			_xpSliderPunchTween = null;
		}

		private static string FormatXp(double value)
		{
			return value.ToString("0");
		}
	}
}
