namespace SushiAndFish.Casino.Original
{
using global::System;
using global::System.Collections.Generic;
using SushiAndFish.Casino.Original.Extensions;
using global::Febucci.UI;
using global::MoreMountains.Feedbacks;
using global::UnityEngine;

public class OnboardingDisplayer : MonoBehaviour
{
	[SerializeField]
	private MMF_Player displayCloseFeedbacks;

	[SerializeField]
	private TextAnimator textAnimator;

	[SerializeField]
	private List<string> lines;

	private int _currentLineIndex;

	private void OnEnable()
	{
		InputEvents.OnInteractEvent = (Action<bool>)Delegate.Combine(InputEvents.OnInteractEvent, new Action<bool>(DisplayOnboardingFeedbacks));
	}

	private void OnDisable()
	{
		InputEvents.OnInteractEvent = (Action<bool>)Delegate.Remove(InputEvents.OnInteractEvent, new Action<bool>(DisplayOnboardingFeedbacks));
	}

	public void Init()
	{
		if (NetworkSingleton<GameManager>.Instance.daysPassed > 0)
		{
			base.gameObject.SetActive(value: false);
		}
		PlayNextLine();
	}

	private void SetText(int index)
	{
		textAnimator.GetComponent<TextAnimatorPlayer>().ShowText(lines[index]);
	}

	private void DisplayOnboardingFeedbacks(bool isPressed)
	{
		if (isPressed)
		{
			if (!textAnimator.allLettersShown)
			{
				textAnimator.GetComponent<TextAnimatorPlayer>().SkipTypewriter();
			}
			else
			{
				PlayNextLine();
			}
		}
	}

	private void PlayNextLine()
	{
		if (_currentLineIndex < lines.Count)
		{
			SetText(_currentLineIndex);
			_currentLineIndex++;
		}
		else
		{
			displayCloseFeedbacks.PlayFeedbacks();
		}
	}
}

}
