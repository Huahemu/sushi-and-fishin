namespace SushiAndFish.Casino.Original
{
using global::Mirror;

public struct WebSocketRelayMessage : NetworkMessage
{
	public string rawData;

	public string messageType;
}

}
