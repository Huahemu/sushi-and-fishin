using DG.Tweening;
using EPOOutline;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Interaction
{
	public abstract class Base3DButton : MonoBehaviour, IBasicInteractable
	{
		[SerializeField]
		private Outlinable _outlinable;

		private float _tweenDuration = 0.05f;

		public bool CanGaze => true;

		public bool CanInteractable => true;

		public virtual void GazeEnter()
		{
			Singleton<HighlightManager>.Instance.SetHighlight(_outlinable);
		}

		public virtual void GazeExit()
		{
			Singleton<HighlightManager>.Instance.ClearCurrentHighlight();
		}

		public virtual void Interact(Interactor interactor, InputActionPhase phase)
		{
		}

		public virtual void Mouse1(Interactor interactor, InputActionPhase phase)
		{
			base.transform.DOScale(0.9f, _tweenDuration).SetEase(Ease.InQuad).OnComplete(() =>
			{
				base.transform.DOScale(1f, _tweenDuration).SetEase(Ease.InQuad);
			});
		}

		public virtual void Mouse2(Interactor interactor, InputActionPhase phase)
		{
		}

		public void TriggerCrossInput(Interactor interactor, BaseHoldable held, InputKind inputKind, InputActionPhase phase)
		{
			switch (inputKind)
			{
			}
		}

		public void TriggerInput(Interactor interactor, InputKind inputKind, InputActionPhase phase)
		{
			switch (inputKind)
			{
			case InputKind.Interact:
				Interact(interactor, phase);
				break;
			case InputKind.Mouse1:
				Mouse1(interactor, phase);
				break;
			case InputKind.Mouse2:
				Mouse2(interactor, phase);
				break;
			}
		}
	}
}
