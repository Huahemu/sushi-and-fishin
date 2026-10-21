using System;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Interaction.GenericLogics
{
	[Serializable]
	public sealed class DebugGenericInteractLogic : IGenericInteractLogic
	{
		[SerializeField]
		private bool _canGaze = true;

		[SerializeField]
		private bool _canInteractable = true;

		[SerializeField]
		private string _tag = "GenericInteract";

		public bool CanGaze => _canGaze;

		public bool CanInteractable => _canInteractable;

		public void GazeEnter(GenericInteractable owner)
		{
			UtilityMethods.Log("[" + _tag + "] GazeEnter -> " + owner.name);
		}

		public void GazeExit(GenericInteractable owner)
		{
			UtilityMethods.Log("[" + _tag + "] GazeExit -> " + owner.name);
		}

		public void TriggerInput(GenericInteractable owner, Interactor interactor, InputKind inputKind, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Performed)
			{
				UtilityMethods.Log($"[{_tag}] TriggerInput -> Owner={owner.name}, Interactor={interactor?.name}, Input={inputKind}, Phase={phase}");
			}
		}

		public void TriggerCrossInput(GenericInteractable owner, Interactor interactor, BaseHoldable held, InputKind inputKind, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Performed)
			{
				UtilityMethods.Log($"[{_tag}] TriggerCrossInput -> Owner={owner.name}, Held={held?.name}, Input={inputKind}, Phase={phase}");
			}
		}
	}
}
