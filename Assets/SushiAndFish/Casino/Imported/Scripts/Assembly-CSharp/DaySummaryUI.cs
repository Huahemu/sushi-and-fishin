namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::DG.Tweening;
using global::DG.Tweening.Core;
using global::DG.Tweening.Plugins.Options;
using SushiAndFish.Casino.Original.Extensions;
using global::FMODUnity;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.Events;

public class DaySummaryUI : MonoBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private float canvasAlphaDuration = 1f;

	[SerializeField]
	private float initialDelay = 1f;

	[SerializeField]
	private float titleAnimationDuration = 2f;

	[SerializeField]
	private float quotaBarFillDuration = 1f;

	[SerializeField]
	private float moneyTextsDuration = 1f;

	[SerializeField]
	private float profitGraphDuration = 2f;

	[SerializeField]
	private float ticketRewardDuration = 0.5f;

	[SerializeField]
	private float delayBetweenTicketReward = 0.5f;

	[SerializeField]
	private float finalTicketRewardDuration = 1f;

	[SerializeField]
	private float finalDelayBeforeSkip = 1f;

	[SerializeField]
	private float appearanceDuration = 0.5f;

	[SerializeField]
	private float delayBetweenAnimations = 0.5f;

	[SerializeField]
	private GameObject SkipKeyPrompt;

	[Header("UI")]
	[SerializeField]
	private CanvasGroup[] canvasGroups;

	[SerializeField]
	private SkipUI skipUI;

	[Header("Header")]
	[SerializeField]
	private TextMeshProUGUI titleText;

	[Header("Money")]
	[SerializeField]
	private TextMeshProUGUI profitText;

	[SerializeField]
	private TextMeshProUGUI balanceText;

	[Header("Quota")]
	[SerializeField]
	private TextMeshProUGUI[] quotaText;

	[SerializeField]
	private DaySummaryQuotaSlider quotaSlider;

	[Header("Ticket Sources")]
	[SerializeField]
	private Transform ticketSourcesRoot;

	[SerializeField]
	private DaySummaryTicketEntry ticketSourceEntryPrefab;

	[SerializeField]
	private TextMeshProUGUI totalTicketsText;

	[Header("SFX")]
	[SerializeField]
	private EventReference textChangeSfx;

	[SerializeField]
	private EventReference ticketTextChangeSfx;

	[SerializeField]
	private EventReference textBlopSfx;

	[SerializeField]
	private SFXLoopComponent statsLoopComponent;

	private List<DaySummaryTicketEntry> _ticketEntries = new List<DaySummaryTicketEntry>();

	private PayoutTracker _payoutTracker;

	private LocalManager _localManager;

	private MoneyManager _moneyManager;

	private GameManager _gameManager;

	private GameSettings _gameSettings;

	private int _pendingSegmentSkips;

	private void Awake()
	{
		SetReferences();
	}

	private void SetReferences()
	{
		if (!_payoutTracker)
		{
			_payoutTracker = NetworkSingleton<PayoutTracker>.Instance;
		}
		if (!_localManager)
		{
			_localManager = MonoSingleton<LocalManager>.Instance;
		}
		if (!_moneyManager)
		{
			_moneyManager = NetworkSingleton<MoneyManager>.Instance;
		}
		if (!_gameManager)
		{
			_gameManager = NetworkSingleton<GameManager>.Instance;
		}
		if (!_gameSettings)
		{
			_gameSettings = SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
		}
	}

	private void OnEnable()
	{
		SkipUI obj = skipUI;
		obj.OnSkipped = (UnityAction)Delegate.Combine(obj.OnSkipped, new UnityAction(OnSkip));
		InputEvents.OnSkipUIEvent = (Action<bool>)Delegate.Combine(InputEvents.OnSkipUIEvent, new Action<bool>(OnSkipUIEvent));
	}

	private void OnDisable()
	{
		SkipUI obj = skipUI;
		obj.OnSkipped = (UnityAction)Delegate.Remove(obj.OnSkipped, new UnityAction(OnSkip));
		InputEvents.OnSkipUIEvent = (Action<bool>)Delegate.Remove(InputEvents.OnSkipUIEvent, new Action<bool>(OnSkipUIEvent));
	}

	private void OnSkipUIEvent(bool isPressed)
	{
		if (isPressed)
		{
			_pendingSegmentSkips++;
		}
	}

	private void OnSkip()
	{
		CanvasGroup[] array = canvasGroups;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].DOFade(0f, 1f).SetDelay(1f);
		}
		statsLoopComponent.LoopSFX(play: false);
	}

	public void Show()
	{
		Reset();
		EmoteWheelController emoteWheelController = UnityEngine.Object.FindAnyObjectByType<EmoteWheelController>(FindObjectsInactive.Include);
		if ((bool)emoteWheelController)
		{
			emoteWheelController.SetEmoteWheelActive(active: false);
		}
		StartCoroutine(DaySummaryRoutine());
	}

	private bool ConsumeSkipRequest()
	{
		if (_pendingSegmentSkips <= 0)
		{
			return false;
		}
		_pendingSegmentSkips--;
		return true;
	}

	private IEnumerator WaitForSecondsOrSkip(float duration, Action onSkip)
	{
		float elapsed = 0f;
		while (elapsed < duration)
		{
			if (ConsumeSkipRequest())
			{
				onSkip?.Invoke();
				break;
			}
			elapsed += Time.deltaTime;
			yield return null;
		}
	}

	private IEnumerator TrackRoutine(IEnumerator routine, Action onComplete)
	{
		yield return routine;
		onComplete?.Invoke();
	}

	private IEnumerator DaySummaryRoutine()
	{
		base.gameObject.SetActive(value: true);
		statsLoopComponent.LoopSFX(play: true);
		SetReferences();
		if (!_payoutTracker || !_localManager || !_moneyManager || !_gameManager || !_gameSettings)
		{
			yield break;
		}
		skipUI.Reset();
		skipUI.SetSkippableServer();
		_pendingSegmentSkips = 0;
		quotaSlider.Reset();
		CanvasGroup[] array = canvasGroups;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].alpha = 0f;
		}
		titleText.text = "";
		int dayIndex = _gameManager.daysPassed + 1;
		long dayStartBalance = _moneyManager.dayStartBalance;
		long profit = _moneyManager.balance - dayStartBalance;
		int quotaExcessTickets = 0;
		if (_gameManager.currentQuota > 0 && _gameManager.currentFloor >= 0 && _gameManager.currentFloor < _gameSettings.floorData.Count)
		{
			quotaExcessTickets = _gameSettings.GetQuotaExcessReward(_gameManager.currentFloor, _gameManager.currentQuota, _moneyManager.balance);
		}
		string text = MoneyFormatter.FormatWithDollar(_gameManager.currentQuota) ?? "";
		TextMeshProUGUI[] array2 = quotaText;
		for (int i = 0; i < array2.Length; i++)
		{
			array2[i].text = text;
		}
		balanceText.text = MoneyFormatter.FormatWithDollar(dayStartBalance) ?? "";
		profitText.text = "+$0";
		_ticketEntries.Clear();
		for (int num = ticketSourcesRoot.childCount - 1; num >= 0; num--)
		{
			UnityEngine.Object.Destroy(ticketSourcesRoot.GetChild(num).gameObject);
		}
		int totalTickets = 0;
		totalTicketsText.text = "0";
		SkipKeyPrompt.SetActive(value: true);
		TweenerCore<float, float, FloatOptions> introTween = canvasGroups[0].DOFade(1f, canvasAlphaDuration);
		yield return WaitForSecondsOrSkip(canvasAlphaDuration, () =>
		{
			introTween.Complete();
		});
		yield return WaitForSecondsOrSkip(initialDelay, null);
		titleText.text = $"Stats for day {dayIndex}";
		yield return WaitForSecondsOrSkip(titleAnimationDuration, null);
		TweenerCore<float, float, FloatOptions> quotaHeadersTweenA = canvasGroups[1].DOFade(1f, appearanceDuration);
		TweenerCore<float, float, FloatOptions> quotaHeadersTweenB = canvasGroups[2].DOFade(1f, appearanceDuration);
		yield return WaitForSecondsOrSkip(appearanceDuration + delayBetweenAnimations, () =>
		{
			quotaHeadersTweenA.Complete();
			quotaHeadersTweenB.Complete();
		});
		int baseTicketRewardForDay = _gameSettings.GetTicketReward(_gameManager.daysPassed);
		if (ConsumeSkipRequest())
		{
			quotaSlider.SetImmediate(_gameManager.currentQuota, _moneyManager.balance, baseTicketRewardForDay, quotaExcessTickets);
		}
		else
		{
			bool sliderDone = false;
			Coroutine sliderRoutine = StartCoroutine(TrackRoutine(quotaSlider.SliderRoutine(_gameManager.currentQuota, _moneyManager.balance, baseTicketRewardForDay, quotaExcessTickets, quotaBarFillDuration), () =>
			{
				sliderDone = true;
			}));
			while (!sliderDone)
			{
				if (ConsumeSkipRequest())
				{
					StopCoroutine(sliderRoutine);
					quotaSlider.SetImmediate(_gameManager.currentQuota, _moneyManager.balance, baseTicketRewardForDay, quotaExcessTickets);
					sliderDone = true;
					break;
				}
				yield return null;
			}
		}
		TweenerCore<float, float, FloatOptions> moneyGroupTween = canvasGroups[3].DOFade(1f, appearanceDuration);
		yield return WaitForSecondsOrSkip(appearanceDuration + delayBetweenAnimations, () =>
		{
			moneyGroupTween.Complete();
		});
		double end = _moneyManager.balance;
		long previousBalance = 0L;
		Tweener balanceTween = DOVirtual.Float(0f, 1f, moneyTextsDuration, (float t) =>
		{
			long num5 = (long)Math.Round((double)dayStartBalance + (end - (double)dayStartBalance) * (double)t);
			balanceText.text = MoneyFormatter.FormatWithDollar(num5) ?? "";
			if (num5 != previousBalance)
			{
				SFXManager.SFXOneShot(textChangeSfx);
			}
			previousBalance = num5;
		}).SetEase(Ease.OutCubic).OnComplete(() =>
		{
			balanceText.transform.DOPunchScale(balanceText.transform.localScale * 0.2f, 0.5f, 1);
			SFXManager.SFXOneShot(textBlopSfx);
		});
		yield return WaitForSecondsOrSkip(moneyTextsDuration + 0.5f, () =>
		{
			balanceTween.Complete();
			balanceText.text = MoneyFormatter.FormatWithDollar(_moneyManager.balance) ?? "";
		});
		string sign = ((profit >= 0) ? "+" : "");
		long previousProfit = 0L;
		Tweener profitTween = DOVirtual.Float(0f, 1f, moneyTextsDuration, (float t) =>
		{
			long num5 = (long)Math.Round((float)profit * t);
			profitText.text = sign + MoneyFormatter.FormatWithDollar(num5);
			if (num5 != previousProfit)
			{
				SFXManager.SFXOneShot(textChangeSfx);
			}
			previousProfit = num5;
		}).SetEase(Ease.OutCubic).OnComplete(() =>
		{
			profitText.transform.DOPunchScale(balanceText.transform.localScale * 0.2f, 0.5f, 1);
			SFXManager.SFXOneShot(textBlopSfx);
		});
		yield return WaitForSecondsOrSkip(moneyTextsDuration + 0.5f, () =>
		{
			profitTween.Complete();
			profitText.text = sign + MoneyFormatter.FormatWithDollar(profit);
		});
		TweenerCore<float, float, FloatOptions> graphGroupTween = canvasGroups[4].DOFade(1f, appearanceDuration);
		yield return WaitForSecondsOrSkip(appearanceDuration + delayBetweenAnimations, () =>
		{
			graphGroupTween.Complete();
		});
		ProfitLineGraph3D profitLineGraph3D = UnityEngine.Object.FindFirstObjectByType<ProfitLineGraph3D>();
		if ((bool)profitLineGraph3D)
		{
			profitLineGraph3D.ResetAndAnimate();
		}
		yield return WaitForSecondsOrSkip(profitGraphDuration, null);
		TweenerCore<float, float, FloatOptions> ticketGroupTween = canvasGroups[5].DOFade(1f, appearanceDuration);
		yield return WaitForSecondsOrSkip(appearanceDuration + delayBetweenAnimations, () =>
		{
			ticketGroupTween.Complete();
		});
		bool num2 = _gameManager.currentQuota > 0 && _moneyManager.balance >= _gameManager.currentQuota;
		if (num2)
		{
			int ticketReward = _gameSettings.GetTicketReward(_gameManager.daysPassed);
			AddTicketEntry("Quota (100%)", ticketReward);
			totalTickets += ticketReward;
		}
		if (num2 && quotaExcessTickets > 0)
		{
			float num3 = Mathf.Max(0.0001f, _gameManager.currentQuota);
			float f = (float)_moneyManager.balance / num3 * 100f - 100f;
			int num4 = Mathf.Max(0, Mathf.RoundToInt(f));
			AddTicketEntry($"Quota Excess Reward ({num4}%)", quotaExcessTickets);
			totalTickets += quotaExcessTickets;
		}
		if ((bool)MonoSingleton<DaySummaryRuntime>.Instance)
		{
			foreach (DaySummaryRuntime.ChallengeReward completedChallenge in MonoSingleton<DaySummaryRuntime>.Instance.CompletedChallenges)
			{
				if (completedChallenge.tickets > 0)
				{
					AddTicketEntry(completedChallenge.challengeName + " (Challenge)", completedChallenge.tickets);
					totalTickets += completedChallenge.tickets;
				}
			}
		}
		bool skipTicketAnimation = ConsumeSkipRequest();
		foreach (DaySummaryTicketEntry ticketEntry in _ticketEntries)
		{
			if (skipTicketAnimation)
			{
				ticketEntry.SetImmediate();
				continue;
			}
			yield return ticketEntry.Animate(ticketRewardDuration);
			if (ConsumeSkipRequest())
			{
				skipTicketAnimation = true;
				continue;
			}
			yield return WaitForSecondsOrSkip(delayBetweenTicketReward, null);
			if (ConsumeSkipRequest())
			{
				skipTicketAnimation = true;
			}
		}
		int previousTicket = 0;
		Tweener totalTicketTween = DOVirtual.Float(0f, totalTickets, finalTicketRewardDuration, (float value) =>
		{
			int num5 = (int)value;
			totalTicketsText.text = num5.ToString();
			if (num5 != previousTicket)
			{
				SFXManager.SFXOneShot(ticketTextChangeSfx);
			}
			previousTicket = num5;
		}).SetEase(Ease.OutCubic).OnComplete(() =>
		{
			totalTicketsText.transform.DOPunchScale(totalTicketsText.transform.localScale * 0.2f, 0.5f, 1);
			SFXManager.SFXOneShot(textBlopSfx);
		});
		yield return WaitForSecondsOrSkip(finalTicketRewardDuration + 0.5f, () =>
		{
			totalTicketTween.Complete();
			totalTicketsText.text = totalTickets.ToString();
		});
		yield return WaitForSecondsOrSkip(finalDelayBeforeSkip, null);
		skipUI.SetSkippableForLocal();
		SkipKeyPrompt.SetActive(value: false);
	}

	private void AddTicketEntry(string label, int tickets)
	{
		if (tickets > 0)
		{
			DaySummaryTicketEntry daySummaryTicketEntry = UnityEngine.Object.Instantiate(ticketSourceEntryPrefab, ticketSourcesRoot);
			daySummaryTicketEntry.Setup(label, tickets);
			_ticketEntries.Add(daySummaryTicketEntry);
		}
	}

	public void Reset()
	{
		StopAllCoroutines();
		DOTween.Kill(base.gameObject, complete: true);
		_pendingSegmentSkips = 0;
		statsLoopComponent.LoopSFX(play: false);
		skipUI.Reset();
		CanvasGroup[] array = canvasGroups;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].alpha = 0f;
		}
	}
}

}
