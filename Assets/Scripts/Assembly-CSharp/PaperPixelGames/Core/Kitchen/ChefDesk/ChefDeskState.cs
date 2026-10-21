namespace PaperPixelGames.Core.Kitchen.ChefDesk
{
	public enum ChefDeskState : byte
	{
		Idle = 0,
		RecipeSelected = 1,
		WaitingForRoll = 2,
		Cutting = 3,
		TransferToEdge = 4,
		Topping = 5,
		Centering = 6
	}
}
