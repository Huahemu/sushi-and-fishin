using System;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Interaction.GenericLogics
{
	[Serializable]
	public sealed class BlueMarketTool : IGenericInteractLogic
	{
		[SerializeField]
		private bool _canGaze = true;

		[SerializeField]
		private bool _canInteractable = true;

		public bool CanGaze => _canGaze;

		public bool CanInteractable => _canInteractable;

		public void GazeEnter(GenericInteractable owner)
		{
			Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_take");
		}

		public void GazeExit(GenericInteractable owner)
		{
			Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
		}

		public void TriggerInput(GenericInteractable owner, Interactor interactor, InputKind inputKind, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && inputKind == InputKind.Mouse1)
			{
				Singleton<GameProgressManager>.Instance.CompleteBlueMarketHandle();
			}
		}

		public void TriggerCrossInput(GenericInteractable owner, Interactor interactor, BaseHoldable held, InputKind inputKind, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && inputKind == InputKind.Mouse1)
			{
				TriggerInput(owner, interactor, inputKind, phase);
			}
		}
	}
}
