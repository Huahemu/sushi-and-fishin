namespace PaperPixelGames.Core.Ai.NPCs
{
	public enum ResidentRestaurantSubState
	{
		None = 0,
		Assigned = 1,
		MovingToSeat = 2,
		ArrivedAtSeat = 3,
		Ordering = 4,
		WaitingOrder = 5,
		Eating = 6,
		RequestingPayment = 7,
		Paying = 8,
		Leaving = 9,
		Completed = 10,
		Cancelled = 11,
		LookingMenu = 12
	}
}
