using System;
using System.Collections.Generic;
using UnityEngine;

namespace Michsky.MUIP
{
	[CreateAssetMenu(fileName = "New Icon Library", menuName = "Modern UI Pack/New Icon Library")]
	public class IconLibrary : ScriptableObject
	{
		[Serializable]
		public class IconItem
		{
			public string iconTitle = "Icon";

			public Texture2D iconPreview;

			public Sprite iconSprite32;

			public Sprite iconSprite64;

			public Sprite iconSprite128;

			public Sprite iconSprite256;
		}

		public bool alwaysUpdate;

		public bool optimizeUpdates = true;

		public Texture2D searchIcon;

		public List<IconItem> icons = new List<IconItem>();
	}
}
