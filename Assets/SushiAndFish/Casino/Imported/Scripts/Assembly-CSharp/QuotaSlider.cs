namespace SushiAndFish.Casino.Original
{
using global::System;
using SushiAndFish.Casino.Original.Extensions;
using global::TMPro;
using global::UnityEngine;

public class QuotaSlider : MonoBehaviour
{
	[Header("Quota Bar Parts")]
	[SerializeField]
	private RectTransform underQuotaFill;

	[SerializeField]
	private RectTransform overQuotaFill;

	[SerializeField]
	private float gapPixels = 4f;

	[SerializeField]
	private float minOverflowWidthPixels = 60f;

	[Header("Value Texts")]
	[SerializeField]
	private TextMeshProUGUI underQuotaValueText;

	[SerializeField]
	private TextMeshProUGUI overQuotaValueText;

	[Header("Overflow Effect")]
	[Tooltip("Optional UI object (e.g. Raw Image with fire material) shown above the overflow area when over quota.")]
	[SerializeField]
	private RectTransform overflowEffect;

	private long _quotaStartBalance;

	private long _startingMoney;

	private void Awake()
	{
		GameSettings gameSettings = SushiAndFish.Casino.Original.CasinoResources.Load<GameSettings>("GameSettings");
		if (gameSettings != null)
		{
			_startingMoney = gameSettings.startingMoney;
		}
	}

	private void OnEnable()
	{
		Subscribe();
		UpdateFromCurrentState();
	}

	private void OnDisable()
	{
		Unsubscribe();
	}

	private void Subscribe()
	{
		if (NetworkSingleton<MoneyManager>.Instance != null)
		{
			MoneyManager instance = NetworkSingleton<MoneyManager>.Instance;
			instance.OnBalanceChanged = (Action<BalanceChangeData>)Delegate.Combine(instance.OnBalanceChanged, new Action<BalanceChangeData>(OnBalanceChanged));
		}
		if (NetworkSingleton<GameManager>.Instance != null)
		{
			NetworkSingleton<GameManager>.Instance.OnQuotaChangedEvent += OnQuotaChanged;
		}
	}

	private void Unsubscribe()
	{
		if (NetworkSingleton<MoneyManager>.Instance != null)
		{
			MoneyManager instance = NetworkSingleton<MoneyManager>.Instance;
			instance.OnBalanceChanged = (Action<BalanceChangeData>)Delegate.Remove(instance.OnBalanceChanged, new Action<BalanceChangeData>(OnBalanceChanged));
		}
		if (NetworkSingleton<GameManager>.Instance != null)
		{
			NetworkSingleton<GameManager>.Instance.OnQuotaChangedEvent -= OnQuotaChanged;
		}
	}

	private void UpdateFromCurrentState()
	{
		if (!(NetworkSingleton<MoneyManager>.Instance == null) && !(NetworkSingleton<GameManager>.Instance == null))
		{
			long balance = NetworkSingleton<MoneyManager>.Instance.balance;
			if (_quotaStartBalance == 0L)
			{
				_quotaStartBalance = balance;
			}
			UpdateBar(balance, NetworkSingleton<GameManager>.Instance.currentQuota);
		}
	}

	private void UpdateBar(long balance, long quota)
	{
		if (underQuotaFill == null || overQuotaFill == null)
		{
			return;
		}
		ResetGap();
		if (balance <= 0 || quota < 0)
		{
			SetAnchors(underQuotaFill, 0f, 0f);
			SetAnchors(overQuotaFill, 0f, 0f);
			overQuotaFill.gameObject.SetActive(value: false);
			SetOverflowEffectActive(active: false);
			SetUnderQuotaText(0L);
			SetOverQuotaText(0L);
			return;
		}
		if (quota == 0L || balance <= quota)
		{
			long num = _quotaStartBalance;
			if (_startingMoney > 0 && _startingMoney > num)
			{
				num = _startingMoney;
			}
			if (balance <= num)
			{
				SetAnchors(underQuotaFill, 0f, 0f);
				SetAnchors(overQuotaFill, 0f, 0f);
				overQuotaFill.gameObject.SetActive(value: false);
				SetOverflowEffectActive(active: false);
				SetUnderQuotaText(balance);
				SetOverQuotaText(0L);
			}
			else
			{
				float num2 = Mathf.Max(1f, quota - num);
				float maxX = Mathf.Clamp01((float)(balance - num) / num2);
				SetAnchors(underQuotaFill, 0f, maxX);
				SetAnchors(overQuotaFill, 0f, 0f);
				overQuotaFill.gameObject.SetActive(value: false);
				SetOverflowEffectActive(active: false);
				SetUnderQuotaText(balance);
				SetOverQuotaText(0L);
			}
			return;
		}
		float num3 = Mathf.Max(0.0001f, balance);
		float num4 = Mathf.Clamp01(Mathf.Clamp(quota, 0f, num3) / num3);
		float num5 = 1f - num4;
		if (minOverflowWidthPixels > 0f)
		{
			RectTransform rectTransform = base.transform as RectTransform;
			if (rectTransform != null)
			{
				float width = rectTransform.rect.width;
				if (width > 0f)
				{
					float num6 = Mathf.Clamp01(minOverflowWidthPixels / width);
					if (num5 > 0f && num5 < num6)
					{
						num4 = Mathf.Clamp01(1f - num6);
						num5 = 1f - num4;
					}
				}
			}
		}
		SetAnchors(underQuotaFill, 0f, num4);
		SetAnchors(overQuotaFill, num4, 1f);
		overQuotaFill.gameObject.SetActive(value: true);
		SetOverflowEffectActive(active: true);
		float num7 = Mathf.Max(0f, gapPixels) * 0.5f;
		underQuotaFill.offsetMax = new Vector2(0f - num7, underQuotaFill.offsetMax.y);
		overQuotaFill.offsetMin = new Vector2(num7, overQuotaFill.offsetMin.y);
		SetUnderQuotaText(quota);
		SetOverQuotaText(balance - quota);
	}

	private void OnBalanceChanged(BalanceChangeData _)
	{
		if (!(NetworkSingleton<MoneyManager>.Instance == null) && !(NetworkSingleton<GameManager>.Instance == null))
		{
			UpdateBar(NetworkSingleton<MoneyManager>.Instance.balance, NetworkSingleton<GameManager>.Instance.currentQuota);
		}
	}

	private void OnQuotaChanged(long oldQuota, long newQuota)
	{
		if (!(NetworkSingleton<MoneyManager>.Instance == null))
		{
			_quotaStartBalance = NetworkSingleton<MoneyManager>.Instance.balance;
			UpdateBar(NetworkSingleton<MoneyManager>.Instance.balance, newQuota);
		}
	}

	private void SetUnderQuotaText(long amount)
	{
		if (underQuotaValueText != null)
		{
			underQuotaValueText.text = MoneyFormatter.FormatWithDollar(amount);
		}
	}

	private void SetOverQuotaText(long amount)
	{
		if (overQuotaValueText != null)
		{
			overQuotaValueText.text = "+" + MoneyFormatter.FormatWithDollar(amount);
		}
	}

	private static void SetAnchors(RectTransform rect, float minX, float maxX)
	{
		rect.anchorMin = new Vector2(minX, rect.anchorMin.y);
		rect.anchorMax = new Vector2(maxX, rect.anchorMax.y);
	}

	private void SetOverflowEffectActive(bool active)
	{
		if (overflowEffect != null)
		{
			overflowEffect.gameObject.SetActive(active);
		}
	}

	private void ResetGap()
	{
		if (underQuotaFill != null)
		{
			underQuotaFill.offsetMax = new Vector2(0f, underQuotaFill.offsetMax.y);
		}
		if (overQuotaFill != null)
		{
			overQuotaFill.offsetMin = new Vector2(0f, overQuotaFill.offsetMin.y);
		}
	}
}

}
