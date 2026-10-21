using System;
using UnityEngine;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public class RestaurantSignCustomizationData
	{
		public bool Repaired;

		public string RestaurantName = "";

		public Color32Data RestaurantNameColor = new Color32Data(Color.black);
	}
}
