using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	[ExecuteInEditMode]
	public class UIManagerNotification : MonoBehaviour
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
		private Image background;

		[SerializeField]
		private Image icon;

		[SerializeField]
		private TextMeshProUGUI title;

		[SerializeField]
		private TextMeshProUGUI description;

		private void Awake()
		{
			if (UIManagerAsset == null)
			{
				UIManagerAsset = Resources.Load<UIManager>("MUIP Manager");
			}
			base.enabled = true;
			if (!UIManagerAsset.enableDynamicUpdate)
			{
				UpdateNotification();
				base.enabled = false;
			}
		}

		private void Update()
		{
			if (!(UIManagerAsset == null) && UIManagerAsset.enableDynamicUpdate)
			{
				UpdateNotification();
			}
		}

		private void UpdateNotification()
		{
			if (!overrideColors)
			{
				background.color = UIManagerAsset.notificationBackgroundColor;
				icon.color = UIManagerAsset.notificationIconColor;
				title.color = UIManagerAsset.notificationTitleColor;
				description.color = UIManagerAsset.notificationDescriptionColor;
			}
			if (!overrideFonts)
			{
				title.font = UIManagerAsset.notificationTitleFont;
				title.fontSize = UIManagerAsset.notificationTitleFontSize;
				description.font = UIManagerAsset.notificationDescriptionFont;
				description.fontSize = UIManagerAsset.notificationDescriptionFontSize;
			}
		}
	}
}
