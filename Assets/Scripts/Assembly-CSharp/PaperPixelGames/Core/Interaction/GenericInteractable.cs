using EPOOutline;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Interaction
{
	public class GenericInteractable : MonoBehaviour, IBasicInteractable
	{
		[Header("Highlight")]
		[SerializeField]
		private Outlinable _outlinable;

		[SerializeField]
		[SerializeReference]
		private IGenericInteractLogic _logic;

		public bool CanGaze => _logic?.CanGaze ?? true;

		public bool CanInteractable => _logic?.CanInteractable ?? true;

		private void Awake()
		{
			if (_outlinable == null)
			{
				TryGetComponent<Outlinable>(out _outlinable);
			}
			if (!(_outlinable == null))
			{
				if (_outlinable.OutlineTargetsCount == 0)
				{
					_outlinable.AddAllChildRenderersToRenderingList(RenderersAddingMode.MeshRenderer | RenderersAddingMode.SkinnedMeshRenderer | RenderersAddingMode.SpriteRenderer);
				}
				_outlinable.enabled = false;
			}
		}

		private void OnDisable()
		{
			ClearHighlight();
		}

		public void GazeEnter()
		{
			_logic?.GazeEnter(this);
			SetHighlight();
		}

		public void GazeExit()
		{
			_logic?.GazeExit(this);
			ClearHighlight();
		}

		public void TriggerCrossInput(Interactor interactor, BaseHoldable held, InputKind inputKind, InputActionPhase phase)
		{
			_logic?.TriggerCrossInput(this, interactor, held, inputKind, phase);
		}

		public void TriggerInput(Interactor interactor, InputKind inputKind, InputActionPhase phase)
		{
			_logic?.TriggerInput(this, interactor, inputKind, phase);
		}

		private void SetHighlight()
		{
			if (!(_outlinable == null))
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
	}
}
