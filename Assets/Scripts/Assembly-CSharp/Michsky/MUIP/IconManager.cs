using UnityEngine;
using UnityEngine.UI;

namespace Michsky.MUIP
{
	[ExecuteInEditMode]
	[DisallowMultipleComponent]
	[AddComponentMenu("Modern UI Pack/Image/Icon Manager")]
	[RequireComponent(typeof(Image))]
	public class IconManager : MonoBehaviour
	{
		public IconLibrary iconLibrary;

		public string selectedIconID;

		public int selectedIconIndex;

		[Range(0f, 3f)]
		public int spriteSize;

		private Image imageObject;

		[HideInInspector]
		public string currentSize;

		[HideInInspector]
		public bool size32;

		[HideInInspector]
		public bool size64;

		[HideInInspector]
		public bool size128;

		[HideInInspector]
		public bool size256;

		private void Awake()
		{
			try
			{
				if (iconLibrary == null)
				{
					iconLibrary = Resources.Load<IconLibrary>("Icon Library");
				}
				if (imageObject == null)
				{
					imageObject = base.gameObject.GetComponent<Image>();
				}
				base.enabled = true;
				UpdateElement();
			}
			catch
			{
				Debug.LogWarning("<b>Icon Library</b> is missing, but it should be assigned.", this);
			}
		}

		private void Update()
		{
			if (iconLibrary.alwaysUpdate)
			{
				UpdateElement();
			}
			if (Application.isPlaying && iconLibrary.optimizeUpdates)
			{
				base.enabled = false;
			}
		}

		public void UpdateElement()
		{
			if (iconLibrary == null)
			{
				base.enabled = false;
				return;
			}
			for (int i = 0; i < iconLibrary.icons.Count; i++)
			{
				if (selectedIconID == iconLibrary.icons[i].iconTitle && base.gameObject.activeInHierarchy)
				{
					if (spriteSize == 0)
					{
						imageObject.sprite = iconLibrary.icons[i].iconSprite32;
					}
					else if (spriteSize == 1)
					{
						imageObject.sprite = iconLibrary.icons[i].iconSprite64;
					}
					else if (spriteSize == 2)
					{
						imageObject.sprite = iconLibrary.icons[i].iconSprite128;
					}
					else if (spriteSize == 3)
					{
						imageObject.sprite = iconLibrary.icons[i].iconSprite256;
					}
					break;
				}
			}
			if (!iconLibrary.alwaysUpdate)
			{
				base.enabled = false;
			}
		}

		public void UpdateSpriteSize(int spriteIndex, int newSize)
		{
			switch (newSize)
			{
			case 0:
				imageObject.sprite = iconLibrary.icons[spriteIndex].iconSprite32;
				break;
			case 1:
				imageObject.sprite = iconLibrary.icons[spriteIndex].iconSprite64;
				break;
			case 2:
				imageObject.sprite = iconLibrary.icons[spriteIndex].iconSprite128;
				break;
			case 3:
				imageObject.sprite = iconLibrary.icons[spriteIndex].iconSprite256;
				break;
			}
		}

		public void ChangeIcon(string newSprite, int preferredSize)
		{
			int num = -1;
			for (int i = 0; i < iconLibrary.icons.Count; i++)
			{
				if (newSprite == iconLibrary.icons[i].iconTitle)
				{
					num = i;
					break;
				}
			}
			if (num != -1)
			{
				UpdateSpriteSize(num, preferredSize);
			}
			else
			{
				Debug.Log("<b>[Icon Manager]</b> Cannot find an icon named '" + newSprite + "'");
			}
		}
	}
}
