using System;
using System.Collections.Generic;

namespace PaperPixelGames.Core.FishFridge
{
	[Serializable]
	public class FridgeStock
	{
		public string Id;

		public List<float> Weights = new List<float>();
	}
}
