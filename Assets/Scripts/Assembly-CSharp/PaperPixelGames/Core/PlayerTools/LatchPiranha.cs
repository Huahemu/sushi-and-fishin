using Deform;
using EPOOutline;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.PlayerTools
{
	public class LatchPiranha : BaseInteractable, IToolActiveInteractable
	{
		[SerializeField]
		private int _index;

		[SerializeField]
		private Collider _collider;

		[SerializeField]
		private GameObject _visual;

		[SerializeField]
		private Outlinable _visualOutlinable;

		[SerializeField]
		private SineDeformer _deformer;

		[SerializeField]
		private DivingEquipment _divingEquipment;

		[SerializeField]
		private AudioSource _removeSfx;

		public int Index => _index;

		public bool IsLatched
		{
			get
			{
				if (_visual != null)
				{
					return _visual.activeSelf;
				}
				return false;
			}
		}

		public bool CanInteractable => true;

		public void BindDivingEquipment(DivingEquipment divingEquipment)
		{
			if (_divingEquipment == null)
			{
				_divingEquipment = divingEquipment;
			}
		}

		protected override void Awake()
		{
			base.Awake();
			CacheVisualOutlinable();
			SetVisualStatus(status: false);
		}

		private void OnDisable()
		{
			SetVisualStatus(status: false);
		}

		public void SetVisualStatus(bool status)
		{
			if (status)
			{
				UtilityMethods.SetLayerRecursive(base.gameObject, "IgnoreHarpoon", includeChildren: false);
			}
			if (_deformer != null)
			{
				_deformer.update = status;
			}
			if (_collider != null)
			{
				_collider.enabled = status;
			}
			if (_visual != null)
			{
				_visual.SetActive(status);
			}
		}

		protected override void ClientInteract(Interactor interactor, InputActionPhase phase)
		{
			base.ClientInteract(interactor, phase);
			TryRemove(phase);
		}

		protected override void ClientCrossInteract(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			base.ClientCrossInteract(interactor, held, phase);
			TryRemove(phase);
		}

		private void TryRemove(InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && !(_divingEquipment == null))
			{
				_divingEquipment.UnlatchPiranha(Index);
			}
		}

		public void PlayRemoveSfxLocal()
		{
			if (_removeSfx != null)
			{
				_removeSfx.Play();
			}
		}

		public override void EnableGazeHints()
		{
			Singleton<HintsManager>.Instance.AddHint("Interact", "key_remove");
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("Interact");
		}

		private void CacheVisualOutlinable()
		{
			if (_visualOutlinable == null && _visual != null)
			{
				_visualOutlinable = _visual.GetComponentInChildren<Outlinable>(includeInactive: true);
			}
			if (_visualOutlinable == null)
			{
				_visualOutlinable = GetComponent<Outlinable>();
			}
			if (_visualOutlinable == null)
			{
				_visualOutlinable = base.gameObject.AddComponent<Outlinable>();
			}
			if (!(_visualOutlinable == null))
			{
				RegisterVisualRenderersToOutline(_visualOutlinable);
				_outlinable = _visualOutlinable;
				_outlinable.enabled = false;
			}
		}

		private void RegisterVisualRenderersToOutline(Outlinable outlinable)
		{
			if (outlinable == null || _visual == null)
			{
				return;
			}
			while (outlinable.OutlineTargetsCount > 0)
			{
				outlinable.RemoveTarget(outlinable[0]);
			}
			Renderer[] componentsInChildren = _visual.GetComponentsInChildren<Renderer>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				if (componentsInChildren[i] != null)
				{
					outlinable.AddRenderer(componentsInChildren[i]);
				}
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "LatchPiranha";
		}
	}
}
