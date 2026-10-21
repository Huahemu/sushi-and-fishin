using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	[ExecuteInEditMode]
	public class UIManagerAnimatedIcon : MonoBehaviour
	{
		[Header("Settings")]
		public UIManager UIManagerAsset;

		[Header("Resources")]
		public List<GameObject> images = new List<GameObject>();

		public List<GameObject> imagesWithAlpha = new List<GameObject>();

		private void Awake()
		{
			if (UIManagerAsset == null)
			{
				UIManagerAsset = Resources.Load<UIManager>("MUIP Manager");
			}
			base.enabled = true;
			if (!UIManagerAsset.enableDynamicUpdate)
			{
				UpdateAnimatedIcon();
				base.enabled = false;
			}
		}

		private void Update()
		{
			if (!(UIManagerAsset == null) && UIManagerAsset.enableDynamicUpdate)
			{
				UpdateAnimatedIcon();
			}
		}

		private void UpdateAnimatedIcon()
		{
			for (int i = 0; i < images.Count; i++)
			{
				if (!(images[i] == null))
				{
					images[i].GetComponent<Image>().color = UIManagerAsset.animatedIconColor;
				}
			}
			for (int j = 0; j < imagesWithAlpha.Count; j++)
			{
				if (!(imagesWithAlpha[j] == null))
				{
					Image component = imagesWithAlpha[j].GetComponent<Image>();
					component.color = new Color(UIManagerAsset.animatedIconColor.r, UIManagerAsset.animatedIconColor.g, UIManagerAsset.animatedIconColor.b, component.color.a);
				}
			}
		}
	}
}
