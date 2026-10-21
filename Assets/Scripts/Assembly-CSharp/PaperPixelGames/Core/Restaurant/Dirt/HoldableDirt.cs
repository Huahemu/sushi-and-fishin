using System;
using EPOOutline;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Restaurant.Dirt
{
	public class HoldableDirt : BaseHoldable
	{
		private const string DirtStackId = "HoldableDirt";

		[SerializeField]
		private Outlinable outlinable;

		[SerializeField]
		private bool _isDefaultDirt;

		[Header("World Visual State")]
		[SerializeField]
		private GameObject _initialDirtObject;

		[SerializeField]
		private Collider _initialDirtCollider;

		[SerializeField]
		private GameObject _placedTrashBagObject;

		[SerializeField]
		private Collider _placedTrashBagCollider;

		private readonly NetworkVariable<bool> _hasBeenHeld = new NetworkVariable<bool>(value: false);

		private bool _isHeldInClientRepresentation;

		private bool _restorePlacedTrashBagTriggerAfterThrow;

		public override string QuickSlotStackId => "HoldableDirt";

		public override void OnNetworkSpawn()
		{
			NetworkVariable<bool> hasBeenHeld = _hasBeenHeld;
			hasBeenHeld.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(hasBeenHeld.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnHeldStateChanged));
			base.OnNetworkSpawn();
			NetworkVariable<bool> canHold = CanHold;
			canHold.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(canHold.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnCanHoldChanged));
			RefreshWorldVisualState();
		}

		public override void OnNetworkDespawn()
		{
			NetworkVariable<bool> canHold = CanHold;
			canHold.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(canHold.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnCanHoldChanged));
			NetworkVariable<bool> hasBeenHeld = _hasBeenHeld;
			hasBeenHeld.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(hasBeenHeld.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnHeldStateChanged));
			base.OnNetworkDespawn();
			if (base.IsServer)
			{
				QuestReferenceHub.Instance.AddProgress(QuestId.CleanUpTheTrash);
				Singleton<HintsManager>.Instance.ClearAllHints();
			}
		}

		public override void ServerHold(Interactor interactor)
		{
			base.ServerHold(interactor);
			if (base.IsServer)
			{
				_hasBeenHeld.Value = true;
			}
		}

		protected override void ClientHoldEveryone()
		{
			_isHeldInClientRepresentation = true;
			SetWorldVisuals(initialDirtVisible: false, placedTrashBagVisible: false);
		}

		public override void ClientHold(Interactor interactor)
		{
			base.ClientHold(interactor);
			_isHeldInClientRepresentation = true;
			SetWorldVisuals(initialDirtVisible: false, placedTrashBagVisible: false);
		}

		protected override void ClientPlace(Vector3 pos, Vector3 rot, ulong holderClientId)
		{
			_isHeldInClientRepresentation = false;
			base.ClientPlace(pos, rot, holderClientId);
		}

		protected override void ClientDrop(Vector3 pos, Quaternion rot)
		{
			_isHeldInClientRepresentation = false;
			base.ClientDrop(pos, rot);
			RefreshWorldVisualState();
		}

		protected override void ClientOnPhysicsThrowStarted(bool isLocalHolder)
		{
			_isHeldInClientRepresentation = false;
			if (_placedTrashBagCollider != null && _placedTrashBagCollider.isTrigger)
			{
				_placedTrashBagCollider.isTrigger = false;
				_restorePlacedTrashBagTriggerAfterThrow = true;
			}
			RefreshWorldVisualState();
		}

		protected override void ClientOnPhysicsThrowEnded()
		{
			if (_restorePlacedTrashBagTriggerAfterThrow && _placedTrashBagCollider != null)
			{
				_placedTrashBagCollider.isTrigger = true;
			}
			_restorePlacedTrashBagTriggerAfterThrow = false;
			RefreshWorldVisualState();
		}

		public override void ClientRefreshRuntimeRepresentationState()
		{
			RefreshWorldVisualState();
		}

		public override void GazeEnter()
		{
			base.GazeEnter();
			if (outlinable != null)
			{
				Singleton<HighlightManager>.Instance.SetHighlight(outlinable);
			}
		}

		public override void GazeExit()
		{
			base.GazeExit();
			if (outlinable != null)
			{
				Singleton<HighlightManager>.Instance.ClearCurrentHighlight();
			}
		}

		public override void EnableGazeHints()
		{
			Singleton<HintsManager>.Instance.ClearAllHints();
			Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_hold");
		}

		public override void DisableGazeHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
		}

		public override void EnableHeldHints()
		{
			Singleton<HintsManager>.Instance.ClearAllHints();
			Singleton<HintsManager>.Instance.AddHint("RightMouseMechanic", "key_place");
			AddHeldThrowHint();
		}

		public override void DisableHeldHints()
		{
			Singleton<HintsManager>.Instance.RemoveHint("RightMouseMechanic");
			RemoveHeldThrowHint();
		}

		private void OnHeldStateChanged(bool previousValue, bool currentValue)
		{
			RefreshWorldVisualState();
		}

		private void OnCanHoldChanged(bool previousValue, bool currentValue)
		{
			RefreshWorldVisualState();
		}

		private void RefreshWorldVisualState()
		{
			if (_isHeldInClientRepresentation)
			{
				SetWorldVisuals(initialDirtVisible: false, placedTrashBagVisible: false);
				return;
			}
			bool value = CanHold.Value;
			SetWorldVisuals(value && !_hasBeenHeld.Value, value && _hasBeenHeld.Value);
		}

		private void SetWorldVisuals(bool initialDirtVisible, bool placedTrashBagVisible)
		{
			if (_initialDirtObject != null)
			{
				_initialDirtObject.SetActive(initialDirtVisible);
			}
			if (_initialDirtCollider != null)
			{
				_initialDirtCollider.enabled = initialDirtVisible;
			}
			if (_placedTrashBagObject != null)
			{
				_placedTrashBagObject.SetActive(placedTrashBagVisible);
			}
			if (_placedTrashBagCollider != null)
			{
				_placedTrashBagCollider.enabled = placedTrashBagVisible;
			}
		}

		protected override void __initializeVariables()
		{
			if (_hasBeenHeld == null)
			{
				throw new Exception("HoldableDirt._hasBeenHeld cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_hasBeenHeld.Initialize(this);
			__nameNetworkVariable(_hasBeenHeld, "_hasBeenHeld");
			NetworkVariableFields.Add(_hasBeenHeld);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "HoldableDirt";
		}
	}
}
