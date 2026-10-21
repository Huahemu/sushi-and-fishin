namespace PaperPixelGames.Framework.Enums
{
	public enum RestaurantTableState : ushort
	{
		Empty = 0,
		TakingOrder = 1,
		DeliveringOrder = 2,
		Eating = 3,
		AwaitingPayment = 4,
		Cleaning = 5,
		WaitingOtherCustomers = 6
	}
}
