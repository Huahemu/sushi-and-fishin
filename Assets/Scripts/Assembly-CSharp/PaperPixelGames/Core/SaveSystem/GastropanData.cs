using System;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public class GastropanData
	{
		public TransformData TransformData = new TransformData();

		public byte Amount;

		public string ItemId = "";

		public bool IsOnDesk;
	}
}
