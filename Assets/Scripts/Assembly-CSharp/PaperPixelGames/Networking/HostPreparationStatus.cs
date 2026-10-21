namespace PaperPixelGames.Networking
{
	public enum HostPreparationStatus
	{
		Waiting = 0,
		Warning = 1,
		Ready = 2,
		TimedOut = 3,
		HostUnavailable = 4,
		SessionChanged = 5,
		VersionMismatch = 6,
		AdmissionClosed = 7
	}
}
