using PaperPixelGames.Core.Kitchen.Yaki3000Machine;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "Yaki3000IngredientSO", menuName = "Data/ServedDishes/Yaki3000IngredientSO")]
	public class Yaki3000IngredientSO : ServedDishSO
	{
		public Yaki3000Product Yaki3000ProductType;
	}
}
