using System;
using PaperPixelGames.Core.Data;

namespace PaperPixelGames.Core.SaveSystem
{
	[Serializable]
	public class ServedDishData
	{
		public string Id;

		public TransformData TransformData = new TransformData();

		public bool HasTrayAttachment;

		public int TrayTrolleyIndex = -1;

		public int TraySlotIndex = -1;

		public byte DishSlotIndex;

		public bool HasConveyorAttachment;

		public int ConveyorSlotIndex = -1;

		public CustomSushiPhysicalData CustomSushi;
	}
}
