using System;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Interaction.Test
{
	public class TestBasicInteractable : MonoBehaviour, IBasicInteractable
	{
		public bool CanGaze => true;

		public bool CanInteractable => true;

		public void GazeEnter()
		{
			UtilityMethods.Log(base.name + " - GazeEnter");
		}

		public void GazeExit()
		{
			UtilityMethods.Log(base.name + " - GazeExit");
		}

		public void TriggerCrossInput(Interactor interactor, BaseHoldable held, InputKind inputKind, InputActionPhase phase)
		{
			throw new NotImplementedException();
		}

		public void TriggerInput(Interactor interactor, InputKind inputKind, InputActionPhase phase)
		{
			throw new NotImplementedException();
		}
	}
}
