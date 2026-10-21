namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Text;
using global::TMPro;
using global::UnityEngine;
using global::UnityEngine.UI;

public class ChallengeEntryUI : MonoBehaviour
{
	[Header("Description Wrapping")]
	[SerializeField]
	private bool useDescriptionWrap = true;

	[SerializeField]
	private int DescriptionWrapChars = 40;

	[Header("Text")]
	[SerializeField]
	private TMP_Text challengeNameText;

	[SerializeField]
	private TMP_Text descriptionText;

	[SerializeField]
	private TMP_Text progressText;

	[SerializeField]
	private TMP_Text progressValueText;

	[SerializeField]
	private TMP_Text rewardText;

	[Header("Progress")]
	[SerializeField]
	private Slider progressSlider;

	[SerializeField]
	private Image progressFillImage;

	public void SetData(ChallengeProgress progress)
	{
		if (progress == null || progress.challenge == null)
		{
			return;
		}
		if (challengeNameText != null)
		{
			challengeNameText.text = progress.challenge.challengeName;
		}
		if (descriptionText != null)
		{
			string processedDescription = progress.challenge.GetProcessedDescription();
			descriptionText.text = (useDescriptionWrap ? WrapDescription(processedDescription, DescriptionWrapChars) : processedDescription);
		}
		if (progressText != null)
		{
			progressText.text = progress.progressText;
		}
		bool flag = progress.challenge.ShouldShowProgress();
		float num = Mathf.Clamp01(progress.progress);
		if (progressValueText != null)
		{
			if (flag && num > 0f)
			{
				progressValueText.text = $"{Mathf.RoundToInt(num * 100f)}%";
			}
			else
			{
				progressValueText.text = string.Empty;
			}
		}
		if (progressSlider != null)
		{
			progressSlider.value = num;
			progressSlider.gameObject.SetActive(flag);
		}
		if (progressFillImage != null)
		{
			progressFillImage.fillAmount = num;
			progressFillImage.gameObject.SetActive(flag);
		}
		if (rewardText != null)
		{
			rewardText.text = progress.challenge.GetTicketReward().ToString();
		}
	}

	private static string WrapDescription(string text, int maxChars)
	{
		if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
		{
			return text;
		}
		StringBuilder stringBuilder = new StringBuilder();
		int i = 0;
		while (i < text.Length)
		{
			int num = Math.Min(maxChars, text.Length - i);
			int num2 = i + num;
			if (num2 < text.Length)
			{
				int num3 = text.LastIndexOf(' ', num2 - 1, num);
				if (num3 >= i)
				{
					num2 = num3 + 1;
				}
			}
			if (stringBuilder.Length > 0)
			{
				stringBuilder.Append('\n');
			}
			stringBuilder.Append(text, i, num2 - i);
			for (i = num2; i < text.Length && text[i] == ' '; i++)
			{
			}
		}
		return stringBuilder.ToString();
	}
}

}
