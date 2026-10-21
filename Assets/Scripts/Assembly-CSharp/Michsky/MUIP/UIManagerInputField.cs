using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	[ExecuteInEditMode]
	public class UIManagerInputField : MonoBehaviour
	{
		[Header("Settings")]
		[SerializeField]
		private UIManager UIManagerAsset;

		public bool overrideColors;

		public bool overrideFonts;

		[Header("Resources")]
		[SerializeField]
		private TextMeshProUGUI mainText;

		[SerializeField]
		private TextMeshProUGUI placeholderText;

		[SerializeField]
		private Image filledImage;

		[SerializeField]
		private Image backgroundImage;

		private void Awake()
		{
			if (UIManagerAsset == null)
			{
				UIManagerAsset = Resources.Load<UIManager>("MUIP Manager");
			}
			base.enabled = true;
			if (!UIManagerAsset.enableDynamicUpdate)
			{
				UpdateInputField();
				base.enabled = false;
			}
		}

		private void Update()
		{
			if (!(UIManagerAsset == null) && UIManagerAsset.enableDynamicUpdate)
			{
				UpdateInputField();
			}
		}

		private void UpdateInputField()
		{
			if (!overrideColors)
			{
				mainText.color = new Color(UIManagerAsset.inputFieldColor.r, UIManagerAsset.inputFieldColor.g, UIManagerAsset.inputFieldColor.b, mainText.color.a);
				placeholderText.color = new Color(UIManagerAsset.inputFieldColor.r, UIManagerAsset.inputFieldColor.g, UIManagerAsset.inputFieldColor.b, placeholderText.color.a);
				filledImage.color = new Color(UIManagerAsset.inputFieldColor.r, UIManagerAsset.inputFieldColor.g, UIManagerAsset.inputFieldColor.b, filledImage.color.a);
				backgroundImage.color = new Color(UIManagerAsset.inputFieldColor.r, UIManagerAsset.inputFieldColor.g, UIManagerAsset.inputFieldColor.b, backgroundImage.color.a);
			}
			if (!overrideFonts)
			{
				mainText.font = UIManagerAsset.inputFieldFont;
				placeholderText.font = UIManagerAsset.inputFieldFont;
			}
		}
	}
}
