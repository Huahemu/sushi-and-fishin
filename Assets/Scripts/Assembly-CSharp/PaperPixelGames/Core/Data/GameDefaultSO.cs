using PaperPixelGames.Core.SaveSystem;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "GameDefault", menuName = "Data/GameDefault")]
	public class GameDefaultSO : ScriptableObject
	{
		public RestaurantData RestaurantData;

		public GameProgressData GameProgressData;
	}
}
