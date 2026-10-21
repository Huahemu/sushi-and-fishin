namespace SushiAndFish.Casino.Original
{
using global::System.Collections;
using global::DG.Tweening;
using global::FMODUnity;
using global::TMPro;
using global::UnityEngine;

public class DaySummaryQuotaSlider : MonoBehaviour
{
	[Header("Quota Bar Parts")]
	[SerializeField]
	private RectTransform underQuotaFill;

	[SerializeField]
	private RectTransform overQuotaFill;

	[SerializeField]
	private float gapPixels = 4f;

	[SerializeField]
	private float minVisualPercent = 0.1f;

	[Header("Texts")]
	[SerializeField]
	private TextMeshProUGUI percentageText;

	[SerializeField]
	private TextMeshProUGUI overfillPercentageText;

	[Header("Ticket Reward")]
	[SerializeField]
	private TextMeshProUGUI ticketRewardText;

	[SerializeField]
	private TextMeshProUGUI overfillTicketRewardText;

	[Header("SFX")]
	[SerializeField]
	private EventReference firstSliderSfx;

	[SerializeField]
	private EventReference secondSliderSfx;

	public IEnumerator SliderRoutine(long quota, long balance, int baseTicket, int overflowTicket, float duration)
	{
		Reset();
		float totalPercent = 0f;
		float underPercent = 0f;
		float overPercent = 0f;
		if (quota > 0 && balance > 0)
		{
			totalPercent = (float)((double)balance / (double)quota) * 100f;
			underPercent = Mathf.Clamp(totalPercent, 0f, 100f);
			overPercent = ((totalPercent > 100f) ? (totalPercent - 100f) : 0f);
		}
		DOVirtual.Float(0f, Mathf.Max(underPercent / 100f, minVisualPercent), duration, SetUnderQuotaFill).SetEase(Ease.OutCubic).SetId(this);
		DOVirtual.Float(0f, underPercent, duration, SetPercentageText).SetEase(Ease.OutCubic).SetId(this);
		if (totalPercent >= 100f)
		{
			DOVirtual.Float(0f, baseTicket, duration, (float num) =>
			{
				ticketRewardText.text = num.ToString("0");
			}).SetEase(Ease.OutCubic).SetId(this);
		}
		SFXManager.SFXOneShot(firstSliderSfx);
		yield return new WaitForSeconds(duration);
		if (overPercent > 0f)
		{
			yield return new WaitForSeconds(0.5f);
			overQuotaFill.gameObject.SetActive(value: true);
			SFXManager.SFXOneShot(secondSliderSfx);
			float value = underPercent / totalPercent;
			DOVirtual.Float(1f, Mathf.Clamp(value, minVisualPercent, 1f - minVisualPercent), duration, (float num) =>
			{
				SetOverQuotaFill(num);
				SetUnderQuotaFill(num);
			}).SetEase(Ease.OutCubic).SetId(this);
			DOVirtual.Float(0f, overPercent, duration, SetOverfillPercentageText).SetEase(Ease.OutCubic).SetId(this);
			DOVirtual.Float(0f, overflowTicket, duration, (float num) =>
			{
				overfillTicketRewardText.text = num.ToString("0");
			}).SetEase(Ease.OutCubic).SetId(this);
			yield return new WaitForSeconds(duration);
		}
	}

	public void Reset()
	{
		percentageText.text = "0%";
		overfillPercentageText.text = "+0%";
		ticketRewardText.text = "0";
		overfillTicketRewardText.text = "0";
		underQuotaFill.anchorMin = new Vector2(0f, underQuotaFill.anchorMin.y);
		underQuotaFill.anchorMax = new Vector2(0f, underQuotaFill.anchorMax.y);
		underQuotaFill.offsetMin = new Vector2(0f, underQuotaFill.offsetMin.y);
		underQuotaFill.offsetMax = new Vector2(gapPixels / 2f, underQuotaFill.offsetMax.y);
		overQuotaFill.anchorMin = new Vector2(1f, overQuotaFill.anchorMin.y);
		overQuotaFill.anchorMax = new Vector2(1f, overQuotaFill.anchorMax.y);
		overQuotaFill.offsetMin = new Vector2(gapPixels / 2f, overQuotaFill.offsetMin.y);
		overQuotaFill.offsetMax = new Vector2(0f, overQuotaFill.offsetMax.y);
		overQuotaFill.gameObject.SetActive(value: false);
	}

	public void SetImmediate(long quota, long balance, int baseTicket, int overflowTicket)
	{
		DOTween.Kill(this);
		Reset();
		float num = 0f;
		float num2 = 0f;
		float num3 = 0f;
		if (quota > 0 && balance > 0)
		{
			num = (float)((double)balance / (double)quota) * 100f;
			num2 = Mathf.Clamp(num, 0f, 100f);
			num3 = ((num > 100f) ? (num - 100f) : 0f);
		}
		SetUnderQuotaFill(Mathf.Max(num2 / 100f, minVisualPercent));
		SetPercentageText(num2);
		if (num >= 100f)
		{
			ticketRewardText.text = baseTicket.ToString();
		}
		if (num3 > 0f)
		{
			overQuotaFill.gameObject.SetActive(value: true);
			float num4 = Mathf.Clamp(num2 / num, minVisualPercent, 1f - minVisualPercent);
			SetOverQuotaFill(num4);
			SetUnderQuotaFill(num4);
			SetOverfillPercentageText(num3);
			overfillTicketRewardText.text = overflowTicket.ToString();
		}
	}

	private void SetPercentageText(float percent)
	{
		int num = Mathf.RoundToInt(percent);
		percentageText.text = $"{num}%";
	}

	private void SetOverfillPercentageText(float percent)
	{
		int num = Mathf.RoundToInt(percent);
		overfillPercentageText.text = $"+{num}%";
	}

	private void SetUnderQuotaFill(float percent)
	{
		underQuotaFill.anchorMin = new Vector2(0f, underQuotaFill.anchorMin.y);
		underQuotaFill.anchorMax = new Vector2(percent, underQuotaFill.anchorMax.y);
		underQuotaFill.offsetMin = new Vector2(0f, underQuotaFill.offsetMin.y);
		underQuotaFill.offsetMax = new Vector2((0f - gapPixels) / 2f, underQuotaFill.offsetMax.y);
	}

	private void SetOverQuotaFill(float percent)
	{
		overQuotaFill.anchorMin = new Vector2(percent, underQuotaFill.anchorMin.y);
		overQuotaFill.anchorMax = new Vector2(1f, underQuotaFill.anchorMax.y);
		overQuotaFill.offsetMin = new Vector2(gapPixels / 2f, underQuotaFill.offsetMin.y);
		overQuotaFill.offsetMax = new Vector2(0f, underQuotaFill.offsetMax.y);
	}
}

}
