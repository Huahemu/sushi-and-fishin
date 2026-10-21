using PaperPixelGames.Framework.Enums;

public struct RestaurantTableStateChangeEvent : IEvent
{
	public RestaurantTableState NewState;

	public int TableId;
}
