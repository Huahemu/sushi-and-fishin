using System;
using System.Collections.Generic;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public class FridgeData
	{
		public List<FridgeFishData> FridgeFishes = new List<FridgeFishData>();
	}
}
