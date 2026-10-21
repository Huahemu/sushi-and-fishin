using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Core.Localization;
using PaperPixelGames.Core.Managers;
using PaperPixelGames.Core.Player;
using PaperPixelGames.Core.Quests;
using PaperPixelGames.Framework.Enums;
using PaperPixelGames.Framework.Patterns;
using PaperPixelGames.Framework.UI;
using PaperPixelGames.Framework.UI.Notify;
using PaperPixelGames.Framework.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Kitchen.ChefDesk
{
	public class ChefDeskBookCanvas : MonoBehaviour
	{
		[SerializeField]
		private ChefDesk _chefDesk;

		[Header("Sushi Item")]
		[SerializeField]
		private ChefDeskSushiItem _sushiItemPrefab;

		[SerializeField]
		private Transform _sushiItemContainer;

		[SerializeField]
		private TMP_InputField _sushiSearchInput;

		[SerializeField]
		private GameObject _sushiListSeparatorPrefab;

		[Header("Custom Recipe Icons")]
		[SerializeField]
		private Sprite _customMakiIcon;

		[SerializeField]
		private Sprite _customUramakiIcon;

		[SerializeField]
		private Sprite _customTemakiIcon;

		[Header("Recipe")]
		[SerializeField]
		private ChefDeskIngredientItem _ingredientItemPrefab;

		[SerializeField]
		private Transform _ingredientItemContainer;

		[Space]
		[SerializeField]
		private BaseButton _startRecipeButton;

		[SerializeField]
		private BaseButton _bookCloseButton;

		[SerializeField]
		private Image _selectedSushiImage;

		[SerializeField]
		private TextMeshProUGUI _selectedSushiNameText;

		[Header("Desk Reset")]
		[SerializeField]
		private GameObject _resetPopupRoot;

		[SerializeField]
		private BaseButton _openResetPopupBtn;

		[SerializeField]
		private BaseButton _confirmResetBtn;

		[SerializeField]
		private BaseButton _cancelResetBtn;

		[Header("Localized Labels")]
		[SerializeField]
		private TMP_Text _recipesTitleText;

		[SerializeField]
		private TMP_Text _recipeTitleText;

		[SerializeField]
		private TMP_Text _resetTitleText;

		[SerializeField]
		private TMP_Text _resetDescriptionText;

		private Coroutine _localizationRoutine;

		private readonly List<ChefDeskSushiItem> _spawnedSushiItems = new List<ChefDeskSushiItem>();

		private readonly List<ChefDeskIngredientItem> _spawnedIngredientItems = new List<ChefDeskIngredientItem>();

		private readonly HashSet<string> _menuSushiRecipeIds = new HashSet<string>();

		private List<SushiRecipeDefinition> _allSushiRecipes = new List<SushiRecipeDefinition>();

		private GameObject _spawnedSushiListSeparator;

		private string _prioritizedQuestSushiRecipeId = string.Empty;

		public GameObject DeskResetCanvasGO => _resetPopupRoot;

		public TextMeshProUGUI SelectedSushiNameText => _selectedSushiNameText;

		public Image SelectedSushiImage => _selectedSushiImage;

		private void OnEnable()
		{
			LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
			RefreshStaticLocalization();
			_localizationRoutine = StartCoroutine(RefreshWhenLocalizationReady());
			RefreshRecipeIngredientAvailability();
			SubscribeToCustomRecipeCatalog();
		}

		private void Start()
		{
			_resetPopupRoot.SetActive(value: false);
			LoadSushiRecipes();
			BuildSushiList();
			_startRecipeButton.Clicked -= OnHandleStartRecipeClicked;
			_startRecipeButton.Clicked += OnHandleStartRecipeClicked;
			_bookCloseButton.Clicked -= OnHandleBookUICloseClicked;
			_bookCloseButton.Clicked += OnHandleBookUICloseClicked;
			_confirmResetBtn.Clicked -= OnConfirmReset;
			_confirmResetBtn.Clicked += OnConfirmReset;
			_cancelResetBtn.Clicked -= OnHideResetPopup;
			_cancelResetBtn.Clicked += OnHideResetPopup;
			_openResetPopupBtn.Clicked -= OnShowResetPopup;
			_openResetPopupBtn.Clicked += OnShowResetPopup;
			_chefDesk.ClientGastroPanStockChanged -= RefreshRecipeIngredientAvailability;
			UnsubscribeFromCustomRecipeCatalog();
			_chefDesk.ClientGastroPanStockChanged += RefreshRecipeIngredientAvailability;
			if (_sushiSearchInput != null)
			{
				_sushiSearchInput.onValueChanged.RemoveListener(OnSushiSearchChanged);
				_sushiSearchInput.onValueChanged.AddListener(OnSushiSearchChanged);
			}
		}

		private void OnDestroy()
		{
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			_startRecipeButton.Clicked -= OnHandleStartRecipeClicked;
			_bookCloseButton.Clicked -= OnHandleBookUICloseClicked;
			_confirmResetBtn.Clicked -= OnConfirmReset;
			_cancelResetBtn.Clicked -= OnHideResetPopup;
			_openResetPopupBtn.Clicked -= OnShowResetPopup;
			_chefDesk.ClientGastroPanStockChanged -= RefreshRecipeIngredientAvailability;
			if (_sushiSearchInput != null)
			{
				_sushiSearchInput.onValueChanged.RemoveListener(OnSushiSearchChanged);
			}
		}

		private void OnDisable()
		{
			LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
			if (_localizationRoutine != null)
			{
				StopCoroutine(_localizationRoutine);
			}
			_localizationRoutine = null;
			_resetPopupRoot.SetActive(value: false);
		}

		private void LoadSushiRecipes()
		{
			IReadOnlyList<ItemSO> allOfType = _chefDesk.ItemDatabaseSO.GetAllOfType(ItemType.ServedDish);
			_allSushiRecipes = (from recipe in (from item in allOfType.OfType<SushiSO>()
					where item.ItemType == ItemType.ServedDish && item.ServedDishType == ServedDishType.Sushi
					select item).Select(SushiRecipeResolver.CreateBuiltInDefinition)
				where recipe != null
				select recipe).ToList();
			IReadOnlyList<CustomSushiRecipeData> readOnlyList = GetRestaurantManager()?.CustomSushiRecipeCatalog?.Recipes;
			if (readOnlyList == null)
			{
				return;
			}
			foreach (CustomSushiRecipeData item in readOnlyList)
			{
				SushiRecipeDefinition sushiRecipeDefinition = SushiRecipeResolver.CreateCustomDefinition(item, ResolveCustomRecipeIcon(item.SushiType));
				if (sushiRecipeDefinition != null)
				{
					_allSushiRecipes.Add(sushiRecipeDefinition);
				}
			}
		}

		private void BuildSushiList()
		{
			ClearSushiList();
			if (_allSushiRecipes == null || _allSushiRecipes.Count <= 0)
			{
				return;
			}
			PopulateMenuSushiRecipeIds();
			_prioritizedQuestSushiRecipeId = ResolveActiveQuestSushiRecipeId();
			List<SushiRecipeDefinition> list = _allSushiRecipes.Where((SushiRecipeDefinition recipe) => recipe != null && _menuSushiRecipeIds.Contains(recipe.RecipeId) && recipe.RecipeId != _prioritizedQuestSushiRecipeId).ToList();
			List<SushiRecipeDefinition> list2 = _allSushiRecipes.Where((SushiRecipeDefinition recipe) => recipe != null && !_menuSushiRecipeIds.Contains(recipe.RecipeId) && recipe.RecipeId != _prioritizedQuestSushiRecipeId).ToList();
			List<SushiRecipeDefinition> list3 = _allSushiRecipes.Where((SushiRecipeDefinition recipe) => recipe != null && recipe.RecipeId == _prioritizedQuestSushiRecipeId).Take(1).ToList();
			SpawnSushiItems(list3);
			SpawnSushiItems(list);
			if ((list3.Count > 0 || list.Count > 0) && list2.Count > 0)
			{
				if (_sushiListSeparatorPrefab != null)
				{
					_spawnedSushiListSeparator = Object.Instantiate(_sushiListSeparatorPrefab, _sushiItemContainer);
					_spawnedSushiListSeparator.SetActive(value: true);
				}
				else
				{
					UtilityMethods.Log("[ChefDeskBook] Sushi list separator prefab is missing.", LogType.Warning);
				}
			}
			SpawnSushiItems(list2);
			ApplySushiSearchFilter();
		}

		private void PopulateMenuSushiRecipeIds()
		{
			_menuSushiRecipeIds.Clear();
			PPG_Player pPG_Player = ((Singleton<PlayersManager>.Instance != null) ? Singleton<PlayersManager>.Instance.LocalPlayer : null);
			IReadOnlyCollection<string> readOnlyCollection = ((pPG_Player != null && pPG_Player.TabletController != null && pPG_Player.TabletController.MenuApp != null) ? pPG_Player.TabletController.MenuApp.MenuRecipeIds : null);
			if (readOnlyCollection == null)
			{
				return;
			}
			foreach (string item in readOnlyCollection)
			{
				if (!string.IsNullOrWhiteSpace(item))
				{
					_menuSushiRecipeIds.Add(item);
				}
			}
		}

		private void SpawnSushiItems(IEnumerable<SushiRecipeDefinition> sushiRecipes)
		{
			foreach (SushiRecipeDefinition sushiRecipe in sushiRecipes)
			{
				ChefDeskSushiItem chefDeskSushiItem = Object.Instantiate(_sushiItemPrefab, _sushiItemContainer);
				chefDeskSushiItem.Prepare(sushiRecipe, this);
				_spawnedSushiItems.Add(chefDeskSushiItem);
			}
		}

		private void ClearSushiList()
		{
			foreach (ChefDeskSushiItem spawnedSushiItem in _spawnedSushiItems)
			{
				if (spawnedSushiItem != null)
				{
					Object.Destroy(spawnedSushiItem.gameObject);
				}
			}
			_spawnedSushiItems.Clear();
			if (_spawnedSushiListSeparator != null)
			{
				Object.Destroy(_spawnedSushiListSeparator);
			}
			_spawnedSushiListSeparator = null;
			_menuSushiRecipeIds.Clear();
			_prioritizedQuestSushiRecipeId = string.Empty;
		}

		private void OnSushiSearchChanged(string searchText)
		{
			ApplySushiSearchFilter();
		}

		private void OnSelectedLocaleChanged(Locale _)
		{
			RefreshStaticLocalization();
			foreach (ChefDeskSushiItem spawnedSushiItem in _spawnedSushiItems)
			{
				spawnedSushiItem?.RefreshLocalization();
			}
			foreach (ChefDeskIngredientItem spawnedIngredientItem in _spawnedIngredientItems)
			{
				spawnedIngredientItem?.RefreshLocalization();
			}
			ApplySushiSearchFilter();
		}

		private IEnumerator RefreshWhenLocalizationReady()
		{
			yield return LocalizationSettings.InitializationOperation;
			OnSelectedLocaleChanged(LocalizationSettings.SelectedLocale);
			_localizationRoutine = null;
		}

		private void RefreshStaticLocalization()
		{
			_startRecipeButton?.SetText(Localize("chef_desk.action.select_recipe", "Select Recipe"));
			_openResetPopupBtn?.SetText(Localize("chef_desk.action.reset_desk", "Reset Desk"));
			_confirmResetBtn?.SetText(Localize("chef_desk.action.fix_table", "Fix Table"));
			_cancelResetBtn?.SetText(Localize("tablet.common.cancel", "Cancel"));
			SetLabel(_recipesTitleText, "chef_desk.recipes_title", "Recipes");
			SetLabel(_recipeTitleText, "chef_desk.recipe_title", "Recipe");
			SetLabel(_resetTitleText, "chef_desk.reset.title", "Table is stuck?");
			SetLabel(_resetDescriptionText, "chef_desk.reset.description", "Reset the work area to recover a stuck chef desk.");
			if (_sushiSearchInput != null && _sushiSearchInput.placeholder is TMP_Text label)
			{
				SetLabel(label, "tablet.menu.search_placeholder", "search recipe...");
			}
		}

		private static string Localize(string key, string fallback)
		{
			return LocalizationDatabase.GetStringOrFallback("UiTable", key, fallback);
		}

		private static void SetLabel(TMP_Text label, string key, string fallback)
		{
			if (label != null)
			{
				label.text = Localize(key, fallback);
			}
		}

		private void ApplySushiSearchFilter()
		{
			string searchText = ((_sushiSearchInput != null) ? _sushiSearchInput.text : string.Empty);
			ChefDeskSushiItem chefDeskSushiItem = null;
			bool flag = false;
			bool flag2 = false;
			bool flag3 = false;
			foreach (ChefDeskSushiItem spawnedSushiItem in _spawnedSushiItems)
			{
				if (spawnedSushiItem == null || spawnedSushiItem.Recipe == null)
				{
					continue;
				}
				bool flag4 = DoesSushiMatchSearch(spawnedSushiItem.Recipe, searchText);
				spawnedSushiItem.gameObject.SetActive(flag4);
				if (flag4)
				{
					if (IsTopSectionSushi(spawnedSushiItem.RecipeId))
					{
						flag2 = true;
					}
					else
					{
						flag3 = true;
					}
					if ((object)chefDeskSushiItem == null)
					{
						chefDeskSushiItem = spawnedSushiItem;
					}
					if (spawnedSushiItem.RecipeId == _chefDesk.PreviewedSushiID)
					{
						flag = true;
					}
				}
			}
			if (_spawnedSushiListSeparator != null)
			{
				_spawnedSushiListSeparator.SetActive(flag2 & flag3);
			}
			if (!flag)
			{
				if (chefDeskSushiItem != null)
				{
					ClientSelectSushiPreview(chefDeskSushiItem.RecipeId);
				}
				else
				{
					ClearSushiPreview();
				}
			}
		}

		private static bool DoesSushiMatchSearch(SushiRecipeDefinition recipe, string searchText)
		{
			if (recipe == null)
			{
				return false;
			}
			if (string.IsNullOrWhiteSpace(searchText))
			{
				return true;
			}
			string searchText2 = searchText.Trim();
			return LocalizationDatabase.ContainsLocalized((recipe.BuiltInSushi != null) ? recipe.BuiltInSushi.LocalizedName : recipe.DisplayName, searchText2);
		}

		private bool IsTopSectionSushi(string sushiId)
		{
			if (!string.IsNullOrWhiteSpace(sushiId))
			{
				if (!_menuSushiRecipeIds.Contains(sushiId))
				{
					return sushiId == _prioritizedQuestSushiRecipeId;
				}
				return true;
			}
			return false;
		}

		private string ResolveActiveQuestSushiRecipeId()
		{
			QuestReferenceHub instance = QuestReferenceHub.Instance;
			ItemDatabaseSO itemDatabaseSO = ((_chefDesk != null) ? _chefDesk.ItemDatabaseSO : null);
			if (instance == null || itemDatabaseSO == null)
			{
				return string.Empty;
			}
			if (IsAnyQuestActive(instance, QuestId.SelectCucumberMakiRecipeFromRecipeBook, QuestId.MakeCucumberMaki))
			{
				return GetSushiRecipeId(itemDatabaseSO.CucumberMakiServeredDishSO);
			}
			if (IsAnyQuestActive(instance, QuestId.SelectMackerelNigiriRecipeFromRecipeBook, QuestId.MakeMackerelNigiri))
			{
				return GetSushiRecipeId(itemDatabaseSO.MackarelNigiriServeredDishSO);
			}
			if (IsAnyQuestActive(instance, QuestId.MakeAChinookSalmonDish))
			{
				return GetSushiRecipeId(itemDatabaseSO.ChinookSalmonTemakiServeredDishSO);
			}
			return string.Empty;
		}

		private static bool IsAnyQuestActive(QuestReferenceHub questHub, params QuestId[] questIds)
		{
			if (questHub == null || questIds == null)
			{
				return false;
			}
			for (int i = 0; i < questIds.Length; i++)
			{
				if (questHub.IsActive(questIds[i]))
				{
					return true;
				}
			}
			return false;
		}

		private static string GetSushiRecipeId(ItemSO itemSO)
		{
			if (!(itemSO is SushiSO sushiSO) || string.IsNullOrWhiteSpace(sushiSO.Id))
			{
				return string.Empty;
			}
			return sushiSO.Id;
		}

		private void ClearSushiPreview()
		{
			foreach (ChefDeskSushiItem spawnedSushiItem in _spawnedSushiItems)
			{
				if (spawnedSushiItem != null)
				{
					spawnedSushiItem.SetSelected(selected: false);
				}
			}
			_chefDesk.PreviewedSushiID = string.Empty;
			if (_selectedSushiNameText != null)
			{
				_selectedSushiNameText.text = string.Empty;
			}
			if (_selectedSushiImage != null)
			{
				_selectedSushiImage.sprite = null;
				_selectedSushiImage.enabled = false;
			}
			ClearRecipeIngredientUI();
		}

		public void ClientSelectSushiPreview(string sushiId)
		{
			if (string.IsNullOrWhiteSpace(sushiId))
			{
				return;
			}
			foreach (ChefDeskSushiItem spawnedSushiItem in _spawnedSushiItems)
			{
				if (spawnedSushiItem != null)
				{
					spawnedSushiItem.SetSelected(selected: false);
				}
			}
			_chefDesk.PreviewedSushiID = sushiId;
			ChefDeskSushiItem chefDeskSushiItem = _spawnedSushiItems.FirstOrDefault((ChefDeskSushiItem item) => item != null && item.RecipeId == sushiId);
			if (!(chefDeskSushiItem == null))
			{
				chefDeskSushiItem.Initialize();
			}
		}

		public void PopulateRecipeIngredientUI(SushiRecipeDefinition recipe)
		{
			if (recipe?.Composition?.IngredientIds == null)
			{
				return;
			}
			ClearRecipeIngredientUI();
			Dictionary<string, ChefDeskIngredientItem> dictionary = new Dictionary<string, ChefDeskIngredientItem>();
			Dictionary<string, int> dictionary2 = new Dictionary<string, int>();
			foreach (string ingredientId in recipe.Composition.IngredientIds)
			{
				if (!string.IsNullOrWhiteSpace(ingredientId))
				{
					if (dictionary.TryGetValue(ingredientId, out var value))
					{
						dictionary2[ingredientId]++;
						value.SetCount(dictionary2[ingredientId]);
						continue;
					}
					ChefDeskIngredientItem chefDeskIngredientItem = Object.Instantiate(_ingredientItemPrefab, _ingredientItemContainer);
					chefDeskIngredientItem.Init(ingredientId, 1, _chefDesk);
					dictionary.Add(ingredientId, chefDeskIngredientItem);
					dictionary2.Add(ingredientId, 1);
					_spawnedIngredientItems.Add(chefDeskIngredientItem);
				}
			}
		}

		private void ClearRecipeIngredientUI()
		{
			foreach (ChefDeskIngredientItem spawnedIngredientItem in _spawnedIngredientItems)
			{
				if (spawnedIngredientItem != null)
				{
					Object.Destroy(spawnedIngredientItem.gameObject);
				}
			}
			_spawnedIngredientItems.Clear();
		}

		public void ToggleBookUI(bool isVisible)
		{
			base.gameObject.SetActive(isVisible);
			if (isVisible)
			{
				LoadSushiRecipes();
				BuildSushiList();
			}
			if (!isVisible)
			{
				_resetPopupRoot.SetActive(value: false);
			}
			if (_chefDesk.CurrentState == ChefDeskState.Cutting)
			{
				Cursor.visible = isVisible;
			}
		}

		private void OnHandleStartRecipeClicked()
		{
			string rejectionMessage;
			if (string.IsNullOrWhiteSpace(_chefDesk.PreviewedSushiID))
			{
				Singleton<NotifyManager>.Instance.ShowToast("Recipe is not selected");
				UtilityMethods.Log("[ChefDeskBook] Cannot start recipe: PreviewedSushiID is empty.");
			}
			else if (!CanStartRecipeFromCurrentState())
			{
				Singleton<NotifyManager>.Instance.ShowToast("You can’t select a recipe right now");
				UtilityMethods.Log($"[ChefDeskBook] Cannot start recipe from state: {_chefDesk.CurrentState}");
			}
			else if (!_chefDesk.ClientCanSelectRecipe(_chefDesk.PreviewedSushiID, out rejectionMessage))
			{
				Singleton<NotifyManager>.Instance.ShowToast(rejectionMessage);
				UtilityMethods.Log("[ChefDeskBook] Cannot select recipe '" + _chefDesk.PreviewedSushiID + "'. Reason=" + rejectionMessage);
			}
			else
			{
				_chefDesk.ServerSushiSelectedRpc(_chefDesk.PreviewedSushiID);
				ToggleBookUI(isVisible: false);
			}
		}

		private bool CanStartRecipeFromCurrentState()
		{
			if (_chefDesk.CurrentState != ChefDeskState.Idle)
			{
				return _chefDesk.CurrentState == ChefDeskState.RecipeSelected;
			}
			return true;
		}

		private void OnHandleBookUICloseClicked()
		{
			ToggleBookUI(isVisible: false);
		}

		private void OnShowResetPopup()
		{
			_resetPopupRoot.SetActive(value: true);
		}

		private void OnConfirmReset()
		{
			_chefDesk.ServerChefDeskResetRpc();
			_resetPopupRoot.SetActive(value: false);
		}

		private void OnHideResetPopup()
		{
			_resetPopupRoot.SetActive(value: false);
		}

		private void RefreshRecipeIngredientAvailability()
		{
			for (int i = 0; i < _spawnedIngredientItems.Count; i++)
			{
				ChefDeskIngredientItem chefDeskIngredientItem = _spawnedIngredientItems[i];
				if (chefDeskIngredientItem != null)
				{
					chefDeskIngredientItem.RefreshAvailability();
				}
			}
		}

		private RestaurantManager GetRestaurantManager()
		{
			if (!(Singleton<GameProgressManager>.Instance != null))
			{
				return null;
			}
			return Singleton<GameProgressManager>.Instance.RestaurantManager;
		}

		private void SubscribeToCustomRecipeCatalog()
		{
			RestaurantManager restaurantManager = GetRestaurantManager();
			if (!(restaurantManager == null))
			{
				restaurantManager.CustomSushiRecipeCatalogChanged -= OnCustomRecipeCatalogChanged;
				restaurantManager.CustomSushiRecipeCatalogChanged += OnCustomRecipeCatalogChanged;
			}
		}

		private void UnsubscribeFromCustomRecipeCatalog()
		{
			RestaurantManager restaurantManager = GetRestaurantManager();
			if (restaurantManager != null)
			{
				restaurantManager.CustomSushiRecipeCatalogChanged -= OnCustomRecipeCatalogChanged;
			}
		}

		private void OnCustomRecipeCatalogChanged()
		{
			if (base.isActiveAndEnabled)
			{
				LoadSushiRecipes();
				BuildSushiList();
			}
		}

		private Sprite ResolveCustomRecipeIcon(SushiType sushiType)
		{
			return sushiType switch
			{
				SushiType.Maki => _customMakiIcon, 
				SushiType.Uramaki => _customUramakiIcon, 
				SushiType.Temaki => _customTemakiIcon, 
				_ => null, 
			};
		}

		private static List<BaseIngredientNeed> GetSortedNeeds(SushiSO sushiSO)
		{
			List<BaseIngredientNeed> list = new List<BaseIngredientNeed>();
			if (sushiSO.Recipe.Ingredients != null)
			{
				list.AddRange(sushiSO.Recipe.Ingredients);
			}
			if (sushiSO.Recipe.ProcessedIngredients != null)
			{
				list.AddRange(sushiSO.Recipe.ProcessedIngredients);
			}
			return list.OrderBy((BaseIngredientNeed need) => need.Order).ToList();
		}
	}
}
