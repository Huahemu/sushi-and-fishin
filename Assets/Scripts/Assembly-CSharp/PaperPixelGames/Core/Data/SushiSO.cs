using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "SushiSO", menuName = "Data/ServedDishes/SushiSO")]
	public class SushiSO : ServedDishSO
	{
		public SushiType SushiType;

		public int SushiLevel;

		public int SushiExp;

		[SerializeField]
		private GameObject _singleSushiPrefab;

		public GameObject SingleSushiPrefab => _singleSushiPrefab;
	}
}
