using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	[ExecuteInEditMode]
	public class UIManagerDropdown : MonoBehaviour
	{
		[HideInInspector]
		public bool overrideColors;

		[HideInInspector]
		public bool overrideFonts;

		[Header("Resources")]
		[SerializeField]
		private UIManager UIManagerAsset;

		[SerializeField]
		private Image background;

		[SerializeField]
		private Image contentBackground;

		[SerializeField]
		private Image mainIcon;

		[SerializeField]
		private TextMeshProUGUI mainText;

		[SerializeField]
		private Image expandIcon;

		private void Awake()
		{
			if (UIManagerAsset == null)
			{
				UIManagerAsset = Resources.Load<UIManager>("MUIP Manager");
			}
			base.enabled = true;
			if (!UIManagerAsset.enableDynamicUpdate)
			{
				UpdateDropdown();
				base.enabled = false;
			}
		}

		private void Update()
		{
			if (!(UIManagerAsset == null) && UIManagerAsset.enableDynamicUpdate)
			{
				UpdateDropdown();
			}
		}

		private void UpdateDropdown()
		{
			if (!overrideFonts && mainText != null)
			{
				mainText.font = UIManagerAsset.dropdownFont;
			}
			if (!overrideColors)
			{
				if (background != null)
				{
					background.color = UIManagerAsset.dropdownBackgroundColor;
				}
				if (contentBackground != null)
				{
					contentBackground.color = UIManagerAsset.dropdownContentBackgroundColor;
				}
				if (mainIcon != null)
				{
					mainIcon.color = UIManagerAsset.dropdownPrimaryColor;
				}
				if (mainText != null)
				{
					mainText.color = UIManagerAsset.dropdownPrimaryColor;
				}
				if (expandIcon != null)
				{
					expandIcon.color = UIManagerAsset.dropdownPrimaryColor;
				}
			}
		}
	}
}
