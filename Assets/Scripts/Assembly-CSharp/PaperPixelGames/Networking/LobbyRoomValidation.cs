using System;
using System.Linq;

namespace PaperPixelGames.Networking
{
	public static class LobbyRoomValidation
	{
		public const int MaxNameLength = 32;

		public const int MaxPasswordLength = 32;

		public static string CleanName(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return "";
			}
			string text = new string(value.Where((char c) => !char.IsControl(c) && c != '<' && c != '>').ToArray()).Trim();
			if (text.Length <= 32)
			{
				return text;
			}
			return text[..(char.IsHighSurrogate(text[31]) ? 31 : 32)];
		}

		public static LobbyRoomResult Validate(LobbyRoomSettings settings, int memberCount, int maxSupported)
		{
			if (settings == null)
			{
				return LobbyRoomResult.Fail("Lobby settings are missing.");
			}
			if (string.IsNullOrWhiteSpace(CleanName(settings.Name)))
			{
				return LobbyRoomResult.Fail("Enter a lobby name.");
			}
			if (settings.Name.Trim().Length > 32)
			{
				return LobbyRoomResult.Fail($"Lobby names can contain up to {32} characters.");
			}
			if (!Enum.IsDefined(typeof(LobbyVisibility), settings.Visibility))
			{
				return LobbyRoomResult.Fail("Choose a valid lobby visibility.");
			}
			if (settings.MaxMembers < 1 || settings.MaxMembers > maxSupported)
			{
				return LobbyRoomResult.Fail($"Choose between 1 and {maxSupported} players.");
			}
			if (settings.MaxMembers < memberCount)
			{
				return LobbyRoomResult.Fail("The player limit cannot be smaller than the current party.");
			}
			if (!string.IsNullOrEmpty(settings.Password) && (settings.Password.Length < 4 || settings.Password.Length > 32))
			{
				return LobbyRoomResult.Fail($"Passwords must contain 4–{32} characters.");
			}
			if (settings.Password != null && settings.Password.Any(char.IsControl))
			{
				return LobbyRoomResult.Fail("Passwords cannot contain control characters.");
			}
			return LobbyRoomResult.Ok();
		}
	}
}
