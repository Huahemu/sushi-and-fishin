using System;

namespace PaperPixelGames.Core.Data
{
	[Serializable]
	public class ProcessedIngredientNeeds : BaseIngredientNeed
	{
		public ProcessedIngredientSO Data;

		public byte Amount;

		public override string IngredientId => Data?.Id;

		public override string IngredientName => Data?.Name;

		public override string LocalizedIngredientName => Data?.LocalizedName;

		public override byte RequiredAmount => Amount;
	}
}
