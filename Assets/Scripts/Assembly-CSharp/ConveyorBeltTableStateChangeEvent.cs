using PaperPixelGames.Framework.Enums;

public struct ConveyorBeltTableStateChangeEvent : IEvent
{
	public ConveyorBeltTableState NewState;

	public int TableId;
}
