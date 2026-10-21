namespace PaperPixelGames.Networking
{
	public enum JoinWaitStatus : byte
	{
		Inactive = 0,
		Waiting = 1,
		Warning = 2,
		NoProgress = 3,
		TimedOut = 4
	}
}
