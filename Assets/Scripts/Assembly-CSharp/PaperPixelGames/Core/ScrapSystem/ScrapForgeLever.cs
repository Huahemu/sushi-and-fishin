using DG.Tweening;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.ScrapSystem
{
	public class ScrapForgeLever : MonoBehaviour, IBasicInteractable
	{
		[SerializeField]
		private Transform _lever;

		public float DefaultX = 55f;

		public float PulledX = 105f;

		private Sequence _currentTween;

		private ScrapForge _forge;

		private bool _onSuccessTween;

		public bool CanGaze => true;

		public bool CanInteractable => true;

		public void Setup(ScrapForge forge)
		{
			_forge = forge;
		}

		public void GazeEnter()
		{
			Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_press");
		}

		public void GazeExit()
		{
			Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
		}

		public void Mouse1(Interactor interactor, InputActionPhase phase)
		{
			if (!_onSuccessTween && phase == InputActionPhase.Started)
			{
				UtilityMethods.Log("[ScrapForgeLever] Mouse1 pressed");
				if (!(_lever == null) && !(_forge == null))
				{
					ResetAndKillTweens();
					_forge.OnLeverPull();
				}
			}
		}

		private void ResetAndKillTweens()
		{
			if (!_onSuccessTween)
			{
				_currentTween?.Kill();
				_currentTween = null;
				if (_lever != null)
				{
					_lever.localRotation = Quaternion.Euler(DefaultX, 0f, 0f);
				}
			}
		}

		public void PlaySuccessTween()
		{
			if (!_onSuccessTween)
			{
				_onSuccessTween = true;
				Sequence sequence = DOTween.Sequence();
				Singleton<SoundManager>.Instance.PlaySFX(SoundType.Scrap_Forge_Lever_Success, base.transform.position);
				sequence.Append(_lever.DOLocalRotate(new Vector3(DefaultX - 7f, 0f, 0f), 0.08f).SetEase(Ease.OutQuad));
				sequence.Append(_lever.DOLocalRotate(new Vector3(PulledX + 3f, 0f, 0f), 0.18f).SetEase(Ease.OutCubic));
				sequence.Append(_lever.DOLocalRotate(new Vector3(PulledX + 1f, 0f, 0f), 0.35f).SetEase(Ease.OutCubic));
				sequence.Join(_lever.DOLocalRotate(new Vector3(DefaultX, 0f, 0f), 0.35f).SetEase(Ease.OutElastic, 1.2f, 0.25f).SetDelay(0.35f)).OnComplete(() =>
				{
					_onSuccessTween = false;
				});
				_currentTween = sequence;
			}
		}

		public void PlayFailTween()
		{
			if (!_onSuccessTween)
			{
				Sequence sequence = DOTween.Sequence();
				Singleton<SoundManager>.Instance.PlaySFX(SoundType.Scrap_Forge_Lever_Fail, base.transform.position);
				sequence.Append(_lever.DOLocalRotate(new Vector3(DefaultX + 10f, 0f, 0f), 0.12f).SetEase(Ease.OutCubic));
				sequence.Append(_lever.DOLocalRotate(new Vector3(DefaultX, 0f, 0f), 0.2f).SetEase(Ease.InOutBack));
				_currentTween = sequence;
			}
		}

		public void TriggerInput(Interactor interactor, InputKind inputKind, InputActionPhase phase)
		{
			if (CanInteractable)
			{
				switch (inputKind)
				{
				case InputKind.Mouse1:
					Mouse1(interactor, phase);
					break;
				case InputKind.Interact:
				case InputKind.Mouse2:
					break;
				}
			}
		}

		public void TriggerCrossInput(Interactor interactor, BaseHoldable held, InputKind inputKind, InputActionPhase phase)
		{
			switch (inputKind)
			{
			case InputKind.Mouse1:
				Mouse1(interactor, phase);
				break;
			case InputKind.Interact:
			case InputKind.Mouse2:
				break;
			}
		}
	}
}
