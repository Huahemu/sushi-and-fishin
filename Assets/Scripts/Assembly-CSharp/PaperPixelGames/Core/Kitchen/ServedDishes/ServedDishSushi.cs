using System;
using System.Collections.Generic;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Interaction.ToolTip;
using PaperPixelGames.Core.Kitchen.ChefDesk;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player.QuickSlot;
using PaperPixelGames.Core.Restaurant;
using PaperPixelGames.Core.SaveSystem;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Indicator;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen.ServedDishes
{
	public class ServedDishSushi : ServedDish
	{
		private const string PlayerLayerName = "Player";

		private const string ViewModelLayerName = "ViewModel";

		private Vector3 _localPositionOffset = Vector3.zero;

		private Vector3 _localEulerOffset = Vector3.zero;

		[Header("Conveyor Scale")]
		[SerializeField]
		private Vector3 _defaultRootLocalScale = Vector3.one;

		[SerializeField]
		private Vector3 _conveyorRootLocalScale = new Vector3(0.9f, 0.9f, 0.9f);

		private PlateGraphicHandler _plateGraphicHandler;

		private readonly List<SeasoningDecalData> _seasoningDecals = new List<SeasoningDecalData>();

		private NetworkVariable<bool> _useConveyorPlateGraphic = new NetworkVariable<bool>(value: false);

		private NetworkVariable<sbyte> _assignedEdgeIndex = new NetworkVariable<sbyte>(-1);

		private NetworkVariable<NetworkObjectReference> _chefDeskReference = new NetworkVariable<NetworkObjectReference>();

		private NetworkVariable<bool> _hasCustomSushiMetadata = new NetworkVariable<bool>(value: false);

		private NetworkVariable<byte> _customRecipeIdentityKind = new NetworkVariable<byte>(2);

		private NetworkVariable<FixedString64Bytes> _customRecipeId = new NetworkVariable<FixedString64Bytes>();

		private NetworkVariable<FixedString128Bytes> _customDisplayName = new NetworkVariable<FixedString128Bytes>();

		private NetworkVariable<int> _customPrice = new NetworkVariable<int>(0);

		private NetworkVariable<SushiCompositionNetworkData> _customComposition = new NetworkVariable<SushiCompositionNetworkData>();

		[Header("Custom Sushi Presentation")]
		[SerializeField]
		private GameObject _customCutPiecePrefab;

		[SerializeField]
		private CustomSushiIngredientVisuals _customIngredientVisuals;

		private CustomSushiIngredientVisuals[] _customIngredientVisualsInChildren;

		private Coroutine _parentVisibilityCoroutine;

		private readonly List<Collider> _forceDisabledColliders = new List<Collider>();

		private bool _realObjectRuntimeVisible = true;

		public bool IsConveryorBelt { get; set; }

		public sbyte AssignedEdgeIndex => _assignedEdgeIndex.Value;

		public IReadOnlyList<SeasoningDecalData> SeasoningDecals => _seasoningDecals;

		public PlateColor PlateColor => _plateGraphicHandler.Color;

		public bool HasCustomSushiMetadata => _hasCustomSushiMetadata.Value;

		public bool IsUnsavedCustomSushi
		{
			get
			{
				if (HasCustomSushiMetadata)
				{
					return _customRecipeIdentityKind.Value == 2;
				}
				return false;
			}
		}

		public SushiRecipeIdentityKind CustomRecipeIdentityKind => (SushiRecipeIdentityKind)_customRecipeIdentityKind.Value;

		public string CustomRecipeId => _customRecipeId.Value.ToString();

		public string CustomDisplayName => _customDisplayName.Value.ToString();

		public int CustomPrice => Mathf.Max(0, _customPrice.Value);

		public SushiCompositionData CustomComposition => _customComposition.Value.ToData();

		public GameObject CustomCutPiecePrefab => _customCutPiecePrefab;

		public override string QuickSlotDisplayName
		{
			get
			{
				if (!HasCustomSushiMetadata)
				{
					return base.QuickSlotDisplayName;
				}
				if (!string.IsNullOrWhiteSpace(CustomDisplayName))
				{
					return CustomDisplayName;
				}
				return "Custom Sushi";
			}
		}

		public override Sprite QuickSlotDisplaySprite
		{
			get
			{
				if (!HasCustomSushiMetadata)
				{
					return base.QuickSlotDisplaySprite;
				}
				Sprite icon = CustomSushiThumbnailStore.GetIcon(CustomRecipeId);
				if (icon != null)
				{
					return icon;
				}
				if (CustomRecipeIdentityKind == SushiRecipeIdentityKind.BuiltIn)
				{
					return (Singleton<GameProgressManager>.Instance?.ItemDatabaseSO?.FindById(CustomRecipeId) as SushiSO)?.Sprite;
				}
				if (CustomRecipeIdentityKind == SushiRecipeIdentityKind.UnsavedCustom)
				{
					return null;
				}
				return Singleton<GameProgressManager>.Instance?.RestaurantManager?.ResolveCustomSushiPlaceholderIcon(CustomComposition.SushiType);
			}
		}

		public override string QuickSlotStackId
		{
			get
			{
				if (!HasCustomSushiMetadata)
				{
					return base.QuickSlotStackId;
				}
				if (string.IsNullOrEmpty(CustomRecipeId))
				{
					return $"CustomSushi:{base.NetworkObjectId}";
				}
				return CustomRecipeId;
			}
		}

		public override string RuntimeRecipeIdentityId
		{
			get
			{
				if (!HasCustomSushiMetadata)
				{
					return base.RuntimeRecipeIdentityId;
				}
				return CustomRecipeId;
			}
		}

		public override int RuntimeDishPrice
		{
			get
			{
				if (!HasCustomSushiMetadata)
				{
					return base.RuntimeDishPrice;
				}
				return CustomPrice;
			}
		}

		public event Action ClientCustomSushiMetadataChanged;

		public override bool TryGetScanIndicatorVisualSettings(out OffscreenIndicatorVisualSettings settings)
		{
			bool num = base.TryGetScanIndicatorVisualSettings(out settings);
			if (num && HasCustomSushiMetadata)
			{
				settings.IconSprite = QuickSlotDisplaySprite;
			}
			return num;
		}

		public override void OnNetworkSpawn()
		{
			base.OnNetworkSpawn();
			CachePlateGraphicHandler();
			NetworkVariable<bool> useConveyorPlateGraphic = _useConveyorPlateGraphic;
			useConveyorPlateGraphic.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(useConveyorPlateGraphic.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(HandleConveyorPlateGraphicChanged));
			NetworkVariable<bool> hasCustomSushiMetadata = _hasCustomSushiMetadata;
			hasCustomSushiMetadata.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Combine(hasCustomSushiMetadata.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(HandleCustomMetadataChanged));
			NetworkVariable<byte> customRecipeIdentityKind = _customRecipeIdentityKind;
			customRecipeIdentityKind.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Combine(customRecipeIdentityKind.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(HandleCustomMetadataChanged));
			NetworkVariable<FixedString64Bytes> customRecipeId = _customRecipeId;
			customRecipeId.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Combine(customRecipeId.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(HandleCustomMetadataChanged));
			NetworkVariable<FixedString128Bytes> customDisplayName = _customDisplayName;
			customDisplayName.OnValueChanged = (NetworkVariable<FixedString128Bytes>.OnValueChangedDelegate)Delegate.Combine(customDisplayName.OnValueChanged, new NetworkVariable<FixedString128Bytes>.OnValueChangedDelegate(HandleCustomMetadataChanged));
			NetworkVariable<int> customPrice = _customPrice;
			customPrice.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Combine(customPrice.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(HandleCustomMetadataChanged));
			NetworkVariable<SushiCompositionNetworkData> customComposition = _customComposition;
			customComposition.OnValueChanged = (NetworkVariable<SushiCompositionNetworkData>.OnValueChangedDelegate)Delegate.Combine(customComposition.OnValueChanged, new NetworkVariable<SushiCompositionNetworkData>.OnValueChangedDelegate(HandleCustomMetadataChanged));
			CustomSushiThumbnailStore.IconChanged += HandleCustomThumbnailChanged;
			ApplyConveyorPlateGraphic(_useConveyorPlateGraphic.Value);
			ApplyCustomIngredientVisuals();
			RefreshCustomQuickSlotDisplay();
			ClientCustomSushiMetadataChanged?.Invoke();
		}

		public override void OnNetworkDespawn()
		{
			StopParentVisibilityCoroutine();
			NetworkVariable<bool> useConveyorPlateGraphic = _useConveyorPlateGraphic;
			useConveyorPlateGraphic.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(useConveyorPlateGraphic.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(HandleConveyorPlateGraphicChanged));
			NetworkVariable<bool> hasCustomSushiMetadata = _hasCustomSushiMetadata;
			hasCustomSushiMetadata.OnValueChanged = (NetworkVariable<bool>.OnValueChangedDelegate)Delegate.Remove(hasCustomSushiMetadata.OnValueChanged, new NetworkVariable<bool>.OnValueChangedDelegate(HandleCustomMetadataChanged));
			NetworkVariable<byte> customRecipeIdentityKind = _customRecipeIdentityKind;
			customRecipeIdentityKind.OnValueChanged = (NetworkVariable<byte>.OnValueChangedDelegate)Delegate.Remove(customRecipeIdentityKind.OnValueChanged, new NetworkVariable<byte>.OnValueChangedDelegate(HandleCustomMetadataChanged));
			NetworkVariable<FixedString64Bytes> customRecipeId = _customRecipeId;
			customRecipeId.OnValueChanged = (NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate)Delegate.Remove(customRecipeId.OnValueChanged, new NetworkVariable<FixedString64Bytes>.OnValueChangedDelegate(HandleCustomMetadataChanged));
			NetworkVariable<FixedString128Bytes> customDisplayName = _customDisplayName;
			customDisplayName.OnValueChanged = (NetworkVariable<FixedString128Bytes>.OnValueChangedDelegate)Delegate.Remove(customDisplayName.OnValueChanged, new NetworkVariable<FixedString128Bytes>.OnValueChangedDelegate(HandleCustomMetadataChanged));
			NetworkVariable<int> customPrice = _customPrice;
			customPrice.OnValueChanged = (NetworkVariable<int>.OnValueChangedDelegate)Delegate.Remove(customPrice.OnValueChanged, new NetworkVariable<int>.OnValueChangedDelegate(HandleCustomMetadataChanged));
			NetworkVariable<SushiCompositionNetworkData> customComposition = _customComposition;
			customComposition.OnValueChanged = (NetworkVariable<SushiCompositionNetworkData>.OnValueChangedDelegate)Delegate.Remove(customComposition.OnValueChanged, new NetworkVariable<SushiCompositionNetworkData>.OnValueChangedDelegate(HandleCustomMetadataChanged));
			CustomSushiThumbnailStore.IconChanged -= HandleCustomThumbnailChanged;
			base.OnNetworkDespawn();
		}

		public override void ClientRefreshRuntimeRepresentationState()
		{
			StopParentVisibilityCoroutine();
			RefreshRealObjectVisibilityByRuntimeState();
		}

		private void HandleConveyorPlateGraphicChanged(bool previousValue, bool newValue)
		{
			ApplyConveyorPlateGraphic(newValue);
		}

		private PlateGraphicHandler CachePlateGraphicHandler()
		{
			if (_plateGraphicHandler == null)
			{
				_plateGraphicHandler = GetComponentInChildren<PlateGraphicHandler>(includeInactive: true);
			}
			return _plateGraphicHandler;
		}

		private void ApplyConveyorPlateGraphic(bool useConveyorPlate)
		{
			ApplyRootScaleByConveyorMode(useConveyorPlate);
			PlateGraphicHandler plateGraphicHandler = CachePlateGraphicHandler();
			if (plateGraphicHandler != null)
			{
				plateGraphicHandler.ApplyMode(useConveyorPlate);
			}
		}

		private void HandleCustomMetadataChanged<T>(T previousValue, T newValue)
		{
			ApplyCustomIngredientVisuals();
			RefreshCustomQuickSlotDisplay();
			ClientCustomSushiMetadataChanged?.Invoke();
		}

		private void HandleCustomThumbnailChanged(string recipeId)
		{
			if (HasCustomSushiMetadata && CustomRecipeId == recipeId)
			{
				RefreshCustomQuickSlotDisplay();
			}
		}

		private void RefreshCustomQuickSlotDisplay()
		{
			if (HasCustomSushiMetadata)
			{
				Singleton<QuickSlotHandler>.Instance?.RefreshDisplayForItem(this);
			}
		}

		private void ApplyCustomIngredientVisuals()
		{
			if (_customIngredientVisualsInChildren == null)
			{
				_customIngredientVisualsInChildren = GetComponentsInChildren<CustomSushiIngredientVisuals>(includeInactive: true);
			}
			bool hasCustomSushiMetadata = HasCustomSushiMetadata;
			SushiCompositionData composition = (hasCustomSushiMetadata ? CustomComposition : null);
			bool flag = false;
			CustomSushiIngredientVisuals[] customIngredientVisualsInChildren = _customIngredientVisualsInChildren;
			foreach (CustomSushiIngredientVisuals customSushiIngredientVisuals in customIngredientVisualsInChildren)
			{
				if (!(customSushiIngredientVisuals == null))
				{
					customSushiIngredientVisuals.Apply(composition, hasCustomSushiMetadata);
					flag |= customSushiIngredientVisuals == _customIngredientVisuals;
				}
			}
			if (!flag)
			{
				_customIngredientVisuals?.Apply(composition, hasCustomSushiMetadata);
			}
		}

		private void ApplyRootScaleByConveyorMode(bool useConveyorPlate)
		{
			base.transform.localScale = (useConveyorPlate ? _conveyorRootLocalScale : _defaultRootLocalScale);
		}

		private void ApplyOriginalPlateGraphicToShadow(GameObject shadowRootObject)
		{
			if (!(shadowRootObject == null))
			{
				PlateGraphicHandler componentInChildren = shadowRootObject.GetComponentInChildren<PlateGraphicHandler>(includeInactive: true);
				if (!(componentInChildren == null))
				{
					componentInChildren.ApplyOriginalPlate();
				}
			}
		}

		public void ServerEnterConveyorPlateMode()
		{
			if (base.IsServer)
			{
				IsConveryorBelt = true;
				ServerSetConveyorPlateGraphic(useConveyorPlate: true);
			}
		}

		public void ServerExitConveyorPlateMode(bool plateGraphic)
		{
			if (base.IsServer)
			{
				IsConveryorBelt = false;
				ServerSetConveyorPlateGraphic(plateGraphic);
			}
		}

		private void ServerSetConveyorPlateGraphic(bool useConveyorPlate)
		{
			if (base.IsServer)
			{
				if (_useConveyorPlateGraphic.Value != useConveyorPlate)
				{
					_useConveyorPlateGraphic.Value = useConveyorPlate;
				}
				ApplyConveyorPlateGraphic(useConveyorPlate);
			}
		}

		public override TooltipData GetTooltip()
		{
			TooltipData data = _itemSO.Tooltip.Data;
			data.Title = ((!HasCustomSushiMetadata) ? _itemSO.LocalizedName : (string.IsNullOrWhiteSpace(CustomDisplayName) ? "Custom Sushi" : CustomDisplayName));
			return data;
		}

		public bool ServerInitializeCustomSushi(SushiCompositionData composition, SushiRecipeIdentityKind identityKind, string recipeId, string displayName, int price)
		{
			if (base.NetworkManager == null || !base.NetworkManager.IsServer || !CustomSushiRules.IsSupportedType(composition?.SushiType ?? SushiType.Empty))
			{
				return false;
			}
			SushiCompositionNetworkData value = default;
			if (!value.TrySet(composition))
			{
				return false;
			}
			_customComposition.Value = value;
			_customRecipeIdentityKind.Value = (byte)identityKind;
			_customRecipeId.Value = recipeId ?? string.Empty;
			_customDisplayName.Value = displayName ?? string.Empty;
			_customPrice.Value = Mathf.Max(0, price);
			_hasCustomSushiMetadata.Value = true;
			ApplyCustomIngredientVisuals();
			return true;
		}

		public bool ServerAdoptSavedCustomRecipe(CustomSushiRecipeData recipe)
		{
			if (!base.IsServer || !HasCustomSushiMetadata || recipe == null || !SushiCompositionComparer.Equals(CustomComposition, recipe.Composition))
			{
				return false;
			}
			_customRecipeIdentityKind.Value = 1;
			_customRecipeId.Value = recipe.RecipeId;
			_customDisplayName.Value = recipe.DisplayName;
			_customPrice.Value = Mathf.Max(0, recipe.Price);
			ServerRefreshRuntimeRecipeStockRegistration();
			return true;
		}

		public bool ServerAdoptRecipeIdentity(SushiRecipeDefinition recipe)
		{
			if (!base.IsServer || !HasCustomSushiMetadata || recipe == null || !SushiCompositionComparer.Equals(CustomComposition, recipe.Composition))
			{
				return false;
			}
			_customRecipeIdentityKind.Value = (byte)recipe.IdentityKind;
			_customRecipeId.Value = recipe.RecipeId ?? string.Empty;
			_customDisplayName.Value = recipe.DisplayName ?? string.Empty;
			_customPrice.Value = Mathf.Max(0, recipe.Price);
			ServerRefreshRuntimeRecipeStockRegistration();
			return true;
		}

		public CustomSushiPhysicalData CreateCustomSushiSaveData()
		{
			if (!HasCustomSushiMetadata)
			{
				return null;
			}
			return new CustomSushiPhysicalData
			{
				IdentityKind = CustomRecipeIdentityKind,
				RecipeId = CustomRecipeId,
				DisplayName = CustomDisplayName,
				Composition = CustomComposition,
				Price = CustomPrice
			};
		}

		protected override void PopulateServedDishSaveData(ServedDishData data)
		{
			base.PopulateServedDishSaveData(data);
			data.CustomSushi = CreateCustomSushiSaveData();
		}

		public override byte GetIndexByDish()
		{
			return 0;
		}

		public override void OnNetworkObjectParentChanged(NetworkObject parentNetworkObject)
		{
			base.OnNetworkObjectParentChanged(parentNetworkObject);
			StopParentVisibilityCoroutine();
			if (!(parentNetworkObject == null))
			{
				if (parentNetworkObject.TryGetComponent<ConveyorBeltTable>(out var component) && component.TryApplyServedDishAnchorPose(base.transform))
				{
					ScheduleParentVisibilityRefresh();
					return;
				}
				if (parentNetworkObject.TryGetComponent<Tray>(out var _))
				{
					ScheduleParentVisibilityRefresh();
					return;
				}
				base.transform.localPosition = _localPositionOffset;
				base.transform.localRotation = Quaternion.Euler(_localEulerOffset);
				ScheduleParentVisibilityRefresh();
			}
		}

		private void ScheduleParentVisibilityRefresh()
		{
			_parentVisibilityCoroutine = Singleton<DelayHelper>.Instance.DelayAction(0.2f, () =>
			{
				_parentVisibilityCoroutine = null;
				RefreshRealObjectVisibilityByRuntimeState();
			});
		}

		public void SetAssignedEdgeIndexServer(sbyte index)
		{
			_assignedEdgeIndex.Value = index;
		}

		public void ServerAssignChefDesk(PaperPixelGames.Core.Kitchen.ChefDesk.ChefDesk chefDesk)
		{
			if (base.IsServer)
			{
				_chefDeskReference.Value = ((chefDesk != null) ? new NetworkObjectReference(chefDesk.NetworkObject) : default(NetworkObjectReference));
			}
		}

		public bool ClientBelongsToChefDesk(PaperPixelGames.Core.Kitchen.ChefDesk.ChefDesk chefDesk)
		{
			if (chefDesk != null && _chefDeskReference.Value.TryGet(out var networkObject))
			{
				return networkObject == chefDesk.NetworkObject;
			}
			return false;
		}

		public override void ServerHold(Interactor interactor)
		{
			bool isConveryorBelt = IsConveryorBelt;
			if (isConveryorBelt)
			{
				ServerSetConveyorPlateGraphic(useConveyorPlate: false);
			}
			base.ServerHold(interactor);
			if (isConveryorBelt)
			{
				IsConveryorBelt = false;
				EventBus<ConveryorBeltTakeItemEvent>.Raise(new ConveryorBeltTakeItemEvent
				{
					SlotTransform = base.transform
				});
			}
			else if (_assignedEdgeIndex.Value != -2)
			{
				_assignedEdgeIndex.Value = -2;
			}
		}

		protected override bool ShouldRaiseTakenFromDeskEvent()
		{
			return !IsConveryorBelt;
		}

		public void AddSeasoningDecalData(string seasoningItemId, Vector3 localPosition, Vector3 localEulerAngles)
		{
			_seasoningDecals.Add(new SeasoningDecalData
			{
				SeasoningItemId = seasoningItemId,
				LocalPosition = localPosition,
				LocalEulerAngles = localEulerAngles
			});
		}

		protected override void SpawnFpsShadow(Transform parent)
		{
			base.SpawnFpsShadow(parent);
			ApplyOriginalPlateGraphicToShadow(_fpsShadowObject);
			RebuildSeasoningVisualsOnShadow(_fpsShadowObject, "ViewModel");
		}

		protected override void SpawnTpsShadow(Transform parent, bool isOwner)
		{
			base.SpawnTpsShadow(parent, isOwner);
			ApplyOriginalPlateGraphicToShadow(_tpsShadowObject);
			RebuildSeasoningVisualsOnShadow(_tpsShadowObject, "Player");
		}

		private void RebuildSeasoningVisualsOnShadow(GameObject shadowRootObject, string targetLayerName)
		{
			if (shadowRootObject == null)
			{
				return;
			}
			int num = LayerMask.NameToLayer(targetLayerName);
			if (num == -1)
			{
				UtilityMethods.Log("<b><color=#FF5A5A>[ServedDishSushi]</color></b> Layer not found: " + targetLayerName);
				return;
			}
			foreach (SeasoningDecalData seasoningDecal in _seasoningDecals)
			{
				Seasoning seasoning = FindSeasoningByItemId(seasoningDecal.SeasoningItemId);
				if (!(seasoning == null) && !(seasoning.DecalPrefab == null))
				{
					GameObject obj = UnityEngine.Object.Instantiate(seasoning.DecalPrefab, shadowRootObject.transform);
					obj.transform.localPosition = seasoningDecal.LocalPosition;
					obj.transform.localRotation = Quaternion.Euler(seasoningDecal.LocalEulerAngles);
					SetLayerRecursively(obj, num);
				}
			}
		}

		private static void SetLayerRecursively(GameObject rootObject, int targetLayer)
		{
			if (!(rootObject == null))
			{
				rootObject.layer = targetLayer;
				Transform transform = rootObject.transform;
				for (int i = 0; i < transform.childCount; i++)
				{
					SetLayerRecursively(transform.GetChild(i).gameObject, targetLayer);
				}
			}
		}

		private Seasoning FindSeasoningByItemId(string itemId)
		{
			Seasoning[] array = UnityEngine.Object.FindObjectsByType<Seasoning>(FindObjectsSortMode.None);
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i].ItemSO != null && array[i].ItemSO.Id == itemId)
				{
					return array[i];
				}
			}
			return null;
		}

		public override void EnableGazeHints()
		{
			if (!TryEnableTakeToTrayGazeHint())
			{
				Singleton<HintsManager>.Instance.ClearAllHints();
				Singleton<HintsManager>.Instance.AddHint("LeftMouseMechanic", "key_hold");
			}
		}

		public override GameObject CreatePlacementPreview(Transform parent)
		{
			return InstantiateCustomSushiGraphic(parent) ?? base.CreatePlacementPreview(parent);
		}

		protected override GameObject InstantiateShadowGraphic(Transform parent)
		{
			return InstantiateCustomSushiGraphic(parent) ?? base.InstantiateShadowGraphic(parent);
		}

		private GameObject InstantiateCustomSushiGraphic(Transform parent)
		{
			if (!HasCustomSushiMetadata || parent == null)
			{
				return null;
			}
			PlateGraphicHandler plateGraphicHandler = CachePlateGraphicHandler();
			if (plateGraphicHandler == null)
			{
				return null;
			}
			GameObject gameObject = UnityEngine.Object.Instantiate(plateGraphicHandler.gameObject, parent);
			gameObject.SetActive(value: true);
			gameObject.GetComponent<PlateGraphicHandler>()?.ApplyOriginalPlate();
			SushiCompositionData customComposition = CustomComposition;
			CustomSushiIngredientVisuals[] componentsInChildren = gameObject.GetComponentsInChildren<CustomSushiIngredientVisuals>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].Apply(customComposition, isCustomSushi: true);
			}
			return gameObject;
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

		private void StopParentVisibilityCoroutine()
		{
			if (_parentVisibilityCoroutine != null)
			{
				Singleton<DelayHelper>.Instance.StopCoroutine(_parentVisibilityCoroutine);
				_parentVisibilityCoroutine = null;
			}
		}

		private void RefreshRealObjectVisibilityByRuntimeState()
		{
			bool flag = IsRepresentedByShadowInHierarchy();
			SetRealObjectRuntimeStatus(!flag);
		}

		private bool IsRepresentedByShadowInHierarchy()
		{
			if (base.HasRuntimeShadowVisual)
			{
				return true;
			}
			Transform parent = base.transform.parent;
			while (parent != null)
			{
				if (parent.TryGetComponent<BaseHoldable>(out var component) && component.HasRuntimeShadowVisual)
				{
					return true;
				}
				parent = parent.parent;
			}
			return false;
		}

		private void SetRealObjectRuntimeStatus(bool visible)
		{
			if (visible)
			{
				if (!_realObjectRuntimeVisible)
				{
					RestoreForceDisabledColliders();
				}
				_realObjectRuntimeVisible = true;
				ObjectsStatus(status: true);
				CollidersStatus(status: true);
			}
			else
			{
				ObjectsStatus(status: false);
				CollidersStatus(status: false);
				if (_realObjectRuntimeVisible)
				{
					ForceDisableAllRuntimeColliders();
				}
				_realObjectRuntimeVisible = false;
			}
		}

		private void ForceDisableAllRuntimeColliders()
		{
			_forceDisabledColliders.Clear();
			Collider[] componentsInChildren = GetComponentsInChildren<Collider>(includeInactive: true);
			foreach (Collider collider in componentsInChildren)
			{
				if (!(collider == null) && collider.enabled)
				{
					collider.enabled = false;
					_forceDisabledColliders.Add(collider);
				}
			}
		}

		private void RestoreForceDisabledColliders()
		{
			for (int i = 0; i < _forceDisabledColliders.Count; i++)
			{
				Collider collider = _forceDisabledColliders[i];
				if (!(collider == null))
				{
					collider.enabled = true;
				}
			}
			_forceDisabledColliders.Clear();
		}

		protected override void __initializeVariables()
		{
			if (_useConveyorPlateGraphic == null)
			{
				throw new Exception("ServedDishSushi._useConveyorPlateGraphic cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_useConveyorPlateGraphic.Initialize(this);
			__nameNetworkVariable(_useConveyorPlateGraphic, "_useConveyorPlateGraphic");
			NetworkVariableFields.Add(_useConveyorPlateGraphic);
			if (_assignedEdgeIndex == null)
			{
				throw new Exception("ServedDishSushi._assignedEdgeIndex cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_assignedEdgeIndex.Initialize(this);
			__nameNetworkVariable(_assignedEdgeIndex, "_assignedEdgeIndex");
			NetworkVariableFields.Add(_assignedEdgeIndex);
			if (_chefDeskReference == null)
			{
				throw new Exception("ServedDishSushi._chefDeskReference cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_chefDeskReference.Initialize(this);
			__nameNetworkVariable(_chefDeskReference, "_chefDeskReference");
			NetworkVariableFields.Add(_chefDeskReference);
			if (_hasCustomSushiMetadata == null)
			{
				throw new Exception("ServedDishSushi._hasCustomSushiMetadata cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_hasCustomSushiMetadata.Initialize(this);
			__nameNetworkVariable(_hasCustomSushiMetadata, "_hasCustomSushiMetadata");
			NetworkVariableFields.Add(_hasCustomSushiMetadata);
			if (_customRecipeIdentityKind == null)
			{
				throw new Exception("ServedDishSushi._customRecipeIdentityKind cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_customRecipeIdentityKind.Initialize(this);
			__nameNetworkVariable(_customRecipeIdentityKind, "_customRecipeIdentityKind");
			NetworkVariableFields.Add(_customRecipeIdentityKind);
			if (_customRecipeId == null)
			{
				throw new Exception("ServedDishSushi._customRecipeId cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_customRecipeId.Initialize(this);
			__nameNetworkVariable(_customRecipeId, "_customRecipeId");
			NetworkVariableFields.Add(_customRecipeId);
			if (_customDisplayName == null)
			{
				throw new Exception("ServedDishSushi._customDisplayName cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_customDisplayName.Initialize(this);
			__nameNetworkVariable(_customDisplayName, "_customDisplayName");
			NetworkVariableFields.Add(_customDisplayName);
			if (_customPrice == null)
			{
				throw new Exception("ServedDishSushi._customPrice cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_customPrice.Initialize(this);
			__nameNetworkVariable(_customPrice, "_customPrice");
			NetworkVariableFields.Add(_customPrice);
			if (_customComposition == null)
			{
				throw new Exception("ServedDishSushi._customComposition cannot be null. All NetworkVariableBase instances must be initialized.");
			}
			_customComposition.Initialize(this);
			__nameNetworkVariable(_customComposition, "_customComposition");
			NetworkVariableFields.Add(_customComposition);
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			base.__initializeRpcs();
		}

		protected override string __getTypeName()
		{
			return "ServedDishSushi";
		}
	}
}
