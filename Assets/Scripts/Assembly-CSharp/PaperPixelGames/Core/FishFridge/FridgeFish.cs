using System;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Interaction.ToolTip;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Ocean.Creatures;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.FishFridge
{
	public class FridgeFish : BaseInteractable, ITooltipProvider
	{
		[SerializeField]
		private Fridge _fridge;

		[SerializeField]
		private GameObject _visualObject;

		[SerializeField]
		private FishSO _fishSO;

		[SerializeField]
		private bool _applyDeadMaterialAmount = true;

		private bool _isFridgeOpen;

		private bool _deadShaderStateApplied;

		private Collider _collider;

		private readonly NetworkVariable<bool> _hasStock = new NetworkVariable<bool>(value: false);

		private readonly NetworkVariable<int> _stockCount = new NetworkVariable<int>(0);

		private Coroutine _coroutine;

		public int StockCount => _stockCount.Value;

		public bool HasStock => _hasStock.Value;

		public FishSO FishSO => _fishSO;

		protected override void Awake()
		{
			base.Awake();
			TryGetComponent<Collider>(out _collider);
			ApplyDeadShaderStateLocal();
			RefreshVisual();
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			NetworkVariable<bool> hasStock = _hasStock;
			hasStock.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(hasStock.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnStockChanged));
			ApplyDeadShaderStateLocal();
			RefreshVisual();
		}

		public override void OnNetworkDespawn()
		{
			NetworkVariable<bool> hasStock = _hasStock;
			hasStock.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(hasStock.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(OnStockChanged));
			base.OnNetworkDespawn();
		}

		public void Initialize(Fridge fridge, FishSO fishSO)
		{
			_fridge = fridge;
			_fishSO = fishSO;
		}

		public override void EnableGazeHints()
		{
			if (_isFridgeOpen && _hasStock.Value)
			{
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_take");
				Singleton<ToolTipHandler>.Instance.OnGazeEnter(this, base.transform.position);
			}
		}

		public override void DisableGazeHints()
		{
			if (_isFridgeOpen && _hasStock.Value)
			{
				ForceHideTooltipAndHints();
			}
		}

		private void ForceHideTooltipAndHints()
		{
			Singleton<HintsManager>.Instance?.RemoveHint("LeftMouseMechanic");
			Singleton<ToolTipHandler>.Instance?.OnGazeExit(this);
		}

		public void SetStockStateServer(int stockCount)
		{
			if (base.IsServer)
			{
				_stockCount.Value = stockCount;
				_hasStock.Value = stockCount > 0;
				_collider.enabled = _hasStock.Value;
			}
		}

		public void RefreshStateLocal()
		{
			ApplyDeadShaderStateLocal();
			RefreshVisual();
		}

		public void SetFridgeOpenStateLocal(bool isOpen)
		{
			_isFridgeOpen = isOpen;
			RefreshVisual(!_isFridgeOpen);
		}

		private void OnStockChanged(bool previousValue, bool newValue)
		{
			RefreshVisual();
		}

		private void RefreshVisual(bool onClosing = false)
		{
			bool shouldShow = _hasStock.Value && _isFridgeOpen;
			ApplyDeadShaderStateLocal();
			DelayHelper instance = Singleton<DelayHelper>.Instance;
			if (_coroutine != null && instance != null)
			{
				instance.StopCoroutine(_coroutine);
			}
			_coroutine = null;
			Action action = () =>
			{
				if (_visualObject != null)
				{
					_visualObject.SetActive(shouldShow);
				}
				_coroutine = null;
			};
			if (instance != null)
			{
				_coroutine = instance.DelayAction((!onClosing) ? 0f : 0.15f, action);
			}
			else
			{
				action();
			}
			if (!shouldShow)
			{
				ForceHideTooltipAndHints();
			}
			if (!(_collider == null) || TryGetComponent<Collider>(out _collider))
			{
				_collider.enabled = shouldShow;
			}
		}

		private void ApplyDeadShaderStateLocal()
		{
			if (_applyDeadMaterialAmount && !_deadShaderStateApplied && !(_visualObject == null) && !(Singleton<SeaCreatureManager>.Instance == null))
			{
				Singleton<SeaCreatureManager>.Instance.ApplyDeadShaderState(_visualObject);
				_deadShaderStateApplied = true;
			}
		}

		protected override void ClientMouse1(Interactor interactor, InputActionPhase phase)
		{
			if (!_isFridgeOpen || phase != InputActionPhase.Started)
			{
				return;
			}
			if (_fridge == null)
			{
				UtilityMethods.Log("[FridgeFish] Fridge reference is missing.", LogType.Warning);
			}
			else if (_fishSO == null)
			{
				UtilityMethods.Log("[FridgeFish] FishSO reference is missing.", LogType.Warning);
			}
			else
			{
				if (!_hasStock.Value)
				{
					return;
				}
				if (!Singleton<QuickSlotHandler>.Instance.CanHoldSlot(_fishSO))
				{
					if (Singleton<NotifyManager>.Instance != null)
					{
						Singleton<NotifyManager>.Instance.ShowToast("Slots are full!", NotificationState.Error);
					}
				}
				else
				{
					_fridge.ServerRemoveStockRpc(new FixedString64Bytes(_fishSO.Id), interactor.NetworkObject.ToReference());
					_fridge.PlayExtractSFX();
				}
			}
		}

		protected override void ClientCrossMouse1(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			ClientMouse1(interactor, phase);
		}

		public TooltipData GetTooltip()
		{
			int num = 0;
			if (_fridge != null && _fishSO != null)
			{
				num = StockCount;
			}
			return new TooltipData
			{
				Title = ((_fishSO != null) ? _fishSO.LocalizedName : LocalizationDatabase.GetString("UiTable", "tooltip.unknown_fish", "Unknown Fish")),
				Body = LocalizationDatabase.GetFormattedString("UiTable", "tooltip.stock", "Stock: {0}", num)
			};
		}

		protected override void __initializeVariables()
		{
			if (_hasStock == null)
			{
				throw new Exception("FridgeFish._hasStock cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_hasStock.Initialize(this);
			__nameNetworkVariable(_hasStock, "_hasStock");
			NetworkVariableFields.Add(_hasStock);
			if (_stockCount == null)
			{
				throw new Exception("FridgeFish._stockCount cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_stockCount.Initialize(this);
			__nameNetworkVariable(_stockCount, "_stockCount");
			NetworkVariableFields.Add(_stockCount);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "FridgeFish";
		}
	}
}
