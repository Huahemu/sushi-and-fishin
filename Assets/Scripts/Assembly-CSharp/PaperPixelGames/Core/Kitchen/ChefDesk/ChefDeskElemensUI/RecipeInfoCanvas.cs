using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using PaperPixelGames.Core.Data;
using PaperPixelGames.Core.Interaction;
using PaperPixelGames.Framework.UI;
using TMPro;
using Unity.Collections;
using UnityEngine;

namespace PaperPixelGames.Core.Kitchen.ChefDesk.ChefDeskElemensUI
{
	public class RecipeInfoCanvas : MonoBehaviour
	{
		[SerializeField]
		private RectTransform _recipeInfoCardRect;

		[SerializeField]
		private RecipeInfoCanvasItem _recipeInfoCardItemPrefab;

		[SerializeField]
		private Transform _contentTransform;

		[SerializeField]
		private BaseButton _hideButton;

		[SerializeField]
		private BaseButton _showButton;

		[SerializeField]
		private TextMeshProUGUI _sushiText;

		[Space]
		[SerializeField]
		private float _hiddenY = -340f;

		[SerializeField]
		private float _shownY;

		[SerializeField]
		private float _duration = 0.3f;

		private readonly List<RecipeInfoCanvasItem> _spawnedItems = new List<RecipeInfoCanvasItem>();

		private readonly Dictionary<string, int> _requiredById = new Dictionary<string, int>();

		private readonly Dictionary<string, int> _placedById = new Dictionary<string, int>();

		private readonly Dictionary<string, RecipeInfoCanvasItem> _uiById = new Dictionary<string, RecipeInfoCanvasItem>();

		private Tween _moveTween;

		private void Awake()
		{
			_hideButton.Clicked -= OnClickHideButton;
			_showButton.Clicked -= OnClickShowButton;
			_hideButton.Clicked += OnClickHideButton;
			_showButton.Clicked += OnClickShowButton;
		}

		private void OnDestroy()
		{
			_hideButton.Clicked -= OnClickHideButton;
			_showButton.Clicked -= OnClickShowButton;
			_moveTween?.Kill();
			_moveTween = null;
		}

		public void ClientPrepare(SushiSO sushiSO)
		{
			if (sushiSO == null)
			{
				return;
			}
			base.gameObject.SetActive(value: true);
			ClearItems();
			ClearRuntimeData();
			_sushiText.text = sushiSO.LocalizedName;
			foreach (BaseIngredientNeed sortedNeed in GetSortedNeeds(sushiSO))
			{
				string ingredientId = sortedNeed.IngredientId;
				if (!string.IsNullOrWhiteSpace(ingredientId))
				{
					if (_requiredById.ContainsKey(ingredientId))
					{
						_requiredById[ingredientId] += sortedNeed.RequiredAmount;
						RecipeInfoCanvasItem recipeInfoCanvasItem = _uiById[ingredientId];
						recipeInfoCanvasItem.SetRequired(_requiredById[ingredientId]);
						recipeInfoCanvasItem.SetPlaced(_placedById[ingredientId]);
						recipeInfoCanvasItem.UpdateIngredientStatusUI(_placedById[ingredientId] >= _requiredById[ingredientId]);
					}
					else
					{
						_requiredById.Add(ingredientId, sortedNeed.RequiredAmount);
						_placedById.Add(ingredientId, 0);
						RecipeInfoCanvasItem recipeInfoCanvasItem2 = UnityEngine.Object.Instantiate(_recipeInfoCardItemPrefab, _contentTransform);
						recipeInfoCanvasItem2.ClientInit(sortedNeed);
						recipeInfoCanvasItem2.SetRequired(sortedNeed.RequiredAmount);
						recipeInfoCanvasItem2.SetPlaced(0);
						recipeInfoCanvasItem2.UpdateIngredientStatusUI(iconAvailable: false);
						_uiById.Add(ingredientId, recipeInfoCanvasItem2);
						_spawnedItems.Add(recipeInfoCanvasItem2);
					}
				}
			}
			MoveTo(_shownY);
		}

