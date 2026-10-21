using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	[ExecuteInEditMode]
	public class UIManagerSlider : MonoBehaviour
	{
		[Header("Settings")]
		[SerializeField]
		private UIManager UIManagerAsset;

		public bool hasLabel;

		public bool hasPopupLabel;

		[HideInInspector]
		public bool overrideColors;

		[HideInInspector]
		public bool overrideFonts;

		[Header("Resources")]
		[SerializeField]
		private Image background;

		[SerializeField]
		private Image bar;

		[SerializeField]
		private Image handle;

		[HideInInspector]
		public TextMeshProUGUI label;

		[HideInInspector]
		public TextMeshProUGUI popupLabel;

		private void Awake()
		{
			if (UIManagerAsset == null)
			{
				UIManagerAsset = Resources.Load<UIManager>("MUIP Manager");
			}
			base.enabled = true;
			if (!UIManagerAsset.enableDynamicUpdate)
			{
				UpdateSlider();
				base.enabled = false;
			}
		}

		private void Update()
		{
			if (!(UIManagerAsset == null) && UIManagerAsset.enableDynamicUpdate)
			{
				UpdateSlider();
			}
		}

		private void UpdateSlider()
		{
			if (UIManagerAsset.sliderThemeType == UIManager.SliderThemeType.Basic)
			{
				if (!overrideColors)
				{
					background.color = UIManagerAsset.sliderBackgroundColor;
					bar.color = UIManagerAsset.sliderColor;
					handle.color = UIManagerAsset.sliderColor;
				}
				if (hasLabel)
				{
					if (!overrideColors)
					{
						label.color = new Color(UIManagerAsset.sliderColor.r, UIManagerAsset.sliderColor.g, UIManagerAsset.sliderColor.b, label.color.a);
					}
					if (!overrideFonts)
					{
						label.font = UIManagerAsset.sliderLabelFont;
					}
				}
				if (hasPopupLabel)
				{
					if (!overrideColors)
					{
						popupLabel.color = new Color(UIManagerAsset.sliderPopupLabelColor.r, UIManagerAsset.sliderPopupLabelColor.g, UIManagerAsset.sliderPopupLabelColor.b, popupLabel.color.a);
					}
					if (!overrideFonts)
					{
						popupLabel.font = UIManagerAsset.sliderLabelFont;
					}
				}
			}
			else
			{
				if (UIManagerAsset.sliderThemeType != UIManager.SliderThemeType.Custom)
				{
					return;
				}
				if (!overrideColors)
				{
					background.color = UIManagerAsset.sliderBackgroundColor;
					bar.color = UIManagerAsset.sliderColor;
					handle.color = UIManagerAsset.sliderHandleColor;
				}
				if (hasLabel)
				{
					if (!overrideColors)
					{
						label.color = new Color(UIManagerAsset.sliderLabelColor.r, UIManagerAsset.sliderLabelColor.g, UIManagerAsset.sliderLabelColor.b, label.color.a);
					}
					if (!overrideFonts)
					{
						label.font = UIManagerAsset.sliderLabelFont;
						label.font = UIManagerAsset.sliderLabelFont;
					}
				}
				if (hasPopupLabel)
				{
					if (!overrideColors)
					{
						popupLabel.color = new Color(UIManagerAsset.sliderPopupLabelColor.r, UIManagerAsset.sliderPopupLabelColor.g, UIManagerAsset.sliderPopupLabelColor.b, popupLabel.color.a);
					}
					if (!overrideFonts)
					{
						popupLabel.font = UIManagerAsset.sliderLabelFont;
					}
				}
			}
		}
	}
}
