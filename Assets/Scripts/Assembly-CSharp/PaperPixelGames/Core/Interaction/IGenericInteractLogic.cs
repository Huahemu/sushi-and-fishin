using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Interaction
{
	public interface IGenericInteractLogic
	{
		bool CanGaze { get; }

		bool CanInteractable { get; }

		void GazeEnter(GenericInteractable owner);

		void GazeExit(GenericInteractable owner);

		void TriggerInput(GenericInteractable owner, Interactor interactor, InputKind inputKind, InputActionPhase phase);

		void TriggerCrossInput(GenericInteractable owner, Interactor interactor, BaseHoldable held, InputKind inputKind, InputActionPhase phase);
	}
}