		public void ClientPrepare(SushiRecipeDefinition recipe, ItemDatabaseSO itemDatabase)
		{
			if (recipe?.Composition?.IngredientIds == null)
			{
				return;
			}
			if (recipe.BuiltInSushi != null)
			{
				ClientPrepare(recipe.BuiltInSushi);
				return;
			}
			base.gameObject.SetActive(value: true);
			ClearItems();
			ClearRuntimeData();
			_sushiText.text = recipe.DisplayName;
			foreach (string ingredientId in recipe.Composition.IngredientIds)
			{
				if (!string.IsNullOrWhiteSpace(ingredientId))
				{
					if (_requiredById.TryGetValue(ingredientId, out var value))
					{
						value++;
						_requiredById[ingredientId] = value;
						_uiById[ingredientId].SetRequired(value);
						continue;
					}
					ItemSO itemSO = itemDatabase?.FindById(ingredientId);
					RecipeInfoCanvasItem recipeInfoCanvasItem = UnityEngine.Object.Instantiate(_recipeInfoCardItemPrefab, _contentTransform);
					recipeInfoCanvasItem.ClientInit(ingredientId, itemSO?.LocalizedName, 1);
					_requiredById.Add(ingredientId, 1);
					_placedById.Add(ingredientId, 0);
					_uiById.Add(ingredientId, recipeInfoCanvasItem);
					_spawnedItems.Add(recipeInfoCanvasItem);
				}
			}
			MoveTo(_shownY);
		}

		public void ClientPrepareCustom(SushiType sushiType, IReadOnlyList<string> placedIngredientIds, string noriId, string riceId, ItemDatabaseSO itemDatabase, int maxFillingPlacements)
		{
			if (!CustomSushiRules.TryGetRequiredBase(sushiType, noriId, riceId, out var firstIngredientId, out var secondIngredientId))
			{
				return;
			}
			base.gameObject.SetActive(value: true);
			ClearItems();
			ClearRuntimeData();
			int num = placedIngredientIds?.Count ?? 0;
			int num2 = Mathf.Clamp(num - 2, 0, Mathf.Max(0, maxFillingPlacements));
			_sushiText.text = $"{sushiType}  •  {num2}/{maxFillingPlacements}";
			AddCustomIngredient(firstIngredientId, 1, CountPlacements(placedIngredientIds, firstIngredientId), itemDatabase);
			AddCustomIngredient(secondIngredientId, 1, CountPlacements(placedIngredientIds, secondIngredientId), itemDatabase);
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			List<string> list = new List<string>();
			for (int i = 2; i < num; i++)
			{
				string text = placedIngredientIds[i];
				if (!string.IsNullOrWhiteSpace(text))
				{
					if (dictionary.TryGetValue(text, out var value))
					{
						dictionary[text] = value + 1;
						continue;
					}
					dictionary.Add(text, 1);
					list.Add(text);
				}
			}
			foreach (string item in list)
			{
				int num3 = dictionary[item];
				AddCustomIngredient(item, num3, num3, itemDatabase, quantityOnly: true);
			}
			MoveTo(_shownY);
		}

		public void ClientIngredientItemOnTablePlaced(FixedString64Bytes ingredientID)
		{
			string key = ingredientID.ToString();
			if (_uiById.TryGetValue(key, out var value) && _requiredById.TryGetValue(key, out var value2))
			{
				int value3 = ((!_placedById.TryGetValue(key, out var value4)) ? 1 : (value4 + 1));
				value3 = Mathf.Clamp(value3, 0, value2);
				_placedById[key] = value3;
				value.SetPlaced(value3);
				value.UpdateIngredientStatusUI(value3 >= value2);
			}
		}

