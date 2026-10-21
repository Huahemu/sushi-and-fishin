using DG.Tweening;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.ScrapSystem
{
	public class ScrapForgeButton : MonoBehaviour, IBasicInteractable
	{
		private static readonly int EmissionID = Shader.PropertyToID("_EmissionColor");

		[SerializeField]
		private Transform _button;

		[SerializeField]
		private int _index;

		[SerializeField]
		private bool _levelButton;

		[Space]
		[Header("Visual")]
		[SerializeField]
		private MeshRenderer _renderer;

		[SerializeField]
		private Material _matDisabled;

		[SerializeField]
		private Material _matOwned;

		[SerializeField]
		private Material _matAvailable;

		[Header("Emission Settings")]
		[SerializeField]
		private float _baseEmission = 0.2f;

		[SerializeField]
		private float _selectedEmission = 2f;

		private MaterialPropertyBlock _block;

		private ScrapForge _forge;

		private float _defaultY;

		public bool CanGaze => true;

		public bool CanInteractable => true;

		private void Awake()
		{
			_defaultY = _button.transform.localPosition.y;
		}

		public void Setup(ScrapForge forge)
		{
			_forge = forge;
		}

		public void SetState(ForgeLevelButtonState state, bool isSelected)
		{
			if (!(_renderer == null))
			{
				Material material = state switch
				{
					ForgeLevelButtonState.Owned => _matOwned, 
					ForgeLevelButtonState.Available => _matAvailable, 
					_ => _matDisabled, 
				};
				_renderer.sharedMaterial = material;
				if (_block == null)
				{
					_block = new MaterialPropertyBlock();
				}
				_renderer.GetPropertyBlock(_block);
				float num = (isSelected ? _selectedEmission : _baseEmission);
				Color value = (material.HasProperty(EmissionID) ? material.GetColor(EmissionID) : Color.white) * num;
				_block.SetColor(EmissionID, value);
				_renderer.SetPropertyBlock(_block);
			}
		}

		private void PlayPressTween()
		{
			_button.DOKill();
			Singleton<SoundManager>.Instance.PlaySFX(SoundType.Scrap_Forge_Button, base.transform.position);
			_button.DOLocalMoveY(_defaultY - 0.015f, 0.05f).OnComplete(() =>
			{
				_button.DOLocalMoveY(_defaultY, 0.15f);
			});
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
			if (phase == InputActionPhase.Started)
			{
				PlayPressTween();
				if (_levelButton)
				{
					_forge.OnLevelButtonClick(_index);
				}
				else
				{
					_forge.OnToolButtonClick(_index);
				}
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
