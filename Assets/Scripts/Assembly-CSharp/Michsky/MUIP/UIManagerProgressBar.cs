using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	[ExecuteInEditMode]
	public class UIManagerProgressBar : MonoBehaviour
	{
		[Header("Settings")]
		[SerializeField]
		private UIManager UIManagerAsset;

		[HideInInspector]
		public bool overrideColors;

		[HideInInspector]
		public bool overrideFonts;

		[Header("Resources")]
		[SerializeField]
		private Image bar;

		[SerializeField]
		private Image background;

		[SerializeField]
		private TextMeshProUGUI label;

		private bool dynamicUpdateEnabled;

		private void Awake()
		{
			if (UIManagerAsset == null)
			{
				UIManagerAsset = Resources.Load<UIManager>("MUIP Manager");
			}
			base.enabled = true;
			if (!UIManagerAsset.enableDynamicUpdate)
			{
				UpdateProgressBar();
				base.enabled = false;
			}
		}

		private void Update()
		{
			if (!(UIManagerAsset == null) && UIManagerAsset.enableDynamicUpdate)
			{
				UpdateProgressBar();
			}
		}

		private void UpdateProgressBar()
		{
			if (!overrideColors)
			{
				bar.color = UIManagerAsset.progressBarColor;
				background.color = UIManagerAsset.progressBarBackgroundColor;
				label.color = UIManagerAsset.progressBarLabelColor;
			}
			if (!overrideFonts)
			{
				label.font = UIManagerAsset.progressBarLabelFont;
				label.fontSize = UIManagerAsset.progressBarLabelFontSize;
			}
		}
	}
}
