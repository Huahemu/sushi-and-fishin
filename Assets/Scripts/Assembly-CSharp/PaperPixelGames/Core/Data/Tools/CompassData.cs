using System.Collections.Generic;
using UnityEngine;

namespace PaperPixelGames.Core.Data.Tools
{
	[CreateAssetMenu(fileName = "CompassData", menuName = "Data/PlayerTools/Compass")]
	public class CompassData : BaseToolData, IWheelSlice
	{
		public List<CompassLevelData> Levels;

		public ItemOffset Level0_OffsetFps;

		public ItemOffset Level0_OffsetTps;
	}
}
