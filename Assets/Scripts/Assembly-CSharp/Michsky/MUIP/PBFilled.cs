using TMPro;
using UnityEngine;

namespace Michsky.MUIP
{
	public class PBFilled : MonoBehaviour
	{
		[Header("Resources")]
		public TextMeshProUGUI minLabel;

		public TextMeshProUGUI maxLabel;

		[Header("Settings")]
		[Range(0f, 100f)]
		public int transitionAfter = 50;

		public Color minColor = new Color(0f, 0f, 0f, 255f);

		public Color maxColor = new Color(255f, 255f, 255f, 255f);

		private ProgressBar progressBar;

		private Animator barAnimatior;

		private void Start()
		{
			progressBar = base.gameObject.GetComponent<ProgressBar>();
			barAnimatior = base.gameObject.GetComponent<Animator>();
			minLabel.color = minColor;
			maxLabel.color = maxColor;
		}

		private void Update()
		{
			if (progressBar.currentPercent >= (float)transitionAfter)
			{
				barAnimatior.Play("Radial PB Filled");
			}
			if (progressBar.currentPercent <= (float)transitionAfter)
			{
				barAnimatior.Play("Radial PB Empty");
			}
			maxLabel.text = minLabel.text;
		}
	}
}
