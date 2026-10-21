using System;
using PaperPixelGames.Core.Restaurant;

[Serializable]
public class CustomerSeatOrder
{
	public int SeatIndex;

	public CustomerOrderData Order;

	public int OrderId;
}
