using PaperPixelGames.Core.Progression;
using UnityEngine;

namespace PaperPixelGames.Core.Data.Tools
{
	[CreateAssetMenu(fileName = "BaseLevelData", menuName = "Data/PlayerTools/BaseLevelData")]
	public class BaseLevelData : ScriptableObject
	{
		public string Name;

		public Sprite Sprite;

		public int Level;

		public int MoneyCost;

		public int ScrapCost;

		public int RequiredRestaurantLevel;

		public ProgressionUnlockKey LevelCondition;

		public GameObject GraphicPrefab;
	}
}
