namespace PaperPixelGames.Networking.Chat
{
	public sealed class ChatMessageData
	{
		public ChatChannel Channel { get; }

		public ChatMessageType Type { get; }

		public ulong SenderSteamId { get; }

		public string Zone { get; }

		public string SenderName { get; }

		public string Message { get; }

		public bool IsLocal { get; }

		public RestaurantChatTone RestaurantTone { get; }

		public ChatMessageData(ChatChannel channel, ChatMessageType type, ulong senderSteamId, string zone, string senderName, string message, bool isLocal, RestaurantChatTone restaurantTone = RestaurantChatTone.Info)
		{
			Channel = channel;
			Type = type;
			SenderSteamId = senderSteamId;
			Zone = zone;
			SenderName = senderName;
			Message = message;
			IsLocal = isLocal;
			RestaurantTone = restaurantTone;
		}
	}
}
