using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Kitchen.Yaki3000Machine.UI;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.Utility;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Kitchen.Yaki3000Machine
{
	public class Yaki3000Canvas : NetworkBehaviour
	{
		[Header("References")]
		[SerializeField]
		private Yaki3000 _yaki3000;

		[SerializeField]
		private ItemDatabaseSO _itemDatabaseSO;

		[Header("Panels")]
		[SerializeField]
		private GameObject _recipesPanel;

		[SerializeField]
		private GameObject _infoPanel;

		[Header("Recipe UI Slots")]
		[SerializeField]
		private List<RecipeIngredientUI> _recipeIngredientSlots;

		[Space]
		[SerializeField]
		private BaseButton _cookButton;

		[SerializeField]
		private BaseButton _backButton;

		[SerializeField]
		private TextMeshProUGUI _infoText;

		private Coroutine _lateJoinRoutine;

		[Header("DessertPanel")]
		[SerializeField]
		private GameObject _dessertPanel;

		[SerializeField]
		private Button _dessertSelectButton;

		[Header("Dessert")]
		[SerializeField]
		private Yaki3000DessertUI_Item _dessertUI_ItemPrefab;

		[SerializeField]
		private Transform _dessertItemSpawnTransform;

		[Header("Recipe")]
		[SerializeField]
		private Yaki3000RecipeUI_Item _recipeUI_ItemPrefab;

		[SerializeField]
		private Transform _recipeItemSpawnTransform;

		private List<Yaki3000DessertUI_Item> _spawnedDesertUI_Item = new List<Yaki3000DessertUI_Item>();

		private List<Yaki3000RecipeUI_Item> _spawnedRecipeUI_Item = new List<Yaki3000RecipeUI_Item>();

		private Yaki3000Product _currentProduct;

		public IReadOnlyList<RecipeIngredientUI> RecipeIngredientSlots => _recipeIngredientSlots;

		public ItemDatabaseSO ItemDatabaseSO => _itemDatabaseSO;

		public TextMeshProUGUI InfoText => _infoText;

		public void Initiazlie()
		{
			EnsureDatabaseRefs();
			HideAllIngredientSlots();
			BindSlots();
			if (_cookButton != null)
			{
				_cookButton.gameObject.SetActive(value: false);
			}
			DessertPanelInit();
			if (_lateJoinRoutine != null)
			{
				StopCoroutine(_lateJoinRoutine);
			}
			_lateJoinRoutine = StartCoroutine(LateJoinApplyStateRoutine());
			if (_cookButton != null)
			{
				_cookButton.Clicked += OnRequestStartCooking;
			}
			if (_backButton != null)
			{
				_backButton.Clicked += OnBackButton;
			}
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			if (_lateJoinRoutine != null)
			{
				StopCoroutine(_lateJoinRoutine);
				_lateJoinRoutine = null;
			}
			if (_cookButton != null)
			{
				_cookButton.Clicked -= OnRequestStartCooking;
			}
			if (_backButton != null)
			{
				_backButton.Clicked -= OnBackButton;
			}
		}

		private void BindSlots()
		{
			if (_recipeIngredientSlots != null)
			{
				for (int i = 0; i < _recipeIngredientSlots.Count; i++)
				{
					_recipeIngredientSlots[i].BindToYakiStatus(this);
				}
			}
		}

		private void HideAllIngredientSlots()
		{
			if (_recipeIngredientSlots != null)
			{
				for (int i = 0; i < _recipeIngredientSlots.Count; i++)
				{
					_recipeIngredientSlots[i].ResetData();
					_recipeIngredientSlots[i].gameObject.SetActive(value: false);
				}
			}
		}

		private void EnsureDatabaseRefs()
		{
			if (_itemDatabaseSO == null && _yaki3000 != null)
			{
				_itemDatabaseSO = _yaki3000.ItemDatabaseSO;
			}
		}

		private IEnumerator LateJoinApplyStateRoutine()
		{
			yield return null;
			EnsureDatabaseRefs();
			int guard = 0;
			while (guard < 30 && (_yaki3000 == null || _itemDatabaseSO == null))
			{
				guard++;
				yield return null;
				EnsureDatabaseRefs();
			}
			ApplyPanelForCurrentState();
		}

		private void ApplyPanelForCurrentState()
		{
			if (_yaki3000 == null)
			{
				PanelesActivityChange(dessertPanel: true);
				return;
			}
			switch ((Yaki3000State)(byte)_yaki3000.CurrentState)
			{
			case Yaki3000State.Empty:
				HideAllIngredientSlots();
				if (_cookButton != null)
				{
					_cookButton.gameObject.SetActive(value: false);
				}
				PanelesActivityChange(dessertPanel: true);
				break;
			case Yaki3000State.Preparing:
				RefreshRecipePanelLocal();
				PanelesActivityChange(dessertPanel: false, recipePanel: true);
				break;
			case Yaki3000State.Cooking:
				PanelesActivityChange(dessertPanel: false, recipePanel: false, preparingPanel: true);
				if (_infoText != null)
				{
					_infoText.text = "Preparing";
				}
				break;
			default:
				PanelesActivityChange(dessertPanel: true);
				break;
			}
		}

		private void RefreshRecipePanelLocal()
		{
			EnsureDatabaseRefs();
			BuildRecipeRequirements(out var ingredientIds, out var requiredAmounts);
			HideAllIngredientSlots();
			ShowRecipeRequirementsLocal(ingredientIds, requiredAmounts);
			SyncCurrentCountsToUI();
		}

		private void ShowRecipeRequirementsLocal(List<FixedString64Bytes> ingredientIds, List<int> requiredAmounts)
		{
			int num = Mathf.Min(_recipeIngredientSlots.Count, ingredientIds.Count);
			for (int i = 0; i < num; i++)
			{
				RecipeIngredientUI recipeIngredientUI = _recipeIngredientSlots[i];
				recipeIngredientUI.gameObject.SetActive(value: true);
				recipeIngredientUI.Initialize(ingredientIds[i], requiredAmounts[i]);
			}
		}

		private void SyncCurrentCountsToUI()
		{
			for (int i = 0; i < _recipeIngredientSlots.Count; i++)
			{
				RecipeIngredientUI recipeIngredientUI = _recipeIngredientSlots[i];
				if (!recipeIngredientUI.gameObject.activeSelf)
				{
					continue;
				}
				int currentAmount = 0;
				NetworkList<IngredientEntry> machineIngredientsEntries = _yaki3000.MachineIngredientsEntries;
				for (int j = 0; j < machineIngredientsEntries.Count; j++)
				{
					if (machineIngredientsEntries[j].IngredientID.Equals(recipeIngredientUI.IngredientID))
					{
						currentAmount = machineIngredientsEntries[j].Count;
						break;
					}
				}
				recipeIngredientUI.UpdateAmount(currentAmount);
			}
			UtilityMethods.Log("Recipe UI counts synced.");
		}

		private void BuildRecipeRequirements(out List<FixedString64Bytes> ingredientIds, out List<int> requiredAmounts)
		{
			ingredientIds = new List<FixedString64Bytes>();
			requiredAmounts = new List<int>();
			Yaki3000IngredientSO yaki3000IngredientSO = FindActiveDessertSO();
			if (yaki3000IngredientSO == null || yaki3000IngredientSO.Recipe == null)
			{
				UtilityMethods.Log("<color=red><b>Active DessertSO/Recipe not found.</b></color>");
				return;
			}
			AppendNeeds(yaki3000IngredientSO.Recipe.Ingredients, ingredientIds, requiredAmounts);
			AppendNeeds(yaki3000IngredientSO.Recipe.ProcessedIngredients, ingredientIds, requiredAmounts);
		}

		private Yaki3000IngredientSO FindActiveDessertSO()
		{
			if (_yaki3000 == null || _yaki3000.ItemDatabaseSO == null)
			{
				return null;
			}
			return _yaki3000.ItemDatabaseSO.FindById(_yaki3000.DessertSO_ID) as Yaki3000IngredientSO;
		}

		private void AppendNeeds<TNeed>(List<TNeed> needs, List<FixedString64Bytes> outIds, List<int> outRequired) where TNeed : BaseIngredientNeed
		{
			if (needs == null || needs.Count == 0)
			{
				return;
			}
			List<TNeed> list = new List<TNeed>(needs);
			list.Sort((TNeed a, TNeed b) => a.Order.CompareTo(b.Order));
			for (int num = 0; num < list.Count; num++)
			{
				TNeed val = list[num];
				if (val != null)
				{
					string ingredientId = val.IngredientId;
					byte requiredAmount = val.RequiredAmount;
					if (!string.IsNullOrEmpty(ingredientId) && requiredAmount > 0)
					{
						outIds.Add(new FixedString64Bytes(ingredientId));
						outRequired.Add(requiredAmount);
					}
				}
			}
		}

		public void ServerNotifyDessertSelected()
		{
			EnsureDatabaseRefs();
			BuildRecipeRequirements(out var ingredientIds, out var requiredAmounts);
			ClientShowRecipeRequirementsRpc(ingredientIds.ToArray(), requiredAmounts.ToArray());
		}

		[Rpc(SendTo.Everyone)]
		private void ClientShowRecipeRequirementsRpc(FixedString64Bytes[] ingredientIds, int[] requiredAmounts)
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
				FastBufferWriter bufferWriter = __beginSendRpc(4006214700u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				bool value = ingredientIds != null;
				bufferWriter.WriteValueSafe(in value, default(FastBufferWriter.ForPrimitives));
				if (value)
				{
					bufferWriter.WriteValueSafe(ingredientIds, default(FastBufferWriter.ForFixedStrings));
				}
				bool value2 = requiredAmounts != null;
				bufferWriter.WriteValueSafe(in value2, default(FastBufferWriter.ForPrimitives));
				if (value2)
				{
					bufferWriter.WriteValueSafe(requiredAmounts, default(FastBufferWriter.ForPrimitives));
				}
				__endSendRpc(ref bufferWriter, 4006214700u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				EnsureDatabaseRefs();
				_cookButton.gameObject.SetActive(value: false);
				PanelesActivityChange(dessertPanel: false, recipePanel: true);
				HideAllIngredientSlots();
				int num = Mathf.Min(_recipeIngredientSlots.Count, ingredientIds.Length);
				for (int i = 0; i < num; i++)
				{
					RecipeIngredientUI recipeIngredientUI = _recipeIngredientSlots[i];
					recipeIngredientUI.gameObject.SetActive(value: true);
					recipeIngredientUI.Initialize(ingredientIds[i], requiredAmounts[i]);
				}
			}
		}

		[Rpc(SendTo.Everyone)]
		public void ClientNotifyRecipeCompletedRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(1215918126u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 1215918126u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				_cookButton.gameObject.SetActive(value: true);
			}
		}

		private void OnRequestStartCooking()
		{
			_yaki3000.ServerTryStartCookingRpc();
		}

		private void OnBackButton()
		{
			_yaki3000.ServerResetToDessertPanelRpc();
		}

		[Rpc(SendTo.Everyone)]
		public void ClientOnCookingStartedRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(4232013024u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 4232013024u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				PanelesActivityChange(dessertPanel: false, recipePanel: false, preparingPanel: true);
				_infoText.text = "Preperaing";
			}
		}

		public void PanelesActivityChange(bool dessertPanel = false, bool recipePanel = false, bool preparingPanel = false)
		{
			_dessertPanel.SetActive(dessertPanel);
			_recipesPanel.SetActive(recipePanel);
			_infoPanel.SetActive(preparingPanel);
		}

		[Rpc(SendTo.Everyone)]
		public void ResetRpc()
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
				FastBufferWriter bufferWriter = __beginSendRpc(764279762u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
				__endSendRpc(ref bufferWriter, 764279762u, rpcParams, attributeParams, SendTo.Everyone, RpcDelivery.Reliable);
			}
			if (__rpc_exec_stage == __RpcExecStage.Execute)
			{
				__rpc_exec_stage = __RpcExecStage.Send;
				if (_cookButton != null)
				{
					_cookButton.gameObject.SetActive(value: false);
				}
				HideAllIngredientSlots();
				PanelesActivityChange(dessertPanel: true);
				if (_infoText != null)
				{
					_infoText.text = string.Empty;
				}
			}
		}

		private void DessertPanelInit()
		{
			_dessertSelectButton.onClick.RemoveAllListeners();
			_dessertSelectButton.onClick.AddListener(OnSelectDessertButton);
			foreach (Yaki3000IngredientSO caschedDessert in _yaki3000.CaschedDesserts)
			{
				Yaki3000DessertUI_Item yaki3000DessertUI_Item = UnityEngine.Object.Instantiate(_dessertUI_ItemPrefab, _dessertItemSpawnTransform);
				yaki3000DessertUI_Item.Initiazlie(caschedDessert);
				_spawnedDesertUI_Item.Add(yaki3000DessertUI_Item);
				yaki3000DessertUI_Item.OnClicked = (Action<byte>)Delegate.Combine(yaki3000DessertUI_Item.OnClicked, new Action<byte>(OnDessertItemClicked));
			}
			if (_spawnedDesertUI_Item.Count > 0)
			{
				OnDessertItemClicked((byte)_spawnedDesertUI_Item[0].Yaki3000Product);
				_spawnedDesertUI_Item[0].SelectedBorder.enabled = true;
			}
		}

		private void OnDessertItemClicked(byte productType)
		{
			Debug.Log($"Dessert clicked: {productType}");
			_currentProduct = (Yaki3000Product)productType;
			ClearRecipeUIItems();
			foreach (Yaki3000DessertUI_Item item in _spawnedDesertUI_Item)
			{
				item.SelectedBorder.enabled = false;
				item.DefaultBorder.enabled = true;
			}
			Yaki3000DessertUI_Item yaki3000DessertUI_Item = _spawnedDesertUI_Item.FirstOrDefault((Yaki3000DessertUI_Item x) => x.Yaki3000Product == _currentProduct);
			if (yaki3000DessertUI_Item == null)
			{
				Debug.LogWarning($"Dessert UI Item not found for {_currentProduct}");
				return;
			}
			yaki3000DessertUI_Item.SelectedBorder.enabled = true;
			SpawnRecipeUIItems(yaki3000DessertUI_Item.DessertSO.Recipe);
		}

		private void ClearRecipeUIItems()
		{
			foreach (Yaki3000RecipeUI_Item item in _spawnedRecipeUI_Item)
			{
				if (!(item == null))
				{
					UnityEngine.Object.Destroy(item.gameObject);
				}
			}
			_spawnedRecipeUI_Item.Clear();
		}

		private void SpawnRecipeUIItems(Recipe recipe)
		{
			if (recipe == null)
			{
				Debug.LogWarning("Recipe is null, cannot spawn recipe UI.");
				return;
			}
			foreach (BaseIngredientNeed item in (from x in recipe.Ingredients.Cast<BaseIngredientNeed>().Concat(recipe.ProcessedIngredients)
				orderby x.Order
				select x).ToList())
			{
				Yaki3000RecipeUI_Item yaki3000RecipeUI_Item = UnityEngine.Object.Instantiate(_recipeUI_ItemPrefab, _recipeItemSpawnTransform);
				yaki3000RecipeUI_Item.Initiazlie(item);
				_spawnedRecipeUI_Item.Add(yaki3000RecipeUI_Item);
			}
		}

		private void OnSelectDessertButton()
		{
			Yaki3000State state = _yaki3000.State;
			if (state != Yaki3000State.Empty && state != Yaki3000State.Preparing)
			{
				UtilityMethods.Log("Urun secilemedi");
			}
			else
			{
				_yaki3000.ServerSetProductRpc((byte)_currentProduct);
			}
		}

		protected override void __initializeVariables()
		{
			base.__initializeVariables();
		}

		protected override void __initializeRpcs()
		{
			__registerRpc(4006214700u, __rpc_handler_4006214700, "ClientShowRecipeRequirementsRpc", RpcInvokePermission.Everyone);
			__registerRpc(1215918126u, __rpc_handler_1215918126, "ClientNotifyRecipeCompletedRpc", RpcInvokePermission.Everyone);
			__registerRpc(4232013024u, __rpc_handler_4232013024, "ClientOnCookingStartedRpc", RpcInvokePermission.Everyone);
			__registerRpc(764279762u, __rpc_handler_764279762, "ResetRpc", RpcInvokePermission.Everyone);
			base.__initializeRpcs();
		}

		private static void __rpc_handler_4006214700(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				reader.ReadValueSafe(out bool value, default(FastBufferWriter.ForPrimitives));
				FixedString64Bytes[] value2 = null;
				if (value)
				{
					reader.ReadValueSafe(out value2, default(FastBufferWriter.ForFixedStrings));
				}
				reader.ReadValueSafe(out bool value3, default(FastBufferWriter.ForPrimitives));
				int[] value4 = null;
				if (value3)
				{
					reader.ReadValueSafe(out value4, default(FastBufferWriter.ForPrimitives));
				}
				((Yaki3000Canvas)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Yaki3000Canvas)target).ClientShowRecipeRequirementsRpc(value2, value4);
				((Yaki3000Canvas)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_1215918126(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((Yaki3000Canvas)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Yaki3000Canvas)target).ClientNotifyRecipeCompletedRpc();
				((Yaki3000Canvas)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_4232013024(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((Yaki3000Canvas)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Yaki3000Canvas)target).ClientOnCookingStartedRpc();
				((Yaki3000Canvas)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		private static void __rpc_handler_764279762(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
		{
			NetworkManager networkManager = target.NetworkManager;
			if ((object)networkManager != null && networkManager.IsListening)
			{
				((Yaki3000Canvas)target).__rpc_exec_stage = __RpcExecStage.Execute;
				((Yaki3000Canvas)target).ResetRpc();
				((Yaki3000Canvas)target).__rpc_exec_stage = __RpcExecStage.Send;
			}
		}

		protected override string __getTypeName()
		{
			return "Yaki3000Canvas";
		}
	}
}
