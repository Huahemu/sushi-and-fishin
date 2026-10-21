using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "BeverageSO", menuName = "Data/ServedDishes/BeverageSO")]
	public class BeverageSO : ServedDishSO
	{
		public BeverageType BeverageType;
	}
}
