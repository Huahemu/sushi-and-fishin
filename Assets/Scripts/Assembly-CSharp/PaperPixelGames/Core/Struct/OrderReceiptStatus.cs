namespace PaperPixelGames.Core.Struct
{
	public enum OrderReceiptStatus : byte
	{
		Waiting = 0,
		Preparing = 1,
		Ready = 2,
		Served = 3,
		Expired = 4
	}
}
