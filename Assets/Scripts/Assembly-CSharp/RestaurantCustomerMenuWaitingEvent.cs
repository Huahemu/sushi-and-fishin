public struct RestaurantCustomerMenuWaitingEvent : IEvent
{
	public int TableId;

	public int SeatIndex;

	public bool IsWaiting;
}
