using PaperPixelGames.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PaperPixelGames.Core.Kitchen.Yaki3000Machine.UI
{
	public class Yaki3000RecipeUI_Item : MonoBehaviour
	{
		[SerializeField]
		private Image _recipeImage;

		[SerializeField]
		private TextMeshProUGUI _recipeNameText;

		[SerializeField]
		private TextMeshProUGUI _amountText;

		public void Initiazlie(BaseIngredientNeed need)
		{
			_recipeNameText.text = need.IngredientName + " " + need.RequiredAmount + "x";
			_recipeImage.sprite = ResolveIcon(need);
		}

		private Sprite ResolveIcon(BaseIngredientNeed need)
		{
			if (need is IngredientNeeds ingredientNeeds)
			{
				return ingredientNeeds.Data.Sprite;
			}
			if (need is ProcessedIngredientNeeds processedIngredientNeeds)
			{
				return processedIngredientNeeds.Data.Sprite;
			}
			return null;
		}
	}
}
