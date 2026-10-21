using System;
using DG.Tweening;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Interaction.ToolTip;
using PaperPixelGames.Core.Kitchen.ChefDesk;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.ProcessedIngredient;
using PaperPixelGames.Framework.Patterns;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Kitchen
{
	public class Seasoning : IngredientEntity
	{
		private byte _initialIngredientCount = 20;

		[Header("Decals")]
		[SerializeField]
		private GameObject _decalPrefab;

		private readonly NetworkVariable<bool> _isOnChefDeskRack = new NetworkVariable<bool>(value: false);

		private float _hoverScaleMultiplier = 1.04f;

		private float _hoverDuration = 0.18f;

		private Tween _hoverTween;

		private Vector3 _baseScale;

		private bool _hoverInitialized;

		private bool _isHovering;

		private Renderer[] _visibilityRenderers;

		private Collider[] _visibilityColliders;

		private bool[] _rendererEnabledStates;

		private bool[] _colliderEnabledStates;

		private bool _visibilityCached;

		private bool _serverPlacingOnChefDeskRack;

		public PaperPixelGames.Core.Kitchen.ChefDesk.ChefDesk CurrentRack { get; private set; }

		public int CurrentRackSlotIndex { get; private set; } = -1;

		public GameObject GraphicObjectPrefab => base.ItemSO.GraphicObjectPrefab;

		public GameObject DecalPrefab => _decalPrefab;

		public byte InitialIngredientCount => _initialIngredientCount;

		public bool IsOnChefDeskRack => _isOnChefDeskRack.Value;

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<bool> isOnChefDeskRack = _isOnChefDeskRack;
			isOnChefDeskRack.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(isOnChefDeskRack.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnRackStateChanged));
		}

		public override void OnNetworkDespawn()
		{
			NetworkVariable<bool> isOnChefDeskRack = _isOnChefDeskRack;
			isOnChefDeskRack.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(isOnChefDeskRack.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnRackStateChanged));
			base.OnNetworkDespawn();
		}

		public void ServerSetRackSlot(PaperPixelGames.Core.Kitchen.ChefDesk.ChefDesk rack, int slotIndex)
		{
			CurrentRack = rack;
			CurrentRackSlotIndex = slotIndex;
			_isOnChefDeskRack.Value = rack != null && slotIndex >= 0;
		}

		public void ServerClearRackSlot()
		{
			CurrentRack = null;
			CurrentRackSlotIndex = -1;
			_isOnChefDeskRack.Value = false;
		}

		public bool ServerPlaceOnChefDeskRack(PaperPixelGames.Core.Kitchen.ChefDesk.ChefDesk rack, int slotIndex, Vector3 position, Vector3 rotation)
		{
			if (!base.IsServer || rack == null || slotIndex < 0)
			{
				return false;
			}
			try
			{
				_serverPlacingOnChefDeskRack = true;
				ServerSetRackSlot(rack, slotIndex);
				base.ServerPlace(position, rotation, forget: false);
			}
			finally
			{
				_serverPlacingOnChefDeskRack = false;
			}
			return true;
		}

		public override void ServerHold(Interactor interactor)
		{
			if (!(CurrentRack != null) || CurrentRackSlotIndex < 0)
			{
				base.ServerHold(interactor);
			}
		}

		public override void EnableGazeHints()
		{
			if (!(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.RemoveHint("Interact");
				Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
				Singleton<HintsManager>.Instance.RemoveHint("RightMouseMechanic");
				Singleton<HintsManager>.Instance.AddHint(IsOnChefDeskRack ? "RightMouseMechanic" : "LeftMouseMechanic", "key_hold");
			}
		}

		public override void DisableGazeHints()
		{
			if (!(Singleton<HintsManager>.Instance == null))
			{
				Singleton<HintsManager>.Instance.RemoveHint("Interact");
				Singleton<HintsManager>.Instance.RemoveHint("LeftMouseMechanic");
				Singleton<HintsManager>.Instance.RemoveHint("RightMouseMechanic");
			}
		}

		protected override void ClientInteract(Interactor interactor, InputActionPhase phase)
		{
		}

		protected override void ClientMouse2(Interactor interactor, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started && IsOnChefDeskRack && !(interactor == null))
			{
				ServerHoldFromRackRpc(interactor.NetworkObject.ToReference());
			}
		}

		protected override bool CanCrossHold(Interactor interactor, BaseHoldable held)
		{
			return !IsOnChefDeskRack;
		}

		[Rpc(SendTo.Server)]
		private void ServerHoldFromRackRpc(NetworkObjectReference interactorRef)
		{
			NetworkManager networkManager = base.NetworkManager;
			if ((object)networkManager == null || !networkManager.IsListening)
			{
				Debug.LogError("Rpc methods can only be invoked after starting the NetworkManager!");
				return;
			}
			if (__rpc_exec_stage != __RpcExecStage.Execute)
			{
				RpcAttribute.RpcAttributeParams attributeParams = default;
				RpcParams rpcParams = default;
				FastBufferWriter bufferWriter = __beginSendRpc(877262494u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
				bufferWriter.WriteValueSafe(in interactorRef, default(FastBufferWriter.ForNetworkSerializable));
				__endSendRpc(ref bufferWriter, 877262494u, rpcParams, attributeParams, SendTo.Server, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (interactorRef.TryGetComponent<Interactor>(out var component) && !(CurrentRack == null) && CurrentRackSlotIndex >= 0)
				{
					CurrentRack.ClearSlot(CurrentRackSlotIndex, this);
					ServerClearRackSlot();
					base.ServerHold(component);
				}
			}
		}

		protected override void ServerPlace(Vector3 pos, Vector3 rot, bool forget)
		{
			if (!_serverPlacingOnChefDeskRack)
			{
				ServerDetachFromRackIfNeeded();
			}
			base.ServerPlace(pos, rot, forget);
		}

		protected override void ServerDrop(Vector3 pos, Quaternion rot)
		{
			ServerDetachFromRackIfNeeded();
			base.ServerDrop(pos, rot);
		}

		private void ServerDetachFromRackIfNeeded()
		{
			if (base.IsServer && !(CurrentRack == null) && CurrentRackSlotIndex >= 0)
			{
				CurrentRack.ClearSlot(CurrentRackSlotIndex, this);
				ServerClearRackSlot();
			}
		}

		private void OnRackStateChanged(bool previousValue, bool currentValue)
		{
			if (previousValue != currentValue)
			{
				RefreshGazeFeedback();
			}
		}

		private void EnsureHoverInit()
		{
			if (!_hoverInitialized)
			{
				_baseScale = base.transform.localScale;
				_hoverInitialized = true;
			}
		}

		public void ClientSetHoverVisual(bool isHovering)
		{
			EnsureHoverInit();
			if (_isHovering != isHovering)
			{
				_isHovering = isHovering;
				_hoverTween?.Kill();
				if (_isHovering)
				{
					_hoverTween = base.transform.DOScale(_baseScale * _hoverScaleMultiplier, _hoverDuration).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
						.SetLink(base.gameObject);
				}
				else
				{
					_hoverTween = base.transform.DOScale(_baseScale, _hoverDuration).SetEase(Ease.OutSine).SetLink(base.gameObject);
				}
			}
		}

		private void OnDisable()
		{
			_hoverTween?.Kill();
			if (_hoverInitialized)
			{
				base.transform.localScale = _baseScale;
			}
			_isHovering = false;
		}

		public override void ClientSetVisibility(bool visible)
		{
			base.ClientSetVisibility(visible);
			SetWorldObjectVisibility(visible);
		}

		public override void ServerSetVisibility(bool visible)
		{
			base.ServerSetVisibility(visible);
			SetWorldObjectVisibility(visible);
		}

		private void SetWorldObjectVisibility(bool visible)
		{
			EnsureVisibilityCache();
			for (int i = 0; i < _visibilityRenderers.Length; i++)
			{
				Renderer renderer = _visibilityRenderers[i];
				if (!(renderer == null))
				{
					renderer.enabled = visible && _rendererEnabledStates[i];
				}
			}
			for (int j = 0; j < _visibilityColliders.Length; j++)
			{
				Collider collider = _visibilityColliders[j];
				if (!(collider == null))
				{
					collider.enabled = visible && _colliderEnabledStates[j];
				}
			}
		}

		private void EnsureVisibilityCache()
		{
			if (!_visibilityCached)
			{
				_visibilityRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
				_visibilityColliders = GetComponentsInChildren<Collider>(includeInactive: true);
				_rendererEnabledStates = new bool[_visibilityRenderers.Length];
				for (int i = 0; i < _visibilityRenderers.Length; i++)
				{
					_rendererEnabledStates[i] = _visibilityRenderers[i] != null && _visibilityRenderers[i].enabled;
				}
				_colliderEnabledStates = new bool[_visibilityColliders.Length];
				for (int j = 0; j < _visibilityColliders.Length; j++)
				{
					_colliderEnabledStates[j] = _visibilityColliders[j] != null && _visibilityColliders[j].enabled;
				}
				_visibilityCached = true;
			}
		}

		public bool HasAnyLeft()
		{
			return base.IngredientCount > 0;
		}

		protected override bool TryResolveInitialIngredientCount(out byte initialCount)
		{
			initialCount = _initialIngredientCount;
			return true;
		}

		public bool ServerTryConsumeOne()
		{
			if (!base.IsServer)
			{
				return false;
			}
			if (base.IngredientCount <= 0)
			{
				return false;
			}
			base.IngredientCount--;
			return true;
		}

		public override TooltipData GetTooltip()
		{
			TooltipData data = base.ItemSO.Tooltip.Data;
			data.Title = base.ItemSO.LocalizedName;
			data.Body = $"{base.IngredientCount}";
			return data;
		}

		protected override void __initializeVariables()
		{
			if (_isOnChefDeskRack == null)
			{
				throw new Exception("Seasoning._isOnChefDeskRack cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_isOnChefDeskRack.Initialize(this);
			__nameNetworkVariable(_isOnChefDeskRack, "_isOnChefDeskRack");
			NetworkVariableFields.Add(_isOnChefDeskRack);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(877262494u, __rpc_handler_877262494, "ServerHoldFromRackRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_877262494(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out NetworkObjectReference value, default(FastBufferWriter.ForNetworkSerializable));
				((Seasoning)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Seasoning)target).ServerHoldFromRackRpc(value);
				((Seasoning)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "Seasoning";
		}
	}
}
