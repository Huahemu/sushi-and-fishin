using System;
using PaperPixelGames.Core.Boss;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Interaction.GenericLogics
{
	[Serializable]
	public sealed class CrabBossPoseidonTridentReward : IGenericInteractLogic
	{
		[SerializeField]
		private CrabBossController _crabBossController;

		public bool CanGaze
		{
			get
			{
				if (_crabBossController != null)
				{
					return _crabBossController.IsPoseidonTridentRewardAvailable;
				}
				return false;
			}
		}

		public bool CanInteractable => CanGaze;

		public void GazeEnter(GenericInteractable owner)
		{
			Singleton<HintsManager>.Instance?.AddHint("LeftMouseMechanic", "key_take");
		}

		public void GazeExit(GenericInteractable owner)
		{
			Singleton<HintsManager>.Instance?.RemoveHint("LeftMouseMechanic");
		}

		public void TriggerInput(GenericInteractable owner, Interactor interactor, InputKind inputKind, InputActionPhase phase)
		{
			if (inputKind == InputKind.Mouse1 && phase == InputActionPhase.Started && CanInteractable)
			{
				_crabBossController.RequestClaimPoseidonTridentReward();
			}
		}

		public void TriggerCrossInput(GenericInteractable owner, Interactor interactor, BaseHoldable held, InputKind inputKind, InputActionPhase phase)
		{
			TriggerInput(owner, interactor, inputKind, phase);
		}
	}
}