		public void ClientIngredientItemOnTableRemoved(FixedString64Bytes ingredientID)
		{
			string key = ingredientID.ToString();
			if (_uiById.TryGetValue(key, out var value) && _requiredById.TryGetValue(key, out var value2))
			{
				int num = Mathf.Max(0, _placedById[key] - 1);
				_placedById[key] = num;
				value.SetPlaced(num);
				value.UpdateIngredientStatusUI(num >= value2);
			}
		}

		public void ClientResetPlacedProgress(bool resetAvailabilityIcon = true, bool resetNextTexts = false)
		{
			foreach (string item in _placedById.Keys.ToList())
			{
				_placedById[item] = 0;
			}
			foreach (KeyValuePair<string, RecipeInfoCanvasItem> item2 in _uiById)
			{
				RecipeInfoCanvasItem value = item2.Value;
				if (!(value == null))
				{
					value.SetPlaced(0);
					if (resetAvailabilityIcon)
					{
						value.UpdateIngredientStatusUI(iconAvailable: false);
					}
				}
			}
		}

		public void Hide()
		{
			MoveTo(_hiddenY);
		}

		public void Show()
		{
			MoveTo(_shownY);
		}

		private void OnClickHideButton()
		{
			Hide();
		}

		private void OnClickShowButton()
		{
			Show();
		}

		private void MoveTo(float targetY)
		{
			_moveTween?.Kill();
			_hideButton.interactable = false;
			_showButton.interactable = false;
			_moveTween = _recipeInfoCardRect.DOAnchorPosY(targetY, _duration).SetEase(Ease.OutCubic).OnComplete(() =>
			{
				bool flag = Mathf.Approximately(targetY, _shownY);
				_hideButton.interactable = true;
				_showButton.interactable = true;
				_hideButton.gameObject.SetActive(flag);
				_showButton.gameObject.SetActive(!flag);
			});
		}

		private void ClearItems()
		{
			foreach (RecipeInfoCanvasItem spawnedItem in _spawnedItems)
			{
				if (spawnedItem != null)
				{
					UnityEngine.Object.Destroy(spawnedItem.gameObject);
				}
			}
			_spawnedItems.Clear();
		}

		private void ClearRuntimeData()
		{
			_requiredById.Clear();
			_placedById.Clear();
			_uiById.Clear();
		}

		private void AddCustomIngredient(string ingredientId, int required, int placed, ItemDatabaseSO itemDatabase, bool quantityOnly = false)
		{
			if (!string.IsNullOrWhiteSpace(ingredientId))
			{
				ItemSO itemSO = itemDatabase?.FindById(ingredientId);
				RecipeInfoCanvasItem recipeInfoCanvasItem = UnityEngine.Object.Instantiate(_recipeInfoCardItemPrefab, _contentTransform);
				recipeInfoCanvasItem.ClientInit(ingredientId, itemSO?.LocalizedName, required);
				recipeInfoCanvasItem.SetPlaced(placed);
				if (quantityOnly)
				{
					recipeInfoCanvasItem.SetQuantityOnly(placed);
				}
				recipeInfoCanvasItem.UpdateIngredientStatusUI(placed >= required);
				_requiredById.Add(ingredientId, required);
				_placedById.Add(ingredientId, Mathf.Clamp(placed, 0, required));
				_uiById.Add(ingredientId, recipeInfoCanvasItem);
				_spawnedItems.Add(recipeInfoCanvasItem);
			}
		}

		private static int CountPlacements(IReadOnlyList<string> ingredientIds, string targetId)
		{
			if (ingredientIds == null || string.IsNullOrWhiteSpace(targetId))
			{
				return 0;
			}
			int num = 0;
			for (int i = 0; i < ingredientIds.Count; i++)
			{
				if (string.Equals(ingredientIds[i], targetId, StringComparison.Ordinal))
				{
					num++;
				}
			}
			return num;
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
