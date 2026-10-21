using TMPro;
using UnityEngine;

namespace Michsky.MUIP
{
	[CreateAssetMenu(fileName = "New UI Manager", menuName = "Modern UI Pack/New UI Manager")]
	public class UIManager : ScriptableObject
	{
		public enum ModalWindowThemeType
		{
			Basic = 0,
			Custom = 1
		}

		public enum NotificationThemeType
		{
			Basic = 0,
			Custom = 1
		}

		public enum SliderThemeType
		{
			Basic = 0,
			Custom = 1
		}

		public enum ToggleThemeType
		{
			Basic = 0,
			Custom = 1
		}

		[HideInInspector]
		public bool enableDynamicUpdate = true;

		[HideInInspector]
		public bool enableExtendedColorPicker = true;

		[HideInInspector]
		public bool editorHints = true;

		public Color animatedIconColor = new Color(255f, 255f, 255f, 255f);

		public Color contextBackgroundColor = new Color(255f, 255f, 255f, 255f);

		public TMP_FontAsset buttonFont;

		public Color buttonNormalColor = new Color(255f, 255f, 255f, 255f);

		public Color buttonAccentColor = new Color(255f, 255f, 255f, 255f);

		[Range(0f, 1f)]
		public float buttonDisabledAlpha = 0.4f;

		public TMP_FontAsset dropdownFont;

		public TMP_FontAsset dropdownItemFont;

		public Color dropdownBackgroundColor = new Color(255f, 255f, 255f, 255f);

		public Color dropdownContentBackgroundColor = new Color(255f, 255f, 255f, 255f);

		public Color dropdownPrimaryColor = new Color(255f, 255f, 255f, 255f);

		public Color dropdownItemBackgroundColor = new Color(255f, 255f, 255f, 255f);

		public Color dropdownItemPrimaryColor = new Color(255f, 255f, 255f, 255f);

		public TMP_FontAsset selectorFont;

		public Color selectorColor = new Color(255f, 255f, 255f, 255f);

		public Color selectorHighlightedColor = new Color(255f, 255f, 255f, 255f);

		public TMP_FontAsset inputFieldFont;

		public Color inputFieldColor = new Color(255f, 255f, 255f, 255f);

		public TMP_FontAsset modalWindowTitleFont;

		public TMP_FontAsset modalWindowContentFont;

		public Color modalWindowTitleColor = new Color(255f, 255f, 255f, 255f);

		public Color modalWindowDescriptionColor = new Color(255f, 255f, 255f, 255f);

		public Color modalWindowIconColor = new Color(255f, 255f, 255f, 255f);

		public Color modalWindowBackgroundColor = new Color(255f, 255f, 255f, 255f);

		public Color modalWindowContentPanelColor = new Color(255f, 255f, 255f, 255f);

		public TMP_FontAsset notificationTitleFont;

		public float notificationTitleFontSize = 22.5f;

		public TMP_FontAsset notificationDescriptionFont;

		public float notificationDescriptionFontSize = 18f;

		public NotificationThemeType notificationThemeType;

		public Color notificationBackgroundColor = new Color(255f, 255f, 255f, 255f);

		public Color notificationTitleColor = new Color(255f, 255f, 255f, 255f);

		public Color notificationDescriptionColor = new Color(255f, 255f, 255f, 255f);

		public Color notificationIconColor = new Color(255f, 255f, 255f, 255f);

		public TMP_FontAsset progressBarLabelFont;

		public float progressBarLabelFontSize = 25f;

		public Color progressBarColor = new Color(255f, 255f, 255f, 255f);

		public Color progressBarBackgroundColor = new Color(255f, 255f, 255f, 255f);

		public Color progressBarLoopBackgroundColor = new Color(255f, 255f, 255f, 255f);

		public Color progressBarLabelColor = new Color(255f, 255f, 255f, 255f);

		public Color scrollbarColor = new Color(255f, 255f, 255f, 255f);

		public Color scrollbarBackgroundColor = new Color(255f, 255f, 255f, 255f);

		public TMP_FontAsset sliderLabelFont;

		public SliderThemeType sliderThemeType;

		public Color sliderColor = new Color(255f, 255f, 255f, 255f);

		public Color sliderBackgroundColor = new Color(255f, 255f, 255f, 255f);

		public Color sliderLabelColor = new Color(255f, 255f, 255f, 255f);

		public Color sliderPopupLabelColor = new Color(255f, 255f, 255f, 255f);

		public Color sliderHandleColor = new Color(255f, 255f, 255f, 255f);

		public Color switchBorderColor = new Color(255f, 255f, 255f, 255f);

		public Color switchBackgroundColor = new Color(255f, 255f, 255f, 255f);

		public Color switchHandleOnColor = new Color(255f, 255f, 255f, 255f);

		public Color switchHandleOffColor = new Color(255f, 255f, 255f, 255f);

		public TMP_FontAsset toggleFont;

		public ToggleThemeType toggleThemeType;

		public Color toggleTextColor = new Color(255f, 255f, 255f, 255f);

		public Color toggleBorderColor = new Color(255f, 255f, 255f, 255f);

		public Color toggleBackgroundColor = new Color(255f, 255f, 255f, 255f);

		public Color toggleCheckColor = new Color(255f, 255f, 255f, 255f);

		public TMP_FontAsset tooltipFont;

		public float tooltipFontSize = 22f;

		public Color tooltipTextColor = new Color(255f, 255f, 255f, 255f);

		public Color tooltipBackgroundColor = new Color(255f, 255f, 255f, 255f);

		public TMP_FontAsset customObjPrimaryFont;

		public TMP_FontAsset customObjSecondaryFont;

		public Color customObjPrimaryColor = new Color(255f, 255f, 255f, 255f);

		public Color customObjSecondaryColor = new Color(255f, 255f, 255f, 255f);
	}
}
