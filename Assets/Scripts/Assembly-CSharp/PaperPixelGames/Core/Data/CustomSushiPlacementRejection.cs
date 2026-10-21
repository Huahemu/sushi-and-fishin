namespace PaperPixelGames.Core.Data
{
	public enum CustomSushiPlacementRejection : byte
	{
		None = 0,
		UnsupportedSushiType = 1,
		InvalidIngredient = 2,
		WrongBaseIngredient = 3,
		BaseIngredientCannotRepeat = 4,
		FillingLimitReached = 5,
		CompositionLocked = 6
	}
}
