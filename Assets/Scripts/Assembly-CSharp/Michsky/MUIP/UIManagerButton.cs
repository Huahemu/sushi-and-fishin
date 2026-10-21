using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	[ExecuteInEditMode]
	[RequireComponent(typeof(ButtonManager))]
	public class UIManagerButton : MonoBehaviour
	{
		[Header("Settings")]
		[SerializeField]
		private bool outlineMode;

		[Header("Resources")]
		[SerializeField]
		private UIManager UIManagerAsset;

		public ButtonManager buttonManager;

		[HideInInspector]
		public bool overrideColors;

		[HideInInspector]
		public bool overrideFonts;

		[HideInInspector]
		public Image disabledBackground;

		[HideInInspector]
		public Image normalBackground;

		[HideInInspector]
		public Image highlightedBackground;

		[HideInInspector]
		public Image disabledIcon;

		[HideInInspector]
		public Image normalIcon;

		[HideInInspector]
		public Image highlightedIcon;

		[HideInInspector]
		public TextMeshProUGUI disabledText;

		[HideInInspector]
		public TextMeshProUGUI normalText;

		[HideInInspector]
		public TextMeshProUGUI highlightedText;

		private void Awake()
		{
			if (UIManagerAsset == null)
			{
				UIManagerAsset = Resources.Load<UIManager>("MUIP Manager");
			}
			if (buttonManager == null)
			{
				buttonManager = GetComponent<ButtonManager>();
			}
			base.enabled = true;
			if (!UIManagerAsset.enableDynamicUpdate)
			{
				UpdateButton();
				base.enabled = false;
			}
		}

		private void Update()
		{
			if (!(UIManagerAsset == null) && !(buttonManager == null) && UIManagerAsset.enableDynamicUpdate)
			{
				UpdateButton();
			}
		}

		private void UpdateButton()
		{
			if (!overrideColors)
			{
				if (disabledBackground != null)
				{
					Image image = disabledBackground;
					Color color = (highlightedBackground.color = new Color(UIManagerAsset.buttonAccentColor.r, UIManagerAsset.buttonAccentColor.g, UIManagerAsset.buttonAccentColor.b, UIManagerAsset.buttonDisabledAlpha));
					image.color = color;
				}
				if (normalBackground != null)
				{
					normalBackground.color = UIManagerAsset.buttonAccentColor;
				}
				if (highlightedBackground != null)
				{
					highlightedBackground.color = UIManagerAsset.buttonAccentColor;
				}
			}
			if (buttonManager.enableIcon && !overrideColors)
			{
				if (!outlineMode)
				{
					if (disabledIcon != null)
					{
						disabledIcon.color = UIManagerAsset.buttonNormalColor;
					}
					if (normalIcon != null)
					{
						normalIcon.color = UIManagerAsset.buttonNormalColor;
					}
					if (highlightedIcon != null)
					{
						highlightedIcon.color = UIManagerAsset.buttonNormalColor;
					}
				}
				else
				{
					if (disabledIcon != null)
					{
						disabledIcon.color = new Color(UIManagerAsset.buttonAccentColor.r, UIManagerAsset.buttonAccentColor.g, UIManagerAsset.buttonAccentColor.b, UIManagerAsset.buttonDisabledAlpha);
					}
					if (normalIcon != null)
					{
						normalIcon.color = UIManagerAsset.buttonAccentColor;
					}
					if (highlightedIcon != null)
					{
						highlightedIcon.color = UIManagerAsset.buttonNormalColor;
					}
				}
			}
			if (!buttonManager.enableText)
			{
				return;
			}
			if (!overrideColors)
			{
				if (!outlineMode)
				{
					if (disabledText != null)
					{
						disabledText.color = UIManagerAsset.buttonNormalColor;
					}
					if (normalText != null)
					{
						normalText.color = UIManagerAsset.buttonNormalColor;
					}
					if (highlightedText != null)
					{
						highlightedText.color = UIManagerAsset.buttonNormalColor;
					}
				}
				else
				{
					if (disabledText != null)
					{
						disabledText.color = new Color(UIManagerAsset.buttonAccentColor.r, UIManagerAsset.buttonAccentColor.g, UIManagerAsset.buttonAccentColor.b, UIManagerAsset.buttonDisabledAlpha);
					}
					if (normalText != null)
					{
						normalText.color = UIManagerAsset.buttonAccentColor;
					}
					if (highlightedText != null)
					{
						highlightedText.color = UIManagerAsset.buttonNormalColor;
					}
				}
			}
			if (!overrideFonts)
			{
				if (disabledText != null)
				{
					disabledText.font = UIManagerAsset.buttonFont;
				}
				if (normalText != null)
				{
					normalText.font = UIManagerAsset.buttonFont;
				}
				if (highlightedText != null)
				{
					highlightedText.font = UIManagerAsset.buttonFont;
				}
			}
		}
	}
}
