namespace PaperPixelGames.Networking
{
	public readonly struct LobbyRoomResult
	{
		public bool Success { get; }

		public string Message { get; }

		public LobbyRoomResult(bool success, string message = "")
		{
			Success = success;
			Message = message ?? "";
		}

		public static LobbyRoomResult Ok(string message = "")
		{
			return new LobbyRoomResult(success: true, message);
		}

		public static LobbyRoomResult Fail(string message)
		{
			return new LobbyRoomResult(success: false, message);
		}
	}
}
