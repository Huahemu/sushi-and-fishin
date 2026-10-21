using Unity.Netcode;

public struct ServedDishTakenFromDeskEvent : IEvent
{
	public NetworkObject DishNetworkObject;
}
