using PaperPixelGames.Core.Interaction;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core
{
	public interface IBasicInteractable
	{
		bool CanGaze { get; }

		bool CanInteractable { get; }

		void GazeEnter();

		void GazeExit();

		void TriggerInput(Interactor interactor, InputKind inputKind, InputActionPhase phase);

		void TriggerCrossInput(Interactor interactor, BaseHoldable held, InputKind inputKind, InputActionPhase phase);
	}
}
