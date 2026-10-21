using EPOOutline;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Kitchen
{
	public class BeverageDoor : MonoBehaviour, IBasicInteractable, IHeldItemMouse1Interactable
	{
		[SerializeField]
		private BeverageFridge _beverageFridge;

		[SerializeField]
		private Animator _doorAnimator;

		[SerializeField]
		private Outlinable _outlinable;

		[Header("Interaction")]
		[SerializeField]
		private float _toggleCooldown = 0.08f;

		private bool _cooldownFlag;

		private bool _isGazing;

		private Coroutine _flagCoroutine;

		public bool CanGaze => true;

		public bool CanInteractable => true;

		private void OnDisable()
		{
			ClearHighlight();
		}

		public void ChangeDoorStatus(bool isOpen, bool playSfx = true)
		{
			if (_doorAnimator != null)
			{
				_doorAnimator.SetBool("IsOpen", isOpen);
			}
			if (playSfx)
			{
				PlayDoorSfx(isOpen);
			}
			RefreshHints();
		}

		public void GazeEnter()
		{
			_isGazing = true;
			RefreshHints();
			SetHighlight();
		}

		public void GazeExit()
		{
			_isGazing = false;
			ClearHints();
			ClearHighlight();
		}

		public void TriggerCrossInput(Interactor interactor, BaseHoldable held, InputKind inputKind, InputActionPhase phase)
		{
			TryHandleInput(interactor, inputKind, phase);
		}

		public void TriggerInput(Interactor interactor, InputKind inputKind, InputActionPhase phase)
		{
			TryHandleInput(interactor, inputKind, phase);
		}

		private void TryHandleInput(Interactor interactor, InputKind inputKind, InputActionPhase phase)
		{
			if (phase != InputActionPhase.Started)
			{
				return;
			}
			if (_beverageFridge == null)
			{
				UtilityMethods.Log("<color=yellow>[BeverageDoor] Interaction failed. BeverageFridge is not assigned.</color>");
			}
			else if (inputKind == InputKind.Interact)
			{
				if (!_beverageFridge.IsPhysicallyUsable)
				{
					_beverageFridge.TryUnlockPhysicalStateAndOpenTutorial(interactor);
				}
			}
			else
			{
				TryToggleDoor(interactor, inputKind, phase);
			}
		}

		public void TryToggleDoor(Interactor interactor, InputKind inputKind, InputActionPhase phase)
		{
			if (inputKind != InputKind.Mouse1 || phase != InputActionPhase.Started || _cooldownFlag)
			{
				return;
			}
			if (_beverageFridge == null)
			{
				UtilityMethods.Log("<color=yellow>[BeverageDoor] Toggle failed. BeverageFridge is not assigned.</color>");
			}
			else if (_beverageFridge.IsPhysicallyUsable)
			{
				StartCooldown();
				if (!_beverageFridge.IsDoorOpen)
				{
					_beverageFridge.TryOpenTutorialForFirstDoorOpen(interactor);
				}
				_beverageFridge.ServerToggleDoorRpc();
			}
		}

		public void RefreshHints()
		{
			if (!_isGazing || Singleton<HintsManager>.Instance == null)
			{
				return;
			}
			ClearHints();
			if (!(_beverageFridge == null))
			{
				if (!_beverageFridge.IsPhysicallyUsable)
				{
					Singleton<HintsManager>.Instance.AddHint("Interact", "key_repair");
					_beverageFridge.ShowUnlockTooltipLocal();
				}
				else
				{
					Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", _beverageFridge.IsDoorOpen ? "key_closelid" : "key_openlid");
				}
			}
		}

		public void ResetRuntimeState()
		{
			if (_flagCoroutine != null && Singleton<DelayHelper>.Instance != null)
			{
				Singleton<DelayHelper>.Instance.StopCoroutine(_flagCoroutine);
			}
			_flagCoroutine = null;
			_cooldownFlag = false;
			RefreshHints();
		}

		private void ClearHints()
		{
			_beverageFridge?.HideUnlockTooltipLocal();
			if (!(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
				Singleton<HintsManager>.Instance.RemoveHint("Interact");
			}
		}

		private void SetHighlight()
		{
			if (_outlinable != null)
			{
				Singleton<HighlightManager>.Instance?.SetHighlight(_outlinable);
			}
		}

		private void ClearHighlight()
		{
			if (!(_outlinable == null))
			{
				HighlightManager instance = Singleton<HighlightManager>.Instance;
				if (instance != null)
				{
					instance.ClearCurrentHighlight(_outlinable);
				}
				else
				{
					_outlinable.enabled = false;
				}
			}
		}

		private void StartCooldown()
		{
			_cooldownFlag = true;
			if (_flagCoroutine != null && Singleton<DelayHelper>.Instance != null)
			{
				Singleton<DelayHelper>.Instance.StopCoroutine(_flagCoroutine);
			}
			if (Singleton<DelayHelper>.Instance == null || _toggleCooldown <= 0f)
			{
				_cooldownFlag = false;
				_flagCoroutine = null;
				return;
			}
			_flagCoroutine = Singleton<DelayHelper>.Instance.DelayAction(_toggleCooldown, () =>
			{
				_cooldownFlag = false;
				_flagCoroutine = null;
			});
		}

		private void PlayDoorSfx(bool isOpen)
		{
			AudioClip audioClip = (isOpen ? _beverageFridge.DoorOpenSfx : _beverageFridge.DoorCloseSfx);
			if (!(audioClip == null))
			{
				_beverageFridge.AudioSource.PlayOneShot(audioClip);
			}
		}
	}
}
