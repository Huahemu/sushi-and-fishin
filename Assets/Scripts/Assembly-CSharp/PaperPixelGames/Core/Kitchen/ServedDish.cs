using DG.Tweening;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.NetworkDevelopment;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Indicator;
using PaperPixelGames.Framework.Patterns;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PaperPixelGames.Core.Kitchen
{
	public class ServedDish : BaseHoldable, ISaveable<ServedDishData>
	{
		[Header("Scan Indicator")]
		[SerializeField]
		private OffscreenIndicatorVisualSettings _scanVisualSettingsOverride = new OffscreenIndicatorVisualSettings();

		private float _hoverScaleMultiplier = 1.04f;

		private float _hoverDuration = 0.18f;

		private Tween _hoverTween;

		private Vector3 _baseScale;

		private bool _hoverInitialized;

		private bool _isHovering;

		private bool _serverRegisteredServedDishStock;

		public virtual string RuntimeRecipeIdentityId
		{
			get
			{
				if (!(base.ItemSO != null))
				{
					return string.Empty;
				}
				return base.ItemSO.Id;
			}
		}

		public virtual int RuntimeDishPrice
		{
			get
			{
				if (!(base.ItemSO is ServedDishSO servedDishSO))
				{
					return 0;
				}
				return Mathf.Max(0, servedDishSO.Price);
			}
		}

		public virtual byte GetIndexByDish()
		{
			return 0;
		}

		public override bool TryGetScanIndicatorVisualSettings(out OffscreenIndicatorVisualSettings settings)
		{
			settings = ((_scanVisualSettingsOverride != null) ? _scanVisualSettingsOverride.Clone() : new OffscreenIndicatorVisualSettings());
			settings.IconSprite = ((base.ItemSO != null) ? base.ItemSO.Sprite : null);
			settings.ApplyDistanceVisible = true;
			settings.DistanceVisible = false;
			return true;
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			if (base.IsServer)
			{
				RestaurantManager restaurantManager = Singleton<GameProgressManager>.Instance.RestaurantManager;
				restaurantManager.RegisterServedDishData(this);
				ServerRegisterServedDishStock(restaurantManager);
			}
		}

		public override void OnNetworkDespawn()
		{
			if (base.IsServer)
			{
				RestaurantManager restaurantManager = Singleton<GameProgressManager>.Instance.RestaurantManager;
				ServerDeregisterServedDishStock(restaurantManager);
				restaurantManager.DeRegisterServedDishData(this);
			}
			base.OnNetworkDespawn();
		}

		private void ServerRegisterServedDishStock(RestaurantManager restaurantManager)
		{
			if (!_serverRegisteredServedDishStock)
			{
				string runtimeRecipeIdentityId = RuntimeRecipeIdentityId;
				if (!(restaurantManager == null) && base.ItemSO is ServedDishSO && !string.IsNullOrWhiteSpace(runtimeRecipeIdentityId))
				{
					restaurantManager.ServerAddStockRpc(runtimeRecipeIdentityId, 1);
					_serverRegisteredServedDishStock = true;
				}
			}
		}

		private void ServerDeregisterServedDishStock(RestaurantManager restaurantManager)
		{
			if (_serverRegisteredServedDishStock)
			{
				string runtimeRecipeIdentityId = RuntimeRecipeIdentityId;
				if (!(restaurantManager == null) && base.ItemSO is ServedDishSO && !string.IsNullOrWhiteSpace(runtimeRecipeIdentityId))
				{
					restaurantManager.ServerRemoveStockRpc(runtimeRecipeIdentityId, 1);
					_serverRegisteredServedDishStock = false;
				}
			}
		}

		protected void ServerRefreshRuntimeRecipeStockRegistration()
		{
			if (base.IsServer && !_serverRegisteredServedDishStock && Singleton<GameProgressManager>.Instance != null)
			{
				ServerRegisterServedDishStock(Singleton<GameProgressManager>.Instance.RestaurantManager);
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

		protected override void ClientCrossMouse1(Interactor interactor, BaseHoldable held, InputActionPhase phase)
		{
			if (phase == InputActionPhase.Started)
			{
				if (held is Tray tray)
				{
					tray.ServerTryPlaceWorldDishOnHeldTrayRpc(base.NetworkObject.ToReference());
				}
				else
				{
					base.ClientCrossMouse1(interactor, held, phase);
				}
			}
		}

		public override void EnableGazeHints()
		{
			if (!TryEnableTakeToTrayGazeHint())
			{
				base.EnableGazeHints();
			}
		}

		protected bool TryEnableTakeToTrayGazeHint()
		{
			if (!(Singleton<QuickSlotHandler>.Instance?.SelectedItem is Tray) || !(base.ItemSO is ServedDishSO) || Singleton<HintsManager>.Instance == null)
			{
				return false;
			}
			Singleton<HintsManager>.Instance.ClearAllHints();
			Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_take_item_to_tray");
			return true;
		}

		public void ServerRaiseTakenFromDeskEventIfNeeded()
		{
			if (base.IsServer && ShouldRaiseTakenFromDeskEvent())
			{
				EventBus<ServedDishTakenFromDeskEvent>.Raise(new ServedDishTakenFromDeskEvent
				{
					DishNetworkObject = base.NetworkObject
				});
			}
		}

		public override void ServerHold(Interactor interactor)
		{
			base.ServerHold(interactor);
			ServerRaiseTakenFromDeskEventIfNeeded();
		}

		public override bool ServerTryStaffPickup(int employeeId)
		{
			if (!base.ServerTryStaffPickup(employeeId))
			{
				return false;
			}
			ServerRaiseTakenFromDeskEventIfNeeded();
			return true;
		}

		protected virtual bool ShouldRaiseTakenFromDeskEvent()
		{
			return true;
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

		public ServedDishData Save()
		{
			if (base.IsStaffCarried)
			{
				return null;
			}
			ServedDishData servedDishData = new ServedDishData
			{
				Id = base.ItemSO.Id,
				TransformData = GetTransformData()
			};
			Tray componentInParent = base.transform.GetComponentInParent<Tray>();
			RestaurantManager restaurantManager = ((Singleton<GameProgressManager>.Instance != null) ? Singleton<GameProgressManager>.Instance.RestaurantManager : null);
			int slotIndex;
			if (componentInParent != null && restaurantManager != null && restaurantManager.TryGetTraySaveLocation(componentInParent, out var trolleyIndex, out var traySlotIndex))
			{
				servedDishData.HasTrayAttachment = true;
				servedDishData.TrayTrolleyIndex = trolleyIndex;
				servedDishData.TraySlotIndex = traySlotIndex;
				servedDishData.DishSlotIndex = GetIndexByDish();
			}
			else if (restaurantManager != null && restaurantManager.TryGetConveyorSaveSlot(this, out slotIndex))
			{
				servedDishData.HasConveyorAttachment = true;
				servedDishData.ConveyorSlotIndex = slotIndex;
			}
			PopulateServedDishSaveData(servedDishData);
			return servedDishData;
		}

		protected virtual void PopulateServedDishSaveData(ServedDishData data)
		{
		}

		public void Load(ServedDishData data)
		{
		}

		public TransformData GetTransformData()
		{
			return new TransformData
			{
				Position = base.transform.position,
				Rotation = base.transform.rotation.eulerAngles
			};
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
			return "ServedDish";
		}
	}
}
