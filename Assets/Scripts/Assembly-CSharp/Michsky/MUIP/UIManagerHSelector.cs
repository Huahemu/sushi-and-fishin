using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	[ExecuteInEditMode]
	public class UIManagerHSelector : MonoBehaviour
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
		private List<GameObject> images = new List<GameObject>();

		[SerializeField]
		private List<GameObject> imagesHighlighted = new List<GameObject>();

		[SerializeField]
		private List<GameObject> texts = new List<GameObject>();

		private Color latestColor;

		private void Awake()
		{
			if (UIManagerAsset == null)
			{
				UIManagerAsset = Resources.Load<UIManager>("MUIP Manager");
			}
			base.enabled = true;
			if (!UIManagerAsset.enableDynamicUpdate)
			{
				UpdateSelector();
				base.enabled = false;
			}
		}

		private void Update()
		{
			if (!(UIManagerAsset == null) && UIManagerAsset.enableDynamicUpdate)
			{
				UpdateSelector();
			}
		}

		private void UpdateSelector()
		{
			if (!overrideColors && latestColor != UIManagerAsset.selectorColor)
			{
				for (int i = 0; i < images.Count; i++)
				{
					Image component = images[i].GetComponent<Image>();
					component.color = new Color(UIManagerAsset.selectorColor.r, UIManagerAsset.selectorColor.g, UIManagerAsset.selectorColor.b, component.color.a);
				}
				for (int j = 0; j < imagesHighlighted.Count; j++)
				{
					Image component2 = imagesHighlighted[j].GetComponent<Image>();
					component2.color = new Color(UIManagerAsset.selectorHighlightedColor.r, UIManagerAsset.selectorHighlightedColor.g, UIManagerAsset.selectorHighlightedColor.b, component2.color.a);
				}
				latestColor = UIManagerAsset.selectorColor;
			}
			for (int k = 0; k < texts.Count; k++)
			{
				TextMeshProUGUI component3 = texts[k].GetComponent<TextMeshProUGUI>();
				if (!overrideColors)
				{
					component3.color = new Color(UIManagerAsset.selectorColor.r, UIManagerAsset.selectorColor.g, UIManagerAsset.selectorColor.b, component3.color.a);
				}
				if (!overrideFonts)
				{
					component3.font = UIManagerAsset.selectorFont;
				}
			}
		}
	}
}
