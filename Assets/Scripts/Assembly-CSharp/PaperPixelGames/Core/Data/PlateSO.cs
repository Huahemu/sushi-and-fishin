using PaperPixelGames.Core.Interaction;
using UnityEngine;

namespace PaperPixelGames.Core.Data
{
	[CreateAssetMenu(fileName = "Plate", menuName = "Data/PlateSO")]
	public class PlateSO : HoldableSO
	{
		public PlateType PlateType;

		public ServedDishType ServedDishType;
	}
}
