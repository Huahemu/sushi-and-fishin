namespace SushiAndFish.Casino.Original
{
using global::UnityEngine.Events;

public class InteractableEventTrigger : InteractableBase
{
	public UnityEvent<PlayerInteract> onInteractEvent;

	public UnityEvent<PlayerInteract> serverOnInteractEvent;

	public UnityEvent<PlayerInteract> rpcOnInteractEvent;

	public UnityEvent<PlayerInteract> onHoverEvent;

	public UnityEvent<PlayerInteract> onHoverExitEvent;

	public UnityEvent<PlayerInteract> onHoldEvent;

	public UnityEvent<PlayerInteract> onHoldExitEvent;

	public override void OnInteract(PlayerInteract playerInteract)
	{
		base.OnInteract(playerInteract);
		onInteractEvent?.Invoke(playerInteract);
	}

	public override void ServerOnInteract(PlayerInteract playerInteract)
	{
		base.ServerOnInteract(playerInteract);
		serverOnInteractEvent?.Invoke(playerInteract);
	}

	public override void RpcOnInteract(PlayerInteract playerInteract)
	{
		base.RpcOnInteract(playerInteract);
		rpcOnInteractEvent?.Invoke(playerInteract);
	}

	public override void OnHover(PlayerInteract playerInteract)
	{
		base.OnHover(playerInteract);
		onHoverEvent?.Invoke(playerInteract);
	}

	public override void OnHoverExit(PlayerInteract playerInteract)
	{
		base.OnHoverExit(playerInteract);
		onHoverExitEvent?.Invoke(playerInteract);
	}

	public override void OnHold(PlayerInteract playerInteract)
	{
		base.OnHold(playerInteract);
		onHoldEvent?.Invoke(playerInteract);
	}

	public override void OnHoldExit(PlayerInteract playerInteract)
	{
		base.OnHoldExit(playerInteract);
		onHoldExitEvent?.Invoke(playerInteract);
	}

	public override bool Weaved()
	{
		return true;
	}
}

}
