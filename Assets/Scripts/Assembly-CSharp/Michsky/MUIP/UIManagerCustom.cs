using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	[ExecuteInEditMode]
	[DisallowMultipleComponent]
	[AddComponentMenu("Modern UI Pack/UI Manager/UI Manager (Custom Object)")]
	public class UIManagerCustom : MonoBehaviour
	{
		public enum ObjectType
		{
			Text = 0,
			Image = 1
		}

		public enum ColorType
		{
			Primary = 0,
			Secondary = 1
		}

		public enum FontType
		{
			Primary = 0,
			Secondary = 1
		}

		[Header("Resources")]
		public UIManager UIManagerAsset;

		[Header("Settings")]
		public ObjectType objectType;

		[Header("Color")]
		public ColorType colorType;

		public bool keepAlphaValue;

		public bool useCustomColor;

		[Header("Font")]
		public FontType fontType;

		public bool useCustomFont;

		private Image imageObject;

		private TextMeshProUGUI textObject;

		private void Awake()
		{
			base.enabled = true;
			if (UIManagerAsset == null)
			{
				UIManagerAsset = Resources.Load<UIManager>("MUIP Manager");
			}
			if (!UIManagerAsset.enableDynamicUpdate)
			{
				UpdateElement();
				base.enabled = false;
			}
		}

		private void Update()
		{
			if (!(UIManagerAsset == null) && UIManagerAsset.enableDynamicUpdate)
			{
				UpdateElement();
			}
		}

		public void UpdateElement()
		{
			if (objectType == ObjectType.Image && imageObject == null)
			{
				imageObject = base.gameObject.GetComponent<Image>();
			}
			else if (objectType == ObjectType.Text && textObject == null)
			{
				textObject = base.gameObject.GetComponent<TextMeshProUGUI>();
			}
			if (objectType == ObjectType.Image && imageObject != null)
			{
				if (!keepAlphaValue)
				{
					if (colorType == ColorType.Primary)
					{
						imageObject.color = UIManagerAsset.customObjPrimaryColor;
					}
					else if (colorType == ColorType.Secondary)
					{
						imageObject.color = UIManagerAsset.customObjSecondaryColor;
					}
				}
				else if (colorType == ColorType.Primary)
				{
					imageObject.color = new Color(UIManagerAsset.customObjPrimaryColor.r, UIManagerAsset.customObjPrimaryColor.g, UIManagerAsset.customObjPrimaryColor.b, imageObject.color.a);
				}
				else if (colorType == ColorType.Secondary)
				{
					imageObject.color = new Color(UIManagerAsset.customObjSecondaryColor.r, UIManagerAsset.customObjSecondaryColor.g, UIManagerAsset.customObjSecondaryColor.b, imageObject.color.a);
				}
			}
			else
			{
				if (objectType != ObjectType.Text || !(textObject != null))
				{
					return;
				}
				if (!useCustomColor)
				{
					if (!keepAlphaValue)
					{
						if (colorType == ColorType.Primary)
						{
							textObject.color = UIManagerAsset.customObjPrimaryColor;
						}
						else if (colorType == ColorType.Secondary)
						{
							textObject.color = UIManagerAsset.customObjSecondaryColor;
						}
					}
					else if (colorType == ColorType.Primary)
					{
						textObject.color = new Color(UIManagerAsset.customObjPrimaryColor.r, UIManagerAsset.customObjPrimaryColor.g, UIManagerAsset.customObjPrimaryColor.b, textObject.color.a);
					}
					else if (colorType == ColorType.Secondary)
					{
						textObject.color = new Color(UIManagerAsset.customObjSecondaryColor.r, UIManagerAsset.customObjSecondaryColor.g, UIManagerAsset.customObjSecondaryColor.b, textObject.color.a);
					}
				}
				if (!useCustomFont)
				{
					if (fontType == FontType.Primary)
					{
						textObject.font = UIManagerAsset.customObjPrimaryFont;
					}
					else if (fontType == FontType.Secondary)
					{
						textObject.font = UIManagerAsset.customObjSecondaryFont;
					}
				}
			}
		}
	}
}
