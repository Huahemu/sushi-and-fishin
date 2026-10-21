using System;
using System.Collections.Generic;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public class FridgeFishData
	{
		public string Id;

		public List<float> Weights = new List<float>();
	}
}
