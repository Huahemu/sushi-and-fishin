public struct RestaurantTablePaymentChangedEvent : IEvent
{
	public int TableId;

	public int TotalPaymentFinal;
}
