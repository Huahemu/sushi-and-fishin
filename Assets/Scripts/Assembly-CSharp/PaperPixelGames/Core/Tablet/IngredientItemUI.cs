using PaperPixelGames.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Tablet
{
	public class IngredientItemUI : MonoBehaviour
	{
		[SerializeField]
		private TextMeshProUGUI _ingredientName;

		[SerializeField]
		private Image _ingredientIcon;

		[SerializeField]
		private TextMeshProUGUI _ingredientAmount;

		private int _requiredIngredientAmount;

		private int _ownedIngredientAmount;

		public void Initialize(BaseIngredientNeed ingredient)
		{
			_requiredIngredientAmount = ingredient.RequiredAmount;
			_ingredientName.text = (string.IsNullOrWhiteSpace(ingredient.LocalizedIngredientName) ? ingredient.IngredientName : ingredient.LocalizedIngredientName);
			Sprite sprite = ((ingredient is ProcessedIngredientNeeds processedIngredientNeeds) ? processedIngredientNeeds.Data.Sprite : ((!(ingredient is IngredientNeeds ingredientNeeds)) ? null : ingredientNeeds.Data.Sprite));
			Sprite sprite2 = sprite;
			if (sprite2 != null)
			{
				_ingredientIcon.sprite = sprite2;
			}
			UpdateIngredientAmount(_ownedIngredientAmount);
		}

		public void Initialize(ItemSO ingredient, int requiredAmount)
		{
			_requiredIngredientAmount = Mathf.Max(0, requiredAmount);
			_ingredientName.text = ((ingredient != null) ? ingredient.LocalizedName : string.Empty);
			if (_ingredientIcon != null)
			{
				_ingredientIcon.sprite = ingredient?.Sprite;
				_ingredientIcon.enabled = ingredient?.Sprite != null;
			}
			UpdateIngredientAmount(_ownedIngredientAmount);
		}

		public void UpdateIngredientAmount(int ownedAmount)
		{
			_ownedIngredientAmount = ownedAmount;
			_ingredientAmount.text = $"{_ownedIngredientAmount}/{_requiredIngredientAmount}";
			_ingredientAmount.color = ((_ownedIngredientAmount < _requiredIngredientAmount) ? Color.red : Color.black);
		}
	}
}
