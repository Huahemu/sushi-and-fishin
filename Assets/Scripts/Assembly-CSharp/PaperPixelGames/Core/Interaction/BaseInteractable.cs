using System;
using EPOOutline;
using PaperPixelGames.Core.Interaction.ToolTip;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Framework.Indicator;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Interaction
{
	public abstract class BaseInteractable : NetworkBehaviour
	{
		[Header("BaseInteract")]
		[SerializeField]
		protected ItemType _itemType;

		[SerializeField]
		protected OffscreenIndicatorTarget _indicatorTarget;

		[SerializeField]
		protected Outlinable _outlinable;

		[SerializeField]
		protected InteractableTooltip _interactableTooltip;

		[SerializeField]
		protected bool _isScannable = true;

		[NonSerialized]
		private NetworkVariable<bool> _scanSuppressedByPlacement = new NetworkVariable<bool>(value: false);

		protected bool _inGaze;

		[NonSerialized]
		public NetworkVariable<bool> CanGaze = new NetworkVariable<bool>(value: true);

		[NonSerialized]
		public NetworkVariable<bool> CanInteract = new NetworkVariable<bool>(value: true);

		[NonSerialized]
		public NetworkVariable<bool> CanCross = new NetworkVariable<bool>(value: true);

		public virtual bool IsScanable
		{
			get
			{
				if (_isScannable)
				{
					return !_scanSuppressedByPlacement.Value;
				}
				return false;
			}
		}

		public ItemType ItemType => _itemType;

		public Outlinable Outlinable => _outlinable;

		public OffscreenIndicatorTarget IndicatorTarget => _indicatorTarget;

		protected bool IsPlacementScanSuppressed => _scanSuppressedByPlacement.Value;

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<bool> scanSuppressedByPlacement = _scanSuppressedByPlacement;
			scanSuppressedByPlacement.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(scanSuppressedByPlacement.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnPlacementScanSuppressionChanged));
		}

		public override void OnNetworkDespawn()
		{
			NetworkVariable<bool> scanSuppressedByPlacement = _scanSuppressedByPlacement;
			scanSuppressedByPlacement.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(scanSuppressedByPlacement.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnPlacementScanSuppressionChanged));
			base.OnNetworkDespawn();
		}

		protected void ServerSetPlacementScanSuppressed(bool suppressed)
		{
			if (base.IsServer && _scanSuppressedByPlacement.Value != suppressed)
			{
				_scanSuppressedByPlacement.Value = suppressed;
			}
		}

		protected virtual void OnPlacementScanSuppressionChanged(bool previousValue, bool currentValue)
		{
		}

		public virtual bool TryGetScanIndicatorVisualSettings(out OffscreenIndicatorVisualSettings settings)
		{
			settings = null;
			return false;
		}

		protected virtual void Awake()
		{
			if (_interactableTooltip == null)
			{
				TryGetComponent<InteractableTooltip>(out _interactableTooltip);
			}
			if (_indicatorTarget == null)
			{
				TryGetComponent<OffscreenIndicatorTarget>(out _indicatorTarget);
			}
			if (_outlinable == null)
			{
				TryGetComponent<Outlinable>(out _outlinable);
			}
			if (_outlinable != null)
			{
				_outlinable.enabled = false;
			}
		}

		public virtual void GazeEnter()
		{
			if (CanGaze.Value)
			{
				_inGaze = true;
				EnableGazeHints();
				if (_outlinable != null)
				{
					_outlinable.enabled = true;
					Singleton<HighlightManager>.Instance.SetHighlight(_outlinable);
				}
			}
		}

		public virtual void GazeExit()
		{
			_inGaze = false;
			DisableGazeHints();
			if (_outlinable != null)
			{
				Singleton<HighlightManager>.Instance.ClearCurrentHighlight();
				_outlinable.enabled = false;
			}
		}

		public abstract void EnableGazeHints();

		public abstract void DisableGazeHints();

		protected void RefreshGazeFeedback()
		{
			if (_inGaze && CanGaze.Value)
			{
				DisableGazeHints();
				EnableGazeHints();
				if (!(_outlinable == null))
				{
					_outlinable.enabled = true;
					Singleton<HighlightManager>.Instance.SetHighlight(_outlinable);
				}
			}
		}

		protected virtual bool CanTrigger(Interactor interactor)
		{
			return CanInteract.Value;
		}

		public void TriggerInput(Interactor interactor, InputKind inputKind, InputActionPhase phase)
		{
			if (phase != InputActionPhase.Started || CanTrigger(interactor))
			{
				switch (inputKind)
				{
				case InputKind.Interact:
					ClientInteract(interactor, phase);
					break;
				case InputKind.Mouse1:
					ClientMouse1(interactor, phase);
					break;
				case InputKind.Mouse2:
					ClientMouse2(interactor, phase);
					break;
				}
			}
		}

		protected virtual void ClientInteract(Interactor interactor, InputActionPhase phase)
		{
		}

		protected virtual void ClientMouse1(Interactor interactor, InputActionPhase phase)
		{
		}

		protected virtual void ClientMouse2(Interactor interactor, InputActionPhase phase)
		{
		}

		protected virtual bool CanCrossTrigger(Interactor interactor, BaseHoldable held)
		{
			if (CanInteract.Value)
			{
				return CanCross.Value;
			}
			return false;
		}

		public void TriggerCrossInput(Interactor interactor, BaseHoldable held, InputKind inputKind, InputActionPhase phase)
		{
			if (phase != InputActionPhase.Started || CanCrossTrigger(interactor, held))
			{
				switch (inputKind)
				{
				case InputKind.Interact:
					ClientCrossInteract(interactor, held, phase);
					break;
				case InputKind.Mouse1:
					ClientCrossMouse1(interactor, held, phase);
					break;
				case InputKind.Mouse2:
					ClientCrossMouse2(interactor, held, phase);
					break;
				}
			}
		}

		protected virtual void ClientCrossInteract(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
		}

		protected virtual void ClientCrossMouse1(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
		}

		protected virtual void ClientCrossMouse2(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
		}

		protected override void __initializeVariables()
		{
			if (_scanSuppressedByPlacement == null)
			{
				throw new Exception("BaseInteractable._scanSuppressedByPlacement cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_scanSuppressedByPlacement.Initialize(this);
			__nameNetworkVariable(_scanSuppressedByPlacement, "_scanSuppressedByPlacement");
			NetworkVariableFields.Add(_scanSuppressedByPlacement);
			if (CanGaze == null)
			{
				throw new Exception("BaseInteractable.CanGaze cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			CanGaze.Initialize(this);
			__nameNetworkVariable(CanGaze, "CanGaze");
			NetworkVariableFields.Add(CanGaze);
			if (CanInteract == null)
			{
				throw new Exception("BaseInteractable.CanInteract cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			CanInteract.Initialize(this);
			__nameNetworkVariable(CanInteract, "CanInteract");
			NetworkVariableFields.Add(CanInteract);
			if (CanCross == null)
			{
				throw new Exception("BaseInteractable.CanCross cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			CanCross.Initialize(this);
			__nameNetworkVariable(CanCross, "CanCross");
			NetworkVariableFields.Add(CanCross);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "BaseInteractable";
		}
	}
}
