namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections;
using global::System.Collections.Generic;
using global::System.Linq;
using global::DG.Tweening;
using global::DG.Tweening.Core;
using global::DG.Tweening.Plugins.Options;
using SushiAndFish.Casino.Original.Extensions;
using global::FMODUnity;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.Events;
using global::UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
	[Header("Settings")]
	[SerializeField]
	private float canvasAlphaDuration = 1f;

	[SerializeField]
	private float initialDelay = 1f;

	[SerializeField]
	private float titleAnimationDuration = 2f;

	[SerializeField]
	private float contributionDuration = 1f;

	[SerializeField]
	private float delayBetweenContributions = 0.5f;

	[SerializeField]
	private float totalBalanceDuration = 1f;

	[SerializeField]
	private float dayIndexDuration = 1f;

	[SerializeField]
	private float ticketBalanceDuration = 1f;

	[SerializeField]
	private float finalDelayBeforeSkip = 1f;

	[SerializeField]
	private float appearanceDuration = 0.5f;

	[SerializeField]
	private float delayBetweenAnimations = 0.5f;

	[Header("UI")]
	[SerializeField]
	private CanvasGroup[] canvasGroups;

	[SerializeField]
	private SkipUI skipUI;

	[Header("Header")]
	[SerializeField]
	private TextMeshProUGUI titleText;

	[SerializeField]
	private TextMeshProUGUI dayReachedText;

	[Header("Totals")]
	[SerializeField]
	private TextMeshProUGUI totalBalanceText;

	[SerializeField]
	private TextMeshProUGUI ticketsText;

	[Header("Player Contributions")]
	[SerializeField]
	private Transform contributionsRoot;

	[SerializeField]
	private GameLostContributionEntry contributionEntryPrefab;

	[Header("SFX")]
	[SerializeField]
	private EventReference textChangeSfx;

	[SerializeField]
	private EventReference enterSummaryAmbSfx;

	[SerializeField]
	private EventReference textBlopSfx;

	private List<GameLostContributionEntry> _contributionEntries = new List<GameLostContributionEntry>();

	private GameManager _gameManager;

	private MoneyManager _moneyManager;

	private MoneyDisplayAndFeedbacks _moneyDisplayAndFeedbacks;

	private LobbySettings _lobbySettings;

	private int _pendingSegmentSkips;

	private void Awake()
	{
		SetReferences();
	}

	private void SetReferences()
	{
		if (!_moneyManager)
		{
			_moneyManager = NetworkSingleton<MoneyManager>.Instance;
		}
		if (!_gameManager)
		{
			_gameManager = NetworkSingleton<GameManager>.Instance;
		}
		if (!_moneyDisplayAndFeedbacks)
		{
			_moneyDisplayAndFeedbacks = NetworkSingleton<MoneyDisplayAndFeedbacks>.Instance;
		}
		if (!_lobbySettings)
		{
			_lobbySettings = SushiAndFish.Casino.Original.CasinoResources.Load<LobbySettings>("LobbySettings");
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
	}

	public void Show()
	{
		Reset();
		EmoteWheelController emoteWheelController = UnityEngine.Object.FindAnyObjectByType<EmoteWheelController>(FindObjectsInactive.Include);
		if ((bool)emoteWheelController)
		{
			emoteWheelController.SetEmoteWheelActive(active: false);
		}
		StartCoroutine(GameOverRoutine());
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

	private IEnumerator GameOverRoutine()
	{
		base.gameObject.SetActive(value: true);
		SFXManager.SFXOneShot(enterSummaryAmbSfx);
		SetReferences();
		if (!_moneyManager || !_gameManager || !_moneyDisplayAndFeedbacks)
		{
			yield break;
		}
		skipUI.Reset();
		skipUI.SetSkippableServer();
		_pendingSegmentSkips = 0;
		CanvasGroup[] array = canvasGroups;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].alpha = 0f;
		}
		titleText.text = "";
		int dayIndex = _gameManager.daysPassed + 1;
		long balance = NetworkSingleton<PayoutTracker>.Instance.GetLifetimeNetTotal();
		long ticketBalance = _moneyManager.ticketBalance;
		_contributionEntries.Clear();
		for (int num = contributionsRoot.childCount - 1; num >= 0; num--)
		{
			UnityEngine.Object.Destroy(contributionsRoot.GetChild(num).gameObject);
		}
		Dictionary<string, long> profitHistorySnapshot = _moneyDisplayAndFeedbacks.GetProfitHistorySnapshot();
		HashSet<string> currentPlayerNames = GetCurrentPlayerNames();
		foreach (KeyValuePair<string, long> item in profitHistorySnapshot.OrderByDescending((KeyValuePair<string, long> e) => e.Value))
		{
			string key = item.Key;
			if (currentPlayerNames.Contains(key))
			{
				long value = item.Value;
				AddContributionEntry(key, value);
			}
		}
		TweenerCore<float, float, FloatOptions> introTween = canvasGroups[0].DOFade(1f, canvasAlphaDuration);
		yield return WaitForSecondsOrSkip(canvasAlphaDuration, () =>
		{
			introTween.Complete();
		});
		yield return WaitForSecondsOrSkip(initialDelay, null);
		titleText.text = ((SceneManager.GetActiveScene().name == "LoseStateScene") ? "Game Over" : "Game Complete");
		yield return WaitForSecondsOrSkip(titleAnimationDuration, null);
		TweenerCore<float, float, FloatOptions> contributionsGroupTween = canvasGroups[1].DOFade(1f, appearanceDuration);
		yield return WaitForSecondsOrSkip(appearanceDuration + delayBetweenAnimations, () =>
		{
			contributionsGroupTween.Complete();
		});
		bool skipContributionAnimation = ConsumeSkipRequest();
		foreach (GameLostContributionEntry contributionEntry in _contributionEntries)
		{
			if (skipContributionAnimation)
			{
				contributionEntry.SetImmediate();
				continue;
			}
			yield return contributionEntry.Animate(contributionDuration);
			if (ConsumeSkipRequest())
			{
				skipContributionAnimation = true;
				continue;
			}
			yield return WaitForSecondsOrSkip(delayBetweenContributions, null);
			if (ConsumeSkipRequest())
			{
				skipContributionAnimation = true;
			}
		}
		long previousBalance = 0L;
		Tweener totalBalanceTween = DOVirtual.Float(0f, 1f, totalBalanceDuration, (float t) =>
		{
			long num2 = (long)Math.Round((float)balance * t);
			totalBalanceText.text = MoneyFormatter.FormatWithDollar(num2) ?? "";
			if (num2 != previousBalance)
			{
				SFXManager.SFXOneShot(textChangeSfx);
			}
			previousBalance = num2;
		}).SetEase(Ease.OutCubic).OnComplete(() =>
		{
			totalBalanceText.transform.DOPunchScale(totalBalanceText.transform.localScale * 0.2f, 0.5f, 1);
			SFXManager.SFXOneShot(textBlopSfx);
		});
		yield return WaitForSecondsOrSkip(totalBalanceDuration, () =>
		{
			totalBalanceTween.Complete();
			totalBalanceText.text = MoneyFormatter.FormatWithDollar(balance) ?? "";
		});
		yield return WaitForSecondsOrSkip(delayBetweenAnimations, null);
		TweenerCore<float, float, FloatOptions> dayTicketsGroupTween = canvasGroups[2].DOFade(1f, appearanceDuration);
		yield return WaitForSecondsOrSkip(appearanceDuration + delayBetweenAnimations, () =>
		{
			dayTicketsGroupTween.Complete();
		});
		int previousDayIndex = 0;
		Tweener dayTween = DOVirtual.Float(0f, 1f, dayIndexDuration, (float t) =>
		{
			int num2 = (int)Math.Round((float)dayIndex * t);
			dayReachedText.text = num2.ToString();
			if (num2 != previousDayIndex)
			{
				SFXManager.SFXOneShot(textChangeSfx);
			}
			previousDayIndex = num2;
		}).SetEase(Ease.OutCubic).OnComplete(() =>
		{
			dayReachedText.transform.DOPunchScale(dayReachedText.transform.localScale * 0.2f, 0.5f, 1);
			SFXManager.SFXOneShot(textBlopSfx);
		});
		yield return WaitForSecondsOrSkip(dayIndexDuration, () =>
		{
			dayTween.Complete();
			dayReachedText.text = dayIndex.ToString();
		});
		yield return WaitForSecondsOrSkip(delayBetweenAnimations, null);
		int previousTicketBalance = 0;
		Tweener ticketTween = DOVirtual.Float(0f, 1f, ticketBalanceDuration, (float t) =>
		{
			int num2 = (int)Math.Round((float)ticketBalance * t);
			ticketsText.text = num2.ToString();
			if (num2 != previousTicketBalance)
			{
				SFXManager.SFXOneShot(textChangeSfx);
			}
			previousTicketBalance = num2;
		}).SetEase(Ease.OutCubic).OnComplete(() =>
		{
			ticketsText.transform.DOPunchScale(ticketsText.transform.localScale * 0.2f, 0.5f, 1);
			SFXManager.SFXOneShot(textBlopSfx);
		});
		yield return WaitForSecondsOrSkip(ticketBalanceDuration, () =>
		{
			ticketTween.Complete();
			ticketsText.text = ticketBalance.ToString();
		});
		yield return WaitForSecondsOrSkip(delayBetweenAnimations, null);
		yield return WaitForSecondsOrSkip(finalDelayBeforeSkip, null);
		skipUI.SetSkippableForLocal();
	}

	private void AddContributionEntry(string playerName, long contribution)
	{
		GameLostContributionEntry gameLostContributionEntry = UnityEngine.Object.Instantiate(contributionEntryPrefab, contributionsRoot);
		gameLostContributionEntry.Setup(playerName, contribution);
		_contributionEntries.Add(gameLostContributionEntry);
	}

	private HashSet<string> GetCurrentPlayerNames()
	{
		if (_lobbySettings == null || _lobbySettings.players == null)
		{
			return new HashSet<string>();
		}
		return (from p in _lobbySettings.players
			where !string.IsNullOrWhiteSpace(p.playerName)
			select p.playerName).ToHashSet();
	}

	public void Reset()
	{
		StopAllCoroutines();
		DOTween.Kill(base.gameObject, complete: true);
		_pendingSegmentSkips = 0;
		skipUI.Reset();
		totalBalanceText.text = "$0";
		dayReachedText.text = "0";
		ticketsText.text = "0";
		CanvasGroup[] array = canvasGroups;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].alpha = 0f;
		}
	}
}

}
