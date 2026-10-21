using System;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public class DeadFishData
	{
		public TransformData TransformData = new TransformData();

		public string FishId;

		public bool IsFishBookCatchRegistered;

		public float FishWeight;
	}
}
