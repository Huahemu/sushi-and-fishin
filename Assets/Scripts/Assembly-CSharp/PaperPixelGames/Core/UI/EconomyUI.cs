using System;
using PaperPixelGames.Core.Economy;
using DG.Tweening;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace PaperPixelGames.Core.UI
{
	public class EconomyUI : MonoBehaviour
	{
		[Header("References")]
		[SerializeField]
		private TextMeshProUGUI _shopBalanceText;

		[SerializeField]
		private TextMeshProUGUI _scrapBalanceText;

		[Header("Fly Feedback")]
		[SerializeField]
		private HudFlyFeedback _hudFlyFeedback;

		[FormerlySerializedAs("_popupParent")]
		[SerializeField]
		private RectTransform _flyFeedbackParent;

		[FormerlySerializedAs("_popupPrefab")]
		[SerializeField]
		private TextMeshProUGUI _flyFeedbackTextPrefab;

		[SerializeField]
		private RectTransform _moneyFeedbackStartAnchor;

		[SerializeField]
		private RectTransform _scrapFeedbackStartAnchor;

		[SerializeField]
		private Color _gainColor = new Color(0.2f, 0.9f, 0.3f);

		[SerializeField]
		private Color _lossColor = new Color(1f, 0.3f, 0.3f);

		[Header("Counter Tween")]
		[SerializeField]
		private float _countDuration = 0.4f;

		[SerializeField]
		private Ease _countEase = Ease.OutCubic;

		[Header("Pulse")]
		[SerializeField]
		private float _pulseScale = 1.08f;

		[SerializeField]
		private float _pulseDuration = 0.12f;

		private int _prevShopBalance;

		private int _prevScrapBalance;

		private Tweener _shopCountTween;

		private Tweener _scrapCountTween;

		private Tweener _shopPulseTween;

		private Tweener _scrapPulseTween;

		private Sequence _scrapInsufficientTween;

		private Sequence _shopInsufficientTween;

		public void SetVisibility(bool visible)
		{
			if (base.gameObject.activeSelf != visible)
			{
				base.gameObject.SetActive(visible);
			}
		}

		private void OnDestroy()
		{
			_shopCountTween?.Kill();
			_scrapCountTween?.Kill();
			_shopPulseTween?.Kill();
			_scrapPulseTween?.Kill();
			_scrapInsufficientTween?.Kill();
			_shopInsufficientTween?.Kill();
		}

        private decimal displayedGoldStored;
        private decimal targetGoldStored;
        private bool goldInitialized;
        private bool displayingInfiniteGold;
        private float goldTweenProgress;

        public void MoneyUpdate(int balance)
        {
            MoneyUpdateExact(balance == int.MaxValue ? 0 : balance, balance == int.MaxValue);
        }

        public void MoneyUpdateExact(decimal balance, bool infinite = false)
        {
            if (!_shopBalanceText) return;
            if (goldInitialized && targetGoldStored == balance && displayingInfiniteGold == infinite) return;
            _shopCountTween?.Kill(false);
            targetGoldStored = balance;
            displayingInfiniteGold = infinite;
            if (!goldInitialized || infinite)
            {
                goldInitialized = true;
                displayedGoldStored = balance;
                _shopBalanceText.text = GoldAmount.FullNumber(balance, infinite);
                return;
            }
            decimal from = displayedGoldStored;
            goldTweenProgress = 0;
            _shopCountTween = DOTween.To(() => goldTweenProgress, value =>
            {
                goldTweenProgress = value;
                decimal fraction = (decimal)value;
                displayedGoldStored = from * (1m - fraction) + balance * fraction;
                _shopBalanceText.text = GoldAmount.FullNumber(displayedGoldStored);
            }, 1f, _countDuration).SetEase(_countEase).SetUpdate(true).OnComplete(() =>
            {
                displayedGoldStored = balance;
                _shopBalanceText.text = GoldAmount.FullNumber(balance);
            });
        }

		public void ScrapUpdate(int balance)
		{
			UtilityMethods.Log($"[EconomyUI] ScrapUpdate:{balance}");
			ApplyBalanceChange(_scrapBalanceText, _prevScrapBalance, balance, isScrap: true);
			_prevScrapBalance = balance;
		}

		public void PlayMoneyGainFeedback(int storedAmount, Action onArrived = null)
		{
			if (storedAmount > 0)
			{
				Singleton<NotifyManager>.Instance?.ShowMoneyGain(storedAmount);
				PulseGainTarget(_shopBalanceText, isScrap: false);
				onArrived?.Invoke();
			}
		}

		public void PlayMoneySpendFeedback(int storedAmount, Action onArrived = null)
		{
			PlaySpendFeedback(storedAmount, includeDollarSign: true, _shopBalanceText, _moneyFeedbackStartAnchor, onArrived);
		}

		public void PlayScrapGainFeedback(int storedAmount, Action onArrived = null)
		{
			if (storedAmount > 0)
			{
				Singleton<NotifyManager>.Instance?.ShowScrapGain(storedAmount);
				PulseGainTarget(_scrapBalanceText, isScrap: true);
				onArrived?.Invoke();
			}
		}

		private void ApplyBalanceChange(TextMeshProUGUI label, int previous, int next, bool isScrap)
		{
			if (label == null)
			{
				return;
			}
			if (MoneyHelper.IsInfinite(next))
			{
				label.text = next.ConvertMoneyText(includeDollarSign: false);
				if (isScrap)
				{
					Pulse(label, ref _scrapPulseTween);
				}
				else
				{
					Pulse(label, ref _shopPulseTween);
				}
			}
			else
			{
				AnimateCounter(label, previous, next, isScrap);
			}
		}

		private void AnimateCounter(TextMeshProUGUI label, int from, int to, bool isScrap)
		{
			if (isScrap)
			{
				_scrapCountTween?.Kill();
			}
			else
			{
				_shopCountTween?.Kill();
			}
			if (isScrap)
			{
				Pulse(label, ref _scrapPulseTween);
			}
			else
			{
				Pulse(label, ref _shopPulseTween);
			}
			int current = from;
			Tweener tweener = DOTween.To(() => current, (int value) =>
			{
				current = value;
				label.text = current.ConvertMoneyText(includeDollarSign: false);
			}, to, _countDuration).SetEase(_countEase);
			if (isScrap)
			{
				_scrapCountTween = tweener;
			}
			else
			{
				_shopCountTween = tweener;
			}
		}

		private void PlaySpendFeedback(int storedAmount, bool includeDollarSign, TextMeshProUGUI target, RectTransform startAnchor, Action onArrived)
		{
			if (storedAmount <= 0)
			{
				return;
			}
			if (target == null)
			{
				onArrived?.Invoke();
				return;
			}
			EnsureFlyFeedbackDefaults();
			RectTransform rectTransform = target.rectTransform;
			RectTransform startAnchor2 = ((startAnchor != null) ? startAnchor : rectTransform);
			string text = "-" + storedAmount.ConvertMoneyText(includeDollarSign);
			if (_hudFlyFeedback == null)
			{
				Pulse(target, ref _shopPulseTween);
				onArrived?.Invoke();
				return;
			}
			_hudFlyFeedback.Play(text, startAnchor2, rectTransform, _lossColor, () =>
			{
				Pulse(target, ref _shopPulseTween);
				onArrived?.Invoke();
			});
		}

		private void PulseGainTarget(TextMeshProUGUI target, bool isScrap)
		{
			if (isScrap)
			{
				Pulse(target, ref _scrapPulseTween);
			}
			else
			{
				Pulse(target, ref _shopPulseTween);
			}
		}

		private void EnsureFlyFeedbackDefaults()
		{
			if (_hudFlyFeedback == null)
			{
				_hudFlyFeedback = GetComponent<HudFlyFeedback>();
			}
			if (_hudFlyFeedback == null)
			{
				_hudFlyFeedback = base.gameObject.AddComponent<HudFlyFeedback>();
			}
			RectTransform flyRoot = ((_flyFeedbackParent != null) ? _flyFeedbackParent : ((_shopBalanceText != null) ? (_shopBalanceText.rectTransform.parent as RectTransform) : (base.transform as RectTransform)));
			TextMeshProUGUI textPrefab = ((_flyFeedbackTextPrefab != null) ? _flyFeedbackTextPrefab : _shopBalanceText);
			_hudFlyFeedback.SetRuntimeDefaults(flyRoot, textPrefab);
		}

		private void Pulse(TextMeshProUGUI label, ref Tweener pulseTweenField)
		{
			pulseTweenField?.Kill();
			RectTransform rectTransform = label.rectTransform;
			rectTransform.localScale = Vector3.one;
			pulseTweenField = rectTransform.DOPunchScale(Vector3.one * (_pulseScale - 1f), _pulseDuration, 1, 0.5f);
		}

		public void FlashShopInsufficient()
		{
			FlashInsufficientLabel(_shopBalanceText, ref _shopInsufficientTween, ref _shopPulseTween, Color.white);
		}

		public void FlashScrapInsufficient()
		{
			FlashInsufficientLabel(_scrapBalanceText, ref _scrapInsufficientTween, ref _scrapPulseTween, Color.white);
		}

		private void FlashInsufficientLabel(TextMeshProUGUI label, ref Sequence colorTweenField, ref Tweener pulseTweenField, Color defaultColor)
		{
			if (!(label == null))
			{
				colorTweenField?.Kill();
				pulseTweenField?.Kill();
				RectTransform rectTransform = label.rectTransform;
				rectTransform.localScale = Vector3.one;
				pulseTweenField = rectTransform.DOPunchScale(Vector3.one * (_pulseScale - 1f), _pulseDuration, 1, 0.5f);
				Color color = label.color;
				Sequence sequence = DOTween.Sequence();
				sequence.Append(DOVirtual.Color(color, _lossColor, _pulseDuration, (Color color2) =>
				{
					label.color = color2;
				}));
				sequence.AppendInterval(_pulseDuration);
				sequence.Append(DOVirtual.Color(Color.white, defaultColor, _pulseDuration, (Color color2) =>
				{
					label.color = color2;
				}));
				colorTweenField = sequence;
			}
		}
	}
}
