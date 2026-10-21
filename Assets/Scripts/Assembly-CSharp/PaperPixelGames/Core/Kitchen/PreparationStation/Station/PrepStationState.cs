namespace PaperPixelGames.Core.Kitchen.PreparationStation.Station
{
	public enum PrepStationState : byte
	{
		Idle = 0,
		Cutting = 1,
		TransferToGastroPan = 2,
		GastroPanMovingToTarget = 3
	}
}
