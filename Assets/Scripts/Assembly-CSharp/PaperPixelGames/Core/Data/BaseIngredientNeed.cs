using System;

namespace PaperPixelGames.Core.Data
{
	[Serializable]
	public abstract class BaseIngredientNeed
	{
		public int Order;

		public abstract string IngredientId { get; }

		public abstract string IngredientName { get; }

		public abstract string LocalizedIngredientName { get; }

		public abstract byte RequiredAmount { get; }
	}
}
