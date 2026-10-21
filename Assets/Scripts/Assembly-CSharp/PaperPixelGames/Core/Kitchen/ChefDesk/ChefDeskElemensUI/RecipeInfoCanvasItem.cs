using PaperPixelGames.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Kitchen.ChefDesk.ChefDeskElemensUI
{
	public class RecipeInfoCanvasItem : MonoBehaviour
	{
		[SerializeField]
		private TextMeshProUGUI _ingredientName;

		[SerializeField]
		private TextMeshProUGUI _countText;

		[SerializeField]
		private Image _ingredientAvailable;

		private int _required;

		private int _placed;

		private bool _quantityOnly;

		public string IngredientID { get; private set; }

		public int RequiredAmount => _required;

		public int PlacedAmount => _placed;

		public void ClientInit(BaseIngredientNeed baseIngredientNeed)
		{
			if (baseIngredientNeed != null)
			{
				IngredientID = baseIngredientNeed.IngredientId;
				_ingredientName.text = baseIngredientNeed.LocalizedIngredientName;
				_required = Mathf.Max(0, baseIngredientNeed.RequiredAmount);
				_placed = 0;
				_quantityOnly = false;
				RefreshCountText();
				UpdateIngredientStatusUI(iconAvailable: false);
			}
		}

		public void ClientInit(string ingredientId, string localizedName, int requiredAmount)
		{
			IngredientID = ingredientId ?? string.Empty;
			_ingredientName.text = (string.IsNullOrWhiteSpace(localizedName) ? IngredientID : localizedName);
			_required = Mathf.Max(0, requiredAmount);
			_placed = 0;
			_quantityOnly = false;
			RefreshCountText();
			UpdateIngredientStatusUI(iconAvailable: false);
		}

		public void SetQuantityOnly(int quantity)
		{
			_quantityOnly = true;
			_required = Mathf.Max(0, quantity);
			_placed = _required;
			RefreshCountText();
			UpdateIngredientStatusUI(iconAvailable: false);
		}

		public void SetRequired(int required)
		{
			_required = Mathf.Max(0, required);
			if (_placed > _required)
			{
				_placed = _required;
			}
			RefreshCountText();
		}

		public void SetPlaced(int placed)
		{
			_placed = Mathf.Clamp(placed, 0, _required);
			RefreshCountText();
		}

		public void UpdateIngredientStatusUI(bool iconAvailable)
		{
			_ingredientAvailable.enabled = !_quantityOnly & iconAvailable;
		}

		private void RefreshCountText()
		{
			_countText.text = (_quantityOnly ? $"x{_placed}" : $"{_placed}/{_required}");
		}
	}
}
