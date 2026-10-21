namespace SushiAndFish.Casino.Original
{
using global::System;
using global::Mirror;

public interface IInteractable
{
	float HoldDuration { get; set; }

	bool HoldInteract { get; set; }

	bool IsInteractable { get; set; }

	bool MeetRequirements { get; set; }

	bool IsBeingHovered { get; set; }

	bool IsBeingHold { get; set; }

	float HoldProgress { get; set; }

	string TooltipMessage { get; set; }

	string InteractableName { get; set; }

	CursorManager.CursorType CursorType { get; set; }

	event Action<IInteractable> OnInteractableChanged;

	void OnHover(PlayerInteract playerInteract);

	[Server]
	void ServerOnHover(PlayerInteract playerInteract);

	void OnHoverExit(PlayerInteract playerInteract);

	[Server]
	void ServerOnHoverExit(PlayerInteract playerInteract);

	void OnHold(PlayerInteract playerInteract);

	[Server]
	void ServerOnHold(PlayerInteract playerInteract);

	void OnHoldExit(PlayerInteract playerInteract);

	[Server]
	void ServerOnHoldExit(PlayerInteract playerInteract);

	void OnInteract(PlayerInteract playerInteract);

	[Server]
	void ServerOnInteract(PlayerInteract playerInteract);

	void RpcOnInteract(PlayerInteract playerInteract);
}

}
