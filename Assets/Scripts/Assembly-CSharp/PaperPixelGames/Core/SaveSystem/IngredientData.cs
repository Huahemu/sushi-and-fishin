using System;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public class IngredientData
	{
		public string IngredientId = "";

		public byte Quantity;

		public TransformData TransformData = new TransformData();
	}
}
