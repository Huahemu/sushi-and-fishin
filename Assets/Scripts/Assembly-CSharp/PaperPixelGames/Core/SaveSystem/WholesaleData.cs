using System;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public class WholesaleData
	{
		public string Id;

		public byte Quantity;

		public TransformData TransformData;
	}
}
