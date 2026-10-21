using System.Collections.Generic;
using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "Ingredient", menuName = "Data/IngredientSO")]
	public class IngredientSO : HoldableSO
	{
		private enum RawIngredientAutoFillCategory
		{
			Grocery = 0,
			Seafood = 1
		}

		public IngredientType IngredientType;

		public List<ProcessedIngredientSO> ProcessedIngredients;

		public bool PlacablePrepStation;

		public GameObject PrepStationCuttingPrefab;

		[SerializeField]
		private RawIngredientAutoFillCategory _autoFillCategory;
	}
}
